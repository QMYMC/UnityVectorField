Shader "VectorField/StreamlineRender"
{
    Properties
    {
        _ColorRamp       ("Color Ramp",           2D)         = "white" {}
        _MinSpeed        ("Min Speed Ref",         Float)      = 0.0
        _MaxSpeed        ("Max Speed Ref",         Float)      = 20.0
        _TrailLen        ("Trail Length (steps)",  Int)        = 100
        _LineWidth       ("Line Width",            Float)      = 0.5
        _StreamlineAlpha ("Alpha",                 Range(0,1)) = 0.6
        _HeadBrightness  ("Head Brightness (HDR)", Float)      = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5
            #include "UnityCG.cginc"

            struct StreamlinePoint
            {
                float3 position;
                float  speed;
            };

            struct StreamlineHead
            {
                int   startIndex;
                int   pointCount;
                float headT;
            };

            StructuredBuffer<StreamlinePoint> _PointBuffer;
            StructuredBuffer<StreamlineHead>  _HeadBuffer;

            sampler2D _ColorRamp;
            float _MinSpeed;
            float _MaxSpeed;
            int   _TrailLen;
            float _LineWidth;
            float _StreamlineAlpha;
            float _HeadBrightness;
            float _ColorSpeedUnitMultiplier;
            float4x4 _FieldLocalToWorld;
            float4x4 _FieldWorldToLocal;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o;
                o.pos   = float4(2, 2, 2, 1); // 默认推到裁剪外
                o.color = float4(0, 0, 0, 0);

                uint segIndex  = vertexID / 6;
                uint lineIndex = segIndex / (uint)_TrailLen;
                uint step      = segIndex % (uint)_TrailLen;

                StreamlineHead head = _HeadBuffer[lineIndex];
                if (head.pointCount <= 1) return o;

                // 每条轨迹只显示 min(trailLength, pointCount) 段，避免短路径重复绕圈
                if (step >= (uint)head.pointCount) return o;

                // 当前段的两个端点（headT 处向前回溯 step 步）
                int headIdx = (int)head.headT;
                // 双取模保证负数情况下结果仍正确
                int localIdx0 = ((headIdx - (int)step)     % head.pointCount + head.pointCount) % head.pointCount;
                int localIdx1 = ((headIdx - (int)step - 1) % head.pointCount + head.pointCount) % head.pointCount;

                // 丢弃接缝片段：开放路径终点→起点不相连，否则产生穿越全场的长线
                if (abs(localIdx0 - localIdx1) != 1) return o;

                int idx0 = head.startIndex + localIdx0;
                int idx1 = head.startIndex + localIdx1;

                float3 p0 = _PointBuffer[idx0].position;
                float3 p1 = _PointBuffer[idx1].position;

                float3 segDir = p1 - p0;
                if (dot(segDir, segDir) < 0.0001) return o;

                // 计算屏幕空间垂直方向（避免线段与视线平行时 cross 结果为零，产生 NaN 黑点）
                float4 clipP0 = mul(UNITY_MATRIX_VP, mul(_FieldLocalToWorld, float4(p0, 1.0)));
                float4 clipP1 = mul(UNITY_MATRIX_VP, mul(_FieldLocalToWorld, float4(p1, 1.0)));
                float2 screenDir = clipP1.xy * clipP0.w - clipP0.xy * clipP1.w; // 避免除以 w
                float screenLen  = dot(screenDir, screenDir);
                if (screenLen < 1e-8) return o; // 两点在屏幕上完全重叠，丢弃
                screenDir = normalize(screenDir);
                float2 screenRight = float2(-screenDir.y, screenDir.x);

                // 6 顶点构成两个三角形的 Quad（p0 侧两顶点，p1 侧两顶点）
                // corner: 0=p0-left, 1=p0-right, 2=p1-left, 3=p1-right
                // tri: 0,2,1 | 1,2,3
                int cornerMap[6] = { 0, 2, 1, 1, 2, 3 };
                int corner = cornerMap[vertexID % 6];

                // 在裁剪空间中横向偏移（单位与 _LineWidth 一致，屏幕像素无关）
                float4 clipBase = (corner >= 2) ? clipP1 : clipP0;
                float  sideSign = (corner % 2 == 0) ? -0.5 : 0.5;
                // 将世界单位宽度转换为裁剪空间宽度（除以 w 近似 NDC 偏移，再乘回 w）
                float3 localCamera = mul(_FieldWorldToLocal, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 forward = normalize(segDir);
                float3 view = normalize(localCamera - p0);
                float3 reference = abs(dot(view, forward)) > .99 ? (abs(forward.y) < .9 ? float3(0,1,0) : float3(1,0,0)) : view;
                float3 right = normalize(cross(reference, forward));
                float3 localVertex = (corner >= 2 ? p1 : p0) + right * sideSign * _LineWidth;
                clipBase = mul(UNITY_MATRIX_VP, mul(_FieldLocalToWorld, float4(localVertex, 1)));

                o.pos = clipBase;

                // 颜色与淡出
                float speed = _PointBuffer[idx0].speed;
                float t     = saturate((speed * _ColorSpeedUnitMultiplier - _MinSpeed) / max(_MaxSpeed - _MinSpeed, 0.001));
                float4 rampColor = tex2Dlod(_ColorRamp, float4(t, 0.5, 0, 0));

                // 尾部使用幂函数渐隐。
                // 额外乘以 1.2 后 saturate：使尾端最后约 17% 的段完全归零
                float stepNorm = (float)step / max((float)_TrailLen - 1.0, 1.0);
                float tailFade = pow(saturate((1.0 - stepNorm) * 1.2), 1.5);

                // 头部 HDR 超亮（step==0）
                float headGlow = (step == 0) ? _HeadBrightness : 1.0;

                // 低速（暗色）段单独压暗 alpha：Jet 颜色最暗端亮度≈0.05，让它提前消失
                float lum = dot(rampColor.rgb, float3(0.299, 0.587, 0.114));
                float brightFade = saturate((lum - 0.04) / 0.18); // 亮度 <0.04 → 0，>0.22 → 1

                o.color = float4(rampColor.rgb * headGlow,
                                 tailFade * _StreamlineAlpha * rampColor.a);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 丢弃极低 alpha 片元，防止暗色尾端在 Bloom 高对比下积累成黑点
                clip(i.color.a - 0.02);
                return i.color;
            }
            ENDCG
        }
    }
}

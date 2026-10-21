Shader "Scientific/WindParticleRender"
{
    Properties
    {
        _ColorRamp         ("Color Ramp",           2D)           = "white" {}
        _MinSpeed          ("Min Speed Ref",         Float)        = 0.0
        _MaxSpeed          ("Max Speed Ref",         Float)        = 20.0
        _Scale             ("Base Scale",            Float)        = 0.1
        _BaseAspect        ("Base Aspect",           Float)        = 2.0
        _StretchMult       ("Stretch Multiplier",    Float)        = 0.4
        _CoreBrightness    ("Core Brightness (HDR)", Float)        = 4.0
        _NearFadeDistance  ("Near Fade Dist",        Float)        = 3.0
        _MinVisibleSpeed   ("Min Visible Speed",     Float)        = 0.3
        _AltFogStrength    ("Altitude Fog Strength", Range(0,1))   = 0.65
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        // 预乘 Alpha：避免密集处过曝，HDR 亮核仍可触发 Bloom
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5
            #include "UnityCG.cginc"

            struct Particle
            {
                float3 position;
                float3 velocity;
                float  physicalSpeed;
                float  life;
                float  seed;
                float  stretch;
            };

            StructuredBuffer<Particle> particles;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                // uv.x = 0(尾) → 1(头)  uv.y = -0.5 → +0.5 横向
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _ColorRamp;
            float _MinSpeed;
            float _MaxSpeed;
            float _Scale;
            float _BaseAspect;
            float _StretchMult;
            float _CoreBrightness;
            float _NearFadeDistance;
            float _MinVisibleSpeed;
            float _AltFogStrength;
            float3 _BoundMin;
            float3 _BoundSize;
            float4x4 _FieldLocalToWorld;
            float4x4 _FieldWorldToLocal;
            float _ColorSpeedUnitMultiplier;
            float _Opacity;

            v2f vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                Particle p = particles[instanceID];

                float worldSpeed = length(p.velocity);
                float speed = p.physicalSpeed;

                // 剔除无效粒子（推到裁剪空间外）
                if (p.life <= 0 || worldSpeed < 1e-9 || speed < 1e-6)
                {
                    o.pos = float4(2, 2, 2, 1);
                    o.uv = 0; o.color = 0;
                    return o;
                }

                // ---- 颜色 ----
                float t = saturate((speed * _ColorSpeedUnitMultiplier - _MinSpeed) / max(_MaxSpeed - _MinSpeed, 0.001));
                float4 rampColor = tex2Dlod(_ColorRamp, float4(t, 0.5, 0, 0));

                // ---- Billboard 朝向 ----
                float3 localCamera = mul(_FieldWorldToLocal, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 viewDir    = normalize(localCamera - p.position);
                float3 forwardDir = normalize(p.velocity);
                float3 refAxis    = abs(dot(viewDir, forwardDir)) > 0.99 ? (abs(forwardDir.y) < .9 ? float3(0,1,0) : float3(1,0,0)) : viewDir;
                float3 right      = normalize(cross(refAxis, forwardDir));

                // ---- 尺寸计算 ----
                float speedNorm  = t;
                float baseScale  = _Scale * lerp(0.4, 1.0, speedNorm);
                float dynAspect  = clamp(_BaseAspect + p.stretch * _StretchMult, 1.5, 12.0);
                float halfLen    = baseScale * dynAspect * 0.5;
                float halfWidth  = baseScale * 0.5;

                // ---- Quad 顶点布局 ----
                // corner 0: tail-left   corner 1: tail-right
                // corner 2: head-left   corner 3: head-right
                // uv.x=0 at tail, uv.x=1 at head
                // Tri1: 0,2,1   Tri2: 1,2,3
                int cornerMap[6] = { 0, 2, 1, 1, 2, 3 };
                int corner = cornerMap[vertexID];

                float3 basePos  = (corner >= 2) ? (p.position + forwardDir * halfLen)
                                                : (p.position - forwardDir * halfLen);
                float  sideSign = (corner % 2 == 0) ? -1.0 : 1.0;
                float3 worldPos = basePos + right * sideSign * halfWidth;

                o.pos = mul(UNITY_MATRIX_VP, mul(_FieldLocalToWorld, float4(worldPos, 1.0)));
                o.uv  = float2(corner >= 2 ? 1.0 : 0.0,
                               sideSign * 0.5);

                // ---- Alpha 合成 ----
                float lifeFade  = smoothstep(0.0, 0.2, p.life) * smoothstep(1.0, 0.8, p.life);
                float distToCam = distance(localCamera, p.position);
                float nearFade  = smoothstep(0.5, _NearFadeDistance, distToCam);
                float speedFade = _MinVisibleSpeed > 0 ? smoothstep(_MinVisibleSpeed * 0.5, _MinVisibleSpeed, speed) : 1;

                // 高度雾化：高空粒子变暗增强 3D 深度感
                float altNorm = saturate((p.position.y - _BoundMin.y) / max(_BoundSize.y, 0.001));
                float altFade = lerp(1.0, 1.0 - _AltFogStrength, altNorm * altNorm);

                float baseAlpha = lifeFade * nearFade * speedFade * altFade * rampColor.a * saturate(_Opacity);

                o.color = float4(rampColor.rgb, baseAlpha);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // uv.x = 0(尾) → 1(头)，uv.y = -0.5 → +0.5
                float alongAxis = i.uv.x;
                float lateral   = i.uv.y;

                // ---- 泪滴 SDF ----
                // 半径从尾(0)到头(0.45)线性增大
                float headRadius = alongAxis * 0.45;
                float dist = abs(lateral) / max(headRadius, 0.001);
                float shape = smoothstep(1.0, 0.4, dist);

                // 完全在形状外则丢弃（性能优化）
                clip(shape - 0.01);

                // HDR 核心亮度可配合后处理 Bloom。
                // 头部中心处亮度超过 1.0
                float coreX = pow(alongAxis, 2.0);
                float coreY = max(0.0, 1.0 - abs(lateral) * 3.0);
                float core  = coreX * coreY * _CoreBrightness;

                float alpha = shape * i.color.a;
                float3 rgb  = i.color.rgb * (1.0 + core);

                // 预乘 Alpha 输出
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}

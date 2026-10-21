Shader "Scientific/WindParticleBackground"
{
    Properties
    {
        _ColorRamp        ("Color Ramp",    2D)    = "white" {}
        _MinSpeed         ("Min Speed Ref", Float) = 0.0
        _MaxSpeed         ("Max Speed Ref", Float) = 20.0
        _NearFadeDistance ("Near Fade Dist",Float) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        // 纯叠加：极低 Alpha 下与场景柔和叠加，营造体积感
        Blend SrcAlpha One
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
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _ColorRamp;
            float _MinSpeed;
            float _MaxSpeed;
            float _NearFadeDistance;

            v2f vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                Particle p = particles[instanceID];
                float worldSpeed = length(p.velocity);
                float speed = p.physicalSpeed;

                if (p.life <= 0 || worldSpeed < 0.000001 || speed < 0.001)
                {
                    o.pos = float4(2, 2, 2, 1);
                    o.uv = 0; o.color = 0;
                    return o;
                }

                // 颜色
                float t = saturate((speed - _MinSpeed) / max(_MaxSpeed - _MinSpeed, 0.001));
                float4 rampColor = tex2Dlod(_ColorRamp, float4(t, 0.5, 0, 0));

                // 极小正方形 Billboard（0.06 单位固定大小）
                float3 viewRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 viewUp    = normalize(UNITY_MATRIX_V[1].xyz);
                float  halfSize  = 0.06;

                float2 corners[4];
                corners[0] = float2(-1, -1);
                corners[1] = float2( 1, -1);
                corners[2] = float2(-1,  1);
                corners[3] = float2( 1,  1);
                int indexMap[6] = { 0, 2, 1, 1, 2, 3 };
                float2 c = corners[indexMap[vertexID]];

                float3 worldPos = p.position
                                + viewRight * c.x * halfSize
                                + viewUp    * c.y * halfSize;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv  = c * 0.5 + 0.5;

                // 极低透明度（0.03 ~ 0.05），仅作体积填充
                float lifeFade  = smoothstep(0.0, 0.2, p.life) * smoothstep(1.0, 0.8, p.life);
                float distToCam = distance(_WorldSpaceCameraPos, p.position);
                float nearFade  = smoothstep(0.5, _NearFadeDistance, distToCam);

                o.color = float4(rampColor.rgb, lifeFade * nearFade * 0.035);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 软圆 SDF
                float2 centered = i.uv - 0.5;
                float dist  = length(centered) * 2.0;
                float alpha = smoothstep(1.0, 0.2, dist) * i.color.a;
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}

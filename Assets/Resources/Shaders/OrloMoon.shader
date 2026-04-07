Shader "Orlo/Moon"
{
    Properties
    {
        _Phase ("Phase", Range(-1,1)) = 0.5
        _NightFactor ("Night Factor", Range(0,1)) = 1
        _GlowSize ("Glow Size", Float) = 2.5
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background+11"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Phase;
                float _NightFactor;
                float _GlowSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float dist = length(p);

                float discRadius = 0.35;
                float discEdge = smoothstep(discRadius, discRadius - 0.02, dist);

                float shadowX = p.x - _Phase * discRadius * 1.4;
                float shadowDist = length(float2(shadowX, p.y));
                float shadow = smoothstep(discRadius * 0.95, discRadius * 0.85, shadowDist);

                float illuminated;
                if (_Phase > 0.0)
                    illuminated = discEdge * (1.0 - shadow * (1.0 - _Phase));
                else
                    illuminated = discEdge * shadow * (1.0 + _Phase);

                if (_Phase > 0.9) illuminated = discEdge;
                if (_Phase < -0.9) illuminated = 0;

                float3 moonColor = float3(0.85, 0.88, 0.95);
                float3 disc = moonColor * illuminated;

                float glowDist = max(0, dist - discRadius * 0.8);
                float glow = exp(-glowDist * glowDist * 15.0) * 0.3;
                float3 glowColor = float3(0.6, 0.65, 0.8) * glow;

                float visibility = lerp(0.15, 1.0, _NightFactor);
                float3 final = (disc + glowColor) * visibility;

                return half4(final, 1.0);
            }
            ENDHLSL
        }
    }
}

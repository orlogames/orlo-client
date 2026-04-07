Shader "Orlo/Stars"
{
    Properties
    {
        _NightFactor ("Night Factor", Range(0,1)) = 1
        _Time2 ("Time", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background+10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NightFactor;
                float _Time2;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 quadUV : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.quadUV = input.uv;
                float twinkle = sin(_Time2 * 1.5 + input.uv2.x * 6.2831) * 0.15 + 0.85;
                float alpha = input.uv2.y * _NightFactor * twinkle;
                output.color = float4(input.color.rgb * alpha, alpha);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.quadUV - 0.5;
                float dist = length(centered) * 2.0;
                float glow = saturate(1.0 - dist * dist);
                glow *= glow;
                return input.color * glow;
            }
            ENDHLSL
        }
    }
}

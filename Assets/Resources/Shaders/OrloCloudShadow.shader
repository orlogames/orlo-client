Shader "Orlo/CloudShadow"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "white" {}
        _UVOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        _Threshold ("Threshold", Float) = 0.42
        _Softness ("Softness", Float) = 0.18
        _Density ("Density", Float) = 0.35
        _ShadowStrength ("Shadow Strength", Float) = 0.3
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend DstColor Zero
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudShadowPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _UVOffset;
                float _Threshold;
                float _Softness;
                float _Density;
                float _ShadowStrength;
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
                float2 uv = input.uv + _UVOffset.xy;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;
                float adjustedThreshold = lerp(0.7, 0.15, _Density);
                float cloudShape = saturate((noise - adjustedThreshold) / max(_Softness, 0.001));
                float shadow = 1.0 - cloudShape * _ShadowStrength;
                return half4(shadow, shadow, shadow, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

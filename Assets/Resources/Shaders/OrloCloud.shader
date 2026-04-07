Shader "Orlo/Clouds"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "white" {}
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _SunDir ("Sun Direction", Vector) = (0, -1, 0, 0)
        _SunColor ("Sun Color", Color) = (1, 0.92, 0.75, 1)
        _UVOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        _Threshold ("Threshold", Float) = 0.42
        _Softness ("Softness", Float) = 0.18
        _Density ("Density", Float) = 0.35
        _Brightness ("Brightness", Float) = 1.1
        _BacklitPower ("Backlit Power", Float) = 0.6
        _CloudAlpha ("Cloud Alpha", Float) = 0.85
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float4 _SunDir;
                float4 _SunColor;
                float4 _UVOffset;
                float _Threshold;
                float _Softness;
                float _Density;
                float _Brightness;
                float _BacklitPower;
                float _CloudAlpha;
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
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.worldPos = vertexInput.positionWS;
                output.viewDir = normalize(vertexInput.positionWS - GetCameraPositionWS());
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv + _UVOffset.xy;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;

                float adjustedThreshold = lerp(0.7, 0.15, _Density);
                float cloudShape = saturate((noise - adjustedThreshold) / max(_Softness, 0.001));

                if (cloudShape < 0.01)
                    discard;

                float3 baseColor = _CloudColor.rgb * _Brightness;

                float3 sunDir = normalize(_SunDir.xyz);
                float backlit = saturate(dot(normalize(input.viewDir), -sunDir));
                backlit = pow(backlit, 3.0) * _BacklitPower;

                float edgeFactor = 1.0 - cloudShape;
                float3 backlitColor = _SunColor.rgb * 1.5;
                float3 finalColor = lerp(baseColor, backlitColor, backlit * edgeFactor);

                finalColor *= lerp(1.0, 0.7, cloudShape * 0.5);

                float sunFacing = saturate(-sunDir.y) * 0.3 + 0.7;
                finalColor *= sunFacing;

                float alpha = cloudShape * _CloudAlpha;
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

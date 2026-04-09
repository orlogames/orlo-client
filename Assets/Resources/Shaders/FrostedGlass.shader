Shader "Orlo/FrostedGlass"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.102, 0.078, 0.031, 0.92)
        _BorderColor ("Border Color", Color) = (0.29, 0.227, 0.125, 1)
        _BlurAmount ("Blur Amount", Range(0, 10)) = 3.0
        _FrostOpacity ("Frost Opacity", Range(0, 1)) = 0.85
        _BorderWidth ("Border Width", Float) = 1.0
        _BorderGlowAlpha ("Border Glow Alpha", Range(0, 0.5)) = 0.2
        _CornerRadius ("Corner Radius", Range(0, 20)) = 8.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
        }

        // Pass 0: Frosted glass background
        // Uses the camera opaque texture as a blurred backdrop
        Pass
        {
            Name "FrostedGlass"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _BorderColor;
                float _BlurAmount;
                float _FrostOpacity;
                float _BorderWidth;
                float _BorderGlowAlpha;
                float _CornerRadius;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            // Simple box blur approximation using bilinear filtering
            half3 BlurSample(float2 uv, float2 texelSize)
            {
                half3 col = half3(0, 0, 0);
                float totalWeight = 0;

                // 9-tap Gaussian-ish kernel
                const int SAMPLES = 9;
                const float2 offsets[SAMPLES] = {
                    float2(-1, -1), float2(0, -1), float2(1, -1),
                    float2(-1,  0), float2(0,  0), float2(1,  0),
                    float2(-1,  1), float2(0,  1), float2(1,  1)
                };
                const float weights[SAMPLES] = {
                    1, 2, 1,
                    2, 4, 2,
                    1, 2, 1
                };

                for (int i = 0; i < SAMPLES; i++)
                {
                    float2 sampleUV = uv + offsets[i] * texelSize * _BlurAmount;
                    col += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, sampleUV).rgb * weights[i];
                    totalWeight += weights[i];
                }

                return col / totalWeight;
            }

            // Rounded rectangle SDF
            float roundedRectSDF(float2 p, float2 size, float radius)
            {
                float2 d = abs(p) - size + radius;
                return length(max(d, 0.0)) - radius;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 0.001);
                float2 texelSize = 1.0 / _ScreenParams.xy;

                // Sample blurred background
                half3 blurred = BlurSample(screenUV, texelSize);

                // Tint the blurred background
                half3 frosted = lerp(blurred, _TintColor.rgb, _FrostOpacity);

                // Rounded rectangle mask
                float2 rectCenter = IN.uv - 0.5;
                float2 rectSize = float2(0.5, 0.5);
                float dist = roundedRectSDF(rectCenter, rectSize, _CornerRadius / max(_ScreenParams.x, 1.0));

                // Smooth edge
                float mask = 1.0 - smoothstep(-0.002, 0.002, dist);

                // Border
                float borderDist = abs(dist) - _BorderWidth / _ScreenParams.x;
                float border = 1.0 - smoothstep(-0.001, 0.001, borderDist);

                half3 finalColor = frosted;
                finalColor = lerp(finalColor, _BorderColor.rgb, border);

                // Border glow
                float glowDist = abs(dist);
                float glow = exp(-glowDist * _ScreenParams.x * 0.5) * _BorderGlowAlpha;
                finalColor += _BorderColor.rgb * glow;

                return half4(finalColor, _TintColor.a * mask * IN.color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

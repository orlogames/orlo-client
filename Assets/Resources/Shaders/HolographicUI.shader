Shader "Orlo/HolographicUI"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _RaceColor ("Race Primary Color", Color) = (1, 0.843, 0, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.878, 0.4, 1)
        _BackgroundColor ("Background Color", Color) = (0.102, 0.078, 0.031, 0.92)

        [Header(Scanlines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.2
        _ScanlineSpeed ("Scanline Speed", Float) = 0.6
        _ScanlineWidth ("Scanline Width", Float) = 2.0

        [Header(Chromatic Aberration)]
        _ChromaticAberration ("Aberration Pixels", Float) = 2.0

        [Header(Noise)]
        _NoiseIntensity ("Noise Intensity", Range(0, 0.5)) = 0.1
        _NoiseScale ("Noise Scale", Float) = 50.0

        [Header(Glow)]
        _GlowMultiplier ("Glow Multiplier", Range(0, 2)) = 0.8
        _FresnelPower ("Fresnel Power", Float) = 3.0

        [Header(Dot Grid)]
        _DotGridSpacing ("Dot Grid Spacing", Float) = 12.0
        _DotGridBrightness ("Dot Grid Brightness", Range(0, 0.3)) = 0.08
        _DotGridRadius ("Dot Radius", Range(0.5, 2)) = 1.0

        [Header(Glitch)]
        _IsGlitching ("Is Glitching", Float) = 0
        _GlitchStrength ("Glitch Strength", Float) = 0.03

        [Header(Alpha)]
        _AlphaFlickerRange ("Alpha Flicker Range", Range(0, 0.1)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "HolographicUI"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

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
                float2 screenUV : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _RaceColor;
                float4 _GlowColor;
                float4 _BackgroundColor;
                float _ScanlineIntensity;
                float _ScanlineSpeed;
                float _ScanlineWidth;
                float _ChromaticAberration;
                float _NoiseIntensity;
                float _NoiseScale;
                float _GlowMultiplier;
                float _FresnelPower;
                float _DotGridSpacing;
                float _DotGridBrightness;
                float _DotGridRadius;
                float _IsGlitching;
                float _GlitchStrength;
                float _AlphaFlickerRange;
            CBUFFER_END

            // ── Noise functions ──
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                // Screen UV for screen-space effects
                float4 screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.screenUV = screenPos.xy / max(screenPos.w, 0.001);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 screenUV = IN.screenUV;
                float time = _Time.y;

                // ── 1. Glitch offset ──
                float2 glitchOffset = float2(0, 0);
                if (_IsGlitching > 0.5)
                {
                    float glitchLine = step(0.95, hash21(float2(floor(uv.y * 20.0), time * 10.0)));
                    glitchOffset.x = glitchLine * _GlitchStrength * (hash21(float2(time, uv.y)) * 2.0 - 1.0);
                }

                // ── 2. Chromatic aberration ──
                float2 aberrationDir = normalize(uv - 0.5) * (_ChromaticAberration / _ScreenParams.x);
                float2 uvR = uv + glitchOffset + aberrationDir;
                float2 uvG = uv + glitchOffset;
                float2 uvB = uv + glitchOffset - aberrationDir;

                float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r;
                float g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG).g;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b;
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG).a;

                half4 col = half4(r, g, b, a) * IN.color;

                // ── 3. Race color tint ──
                col.rgb = lerp(col.rgb, col.rgb * _RaceColor.rgb, 0.3);

                // ── 4. Scanlines ──
                float scanlineY = (screenUV.y * _ScreenParams.y + time * _ScanlineSpeed * 50.0);
                float scanline = 1.0 - _ScanlineIntensity * 0.5 *
                    (1.0 - smoothstep(0.0, _ScanlineWidth, fmod(scanlineY, 3.0)));
                col.rgb *= scanline;

                // ── 5. Noise grain ──
                float noise = valueNoise(screenUV * _NoiseScale + time * 5.0);
                col.rgb += (noise - 0.5) * _NoiseIntensity;

                // ── 6. Dot grid ──
                if (_DotGridSpacing > 0 && _DotGridBrightness > 0)
                {
                    float2 dotUV = fmod(screenUV * _ScreenParams.xy, _DotGridSpacing) / _DotGridSpacing;
                    float dotDist = length(dotUV - 0.5) * _DotGridSpacing;
                    float dot = 1.0 - smoothstep(_DotGridRadius - 0.5, _DotGridRadius + 0.5, dotDist);

                    // Proximity glow: dots near content are brighter
                    float contentBrightness = dot3(col.rgb, float3(0.299, 0.587, 0.114));
                    float dotIntensity = _DotGridBrightness * (0.3 + contentBrightness * 0.7);

                    col.rgb += _RaceColor.rgb * dot * dotIntensity;
                }

                // ── 7. Fresnel rim glow ──
                float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float fresnel = pow(saturate(1.0 - edgeDist * _FresnelPower), 2.0);
                col.rgb += _GlowColor.rgb * fresnel * _GlowMultiplier * 0.3;

                // ── 8. Bloom contribution (brighter elements emit more) ──
                float luminance = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                col.rgb += _GlowColor.rgb * max(luminance - 0.7, 0.0) * _GlowMultiplier;

                // ── 9. Alpha flicker ──
                float flicker = 1.0 - _AlphaFlickerRange * sin(time * 7.3 + screenUV.y * 13.0);
                col.a *= flicker;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

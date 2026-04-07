Shader "Orlo/Foliage"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.15, 0.4, 0.1, 1)
        _BaseMap ("Albedo", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1

        [Header(Wind)]
        _WindSpeed ("Wind Speed", Float) = 1.5
        _WindStrength ("Wind Strength", Float) = 0.15
        _WindFrequency ("Wind Frequency", Float) = 1.2

        [Header(Subsurface)]
        _TranslucencyColor ("Translucency Color", Color) = (0.4, 0.7, 0.2, 1)
        _TranslucencyPower ("Translucency Power", Range(0, 3)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 200
        Cull Off  // Two-sided for leaves

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _Cutoff;
                half _Smoothness;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                half4 _TranslucencyColor;
                half _TranslucencyPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR; // R=trunk sway, G=branch flex, B=leaf flutter
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half   facing      : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Wind animation using vertex colors as weight masks
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WindSpeed;

                // Multi-layer wind displacement
                float trunkSway = input.color.r; // Large slow sway
                float branchFlex = input.color.g; // Medium flex
                float leafFlutter = input.color.b; // Fast small flutter

                float windX = sin(time * 0.7 + worldPos.x * _WindFrequency * 0.3) * trunkSway * _WindStrength * 2.0
                            + sin(time * 1.3 + worldPos.x * _WindFrequency) * branchFlex * _WindStrength
                            + sin(time * 3.7 + worldPos.z * _WindFrequency * 2.0) * leafFlutter * _WindStrength * 0.5;

                float windZ = sin(time * 0.5 + worldPos.z * _WindFrequency * 0.4) * trunkSway * _WindStrength * 1.5
                            + cos(time * 1.7 + worldPos.z * _WindFrequency * 1.2) * branchFlex * _WindStrength * 0.7;

                input.positionOS.x += windX;
                input.positionOS.z += windZ;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.facing = 1.0;

                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                // Sample albedo texture
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = texColor * _BaseColor;

                // Alpha test
                clip(baseColor.a - _Cutoff);

                // Flip normal for backfaces (two-sided foliage)
                float3 normalWS = normalize(input.normalWS) * (facing > 0 ? 1.0 : -1.0);

                // Standard PBR lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.metallic = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Subsurface Scattering approximation — light passing through thin leaves
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 viewDir = inputData.viewDirectionWS;
                float translucency = saturate(dot(-viewDir, mainLight.direction));
                translucency = pow(translucency, 3.0) * _TranslucencyPower;
                color.rgb += _TranslucencyColor.rgb * translucency * mainLight.color * mainLight.shadowAttenuation * 0.5;

                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // Shadow caster (alpha-tested)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _Cutoff;
                half _Smoothness;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                half4 _TranslucencyColor;
                half _TranslucencyPower;
            CBUFFER_END

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Apply same wind as forward pass
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WindSpeed;
                input.positionOS.x += sin(time * 0.7 + worldPos.x * _WindFrequency * 0.3) * input.color.r * _WindStrength * 2.0;
                input.positionOS.z += sin(time * 0.5 + worldPos.z * _WindFrequency * 0.4) * input.color.r * _WindStrength * 1.5;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = posCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}

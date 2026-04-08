Shader "Orlo/TerrainTextured"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0,1)) = 0.05
        _GrassTex ("Grass", 2D) = "white" {}
        _RockTex  ("Rock",  2D) = "white" {}
        _DirtTex  ("Dirt",  2D) = "white" {}
        _SandTex  ("Sand",  2D) = "white" {}
        _GrassNorm ("Grass Normal", 2D) = "bump" {}
        _RockNorm  ("Rock Normal",  2D) = "bump" {}
        _DirtNorm  ("Dirt Normal",  2D) = "bump" {}
        _SandNorm  ("Sand Normal",  2D) = "bump" {}
        _TexScale ("Texture Scale", Float) = 0.1
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

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
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE LOD_FADE_CROSSFADE _LIGHT_LAYERS DEBUG_DISPLAY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_RockTex);  SAMPLER(sampler_RockTex);
            TEXTURE2D(_DirtTex);  SAMPLER(sampler_DirtTex);
            TEXTURE2D(_SandTex);  SAMPLER(sampler_SandTex);
            TEXTURE2D(_GrassNorm); SAMPLER(sampler_GrassNorm);
            TEXTURE2D(_RockNorm);  SAMPLER(sampler_RockNorm);
            TEXTURE2D(_DirtNorm);  SAMPLER(sampler_DirtNorm);
            TEXTURE2D(_SandNorm);  SAMPLER(sampler_SandNorm);

            CBUFFER_START(UnityPerMaterial)
                half _Smoothness;
                float _TexScale;
                half _NormalStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 tangentWS   : TEXCOORD2;
                float4 vertColor   : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.vertColor = input.color;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.shadowCoord = GetShadowCoord(vertexInput);

                return output;
            }

            half3 UnpackNormalScale(half4 packedNormal, half scale)
            {
                half3 normal;
                normal.xy = (packedNormal.wy * 2.0 - 1.0) * scale;
                normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
                return normal;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz * _TexScale;

                // Splatmap weights from vertex color (R=grass, G=rock, B=dirt, A=sand)
                half gw = input.vertColor.r;
                half rw = input.vertColor.g;
                half dw = input.vertColor.b;
                half sw = input.vertColor.a;

                // If all weights are zero (no splatmap data), default to grass
                // to prevent black patches on the terrain
                half totalWeight = gw + rw + dw + sw;
                if (totalWeight < 0.001)
                {
                    gw = 1.0;
                }

                // Sample and blend albedo textures
                half3 albedo = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv).rgb * gw
                             + SAMPLE_TEXTURE2D(_RockTex,  sampler_RockTex,  uv).rgb * rw
                             + SAMPLE_TEXTURE2D(_DirtTex,  sampler_DirtTex,  uv).rgb * dw
                             + SAMPLE_TEXTURE2D(_SandTex,  sampler_SandTex,  uv).rgb * sw;

                // Sample and blend normal maps
                half3 grassN = UnpackNormalScale(SAMPLE_TEXTURE2D(_GrassNorm, sampler_GrassNorm, uv), _NormalStrength);
                half3 rockN  = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNorm,  sampler_RockNorm,  uv), _NormalStrength);
                half3 dirtN  = UnpackNormalScale(SAMPLE_TEXTURE2D(_DirtNorm,  sampler_DirtNorm,  uv), _NormalStrength);
                half3 sandN  = UnpackNormalScale(SAMPLE_TEXTURE2D(_SandNorm,  sampler_SandNorm,  uv), _NormalStrength);

                half3 blendedNormalTS = normalize(grassN * gw + rockN * rw + dirtN * dw + sandN * sw);

                // Transform normal from tangent to world space
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3x3 TBN = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(blendedNormalTS, TBN));

                // Set up PBR surface data
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = blendedNormalTS;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass for casting shadows
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth pass for depth prepass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}

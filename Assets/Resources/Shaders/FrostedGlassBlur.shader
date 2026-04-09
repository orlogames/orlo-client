Shader "Hidden/Orlo/FrostedGlassBlur"
{
    // Single-pass directional Gaussian blur for the FrostedGlassFeature.
    // Called twice per iteration (horizontal + vertical) in a ping-pong.

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "GaussianBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurRadius;
            float4 _BlurDirection;

            // 9-tap Gaussian kernel (sigma ≈ 2.5)
            static const int KERNEL_SIZE = 9;
            static const float KERNEL_WEIGHTS[KERNEL_SIZE] = {
                0.0162, 0.0540, 0.1218, 0.1872, 0.2416,
                0.1872, 0.1218, 0.0540, 0.0162
            };
            static const float KERNEL_OFFSETS[KERNEL_SIZE] = {
                -4, -3, -2, -1, 0, 1, 2, 3, 4
            };

            half4 Frag(Varyings input) : SV_Target
            {
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float2 dir = _BlurDirection.xy * texelSize * _BlurRadius;

                half4 col = half4(0, 0, 0, 0);
                for (int i = 0; i < KERNEL_SIZE; i++)
                {
                    float2 offset = dir * KERNEL_OFFSETS[i];
                    col += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + offset) * KERNEL_WEIGHTS[i];
                }

                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

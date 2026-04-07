// Billboard particle shader that reads positions from a StructuredBuffer.
// Each instance is a camera-facing quad scaled by the particle's size.
// Supports additive blending for the glowing energy-from-air look.

Shader "Hidden/MicroparticleRender"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Blend SrcAlpha One // Additive blending for glow
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 position;
                float3 target;
                float3 velocity;
                float4 color;
                float  life;
                float  phase;
                float  seed;
                float  size;
            };

            StructuredBuffer<Particle> _Particles;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                Particle p = _Particles[instanceID];

                // Billboard: orient quad to face camera
                float3 worldPos = p.position;
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp    = UNITY_MATRIX_V[1].xyz;

                float size = p.size;
                float3 vertexOffset = (input.positionOS.x * camRight + input.positionOS.y * camUp) * size;
                float3 finalPos = worldPos + vertexOffset;

                output.positionCS = mul(UNITY_MATRIX_VP, float4(finalPos, 1.0));
                output.uv = input.uv;
                output.color = p.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Soft circular particle with glow falloff
                float2 center = input.uv - 0.5;
                float dist = length(center) * 2.0;
                float alpha = saturate(1.0 - dist * dist);

                // Core glow (brighter center)
                float core = exp(-dist * dist * 8.0);
                float3 color = input.color.rgb * (0.5 + core * 0.5);

                return half4(color, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

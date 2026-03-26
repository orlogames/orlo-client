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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha One // Additive blending for glow
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "UnityCG.cginc"

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
            sampler2D _MainTex;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata_full v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                Particle p = _Particles[instanceID];

                // Billboard: orient quad to face camera
                float3 worldPos = p.position;
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp    = UNITY_MATRIX_V[1].xyz;

                float size = p.size;
                float3 vertexOffset = (v.vertex.x * camRight + v.vertex.y * camUp) * size;
                float3 finalPos = worldPos + vertexOffset;

                o.pos = mul(UNITY_MATRIX_VP, float4(finalPos, 1.0));
                o.uv = v.texcoord;
                o.color = p.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Soft circular particle with glow falloff
                float2 center = i.uv - 0.5;
                float dist = length(center) * 2.0;
                float alpha = saturate(1.0 - dist * dist);

                // Core glow (brighter center)
                float core = exp(-dist * dist * 8.0);
                float3 color = i.color.rgb * (0.5 + core * 0.5);

                return fixed4(color, alpha * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}

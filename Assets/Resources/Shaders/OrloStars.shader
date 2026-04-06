Shader "Orlo/Stars"
{
    Properties
    {
        _NightFactor ("Night Factor", Range(0,1)) = 1
        _Time2 ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _NightFactor;
            float _Time2;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;     // quad corner UVs (0-1)
                float2 uv2 : TEXCOORD1;    // x=twinkle phase, y=brightness
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 quadUV : TEXCOORD0;  // for circle falloff
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.quadUV = v.uv;
                // uv2.x = twinkle phase, uv2.y = base brightness
                float twinkle = sin(_Time2 * 1.5 + v.uv2.x * 6.2831) * 0.15 + 0.85;
                float alpha = v.uv2.y * _NightFactor * twinkle;
                o.color = float4(v.color.rgb * alpha, alpha);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Soft circle falloff — distance from quad center
                float2 centered = i.quadUV - 0.5;
                float dist = length(centered) * 2.0; // 0 at center, 1 at edge
                float glow = saturate(1.0 - dist * dist); // Quadratic falloff
                glow *= glow; // Extra softness
                return i.color * glow;
            }
            ENDCG
        }
    }
}

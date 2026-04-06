Shader "Orlo/Moon"
{
    Properties
    {
        _Phase ("Phase", Range(-1,1)) = 0.5
        _NightFactor ("Night Factor", Range(0,1)) = 1
        _GlowSize ("Glow Size", Float) = 2.5
    }
    SubShader
    {
        Tags { "Queue"="Background+11" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Phase;
            float _NightFactor;
            float _GlowSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Center UV to -1..1
                float2 p = i.uv * 2.0 - 1.0;
                float dist = length(p);

                // Moon disc: radius 0.35 of quad (rest is glow space)
                float discRadius = 0.35;
                float discEdge = smoothstep(discRadius, discRadius - 0.02, dist);

                // Phase shadow: shift a circle to create crescent
                // _Phase: -1=new moon, 0=half, 1=full moon
                float shadowX = p.x - _Phase * discRadius * 1.4;
                float shadowDist = length(float2(shadowX, p.y));
                float shadow = smoothstep(discRadius * 0.95, discRadius * 0.85, shadowDist);

                // Illuminated portion: where disc exists AND not in shadow
                float illuminated;
                if (_Phase > 0.0)
                    illuminated = discEdge * (1.0 - shadow * (1.0 - _Phase));
                else
                    illuminated = discEdge * shadow * (1.0 + _Phase);

                // Clamp for full/new moon
                if (_Phase > 0.9) illuminated = discEdge;
                if (_Phase < -0.9) illuminated = 0;

                // Moon color: pale silver-blue
                float3 moonColor = float3(0.85, 0.88, 0.95);
                float3 disc = moonColor * illuminated;

                // Glow halo: gaussian falloff beyond disc
                float glowDist = max(0, dist - discRadius * 0.8);
                float glow = exp(-glowDist * glowDist * 15.0) * 0.3;
                float3 glowColor = float3(0.6, 0.65, 0.8) * glow;

                // Combine: visible more at night, faintly during day
                float visibility = lerp(0.15, 1.0, _NightFactor);
                float3 final = (disc + glowColor) * visibility;

                return fixed4(final, 1.0);
            }
            ENDCG
        }
    }
}

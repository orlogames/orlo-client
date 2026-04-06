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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // uv.x = twinkle phase, uv.y = base brightness
                float twinkle = sin(_Time2 * 1.5 + v.uv.x * 6.2831) * 0.15 + 0.85;
                float alpha = v.uv.y * _NightFactor * twinkle;
                o.color = float4(v.color.rgb * alpha, alpha);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}

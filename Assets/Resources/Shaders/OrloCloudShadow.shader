Shader "Orlo/CloudShadow"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "white" {}
        _UVOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        _Threshold ("Threshold", Float) = 0.42
        _Softness ("Softness", Float) = 0.18
        _Density ("Density", Float) = 0.35
        _ShadowStrength ("Shadow Strength", Float) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
            float4 _UVOffset;
            float _Threshold;
            float _Softness;
            float _Density;
            float _ShadowStrength;

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

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv + _UVOffset.xy;
                float noise = tex2D(_NoiseTex, uv).r;
                float adjustedThreshold = lerp(0.7, 0.15, _Density);
                float cloudShape = saturate((noise - adjustedThreshold) / max(_Softness, 0.001));
                float shadow = 1.0 - cloudShape * _ShadowStrength;
                return half4(shadow, shadow, shadow, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

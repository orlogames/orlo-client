Shader "Orlo/Composite"
{
    Properties { _MainTex ("Base", 2D) = "white" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BloomTex;
            float _BloomIntensity;
            float _Warmth;
            float _Contrast;
            float _Saturation;
            float _VignetteIntensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            half4 frag(v2f i) : SV_Target
            {
                half4 scene = tex2D(_MainTex, i.uv);
                half4 bloom = tex2D(_BloomTex, i.uv);

                // Additive bloom
                half3 c = scene.rgb + bloom.rgb * _BloomIntensity;

                // Warm color shift (add to red, slightly to green, reduce blue)
                c.r += _Warmth;
                c.g += _Warmth * 0.4;
                c.b -= _Warmth * 0.3;

                // Contrast (around 0.5 midpoint)
                c = (c - 0.5) * _Contrast + 0.5;

                // Saturation
                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(float3(luma, luma, luma), c, _Saturation);

                // Vignette
                float2 d = i.uv - 0.5;
                float vignette = 1.0 - dot(d, d) * _VignetteIntensity * 2.0;
                c *= vignette;

                return half4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

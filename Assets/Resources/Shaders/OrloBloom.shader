Shader "Orlo/Bloom"
{
    Properties { _MainTex ("Base", 2D) = "white" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        // Pass 0: Threshold + Downsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragThreshold
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Threshold;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            half4 fragThreshold(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                float brightness = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));
                float contribution = max(0, brightness - _Threshold);
                return half4(c.rgb * contribution, 1);
            }
            ENDCG
        }

        // Pass 1: Gaussian blur (horizontal + vertical in one pass via box filter)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            half4 fragBlur(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                half4 c = tex2D(_MainTex, i.uv) * 0.25;
                c += tex2D(_MainTex, i.uv + float2(texel.x, 0)) * 0.125;
                c += tex2D(_MainTex, i.uv - float2(texel.x, 0)) * 0.125;
                c += tex2D(_MainTex, i.uv + float2(0, texel.y)) * 0.125;
                c += tex2D(_MainTex, i.uv - float2(0, texel.y)) * 0.125;
                c += tex2D(_MainTex, i.uv + texel) * 0.0625;
                c += tex2D(_MainTex, i.uv - texel) * 0.0625;
                c += tex2D(_MainTex, i.uv + float2(texel.x, -texel.y)) * 0.0625;
                c += tex2D(_MainTex, i.uv + float2(-texel.x, texel.y)) * 0.0625;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}

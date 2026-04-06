Shader "Orlo/Clouds"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "white" {}
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _SunDir ("Sun Direction", Vector) = (0, -1, 0, 0)
        _SunColor ("Sun Color", Color) = (1, 0.92, 0.75, 1)
        _UVOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        _Threshold ("Threshold", Float) = 0.42
        _Softness ("Softness", Float) = 0.18
        _Density ("Density", Float) = 0.35
        _Brightness ("Brightness", Float) = 1.1
        _BacklitPower ("Backlit Power", Float) = 0.6
        _CloudAlpha ("Cloud Alpha", Float) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
            float4 _CloudColor;
            float4 _SunDir;
            float4 _SunColor;
            float4 _UVOffset;
            float _Threshold;
            float _Softness;
            float _Density;
            float _Brightness;
            float _BacklitPower;
            float _CloudAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(o.worldPos - _WorldSpaceCameraPos.xyz);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv + _UVOffset.xy;

                // Sample noise texture (3 octaves baked in during generation)
                float noise = tex2D(_NoiseTex, uv).r;

                // Apply density-adjusted threshold
                float adjustedThreshold = lerp(0.7, 0.15, _Density);
                float cloudShape = saturate((noise - adjustedThreshold) / max(_Softness, 0.001));

                // Discard fully transparent fragments
                if (cloudShape < 0.01)
                    discard;

                // Base cloud color from sun
                float3 baseColor = _CloudColor.rgb * _Brightness;

                // Backlit glow: when looking toward the sun through clouds,
                // edges glow brighter. Compute dot(viewDir, -sunDir).
                float3 sunDir = normalize(_SunDir.xyz);
                float backlit = saturate(dot(normalize(i.viewDir), -sunDir));
                backlit = pow(backlit, 3.0) * _BacklitPower;

                // Edge glow: thinner cloud edges get more backlit effect
                float edgeFactor = 1.0 - cloudShape;
                float3 backlitColor = _SunColor.rgb * 1.5;
                float3 finalColor = lerp(baseColor, backlitColor, backlit * edgeFactor);

                // Slight darkening toward center (thicker clouds = darker base)
                finalColor *= lerp(1.0, 0.7, cloudShape * 0.5);

                // Sun-facing side brighter (simple NdotL on the cloud plane normal = up)
                float sunFacing = saturate(-sunDir.y) * 0.3 + 0.7;
                finalColor *= sunFacing;

                float alpha = cloudShape * _CloudAlpha;

                return half4(finalColor, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}

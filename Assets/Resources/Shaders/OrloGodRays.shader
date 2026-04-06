Shader "Orlo/GodRays"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        // Pass 0: Brightness threshold (extract bright pixels near the sun)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragThreshold
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _BrightThreshold;
            float2 _SunScreenPos;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            half4 fragThreshold(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                float brightness = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));

                // Radial falloff from sun position - only extract bright pixels near the sun
                float2 delta = i.uv - _SunScreenPos;
                float dist = length(delta);
                float radialMask = saturate(1.0 - dist * 1.2);

                float contribution = max(0, brightness - _BrightThreshold) * radialMask;
                return half4(c.rgb * contribution, 1);
            }
            ENDCG
        }

        // Pass 1: Radial blur from sun screen position
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragRadialBlur
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float2 _SunScreenPos;
            int _NumSamples;
            float _Decay;
            float _Weight;
            float _RayExposure;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            half4 fragRadialBlur(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 deltaUV = (uv - _SunScreenPos);

                // Step size toward the sun
                float invSamples = 1.0 / (float)_NumSamples;
                deltaUV *= invSamples;

                float illuminationDecay = 1.0;
                half3 accumColor = 0;

                // March from current pixel toward the sun, accumulating light
                float2 sampleUV = uv;
                for (int s = 0; s < 64; s++) // Hardcoded max for shader compiler
                {
                    if (s >= _NumSamples) break;

                    sampleUV -= deltaUV;
                    half3 sampleColor = tex2D(_MainTex, sampleUV).rgb;
                    sampleColor *= illuminationDecay * _Weight;
                    accumColor += sampleColor;
                    illuminationDecay *= _Decay;
                }

                return half4(accumColor * _RayExposure, 1);
            }
            ENDCG
        }

        // Pass 2: Additive composite (god rays over scene)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _RaysTex;
            float _RayIntensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            half4 fragComposite(v2f i) : SV_Target
            {
                half4 scene = tex2D(_MainTex, i.uv);
                half3 rays = tex2D(_RaysTex, i.uv).rgb;
                return half4(scene.rgb + rays * _RayIntensity, scene.a);
            }
            ENDCG
        }
    }
    Fallback Off
}

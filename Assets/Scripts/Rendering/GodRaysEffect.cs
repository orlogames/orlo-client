using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Screen-space volumetric light shafts (god rays) via radial blur from the sun position.
    /// Built-in Render Pipeline compatible using OnRenderImage.
    /// Works with CloudRenderer to produce light streaming through cloud gaps.
    ///
    /// Technique:
    /// 1. Threshold bright pixels from the scene (sun disk + sky near sun)
    /// 2. Radial blur from the sun's screen position outward
    /// 3. Additively composite over the final scene
    /// All done at half resolution for performance.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GodRaysEffect : MonoBehaviour
    {
        [Header("Ray Quality")]
        [SerializeField] private int numSamples = 64;
        [SerializeField] private float decay = 0.96f;
        [SerializeField] private float weight = 0.6f;
        [SerializeField] private float exposure = 0.22f;

        [Header("Threshold")]
        [SerializeField] private float brightThreshold = 0.75f;

        [Header("Intensity")]
        [SerializeField] private float rayIntensity = 1.0f;
        [SerializeField] private float maxIntensity = 1.5f;

        private Material godRaysMaterial;
        private Camera cam;
        private Light sunLight;

        // External modulation from CloudRenderer
        private float godRayFactor = 0.5f;
        private bool sunAboveHorizon = true;

        private static readonly string GodRaysShaderSource = @"
Shader ""Hidden/Orlo/GodRays""
{
    Properties
    {
        _MainTex (""Base"", 2D) = ""white"" {}
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
            #include ""UnityCG.cginc""

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

                // Radial falloff from sun position — only extract bright pixels near the sun
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
            #include ""UnityCG.cginc""

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
            #include ""UnityCG.cginc""

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
}";

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.allowHDR = true;

            godRaysMaterial = CompileMaterial(GodRaysShaderSource);
            if (godRaysMaterial == null)
            {
                Debug.LogWarning("[GodRays] Failed to compile shader — god rays disabled");
                enabled = false;
            }
        }

        private Material CompileMaterial(string shaderSource)
        {
            try
            {
                var mat = new Material(shaderSource);
                if (mat.shader == null || !mat.shader.isSupported)
                {
                    Object.Destroy(mat);
                    return null;
                }
                return mat;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GodRays] Shader compilation failed: {e.Message}");
                return null;
            }
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (godRaysMaterial == null || !sunAboveHorizon || godRayFactor < 0.01f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // Find sun if not cached
            if (sunLight == null)
                sunLight = FindSunLight();

            if (sunLight == null)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // Compute sun screen position
            Vector3 sunWorldDir = -sunLight.transform.forward;
            Vector3 sunFarPoint = cam.transform.position + sunWorldDir * cam.farClipPlane * 0.9f;
            Vector3 sunScreen = cam.WorldToViewportPoint(sunFarPoint);

            // If sun is behind the camera, fade out gracefully
            bool sunInFront = sunScreen.z > 0;
            if (!sunInFront)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // Fade rays when sun is near screen edges
            float edgeFade = 1f;
            float distFromCenter = Mathf.Max(
                Mathf.Abs(sunScreen.x - 0.5f),
                Mathf.Abs(sunScreen.y - 0.5f)
            );
            if (distFromCenter > 0.4f)
                edgeFade = Mathf.Clamp01((0.8f - distFromCenter) / 0.4f);

            float finalIntensity = rayIntensity * godRayFactor * edgeFade;
            finalIntensity = Mathf.Min(finalIntensity, maxIntensity);

            if (finalIntensity < 0.01f)
            {
                Graphics.Blit(src, dst);
                return;
            }

            Vector2 sunPos = new Vector2(sunScreen.x, sunScreen.y);

            // Half-resolution for performance
            int halfW = src.width / 2;
            int halfH = src.height / 2;

            // Pass 0: Extract bright pixels near the sun
            var brightRT = RenderTexture.GetTemporary(halfW, halfH, 0, src.format);
            godRaysMaterial.SetFloat("_BrightThreshold", brightThreshold);
            godRaysMaterial.SetVector("_SunScreenPos", sunPos);
            Graphics.Blit(src, brightRT, godRaysMaterial, 0);

            // Pass 1: Radial blur
            var raysRT = RenderTexture.GetTemporary(halfW, halfH, 0, src.format);
            godRaysMaterial.SetVector("_SunScreenPos", sunPos);
            godRaysMaterial.SetInt("_NumSamples", numSamples);
            godRaysMaterial.SetFloat("_Decay", decay);
            godRaysMaterial.SetFloat("_Weight", weight);
            godRaysMaterial.SetFloat("_RayExposure", exposure);
            Graphics.Blit(brightRT, raysRT, godRaysMaterial, 1);

            // Pass 2: Composite
            godRaysMaterial.SetTexture("_RaysTex", raysRT);
            godRaysMaterial.SetFloat("_RayIntensity", finalIntensity);
            Graphics.Blit(src, dst, godRaysMaterial, 2);

            // Cleanup
            RenderTexture.ReleaseTemporary(brightRT);
            RenderTexture.ReleaseTemporary(raysRT);
        }

        private Light FindSunLight()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                    return light;
            }
            return null;
        }

        // --- Public API ---

        /// <summary>
        /// Set the god ray visibility factor from CloudRenderer.
        /// Partly cloudy conditions produce the best god rays.
        /// </summary>
        public void SetGodRayFactor(float factor)
        {
            godRayFactor = Mathf.Clamp01(factor);
        }

        /// <summary>
        /// Set whether the sun is above the horizon.
        /// God rays are disabled at night.
        /// </summary>
        public void SetSunAboveHorizon(bool above)
        {
            sunAboveHorizon = above;
        }

        /// <summary>
        /// Set overall ray intensity (0-2 range).
        /// </summary>
        public void SetIntensity(float intensity)
        {
            rayIntensity = Mathf.Clamp(intensity, 0f, 2f);
        }

        private void OnDestroy()
        {
            if (godRaysMaterial != null) Destroy(godRaysMaterial);
        }
    }
}

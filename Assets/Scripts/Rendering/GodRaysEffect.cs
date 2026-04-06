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
        [SerializeField] private float weight = 0.75f;
        [SerializeField] private float exposure = 0.30f;

        [Header("Threshold")]
        [SerializeField] private float brightThreshold = 0.65f;

        [Header("Intensity")]
        [SerializeField] private float rayIntensity = 1.3f;
        [SerializeField] private float maxIntensity = 1.5f;

        private Material godRaysMaterial;
        private Camera cam;
        private Light sunLight;

        // External modulation from CloudRenderer
        private float godRayFactor = 0.5f;
        private bool sunAboveHorizon = true;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.allowHDR = true;

            var shader = Resources.Load<Shader>("Shaders/OrloGodRays");
            if (shader == null) shader = Shader.Find("Orlo/GodRays");
            if (shader == null)
            {
                Debug.LogWarning("[GodRays] God rays shader not found — god rays disabled");
                enabled = false;
                return;
            }

            godRaysMaterial = new Material(shader);
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

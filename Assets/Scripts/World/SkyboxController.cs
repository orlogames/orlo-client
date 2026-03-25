using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Procedural skybox driven by server environment state.
    /// Uses a gradient-based approach with sun/moon positioning.
    /// </summary>
    public class SkyboxController : MonoBehaviour
    {
        // Current server state
        private float timeOfDay = 0.25f;
        private Color sunColor = new Color(1f, 0.95f, 0.8f);
        private Color ambientColor = new Color(0.3f, 0.35f, 0.45f);
        private float weatherIntensity = 0f;

        // Interpolation targets (smooth transitions)
        private float targetTimeOfDay = 0.25f;
        private Color targetSunColor;
        private Color targetAmbientColor;

        // Scene references
        private Light directionalLight;
        private Material skyboxMaterial;

        // Sky gradient colors
        private static readonly Color DayTop = new Color(0.25f, 0.5f, 0.95f);
        private static readonly Color DayHorizon = new Color(0.6f, 0.75f, 0.95f);
        private static readonly Color NightTop = new Color(0.02f, 0.02f, 0.08f);
        private static readonly Color NightHorizon = new Color(0.05f, 0.05f, 0.15f);
        private static readonly Color SunsetTop = new Color(0.3f, 0.2f, 0.5f);
        private static readonly Color SunsetHorizon = new Color(0.9f, 0.4f, 0.15f);

        private void Start()
        {
            // Create or find directional light
            directionalLight = FindFirstObjectByType<Light>();
            if (directionalLight == null)
            {
                var go = new GameObject("Sun");
                directionalLight = go.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
                directionalLight.shadows = LightShadows.Soft;
            }

            // Create procedural skybox material
            skyboxMaterial = new Material(Shader.Find("Skybox/Procedural"));
            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }

            targetSunColor = sunColor;
            targetAmbientColor = ambientColor;
        }

        /// <summary>
        /// Called when server sends EnvironmentUpdate
        /// </summary>
        public void OnEnvironmentUpdate(float time, float sunR, float sunG, float sunB,
                                         float ambR, float ambG, float ambB,
                                         float fogDensity, float fogR, float fogG, float fogB,
                                         float intensity)
        {
            targetTimeOfDay = time;
            targetSunColor = new Color(sunR, sunG, sunB);
            targetAmbientColor = new Color(ambR, ambG, ambB);
            weatherIntensity = intensity;

            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = new Color(fogR, fogG, fogB);
            RenderSettings.fog = fogDensity > 0.001f;
        }

        private void Update()
        {
            float lerpSpeed = 2f * Time.deltaTime;

            // Smooth interpolation to server state
            timeOfDay = Mathf.Lerp(timeOfDay, targetTimeOfDay, lerpSpeed);
            sunColor = Color.Lerp(sunColor, targetSunColor, lerpSpeed);
            ambientColor = Color.Lerp(ambientColor, targetAmbientColor, lerpSpeed);

            UpdateSunPosition();
            UpdateSkyColors();
            UpdateAmbientLighting();
        }

        private void UpdateSunPosition()
        {
            if (directionalLight == null) return;

            // Sun angle: 0 at horizon (sunrise), 90 at zenith, 180 at horizon (sunset)
            float sunAngle = timeOfDay * 360f - 90f;
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
            directionalLight.color = sunColor;
            directionalLight.intensity = Mathf.Max(0f, Mathf.Sin(timeOfDay * Mathf.PI * 2f));

            // Reduce light during weather
            directionalLight.intensity *= (1f - weatherIntensity * 0.5f);
        }

        private void UpdateSkyColors()
        {
            if (skyboxMaterial == null) return;

            // Determine blend factors
            float sunHeight = Mathf.Max(0f, Mathf.Sin(timeOfDay * Mathf.PI * 2f));
            float dawnDusk = Mathf.Max(0f,
                Mathf.Sin(timeOfDay * Mathf.PI * 4f) * (1f - Mathf.Abs(sunHeight - 0.3f) * 3f));
            dawnDusk = Mathf.Clamp01(dawnDusk);

            Color topColor = Color.Lerp(NightTop, DayTop, sunHeight);
            topColor = Color.Lerp(topColor, SunsetTop, dawnDusk * 0.5f);

            Color horizonColor = Color.Lerp(NightHorizon, DayHorizon, sunHeight);
            horizonColor = Color.Lerp(horizonColor, SunsetHorizon, dawnDusk);

            // Grey out during weather
            Color grey = new Color(0.5f, 0.5f, 0.55f);
            topColor = Color.Lerp(topColor, grey, weatherIntensity * 0.6f);
            horizonColor = Color.Lerp(horizonColor, grey, weatherIntensity * 0.4f);

            skyboxMaterial.SetColor("_SkyTint", topColor);
            skyboxMaterial.SetColor("_GroundColor", horizonColor);
            skyboxMaterial.SetFloat("_Exposure", Mathf.Lerp(0.5f, 1.3f, sunHeight));
        }

        private void UpdateAmbientLighting()
        {
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }
    }
}

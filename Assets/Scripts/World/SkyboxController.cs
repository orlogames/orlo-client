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

        // Sky gradient colors — Threshold visual target (gritty realism, desaturated)
        private static readonly Color DayTop = new Color(0.35f, 0.52f, 0.78f);          // Muted steel blue
        private static readonly Color DayHorizon = new Color(0.55f, 0.62f, 0.72f);      // Hazy, desaturated
        private static readonly Color NightTop = new Color(0.015f, 0.015f, 0.06f);
        private static readonly Color NightHorizon = new Color(0.04f, 0.04f, 0.12f);
        private static readonly Color SunsetTop = new Color(0.28f, 0.18f, 0.42f);       // Deep purple
        private static readonly Color SunsetHorizon = new Color(0.85f, 0.35f, 0.12f);   // Warm amber

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

            UpdateSkyVisuals();
        }

        /// <summary>
        /// Force initialization — call when skybox needs to be ready immediately (e.g. after character spawn).
        /// </summary>
        public void ForceInitialize()
        {
            if (skyboxMaterial == null)
                Start();
            else
                UpdateSkyVisuals();
        }

        private void UpdateSkyVisuals()
        {
            if (skyboxMaterial == null) return;

            // Set initial daytime sky
            float sunAngle = timeOfDay * 360f - 90f;
            // Threshold visual target — gritty realism (Arc Raiders / Division 2)
            skyboxMaterial.SetFloat("_SunSize", 0.035f);
            skyboxMaterial.SetFloat("_SunSizeConvergence", 6f);
            skyboxMaterial.SetFloat("_AtmosphereThickness", 1.2f);
            skyboxMaterial.SetColor("_SkyTint", DayTop);
            skyboxMaterial.SetColor("_GroundColor", new Color(0.32f, 0.30f, 0.28f));   // Earthy ground
            skyboxMaterial.SetFloat("_Exposure", 1.15f);

            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.35f, 0.50f);           // Cool desaturated sky
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.28f);       // Muted midtones
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.10f);        // Dark warm ground

            // Atmospheric fog — depth + haze for frontier settlement feel
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.48f, 0.52f, 0.58f);                  // Cool atmospheric haze
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 400f;

            // Sun — warm directional, deep shadows
            if (directionalLight != null)
            {
                directionalLight.color = new Color(1f, 0.92f, 0.78f);                  // Warm golden sun
                directionalLight.intensity = 1.35f;
                directionalLight.shadows = LightShadows.Soft;
                directionalLight.shadowStrength = 0.85f;
                directionalLight.transform.rotation = Quaternion.Euler(45f, -35f, 0f);  // Late morning angle
            }

            DynamicGI.UpdateEnvironment();
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

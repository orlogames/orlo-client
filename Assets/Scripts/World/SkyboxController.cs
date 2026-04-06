using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Procedural skybox driven by server environment state.
    /// Uses a gradient-based approach with sun/moon positioning.
    /// </summary>
    public class SkyboxController : MonoBehaviour
    {
        // Current server state — default to golden hour (~0.8 on 0-1 day cycle)
        private float timeOfDay = 0.8f;
        private Color sunColor = new Color(1f, 0.92f, 0.75f);
        private Color ambientColor = new Color(0.55f, 0.5f, 0.4f);
        private float weatherIntensity = 0f;

        // Interpolation targets (smooth transitions)
        private float targetTimeOfDay = 0.8f;
        private Color targetSunColor;
        private Color targetAmbientColor;

        // Scene references
        private Light directionalLight;
        private Material skyboxMaterial;

        // Sky gradient colors — warm cinematic palette (golden hour emphasis)
        private static readonly Color DayTop = new Color(0.38f, 0.55f, 0.82f);          // Vivid sky blue
        private static readonly Color DayHorizon = new Color(0.62f, 0.65f, 0.70f);      // Warm haze
        private static readonly Color NightTop = new Color(0.015f, 0.015f, 0.06f);
        private static readonly Color NightHorizon = new Color(0.04f, 0.04f, 0.12f);
        private static readonly Color SunsetTop = new Color(0.35f, 0.18f, 0.45f);       // Rich purple
        private static readonly Color SunsetHorizon = new Color(0.95f, 0.45f, 0.15f);   // Vibrant amber-orange

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

            // Golden hour sky — warm cinematic atmosphere
            float sunAngle = timeOfDay * 360f - 90f;
            skyboxMaterial.SetFloat("_SunSize", 0.06f);               // Larger sun disk near horizon
            skyboxMaterial.SetFloat("_SunSizeConvergence", 4f);       // Softer sun falloff
            skyboxMaterial.SetFloat("_AtmosphereThickness", 1.6f);    // Thicker atmosphere for warm scattering
            skyboxMaterial.SetColor("_SkyTint", DayTop);
            skyboxMaterial.SetColor("_GroundColor", new Color(0.45f, 0.38f, 0.30f));   // Warm earthy ground
            skyboxMaterial.SetFloat("_Exposure", 1.25f);

            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.6f, 0.75f);             // Cool sky fill
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.4f);          // Warm midtones
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.35f, 0.25f);          // Warm ground bounce
            RenderSettings.ambientIntensity = 1.0f;

            // Atmospheric fog — warm haze for depth and golden hour feel
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.65f, 0.6f, 0.5f);                    // Warm atmospheric haze
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 400f;

            // Sun — golden hour directional light, low angle, deep shadows
            if (directionalLight != null)
            {
                directionalLight.color = new Color(1f, 0.92f, 0.75f);                  // Golden sun
                directionalLight.intensity = 1.4f;
                directionalLight.shadows = LightShadows.Soft;
                directionalLight.shadowStrength = 0.85f;
                directionalLight.transform.rotation = Quaternion.Euler(35f, -45f, 0f);  // Low golden hour angle
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

            // Increase sun disk size near horizon for golden hour glow
            float horizonFactor = 1f - Mathf.Abs(sunHeight - 0.3f) * 2f;
            horizonFactor = Mathf.Clamp01(horizonFactor);
            skyboxMaterial.SetFloat("_SunSize", Mathf.Lerp(0.04f, 0.08f, horizonFactor));
            skyboxMaterial.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.2f, 1.8f, horizonFactor));
        }

        // --- Public API for CloudRenderer / GodRaysEffect ---

        /// <summary>
        /// Forward direction of the sun (directional light). Used by CloudRenderer for
        /// backlit edges and shadow projection, and by GodRaysEffect for screen-space position.
        /// </summary>
        public Vector3 SunDirection => directionalLight != null
            ? directionalLight.transform.forward
            : Vector3.down;

        /// <summary>
        /// Current sun color (directional light color).
        /// </summary>
        public Color SunColor => directionalLight != null
            ? directionalLight.color
            : Color.white;

        /// <summary>
        /// Whether the sun is above the horizon (intensity > 0).
        /// God rays should be disabled at night.
        /// </summary>
        public bool IsSunAboveHorizon
        {
            get
            {
                float sunHeight = Mathf.Max(0f, Mathf.Sin(timeOfDay * Mathf.PI * 2f));
                return sunHeight > 0.05f;
            }
        }

        /// <summary>
        /// Current weather intensity (0-1). Used to modulate cloud density externally.
        /// </summary>
        public float WeatherIntensity => weatherIntensity;

        private void UpdateAmbientLighting()
        {
            // Use trilight ambient to preserve warm golden hour ground/equator/sky separation
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            float sunHeight = Mathf.Max(0f, Mathf.Sin(timeOfDay * Mathf.PI * 2f));

            // Sky: cool blue at day, dark at night
            RenderSettings.ambientSkyColor = Color.Lerp(
                new Color(0.05f, 0.05f, 0.1f),   // night sky
                new Color(0.5f, 0.6f, 0.75f),     // day sky (cool fill)
                sunHeight
            );

            // Equator: warm midtones from sun scatter
            RenderSettings.ambientEquatorColor = Color.Lerp(
                new Color(0.03f, 0.03f, 0.06f),   // night
                ambientColor,                       // server-driven warm color
                sunHeight
            );

            // Ground: warm bounce light
            RenderSettings.ambientGroundColor = Color.Lerp(
                new Color(0.02f, 0.02f, 0.03f),   // night ground
                new Color(0.4f, 0.35f, 0.25f),     // warm ground bounce
                sunHeight
            );
        }
    }
}

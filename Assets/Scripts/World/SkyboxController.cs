using UnityEngine;
using Orlo.Rendering;

namespace Orlo.World
{
    /// <summary>
    /// Procedural skybox driven by server environment state.
    /// Uses a gradient-based approach with sun/moon positioning.
    /// Manages StarFieldRenderer (procedural stars) and MoonRenderer (phased moon).
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

        // Night sky subsystems
        private StarFieldRenderer starField;
        private MoonRenderer moonRenderer;

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

            InitializeNightSky();
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

        private void InitializeNightSky()
        {
            // Star field dome — follows camera, fades with day/night
            var starGo = new GameObject("StarField");
            starGo.transform.SetParent(transform);
            starField = starGo.AddComponent<StarFieldRenderer>();
            starField.Initialize();

            // Moon — orbits sky dome with phase cycle
            var moonGo = new GameObject("Moon");
            moonGo.transform.SetParent(transform);
            moonRenderer = moonGo.AddComponent<MoonRenderer>();
            moonRenderer.Initialize(directionalLight);

            Debug.Log("[Orlo] Night sky initialized (stars + moon)");
        }

        /// <summary>
        /// Compute how visible the night sky is (0=full day, 1=full night).
        /// Stars fade in during sunset and out during sunrise.
        /// </summary>
        private float ComputeNightFactor()
        {
            // sunHeight: 0 at horizon, 1 at zenith. Negative when sun below horizon.
            float sunHeight = Mathf.Sin(timeOfDay * Mathf.PI * 2f);

            // Stars fully visible when sun well below horizon, invisible when above
            // Transition band: sunHeight -0.1 (full night) to 0.15 (full day)
            float night = 1f - Mathf.InverseLerp(-0.1f, 0.15f, sunHeight);

            // Reduce star visibility during weather (clouds obscure stars)
            night *= (1f - weatherIntensity * 0.8f);

            return Mathf.Clamp01(night);
        }

        private void UpdateSkyVisuals()
        {
            if (skyboxMaterial == null) return;

            // Golden hour sky — warm cinematic atmosphere matching concept art
            float sunAngle = timeOfDay * 360f - 90f;
            skyboxMaterial.SetFloat("_SunSize", 0.08f);               // Large sun disk near horizon
            skyboxMaterial.SetFloat("_SunSizeConvergence", 3f);       // Soft warm sun falloff
            skyboxMaterial.SetFloat("_AtmosphereThickness", 2.0f);    // Thick atmosphere for golden scattering
            skyboxMaterial.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.75f));  // Warm blue-gold sky
            skyboxMaterial.SetColor("_GroundColor", new Color(0.50f, 0.40f, 0.28f));   // Warm earthy ground
            skyboxMaterial.SetFloat("_Exposure", 1.4f);               // Slightly brighter

            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.50f, 0.65f);           // Warm sky fill
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.55f, 0.35f);        // Golden midtones
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.45f, 0.30f);         // Warm bounce
            RenderSettings.ambientIntensity = 1.15f;

            // Atmospheric fog — dense golden haze for depth and mood
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;                                          // Thicker than before
            RenderSettings.fogColor = new Color(0.72f, 0.58f, 0.38f);                  // Rich golden haze

            // Sun — low golden hour angle, dramatic shadows
            if (directionalLight != null)
            {
                directionalLight.color = new Color(1f, 0.88f, 0.65f);                  // Deep golden sun
                directionalLight.intensity = 2.0f;                                      // Brighter
                directionalLight.shadows = LightShadows.Soft;
                directionalLight.shadowStrength = 0.8f;
                directionalLight.transform.rotation = Quaternion.Euler(25f, -45f, 0f);  // Low dramatic golden hour
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
            UpdateNightSky();
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

        private void UpdateNightSky()
        {
            float nightFactor = ComputeNightFactor();

            if (starField != null)
                starField.SetNightFactor(nightFactor);

            if (moonRenderer != null)
            {
                moonRenderer.SetTimeOfDay(timeOfDay);
                moonRenderer.SetNightFactor(nightFactor);
            }
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

        /// <summary>
        /// Access to the star field for "look at star" interaction.
        /// Returns the name of the star system the player is looking at, or null.
        /// </summary>
        public string GetStarSystemAtCursor()
        {
            if (starField == null) return null;
            var cam = Camera.main;
            if (cam == null) return null;
            return starField.GetNamedStarAt(cam.transform.forward);
        }

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

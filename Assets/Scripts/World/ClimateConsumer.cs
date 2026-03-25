using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Consumes cosmic climate data and drives rendering, weather, and gameplay systems.
    /// Receives ClimatePackets from server via NetworkManager or from a local simulation.
    /// Translates physics-based climate state into Unity shader uniforms, particle effects,
    /// NPC behavior modifiers, and environmental audio triggers.
    /// </summary>
    public class ClimateConsumer : MonoBehaviour
    {
        public static ClimateConsumer Instance { get; private set; }

        [Header("References")]
        [SerializeField] private SkyboxController skybox;
        [SerializeField] private WeatherController weather;
        [SerializeField] private GameDirector director;
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;

        [Header("Climate Settings")]
        [SerializeField] private float transitionSpeed = 2f;

        // Current interpolated climate state at player position
        public ClimateData CurrentClimate { get; private set; }
        public SeasonInfo CurrentSeason { get; private set; }
        public StarInfo CurrentStar { get; private set; }

        // Interpolation targets
        private ClimateData targetClimate;
        private StarStateData currentStarState;

        // Wetness accumulation (builds during rain, decays after)
        private float wetness;
        private bool wasRaining;

        // Disaster flags readable by other systems
        public bool EarthquakeActive { get; private set; }
        public float EarthquakeIntensity { get; private set; }

        // Flare boost timer
        private float flareBoostTimer;
        private float flareBoostIntensity;

        // Cached shader property IDs for performance
        private static readonly int ShaderSkyTint = Shader.PropertyToID("_SkyTint");
        private static readonly int ShaderSunColor = Shader.PropertyToID("_SunColor");
        private static readonly int ShaderSunIntensity = Shader.PropertyToID("_SunIntensity");
        private static readonly int ShaderSunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int ShaderFogDensity = Shader.PropertyToID("_FogDensity");
        private static readonly int ShaderFogColor = Shader.PropertyToID("_FogColor");
        private static readonly int ShaderRainIntensity = Shader.PropertyToID("_RainIntensity");
        private static readonly int ShaderSnowIntensity = Shader.PropertyToID("_SnowIntensity");
        private static readonly int ShaderWindStrength = Shader.PropertyToID("_WindStrength");
        private static readonly int ShaderWindDirection = Shader.PropertyToID("_WindDirection");
        private static readonly int ShaderWetness = Shader.PropertyToID("_Wetness");
        private static readonly int ShaderAuroraIntensity = Shader.PropertyToID("_AuroraIntensity");
        private static readonly int ShaderAshDensity = Shader.PropertyToID("_AshDensity");
        private static readonly int ShaderHeatHaze = Shader.PropertyToID("_HeatHaze");
        private static readonly int ShaderLensFrost = Shader.PropertyToID("_LensFrost");

        // Constants
        private const float WetnessAccumulationTime = 30f;  // 0->1 over 30s
        private const float WetnessDecayTime = 60f;         // 1->0 over 60s
        private const float HeatHazeTemperatureThreshold = 35f;
        private const float HeatHazeHumidityThreshold = 0.3f;
        private const float LensFrostTemperatureThreshold = -10f;
        private const float MoonIntensity = 0.05f;

        // --- PUBLIC API ---

        /// <summary>
        /// Called by NetworkManager when server sends climate packet.
        /// </summary>
        public void OnClimatePacket(ClimatePacketData packet)
        {
            targetClimate = new ClimateData
            {
                temperature = packet.temperature,
                humidity = packet.humidity,
                pressure = packet.pressure,
                windVector = packet.windVector,
                precipitationRate = packet.precipitationRate,
                precipitationType = packet.precipitationType ?? "",
                cloudCover = packet.cloudCover,
                stormCategory = packet.stormCategory,
                visibility = packet.visibility,
                solarElevation = packet.solarElevation,
                isNight = packet.isNight,
                season = packet.season,
                atmosphericTint = packet.atmosphericTint,
                biome = packet.biome ?? "",
                activeDisasters = packet.activeDisasters != null
                    ? new List<string>(packet.activeDisasters)
                    : new List<string>()
            };
        }

        /// <summary>
        /// Called by NetworkManager when server sends star state.
        /// </summary>
        public void OnStarState(StarStateData state)
        {
            currentStarState = state;

            CurrentStar = new StarInfo
            {
                type = state.starType ?? "",
                color = state.spectralColor,
                luminosity = state.luminosity,
                uvIndex = state.uvIndex,
                isFlaring = state.hasFlare
            };

            // Trigger flare boost
            if (state.hasFlare && state.flareIntensity > 0f)
            {
                flareBoostTimer = 2f; // 2 second flare flash
                flareBoostIntensity = Mathf.Lerp(0.2f, 0.5f, state.flareIntensity);
            }
        }

        /// <summary>
        /// Get temperature at player position (for NPC AI), with season modifier applied.
        /// </summary>
        public float GetTemperature()
        {
            return CurrentClimate.temperature + CurrentSeason.temperatureMod;
        }

        /// <summary>
        /// Get wind vector at player position.
        /// </summary>
        public Vector2 GetWindVector()
        {
            return CurrentClimate.windVector;
        }

        /// <summary>
        /// Is it currently storming?
        /// </summary>
        public bool IsStorming()
        {
            return CurrentClimate.stormCategory > 0;
        }

        /// <summary>
        /// Get active disaster types at player position.
        /// </summary>
        public List<string> GetActiveDisasters()
        {
            return CurrentClimate.activeDisasters ?? new List<string>();
        }

        /// <summary>
        /// Get season name for current hemisphere (Northern).
        /// Maps season float: >0.5=Summer, 0 to 0.5=Spring, -0.5 to 0=Autumn, less than -0.5=Winter.
        /// </summary>
        public string GetSeasonName()
        {
            return CurrentSeason.name;
        }

        /// <summary>
        /// Get visibility in km.
        /// </summary>
        public float GetVisibility()
        {
            return CurrentClimate.visibility;
        }

        /// <summary>
        /// Is it night at player position?
        /// </summary>
        public bool IsNight()
        {
            return CurrentClimate.isNight;
        }

        // --- INTERNAL ---

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize with safe defaults
            targetClimate = new ClimateData
            {
                temperature = 20f,
                humidity = 0.5f,
                pressure = 1013f,
                windVector = Vector2.zero,
                precipitationRate = 0f,
                precipitationType = "",
                cloudCover = 0.3f,
                stormCategory = 0,
                visibility = 10f,
                solarElevation = 45f,
                isNight = false,
                season = 0.25f,
                atmosphericTint = new Color(0.5f, 0.6f, 0.8f),
                biome = "temperate",
                activeDisasters = new List<string>()
            };
            CurrentClimate = targetClimate;

            currentStarState = new StarStateData
            {
                starType = "G2V",
                luminosity = 1f,
                temperature = 5778f,
                spectralColor = new Color(1f, 0.96f, 0.84f),
                uvIndex = 5f,
                hasCME = false,
                hasFlare = false,
                flareIntensity = 0f,
                activityPhase = 0f
            };

            CurrentStar = new StarInfo
            {
                type = "G2V",
                color = new Color(1f, 0.96f, 0.84f),
                luminosity = 1f,
                uvIndex = 5f,
                isFlaring = false
            };

            UpdateSeasonInfo(targetClimate.season);

            // Auto-find references if not assigned in inspector
            if (skybox == null) skybox = FindFirstObjectByType<SkyboxController>();
            if (weather == null) weather = FindFirstObjectByType<WeatherController>();
            if (director == null) director = GameDirector.Instance;
        }

        private void Update()
        {
            float delta = Time.deltaTime;

            InterpolateClimate(delta);
            UpdateSeasonInfo(CurrentClimate.season);
            UpdateWetness(delta);
            UpdateFlareBoost(delta);

            ApplyLightingFromStar();
            ApplySkyUniforms();
            ApplyWeatherEffects();
            ApplyFogSettings();
            ApplyAuroraEffect();
            ApplyDisasterVisuals();
            SetGlobalShaderUniforms();
        }

        /// <summary>
        /// Interpolate between current and target climate state for smooth transitions.
        /// </summary>
        private void InterpolateClimate(float delta)
        {
            float t = transitionSpeed * delta;
            var current = CurrentClimate;

            current.temperature = Mathf.Lerp(current.temperature, targetClimate.temperature, t);
            current.humidity = Mathf.Lerp(current.humidity, targetClimate.humidity, t);
            current.pressure = Mathf.Lerp(current.pressure, targetClimate.pressure, t);
            current.windVector = Vector2.Lerp(current.windVector, targetClimate.windVector, t);
            current.precipitationRate = Mathf.Lerp(current.precipitationRate, targetClimate.precipitationRate, t);
            current.cloudCover = Mathf.Lerp(current.cloudCover, targetClimate.cloudCover, t);
            current.visibility = Mathf.Lerp(current.visibility, targetClimate.visibility, t);
            current.solarElevation = Mathf.Lerp(current.solarElevation, targetClimate.solarElevation, t);
            current.season = Mathf.Lerp(current.season, targetClimate.season, t);
            current.atmosphericTint = Color.Lerp(current.atmosphericTint, targetClimate.atmosphericTint, t);

            // Discrete fields snap immediately
            current.precipitationType = targetClimate.precipitationType;
            current.stormCategory = targetClimate.stormCategory;
            current.isNight = targetClimate.isNight;
            current.biome = targetClimate.biome;
            current.activeDisasters = targetClimate.activeDisasters;

            CurrentClimate = current;
        }

        /// <summary>
        /// Update season info from the season float value.
        /// Northern hemisphere: >0.5=Summer, 0 to 0.5=Spring, -0.5 to 0=Autumn, less than -0.5=Winter.
        /// </summary>
        private void UpdateSeasonInfo(float season)
        {
            string name;
            float temperatureMod;
            float dayLengthMod;

            if (season > 0.5f)
            {
                name = "Summer";
                temperatureMod = 8f;
                dayLengthMod = 1.3f;
            }
            else if (season > 0f)
            {
                name = "Spring";
                temperatureMod = 3f;
                dayLengthMod = 1.1f;
            }
            else if (season > -0.5f)
            {
                name = "Autumn";
                temperatureMod = -3f;
                dayLengthMod = 0.9f;
            }
            else
            {
                name = "Winter";
                temperatureMod = -10f;
                dayLengthMod = 0.7f;
            }

            CurrentSeason = new SeasonInfo
            {
                name = name,
                progress = season,
                temperatureMod = temperatureMod,
                dayLengthMod = dayLengthMod
            };
        }

        /// <summary>
        /// Accumulate wetness during rain, decay after rain stops.
        /// </summary>
        private void UpdateWetness(float delta)
        {
            bool isRaining = CurrentClimate.precipitationType == "rain" &&
                             CurrentClimate.precipitationRate > 0.1f;

            if (isRaining)
            {
                // Accumulate: 0 -> 1 over WetnessAccumulationTime seconds
                float rate = (CurrentClimate.precipitationRate / 20f) / WetnessAccumulationTime;
                wetness = Mathf.Min(1f, wetness + rate * delta);
            }
            else
            {
                // Decay: 1 -> 0 over WetnessDecayTime seconds
                wetness = Mathf.Max(0f, wetness - (1f / WetnessDecayTime) * delta);
            }

            wasRaining = isRaining;
        }

        /// <summary>
        /// Tick down the solar flare intensity boost.
        /// </summary>
        private void UpdateFlareBoost(float delta)
        {
            if (flareBoostTimer > 0f)
            {
                flareBoostTimer -= delta;
                if (flareBoostTimer <= 0f)
                {
                    flareBoostTimer = 0f;
                    flareBoostIntensity = 0f;
                }
            }
        }

        /// <summary>
        /// Apply climate state to sky tint, darkened at night.
        /// </summary>
        private void ApplySkyUniforms()
        {
            if (skybox == null) return;

            // Compute normalized time from solar elevation for skybox
            // Solar elevation: -90 (below horizon) to +90 (zenith)
            // Map to 0-1 range: 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
            float normalizedTime = Mathf.InverseLerp(-90f, 90f, CurrentClimate.solarElevation);
            normalizedTime = CurrentClimate.isNight ? Mathf.Clamp(normalizedTime, 0f, 0.2f) : normalizedTime;

            float sunHeight = Mathf.Max(0f, Mathf.Sin(CurrentClimate.solarElevation * Mathf.Deg2Rad));

            // Sun color: star spectral shifted toward orange at low elevation, white at high
            Color baseSunColor = currentStarState.spectralColor;
            Color horizonWarm = new Color(1f, 0.5f, 0.2f);
            float absorptionFactor = 1f - Mathf.Clamp01(CurrentClimate.solarElevation / 45f);
            Color sunColor = Color.Lerp(baseSunColor, horizonWarm, absorptionFactor * 0.6f);

            // Reduce by cloud cover
            sunColor = Color.Lerp(sunColor, Color.gray, CurrentClimate.cloudCover * 0.5f);

            // Ambient: blend between star color (day) and dark blue (night), tinted by atmosphericTint
            Color dayAmbient = currentStarState.spectralColor * 0.4f;
            Color nightAmbient = new Color(0.03f, 0.04f, 0.1f);
            Color ambient = Color.Lerp(nightAmbient, dayAmbient, sunHeight);
            ambient = Color.Lerp(ambient, CurrentClimate.atmosphericTint, 0.3f);

            // Compute weather intensity from precipitation and storm
            float weatherIntensity = Mathf.Clamp01(
                CurrentClimate.precipitationRate / 30f +
                CurrentClimate.stormCategory * 0.15f);

            // Forward to SkyboxController
            skybox.OnEnvironmentUpdate(
                normalizedTime,
                sunColor.r, sunColor.g, sunColor.b,
                ambient.r, ambient.g, ambient.b,
                0f, 0f, 0f, 0f, // fog handled separately in ApplyFogSettings
                weatherIntensity);
        }

        /// <summary>
        /// Forward weather type and intensity to WeatherController.
        /// </summary>
        private void ApplyWeatherEffects()
        {
            if (weather == null) return;

            // Map climate state to WeatherController's enum
            // WeatherController.WeatherType: 0=Clear, 1=Cloudy, 2=Rain, 3=Storm, 4=Fog, 5=Snow
            int weatherType;
            float intensity;

            if (CurrentClimate.stormCategory >= 3)
            {
                weatherType = 3; // Storm
                intensity = Mathf.Clamp01(CurrentClimate.stormCategory / 5f);
            }
            else if (CurrentClimate.precipitationType == "snow" && CurrentClimate.precipitationRate > 0.1f)
            {
                weatherType = 5; // Snow
                intensity = Mathf.Clamp01(CurrentClimate.precipitationRate / 20f);
            }
            else if (CurrentClimate.precipitationType == "rain" && CurrentClimate.precipitationRate > 0.1f)
            {
                if (CurrentClimate.stormCategory > 0)
                {
                    weatherType = 3; // Storm
                    intensity = Mathf.Clamp01(CurrentClimate.precipitationRate / 30f +
                                              CurrentClimate.stormCategory * 0.2f);
                }
                else
                {
                    weatherType = 2; // Rain
                    intensity = Mathf.Clamp01(CurrentClimate.precipitationRate / 20f);
                }
            }
            else if (CurrentClimate.visibility < 2f)
            {
                weatherType = 4; // Fog
                intensity = Mathf.Clamp01(1f - CurrentClimate.visibility / 2f);
            }
            else if (CurrentClimate.cloudCover > 0.6f)
            {
                weatherType = 1; // Cloudy
                intensity = Mathf.Clamp01(CurrentClimate.cloudCover);
            }
            else
            {
                weatherType = 0; // Clear
                intensity = 0f;
            }

            // Wind direction in radians from vector
            float windDir = Mathf.Atan2(CurrentClimate.windVector.y, CurrentClimate.windVector.x);
            float windSpd = CurrentClimate.windVector.magnitude;

            weather.OnEnvironmentUpdate(weatherType, intensity, windDir, windSpd);
        }

        /// <summary>
        /// Apply fog from visibility and atmospheric tint.
        /// </summary>
        private void ApplyFogSettings()
        {
            // fogDensity = 1 / (visibility_in_meters)
            float visibilityMeters = Mathf.Max(0.01f, CurrentClimate.visibility * 1000f);
            float fogDensity = 1f / visibilityMeters;

            // Fog color: atmospheric tint blended with grey cloud color
            Color cloudGrey = new Color(0.6f, 0.62f, 0.65f);
            Color fogColor = Color.Lerp(CurrentClimate.atmosphericTint, cloudGrey, CurrentClimate.cloudCover * 0.5f);

            // Darken fog at night
            if (CurrentClimate.isNight)
            {
                fogColor *= 0.15f;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
        }

        /// <summary>
        /// Apply sun and moon light direction, color, and intensity from star and climate data.
        /// </summary>
        private void ApplyLightingFromStar()
        {
            // --- Sun ---
            if (sunLight != null)
            {
                // Direction from solar elevation + time-of-day azimuth
                float elevationRad = CurrentClimate.solarElevation * Mathf.Deg2Rad;
                // Azimuth: use normalized time from director if available, else derive from elevation
                float azimuth = 170f; // Default south-ish
                if (director != null)
                {
                    azimuth = director.NormalizedTime * 360f;
                }

                Vector3 sunDir = new Vector3(
                    Mathf.Cos(elevationRad) * Mathf.Sin(azimuth * Mathf.Deg2Rad),
                    Mathf.Sin(elevationRad),
                    Mathf.Cos(elevationRad) * Mathf.Cos(azimuth * Mathf.Deg2Rad));
                sunLight.transform.rotation = Quaternion.LookRotation(-sunDir);

                // Color: star spectral x atmospheric absorption (orange at low, white at high)
                float absorptionFactor = 1f - Mathf.Clamp01(CurrentClimate.solarElevation / 45f);
                Color horizonWarm = new Color(1f, 0.5f, 0.2f);
                Color sunColor = Color.Lerp(currentStarState.spectralColor, horizonWarm, absorptionFactor * 0.6f);
                sunLight.color = sunColor;

                // Intensity: 0 at night, ramps with solarElevation, reduced by cloud and weather
                float baseIntensity;
                if (CurrentClimate.isNight)
                {
                    baseIntensity = 0f;
                }
                else
                {
                    baseIntensity = Mathf.Clamp01(CurrentClimate.solarElevation / 60f);
                }

                // Apply star luminosity
                baseIntensity *= currentStarState.luminosity;

                // Reduce by cloud cover
                baseIntensity *= (1f - CurrentClimate.cloudCover * 0.6f);

                // Reduce by weather/precipitation
                float weatherReduction = Mathf.Clamp01(CurrentClimate.precipitationRate / 30f);
                baseIntensity *= (1f - weatherReduction * 0.4f);

                // Solar flare boost
                if (flareBoostTimer > 0f)
                {
                    baseIntensity *= (1f + flareBoostIntensity);
                }

                sunLight.intensity = baseIntensity;
                sunLight.enabled = !CurrentClimate.isNight;
            }

            // --- Moon ---
            if (moonLight != null)
            {
                moonLight.enabled = CurrentClimate.isNight;
                if (CurrentClimate.isNight)
                {
                    moonLight.color = new Color(0.6f, 0.65f, 0.9f);
                    moonLight.intensity = MoonIntensity;

                    // Moon roughly opposite the sun
                    float moonElev = Mathf.Abs(CurrentClimate.solarElevation + 30f);
                    moonLight.transform.rotation = Quaternion.Euler(moonElev, 350f, 0f);
                }
            }

            // --- Ambient ---
            float sunHeight = Mathf.Max(0f, Mathf.Sin(CurrentClimate.solarElevation * Mathf.Deg2Rad));
            Color dayAmbient = currentStarState.spectralColor * 0.35f;
            Color nightAmbient = new Color(0.03f, 0.04f, 0.1f);
            Color ambient = Color.Lerp(nightAmbient, dayAmbient, sunHeight);
            ambient = Color.Lerp(ambient, CurrentClimate.atmosphericTint, 0.25f);

            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        /// <summary>
        /// Apply aurora effect driven by CME/flare events and latitude.
        /// </summary>
        private void ApplyAuroraEffect()
        {
            // Aurora intensity: stronger with CME, solar flares, and at polar latitudes
            // We don't have player latitude directly, so use a base intensity from star state
            float auroraBase = 0f;

            if (currentStarState.hasCME)
            {
                // CME: aurora visible at all latitudes (solar storm disaster)
                auroraBase = 0.6f;
            }

            if (currentStarState.hasFlare)
            {
                auroraBase = Mathf.Max(auroraBase, currentStarState.flareIntensity * 0.4f);
            }

            // Check for solar storm in active disasters (aurora at all latitudes)
            if (CurrentClimate.activeDisasters != null && CurrentClimate.activeDisasters.Contains("solar_storm"))
            {
                auroraBase = Mathf.Max(auroraBase, 0.8f);
            }

            // Only visible at night
            if (!CurrentClimate.isNight)
            {
                auroraBase *= 0.05f; // Barely perceptible during day
            }

            Shader.SetGlobalFloat(ShaderAuroraIntensity, auroraBase);
        }

        /// <summary>
        /// Apply visual effects for each active disaster type.
        /// </summary>
        private void ApplyDisasterVisuals()
        {
            var disasters = CurrentClimate.activeDisasters;
            float ashDensity = 0f;
            EarthquakeActive = false;
            EarthquakeIntensity = 0f;

            if (disasters == null || disasters.Count == 0)
            {
                Shader.SetGlobalFloat(ShaderAshDensity, 0f);
                return;
            }

            foreach (string disaster in disasters)
            {
                switch (disaster)
                {
                    case "volcanic":
                        // Orange-red fog, ash particles
                        ashDensity = 0.7f;
                        // Override fog color toward volcanic orange
                        RenderSettings.fogColor = Color.Lerp(
                            RenderSettings.fogColor,
                            new Color(0.6f, 0.25f, 0.1f),
                            0.5f);
                        RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.005f);
                        // Audio trigger: rumble
                        Orlo.Audio.AudioManager.Instance?.PlaySoundAt(
                            "volcanic_rumble", transform.position, 0.8f, 500f);
                        break;

                    case "earthquake":
                        // Camera shake flag for PlayerController to read
                        EarthquakeActive = true;
                        EarthquakeIntensity = 0.6f;
                        break;

                    case "wildfire":
                        // Smoke haze: ash density + orange overlay
                        ashDensity = Mathf.Max(ashDensity, 0.4f);
                        RenderSettings.fogColor = Color.Lerp(
                            RenderSettings.fogColor,
                            new Color(0.5f, 0.35f, 0.15f),
                            0.4f);
                        RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.003f);
                        break;

                    case "flood":
                        // Raise water plane in affected areas
                        var waterPlane = FindFirstObjectByType<WaterPlane>();
                        if (waterPlane != null)
                        {
                            // Shift water up by 2 meters during flood
                            waterPlane.transform.position = new Vector3(
                                waterPlane.transform.position.x,
                                Mathf.Lerp(waterPlane.transform.position.y,
                                    waterPlane.transform.position.y + 2f,
                                    Time.deltaTime * 0.5f),
                                waterPlane.transform.position.z);
                        }
                        break;

                    case "solar_storm":
                        // Aurora at all latitudes handled in ApplyAuroraEffect
                        // Bright sky colors
                        RenderSettings.ambientLight = Color.Lerp(
                            RenderSettings.ambientLight,
                            new Color(0.15f, 0.4f, 0.25f),
                            0.3f);
                        break;

                    case "meteor_impact":
                        // Bright flash + camera shake
                        EarthquakeActive = true;
                        EarthquakeIntensity = Mathf.Max(EarthquakeIntensity, 0.9f);
                        // Brief sun intensity spike for the flash
                        if (sunLight != null)
                        {
                            sunLight.intensity = Mathf.Max(sunLight.intensity, 3f);
                        }
                        break;
                }
            }

            Shader.SetGlobalFloat(ShaderAshDensity, ashDensity);
        }

        /// <summary>
        /// Set all global shader properties for all materials.
        /// Called once per frame after all climate calculations are complete.
        /// </summary>
        private void SetGlobalShaderUniforms()
        {
            var climate = CurrentClimate;

            // Sky tint: atmospheric tint, darkened at night
            Color skyTint = climate.atmosphericTint;
            if (climate.isNight)
            {
                skyTint *= 0.1f;
                skyTint.a = 1f;
            }
            Shader.SetGlobalColor(ShaderSkyTint, skyTint);

            // Sun color: from star spectral color attenuated by cloud cover
            Color sunColor = currentStarState.spectralColor;
            sunColor = Color.Lerp(sunColor, Color.gray, climate.cloudCover * 0.4f);
            Shader.SetGlobalColor(ShaderSunColor, sunColor);

            // Sun intensity: star luminosity x elevation x cloud x weather
            float sunIntensity = 0f;
            if (!climate.isNight)
            {
                sunIntensity = Mathf.Clamp01(climate.solarElevation / 60f)
                    * currentStarState.luminosity
                    * (1f - climate.cloudCover * 0.6f)
                    * (1f - Mathf.Clamp01(climate.precipitationRate / 30f) * 0.4f);

                if (flareBoostTimer > 0f)
                {
                    sunIntensity *= (1f + flareBoostIntensity);
                }
            }
            Shader.SetGlobalFloat(ShaderSunIntensity, sunIntensity);

            // Sun direction: computed from solarElevation and azimuth
            float elevRad = climate.solarElevation * Mathf.Deg2Rad;
            float azimuthRad = 0f;
            if (director != null)
            {
                azimuthRad = director.NormalizedTime * Mathf.PI * 2f;
            }
            Vector4 sunDirection = new Vector4(
                Mathf.Cos(elevRad) * Mathf.Sin(azimuthRad),
                Mathf.Sin(elevRad),
                Mathf.Cos(elevRad) * Mathf.Cos(azimuthRad),
                0f);
            Shader.SetGlobalVector(ShaderSunDirection, sunDirection);

            // Fog density: from visibility
            float fogDensity = 1f / Mathf.Max(0.01f, climate.visibility * 1000f);
            Shader.SetGlobalFloat(ShaderFogDensity, fogDensity);

            // Fog color: atmospheric tint blended with cloud grey
            Color cloudGrey = new Color(0.6f, 0.62f, 0.65f);
            Color fogColor = Color.Lerp(climate.atmosphericTint, cloudGrey, climate.cloudCover * 0.5f);
            if (climate.isNight) { fogColor *= 0.15f; fogColor.a = 1f; }
            Shader.SetGlobalColor(ShaderFogColor, fogColor);

            // Rain intensity
            float rainIntensity = (climate.precipitationType == "rain")
                ? Mathf.Clamp01(climate.precipitationRate / 30f)
                : 0f;
            Shader.SetGlobalFloat(ShaderRainIntensity, rainIntensity);

            // Snow intensity
            float snowIntensity = (climate.precipitationType == "snow")
                ? Mathf.Clamp01(climate.precipitationRate / 20f)
                : 0f;
            Shader.SetGlobalFloat(ShaderSnowIntensity, snowIntensity);

            // Wind
            float windStrength = climate.windVector.magnitude;
            Shader.SetGlobalFloat(ShaderWindStrength, windStrength);

            Vector2 windNorm = climate.windVector.magnitude > 0.001f
                ? climate.windVector.normalized
                : Vector2.zero;
            Shader.SetGlobalVector(ShaderWindDirection, new Vector4(windNorm.x, windNorm.y, 0f, 0f));

            // Wetness
            Shader.SetGlobalFloat(ShaderWetness, wetness);

            // Heat haze: when temperature > 35 and humidity < 0.3
            float heatHaze = 0f;
            if (climate.temperature > HeatHazeTemperatureThreshold && climate.humidity < HeatHazeHumidityThreshold)
            {
                float tempFactor = Mathf.Clamp01((climate.temperature - HeatHazeTemperatureThreshold) / 15f);
                float humidFactor = Mathf.Clamp01((HeatHazeHumidityThreshold - climate.humidity) / HeatHazeHumidityThreshold);
                heatHaze = tempFactor * humidFactor;
            }
            Shader.SetGlobalFloat(ShaderHeatHaze, heatHaze);

            // Lens frost: when temperature < -10
            float lensFrost = 0f;
            if (climate.temperature < LensFrostTemperatureThreshold)
            {
                lensFrost = Mathf.Clamp01((LensFrostTemperatureThreshold - climate.temperature) / 20f);
            }
            Shader.SetGlobalFloat(ShaderLensFrost, lensFrost);

            // Aurora intensity is set in ApplyAuroraEffect
            // Ash density is set in ApplyDisasterVisuals
        }
    }

    [Serializable]
    public struct ClimatePacketData
    {
        public int tileId;
        public float lat, lon;
        public string biome;
        public float temperature;       // Celsius
        public float pressure;          // hPa
        public float humidity;          // 0-1
        public Vector2 windVector;      // m/s
        public float precipitationRate; // mm/hour
        public string precipitationType;
        public float cloudCover;        // 0-1
        public int stormCategory;       // 0-5
        public string[] activeDisasters;
        public bool lightningStrike;
        public float visibility;        // km
        public float solarElevation;    // degrees
        public bool isNight;
        public float season;            // -1 to 1
        public Color atmosphericTint;
        public float ecosystemHealth;
        public float vegetationDensity;
    }

    [Serializable]
    public struct StarStateData
    {
        public string starType;
        public float luminosity;
        public float temperature;
        public Color spectralColor;
        public float uvIndex;
        public bool hasCME;
        public bool hasFlare;
        public float flareIntensity;
        public float activityPhase;
    }

    [Serializable]
    public struct ClimateData
    {
        public float temperature;
        public float humidity;
        public float pressure;
        public Vector2 windVector;
        public float precipitationRate;
        public string precipitationType;
        public float cloudCover;
        public int stormCategory;
        public float visibility;
        public float solarElevation;
        public bool isNight;
        public float season;
        public Color atmosphericTint;
        public string biome;
        public List<string> activeDisasters;
    }

    [Serializable]
    public struct SeasonInfo
    {
        public string name;          // "Spring", "Summer", "Autumn", "Winter"
        public float progress;       // -1 to 1
        public float temperatureMod; // modifier on base temp
        public float dayLengthMod;   // modifier on day length
    }

    [Serializable]
    public struct StarInfo
    {
        public string type;
        public Color color;
        public float luminosity;
        public float uvIndex;
        public bool isFlaring;
    }
}

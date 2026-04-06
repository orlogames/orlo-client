using UnityEngine;
using System.Collections.Generic;

namespace Orlo.World
{
    /// <summary>
    /// Client-side weather particle effects driven by server environment state.
    /// Creates rain, snow, storm, and fog particle systems dynamically.
    /// </summary>
    public class WeatherController : MonoBehaviour
    {
        private enum WeatherType { Clear, Cloudy, Rain, Storm, Fog, Snow }

        private WeatherType currentWeather = WeatherType.Clear;
        private float intensity = 0f;
        private float windDirection = 0f;
        private float windSpeed = 2f;

        // Particle systems (created on demand)
        private ParticleSystem rainSystem;
        private ParticleSystem snowSystem;
        private ParticleSystem stormSystem;
        private ParticleSystem fogSystem;

        private Transform playerTransform;

        private void Start()
        {
            CreateRainSystem();
            CreateSnowSystem();
            CreateStormSystem();
            CreateFogSystem();
        }

        /// <summary>
        /// Called when server sends EnvironmentUpdate
        /// </summary>
        public void OnEnvironmentUpdate(int weatherType, float weatherIntensity,
                                         float windDir, float windSpd)
        {
            currentWeather = (WeatherType)weatherType;
            intensity = Mathf.Clamp01(weatherIntensity);
            windDirection = windDir;
            windSpeed = windSpd;
        }

        public void SetPlayerTransform(Transform t) => playerTransform = t;

        private void Update()
        {
            // Follow the player
            if (playerTransform != null)
            {
                transform.position = playerTransform.position + Vector3.up * 30f;
            }

            Vector3 windVec = new Vector3(
                Mathf.Cos(windDirection) * windSpeed,
                0f,
                Mathf.Sin(windDirection) * windSpeed);

            UpdateRain(windVec);
            UpdateSnow(windVec);
            UpdateStorm(windVec);
            UpdateFog();
        }

        // --- Public API for CloudRenderer integration ---

        /// <summary>
        /// Recommended cloud density for the current weather state (0-1).
        /// Clear = 0.1, Cloudy = 0.6, Rain = 0.8, Storm = 1.0, Fog = 0.5, Snow = 0.7.
        /// </summary>
        public float CloudDensity
        {
            get
            {
                switch (currentWeather)
                {
                    case WeatherType.Clear:  return 0.1f + intensity * 0.15f;
                    case WeatherType.Cloudy: return 0.4f + intensity * 0.4f;
                    case WeatherType.Rain:   return 0.7f + intensity * 0.3f;
                    case WeatherType.Storm:  return 0.85f + intensity * 0.15f;
                    case WeatherType.Fog:    return 0.3f + intensity * 0.3f;
                    case WeatherType.Snow:   return 0.5f + intensity * 0.3f;
                    default:                 return 0.2f;
                }
            }
        }

        /// <summary>Current wind direction in radians.</summary>
        public float WindDirection => windDirection;

        /// <summary>Current wind speed.</summary>
        public float WindSpeed => windSpeed;

        /// <summary>Whether the current weather produces overcast conditions (suppress god rays).</summary>
        public bool IsOvercast => currentWeather == WeatherType.Storm ||
                                  (currentWeather == WeatherType.Rain && intensity > 0.7f);

        private void UpdateRain(Vector3 wind)
        {
            if (rainSystem == null) return;
            bool active = currentWeather == WeatherType.Rain || currentWeather == WeatherType.Storm;

            var emission = rainSystem.emission;
            emission.rateOverTime = active ? intensity * 3000f : 0f;

            var velocity = rainSystem.velocityOverLifetime;
            velocity.x = wind.x * 0.3f;
            velocity.z = wind.z * 0.3f;

            if (active && !rainSystem.isPlaying) rainSystem.Play();
            else if (!active && rainSystem.isPlaying && rainSystem.particleCount == 0) rainSystem.Stop();
        }

        private void UpdateSnow(Vector3 wind)
        {
            if (snowSystem == null) return;
            bool active = currentWeather == WeatherType.Snow;

            var emission = snowSystem.emission;
            emission.rateOverTime = active ? intensity * 800f : 0f;

            var velocity = snowSystem.velocityOverLifetime;
            velocity.x = wind.x * 0.5f;
            velocity.z = wind.z * 0.5f;

            if (active && !snowSystem.isPlaying) snowSystem.Play();
            else if (!active && snowSystem.isPlaying && snowSystem.particleCount == 0) snowSystem.Stop();
        }

        private void UpdateStorm(Vector3 wind)
        {
            if (stormSystem == null) return;
            bool active = currentWeather == WeatherType.Storm && intensity > 0.7f;

            var emission = stormSystem.emission;
            emission.rateOverTime = active ? 5f : 0f; // Lightning flashes

            if (active && !stormSystem.isPlaying) stormSystem.Play();
            else if (!active && stormSystem.isPlaying) stormSystem.Stop();
        }

        private void UpdateFog()
        {
            if (fogSystem == null) return;
            bool active = currentWeather == WeatherType.Fog;

            var emission = fogSystem.emission;
            emission.rateOverTime = active ? intensity * 50f : 0f;

            if (active && !fogSystem.isPlaying) fogSystem.Play();
            else if (!active && fogSystem.isPlaying && fogSystem.particleCount == 0) fogSystem.Stop();
        }

        private void CreateRainSystem()
        {
            var go = new GameObject("RainParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            rainSystem = go.AddComponent<ParticleSystem>();
            rainSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rainSystem.main;
            main.loop = true;
            main.startLifetime = 1.5f;
            main.startSpeed = 25f;
            main.startSize = 0.03f;
            main.maxParticles = 5000;
            main.startColor = new Color(0.7f, 0.75f, 0.85f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.5f;

            var shape = rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 0f, 60f);

            var emission = rainSystem.emission;
            emission.rateOverTime = 0f;

            var velocity = rainSystem.velocityOverLifetime;
            velocity.enabled = true;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 8f;
        }

        private void CreateSnowSystem()
        {
            var go = new GameObject("SnowParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            snowSystem = go.AddComponent<ParticleSystem>();
            snowSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = snowSystem.main;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSpeed = 1.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.maxParticles = 3000;
            main.startColor = new Color(0.95f, 0.95f, 1f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.1f;

            var shape = snowSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 0f, 60f);

            var emission = snowSystem.emission;
            emission.rateOverTime = 0f;

            var velocity = snowSystem.velocityOverLifetime;
            velocity.enabled = true;

            var noise = snowSystem.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.5f;
        }

        private void CreateStormSystem()
        {
            var go = new GameObject("StormParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            stormSystem = go.AddComponent<ParticleSystem>();
            stormSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = stormSystem.main;
            main.loop = true;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = 200f;
            main.maxParticles = 3;
            main.startColor = new Color(0.9f, 0.9f, 1f, 0.3f);

            var shape = stormSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 100f;

            var emission = stormSystem.emission;
            emission.rateOverTime = 0f;

            var colorOverLifetime = stormSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.1f),
                    new GradientColorKey(Color.black, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.8f, 0.05f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
        }

        private void CreateFogSystem()
        {
            var go = new GameObject("FogParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0, -10f, 0);
            fogSystem = go.AddComponent<ParticleSystem>();
            fogSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = fogSystem.main;
            main.loop = true;
            main.startLifetime = 10f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 25f);
            main.maxParticles = 100;
            main.startColor = new Color(0.8f, 0.8f, 0.85f, 0.15f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = fogSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80f, 5f, 80f);

            var emission = fogSystem.emission;
            emission.rateOverTime = 0f;

            var sizeOverLifetime = fogSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.5f)));
        }
    }
}

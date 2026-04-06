using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Renders a moon with orbital motion, phase cycle, and subtle glow.
    /// The moon orbits the sky dome on a ~30 game-day lunar cycle.
    /// Phase is computed from the angle between moon and sun positions.
    /// </summary>
    public class MoonRenderer : MonoBehaviour
    {
        // ----- Configuration -----
        private const float DomeRadius = 4800f;        // Slightly inside star dome
        private const float MoonAngularSize = 2.0f;    // Degrees subtended
        private const float MoonQuadSize = DomeRadius * Mathf.Deg2Rad * MoonAngularSize;
        private const float GlowMultiplier = 2.5f;     // Glow extends this much beyond disc
        private const float LunarCycleDays = 30f;      // Game-days per full lunar cycle
        private const float MoonLightIntensity = 0.15f;

        // ----- Runtime state -----
        private Material moonMaterial;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Light moonLight;

        private float timeOfDay;       // 0-1, set by SkyboxController
        private float dayCount;        // Accumulated game days for lunar cycle
        private float nightFactor;     // 0=day, 1=night

        // Shader property IDs
        private static readonly int PropPhase = Shader.PropertyToID("_Phase");
        private static readonly int PropNightFactor = Shader.PropertyToID("_NightFactor");
        private static readonly int PropGlowSize = Shader.PropertyToID("_GlowSize");

        // ===== Lifecycle =====

        public void Initialize(Light sunLight)
        {
            CreateMoonQuad();
            CreateMaterial();
            CreateMoonLight();
        }

        private void LateUpdate()
        {
            if (moonMaterial == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // Accumulate day count from timeOfDay changes
            // (In a real implementation this would come from server time)
            dayCount += Time.deltaTime / 600f; // Assume ~10 min real = 1 game day for testing

            // Moon orbital position: offset from sun by lunar phase angle
            float lunarPhaseAngle = (dayCount / LunarCycleDays) * 360f;
            float moonAzimuth = timeOfDay * 360f + lunarPhaseAngle + 180f; // Opposite-ish to sun
            float moonAltitude = 30f + 35f * Mathf.Sin((timeOfDay + dayCount / LunarCycleDays) * Mathf.PI * 2f);
            moonAltitude = Mathf.Clamp(moonAltitude, -10f, 75f);

            // Convert to world direction
            float az = moonAzimuth * Mathf.Deg2Rad;
            float alt = moonAltitude * Mathf.Deg2Rad;
            Vector3 moonDir = new Vector3(
                Mathf.Sin(az) * Mathf.Cos(alt),
                Mathf.Sin(alt),
                Mathf.Cos(az) * Mathf.Cos(alt)
            ).normalized;

            // Position moon on dome, centered on camera
            transform.position = cam.transform.position + moonDir * DomeRadius;

            // Billboard: always face camera
            transform.LookAt(cam.transform.position);
            transform.Rotate(0f, 180f, 0f); // Flip to face inward

            // Compute phase from sun-moon angle
            // Sun direction from timeOfDay
            float sunAngle = timeOfDay * 360f - 90f;
            Vector3 sunDir = Quaternion.Euler(sunAngle, 170f, 0f) * Vector3.forward;
            float phaseDot = Vector3.Dot(moonDir, sunDir.normalized);
            // Map: facing sun = full moon (+1), facing away = new moon (-1)
            float phase = -phaseDot; // Invert: when moon faces sun, it is fully illuminated from our view

            moonMaterial.SetFloat(PropPhase, phase);
            moonMaterial.SetFloat(PropNightFactor, nightFactor);

            // Moon light: only at night, intensity based on phase and altitude
            if (moonLight != null)
            {
                float aboveHorizon = Mathf.Clamp01(moonAltitude / 20f);
                float phaseLight = Mathf.Clamp01((phase + 1f) * 0.5f); // 0 at new, 1 at full
                moonLight.intensity = MoonLightIntensity * nightFactor * phaseLight * aboveHorizon;
                moonLight.transform.rotation = Quaternion.LookRotation(-moonDir);
                moonLight.enabled = moonLight.intensity > 0.01f;
            }

            // Hide when below horizon
            bool visible = moonAltitude > -5f;
            meshRenderer.enabled = visible;
        }

        /// <summary>
        /// Called by SkyboxController each frame.
        /// </summary>
        public void SetTimeOfDay(float time)
        {
            timeOfDay = time;
        }

        /// <summary>
        /// Called by SkyboxController each frame with current night factor.
        /// </summary>
        public void SetNightFactor(float factor)
        {
            nightFactor = factor;
        }

        // ===== Setup =====

        private void CreateMoonQuad()
        {
            var mesh = new Mesh();
            mesh.name = "MoonQuad";

            float half = MoonQuadSize * GlowMultiplier; // Larger for glow
            mesh.vertices = new Vector3[]
            {
                new Vector3(-half, -half, 0f),
                new Vector3( half, -half, 0f),
                new Vector3( half,  half, 0f),
                new Vector3(-half,  half, 0f),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * half * 2f);

            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private void CreateMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/OrloMoon");
            if (shader == null) shader = Shader.Find("Orlo/Moon");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
                Debug.LogWarning("[Orlo] Moon shader not found in Resources/Shaders/OrloMoon, using fallback");
            }

            moonMaterial = new Material(shader);
            moonMaterial.renderQueue = 2901; // After stars, before transparent geometry
            meshRenderer.sharedMaterial = moonMaterial;
        }

        private void CreateMoonLight()
        {
            var lightGo = new GameObject("MoonLight");
            lightGo.transform.SetParent(transform.parent); // Sibling, not child
            moonLight = lightGo.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.6f, 0.65f, 0.8f); // Cool blue-silver
            moonLight.intensity = 0f;
            moonLight.shadows = LightShadows.None; // Soft shadows too expensive for secondary light
            moonLight.enabled = false;
        }
    }
}

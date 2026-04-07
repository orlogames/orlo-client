using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Sets up enhanced lighting for settlement areas — reflection probes, light cookies,
    /// ambient point lights, and atmospheric effects that make the settlement feel alive.
    /// Called by GameBootstrap after world systems initialize.
    /// </summary>
    public class SettlementLighting : MonoBehaviour
    {
        private static SettlementLighting _instance;
        public static SettlementLighting Instance => _instance;

        private void Awake()
        {
            _instance = this;
        }

        /// <summary>
        /// Initialize settlement lighting at a given world position.
        /// Call this when the player enters a settlement area.
        /// </summary>
        public void SetupSettlementLighting(Vector3 settlementCenter)
        {
            SetupReflectionProbes(settlementCenter);
            SetupCanopyLightCookie();
            SetupAmbientLights(settlementCenter);
            Debug.Log($"[SettlementLighting] Enhanced lighting active at {settlementCenter}");
        }

        private void SetupReflectionProbes(Vector3 center)
        {
            // Main fountain/nexus reflection probe
            CreateReflectionProbe("NexusProbe", center + Vector3.up * 2f,
                new Vector3(30f, 10f, 30f), 1f);

            // Settlement area wide probe (lower priority, larger area)
            CreateReflectionProbe("SettlementProbe", center + Vector3.up * 5f,
                new Vector3(100f, 20f, 100f), 0.5f);
        }

        private void CreateReflectionProbe(string name, Vector3 position, Vector3 size, float importance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = position;

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = size;
            probe.resolution = 256;
            probe.boxProjection = true;
            probe.importance = Mathf.RoundToInt(importance * 100);
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 200f;

            // Warm tint for golden hour reflections
            probe.backgroundColor = new Color(0.4f, 0.35f, 0.25f);

            Debug.Log($"[SettlementLighting] Created {name}: size={size}, pos={position}");
        }

        private void SetupCanopyLightCookie()
        {
            // Find the main directional light (sun) and apply canopy cookie
            var sun = FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                LightCookieGenerator.ApplyToDirectionalLight(sun);
            }
        }

        private void SetupAmbientLights(Vector3 center)
        {
            // Create warm fill lights at settlement edges for softer shadows
            CreateFillLight("FillNorth", center + new Vector3(0, 8, 40), 0.3f);
            CreateFillLight("FillSouth", center + new Vector3(0, 8, -40), 0.25f);
            CreateFillLight("FillEast", center + new Vector3(40, 8, 0), 0.2f);
        }

        private void CreateFillLight(string name, Vector3 position, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.88f, 0.7f); // Warm amber bounce
            light.intensity = intensity;
            light.range = 60f;
            light.shadows = LightShadows.None; // Fill lights don't cast shadows
        }

        /// <summary>
        /// Clean up lighting when leaving settlement.
        /// </summary>
        public void CleanupSettlementLighting()
        {
            // Remove reflection probes and fill lights
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // Remove canopy cookie from sun
            var sun = FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.cookie = null;
            }
        }
    }
}

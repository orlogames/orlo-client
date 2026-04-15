using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Fake volumetric god rays using oriented particle billboard strips.
    /// Creates visible light shafts by spawning elongated additive particles
    /// in the direction of the sun. Cheap approximation of volumetric lighting.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GodRaysEffect : MonoBehaviour
    {
        [Header("Ray Settings")]
        [SerializeField] private int rayCount = 24;
        [SerializeField] private float rayLength = 30f;
        [SerializeField] private float rayWidth = 1.5f;
        [SerializeField] private float rayIntensity = 0.4f;
        [SerializeField] private float raySpread = 25f;

        private ParticleSystem _raySystem;
        private Light _sun;
        private float _godRayFactor = 1f;
        private bool _sunAboveHorizon = true;

        public void SetCloudFactor(float factor) { _godRayFactor = Mathf.Clamp01(1f - factor * 0.5f); }
        public void SetGodRayFactor(float factor) { _godRayFactor = factor; }
        public void SetSunAboveHorizon(bool above) { _sunAboveHorizon = above; }

        private void Start()
        {
            _sun = FindSun();
            if (_sun == null) return;

            CreateRayParticleSystem();
        }

        private Light FindSun()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                    return light;
            }
            return null;
        }

        private void CreateRayParticleSystem()
        {
            var go = new GameObject("GodRayParticles");
            go.transform.SetParent(transform, false);

            _raySystem = go.AddComponent<ParticleSystem>();
            var main = _raySystem.main;
            main.maxParticles = rayCount;
            main.startLifetime = 8f;
            main.startSpeed = 0f;
            main.startSize = rayWidth;
            main.startColor = new Color(1f, 0.9f, 0.7f, 0.08f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = true;

            var emission = _raySystem.emission;
            emission.rateOverTime = rayCount / 4f;

            var shape = _raySystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = raySpread;

            // Stretch particles along sun direction to create shaft effect
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = rayLength;
            renderer.velocityScale = 0f;

            // Use additive particle material
            var shader = OrloShaders.ParticlesUnlit;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[GodRaysEffect] No particle shader available; god rays disabled");
                enabled = false;
                return;
            }
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.75f, 0.06f));

            // Generate soft circle texture for particles
            var tex = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                d = d * d; // Quadratic falloff
                tex.SetPixel(x, y, new Color(1, 1, 1, d));
            }
            tex.Apply();
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;

            // Additive blending
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3100;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            renderer.material = mat;
        }

        private void LateUpdate()
        {
            if (_raySystem == null || _sun == null) return;

            // Position rays around camera, oriented along sun direction
            var cam = GetComponent<Camera>();
            Vector3 sunDir = -_sun.transform.forward;
            Vector3 camPos = cam.transform.position;

            // Place particle system in front of camera along sun direction
            _raySystem.transform.position = camPos + sunDir * 15f + Vector3.up * 10f;
            _raySystem.transform.rotation = Quaternion.LookRotation(sunDir);

            // Adjust intensity based on god ray factor and sun position
            float intensity = _sunAboveHorizon ? _godRayFactor * rayIntensity : 0f;
            var main = _raySystem.main;
            main.startColor = new Color(1f, 0.9f, 0.7f, intensity * 0.08f);
        }
    }
}

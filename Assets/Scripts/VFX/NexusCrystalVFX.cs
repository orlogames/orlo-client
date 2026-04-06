using UnityEngine;

namespace Orlo.VFX
{
    /// <summary>
    /// Visual effects for the nexus crystal fountain.
    /// Creates pulsing pink crystal glow, steady teal base glow,
    /// and upward-drifting teal-to-pink mote particles.
    /// Attach to the root GameObject of a nexus_crystal_fountain entity.
    /// </summary>
    public class NexusCrystalVFX : MonoBehaviour
    {
        [Header("Crystal Glow")]
        [SerializeField] private Color crystalColor = new Color(1.0f, 0.2f, 0.6f);
        [SerializeField] private float crystalIntensityMin = 1.5f;
        [SerializeField] private float crystalIntensityMax = 2.5f;
        [SerializeField] private float crystalPulsePeriod = 3f;
        [SerializeField] private float crystalRange = 10f;

        [Header("Base Glow")]
        [SerializeField] private Color baseColor = new Color(0.0f, 0.8f, 0.8f);
        [SerializeField] private float baseIntensity = 1.5f;
        [SerializeField] private float baseRange = 6f;
        [SerializeField] private float baseYOffset = 0.2f;

        [Header("Crystal Motes")]
        [SerializeField] private float moteEmissionRate = 5f;
        [SerializeField] private float moteLifetime = 3f;
        [SerializeField] private float moteRiseSpeed = 0.3f;
        [SerializeField] private float moteSizeMin = 0.02f;
        [SerializeField] private float moteSizeMax = 0.04f;

        private Light _crystalLight;
        private Light _baseLight;
        private ParticleSystem _moteParticles;

        private void Start()
        {
            CreateCrystalLight();
            CreateBaseLight();
            CreateMoteParticles();
        }

        private void Update()
        {
            // Pulse the crystal light intensity on a sine wave
            if (_crystalLight != null)
            {
                float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / crystalPulsePeriod) + 1f) * 0.5f;
                _crystalLight.intensity = Mathf.Lerp(crystalIntensityMin, crystalIntensityMax, t);
            }
        }

        private void CreateCrystalLight()
        {
            var go = new GameObject("CrystalGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.5f, 0f); // mid-crystal height

            _crystalLight = go.AddComponent<Light>();
            _crystalLight.type = LightType.Point;
            _crystalLight.color = crystalColor;
            _crystalLight.intensity = crystalIntensityMax;
            _crystalLight.range = crystalRange;
            _crystalLight.shadows = LightShadows.None;
        }

        private void CreateBaseLight()
        {
            var go = new GameObject("BaseGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, baseYOffset, 0f);

            _baseLight = go.AddComponent<Light>();
            _baseLight.type = LightType.Point;
            _baseLight.color = baseColor;
            _baseLight.intensity = baseIntensity;
            _baseLight.range = baseRange;
            _baseLight.shadows = LightShadows.None;
        }

        private void CreateMoteParticles()
        {
            var go = new GameObject("CrystalMotes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            _moteParticles = go.AddComponent<ParticleSystem>();

            // Stop the auto-play so we can configure before it emits
            _moteParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _moteParticles.main;
            main.startLifetime = moteLifetime;
            main.startSpeed = moteRiseSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(moteSizeMin, moteSizeMax);
            main.maxParticles = Mathf.CeilToInt(moteEmissionRate * moteLifetime * 1.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            // Color gradient: teal at birth -> pink at death
            var colorOverLifetime = _moteParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.0f, 0.8f, 0.8f), 0f),   // teal
                    new GradientColorKey(new Color(1.0f, 0.2f, 0.6f), 1f),   // pink
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0f),   // fade in
                    new GradientAlphaKey(0.8f, 0.2f),
                    new GradientAlphaKey(0.8f, 0.7f),
                    new GradientAlphaKey(0.0f, 1f),   // fade out
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Emission rate
            var emission = _moteParticles.emission;
            emission.rateOverTime = moteEmissionRate;

            // Shape: small sphere at entity center so motes originate near the crystal
            var shape = _moteParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            // Velocity over lifetime: upward drift
            var vel = _moteParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(moteRiseSpeed * 0.8f, moteRiseSpeed * 1.2f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            // Billboard rendering (default for ParticleSystem)
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                // Use additive material for glow
                renderer.material = CreateMoteMaterial();
            }

            // Start emitting
            _moteParticles.Play();
        }

        private Material CreateMoteMaterial()
        {
            // Use additive particle shader for glowing motes
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.SetColor("_Color", Color.white);
            mat.mainTexture = CreateSoftGlowTexture(32);

            // Try to set additive blending
            mat.SetFloat("_Mode", 1f); // Additive
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.renderQueue = 3100;

            return mat;
        }

        private static Texture2D CreateSoftGlowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            // ParticleSystem and Lights are children, destroyed with this GO
        }
    }
}

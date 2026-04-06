using UnityEngine;

namespace Orlo.VFX
{
    /// <summary>
    /// Floating dust motes visible in god ray shafts during golden hour.
    /// Attaches as a child of the main camera and creates a ParticleSystem
    /// filling a 20x10x20m box around the player with warm gold particles.
    /// Defaults to active (golden hour is the default time of day).
    /// </summary>
    public class GoldenHourParticles : MonoBehaviour
    {
        [Header("Particle Settings")]
        [SerializeField] private int maxParticles = 300;
        [SerializeField] private float boxWidth = 20f;
        [SerializeField] private float boxHeight = 10f;
        [SerializeField] private float boxDepth = 20f;
        [SerializeField] private float sizeMin = 0.02f;
        [SerializeField] private float sizeMax = 0.05f;
        [SerializeField] private float driftSpeed = 0.1f;
        [SerializeField] private Color moteColor = new Color(1.0f, 0.9f, 0.7f, 0.3f);

        [Header("Time of Day")]
        [SerializeField] private bool alwaysActive = true;

        private ParticleSystem _particleSystem;

        private void Start()
        {
            CreateParticleSystem();
        }

        /// <summary>
        /// Enable or disable dust motes based on time of day.
        /// Call with true during golden hour, false otherwise.
        /// </summary>
        public void SetGoldenHour(bool isGoldenHour)
        {
            if (alwaysActive) return;

            if (_particleSystem == null) return;

            if (isGoldenHour && !_particleSystem.isPlaying)
                _particleSystem.Play();
            else if (!isGoldenHour && _particleSystem.isPlaying)
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void CreateParticleSystem()
        {
            _particleSystem = gameObject.AddComponent<ParticleSystem>();

            // Stop auto-play to configure
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Main module
            var main = _particleSystem.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = 0f; // movement via velocity over lifetime
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.startColor = moteColor;
            main.loop = true;
            main.playOnAwake = false;

            // Emission: burst enough to fill the volume, then steady replacement
            var emission = _particleSystem.emission;
            float avgLifetime = 10f;
            emission.rateOverTime = maxParticles / avgLifetime;

            // Shape: box around camera
            var shape = _particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(boxWidth, boxHeight, boxDepth);

            // Slow random drift via velocity over lifetime
            var vel = _particleSystem.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
            vel.y = new ParticleSystem.MinMaxCurve(-driftSpeed * 0.3f, driftSpeed * 0.5f);
            vel.z = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);

            // Subtle size variation over lifetime
            var sizeOverLifetime = _particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0f);
            sizeCurve.AddKey(0.1f, 1f);
            sizeCurve.AddKey(0.9f, 1f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Alpha fade in/out
            var colorOverLifetime = _particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.9f, 0.7f), 0f),
                    new GradientColorKey(new Color(1.0f, 0.9f, 0.7f), 1f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(moteColor.a, 0.15f),
                    new GradientAlphaKey(moteColor.a, 0.85f),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Renderer: billboard facing camera
            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = CreateDustMoteMaterial();
            }

            // Start emitting
            _particleSystem.Play();
        }

        private Material CreateDustMoteMaterial()
        {
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null)
                shader = Shader.Find("Standard");

            var mat = new Material(shader);
            mat.SetColor("_Color", Color.white);

            // Generate a soft circle texture so particles aren't squares
            mat.mainTexture = CreateSoftCircleTexture(32);

            // Alpha blended for soft dust look
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = 3000;

            return mat;
        }

        /// <summary>
        /// Creates a procedural soft circle texture for particle rendering.
        /// </summary>
        private static Texture2D CreateSoftCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist * dist);
                    alpha *= alpha; // Extra softness
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}

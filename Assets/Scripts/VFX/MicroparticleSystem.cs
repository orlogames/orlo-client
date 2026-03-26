using UnityEngine;

namespace Orlo.VFX
{
    /// <summary>
    /// Microparticle assembly/disassembly effect system.
    ///
    /// Drives the T-1000/nanobot visual effect where particles form from the air
    /// to create a solid entity. Used for:
    ///   - Player avatar spawning
    ///   - Aether being materialization
    ///   - Teleportation effects
    ///   - Death/respawn dissolve
    ///   - Species creation/evolution
    ///
    /// Attach to any GameObject with mesh geometry. Call Assemble() to start the
    /// particle convergence, Dissolve() to scatter back into particles.
    ///
    /// The system:
    ///   1. Samples the target mesh surface for particle target positions
    ///   2. Runs a GPU compute shader for per-particle simulation
    ///   3. Renders particles via Graphics.DrawMeshInstancedIndirect
    ///   4. Crossfades between particle cloud and solid mesh
    /// </summary>
    public class MicroparticleSystem : MonoBehaviour
    {
        [Header("Particle Settings")]
        [Tooltip("Number of particles. Higher = denser, more detailed assembly.")]
        [SerializeField] private int particleCount = 3000;

        [Tooltip("Radius of the initial scatter volume around the entity.")]
        [SerializeField] private float scatterRadius = 5f;

        [Header("Assembly Timing")]
        [Tooltip("How long the converge phase takes in seconds.")]
        [SerializeField] private float assemblyDuration = 2.5f;

        [Tooltip("How long the solidify phase takes in seconds.")]
        [SerializeField] private float solidifyDuration = 0.5f;

        [Tooltip("How long the dissolve phase takes in seconds.")]
        [SerializeField] private float dissolveDuration = 2.0f;

        [Header("Simulation")]
        [SerializeField] private float convergeSpeed = 15f;
        [SerializeField] private float noiseScale = 3f;
        [SerializeField] private float noiseStrength = 4f;
        [SerializeField] private float dampingFactor = 0.92f;

        [Header("Rendering")]
        [SerializeField] private Color particleColor = new Color(0.6f, 0.8f, 1f, 0.7f);
        [SerializeField] private Color aetherColor = new Color(0.5f, 0.2f, 1f, 0.8f);
        [SerializeField] private bool isAetherBeing = false;

        // Compute shader — loaded from Resources or assigned in editor
        private ComputeShader _computeShader;
        private ComputeBuffer _particleBuffer;
        private ComputeBuffer _argsBuffer;

        // Rendering
        private Material _particleMaterial;
        private Mesh _particleQuad;

        // Kernel IDs
        private int _initKernel;
        private int _simKernel;

        // State
        private Phase _currentPhase = Phase.Idle;
        private float _phaseTimer;
        private float _phaseDuration;
        private bool _initialized;

        // Mesh visibility control
        private Renderer[] _meshRenderers;
        private bool _meshVisible = true;

        public enum Phase
        {
            Idle,       // No effect active
            Scatter,    // Particles drifting randomly (pre-assembly)
            Converge,   // Particles spiraling toward targets
            Solidify,   // Particles snapping to targets, mesh fading in
            Assembled,  // Effect complete, mesh visible, particles off
            Dissolve,   // Reverse — mesh fading out, particles scattering
        }

        public Phase CurrentPhase => _currentPhase;
        public bool IsAssembled => _currentPhase == Phase.Assembled;

        private struct ParticleData
        {
            public Vector3 position;
            public Vector3 target;
            public Vector3 velocity;
            public Vector4 color;
            public float life;
            public float phase;
            public float seed;
            public float size;
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        /// <summary>
        /// Initialize the particle system for a target mesh.
        /// Call this after the mesh is built (e.g. after ProceduralCharacter.Build).
        /// </summary>
        public void Initialize(ComputeShader computeShader = null)
        {
            if (_initialized) return;

            _computeShader = computeShader;
            if (_computeShader == null)
            {
                _computeShader = Resources.Load<ComputeShader>("MicroparticleCompute");
            }

            if (_computeShader == null)
            {
                Debug.LogError("[MicroparticleSystem] No compute shader found. " +
                    "Place MicroparticleCompute.compute in Assets/Resources/");
                return;
            }

            _initKernel = _computeShader.FindKernel("Initialize");
            _simKernel = _computeShader.FindKernel("Simulate");

            // Cache mesh renderers for visibility toggling
            _meshRenderers = GetComponentsInChildren<Renderer>();

            // Sample target positions from the mesh
            Vector3[] targets = MicroparticleMeshSampler.SampleGameObject(gameObject, particleCount);
            if (targets.Length == 0)
            {
                Debug.LogWarning($"[MicroparticleSystem] No mesh samples from {gameObject.name}");
                return;
            }

            // Adjust particle count to match actual samples
            particleCount = targets.Length;

            // Create particle data
            var particles = new ParticleData[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                particles[i] = new ParticleData
                {
                    position = Vector3.zero, // Will be set by Initialize kernel
                    target = targets[i],
                    velocity = Vector3.zero,
                    color = isAetherBeing
                        ? (Vector4)aetherColor
                        : (Vector4)particleColor,
                    life = 0f,
                    phase = 0f,
                    seed = Random.value,
                    size = 0.01f,
                };
            }

            // Create GPU buffers
            int stride = System.Runtime.InteropServices.Marshal.SizeOf<ParticleData>();
            _particleBuffer = new ComputeBuffer(particleCount, stride);
            _particleBuffer.SetData(particles);

            // Args buffer for DrawMeshInstancedIndirect
            // [indexCount, instanceCount, startIndex, baseVertex, startInstance]
            _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

            // Create particle quad mesh
            _particleQuad = CreateParticleQuad();

            var args = new uint[5];
            args[0] = (uint)_particleQuad.GetIndexCount(0);
            args[1] = (uint)particleCount;
            args[2] = 0;
            args[3] = 0;
            args[4] = 0;
            _argsBuffer.SetData(args);

            // Create particle material
            _particleMaterial = CreateParticleMaterial();
            _particleMaterial.SetBuffer("_Particles", _particleBuffer);

            // Run initialize kernel to scatter particles
            SetCommonUniforms();
            _computeShader.SetBuffer(_initKernel, "_Particles", _particleBuffer);
            _computeShader.Dispatch(_initKernel, Mathf.CeilToInt(particleCount / 256f), 1, 1);

            _initialized = true;
        }

        /// <summary>
        /// Start the assembly effect — particles converge from air to form the entity.
        /// Mesh starts hidden, becomes visible during solidify phase.
        /// </summary>
        public void Assemble()
        {
            if (!_initialized)
            {
                Initialize();
                if (!_initialized) return;
            }

            SetMeshVisible(false);
            _currentPhase = Phase.Scatter;
            _phaseTimer = 0f;
            _phaseDuration = 0.5f; // Brief scatter before convergence

            // Re-scatter particles if needed
            SetCommonUniforms();
            _computeShader.SetBuffer(_initKernel, "_Particles", _particleBuffer);
            _computeShader.Dispatch(_initKernel, Mathf.CeilToInt(particleCount / 256f), 1, 1);
        }

        /// <summary>
        /// Start the dissolve effect — entity breaks apart into particles.
        /// </summary>
        public void Dissolve()
        {
            if (!_initialized)
            {
                Initialize();
                if (!_initialized) return;
            }

            _currentPhase = Phase.Dissolve;
            _phaseTimer = 0f;
            _phaseDuration = dissolveDuration;
        }

        /// <summary>
        /// Immediately show the mesh without any particle effect.
        /// </summary>
        public void ShowImmediate()
        {
            SetMeshVisible(true);
            _currentPhase = Phase.Assembled;
        }

        private void Update()
        {
            if (!_initialized || _currentPhase == Phase.Idle || _currentPhase == Phase.Assembled)
                return;

            _phaseTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_phaseTimer / _phaseDuration);

            // Phase state machine
            switch (_currentPhase)
            {
                case Phase.Scatter:
                    if (progress >= 1f)
                    {
                        _currentPhase = Phase.Converge;
                        _phaseTimer = 0f;
                        _phaseDuration = assemblyDuration;
                    }
                    break;

                case Phase.Converge:
                    if (progress >= 1f)
                    {
                        _currentPhase = Phase.Solidify;
                        _phaseTimer = 0f;
                        _phaseDuration = solidifyDuration;
                    }
                    break;

                case Phase.Solidify:
                    // Crossfade: show mesh as particles lock
                    SetMeshAlpha(progress);
                    if (progress >= 0.5f && !_meshVisible)
                    {
                        SetMeshVisible(true);
                    }

                    if (progress >= 1f)
                    {
                        if (isAetherBeing)
                        {
                            // Aether beings never fully solidify — loop back to subtle scatter
                            _currentPhase = Phase.Scatter;
                            _phaseTimer = 0f;
                            _phaseDuration = 3f; // Slow breathe cycle
                            SetMeshVisible(true);
                        }
                        else
                        {
                            _currentPhase = Phase.Assembled;
                            SetMeshVisible(true);
                            SetMeshAlpha(1f);
                        }
                    }
                    break;

                case Phase.Dissolve:
                    // Fade out mesh as particles scatter
                    SetMeshAlpha(1f - progress);
                    if (progress >= 0.3f && _meshVisible)
                    {
                        SetMeshVisible(false);
                    }

                    if (progress >= 1f)
                    {
                        _currentPhase = Phase.Idle;
                    }
                    break;
            }

            // Run GPU simulation
            DispatchSimulation(progress);

            // Render particles
            RenderParticles();
        }

        private void DispatchSimulation(float progress)
        {
            float phaseFloat = _currentPhase switch
            {
                Phase.Scatter => 0f,
                Phase.Converge => 1f,
                Phase.Solidify => 2f,
                Phase.Dissolve => 3f,
                _ => 0f,
            };

            _computeShader.SetBuffer(_simKernel, "_Particles", _particleBuffer);
            _computeShader.SetFloat("_DeltaTime", Time.deltaTime);
            _computeShader.SetFloat("_Time", Time.time);
            _computeShader.SetFloat("_Phase", phaseFloat);
            _computeShader.SetFloat("_PhaseProgress", progress);
            _computeShader.SetFloat("_ConvergeSpeed", convergeSpeed);
            _computeShader.SetFloat("_NoiseScale", noiseScale);
            _computeShader.SetFloat("_NoiseStrength", noiseStrength);
            _computeShader.SetFloat("_DampingFactor", dampingFactor);
            _computeShader.SetFloat("_ScatterRadius", scatterRadius);
            _computeShader.SetVector("_Center", transform.position);
            _computeShader.SetInt("_ParticleCount", particleCount);

            int threadGroups = Mathf.CeilToInt(particleCount / 256f);
            _computeShader.Dispatch(_simKernel, threadGroups, 1, 1);
        }

        private void RenderParticles()
        {
            if (_currentPhase == Phase.Assembled && !isAetherBeing) return;

            _particleMaterial.SetBuffer("_Particles", _particleBuffer);
            _particleMaterial.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);

            var bounds = new Bounds(transform.position, Vector3.one * scatterRadius * 2f);
            Graphics.DrawMeshInstancedIndirect(
                _particleQuad,
                0,
                _particleMaterial,
                bounds,
                _argsBuffer
            );
        }

        private void SetCommonUniforms()
        {
            _computeShader.SetFloat("_ScatterRadius", scatterRadius);
            _computeShader.SetVector("_Center", transform.position);
            _computeShader.SetInt("_ParticleCount", particleCount);
        }

        private void SetMeshVisible(bool visible)
        {
            _meshVisible = visible;
            if (_meshRenderers == null) return;
            foreach (var r in _meshRenderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        private void SetMeshAlpha(float alpha)
        {
            if (_meshRenderers == null) return;
            foreach (var r in _meshRenderers)
            {
                if (r == null || r.material == null) continue;

                // Set transparency on Standard shader
                var mat = r.material;
                var color = mat.color;
                color.a = alpha;
                mat.color = color;

                if (alpha < 1f)
                {
                    mat.SetFloat("_Mode", 3); // Transparent
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
                else
                {
                    mat.SetFloat("_Mode", 0); // Opaque
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = -1;
                }
            }
        }

        private Mesh CreateParticleQuad()
        {
            var mesh = new Mesh { name = "ParticleQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
            };
            mesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateParticleMaterial()
        {
            // Inline shader for billboard particles reading from structured buffer
            var shader = Shader.Find("Hidden/MicroparticleRender");
            if (shader == null)
            {
                // Fallback to a simple additive particle shader
                shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null)
                    shader = Shader.Find("Standard");
            }
            var mat = new Material(shader);
            mat.enableInstancing = true;
            return mat;
        }

        private void ReleaseBuffers()
        {
            _particleBuffer?.Release();
            _particleBuffer = null;
            _argsBuffer?.Release();
            _argsBuffer = null;
            if (_particleMaterial != null) Destroy(_particleMaterial);
            if (_particleQuad != null) Destroy(_particleQuad);
        }
    }
}

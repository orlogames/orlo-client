using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Centralized shader lookup for URP migration.
    /// All shader references go through here so we can switch pipelines in one place.
    /// </summary>
    public static class OrloShaders
    {
        // URP Lit shader name
        private const string URP_LIT = "Universal Render Pipeline/Lit";
        private const string URP_UNLIT = "Universal Render Pipeline/Unlit";
        private const string URP_SIMPLE_LIT = "Universal Render Pipeline/Simple Lit";
        private const string URP_PARTICLES_UNLIT = "Universal Render Pipeline/Particles/Unlit";
        private const string URP_PARTICLES_LIT = "Universal Render Pipeline/Particles/Lit";

        // Fallback chain for the main PBR shader
        private static Shader _litShader;
        private static Shader _unlitShader;
        private static Shader _particlesUnlitShader;
        private static Shader _particlesLitShader;
        private static Shader _spritesDefaultShader;

        /// <summary>
        /// Main PBR shader (URP Lit). Use for all opaque/transparent objects that need lighting.
        /// Replaces all Shader.Find("Standard") calls.
        /// </summary>
        public static Shader Lit
        {
            get
            {
                if (_litShader == null)
                {
                    _litShader = Shader.Find(URP_LIT);
                    if (_litShader == null)
                    {
                        // Fallback for editor or if URP not initialized yet
                        _litShader = Shader.Find("Standard");
                        if (_litShader != null)
                            Debug.LogWarning("[OrloShaders] URP Lit shader not found, falling back to Standard. Is URP installed?");
                    }
                    if (_litShader == null)
                    {
                        _litShader = Shader.Find("Legacy Shaders/Diffuse");
                        Debug.LogError("[OrloShaders] No PBR shader found! Check URP installation.");
                    }
                }
                return _litShader;
            }
        }

        /// <summary>
        /// Unlit shader (URP Unlit). Use for UI elements, skybox components, billboards.
        /// </summary>
        public static Shader Unlit
        {
            get
            {
                if (_unlitShader == null)
                {
                    _unlitShader = Shader.Find(URP_UNLIT);
                    if (_unlitShader == null)
                        _unlitShader = Shader.Find("Unlit/Color");
                }
                return _unlitShader;
            }
        }

        /// <summary>
        /// Particles Unlit shader. Use for additive/alpha-blended particles.
        /// </summary>
        public static Shader ParticlesUnlit
        {
            get
            {
                if (_particlesUnlitShader == null)
                {
                    _particlesUnlitShader = Shader.Find(URP_PARTICLES_UNLIT);
                    if (_particlesUnlitShader == null)
                        _particlesUnlitShader = Shader.Find("Particles/Standard Unlit");
                    if (_particlesUnlitShader == null)
                        _particlesUnlitShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                }
                return _particlesUnlitShader;
            }
        }

        /// <summary>
        /// Particles Lit shader. Use for particles that should receive scene lighting.
        /// </summary>
        public static Shader ParticlesLit
        {
            get
            {
                if (_particlesLitShader == null)
                {
                    _particlesLitShader = Shader.Find(URP_PARTICLES_LIT);
                    if (_particlesLitShader == null)
                        _particlesLitShader = ParticlesUnlit; // fallback
                }
                return _particlesLitShader;
            }
        }

        /// <summary>
        /// Sprites/Default shader for UI and line renderers.
        /// </summary>
        public static Shader SpritesDefault
        {
            get
            {
                if (_spritesDefaultShader == null)
                {
                    _spritesDefaultShader = Shader.Find("Sprites/Default");
                    if (_spritesDefaultShader == null)
                        _spritesDefaultShader = Unlit;
                }
                return _spritesDefaultShader;
            }
        }

        /// <summary>
        /// Load a custom Orlo shader from Resources/Shaders/.
        /// Falls back to URP Lit if not found.
        /// </summary>
        public static Shader LoadCustom(string shaderName)
        {
            var shader = Resources.Load<Shader>($"Shaders/{shaderName}");
            if (shader == null)
            {
                Debug.LogWarning($"[OrloShaders] Custom shader '{shaderName}' not found in Resources/Shaders/, using URP Lit fallback");
                return Lit;
            }
            return shader;
        }

        /// <summary>
        /// Create a new material with URP Lit shader and the given color.
        /// </summary>
        public static Material CreateLit(Color color)
        {
            var mat = new Material(Lit);
            // URP Lit uses _BaseColor instead of _Color
            mat.SetColor("_BaseColor", color);
            // Also set legacy _Color for compatibility
            if (mat.HasProperty("_Color"))
                mat.color = color;
            return mat;
        }

        /// <summary>
        /// Create a new material with URP Lit shader, color, metallic, and smoothness.
        /// </summary>
        public static Material CreateLit(Color color, float metallic, float smoothness)
        {
            var mat = CreateLit(color);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            // Built-in uses _Glossiness
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        /// <summary>
        /// Create a new emissive material (for windows, crystals, glowing objects).
        /// </summary>
        public static Material CreateEmissive(Color baseColor, Color emissionColor, float emissionIntensity = 1f)
        {
            var mat = CreateLit(baseColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return mat;
        }

        /// <summary>
        /// Create a transparent material (for glass, water, energy shields).
        /// </summary>
        public static Material CreateTransparent(Color color)
        {
            var mat = CreateLit(color);
            // URP surface type: 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }

        // TMD UI shaders
        private static Shader _holographicUIShader;
        private static Shader _frostedGlassShader;

        /// <summary>
        /// TMD Holographic UI shader — scanlines, chromatic aberration, noise, dot-grid, fresnel glow.
        /// Used for all TMD-projected interface elements.
        /// </summary>
        public static Shader HolographicUI
        {
            get
            {
                if (_holographicUIShader == null)
                    _holographicUIShader = LoadCustom("HolographicUI");
                return _holographicUIShader;
            }
        }

        /// <summary>
        /// Frosted glass shader — blurred background with tinted overlay.
        /// Used for panel backgrounds and glassmorphic UI elements.
        /// </summary>
        public static Shader FrostedGlass
        {
            get
            {
                if (_frostedGlassShader == null)
                    _frostedGlassShader = LoadCustom("FrostedGlass");
                return _frostedGlassShader;
            }
        }

        /// <summary>
        /// Invalidate cached shaders (call if render pipeline changes at runtime).
        /// </summary>
        public static void InvalidateCache()
        {
            _litShader = null;
            _unlitShader = null;
            _particlesUnlitShader = null;
            _particlesLitShader = null;
            _spritesDefaultShader = null;
            _holographicUIShader = null;
            _frostedGlassShader = null;
        }
    }
}

using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Detects proximity to Precursor ruins and POIs, driving TMD interference effects.
    /// When close to a Precursor site, the TMD UI degrades with noise and chromatic aberration.
    /// At high interference, Precursor glyphs flash across the screen.
    /// </summary>
    public class PrecursorDetector : MonoBehaviour
    {
        public static PrecursorDetector Instance { get; private set; }

        /// <summary>Maximum detection range in meters.</summary>
        private const float MaxRange = 50f;

        /// <summary>Interference threshold above which glyphs appear.</summary>
        private const float GlyphThreshold = 0.5f;

        /// <summary>How often a glyph can flash (seconds).</summary>
        private const float GlyphCooldownMin = 0.3f;
        private const float GlyphCooldownMax = 1.2f;

        /// <summary>How long each glyph stays on screen (seconds).</summary>
        private const float GlyphDuration = 0.18f;

        // Hardcoded Precursor site locations (world coordinates)
        private static readonly Vector3[] PrecursorSites = new Vector3[]
        {
            new Vector3(512f, 0f, 512f), // Threshold nexus crystal
        };

        private static readonly string[] PrecursorGlyphs = new string[]
        {
            "\u25C7", // ◇
            "\u25C6", // ◆
            "\u25B3", // △
            "\u25BD", // ▽
            "\u2609", // ☉
            "\u2295", // ⊕
            "\u2297", // ⊗
            "\u25CE", // ◎
            "\u2316", // ⌖
            "\u2388", // ⎈
        };

        private float _currentInterference;
        private float _glyphTimer;
        private bool _showingGlyph;
        private float _glyphShowTimer;
        private string _activeGlyph;
        private Vector2 _glyphScreenPos;
        private GUIStyle _glyphStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            UpdateInterference();
            UpdateGlyphs();
        }

        private void UpdateInterference()
        {
            if (Camera.main == null)
            {
                if (_currentInterference > 0f)
                {
                    _currentInterference = 0f;
                    if (TMDTheme.Instance != null)
                        TMDTheme.Instance.SetPrecursorInterference(0f);
                }
                return;
            }

            Vector3 playerPos = Camera.main.transform.position;
            float closestNormalized = 0f;

            for (int i = 0; i < PrecursorSites.Length; i++)
            {
                // Use XZ distance only (Y is height, don't penalize vertical offset)
                Vector3 site = PrecursorSites[i];
                float dx = playerPos.x - site.x;
                float dz = playerPos.z - site.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                float intensity = 1f - Mathf.Clamp01(dist / MaxRange);
                if (intensity > closestNormalized)
                    closestNormalized = intensity;
            }

            _currentInterference = closestNormalized;

            if (TMDTheme.Instance != null)
                TMDTheme.Instance.SetPrecursorInterference(_currentInterference);

            // Drive Precursor hum audio
            if (TMDSoundDesigner.Instance != null)
                TMDSoundDesigner.Instance.PrecursorHum(_currentInterference);
        }

        private void UpdateGlyphs()
        {
            if (_currentInterference < GlyphThreshold)
            {
                _showingGlyph = false;
                return;
            }

            if (_showingGlyph)
            {
                _glyphShowTimer -= Time.deltaTime;
                if (_glyphShowTimer <= 0f)
                    _showingGlyph = false;
            }
            else
            {
                _glyphTimer -= Time.deltaTime;
                if (_glyphTimer <= 0f)
                {
                    // Higher interference = more frequent glyphs
                    float frequencyMod = Mathf.InverseLerp(GlyphThreshold, 1f, _currentInterference);
                    float cooldown = Mathf.Lerp(GlyphCooldownMax, GlyphCooldownMin, frequencyMod);
                    _glyphTimer = cooldown;

                    _showingGlyph = true;
                    _glyphShowTimer = GlyphDuration;
                    _activeGlyph = PrecursorGlyphs[Random.Range(0, PrecursorGlyphs.Length)];
                    _glyphScreenPos = new Vector2(
                        Random.Range(0.1f, 0.9f) * Screen.width,
                        Random.Range(0.1f, 0.9f) * Screen.height
                    );
                }
            }
        }

        private void OnGUI()
        {
            if (!_showingGlyph || _activeGlyph == null) return;

            if (_glyphStyle == null)
            {
                _glyphStyle = new GUIStyle(GUI.skin.label);
                _glyphStyle.fontSize = 28;
                _glyphStyle.alignment = TextAnchor.MiddleCenter;
            }

            // Glow color from race palette, fading with remaining show time
            Color glyphColor;
            if (TMDTheme.Instance != null)
            {
                Color glow = TMDTheme.Instance.Palette.Glow;
                float alpha = Mathf.Clamp01(_glyphShowTimer / GlyphDuration) * 0.7f;
                glyphColor = new Color(glow.r, glow.g, glow.b, alpha);
            }
            else
            {
                glyphColor = new Color(1f, 1f, 1f, 0.5f);
            }

            _glyphStyle.normal.textColor = glyphColor;

            Rect glyphRect = new Rect(_glyphScreenPos.x - 20f, _glyphScreenPos.y - 20f, 40f, 40f);
            GUI.Label(glyphRect, _activeGlyph, _glyphStyle);
        }

        /// <summary>Current interference level (0..1). Read-only for external queries.</summary>
        public float CurrentInterference => _currentInterference;

        /// <summary>Register an additional Precursor site at runtime (e.g., from server data).</summary>
        public static void RegisterSite(Vector3 worldPosition)
        {
            // For now, sites are hardcoded. Future: dynamic list from server.
            Debug.Log($"[PrecursorDetector] Site registration requested at {worldPosition} (not yet dynamic)");
        }
    }
}

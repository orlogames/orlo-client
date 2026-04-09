using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Central singleton providing the active race palette and TMD tier visual parameters.
    /// Every UI element queries TMDTheme for its colors and holographic settings.
    /// Also manages environmental signal degradation (Precursor proximity).
    /// </summary>
    public class TMDTheme : MonoBehaviour
    {
        public static TMDTheme Instance { get; private set; }

        [Header("TMD State")]
        [SerializeField] private int tmdTier = 1;
        [SerializeField] private string raceName = "Solari";

        /// <summary>0..1 — proximity to Precursor ruins. Increases noise/interference.</summary>
        [Range(0f, 1f)]
        [SerializeField] private float precursorInterference = 0f;

        // Cached state
        private RacePalette _palette;
        private TMDTierSettings _tierSettings;
        private Material _holographicMaterial;
        private Material _frostedGlassMaterial;
        private float _glitchTimer;
        private bool _isGlitching;
        private float _glitchDuration = 0.15f;
        private float _breathePhase;

        // Public API
        public RacePalette Palette => _palette;
        public TMDTierSettings TierSettings => _tierSettings;
        public int Tier => tmdTier;
        public float PrecursorInterference => precursorInterference;
        public bool IsGlitching => _isGlitching;
        public float BreathePhase => _breathePhase;

        /// <summary>Effective scanline intensity (tier + Precursor interference)</summary>
        public float EffectiveScanlines => Mathf.Clamp01(_tierSettings.ScanlineIntensity + precursorInterference * 0.3f);
        /// <summary>Effective noise (tier + Precursor interference)</summary>
        public float EffectiveNoise => Mathf.Clamp01(_tierSettings.NoiseIntensity + precursorInterference * 0.2f);
        /// <summary>Effective chromatic aberration (tier + Precursor interference)</summary>
        public float EffectiveAberration => _tierSettings.ChromaticAberration + precursorInterference * 4f;

        /// <summary>Material for holographic UI elements (shared instance, auto-updated)</summary>
        public Material HolographicMaterial
        {
            get
            {
                if (_holographicMaterial == null) CreateHolographicMaterial();
                return _holographicMaterial;
            }
        }

        /// <summary>Material for frosted glass panels (shared instance, auto-updated)</summary>
        public Material FrostedGlassMaterial
        {
            get
            {
                if (_frostedGlassMaterial == null) CreateFrostedGlassMaterial();
                return _frostedGlassMaterial;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshPalette();
            RefreshTier();
        }

        private void Update()
        {
            UpdateGlitch();
            UpdateBreathe();
            UpdateMaterialProperties();
        }

        /// <summary>Change the active race (e.g., on character select). Refreshes all colors.</summary>
        public void SetRace(string newRace)
        {
            raceName = newRace;
            RefreshPalette();
        }

        /// <summary>Set the TMD upgrade tier (1-5). Refreshes all visual parameters.</summary>
        public void SetTier(int tier)
        {
            tmdTier = Mathf.Clamp(tier, 1, 5);
            RefreshTier();
        }

        /// <summary>Set Precursor proximity [0..1]. Called by world systems near ruins.</summary>
        public void SetPrecursorInterference(float intensity)
        {
            precursorInterference = Mathf.Clamp01(intensity);
        }

        private void RefreshPalette()
        {
            _palette = RacePalette.ForRace(raceName);
            Debug.Log($"[TMDTheme] Palette set to {_palette.RaceName}");
        }

        private void RefreshTier()
        {
            _tierSettings = TMDTierSettings.ForTier(tmdTier);
            _glitchTimer = _tierSettings.GlitchInterval > 0 ? Random.Range(0f, _tierSettings.GlitchInterval) : 0f;
            Debug.Log($"[TMDTheme] TMD tier set to {tmdTier}");
        }

        private void UpdateGlitch()
        {
            if (_tierSettings.GlitchInterval <= 0f)
            {
                _isGlitching = false;
                return;
            }

            _glitchTimer -= Time.deltaTime;
            if (_glitchTimer <= 0f && !_isGlitching)
            {
                _isGlitching = true;
                _glitchTimer = _glitchDuration;
            }
            else if (_isGlitching && _glitchTimer <= 0f)
            {
                _isGlitching = false;
                _glitchTimer = _tierSettings.GlitchInterval + Random.Range(-5f, 5f);
            }
        }

        private void UpdateBreathe()
        {
            // 4-second breathing cycle (0.5% scale oscillation)
            _breathePhase = Mathf.Sin(Time.time * Mathf.PI * 0.5f) * 0.005f;
        }

        private void UpdateMaterialProperties()
        {
            if (_holographicMaterial != null)
            {
                _holographicMaterial.SetColor("_RaceColor", _palette.Primary);
                _holographicMaterial.SetColor("_GlowColor", _palette.Glow);
                _holographicMaterial.SetColor("_BackgroundColor", _palette.Background);
                _holographicMaterial.SetFloat("_ScanlineIntensity", EffectiveScanlines);
                _holographicMaterial.SetFloat("_ScanlineSpeed", _tierSettings.ScanlineSpeed);
                _holographicMaterial.SetFloat("_ChromaticAberration", EffectiveAberration);
                _holographicMaterial.SetFloat("_NoiseIntensity", EffectiveNoise);
                _holographicMaterial.SetFloat("_GlowMultiplier", _tierSettings.GlowMultiplier);
                _holographicMaterial.SetFloat("_DotGridSpacing", _tierSettings.DotGridSpacing);
                _holographicMaterial.SetFloat("_IsGlitching", _isGlitching ? 1f : 0f);
                _holographicMaterial.SetFloat("_Time", Time.time);
            }

            if (_frostedGlassMaterial != null)
            {
                _frostedGlassMaterial.SetColor("_TintColor", _palette.PanelBackground);
                _frostedGlassMaterial.SetColor("_BorderColor", _palette.Border);
                _frostedGlassMaterial.SetFloat("_BorderGlowAlpha", _palette.BorderGlow.a);
            }
        }

        private void CreateHolographicMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/HolographicUI");
            if (shader == null)
            {
                Debug.LogWarning("[TMDTheme] HolographicUI shader not found, using Unlit fallback");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            _holographicMaterial = new Material(shader);
            _holographicMaterial.name = "TMD_Holographic";
        }

        private void CreateFrostedGlassMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/FrostedGlass");
            if (shader == null)
            {
                Debug.LogWarning("[TMDTheme] FrostedGlass shader not found, using Unlit fallback");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            _frostedGlassMaterial = new Material(shader);
            _frostedGlassMaterial.name = "TMD_FrostedGlass";
        }

        // ────────────────────────────────────────────────────────────
        // OnGUI helper methods — used by existing UI scripts during migration
        // These draw holographic-styled rects, labels, and buttons using OnGUI
        // ────────────────────────────────────────────────────────────

        private static Texture2D _panelTex;
        private static Texture2D _borderTex;
        private static Texture2D _barTex;
        private static GUIStyle _labelStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _buttonStyle;

        /// <summary>Draw a TMD-styled panel background (dark glass + border)</summary>
        public static void DrawPanel(Rect rect)
        {
            if (Instance == null) return;
            var p = Instance._palette;
            EnsureTextures();

            // Panel background
            GUI.color = p.PanelBackground;
            GUI.DrawTexture(rect, _panelTex);

            // Border (1px)
            GUI.color = p.Border;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), _borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), _borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), _borderTex);
            GUI.DrawTexture(new Rect(rect.xMax - 1, rect.y, 1, rect.height), _borderTex);

            // Outer glow (2px, 20% alpha)
            GUI.color = new Color(p.Glow.r, p.Glow.g, p.Glow.b, 0.08f);
            GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, 2), _borderTex);
            GUI.DrawTexture(new Rect(rect.x - 2, rect.yMax, rect.width + 4, 2), _borderTex);
            GUI.DrawTexture(new Rect(rect.x - 2, rect.y, 2, rect.height), _borderTex);
            GUI.DrawTexture(new Rect(rect.xMax, rect.y, 2, rect.height), _borderTex);

            GUI.color = Color.white;
        }

        /// <summary>Draw a TMD-styled title bar at the top of a panel</summary>
        public static void DrawTitle(Rect panelRect, string title)
        {
            if (Instance == null) return;
            var p = Instance._palette;
            EnsureStyles();

            var titleRect = new Rect(panelRect.x + 12, panelRect.y + 8, panelRect.width - 24, 28);
            _titleStyle.normal.textColor = p.Primary;
            GUI.Label(titleRect, title.ToUpperInvariant(), _titleStyle);

            // Underline
            GUI.color = new Color(p.Primary.r, p.Primary.g, p.Primary.b, 0.4f);
            GUI.DrawTexture(new Rect(panelRect.x + 8, panelRect.y + 38, panelRect.width - 16, 1), _borderTex);
            GUI.color = Color.white;
        }

        /// <summary>Get the standard TMD label style with race-colored text</summary>
        public static GUIStyle LabelStyle
        {
            get
            {
                EnsureStyles();
                if (Instance != null)
                    _labelStyle.normal.textColor = Instance._palette.Text;
                return _labelStyle;
            }
        }

        /// <summary>Get the TMD title style with race primary color</summary>
        public static GUIStyle TitleStyle
        {
            get
            {
                EnsureStyles();
                if (Instance != null)
                    _titleStyle.normal.textColor = Instance._palette.Primary;
                return _titleStyle;
            }
        }

        /// <summary>Draw a TMD-styled button. Returns true on click.</summary>
        public static bool DrawButton(Rect rect, string text)
        {
            if (Instance == null) return GUI.Button(rect, text);
            var p = Instance._palette;
            EnsureStyles();

            bool hover = rect.Contains(Event.current.mousePosition);
            var bgColor = hover
                ? new Color(p.Primary.r, p.Primary.g, p.Primary.b, 0.25f)
                : new Color(p.Background.r, p.Background.g, p.Background.b, 0.8f);

            GUI.color = bgColor;
            GUI.DrawTexture(rect, _panelTex);

            // Border
            var borderColor = hover ? p.Primary : p.Border;
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), _borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), _borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), _borderTex);
            GUI.DrawTexture(new Rect(rect.xMax - 1, rect.y, 1, rect.height), _borderTex);

            GUI.color = Color.white;
            _buttonStyle.normal.textColor = hover ? p.Primary : p.Text;
            GUI.Label(rect, text, _buttonStyle);

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Draw a TMD-styled progress bar with gradient fill and leading edge glow</summary>
        public static void DrawProgressBar(Rect rect, float fill, Color? overrideColor = null)
        {
            if (Instance == null) return;
            var p = Instance._palette;
            EnsureTextures();

            fill = Mathf.Clamp01(fill);

            // Track background
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(rect, _panelTex);

            // Fill
            var fillColor = overrideColor ?? p.Primary;
            var fillRect = new Rect(rect.x, rect.y, rect.width * fill, rect.height);
            GUI.color = fillColor;
            GUI.DrawTexture(fillRect, _barTex);

            // Leading edge glow
            if (fill > 0.01f && fill < 0.99f)
            {
                float edgeX = rect.x + rect.width * fill - 2;
                GUI.color = new Color(p.Glow.r, p.Glow.g, p.Glow.b, 0.6f);
                GUI.DrawTexture(new Rect(edgeX, rect.y, 4, rect.height), _barTex);
            }

            // Border
            GUI.color = new Color(p.Border.r, p.Border.g, p.Border.b, 0.6f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), _borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), _borderTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), _borderTex);
            GUI.DrawTexture(new Rect(rect.xMax - 1, rect.y, 1, rect.height), _borderTex);

            GUI.color = Color.white;
        }

        /// <summary>Draw a scanline overlay across a rect (TMD holographic effect)</summary>
        public static void DrawScanlines(Rect rect)
        {
            if (Instance == null) return;
            float intensity = Instance.EffectiveScanlines;
            if (intensity <= 0.001f) return;

            EnsureTextures();
            float speed = Instance._tierSettings.ScanlineSpeed;
            float offset = (Time.time * speed * 50f) % rect.height;

            GUI.color = new Color(0, 0, 0, intensity * 0.4f);
            for (float y = -offset; y < rect.height; y += 3f)
            {
                if (y >= 0)
                    GUI.DrawTexture(new Rect(rect.x, rect.y + y, rect.width, 1), _borderTex);
            }
            GUI.color = Color.white;
        }

        private static void EnsureTextures()
        {
            if (_panelTex == null)
            {
                _panelTex = new Texture2D(1, 1);
                _panelTex.SetPixel(0, 0, Color.white);
                _panelTex.Apply();
            }
            if (_borderTex == null)
            {
                _borderTex = new Texture2D(1, 1);
                _borderTex.SetPixel(0, 0, Color.white);
                _borderTex.Apply();
            }
            if (_barTex == null)
            {
                _barTex = new Texture2D(1, 1);
                _barTex.SetPixel(0, 0, Color.white);
                _barTex.Apply();
            }
        }

        private static void EnsureStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.fontSize = 13;
                _labelStyle.alignment = TextAnchor.MiddleLeft;
                _labelStyle.wordWrap = true;
            }
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.fontSize = 16;
                _titleStyle.fontStyle = FontStyle.Bold;
                _titleStyle.alignment = TextAnchor.MiddleLeft;
            }
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.label);
                _buttonStyle.fontSize = 13;
                _buttonStyle.alignment = TextAnchor.MiddleCenter;
            }
        }
    }
}

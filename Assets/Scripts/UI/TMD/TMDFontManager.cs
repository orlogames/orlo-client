using UnityEngine;
using TMPro;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Manages TextMeshPro SDF material presets for the TMD holographic interface.
    /// Creates runtime material instances with race-colored glow, outline, and underlay.
    ///
    /// Material presets:
    /// - Normal:    Clean SDF text, race-colored, no effects
    /// - Glow:      Race-colored outer glow (for labels, headers when hovered)
    /// - Title:     Bold with race primary color + strong glow (for panel titles)
    /// - Hologram:  Full holographic treatment — glow + underlay + slight jitter
    /// - Data:      Monospace feel — clean, accent-colored (for numbers, stats)
    /// </summary>
    public class TMDFontManager : MonoBehaviour
    {
        public static TMDFontManager Instance { get; private set; }

        // Runtime material instances (one per preset, updated when palette changes)
        private Material _normalMaterial;
        private Material _glowMaterial;
        private Material _titleMaterial;
        private Material _hologramMaterial;
        private Material _dataMaterial;

        // Base TMP font asset (looked up at runtime)
        private TMP_FontAsset _baseFont;
        private string _lastRace;

        public Material NormalMaterial => EnsureMaterial(ref _normalMaterial, SetupNormal);
        public Material GlowMaterial => EnsureMaterial(ref _glowMaterial, SetupGlow);
        public Material TitleMaterial => EnsureMaterial(ref _titleMaterial, SetupTitle);
        public Material HologramMaterial => EnsureMaterial(ref _hologramMaterial, SetupHologram);
        public Material DataMaterial => EnsureMaterial(ref _dataMaterial, SetupData);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            FindBaseFont();
        }

        private void Update()
        {
            // Refresh materials when race changes
            if (TMDTheme.Instance == null) return;
            string currentRace = TMDTheme.Instance.Palette.RaceName;
            if (currentRace != _lastRace)
            {
                _lastRace = currentRace;
                RefreshAllMaterials();
            }
        }

        /// <summary>
        /// Apply TMD-styled text to a TextMeshProUGUI component.
        /// </summary>
        public static void ApplyStyle(TextMeshProUGUI text, TMDTextStyle style)
        {
            if (Instance == null || text == null) return;

            switch (style)
            {
                case TMDTextStyle.Normal:
                    text.fontSharedMaterial = Instance.NormalMaterial;
                    text.fontSize = 14;
                    break;
                case TMDTextStyle.Glow:
                    text.fontSharedMaterial = Instance.GlowMaterial;
                    text.fontSize = 14;
                    break;
                case TMDTextStyle.Title:
                    text.fontSharedMaterial = Instance.TitleMaterial;
                    text.fontSize = 18;
                    text.fontStyle = FontStyles.Bold;
                    break;
                case TMDTextStyle.Hologram:
                    text.fontSharedMaterial = Instance.HologramMaterial;
                    text.fontSize = 14;
                    break;
                case TMDTextStyle.Data:
                    text.fontSharedMaterial = Instance.DataMaterial;
                    text.fontSize = 13;
                    break;
            }
        }

        private void FindBaseFont()
        {
            // Try to load a bundled font, fall back to TMP default
            _baseFont = Resources.Load<TMP_FontAsset>("Fonts/OrloSDF");
            if (_baseFont == null)
            {
                // Use TMP's default font as base
                _baseFont = TMP_Settings.defaultFontAsset;
            }
            if (_baseFont == null)
            {
                Debug.LogWarning("[TMDFontManager] No TMP font found. SDF text will use fallback.");
            }
        }

        private Material EnsureMaterial(ref Material mat, System.Action<Material> setup)
        {
            if (mat == null && _baseFont != null)
            {
                mat = new Material(_baseFont.material);
                setup(mat);
            }
            return mat;
        }

        private void RefreshAllMaterials()
        {
            if (_normalMaterial != null) SetupNormal(_normalMaterial);
            if (_glowMaterial != null) SetupGlow(_glowMaterial);
            if (_titleMaterial != null) SetupTitle(_titleMaterial);
            if (_hologramMaterial != null) SetupHologram(_hologramMaterial);
            if (_dataMaterial != null) SetupData(_dataMaterial);
        }

        private RacePalette P => TMDTheme.Instance?.Palette ?? RacePalette.Solari;

        private void SetupNormal(Material mat)
        {
            mat.name = "TMD_Text_Normal";
            SetFaceColor(mat, P.Text);
            DisableGlow(mat);
            DisableUnderlay(mat);
            DisableOutline(mat);
        }

        private void SetupGlow(Material mat)
        {
            mat.name = "TMD_Text_Glow";
            SetFaceColor(mat, P.Text);
            EnableGlow(mat, P.Glow, 0.3f, 0.5f);
            DisableUnderlay(mat);
            DisableOutline(mat);
        }

        private void SetupTitle(Material mat)
        {
            mat.name = "TMD_Text_Title";
            SetFaceColor(mat, P.Primary);
            EnableGlow(mat, P.Glow, 0.5f, 0.8f);
            DisableUnderlay(mat);
            EnableOutline(mat, P.PrimaryDim, 0.05f);
        }

        private void SetupHologram(Material mat)
        {
            mat.name = "TMD_Text_Hologram";
            SetFaceColor(mat, P.Primary);
            EnableGlow(mat, P.Glow, 0.6f, 1.0f);
            EnableUnderlay(mat, P.PrimaryDim, 0.4f, new Vector2(0.5f, -0.5f));
            DisableOutline(mat);
        }

        private void SetupData(Material mat)
        {
            mat.name = "TMD_Text_Data";
            SetFaceColor(mat, P.Accent);
            EnableGlow(mat, P.GlowHalf, 0.15f, 0.3f);
            DisableUnderlay(mat);
            DisableOutline(mat);
        }

        // ── TMP Material Property Helpers ──

        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowOffsetId = Shader.PropertyToID("_GlowOffset");
        private static readonly int GlowOuterId = Shader.PropertyToID("_GlowOuter");
        private static readonly int GlowPowerId = Shader.PropertyToID("_GlowPower");
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
        private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
        private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");
        private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
        private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private static void SetFaceColor(Material mat, Color c)
        {
            if (mat.HasProperty(FaceColorId))
                mat.SetColor(FaceColorId, c);
        }

        private static void EnableGlow(Material mat, Color c, float offset, float outer)
        {
            if (!mat.HasProperty(GlowColorId)) return;
            mat.SetColor(GlowColorId, c);
            mat.SetFloat(GlowOffsetId, offset);
            mat.SetFloat(GlowOuterId, outer);
            mat.SetFloat(GlowPowerId, 0.8f);
            mat.EnableKeyword("GLOW_ON");
        }

        private static void DisableGlow(Material mat)
        {
            if (mat.HasProperty(GlowColorId))
                mat.SetColor(GlowColorId, Color.clear);
            mat.DisableKeyword("GLOW_ON");
        }

        private static void EnableUnderlay(Material mat, Color c, float dilate, Vector2 offset)
        {
            if (!mat.HasProperty(UnderlayColorId)) return;
            mat.SetColor(UnderlayColorId, c);
            mat.SetFloat(UnderlayOffsetXId, offset.x);
            mat.SetFloat(UnderlayOffsetYId, offset.y);
            mat.SetFloat(UnderlayDilateId, dilate);
            mat.SetFloat(UnderlaySoftnessId, 0.3f);
            mat.EnableKeyword("UNDERLAY_ON");
        }

        private static void DisableUnderlay(Material mat)
        {
            mat.DisableKeyword("UNDERLAY_ON");
        }

        private static void EnableOutline(Material mat, Color c, float width)
        {
            if (!mat.HasProperty(OutlineColorId)) return;
            mat.SetColor(OutlineColorId, c);
            mat.SetFloat(OutlineWidthId, width);
        }

        private static void DisableOutline(Material mat)
        {
            if (mat.HasProperty(OutlineWidthId))
                mat.SetFloat(OutlineWidthId, 0f);
        }
    }

    public enum TMDTextStyle
    {
        Normal,
        Glow,
        Title,
        Hologram,
        Data
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Extension methods for VisualElement to store/retrieve typed properties
    /// via a Dictionary kept in the userData slot.
    /// </summary>
    internal static class VisualElementPropertyExtensions
    {
        private static Dictionary<string, object> GetOrCreateProps(VisualElement ve)
        {
            if (ve.userData is Dictionary<string, object> dict)
                return dict;
            dict = new Dictionary<string, object>();
            ve.userData = dict;
            return dict;
        }

        public static void SetProperty(this VisualElement ve, string key, object value)
        {
            GetOrCreateProps(ve)[key] = value;
        }

        public static object GetProperty(this VisualElement ve, string key)
        {
            if (ve.userData is Dictionary<string, object> dict && dict.TryGetValue(key, out var val))
                return val;
            return null;
        }
    }

    /// <summary>
    /// Bridges TMDTheme runtime state to UI Toolkit USS custom properties.
    /// Attach to a GameObject that also has a UIDocument component.
    ///
    /// Two sync mechanisms:
    ///   1. Race palette: toggles USS class (race-solari, race-vael, etc.) on the
    ///      root VisualElement. Each race's USS file defines --tmd-* custom properties
    ///      that child elements resolve via var(--tmd-primary) etc.
    ///   2. Tier settings + effective values: stored as named properties on the root
    ///      VisualElement via SetProperty(), readable by custom VisualElements and
    ///      C# code via GetProperty(). Also sets inline styles for values that map
    ///      directly to standard USS properties (opacity, etc.).
    ///
    /// Polls TMDTheme each frame for changes (race, tier, Precursor interference).
    /// Works in editor via [ExecuteAlways].
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public class TMDStyleBridge : MonoBehaviour
    {
        // Property name constants for GetProperty/SetProperty access from other scripts
        public const string PropTier = "tmd-tier";
        public const string PropScanlineIntensity = "tmd-tier-scanline-intensity";
        public const string PropScanlineSpeed = "tmd-tier-scanline-speed";
        public const string PropChromaticAberration = "tmd-tier-chromatic-aberration";
        public const string PropNoiseIntensity = "tmd-tier-noise-intensity";
        public const string PropGlitchInterval = "tmd-tier-glitch-interval";
        public const string PropGlowMultiplier = "tmd-tier-glow-multiplier";
        public const string PropDotGridSpacing = "tmd-tier-dot-grid-spacing";
        public const string PropTextJitter = "tmd-tier-text-jitter";
        public const string PropDotGridAnimates = "tmd-tier-dot-grid-animates";
        public const string PropSecondaryHalo = "tmd-tier-secondary-halo";
        public const string PropEdgeParticles = "tmd-tier-edge-particles";
        public const string PropEffectiveScanlines = "tmd-effective-scanlines";
        public const string PropEffectiveNoise = "tmd-effective-noise";
        public const string PropEffectiveAberration = "tmd-effective-aberration";
        public const string PropPrecursorInterference = "tmd-precursor-interference";

        // Race palette color property names (set on root for direct C# reads)
        public const string PropPrimary = "tmd-primary";
        public const string PropSecondary = "tmd-secondary";
        public const string PropAccent = "tmd-accent";
        public const string PropBackground = "tmd-background";
        public const string PropBorder = "tmd-border";
        public const string PropText = "tmd-text";
        public const string PropGlow = "tmd-glow";
        public const string PropDanger = "tmd-danger";
        public const string PropSuccess = "tmd-success";
        public const string PropPrimaryDim = "tmd-primary-dim";
        public const string PropGlowHalf = "tmd-glow-half";
        public const string PropBackgroundSolid = "tmd-background-solid";
        public const string PropBorderGlow = "tmd-border-glow";
        public const string PropTextDim = "tmd-text-dim";
        public const string PropPanelBackground = "tmd-panel-background";

        private UIDocument _document;
        private VisualElement _root;

        // Cached state for change detection
        private string _lastRaceName;
        private int _lastTier;
        private float _lastPrecursorInterference;

        /// <summary>Event raised after USS properties are updated. Subscribe to react to theme changes.</summary>
        public event System.Action ThemeChanged;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            AcquireRoot();
            RefreshTheme();
        }

        private void Update()
        {
            if (_root == null)
            {
                AcquireRoot();
                if (_root == null) return;
            }

            var theme = TMDTheme.Instance;
            if (theme == null) return;

            bool changed = false;

            string currentRace = theme.Palette?.RaceName;
            if (currentRace != _lastRaceName)
            {
                _lastRaceName = currentRace;
                changed = true;
            }

            if (theme.Tier != _lastTier)
            {
                _lastTier = theme.Tier;
                changed = true;
            }

            // Precursor interference is continuous; update when it shifts noticeably
            float interference = theme.PrecursorInterference;
            if (Mathf.Abs(interference - _lastPrecursorInterference) > 0.005f)
            {
                _lastPrecursorInterference = interference;
                changed = true;
            }

            if (changed)
            {
                PushAllProperties(theme);
                ThemeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Manually re-sync all USS custom properties from the current TMDTheme state.
        /// Call this after programmatic theme changes that need immediate visual sync.
        /// </summary>
        public void RefreshTheme()
        {
            if (_root == null) AcquireRoot();
            if (_root == null) return;

            var theme = TMDTheme.Instance;
            if (theme == null)
            {
                PushDefaults();
                ThemeChanged?.Invoke();
                return;
            }

            _lastRaceName = theme.Palette?.RaceName;
            _lastTier = theme.Tier;
            _lastPrecursorInterference = theme.PrecursorInterference;

            PushAllProperties(theme);
            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// Helper: read a float property that was pushed by the bridge.
        /// Returns defaultValue if the property is not set.
        /// </summary>
        public static float GetFloatProperty(VisualElement element, string propertyName, float defaultValue = 0f)
        {
            var root = FindBridgeRoot(element);
            if (root == null) return defaultValue;
            var val = root.GetProperty(propertyName);
            return val is float f ? f : defaultValue;
        }

        /// <summary>
        /// Helper: read a Color property that was pushed by the bridge.
        /// Returns defaultColor if the property is not set.
        /// </summary>
        public static Color GetColorProperty(VisualElement element, string propertyName, Color? defaultColor = null)
        {
            var root = FindBridgeRoot(element);
            if (root == null) return defaultColor ?? Color.magenta;
            var val = root.GetProperty(propertyName);
            return val is Color c ? c : (defaultColor ?? Color.magenta);
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private void AcquireRoot()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document != null)
                _root = _document.rootVisualElement;
        }

        private void PushAllProperties(TMDTheme theme)
        {
            if (_root == null) return;

            PushRaceClass(theme.Palette);
            PushRacePaletteProperties(theme.Palette);
            PushTierProperties(theme.TierSettings, theme);
        }

        // ------------------------------------------------------------------
        // Race USS class switching
        // ------------------------------------------------------------------

        private static readonly string[] RaceClasses =
        {
            "race-solari",
            "race-vael",
            "race-korrath",
            "race-thyren"
        };

        private void PushRaceClass(RacePalette palette)
        {
            if (palette == null) return;

            string targetClass = "race-" + palette.RaceName.ToLowerInvariant();

            for (int i = 0; i < RaceClasses.Length; i++)
            {
                if (RaceClasses[i] == targetClass)
                    _root.AddToClassList(RaceClasses[i]);
                else
                    _root.RemoveFromClassList(RaceClasses[i]);
            }
        }

        // ------------------------------------------------------------------
        // Race palette -> VisualElement properties (for C# access)
        // ------------------------------------------------------------------

        private void PushRacePaletteProperties(RacePalette palette)
        {
            if (palette == null) return;

            SetColorProp(PropPrimary, palette.Primary);
            SetColorProp(PropSecondary, palette.Secondary);
            SetColorProp(PropAccent, palette.Accent);
            SetColorProp(PropBackground, palette.Background);
            SetColorProp(PropBorder, palette.Border);
            SetColorProp(PropText, palette.Text);
            SetColorProp(PropGlow, palette.Glow);
            SetColorProp(PropDanger, palette.Danger);
            SetColorProp(PropSuccess, palette.Success);
            SetColorProp(PropPrimaryDim, palette.PrimaryDim);
            SetColorProp(PropGlowHalf, palette.GlowHalf);
            SetColorProp(PropBackgroundSolid, palette.BackgroundSolid);
            SetColorProp(PropBorderGlow, palette.BorderGlow);
            SetColorProp(PropTextDim, palette.TextDim);
            SetColorProp(PropPanelBackground, palette.PanelBackground);
        }

        // ------------------------------------------------------------------
        // Tier settings -> VisualElement properties
        // ------------------------------------------------------------------

        private void PushTierProperties(TMDTierSettings tier, TMDTheme theme)
        {
            SetFloatProp(PropTier, theme.Tier);
            SetFloatProp(PropScanlineIntensity, tier.ScanlineIntensity);
            SetFloatProp(PropScanlineSpeed, tier.ScanlineSpeed);
            SetFloatProp(PropChromaticAberration, tier.ChromaticAberration);
            SetFloatProp(PropNoiseIntensity, tier.NoiseIntensity);
            SetFloatProp(PropGlitchInterval, tier.GlitchInterval);
            SetFloatProp(PropGlowMultiplier, tier.GlowMultiplier);
            SetFloatProp(PropDotGridSpacing, tier.DotGridSpacing);
            SetFloatProp(PropTextJitter, tier.TextJitter);
            SetFloatProp(PropDotGridAnimates, tier.DotGridAnimates ? 1f : 0f);
            SetFloatProp(PropSecondaryHalo, tier.SecondaryHalo ? 1f : 0f);
            SetFloatProp(PropEdgeParticles, tier.EdgeParticles ? 1f : 0f);

            // Effective values (tier + Precursor interference)
            SetFloatProp(PropEffectiveScanlines, theme.EffectiveScanlines);
            SetFloatProp(PropEffectiveNoise, theme.EffectiveNoise);
            SetFloatProp(PropEffectiveAberration, theme.EffectiveAberration);
            SetFloatProp(PropPrecursorInterference, theme.PrecursorInterference);
        }

        // ------------------------------------------------------------------
        // Defaults (pre-initialization fallback)
        // ------------------------------------------------------------------

        private void PushDefaults()
        {
            // Push Solari/Tier1 defaults so USS classes resolve and properties
            // are available even before TMDTheme singleton initializes.
            var palette = RacePalette.Solari;
            var tier = TMDTierSettings.Tier1;

            PushRaceClass(palette);
            PushRacePaletteProperties(palette);

            SetFloatProp(PropTier, 1f);
            SetFloatProp(PropScanlineIntensity, tier.ScanlineIntensity);
            SetFloatProp(PropScanlineSpeed, tier.ScanlineSpeed);
            SetFloatProp(PropChromaticAberration, tier.ChromaticAberration);
            SetFloatProp(PropNoiseIntensity, tier.NoiseIntensity);
            SetFloatProp(PropGlitchInterval, tier.GlitchInterval);
            SetFloatProp(PropGlowMultiplier, tier.GlowMultiplier);
            SetFloatProp(PropDotGridSpacing, tier.DotGridSpacing);
            SetFloatProp(PropTextJitter, tier.TextJitter);
            SetFloatProp(PropDotGridAnimates, tier.DotGridAnimates ? 1f : 0f);
            SetFloatProp(PropSecondaryHalo, tier.SecondaryHalo ? 1f : 0f);
            SetFloatProp(PropEdgeParticles, tier.EdgeParticles ? 1f : 0f);
            SetFloatProp(PropEffectiveScanlines, tier.ScanlineIntensity);
            SetFloatProp(PropEffectiveNoise, tier.NoiseIntensity);
            SetFloatProp(PropEffectiveAberration, tier.ChromaticAberration);
            SetFloatProp(PropPrecursorInterference, 0f);
        }

        // ------------------------------------------------------------------
        // Property setters (VisualElement.SetProperty for typed data)
        // ------------------------------------------------------------------

        private void SetColorProp(string name, Color color)
        {
            _root.SetProperty(name, color);
        }

        private void SetFloatProp(string name, float value)
        {
            _root.SetProperty(name, value);
        }

        // ------------------------------------------------------------------
        // Utility: walk up the tree to find the bridge root
        // ------------------------------------------------------------------

        private static VisualElement FindBridgeRoot(VisualElement element)
        {
            // The bridge root is the UIDocument root. Walk up to the panel root.
            var current = element;
            while (current != null)
            {
                // Check if this element has bridge properties set (fast check)
                if (current.GetProperty(PropTier) != null)
                    return current;
                current = current.parent;
            }
            return null;
        }
    }
}

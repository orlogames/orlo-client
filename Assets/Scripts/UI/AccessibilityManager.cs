using System;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Singleton managing all accessibility settings: colorblind modes, UI scale,
    /// font size, flash effects toggle. Persists to PlayerPrefs as JSON.
    /// All UI scripts call RemapColor() for colorblind-safe rendering.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        public static AccessibilityManager Instance { get; private set; }

        private const string PrefsKey = "OrloAccessibility";

        // ── Settings ────────────────────────────────────────────────────
        public ColorblindMode ColorblindMode { get; set; } = ColorblindMode.Normal;
        public float UIScale { get; set; } = 1.0f;          // 0.75 - 2.0
        public float FontSizeMultiplier { get; set; } = 1.0f; // 0.75 - 2.0
        public bool FlashEffectsEnabled { get; set; } = true;

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ── Persistence ─────────────────────────────────────────────────

        [Serializable]
        private class SaveData
        {
            public int colorblindMode;
            public float uiScale = 1f;
            public float fontSizeMultiplier = 1f;
            public bool flashEffects = true;
        }

        public void Save()
        {
            var data = new SaveData
            {
                colorblindMode = (int)ColorblindMode,
                uiScale = UIScale,
                fontSizeMultiplier = FontSizeMultiplier,
                flashEffects = FlashEffectsEnabled
            };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Debug.Log("[AccessibilityManager] Settings saved.");
        }

        private void Load()
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(PrefsKey));
                ColorblindMode = (ColorblindMode)data.colorblindMode;
                UIScale = Mathf.Clamp(data.uiScale, 0.75f, 2.0f);
                FontSizeMultiplier = Mathf.Clamp(data.fontSizeMultiplier, 0.75f, 2.0f);
                FlashEffectsEnabled = data.flashEffects;
                Debug.Log($"[AccessibilityManager] Loaded — mode={ColorblindMode}, scale={UIScale}, font={FontSizeMultiplier}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AccessibilityManager] Failed to load: {e.Message}");
            }
        }

        // ── Color Remapping ─────────────────────────────────────────────
        //
        // Key game colors and their remapped equivalents per mode.
        // Other scripts call RemapColor() on any color before rendering.

        /// <summary>
        /// Remap a color for the active colorblind mode.
        /// Recognizes key game palette colors and substitutes accessible alternatives.
        /// Unknown colors pass through with a channel-level simulation.
        /// </summary>
        public Color RemapColor(Color c)
        {
            if (ColorblindMode == ColorblindMode.Normal)
                return c;

            // Try exact-match palette swap first (within tolerance)
            Color? paletteSwap = TryPaletteSwap(c);
            if (paletteSwap.HasValue)
                return paletteSwap.Value;

            // Fallback: channel-level simulation shift
            return SimulateShift(c);
        }

        /// <summary>
        /// Convenience: remap a color with a specified alpha override.
        /// </summary>
        public Color RemapColor(Color c, float alpha)
        {
            Color remapped = RemapColor(c);
            remapped.a = alpha;
            return remapped;
        }

        // ── Palette definitions ─────────────────────────────────────────
        //
        // Each entry: (original color, protanopia, deuteranopia, tritanopia)
        // Colors matched with ~0.1 tolerance per channel.

        private struct PaletteEntry
        {
            public Color Original;
            public Color Protanopia;
            public Color Deuteranopia;
            public Color Tritanopia;
        }

        // Health red (Vitality bar, enemy names, damage)
        private static readonly Color HealthRed = new Color(0.85f, 0.15f, 0.15f);
        // Stamina green
        private static readonly Color StaminaGreen = new Color(0.15f, 0.75f, 0.25f);
        // Focus blue
        private static readonly Color FocusBlue = new Color(0.25f, 0.45f, 0.95f);
        // Enemy name red
        private static readonly Color EnemyRed = new Color(1f, 0.4f, 0.4f);
        // Friendly green
        private static readonly Color FriendlyGreen = new Color(0.5f, 1f, 0.5f);
        // Target hostile border
        private static readonly Color HostileBorder = new Color(0.8f, 0.2f, 0.2f);
        // Target friendly border
        private static readonly Color FriendlyBorder = new Color(0.3f, 0.6f, 0.3f);

        // Rarity colors
        private static readonly Color RarityUncommon = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color RarityRare = new Color(0.2f, 0.5f, 1.0f);
        private static readonly Color RarityEpic = new Color(0.7f, 0.3f, 0.9f);
        private static readonly Color RarityLegendary = new Color(1.0f, 0.6f, 0.1f);

        private static readonly PaletteEntry[] Palette = new[]
        {
            // Health / Vitality (red → orange for protanopia/deuteranopia)
            new PaletteEntry {
                Original = HealthRed,
                Protanopia    = new Color(0.95f, 0.55f, 0.1f),   // orange
                Deuteranopia  = new Color(0.95f, 0.55f, 0.1f),
                Tritanopia    = new Color(0.85f, 0.15f, 0.15f)   // unchanged (red is fine)
            },
            // Stamina (green → yellow for protanopia/deuteranopia)
            new PaletteEntry {
                Original = StaminaGreen,
                Protanopia    = new Color(0.95f, 0.85f, 0.15f),  // yellow
                Deuteranopia  = new Color(0.95f, 0.85f, 0.15f),
                Tritanopia    = new Color(0.15f, 0.75f, 0.25f)   // unchanged
            },
            // Focus (blue → cyan for tritanopia)
            new PaletteEntry {
                Original = FocusBlue,
                Protanopia    = new Color(0.25f, 0.45f, 0.95f),  // unchanged
                Deuteranopia  = new Color(0.25f, 0.45f, 0.95f),
                Tritanopia    = new Color(0.1f, 0.85f, 0.85f)    // cyan
            },
            // Enemy names
            new PaletteEntry {
                Original = EnemyRed,
                Protanopia    = new Color(1f, 0.7f, 0.2f),       // orange-yellow
                Deuteranopia  = new Color(1f, 0.7f, 0.2f),
                Tritanopia    = new Color(1f, 0.4f, 0.4f)
            },
            // Friendly names
            new PaletteEntry {
                Original = FriendlyGreen,
                Protanopia    = new Color(0.3f, 0.9f, 1f),       // cyan
                Deuteranopia  = new Color(0.3f, 0.9f, 1f),
                Tritanopia    = new Color(0.5f, 1f, 0.5f)
            },
            // Hostile border
            new PaletteEntry {
                Original = HostileBorder,
                Protanopia    = new Color(0.9f, 0.5f, 0.1f),
                Deuteranopia  = new Color(0.9f, 0.5f, 0.1f),
                Tritanopia    = new Color(0.8f, 0.2f, 0.2f)
            },
            // Friendly border
            new PaletteEntry {
                Original = FriendlyBorder,
                Protanopia    = new Color(0.2f, 0.6f, 0.9f),
                Deuteranopia  = new Color(0.2f, 0.6f, 0.9f),
                Tritanopia    = new Color(0.3f, 0.6f, 0.3f)
            },
            // Rarity: Uncommon green → yellow
            new PaletteEntry {
                Original = RarityUncommon,
                Protanopia    = new Color(0.85f, 0.85f, 0.2f),
                Deuteranopia  = new Color(0.85f, 0.85f, 0.2f),
                Tritanopia    = new Color(0.2f, 0.8f, 0.2f)
            },
            // Rarity: Rare blue → brighter blue
            new PaletteEntry {
                Original = RarityRare,
                Protanopia    = new Color(0.3f, 0.6f, 1.0f),
                Deuteranopia  = new Color(0.3f, 0.6f, 1.0f),
                Tritanopia    = new Color(0.1f, 0.8f, 0.9f)
            },
            // Rarity: Epic purple (generally fine, slight adjustments)
            new PaletteEntry {
                Original = RarityEpic,
                Protanopia    = new Color(0.6f, 0.4f, 1.0f),
                Deuteranopia  = new Color(0.6f, 0.4f, 1.0f),
                Tritanopia    = new Color(0.8f, 0.3f, 0.7f)
            },
            // Rarity: Legendary orange (generally fine)
            new PaletteEntry {
                Original = RarityLegendary,
                Protanopia    = new Color(1.0f, 0.7f, 0.1f),
                Deuteranopia  = new Color(1.0f, 0.7f, 0.1f),
                Tritanopia    = new Color(1.0f, 0.5f, 0.2f)
            },
        };

        private const float Tolerance = 0.12f;

        private Color? TryPaletteSwap(Color c)
        {
            foreach (var entry in Palette)
            {
                if (ColorClose(c, entry.Original))
                {
                    return ColorblindMode switch
                    {
                        ColorblindMode.Protanopia   => entry.Protanopia,
                        ColorblindMode.Deuteranopia => entry.Deuteranopia,
                        ColorblindMode.Tritanopia   => entry.Tritanopia,
                        _ => null
                    };
                }
            }
            return null;
        }

        private static bool ColorClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < Tolerance
                && Mathf.Abs(a.g - b.g) < Tolerance
                && Mathf.Abs(a.b - b.b) < Tolerance;
        }

        /// <summary>
        /// Simple channel-level shift for colors not in the palette.
        /// Shifts problematic channels to preserve distinguishability.
        /// </summary>
        private Color SimulateShift(Color c)
        {
            switch (ColorblindMode)
            {
                case ColorblindMode.Protanopia:
                    // Reduce red channel contribution, boost green distinction
                    return new Color(
                        c.r * 0.56f + c.g * 0.44f,
                        c.g * 0.7f + c.r * 0.3f,
                        c.b,
                        c.a);
                case ColorblindMode.Deuteranopia:
                    // Similar to protanopia — merge red/green
                    return new Color(
                        c.r * 0.63f + c.g * 0.37f,
                        c.g * 0.6f + c.r * 0.4f,
                        c.b,
                        c.a);
                case ColorblindMode.Tritanopia:
                    // Blue-yellow confusion — shift blue toward cyan
                    return new Color(
                        c.r,
                        c.g * 0.7f + c.b * 0.3f,
                        c.b * 0.56f + c.g * 0.44f,
                        c.a);
                default:
                    return c;
            }
        }
    }

    public enum ColorblindMode
    {
        Normal = 0,
        Protanopia = 1,    // Red-blind
        Deuteranopia = 2,  // Green-blind
        Tritanopia = 3     // Blue-blind
    }
}

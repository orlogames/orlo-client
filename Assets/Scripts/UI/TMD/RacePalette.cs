using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Complete color system for a single race's TMD holographic UI.
    /// Every UI element draws its colors from the active palette.
    /// </summary>
    [System.Serializable]
    public class RacePalette
    {
        public string RaceName;
        public Color Primary;
        public Color Secondary;
        public Color Accent;
        public Color Background;
        public Color Border;
        public Color Text;
        public Color Glow;
        public Color Danger;
        public Color Success;

        // Derived colors (computed once)
        public Color PrimaryDim => Color.Lerp(Primary, Color.black, 0.5f);
        public Color GlowHalf => new Color(Glow.r, Glow.g, Glow.b, 0.5f);
        public Color BackgroundSolid => new Color(Background.r, Background.g, Background.b, 1f);
        public Color BorderGlow => new Color(Border.r, Border.g, Border.b, 0.2f);
        public Color TextDim => Color.Lerp(Text, Color.black, 0.4f);
        public Color PanelBackground => new Color(Background.r, Background.g, Background.b, 0.92f);

        /// <summary>Solari — warm gold, human-descended</summary>
        public static RacePalette Solari => new RacePalette
        {
            RaceName = "Solari",
            Primary    = HexColor("FFD700"),
            Secondary  = HexColor("FFA500"),
            Accent     = HexColor("FFFFFF"),
            Background = new Color(0.102f, 0.078f, 0.031f, 0.92f), // #1A1408
            Border     = HexColor("4A3A20"),
            Text       = HexColor("F5E6C8"),
            Glow       = HexColor("FFE066"),
            Danger     = HexColor("FF4444"),
            Success    = HexColor("66FF66"),
        };

        /// <summary>Vael — bioluminescent green, plant-symbiotic</summary>
        public static RacePalette Vael => new RacePalette
        {
            RaceName = "Vael",
            Primary    = HexColor("00FF88"),
            Secondary  = HexColor("44DDAA"),
            Accent     = HexColor("AAFFCC"),
            Background = new Color(0.031f, 0.102f, 0.063f, 0.92f), // #081A10
            Border     = HexColor("1A4A2A"),
            Text       = HexColor("C8F5D8"),
            Glow       = HexColor("66FFB2"),
            Danger     = HexColor("FF6644"),
            Success    = HexColor("88FF88"),
        };

        /// <summary>Korrath — ember orange, volcanic-adapted</summary>
        public static RacePalette Korrath => new RacePalette
        {
            RaceName = "Korrath",
            Primary    = HexColor("FF6600"),
            Secondary  = HexColor("FF4400"),
            Accent     = HexColor("FFCC88"),
            Background = new Color(0.102f, 0.047f, 0.016f, 0.92f), // #1A0C04
            Border     = HexColor("4A2A10"),
            Text       = HexColor("F5D8C0"),
            Glow       = HexColor("FF8833"),
            Danger     = HexColor("FF2222"),
            Success    = HexColor("88FF44"),
        };

        /// <summary>Thyren — void purple, void-touched</summary>
        public static RacePalette Thyren => new RacePalette
        {
            RaceName = "Thyren",
            Primary    = HexColor("AA66FF"),
            Secondary  = HexColor("6644CC"),
            Accent     = HexColor("DDBBFF"),
            Background = new Color(0.047f, 0.031f, 0.086f, 0.92f), // #0C0816
            Border     = HexColor("2A1A4A"),
            Text       = HexColor("D8C8F5"),
            Glow       = HexColor("BB88FF"),
            Danger     = HexColor("FF4466"),
            Success    = HexColor("66FFAA"),
        };

        public static RacePalette ForRace(string raceName)
        {
            if (string.IsNullOrEmpty(raceName)) return Solari;
            switch (raceName.ToLowerInvariant())
            {
                case "solari": return Solari;
                case "vael": return Vael;
                case "korrath": return Korrath;
                case "thyren": return Thyren;
                default: return Solari;
            }
        }

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) return c;
            return Color.magenta;
        }
    }
}

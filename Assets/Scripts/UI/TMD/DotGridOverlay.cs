using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// GMUNK-inspired dot-grid substrate rendered as a screen-space OnGUI overlay.
    /// Faint dots at tier-defined spacing form a holographic background lattice.
    /// Dots near active UI panels glow brighter in race color.
    /// Tier 5 enables a breathing animation pattern.
    /// </summary>
    public class DotGridOverlay : MonoBehaviour
    {
        public static DotGridOverlay Instance { get; private set; }

        /// <summary>
        /// Set to true by any TMD panel when it is open.
        /// When true, dots near center-screen glow brighter.
        /// </summary>
        public static bool IsAnyPanelOpen;

        private const int DotSize = 2;
        private const float BaseDotAlpha = 0.04f;
        private const float BrightDotAlpha = 0.12f;
        private const float PanelGlowRadius = 300f; // pixels from screen center

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            if (TMDTheme.Instance == null) return;
            if (Event.current.type != EventType.Repaint) return;

            var tier = TMDTheme.Instance.TierSettings;
            float spacing = tier.DotGridSpacing;
            if (spacing < 4f) spacing = 4f; // Safety minimum

            Color raceColor = TMDTheme.Instance.Palette.Primary;
            float breathe = 0f;

            if (tier.DotGridAnimates)
            {
                // 4-second breathing cycle matching TMDTheme.BreathePhase
                breathe = Mathf.Sin(Time.time * Mathf.PI * 0.5f) * 0.5f + 0.5f; // 0..1
            }

            float sw = Screen.width;
            float sh = Screen.height;
            float cx = sw * 0.5f;
            float cy = sh * 0.5f;

            // Sample every Nth dot to keep performance light.
            // At spacing=4 on a 1920x1080 screen that's ~129,600 dots — too many.
            // Skip factor ensures we draw at most ~2500 dots.
            int maxDotsPerAxis = 50;
            float stepX = Mathf.Max(spacing, sw / maxDotsPerAxis);
            float stepY = Mathf.Max(spacing, sh / maxDotsPerAxis);

            for (float x = stepX * 0.5f; x < sw; x += stepX)
            {
                for (float y = stepY * 0.5f; y < sh; y += stepY)
                {
                    float alpha = BaseDotAlpha;

                    // Brighter near screen center when a panel is open
                    if (IsAnyPanelOpen)
                    {
                        float dx = x - cx;
                        float dy = y - cy;
                        float distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                        float glow = 1f - Mathf.Clamp01(distFromCenter / PanelGlowRadius);
                        alpha = Mathf.Lerp(BaseDotAlpha, BrightDotAlpha, glow);
                    }

                    // Breathing modulation for Tier 5
                    if (tier.DotGridAnimates)
                    {
                        // Radial wave from center
                        float dx = x - cx;
                        float dy = y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float wave = Mathf.Sin(dist * 0.02f - Time.time * 2f) * 0.5f + 0.5f;
                        alpha *= 0.6f + 0.4f * wave;
                        alpha *= 0.8f + 0.2f * breathe;
                    }

                    GUI.color = new Color(raceColor.r, raceColor.g, raceColor.b, alpha);
                    GUI.DrawTexture(new Rect(x, y, DotSize, DotSize), Texture2D.whiteTexture);
                }
            }

            GUI.color = Color.white;
        }
    }
}

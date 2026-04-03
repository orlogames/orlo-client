using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Global UI scaling helpers for OnGUI-based UI.
    /// Reads scale factor and font size multiplier from AccessibilityManager.
    /// All OnGUI scripts should call ScaledRect() and ScaledFontSize() for consistent scaling.
    /// </summary>
    public static class UIScaler
    {
        // ── Scale Factor ────────────────────────────────────────────────

        /// <summary>
        /// Current global UI scale factor (0.75 - 2.0).
        /// Falls back to 1.0 if AccessibilityManager is not initialized.
        /// </summary>
        public static float Scale
        {
            get
            {
                if (AccessibilityManager.Instance != null)
                    return AccessibilityManager.Instance.UIScale;
                return 1.0f;
            }
        }

        /// <summary>
        /// Current font size multiplier (0.75 - 2.0).
        /// </summary>
        public static float FontScale
        {
            get
            {
                if (AccessibilityManager.Instance != null)
                    return AccessibilityManager.Instance.FontSizeMultiplier;
                return 1.0f;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Scale a Rect by the global UI scale factor.
        /// Position is scaled relative to its anchor point, size is scaled uniformly.
        /// </summary>
        public static Rect ScaledRect(Rect r)
        {
            float s = Scale;
            if (Mathf.Approximately(s, 1f)) return r;
            return new Rect(r.x * s, r.y * s, r.width * s, r.height * s);
        }

        /// <summary>
        /// Scale a Rect by the global UI scale, anchored from a specific screen position.
        /// Useful for centered or right-aligned elements where position should not drift.
        /// </summary>
        public static Rect ScaledRect(Rect r, float anchorX, float anchorY)
        {
            float s = Scale;
            if (Mathf.Approximately(s, 1f)) return r;
            float newW = r.width * s;
            float newH = r.height * s;
            float newX = anchorX + (r.x - anchorX) * s;
            float newY = anchorY + (r.y - anchorY) * s;
            return new Rect(newX, newY, newW, newH);
        }

        /// <summary>
        /// Scale a font size by the font multiplier.
        /// Returns at least 8 to prevent unreadable text.
        /// </summary>
        public static int ScaledFontSize(int baseSize)
        {
            return Mathf.Max(8, Mathf.RoundToInt(baseSize * FontScale));
        }

        /// <summary>
        /// Scale a float dimension by the global UI scale.
        /// </summary>
        public static float ScaledValue(float value)
        {
            return value * Scale;
        }

        /// <summary>
        /// Scale an integer dimension by the global UI scale.
        /// </summary>
        public static int ScaledValue(int value)
        {
            return Mathf.RoundToInt(value * Scale);
        }
    }
}

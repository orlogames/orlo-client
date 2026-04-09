namespace Orlo.UI.Animation
{
    /// <summary>
    /// Named spring presets from the UI redesign spec (section 3.6).
    /// Each preset defines stiffness and damping ratio for common animation curves.
    /// </summary>
    public readonly struct SpringPreset
    {
        public readonly float Stiffness;
        public readonly float Damping;

        public SpringPreset(float stiffness, float damping)
        {
            Stiffness = stiffness;
            Damping = damping;
        }
    }

    public static class SpringPresets
    {
        /// <summary>Panel close — fast, no overshoot. spring(400, 1.0)</summary>
        public static readonly SpringPreset EaseOutCubic = new SpringPreset(400f, 1.0f);

        /// <summary>Panel open, notifications — bouncy. spring(300, 0.65)</summary>
        public static readonly SpringPreset EaseOutBounce = new SpringPreset(300f, 0.65f);

        /// <summary>Tab switches — smooth in-out. spring(250, 0.85)</summary>
        public static readonly SpringPreset EaseInOut = new SpringPreset(250f, 0.85f);

        /// <summary>Hover scale — responsive with slight overshoot. spring(350, 0.75)</summary>
        public static readonly SpringPreset EaseHover = new SpringPreset(350f, 0.75f);

        // --- Additional presets carried over from TMD SpringPresets ---

        /// <summary>Click squish — fast compress then bounce back.</summary>
        public static readonly SpringPreset ClickSquish = new SpringPreset(600f, 0.55f);

        /// <summary>Panel open with more overshoot than EaseOutBounce.</summary>
        public static readonly SpringPreset PanelOpen = new SpringPreset(400f, 0.65f);

        /// <summary>Panel close — fast, critically damped.</summary>
        public static readonly SpringPreset PanelClose = new SpringPreset(500f, 1.0f);

        /// <summary>Tab underline slide.</summary>
        public static readonly SpringPreset TabSlide = new SpringPreset(350f, 0.7f);

        /// <summary>Notification slide-in.</summary>
        public static readonly SpringPreset NotificationSlide = new SpringPreset(300f, 0.75f);

        /// <summary>Color transition (race palette, theme changes).</summary>
        public static readonly SpringPreset ColorTransition = new SpringPreset(150f, 0.9f);

        /// <summary>Drag momentum / scroll rubber-band.</summary>
        public static readonly SpringPreset DragMomentum = new SpringPreset(250f, 0.8f);

        /// <summary>Iris open (panel scale from center).</summary>
        public static readonly SpringPreset IrisOpen = new SpringPreset(450f, 0.6f);

        /// <summary>Scroll rubber-band at list ends.</summary>
        public static readonly SpringPreset ScrollRubberBand = new SpringPreset(400f, 0.6f);
    }
}

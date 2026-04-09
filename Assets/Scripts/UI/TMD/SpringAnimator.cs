using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Zero-allocation spring physics for UI micro-animations.
    /// Replaces PrimeTween dependency — lightweight, no external packages needed.
    ///
    /// Usage:
    ///   var spring = new SpringValue(startValue);
    ///   spring.Target = 1.0f;        // set target, spring will animate
    ///   spring.Update(deltaTime);    // call per frame
    ///   float current = spring.Value; // read animated value
    /// </summary>
    [System.Serializable]
    public struct SpringValue
    {
        public float Value;
        public float Target;
        public float Velocity;

        /// <summary>Spring stiffness (higher = snappier). Default: 300</summary>
        public float Stiffness;
        /// <summary>Damping ratio (1.0 = critically damped, &lt;1 = bouncy). Default: 0.7</summary>
        public float Damping;

        public SpringValue(float initial, float stiffness = 300f, float damping = 0.7f)
        {
            Value = initial;
            Target = initial;
            Velocity = 0f;
            Stiffness = stiffness;
            Damping = damping;
        }

        /// <summary>Advance the spring by dt seconds.</summary>
        public void Update(float dt)
        {
            // Semi-implicit Euler with damped harmonic oscillator
            float displacement = Value - Target;
            float dampingForce = 2f * Damping * Mathf.Sqrt(Stiffness) * Velocity;
            float springForce = Stiffness * displacement;
            float acceleration = -(springForce + dampingForce);

            Velocity += acceleration * dt;
            Value += Velocity * dt;

            // Snap to target if close enough (avoid eternal micro-oscillation)
            if (Mathf.Abs(displacement) < 0.0001f && Mathf.Abs(Velocity) < 0.001f)
            {
                Value = Target;
                Velocity = 0f;
            }
        }

        /// <summary>True when the spring has settled at its target.</summary>
        public bool IsSettled => Mathf.Abs(Value - Target) < 0.0001f && Mathf.Abs(Velocity) < 0.001f;

        /// <summary>Instantly snap to target with no animation.</summary>
        public void Snap()
        {
            Value = Target;
            Velocity = 0f;
        }

        /// <summary>Set target and give an initial velocity kick (for impacts, clicks).</summary>
        public void Kick(float target, float initialVelocity)
        {
            Target = target;
            Velocity = initialVelocity;
        }
    }

    /// <summary>
    /// 2D spring for animating positions, scales, or UV offsets.
    /// </summary>
    [System.Serializable]
    public struct SpringValue2
    {
        public SpringValue X;
        public SpringValue Y;

        public Vector2 Value => new Vector2(X.Value, Y.Value);
        public bool IsSettled => X.IsSettled && Y.IsSettled;

        public Vector2 Target
        {
            get => new Vector2(X.Target, Y.Target);
            set { X.Target = value.x; Y.Target = value.y; }
        }

        public SpringValue2(Vector2 initial, float stiffness = 300f, float damping = 0.7f)
        {
            X = new SpringValue(initial.x, stiffness, damping);
            Y = new SpringValue(initial.y, stiffness, damping);
        }

        public void Update(float dt)
        {
            X.Update(dt);
            Y.Update(dt);
        }

        public void Snap()
        {
            X.Snap();
            Y.Snap();
        }
    }

    /// <summary>
    /// Color spring for smooth race-palette transitions.
    /// </summary>
    [System.Serializable]
    public struct SpringColor
    {
        public SpringValue R, G, B, A;

        public Color Value => new Color(R.Value, G.Value, B.Value, A.Value);
        public bool IsSettled => R.IsSettled && G.IsSettled && B.IsSettled && A.IsSettled;

        public Color Target
        {
            get => new Color(R.Target, G.Target, B.Target, A.Target);
            set { R.Target = value.r; G.Target = value.g; B.Target = value.b; A.Target = value.a; }
        }

        public SpringColor(Color initial, float stiffness = 200f, float damping = 0.85f)
        {
            R = new SpringValue(initial.r, stiffness, damping);
            G = new SpringValue(initial.g, stiffness, damping);
            B = new SpringValue(initial.b, stiffness, damping);
            A = new SpringValue(initial.a, stiffness, damping);
        }

        public void Update(float dt)
        {
            R.Update(dt);
            G.Update(dt);
            B.Update(dt);
            A.Update(dt);
        }

        public void Snap()
        {
            R.Snap();
            G.Snap();
            B.Snap();
            A.Snap();
        }
    }

    /// <summary>
    /// Pre-configured spring presets for common UI animations.
    /// </summary>
    public static class SpringPresets
    {
        /// <summary>Panel open — quick with slight overshoot (0.25s feel)</summary>
        public static SpringValue PanelOpen(float from, float to) =>
            new SpringValue(from, 400f, 0.65f) { Target = to };

        /// <summary>Panel close — fast, no overshoot (0.15s feel)</summary>
        public static SpringValue PanelClose(float from, float to) =>
            new SpringValue(from, 500f, 1.0f) { Target = to };

        /// <summary>Hover scale — subtle, responsive</summary>
        public static SpringValue HoverScale() =>
            new SpringValue(1.0f, 350f, 0.75f);

        /// <summary>Click squish — fast compress then bounce back</summary>
        public static SpringValue ClickSquish()
        {
            var s = new SpringValue(1.0f, 600f, 0.55f);
            s.Kick(1.0f, -3.0f); // negative kick compresses first
            return s;
        }

        /// <summary>Tab underline slide — smooth with spring</summary>
        public static SpringValue TabSlide(float from, float to) =>
            new SpringValue(from, 350f, 0.7f) { Target = to };

        /// <summary>Notification slide-in from right</summary>
        public static SpringValue NotificationSlideIn(float screenWidth) =>
            new SpringValue(screenWidth + 300f, 300f, 0.75f) { Target = screenWidth - 320f };

        /// <summary>Iris open (panel open from center) — both X and Y scale</summary>
        public static SpringValue2 IrisOpen() =>
            new SpringValue2(Vector2.zero, 450f, 0.6f) { Target = Vector2.one };

        /// <summary>Color transition when switching races</summary>
        public static SpringColor RaceTransition(Color from, Color to) =>
            new SpringColor(from, 150f, 0.9f) { Target = to };

        /// <summary>Item drag momentum</summary>
        public static SpringValue2 DragMomentum(Vector2 from, Vector2 to) =>
            new SpringValue2(from, 250f, 0.8f) { Target = to };

        /// <summary>Scroll rubber-band at list ends</summary>
        public static SpringValue ScrollRubberBand(float from) =>
            new SpringValue(from, 400f, 0.6f) { Target = 0f };
    }
}

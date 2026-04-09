using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.Animation
{
    /// <summary>
    /// Animates a Vector2 value using spring physics, driving VisualElement style properties
    /// such as position, size, or translate. Port of SpringValue2 from TMD/SpringAnimator.cs.
    /// </summary>
    public class UISpringVector2
    {
        private readonly UISpringFloat _x;
        private readonly UISpringFloat _y;

        private VisualElement _boundElement;
        private IVisualElementScheduledItem _scheduledUpdate;
        private System.Action<VisualElement, Vector2> _applier;

        public Vector2 Value => new Vector2(_x.Value, _y.Value);
        public Vector2 Target => new Vector2(_x.Target, _y.Target);
        public bool IsSettled => _x.IsSettled && _y.IsSettled;

        public float Stiffness
        {
            get => _x.Stiffness;
            set { _x.Stiffness = value; _y.Stiffness = value; }
        }

        public float Damping
        {
            get => _x.Damping;
            set { _x.Damping = value; _y.Damping = value; }
        }

        public UISpringVector2(Vector2 initial, float stiffness = 300f, float damping = 0.7f)
        {
            _x = new UISpringFloat(initial.x, stiffness, damping);
            _y = new UISpringFloat(initial.y, stiffness, damping);
        }

        /// <summary>
        /// Bind this spring to a VisualElement. The applier receives (element, currentValue).
        /// Unlike UISpringFloat, scheduling is managed here — do NOT also bind the inner springs.
        /// </summary>
        public UISpringVector2 Bind(VisualElement element, System.Action<VisualElement, Vector2> applier)
        {
            Cancel();
            _boundElement = element;
            _applier = applier;
            return this;
        }

        /// <summary>Set a new target value, starting the spring animation.</summary>
        public void SetTarget(Vector2 target)
        {
            _x.SetTarget(target.x);
            _y.SetTarget(target.y);
            // Cancel inner schedules — we drive stepping ourselves
            _x.Cancel();
            _y.Cancel();
            EnsureScheduled();
        }

        /// <summary>Instantly snap to a value with no animation.</summary>
        public void Snap(Vector2 value)
        {
            _x.Snap(value.x);
            _y.Snap(value.y);
            _applier?.Invoke(_boundElement, Value);
        }

        /// <summary>Set target with initial velocity kick.</summary>
        public void Kick(Vector2 target, Vector2 initialVelocity)
        {
            _x.Kick(target.x, initialVelocity.x);
            _y.Kick(target.y, initialVelocity.y);
            _x.Cancel();
            _y.Cancel();
            EnsureScheduled();
        }

        /// <summary>Cancel the scheduled update loop.</summary>
        public void Cancel()
        {
            _x.Cancel();
            _y.Cancel();
            if (_scheduledUpdate != null)
            {
                _scheduledUpdate.Pause();
                _scheduledUpdate = null;
            }
        }

        private void EnsureScheduled()
        {
            if (_boundElement == null || _applier == null) return;
            if (_scheduledUpdate != null) return;

            _scheduledUpdate = _boundElement.schedule.Execute(OnFrame).Every(16);
        }

        private void OnFrame(TimerState ts)
        {
            float dt = Mathf.Min(ts.deltaTime / 1000f, 0.05f);
            if (dt <= 0f) dt = 0.016f;

            _x.Step(dt);
            _y.Step(dt);
            _applier?.Invoke(_boundElement, Value);

            if (IsSettled)
            {
                _applier?.Invoke(_boundElement, new Vector2(_x.Target, _y.Target));
                Cancel();
            }
        }
    }
}

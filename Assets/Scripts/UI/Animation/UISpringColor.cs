using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.Animation
{
    /// <summary>
    /// Animates a Color value using spring physics, driving VisualElement style properties
    /// such as background-color, border-color, or tint. Port of SpringColor from TMD/SpringAnimator.cs.
    /// </summary>
    public class UISpringColor
    {
        private readonly UISpringFloat _r;
        private readonly UISpringFloat _g;
        private readonly UISpringFloat _b;
        private readonly UISpringFloat _a;

        private VisualElement _boundElement;
        private IVisualElementScheduledItem _scheduledUpdate;
        private System.Action<VisualElement, Color> _applier;

        public Color Value => new Color(_r.Value, _g.Value, _b.Value, _a.Value);
        public Color Target => new Color(_r.Target, _g.Target, _b.Target, _a.Target);
        public bool IsSettled => _r.IsSettled && _g.IsSettled && _b.IsSettled && _a.IsSettled;

        public float Stiffness
        {
            get => _r.Stiffness;
            set { _r.Stiffness = value; _g.Stiffness = value; _b.Stiffness = value; _a.Stiffness = value; }
        }

        public float Damping
        {
            get => _r.Damping;
            set { _r.Damping = value; _g.Damping = value; _b.Damping = value; _a.Damping = value; }
        }

        public UISpringColor(Color initial, float stiffness = 200f, float damping = 0.85f)
        {
            _r = new UISpringFloat(initial.r, stiffness, damping);
            _g = new UISpringFloat(initial.g, stiffness, damping);
            _b = new UISpringFloat(initial.b, stiffness, damping);
            _a = new UISpringFloat(initial.a, stiffness, damping);
        }

        /// <summary>
        /// Bind this spring to a VisualElement. The applier receives (element, currentColor).
        /// </summary>
        public UISpringColor Bind(VisualElement element, System.Action<VisualElement, Color> applier)
        {
            Cancel();
            _boundElement = element;
            _applier = applier;
            return this;
        }

        /// <summary>Set a new target color, starting the spring animation.</summary>
        public void SetTarget(Color target)
        {
            _r.SetTarget(target.r);
            _g.SetTarget(target.g);
            _b.SetTarget(target.b);
            _a.SetTarget(target.a);
            // Cancel inner schedules — we drive stepping ourselves
            _r.Cancel(); _g.Cancel(); _b.Cancel(); _a.Cancel();
            EnsureScheduled();
        }

        /// <summary>Instantly snap to a color with no animation.</summary>
        public void Snap(Color value)
        {
            _r.Snap(value.r);
            _g.Snap(value.g);
            _b.Snap(value.b);
            _a.Snap(value.a);
            _applier?.Invoke(_boundElement, Value);
        }

        /// <summary>Cancel the scheduled update loop.</summary>
        public void Cancel()
        {
            _r.Cancel(); _g.Cancel(); _b.Cancel(); _a.Cancel();
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

            _r.Step(dt);
            _g.Step(dt);
            _b.Step(dt);
            _a.Step(dt);
            _applier?.Invoke(_boundElement, Value);

            if (IsSettled)
            {
                _applier?.Invoke(_boundElement, Target);
                Cancel();
            }
        }
    }
}

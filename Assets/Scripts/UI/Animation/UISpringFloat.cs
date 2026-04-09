using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.Animation
{
    /// <summary>
    /// Animates a single float value using spring physics, driving VisualElement style properties.
    /// Port of SpringValue from TMD/SpringAnimator.cs for UI Toolkit.
    ///
    /// Uses VisualElement.schedule for frame updates — no MonoBehaviour required.
    /// </summary>
    public class UISpringFloat
    {
        private float _value;
        private float _target;
        private float _velocity;
        private float _stiffness;
        private float _damping;

        private VisualElement _boundElement;
        private IVisualElementScheduledItem _scheduledUpdate;
        private System.Action<VisualElement, float> _applier;

        // Settlement thresholds (match TMD SpringValue)
        private const float DisplacementThreshold = 0.0001f;
        private const float VelocityThreshold = 0.001f;

        public float Value => _value;
        public float Target => _target;
        public float Velocity => _velocity;
        public float Stiffness { get => _stiffness; set => _stiffness = value; }
        public float Damping { get => _damping; set => _damping = value; }

        /// <summary>True when the spring has settled at its target.</summary>
        public bool IsSettled =>
            Mathf.Abs(_value - _target) < DisplacementThreshold &&
            Mathf.Abs(_velocity) < VelocityThreshold;

        public UISpringFloat(float initial, float stiffness = 300f, float damping = 0.7f)
        {
            _value = initial;
            _target = initial;
            _velocity = 0f;
            _stiffness = stiffness;
            _damping = damping;
        }

        /// <summary>
        /// Bind this spring to a VisualElement, applying values each frame via the applier callback.
        /// The applier receives (element, currentValue) and should set the relevant style property.
        /// </summary>
        public UISpringFloat Bind(VisualElement element, System.Action<VisualElement, float> applier)
        {
            Cancel();
            _boundElement = element;
            _applier = applier;
            return this;
        }

        /// <summary>Set a new target value, starting the spring animation.</summary>
        public void SetTarget(float target)
        {
            _target = target;
            EnsureScheduled();
        }

        /// <summary>Instantly snap to a value with no animation.</summary>
        public void Snap(float value)
        {
            _value = value;
            _target = value;
            _velocity = 0f;
            _applier?.Invoke(_boundElement, _value);
        }

        /// <summary>Set target with an initial velocity kick (for click impacts, flings).</summary>
        public void Kick(float target, float initialVelocity)
        {
            _target = target;
            _velocity = initialVelocity;
            EnsureScheduled();
        }

        /// <summary>Advance the spring by dt seconds. Same math as TMD SpringValue.</summary>
        public void Step(float dt)
        {
            // Semi-implicit Euler with damped harmonic oscillator
            float displacement = _value - _target;
            float dampingForce = 2f * _damping * Mathf.Sqrt(_stiffness) * _velocity;
            float springForce = _stiffness * displacement;
            float acceleration = -(springForce + dampingForce);

            _velocity += acceleration * dt;
            _value += _velocity * dt;

            // Snap to target if close enough (avoid eternal micro-oscillation)
            if (Mathf.Abs(displacement) < DisplacementThreshold && Mathf.Abs(_velocity) < VelocityThreshold)
            {
                _value = _target;
                _velocity = 0f;
            }
        }

        /// <summary>Cancel the scheduled update loop.</summary>
        public void Cancel()
        {
            if (_scheduledUpdate != null)
            {
                _scheduledUpdate.Pause();
                _scheduledUpdate = null;
            }
        }

        private void EnsureScheduled()
        {
            if (_boundElement == null || _applier == null) return;
            if (_scheduledUpdate != null) return; // already running

            // Schedule at ~60fps (16ms). UI Toolkit schedule uses milliseconds.
            _scheduledUpdate = _boundElement.schedule.Execute(OnFrame).Every(16);
        }

        private void OnFrame(TimerState ts)
        {
            // Use unscaled delta so UI animates during pause
            float dt = Mathf.Min(ts.deltaTime / 1000f, 0.05f); // cap at 50ms to avoid explosion
            if (dt <= 0f) dt = 0.016f;

            Step(dt);
            _applier?.Invoke(_boundElement, _value);

            if (IsSettled)
            {
                _applier?.Invoke(_boundElement, _target);
                Cancel();
            }
        }
    }
}

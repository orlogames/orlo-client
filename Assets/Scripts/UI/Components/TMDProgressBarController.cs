using UnityEngine;
using UnityEngine.UIElements;
using Orlo.UI.TMD;

namespace Orlo.UI.Components
{
    /// <summary>
    /// Controller for TMDProgressBar.uxml instances.
    /// Handles animated fill transitions (spring-based), delayed damage visualization,
    /// and label formatting.
    ///
    /// The delayed fill shows where health *was* before damage, then drains down
    /// over a short duration to give visual feedback on damage taken.
    /// </summary>
    public class TMDProgressBarController
    {
        private VisualElement _root;
        private VisualElement _track;
        private VisualElement _fill;
        private VisualElement _fillDelayed;
        private Label _label;

        // Current values
        private float _currentValue;
        private float _maxValue = 1f;
        private float _displayedFillPercent;
        private float _delayedFillPercent;

        // Animation
        private float _targetFillPercent;
        private float _fillVelocity;
        private float _delayedVelocity;
        private float _delayedDrainTimer;

        // Configuration
        private float _fillSpringStiffness = 300f;
        private float _fillSpringDamping = 0.85f;
        private float _delayedDrainDelay = 0.5f;   // seconds before delayed fill starts draining
        private float _delayedDrainSpeed = 120f;     // percent per second for delayed drain
        private string _labelFormat = "{0}/{1}";     // e.g. "450/600"
        private bool _showLabel = true;
        private bool _animateFill = true;

        /// <summary>The root VisualElement of this progress bar.</summary>
        public VisualElement Root => _root;

        /// <summary>Current value (0 to MaxValue).</summary>
        public float Value => _currentValue;

        /// <summary>Maximum value.</summary>
        public float MaxValue => _maxValue;

        /// <summary>Current fill percentage (0-100), including animation.</summary>
        public float FillPercent => _displayedFillPercent;

        /// <summary>
        /// Format string for the label. Use {0} for current value, {1} for max value,
        /// {2} for percentage. Default: "{0}/{1}"
        /// </summary>
        public string LabelFormat
        {
            get => _labelFormat;
            set => _labelFormat = value;
        }

        /// <summary>Whether to show the text label overlay.</summary>
        public bool ShowLabel
        {
            get => _showLabel;
            set
            {
                _showLabel = value;
                if (_label != null)
                    _label.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Whether fill changes animate or snap immediately.</summary>
        public bool AnimateFill
        {
            get => _animateFill;
            set => _animateFill = value;
        }

        /// <summary>
        /// Bind the controller to an instantiated TMDProgressBar template root.
        /// </summary>
        public void Bind(VisualElement barRoot)
        {
            _root = barRoot.Q<VisualElement>("tmd-progress-bar") ?? barRoot;
            _track = _root.Q<VisualElement>("bar-track");
            _fill = _root.Q<VisualElement>("bar-fill");
            _fillDelayed = _root.Q<VisualElement>("bar-fill-delayed");
            _label = _root.Q<Label>("bar-label");

            // Initialize at zero
            SetFillImmediate(0f);
            SetDelayedFillImmediate(0f);

            if (_label != null)
                _label.style.display = _showLabel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Set the bar value. Animates the fill if AnimateFill is true.
        /// Triggers delayed damage visualization when value decreases.
        /// </summary>
        /// <param name="current">Current value (e.g., current health).</param>
        /// <param name="max">Maximum value (e.g., max health).</param>
        public void SetValue(float current, float max)
        {
            if (max <= 0f) max = 1f;

            float oldPercent = _targetFillPercent;
            _currentValue = Mathf.Clamp(current, 0f, max);
            _maxValue = max;
            _targetFillPercent = (_currentValue / _maxValue) * 100f;

            // Damage taken — set up delayed fill drain
            if (_targetFillPercent < oldPercent)
            {
                // Keep delayed fill at the old (higher) value
                if (_delayedFillPercent < oldPercent)
                    _delayedFillPercent = oldPercent;

                _delayedDrainTimer = _delayedDrainDelay;
            }

            if (!_animateFill)
            {
                _displayedFillPercent = _targetFillPercent;
                SetFillImmediate(_displayedFillPercent);
            }

            UpdateLabel();
        }

        /// <summary>
        /// Call each frame to drive fill animation and delayed drain.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime or equivalent.</param>
        public void Update(float deltaTime)
        {
            if (!_animateFill) return;

            // Spring-based fill animation
            AnimateSpring(ref _displayedFillPercent, ref _fillVelocity,
                         _targetFillPercent, _fillSpringStiffness, _fillSpringDamping, deltaTime);

            SetFillImmediate(_displayedFillPercent);

            // Delayed fill drain
            UpdateDelayedFill(deltaTime);
        }

        /// <summary>
        /// Set the fill color (e.g., red for vitality, green for stamina, blue for focus).
        /// </summary>
        public void SetFillColor(Color color)
        {
            if (_fill != null)
                _fill.style.backgroundColor = color;
        }

        /// <summary>
        /// Set the delayed fill color (typically a dimmer/desaturated version of the fill color).
        /// </summary>
        public void SetDelayedFillColor(Color color)
        {
            if (_fillDelayed != null)
                _fillDelayed.style.backgroundColor = color;
        }

        /// <summary>
        /// Set the track background color.
        /// </summary>
        public void SetTrackColor(Color color)
        {
            if (_track != null)
                _track.style.backgroundColor = color;
        }

        /// <summary>
        /// Apply race palette colors to the bar's border/glow.
        /// </summary>
        public void ApplyPalette(RacePalette palette)
        {
            if (palette == null || _root == null) return;

            _root.style.borderTopColor = palette.Border;
            _root.style.borderBottomColor = palette.Border;
            _root.style.borderLeftColor = palette.Border;
            _root.style.borderRightColor = palette.Border;
        }

        /// <summary>
        /// Configure animation parameters.
        /// </summary>
        /// <param name="stiffness">Spring stiffness (higher = faster snap). Default: 300.</param>
        /// <param name="damping">Spring damping (0-1, higher = less oscillation). Default: 0.85.</param>
        /// <param name="delayedDrainDelay">Seconds before delayed fill begins draining. Default: 0.5.</param>
        /// <param name="delayedDrainSpeed">Percent per second for delayed fill drain. Default: 120.</param>
        public void ConfigureAnimation(float stiffness = 300f, float damping = 0.85f,
                                        float delayedDrainDelay = 0.5f, float delayedDrainSpeed = 120f)
        {
            _fillSpringStiffness = stiffness;
            _fillSpringDamping = damping;
            _delayedDrainDelay = delayedDrainDelay;
            _delayedDrainSpeed = delayedDrainSpeed;
        }

        // -- Internal --

        private void SetFillImmediate(float percent)
        {
            if (_fill != null)
                _fill.style.width = new Length(Mathf.Clamp(percent, 0f, 100f), LengthUnit.Percent);
        }

        private void SetDelayedFillImmediate(float percent)
        {
            if (_fillDelayed != null)
                _fillDelayed.style.width = new Length(Mathf.Clamp(percent, 0f, 100f), LengthUnit.Percent);
        }

        private void UpdateDelayedFill(float deltaTime)
        {
            if (_fillDelayed == null) return;

            // Wait for drain delay
            if (_delayedDrainTimer > 0f)
            {
                _delayedDrainTimer -= deltaTime;
                SetDelayedFillImmediate(_delayedFillPercent);
                return;
            }

            // Drain toward current fill
            if (_delayedFillPercent > _displayedFillPercent + 0.1f)
            {
                _delayedFillPercent -= _delayedDrainSpeed * deltaTime;
                _delayedFillPercent = Mathf.Max(_delayedFillPercent, _displayedFillPercent);
                _fillDelayed.style.display = DisplayStyle.Flex;
            }
            else
            {
                _delayedFillPercent = _displayedFillPercent;
                _fillDelayed.style.display = DisplayStyle.None;
            }

            SetDelayedFillImmediate(_delayedFillPercent);
        }

        private void UpdateLabel()
        {
            if (_label == null || !_showLabel) return;

            int current = Mathf.RoundToInt(_currentValue);
            int max = Mathf.RoundToInt(_maxValue);
            int pct = _maxValue > 0 ? Mathf.RoundToInt((_currentValue / _maxValue) * 100f) : 0;

            _label.text = string.Format(_labelFormat, current, max, pct);
        }

        /// <summary>
        /// Simple critically-damped spring simulation matching the existing SpringValue system.
        /// </summary>
        private static void AnimateSpring(ref float current, ref float velocity,
                                           float target, float stiffness, float damping,
                                           float dt)
        {
            float diff = current - target;
            float springForce = -stiffness * diff;
            float dampForce = -2f * damping * Mathf.Sqrt(stiffness) * velocity;
            float acceleration = springForce + dampForce;

            velocity += acceleration * dt;
            current += velocity * dt;

            // Snap if close enough
            if (Mathf.Abs(diff) < 0.05f && Mathf.Abs(velocity) < 0.1f)
            {
                current = target;
                velocity = 0f;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.Animation
{
    /// <summary>
    /// Extension methods for VisualElement that provide convenient spring animations.
    /// Springs are tracked per-element so they can be cancelled cleanly.
    ///
    /// Usage:
    ///   element.SpringOpacity(1f, SpringPresets.EaseOutCubic);
    ///   element.SpringScale(new Vector2(1.05f, 1.05f), SpringPresets.EaseHover);
    ///   element.CancelSprings();
    /// </summary>
    public static class UISpringExtensions
    {
        // Track active springs per element for cancellation.
        // Key is (element, property-name) to allow multiple independent springs on one element.
        private static readonly Dictionary<(VisualElement, string), object> _activeSprings
            = new Dictionary<(VisualElement, string), object>();

        /// <summary>
        /// Animate a named style property to a target float value using a spring preset.
        /// Supported properties: "opacity", "width", "height", "left", "top", "right", "bottom",
        /// "rotate", "border-width", "border-radius", "font-size".
        /// For translate/scale, use SpringTranslate / SpringScale instead.
        /// </summary>
        public static UISpringFloat SpringTo(this VisualElement element, string property, float target, SpringPreset preset)
        {
            var key = (element, property);

            // Cancel existing spring for this property
            if (_activeSprings.TryGetValue(key, out var existing))
            {
                if (existing is UISpringFloat oldSpring)
                    oldSpring.Cancel();
            }

            var applier = GetFloatApplier(property);
            if (applier == null)
            {
                Debug.LogWarning($"[UISpring] Unknown property '{property}' — cannot animate.");
                return null;
            }

            // Read current value from style (approximate — we start from where we are)
            float current = ReadCurrentFloat(element, property);

            var spring = new UISpringFloat(current, preset.Stiffness, preset.Damping);
            spring.Bind(element, applier);
            spring.SetTarget(target);

            _activeSprings[key] = spring;
            return spring;
        }

        /// <summary>Animate scale (X, Y) using spring physics.</summary>
        public static UISpringVector2 SpringScale(this VisualElement element, Vector2 target, SpringPreset preset)
        {
            var key = (element, "scale");

            if (_activeSprings.TryGetValue(key, out var existing))
            {
                if (existing is UISpringVector2 oldSpring)
                    oldSpring.Cancel();
            }

            var currentScale = element.resolvedStyle.scale;
            var current = new Vector2(currentScale.value.x, currentScale.value.y);

            var spring = new UISpringVector2(current, preset.Stiffness, preset.Damping);
            spring.Bind(element, (el, val) =>
            {
                el.style.scale = new StyleScale(new Scale(new Vector3(val.x, val.y, 1f)));
            });
            spring.SetTarget(target);

            _activeSprings[key] = spring;
            return spring;
        }

        /// <summary>Animate opacity using spring physics.</summary>
        public static UISpringFloat SpringOpacity(this VisualElement element, float target, SpringPreset preset)
        {
            return element.SpringTo("opacity", target, preset);
        }

        /// <summary>Animate translate (X, Y) using spring physics.</summary>
        public static UISpringVector2 SpringTranslate(this VisualElement element, Vector2 target, SpringPreset preset)
        {
            var key = (element, "translate");

            if (_activeSprings.TryGetValue(key, out var existing))
            {
                if (existing is UISpringVector2 oldSpring)
                    oldSpring.Cancel();
            }

            var currentTranslate = element.resolvedStyle.translate;
            var current = new Vector2(currentTranslate.x, currentTranslate.y);

            var spring = new UISpringVector2(current, preset.Stiffness, preset.Damping);
            spring.Bind(element, (el, val) =>
            {
                el.style.translate = new StyleTranslate(new Translate(val.x, val.y));
            });
            spring.SetTarget(target);

            _activeSprings[key] = spring;
            return spring;
        }

        /// <summary>Animate background color using spring physics.</summary>
        public static UISpringColor SpringBackgroundColor(this VisualElement element, Color target, SpringPreset preset)
        {
            var key = (element, "background-color");

            if (_activeSprings.TryGetValue(key, out var existing))
            {
                if (existing is UISpringColor oldSpring)
                    oldSpring.Cancel();
            }

            var current = element.resolvedStyle.backgroundColor;

            var spring = new UISpringColor(current, preset.Stiffness, preset.Damping);
            spring.Bind(element, (el, val) =>
            {
                el.style.backgroundColor = new StyleColor(val);
            });
            spring.SetTarget(target);

            _activeSprings[key] = spring;
            return spring;
        }

        /// <summary>Animate border color using spring physics.</summary>
        public static UISpringColor SpringBorderColor(this VisualElement element, Color target, SpringPreset preset)
        {
            var key = (element, "border-color");

            if (_activeSprings.TryGetValue(key, out var existing))
            {
                if (existing is UISpringColor oldSpring)
                    oldSpring.Cancel();
            }

            var current = element.resolvedStyle.borderTopColor; // use top as representative

            var spring = new UISpringColor(current, preset.Stiffness, preset.Damping);
            spring.Bind(element, (el, val) =>
            {
                var c = new StyleColor(val);
                el.style.borderTopColor = c;
                el.style.borderRightColor = c;
                el.style.borderBottomColor = c;
                el.style.borderLeftColor = c;
            });
            spring.SetTarget(target);

            _activeSprings[key] = spring;
            return spring;
        }

        /// <summary>Cancel all active springs on this element.</summary>
        public static void CancelSprings(this VisualElement element)
        {
            var toRemove = new List<(VisualElement, string)>();

            foreach (var kvp in _activeSprings)
            {
                if (kvp.Key.Item1 != element) continue;

                switch (kvp.Value)
                {
                    case UISpringFloat f: f.Cancel(); break;
                    case UISpringVector2 v: v.Cancel(); break;
                    case UISpringColor c: c.Cancel(); break;
                }
                toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
                _activeSprings.Remove(key);
        }

        /// <summary>Cancel a specific spring property on this element.</summary>
        public static void CancelSpring(this VisualElement element, string property)
        {
            var key = (element, property);
            if (_activeSprings.TryGetValue(key, out var existing))
            {
                switch (existing)
                {
                    case UISpringFloat f: f.Cancel(); break;
                    case UISpringVector2 v: v.Cancel(); break;
                    case UISpringColor c: c.Cancel(); break;
                }
                _activeSprings.Remove(key);
            }
        }

        // --- Float property appliers ---

        private static System.Action<VisualElement, float> GetFloatApplier(string property)
        {
            switch (property)
            {
                case "opacity":
                    return (el, v) => el.style.opacity = new StyleFloat(Mathf.Clamp01(v));
                case "width":
                    return (el, v) => el.style.width = new StyleLength(v);
                case "height":
                    return (el, v) => el.style.height = new StyleLength(v);
                case "left":
                    return (el, v) => el.style.left = new StyleLength(v);
                case "top":
                    return (el, v) => el.style.top = new StyleLength(v);
                case "right":
                    return (el, v) => el.style.right = new StyleLength(v);
                case "bottom":
                    return (el, v) => el.style.bottom = new StyleLength(v);
                case "rotate":
                    return (el, v) => el.style.rotate = new StyleRotate(new Rotate(v));
                case "font-size":
                    return (el, v) => el.style.fontSize = new StyleLength(v);
                case "border-width":
                    return (el, v) =>
                    {
                        var sv = new StyleFloat(v);
                        el.style.borderTopWidth = sv;
                        el.style.borderRightWidth = sv;
                        el.style.borderBottomWidth = sv;
                        el.style.borderLeftWidth = sv;
                    };
                case "border-radius":
                    return (el, v) =>
                    {
                        var sv = new StyleLength(v);
                        el.style.borderTopLeftRadius = sv;
                        el.style.borderTopRightRadius = sv;
                        el.style.borderBottomRightRadius = sv;
                        el.style.borderBottomLeftRadius = sv;
                    };
                default:
                    return null;
            }
        }

        private static float ReadCurrentFloat(VisualElement element, string property)
        {
            switch (property)
            {
                case "opacity": return element.resolvedStyle.opacity;
                case "width": return element.resolvedStyle.width;
                case "height": return element.resolvedStyle.height;
                case "left": return element.resolvedStyle.left;
                case "top": return element.resolvedStyle.top;
                case "right": return element.resolvedStyle.right;
                case "bottom": return element.resolvedStyle.bottom;
                case "rotate": return element.resolvedStyle.rotate.angle.value;
                case "font-size": return element.resolvedStyle.fontSize;
                case "border-width": return element.resolvedStyle.borderTopWidth;
                case "border-radius": return element.resolvedStyle.borderTopLeftRadius;
                default: return 0f;
            }
        }
    }
}

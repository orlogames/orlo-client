using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Spawns floating damage / feedback text above entities in world space.
    /// Uses OnGUI with WorldToScreenPoint so numbers appear over the hit target.
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        public static CombatFeedback Instance { get; private set; }

        private struct FloatText
        {
            public Vector3 WorldPos;
            public string  Text;
            public Color   Color;
            public float   TimeRemaining;
            public float   OffsetY;   // drifts upward over lifetime
        }

        private readonly List<FloatText> _pops = new();
        private const float Lifetime = 1.5f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Show a floating text pop at the given world position.</summary>
        public void ShowFloatingText(Vector3 worldPos, string text, Color color)
        {
            _pops.Add(new FloatText
            {
                WorldPos      = worldPos + Vector3.up * 1.8f,
                Text          = text,
                Color         = color,
                TimeRemaining = Lifetime,
                OffsetY       = 0f
            });
        }

        private void Update()
        {
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.TimeRemaining -= Time.deltaTime;
                p.OffsetY       += Time.deltaTime * 40f;   // drift up 40 px/s in screen space
                if (p.TimeRemaining <= 0)
                    _pops.RemoveAt(i);
                else
                    _pops[i] = p;
            }
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;

            var critStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize    = UIScaler.ScaledFontSize(20),
                fontStyle   = FontStyle.Bold,
                alignment   = TextAnchor.MiddleCenter
            };
            var normalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize    = UIScaler.ScaledFontSize(15),
                fontStyle   = FontStyle.Bold,
                alignment   = TextAnchor.MiddleCenter
            };

            foreach (var p in _pops)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(p.WorldPos);
                if (screen.z <= 0) continue;   // behind camera

                // Unity GUI Y is inverted vs screen space
                float guiX = screen.x - 50f;
                float guiY = Screen.height - screen.y - p.OffsetY;

                float alpha = p.TimeRemaining / Lifetime;
                var c = AccessibilityManager.Instance != null
                    ? AccessibilityManager.Instance.RemapColor(p.Color)
                    : p.Color;
                GUI.color = new Color(c.r, c.g, c.b, alpha);

                bool isCrit = p.Text.StartsWith("CRIT");
                var style = isCrit ? critStyle : normalStyle;
                GUI.Label(new Rect(guiX, guiY, 100f, 30f), p.Text, style);
            }

            GUI.color = Color.white;
        }
    }
}

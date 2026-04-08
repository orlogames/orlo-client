using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Displays the local player's three health pools (Vitality / Stamina / Focus)
    /// as colour-coded bars in the bottom-left corner, WoW/SWG style.
    /// Also handles floating damage number spawning via CombatFeedback.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        public static CombatHUD Instance { get; private set; }

        // Three health pools
        private float _vitality, _maxVitality = 100;
        private float _stamina,  _maxStamina  = 100;
        private float _focus,    _maxFocus    = 100;

        // Strain
        private float _strain; // 0-100%
        private float _strainPulse;

        // Flash effect when taking damage
        private float _damageFlashTimer;
        private const float FlashDuration = 0.3f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Refresh all three pools from a HealthUpdate packet.</summary>
        public void UpdatePools(float vit, float maxVit, float stam, float maxStam, float foc, float maxFoc)
        {
            _vitality    = vit;    _maxVitality = maxVit > 0 ? maxVit : 1;
            _stamina     = stam;   _maxStamina  = maxStam > 0 ? maxStam : 1;
            _focus       = foc;    _maxFocus    = maxFoc  > 0 ? maxFoc  : 1;
        }

        /// <summary>Trigger the damage flash indicator.</summary>
        public void TakeDamage(float amount, string poolName)
        {
            _damageFlashTimer = FlashDuration;
        }

        /// <summary>Update strain value from server (0-100).</summary>
        public void UpdateStrain(float strain)
        {
            _strain = Mathf.Clamp(strain, 0f, 100f);
        }

        private const string HUD_KEY = "HAM";
        private bool _hudRegistered;

        private void OnGUI()
        {
            float s = UIScaler.Scale;
            float barW = 200f * s;
            float barH = 16f * s;
            float gap  = 4f * s;
            float padX = 14f * s;
            float padY = 14f * s;

            float totalH = (barH + gap) * 3;

            // Register with HUDLayout for draggable positioning
            if (!_hudRegistered && HUDLayout.Instance != null)
            {
                HUDLayout.Instance.Register(HUD_KEY, "Health", padX, padY, barW, totalH);
                _hudRegistered = true;
            }

            // Get position from HUDLayout (defaults to top-left)
            float x, y;
            if (HUDLayout.Instance != null)
            {
                var pos = HUDLayout.Instance.GetPosition(HUD_KEY);
                x = pos.x;
                y = pos.y;
            }
            else
            {
                x = padX;
                y = padY; // Top-left default
            }

            // Red damage flash overlay (respects flash effects setting)
            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= Time.deltaTime;
                bool flashEnabled = AccessibilityManager.Instance == null || AccessibilityManager.Instance.FlashEffectsEnabled;
                if (flashEnabled)
                {
                    float alpha = _damageFlashTimer / FlashDuration * 0.35f;
                    GUI.color = new Color(1f, 0f, 0f, alpha);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            var am = AccessibilityManager.Instance;
            Color vitColor = am != null ? am.RemapColor(new Color(0.85f, 0.15f, 0.15f)) : new Color(0.85f, 0.15f, 0.15f);
            Color stamColor = am != null ? am.RemapColor(new Color(0.15f, 0.75f, 0.25f)) : new Color(0.15f, 0.75f, 0.25f);
            Color focColor = am != null ? am.RemapColor(new Color(0.25f, 0.45f, 0.95f)) : new Color(0.25f, 0.45f, 0.95f);

            DrawBar(x, y,           barW, barH, _vitality / _maxVitality, vitColor, $"VIT  {_vitality:F0}/{_maxVitality:F0}");
            DrawBar(x, y + barH + gap, barW, barH, _stamina  / _maxStamina,  stamColor, $"STAM {_stamina:F0}/{_maxStamina:F0}");
            DrawBar(x, y + (barH + gap) * 2, barW, barH, _focus / _maxFocus, focColor, $"FOC  {_focus:F0}/{_maxFocus:F0}");

            // Strain bar (below health pools)
            float strainY = y + (barH + gap) * 3 + 2f * s;
            DrawStrainBar(x, strainY, barW, barH, s);
        }

        private void DrawStrainBar(float x, float y, float w, float h, float s)
        {
            if (_strain <= 0) return;

            float fill = _strain / 100f;

            // Pulsing effect when strain > 70%
            if (_strain > 70f)
            {
                _strainPulse += Time.deltaTime * 3f;
                float pulse = Mathf.Sin(_strainPulse) * 0.15f + 0.85f;
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f * pulse);
            }
            else
            {
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            }
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            // Orange to red gradient based on strain level
            Color strainColor = Color.Lerp(new Color(1f, 0.6f, 0f), new Color(0.9f, 0.15f, 0.1f), fill);
            if (_strain > 70f)
            {
                float pulse = Mathf.Sin(_strainPulse) * 0.15f + 0.85f;
                strainColor *= pulse;
            }

            GUI.color = strainColor;
            GUI.DrawTexture(new Rect(x, y, w * fill, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(10),
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(x + 4, y, w, h), $"Strain: {_strain:F0}%", style);

            // Tooltip on hover
            Rect barRect = new Rect(x, y, w, h);
            if (barRect.Contains(Event.current.mousePosition))
            {
                var tipStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 10, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }, wordWrap = true
                };
                GUI.Box(new Rect(x, y - 24, 200, 22), "Visit a cantina or rest to cure strain", tipStyle);
            }
        }

        private static void DrawBar(float x, float y, float w, float h, float fill, Color color, string label)
        {
            // Background
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            // Fill
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill), h), Texture2D.whiteTexture);

            // Label
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { fontSize = UIScaler.ScaledFontSize(11), alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(x + 4, y, w, h), label, style);
        }
    }
}

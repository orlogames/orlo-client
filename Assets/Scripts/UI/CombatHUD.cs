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

        private void OnGUI()
        {
            const float barW = 200f;
            const float barH = 16f;
            const float gap  = 4f;
            const float padX = 14f;
            const float padY = 14f;

            float x = padX;
            float y = Screen.height - padY - (barH + gap) * 3;

            // Red damage flash overlay
            if (_damageFlashTimer > 0)
            {
                _damageFlashTimer -= Time.deltaTime;
                float alpha = _damageFlashTimer / FlashDuration * 0.35f;
                GUI.color = new Color(1f, 0f, 0f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            DrawBar(x, y,           barW, barH, _vitality / _maxVitality, new Color(0.85f, 0.15f, 0.15f), $"VIT  {_vitality:F0}/{_maxVitality:F0}");
            DrawBar(x, y + barH + gap, barW, barH, _stamina  / _maxStamina,  new Color(0.15f, 0.75f, 0.25f), $"STAM {_stamina:F0}/{_maxStamina:F0}");
            DrawBar(x, y + (barH + gap) * 2, barW, barH, _focus / _maxFocus, new Color(0.25f, 0.45f, 0.95f), $"FOC  {_focus:F0}/{_maxFocus:F0}");
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
            var style = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(x + 4, y, w, h), label, style);
        }
    }
}

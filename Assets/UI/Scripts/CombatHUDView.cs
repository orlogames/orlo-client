using UnityEngine;
using UnityEngine.UIElements;
using Orlo.Player;

namespace Orlo.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatHUDView : MonoBehaviour
    {
        UIDocument    _doc;
        VisualElement _root;

        VisualElement _vitalityFill, _staminaFill, _focusFill, _strainFill;
        Label         _vitalityLabel, _staminaLabel, _focusLabel, _strainLabel;
        VisualElement _strainRow;
        VisualElement _damageFlash;

        float _flashAlpha;

        void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement;

            _vitalityFill  = _root.Q("vitality-fill");
            _staminaFill   = _root.Q("stamina-fill");
            _focusFill     = _root.Q("focus-fill");
            _strainFill    = _root.Q("strain-fill");
            _vitalityLabel = _root.Q<Label>("vitality-label");
            _staminaLabel  = _root.Q<Label>("stamina-label");
            _focusLabel    = _root.Q<Label>("focus-label");
            _strainLabel   = _root.Q<Label>("strain-label");
            _strainRow     = _root.Q("strain-row");
            _damageFlash   = _root.Q("damage-flash");

            UIToolkitRoot.Register("CombatHUD");
        }

        void OnDestroy() => UIToolkitRoot.Unregister("CombatHUD");

        void Update()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            SetBar(_vitalityFill, _vitalityLabel, player.Vitality,  player.MaxVitality);
            SetBar(_staminaFill,  _staminaLabel,  player.Stamina,   player.MaxStamina);
            SetBar(_focusFill,    _focusLabel,     player.Focus,     player.MaxFocus);

            bool hasStrain = player.MaxStrain > 0 && player.Strain > 0;
            _strainRow.style.display = hasStrain ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasStrain)
            {
                SetBar(_strainFill, _strainLabel, player.Strain, player.MaxStrain);
                float pct = player.Strain / (float)player.MaxStrain;
                _strainFill.EnableInClassList("strain--critical", pct >= 0.7f);
            }

            if (_flashAlpha > 0f)
            {
                _flashAlpha = Mathf.Max(0f, _flashAlpha - Time.deltaTime / 0.4f);
                _damageFlash.style.display = DisplayStyle.Flex;
                _damageFlash.style.opacity = _flashAlpha;
                if (_flashAlpha <= 0f)
                    _damageFlash.style.display = DisplayStyle.None;
            }
        }

        static void SetBar(VisualElement fill, Label label, float current, float max)
        {
            float pct = max > 0f ? current / max : 0f;
            fill.style.width = Length.Percent(pct * 100f);
            label.text       = $"{(int)current}";
        }

        public void TriggerDamageFlash()
        {
            var acc = AccessibilityManager.Instance;
            if (acc != null && !acc.FlashEffectsEnabled) return;
            _flashAlpha = 1f;
            _damageFlash.style.opacity = 1f;
            _damageFlash.style.display = DisplayStyle.Flex;
        }
    }
}

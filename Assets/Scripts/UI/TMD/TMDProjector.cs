using UnityEngine;
using UnityEngine.UI;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Creates a world-space holographic display on the player's left forearm.
    /// Shows a mini health bar and active quest objective text.
    /// Other players see YOUR race's colors on your TMD projection.
    /// Attaches to the "LeftHand" or "LeftForeArm" bone if found,
    /// otherwise offsets from the player transform.
    /// </summary>
    public class TMDProjector : MonoBehaviour
    {
        private const float CanvasWidth = 0.3f;
        private const float CanvasHeight = 0.12f;
        private const float CanvasPixelsPerUnit = 400f;

        // Health state (set externally by CombatHUD or health update handler)
        private float _healthCurrent = 100f;
        private float _healthMax = 100f;

        // Quest objective (set externally by quest tracking system)
        private string _questObjective = "";

        // Internal refs
        private Canvas _canvas;
        private GameObject _canvasGo;
        private RectTransform _canvasRect;
        private Image _bgImage;
        private Image _healthBarBg;
        private Image _healthBarFill;
        private Image _healthBarGlow;
        private Text _questText;
        private Text _healthText;
        private Material _holoMaterial;
        private Transform _attachBone;
        private bool _initialized;

        /// <summary>Set the player's current health for the forearm display.</summary>
        public void SetHealth(float current, float max)
        {
            _healthCurrent = current;
            _healthMax = Mathf.Max(max, 1f);
        }

        /// <summary>Set the active quest objective text shown on the forearm display.</summary>
        public void SetQuestObjective(string text)
        {
            _questObjective = text ?? "";
        }

        private void Start()
        {
            FindAttachBone();
            CreateCanvas();
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            UpdatePosition();
            UpdateHealthBar();
            UpdateQuestText();
            UpdateMaterial();
        }

        private void OnDestroy()
        {
            if (_holoMaterial != null)
                Destroy(_holoMaterial);
            if (_canvasGo != null)
                Destroy(_canvasGo);
        }

        private void FindAttachBone()
        {
            // Search for standard humanoid bone names
            string[] boneNames = { "LeftForeArm", "LeftHand", "Left_ForeArm", "Left_Hand",
                                   "mixamorig:LeftForeArm", "mixamorig:LeftHand",
                                   "Bip01 L Forearm", "l_forearm", "L_Forearm" };

            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var boneName in boneNames)
            {
                foreach (var t in allTransforms)
                {
                    if (t.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        _attachBone = t;
                        return;
                    }
                }
            }

            // Fallback: no bone found, will offset from player transform
            _attachBone = null;
        }

        private void CreateCanvas()
        {
            // Create holographic material instance
            if (TMDTheme.Instance != null)
            {
                _holoMaterial = new Material(TMDTheme.Instance.HolographicMaterial);
                _holoMaterial.name = "TMDProjector_Holographic";
            }
            else
            {
                var shader = Orlo.Rendering.OrloShaders.HolographicUI;
                _holoMaterial = new Material(shader);
                _holoMaterial.name = "TMDProjector_Holographic_Fallback";
            }

            // Root object
            _canvasGo = new GameObject("TMDProjector_Canvas");
            _canvasGo.transform.SetParent(transform, false);

            // Canvas setup
            _canvas = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 5;

            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

            _canvasRect = _canvasGo.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(CanvasWidth * CanvasPixelsPerUnit, CanvasHeight * CanvasPixelsPerUnit);
            _canvasRect.localScale = Vector3.one / CanvasPixelsPerUnit;

            // Background quad with holographic material
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_canvasGo.transform, false);
            _bgImage = bgGo.AddComponent<Image>();
            _bgImage.material = _holoMaterial;
            _bgImage.color = new Color(0.05f, 0.05f, 0.1f, 0.7f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Health bar background
            var hpBgGo = new GameObject("HealthBarBg");
            hpBgGo.transform.SetParent(_canvasGo.transform, false);
            _healthBarBg = hpBgGo.AddComponent<Image>();
            _healthBarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.05f, 0.55f);
            hpBgRect.anchorMax = new Vector2(0.95f, 0.85f);
            hpBgRect.offsetMin = Vector2.zero;
            hpBgRect.offsetMax = Vector2.zero;

            // Health bar fill
            var hpFillGo = new GameObject("HealthBarFill");
            hpFillGo.transform.SetParent(hpBgGo.transform, false);
            _healthBarFill = hpFillGo.AddComponent<Image>();
            _healthBarFill.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            var hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = new Vector2(1f, 1f);
            hpFillRect.offsetMin = Vector2.zero;
            hpFillRect.offsetMax = Vector2.zero;
            hpFillRect.pivot = new Vector2(0f, 0.5f);

            // Health bar leading edge glow
            var hpGlowGo = new GameObject("HealthBarGlow");
            hpGlowGo.transform.SetParent(hpBgGo.transform, false);
            _healthBarGlow = hpGlowGo.AddComponent<Image>();
            _healthBarGlow.color = Color.white;
            var hpGlowRect = hpGlowGo.GetComponent<RectTransform>();
            hpGlowRect.sizeDelta = new Vector2(3f, 0f);
            hpGlowRect.anchorMin = new Vector2(0f, 0f);
            hpGlowRect.anchorMax = new Vector2(0f, 1f);
            hpGlowRect.pivot = new Vector2(0.5f, 0.5f);

            // Health text
            var hpTextGo = new GameObject("HealthText");
            hpTextGo.transform.SetParent(hpBgGo.transform, false);
            _healthText = hpTextGo.AddComponent<Text>();
            _healthText.text = "100%";
            _healthText.fontSize = 10;
            _healthText.alignment = TextAnchor.MiddleCenter;
            _healthText.color = Color.white;
            _healthText.font = Font.CreateDynamicFontFromOSFont("Arial", 10);
            var hpTextRect = hpTextGo.GetComponent<RectTransform>();
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;

            // Quest objective text
            var questGo = new GameObject("QuestText");
            questGo.transform.SetParent(_canvasGo.transform, false);
            _questText = questGo.AddComponent<Text>();
            _questText.text = "";
            _questText.fontSize = 8;
            _questText.alignment = TextAnchor.MiddleLeft;
            _questText.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            _questText.font = Font.CreateDynamicFontFromOSFont("Arial", 8);
            _questText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var questRect = questGo.GetComponent<RectTransform>();
            questRect.anchorMin = new Vector2(0.05f, 0.1f);
            questRect.anchorMax = new Vector2(0.95f, 0.5f);
            questRect.offsetMin = Vector2.zero;
            questRect.offsetMax = Vector2.zero;
        }

        private void UpdatePosition()
        {
            if (_canvasGo == null) return;

            if (_attachBone != null)
            {
                // Attach to forearm bone — project hologram slightly above the wrist
                _canvasGo.transform.position = _attachBone.position + _attachBone.up * 0.08f + _attachBone.forward * 0.05f;
                _canvasGo.transform.rotation = _attachBone.rotation * Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // Fallback: offset from player, left side
                Vector3 offset = transform.position
                    + transform.up * 1.0f
                    - transform.right * 0.35f
                    + transform.forward * 0.15f;
                _canvasGo.transform.position = offset;

                // Face slightly outward from the arm
                if (Camera.main != null)
                {
                    Vector3 toCam = Camera.main.transform.position - _canvasGo.transform.position;
                    _canvasGo.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }
            }
        }

        private void UpdateHealthBar()
        {
            if (_healthBarFill == null) return;

            float fill = _healthCurrent / _healthMax;
            var fillRect = _healthBarFill.GetComponent<RectTransform>();
            fillRect.anchorMax = new Vector2(fill, 1f);

            // Color based on health percentage
            Color barColor;
            if (fill > 0.6f)
                barColor = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(0.2f, 0.9f, 0.3f), (fill - 0.6f) / 0.4f);
            else if (fill > 0.25f)
                barColor = Color.Lerp(new Color(0.9f, 0.2f, 0.1f), new Color(1f, 0.9f, 0.2f), (fill - 0.25f) / 0.35f);
            else
                barColor = new Color(0.9f, 0.2f, 0.1f);

            // Tint with race color
            if (TMDTheme.Instance != null)
                barColor = Color.Lerp(barColor, TMDTheme.Instance.Palette.Primary, 0.2f);

            _healthBarFill.color = new Color(barColor.r, barColor.g, barColor.b, 0.9f);

            // Leading edge glow position
            if (_healthBarGlow != null)
            {
                var glowRect = _healthBarGlow.GetComponent<RectTransform>();
                glowRect.anchorMin = new Vector2(fill, 0f);
                glowRect.anchorMax = new Vector2(fill, 1f);
                _healthBarGlow.color = fill > 0.01f && fill < 0.99f
                    ? new Color(barColor.r, barColor.g, barColor.b, 0.6f)
                    : Color.clear;
            }

            // Health percentage text
            if (_healthText != null)
            {
                int pct = Mathf.RoundToInt(fill * 100f);
                _healthText.text = $"{pct}%";
            }
        }

        private void UpdateQuestText()
        {
            if (_questText == null) return;
            _questText.text = _questObjective;
            _questText.gameObject.SetActive(!string.IsNullOrEmpty(_questObjective));
        }

        private void UpdateMaterial()
        {
            if (_holoMaterial == null || TMDTheme.Instance == null) return;

            var theme = TMDTheme.Instance;
            var palette = theme.Palette;

            _holoMaterial.SetColor("_RaceColor", palette.Primary);
            _holoMaterial.SetColor("_GlowColor", palette.Glow);
            _holoMaterial.SetColor("_BackgroundColor", palette.Background);
            _holoMaterial.SetFloat("_ScanlineIntensity", theme.EffectiveScanlines);
            _holoMaterial.SetFloat("_ChromaticAberration", theme.EffectiveAberration);
            _holoMaterial.SetFloat("_NoiseIntensity", theme.EffectiveNoise);
            _holoMaterial.SetFloat("_GlowMultiplier", theme.TierSettings.GlowMultiplier);
            _holoMaterial.SetFloat("_DotGridSpacing", theme.TierSettings.DotGridSpacing);
            _holoMaterial.SetFloat("_IsGlitching", theme.IsGlitching ? 1f : 0f);

            // Tint quest text with race accent
            if (_questText != null)
                _questText.color = new Color(palette.Accent.r, palette.Accent.g, palette.Accent.b, 0.9f);
        }
    }
}

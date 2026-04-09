using UnityEngine;
using UnityEngine.UI;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// World-space holographic nameplate rendered above an entity.
    /// Name text colored by entity type (player=white, NPC=race accent, creature=danger).
    /// Health bar underneath. Uses HolographicUI material for background.
    /// Fades with distance: fully visible <20m, fades 20-40m, invisible >40m.
    /// Billboards to always face the camera.
    /// </summary>
    public class HolographicNameplate : MonoBehaviour
    {
        public enum EntityType { Player, NPC, Creature }

        private const float CanvasWidth = 0.8f;
        private const float CanvasHeight = 0.2f;
        private const float CanvasPixelsPerUnit = 200f;
        private const float OffsetY = 2.3f;
        private const float FadeNearDist = 20f;
        private const float FadeFarDist = 40f;

        // Configuration
        private string _entityName = "Unknown";
        private EntityType _entityType = EntityType.Creature;
        private float _healthCurrent = 100f;
        private float _healthMax = 100f;
        private string _raceName = "Solari"; // for NPC/player race tinting

        // Internal refs
        private Canvas _canvas;
        private GameObject _canvasGo;
        private CanvasGroup _canvasGroup;
        private Image _bgImage;
        private Image _healthBarBg;
        private Image _healthBarFill;
        private Text _nameText;
        private Material _holoMaterial;
        private bool _initialized;

        /// <summary>Configure the nameplate. Call after adding the component.</summary>
        public void Setup(string name, EntityType type, string raceName = null)
        {
            _entityName = name ?? "Unknown";
            _entityType = type;
            _raceName = raceName ?? "Solari";

            if (_initialized)
                ApplyVisuals();
        }

        /// <summary>Update health bar display.</summary>
        public void SetHealth(float current, float max)
        {
            _healthCurrent = current;
            _healthMax = Mathf.Max(max, 1f);
        }

        private void Start()
        {
            CreateCanvas();
            ApplyVisuals();
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized || _canvasGo == null) return;

            UpdateBillboard();
            UpdateDistanceFade();
            UpdateHealthBar();
            UpdateMaterial();
        }

        private void OnDestroy()
        {
            if (_holoMaterial != null)
                Destroy(_holoMaterial);
            if (_canvasGo != null)
                Destroy(_canvasGo);
        }

        private void CreateCanvas()
        {
            // Create holographic material instance
            if (TMDTheme.Instance != null)
            {
                _holoMaterial = new Material(TMDTheme.Instance.HolographicMaterial);
                _holoMaterial.name = "Nameplate_Holographic";
            }
            else
            {
                var shader = Orlo.Rendering.OrloShaders.HolographicUI;
                _holoMaterial = new Material(shader);
                _holoMaterial.name = "Nameplate_Holographic_Fallback";
            }

            // Root canvas
            _canvasGo = new GameObject("Nameplate_Canvas");
            _canvasGo.transform.SetParent(transform, false);
            _canvasGo.transform.localPosition = Vector3.up * OffsetY;

            _canvas = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 10;

            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

            _canvasGroup = _canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;

            var canvasRect = _canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(CanvasWidth * CanvasPixelsPerUnit, CanvasHeight * CanvasPixelsPerUnit);
            canvasRect.localScale = Vector3.one / CanvasPixelsPerUnit;

            // Background quad with holographic material
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_canvasGo.transform, false);
            _bgImage = bgGo.AddComponent<Image>();
            _bgImage.material = _holoMaterial;
            _bgImage.color = new Color(0.02f, 0.02f, 0.05f, 0.6f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Name text
            var nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(_canvasGo.transform, false);
            _nameText = nameGo.AddComponent<Text>();
            _nameText.text = _entityName;
            _nameText.fontSize = 14;
            _nameText.fontStyle = FontStyle.Bold;
            _nameText.alignment = TextAnchor.MiddleCenter;
            _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.45f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(4f, 0f);
            nameRect.offsetMax = new Vector2(-4f, 0f);

            // Health bar background
            var hpBgGo = new GameObject("HealthBarBg");
            hpBgGo.transform.SetParent(_canvasGo.transform, false);
            _healthBarBg = hpBgGo.AddComponent<Image>();
            _healthBarBg.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.1f, 0.1f);
            hpBgRect.anchorMax = new Vector2(0.9f, 0.4f);
            hpBgRect.offsetMin = Vector2.zero;
            hpBgRect.offsetMax = Vector2.zero;

            // Health bar fill
            var hpFillGo = new GameObject("HealthBarFill");
            hpFillGo.transform.SetParent(hpBgGo.transform, false);
            _healthBarFill = hpFillGo.AddComponent<Image>();
            _healthBarFill.color = new Color(0.2f, 0.8f, 0.3f, 0.9f);
            var hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = Vector2.zero;
            hpFillRect.offsetMax = Vector2.zero;
            hpFillRect.pivot = new Vector2(0f, 0.5f);
        }

        private void ApplyVisuals()
        {
            if (_nameText == null) return;

            _nameText.text = _entityName;

            // Color by entity type
            Color nameColor;
            switch (_entityType)
            {
                case EntityType.Player:
                    nameColor = Color.white;
                    break;
                case EntityType.NPC:
                    var npcPalette = RacePalette.ForRace(_raceName);
                    nameColor = npcPalette.Accent;
                    break;
                case EntityType.Creature:
                default:
                    nameColor = TMDTheme.Instance != null
                        ? TMDTheme.Instance.Palette.Danger
                        : new Color(1f, 0.3f, 0.2f);
                    break;
            }
            _nameText.color = nameColor;
        }

        private void UpdateBillboard()
        {
            if (Camera.main == null) return;

            // Face the camera, keeping upright
            Vector3 toCam = Camera.main.transform.position - _canvasGo.transform.position;
            toCam.y = 0f; // keep upright
            if (toCam.sqrMagnitude > 0.001f)
                _canvasGo.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        private void UpdateDistanceFade()
        {
            if (Camera.main == null || _canvasGroup == null) return;

            float dist = Vector3.Distance(Camera.main.transform.position, transform.position);

            if (dist > FadeFarDist)
            {
                _canvasGroup.alpha = 0f;
                return;
            }
            if (dist < FadeNearDist)
            {
                _canvasGroup.alpha = 1f;
                return;
            }

            // Linear fade between near and far
            _canvasGroup.alpha = 1f - (dist - FadeNearDist) / (FadeFarDist - FadeNearDist);
        }

        private void UpdateHealthBar()
        {
            if (_healthBarFill == null) return;

            float fill = _healthCurrent / _healthMax;
            var fillRect = _healthBarFill.GetComponent<RectTransform>();
            fillRect.anchorMax = new Vector2(fill, 1f);

            // Color: green > yellow > red
            Color barColor;
            if (fill > 0.6f)
                barColor = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(0.2f, 0.8f, 0.3f), (fill - 0.6f) / 0.4f);
            else if (fill > 0.25f)
                barColor = Color.Lerp(new Color(0.9f, 0.2f, 0.1f), new Color(1f, 0.9f, 0.2f), (fill - 0.25f) / 0.35f);
            else
                barColor = new Color(0.9f, 0.2f, 0.1f);

            _healthBarFill.color = new Color(barColor.r, barColor.g, barColor.b, 0.9f);

            // Hide health bar at full health for non-creatures to reduce clutter
            bool showHealth = _entityType == EntityType.Creature || fill < 0.99f;
            _healthBarBg.gameObject.SetActive(showHealth);
        }

        private void UpdateMaterial()
        {
            if (_holoMaterial == null || TMDTheme.Instance == null) return;

            var theme = TMDTheme.Instance;
            var palette = theme.Palette;

            _holoMaterial.SetColor("_RaceColor", palette.Primary);
            _holoMaterial.SetColor("_GlowColor", palette.Glow);
            _holoMaterial.SetFloat("_ScanlineIntensity", theme.EffectiveScanlines * 0.5f); // lighter scanlines on nameplates
            _holoMaterial.SetFloat("_ChromaticAberration", theme.EffectiveAberration * 0.3f);
            _holoMaterial.SetFloat("_NoiseIntensity", theme.EffectiveNoise * 0.5f);
            _holoMaterial.SetFloat("_GlowMultiplier", theme.TierSettings.GlowMultiplier * 0.6f);
            _holoMaterial.SetFloat("_DotGridSpacing", theme.TierSettings.DotGridSpacing);
            _holoMaterial.SetFloat("_IsGlitching", theme.IsGlitching ? 1f : 0f);
        }
    }
}

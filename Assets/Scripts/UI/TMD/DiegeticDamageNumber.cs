using UnityEngine;
using UnityEngine.UI;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Floating damage number rendered in world space with holographic styling.
    /// Race-colored for player-dealt damage, white for received damage.
    /// Numbers float upward with gravity curve. Crit numbers are 1.5x size
    /// with a glow flash in the race's Glow color. Fades out over 1.5 seconds.
    ///
    /// Usage: DiegeticDamageNumber.Spawn(worldPos, "125", color, isCrit)
    /// Self-destructs after lifetime expires.
    /// </summary>
    public class DiegeticDamageNumber : MonoBehaviour
    {
        private const float Lifetime = 1.5f;
        private const float FloatSpeed = 1.2f;     // meters/sec upward
        private const float Gravity = 0.8f;         // deceleration
        private const float BaseFontSize = 18;
        private const float CritScale = 1.5f;
        private const float CritFlashDuration = 0.15f;
        private const float CanvasPixelsPerUnit = 300f;

        private float _elapsed;
        private float _velocityY;
        private bool _isCrit;
        private Color _baseColor;
        private Color _glowColor;

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Text _text;
        private Image _glowBg;
        private Material _holoMaterial;

        /// <summary>
        /// Spawn a floating damage number at the given world position.
        /// </summary>
        /// <param name="worldPos">Where the number appears (typically above the hit entity).</param>
        /// <param name="text">The damage text (e.g., "125" or "CRIT 340").</param>
        /// <param name="color">Base color. Use race Primary for dealt damage, white for received.</param>
        /// <param name="isCrit">If true, number is 1.5x larger with a glow flash.</param>
        public static DiegeticDamageNumber Spawn(Vector3 worldPos, string text, Color color, bool isCrit = false)
        {
            var go = new GameObject("DmgNumber");
            go.transform.position = worldPos + Vector3.up * 1.8f;

            var dmg = go.AddComponent<DiegeticDamageNumber>();
            dmg._baseColor = color;
            dmg._isCrit = isCrit;
            dmg._velocityY = FloatSpeed;

            // Determine glow color
            if (TMDTheme.Instance != null)
                dmg._glowColor = TMDTheme.Instance.Palette.Glow;
            else
                dmg._glowColor = new Color(1f, 0.9f, 0.4f);

            dmg.CreateVisuals(text);
            return dmg;
        }

        private void CreateVisuals(string damageText)
        {
            // Holographic material instance
            if (TMDTheme.Instance != null)
            {
                _holoMaterial = new Material(TMDTheme.Instance.HolographicMaterial);
                _holoMaterial.name = "DmgNum_Holographic";
            }
            else
            {
                var shader = Orlo.Rendering.OrloShaders.HolographicUI;
                _holoMaterial = new Material(shader);
                _holoMaterial.name = "DmgNum_Holographic_Fallback";
            }

            // Reduce effects for small UI element
            _holoMaterial.SetFloat("_ScanlineIntensity", 0.1f);
            _holoMaterial.SetFloat("_NoiseIntensity", 0.05f);
            _holoMaterial.SetFloat("_ChromaticAberration", 0.5f);
            _holoMaterial.SetFloat("_DotGridSpacing", 0f); // no dot grid on tiny numbers
            _holoMaterial.SetColor("_RaceColor", _baseColor);
            _holoMaterial.SetColor("_GlowColor", _glowColor);

            // Canvas
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = Vector3.zero;

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 20;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = CanvasPixelsPerUnit;

            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            float width = _isCrit ? 1.0f : 0.6f;
            float height = _isCrit ? 0.3f : 0.2f;
            canvasRect.sizeDelta = new Vector2(width * CanvasPixelsPerUnit, height * CanvasPixelsPerUnit);
            canvasRect.localScale = Vector3.one / CanvasPixelsPerUnit;

            // Glow background (for crits)
            if (_isCrit)
            {
                var glowGo = new GameObject("GlowBg");
                glowGo.transform.SetParent(canvasGo.transform, false);
                _glowBg = glowGo.AddComponent<Image>();
                _glowBg.material = _holoMaterial;
                _glowBg.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0.4f);
                var glowRect = glowGo.GetComponent<RectTransform>();
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                glowRect.offsetMin = new Vector2(-8f, -4f);
                glowRect.offsetMax = new Vector2(8f, 4f);
            }

            // Damage text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            _text = textGo.AddComponent<Text>();
            _text.text = damageText;
            _text.fontSize = Mathf.RoundToInt(_isCrit ? BaseFontSize * CritScale : BaseFontSize);
            _text.fontStyle = _isCrit ? FontStyle.Bold : FontStyle.Normal;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.color = _baseColor;
            _text.font = Font.CreateDynamicFontFromOSFont("Arial", _text.fontSize);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Add slight random horizontal offset for visual variety
            float xJitter = Random.Range(-0.15f, 0.15f);
            transform.position += Vector3.right * xJitter;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            // Float upward with gravity deceleration
            _velocityY -= Gravity * Time.deltaTime;
            _velocityY = Mathf.Max(_velocityY, 0.05f); // never fall back down
            transform.position += Vector3.up * _velocityY * Time.deltaTime;

            // Billboard to camera
            if (Camera.main != null)
            {
                Vector3 toCam = Camera.main.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }

            // Fade out
            float t = _elapsed / Lifetime;
            float alpha;
            if (t < 0.1f)
                alpha = t / 0.1f; // quick fade in
            else
                alpha = 1f - Mathf.Pow((t - 0.1f) / 0.9f, 2f); // smooth fade out

            if (_canvasGroup != null)
                _canvasGroup.alpha = alpha;

            // Crit glow flash
            if (_isCrit && _glowBg != null)
            {
                float flashAlpha = _elapsed < CritFlashDuration
                    ? 0.6f * (1f - _elapsed / CritFlashDuration)
                    : 0f;
                _glowBg.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, flashAlpha);
            }

            // Scale animation — pop in then shrink slightly
            float scale = 1f;
            if (t < 0.1f)
                scale = 0.5f + t / 0.1f * 0.5f; // pop from 0.5 to 1.0
            else if (_isCrit && t < 0.2f)
                scale = 1f + (0.2f - t) * 0.5f; // crits overshoot briefly

            transform.localScale = Vector3.one * scale;
        }

        private void OnDestroy()
        {
            if (_holoMaterial != null)
                Destroy(_holoMaterial);
        }
    }
}

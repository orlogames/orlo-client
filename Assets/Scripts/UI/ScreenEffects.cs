using UnityEngine;
using Orlo.UI.Settings;

namespace Orlo.UI
{
    /// <summary>
    /// Screen-space visual effects for combat and movement feel.
    /// - Camera shake on damage taken (scales with damage %)
    /// - Red flash on critical hits
    /// - Speed lines when sprinting
    /// - Vignette effect when low health (&lt;25%)
    /// Uses OnGUI for overlay effects and modifies camera transform for shake.
    /// </summary>
    public class ScreenEffects : MonoBehaviour
    {
        public static ScreenEffects Instance { get; private set; }

        // Camera shake
        private float _shakeIntensity;
        private float _shakeTimer;
        private float _shakeDuration;
        private Vector3 _shakeOffset;
        private Transform _cameraTransform;

        // Critical hit flash
        private float _critFlashTimer;
        private const float CritFlashDuration = 0.25f;

        // Speed lines
        private bool _sprinting;
        private float _sprintAlpha;
        private const float SprintFadeSpeed = 4f;

        // Low health vignette
        private float _healthPercent = 1f;
        private float _vignetteAlpha;
        private float _vignettePulseTimer;

        // Cached textures
        private Texture2D _vignetteTexture;
        private bool _texturesInitialized;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Trigger camera shake. Intensity is 0-1 where 1 = full screen shake.
        /// Duration in seconds.
        /// </summary>
        public void Shake(float intensity, float duration = 0.3f)
        {
            // Respect settings
            if (SettingsManager.Instance != null && SettingsManager.Instance.Current != null
                && !SettingsManager.Instance.Current.screenShake)
                return;

            // Take the stronger of current and new shake
            if (intensity > _shakeIntensity || _shakeTimer <= 0)
            {
                _shakeIntensity = Mathf.Clamp01(intensity);
                _shakeDuration = duration;
                _shakeTimer = duration;
            }
        }

        /// <summary>
        /// Trigger shake scaled by damage percentage (damage / maxHealth).
        /// Light hits = subtle shake, heavy hits = strong shake.
        /// </summary>
        public void ShakeFromDamage(float damagePercent)
        {
            float intensity = Mathf.Clamp(damagePercent * 2f, 0.05f, 1f);
            float duration = Mathf.Lerp(0.15f, 0.5f, intensity);
            Shake(intensity, duration);
        }

        /// <summary>Trigger a red flash for critical hits.</summary>
        public void CritFlash()
        {
            _critFlashTimer = CritFlashDuration;
        }

        /// <summary>Set whether the player is sprinting for speed line effect.</summary>
        public void SetSprinting(bool sprinting)
        {
            _sprinting = sprinting;
        }

        /// <summary>Update health percentage for low-health vignette (0-1).</summary>
        public void SetHealthPercent(float percent)
        {
            _healthPercent = Mathf.Clamp01(percent);
        }

        private void Update()
        {
            // Camera shake
            if (_shakeTimer > 0)
            {
                _shakeTimer -= Time.deltaTime;
                float decay = _shakeTimer / _shakeDuration;
                float magnitude = _shakeIntensity * decay * 0.15f; // max 0.15 units offset

                _shakeOffset = new Vector3(
                    Random.Range(-magnitude, magnitude),
                    Random.Range(-magnitude, magnitude),
                    0
                );

                if (_cameraTransform == null && Camera.main != null)
                    _cameraTransform = Camera.main.transform;

                if (_cameraTransform != null)
                    _cameraTransform.localPosition += _shakeOffset;

                if (_shakeTimer <= 0)
                {
                    _shakeIntensity = 0;
                    _shakeOffset = Vector3.zero;
                }
            }

            // Crit flash decay
            if (_critFlashTimer > 0)
                _critFlashTimer -= Time.deltaTime;

            // Sprint alpha fade
            float targetSprint = _sprinting ? 1f : 0f;
            _sprintAlpha = Mathf.MoveTowards(_sprintAlpha, targetSprint, SprintFadeSpeed * Time.deltaTime);

            // Vignette pulse for low health
            if (_healthPercent < 0.25f)
            {
                _vignettePulseTimer += Time.deltaTime * 2f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(_vignettePulseTimer);
                float healthFactor = 1f - (_healthPercent / 0.25f); // 0 at 25%, 1 at 0%
                _vignetteAlpha = Mathf.Lerp(0.2f, 0.6f, healthFactor) * Mathf.Lerp(0.7f, 1f, pulse);
            }
            else
            {
                _vignetteAlpha = Mathf.MoveTowards(_vignetteAlpha, 0f, Time.deltaTime * 3f);
                _vignettePulseTimer = 0;
            }
        }

        private void InitTextures()
        {
            if (_texturesInitialized) return;
            _texturesInitialized = true;

            // Create a radial vignette texture (dark edges, transparent center)
            int size = 128;
            _vignetteTexture = new Texture2D(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Clamp01((dist - 0.4f) / 0.6f); // fade starts at 40% from center
                    alpha = alpha * alpha; // quadratic falloff for smoother edge
                    _vignetteTexture.SetPixel(x, y, new Color(0, 0, 0, alpha));
                }
            }
            _vignetteTexture.Apply();
        }

        private void OnGUI()
        {
            InitTextures();

            Rect fullScreen = new Rect(0, 0, Screen.width, Screen.height);

            // Critical hit flash (white-red flash)
            if (_critFlashTimer > 0)
            {
                float t = _critFlashTimer / CritFlashDuration;
                GUI.color = new Color(1f, 0.3f, 0.2f, t * 0.3f);
                GUI.DrawTexture(fullScreen, Texture2D.whiteTexture);
            }

            // Speed lines when sprinting (diagonal lines from edges)
            if (_sprintAlpha > 0.01f)
            {
                DrawSpeedLines(_sprintAlpha);
            }

            // Low health vignette (pulsing dark red edges)
            if (_vignetteAlpha > 0.01f)
            {
                GUI.color = new Color(0.6f, 0f, 0f, _vignetteAlpha);
                GUI.DrawTexture(fullScreen, _vignetteTexture);
            }

            GUI.color = Color.white;
        }

        private void DrawSpeedLines(float alpha)
        {
            // Draw converging lines from screen edges to create a speed tunnel effect.
            // We draw thin dark semi-transparent lines radiating from center outward.
            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            int lineCount = 12;
            float lineAlpha = alpha * 0.15f;

            for (int i = 0; i < lineCount; i++)
            {
                float angle = (i / (float)lineCount) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Lines from ~60% to edge of screen
                float innerR = Mathf.Min(Screen.width, Screen.height) * 0.3f;
                float outerR = Mathf.Max(Screen.width, Screen.height) * 0.7f;

                float x1 = centerX + cos * innerR;
                float y1 = centerY + sin * innerR;
                float x2 = centerX + cos * outerR;
                float y2 = centerY + sin * outerR;

                // Approximate line as thin rect
                float length = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
                float angleDeg = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;

                GUI.color = new Color(0.8f, 0.85f, 1f, lineAlpha);

                // Use a small rotated rect. OnGUI does not natively support rotation,
                // so we draw a simple gradient rect along the line direction as a visual hint.
                float midX = (x1 + x2) / 2f;
                float midY = (y1 + y2) / 2f;
                float w = Mathf.Abs(cos) > Mathf.Abs(sin) ? length * 0.4f : 2f;
                float h = Mathf.Abs(sin) > Mathf.Abs(cos) ? length * 0.4f : 2f;

                GUI.DrawTexture(new Rect(midX - w / 2f, midY - h / 2f, w, h), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }
    }
}

using System;
using UnityEngine;

namespace Orlo.UI.Lobby
{
    /// <summary>
    /// Animated "ENTER WORLD" button rendered in OnGUI with premium glow effects.
    /// </summary>
    public class EnterWorldButton : MonoBehaviour
    {
        public static EnterWorldButton Instance { get; private set; }

        public Action OnClick;
        public bool Enabled = true;
        public float YPosition = 0.75f; // Fraction of screen height

        private const float WIDTH = 280f;
        private const float HEIGHT = 56f;
        private const int FONT_SIZE = 18;
        private const string LABEL = "E N T E R   W O R L D";
        private const float GLOW_PERIOD = 3f;
        private const float FLASH_DURATION = 0.15f;
        private const float PULSE_PERIOD = 2f;

        private bool _visible = true;
        private bool _pulse;
        private float _flashTimer;
        private bool _isHovered;

        private GUIStyle _buttonStyle;
        private Texture2D _bgTex;
        private Texture2D _bgHoverTex;
        private Texture2D _bgDisabledTex;
        private Texture2D _whiteTex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show() => _visible = true;
        public void Hide() => _visible = false;
        public void SetPulse(bool pulse) => _pulse = pulse;

        private void EnsureResources()
        {
            if (_bgTex != null) return;

            _bgTex = MakeSolidTex(new Color(0.1f, 0.12f, 0.2f));
            _bgHoverTex = MakeSolidTex(new Color(0.15f, 0.18f, 0.3f));
            _bgDisabledTex = MakeSolidTex(new Color(0.1f, 0.1f, 0.1f));
            _whiteTex = MakeSolidTex(Color.white);

            _buttonStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = FONT_SIZE,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.8f, 1f), background = _bgTex },
                hover = { textColor = Color.white, background = _bgHoverTex }
            };
        }

        private Texture2D MakeSolidTex(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureResources();

            float scale = 1f;
            if (_pulse && Enabled)
                scale = 1f + 0.02f * Mathf.Sin(Time.time * Mathf.PI * 2f / PULSE_PERIOD);

            float w = WIDTH * scale;
            float h = HEIGHT * scale;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * YPosition - h * 0.5f;
            Rect btnRect = new Rect(x, y, w, h);

            _isHovered = Enabled && btnRect.Contains(Event.current.mousePosition);

            // --- Glow border ---
            if (Enabled)
            {
                float sine = (Mathf.Sin(Time.time * Mathf.PI * 2f / GLOW_PERIOD) + 1f) * 0.5f;
                Color glowColor = Color.Lerp(
                    new Color(0.3f, 0.5f, 1f),
                    new Color(0.3f, 0.9f, 1f),
                    sine
                );

                for (int layer = 0; layer < 3; layer++)
                {
                    float alpha = 0.8f * (1f - layer * 0.3f);
                    float expand = (layer + 1) * 2f;

                    Color c = glowColor;
                    c.a = alpha * (_isHovered ? 1.5f : 1f);
                    c.a = Mathf.Clamp01(c.a);

                    Rect glowRect = new Rect(
                        btnRect.x - expand,
                        btnRect.y - expand,
                        btnRect.width + expand * 2f,
                        btnRect.height + expand * 2f
                    );
                    DrawBorder(glowRect, 2f, c);
                }
            }

            // --- Button background ---
            if (!Enabled)
            {
                GUI.DrawTexture(btnRect, _bgDisabledTex);
                var disabledStyle = new GUIStyle(_buttonStyle)
                {
                    normal = { textColor = new Color(0.4f, 0.4f, 0.4f) }
                };
                disabledStyle.normal.background = _bgDisabledTex;
                GUI.Label(btnRect, LABEL, disabledStyle);
                return;
            }

            // Draw button
            var style = new GUIStyle(_buttonStyle);
            style.normal.background = _isHovered ? _bgHoverTex : _bgTex;
            style.normal.textColor = _isHovered ? Color.white : new Color(0.7f, 0.8f, 1f);
            GUI.Label(btnRect, LABEL, style);

            // --- Flash overlay ---
            if (_flashTimer > 0f)
            {
                float flashAlpha = _flashTimer / FLASH_DURATION;
                Color flashColor = new Color(1f, 1f, 1f, flashAlpha * 0.6f);
                GUI.color = flashColor;
                GUI.DrawTexture(btnRect, _whiteTex);
                GUI.color = Color.white;
                _flashTimer -= Time.deltaTime;
            }

            // --- Click detection ---
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && _isHovered)
            {
                _flashTimer = FLASH_DURATION;
                OnClick?.Invoke();
                Event.current.Use();
            }
        }

        private void DrawBorder(Rect rect, float thickness, Color color)
        {
            GUI.color = color;
            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTex);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _whiteTex);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTex);
            // Right
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _whiteTex);
            GUI.color = Color.white;
        }
    }
}

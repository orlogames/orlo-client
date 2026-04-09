using UnityEngine;
using Orlo.UI.Settings;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// TMD-styled login and registration screen.
    /// Renders a glassmorphic card with race-tinted holographic UI over LobbyBackground.
    /// Uses Solari palette by default (gold/warm) since race isn't known yet at login.
    /// </summary>
    public class LoginUI : MonoBehaviour
    {
        public System.Action<string, string> OnLogin;
        public System.Action<string, string, string> OnRegister;

        private string _username = "";
        private string _password = "";
        private string _email = "";
        private string _statusMessage = "";
        private string _errorMessage = "";
        private bool _isRegistering = false;
        private bool _visible = true;
        private bool _inputLocked = false;

        // Cached textures
        private Texture2D _whiteTex;
        private Texture2D _fieldBg;

        // Server status
        private bool _serverOnline = false;
        private float _lastStatusCheck = -999f;
        private const float STATUS_CHECK_INTERVAL = 15f;

        // Spring animations
        private SpringValue _cardScale;
        private SpringColor _buttonColor;
        private bool _springsInitialized;

        public void Show() { _visible = true; _inputLocked = false; _statusMessage = ""; _errorMessage = ""; }
        public void Hide() { _visible = false; }
        public void SetStatus(string msg) { _statusMessage = msg; _errorMessage = ""; _inputLocked = false; }
        public void SetError(string msg) { _errorMessage = msg; _statusMessage = ""; _inputLocked = false; }

        private void OnDestroy()
        {
            if (_whiteTex != null) Destroy(_whiteTex);
            if (_fieldBg != null) Destroy(_fieldBg);
        }

        private void EnsureTextures()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            if (_fieldBg == null)
            {
                _fieldBg = new Texture2D(1, 1);
                _fieldBg.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.04f, 0.9f));
                _fieldBg.Apply();
            }
        }

        private void InitSprings()
        {
            if (_springsInitialized) return;
            _springsInitialized = true;
            _cardScale = SpringPresets.PanelOpen(0.8f, 1.0f);
            var p = TMDTheme.Instance?.Palette ?? RacePalette.Solari;
            _buttonColor = new SpringColor(p.Primary, 200f, 0.85f);
            _buttonColor.Target = p.Primary;
        }

        private void Update()
        {
            if (Time.unscaledTime - _lastStatusCheck > STATUS_CHECK_INTERVAL)
            {
                _lastStatusCheck = Time.unscaledTime;
                CheckServerStatus();
            }

            if (_visible && !_inputLocked && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (_username.Length > 0 && _password.Length > 0)
                {
                    _inputLocked = true;
                    _errorMessage = "";
                    if (_isRegistering)
                    {
                        _statusMessage = "Creating account...";
                        OnRegister?.Invoke(_username, _password, _email);
                    }
                    else
                    {
                        _statusMessage = "Signing in...";
                        OnLogin?.Invoke(_username, _password);
                    }
                }
            }

            if (_visible && Input.GetKeyDown(KeyCode.Tab))
            {
                string focused = GUI.GetNameOfFocusedControl();
                if (focused == "username_field")
                    GUI.FocusControl("password_field");
                else if (focused == "password_field" && _isRegistering)
                    GUI.FocusControl("email_field");
                else
                    GUI.FocusControl("username_field");
            }

            // Update springs
            if (_springsInitialized)
            {
                _cardScale.Update(Time.unscaledDeltaTime);
                _buttonColor.Update(Time.unscaledDeltaTime);
            }
        }

        private void CheckServerStatus()
        {
            StartCoroutine(CheckHealthCoroutine());
        }

        private System.Collections.IEnumerator CheckHealthCoroutine()
        {
            var req = UnityEngine.Networking.UnityWebRequest.Get("https://api.orlo.games/health");
            req.timeout = 5;
            yield return req.SendWebRequest();
            _serverOnline = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
            req.Dispose();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureTextures();
            InitSprings();

            var p = TMDTheme.Instance?.Palette ?? RacePalette.Solari;

            DrawTitle(p);
            DrawCard(p);
            DrawBottomBar(p);
        }

        private void DrawTitle(RacePalette p)
        {
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float titleY = Screen.height * 0.12f;
            var titleRect = new Rect(0, titleY, Screen.width, 50);

            // Glow passes (race-colored)
            var glowColor = new Color(p.Glow.r, p.Glow.g, p.Glow.b, 0.12f);
            titleStyle.normal.textColor = glowColor;
            float g = 3f;
            GUI.Label(new Rect(-g, titleY, Screen.width, 50), "ORLO", titleStyle);
            GUI.Label(new Rect(g, titleY, Screen.width, 50), "ORLO", titleStyle);
            GUI.Label(new Rect(0, titleY - g, Screen.width, 50), "ORLO", titleStyle);
            GUI.Label(new Rect(0, titleY + g, Screen.width, 50), "ORLO", titleStyle);

            // Main title (race primary)
            titleStyle.normal.textColor = p.Primary;
            GUI.Label(titleRect, "ORLO", titleStyle);
        }

        private void DrawCard(RacePalette p)
        {
            float cardW = 360f;
            float pad = 24f;
            float fieldH = 28f;
            float labelH = 18f;
            float spacing = 6f;
            float buttonH = 38f;

            float contentH = pad + 24 + 12
                + (labelH + spacing + fieldH + 12) * 2
                + (_isRegistering ? (labelH + spacing + fieldH + 12) : 0)
                + 8 + buttonH + 10 + 20
                + (HasMessage() ? 28 : 0) + pad;

            // Apply spring scale
            float scale = _cardScale.Value;
            float scaledW = cardW * scale;
            float scaledH = contentH * scale;
            float cardX = (Screen.width - scaledW) / 2f;
            float cardY = (Screen.height - scaledH) / 2f;

            // TMD panel background
            var cardRect = new Rect(cardX, cardY, scaledW, scaledH);
            TMDTheme.DrawPanel(cardRect);

            // Content area (use unscaled positions inside the panel)
            float cx = cardX + pad;
            float cy = cardY + pad;
            float innerW = scaledW - pad * 2;

            // Header
            var headerStyle = new GUIStyle(TMDTheme.TitleStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(cx, cy, innerW, 24), _isRegistering ? "CREATE ACCOUNT" : "SIGN IN", headerStyle);
            cy += 36;

            // Field label style (race-tinted dim)
            var labelStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontSize = 11
            };
            labelStyle.normal.textColor = p.TextDim;

            // Field input style
            var fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = _fieldBg, textColor = p.Text },
                focused = { background = _fieldBg, textColor = p.Accent },
                hover = { background = _fieldBg, textColor = p.Text },
                active = { background = _fieldBg, textColor = p.Text },
                padding = new RectOffset(8, 8, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };

            // Username
            GUI.Label(new Rect(cx, cy, innerW, labelH), "USERNAME", labelStyle);
            cy += labelH + spacing;
            GUI.enabled = !_inputLocked;
            GUI.SetNextControlName("username_field");
            _username = GUI.TextField(new Rect(cx, cy, innerW, fieldH), _username, 32, fieldStyle);
            // Field border (race-colored)
            DrawFieldBorder(new Rect(cx, cy, innerW, fieldH), p);
            cy += fieldH + 12;

            // Password
            GUI.Label(new Rect(cx, cy, innerW, labelH), "PASSWORD", labelStyle);
            cy += labelH + spacing;
            GUI.SetNextControlName("password_field");
            _password = GUI.PasswordField(new Rect(cx, cy, innerW, fieldH), _password, '*', 64, fieldStyle);
            DrawFieldBorder(new Rect(cx, cy, innerW, fieldH), p);
            cy += fieldH + 12;

            // Email (register mode)
            if (_isRegistering)
            {
                GUI.Label(new Rect(cx, cy, innerW, labelH), "EMAIL", labelStyle);
                cy += labelH + spacing;
                GUI.SetNextControlName("email_field");
                _email = GUI.TextField(new Rect(cx, cy, innerW, fieldH), _email, 128, fieldStyle);
                DrawFieldBorder(new Rect(cx, cy, innerW, fieldH), p);
                cy += fieldH + 12;
            }
            GUI.enabled = true;

            // Login / Register button
            cy += 8;
            var btnRect = new Rect(cx, cy, innerW, buttonH);
            bool hover = btnRect.Contains(Event.current.mousePosition);
            bool canSubmit = !_inputLocked && _username.Length > 0 && _password.Length > 0;

            // Button with TMD styling
            _buttonColor.Target = hover && canSubmit ? p.Glow : p.Primary;
            Color btnColor = _buttonColor.Value;
            float btnAlpha = canSubmit ? 1f : 0.35f;

            GUI.color = new Color(btnColor.r, btnColor.g, btnColor.b, 0.25f * btnAlpha);
            GUI.DrawTexture(btnRect, _whiteTex);

            // Button border
            GUI.color = new Color(btnColor.r, btnColor.g, btnColor.b, 0.8f * btnAlpha);
            GUI.DrawTexture(new Rect(btnRect.x, btnRect.y, btnRect.width, 1), _whiteTex);
            GUI.DrawTexture(new Rect(btnRect.x, btnRect.yMax - 1, btnRect.width, 1), _whiteTex);
            GUI.DrawTexture(new Rect(btnRect.x, btnRect.y, 1, btnRect.height), _whiteTex);
            GUI.DrawTexture(new Rect(btnRect.xMax - 1, btnRect.y, 1, btnRect.height), _whiteTex);
            GUI.color = Color.white;

            var btnLabelStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnLabelStyle.normal.textColor = new Color(btnColor.r, btnColor.g, btnColor.b, btnAlpha);
            GUI.Label(btnRect, _isRegistering ? "CREATE ACCOUNT" : "SIGN IN", btnLabelStyle);

            GUI.enabled = canSubmit;
            if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
            {
                _inputLocked = true;
                _errorMessage = "";
                if (_isRegistering)
                {
                    _statusMessage = "Creating account...";
                    OnRegister?.Invoke(_username, _password, _email);
                }
                else
                {
                    _statusMessage = "Signing in...";
                    OnLogin?.Invoke(_username, _password);
                }
            }
            GUI.enabled = true;
            cy += buttonH + 10;

            // Toggle link
            var linkRect = new Rect(cx, cy, innerW, 20);
            bool linkHover = linkRect.Contains(Event.current.mousePosition);
            var linkStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            linkStyle.normal.textColor = linkHover ? p.Primary : p.TextDim;
            string linkText = _isRegistering
                ? "Already have an account?  Sign in"
                : "Don't have an account?  Create one";
            GUI.Label(linkRect, linkText, linkStyle);
            if (GUI.Button(linkRect, GUIContent.none, GUIStyle.none))
            {
                _isRegistering = !_isRegistering;
                _statusMessage = "";
                _errorMessage = "";
            }
            cy += 24;

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var statusStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    fontSize = 12, alignment = TextAnchor.MiddleCenter
                };
                statusStyle.normal.textColor = p.Secondary;
                GUI.Label(new Rect(cx, cy, innerW, 20), _statusMessage, statusStyle);
            }

            // Error message
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                var errStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = true
                };
                errStyle.normal.textColor = p.Danger;
                GUI.Label(new Rect(cx, cy, innerW, 20), _errorMessage, errStyle);
            }

            // Scanline overlay on entire card
            TMDTheme.DrawScanlines(cardRect);
        }

        private void DrawFieldBorder(Rect rect, RacePalette p)
        {
            string focused = GUI.GetNameOfFocusedControl();
            bool isFocused = false;
            if (rect.Contains(Event.current.mousePosition)) isFocused = true;

            Color borderColor = isFocused ? p.Primary : p.Border;
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), _whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), _whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), _whiteTex);
            GUI.DrawTexture(new Rect(rect.xMax - 1, rect.y, 1, rect.height), _whiteTex);
            GUI.color = Color.white;
        }

        private void DrawBottomBar(RacePalette p)
        {
            float margin = 16f;

            // Version (bottom-left)
            var versionStyle = new GUIStyle(TMDTheme.LabelStyle) { fontSize = 11 };
            versionStyle.normal.textColor = p.TextDim;
            GUI.Label(new Rect(margin, Screen.height - 30, 200, 20), "v" + Application.version, versionStyle);

            // Server status (bottom-right)
            float rightX = Screen.width - margin;
            var statusStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontSize = 11, alignment = TextAnchor.MiddleRight
            };
            statusStyle.normal.textColor = _serverOnline ? p.Success : p.Danger;
            string statusText = _serverOnline ? "Server Online" : "Server Offline";
            GUI.Label(new Rect(rightX - 120, Screen.height - 30, 120, 20), statusText, statusStyle);

            // Status dot
            float dotSize = 8f;
            GUI.color = _serverOnline ? p.Success : p.Danger;
            GUI.DrawTexture(new Rect(rightX - 134, Screen.height - 24, dotSize, dotSize), _whiteTex);
            GUI.color = Color.white;

            // Settings gear
            float gearY = Screen.height - 58;
            var gearRect = new Rect(rightX - 20, gearY, 20, 20);
            bool gearHover = gearRect.Contains(Event.current.mousePosition);
            var gearStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter
            };
            gearStyle.normal.textColor = gearHover ? p.Primary : p.TextDim;
            GUI.Label(gearRect, "\u2699", gearStyle);

            if (GUI.Button(gearRect, GUIContent.none, GUIStyle.none))
            {
                var settings = FindFirstObjectByType<SettingsUI>();
                if (settings != null)
                    settings.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
            }
        }

        private bool HasMessage()
        {
            return !string.IsNullOrEmpty(_statusMessage) || !string.IsNullOrEmpty(_errorMessage);
        }
    }
}

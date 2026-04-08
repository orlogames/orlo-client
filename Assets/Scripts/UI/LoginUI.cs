using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Premium styled OnGUI login and registration screen.
    /// Renders a centered card with styled fields over LobbyBackground.
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

        // Cached styles and textures (rebuilt each OnGUI for safety)
        private Texture2D _cardBg;
        private Texture2D _fieldBg;
        private Texture2D _buttonBg;
        private Texture2D _buttonHoverBg;
        private Texture2D _borderTex;
        private Texture2D _dotGreen;
        private Texture2D _dotRed;

        // Server status
        private bool _serverOnline = false;
        private float _lastStatusCheck = -999f;
        private const float STATUS_CHECK_INTERVAL = 15f;

        // Hover tracking
        private bool _loginButtonHover = false;
        private bool _linkHover = false;
        private bool _gearHover = false;

        public void Show() { _visible = true; _inputLocked = false; _statusMessage = ""; _errorMessage = ""; }
        public void Hide() { _visible = false; }
        public void SetStatus(string msg) { _statusMessage = msg; _errorMessage = ""; _inputLocked = false; }
        public void SetError(string msg) { _errorMessage = msg; _statusMessage = ""; _inputLocked = false; }

        private void OnDestroy()
        {
            DestroyTex(_cardBg);
            DestroyTex(_fieldBg);
            DestroyTex(_buttonBg);
            DestroyTex(_buttonHoverBg);
            DestroyTex(_borderTex);
            DestroyTex(_dotGreen);
            DestroyTex(_dotRed);
        }

        private void DestroyTex(Texture2D tex)
        {
            if (tex != null) Destroy(tex);
        }

        private Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void EnsureTextures()
        {
            if (_cardBg == null) _cardBg = MakeTex(new Color(0.06f, 0.06f, 0.1f, 0.92f));
            if (_fieldBg == null) _fieldBg = MakeTex(new Color(0.04f, 0.04f, 0.07f, 1f));
            if (_buttonBg == null) _buttonBg = MakeTex(new Color(0.2f, 0.35f, 0.7f, 1f));
            if (_buttonHoverBg == null) _buttonHoverBg = MakeTex(new Color(0.3f, 0.45f, 0.85f, 1f));
            if (_borderTex == null) _borderTex = MakeTex(new Color(0.25f, 0.35f, 0.6f, 0.6f));
            if (_dotGreen == null) _dotGreen = MakeTex(new Color(0.2f, 0.9f, 0.3f, 1f));
            if (_dotRed == null) _dotRed = MakeTex(new Color(0.9f, 0.2f, 0.2f, 1f));
        }

        private void Update()
        {
            // Periodic server status check
            if (Time.unscaledTime - _lastStatusCheck > STATUS_CHECK_INTERVAL)
            {
                _lastStatusCheck = Time.unscaledTime;
                CheckServerStatus();
            }

            // Enter key submits
            if (_visible && !_inputLocked && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (_username.Length > 0 && _password.Length > 0)
                {
                    if (_isRegistering)
                    {
                        _inputLocked = true;
                        _statusMessage = "Creating account...";
                        _errorMessage = "";
                        OnRegister?.Invoke(_username, _password, _email);
                    }
                    else
                    {
                        _inputLocked = true;
                        _statusMessage = "Signing in...";
                        _errorMessage = "";
                        OnLogin?.Invoke(_username, _password);
                    }
                }
            }

            // Tab key cycles fields
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
        }

        private void CheckServerStatus()
        {
            // Simple coroutine-free approach: use UnityWebRequest in a coroutine
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

            // ── ORLO Title with glow ──
            DrawTitle();

            // ── Login Card ──
            DrawCard();

            // ── Bottom bar: version, server status, settings gear ──
            DrawBottomBar();
        }

        // ────────────────────────────────────────────────────
        //  TITLE
        // ────────────────────────────────────────────────────
        private void DrawTitle()
        {
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float titleY = Screen.height * 0.12f;
            var titleRect = new Rect(0, titleY, Screen.width, 50);

            // Glow passes (4 offset draws at low alpha)
            var glowColor = new Color(0.7f, 0.85f, 1f, 0.12f);
            titleStyle.normal.textColor = glowColor;
            float g = 3f;
            GUI.Label(new Rect(titleRect.x - g, titleRect.y, titleRect.width, titleRect.height), "ORLO", titleStyle);
            GUI.Label(new Rect(titleRect.x + g, titleRect.y, titleRect.width, titleRect.height), "ORLO", titleStyle);
            GUI.Label(new Rect(titleRect.x, titleRect.y - g, titleRect.width, titleRect.height), "ORLO", titleStyle);
            GUI.Label(new Rect(titleRect.x, titleRect.y + g, titleRect.width, titleRect.height), "ORLO", titleStyle);

            // Main title
            titleStyle.normal.textColor = new Color(0.7f, 0.85f, 1f, 1f);
            GUI.Label(titleRect, "ORLO", titleStyle);
        }

        // ────────────────────────────────────────────────────
        //  CARD
        // ────────────────────────────────────────────────────
        private void DrawCard()
        {
            float cardW = 360f;
            float pad = 24f;
            float fieldH = 28f;
            float labelH = 18f;
            float spacing = 6f;
            float buttonH = 38f;

            // Calculate card height dynamically
            float contentH = pad                         // top padding
                + 24 + 12                                // header + gap
                + (labelH + spacing + fieldH + 12) * 2   // username + password blocks
                + (_isRegistering ? (labelH + spacing + fieldH + 12) : 0) // email block
                + 8 + buttonH + 10                       // button
                + 20                                     // link text
                + (HasMessage() ? 28 : 0)                // status/error
                + pad;                                   // bottom padding

            float cardX = (Screen.width - cardW) / 2f;
            float cardY = (Screen.height - contentH) / 2f;
            var cardRect = new Rect(cardX, cardY, cardW, contentH);

            // Border (drawn slightly larger)
            float b = 1f;
            GUI.DrawTexture(new Rect(cardRect.x - b, cardRect.y - b, cardRect.width + b * 2, cardRect.height + b * 2), _borderTex);

            // Card background
            GUI.DrawTexture(cardRect, _cardBg);

            // Content area
            float cx = cardX + pad;
            float cy = cardY + pad;
            float innerW = cardW - pad * 2;

            // ── Header ──
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(cx, cy, innerW, 24), _isRegistering ? "Create Account" : "Sign In", headerStyle);
            cy += 24 + 12;

            // ── Field styles ──
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.6f, 0.65f, 0.75f, 1f) }
            };
            var fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = _fieldBg, textColor = new Color(0.9f, 0.92f, 0.96f, 1f) },
                focused = { background = _fieldBg, textColor = Color.white },
                hover = { background = _fieldBg, textColor = Color.white },
                active = { background = _fieldBg, textColor = Color.white },
                padding = new RectOffset(8, 8, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };

            // ── Username ──
            GUI.Label(new Rect(cx, cy, innerW, labelH), "USERNAME", labelStyle);
            cy += labelH + spacing;
            GUI.enabled = !_inputLocked;
            GUI.SetNextControlName("username_field");
            _username = GUI.TextField(new Rect(cx, cy, innerW, fieldH), _username, 32, fieldStyle);
            cy += fieldH + 12;

            // ── Password ──
            GUI.Label(new Rect(cx, cy, innerW, labelH), "PASSWORD", labelStyle);
            cy += labelH + spacing;
            GUI.SetNextControlName("password_field");
            _password = GUI.PasswordField(new Rect(cx, cy, innerW, fieldH), _password, '*', 64, fieldStyle);
            cy += fieldH + 12;

            // ── Email (register mode) ──
            if (_isRegistering)
            {
                GUI.Label(new Rect(cx, cy, innerW, labelH), "EMAIL", labelStyle);
                cy += labelH + spacing;
                GUI.SetNextControlName("email_field");
                _email = GUI.TextField(new Rect(cx, cy, innerW, fieldH), _email, 128, fieldStyle);
                cy += fieldH + 12;
            }
            GUI.enabled = true;

            // ── Login / Register button ──
            cy += 8;
            var btnRect = new Rect(cx, cy, innerW, buttonH);
            _loginButtonHover = btnRect.Contains(Event.current.mousePosition);
            bool canSubmit = !_inputLocked && _username.Length > 0 && _password.Length > 0;

            var btnTex = (_loginButtonHover && canSubmit) ? _buttonHoverBg : _buttonBg;
            float btnAlpha = canSubmit ? 1f : 0.4f;
            var prevColor = GUI.color;
            GUI.color = new Color(1, 1, 1, btnAlpha);
            GUI.DrawTexture(btnRect, btnTex);
            GUI.color = prevColor;

            var btnLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            string btnText = _isRegistering ? "Create Account" : "Sign In";
            GUI.Label(btnRect, btnText, btnLabelStyle);

            // Invisible button on top for click
            GUI.enabled = canSubmit;
            var invisBtnStyle = new GUIStyle(GUI.skin.label); // no visual
            if (GUI.Button(btnRect, GUIContent.none, invisBtnStyle))
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

            // ── Toggle link ──
            var linkRect = new Rect(cx, cy, innerW, 20);
            _linkHover = linkRect.Contains(Event.current.mousePosition);
            var linkStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _linkHover ? new Color(0.7f, 0.85f, 1f, 1f) : new Color(0.45f, 0.55f, 0.75f, 1f) }
            };
            string linkText = _isRegistering
                ? "Already have an account?  Sign in"
                : "Don't have an account?  Create one";
            GUI.Label(linkRect, linkText, linkStyle);
            if (GUI.Button(linkRect, GUIContent.none, invisBtnStyle))
            {
                _isRegistering = !_isRegistering;
                _statusMessage = "";
                _errorMessage = "";
            }

            // Underline effect on hover
            if (_linkHover)
            {
                // Approximate underline: measure rough text width and draw a 1px line
                float textW = linkText.Length * 5.5f; // rough estimate
                float lineX = cx + (innerW - textW) / 2f;
                GUI.DrawTexture(new Rect(lineX, cy + 17, textW, 1), _borderTex);
            }
            cy += 24;

            // ── Status message ──
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.4f, 0.9f, 0.95f, 1f) }
                };
                GUI.Label(new Rect(cx, cy, innerW, 20), _statusMessage, statusStyle);
            }

            // ── Error message ──
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                var errStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    normal = { textColor = new Color(1f, 0.35f, 0.3f, 1f) }
                };
                GUI.Label(new Rect(cx, cy, innerW, 20), _errorMessage, errStyle);
            }
        }

        // ────────────────────────────────────────────────────
        //  BOTTOM BAR
        // ────────────────────────────────────────────────────
        private void DrawBottomBar()
        {
            float margin = 16f;

            // ── Version (bottom-left) ──
            var versionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.35f, 0.38f, 0.45f, 1f) }
            };
            string version = "v" + Application.version;
            GUI.Label(new Rect(margin, Screen.height - 30, 200, 20), version, versionStyle);

            // ── Server status (bottom-right) ──
            float rightX = Screen.width - margin;
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.53f, 0.6f, 1f) }
            };
            string statusText = _serverOnline ? "Server Online" : "Server Offline";
            float statusW = 100f;
            float dotSize = 8f;
            float statusY = Screen.height - 30;

            GUI.Label(new Rect(rightX - statusW, statusY, statusW, 20), statusText, statusStyle);

            // Dot
            var dotTex = _serverOnline ? _dotGreen : _dotRed;
            float dotX = rightX - statusW - dotSize - 4;
            float dotY = statusY + 6;
            GUI.DrawTexture(new Rect(dotX, dotY, dotSize, dotSize), dotTex);

            // ── Settings gear (above server status) ──
            float gearY = statusY - 28;
            float gearSize = 20f;
            var gearRect = new Rect(rightX - gearSize, gearY, gearSize, gearSize);
            _gearHover = gearRect.Contains(Event.current.mousePosition);

            var gearStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _gearHover ? new Color(0.7f, 0.75f, 0.85f, 1f) : new Color(0.4f, 0.43f, 0.5f, 1f) }
            };
            GUI.Label(gearRect, "\u2699", gearStyle); // Unicode gear

            if (GUI.Button(gearRect, GUIContent.none, GUIStyle.none))
            {
                var settings = FindObjectOfType<SettingsUI>();
                if (settings != null)
                {
                    // Toggle settings visibility via reflection or public method if available
                    settings.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        private bool HasMessage()
        {
            return !string.IsNullOrEmpty(_statusMessage) || !string.IsNullOrEmpty(_errorMessage);
        }
    }
}

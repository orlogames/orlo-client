using UnityEngine;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// OnGUI login and registration screen.
    /// Shows username/password fields with Login and Register buttons.
    /// </summary>
    public class LoginUI : MonoBehaviour
    {
        public System.Action<string, string> OnLogin;
        public System.Action<string, string, string> OnRegister;

        private string _username = "";
        private string _password = "";
        private string _email = "";
        private string _statusMessage = "";
        private bool _isRegistering = false;
        private bool _visible = true;
        private bool _inputLocked = false;

        public void Show() { _visible = true; _inputLocked = false; _statusMessage = ""; }
        public void Hide() { _visible = false; }

        public void SetStatus(string msg) { _statusMessage = msg; _inputLocked = false; }
        public void SetError(string msg) { _statusMessage = msg; _inputLocked = false; }

        private void OnGUI()
        {
            if (!_visible) return;

            float w = 360, h = _isRegistering ? 320 : 280;
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);

            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Space(15);

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            GUILayout.Label(_isRegistering ? "Create Account" : "Orlo Login", titleStyle);
            GUILayout.Space(15);

            // Username
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Username", GUILayout.Width(80));
            GUI.enabled = !_inputLocked;
            _username = GUILayout.TextField(_username, 32, GUILayout.Width(220));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Password
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Password", GUILayout.Width(80));
            GUI.enabled = !_inputLocked;
            _password = GUILayout.PasswordField(_password, '*', 64, GUILayout.Width(220));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Email (register mode only)
            if (_isRegistering)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label("Email", GUILayout.Width(80));
                GUI.enabled = !_inputLocked;
                _email = GUILayout.TextField(_email, 128, GUILayout.Width(220));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }

            GUILayout.Space(10);

            // Buttons
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUI.enabled = !_inputLocked && _username.Length > 0 && _password.Length > 0;

            if (_isRegistering)
            {
                if (GUILayout.Button("Register", GUILayout.Height(35), GUILayout.Width(150)))
                {
                    _inputLocked = true;
                    _statusMessage = "Registering...";
                    OnRegister?.Invoke(_username, _password, _email);
                }
            }
            else
            {
                if (GUILayout.Button("Login", GUILayout.Height(35), GUILayout.Width(150)))
                {
                    _inputLocked = true;
                    _statusMessage = "Logging in...";
                    OnLogin?.Invoke(_username, _password);
                }
            }
            GUI.enabled = !_inputLocked;

            if (GUILayout.Button(_isRegistering ? "Back to Login" : "New Account", GUILayout.Height(35), GUILayout.Width(150)))
            {
                _isRegistering = !_isRegistering;
                _statusMessage = "";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(10);
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = _statusMessage.Contains("fail") || _statusMessage.Contains("error")
                        ? Color.red : Color.yellow }
                };
                GUILayout.Label(_statusMessage, statusStyle);
            }

            GUILayout.EndArea();
        }
    }
}

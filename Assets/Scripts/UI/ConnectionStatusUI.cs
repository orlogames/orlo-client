using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Full-screen overlay showing connection status, errors, and retry options.
    /// Displayed when the client is connecting to the game server or when an error occurs.
    /// </summary>
    public class ConnectionStatusUI : MonoBehaviour
    {
        public static ConnectionStatusUI Instance { get; private set; }

        private bool _visible;
        private string _status = "Connecting...";
        private string _error;
        private bool _showRetry;
        private float _dotTimer;
        private int _dotCount;

        public System.Action OnRetry;
        public System.Action OnQuit;
        private bool _showQuit;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Show(string status)
        {
            _visible = true;
            _status = status;
            _error = null;
            _showRetry = false;
        }

        public void SetStatus(string status)
        {
            _status = status;
            _error = null;
            _showRetry = false;
        }

        public void ShowError(string error, bool showQuit = false)
        {
            _error = error;
            _showRetry = true;
            _showQuit = showQuit;
        }

        public void Hide()
        {
            _visible = false;
        }

        private void Update()
        {
            if (!_visible) return;
            _dotTimer += Time.deltaTime;
            if (_dotTimer > 0.5f)
            {
                _dotTimer = 0;
                _dotCount = (_dotCount + 1) % 4;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // Full-screen dark overlay
            var bgColor = new Color(0.03f, 0.02f, 0.06f, 0.95f);
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

            // ORLO title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.7f, 0.85f, 1.0f);
            GUI.Label(new Rect(cx - 200, cy - 120, 400, 60), "ORLO", titleStyle);

            // Status text with animated dots
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            statusStyle.normal.textColor = new Color(0.6f, 0.65f, 0.75f);

            string dots = new string('.', _dotCount);
            string displayStatus = _error == null ? _status + dots : _status;
            GUI.Label(new Rect(cx - 300, cy - 40, 600, 30), displayStatus, statusStyle);

            // Error message
            if (_error != null)
            {
                var errorStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                errorStyle.normal.textColor = new Color(1.0f, 0.4f, 0.4f);
                GUI.Label(new Rect(cx - 300, cy, 600, 50), _error, errorStyle);
            }

            // Retry and Quit buttons
            if (_showRetry)
            {
                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };

                if (_showQuit)
                {
                    // Two buttons side-by-side: Retry + Quit
                    float btnW = 140;
                    float gap = 20;
                    float startBtnX = cx - (btnW * 2 + gap) / 2f;

                    if (GUI.Button(new Rect(startBtnX, cy + 60, btnW, 40), "Retry", btnStyle))
                    {
                        _error = null;
                        _showRetry = false;
                        _status = "Connecting";
                        OnRetry?.Invoke();
                    }

                    GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
                    if (GUI.Button(new Rect(startBtnX + btnW + gap, cy + 60, btnW, 40), "Quit", btnStyle))
                    {
                        OnQuit?.Invoke();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    // Single retry button
                    if (GUI.Button(new Rect(cx - 80, cy + 60, 160, 40), "Retry", btnStyle))
                    {
                        _error = null;
                        _showRetry = false;
                        _status = "Connecting";
                        OnRetry?.Invoke();
                    }
                }
            }

            // Server info (small, bottom)
            var infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            infoStyle.normal.textColor = new Color(0.4f, 0.4f, 0.5f);
            GUI.Label(new Rect(cx - 200, Screen.height - 40, 400, 20),
                $"Server: {Network.NetworkManager.Instance?.ServerHost ?? "unknown"}", infoStyle);
        }
    }
}

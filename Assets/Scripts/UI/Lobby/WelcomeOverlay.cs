using System;
using UnityEngine;

namespace Orlo.UI.Lobby
{
    public class WelcomeOverlay : MonoBehaviour
    {
        public static WelcomeOverlay Instance { get; private set; }

        public Action OnDismissed;

        private enum Mode { None, FirstTime, Returning }
        private Mode _mode = Mode.None;

        private string _playerName;
        private string[] _events;
        private int _unreadMail;
        private string _patchVersion;

        private float _returnBannerY;
        private float _returnBannerTargetY;
        private float _returnShowTime;
        private const float ReturnAutoDismiss = 8f;
        private const float SlideSpeed = 600f;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _controlLabelStyle;
        private GUIStyle _controlKeyStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _bannerTitleStyle;
        private GUIStyle _bannerBodyStyle;
        private bool _stylesInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static bool ShouldShow()
        {
            return PlayerPrefs.GetInt("welcome_shown", 0) == 0;
        }

        public void ShowFirstTime()
        {
            _mode = Mode.FirstTime;
            Orlo.Player.OrbitCamera.BlockInput = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ShowReturning(string playerName, string[] events, int unreadMail, string patchVersion)
        {
            _playerName = playerName;
            _events = events;
            _unreadMail = unreadMail;
            _patchVersion = patchVersion;
            _mode = Mode.Returning;
            _returnBannerY = -200f;
            _returnBannerTargetY = 40f;
            _returnShowTime = Time.time;
        }

        public void Hide()
        {
            Orlo.Player.OrbitCamera.BlockInput = false;
            if (_mode == Mode.FirstTime)
            {
                PlayerPrefs.SetInt("welcome_shown", 1);
                PlayerPrefs.Save();
            }
            _mode = Mode.None;
            OnDismissed?.Invoke();
        }

        private void InitStyles()
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.84f, 0f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            _controlKeyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.85f, 0.6f) }
            };
            _controlLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(20, 20, 8, 8)
            };
            _bannerTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _bannerBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            _stylesInitialized = true;
        }

        private void Update()
        {
            if (_mode == Mode.Returning)
            {
                _returnBannerY = Mathf.MoveTowards(_returnBannerY, _returnBannerTargetY, SlideSpeed * Time.deltaTime);
                if (Time.time - _returnShowTime > ReturnAutoDismiss)
                    _returnBannerTargetY = -200f;
                if (_returnBannerTargetY < 0 && _returnBannerY <= -199f)
                    Hide();
            }
        }

        public void SetReturnData(string playerName, string[] events, int unreadMail, string patchVersion)
        {
            _playerName = playerName;
            _events = events;
            _unreadMail = unreadMail;
            _patchVersion = patchVersion;
        }

        private void OnGUI()
        {
            if (_mode == Mode.None) return;
            if (!_stylesInitialized) InitStyles();

            if (_mode == Mode.FirstTime) DrawFirstTime();
            else if (_mode == Mode.Returning) DrawReturning();
        }

        private void DrawFirstTime()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.85f), 0, 0);

            float cardW = 500f, cardH = 400f;
            float cx = (Screen.width - cardW) / 2f;
            float cy = (Screen.height - cardH) / 2f;

            GUI.DrawTexture(new Rect(cx, cy, cardW, cardH), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, new Color(0.08f, 0.08f, 0.12f, 0.95f), 0, 0);

            float y = cy + 20f;
            GUI.Label(new Rect(cx, y, cardW, 40f), "Welcome to Orlo", _titleStyle);
            y += 48f;

            GUI.DrawTexture(new Rect(cx + 40f, y, cardW - 80f, 1f), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, new Color(0.4f, 0.35f, 0.2f, 0.6f), 0, 0);
            y += 12f;

            GUI.Label(new Rect(cx + 30f, y, cardW - 60f, 50f),
                "Your journey begins at Threshold, a frontier settlement on Veridian Prime.", _bodyStyle);
            y += 55f;

            string[] keys = { "WASD", "Mouse", "Left Click", "Tab", "I", "M", "Enter" };
            string[] descs = { "Move", "Look around", "Attack target", "Cycle targets", "Inventory", "Minimap", "Chat" };
            for (int i = 0; i < keys.Length; i++)
            {
                GUI.Label(new Rect(cx + 60f, y, 100f, 22f), keys[i], _controlKeyStyle);
                GUI.Label(new Rect(cx + 170f, y, 260f, 22f), "\u2014  " + descs[i], _controlLabelStyle);
                y += 24f;
            }

            y += 12f;
            float btnW = 200f, btnH = 36f;
            if (GUI.Button(new Rect(cx + (cardW - btnW) / 2f, y, btnW, btnH), "Begin Your Journey", _buttonStyle))
                Hide();
        }

        private void DrawReturning()
        {
            float bannerW = 600f;
            float bannerX = (Screen.width - bannerW) / 2f;

            int lineCount = 0;
            if (_events != null) lineCount += _events.Length;
            if (_unreadMail > 0) lineCount++;
            if (!string.IsNullOrEmpty(_patchVersion)) lineCount++;
            float bannerH = 70f + lineCount * 22f;

            GUI.DrawTexture(new Rect(bannerX, _returnBannerY, bannerW, bannerH), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, new Color(0.06f, 0.06f, 0.1f, 0.92f), 0, 0);

            float y = _returnBannerY + 12f;
            string title = string.IsNullOrEmpty(_playerName) ? "Welcome back" : "Welcome back, " + _playerName;
            GUI.Label(new Rect(bannerX, y, bannerW, 30f), title, _bannerTitleStyle);
            y += 34f;

            float tx = bannerX + 30f;
            if (_events != null)
            {
                foreach (var ev in _events)
                {
                    GUI.Label(new Rect(tx, y, bannerW - 60f, 20f), "\u2022 " + ev, _bannerBodyStyle);
                    y += 22f;
                }
            }
            if (_unreadMail > 0)
            {
                GUI.Label(new Rect(tx, y, bannerW - 60f, 20f), "\u2022 " + _unreadMail + " unread mail", _bannerBodyStyle);
                y += 22f;
            }
            if (!string.IsNullOrEmpty(_patchVersion))
            {
                GUI.Label(new Rect(tx, y, bannerW - 60f, 20f), "\u2022 Patch " + _patchVersion, _bannerBodyStyle);
                y += 22f;
            }

            float dismissW = 80f;
            if (GUI.Button(new Rect(bannerX + bannerW - dismissW - 15f, _returnBannerY + 8f, dismissW, 24f), "Dismiss", _buttonStyle))
                _returnBannerTargetY = -200f;
        }
    }
}

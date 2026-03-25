using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Notification toast system — slides notifications in from the right.
    /// Supports Info, Warning, Error, Achievement, and Discovery types.
    /// </summary>
    public class NotificationUI : MonoBehaviour
    {
        private struct Notification
        {
            public string title;
            public string message;
            public int type; // 0=Info, 1=Warning, 2=Error, 3=Achievement, 4=Discovery
            public float timeRemaining;
            public float slideIn; // 0..1 animation
        }

        private List<Notification> notifications = new List<Notification>();
        private const float DEFAULT_DURATION = 5f;
        private const float MAX_VISIBLE = 5;
        private const float NOTIFICATION_WIDTH = 320f;
        private const float NOTIFICATION_HEIGHT = 60f;
        private const float SPACING = 8f;
        private const float SLIDE_SPEED = 5f;

        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle messageStyle;
        private bool stylesInitialized = false;

        private static NotificationUI instance;
        public static NotificationUI Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        /// <summary>
        /// Called when server sends a Notification message
        /// </summary>
        public void Show(string title, string message, int type = 0, float duration = 0f)
        {
            if (duration <= 0f) duration = DEFAULT_DURATION;
            if (type == 3) duration = 8f; // Achievements stay longer

            notifications.Add(new Notification
            {
                title = title,
                message = message,
                type = type,
                timeRemaining = duration,
                slideIn = 0f
            });

            // Cap the list
            while (notifications.Count > 10)
                notifications.RemoveAt(0);
        }

        /// <summary>
        /// Convenience methods
        /// </summary>
        public void ShowInfo(string title, string message) => Show(title, message, 0);
        public void ShowWarning(string title, string message) => Show(title, message, 1);
        public void ShowError(string title, string message) => Show(title, message, 2);
        public void ShowAchievement(string title, string message) => Show(title, message, 3);
        public void ShowDiscovery(string title, string message) => Show(title, message, 4);

        private void Update()
        {
            for (int i = notifications.Count - 1; i >= 0; i--)
            {
                var n = notifications[i];
                n.timeRemaining -= Time.deltaTime;
                n.slideIn = Mathf.Min(1f, n.slideIn + Time.deltaTime * SLIDE_SPEED);
                notifications[i] = n;

                if (n.timeRemaining <= 0f)
                    notifications.RemoveAt(i);
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(12, 12, 8, 8);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 13;

            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.fontSize = 11;
            messageStyle.wordWrap = true;
            messageStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        }

        private void OnGUI()
        {
            if (notifications.Count == 0) return;
            InitStyles();

            int visible = Mathf.Min(notifications.Count, (int)MAX_VISIBLE);
            float startY = 80f;

            for (int i = 0; i < visible; i++)
            {
                var n = notifications[notifications.Count - 1 - i];

                // Slide from right
                float slideOffset = (1f - n.slideIn) * (NOTIFICATION_WIDTH + 20f);
                // Fade out in last second
                float alpha = Mathf.Clamp01(n.timeRemaining);

                Color bgColor = n.type switch
                {
                    1 => new Color(0.5f, 0.4f, 0.1f, 0.9f * alpha),  // Warning
                    2 => new Color(0.5f, 0.1f, 0.1f, 0.9f * alpha),  // Error
                    3 => new Color(0.3f, 0.2f, 0.5f, 0.92f * alpha), // Achievement
                    4 => new Color(0.1f, 0.3f, 0.4f, 0.9f * alpha),  // Discovery
                    _ => new Color(0.1f, 0.12f, 0.18f, 0.9f * alpha), // Info
                };

                Color titleColor = n.type switch
                {
                    3 => new Color(1f, 0.8f, 0.2f),       // Achievement gold
                    4 => new Color(0.3f, 0.9f, 0.9f),     // Discovery cyan
                    2 => new Color(1f, 0.4f, 0.4f),       // Error red
                    1 => new Color(1f, 0.85f, 0.3f),      // Warning yellow
                    _ => Color.white,
                };

                float x = Screen.width - NOTIFICATION_WIDTH - 20f + slideOffset;
                float y = startY + i * (NOTIFICATION_HEIGHT + SPACING);

                boxStyle.normal.background = MakeTex(1, 1, bgColor);
                GUI.Box(new Rect(x, y, NOTIFICATION_WIDTH, NOTIFICATION_HEIGHT), "", boxStyle);

                // Icon prefix
                string prefix = n.type switch
                {
                    1 => "! ",
                    2 => "X ",
                    3 => "* ",
                    4 => "? ",
                    _ => ""
                };

                titleStyle.normal.textColor = titleColor * alpha;
                GUI.Label(new Rect(x + 12, y + 6, NOTIFICATION_WIDTH - 24, 20),
                    prefix + n.title, titleStyle);

                messageStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, alpha);
                GUI.Label(new Rect(x + 12, y + 26, NOTIFICATION_WIDTH - 24, 30),
                    n.message, messageStyle);
            }
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }
}

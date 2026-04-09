using UnityEngine;
using System.Collections.Generic;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// Notification toast system — slides notifications in from the right using spring animation.
    /// Supports Info, Warning, Error, Achievement, and Discovery types.
    /// TMD Integration: panel backgrounds, race-colored borders, spring-animated slide-in.
    /// </summary>
    public class NotificationUI : MonoBehaviour
    {
        private struct Notification
        {
            public string title;
            public string message;
            public int type; // 0=Info, 1=Warning, 2=Error, 3=Achievement, 4=Discovery
            public float timeRemaining;
            public SpringValue slideSpring; // Spring-animated X position
            public float fadeAlpha; // For exit fade
        }

        private List<Notification> notifications = new List<Notification>();
        private const float DEFAULT_DURATION = 5f;
        private const float MAX_VISIBLE = 5;
        private const float NOTIFICATION_WIDTH = 320f;
        private const float NOTIFICATION_HEIGHT = 60f;
        private const float SPACING = 8f;

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

            // Spring-animated slide-in from right edge
            var spring = SpringPresets.NotificationSlideIn(Screen.width);

            notifications.Add(new Notification
            {
                title = title,
                message = message,
                type = type,
                timeRemaining = duration,
                slideSpring = spring,
                fadeAlpha = 1f
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
            float dt = Time.deltaTime;
            for (int i = notifications.Count - 1; i >= 0; i--)
            {
                var n = notifications[i];
                n.timeRemaining -= dt;

                // Update spring animation
                n.slideSpring.Update(dt);

                // Fade out in last second
                if (n.timeRemaining < 1f)
                    n.fadeAlpha = Mathf.Clamp01(n.timeRemaining);

                notifications[i] = n;

                if (n.timeRemaining <= 0f)
                    notifications.RemoveAt(i);
            }
        }

        private void OnGUI()
        {
            if (notifications.Count == 0) return;

            var p = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            int visible = Mathf.Min(notifications.Count, (int)MAX_VISIBLE);
            float startY = 80f;

            for (int i = 0; i < visible; i++)
            {
                var n = notifications[notifications.Count - 1 - i];
                float alpha = n.fadeAlpha;

                // Spring-animated X position
                float x = n.slideSpring.Value;
                float y = startY + i * (NOTIFICATION_HEIGHT + SPACING);

                Rect notifRect = new Rect(x, y, NOTIFICATION_WIDTH, NOTIFICATION_HEIGHT);

                // TMD panel background with fade
                GUI.color = new Color(1, 1, 1, alpha);
                TMDTheme.DrawPanel(notifRect);

                // Type-specific accent border on left edge
                Color accentColor = GetTypeAccentColor(n.type, p);
                GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.9f * alpha);
                GUI.DrawTexture(new Rect(x, y, 3, NOTIFICATION_HEIGHT), Texture2D.whiteTexture);

                // Type-specific top border glow
                GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.3f * alpha);
                GUI.DrawTexture(new Rect(x, y, NOTIFICATION_WIDTH, 1), Texture2D.whiteTexture);

                // Icon prefix
                string prefix = n.type switch
                {
                    1 => "! ",
                    2 => "X ",
                    3 => "* ",
                    4 => "? ",
                    _ => ""
                };

                // Title — race-colored for achievements, type-colored for others
                Color titleColor = GetTypeTitleColor(n.type, p);
                var titleStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 13;
                titleStyle.normal.textColor = new Color(titleColor.r, titleColor.g, titleColor.b, alpha);
                GUI.Label(new Rect(x + 12, y + 6, NOTIFICATION_WIDTH - 24, 20),
                    prefix + n.title, titleStyle);

                // Message — race text color
                var messageStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
                messageStyle.fontSize = 11;
                messageStyle.wordWrap = true;
                messageStyle.normal.textColor = new Color(p.Text.r, p.Text.g, p.Text.b, alpha * 0.85f);
                GUI.Label(new Rect(x + 12, y + 26, NOTIFICATION_WIDTH - 24, 30),
                    n.message, messageStyle);

                // Scanlines on each notification
                TMDTheme.DrawScanlines(notifRect);
            }

            GUI.color = Color.white;
        }

        private static Color GetTypeAccentColor(int type, RacePalette p)
        {
            return type switch
            {
                1 => Color.Lerp(p.Primary, new Color(1f, 0.85f, 0.3f), 0.5f), // Warning — race-tinted yellow
                2 => p.Danger,                                                    // Error
                3 => p.Primary,                                                   // Achievement — full race primary
                4 => p.Secondary,                                                 // Discovery — race secondary
                _ => p.Border,                                                    // Info — subtle border
            };
        }

        private static Color GetTypeTitleColor(int type, RacePalette p)
        {
            return type switch
            {
                1 => Color.Lerp(p.Primary, new Color(1f, 0.85f, 0.3f), 0.5f), // Warning
                2 => p.Danger,                                                    // Error
                3 => p.Primary,                                                   // Achievement — race gold/green/ember/purple
                4 => p.Secondary,                                                 // Discovery
                _ => p.Text,                                                      // Info
            };
        }
    }
}

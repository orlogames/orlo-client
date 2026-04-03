using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Manages progressive UI disclosure based on player level/progression.
    /// UI elements register with this system and are hidden until the player
    /// reaches the required level. Shows a brief notification when new UI unlocks.
    ///
    /// Level thresholds:
    ///   1  — Movement + basic combat bar
    ///   3  — Inventory + minimap
    ///   5  — Crafting + TMD
    ///   8  — Vendor + trading
    ///   10 — Guild + party + full social
    /// </summary>
    public class ProgressiveDisclosure : MonoBehaviour
    {
        public static ProgressiveDisclosure Instance { get; private set; }

        /// <summary>A UI element registered for progressive unlock.</summary>
        private struct RegisteredUI
        {
            public MonoBehaviour Component;
            public string DisplayName;
            public int RequiredLevel;
            public bool Unlocked;
        }

        private readonly List<RegisteredUI> _registeredUIs = new();
        private int _playerLevel = 1;

        // Unlock notification
        private string _unlockMessage;
        private float _unlockTimer;
        private const float UnlockNotificationDuration = 4f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Register a UI component for progressive disclosure.
        /// The component's GameObject will be disabled until the player reaches the required level.
        /// </summary>
        public void Register(MonoBehaviour component, string displayName, int requiredLevel)
        {
            if (component == null) return;

            _registeredUIs.Add(new RegisteredUI
            {
                Component = component,
                DisplayName = displayName,
                RequiredLevel = requiredLevel,
                Unlocked = false
            });

            // Apply current state immediately
            bool shouldUnlock = _playerLevel >= requiredLevel;
            component.enabled = shouldUnlock;
        }

        /// <summary>
        /// Called when the player's level changes. Evaluates all registered UIs
        /// and unlocks any that meet the new level threshold.
        /// </summary>
        public void SetPlayerLevel(int level)
        {
            int previousLevel = _playerLevel;
            _playerLevel = level;

            if (level <= previousLevel) return; // No unlocks on level decrease

            List<string> newlyUnlocked = new();

            for (int i = 0; i < _registeredUIs.Count; i++)
            {
                var ui = _registeredUIs[i];
                if (!ui.Unlocked && _playerLevel >= ui.RequiredLevel && ui.Component != null)
                {
                    ui.Unlocked = true;
                    ui.Component.enabled = true;
                    _registeredUIs[i] = ui;
                    newlyUnlocked.Add(ui.DisplayName);
                }
            }

            // Show unlock notification
            if (newlyUnlocked.Count > 0)
            {
                _unlockMessage = "New UI Unlocked: " + string.Join(", ", newlyUnlocked);
                _unlockTimer = UnlockNotificationDuration;

                // Also push to the notification system if available
                if (NotificationUI.Instance != null)
                {
                    foreach (var name in newlyUnlocked)
                    {
                        NotificationUI.Instance.ShowInfo("UI Unlocked", $"{name} is now available!");
                    }
                }
            }
        }

        /// <summary>Returns whether a given level threshold has been reached.</summary>
        public bool IsUnlocked(int requiredLevel)
        {
            return _playerLevel >= requiredLevel;
        }

        /// <summary>Current player level tracked by the disclosure system.</summary>
        public int PlayerLevel => _playerLevel;

        private void Update()
        {
            if (_unlockTimer > 0)
                _unlockTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (_unlockTimer <= 0 || string.IsNullOrEmpty(_unlockMessage)) return;

            float alpha = Mathf.Clamp01(_unlockTimer / 1f); // fade in last second
            if (_unlockTimer > UnlockNotificationDuration - 0.5f)
                alpha = Mathf.Clamp01((UnlockNotificationDuration - _unlockTimer) / 0.5f); // fade in first 0.5s

            // Gold banner at top-center
            float bannerW = 500f;
            float bannerH = 44f;
            float x = (Screen.width - bannerW) / 2f;
            float y = 110f;

            GUI.color = new Color(0.15f, 0.12f, 0.05f, 0.92f * alpha);
            GUI.DrawTexture(new Rect(x, y, bannerW, bannerH), Texture2D.whiteTexture);

            // Gold border
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(x, y, bannerW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + bannerH - 2, bannerW, 2), Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.2f, alpha) }
            };

            GUI.Label(new Rect(x, y, bannerW, bannerH), _unlockMessage, style);
            GUI.color = Color.white;
        }
    }
}

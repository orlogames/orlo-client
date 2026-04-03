using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Screen reader accessibility support. Maintains a queue of announcements
    /// and logs them with [SR] prefix for external screen reader tools.
    /// Key game events auto-announce for players using assistive technology.
    /// Future: integrate with platform TTS APIs.
    /// </summary>
    public class ScreenReaderHints : MonoBehaviour
    {
        public static ScreenReaderHints Instance { get; private set; }

        // Announcement queue (newest first, capped)
        private readonly Queue<string> _announcements = new();
        private const int MaxQueueSize = 50;

        // Rate limiting to avoid spam
        private float _lastAnnounceTime;
        private const float MinInterval = 0.3f;
        private readonly Queue<string> _pending = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Drain pending queue with rate limiting
            if (_pending.Count > 0 && Time.unscaledTime - _lastAnnounceTime >= MinInterval)
            {
                string text = _pending.Dequeue();
                EmitAnnouncement(text);
            }
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Queue an announcement for screen reader output.
        /// Logged with [SR] prefix for external tools to capture.
        /// </summary>
        public static void Announce(string text)
        {
            if (Instance == null || string.IsNullOrEmpty(text)) return;
            Instance._pending.Enqueue(text);
        }

        /// <summary>
        /// Immediate high-priority announcement (skips rate limiter).
        /// Use for critical events like death, disconnect, etc.
        /// </summary>
        public static void AnnounceImmediate(string text)
        {
            if (Instance == null || string.IsNullOrEmpty(text)) return;
            Instance.EmitAnnouncement(text);
        }

        /// <summary>
        /// Get the most recent announcements (newest first).
        /// UI can display these as a text log.
        /// </summary>
        public static string[] GetRecentAnnouncements(int count = 10)
        {
            if (Instance == null) return new string[0];
            var arr = Instance._announcements.ToArray();
            int len = Mathf.Min(count, arr.Length);
            var result = new string[len];
            // Queue is FIFO, so reverse for newest-first
            for (int i = 0; i < len; i++)
                result[i] = arr[arr.Length - 1 - i];
            return result;
        }

        // ── Auto-Announce Helpers ───────────────────────────────────────
        // Called by game systems to announce key events.

        /// <summary>Announce level up.</summary>
        public static void AnnounceLevelUp(int newLevel)
        {
            Announce($"Level up! You are now level {newLevel}.");
        }

        /// <summary>Announce quest completion.</summary>
        public static void AnnounceQuestComplete(string questName)
        {
            Announce($"Quest complete: {questName}.");
        }

        /// <summary>Announce item received.</summary>
        public static void AnnounceItemReceived(string itemName, int quantity)
        {
            if (quantity > 1)
                Announce($"Received {quantity}x {itemName}.");
            else
                Announce($"Received {itemName}.");
        }

        /// <summary>Announce combat state change.</summary>
        public static void AnnounceCombatState(string state)
        {
            Announce($"Combat: {state}.");
        }

        /// <summary>Announce player death.</summary>
        public static void AnnounceDeath()
        {
            AnnounceImmediate("You have been defeated.");
        }

        /// <summary>Announce respawn.</summary>
        public static void AnnounceRespawn()
        {
            AnnounceImmediate("You have respawned.");
        }

        /// <summary>Announce connection state change.</summary>
        public static void AnnounceConnection(bool connected)
        {
            AnnounceImmediate(connected ? "Connected to server." : "Disconnected from server.");
        }

        /// <summary>Announce target acquired.</summary>
        public static void AnnounceTarget(string name, int level, bool hostile)
        {
            string type = hostile ? "hostile" : "friendly";
            Announce($"Targeting {type}: {name}, level {level}.");
        }

        /// <summary>Announce area entered.</summary>
        public static void AnnounceArea(string areaName)
        {
            Announce($"Entering {areaName}.");
        }

        /// <summary>Announce damage taken.</summary>
        public static void AnnounceDamageTaken(float amount, string source)
        {
            if (!string.IsNullOrEmpty(source))
                Announce($"Took {amount:F0} damage from {source}.");
            else
                Announce($"Took {amount:F0} damage.");
        }

        /// <summary>Announce entity killed.</summary>
        public static void AnnounceKill(string entityName)
        {
            Announce($"Defeated {entityName}.");
        }

        // ── Internal ────────────────────────────────────────────────────

        private void EmitAnnouncement(string text)
        {
            _lastAnnounceTime = Time.unscaledTime;

            // Add to queue
            _announcements.Enqueue(text);
            while (_announcements.Count > MaxQueueSize)
                _announcements.Dequeue();

            // Log with [SR] prefix for external screen reader tools
            Debug.Log($"[SR] {text}");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// NPC quest dialog window — shows quest offers, in-progress status, and turn-in.
    /// Opened when interacting with quest-giving NPCs. Follows ShopUI visual style.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class QuestDialogUI : MonoBehaviour
    {
        public static QuestDialogUI Instance { get; private set; }

        private bool _visible;
        private string _npcName = "";
        private string _dialogue = "";
        private ulong _npcEntityId;

        // Quest data from server
        public struct QuestObjective
        {
            public string Description;
            public int Current, Required;
            public bool Complete;
        }

        public struct QuestRewardData
        {
            public uint XP;
            public uint SkillPoints;
            public long Credits;
            public List<string> ItemNames; // Display names for reward items
        }

        public enum QuestDialogState
        {
            Offer,       // New quest — Accept/Decline
            InProgress,  // Accepted but not complete — show objective progress
            ReadyToTurnIn // All objectives met — Turn In button
        }

        public struct QuestData
        {
            public string QuestId;
            public string Name;
            public string Description;
            public QuestDialogState State;
            public List<QuestObjective> Objectives;
            public QuestRewardData Rewards;
        }

        private QuestData _currentQuest;
        private string _statusMessage = "";
        private float _statusTimer;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─── Public API ─────────────────────────────────────────────────────

        /// <summary>Show the quest dialog with an NPC offering a quest.</summary>
        public void ShowQuestOffer(ulong npcEntityId, string npcName, string dialogue, QuestData quest)
        {
            _npcEntityId = npcEntityId;
            _npcName = npcName;
            _dialogue = dialogue;
            _currentQuest = quest;
            _currentQuest.State = QuestDialogState.Offer;
            _statusMessage = "";
            _visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Show quest in-progress status from NPC dialog.</summary>
        public void ShowQuestProgress(ulong npcEntityId, string npcName, string dialogue, QuestData quest)
        {
            _npcEntityId = npcEntityId;
            _npcName = npcName;
            _dialogue = dialogue;
            _currentQuest = quest;

            // Determine state from objectives
            bool allComplete = true;
            if (quest.Objectives != null)
            {
                foreach (var obj in quest.Objectives)
                {
                    if (!obj.Complete && obj.Current < obj.Required)
                    {
                        allComplete = false;
                        break;
                    }
                }
            }
            _currentQuest.State = allComplete ? QuestDialogState.ReadyToTurnIn : QuestDialogState.InProgress;
            _statusMessage = "";
            _visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Update quest objective progress while dialog is open.</summary>
        public void UpdateObjective(string questId, int objectiveIndex, int currentCount, int requiredCount)
        {
            if (!_visible || _currentQuest.QuestId != questId) return;
            if (_currentQuest.Objectives == null || objectiveIndex >= _currentQuest.Objectives.Count) return;

            var obj = _currentQuest.Objectives[objectiveIndex];
            obj.Current = currentCount;
            obj.Required = requiredCount;
            obj.Complete = currentCount >= requiredCount;
            _currentQuest.Objectives[objectiveIndex] = obj;

            // Check if all objectives now complete
            bool allDone = true;
            foreach (var o in _currentQuest.Objectives)
            {
                if (!o.Complete && o.Current < o.Required) { allDone = false; break; }
            }
            if (allDone && _currentQuest.State == QuestDialogState.InProgress)
                _currentQuest.State = QuestDialogState.ReadyToTurnIn;
        }

        /// <summary>Show turn-in result (success/failure message).</summary>
        public void ShowTurnInResult(bool success, string message)
        {
            if (success)
            {
                _statusMessage = message;
                _statusTimer = 3f;
                // Auto-close after brief delay
            }
            else
            {
                _statusMessage = $"Failed: {message}";
                _statusTimer = 5f;
            }
        }

        public void Hide()
        {
            _visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ─── Update / OnGUI ─────────────────────────────────────────────────

        private void Update()
        {
            if (_visible && Input.GetKeyDown(KeyCode.Escape))
                Hide();

            if (_statusTimer > 0)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0)
                    _statusMessage = "";
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            float w = 460, h = 480;
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);

            // Dark background
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(rect);
            GUILayout.Space(10);

            // NPC name header
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUILayout.Label(_npcName, titleStyle);

            // NPC dialogue
            var dialogueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter,
                fontSize = 12, wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.8f, 0.7f) }
            };
            GUILayout.Label($"\"{_dialogue}\"", dialogueStyle);
            GUILayout.Space(8);

            // Separator
            DrawSeparator(w - 20);
            GUILayout.Space(4);

            // Quest title
            var questTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };
            GUILayout.Label(_currentQuest.Name ?? "Unknown Quest", questTitleStyle);
            GUILayout.Space(2);

            // Quest description
            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            GUILayout.Label(_currentQuest.Description ?? "", descStyle, GUILayout.Height(50));
            GUILayout.Space(6);

            // Objectives
            if (_currentQuest.Objectives != null && _currentQuest.Objectives.Count > 0)
            {
                var objHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                GUILayout.Label("Objectives:", objHeaderStyle);
                GUILayout.Space(2);

                foreach (var obj in _currentQuest.Objectives)
                {
                    bool done = obj.Complete || obj.Current >= obj.Required;
                    string check = done ? "[x]" : "[ ]";
                    var objStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        normal = { textColor = done ? new Color(0.3f, 0.9f, 0.3f) : Color.white }
                    };

                    string progressText = obj.Required > 1
                        ? $"{check} {obj.Description} ({obj.Current}/{obj.Required})"
                        : $"{check} {obj.Description}";
                    GUILayout.Label(progressText, objStyle);

                    // Progress bar for multi-count objectives
                    if (obj.Required > 1 && !done)
                    {
                        Rect barRect = GUILayoutUtility.GetRect(w - 40, 6);
                        barRect.x += 20;
                        barRect.width -= 40;
                        GUI.color = new Color(0.2f, 0.2f, 0.2f);
                        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                        float pct = Mathf.Clamp01((float)obj.Current / obj.Required);
                        GUI.color = Color.Lerp(new Color(0.8f, 0.3f, 0.1f), new Color(0.3f, 0.9f, 0.3f), pct);
                        GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    GUILayout.Space(2);
                }
                GUILayout.Space(4);
            }

            // Rewards
            DrawSeparator(w - 20);
            GUILayout.Space(4);
            var rewardHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };
            GUILayout.Label("Rewards:", rewardHeaderStyle);
            GUILayout.Space(2);

            var rewardStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.7f) }
            };

            if (_currentQuest.Rewards.XP > 0)
                GUILayout.Label($"  + {_currentQuest.Rewards.XP} XP", rewardStyle);
            if (_currentQuest.Rewards.Credits > 0)
                GUILayout.Label($"  + {_currentQuest.Rewards.Credits:N0} Credits", rewardStyle);
            if (_currentQuest.Rewards.SkillPoints > 0)
                GUILayout.Label($"  + {_currentQuest.Rewards.SkillPoints} Skill Points", rewardStyle);
            if (_currentQuest.Rewards.ItemNames != null)
            {
                foreach (var itemName in _currentQuest.Rewards.ItemNames)
                    GUILayout.Label($"  + {itemName}", rewardStyle);
            }

            GUILayout.FlexibleSpace();

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = 12,
                    normal = { textColor = Color.green }
                };
                GUILayout.Label(_statusMessage, statusStyle);
                GUILayout.Space(4);
            }

            // Action buttons based on state
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            switch (_currentQuest.State)
            {
                case QuestDialogState.Offer:
                    if (GUILayout.Button("Accept", GUILayout.Width(100), GUILayout.Height(32)))
                    {
                        AcceptQuest();
                    }
                    GUILayout.Space(10);
                    if (GUILayout.Button("Decline", GUILayout.Width(100), GUILayout.Height(32)))
                    {
                        Hide();
                    }
                    break;

                case QuestDialogState.InProgress:
                    GUI.enabled = false;
                    GUILayout.Button("In Progress...", GUILayout.Width(120), GUILayout.Height(32));
                    GUI.enabled = true;
                    GUILayout.Space(10);
                    if (GUILayout.Button("Abandon", GUILayout.Width(100), GUILayout.Height(32)))
                    {
                        AbandonQuest();
                    }
                    break;

                case QuestDialogState.ReadyToTurnIn:
                    // Highlight the turn-in button
                    GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                    if (GUILayout.Button("Turn In", GUILayout.Width(120), GUILayout.Height(32)))
                    {
                        TurnInQuest();
                    }
                    GUI.backgroundColor = Color.white;
                    break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Close button
            if (GUILayout.Button("Close (Esc)", GUILayout.Height(24)))
                Hide();

            GUILayout.EndArea();
        }

        // ─── Actions ────────────────────────────────────────────────────────

        private void AcceptQuest()
        {
            if (string.IsNullOrEmpty(_currentQuest.QuestId)) return;
            Debug.Log($"[QuestDialog] Accepting quest: {_currentQuest.QuestId}");

            var data = PacketBuilder.QuestAccept(_currentQuest.QuestId);
            NetworkManager.Instance?.Send(data);

            _currentQuest.State = QuestDialogState.InProgress;
            _statusMessage = "Quest accepted!";
            _statusTimer = 2f;
        }

        private void TurnInQuest()
        {
            if (string.IsNullOrEmpty(_currentQuest.QuestId)) return;
            Debug.Log($"[QuestDialog] Turning in quest: {_currentQuest.QuestId}");

            var data = PacketBuilder.QuestTurnIn(_currentQuest.QuestId);
            NetworkManager.Instance?.Send(data);
        }

        private void AbandonQuest()
        {
            if (string.IsNullOrEmpty(_currentQuest.QuestId)) return;
            Debug.Log($"[QuestDialog] Abandoning quest: {_currentQuest.QuestId}");

            var data = PacketBuilder.QuestAbandon(_currentQuest.QuestId);
            NetworkManager.Instance?.Send(data);
            Hide();
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        private void DrawSeparator(float width)
        {
            Rect sep = GUILayoutUtility.GetRect(width, 1);
            sep.x += 10;
            sep.width = width;
            GUI.color = new Color(0.4f, 0.4f, 0.4f);
            GUI.DrawTexture(sep, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;
using Orlo.UI.TMD;

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

        private RacePalette P => TMDTheme.Instance?.Palette ?? RacePalette.Solari;

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

            // TMD glassmorphic panel
            TMDTheme.DrawPanel(rect);

            // We use absolute positioning within the panel instead of GUILayout for TMD consistency
            float cx = rect.x + 16;
            float cy = rect.y + 12;
            float pw = w - 32;

            // NPC name in race Primary
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = P.Primary }
            };
            GUI.Label(new Rect(cx, cy, pw, 24), _npcName, titleStyle);
            cy += 28;

            // NPC dialogue in TMD LabelStyle
            var dialogueStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            dialogueStyle.normal.textColor = P.TextDim;
            float dlgH = dialogueStyle.CalcHeight(new GUIContent($"\"{_dialogue}\""), pw);
            GUI.Label(new Rect(cx, cy, pw, dlgH), $"\"{_dialogue}\"", dialogueStyle);
            cy += dlgH + 8;

            // Separator
            GUI.color = new Color(P.Border.r, P.Border.g, P.Border.b, 0.5f);
            GUI.DrawTexture(new Rect(cx, cy, pw, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            cy += 6;

            // Quest title in race Accent
            var questTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                normal = { textColor = P.Accent }
            };
            GUI.Label(new Rect(cx, cy, pw, 22), _currentQuest.Name ?? "Unknown Quest", questTitleStyle);
            cy += 24;

            // Quest description
            var descStyle = new GUIStyle(TMDTheme.LabelStyle) { wordWrap = true };
            float descH = descStyle.CalcHeight(new GUIContent(_currentQuest.Description ?? ""), pw);
            descH = Mathf.Min(descH, 50);
            GUI.Label(new Rect(cx, cy, pw, descH), _currentQuest.Description ?? "", descStyle);
            cy += descH + 6;

            // Objectives with TMD checkmarks
            if (_currentQuest.Objectives != null && _currentQuest.Objectives.Count > 0)
            {
                var objHeaderStyle = new GUIStyle(TMDTheme.TitleStyle) { fontSize = 13 };
                GUI.Label(new Rect(cx, cy, pw, 18), "OBJECTIVES", objHeaderStyle);
                cy += 20;

                foreach (var obj in _currentQuest.Objectives)
                {
                    bool done = obj.Complete || obj.Current >= obj.Required;
                    string check = done ? "\u2713" : "\u25CB"; // TMD checkmark vs circle
                    Color objCol = done ? P.Success : P.Text;
                    var objStyle = new GUIStyle(TMDTheme.LabelStyle);
                    objStyle.normal.textColor = objCol;

                    string progressText = obj.Required > 1
                        ? $"{check}  {obj.Description} ({obj.Current}/{obj.Required})"
                        : $"{check}  {obj.Description}";
                    GUI.Label(new Rect(cx, cy, pw, 18), progressText, objStyle);
                    cy += 20;

                    // Progress bar for multi-count objectives via TMD
                    if (obj.Required > 1 && !done)
                    {
                        float pct = Mathf.Clamp01((float)obj.Current / obj.Required);
                        TMDTheme.DrawProgressBar(new Rect(cx + 20, cy, pw - 40, 6), pct, P.Primary);
                        cy += 10;
                    }
                }
                cy += 4;
            }

            // Rewards separator
            GUI.color = new Color(P.Border.r, P.Border.g, P.Border.b, 0.5f);
            GUI.DrawTexture(new Rect(cx, cy, pw, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            cy += 6;

            // Rewards header in race Accent
            var rewardHeaderStyle = new GUIStyle(TMDTheme.TitleStyle) { fontSize = 13 };
            rewardHeaderStyle.normal.textColor = P.Accent;
            GUI.Label(new Rect(cx, cy, pw, 18), "REWARDS", rewardHeaderStyle);
            cy += 20;

            var rewardStyle = new GUIStyle(TMDTheme.LabelStyle);
            rewardStyle.normal.textColor = P.Accent;

            if (_currentQuest.Rewards.XP > 0)
            { GUI.Label(new Rect(cx + 8, cy, pw - 8, 18), $"+ {_currentQuest.Rewards.XP} XP", rewardStyle); cy += 18; }
            if (_currentQuest.Rewards.Credits > 0)
            { GUI.Label(new Rect(cx + 8, cy, pw - 8, 18), $"+ {_currentQuest.Rewards.Credits:N0} Credits", rewardStyle); cy += 18; }
            if (_currentQuest.Rewards.SkillPoints > 0)
            { GUI.Label(new Rect(cx + 8, cy, pw - 8, 18), $"+ {_currentQuest.Rewards.SkillPoints} Skill Points", rewardStyle); cy += 18; }
            if (_currentQuest.Rewards.ItemNames != null)
            {
                foreach (var itemName in _currentQuest.Rewards.ItemNames)
                { GUI.Label(new Rect(cx + 8, cy, pw - 8, 18), $"+ {itemName}", rewardStyle); cy += 18; }
            }

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                cy = Mathf.Max(cy + 8, rect.yMax - 90);
                var statusStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                statusStyle.normal.textColor = P.Success;
                GUI.Label(new Rect(cx, cy, pw, 18), _statusMessage, statusStyle);
                cy += 22;
            }

            // Action buttons via TMDTheme.DrawButton — positioned at bottom
            float btnY = rect.yMax - 68;
            float btnCenterX = rect.x + w / 2f;

            switch (_currentQuest.State)
            {
                case QuestDialogState.Offer:
                    if (TMDTheme.DrawButton(new Rect(btnCenterX - 110, btnY, 100, 32), "Accept"))
                        AcceptQuest();
                    if (TMDTheme.DrawButton(new Rect(btnCenterX + 10, btnY, 100, 32), "Decline"))
                        Hide();
                    break;

                case QuestDialogState.InProgress:
                    GUI.enabled = false;
                    TMDTheme.DrawButton(new Rect(btnCenterX - 130, btnY, 120, 32), "In Progress...");
                    GUI.enabled = true;
                    if (TMDTheme.DrawButton(new Rect(btnCenterX + 10, btnY, 100, 32), "Abandon"))
                        AbandonQuest();
                    break;

                case QuestDialogState.ReadyToTurnIn:
                    if (TMDTheme.DrawButton(new Rect(btnCenterX - 60, btnY, 120, 32), "Turn In"))
                        TurnInQuest();
                    break;
            }

            // Close button
            if (TMDTheme.DrawButton(new Rect(cx, rect.yMax - 32, pw, 24), "Close (Esc)"))
                Hide();

            // Scanline overlay
            TMDTheme.DrawScanlines(rect);
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

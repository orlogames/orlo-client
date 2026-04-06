using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// Combat action bar for martial arts moves.
    /// Shows available moves, cooldowns, combo counter, and stance info.
    /// Mapped to number keys 1-0 (10 slots).
    /// </summary>
    public class CombatBarUI : MonoBehaviour
    {
        public static CombatBarUI Instance { get; private set; }

        public struct MoveSlot
        {
            public uint MoveId;
            public string Name, Description, Effect;
            public float DamageMultiplier, StaminaCost, Cooldown, CooldownRemaining;
            public uint RequiredRank;
            public bool IsFinisher;
        }

        private List<MoveSlot> _moves = new();
        private uint _activeStyle;
        private uint _unarmedRank;
        private uint _comboCount;
        private string _lastMoveResult = "";
        private float _resultTimer;

        private string[] _styleNames = { "Brawler", "Iron Fist", "Wind Step", "Void Palm" };

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetState(uint style, uint rank, uint combo, List<MoveSlot> moves)
        {
            _activeStyle = style;
            _unarmedRank = rank;
            _comboCount = combo;
            _moves = moves;
        }

        public void ShowMoveResult(string result)
        {
            _lastMoveResult = result;
            _resultTimer = 2.0f;
        }

        private void Update()
        {
            if (_resultTimer > 0) _resultTimer -= Time.deltaTime;

            // Number keys 1-9 and 0 trigger move slots 0-9
            for (int i = 0; i < Mathf.Min(_moves.Count, 10); i++)
            {
                KeyCode key = i < 9 ? KeyCode.Alpha1 + i : KeyCode.Alpha0;
                if (Input.GetKeyDown(key))
                {
                    ExecuteMove(_moves[i].MoveId);
                }
            }
        }

        private void ExecuteMove(uint moveId)
        {
            // Use the selected target from TargetingSystem, or 0 for auto-target
            ulong targetId = Player.TargetingSystem.Instance != null
                ? Player.TargetingSystem.Instance.TargetEntityId
                : 0;

            var data = PacketBuilder.MartialMove(targetId, moveId);
            NetworkManager.Instance.Send(data);

            if (targetId == 0)
                Debug.Log($"[Combat] MartialMove #{moveId} — auto-target (no selection)");
        }

        private void OnGUI()
        {
            if (_moves.Count == 0) return;

            // Combat bar at bottom center of screen — up to 10 slots
            int slotCount = Mathf.Min(_moves.Count, 10);
            float barWidth = slotCount * 72 + 10;
            float barHeight = 80;
            float barX = (Screen.width - barWidth) / 2;
            float barY = Screen.height - barHeight - 10;

            GUI.Box(new Rect(barX - 5, barY - 25, barWidth + 10, barHeight + 30), "");

            // Style + rank header
            string styleName = _activeStyle < _styleNames.Length ? _styleNames[_activeStyle] : "Unknown";
            GUI.Label(new Rect(barX, barY - 22, barWidth, 20),
                $"{styleName} (Rank {_unarmedRank})" +
                (_comboCount > 1 ? $"  COMBO x{_comboCount}!" : ""));

            // Move buttons
            for (int i = 0; i < slotCount; i++)
            {
                var move = _moves[i];
                float bx = barX + i * 72;
                var btnRect = new Rect(bx, barY, 68, 55);

                bool onCooldown = move.CooldownRemaining > 0;
                GUI.enabled = !onCooldown;

                // Key label: 1-9 then 0 for slot 10
                string keyLabel = i < 9 ? (i + 1).ToString() : "0";
                string label = $"[{keyLabel}]\n{move.Name}";
                if (onCooldown)
                    label += $"\n{move.CooldownRemaining:F1}s";

                if (GUI.Button(btnRect, label))
                {
                    ExecuteMove(move.MoveId);
                }
                GUI.enabled = true;

                // Stamina cost below
                GUI.Label(new Rect(bx, barY + 57, 68, 16),
                    $"{move.StaminaCost:F0} stam");

                // Finisher indicator
                if (move.IsFinisher)
                {
                    var oldColor = GUI.color;
                    GUI.color = Color.yellow;
                    GUI.Label(new Rect(bx + 48, barY - 2, 20, 16), "F");
                    GUI.color = oldColor;
                }
            }

            // Move result feedback
            if (_resultTimer > 0 && !string.IsNullOrEmpty(_lastMoveResult))
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };
                GUI.Label(new Rect(barX, barY - 50, barWidth, 25), _lastMoveResult, style);
            }
        }
    }
}

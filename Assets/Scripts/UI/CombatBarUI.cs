using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// Combat action bar for martial arts moves — TMD holographic slot design.
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

        // Hover tracking for holographic slot glow
        private int _hoveredSlot = -1;

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
            if (!GameBootstrap.InWorld) return;

            var p = TMDTheme.Instance?.Palette ?? RacePalette.Solari;
            float s = UIScaler.Scale;

            int slotCount = Mathf.Min(_moves.Count, 10);
            float slotW = 68f * s;
            float slotH = 55f * s;
            float slotGap = 4f * s;
            float barWidth = slotCount * (slotW + slotGap) - slotGap;
            float barX = (Screen.width - barWidth) / 2f;
            float barY = Screen.height - slotH - 40f * s;
            float panelPad = 6f * s;

            // Panel background behind the entire action bar
            var panelRect = new Rect(
                barX - panelPad,
                barY - 24f * s - panelPad,
                barWidth + panelPad * 2,
                slotH + 24f * s + 20f * s + panelPad * 2
            );
            TMDTheme.DrawPanel(panelRect);

            // Style + rank header
            string styleName = _activeStyle < _styleNames.Length ? _styleNames[_activeStyle] : "Unknown";
            string header = $"{styleName}  Rank {_unarmedRank}";
            if (_comboCount > 1)
                header += $"   COMBO x{_comboCount}!";

            var headerStyle = new GUIStyle(TMDTheme.TitleStyle)
            {
                fontSize = UIScaler.ScaledFontSize(12),
                alignment = TextAnchor.MiddleCenter
            };
            // Combo count gets race-glow color
            if (_comboCount > 1)
                headerStyle.normal.textColor = p.Glow;

            GUI.Label(new Rect(barX, barY - 22f * s, barWidth, 20f * s), header, headerStyle);

            // Move slots — holographic frames
            _hoveredSlot = -1;
            for (int i = 0; i < slotCount; i++)
            {
                var move = _moves[i];
                float bx = barX + i * (slotW + slotGap);
                var slotRect = new Rect(bx, barY, slotW, slotH);
                bool onCooldown = move.CooldownRemaining > 0;
                bool hovered = slotRect.Contains(Event.current.mousePosition);
                if (hovered) _hoveredSlot = i;

                // Slot background
                Color slotBg = hovered
                    ? new Color(p.Primary.r, p.Primary.g, p.Primary.b, 0.2f)
                    : new Color(p.Background.r, p.Background.g, p.Background.b, 0.7f);
                GUI.color = slotBg;
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

                // Cooldown overlay (dark sweep)
                if (onCooldown && move.Cooldown > 0)
                {
                    float cdFill = move.CooldownRemaining / move.Cooldown;
                    GUI.color = new Color(0, 0, 0, 0.5f);
                    GUI.DrawTexture(new Rect(bx, barY, slotW, slotH * cdFill), Texture2D.whiteTexture);
                }

                // Slot border (race-colored, brighter on hover/finisher)
                Color borderColor = hovered ? p.Primary :
                    (move.IsFinisher ? p.Secondary : p.Border);
                GUI.color = borderColor;
                GUI.DrawTexture(new Rect(bx, barY, slotW, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(bx, barY + slotH - 1, slotW, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(bx, barY, 1, slotH), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(bx + slotW - 1, barY, 1, slotH), Texture2D.whiteTexture);

                // Key label (top-left corner)
                string keyLabel = i < 9 ? (i + 1).ToString() : "0";
                var keyStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    fontSize = UIScaler.ScaledFontSize(9),
                    alignment = TextAnchor.UpperLeft
                };
                keyStyle.normal.textColor = p.TextDim;
                GUI.Label(new Rect(bx + 3, barY + 2, 16, 14), keyLabel, keyStyle);

                // Move name (centered)
                var nameStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    fontSize = UIScaler.ScaledFontSize(10),
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                nameStyle.normal.textColor = onCooldown ? p.TextDim : p.Text;
                string moveLabel = move.Name;
                if (onCooldown)
                    moveLabel += $"\n{move.CooldownRemaining:F1}s";
                GUI.Label(new Rect(bx + 2, barY + 12, slotW - 4, slotH - 14), moveLabel, nameStyle);

                // Finisher indicator (race accent color, top-right)
                if (move.IsFinisher)
                {
                    var finStyle = new GUIStyle(TMDTheme.LabelStyle)
                    {
                        fontSize = UIScaler.ScaledFontSize(9),
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperRight
                    };
                    finStyle.normal.textColor = p.Secondary;
                    GUI.Label(new Rect(bx, barY + 2, slotW - 3, 14), "F", finStyle);
                }

                // Stamina cost (below slot)
                var costStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    fontSize = UIScaler.ScaledFontSize(9),
                    alignment = TextAnchor.MiddleCenter
                };
                costStyle.normal.textColor = p.TextDim;
                GUI.Label(new Rect(bx, barY + slotH + 2, slotW, 14f * s), $"{move.StaminaCost:F0} stam", costStyle);

                // Invisible click button
                GUI.color = Color.white;
                GUI.enabled = !onCooldown;
                if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none))
                    ExecuteMove(move.MoveId);
                GUI.enabled = true;
            }

            // Move result feedback (above action bar, race glow color)
            if (_resultTimer > 0 && !string.IsNullOrEmpty(_lastMoveResult))
            {
                var resultStyle = new GUIStyle(TMDTheme.TitleStyle)
                {
                    fontSize = UIScaler.ScaledFontSize(16),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                resultStyle.normal.textColor = p.Glow;
                float alpha = Mathf.Clamp01(_resultTimer);
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(new Rect(barX, barY - 50f * s, barWidth, 25), _lastMoveResult, resultStyle);
                GUI.color = Color.white;
            }

            // Scanline overlay
            TMDTheme.DrawScanlines(panelRect);
        }
    }
}

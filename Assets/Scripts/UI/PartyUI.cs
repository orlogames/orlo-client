using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Party frame showing members, HP bars, invite/leave controls.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class PartyUI : MonoBehaviour
    {
        private const float WindowW = 180f;
        private const float WindowH = 200f;

        private bool _showInviteInput;
        private string _inviteName = "";

        // Placeholder party data — replace with real PartyData reference
        [System.Serializable]
        public struct PartyMember
        {
            public string Name;
            public float CurrentHP;
            public float MaxHP;
            public bool IsLeader;
        }

        private List<PartyMember> _members = new List<PartyMember>();
        private bool _inParty;

        private void Awake()
        {
            // Seed test party data
            _inParty = true;
            _members.Add(new PartyMember { Name = "You", CurrentHP = 85, MaxHP = 100, IsLeader = true });
            _members.Add(new PartyMember { Name = "Ranger_42", CurrentHP = 60, MaxHP = 80, IsLeader = false });
            _members.Add(new PartyMember { Name = "HealBot", CurrentHP = 45, MaxHP = 120, IsLeader = false });
        }

        /// <summary>Check external PartyData if available; for now uses internal state.</summary>
        public void SetPartyData(List<PartyMember> members)
        {
            _members = members;
            _inParty = members != null && members.Count > 0;
        }

        private void OnGUI()
        {
            if (!_inParty || _members.Count == 0) return;

            // Position: top-left, below compass area (~80px from top)
            float x = 10f;
            float y = 80f;

            // Calculate dynamic height
            float memberAreaH = _members.Count * 36f;
            float buttonsH = 30f;
            float inviteH = _showInviteInput ? 26f : 0f;
            float totalH = 24f + memberAreaH + buttonsH + inviteH + 12f;
            totalH = Mathf.Min(totalH, WindowH);

            Rect windowRect = new Rect(x, y, WindowW, totalH);

            // Background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(x, y, WindowW, 22);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            GUI.DrawTexture(titleBar, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(titleBar, $"  Party ({_members.Count})", BoldLabel());

            float cy = y + 26;

            // Member list
            for (int i = 0; i < _members.Count; i++)
            {
                PartyMember m = _members[i];
                float mx = x + 6;
                float my = cy;

                // Name with leader prefix
                string displayName = m.IsLeader ? $"[L] {m.Name}" : m.Name;
                GUI.color = m.IsLeader ? Color.yellow : Color.white;
                GUI.Label(new Rect(mx, my, WindowW - 12, 16), displayName, SmallLabel());
                GUI.color = Color.white;

                // HP bar background
                float barY = my + 16;
                float barW = WindowW - 16;
                float barH = 12f;
                Rect barBg = new Rect(mx, barY, barW, barH);
                GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                GUI.DrawTexture(barBg, Texture2D.whiteTexture);

                // HP bar fill
                float hpPct = m.MaxHP > 0 ? Mathf.Clamp01(m.CurrentHP / m.MaxHP) : 0f;
                Color hpColor = hpPct > 0.5f ? Color.green : (hpPct > 0.25f ? Color.yellow : Color.red);
                GUI.color = hpColor;
                GUI.DrawTexture(new Rect(mx, barY, barW * hpPct, barH), Texture2D.whiteTexture);

                // HP text
                GUI.color = Color.white;
                GUI.Label(barBg, $"{(int)m.CurrentHP}/{(int)m.MaxHP}", SmallLabelCentered());

                cy += 36;
            }

            cy += 4;

            // Buttons row
            float btnW = 74f;
            float btnH = 22f;

            if (GUI.Button(new Rect(x + 6, cy, btnW, btnH), "Invite"))
            {
                _showInviteInput = !_showInviteInput;
                _inviteName = "";
            }

            if (GUI.Button(new Rect(x + 6 + btnW + 6, cy, btnW, btnH), "Leave"))
            {
                Debug.Log("[PartyUI] Left party.");
                _members.Clear();
                _inParty = false;
            }

            cy += btnH + 4;

            // Invite input
            if (_showInviteInput)
            {
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                GUI.DrawTexture(new Rect(x + 6, cy, WindowW - 16, 22), Texture2D.whiteTexture);
                GUI.color = Color.white;

                _inviteName = GUI.TextField(new Rect(x + 8, cy + 1, WindowW - 60, 20), _inviteName, SmallInputStyle());

                if (GUI.Button(new Rect(x + WindowW - 48, cy, 40, 22), "OK") && !string.IsNullOrEmpty(_inviteName))
                {
                    Debug.Log($"[PartyUI] Invited: {_inviteName}");
                    _showInviteInput = false;
                    _inviteName = "";
                }
            }

            GUI.color = Color.white;
        }

        private GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white }, wordWrap = false };
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private GUIStyle SmallInputStyle()
        {
            return new GUIStyle(GUI.skin.textField)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                focused = { textColor = Color.white }
            };
        }
    }
}

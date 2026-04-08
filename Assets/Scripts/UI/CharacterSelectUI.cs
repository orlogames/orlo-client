using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Character selection / lobby screen shown after login.
    /// Three-panel layout: character list (left), 3D preview (center), info (right).
    /// Pre-game social sidebar, server status, delete with confirmation.
    /// Uses OnGUI immediate mode rendering.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        public Action<ulong, string> OnCharacterSelected;
        public Action OnCreateNew;

        private bool _visible = true;
        private int _selectedIndex = 0;
        private List<CharacterEntry> _characters = new();
        private int _maxCharacters = 4;
        private Vector2 _listScroll;
        private Vector2 _socialScroll;

        // Delete state
        private bool _deleteConfirmActive;
        private string _deleteConfirmInput = "";
        private int _deleteTargetIndex = -1;
        private float _deleteCooldownEnd;
        private bool _deletePending;

        // Server status
        private bool _serverOnline = true;
        private int _playerCount;
        private string _serverStatusText = "Online";
        private Color _serverStatusColor = Color.green;

        // Pre-game social sidebar
        private bool _socialExpanded = true;

        // Friends in sidebar
        public struct LobbyFriend
        {
            public string Name;
            public bool Online;
            public string Zone;
        }
        private List<LobbyFriend> _lobbyFriends = new List<LobbyFriend>();
        private string _guildChatInput = "";
        private List<string> _guildChatMessages = new List<string>();

        // Party invites
        public struct PartyInvite
        {
            public string FromPlayer;
            public float ReceivedTime;
        }
        private List<PartyInvite> _partyInvites = new List<PartyInvite>();

        // Race names
        private static readonly string[] RaceNames = { "Solari", "Vael", "Korrath", "Thyren" };
        private static readonly string[] ClassNames = { "Explorer", "Warrior", "Artisan", "Medic", "Ranger", "Pilot" };

        public struct CharacterEntry
        {
            public ulong id;
            public string firstName;
            public string lastName;
            public int level;
            public string zoneName;
            public int race;
            public int classId;
            public float playtimeHours;
            public long credits;
            public string guildName;
            public string factionName;
            public int criminalRating;
            public string lastPlayedRelative;
            public bool pendingDelete;
            public float deleteTimeRemaining;
        }

        public void Show() { _visible = true; _selectedIndex = 0; _deleteConfirmActive = false; }
        public void Hide() { _visible = false; }

        public void SetCharacters(List<CharacterEntry> characters, int maxSlots)
        {
            _characters = characters;
            _maxCharacters = maxSlots;
            _selectedIndex = _characters.Count > 0 ? 0 : -1;
        }

        public void SetServerStatus(bool online, int playerCount, string statusText)
        {
            _serverOnline = online;
            _playerCount = playerCount;
            _serverStatusText = statusText;
            _serverStatusColor = online ? Color.green : Color.red;
        }

        public void SetLobbyFriends(List<LobbyFriend> friends) { _lobbyFriends = friends ?? new List<LobbyFriend>(); }
        public void AddGuildChatMessage(string msg) { _guildChatMessages.Add(msg); if (_guildChatMessages.Count > 50) _guildChatMessages.RemoveAt(0); }
        public void AddPartyInvite(string from) { _partyInvites.Add(new PartyInvite { FromPlayer = from, ReceivedTime = Time.time }); }

        public void OnDeleteStatus(ulong characterId, bool success, float cooldownRemaining)
        {
            if (success && cooldownRemaining > 0)
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    if (_characters[i].id == characterId)
                    {
                        var ch = _characters[i];
                        ch.pendingDelete = true;
                        ch.deleteTimeRemaining = cooldownRemaining;
                        _characters[i] = ch;
                        break;
                    }
                }
            }
            _deleteConfirmActive = false;
        }

        private void Update()
        {
            // Tick delete cooldowns
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].pendingDelete && _characters[i].deleteTimeRemaining > 0)
                {
                    var ch = _characters[i];
                    ch.deleteTimeRemaining -= Time.deltaTime;
                    _characters[i] = ch;
                }
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // Full-screen dark background
            GUI.color = new Color(0.04f, 0.03f, 0.07f, 0.98f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(0, 20, Screen.width, 40), "ORLO", titleStyle);

            // Server status (top right)
            DrawServerStatus();

            // Top right buttons
            float btnSize = 28;
            if (GUI.Button(new Rect(Screen.width - 70, 10, 60, btnSize), "Exit"))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            // Main layout
            float topPad = 75;
            float bottomPad = 70;
            float mainH = Screen.height - topPad - bottomPad;
            float totalW = Mathf.Min(Screen.width * 0.92f, 1400f);
            float startX = (Screen.width - totalW) / 2f;

            float leftW = totalW * 0.25f;
            float rightW = totalW * 0.25f;
            float centerW = totalW - leftW - rightW - 20;

            // Left panel: character list
            DrawCharacterList(new Rect(startX, topPad, leftW, mainH));

            // Center: 3D preview area (placeholder)
            DrawPreviewArea(new Rect(startX + leftW + 10, topPad, centerW, mainH));

            // Right panel: character info
            if (_selectedIndex >= 0 && _selectedIndex < _characters.Count)
                DrawCharacterInfo(new Rect(startX + leftW + centerW + 20, topPad, rightW, mainH));

            // Bottom bar
            DrawBottomBar(startX, totalW);

            // Pre-game social sidebar (collapsible, right edge)
            DrawSocialSidebar();

            // Delete confirmation dialog
            if (_deleteConfirmActive)
                DrawDeleteConfirm();
        }

        private void DrawServerStatus()
        {
            float sx = Screen.width - 240;
            float sy = 14;

            // Status dot
            GUI.color = _serverStatusColor;
            GUI.DrawTexture(new Rect(sx, sy + 4, 10, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.7f, 0.7f, 0.8f) } };
            GUI.Label(new Rect(sx + 14, sy, 160, 20), $"{_serverStatusText} ({_playerCount} players)", statusStyle);
        }

        private void DrawCharacterList(Rect area)
        {
            GUI.color = new Color(0.07f, 0.06f, 0.1f, 0.9f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = area.y + 10;
            float padX = 12;

            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.8f, 0.85f, 0.95f) } };
            GUI.Label(new Rect(area.x + padX, y, area.width - padX * 2, 24), $"Characters ({_characters.Count}/{_maxCharacters})", headerStyle);
            y += 32;

            float listH = area.height - 80;
            float totalH = (_characters.Count + (_characters.Count < _maxCharacters ? 1 : 0)) * 76f;
            _listScroll = GUI.BeginScrollView(new Rect(area.x, y, area.width, listH), _listScroll,
                new Rect(0, 0, area.width - 16, Mathf.Max(totalH, listH)));

            float ly = 0;
            for (int i = 0; i < _characters.Count; i++)
            {
                bool selected = i == _selectedIndex;
                DrawCharacterSlot(new Rect(padX, ly, area.width - padX * 2 - 16, 70), _characters[i], selected, i);
                ly += 76;
            }

            // Empty slots
            for (int i = _characters.Count; i < _maxCharacters; i++)
            {
                Rect slotRect = new Rect(padX, ly, area.width - padX * 2 - 16, 70);
                GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.3f, 0.3f, 0.4f);
                var emptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, normal = { textColor = new Color(0.4f, 0.4f, 0.5f) } };
                GUI.Label(slotRect, "Empty Slot", emptyStyle);
                GUI.color = Color.white;
                ly += 76;
            }

            GUI.EndScrollView();
        }

        private void DrawCharacterSlot(Rect area, CharacterEntry ch, bool selected, int index)
        {
            Color bgColor = selected
                ? new Color(0.15f, 0.2f, 0.35f, 0.9f)
                : new Color(0.09f, 0.09f, 0.13f, 0.7f);

            if (ch.pendingDelete)
                bgColor = new Color(0.3f, 0.1f, 0.1f, 0.7f);

            GUI.color = bgColor;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Selection border
            if (selected)
            {
                GUI.color = new Color(0.4f, 0.6f, 1f, 0.8f);
                DrawBorder(area, 2);
                GUI.color = Color.white;
            }

            // Click to select
            if (Event.current.type == EventType.MouseDown && area.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                Event.current.Use();
            }

            // Double-click to enter
            if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 &&
                area.Contains(Event.current.mousePosition) && selected && !ch.pendingDelete)
            {
                string fullName = $"{ch.firstName} {ch.lastName}";
                OnCharacterSelected?.Invoke(ch.id, fullName);
                Hide();
                Event.current.Use();
            }

            float x = area.x + 10;
            float y = area.y + 6;

            // Name
            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = selected ? new Color(0.9f, 0.95f, 1f) : new Color(0.7f, 0.75f, 0.85f) } };
            GUI.Label(new Rect(x, y, area.width - 20, 20), $"{ch.firstName} {ch.lastName}", nameStyle);

            // Level + class + race
            string raceName = ch.race >= 0 && ch.race < RaceNames.Length ? RaceNames[ch.race] : "Unknown";
            string className = ch.classId >= 0 && ch.classId < ClassNames.Length ? ClassNames[ch.classId] : "";
            var detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.5f, 0.55f, 0.65f) } };
            string detail = !string.IsNullOrEmpty(className) ? $"L{ch.level} {className} {raceName}" : $"L{ch.level} {raceName}";
            GUI.Label(new Rect(x, y + 20, area.width - 20, 16), detail, detailStyle);

            // Last location + last played
            string zone = string.IsNullOrEmpty(ch.zoneName) ? "Threshold" : ch.zoneName;
            string lastPlayed = string.IsNullOrEmpty(ch.lastPlayedRelative) ? "" : $" | {ch.lastPlayedRelative}";
            GUI.Label(new Rect(x, y + 36, area.width - 20, 16), $"{zone}{lastPlayed}", detailStyle);

            // Pending delete indicator
            if (ch.pendingDelete)
            {
                float remaining = ch.deleteTimeRemaining;
                int hours = (int)(remaining / 3600);
                int mins = (int)((remaining % 3600) / 60);
                var delStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(1f, 0.4f, 0.3f) } };
                GUI.Label(new Rect(x, y + 50, 200, 14), $"Deleting... ({hours}h {mins}m)", delStyle);

                // Cancel button
                if (GUI.Button(new Rect(area.x + area.width - 60, area.y + area.height - 22, 50, 18), "Cancel"))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.CancelCharacterDelete(ch.id));
                }
            }
        }

        private void DrawPreviewArea(Rect area)
        {
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.6f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Placeholder text (CharacterPreviewManager renders the 3D model here)
            if (_selectedIndex < 0 || _selectedIndex >= _characters.Count)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.3f, 0.3f, 0.4f) } };
                GUI.Label(area, "Select a character", style);
            }
            else
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.LowerCenter, normal = { textColor = new Color(0.4f, 0.4f, 0.5f) } };
                GUI.Label(new Rect(area.x, area.y + area.height - 24, area.width, 20), "Click and drag to rotate", style);
            }
        }

        private void DrawCharacterInfo(Rect area)
        {
            GUI.color = new Color(0.07f, 0.06f, 0.1f, 0.9f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var ch = _characters[_selectedIndex];
            float y = area.y + 12;
            float x = area.x + 12;
            float w = area.width - 24;

            // Full name header
            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.9f, 0.85f, 0.5f) } };
            GUI.Label(new Rect(x, y, w, 22), $"{ch.firstName} {ch.lastName}", nameStyle);
            y += 28;

            // Info rows
            string raceName = ch.race >= 0 && ch.race < RaceNames.Length ? RaceNames[ch.race] : "Unknown";
            string className = ch.classId >= 0 && ch.classId < ClassNames.Length ? ClassNames[ch.classId] : "Explorer";

            DrawInfoRow(x, ref y, w, "Level", ch.level.ToString());
            DrawInfoRow(x, ref y, w, "Class", className);
            DrawInfoRow(x, ref y, w, "Race", raceName);
            DrawInfoRow(x, ref y, w, "Last Location", string.IsNullOrEmpty(ch.zoneName) ? "Threshold" : ch.zoneName);

            int hours = (int)ch.playtimeHours;
            int mins = (int)((ch.playtimeHours - hours) * 60);
            DrawInfoRow(x, ref y, w, "Play Time", $"{hours}h {mins}m");

            var credStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            GUI.Label(new Rect(x, y, 80, 18), "Credits", DimStyle());
            GUI.Label(new Rect(x + 84, y, w - 84, 18), $"{ch.credits:N0}", credStyle);
            y += 20;

            if (!string.IsNullOrEmpty(ch.guildName))
                DrawInfoRow(x, ref y, w, "Guild", ch.guildName);
            if (!string.IsNullOrEmpty(ch.factionName))
                DrawInfoRow(x, ref y, w, "Faction", ch.factionName);

            if (ch.criminalRating > 0)
            {
                string[] crLabels = { "", "Suspect", "Criminal", "Notorious" };
                string crLabel = ch.criminalRating < crLabels.Length ? crLabels[ch.criminalRating] : $"Rating {ch.criminalRating}";
                var crStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.3f, 0.2f) } };
                GUI.Label(new Rect(x, y, 80, 18), "Criminal", DimStyle());
                GUI.Label(new Rect(x + 84, y, w - 84, 18), crLabel, crStyle);
                y += 20;
            }
        }

        private void DrawInfoRow(float x, ref float y, float w, string label, string value)
        {
            GUI.Label(new Rect(x, y, 80, 18), label, DimStyle());
            GUI.Label(new Rect(x + 84, y, w - 84, 18), value, SmallStyle());
            y += 20;
        }

        private void DrawBottomBar(float startX, float totalW)
        {
            float barY = Screen.height - 60;
            float btnH = 40;

            // Delete button
            GUI.enabled = _selectedIndex >= 0 && _selectedIndex < _characters.Count;
            GUI.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
            if (GUI.Button(new Rect(startX, barY, 100, btnH), "Delete"))
            {
                _deleteConfirmActive = true;
                _deleteTargetIndex = _selectedIndex;
                _deleteConfirmInput = "";
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // Create character
            GUI.enabled = _characters.Count < _maxCharacters;
            GUI.backgroundColor = new Color(0.3f, 0.4f, 0.8f);
            if (GUI.Button(new Rect(startX + totalW / 2f - 80, barY, 160, btnH), "Create Character"))
            {
                OnCreateNew?.Invoke();
                Hide();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // Enter world
            bool canEnter = _selectedIndex >= 0 && _selectedIndex < _characters.Count && !_characters[_selectedIndex].pendingDelete;
            GUI.enabled = canEnter;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            var enterStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(startX + totalW - 180, barY, 180, btnH), "ENTER WORLD", enterStyle))
            {
                var ch = _characters[_selectedIndex];
                OnCharacterSelected?.Invoke(ch.id, $"{ch.firstName} {ch.lastName}");
                Hide();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void DrawDeleteConfirm()
        {
            // Modal overlay
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float dlgW = 350;
            float dlgH = 180;
            float dlgX = (Screen.width - dlgW) / 2f;
            float dlgY = (Screen.height - dlgH) / 2f;

            GUI.color = new Color(0.1f, 0.08f, 0.14f, 0.98f);
            GUI.DrawTexture(new Rect(dlgX, dlgY, dlgW, dlgH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Border
            GUI.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            DrawBorder(new Rect(dlgX, dlgY, dlgW, dlgH), 2);
            GUI.color = Color.white;

            if (_deleteTargetIndex < 0 || _deleteTargetIndex >= _characters.Count)
            {
                _deleteConfirmActive = false;
                return;
            }

            var ch = _characters[_deleteTargetIndex];
            string charName = $"{ch.firstName} {ch.lastName}";

            float y = dlgY + 12;
            var warnStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.4f, 0.3f) } };
            GUI.Label(new Rect(dlgX, y, dlgW, 20), "Delete Character?", warnStyle);
            y += 26;

            var msgStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            string msg = ch.level >= 10
                ? $"Type \"{charName}\" to confirm.\nLevel 10+ characters have a 24h deletion cooldown."
                : $"Type \"{charName}\" to confirm.\nThis character will be immediately deleted.";
            GUI.Label(new Rect(dlgX + 20, y, dlgW - 40, 40), msg, msgStyle);
            y += 48;

            _deleteConfirmInput = GUI.TextField(new Rect(dlgX + 20, y, dlgW - 40, 22), _deleteConfirmInput,
                new GUIStyle(GUI.skin.textField) { fontSize = 12, normal = { textColor = Color.white }, focused = { textColor = Color.white } });
            y += 30;

            // Buttons
            bool nameMatches = _deleteConfirmInput.Trim() == charName;
            GUI.enabled = nameMatches;
            GUI.backgroundColor = new Color(0.6f, 0.15f, 0.15f);
            if (GUI.Button(new Rect(dlgX + 20, y, 100, 30), "Delete"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.CharacterDeleteExtended(ch.id));
                _deleteConfirmActive = false;
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            if (GUI.Button(new Rect(dlgX + dlgW - 120, y, 100, 30), "Cancel"))
                _deleteConfirmActive = false;
        }

        private void DrawSocialSidebar()
        {
            float sideW = _socialExpanded ? 200f : 30f;
            float sideX = Screen.width - sideW;
            float sideY = 50;
            float sideH = Screen.height - 120;

            // Toggle button
            if (GUI.Button(new Rect(sideX, sideY, 28, 28), _socialExpanded ? ">>" : "<<"))
                _socialExpanded = !_socialExpanded;

            if (!_socialExpanded) return;

            GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(sideX, sideY + 30, sideW, sideH - 30), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = sideY + 34;
            float x = sideX + 6;
            float w = sideW - 12;

            // Friends section
            GUI.Label(new Rect(x, y, w, 16), "Friends", SectionStyle());
            y += 18;

            int displayed = 0;
            foreach (var f in _lobbyFriends)
            {
                if (displayed >= 8) break;
                GUI.color = f.Online ? Color.green : new Color(0.4f, 0.4f, 0.4f);
                GUI.DrawTexture(new Rect(x, y + 3, 6, 6), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(x + 10, y, 100, 14), f.Name, new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white } });

                if (f.Online)
                {
                    if (GUI.Button(new Rect(x + w - 20, y, 18, 14), "W"))
                    {
                        // Open whisper
                        ChatUI.Instance?.ReceiveMessage("System", "System", $"/w {f.Name} ");
                    }
                }
                y += 16;
                displayed++;
            }

            y += 8;

            // Party invites
            if (_partyInvites.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, 16), "Party Invites", SectionStyle());
                y += 18;

                for (int i = _partyInvites.Count - 1; i >= 0; i--)
                {
                    var inv = _partyInvites[i];
                    GUI.Label(new Rect(x, y, 80, 14), inv.FromPlayer, new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white } });
                    if (GUI.Button(new Rect(x + 84, y, 30, 14), "Yes"))
                    {
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.PartyAccept(inv.FromPlayer));
                        _partyInvites.RemoveAt(i);
                    }
                    if (GUI.Button(new Rect(x + 118, y, 30, 14), "No"))
                        _partyInvites.RemoveAt(i);
                    y += 16;
                }
                y += 8;
            }

            // Guild chat (if in guild)
            if (_guildChatMessages.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, 16), "Guild Chat", SectionStyle());
                y += 18;

                float chatH = Mathf.Min(sideH - (y - sideY) - 30, 120);
                float totalH = _guildChatMessages.Count * 14f;
                _socialScroll = GUI.BeginScrollView(new Rect(x, y, w, chatH), _socialScroll,
                    new Rect(0, 0, w - 12, Mathf.Max(totalH, chatH)));

                float ly = 0;
                foreach (var msg in _guildChatMessages)
                {
                    GUI.Label(new Rect(0, ly, w - 12, 12), msg, new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = new Color(0, 0.8f, 0.6f) }, wordWrap = true });
                    ly += 14;
                }
                GUI.EndScrollView();
                y += chatH + 2;

                // Input
                _guildChatInput = GUI.TextField(new Rect(x, y, w - 30, 18), _guildChatInput,
                    new GUIStyle(GUI.skin.textField) { fontSize = 10, normal = { textColor = Color.white }, focused = { textColor = Color.white } });
                if (GUI.Button(new Rect(x + w - 28, y, 28, 18), ">"))
                {
                    if (!string.IsNullOrEmpty(_guildChatInput.Trim()))
                    {
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.ChatSend(5, _guildChatInput.Trim()));
                        _guildChatInput = "";
                    }
                }
            }
        }

        private static void DrawBorder(Rect area, float width)
        {
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.y + area.height - width, area.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.y, width, area.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x + area.width - width, area.y, width, area.height), Texture2D.whiteTexture);
        }

        private GUIStyle SmallStyle() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
        private GUIStyle DimStyle() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };
        private GUIStyle SectionStyle() => new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.6f, 0.7f, 0.9f) } };
    }
}

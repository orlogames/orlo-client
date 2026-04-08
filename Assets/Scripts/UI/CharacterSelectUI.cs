using System;
using System.Collections.Generic;
using UnityEngine;
using Orlo.UI.CharacterCreation;
using Orlo.UI.Lobby;

namespace Orlo.UI
{
    /// <summary>
    /// Full lobby experience: three-panel layout with character list (left),
    /// 3D preview (center), character info (right), plus social sidebar and top bar.
    /// Integrates with LobbyBackground, CharacterPlatform, EnterWorldButton, NewsTicker.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        public Action<ulong, string> OnCharacterSelected;
        public Action OnCreateNew;

        private bool _visible;
        private int _selectedIndex = -1;
        private List<CharacterEntry> _characters = new();
        private int _maxCharacters = 4;
        private Vector2 _listScroll;
        private Vector2 _socialScroll;
        private float _lastClickTime;
        private int _lastClickIndex = -1;

        // Delete state
        private bool _deleteConfirmActive;
        private string _deleteConfirmInput = "";
        private int _deleteTargetIndex = -1;

        // Server status
        private bool _serverOnline = true;
        private int _playerCount;
        private string _serverStatusText = "Online";

        // Social sidebar
        private bool _socialExpanded = true;
        private List<LobbyFriend> _lobbyFriends = new();
        private string _guildChatInput = "";
        private List<string> _guildChatMessages = new();
        private List<PartyInvite> _partyInvites = new();

        // External references (found at runtime)
        private CharacterPreviewManager _previewManager;
        private EnterWorldButton _enterWorldButton;

        // Cached styles (rebuilt each OnGUI frame to avoid stale skin refs)
        private static readonly string[] RaceNames = { "Solari", "Vael", "Korrath", "Thyren" };
        private static readonly string[] ClassNames = { "Explorer", "Warrior", "Artisan", "Medic", "Ranger", "Pilot" };

        // Colors
        private static readonly Color ColPanelBg = new(0.07f, 0.07f, 0.11f, 0.8f);
        private static readonly Color ColSelected = new(0.15f, 0.2f, 0.35f, 0.9f);
        private static readonly Color ColSlotNormal = new(0.07f, 0.07f, 0.11f, 0.8f);
        private static readonly Color ColDeleteTint = new(0.3f, 0.1f, 0.1f, 0.7f);
        private static readonly Color ColBorderGlow = new(0.4f, 0.6f, 1f, 0.8f);
        private static readonly Color ColGold = new(0.95f, 0.85f, 0.4f);
        private static readonly Color ColBlueWhite = new(0.7f, 0.85f, 1f);
        private static readonly Color ColDim = new(0.5f, 0.5f, 0.6f);
        private static readonly Color ColText = new(0.85f, 0.85f, 0.9f);
        private static readonly Color ColRed = new(1f, 0.35f, 0.25f);
        private static readonly Color ColGuildChat = new(0f, 0.8f, 0.6f);

        // ---- Structs ----

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

        public struct LobbyFriend
        {
            public string Name;
            public bool Online;
            public string Zone;
        }

        public struct PartyInvite
        {
            public string FromPlayer;
            public float ReceivedTime;
        }

        // ---- Public API ----

        public void Show()
        {
            _visible = true;
            _selectedIndex = _characters.Count > 0 ? 0 : -1;
            _deleteConfirmActive = false;
            FindReferences();
        }

        public void Hide() { _visible = false; }

        /// <summary>Enter world with the currently selected character (called by EnterWorldButton).</summary>
        public void EnterWithSelectedCharacter()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _characters.Count) return;
            var ch = _characters[_selectedIndex];
            if (ch.pendingDelete) return;
            string fullName = $"{ch.firstName} {ch.lastName}";
            OnCharacterSelected?.Invoke(ch.id, fullName);
            Hide();
        }

        public void SetCharacters(List<CharacterEntry> characters, int maxSlots)
        {
            _characters = characters ?? new List<CharacterEntry>();
            _maxCharacters = maxSlots;
            _selectedIndex = _characters.Count > 0 ? 0 : -1;
        }

        public void SetServerStatus(bool online, int playerCount, string statusText)
        {
            _serverOnline = online;
            _playerCount = playerCount;
            _serverStatusText = statusText;
        }

        public void SetLobbyFriends(List<LobbyFriend> friends) => _lobbyFriends = friends ?? new List<LobbyFriend>();

        public void AddGuildChatMessage(string msg)
        {
            _guildChatMessages.Add(msg);
            if (_guildChatMessages.Count > 50) _guildChatMessages.RemoveAt(0);
        }

        public void AddPartyInvite(string from)
        {
            _partyInvites.Add(new PartyInvite { FromPlayer = from, ReceivedTime = Time.time });
        }

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

        // ---- Unity Lifecycle ----

        private void FindReferences()
        {
            if (_previewManager == null)
                _previewManager = FindFirstObjectByType<CharacterPreviewManager>();
            if (_enterWorldButton == null)
                _enterWorldButton = FindFirstObjectByType<EnterWorldButton>();
        }

        private void Update()
        {
            if (!_visible) return;

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

            // Keyboard shortcuts (only when delete dialog is closed)
            if (!_deleteConfirmActive)
                HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            if (_characters.Count == 0) return;

            // A / Left arrow = previous character
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _selectedIndex = (_selectedIndex - 1 + _characters.Count) % _characters.Count;
            }
            // D / Right arrow = next character
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                _selectedIndex = (_selectedIndex + 1) % _characters.Count;
            }
            // Enter = enter world
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TryEnterWorld();
            }
        }

        private void TryEnterWorld()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _characters.Count) return;
            var ch = _characters[_selectedIndex];
            if (ch.pendingDelete) return;
            OnCharacterSelected?.Invoke(ch.id, $"{ch.firstName} {ch.lastName}");
            Hide();
        }

        // ---- OnGUI Layout ----

        private void OnGUI()
        {
            if (!_visible) return;

            float sw = Screen.width;
            float sh = Screen.height;
            float topBarH = 50f;
            float bottomPad = 80f;

            // Panel dimensions
            float leftW = sw * 0.20f;
            float rightW = sw * 0.20f;
            float centerW = sw - leftW - rightW;
            float panelY = topBarH;
            float panelH = sh - topBarH - bottomPad;

            // Draw layers
            DrawTopBar(sw, topBarH);
            DrawLeftPanel(new Rect(0, panelY, leftW, panelH));
            DrawCenterArea(new Rect(leftW, panelY, centerW, panelH));
            DrawRightPanel(new Rect(sw - rightW, panelY, rightW, panelH));
            DrawSocialSidebar(sw, sh, topBarH);

            if (_deleteConfirmActive)
                DrawDeleteConfirm(sw, sh);
        }

        // ---- Top Bar ----

        private void DrawTopBar(float sw, float h)
        {
            // Background
            GUI.color = new Color(0.04f, 0.04f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, sw, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Bottom border line
            GUI.color = new Color(0.2f, 0.3f, 0.5f, 0.4f);
            GUI.DrawTexture(new Rect(0, h - 1, sw, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // ORLO title (left)
            var titleStyle = MakeStyle(24, FontStyle.Bold, ColBlueWhite, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(20, 0, 200, h), "ORLO", titleStyle);

            // Server status (center-right)
            float statusX = sw - 400;
            GUI.color = _serverOnline ? Color.green : Color.red;
            GUI.DrawTexture(new Rect(statusX, h / 2f - 4, 8, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var statusStyle = MakeStyle(12, FontStyle.Normal, new Color(0.7f, 0.7f, 0.8f));
            string statusStr = $"Veridian Prime -- {_serverStatusText} ({_playerCount} players)";
            GUI.Label(new Rect(statusX + 14, 0, 260, h), statusStr, statusStyle);

            // Settings gear (placeholder)
            if (GUI.Button(new Rect(sw - 110, 10, 40, 30), "Cfg"))
            {
                // Settings would open here
            }

            // Exit button
            if (GUI.Button(new Rect(sw - 65, 10, 50, 30), "Exit"))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        // ---- Left Panel: Character List ----

        private void DrawLeftPanel(Rect area)
        {
            GUI.color = ColPanelBg;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pad = 12f;
            float y = area.y + pad;
            float w = area.width - pad * 2;

            // Header
            var headerStyle = MakeStyle(14, FontStyle.Bold, new Color(0.8f, 0.85f, 0.95f));
            GUI.Label(new Rect(area.x + pad, y, w, 20), $"Characters ({_characters.Count}/{_maxCharacters})", headerStyle);
            y += 28;

            // Scrollable character list
            float slotH = 80f;
            float gap = 6f;
            int totalSlots = Mathf.Max(_characters.Count + (_characters.Count < _maxCharacters ? 1 : 0), 1);
            float contentH = totalSlots * (slotH + gap);
            float listH = area.y + area.height - y - 90;

            _listScroll = GUI.BeginScrollView(
                new Rect(area.x, y, area.width, listH), _listScroll,
                new Rect(0, 0, area.width - 16, Mathf.Max(contentH, listH)));

            float ly = 0;
            for (int i = 0; i < _characters.Count; i++)
            {
                DrawCharacterSlot(new Rect(pad, ly, w - 2, slotH), _characters[i], i == _selectedIndex, i);
                ly += slotH + gap;
            }

            // Empty / create-new slots
            for (int i = _characters.Count; i < _maxCharacters; i++)
            {
                Rect slotR = new Rect(pad, ly, w - 2, slotH);
                GUI.color = new Color(0.06f, 0.06f, 0.09f, 0.5f);
                GUI.DrawTexture(slotR, Texture2D.whiteTexture);
                GUI.color = new Color(0.25f, 0.3f, 0.4f, 0.6f);
                DrawDashedBorder(slotR, 1);
                GUI.color = Color.white;

                var emptyStyle = MakeStyle(12, FontStyle.Italic, new Color(0.35f, 0.4f, 0.55f), TextAnchor.MiddleCenter);
                GUI.Label(slotR, "+ Create New Character", emptyStyle);

                if (Event.current.type == EventType.MouseDown && slotR.Contains(Event.current.mousePosition))
                {
                    OnCreateNew?.Invoke();
                    Hide();
                    Event.current.Use();
                }

                ly += slotH + gap;
            }
            GUI.EndScrollView();

            // Account summary at bottom
            float summY = area.y + area.height - 80;
            GUI.color = new Color(0.06f, 0.06f, 0.09f, 0.6f);
            GUI.DrawTexture(new Rect(area.x, summY, area.width, 80), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var secStyle = MakeStyle(11, FontStyle.Bold, new Color(0.6f, 0.65f, 0.8f));
            GUI.Label(new Rect(area.x + pad, summY + 8, w, 16), "Account", secStyle);

            float totalHours = 0;
            foreach (var c in _characters) totalHours += c.playtimeHours;
            int th = (int)totalHours;
            int tm = (int)((totalHours - th) * 60);

            var dimStyle = MakeStyle(10, FontStyle.Normal, ColDim);
            GUI.Label(new Rect(area.x + pad, summY + 28, w, 14), $"Total Playtime: {th}h {tm}m", dimStyle);
            GUI.Label(new Rect(area.x + pad, summY + 44, w, 14), $"Characters: {_characters.Count}/{_maxCharacters}", dimStyle);
        }

        private void DrawCharacterSlot(Rect area, CharacterEntry ch, bool selected, int index)
        {
            Color bg = ch.pendingDelete ? ColDeleteTint : (selected ? ColSelected : ColSlotNormal);
            GUI.color = bg;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Glow border on selection
            if (selected)
            {
                GUI.color = ColBorderGlow;
                DrawBorder(area, 2);
                GUI.color = Color.white;
            }

            // Click / double-click
            if (Event.current.type == EventType.MouseDown && area.Contains(Event.current.mousePosition))
            {
                if (_lastClickIndex == index && Time.realtimeSinceStartup - _lastClickTime < 0.35f && !ch.pendingDelete)
                {
                    // Double-click: enter world
                    OnCharacterSelected?.Invoke(ch.id, $"{ch.firstName} {ch.lastName}");
                    Hide();
                    Event.current.Use();
                    return;
                }
                _selectedIndex = index;
                _lastClickIndex = index;
                _lastClickTime = Time.realtimeSinceStartup;
                Event.current.Use();
            }

            float x = area.x + 10;
            float y = area.y + 8;
            float contentW = area.width - 20;

            // Name (bold)
            var nameColor = selected ? new Color(0.95f, 0.97f, 1f) : new Color(0.75f, 0.78f, 0.88f);
            var nameStyle = MakeStyle(14, FontStyle.Bold, nameColor);
            GUI.Label(new Rect(x, y, contentW, 18), $"{ch.firstName} {ch.lastName}", nameStyle);

            // Level + race
            string raceName = ch.race >= 0 && ch.race < RaceNames.Length ? RaceNames[ch.race] : "Unknown";
            var detStyle = MakeStyle(10, FontStyle.Normal, ColDim);
            GUI.Label(new Rect(x, y + 20, contentW, 14), $"L{ch.level} {raceName}", detStyle);

            // Zone + guild tag
            string zone = string.IsNullOrEmpty(ch.zoneName) ? "Threshold" : ch.zoneName;
            string guildTag = !string.IsNullOrEmpty(ch.guildName) ? $"  <{ch.guildName}>" : "";
            GUI.Label(new Rect(x, y + 36, contentW, 14), $"{zone}{guildTag}", detStyle);

            // Pending delete overlay
            if (ch.pendingDelete)
            {
                float rem = ch.deleteTimeRemaining;
                int hrs = (int)(rem / 3600);
                int mins = (int)((rem % 3600) / 60);
                var delStyle = MakeStyle(10, FontStyle.Normal, ColRed);
                GUI.Label(new Rect(x, y + 52, 160, 14), $"Deleting in {hrs}h {mins}m", delStyle);

                if (GUI.Button(new Rect(area.x + area.width - 58, area.y + area.height - 22, 50, 18), "Cancel"))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.CancelCharacterDelete(ch.id));
                }
            }
        }

        // ---- Center Area: 3D Preview ----

        private void DrawCenterArea(Rect area)
        {
            // Transparent center lets LobbyBackground/CharacterPlatform show through
            GUI.color = new Color(0.02f, 0.02f, 0.04f, 0.3f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_selectedIndex < 0 || _selectedIndex >= _characters.Count)
            {
                var style = MakeStyle(18, FontStyle.Italic, new Color(0.3f, 0.3f, 0.4f), TextAnchor.MiddleCenter);
                GUI.Label(area, "Select a character", style);
                return;
            }

            // Draw preview render texture if available
            if (_previewManager != null && _previewManager.PreviewTexture != null)
            {
                float texW = area.width * 0.85f;
                float texH = area.height * 0.8f;
                float texX = area.x + (area.width - texW) / 2f;
                float texY = area.y + 10;
                Rect previewRect = new Rect(texX, texY, texW, texH);

                GUI.DrawTexture(previewRect, _previewManager.PreviewTexture, ScaleMode.ScaleToFit);
                _previewManager.HandleOrbitInput(previewRect);
            }

            // Character name below preview
            var ch = _characters[_selectedIndex];
            var nameStyle = MakeStyle(18, FontStyle.Bold, ColGold, TextAnchor.UpperCenter);
            GUI.Label(new Rect(area.x, area.y + area.height - 60, area.width, 24),
                $"{ch.firstName} {ch.lastName}", nameStyle);

            // Left / right arrows to cycle
            float arrowY = area.y + area.height / 2f - 20;
            if (_characters.Count > 1)
            {
                if (GUI.Button(new Rect(area.x + 8, arrowY, 36, 40), "<"))
                    _selectedIndex = (_selectedIndex - 1 + _characters.Count) % _characters.Count;

                if (GUI.Button(new Rect(area.x + area.width - 44, arrowY, 36, 40), ">"))
                    _selectedIndex = (_selectedIndex + 1) % _characters.Count;
            }

            // Hint text
            var hintStyle = MakeStyle(10, FontStyle.Normal, new Color(0.4f, 0.4f, 0.5f), TextAnchor.UpperCenter);
            GUI.Label(new Rect(area.x, area.y + area.height - 34, area.width, 16),
                "Drag to rotate  |  A / D to cycle  |  Enter to play", hintStyle);
        }

        // ---- Right Panel: Character Info ----

        private void DrawRightPanel(Rect area)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _characters.Count)
            {
                // Empty right panel
                GUI.color = ColPanelBg;
                GUI.DrawTexture(area, Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            GUI.color = ColPanelBg;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var ch = _characters[_selectedIndex];
            float pad = 14f;
            float x = area.x + pad;
            float w = area.width - pad * 2;
            float y = area.y + pad;

            // Character name header
            var nameStyle = MakeStyle(16, FontStyle.Bold, ColGold);
            GUI.Label(new Rect(x, y, w, 22), $"{ch.firstName} {ch.lastName}", nameStyle);
            y += 30;

            // Separator
            GUI.color = new Color(0.3f, 0.35f, 0.5f, 0.3f);
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 10;

            // Info rows
            string raceName = ch.race >= 0 && ch.race < RaceNames.Length ? RaceNames[ch.race] : "Unknown";
            string className = ch.classId >= 0 && ch.classId < ClassNames.Length ? ClassNames[ch.classId] : "Explorer";

            InfoRow(x, ref y, w, "Level", ch.level.ToString());
            InfoRow(x, ref y, w, "Race", raceName);
            InfoRow(x, ref y, w, "Class", className);
            InfoRow(x, ref y, w, "Last Location", string.IsNullOrEmpty(ch.zoneName) ? "Threshold" : ch.zoneName);

            int hrs = (int)ch.playtimeHours;
            int mins = (int)((ch.playtimeHours - hrs) * 60);
            InfoRow(x, ref y, w, "Playtime", $"{hrs}h {mins}m");

            // Credits (gold)
            var labelStyle = MakeStyle(11, FontStyle.Normal, ColDim);
            GUI.Label(new Rect(x, y, 80, 18), "Credits", labelStyle);
            var credStyle = MakeStyle(11, FontStyle.Normal, ColGold);
            GUI.Label(new Rect(x + 84, y, w - 84, 18), $"{ch.credits:N0}", credStyle);
            y += 20;

            if (!string.IsNullOrEmpty(ch.guildName))
                InfoRow(x, ref y, w, "Guild", ch.guildName);
            if (!string.IsNullOrEmpty(ch.factionName))
                InfoRow(x, ref y, w, "Faction", ch.factionName);

            if (ch.criminalRating > 0)
            {
                string[] crLabels = { "", "Suspect", "Criminal", "Notorious" };
                string crLabel = ch.criminalRating < crLabels.Length ? crLabels[ch.criminalRating] : $"Rating {ch.criminalRating}";
                GUI.Label(new Rect(x, y, 80, 18), "Criminal", labelStyle);
                var crStyle = MakeStyle(11, FontStyle.Normal, ColRed);
                GUI.Label(new Rect(x + 84, y, w - 84, 18), crLabel, crStyle);
                y += 20;
            }

            y += 10;

            // Equipment section
            GUI.color = new Color(0.3f, 0.35f, 0.5f, 0.3f);
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 10;

            var secStyle = MakeStyle(12, FontStyle.Bold, new Color(0.65f, 0.7f, 0.85f));
            GUI.Label(new Rect(x, y, w, 18), "Equipment", secStyle);
            y += 22;

            var placeholderStyle = MakeStyle(10, FontStyle.Italic, new Color(0.35f, 0.35f, 0.45f));
            GUI.Label(new Rect(x, y, w, 16), "Equipment details coming soon", placeholderStyle);
            y += 28;

            // Quick Stats section
            GUI.color = new Color(0.3f, 0.35f, 0.5f, 0.3f);
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 10;

            GUI.Label(new Rect(x, y, w, 18), "Quick Stats", secStyle);
            y += 22;
            GUI.Label(new Rect(x, y, w, 16), "Coming soon", placeholderStyle);

            // Delete button at bottom
            float delY = area.y + area.height - 40;
            GUI.backgroundColor = new Color(0.4f, 0.12f, 0.12f);
            var delBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, normal = { textColor = ColRed } };
            if (GUI.Button(new Rect(x, delY, w, 26), "Delete Character", delBtnStyle))
            {
                _deleteConfirmActive = true;
                _deleteTargetIndex = _selectedIndex;
                _deleteConfirmInput = "";
            }
            GUI.backgroundColor = Color.white;
        }

        // ---- Social Sidebar ----

        private void DrawSocialSidebar(float sw, float sh, float topBarH)
        {
            float sideW = _socialExpanded ? 200f : 30f;
            float sideX = sw - sideW;
            float sideY = topBarH + 4;
            float sideH = sh - topBarH - 90;

            // Toggle button
            string toggleLabel = _socialExpanded ? ">>" : "<<";
            if (GUI.Button(new Rect(sideX, sideY, 28, 28), toggleLabel))
                _socialExpanded = !_socialExpanded;

            if (!_socialExpanded) return;

            GUI.color = new Color(0.05f, 0.05f, 0.09f, 0.92f);
            GUI.DrawTexture(new Rect(sideX, sideY + 30, sideW, sideH - 30), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = sideY + 36;
            float x = sideX + 8;
            float w = sideW - 16;

            // Friends
            var secStyle = MakeStyle(11, FontStyle.Bold, new Color(0.6f, 0.7f, 0.9f));
            GUI.Label(new Rect(x, y, w, 16), "Friends", secStyle);
            y += 18;

            int shown = 0;
            foreach (var f in _lobbyFriends)
            {
                if (shown >= 8) break;
                GUI.color = f.Online ? Color.green : new Color(0.35f, 0.35f, 0.35f);
                GUI.DrawTexture(new Rect(x, y + 3, 6, 6), Texture2D.whiteTexture);
                GUI.color = Color.white;

                var fStyle = MakeStyle(10, FontStyle.Normal, f.Online ? Color.white : ColDim);
                GUI.Label(new Rect(x + 10, y, 100, 14), f.Name, fStyle);

                if (f.Online && GUI.Button(new Rect(x + w - 20, y, 18, 14), "W"))
                    ChatUI.Instance?.ReceiveMessage("System", "System", $"/w {f.Name} ");

                y += 16;
                shown++;
            }
            y += 8;

            // Party invites
            if (_partyInvites.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, 16), "Party Invites", secStyle);
                y += 18;

                for (int i = _partyInvites.Count - 1; i >= 0; i--)
                {
                    var inv = _partyInvites[i];
                    var invStyle = MakeStyle(10, FontStyle.Normal, Color.white);
                    GUI.Label(new Rect(x, y, 80, 14), inv.FromPlayer, invStyle);
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

            // Guild chat
            if (_guildChatMessages.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, 16), "Guild Chat", secStyle);
                y += 18;

                float chatH = Mathf.Min(sideH - (y - sideY) - 40, 140);
                float totalH = _guildChatMessages.Count * 14f;
                _socialScroll = GUI.BeginScrollView(
                    new Rect(x, y, w, chatH), _socialScroll,
                    new Rect(0, 0, w - 12, Mathf.Max(totalH, chatH)));

                float ly = 0;
                var chatStyle = MakeStyle(9, FontStyle.Normal, ColGuildChat);
                chatStyle.wordWrap = true;
                foreach (var msg in _guildChatMessages)
                {
                    GUI.Label(new Rect(0, ly, w - 12, 12), msg, chatStyle);
                    ly += 14;
                }
                GUI.EndScrollView();
                y += chatH + 4;

                // Chat input
                _guildChatInput = GUI.TextField(new Rect(x, y, w - 30, 18), _guildChatInput,
                    new GUIStyle(GUI.skin.textField) { fontSize = 10 });
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

        // ---- Delete Confirmation Modal ----

        private void DrawDeleteConfirm(float sw, float sh)
        {
            // Overlay
            GUI.color = new Color(0, 0, 0, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float dlgW = 380;
            float dlgH = 200;
            float dlgX = (sw - dlgW) / 2f;
            float dlgY = (sh - dlgH) / 2f;

            // Card background
            GUI.color = new Color(0.08f, 0.07f, 0.12f, 0.98f);
            GUI.DrawTexture(new Rect(dlgX, dlgY, dlgW, dlgH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Red border
            GUI.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
            DrawBorder(new Rect(dlgX, dlgY, dlgW, dlgH), 2);
            GUI.color = Color.white;

            if (_deleteTargetIndex < 0 || _deleteTargetIndex >= _characters.Count)
            {
                _deleteConfirmActive = false;
                return;
            }

            var ch = _characters[_deleteTargetIndex];
            string charName = $"{ch.firstName} {ch.lastName}";

            float y = dlgY + 16;

            // Header
            var headerStyle = MakeStyle(15, FontStyle.Bold, ColRed, TextAnchor.MiddleCenter);
            GUI.Label(new Rect(dlgX, y, dlgW, 22), "Delete Character?", headerStyle);
            y += 30;

            // Message
            string msg = ch.level >= 10
                ? $"Type \"{charName}\" to confirm.\nLevel 10+ characters have a 24h deletion cooldown."
                : $"Type \"{charName}\" to confirm.\nThis character will be immediately deleted.";
            var msgStyle = MakeStyle(11, FontStyle.Normal, ColText, TextAnchor.MiddleCenter);
            msgStyle.wordWrap = true;
            GUI.Label(new Rect(dlgX + 24, y, dlgW - 48, 44), msg, msgStyle);
            y += 52;

            // Input field
            _deleteConfirmInput = GUI.TextField(
                new Rect(dlgX + 24, y, dlgW - 48, 24), _deleteConfirmInput,
                new GUIStyle(GUI.skin.textField) { fontSize = 12 });
            y += 34;

            // Buttons
            bool nameMatches = _deleteConfirmInput.Trim() == charName;
            GUI.enabled = nameMatches;
            GUI.backgroundColor = new Color(0.6f, 0.12f, 0.12f);
            if (GUI.Button(new Rect(dlgX + 24, y, 110, 32), "Delete"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.CharacterDeleteExtended(ch.id));
                _deleteConfirmActive = false;
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            if (GUI.Button(new Rect(dlgX + dlgW - 134, y, 110, 32), "Cancel"))
                _deleteConfirmActive = false;
        }

        // ---- Helpers ----

        private void InfoRow(float x, ref float y, float w, string label, string value)
        {
            var lbl = MakeStyle(11, FontStyle.Normal, ColDim);
            var val = MakeStyle(11, FontStyle.Normal, ColText);
            GUI.Label(new Rect(x, y, 84, 18), label, lbl);
            GUI.Label(new Rect(x + 84, y, w - 84, 18), value, val);
            y += 20;
        }

        private static GUIStyle MakeStyle(int size, FontStyle font, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = font,
                alignment = anchor,
                normal = { textColor = color }
            };
        }

        private static void DrawBorder(Rect r, float w)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - w, r.width, w), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x + r.width - w, r.y, w, r.height), Texture2D.whiteTexture);
        }

        private static void DrawDashedBorder(Rect r, float w)
        {
            // Simplified dashed border via corner ticks and mid-edge ticks
            float dashLen = 8f;
            float gap = 6f;

            // Top edge
            for (float dx = 0; dx < r.width; dx += dashLen + gap)
                GUI.DrawTexture(new Rect(r.x + dx, r.y, Mathf.Min(dashLen, r.width - dx), w), Texture2D.whiteTexture);
            // Bottom edge
            for (float dx = 0; dx < r.width; dx += dashLen + gap)
                GUI.DrawTexture(new Rect(r.x + dx, r.y + r.height - w, Mathf.Min(dashLen, r.width - dx), w), Texture2D.whiteTexture);
            // Left edge
            for (float dy = 0; dy < r.height; dy += dashLen + gap)
                GUI.DrawTexture(new Rect(r.x, r.y + dy, w, Mathf.Min(dashLen, r.height - dy)), Texture2D.whiteTexture);
            // Right edge
            for (float dy = 0; dy < r.height; dy += dashLen + gap)
                GUI.DrawTexture(new Rect(r.x + r.width - w, r.y + dy, w, Mathf.Min(dashLen, r.height - dy)), Texture2D.whiteTexture);
        }
    }
}

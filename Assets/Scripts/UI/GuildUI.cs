using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Guild management panel. Toggle with G key.
    /// Tabs: Info, Roster, Bank, Log, Ranks.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class GuildUI : MonoBehaviour
    {
        public static GuildUI Instance { get; private set; }

        private enum Tab { Info, Roster, Bank, Log, Ranks }

        private bool _visible;
        private Tab _activeTab = Tab.Info;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        private const float WinW = 520f;
        private const float WinH = 480f;

        // ---- Guild Data ----

        private bool _inGuild;
        private string _guildName = "";
        private string _guildTag = "";
        private int _memberCount;
        private int _renownLevel;
        private string _motd = "";
        private bool _editingMotd;
        private string _motdInput = "";
        private bool _canEditMotd;

        // Create guild
        private string _createName = "";
        private string _createTag = "";

        // Roster
        public struct GuildMember
        {
            public string Name;
            public int Rank;
            public int Level;
            public bool Online;
            public string LastSeen;
            public string Note;
        }
        private List<GuildMember> _roster = new List<GuildMember>();
        private string _inviteInput = "";

        // Bank
        private int _activeBankTab;
        public struct BankSlot
        {
            public bool Occupied;
            public string ItemName;
            public int StackCount;
            public Color RarityColor;
        }
        private BankSlot[,] _bankSlots = new BankSlot[4, 50]; // 4 tabs, 50 slots each
        private long _bankCredits;

        // Log
        public struct GuildLogEntry
        {
            public string Actor;
            public string Action;
            public string Timestamp;
        }
        private List<GuildLogEntry> _log = new List<GuildLogEntry>();

        // Ranks — uses GuildRankData from PacketBuilder to avoid circular dependency
        private Network.PacketBuilder.GuildRankData[] _ranks = new Network.PacketBuilder.GuildRankData[10];
        private bool _isLeader;
        private int _myRank;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
            InitDefaultRanks();
        }

        private void InitDefaultRanks()
        {
            string[] defaultNames = { "Leader", "Officer", "Veteran", "Member", "Recruit", "", "", "", "", "" };
            for (int i = 0; i < 10; i++)
            {
                _ranks[i] = new Network.PacketBuilder.GuildRankData { Name = i < defaultNames.Length ? defaultNames[i] : "" };
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.G) && !ChatUI.Instance?.IsInputActive == true)
                _visible = !_visible;
        }

        // ---- Public API ----

        public void Toggle() { _visible = !_visible; }

        public void SetGuildInfo(string name, string tag, int members, int renown, string motd, bool canEditMotd, bool isLeader, int myRank)
        {
            _inGuild = true;
            _guildName = name;
            _guildTag = tag;
            _memberCount = members;
            _renownLevel = renown;
            _motd = motd;
            _canEditMotd = canEditMotd;
            _isLeader = isLeader;
            _myRank = myRank;
        }

        public void ClearGuild() { _inGuild = false; _guildName = ""; _roster.Clear(); }

        public void SetRoster(List<GuildMember> roster) { _roster = roster ?? new List<GuildMember>(); }

        public void SetBankContents(int tab, BankSlot[] slots, long credits)
        {
            if (tab < 0 || tab >= 4 || slots == null) return;
            for (int i = 0; i < Mathf.Min(slots.Length, 50); i++)
                _bankSlots[tab, i] = slots[i];
            _bankCredits = credits;
        }

        public void SetLog(List<GuildLogEntry> log) { _log = log ?? new List<GuildLogEntry>(); }
        public void SetRanks(Network.PacketBuilder.GuildRankData[] ranks) { if (ranks != null && ranks.Length == 10) _ranks = ranks; }
        public void SetRenown(int level) { _renownLevel = level; }

        // ---- OnGUI ----

        private void OnGUI()
        {
            if (!_visible) return;

            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, WinW - 28, 28);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(_windowPos.x, _windowPos.y, WinW, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string title = _inGuild ? $"Guild: {_guildName} [{_guildTag}]" : "Guild";
            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 300, 28), title, TitleStyle());

            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            {
                _visible = false; return;
            }
            HandleDrag(titleBar);

            if (!_inGuild)
            {
                DrawNoGuild();
                return;
            }

            float cy = _windowPos.y + 32;
            float cx = _windowPos.x + 8;
            float cw = WinW - 16;

            // Tab bar
            Tab[] tabs = { Tab.Info, Tab.Roster, Tab.Bank, Tab.Log, Tab.Ranks };
            float tabW = cw / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool sel = _activeTab == tabs[i];
                // Hide Ranks tab if not leader
                if (tabs[i] == Tab.Ranks && !_isLeader) continue;

                GUI.color = sel ? new Color(0.2f, 0.3f, 0.5f, 0.9f) : new Color(0.12f, 0.12f, 0.18f, 0.9f);
                GUI.DrawTexture(new Rect(cx + i * tabW, cy, tabW - 2, 22), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(cx + i * tabW, cy, tabW - 2, 22), tabs[i].ToString(), SmallCentered()))
                    _activeTab = tabs[i];
            }
            cy += 26;

            Rect contentRect = new Rect(cx, cy, cw, WinH - (cy - _windowPos.y) - 8);

            switch (_activeTab)
            {
                case Tab.Info: DrawInfoTab(contentRect); break;
                case Tab.Roster: DrawRosterTab(contentRect); break;
                case Tab.Bank: DrawBankTab(contentRect); break;
                case Tab.Log: DrawLogTab(contentRect); break;
                case Tab.Ranks: DrawRanksTab(contentRect); break;
            }

            GUI.color = Color.white;
        }

        private void DrawNoGuild()
        {
            float cx = _windowPos.x + WinW / 2f;
            float cy = _windowPos.y + 80;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.6f, 0.7f) } };
            GUI.Label(new Rect(_windowPos.x + 20, cy, WinW - 40, 30), "You are not in a guild.", style);
            cy += 50;

            GUI.Label(new Rect(_windowPos.x + 20, cy, 80, 20), "Name:", SmallLabel());
            _createName = GUI.TextField(new Rect(_windowPos.x + 100, cy, 200, 20), _createName, SmallInputStyle());
            cy += 26;

            GUI.Label(new Rect(_windowPos.x + 20, cy, 80, 20), "Tag:", SmallLabel());
            _createTag = GUI.TextField(new Rect(_windowPos.x + 100, cy, 80, 20), _createTag, SmallInputStyle());
            cy += 26;

            var costStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            GUI.Label(new Rect(_windowPos.x + 100, cy, 200, 20), "Cost: 5,000 credits", costStyle);
            cy += 26;

            GUI.enabled = !string.IsNullOrEmpty(_createName) && !string.IsNullOrEmpty(_createTag);
            if (GUI.Button(new Rect(_windowPos.x + 100, cy, 120, 28), "Create Guild"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.CreateGuild(_createName, _createTag));
            }
            GUI.enabled = true;
        }

        private void DrawInfoTab(Rect area)
        {
            float y = area.y + 4;
            float x = area.x + 4;

            InfoRow(x, ref y, "Guild Name", _guildName);
            InfoRow(x, ref y, "Tag", $"[{_guildTag}]");
            InfoRow(x, ref y, "Members", $"{_memberCount}");
            InfoRow(x, ref y, "Renown", $"Level {_renownLevel}");

            y += 8;
            GUI.Label(new Rect(x, y, 200, 18), "Message of the Day:", SectionHeader());
            y += 20;

            if (_editingMotd)
            {
                _motdInput = GUI.TextArea(new Rect(x, y, area.width - 8, 60), _motdInput, 200);
                y += 64;
                if (GUI.Button(new Rect(x, y, 60, 20), "Save"))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.SetGuildMOTD(_motdInput));
                    _motd = _motdInput;
                    _editingMotd = false;
                }
                if (GUI.Button(new Rect(x + 64, y, 60, 20), "Cancel"))
                    _editingMotd = false;
            }
            else
            {
                var motdStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.7f) } };
                GUI.Label(new Rect(x, y, area.width - 8, 60), string.IsNullOrEmpty(_motd) ? "(No MOTD set)" : _motd, motdStyle);
                y += 64;
                if (_canEditMotd)
                {
                    if (GUI.Button(new Rect(x, y, 80, 20), "Edit MOTD"))
                    {
                        _editingMotd = true;
                        _motdInput = _motd;
                    }
                }
            }

            // Leave guild button at bottom
            float bottomY = area.y + area.height - 30;
            GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            if (GUI.Button(new Rect(x, bottomY, 100, 24), "Leave Guild"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.LeaveGuild());
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawRosterTab(Rect area)
        {
            float y = area.y;
            float x = area.x + 4;
            float w = area.width - 8;

            // Invite bar
            _inviteInput = GUI.TextField(new Rect(x, y, w - 60, 20), _inviteInput, SmallInputStyle());
            if (GUI.Button(new Rect(x + w - 56, y, 56, 20), "Invite"))
            {
                if (!string.IsNullOrEmpty(_inviteInput.Trim()))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.GuildInviteRequest(_inviteInput.Trim()));
                    _inviteInput = "";
                }
            }
            y += 24;

            // Header
            GUI.Label(new Rect(x, y, 120, 16), "Name", DimLabel());
            GUI.Label(new Rect(x + 124, y, 60, 16), "Rank", DimLabel());
            GUI.Label(new Rect(x + 188, y, 40, 16), "Lvl", DimLabel());
            GUI.Label(new Rect(x + 232, y, 60, 16), "Status", DimLabel());
            y += 18;

            float listH = area.height - (y - area.y) - 4;
            float totalH = _roster.Count * 24f;
            Rect scrollRect = new Rect(x, y, w, listH);
            _scrollPos = GUI.BeginScrollView(scrollRect, _scrollPos, new Rect(0, 0, w - 16, Mathf.Max(totalH, listH)));

            float ly = 0;
            foreach (var m in _roster)
            {
                // Online indicator
                GUI.color = m.Online ? Color.green : new Color(0.4f, 0.4f, 0.4f);
                GUI.DrawTexture(new Rect(0, ly + 4, 6, 6), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(10, ly, 110, 20), m.Name, SmallLabel());
                string rankName = m.Rank >= 0 && m.Rank < _ranks.Length ? _ranks[m.Rank].Name : $"Rank {m.Rank}";
                GUI.Label(new Rect(124, ly, 60, 20), rankName, DimLabel());
                GUI.Label(new Rect(188, ly, 40, 20), m.Level.ToString(), SmallLabel());
                GUI.Label(new Rect(232, ly, 80, 20), m.Online ? "Online" : m.LastSeen, DimLabel());

                // Action buttons (permission-gated)
                float btnX = 316;
                bool canKick = _myRank <= 1;
                bool canPromote = _myRank <= 1;
                if (canPromote && GUI.Button(new Rect(btnX, ly, 20, 18), "+")) // Promote
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.GuildPromote(m.Name));
                btnX += 24;
                if (canPromote && GUI.Button(new Rect(btnX, ly, 20, 18), "-")) // Demote
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.GuildDemote(m.Name));
                btnX += 24;
                if (canKick && GUI.Button(new Rect(btnX, ly, 30, 18), "Kick"))
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.GuildKick(m.Name));

                ly += 22;
            }

            GUI.EndScrollView();
        }

        private void DrawBankTab(Rect area)
        {
            float y = area.y;
            float x = area.x + 4;
            float w = area.width - 8;

            // Bank tab buttons
            for (int t = 0; t < 4; t++)
            {
                bool sel = _activeBankTab == t;
                GUI.color = sel ? new Color(0.2f, 0.35f, 0.5f, 0.9f) : new Color(0.15f, 0.15f, 0.2f, 0.9f);
                if (GUI.Button(new Rect(x + t * 55, y, 52, 20), $"Tab {t + 1}"))
                {
                    _activeBankTab = t;
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.GuildBankOpen(_activeBankTab));
                }
            }
            GUI.color = Color.white;
            y += 24;

            // Credits
            var credStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
            GUI.Label(new Rect(x, y, 200, 18), $"Bank Credits: {_bankCredits:N0}", credStyle);
            y += 22;

            // Grid: 10 columns x 5 rows
            float slotSize = 42f;
            float padding = 3f;
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 10; col++)
                {
                    int idx = row * 10 + col;
                    float sx = x + col * (slotSize + padding);
                    float sy = y + row * (slotSize + padding);

                    var slot = _bankSlots[_activeBankTab, idx];

                    GUI.color = slot.Occupied ? new Color(0.15f, 0.2f, 0.25f, 0.9f) : new Color(0.1f, 0.1f, 0.12f, 0.7f);
                    GUI.DrawTexture(new Rect(sx, sy, slotSize, slotSize), Texture2D.whiteTexture);

                    if (slot.Occupied)
                    {
                        // Rarity border
                        GUI.color = slot.RarityColor;
                        GUI.DrawTexture(new Rect(sx, sy, slotSize, 2), Texture2D.whiteTexture);
                        GUI.color = Color.white;

                        // Item name (truncated)
                        string name = slot.ItemName.Length > 6 ? slot.ItemName.Substring(0, 5) + ".." : slot.ItemName;
                        var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                        GUI.Label(new Rect(sx, sy + 8, slotSize, 20), name, nameStyle);

                        if (slot.StackCount > 1)
                        {
                            var stackStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.LowerRight, normal = { textColor = Color.white } };
                            GUI.Label(new Rect(sx, sy, slotSize - 2, slotSize - 2), slot.StackCount.ToString(), stackStyle);
                        }
                    }
                    GUI.color = Color.white;
                }
            }
        }

        private void DrawLogTab(Rect area)
        {
            float totalH = _log.Count * 20f;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos, new Rect(0, 0, area.width - 16, Mathf.Max(totalH, area.height)));

            float y = 0;
            foreach (var entry in _log)
            {
                GUI.Label(new Rect(4, y, 80, 18), entry.Timestamp, DimLabel());
                GUI.Label(new Rect(88, y, 80, 18), entry.Actor, SmallLabel());
                GUI.Label(new Rect(172, y, area.width - 190, 18), entry.Action, DimLabel());
                y += 18;
            }

            GUI.EndScrollView();
        }

        private void DrawRanksTab(Rect area)
        {
            if (!_isLeader)
            {
                GUI.Label(new Rect(area.x + 8, area.y + 8, 200, 20), "Only the guild leader can edit ranks.", SmallLabel());
                return;
            }

            float totalH = 10 * 50f;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos, new Rect(0, 0, area.width - 16, totalH));

            float y = 0;
            for (int i = 0; i < 10; i++)
            {
                GUI.Label(new Rect(4, y, 30, 18), $"#{i}", DimLabel());
                _ranks[i] = new Network.PacketBuilder.GuildRankData
                {
                    Name = GUI.TextField(new Rect(36, y, 100, 18), _ranks[i].Name, SmallInputStyle()),
                    CanInvite = GUI.Toggle(new Rect(142, y, 60, 18), _ranks[i].CanInvite, "Invite"),
                    CanKick = GUI.Toggle(new Rect(206, y, 50, 18), _ranks[i].CanKick, "Kick"),
                    CanPromote = GUI.Toggle(new Rect(260, y, 70, 18), _ranks[i].CanPromote, "Promote"),
                    CanEditMotd = GUI.Toggle(new Rect(334, y, 60, 18), _ranks[i].CanEditMotd, "MOTD"),
                    CanBankDeposit = _ranks[i].CanBankDeposit,
                    CanBankWithdraw = _ranks[i].CanBankWithdraw,
                    BankWithdrawLimit = _ranks[i].BankWithdrawLimit
                };

                GUI.Toggle(new Rect(142, y + 20, 80, 18), _ranks[i].CanBankDeposit, "Bank Dep");
                GUI.Toggle(new Rect(226, y + 20, 80, 18), _ranks[i].CanBankWithdraw, "Bank Wd");

                y += 44;
            }

            GUI.EndScrollView();

            float saveY = area.y + area.height - 28;
            if (GUI.Button(new Rect(area.x + 4, saveY, 100, 24), "Save Ranks"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.SetGuildRanks(_ranks));
            }
        }

        // ---- Helpers ----

        private void InfoRow(float x, ref float y, string label, string value)
        {
            GUI.Label(new Rect(x, y, 120, 18), label, DimLabel());
            GUI.Label(new Rect(x + 124, y, 200, 18), value, SmallLabel());
            y += 20;
        }

        private void HandleDrag(Rect titleBar)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            { _dragging = true; _dragOffset = e.mousePosition - _windowPos; e.Use(); }
            if (_dragging && e.type == EventType.MouseDrag)
            { _windowPos = e.mousePosition - _dragOffset; e.Use(); }
            if (e.type == EventType.MouseUp) _dragging = false;
        }

        private GUIStyle TitleStyle() => new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
        private GUIStyle SmallLabel() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
        private GUIStyle SmallCentered() => new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        private GUIStyle DimLabel() => new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };
        private GUIStyle SectionHeader() => new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.8f, 1f) } };
        private GUIStyle SmallInputStyle() => new GUIStyle(GUI.skin.textField) { fontSize = 11, normal = { textColor = Color.white }, focused = { textColor = Color.white } };
    }
}

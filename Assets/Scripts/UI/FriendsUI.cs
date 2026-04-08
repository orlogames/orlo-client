using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Friends / Blocked / Recent panel. Toggle with O key.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class FriendsUI : MonoBehaviour
    {
        public static FriendsUI Instance { get; private set; }

        private enum Tab { Friends, Blocked, Recent }

        private bool _visible;
        private Tab _activeTab = Tab.Friends;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;
        private string _addFriendInput = "";

        // Player status
        public enum PlayerStatus { Online, AFK, Busy, LFP, LFT }
        private PlayerStatus _myStatus = PlayerStatus.Online;
        private bool _statusDropdown;

        // Context menu
        private bool _contextOpen;
        private int _contextIndex = -1;
        private Vector2 _contextPos;

        private const float WinW = 300f;
        private const float WinH = 450f;

        // ---- Data ----

        public struct FriendEntry
        {
            public string Name;
            public bool Online;
            public string ZoneName;
            public string Note;
            public int Category; // 0=Friend, 1=Good Friend, 2=Best Friend
        }

        public struct BlockedEntry
        {
            public string Name;
        }

        public struct RecentEntry
        {
            public string Name;
            public string InteractionType;
            public float TimeAgo;
        }

        public struct PendingRequest
        {
            public string Name;
            public bool Incoming;
        }

        private List<FriendEntry> _friends = new List<FriendEntry>();
        private List<BlockedEntry> _blocked = new List<BlockedEntry>();
        private List<RecentEntry> _recent = new List<RecentEntry>();
        private List<PendingRequest> _pending = new List<PendingRequest>();

        private static readonly string[] CategoryNames = { "Friend", "Good Friend", "Best Friend" };
        private static readonly string[] StatusNames = { "Online", "AFK", "Busy", "LFP", "LFT" };
        private static readonly Color[] StatusColors = {
            Color.green, Color.yellow, Color.red, Color.cyan, new Color(0.6f, 0.4f, 1f)
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.O) && !ChatUI.Instance?.IsInputActive == true)
                _visible = !_visible;
        }

        // ---- Public API ----

        public void Toggle() { _visible = !_visible; }

        public void SetFriendsList(List<FriendEntry> friends) { _friends = friends ?? new List<FriendEntry>(); }
        public void SetBlockedList(List<BlockedEntry> blocked) { _blocked = blocked ?? new List<BlockedEntry>(); }
        public void SetRecentList(List<RecentEntry> recent) { _recent = recent ?? new List<RecentEntry>(); }
        public void SetPendingRequests(List<PendingRequest> pending) { _pending = pending ?? new List<PendingRequest>(); }

        public void UpdateFriendStatus(string name, bool online, string zone)
        {
            for (int i = 0; i < _friends.Count; i++)
            {
                if (_friends[i].Name == name)
                {
                    var f = _friends[i];
                    f.Online = online;
                    f.ZoneName = zone;
                    _friends[i] = f;
                    return;
                }
            }
        }

        public void UpdatePlayerStatus(string name, PlayerStatus status)
        {
            // Could show visual indicators on friends
        }

        // ---- OnGUI ----

        private void OnGUI()
        {
            if (!_visible) return;

            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            // Window bg
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, WinW - 28, 28);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(_windowPos.x, _windowPos.y, WinW, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "Friends", TitleStyle());

            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            {
                _visible = false; return;
            }
            HandleDrag(titleBar);

            float cy = _windowPos.y + 32;
            float cx = _windowPos.x + 8;
            float cw = WinW - 16;

            // Status bar
            Color sColor = StatusColors[(int)_myStatus];
            GUI.color = sColor;
            GUI.DrawTexture(new Rect(cx, cy, 8, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (GUI.Button(new Rect(cx + 12, cy - 2, 80, 18), StatusNames[(int)_myStatus], SmallLabel()))
                _statusDropdown = !_statusDropdown;

            if (_statusDropdown)
            {
                for (int i = 0; i < StatusNames.Length; i++)
                {
                    if (GUI.Button(new Rect(cx + 12, cy + 16 + i * 18, 80, 18), StatusNames[i], SmallLabel()))
                    {
                        _myStatus = (PlayerStatus)i;
                        _statusDropdown = false;
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.SetPlayerStatus(i));
                    }
                }
            }

            cy += 22;

            // Tab bar
            float tabW = cw / 3f;
            Tab[] tabs = { Tab.Friends, Tab.Blocked, Tab.Recent };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool sel = _activeTab == tabs[i];
                GUI.color = sel ? new Color(0.2f, 0.3f, 0.5f, 0.9f) : new Color(0.12f, 0.12f, 0.18f, 0.9f);
                GUI.DrawTexture(new Rect(cx + i * tabW, cy, tabW - 2, 22), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(cx + i * tabW, cy, tabW - 2, 22), tabs[i].ToString(), SmallLabelCentered()))
                    _activeTab = tabs[i];
            }
            cy += 26;

            // Content
            float contentH = WinH - (cy - _windowPos.y) - 30;
            Rect scrollArea = new Rect(cx, cy, cw, contentH);

            switch (_activeTab)
            {
                case Tab.Friends: DrawFriendsTab(scrollArea); break;
                case Tab.Blocked: DrawBlockedTab(scrollArea); break;
                case Tab.Recent: DrawRecentTab(scrollArea); break;
            }

            // Add friend input at bottom
            float bottomY = _windowPos.y + WinH - 26;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(cx, bottomY, cw - 50, 22), Texture2D.whiteTexture);
            GUI.color = Color.white;
            _addFriendInput = GUI.TextField(new Rect(cx + 2, bottomY + 1, cw - 54, 20), _addFriendInput, SmallInputStyle());
            if (GUI.Button(new Rect(cx + cw - 46, bottomY, 46, 22), "Add"))
            {
                if (!string.IsNullOrEmpty(_addFriendInput.Trim()))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.FriendRequest(_addFriendInput.Trim()));
                    _addFriendInput = "";
                }
            }

            // Context menu
            if (_contextOpen) DrawContextMenu();

            GUI.color = Color.white;
        }

        private void DrawFriendsTab(Rect area)
        {
            // Pending requests first
            float totalH = _pending.Count * 26f + _friends.Count * 30f + 10;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos, new Rect(0, 0, area.width - 16, Mathf.Max(totalH, area.height)));

            float y = 0;

            if (_pending.Count > 0)
            {
                GUI.Label(new Rect(4, y, 200, 18), "Pending Requests", SectionHeader());
                y += 20;

                foreach (var req in _pending)
                {
                    GUI.Label(new Rect(4, y, 120, 20), req.Name, SmallLabel());
                    if (req.Incoming)
                    {
                        if (GUI.Button(new Rect(130, y, 50, 18), "Accept"))
                            Network.NetworkManager.Instance?.Send(Network.PacketBuilder.FriendAccept(req.Name));
                        if (GUI.Button(new Rect(184, y, 50, 18), "Deny"))
                            Network.NetworkManager.Instance?.Send(Network.PacketBuilder.FriendDecline(req.Name));
                    }
                    else
                    {
                        GUI.Label(new Rect(130, y, 100, 20), "(sent)", DimLabel());
                    }
                    y += 22;
                }
                y += 8;
            }

            // Friends list sorted: online first
            var sorted = new List<FriendEntry>(_friends);
            sorted.Sort((a, b) => b.Online.CompareTo(a.Online));

            for (int i = 0; i < sorted.Count; i++)
            {
                var f = sorted[i];
                float fx = 4;

                // Online dot
                GUI.color = f.Online ? Color.green : new Color(0.4f, 0.4f, 0.4f);
                GUI.DrawTexture(new Rect(fx, y + 5, 8, 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
                fx += 14;

                // Name
                GUI.Label(new Rect(fx, y, 120, 18), f.Name, SmallLabel());
                fx += 124;

                // Zone (if online)
                if (f.Online && !string.IsNullOrEmpty(f.ZoneName))
                    GUI.Label(new Rect(fx, y, 100, 18), f.ZoneName, DimLabel());

                // Category badge
                string cat = CategoryNames[Mathf.Clamp(f.Category, 0, 2)];
                GUI.Label(new Rect(fx + 80, y, 60, 18), cat, DimLabel());

                // Right-click context menu
                Rect rowRect = new Rect(0, y, area.width - 16, 26);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                    rowRect.Contains(Event.current.mousePosition))
                {
                    _contextOpen = true;
                    _contextIndex = i;
                    _contextPos = Event.current.mousePosition + new Vector2(_windowPos.x, _windowPos.y);
                    Event.current.Use();
                }

                y += 26;
            }

            GUI.EndScrollView();
        }

        private void DrawBlockedTab(Rect area)
        {
            float totalH = _blocked.Count * 24f + 4;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos, new Rect(0, 0, area.width - 16, Mathf.Max(totalH, area.height)));

            float y = 0;
            foreach (var b in _blocked)
            {
                GUI.Label(new Rect(4, y, 150, 20), b.Name, SmallLabel());
                if (GUI.Button(new Rect(area.width - 80, y, 60, 18), "Unblock"))
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.FriendUnblock(b.Name));
                y += 22;
            }

            GUI.EndScrollView();
        }

        private void DrawRecentTab(Rect area)
        {
            float totalH = _recent.Count * 22f + 4;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos, new Rect(0, 0, area.width - 16, Mathf.Max(totalH, area.height)));

            float y = 0;
            foreach (var r in _recent)
            {
                GUI.Label(new Rect(4, y, 100, 18), r.Name, SmallLabel());
                GUI.Label(new Rect(108, y, 80, 18), r.InteractionType, DimLabel());
                string timeStr = r.TimeAgo < 60 ? $"{r.TimeAgo:F0}s ago" :
                    r.TimeAgo < 3600 ? $"{r.TimeAgo / 60:F0}m ago" : $"{r.TimeAgo / 3600:F0}h ago";
                GUI.Label(new Rect(192, y, 80, 18), timeStr, DimLabel());
                y += 20;
            }

            GUI.EndScrollView();
        }

        private void DrawContextMenu()
        {
            if (_contextIndex < 0 || _contextIndex >= _friends.Count)
            {
                _contextOpen = false;
                return;
            }

            string[] options = { "Whisper", "Invite to Party", "View Profile", "Set Note", "Remove Friend" };
            float menuW = 140f;
            float menuH = options.Length * 22f + 4;

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(_contextPos.x, _contextPos.y, menuW, menuH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var f = _friends[_contextIndex];

            for (int i = 0; i < options.Length; i++)
            {
                if (GUI.Button(new Rect(_contextPos.x + 2, _contextPos.y + 2 + i * 22, menuW - 4, 20), options[i], SmallLabel()))
                {
                    switch (i)
                    {
                        case 0: // Whisper
                            ChatUI.Instance?.ReceiveMessage("System", "System", $"Type /w {f.Name} <message>");
                            break;
                        case 1: // Invite to Party
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.PartyInvite(f.Name));
                            break;
                        case 2: // View Profile
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.PlayerProfileRequest(f.Name));
                            break;
                        case 3: // Set Note
                            // TODO: open note input
                            break;
                        case 4: // Remove
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.FriendRemove(f.Name));
                            break;
                    }
                    _contextOpen = false;
                }
            }

            // Close on click elsewhere
            if (Event.current.type == EventType.MouseDown && !new Rect(_contextPos.x, _contextPos.y, menuW, menuH).Contains(Event.current.mousePosition))
            {
                _contextOpen = false;
                Event.current.Use();
            }
        }

        // ---- Helpers ----

        private void HandleDrag(Rect titleBar)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - _windowPos;
                e.Use();
            }
            if (_dragging && e.type == EventType.MouseDrag)
            {
                _windowPos = e.mousePosition - _dragOffset;
                e.Use();
            }
            if (e.type == EventType.MouseUp) _dragging = false;
        }

        private GUIStyle TitleStyle() =>
            new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
        private GUIStyle SmallLabel() =>
            new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white }, wordWrap = false };
        private GUIStyle SmallLabelCentered() =>
            new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        private GUIStyle DimLabel() =>
            new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };
        private GUIStyle SectionHeader() =>
            new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.8f, 1f) } };
        private GUIStyle SmallInputStyle() =>
            new GUIStyle(GUI.skin.textField) { fontSize = 11, normal = { textColor = Color.white }, focused = { textColor = Color.white } };
    }
}

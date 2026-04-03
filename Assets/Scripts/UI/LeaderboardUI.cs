using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Leaderboard panel fetching from api.orlo.games/leaderboards/:boardId.
    /// Toggle with L key. Tabs for Combat, Crafting, Exploration, Wealth.
    /// Shows top 100 with rank, name, and score. Highlights current player.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class LeaderboardUI : MonoBehaviour
    {
        public static LeaderboardUI Instance { get; private set; }

        private bool _visible;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        private enum BoardTab { Combat, Crafting, Exploration, Wealth }
        private BoardTab _activeTab = BoardTab.Combat;

        // Board IDs map to API endpoint slugs
        private static readonly string[] BoardIds = { "combat_kills", "crafting_items", "exploration_distance", "economy_credits" };
        private static readonly string[] BoardLabels = { "Combat", "Crafting", "Exploration", "Wealth" };

        /// <summary>An entry on a leaderboard.</summary>
        public struct LeaderboardEntry
        {
            public int Rank;
            public string Name;
            public long Score;
            public bool IsLocalPlayer;
        }

        // Cached leaderboard data per tab
        private readonly Dictionary<BoardTab, List<LeaderboardEntry>> _boards = new();
        private readonly Dictionary<BoardTab, float> _fetchTimestamps = new();
        private BoardTab _pendingFetch = (BoardTab)(-1);
        private bool _fetching;
        private string _errorMessage;

        // Local player name for highlighting
        private string _localPlayerName = "";

        // Layout
        private const float WinW = 440f;
        private const float WinH = 520f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                _visible = !_visible;
                if (_visible) RequestBoardIfStale(_activeTab);
            }
        }

        /// <summary>Set local player name for row highlighting.</summary>
        public void SetLocalPlayerName(string name)
        {
            _localPlayerName = name ?? "";
        }

        /// <summary>
        /// Called by network handler when leaderboard data arrives from the API.
        /// </summary>
        public void SetBoardData(string boardId, List<LeaderboardEntry> entries)
        {
            _fetching = false;
            _errorMessage = null;

            for (int i = 0; i < BoardIds.Length; i++)
            {
                if (BoardIds[i] == boardId)
                {
                    var tab = (BoardTab)i;
                    _boards[tab] = entries;
                    _fetchTimestamps[tab] = Time.realtimeSinceStartup;
                    return;
                }
            }
        }

        /// <summary>Called when a leaderboard fetch fails.</summary>
        public void SetError(string error)
        {
            _fetching = false;
            _errorMessage = error;
        }

        private void RequestBoardIfStale(BoardTab tab)
        {
            // Re-fetch if data is older than 60 seconds
            if (_fetchTimestamps.TryGetValue(tab, out float ts) && Time.realtimeSinceStartup - ts < 60f)
                return;

            _fetching = true;
            _errorMessage = null;
            _pendingFetch = tab;

            // The actual HTTP fetch would be triggered here via a network manager.
            // For now, log intent. PacketHandler or a coroutine-based HTTP client
            // should call SetBoardData when the response arrives.
            Debug.Log($"[LeaderboardUI] Requesting leaderboard: {BoardIds[(int)tab]}");
        }

        // ── OnGUI ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;

            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            // Window background
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, WinW - 28, 28);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(_windowPos.x, _windowPos.y, WinW, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "Leaderboards", titleStyle);

            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            {
                _visible = false;
                return;
            }

            HandleDrag(titleBar);

            // Tab bar
            float tabY = _windowPos.y + 30;
            DrawTabBar(tabY);

            // Content
            float contentY = tabY + 30;
            Rect contentRect = new Rect(_windowPos.x + 8, contentY, WinW - 16, WinH - (contentY - _windowPos.y) - 8);

            GUI.color = new Color(0.1f, 0.1f, 0.14f, 1f);
            GUI.DrawTexture(contentRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(contentRect.x + 8, contentRect.y + 4, contentRect.width - 16, contentRect.height - 8));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (_fetching)
            {
                var loadStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
                };
                GUILayout.Space(40);
                GUILayout.Label("Loading...", loadStyle);
            }
            else if (!string.IsNullOrEmpty(_errorMessage))
            {
                var errStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.4f, 0.4f) },
                    wordWrap = true
                };
                GUILayout.Space(20);
                GUILayout.Label(_errorMessage, errStyle);
            }
            else if (_boards.TryGetValue(_activeTab, out var entries) && entries.Count > 0)
            {
                DrawBoardEntries(entries);
            }
            else
            {
                var emptyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                };
                GUILayout.Space(40);
                GUILayout.Label("No leaderboard data yet.", emptyStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTabBar(float y)
        {
            float tabW = WinW / BoardLabels.Length;

            for (int i = 0; i < BoardLabels.Length; i++)
            {
                bool active = (int)_activeTab == i;
                Rect tabRect = new Rect(_windowPos.x + i * tabW, y, tabW, 28);

                GUI.color = active
                    ? new Color(0.2f, 0.25f, 0.4f, 1f)
                    : new Color(0.1f, 0.1f, 0.14f, 1f);
                GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var tabStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = active ? Color.white : new Color(0.6f, 0.6f, 0.6f) }
                };

                if (GUI.Button(tabRect, BoardLabels[i], tabStyle))
                {
                    _activeTab = (BoardTab)i;
                    _scrollPos = Vector2.zero;
                    RequestBoardIfStale(_activeTab);
                }
            }
        }

        private void DrawBoardEntries(List<LeaderboardEntry> entries)
        {
            // Header row
            GUILayout.BeginHorizontal();
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.8f, 1f) }
            };
            GUILayout.Label("Rank", headerStyle, GUILayout.Width(50));
            GUILayout.Label("Player", headerStyle, GUILayout.Width(220));
            GUILayout.Label("Score", headerStyle, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            // Separator
            Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Entries
            foreach (var entry in entries)
            {
                bool isLocal = entry.IsLocalPlayer ||
                    (!string.IsNullOrEmpty(_localPlayerName) && entry.Name == _localPlayerName);

                Color rowColor = isLocal ? new Color(0.9f, 0.85f, 0.3f) : new Color(0.8f, 0.8f, 0.8f);

                // Highlight background for local player
                if (isLocal)
                {
                    Rect rowBg = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(0));
                    GUI.color = new Color(0.2f, 0.18f, 0.05f, 0.6f);
                    GUI.DrawTexture(new Rect(rowBg.x, rowBg.y, rowBg.width, 20), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                GUILayout.BeginHorizontal();
                var rowStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = isLocal ? FontStyle.Bold : FontStyle.Normal,
                    normal = { textColor = rowColor }
                };

                // Top 3 get special rank colors
                Color rankColor = entry.Rank switch
                {
                    1 => new Color(1f, 0.85f, 0.2f),  // gold
                    2 => new Color(0.75f, 0.75f, 0.8f), // silver
                    3 => new Color(0.8f, 0.5f, 0.2f),  // bronze
                    _ => rowColor
                };
                var rankStyle = new GUIStyle(rowStyle) { normal = { textColor = rankColor } };

                GUILayout.Label($"#{entry.Rank}", rankStyle, GUILayout.Width(50));
                GUILayout.Label(entry.Name, rowStyle, GUILayout.Width(220));
                GUILayout.Label(FormatScore(entry.Score), rowStyle, GUILayout.Width(100));
                GUILayout.EndHorizontal();
            }
        }

        private static string FormatScore(long score)
        {
            if (score >= 1_000_000) return $"{score / 1_000_000f:F1}M";
            if (score >= 1_000) return $"{score / 1_000f:F1}K";
            return score.ToString();
        }

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
            if (e.type == EventType.MouseUp)
                _dragging = false;
        }
    }
}

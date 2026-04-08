using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// LFG (Looking For Group) board UI. Opens at board objects or via /lfg command.
    /// Browse listings, post your own, apply to groups.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class LFGBoardUI : MonoBehaviour
    {
        public static LFGBoardUI Instance { get; private set; }

        private enum View { Browse, Post }

        private bool _visible;
        private View _activeView = View.Browse;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        private const float WinW = 440f;
        private const float WinH = 380f;

        // ---- Data ----

        public struct LFGListing
        {
            public ulong ListingId;
            public string Author;
            public string Activity;
            public string Description;
            public int MinLevel;
            public int MaxLevel;
            public int CurrentSize;
            public int MaxSize;
            public bool IsOwn;
        }

        private List<LFGListing> _listings = new List<LFGListing>();

        // Filters
        private int _filterActivity; // 0=All, 1=Dungeon, 2=PvP, 3=Crafting, 4=Exploration
        private static readonly string[] ActivityNames = { "All", "Dungeon", "PvP", "Crafting", "Exploration" };

        // Post form
        private int _postActivity = 1;
        private string _postDescription = "";
        private int _postMaxSize = 4;
        private int _postMinLevel = 1;
        private int _postMaxLevel = 50;
        private string _postMaxSizeStr = "4";
        private string _postMinLevelStr = "1";
        private string _postMaxLevelStr = "50";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible)
            {
                _activeView = View.Browse;
                Network.NetworkManager.Instance?.Send(Network.PacketBuilder.LFGBoardRequest());
            }
        }

        public void Show() { _visible = true; }

        // ---- Public API ----

        public void SetListings(List<LFGListing> listings) { _listings = listings ?? new List<LFGListing>(); }

        // ---- OnGUI ----

        private void OnGUI()
        {
            if (!_visible) return;

            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, WinW - 28, 28);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(_windowPos.x, _windowPos.y, WinW, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "LFG Board", TitleStyle());
            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            { _visible = false; return; }
            HandleDrag(titleBar);

            float cy = _windowPos.y + 32;
            float cx = _windowPos.x + 8;
            float cw = WinW - 16;

            // View toggle
            float tabW = cw / 2f;
            View[] views = { View.Browse, View.Post };
            string[] viewLabels = { "Browse", "Post Listing" };
            for (int i = 0; i < views.Length; i++)
            {
                bool sel = _activeView == views[i];
                GUI.color = sel ? new Color(0.2f, 0.3f, 0.5f, 0.9f) : new Color(0.12f, 0.12f, 0.18f, 0.9f);
                GUI.DrawTexture(new Rect(cx + i * tabW, cy, tabW - 2, 22), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(cx + i * tabW, cy, tabW - 2, 22), viewLabels[i], SmallCentered()))
                    _activeView = views[i];
            }
            cy += 26;

            Rect contentRect = new Rect(cx, cy, cw, WinH - (cy - _windowPos.y) - 8);

            if (_activeView == View.Browse)
                DrawBrowse(contentRect);
            else
                DrawPost(contentRect);

            GUI.color = Color.white;
        }

        private void DrawBrowse(Rect area)
        {
            float y = area.y;
            float x = area.x;
            float w = area.width;

            // Activity filter
            GUI.Label(new Rect(x, y, 50, 18), "Filter:", SmallLabel());
            float filterX = x + 54;
            for (int i = 0; i < ActivityNames.Length; i++)
            {
                bool sel = _filterActivity == i;
                GUI.color = sel ? new Color(0.3f, 0.5f, 0.7f, 0.9f) : new Color(0.15f, 0.15f, 0.2f, 0.8f);
                float bw = 65f;
                GUI.DrawTexture(new Rect(filterX, y, bw, 18), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(filterX, y, bw, 18), ActivityNames[i], SmallCentered()))
                    _filterActivity = i;
                filterX += bw + 2;
            }
            y += 22;

            // Listings
            float listH = area.height - (y - area.y);
            var filtered = new List<LFGListing>();
            foreach (var l in _listings)
            {
                if (_filterActivity == 0 || l.Activity == ActivityNames[_filterActivity])
                    filtered.Add(l);
            }

            float totalH = filtered.Count * 60f;
            _scrollPos = GUI.BeginScrollView(new Rect(x, y, w, listH), _scrollPos,
                new Rect(0, 0, w - 16, Mathf.Max(totalH, listH)));

            float ly = 0;
            foreach (var listing in filtered)
            {
                // Card background
                GUI.color = new Color(0.1f, 0.1f, 0.14f, 0.9f);
                GUI.DrawTexture(new Rect(0, ly, w - 20, 54), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Activity badge
                GUI.color = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                GUI.DrawTexture(new Rect(4, ly + 4, 60, 16), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(4, ly + 4, 60, 16), listing.Activity, SmallCentered());

                // Author + size
                GUI.Label(new Rect(70, ly + 4, 120, 16), listing.Author, SmallLabel());
                GUI.Label(new Rect(w - 100, ly + 4, 70, 16), $"{listing.CurrentSize}/{listing.MaxSize}", DimLabel());

                // Description
                GUI.Label(new Rect(4, ly + 22, w - 90, 16), listing.Description, DimLabel());

                // Level range
                GUI.Label(new Rect(4, ly + 38, 100, 14), $"Lv {listing.MinLevel}-{listing.MaxLevel}", DimLabel());

                // Apply / Remove button
                if (listing.IsOwn)
                {
                    if (GUI.Button(new Rect(w - 80, ly + 30, 60, 20), "Remove"))
                        Network.NetworkManager.Instance?.Send(Network.PacketBuilder.RemoveLFG(listing.ListingId));
                }
                else
                {
                    if (GUI.Button(new Rect(w - 80, ly + 30, 60, 20), "Apply"))
                        Network.NetworkManager.Instance?.Send(Network.PacketBuilder.ApplyLFG(listing.ListingId));
                }

                ly += 58;
            }

            GUI.EndScrollView();
        }

        private void DrawPost(Rect area)
        {
            float y = area.y + 4;
            float x = area.x + 4;
            float w = area.width - 8;

            // Activity
            GUI.Label(new Rect(x, y, 80, 20), "Activity:", SmallLabel());
            float actX = x + 84;
            for (int i = 1; i < ActivityNames.Length; i++)
            {
                bool sel = _postActivity == i;
                GUI.color = sel ? new Color(0.3f, 0.5f, 0.7f, 0.9f) : new Color(0.15f, 0.15f, 0.2f, 0.8f);
                GUI.DrawTexture(new Rect(actX, y, 65, 20), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(actX, y, 65, 20), ActivityNames[i], SmallCentered()))
                    _postActivity = i;
                actX += 68;
            }
            y += 26;

            // Description
            GUI.Label(new Rect(x, y, 80, 20), "Description:", SmallLabel());
            y += 20;
            _postDescription = GUI.TextArea(new Rect(x, y, w, 60), _postDescription, 200);
            y += 64;

            // Party size
            GUI.Label(new Rect(x, y, 80, 20), "Max Size:", SmallLabel());
            _postMaxSizeStr = GUI.TextField(new Rect(x + 84, y, 40, 20), _postMaxSizeStr, SmallInputStyle());
            int.TryParse(_postMaxSizeStr, out _postMaxSize);
            y += 24;

            // Level range
            GUI.Label(new Rect(x, y, 80, 20), "Level:", SmallLabel());
            _postMinLevelStr = GUI.TextField(new Rect(x + 84, y, 40, 20), _postMinLevelStr, SmallInputStyle());
            GUI.Label(new Rect(x + 128, y, 10, 20), "-", SmallLabel());
            _postMaxLevelStr = GUI.TextField(new Rect(x + 142, y, 40, 20), _postMaxLevelStr, SmallInputStyle());
            int.TryParse(_postMinLevelStr, out _postMinLevel);
            int.TryParse(_postMaxLevelStr, out _postMaxLevel);
            y += 30;

            // Post button
            GUI.enabled = !string.IsNullOrEmpty(_postDescription);
            if (GUI.Button(new Rect(x, y, 100, 26), "Post Listing"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.PostLFG(ActivityNames[_postActivity], _postDescription,
                        _postMaxSize, _postMinLevel, _postMaxLevel));
                _postDescription = "";
                _activeView = View.Browse;
                ChatUI.Instance?.AddSystemMessage("LFG listing posted!");
            }
            GUI.enabled = true;
        }

        // ---- Helpers ----

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
        private GUIStyle SmallInputStyle() => new GUIStyle(GUI.skin.textField) { fontSize = 11, normal = { textColor = Color.white }, focused = { textColor = Color.white } };
    }
}

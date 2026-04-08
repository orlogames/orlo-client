using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Settlement bulletin board with 7 category tabs.
    /// Opens at board objects in settlements.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class BulletinBoardUI : MonoBehaviour
    {
        public static BulletinBoardUI Instance { get; private set; }

        private enum BoardTab { LFG, Trade, GuildRecruit, Events, Bounty, Mentor, City }
        private enum View { List, Detail, Post }

        private bool _visible;
        private BoardTab _activeTab = BoardTab.LFG;
        private View _activeView = View.List;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        private const float WinW = 500f;
        private const float WinH = 420f;

        // ---- Data ----

        public struct BulletinPost
        {
            public ulong PostId;
            public string Title;
            public string Author;
            public string Body;
            public string Date;
            public string Category;
            public string ContactInfo;
            public bool IsOwn;
        }

        private List<BulletinPost> _posts = new List<BulletinPost>();
        private int _selectedPost = -1;

        // Post form
        private string _postTitle = "";
        private string _postBody = "";

        private static readonly string[] TabNames = { "LFG", "Trade", "Guild", "Events", "Bounty", "Mentor", "City" };

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
                _activeView = View.List;
                RequestBoard();
            }
        }

        public void Show() { _visible = true; _activeView = View.List; RequestBoard(); }

        // ---- Public API ----

        public void SetPosts(List<BulletinPost> posts) { _posts = posts ?? new List<BulletinPost>(); }

        private void RequestBoard()
        {
            Network.NetworkManager.Instance?.Send(
                Network.PacketBuilder.BulletinBoardRequest(TabNames[(int)_activeTab]));
        }

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

            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "Bulletin Board", TitleStyle());
            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            { _visible = false; return; }
            HandleDrag(titleBar);

            float cy = _windowPos.y + 32;
            float cx = _windowPos.x + 8;
            float cw = WinW - 16;

            // Category tabs
            float tabW = cw / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                bool sel = (int)_activeTab == i;
                GUI.color = sel ? new Color(0.2f, 0.3f, 0.5f, 0.9f) : new Color(0.12f, 0.12f, 0.18f, 0.9f);
                GUI.DrawTexture(new Rect(cx + i * tabW, cy, tabW - 2, 20), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(cx + i * tabW, cy, tabW - 2, 20), TabNames[i], TabStyle()))
                {
                    _activeTab = (BoardTab)i;
                    _activeView = View.List;
                    RequestBoard();
                }
            }
            cy += 24;

            Rect contentRect = new Rect(cx, cy, cw, WinH - (cy - _windowPos.y) - 8);

            switch (_activeView)
            {
                case View.List: DrawList(contentRect); break;
                case View.Detail: DrawDetail(contentRect); break;
                case View.Post: DrawPostForm(contentRect); break;
            }

            GUI.color = Color.white;
        }

        private void DrawList(Rect area)
        {
            float y = area.y;
            float x = area.x;
            float w = area.width;

            // Post button
            if (GUI.Button(new Rect(x + w - 80, y, 80, 20), "New Post"))
            {
                _activeView = View.Post;
                _postTitle = "";
                _postBody = "";
            }
            y += 24;

            float listH = area.height - 28;
            float totalH = _posts.Count * 32f;
            _scrollPos = GUI.BeginScrollView(new Rect(x, y, w, listH), _scrollPos,
                new Rect(0, 0, w - 16, Mathf.Max(totalH, listH)));

            float ly = 0;
            for (int i = 0; i < _posts.Count; i++)
            {
                var post = _posts[i];
                Rect row = new Rect(0, ly, w - 20, 28);

                bool hover = row.Contains(Event.current.mousePosition);
                GUI.color = hover ? new Color(0.15f, 0.2f, 0.3f, 0.8f) : new Color(0.1f, 0.1f, 0.12f, 0.5f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUI.Label(new Rect(4, ly + 4, 200, 18), post.Title, SmallLabel());
                GUI.Label(new Rect(210, ly + 4, 80, 18), post.Author, DimLabel());
                GUI.Label(new Rect(w - 80, ly + 4, 60, 18), post.Date, DimLabel());

                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    _selectedPost = i;
                    _activeView = View.Detail;
                    Event.current.Use();
                }

                ly += 30;
            }

            GUI.EndScrollView();
        }

        private void DrawDetail(Rect area)
        {
            if (_selectedPost < 0 || _selectedPost >= _posts.Count)
            {
                _activeView = View.List;
                return;
            }

            var post = _posts[_selectedPost];
            float y = area.y + 4;
            float x = area.x + 4;
            float w = area.width - 8;

            if (GUI.Button(new Rect(x, y, 60, 20), "< Back"))
            {
                _activeView = View.List;
                return;
            }
            y += 24;

            GUI.Label(new Rect(x, y, w, 22), post.Title, BoldStyle());
            y += 24;

            GUI.Label(new Rect(x, y, 200, 18), $"By: {post.Author}", DimLabel());
            GUI.Label(new Rect(x + 200, y, 200, 18), post.Date, DimLabel());
            y += 22;

            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 4;

            var bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            float bodyH = bodyStyle.CalcHeight(new GUIContent(post.Body ?? ""), w);
            GUI.Label(new Rect(x, y, w, bodyH), post.Body ?? "", bodyStyle);
            y += bodyH + 8;

            if (!string.IsNullOrEmpty(post.ContactInfo))
            {
                GUI.Label(new Rect(x, y, w, 18), $"Contact: {post.ContactInfo}", DimLabel());
                y += 20;
            }

            // Remove own post
            if (post.IsOwn)
            {
                if (GUI.Button(new Rect(x, area.y + area.height - 30, 100, 24), "Remove Post"))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.RemoveBulletin(post.PostId));
                    _activeView = View.List;
                }
            }
        }

        private void DrawPostForm(Rect area)
        {
            float y = area.y + 4;
            float x = area.x + 4;
            float w = area.width - 8;

            if (GUI.Button(new Rect(x, y, 60, 20), "< Back"))
            {
                _activeView = View.List;
                return;
            }
            y += 28;

            GUI.Label(new Rect(x, y, 50, 20), "Title:", SmallLabel());
            _postTitle = GUI.TextField(new Rect(x + 54, y, w - 54, 20), _postTitle, SmallInputStyle());
            y += 24;

            GUI.Label(new Rect(x, y, 50, 20), "Body:", SmallLabel());
            y += 20;
            _postBody = GUI.TextArea(new Rect(x, y, w, 140), _postBody, 500);
            y += 148;

            GUI.Label(new Rect(x, y, 200, 18), $"Category: {TabNames[(int)_activeTab]}", DimLabel());
            y += 24;

            GUI.enabled = !string.IsNullOrEmpty(_postTitle) && !string.IsNullOrEmpty(_postBody);
            if (GUI.Button(new Rect(x, y, 100, 26), "Post"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.PostBulletin(TabNames[(int)_activeTab], _postTitle, _postBody));
                _activeView = View.List;
                ChatUI.Instance?.AddSystemMessage("Bulletin posted!");
                RequestBoard();
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
        private GUIStyle TabStyle() => new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        private GUIStyle SmallLabel() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
        private GUIStyle SmallCentered() => new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        private GUIStyle DimLabel() => new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };
        private GUIStyle BoldStyle() => new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        private GUIStyle SmallInputStyle() => new GUIStyle(GUI.skin.textField) { fontSize = 11, normal = { textColor = Color.white }, focused = { textColor = Color.white } };
    }
}

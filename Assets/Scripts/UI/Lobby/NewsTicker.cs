using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI.Lobby
{
    public struct NewsItemData
    {
        public string category;
        public string text;
    }

    public class NewsTicker : MonoBehaviour
    {
        public static NewsTicker Instance { get; private set; }

        private readonly List<NewsItemData> _items = new List<NewsItemData>();
        private float _scrollOffset;
        private bool _visible = true;
        private const float ScrollSpeed = 40f;
        private const float BarHeight = 32f;
        private const string Divider = " \u2014 ";

        private GUIStyle _textStyle;
        private bool _stylesInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_items.Count == 0) LoadDefaults();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LoadDefaults()
        {
            _items.Add(new NewsItemData { category = "event", text = "Welcome to Orlo \u2014 The frontier awaits" });
            _items.Add(new NewsItemData { category = "patch", text = "Build v0.0.97 \u2014 Combat feedback and chat systems" });
            _items.Add(new NewsItemData { category = "announcement", text = "Early access coming soon" });
        }

        public void AddNews(string category, string text)
        {
            _items.Add(new NewsItemData { category = category, text = text });
        }

        public void ClearNews()
        {
            _items.Clear();
            _scrollOffset = 0f;
        }

        public void SetItems(List<NewsItemData> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _scrollOffset = 0f;
        }

        public void Show() { _visible = true; }
        public void Hide() { _visible = false; }

        private void InitStyles()
        {
            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            _stylesInitialized = true;
        }

        private Color GetCategoryColor(string category)
        {
            switch (category)
            {
                case "event": return new Color(1f, 0.84f, 0f);
                case "patch": return new Color(0f, 0.9f, 1f);
                default: return Color.white;
            }
        }

        private void Update()
        {
            if (_visible && _items.Count > 0)
                _scrollOffset += ScrollSpeed * Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!_visible || _items.Count == 0) return;
            if (!_stylesInitialized) InitStyles();

            float y = Screen.height - BarHeight;
            GUI.DrawTexture(new Rect(0, y, Screen.width, BarHeight), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, new Color(0.03f, 0.03f, 0.06f, 0.85f), 0, 0);

            string fullText = "";
            var colors = new List<(int start, int length, Color color)>();
            foreach (var item in _items)
            {
                if (fullText.Length > 0) fullText += Divider;
                int dotStart = fullText.Length;
                fullText += "\u25CF ";
                colors.Add((dotStart, 2, GetCategoryColor(item.category)));
                fullText += item.text;
            }

            float textWidth = _textStyle.CalcSize(new GUIContent(fullText + Divider)).x;
            if (textWidth < 1f) return;

            float offset = _scrollOffset % textWidth;
            float startX = Screen.width - offset;

            GUI.BeginGroup(new Rect(0, y, Screen.width, BarHeight));
            for (float x = startX; x < Screen.width + textWidth; x += textWidth)
            {
                GUI.Label(new Rect(x, 0, textWidth + 100, BarHeight), fullText, _textStyle);
            }
            GUI.EndGroup();
        }
    }
}

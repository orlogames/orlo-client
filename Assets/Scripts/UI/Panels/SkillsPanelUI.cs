using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orlo.UI.Panels
{
    /// <summary>
    /// Skills overview panel. Toggle with K key.
    /// Shows all skills grouped by category with XP progress, milestones, and recent gains.
    /// Uses OnGUI immediate mode with 1x1 white pixel rendering.
    /// </summary>
    public class SkillsPanelUI : MonoBehaviour
    {
        public static SkillsPanelUI Instance { get; private set; }

        // ---- Data Types ----

        public struct MilestoneData
        {
            public int Rank;
            public string UnlockName;
            public bool Unlocked;
        }

        public struct SkillData
        {
            public string Name;
            public string Category;
            public string Description;
            public int Rank;
            public int XpCurrent;
            public int XpToNext;
            public int TotalXp;
            public float LastXpTime;       // Time.time of last XP gain
            public string[] RecentGains;   // last 4 XP event descriptions
            public MilestoneData[] Milestones;
        }

        // ---- Constants ----

        private const float WinW = 720f;
        private const float WinH = 520f;
        private const float RowH = 56f;
        private const float ExpandedH = 140f;
        private const float HeaderBarH = 36f;
        private const float FilterBarH = 28f;
        private const float IconSize = 40f;
        private const float BarW = 140f;
        private const float BarH = 6f;
        private const int MaxRank = 100;
        private const int SkillCount = 16;
        private const int TheoreticalMax = SkillCount * MaxRank;
        private const float RecentXpWindow = 600f; // 10 min in seconds

        // Colors
        private static readonly Color BgColor = new Color(0.06f, 0.06f, 0.1f, 0.92f);
        private static readonly Color BorderColor = new Color(0.15f, 0.2f, 0.35f, 1f);
        private static readonly Color HeaderBg = new Color(0.08f, 0.08f, 0.14f, 1f);
        private static readonly Color CyanAccent = new Color(0.35f, 0.6f, 0.95f, 1f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color DimText = new Color(0.4f, 0.4f, 0.5f, 1f);
        private static readonly Color White = Color.white;
        private static readonly Color RowHover = new Color(0.1f, 0.12f, 0.2f, 0.6f);
        private static readonly Color RowAlt = new Color(0.07f, 0.07f, 0.12f, 0.4f);
        private static readonly Color ExpandedBg = new Color(0.05f, 0.05f, 0.09f, 0.8f);
        private static readonly Color DimCyan = new Color(0.2f, 0.35f, 0.55f, 0.5f);
        private static readonly Color GoldBar = new Color(1f, 0.85f, 0.2f, 0.8f);
        private static readonly Color CategoryHeaderColor = new Color(0.25f, 0.3f, 0.45f, 1f);

        // Categories
        private static readonly string[] Categories = { "ALL", "COMBAT", "CRAFTING", "SURVIVAL", "SOCIAL", "PILOT" };
        private static readonly string[] SortModes = { "RANK", "RECENT", "NAME" };

        private static readonly Dictionary<string, Color> CategoryColors = new Dictionary<string, Color>
        {
            { "COMBAT",   new Color(0.9f, 0.3f, 0.3f, 1f) },
            { "CRAFTING", new Color(0.3f, 0.8f, 0.4f, 1f) },
            { "SURVIVAL", new Color(0.85f, 0.7f, 0.2f, 1f) },
            { "SOCIAL",   new Color(0.5f, 0.6f, 0.95f, 1f) },
            { "PILOT",    new Color(0.4f, 0.85f, 0.9f, 1f) },
        };

        private static readonly string[] CategoryOrder = { "COMBAT", "CRAFTING", "SURVIVAL", "SOCIAL", "PILOT" };

        // ---- State ----

        private bool _visible;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;
        private int _activeCategory;    // index into Categories
        private int _activeSortMode;    // index into SortModes
        private int _expandedIndex = -1; // index into filtered list, -1 = none
        private float _sessionStartTime;

        private SkillData[] _skills = Array.Empty<SkillData>();
        private Texture2D _pixel;

        // ---- GUIStyles (lazy) ----

        private GUIStyle _titleStyle, _smallStyle, _tinyStyle, _boldSmall, _centeredSmall;
        private bool _stylesInit;

        // ---- Lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
            _sessionStartTime = Time.time;
            _pixel = Texture2D.whiteTexture;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.K) && !(ChatUI.Instance != null && ChatUI.Instance.IsInputActive))
                Toggle();
            if (_visible && Input.GetKeyDown(KeyCode.Escape))
                _visible = false;
        }

        // ---- Public API ----

        public void Toggle() { _visible = !_visible; }

        public void SetSkills(SkillData[] skills)
        {
            _skills = skills ?? Array.Empty<SkillData>();
        }

        // ---- OnGUI ----

        private void OnGUI()
        {
            if (!_visible || _skills.Length == 0) return;
            EnsureStyles();

            Rect win = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            // Background
            DrawRect(win, BgColor);
            // Border (1px)
            DrawBorder(win, BorderColor, 1f);

            // Header bar
            Rect header = new Rect(win.x, win.y, WinW, HeaderBarH);
            DrawRect(header, HeaderBg);

            // Title
            GUI.color = White;
            GUI.Label(new Rect(win.x + 12, win.y + 6, 120, 24), "SKILLS", _titleStyle);

            // Mastered count
            int totalRanks = 0;
            foreach (var s in _skills) totalRanks += s.Rank;
            string masteredText = $"{totalRanks} / {TheoreticalMax} MASTERED";
            GUI.color = Gold;
            GUI.Label(new Rect(win.x + WinW - 220, win.y + 8, 190, 20), masteredText, _boldSmall);
            GUI.color = White;

            // Close button
            if (GUI.Button(new Rect(win.x + WinW - 28, win.y + 6, 22, 22), "X"))
            { _visible = false; return; }

            // Drag
            HandleDrag(header);

            float cy = win.y + HeaderBarH + 2;

            // Filter bar
            DrawFilterBar(win.x, cy, WinW);
            cy += FilterBarH + 4;

            // Content area
            float contentH = WinH - (cy - win.y) - 4;
            Rect contentArea = new Rect(win.x, cy, WinW, contentH);

            // Build filtered + sorted list
            var filtered = BuildFilteredList();

            // Calculate total scroll height
            float totalH = 0f;
            if (_activeCategory == 0) // ALL mode — grouped
            {
                foreach (var cat in CategoryOrder)
                {
                    bool hasAny = false;
                    for (int i = 0; i < filtered.Count; i++)
                    {
                        if (string.Equals(filtered[i].Category, cat, StringComparison.OrdinalIgnoreCase))
                        { hasAny = true; break; }
                    }
                    if (hasAny) totalH += 24f; // category header
                    for (int i = 0; i < filtered.Count; i++)
                    {
                        if (!string.Equals(filtered[i].Category, cat, StringComparison.OrdinalIgnoreCase)) continue;
                        totalH += RowH;
                        if (_expandedIndex == i) totalH += ExpandedH;
                    }
                }
            }
            else
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    totalH += RowH;
                    if (_expandedIndex == i) totalH += ExpandedH;
                }
            }

            // Scroll view
            Rect scrollContent = new Rect(0, 0, WinW - 18, totalH);
            _scrollPos = GUI.BeginScrollView(contentArea, _scrollPos, scrollContent);

            float drawY = 0f;
            if (_activeCategory == 0)
            {
                // ALL: grouped by category
                foreach (var cat in CategoryOrder)
                {
                    bool hasAny = false;
                    for (int i = 0; i < filtered.Count; i++)
                    {
                        if (string.Equals(filtered[i].Category, cat, StringComparison.OrdinalIgnoreCase))
                        { hasAny = true; break; }
                    }
                    if (!hasAny) continue;

                    // Category header
                    DrawCategoryHeader(drawY, cat, WinW - 18);
                    drawY += 24f;

                    for (int i = 0; i < filtered.Count; i++)
                    {
                        if (!string.Equals(filtered[i].Category, cat, StringComparison.OrdinalIgnoreCase)) continue;
                        DrawSkillRow(i, filtered[i], drawY, WinW - 18);
                        drawY += RowH;
                        if (_expandedIndex == i)
                        {
                            DrawExpandedDetail(i, filtered[i], drawY, WinW - 18);
                            drawY += ExpandedH;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    DrawSkillRow(i, filtered[i], drawY, WinW - 18);
                    drawY += RowH;
                    if (_expandedIndex == i)
                    {
                        DrawExpandedDetail(i, filtered[i], drawY, WinW - 18);
                        drawY += ExpandedH;
                    }
                }
            }

            GUI.EndScrollView();
        }

        // ---- Filter Bar ----

        private void DrawFilterBar(float x, float y, float w)
        {
            DrawRect(new Rect(x, y, w, FilterBarH), new Color(0.07f, 0.07f, 0.12f, 0.8f));

            float tabX = x + 8;
            for (int i = 0; i < Categories.Length; i++)
            {
                float tabW = _smallStyle.CalcSize(new GUIContent(Categories[i])).x + 16;
                bool active = _activeCategory == i;

                GUI.color = active ? White : DimText;
                if (GUI.Button(new Rect(tabX, y + 2, tabW, FilterBarH - 4), Categories[i], _centeredSmall))
                {
                    _activeCategory = i;
                    _expandedIndex = -1;
                    _scrollPos = Vector2.zero;
                }

                // Active underline
                if (active)
                {
                    GUI.color = CyanAccent;
                    DrawRect(new Rect(tabX + 2, y + FilterBarH - 3, tabW - 4, 2f), CyanAccent);
                }

                tabX += tabW + 2;
            }

            // Sort button (right side)
            string sortLabel = $"SORT: {SortModes[_activeSortMode]}";
            float sortW = _smallStyle.CalcSize(new GUIContent(sortLabel)).x + 12;
            GUI.color = DimText;
            if (GUI.Button(new Rect(x + w - sortW - 12, y + 4, sortW, FilterBarH - 8), sortLabel, _smallStyle))
            {
                _activeSortMode = (_activeSortMode + 1) % SortModes.Length;
                _expandedIndex = -1;
            }
            GUI.color = White;
        }

        // ---- Category Header (ALL mode) ----

        private void DrawCategoryHeader(float y, string category, float w)
        {
            string dashes = new string('\u2500', 6);
            string label = $"{dashes} {category} {dashes}";
            GUI.color = CategoryHeaderColor;
            GUI.Label(new Rect(12, y + 3, w, 20), label, _centeredSmall);
            GUI.color = White;
        }

        // ---- Skill Row ----

        private void DrawSkillRow(int index, SkillData skill, float y, float w)
        {
            Rect rowRect = new Rect(0, y, w, RowH);
            bool mastered = skill.Rank >= MaxRank;
            bool dormant = skill.Rank == 0 && skill.TotalXp == 0;
            bool hovered = rowRect.Contains(Event.current.mousePosition);

            // Row background
            Color rowBg = hovered ? RowHover : (index % 2 == 0 ? RowAlt : Color.clear);
            if (rowBg.a > 0) DrawRect(rowRect, rowBg);

            // Left edge stripe (3px)
            float timeSinceXp = Time.time - skill.LastXpTime;
            if (timeSinceXp < RecentXpWindow && skill.LastXpTime > 0)
            {
                DrawRect(new Rect(0, y, 3, RowH), CyanAccent);
            }
            else if (skill.LastXpTime >= _sessionStartTime && skill.LastXpTime > 0)
            {
                DrawRect(new Rect(0, y, 3, RowH), DimCyan);
            }

            // Icon area (colored square with letter)
            float iconX = 8f;
            float iconY = y + (RowH - IconSize) / 2f;
            Color iconColor = GetCategoryColor(skill.Category);
            float iconAlpha = dormant ? 0.3f : 1f;
            GUI.color = new Color(iconColor.r, iconColor.g, iconColor.b, iconAlpha * 0.6f);
            DrawRect(new Rect(iconX, iconY, IconSize, IconSize), GUI.color);

            if (mastered)
            {
                // Gold border for mastered
                GUI.color = Gold;
                DrawBorder(new Rect(iconX, iconY, IconSize, IconSize), Gold, 2f);
            }

            GUI.color = new Color(1f, 1f, 1f, iconAlpha);
            string letter = skill.Name.Length > 0 ? skill.Name.Substring(0, 1) : "?";
            var letterStyle = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            GUI.Label(new Rect(iconX, iconY, IconSize, IconSize), letter, letterStyle);
            GUI.color = White;

            // Skill name + description
            float textX = iconX + IconSize + 10;
            float textW = w - textX - 210;

            Color nameColor = dormant ? DimText : White;
            GUI.color = nameColor;
            GUI.Label(new Rect(textX, y + 8, textW, 20), skill.Name, _boldSmall);

            GUI.color = DimText;
            string desc = skill.Description ?? "";
            if (desc.Length > 60) desc = desc.Substring(0, 57) + "...";
            GUI.Label(new Rect(textX, y + 26, textW, 18), desc, _tinyStyle);

            // Right side: rank + XP bar
            float rightX = w - 200;

            // Rank text
            string rankText = mastered ? "MASTERED" : $"Rank {skill.Rank}";
            Color rankColor = mastered ? Gold : (skill.Rank > 0 ? Gold : DimText);
            GUI.color = rankColor;
            GUI.Label(new Rect(rightX, y + 8, 70, 20), rankText, _smallStyle);

            // XP bar
            float barX = rightX + 72;
            float barY = y + 14;

            if (dormant)
            {
                GUI.color = DimText;
                GUI.Label(new Rect(barX, y + 8, BarW, 20), "\u2014", _smallStyle);
            }
            else
            {
                // Bar background
                GUI.color = new Color(0.15f, 0.15f, 0.25f, 0.8f);
                DrawRect(new Rect(barX, barY, BarW, BarH), GUI.color);

                // Bar fill
                float pct = skill.XpToNext > 0 ? Mathf.Clamp01((float)skill.XpCurrent / skill.XpToNext) : 1f;
                Color barColor = mastered ? GoldBar : CyanAccent;
                GUI.color = barColor;
                DrawRect(new Rect(barX, barY, BarW * pct, BarH), GUI.color);

                // Percentage
                string pctText = mastered ? "100%" : $"{Mathf.RoundToInt(pct * 100)}%";
                GUI.color = new Color(0.7f, 0.7f, 0.8f, 1f);
                GUI.Label(new Rect(barX + BarW + 6, y + 8, 42, 20), pctText, _tinyStyle);
            }

            GUI.color = White;

            // Click to expand/collapse
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                _expandedIndex = (_expandedIndex == index) ? -1 : index;
                Event.current.Use();
            }
        }

        // ---- Expanded Detail ----

        private void DrawExpandedDetail(int index, SkillData skill, float y, float w)
        {
            Rect area = new Rect(4, y, w - 8, ExpandedH);
            DrawRect(area, ExpandedBg);
            DrawBorder(area, new Color(CyanAccent.r, CyanAccent.g, CyanAccent.b, 0.3f), 1f);

            float pad = 10f;
            float innerX = area.x + pad;
            float innerY = area.y + pad;
            float halfW = (area.width - pad * 3) / 2f;

            // Top line: exact XP
            string xpLeft = skill.Rank >= MaxRank
                ? "XP: MAX"
                : $"XP: {skill.XpCurrent:N0} / {skill.XpToNext:N0} to Rank {skill.Rank + 1}";
            GUI.color = White;
            GUI.Label(new Rect(innerX, innerY, halfW, 16), xpLeft, _smallStyle);

            GUI.color = DimText;
            string xpRight = $"Total XP: {skill.TotalXp:N0}";
            var rightStyle = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(innerX + halfW + pad, innerY, halfW, 16), xpRight, rightStyle);
            GUI.color = White;

            float colY = innerY + 22;

            // Left column: Milestones
            GUI.color = CyanAccent;
            GUI.Label(new Rect(innerX, colY, halfW, 16), "Rank Milestones", _boldSmall);
            GUI.color = White;
            colY += 18;

            var milestones = skill.Milestones;
            int mCount = milestones != null ? Mathf.Min(milestones.Length, 4) : 0;
            for (int m = 0; m < mCount; m++)
            {
                var ms = milestones[m];
                string icon;
                Color iconCol;
                if (ms.Unlocked)
                {
                    icon = "\u2713"; // checkmark
                    iconCol = Gold;
                }
                else if (!ms.Unlocked && (m == 0 || (m > 0 && milestones[m - 1].Unlocked)))
                {
                    icon = "\u25B6"; // arrow (next)
                    iconCol = CyanAccent;
                }
                else
                {
                    icon = "\u25CB"; // circle (future)
                    iconCol = DimText;
                }

                GUI.color = iconCol;
                GUI.Label(new Rect(innerX, colY, 16, 16), icon, _smallStyle);
                GUI.color = ms.Unlocked ? White : DimText;
                GUI.Label(new Rect(innerX + 18, colY, halfW - 20, 16), $"R{ms.Rank}: {ms.UnlockName}", _tinyStyle);
                colY += 17;
            }
            if (mCount == 0)
            {
                GUI.color = DimText;
                GUI.Label(new Rect(innerX, colY, halfW, 16), "No milestones", _tinyStyle);
            }

            // Right column: Recent gains
            float rColX = innerX + halfW + pad;
            float rColY = innerY + 22;
            GUI.color = CyanAccent;
            GUI.Label(new Rect(rColX, rColY, halfW, 16), "Recent Gains", _boldSmall);
            GUI.color = White;
            rColY += 18;

            var gains = skill.RecentGains;
            int gCount = gains != null ? Mathf.Min(gains.Length, 4) : 0;
            for (int g = 0; g < gCount; g++)
            {
                GUI.color = new Color(0.7f, 0.75f, 0.85f, 1f);
                string gainText = gains[g] ?? "";
                if (gainText.Length > 40) gainText = gainText.Substring(0, 37) + "...";
                GUI.Label(new Rect(rColX, rColY, halfW, 16), gainText, _tinyStyle);
                rColY += 17;
            }
            if (gCount == 0)
            {
                GUI.color = DimText;
                GUI.Label(new Rect(rColX, rColY, halfW, 16), "No recent gains", _tinyStyle);
            }

            // Bottom line: session summary
            float bottomY = area.y + ExpandedH - 22;
            int sessionXp = EstimateSessionXp(skill);
            string timeEst = EstimateTimeToRank(skill);
            GUI.color = DimText;
            string bottomText = $"Session: +{sessionXp:N0} XP";
            if (timeEst != null) bottomText += $" \u00B7 {timeEst}";
            GUI.Label(new Rect(innerX, bottomY, area.width - pad * 2, 16), bottomText, _tinyStyle);
            GUI.color = White;
        }

        // ---- Helpers ----

        private List<SkillData> BuildFilteredList()
        {
            var list = new List<SkillData>();
            string catFilter = _activeCategory > 0 ? Categories[_activeCategory] : null;

            foreach (var s in _skills)
            {
                if (catFilter != null && !string.Equals(s.Category, catFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(s);
            }

            switch (_activeSortMode)
            {
                case 0: // RANK (descending)
                    list.Sort((a, b) => b.Rank != a.Rank ? b.Rank.CompareTo(a.Rank) : string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                    break;
                case 1: // RECENT (most recent XP first)
                    list.Sort((a, b) => b.LastXpTime.CompareTo(a.LastXpTime));
                    break;
                case 2: // NAME (alphabetical)
                    list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                    break;
            }

            return list;
        }

        private Color GetCategoryColor(string category)
        {
            if (category != null && CategoryColors.TryGetValue(category.ToUpperInvariant(), out var c))
                return c;
            return DimText;
        }

        private int EstimateSessionXp(SkillData skill)
        {
            // Rough estimate: if we had a session start baseline we could track this properly.
            // For now, sum recent gains heuristically or return 0.
            if (skill.RecentGains == null || skill.RecentGains.Length == 0) return 0;
            // Each gain line like "+25 XP from Combat" — parse leading number
            int total = 0;
            foreach (var g in skill.RecentGains)
            {
                if (g == null) continue;
                int xp = ParseLeadingXp(g);
                if (xp > 0) total += xp;
            }
            return total;
        }

        private int ParseLeadingXp(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int start = text.IndexOf('+');
            if (start < 0) start = 0;
            else start++;
            int end = start;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == ','))
                end++;
            if (end <= start) return 0;
            string numStr = text.Substring(start, end - start).Replace(",", "");
            int.TryParse(numStr, out int val);
            return val;
        }

        private string EstimateTimeToRank(SkillData skill)
        {
            if (skill.Rank >= MaxRank) return null;
            if (skill.XpToNext <= 0) return null;

            float elapsed = Time.time - _sessionStartTime;
            if (elapsed < 60f) return null; // too early to estimate

            int sessionXp = EstimateSessionXp(skill);
            if (sessionXp <= 0) return null;

            float xpPerHour = sessionXp / (elapsed / 3600f);
            if (xpPerHour < 1f) return null;

            int remaining = skill.XpToNext - skill.XpCurrent;
            float hours = remaining / xpPerHour;

            if (hours < 1f) return $"~{Mathf.CeilToInt(hours * 60)}m to Rank {skill.Rank + 1} at current pace";
            return $"~{hours:F1}h to Rank {skill.Rank + 1} at current pace";
        }

        // ---- Drawing Primitives ----

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _pixel);
            GUI.color = prev;
        }

        private void DrawBorder(Rect r, Color c, float thickness)
        {
            DrawRect(new Rect(r.x, r.y, r.width, thickness), c);                           // top
            DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);             // bottom
            DrawRect(new Rect(r.x, r.y, thickness, r.height), c);                           // left
            DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);             // right
        }

        private void HandleDrag(Rect dragArea)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && dragArea.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - new Vector2(_windowPos.x, _windowPos.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                _windowPos = e.mousePosition - _dragOffset;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
            }
        }

        // ---- Styles ----

        private void EnsureStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = White },
                alignment = TextAnchor.MiddleLeft
            };

            _boldSmall = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = White },
                alignment = TextAnchor.MiddleLeft
            };

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = White },
                alignment = TextAnchor.MiddleLeft
            };

            _tinyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = DimText },
                alignment = TextAnchor.MiddleLeft
            };

            _centeredSmall = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = White },
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}

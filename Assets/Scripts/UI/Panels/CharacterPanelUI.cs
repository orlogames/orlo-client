using UnityEngine;
using System.Collections.Generic;
using Orlo.UI.TMD;

namespace Orlo.UI.Panels
{
    /// <summary>
    /// 5-tab character sheet panel (Overview, Skills, Reputation, Badges, History).
    /// Toggle with P key or toolbar. OnGUI immediate mode rendering.
    /// </summary>
    public class CharacterPanelUI : MonoBehaviour
    {
        public static CharacterPanelUI Instance { get; private set; }

        private enum Tab { Overview, Skills, Reputation, Badges, History }

        private bool _visible;
        private Tab _activeTab = Tab.Overview;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        private const float WinW = 480f;
        private const float WinH = 680f;
        private const float HeaderH = 36f;
        private const float TabBarH = 28f;

        private Texture2D _pixel;

        // ---- TMD Spring for tab underline ----
        private SpringValue _tabUnderlineX;
        private float _tabUnderlineW;
        private int _lastTabIndex;

        // ---- Colors (palette-derived at runtime) ----
        private static readonly Color ColVitality = new Color(0.85f, 0.2f, 0.2f, 1f);
        private static readonly Color ColStamina = new Color(0.9f, 0.7f, 0.15f, 1f);
        private static readonly Color ColFocus = new Color(0.2f, 0.45f, 0.9f, 1f);

        // ---- Tab Names ----
        private static readonly string[] TabNames = { "OVERVIEW", "SKILLS", "REPUTATION", "BADGES", "HISTORY" };

        // ---- Identity Data ----
        private string _name = "Unknown";
        private string _race = "Human";
        private int _level = 1;
        private string _className = "";
        private string _title = "";
        private string _guildTag = "";
        private int _criminalRating;
        private long _xpCurrent;
        private long _xpMax = 1;

        // ---- Health Pools ----
        private float _vit, _maxVit = 100f;
        private float _sta, _maxSta = 100f;
        private float _foc, _maxFoc = 100f;

        // ---- Core Attributes ----
        private float _armor, _melee, _ranged, _crit, _speed, _luck;

        // ---- Equipment ----
        public struct EquipSlot
        {
            public string Abbreviation;
            public int ItemLevel;
            public int Quality; // 0=empty, 1=Standard, 2=Enhanced, 3=Exceptional, 4=Legendary
        }
        private EquipSlot[] _equipSlots = new EquipSlot[17]; // 14 armor + 3 weapon

        // ---- Skills ----
        public struct SkillEntry
        {
            public string Name;
            public int Rank;
            public long XpCurrent;
            public long XpMax;
        }
        private SkillEntry[] _skills = new SkillEntry[0];

        // ---- Factions ----
        public struct FactionEntry
        {
            public string Name;
            public int Standing;
            public int NextTier;
            public string Tier;
            public Color FactionColor;
        }
        private FactionEntry[] _factions = new FactionEntry[0];

        // ---- Badges ----
        public struct BadgeEntry
        {
            public string Name;
            public string Icon;
            public int Rarity; // 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary
            public bool Showcased;
            public string EarnedDate;
        }
        private BadgeEntry[] _badges = new BadgeEntry[0];
        private int _totalBadges;
        private int _badgeFilterRarity = -1; // -1 = all

        // ---- History ----
        public struct HistoryEvent
        {
            public string Timestamp;
            public string Description;
            public int Type; // 0=Combat, 1=Crafting, 2=Progression, 3=Social, 4=Exploration
        }
        private List<HistoryEvent> _history = new List<HistoryEvent>();

        // ---- Quick Stats ----
        private float _playtimeHours;
        private long _credits;
        private int _kills;
        private int _deaths;
        private string _zone = "Unknown";

        // ================================================================
        // Lifecycle
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            float savedX = PlayerPrefs.GetFloat("CharPanel_X", Screen.width / 2f - WinW / 2f);
            float savedY = PlayerPrefs.GetFloat("CharPanel_Y", Screen.height / 2f - WinH / 2f);
            _windowPos = new Vector2(savedX, savedY);

            _tabUnderlineX = new SpringValue(0f, 350f, 0.7f);
            _tabUnderlineW = WinW / TabNames.Length;
        }

        private void Update()
        {
            _tabUnderlineX.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_pixel != null) Destroy(_pixel);
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible) _scrollPos = Vector2.zero;
        }

        public bool IsVisible => _visible;

        // ================================================================
        // Public Data Setters
        // ================================================================

        public void SetIdentity(string name, string race, int level, string title,
            string guildTag, int criminalRating, long xpCurrent, long xpMax)
        {
            _name = name; _race = race; _level = level; _title = title;
            _guildTag = guildTag; _criminalRating = criminalRating;
            _xpCurrent = xpCurrent; _xpMax = System.Math.Max(xpMax, 1L);
        }

        public void SetHealthPools(float vit, float maxVit, float sta, float maxSta,
            float foc, float maxFoc)
        {
            _vit = vit; _maxVit = maxVit; _sta = sta; _maxSta = maxSta;
            _foc = foc; _maxFoc = maxFoc;
        }

        public void SetAttributes(float armor, float melee, float ranged,
            float crit, float speed, float luck)
        {
            _armor = armor; _melee = melee; _ranged = ranged;
            _crit = crit; _speed = speed; _luck = luck;
        }

        public void SetEquipment(EquipSlot[] slots)
        {
            _equipSlots = slots ?? new EquipSlot[17];
        }

        public void SetSkills(SkillEntry[] skills)
        {
            _skills = skills ?? new SkillEntry[0];
        }

        public void SetFactions(FactionEntry[] factions)
        {
            _factions = factions ?? new FactionEntry[0];
        }

        public void SetBadges(BadgeEntry[] badges, int totalBadges)
        {
            _badges = badges ?? new BadgeEntry[0];
            _totalBadges = totalBadges;
        }

        public void AddHistoryEvent(string timestamp, string description, int type)
        {
            _history.Insert(0, new HistoryEvent
            {
                Timestamp = timestamp,
                Description = description,
                Type = type
            });
            if (_history.Count > 200) _history.RemoveAt(_history.Count - 1);
        }

        public void SetQuickStats(float playtimeHours, long credits, int kills, int deaths, string zone)
        {
            _playtimeHours = playtimeHours; _credits = credits;
            _kills = kills; _deaths = deaths; _zone = zone;
        }

        // ================================================================
        // OnGUI
        // ================================================================

        // ---- Palette helpers ----
        private RacePalette P => TMDTheme.Instance?.Palette ?? RacePalette.Solari;

        private void OnGUI()
        {
            if (!_visible) return;

            // Escape to close
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _visible = false;
                Event.current.Use();
                return;
            }

            Rect winRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            // TMD glassmorphic background + border
            TMDTheme.DrawPanel(winRect);

            // Header bar (draggable)
            Rect headerRect = new Rect(winRect.x, winRect.y, WinW, HeaderH);
            HandleDrag(headerRect);

            // Title via TMD
            TMDTheme.DrawTitle(winRect, "CHARACTER");

            // Close X
            GUIStyle closeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = P.TextDim }
            };
            Rect closeRect = new Rect(winRect.xMax - 32, winRect.y + 2, 28, HeaderH - 4);
            if (GUI.Button(closeRect, "", GUIStyle.none))
            {
                _visible = false;
                return;
            }
            GUI.Label(closeRect, "X", closeStyle);

            // Tab bar
            float tabY = winRect.y + HeaderH + 8;
            Rect tabBarRect = new Rect(winRect.x, tabY, WinW, TabBarH);
            DrawRect(tabBarRect, P.PanelBackground);
            DrawTabs(tabBarRect);

            // Content area
            float contentY = tabY + TabBarH + 4;
            float contentH = WinH - HeaderH - TabBarH - 16;
            Rect contentRect = new Rect(winRect.x, contentY, WinW, contentH);

            // Scrollable content
            Rect viewRect = new Rect(0, 0, WinW - 20, GetContentHeight());
            _scrollPos = GUI.BeginScrollView(contentRect, _scrollPos,
                new Rect(0, 0, WinW - 20, Mathf.Max(viewRect.height, contentH)));

            float cx = 12f;
            float cy = 4f;

            switch (_activeTab)
            {
                case Tab.Overview: DrawOverview(cx, ref cy); break;
                case Tab.Skills: DrawSkills(cx, ref cy); break;
                case Tab.Reputation: DrawReputation(cx, ref cy); break;
                case Tab.Badges: DrawBadges(cx, ref cy); break;
                case Tab.History: DrawHistory(cx, ref cy); break;
            }

            GUI.EndScrollView();

            // Scanline overlay
            TMDTheme.DrawScanlines(winRect);

            // Save position on drag
            if (_dragging && Event.current.type == EventType.MouseUp)
            {
                PlayerPrefs.SetFloat("CharPanel_X", _windowPos.x);
                PlayerPrefs.SetFloat("CharPanel_Y", _windowPos.y);
            }
        }

        // ================================================================
        // Tab Bar
        // ================================================================

        private void DrawTabs(Rect bar)
        {
            float tabW = bar.width / TabNames.Length;
            int activeIdx = (int)_activeTab;

            // Update spring target for underline animation
            if (activeIdx != _lastTabIndex)
            {
                _tabUnderlineX.Target = bar.x + activeIdx * tabW + 4;
                _tabUnderlineW = tabW - 8;
                _lastTabIndex = activeIdx;
            }
            // Snap on first frame
            if (_tabUnderlineX.Target == 0f && _tabUnderlineX.Value == 0f)
            {
                _tabUnderlineX = SpringPresets.TabSlide(bar.x + activeIdx * tabW + 4, bar.x + activeIdx * tabW + 4);
                _tabUnderlineW = tabW - 8;
            }

            for (int i = 0; i < TabNames.Length; i++)
            {
                Rect tabRect = new Rect(bar.x + i * tabW, bar.y, tabW, bar.height);
                bool isActive = i == activeIdx;
                bool hover = tabRect.Contains(Event.current.mousePosition);

                if (!isActive && hover)
                    DrawRect(tabRect, new Color(P.Primary.r, P.Primary.g, P.Primary.b, 0.1f));

                GUIStyle tabStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = isActive ? P.Primary : P.TextDim }
                };
                GUI.Label(tabRect, TabNames[i], tabStyle);

                if (GUI.Button(tabRect, "", GUIStyle.none))
                {
                    _activeTab = (Tab)i;
                    _scrollPos = Vector2.zero;
                }
            }

            // Animated underline via spring
            DrawRect(new Rect(_tabUnderlineX.Value, bar.yMax - 2, _tabUnderlineW, 2), P.Primary);
        }

        // ================================================================
        // Tab 1 — Overview
        // ================================================================

        private void DrawOverview(float x, ref float y)
        {
            float w = WinW - 40f;

            // Identity block
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                normal = { textColor = P.Text }
            };
            GUI.Label(new Rect(x, y, w, 22), _name, nameStyle);

            if (!string.IsNullOrEmpty(_guildTag))
            {
                float nameW = nameStyle.CalcSize(new GUIContent(_name)).x;
                GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14, normal = { textColor = P.Primary }
                };
                GUI.Label(new Rect(x + nameW + 6, y + 2, 120, 20), "<" + _guildTag + ">", tagStyle);
            }
            y += 22;

            if (!string.IsNullOrEmpty(_title))
            {
                SmallLabel(x, y, _title, P.Accent);
                y += 16;
            }

            // Race / Level / Class
            string infoLine = _race + " \u00B7 Level " + _level;
            if (!string.IsNullOrEmpty(_className)) infoLine += " \u00B7 " + _className;
            SmallLabel(x, y, infoLine, P.TextDim);
            y += 18;

            // XP bar via TMD
            TMDTheme.DrawProgressBar(new Rect(x, y, 200, 6), (float)_xpCurrent / _xpMax, P.Secondary);
            GUIStyle xpTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = P.TextDim }
            };
            GUI.Label(new Rect(x, y + 7, 200, 12), _xpCurrent + " / " + _xpMax + " XP", xpTextStyle);
            y += 18;

            // Criminal rating
            if (_criminalRating > 0)
            {
                string crLabel; Color crColor;
                GetCriminalDisplay(_criminalRating, out crLabel, out crColor);
                SmallLabel(x, y, "Criminal: " + crLabel, crColor);
                y += 16;
            }

            y += 8;
            DrawSeparator(x, y, w);
            y += 8;

            // Health pools
            DrawPoolBar(x, y, w, "VIT", _vit, _maxVit, ColVitality);
            y += 22;
            DrawPoolBar(x, y, w, "STA", _sta, _maxSta, ColStamina);
            y += 22;
            DrawPoolBar(x, y, w, "FOC", _foc, _maxFoc, ColFocus);
            y += 28;

            DrawSeparator(x, y, w);
            y += 8;

            // Core attributes — 3x2 grid
            float cellW = w / 3f;
            string[] attrLabels = { "Armor Rating", "Melee Power", "Ranged Power",
                                    "Crit Chance", "Move Speed", "Luck" };
            float[] attrValues = { _armor, _melee, _ranged, _crit, _speed, _luck };
            string[] attrFormats = { "F0", "F0", "F0", "F1", "F1", "F0" };
            string[] attrSuffixes = { "", "", "", "%", " m/s", "" };

            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float ax = x + col * cellW;
                float ay = y + row * 38;

                SmallLabel(ax, ay, attrLabels[i], P.TextDim, 10);

                GUIStyle valStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14, fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(ax, ay + 14, cellW, 20),
                    attrValues[i].ToString(attrFormats[i]) + attrSuffixes[i], valStyle);
            }
            y += 84;

            DrawSeparator(x, y, w);
            y += 8;

            // Equipment summary — 14 armor slots in a row
            DrawEquipmentRow(x, y, 14, 0);
            y += 34;

            // 3 weapon stance cells
            DrawEquipmentRow(x, y, 3, 14);
            y += 34;

            DrawSeparator(x, y, w);
            y += 8;

            // Quick stats footer
            DrawQuickStats(x, ref y, w);
        }

        private void DrawPoolBar(float x, float y, float w, string label, float val, float max, Color col)
        {
            GUIStyle lbl = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = col }, alignment = TextAnchor.MiddleLeft
            };
            GUI.Label(new Rect(x, y, 32, 16), label, lbl);

            float barX = x + 34;
            float barW = w - 34 - 80;
            float fill = max > 0 ? Mathf.Clamp01(val / max) : 0f;
            TMDTheme.DrawProgressBar(new Rect(barX, y + 3, barW, 10), fill, col);

            GUIStyle numStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleRight,
                normal = { textColor = P.Text }
            };
            GUI.Label(new Rect(barX + barW + 4, y, 76, 16), Mathf.RoundToInt(val) + " / " + Mathf.RoundToInt(max), numStyle);
        }

        private void DrawEquipmentRow(float x, float y, int count, int startIdx)
        {
            float slotSize = 28f;
            float gap = 3f;

            for (int i = 0; i < count && (startIdx + i) < _equipSlots.Length; i++)
            {
                float sx = x + i * (slotSize + gap);
                Rect slotRect = new Rect(sx, y, slotSize, slotSize);
                var slot = _equipSlots[startIdx + i];
                Color borderCol = GetQualityColor(slot.Quality);

                DrawRect(slotRect, new Color(0.08f, 0.08f, 0.12f, 0.9f));
                DrawBorder(slotRect, borderCol, 1f);

                string text = slot.Quality == 0 ? "--" : slot.Abbreviation + "\n" + slot.ItemLevel;
                GUIStyle slotStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 8, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = slot.Quality == 0 ? P.TextDim : Color.white },
                    wordWrap = true
                };
                GUI.Label(slotRect, text, slotStyle);
            }
        }

        private void DrawQuickStats(float x, ref float y, float w)
        {
            float colW = w / 2f;

            // Row 1
            DrawStatPair(x, y, "Playtime", FormatPlaytime(_playtimeHours), P.Text);
            DrawStatPair(x + colW, y, "Credits", _credits.ToString("N0"), P.Accent);
            y += 16;

            // Row 2
            DrawStatPair(x, y, "Kills", _kills.ToString(), P.Text);
            DrawStatPair(x + colW, y, "Deaths", _deaths.ToString(), P.Text);
            y += 16;

            // Row 3
            DrawStatPair(x, y, "Zone", _zone, P.Primary);
            y += 16;
        }

        private void DrawStatPair(float x, float y, string label, string value, Color valueColor)
        {
            SmallLabel(x, y, label + ":", P.TextDim, 10);
            GUIStyle valStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = valueColor }
            };
            GUI.Label(new Rect(x + 70, y, 140, 14), value, valStyle);
        }

        // ================================================================
        // Tab 2 — Skills
        // ================================================================

        private void DrawSkills(float x, ref float y)
        {
            float w = WinW - 40f;

            if (_skills.Length == 0)
            {
                SmallLabel(x, y + 20, "No skills learned yet.", P.TextDim);
                y += 50;
                return;
            }

            // Sort by rank descending (work on a copy to avoid re-sorting every frame)
            for (int i = 0; i < _skills.Length; i++)
            {
                var sk = _skills[i];
                bool mastered = sk.Rank >= 100;

                // Skill name
                GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, fontStyle = FontStyle.Bold,
                    normal = { textColor = mastered ? P.Accent : P.Text }
                };
                GUI.Label(new Rect(x, y, w * 0.5f, 18), sk.Name, nameStyle);

                // Star rating (5 stars for 0-100 rank)
                float starX = x + w * 0.5f;
                int fullStars = sk.Rank / 20;
                for (int s = 0; s < 5; s++)
                {
                    Color starCol = s < fullStars ? P.Accent : new Color(0.25f, 0.25f, 0.3f);
                    GUIStyle starStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12, normal = { textColor = starCol }
                    };
                    GUI.Label(new Rect(starX + s * 14, y, 14, 18), "\u2605", starStyle);
                }

                // Rank text
                GUIStyle rankStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, alignment = TextAnchor.MiddleRight,
                    normal = { textColor = P.TextDim }
                };
                GUI.Label(new Rect(x + w - 80, y, 80, 18),
                    mastered ? "MASTERED" : "Rank " + sk.Rank + "/100", rankStyle);
                y += 20;

                // XP bar via TMD
                float barW = w;
                float barH = 4f;
                Color barCol = mastered ? P.Accent : P.Primary;
                float fill = sk.XpMax > 0 ? Mathf.Clamp01((float)sk.XpCurrent / sk.XpMax) : 0f;
                if (mastered) fill = 1f;

                TMDTheme.DrawProgressBar(new Rect(x, y, barW, barH), fill, barCol);
                y += 12;

                DrawSeparator(x, y, w, 0.3f);
                y += 8;
            }
        }

        // ================================================================
        // Tab 3 — Reputation
        // ================================================================

        private void DrawReputation(float x, ref float y)
        {
            float w = WinW - 40f;

            if (_factions.Length == 0)
            {
                SmallLabel(x, y + 20, "No faction standings yet.", P.TextDim);
                y += 50;
                return;
            }

            for (int i = 0; i < _factions.Length; i++)
            {
                var fac = _factions[i];

                // Faction name
                GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold,
                    normal = { textColor = fac.FactionColor }
                };
                GUI.Label(new Rect(x, y, w, 20), fac.Name, nameStyle);
                y += 20;

                // Standing bar via TMD
                float barW = w;
                float barH = 8f;
                float fill = fac.NextTier > 0 ? Mathf.Clamp01((float)fac.Standing / fac.NextTier) : 1f;

                TMDTheme.DrawProgressBar(new Rect(x, y, barW, barH), fill, fac.FactionColor);
                y += 14;

                // Tier label + numbers
                GUIStyle tierStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, fontStyle = FontStyle.Bold,
                    normal = { textColor = GetTierColor(fac.Tier) }
                };
                GUI.Label(new Rect(x, y, 120, 14), fac.Tier, tierStyle);

                GUIStyle numStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, alignment = TextAnchor.MiddleRight,
                    normal = { textColor = P.TextDim }
                };
                GUI.Label(new Rect(x + w - 120, y, 120, 14),
                    fac.Standing + " / " + fac.NextTier, numStyle);
                y += 22;

                if (i < _factions.Length - 1)
                {
                    DrawSeparator(x, y, w);
                    y += 8;
                }
            }
        }

        // ================================================================
        // Tab 4 — Badges
        // ================================================================

        private void DrawBadges(float x, ref float y)
        {
            float w = WinW - 40f;

            // Header with count
            int earned = _badges.Length;
            float pct = _totalBadges > 0 ? (earned * 100f / _totalBadges) : 0f;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = P.Text }
            };
            GUI.Label(new Rect(x, y, w - 80, 18),
                "BADGES  " + earned + " / " + _totalBadges + "  (" + pct.ToString("F0") + "%)", headerStyle);

            // Filter dropdown
            string[] filterNames = { "All", "Common", "Uncommon", "Rare", "Epic", "Legendary" };
            Rect filterRect = new Rect(x + w - 80, y, 80, 18);
            GUIStyle filterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleRight,
                normal = { textColor = P.Primary }
            };
            string filterLabel = _badgeFilterRarity < 0 ? "All" : filterNames[_badgeFilterRarity + 1];
            if (GUI.Button(filterRect, "", GUIStyle.none))
            {
                _badgeFilterRarity++;
                if (_badgeFilterRarity > 4) _badgeFilterRarity = -1;
            }
            GUI.Label(filterRect, "\u25BC " + filterLabel, filterStyle);
            y += 24;

            // Grid — 4 columns
            float tileW = 104f;
            float tileH = 80f;
            float tileGap = 6f;
            int col = 0;
            float startY = y;

            for (int i = 0; i < _badges.Length; i++)
            {
                var badge = _badges[i];
                if (_badgeFilterRarity >= 0 && badge.Rarity != _badgeFilterRarity) continue;

                float tx = x + col * (tileW + tileGap);
                float ty = y;
                Rect tileRect = new Rect(tx, ty, tileW, tileH);

                // Background
                DrawRect(tileRect, new Color(0.08f, 0.08f, 0.12f, 0.9f));

                // Rarity border
                Color rarityCol = GetRarityColor(badge.Rarity);
                DrawBorder(tileRect, rarityCol, 1f);

                // Showcased star
                if (badge.Showcased)
                {
                    GUIStyle starStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10, normal = { textColor = P.Accent },
                        alignment = TextAnchor.UpperRight
                    };
                    GUI.Label(new Rect(tx, ty, tileW - 2, 14), "\u2605", starStyle);
                }

                // Icon
                GUIStyle iconStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = rarityCol }
                };
                GUI.Label(new Rect(tx, ty + 8, tileW, 36), badge.Icon, iconStyle);

                // Name (2 lines max)
                GUIStyle badgeNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9, alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                    normal = { textColor = P.Text }
                };
                GUI.Label(new Rect(tx + 4, ty + 48, tileW - 8, 28), badge.Name, badgeNameStyle);

                col++;
                if (col >= 4)
                {
                    col = 0;
                    y += tileH + tileGap;
                }
            }
            if (col > 0) y += tileH + tileGap;

            if (y == startY)
            {
                SmallLabel(x, y + 8, "No badges match this filter.", P.TextDim);
                y += 30;
            }
        }

        // ================================================================
        // Tab 5 — History
        // ================================================================

        private void DrawHistory(float x, ref float y)
        {
            float w = WinW - 40f;

            if (_history.Count == 0)
            {
                SmallLabel(x, y + 20, "No recent events.", P.TextDim);
                y += 50;
                return;
            }

            string lastDateHeader = "";
            for (int i = 0; i < _history.Count; i++)
            {
                var ev = _history[i];
                string dateHeader = GetDateHeader(ev.Timestamp);

                if (dateHeader != lastDateHeader)
                {
                    if (i > 0) y += 4;
                    GUIStyle dateStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11, fontStyle = FontStyle.Bold,
                        normal = { textColor = P.TextDim }
                    };
                    GUI.Label(new Rect(x, y, w, 16), dateHeader, dateStyle);
                    y += 18;
                    lastDateHeader = dateHeader;
                }

                Color evColor = GetEventColor(ev.Type);
                string evIcon = GetEventIcon(ev.Type);

                // Timestamp
                SmallLabel(x, y, ev.Timestamp, P.TextDim, 9);

                // Icon
                GUIStyle iconStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, normal = { textColor = evColor }
                };
                GUI.Label(new Rect(x + 52, y, 16, 14), evIcon, iconStyle);

                // Description
                GUIStyle descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, wordWrap = true,
                    normal = { textColor = P.Text }
                };
                float descW = w - 72;
                float descH = descStyle.CalcHeight(new GUIContent(ev.Description), descW);
                GUI.Label(new Rect(x + 70, y, descW, descH), ev.Description, descStyle);
                y += Mathf.Max(descH, 14) + 4;
            }
        }

        // ================================================================
        // Drawing Helpers
        // ================================================================

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _pixel);
            GUI.color = prev;
        }

        private void DrawBorder(Rect r, Color c, float thickness)
        {
            DrawRect(new Rect(r.x, r.y, r.width, thickness), c);                     // top
            DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);       // bottom
            DrawRect(new Rect(r.x, r.y, thickness, r.height), c);                     // left
            DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);      // right
        }

        private void DrawSeparator(float x, float y, float w, float alpha = 0.5f)
        {
            DrawRect(new Rect(x, y, w, 1), new Color(P.Border.r, P.Border.g, P.Border.b, alpha));
        }

        private void DrawLabeledBar(float x, float y, float w, float h, float fill,
            Color barColor, string text, Color textColor)
        {
            DrawRect(new Rect(x, y, w, h), new Color(0.1f, 0.1f, 0.15f, 1f));
            if (fill > 0)
                DrawRect(new Rect(x, y, w * Mathf.Clamp01(fill), h), barColor);

            GUIStyle txtStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = textColor }
            };
            GUI.Label(new Rect(x, y + h + 1, w, 12), text, txtStyle);
        }

        private void SmallLabel(float x, float y, string text, Color color, int size = 10)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, normal = { textColor = color }
            };
            GUI.Label(new Rect(x, y, WinW - 40, 16), text, style);
        }

        private void HandleDrag(Rect dragArea)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && dragArea.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - _windowPos;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                _windowPos = e.mousePosition - _dragOffset;
                _windowPos.x = Mathf.Clamp(_windowPos.x, 0, Screen.width - WinW);
                _windowPos.y = Mathf.Clamp(_windowPos.y, 0, Screen.height - WinH);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
            }
        }

        // ================================================================
        // Content Height Estimation
        // ================================================================

        private float GetContentHeight()
        {
            switch (_activeTab)
            {
                case Tab.Overview: return 460f;
                case Tab.Skills: return Mathf.Max(100, _skills.Length * 40f + 20);
                case Tab.Reputation: return Mathf.Max(100, _factions.Length * 66f + 20);
                case Tab.Badges:
                    int visibleCount = 0;
                    for (int i = 0; i < _badges.Length; i++)
                        if (_badgeFilterRarity < 0 || _badges[i].Rarity == _badgeFilterRarity) visibleCount++;
                    int rows = Mathf.CeilToInt(visibleCount / 4f);
                    return Mathf.Max(100, rows * 86f + 40);
                case Tab.History: return Mathf.Max(100, _history.Count * 22f + 40);
                default: return 400f;
            }
        }

        // ================================================================
        // Lookup Helpers
        // ================================================================

        private Color GetQualityColor(int quality)
        {
            switch (quality)
            {
                case 1: return new Color(0.5f, 0.5f, 0.5f);     // Standard — grey
                case 2: return new Color(0.3f, 0.8f, 0.3f);     // Enhanced — green
                case 3: return new Color(0.3f, 0.5f, 1f);       // Exceptional — blue
                case 4: return P.Accent;                          // Legendary — gold
                default: return new Color(0.2f, 0.2f, 0.25f);   // Empty
            }
        }

        private Color GetRarityColor(int rarity)
        {
            switch (rarity)
            {
                case 0: return new Color(0.5f, 0.5f, 0.5f);     // Common — grey
                case 1: return new Color(0.3f, 0.8f, 0.3f);     // Uncommon — green
                case 2: return new Color(0.3f, 0.5f, 1f);       // Rare — blue
                case 3: return new Color(0.65f, 0.3f, 0.9f);    // Epic — purple
                case 4: return new Color(1f, 0.85f, 0.2f);      // Legendary — gold
                default: return new Color(0.4f, 0.4f, 0.4f);
            }
        }

        private Color GetTierColor(string tier)
        {
            if (tier == null) return P.TextDim;
            switch (tier)
            {
                case "Hostile": return new Color(0.9f, 0.2f, 0.2f);
                case "Unfriendly": return new Color(0.9f, 0.5f, 0.2f);
                case "Neutral": return P.TextDim;
                case "Respected": return new Color(0.3f, 0.8f, 0.3f);
                case "Allied": return new Color(0.3f, 0.5f, 1f);
                case "Exalted": return new Color(0.65f, 0.3f, 0.9f);
                default: return P.TextDim;
            }
        }

        private Color GetEventColor(int type)
        {
            switch (type)
            {
                case 0: return new Color(0.9f, 0.3f, 0.3f);     // Combat — red
                case 1: return new Color(0.9f, 0.6f, 0.2f);     // Crafting — orange
                case 2: return P.Accent;                          // Progression — gold
                case 3: return new Color(0.3f, 0.8f, 0.3f);     // Social — green
                case 4: return P.Primary;                        // Exploration — cyan
                default: return P.TextDim;
            }
        }

        private static string GetEventIcon(int type)
        {
            switch (type)
            {
                case 0: return "\u2694"; // Combat — crossed swords
                case 1: return "\u2692"; // Crafting — hammer & pick
                case 2: return "\u2B06"; // Progression — up arrow
                case 3: return "\u2764"; // Social — heart
                case 4: return "\u2690"; // Exploration — flag
                default: return "\u25CF";
            }
        }

        private static void GetCriminalDisplay(int rating, out string label, out Color color)
        {
            if (rating <= 0) { label = "Innocent"; color = new Color(0.3f, 0.8f, 0.3f); }
            else if (rating < 50) { label = "Suspect"; color = new Color(0.9f, 0.8f, 0.2f); }
            else if (rating < 150) { label = "Criminal"; color = new Color(0.9f, 0.5f, 0.2f); }
            else { label = "Notorious"; color = new Color(0.9f, 0.2f, 0.2f); }
        }

        private static string GetDateHeader(string timestamp)
        {
            // Expects timestamp like "12:34" or "2026-04-08 12:34"
            if (string.IsNullOrEmpty(timestamp)) return "Unknown";
            if (timestamp.Length <= 5) return "Today";
            // Attempt parse
            if (System.DateTime.TryParse(timestamp, out System.DateTime dt))
            {
                int daysAgo = (System.DateTime.Now.Date - dt.Date).Days;
                if (daysAgo == 0) return "Today";
                if (daysAgo == 1) return "Yesterday";
                return daysAgo + " days ago";
            }
            return "Recent";
        }

        private static string FormatPlaytime(float hours)
        {
            if (hours < 1) return Mathf.RoundToInt(hours * 60) + "m";
            if (hours < 100) return hours.ToString("F1") + "h";
            return Mathf.RoundToInt(hours) + "h";
        }
    }
}

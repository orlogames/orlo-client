using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orlo.UI.Panels
{
    /// <summary>
    /// Quest journal with codex, mission board, and quest tracking.
    /// Toggle with J key. OnGUI immediate mode rendering.
    /// </summary>
    public class QuestJournalUI : MonoBehaviour
    {
        public static QuestJournalUI Instance { get; private set; }

        // --- Callbacks ---
        public Action<int> OnTrackQuest;
        public Action<int> OnAbandonQuest;
        public Action<int> OnAcceptQuest;
        public Action<int> OnAcceptMission;

        // --- Data Structures ---
        public struct ObjectiveData { public string description; public int current, target; public bool completed; }
        public struct RewardData { public string type, label; public int value; public int rarity; }
        public struct QuestData
        {
            public int id; public string name, type, loreText;
            public ObjectiveData[] objectives; public RewardData[] rewards;
            public bool isTracked; public int chapter, totalChapters;
        }
        public struct AvailableQuest { public int id; public string name, sourceName; public int levelReq; public float distance; public bool canAccept; }
        public struct CompletedQuest { public int id; public string name, type, completedAgo; public int xpGained; }
        public struct CodexEntry { public string category, title, body, sourceQuest, discoveredDate; public bool isNew; }
        public struct MissionData { public int id; public string name, type; public RewardData[] rewards; public float expiryHours; public bool isUrgent; }

        // --- State ---
        private bool _visible;
        private enum Tab { Active, Available, Completed, Codex, Missions }
        private Tab _activeTab = Tab.Active;
        private Vector2 _scrollLeft, _scrollRight, _scrollCodexEntries, _scrollCodexBody;
        private int _selectedQuestIdx = -1;
        private int _selectedCodexIdx = -1;
        private string _activeCodexCategory = "World";
        private bool _confirmAbandon;

        // --- Data ---
        private QuestData[] _activeQuests = Array.Empty<QuestData>();
        private AvailableQuest[] _availableQuests = Array.Empty<AvailableQuest>();
        private CompletedQuest[] _completedQuests = Array.Empty<CompletedQuest>();
        private CodexEntry[] _codexEntries = Array.Empty<CodexEntry>();
        private MissionData[] _missions = Array.Empty<MissionData>();
        private string _missionSettlement = "";
        private float _missionRefreshSeconds;

        // --- Layout ---
        private const float WinW = 700f, WinH = 500f, SideW = 140f, TabH = 32f;

        // --- Colors ---
        private static readonly Color BgColor = new Color(0.06f, 0.06f, 0.1f, 0.92f);
        private static readonly Color BorderColor = new Color(0.2f, 0.25f, 0.4f);
        private static readonly Color Cyan = new Color(0.35f, 0.6f, 0.95f);
        private static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
        private static readonly Color DimText = new Color(0.5f, 0.5f, 0.6f);
        private static readonly Color HoverBg = new Color(0.12f, 0.12f, 0.18f);
        private static readonly Color RowAlt = new Color(0.08f, 0.08f, 0.13f);
        private static readonly Color DimRed = new Color(0.7f, 0.2f, 0.2f);

        private static readonly string[] CodexCategories = { "World", "Creatures", "Technology", "Factions", "Characters", "Resources", "Lore", "History" };
        private static readonly string[] QuestTypeOrder = { "STORY ARC", "FACTION", "EXPLORATION", "MISSION" };

        private void Awake() { Instance = this; }

        public void Toggle() { _visible = !_visible; if (_visible) { _selectedQuestIdx = -1; _confirmAbandon = false; } }

        public void SetActiveQuests(QuestData[] quests) { _activeQuests = quests ?? Array.Empty<QuestData>(); }
        public void SetAvailableQuests(AvailableQuest[] quests) { _availableQuests = quests ?? Array.Empty<AvailableQuest>(); }
        public void SetCompletedQuests(CompletedQuest[] quests) { _completedQuests = quests ?? Array.Empty<CompletedQuest>(); }
        public void SetCodexEntries(CodexEntry[] entries) { _codexEntries = entries ?? Array.Empty<CodexEntry>(); }
        public void SetMissions(MissionData[] missions, string settlement, float refreshSeconds)
        {
            _missions = missions ?? Array.Empty<MissionData>();
            _missionSettlement = settlement ?? "";
            _missionRefreshSeconds = refreshSeconds;
        }

        public void TrackQuest(int questId)
        {
            for (int i = 0; i < _activeQuests.Length; i++)
            {
                var q = _activeQuests[i];
                q.isTracked = q.id == questId;
                _activeQuests[i] = q;
            }
        }
        public void AbandonQuest(int questId) { _activeQuests = _activeQuests.Where(q => q.id != questId).ToArray(); _selectedQuestIdx = -1; }
        public void AcceptQuest(int questId) { /* server will move to active via SetActiveQuests */ }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.J)) Toggle();
            if (_visible && Input.GetKeyDown(KeyCode.Escape)) { _visible = false; }
            if (_missionRefreshSeconds > 0) _missionRefreshSeconds -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            var winX = (Screen.width - WinW) / 2f;
            var winY = (Screen.height - WinH) / 2f;
            var winR = new Rect(winX, winY, WinW, WinH);

            // Background
            DrawRect(winR, BgColor);
            DrawBorder(winR, BorderColor);

            // Title bar
            var titleR = new Rect(winX, winY, WinW, 26);
            DrawRect(titleR, new Color(0.04f, 0.04f, 0.08f));
            GUI.color = Cyan;
            GUI.Label(new Rect(winX + 10, winY + 4, 200, 20), "QUEST JOURNAL");
            // Close button
            GUI.color = DimText;
            if (GUI.Button(new Rect(winX + WinW - 24, winY + 3, 20, 20), "X", GUIStyle.none)) _visible = false;
            GUI.color = Color.white;

            var bodyY = winY + 26;
            var bodyH = WinH - 26;

            // Left sidebar
            DrawSidebar(winX, bodyY, bodyH);

            // Content area
            var contentR = new Rect(winX + SideW, bodyY, WinW - SideW, bodyH);
            DrawRect(contentR, new Color(0.05f, 0.05f, 0.09f, 0.5f));

            switch (_activeTab)
            {
                case Tab.Active: DrawActiveTab(contentR); break;
                case Tab.Available: DrawAvailableTab(contentR); break;
                case Tab.Completed: DrawCompletedTab(contentR); break;
                case Tab.Codex: DrawCodexTab(contentR); break;
                case Tab.Missions: DrawMissionsTab(contentR); break;
            }
        }

        // ===================== SIDEBAR =====================

        private void DrawSidebar(float x, float y, float h)
        {
            var sideR = new Rect(x, y, SideW, h);
            DrawRect(sideR, new Color(0.04f, 0.04f, 0.07f));
            DrawRect(new Rect(x + SideW - 1, y, 1, h), BorderColor);

            var tabs = new[] {
                ("Active", $"{_activeQuests.Length}/5"),
                ("Available", $"{_availableQuests.Length}"),
                ("Completed", $"{_completedQuests.Length}"),
                ("Codex", $"{_codexEntries.Length}"),
                ("Missions", $"{_missions.Length}")
            };

            for (int i = 0; i < tabs.Length; i++)
            {
                var tabEnum = (Tab)i;
                var ty = y + 8 + i * (TabH + 4);
                var tabR = new Rect(x + 4, ty, SideW - 8, TabH);
                bool active = _activeTab == tabEnum;
                bool hover = tabR.Contains(Event.current.mousePosition);

                DrawRect(tabR, active ? new Color(0.1f, 0.12f, 0.2f) : hover ? HoverBg : Color.clear);

                // Cyan left stripe for active tab
                if (active) DrawRect(new Rect(x + 4, ty, 3, TabH), Cyan);

                GUI.color = active ? Color.white : hover ? new Color(0.7f, 0.7f, 0.8f) : DimText;
                GUI.Label(new Rect(x + 14, ty + 7, 80, 20), tabs[i].Item1);
                GUI.color = active ? Cyan : DimText;
                var countStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
                GUI.Label(new Rect(x + 4, ty + 7, SideW - 18, 20), tabs[i].Item2, countStyle);

                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _activeTab = tabEnum;
                    _selectedQuestIdx = -1;
                    _selectedCodexIdx = -1;
                    _confirmAbandon = false;
                    Event.current.Use();
                }
            }
            GUI.color = Color.white;
        }

        // ===================== ACTIVE TAB =====================

        private void DrawActiveTab(Rect area)
        {
            float listW = _selectedQuestIdx >= 0 ? area.width * 0.42f : area.width;
            var listR = new Rect(area.x, area.y, listW, area.height);

            _scrollLeft = GUI.BeginScrollView(listR, _scrollLeft, new Rect(0, 0, listW - 16, _activeQuests.Length * 52 + QuestTypeOrder.Length * 24 + 20));
            float cy = 4;

            foreach (var typeLabel in QuestTypeOrder)
            {
                var group = _activeQuests.Where(q => MapTypeLabel(q.type) == typeLabel).ToArray();
                if (group.Length == 0) continue;

                // Type divider
                var divColor = typeLabel == "STORY ARC" ? Gold : typeLabel == "FACTION" ? new Color(0.6f, 0.3f, 0.8f)
                    : typeLabel == "EXPLORATION" ? Cyan : DimText;
                DrawRect(new Rect(4, cy, listW - 24, 1), divColor);
                GUI.color = divColor;
                GUI.Label(new Rect(8, cy + 2, 200, 16), typeLabel, SmallBold());
                cy += 20;

                for (int gi = 0; gi < group.Length; gi++)
                {
                    var q = group[gi];
                    int globalIdx = Array.IndexOf(_activeQuests, q);
                    bool selected = globalIdx == _selectedQuestIdx;
                    var rowR = new Rect(4, cy, listW - 24, 48);
                    bool hover = rowR.Contains(Event.current.mousePosition - listR.position + _scrollLeft);

                    DrawRect(rowR, selected ? new Color(0.12f, 0.14f, 0.22f) : hover ? HoverBg : (gi % 2 == 0 ? RowAlt : Color.clear));

                    // Track star
                    var starR = new Rect(8, cy + 4, 16, 16);
                    GUI.color = q.isTracked ? Gold : DimText;
                    GUI.Label(starR, q.isTracked ? "[*]" : "[ ]");

                    // Quest name
                    GUI.color = selected ? Color.white : new Color(0.85f, 0.85f, 0.9f);
                    GUI.Label(new Rect(28, cy + 4, listW - 100, 18), q.name);

                    // Type label right-aligned
                    GUI.color = divColor;
                    var typeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 9 };
                    GUI.Label(new Rect(4, cy + 4, listW - 30, 16), q.type, typeStyle);

                    // Objective summary
                    int done = q.objectives?.Count(o => o.completed) ?? 0;
                    int total = q.objectives?.Length ?? 0;
                    string brief = total > 0 ? q.objectives[0].description : "";
                    if (brief.Length > 40) brief = brief.Substring(0, 37) + "...";
                    GUI.color = DimText;
                    GUI.Label(new Rect(28, cy + 22, listW - 100, 16), brief, SmallStyle());
                    GUI.color = Cyan;
                    var fracStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
                    GUI.Label(new Rect(4, cy + 22, listW - 30, 16), $"{done}/{total} obj", fracStyle);

                    // Click handlers
                    if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        if (starR.Contains(Event.current.mousePosition - listR.position + _scrollLeft))
                        {
                            OnTrackQuest?.Invoke(q.id);
                            TrackQuest(q.id);
                        }
                        else
                        {
                            _selectedQuestIdx = globalIdx;
                            _confirmAbandon = false;
                        }
                        Event.current.Use();
                    }
                    cy += 52;
                }
            }
            GUI.color = Color.white;
            GUI.EndScrollView();

            // Detail panel
            if (_selectedQuestIdx >= 0 && _selectedQuestIdx < _activeQuests.Length)
            {
                var detailR = new Rect(area.x + listW, area.y, area.width - listW, area.height);
                DrawRect(new Rect(detailR.x, detailR.y, 1, detailR.height), BorderColor);
                DrawQuestDetail(detailR, _activeQuests[_selectedQuestIdx], true);
            }
        }

        private void DrawQuestDetail(Rect area, QuestData q, bool canAbandon)
        {
            float px = area.x + 12, py = area.y + 10, pw = area.width - 24;
            _scrollRight = GUI.BeginScrollView(area, _scrollRight, new Rect(0, 0, area.width - 16, 500));
            float ly = 8;

            // Title
            GUI.color = Color.white;
            var titleS = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(10, ly, pw, 24), q.name, titleS);
            ly += 26;

            // Subtitle
            GUI.color = DimText;
            string sub = q.type;
            if (q.totalChapters > 0) sub += $"  -  Chapter {q.chapter}/{q.totalChapters}";
            GUI.Label(new Rect(10, ly, pw, 16), sub, SmallStyle());
            ly += 20;

            // Track button
            GUI.color = q.isTracked ? Gold : Cyan;
            if (GUI.Button(new Rect(10, ly, 80, 20), q.isTracked ? "TRACKING" : "TRACK"))
            {
                OnTrackQuest?.Invoke(q.id);
                TrackQuest(q.id);
            }
            ly += 28;

            // Objectives
            DrawRect(new Rect(10, ly, pw, 1), BorderColor);
            ly += 6;
            GUI.color = new Color(0.7f, 0.7f, 0.8f);
            GUI.Label(new Rect(10, ly, pw, 16), "OBJECTIVES", SmallBold());
            ly += 20;

            if (q.objectives != null)
            {
                foreach (var obj in q.objectives)
                {
                    GUI.color = obj.completed ? Cyan : DimText;
                    string mark = obj.completed ? "\u2713" : "\u25CB";
                    string count = obj.target > 1 ? $" ({obj.current}/{obj.target})" : "";
                    GUI.Label(new Rect(14, ly, pw - 10, 18), $"{mark}  {obj.description}{count}");
                    ly += 20;
                }
            }
            ly += 8;

            // Rewards
            DrawRect(new Rect(10, ly, pw, 1), BorderColor);
            ly += 6;
            GUI.color = new Color(0.7f, 0.7f, 0.8f);
            GUI.Label(new Rect(10, ly, pw, 16), "REWARDS", SmallBold());
            ly += 20;

            if (q.rewards != null)
            {
                foreach (var r in q.rewards)
                {
                    GUI.color = r.type == "xp" ? Cyan : r.type == "credits" ? Gold
                        : r.type == "standing" ? new Color(0.6f, 0.3f, 0.8f)
                        : RarityColor(r.rarity);
                    GUI.Label(new Rect(14, ly, pw - 10, 18), $"{r.label}: {r.value}");
                    ly += 20;
                }
            }
            ly += 8;

            // Lore
            if (!string.IsNullOrEmpty(q.loreText))
            {
                DrawRect(new Rect(10, ly, pw, 1), BorderColor);
                ly += 6;
                GUI.color = new Color(0.4f, 0.45f, 0.6f);
                var loreS = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, wordWrap = true };
                float loreH = loreS.CalcHeight(new GUIContent(q.loreText), pw - 20);
                GUI.Label(new Rect(14, ly, pw - 20, loreH), q.loreText, loreS);
                ly += loreH + 12;
            }

            // Abandon
            if (canAbandon)
            {
                ly += 10;
                if (!_confirmAbandon)
                {
                    GUI.color = DimRed;
                    if (GUI.Button(new Rect(10, ly, 130, 22), "ABANDON QUEST")) _confirmAbandon = true;
                }
                else
                {
                    GUI.color = new Color(0.9f, 0.3f, 0.3f);
                    GUI.Label(new Rect(10, ly, pw, 18), "Are you sure?");
                    ly += 22;
                    GUI.color = DimRed;
                    if (GUI.Button(new Rect(10, ly, 60, 20), "Yes"))
                    {
                        OnAbandonQuest?.Invoke(q.id);
                        AbandonQuest(q.id);
                        _confirmAbandon = false;
                    }
                    GUI.color = DimText;
                    if (GUI.Button(new Rect(80, ly, 60, 20), "No")) _confirmAbandon = false;
                }
            }

            GUI.color = Color.white;
            GUI.EndScrollView();
        }

        // ===================== AVAILABLE TAB =====================

        private void DrawAvailableTab(Rect area)
        {
            _scrollLeft = GUI.BeginScrollView(area, _scrollLeft, new Rect(0, 0, area.width - 16, _availableQuests.Length * 44 + 20));
            float cy = 8;
            bool full = _activeQuests.Length >= 5;

            for (int i = 0; i < _availableQuests.Length; i++)
            {
                var q = _availableQuests[i];
                var rowR = new Rect(8, cy, area.width - 32, 40);
                bool hover = rowR.Contains(Event.current.mousePosition - area.position + _scrollLeft);
                DrawRect(rowR, hover ? HoverBg : (i % 2 == 0 ? RowAlt : Color.clear));

                // Name
                GUI.color = q.canAccept && !full ? Color.white : DimText;
                GUI.Label(new Rect(12, cy + 4, 220, 18), q.name);

                // Level requirement
                bool levelMet = q.canAccept;
                GUI.color = levelMet ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
                GUI.Label(new Rect(240, cy + 4, 50, 18), $"L{q.levelReq}", SmallStyle());

                // Source + distance
                GUI.color = DimText;
                GUI.Label(new Rect(12, cy + 22, 300, 16), $"{q.sourceName}  ({q.distance:F0}m)", SmallStyle());

                // Accept button
                float btnX = area.width - 100;
                if (q.canAccept && !full)
                {
                    GUI.color = Cyan;
                    if (GUI.Button(new Rect(btnX, cy + 8, 64, 22), "ACCEPT"))
                    {
                        OnAcceptQuest?.Invoke(q.id);
                        AcceptQuest(q.id);
                    }
                }
                else
                {
                    GUI.color = DimText;
                    GUI.Label(new Rect(btnX, cy + 10, 64, 18), full ? "5/5" : "Locked", SmallStyle());
                }

                cy += 44;
            }

            if (_availableQuests.Length == 0)
            {
                GUI.color = DimText;
                GUI.Label(new Rect(20, 30, 300, 20), "No quests available nearby.");
            }

            GUI.color = Color.white;
            GUI.EndScrollView();
        }

        // ===================== COMPLETED TAB =====================

        private void DrawCompletedTab(Rect area)
        {
            _scrollLeft = GUI.BeginScrollView(area, _scrollLeft, new Rect(0, 0, area.width - 16, _completedQuests.Length * 28 + 20));
            float cy = 8;

            for (int i = 0; i < _completedQuests.Length; i++)
            {
                var q = _completedQuests[i];
                var rowR = new Rect(8, cy, area.width - 32, 24);
                bool hover = rowR.Contains(Event.current.mousePosition - area.position + _scrollLeft);
                DrawRect(rowR, hover ? HoverBg : (i % 2 == 0 ? RowAlt : Color.clear));

                GUI.color = Cyan;
                GUI.Label(new Rect(12, cy + 3, 16, 18), "\u2713");
                GUI.color = new Color(0.75f, 0.75f, 0.8f);
                GUI.Label(new Rect(28, cy + 3, 200, 18), q.name);

                GUI.color = DimText;
                var rightS = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
                GUI.Label(new Rect(8, cy + 3, area.width - 52, 18), $"{q.completedAgo}   +{q.xpGained} XP", rightS);

                // Click for read-only detail (future: expand inline)
                if (hover && Event.current.type == EventType.MouseDown) Event.current.Use();
                cy += 28;
            }

            if (_completedQuests.Length == 0)
            {
                GUI.color = DimText;
                GUI.Label(new Rect(20, 30, 300, 20), "No completed quests yet.");
            }

            GUI.color = Color.white;
            GUI.EndScrollView();
        }

        // ===================== CODEX TAB =====================

        private void DrawCodexTab(Rect area)
        {
            float catBarH = 26;
            float catW = (area.width - 16) / CodexCategories.Length;

            // Category buttons
            for (int i = 0; i < CodexCategories.Length; i++)
            {
                var cat = CodexCategories[i];
                bool active = _activeCodexCategory == cat;
                var btnR = new Rect(area.x + 8 + i * catW, area.y + 4, catW - 4, catBarH - 4);
                bool hover = btnR.Contains(Event.current.mousePosition);
                DrawRect(btnR, active ? new Color(0.12f, 0.14f, 0.22f) : hover ? HoverBg : Color.clear);

                GUI.color = active ? Cyan : DimText;
                var catS = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                GUI.Label(btnR, cat, catS);

                // Unread gold dot
                bool hasUnread = _codexEntries.Any(e => e.category == cat && e.isNew);
                if (hasUnread)
                {
                    GUI.color = Gold;
                    GUI.Label(new Rect(btnR.xMax - 10, btnR.y + 2, 8, 8), "\u2022");
                }

                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _activeCodexCategory = cat;
                    _selectedCodexIdx = -1;
                    Event.current.Use();
                }
            }

            // Filter entries
            var filtered = _codexEntries.Where(e => e.category == _activeCodexCategory).ToArray();
            float entryListH = (area.height - catBarH) * 0.45f;
            var listR = new Rect(area.x, area.y + catBarH + 4, area.width, entryListH);

            _scrollCodexEntries = GUI.BeginScrollView(listR, _scrollCodexEntries, new Rect(0, 0, area.width - 16, filtered.Length * 26 + 10));
            float cy = 4;
            for (int i = 0; i < filtered.Length; i++)
            {
                var e = filtered[i];
                bool sel = i == _selectedCodexIdx;
                var rowR = new Rect(4, cy, area.width - 24, 24);
                bool hover = rowR.Contains(Event.current.mousePosition - listR.position + _scrollCodexEntries);
                DrawRect(rowR, sel ? new Color(0.12f, 0.14f, 0.22f) : hover ? HoverBg : Color.clear);

                GUI.color = sel ? Color.white : new Color(0.8f, 0.8f, 0.85f);
                GUI.Label(new Rect(10, cy + 3, area.width - 80, 18), e.title);

                if (e.isNew)
                {
                    GUI.color = Gold;
                    GUI.Label(new Rect(area.width - 70, cy + 3, 50, 18), "[NEW]", SmallBold());
                }

                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _selectedCodexIdx = i;
                    // Mark as read
                    if (e.isNew)
                    {
                        int globalIdx = Array.IndexOf(_codexEntries, e);
                        if (globalIdx >= 0)
                        {
                            var updated = _codexEntries[globalIdx];
                            updated.isNew = false;
                            _codexEntries[globalIdx] = updated;
                        }
                    }
                    Event.current.Use();
                }
                cy += 26;
            }
            GUI.color = Color.white;
            GUI.EndScrollView();

            // Entry body
            var bodyR = new Rect(area.x, area.y + catBarH + entryListH + 8, area.width, area.height - catBarH - entryListH - 12);
            DrawRect(new Rect(bodyR.x + 8, bodyR.y, bodyR.width - 16, 1), BorderColor);

            if (_selectedCodexIdx >= 0 && _selectedCodexIdx < filtered.Length)
            {
                var entry = filtered[_selectedCodexIdx];
                var bodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
                float bodyH = bodyStyle.CalcHeight(new GUIContent(entry.body), bodyR.width - 32);
                float totalH = bodyH + 50;

                _scrollCodexBody = GUI.BeginScrollView(bodyR, _scrollCodexBody, new Rect(0, 0, bodyR.width - 16, totalH));

                GUI.color = Cyan;
                GUI.Label(new Rect(12, 6, bodyR.width - 24, 20), entry.title, SmallBold());
                GUI.color = new Color(0.75f, 0.75f, 0.8f);
                GUI.Label(new Rect(12, 26, bodyR.width - 24, bodyH), entry.body, bodyStyle);

                float footY = 28 + bodyH;
                GUI.color = DimText;
                if (!string.IsNullOrEmpty(entry.sourceQuest))
                    GUI.Label(new Rect(12, footY, 300, 16), $"Source: {entry.sourceQuest}", SmallStyle());
                GUI.Label(new Rect(bodyR.width - 160, footY, 140, 16), entry.discoveredDate, SmallStyle());

                GUI.color = Color.white;
                GUI.EndScrollView();
            }
        }

        // ===================== MISSIONS TAB =====================

        private void DrawMissionsTab(Rect area)
        {
            if (string.IsNullOrEmpty(_missionSettlement))
            {
                GUI.color = DimText;
                GUI.Label(new Rect(area.x + 20, area.y + 40, 300, 20), "No mission board in range.");
                GUI.color = Color.white;
                return;
            }

            // Header
            GUI.color = Color.white;
            var headerS = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(area.x + 12, area.y + 8, 300, 22), _missionSettlement, headerS);

            GUI.color = DimText;
            int mins = Mathf.Max(0, (int)(_missionRefreshSeconds / 60));
            int secs = Mathf.Max(0, (int)(_missionRefreshSeconds % 60));
            var refreshS = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 11 };
            GUI.Label(new Rect(area.x, area.y + 10, area.width - 14, 18), $"Refreshes in: {mins}:{secs:D2}", refreshS);

            DrawRect(new Rect(area.x + 10, area.y + 32, area.width - 20, 1), BorderColor);

            // Group by type
            var types = new[] { "Kill", "Gather", "Deliver", "Escort", "Defend", "Explore", "Craft", "Investigate" };
            float listH = area.height - 70;
            var listR = new Rect(area.x, area.y + 36, area.width, listH);

            int totalRows = 0;
            foreach (var t in types) totalRows += _missions.Count(m => m.type == t) > 0 ? _missions.Count(m => m.type == t) + 1 : 0;

            _scrollLeft = GUI.BeginScrollView(listR, _scrollLeft, new Rect(0, 0, area.width - 16, totalRows * 32 + 20));
            float cy = 4;
            int selectedMission = -1;

            foreach (var mType in types)
            {
                var group = _missions.Where(m => m.type == mType).ToArray();
                if (group.Length == 0) continue;

                GUI.color = DimText;
                GUI.Label(new Rect(8, cy, 200, 18), mType.ToUpper(), SmallBold());
                cy += 22;

                foreach (var m in group)
                {
                    var rowR = new Rect(8, cy, area.width - 32, 28);
                    bool hover = rowR.Contains(Event.current.mousePosition - listR.position + _scrollLeft);
                    DrawRect(rowR, hover ? HoverBg : Color.clear);

                    // Urgent marker
                    float nx = 12;
                    if (m.isUrgent)
                    {
                        GUI.color = new Color(0.9f, 0.3f, 0.2f);
                        GUI.Label(new Rect(nx, cy + 4, 20, 18), "[!]");
                        nx += 22;
                    }

                    // Name
                    GUI.color = Color.white;
                    GUI.Label(new Rect(nx, cy + 4, 200, 18), m.name);

                    // Rewards inline
                    float rx = 240;
                    if (m.rewards != null)
                    {
                        foreach (var r in m.rewards)
                        {
                            GUI.color = r.type == "xp" ? Cyan : r.type == "credits" ? Gold : DimText;
                            string rStr = $"{r.value} {r.label}";
                            GUI.Label(new Rect(rx, cy + 4, 80, 18), rStr, SmallStyle());
                            rx += 80;
                        }
                    }

                    // Expiry
                    GUI.color = DimText;
                    var expiryS = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 10 };
                    GUI.Label(new Rect(8, cy + 4, area.width - 52, 18), $"{m.expiryHours:F1}h", expiryS);

                    if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        selectedMission = m.id;
                        Event.current.Use();
                    }
                    cy += 32;
                }
            }
            GUI.color = Color.white;
            GUI.EndScrollView();

            // Accept button
            var btnR2 = new Rect(area.x + area.width - 150, area.y + area.height - 30, 136, 24);
            GUI.color = Cyan;
            if (GUI.Button(btnR2, "ACCEPT SELECTED") && selectedMission >= 0)
            {
                OnAcceptMission?.Invoke(selectedMission);
            }
            GUI.color = Color.white;
        }

        // ===================== HELPERS =====================

        private static string MapTypeLabel(string type)
        {
            if (string.IsNullOrEmpty(type)) return "MISSION";
            var t = type.ToLower();
            if (t.Contains("story") || t.Contains("arc")) return "STORY ARC";
            if (t.Contains("faction")) return "FACTION";
            if (t.Contains("explor") || t.Contains("cartograph")) return "EXPLORATION";
            return "MISSION";
        }

        private static Color RarityColor(int rarity)
        {
            return rarity switch
            {
                1 => new Color(0.3f, 0.8f, 0.3f),   // Uncommon
                2 => new Color(0.3f, 0.5f, 1f),      // Rare
                3 => new Color(0.7f, 0.3f, 0.9f),    // Epic
                4 => Gold,                             // Legendary
                _ => new Color(0.75f, 0.75f, 0.75f),  // Common
            };
        }

        private static void DrawRect(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawBorder(Rect r, Color c)
        {
            DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }

        private static GUIStyle SmallStyle()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10 };
        }

        private static GUIStyle SmallBold()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
        }
    }
}

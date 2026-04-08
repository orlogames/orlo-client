using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI.Panels
{
    public class CombatPanelUI : MonoBehaviour
    {
        public static CombatPanelUI Instance { get; private set; }

        // --- Data Structs ---
        public struct ResistanceEntry { public string typeName; public float value, cap, conditionPercent; }
        public struct ActionSlot { public int slotIndex; public string abilityName, abilityIcon; }
        public struct AbilityInfo { public string name, category, icon, damageType; public float cooldown, cost; }
        public struct StanceInfo { public string weaponName, type, damage; public float speed, range, condition; public string bonus, penalty; }
        public struct CombatStats
        {
            public float avgDps, totalDealt, totalTaken, accuracy, critRate;
            public int kills, deaths, bestCombo, incaps;
            public float timeInCombat;
        }
        public struct DamageBreakdown { public string typeName; public float value; public Color color; }
        struct RecentKill { public string creature, timeAgo, damageType; public int damage; }

        // --- State ---
        bool _visible;
        int _tab;
        readonly string[] _tabNames = { "Overview", "Action Bars", "Stances", "Statistics" };

        // Health
        float _vit, _maxVit, _vitRegen, _sta, _maxSta, _staRegen, _foc, _maxFoc, _focRegen;
        // Resistances
        ResistanceEntry[] _resistances = new ResistanceEntry[0];
        // Modifiers
        int _combo; bool _momentum; string _stanceBonus = ""; string[] _activeEffects = new string[0];
        // Weapon
        string _wpnName = "", _wpnDmgRange = "", _wpnDmgType = ""; float _wpnSpeed, _wpnRange, _wpnCondition;
        // Action bars
        Dictionary<int, ActionSlot[]> _bars = new Dictionary<int, ActionSlot[]>();
        int _editingBar;
        // Abilities
        AbilityInfo[] _abilities = new AbilityInfo[0];
        int _pickedAbility = -1;
        Vector2 _abilityScroll;
        HashSet<string> _collapsedCategories = new HashSet<string>();
        // Stances
        StanceInfo[] _stances = new StanceInfo[0]; int _activeStance;
        // Stats
        CombatStats _sessionStats, _lifetimeStats; bool _showLifetime;
        DamageBreakdown[] _dealtBreakdown = new DamageBreakdown[0], _takenBreakdown = new DamageBreakdown[0];
        List<RecentKill> _recentKills = new List<RecentKill>();

        // Rendering
        Texture2D _px;
        readonly Color _bg = new Color(0.06f, 0.06f, 0.1f, 0.92f);
        readonly Color _border = new Color(0.25f, 0.3f, 0.5f);
        readonly Color _cyan = new Color(0.4f, 0.9f, 1f);
        readonly Color _gold = new Color(1f, 0.85f, 0.35f);
        readonly Color _tabActive = new Color(0.15f, 0.18f, 0.28f);
        readonly Color _tabHover = new Color(0.1f, 0.12f, 0.2f);
        readonly Color _slotBg = new Color(0.08f, 0.08f, 0.14f);
        readonly Color _barBg = new Color(0.04f, 0.04f, 0.08f);
        readonly Color _green = new Color(0.3f, 0.9f, 0.4f);
        readonly Color _red = new Color(0.9f, 0.3f, 0.3f);

        const int W = 680, H = 520;

        void Awake()
        {
            Instance = this;
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Toggle() { _visible = !_visible; _pickedAbility = -1; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.C) && !IsChatFocused()) Toggle();
            if (_visible && Input.GetKeyDown(KeyCode.Escape)) { _visible = false; _pickedAbility = -1; }
        }

        bool IsChatFocused()
        {
            var chat = FindObjectOfType<MonoBehaviour>();
            // Simple guard: if any input field is focused, skip toggle
            return GUIUtility.keyboardControl != 0;
        }

        // --- Data Setters ---
        public void SetHealthPools(float vit, float maxVit, float vitRegen, float sta, float maxSta, float staRegen, float foc, float maxFoc, float focRegen)
        { _vit = vit; _maxVit = maxVit; _vitRegen = vitRegen; _sta = sta; _maxSta = maxSta; _staRegen = staRegen; _foc = foc; _maxFoc = maxFoc; _focRegen = focRegen; }
        public void SetResistances(ResistanceEntry[] entries) => _resistances = entries ?? new ResistanceEntry[0];
        public void SetModifiers(int combo, bool momentum, string stanceBonus, string[] activeEffects)
        { _combo = combo; _momentum = momentum; _stanceBonus = stanceBonus ?? ""; _activeEffects = activeEffects ?? new string[0]; }
        public void SetWeapon(string name, string damageRange, string damageType, float speed, float range, float condition)
        { _wpnName = name ?? ""; _wpnDmgRange = damageRange ?? ""; _wpnDmgType = damageType ?? ""; _wpnSpeed = speed; _wpnRange = range; _wpnCondition = condition; }
        public void SetActionBar(int barIndex, ActionSlot[] slots) => _bars[barIndex] = slots ?? new ActionSlot[0];
        public void SetAbilities(AbilityInfo[] abilities) => _abilities = abilities ?? new AbilityInfo[0];
        public void SetStances(StanceInfo[] stances, int activeIndex) { _stances = stances ?? new StanceInfo[0]; _activeStance = activeIndex; }
        public void SetStats(CombatStats stats, bool isSession) { if (isSession) _sessionStats = stats; else _lifetimeStats = stats; }
        public void SetDamageBreakdown(DamageBreakdown[] dealt, DamageBreakdown[] taken) { _dealtBreakdown = dealt ?? new DamageBreakdown[0]; _takenBreakdown = taken ?? new DamageBreakdown[0]; }
        public void AddRecentKill(string creature, string timeAgo, int damage, string damageType)
        {
            _recentKills.Insert(0, new RecentKill { creature = creature, timeAgo = timeAgo, damage = damage, damageType = damageType });
            if (_recentKills.Count > 5) _recentKills.RemoveAt(5);
        }

        // --- Drawing Helpers ---
        void DrawRect(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _px); GUI.color = Color.white; }
        void DrawBorder(Rect r, Color c, int t = 1)
        {
            DrawRect(new Rect(r.x, r.y, r.width, t), c);
            DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            DrawRect(new Rect(r.x, r.y, t, r.height), c);
            DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
        void Label(Rect r, string text, Color c, int size = 12, TextAnchor align = TextAnchor.UpperLeft, bool bold = false)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = align, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            s.normal.textColor = c; s.wordWrap = true;
            GUI.Label(r, text, s);
        }
        void DrawBar(Rect r, float fill, Color fg, Color bg)
        {
            DrawRect(r, bg);
            if (fill > 0) DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), fg);
        }
        bool Button(Rect r, string text, Color textColor, Color bgColor, int size = 12)
        {
            var hover = r.Contains(Event.current.mousePosition);
            DrawRect(r, hover ? bgColor * 1.3f : bgColor);
            DrawBorder(r, _border);
            Label(r, text, textColor, size, TextAnchor.MiddleCenter);
            return hover && Event.current.type == EventType.MouseDown && Event.current.button == 0 && Consume();
        }
        bool Consume() { Event.current.Use(); return true; }

        // --- OnGUI ---
        void OnGUI()
        {
            if (!_visible) return;
            var panelR = new Rect((Screen.width - W) / 2f, (Screen.height - H) / 2f, W, H);
            DrawRect(panelR, _bg);
            DrawBorder(panelR, _border, 2);

            // Title
            Label(new Rect(panelR.x + 16, panelR.y + 8, 200, 24), "COMBAT", _cyan, 16, TextAnchor.MiddleLeft, true);
            // Close
            if (Button(new Rect(panelR.xMax - 32, panelR.y + 6, 24, 24), "X", _red, _slotBg, 14))
            { _visible = false; _pickedAbility = -1; }

            // Tabs
            float tabW = (W - 32) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var tr = new Rect(panelR.x + 16 + i * tabW, panelR.y + 36, tabW, 28);
                bool active = _tab == i;
                bool hover = tr.Contains(Event.current.mousePosition);
                DrawRect(tr, active ? _tabActive : (hover ? _tabHover : _slotBg));
                if (active) DrawRect(new Rect(tr.x, tr.yMax - 2, tr.width, 2), _cyan);
                Label(tr, _tabNames[i], active ? _cyan : Color.grey, 12, TextAnchor.MiddleCenter, active);
                if (!active && hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { _tab = i; _pickedAbility = -1; Event.current.Use(); }
            }

            var content = new Rect(panelR.x + 16, panelR.y + 72, W - 32, H - 88);
            switch (_tab)
            {
                case 0: DrawOverview(content); break;
                case 1: DrawActionBars(content); break;
                case 2: DrawStances(content); break;
                case 3: DrawStatistics(content); break;
            }
        }

        // ============ TAB 0: Overview ============
        void DrawOverview(Rect area)
        {
            float colW = (area.width - 16) / 3f;
            var left = new Rect(area.x, area.y, colW, area.height - 64);
            var center = new Rect(area.x + colW + 8, area.y, colW, area.height - 64);
            var right = new Rect(area.x + 2 * (colW + 8), area.y, colW, area.height - 64);

            // Left: Health Pools
            Label(left, "Health Pools", _cyan, 13, TextAnchor.UpperLeft, true);
            DrawPoolBar(new Rect(left.x, left.y + 22, left.width, 16), "VIT", _vit, _maxVit, _vitRegen, new Color(0.8f, 0.2f, 0.2f));
            DrawPoolBar(new Rect(left.x, left.y + 56, left.width, 16), "STA", _sta, _maxSta, _staRegen, new Color(0.2f, 0.7f, 0.2f));
            DrawPoolBar(new Rect(left.x, left.y + 90, left.width, 16), "FOC", _foc, _maxFoc, _focRegen, new Color(0.3f, 0.4f, 0.9f));

            // Center: Armor Resistances
            Label(center, "Armor Resistances", _cyan, 13, TextAnchor.UpperLeft, true);
            float ry = center.y + 22;
            Label(new Rect(center.x, ry, 70, 16), "Type", Color.grey, 10, TextAnchor.MiddleLeft, true);
            Label(new Rect(center.x + 72, ry, 36, 16), "Res", Color.grey, 10, TextAnchor.MiddleCenter, true);
            Label(new Rect(center.x + 110, ry, 36, 16), "Cap", Color.grey, 10, TextAnchor.MiddleCenter, true);
            Label(new Rect(center.x + 148, ry, 48, 16), "Cond", Color.grey, 10, TextAnchor.MiddleCenter, true);
            ry += 18;
            foreach (var res in _resistances)
            {
                Label(new Rect(center.x, ry, 70, 16), res.typeName, Color.white, 11);
                Label(new Rect(center.x + 72, ry, 36, 16), $"{res.value:F0}", _gold, 11, TextAnchor.MiddleCenter);
                Label(new Rect(center.x + 110, ry, 36, 16), $"{res.cap:F0}", Color.grey, 11, TextAnchor.MiddleCenter);
                Color cc = res.conditionPercent > 75 ? _green : (res.conditionPercent > 25 ? _gold : _red);
                Label(new Rect(center.x + 148, ry, 48, 16), $"{res.conditionPercent:F0}%", cc, 11, TextAnchor.MiddleCenter);
                ry += 18;
            }

            // Right: Combat Modifiers
            Label(right, "Combat Modifiers", _cyan, 13, TextAnchor.UpperLeft, true);
            float my = right.y + 22;
            Label(new Rect(right.x, my, right.width, 16), $"Combo: x{_combo}", _gold, 12); my += 18;
            Label(new Rect(right.x, my, right.width, 16), $"Momentum: {(_momentum ? "ACTIVE" : "none")}", _momentum ? _green : Color.grey, 12); my += 18;
            if (!string.IsNullOrEmpty(_stanceBonus))
            { Label(new Rect(right.x, my, right.width, 16), $"Stance: {_stanceBonus}", _green, 12); my += 18; }
            if (_activeEffects.Length > 0)
            {
                Label(new Rect(right.x, my, right.width, 16), "Effects:", Color.grey, 11); my += 16;
                foreach (var eff in _activeEffects)
                { Label(new Rect(right.x + 8, my, right.width - 8, 14), eff, _gold, 11); my += 15; }
            }

            // Bottom: Equipped Weapon
            var wpnR = new Rect(area.x, area.yMax - 56, area.width, 50);
            DrawRect(wpnR, _slotBg);
            DrawBorder(wpnR, _border);
            Label(new Rect(wpnR.x + 8, wpnR.y + 4, 160, 16), string.IsNullOrEmpty(_wpnName) ? "No Weapon" : _wpnName, _cyan, 13, TextAnchor.MiddleLeft, true);
            if (!string.IsNullOrEmpty(_wpnName))
            {
                Label(new Rect(wpnR.x + 180, wpnR.y + 4, 160, 16), $"{_wpnDmgRange} {_wpnDmgType}", _gold, 12);
                Label(new Rect(wpnR.x + 360, wpnR.y + 4, 100, 16), $"Spd {_wpnSpeed:F1}  Rng {_wpnRange:F0}", Color.grey, 11);
                DrawBar(new Rect(wpnR.x + 8, wpnR.y + 28, wpnR.width - 16, 10), _wpnCondition,
                    _wpnCondition > 0.75f ? _green : (_wpnCondition > 0.25f ? _gold : _red), _barBg);
                Label(new Rect(wpnR.x + 8, wpnR.y + 28, wpnR.width - 16, 10), $"{_wpnCondition * 100:F0}%", Color.white, 9, TextAnchor.MiddleCenter);
            }
        }

        void DrawPoolBar(Rect r, string label, float cur, float max, float regen, Color c)
        {
            Label(new Rect(r.x, r.y, 30, 14), label, c, 11, TextAnchor.MiddleLeft, true);
            var barR = new Rect(r.x + 32, r.y, r.width - 100, r.height);
            DrawBar(barR, max > 0 ? cur / max : 0, c, _barBg);
            Label(new Rect(barR.x, barR.y, barR.width, barR.height), $"{cur:F0}/{max:F0}", Color.white, 10, TextAnchor.MiddleCenter);
            Label(new Rect(barR.xMax + 4, r.y, 60, 14), $"+{regen:F1}/s", _gold, 10);
        }

        // ============ TAB 1: Action Bars ============
        void DrawActionBars(Rect area)
        {
            // Dropdown
            Label(new Rect(area.x, area.y, 60, 20), "Editing:", Color.grey, 12);
            if (Button(new Rect(area.x + 62, area.y, 140, 20), $"Bar {_editingBar + 1}", _cyan, _slotBg, 12))
                _editingBar = (_editingBar + 1) % Mathf.Max(1, _stances.Length > 0 ? _stances.Length : 1);

            // 10-slot bar
            float slotSize = 48, gap = 4, totalW = 10 * slotSize + 9 * gap;
            float startX = area.x + (area.width - totalW) / 2f;
            float barY = area.y + 30;
            ActionSlot[] slots = _bars.ContainsKey(_editingBar) ? _bars[_editingBar] : null;
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            for (int i = 0; i < 10; i++)
            {
                var sr = new Rect(startX + i * (slotSize + gap), barY, slotSize, slotSize);
                bool filled = slots != null && i < slots.Length && !string.IsNullOrEmpty(slots[i].abilityName);
                DrawRect(sr, _slotBg);
                if (filled)
                {
                    DrawBorder(sr, _cyan);
                    Label(new Rect(sr.x + 2, sr.y + 14, sr.width - 4, 20), slots[i].abilityName, Color.white, 9, TextAnchor.MiddleCenter);
                }
                else
                {
                    DrawBorder(sr, new Color(0.3f, 0.3f, 0.4f));
                    Label(sr, "--", Color.grey, 14, TextAnchor.MiddleCenter);
                }
                Label(new Rect(sr.x, sr.y + 1, sr.width - 3, 14), keys[i], Color.grey, 9, TextAnchor.UpperRight);

                // Click to place picked ability or clear slot
                if (sr.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    if (_pickedAbility >= 0 && _pickedAbility < _abilities.Length)
                    {
                        if (slots == null) { slots = new ActionSlot[10]; _bars[_editingBar] = slots; }
                        if (i < slots.Length) slots[i].abilityName = _abilities[_pickedAbility].name;
                        _pickedAbility = -1;
                    }
                    else if (filled)
                    {
                        slots[i].abilityName = null;
                    }
                    Event.current.Use();
                }
            }

            // Picked indicator
            if (_pickedAbility >= 0 && _pickedAbility < _abilities.Length)
                Label(new Rect(area.x, barY + slotSize + 4, area.width, 16), $"Placing: {_abilities[_pickedAbility].name} -- click a slot above", _gold, 11, TextAnchor.MiddleCenter);

            // Ability palette
            float paletteY = barY + slotSize + 26;
            Label(new Rect(area.x, paletteY, 200, 18), "Abilities", _cyan, 13, TextAnchor.UpperLeft, true);
            paletteY += 22;

            float scrollH = area.yMax - paletteY;
            var scrollArea = new Rect(area.x, paletteY, area.width, scrollH);
            float innerH = EstimatePaletteHeight();
            _abilityScroll = GUI.BeginScrollView(scrollArea, _abilityScroll, new Rect(0, 0, area.width - 20, innerH));

            float py = 0;
            var categories = new Dictionary<string, List<int>>();
            for (int i = 0; i < _abilities.Length; i++)
            {
                string cat = string.IsNullOrEmpty(_abilities[i].category) ? "Other" : _abilities[i].category;
                if (!categories.ContainsKey(cat)) categories[cat] = new List<int>();
                categories[cat].Add(i);
            }

            foreach (var kv in categories)
            {
                bool collapsed = _collapsedCategories.Contains(kv.Key);
                string arrow = collapsed ? "+" : "-";
                var hdrR = new Rect(0, py, area.width - 20, 20);
                Label(hdrR, $" {arrow}  {kv.Key}", Color.grey, 12, TextAnchor.MiddleLeft, true);
                if (hdrR.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { if (collapsed) _collapsedCategories.Remove(kv.Key); else _collapsedCategories.Add(kv.Key); Event.current.Use(); }
                py += 22;

                if (collapsed) continue;
                float ax = 0;
                foreach (int idx in kv.Value)
                {
                    if (ax + slotSize > area.width - 20) { ax = 0; py += slotSize + 18; }
                    var ar = new Rect(ax, py, slotSize, slotSize);
                    bool picked = _pickedAbility == idx;
                    DrawRect(ar, picked ? _tabActive : _slotBg);
                    DrawBorder(ar, picked ? _gold : _border);
                    Label(new Rect(ar.x + 2, ar.y + 10, ar.width - 4, 28), _abilities[idx].name, Color.white, 9, TextAnchor.MiddleCenter);
                    if (ar.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    { _pickedAbility = (_pickedAbility == idx) ? -1 : idx; Event.current.Use(); }
                    ax += slotSize + gap;
                }
                py += slotSize + 20;
            }
            GUI.EndScrollView();
        }

        float EstimatePaletteHeight()
        {
            float h = 0;
            var cats = new HashSet<string>();
            foreach (var a in _abilities) cats.Add(string.IsNullOrEmpty(a.category) ? "Other" : a.category);
            foreach (var cat in cats)
            {
                h += 22;
                if (_collapsedCategories.Contains(cat)) continue;
                int count = 0;
                foreach (var a in _abilities) if ((string.IsNullOrEmpty(a.category) ? "Other" : a.category) == cat) count++;
                int cols = Mathf.Max(1, (int)((W - 52) / 52f));
                int rows = Mathf.CeilToInt(count / (float)cols);
                h += rows * 68 + 2;
            }
            return h + 20;
        }

        // ============ TAB 2: Stances ============
        void DrawStances(Rect area)
        {
            int count = Mathf.Max(_stances.Length, 3);
            float colW = (area.width - 16) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var col = new Rect(area.x + i * (colW + 8), area.y, colW, area.height - 60);
                DrawRect(col, _slotBg);
                DrawBorder(col, i == _activeStance ? _cyan : _border);

                float cy = col.y + 6;
                string title = $"Stance {i + 1}";
                if (i == _activeStance) title += "  [ACTIVE]";
                Label(new Rect(col.x + 8, cy, colW - 16, 18), title, i == _activeStance ? _cyan : Color.grey, 13, TextAnchor.UpperLeft, true);
                cy += 24;

                if (i < _stances.Length)
                {
                    var st = _stances[i];
                    Label(new Rect(col.x + 8, cy, colW - 16, 16), st.weaponName, _gold, 12, TextAnchor.UpperLeft, true); cy += 20;
                    DrawStanceRow(col.x + 8, ref cy, colW - 16, "Type", st.type);
                    DrawStanceRow(col.x + 8, ref cy, colW - 16, "Damage", st.damage);
                    DrawStanceRow(col.x + 8, ref cy, colW - 16, "Speed", $"{st.speed:F1}");
                    DrawStanceRow(col.x + 8, ref cy, colW - 16, "Range", $"{st.range:F0}m");
                    cy += 4;
                    Label(new Rect(col.x + 8, cy, colW - 16, 14), "Condition", Color.grey, 10); cy += 14;
                    DrawBar(new Rect(col.x + 8, cy, colW - 16, 10), st.condition,
                        st.condition > 0.75f ? _green : (st.condition > 0.25f ? _gold : _red), _barBg); cy += 16;
                    if (!string.IsNullOrEmpty(st.bonus))
                    { Label(new Rect(col.x + 8, cy, colW - 16, 14), $"+{st.bonus}", _green, 11); cy += 16; }
                    if (!string.IsNullOrEmpty(st.penalty))
                    { Label(new Rect(col.x + 8, cy, colW - 16, 14), $"-{st.penalty}", _red, 11); cy += 16; }
                }
                else
                {
                    Label(new Rect(col.x + 8, cy + 40, colW - 16, 40), "Equip a weapon\nto this stance", Color.grey, 12, TextAnchor.MiddleCenter);
                }

                // Click to activate
                if (i < _stances.Length && col.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { _activeStance = i; Event.current.Use(); }
            }

            // Comparison row
            float compY = area.yMax - 50;
            DrawRect(new Rect(area.x, compY, area.width, 44), _slotBg);
            DrawBorder(new Rect(area.x, compY, area.width, 44), _border);
            Label(new Rect(area.x + 8, compY + 2, 100, 14), "Comparison", _cyan, 11, TextAnchor.MiddleLeft, true);
            string[] labels = { "Eff. DPS", "Range", "Def Mod" };
            for (int r = 0; r < 3; r++)
            {
                Label(new Rect(area.x + 8, compY + 18 + r * 0, 60, 14), "", Color.grey, 10); // spacer
            }
            // Header row
            for (int c = 0; c < 3; c++)
            {
                float cx = area.x + 80 + c * colW;
                if (c < _stances.Length)
                {
                    var s = _stances[c];
                    float dps = s.speed > 0 ? float.Parse(s.damage.Split('-')[0]) / s.speed : 0;
                    Label(new Rect(cx, compY + 16, colW - 16, 12), $"DPS:{dps:F1} Rng:{s.range:F0}m", _gold, 10, TextAnchor.MiddleCenter);
                }
                else
                {
                    Label(new Rect(cx, compY + 16, colW - 16, 12), "--", Color.grey, 10, TextAnchor.MiddleCenter);
                }
            }
        }

        void DrawStanceRow(float x, ref float y, float w, string label, string value)
        {
            Label(new Rect(x, y, 60, 16), label, Color.grey, 11);
            Label(new Rect(x + 62, y, w - 62, 16), value, Color.white, 11);
            y += 18;
        }

        // ============ TAB 3: Statistics ============
        void DrawStatistics(Rect area)
        {
            // Toggle pill
            float pillW = 200, pillH = 22;
            float pillX = area.x + (area.width - pillW) / 2f;
            DrawRect(new Rect(pillX, area.y, pillW, pillH), _slotBg);
            DrawBorder(new Rect(pillX, area.y, pillW, pillH), _border);
            var leftPill = new Rect(pillX, area.y, pillW / 2, pillH);
            var rightPill = new Rect(pillX + pillW / 2, area.y, pillW / 2, pillH);
            DrawRect(!_showLifetime ? leftPill : rightPill, _tabActive);
            Label(leftPill, "This Session", !_showLifetime ? _cyan : Color.grey, 11, TextAnchor.MiddleCenter);
            Label(rightPill, "Lifetime", _showLifetime ? _cyan : Color.grey, 11, TextAnchor.MiddleCenter);
            if (leftPill.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            { _showLifetime = false; Event.current.Use(); }
            if (rightPill.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            { _showLifetime = true; Event.current.Use(); }

            var stats = _showLifetime ? _lifetimeStats : _sessionStats;
            float colW = (area.width - 16) / 2f;
            float sy = area.y + 32;

            // Left: Performance
            Label(new Rect(area.x, sy, colW, 16), "Performance", _cyan, 13, TextAnchor.UpperLeft, true);
            sy += 20;
            DrawStatRow(area.x, ref sy, colW, "Avg DPS", $"{stats.avgDps:F1}");
            DrawStatRow(area.x, ref sy, colW, "Total Dealt", $"{stats.totalDealt:F0}");
            DrawStatRow(area.x, ref sy, colW, "Total Taken", $"{stats.totalTaken:F0}");
            DrawStatRow(area.x, ref sy, colW, "Kills", $"{stats.kills}");
            DrawStatRow(area.x, ref sy, colW, "Deaths", $"{stats.deaths}");
            float kd = stats.deaths > 0 ? stats.kills / (float)stats.deaths : stats.kills;
            DrawStatRow(area.x, ref sy, colW, "K/D", $"{kd:F2}");
            DrawStatRow(area.x, ref sy, colW, "Accuracy", $"{stats.accuracy:F1}%");
            DrawStatRow(area.x, ref sy, colW, "Crit Rate", $"{stats.critRate:F1}%");
            DrawStatRow(area.x, ref sy, colW, "Best Combo", $"{stats.bestCombo}");
            DrawStatRow(area.x, ref sy, colW, "Incaps", $"{stats.incaps}");
            int mins = (int)(stats.timeInCombat / 60);
            int secs = (int)(stats.timeInCombat % 60);
            DrawStatRow(area.x, ref sy, colW, "Time in Combat", $"{mins}m {secs}s");

            // Right: Damage Breakdown
            float rx = area.x + colW + 16;
            float ry = area.y + 32;
            Label(new Rect(rx, ry, colW - 16, 16), "Damage Dealt by Type", _cyan, 13, TextAnchor.UpperLeft, true);
            ry += 20;
            DrawDamageBreakdown(rx, ref ry, colW - 16, _dealtBreakdown);
            ry += 10;
            Label(new Rect(rx, ry, colW - 16, 16), "Damage Taken by Type", _cyan, 13, TextAnchor.UpperLeft, true);
            ry += 20;
            DrawDamageBreakdown(rx, ref ry, colW - 16, _takenBreakdown);

            // Bottom: Recent Kills
            float killY = area.yMax - 90;
            Label(new Rect(area.x, killY, area.width, 16), "Recent Kills", _cyan, 13, TextAnchor.UpperLeft, true);
            killY += 18;
            DrawRect(new Rect(area.x, killY, area.width, 68), _slotBg);
            DrawBorder(new Rect(area.x, killY, area.width, 68), _border);
            float ky = killY + 4;
            if (_recentKills.Count == 0)
            {
                Label(new Rect(area.x + 8, ky + 16, area.width - 16, 16), "No kills yet", Color.grey, 11, TextAnchor.MiddleCenter);
            }
            else
            {
                foreach (var kill in _recentKills)
                {
                    Label(new Rect(area.x + 8, ky, 160, 13), kill.creature, Color.white, 11);
                    Label(new Rect(area.x + 170, ky, 80, 13), kill.timeAgo, Color.grey, 10);
                    Label(new Rect(area.x + 260, ky, 80, 13), $"{kill.damage} dmg", _gold, 10);
                    Label(new Rect(area.x + 350, ky, 80, 13), kill.damageType, GetTypeColor(kill.damageType), 10);
                    ky += 13;
                }
            }
        }

        void DrawStatRow(float x, ref float y, float w, string label, string value)
        {
            Label(new Rect(x, y, w * 0.55f, 16), label, Color.grey, 11);
            Label(new Rect(x + w * 0.55f, y, w * 0.45f, 16), value, _gold, 11, TextAnchor.UpperRight);
            y += 17;
        }

        void DrawDamageBreakdown(float x, ref float y, float w, DamageBreakdown[] data)
        {
            if (data.Length == 0) { Label(new Rect(x, y, w, 14), "No data", Color.grey, 10); y += 16; return; }
            float maxVal = 1;
            foreach (var d in data) if (d.value > maxVal) maxVal = d.value;
            foreach (var d in data)
            {
                Label(new Rect(x, y, 70, 14), d.typeName, Color.grey, 10);
                float barW = (w - 120) * Mathf.Clamp01(d.value / maxVal);
                DrawRect(new Rect(x + 72, y + 2, barW, 10), d.color);
                DrawRect(new Rect(x + 72, y + 2, w - 120, 10), new Color(d.color.r, d.color.g, d.color.b, 0.12f));
                Label(new Rect(x + w - 46, y, 46, 14), $"{d.value:F0}", _gold, 10, TextAnchor.UpperRight);
                y += 16;
            }
        }

        Color GetTypeColor(string type)
        {
            if (type == null) return Color.white;
            switch (type.ToLower())
            {
                case "kinetic": return Color.white;
                case "energy": return _cyan;
                case "explosive": return new Color(1f, 0.6f, 0.2f);
                case "convergence": return new Color(0.7f, 0.3f, 1f);
                case "thermal": return _red;
                case "corrosive": return _green;
                default: return Color.grey;
            }
        }
    }
}

using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Player profile panel showing character info, stats, skills, and achievements.
    /// Toggle with P key. Uses OnGUI for rapid prototyping.
    /// </summary>
    public class PlayerProfileUI : MonoBehaviour
    {
        public static PlayerProfileUI Instance { get; private set; }

        private bool _visible;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        // Character info
        private string _characterName = "Unknown";
        private string _raceName = "Human";
        private int _level = 1;
        private string _factionName = "Unaffiliated";
        private string _guildName = "";
        private string _title = "";

        // Pools
        private float _vitality, _maxVitality = 100;
        private float _stamina, _maxStamina = 100;
        private float _focus, _maxFocus = 100;

        // Armor ratings (6 damage types)
        private float[] _armorRatings = new float[6];
        private static readonly string[] ArmorLabels = {
            "Kinetic", "Energy", "Explosive", "Convergence", "Thermal", "Corrosive"
        };

        // Skills
        private struct SkillEntry
        {
            public string Name;
            public int Level;
            public int MaxLevel;
        }
        private SkillEntry[] _skills = new SkillEntry[0];

        // Lifetime stats
        private int _kills;
        private int _deaths;
        private int _itemsCrafted;
        private int _resourcesGathered;
        private float _distanceTraveled;
        private float _playtimeHours;

        // Layout
        private const float WinW = 360f;
        private const float WinH = 520f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P)) _visible = !_visible;
        }

        // ── Public API for server data ──────────────────────────────────

        public void SetCharacterInfo(string name, string race, int level, string faction, string guild, string title)
        {
            _characterName = name;
            _raceName = race;
            _level = level;
            _factionName = faction;
            _guildName = guild;
            _title = title;
        }

        public void SetPools(float vit, float maxVit, float stam, float maxStam, float foc, float maxFoc)
        {
            _vitality = vit; _maxVitality = maxVit > 0 ? maxVit : 1;
            _stamina = stam; _maxStamina = maxStam > 0 ? maxStam : 1;
            _focus = foc; _maxFocus = maxFoc > 0 ? maxFoc : 1;
        }

        public void SetArmorRatings(float[] ratings)
        {
            if (ratings != null && ratings.Length == 6) _armorRatings = ratings;
        }

        public void SetSkills(SkillEntry[] skills)
        {
            if (skills != null) _skills = skills;
        }

        public void SetLifetimeStats(int kills, int deaths, int crafted, int gathered, float distance, float playtime)
        {
            _kills = kills;
            _deaths = deaths;
            _itemsCrafted = crafted;
            _resourcesGathered = gathered;
            _distanceTraveled = distance;
            _playtimeHours = playtime;
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
            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "Player Profile", titleStyle);

            // Close button
            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            {
                _visible = false;
                return;
            }

            HandleDrag(titleBar);

            // Content area
            float contentX = _windowPos.x + 12;
            float contentW = WinW - 24;
            Rect scrollArea = new Rect(_windowPos.x + 4, _windowPos.y + 32, WinW - 8, WinH - 36);

            GUILayout.BeginArea(scrollArea);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // ── Character Info ───────────────────────────────────────
            SectionHeader("Character");

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.85f, 0.5f) }
            };
            GUILayout.Label(_characterName, nameStyle);

            if (!string.IsNullOrEmpty(_title))
            {
                var ttlStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.9f) }
                };
                GUILayout.Label(_title, ttlStyle);
            }

            InfoRow("Race", _raceName);
            InfoRow("Level", _level.ToString());
            InfoRow("Faction", _factionName);
            if (!string.IsNullOrEmpty(_guildName))
                InfoRow("Guild", _guildName);

            // ── Health Pools ─────────────────────────────────────────
            SectionHeader("Health Pools");

            DrawPoolBar("Vitality", _vitality, _maxVitality, new Color(0.85f, 0.15f, 0.15f));
            DrawPoolBar("Stamina", _stamina, _maxStamina, new Color(0.15f, 0.75f, 0.25f));
            DrawPoolBar("Focus", _focus, _maxFocus, new Color(0.25f, 0.45f, 0.95f));

            // ── Armor Ratings ────────────────────────────────────────
            SectionHeader("Armor Ratings");

            for (int i = 0; i < 6; i++)
            {
                InfoRow(ArmorLabels[i], $"{_armorRatings[i]:F0}");
            }

            // ── Skills ──────────────────────────────────────────────
            if (_skills.Length > 0)
            {
                SectionHeader("Skills");

                foreach (var skill in _skills)
                {
                    GUILayout.BeginHorizontal();
                    var lbl = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
                    GUILayout.Label(skill.Name, lbl, GUILayout.Width(160));

                    // Skill level bar
                    float pct = skill.MaxLevel > 0 ? (float)skill.Level / skill.MaxLevel : 0;
                    Rect barRect = GUILayoutUtility.GetRect(140, 14);
                    GUI.color = new Color(0.15f, 0.15f, 0.15f);
                    GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                    GUI.color = new Color(0.3f, 0.7f, 0.9f);
                    GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    var valStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                    GUI.Label(barRect, $"{skill.Level}/{skill.MaxLevel}", valStyle);

                    GUILayout.EndHorizontal();
                }
            }

            // ── Lifetime Stats ──────────────────────────────────────
            SectionHeader("Lifetime Stats");

            InfoRow("Kills", _kills.ToString());
            InfoRow("Deaths", _deaths.ToString());
            InfoRow("K/D Ratio", _deaths > 0 ? $"{(float)_kills / _deaths:F2}" : "N/A");
            InfoRow("Items Crafted", _itemsCrafted.ToString());
            InfoRow("Resources Gathered", _resourcesGathered.ToString());
            InfoRow("Distance Traveled", $"{_distanceTraveled:F0}m");
            InfoRow("Playtime", $"{_playtimeHours:F1} hours");

            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private void SectionHeader(string text)
        {
            GUILayout.Space(6);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.8f, 1f) }
            };
            GUILayout.Label(text, style);

            Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Space(2);
        }

        private void InfoRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            var valStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            GUILayout.Label(label, lblStyle, GUILayout.Width(150));
            GUILayout.Label(value, valStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawPoolBar(string label, float current, float max, Color color)
        {
            GUILayout.BeginHorizontal();
            var lbl = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            GUILayout.Label(label, lbl, GUILayout.Width(70));

            Rect barRect = GUILayoutUtility.GetRect(200, 16);
            float pct = Mathf.Clamp01(current / max);

            GUI.color = new Color(0.12f, 0.12f, 0.12f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var valStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(barRect, $"{current:F0} / {max:F0}", valStyle);

            GUILayout.EndHorizontal();
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

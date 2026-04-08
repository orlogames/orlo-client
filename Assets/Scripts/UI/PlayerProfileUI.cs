using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Player profile / identity card panel.
    /// Toggle with P key for own profile. Also used for inspecting other players.
    /// Shows: bio, title, achievement showcase, commendations, top skills, full stats.
    /// Uses OnGUI for rapid prototyping.
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
        private bool _isOwnProfile = true;

        // Identity card fields
        private string _bio = "";
        private bool _editingBio;
        private string _bioInput = "";
        private int _selectedTitleIndex;
        private string[] _availableTitles = { "(None)", "Explorer", "Pathfinder", "Veteran", "Artisan", "Champion" };
        private bool _titleDropdown;
        private int _commendationCount;

        // Achievement showcase (5 slots)
        public struct AchievementSlot
        {
            public string Name;
            public string Description;
            public bool Filled;
        }
        private AchievementSlot[] _showcase = new AchievementSlot[5];

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
        public struct SkillEntry
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

        private const float WinW = 380f;
        private const float WinH = 560f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P) && !ChatUI.Instance?.IsInputActive == true)
            {
                if (_visible && !_isOwnProfile)
                {
                    // Pressing P while viewing someone else switches back to own profile
                    _isOwnProfile = true;
                }
                else
                {
                    Toggle();
                }
            }
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible) _isOwnProfile = true;
        }

        // ---- Public API for server data ----

        public void SetCharacterInfo(string name, string race, int level, string faction, string guild, string title)
        {
            _characterName = name; _raceName = race; _level = level;
            _factionName = faction; _guildName = guild; _title = title;
        }

        public void SetIdentityCard(string bio, int titleIndex, int commendations, AchievementSlot[] showcase)
        {
            _bio = bio ?? "";
            _selectedTitleIndex = titleIndex;
            _commendationCount = commendations;
            if (showcase != null && showcase.Length == 5) _showcase = showcase;
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
            _kills = kills; _deaths = deaths; _itemsCrafted = crafted;
            _resourcesGathered = gathered; _distanceTraveled = distance; _playtimeHours = playtime;
        }

        /// <summary>Show read-only profile for another player.</summary>
        public void ShowOtherProfile(string name, string race, int level, string faction, string guild,
            string title, string bio, int commendations, AchievementSlot[] showcase, SkillEntry[] topSkills)
        {
            _isOwnProfile = false;
            _characterName = name; _raceName = race; _level = level;
            _factionName = faction; _guildName = guild; _title = title;
            _bio = bio ?? ""; _commendationCount = commendations;
            if (showcase != null && showcase.Length == 5) _showcase = showcase;
            if (topSkills != null) _skills = topSkills;
            _visible = true;
        }

        // ---- OnGUI ----

        private void OnGUI()
        {
            if (!_visible) return;

            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, WinW - 28, 28);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(_windowPos.x, _windowPos.y, WinW, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string headerText = _isOwnProfile ? "Player Profile" : $"Profile: {_characterName}";
            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 250, 28), headerText,
                new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } });

            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            { _visible = false; return; }
            HandleDrag(titleBar);

            // Content
            Rect scrollArea = new Rect(_windowPos.x + 4, _windowPos.y + 32, WinW - 8, WinH - 36);
            GUILayout.BeginArea(scrollArea);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // ---- Character Info ----
            SectionHeader("Character");

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.9f, 0.85f, 0.5f) } };
            GUILayout.Label(_characterName, nameStyle);

            // Title
            if (_isOwnProfile)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Title:", DimLabelStyle(), GUILayout.Width(40));
                if (GUILayout.Button(_selectedTitleIndex > 0 && _selectedTitleIndex < _availableTitles.Length
                    ? _availableTitles[_selectedTitleIndex] : "(None)", GUILayout.Width(120)))
                    _titleDropdown = !_titleDropdown;
                GUILayout.EndHorizontal();

                if (_titleDropdown)
                {
                    for (int i = 0; i < _availableTitles.Length; i++)
                    {
                        if (GUILayout.Button(_availableTitles[i], SmallLabelStyle(), GUILayout.Width(120)))
                        {
                            _selectedTitleIndex = i;
                            _titleDropdown = false;
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.SetPlayerProfile(_bio, _selectedTitleIndex));
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(_title))
            {
                GUILayout.Label(_title, new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic, normal = { textColor = new Color(0.7f, 0.7f, 0.9f) } });
            }

            InfoRow("Race", _raceName);
            InfoRow("Level", _level.ToString());
            InfoRow("Faction", _factionName);
            if (!string.IsNullOrEmpty(_guildName)) InfoRow("Guild", _guildName);
            InfoRow("Commendations", _commendationCount.ToString());

            // ---- Bio ----
            SectionHeader("Bio");
            if (_isOwnProfile)
            {
                if (_editingBio)
                {
                    _bioInput = GUILayout.TextArea(_bioInput, 200, GUILayout.Height(40));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Save", GUILayout.Width(50)))
                    {
                        _bio = _bioInput;
                        _editingBio = false;
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.SetPlayerProfile(_bio, _selectedTitleIndex));
                    }
                    if (GUILayout.Button("Cancel", GUILayout.Width(50)))
                        _editingBio = false;
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label(string.IsNullOrEmpty(_bio) ? "(No bio set)" : _bio, BioStyle());
                    if (GUILayout.Button("Edit Bio", GUILayout.Width(70)))
                    {
                        _editingBio = true;
                        _bioInput = _bio;
                    }
                }
            }
            else
            {
                GUILayout.Label(string.IsNullOrEmpty(_bio) ? "(No bio)" : _bio, BioStyle());
            }

            // ---- Achievement Showcase ----
            SectionHeader("Achievement Showcase");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 5; i++)
            {
                var slot = _showcase[i];
                Rect r = GUILayoutUtility.GetRect(56, 40);

                GUI.color = slot.Filled ? new Color(0.15f, 0.2f, 0.3f, 0.9f) : new Color(0.1f, 0.1f, 0.12f, 0.5f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);

                if (slot.Filled)
                {
                    GUI.color = new Color(1f, 0.85f, 0.3f);
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    var aStyle = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
                    GUI.Label(r, slot.Name, aStyle);

                    // Tooltip on hover
                    if (r.Contains(Event.current.mousePosition) && !string.IsNullOrEmpty(slot.Description))
                    {
                        var tip = new GUIStyle(GUI.skin.box) { fontSize = 10, wordWrap = true, normal = { textColor = Color.white } };
                        GUI.Box(new Rect(r.x, r.y - 30, 120, 28), slot.Description, tip);
                    }
                }
                else
                {
                    GUI.color = new Color(0.3f, 0.3f, 0.4f);
                    var eStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.4f, 0.4f, 0.5f) } };
                    GUI.Label(r, "Empty", eStyle);
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();

            // ---- Health Pools ----
            if (_isOwnProfile)
            {
                SectionHeader("Health Pools");
                DrawPoolBar("Vitality", _vitality, _maxVitality, new Color(0.85f, 0.15f, 0.15f));
                DrawPoolBar("Stamina", _stamina, _maxStamina, new Color(0.15f, 0.75f, 0.25f));
                DrawPoolBar("Focus", _focus, _maxFocus, new Color(0.25f, 0.45f, 0.95f));

                // ---- Armor Ratings ----
                SectionHeader("Armor Ratings");
                for (int i = 0; i < 6; i++)
                    InfoRow(ArmorLabels[i], $"{_armorRatings[i]:F0}");
            }

            // ---- Top Skills ----
            if (_skills.Length > 0)
            {
                SectionHeader(_isOwnProfile ? "Skills" : "Top Skills");
                int displayCount = _isOwnProfile ? _skills.Length : Mathf.Min(_skills.Length, 3);
                for (int i = 0; i < displayCount; i++)
                {
                    var skill = _skills[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(skill.Name, SmallLabelStyle(), GUILayout.Width(160));
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

            // ---- Lifetime Stats ----
            if (_isOwnProfile)
            {
                SectionHeader("Lifetime Stats");
                InfoRow("Kills", _kills.ToString());
                InfoRow("Deaths", _deaths.ToString());
                InfoRow("K/D Ratio", _deaths > 0 ? $"{(float)_kills / _deaths:F2}" : "N/A");
                InfoRow("Items Crafted", _itemsCrafted.ToString());
                InfoRow("Resources Gathered", _resourcesGathered.ToString());
                InfoRow("Distance Traveled", $"{_distanceTraveled:F0}m");
                InfoRow("Playtime", $"{_playtimeHours:F1} hours");
            }

            // Commend button (when viewing others)
            if (!_isOwnProfile)
            {
                GUILayout.Space(10);
                if (GUILayout.Button("Commend Player", GUILayout.Width(120)))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.CommendPlayer(_characterName));
                }
            }

            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---- Helpers ----

        private void SectionHeader(string text)
        {
            GUILayout.Space(6);
            GUILayout.Label(text, new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.8f, 1f) } });
            Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Space(2);
        }

        private void InfoRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, DimLabelStyle(), GUILayout.Width(150));
            GUILayout.Label(value, SmallLabelStyle());
            GUILayout.EndHorizontal();
        }

        private void DrawPoolBar(string label, float current, float max, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, SmallLabelStyle(), GUILayout.Width(70));
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
            { _dragging = true; _dragOffset = e.mousePosition - _windowPos; e.Use(); }
            if (_dragging && e.type == EventType.MouseDrag)
            { _windowPos = e.mousePosition - _dragOffset; e.Use(); }
            if (e.type == EventType.MouseUp) _dragging = false;
        }

        private GUIStyle SmallLabelStyle() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
        private GUIStyle DimLabelStyle() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        private GUIStyle BioStyle() => new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.8f, 0.8f, 0.75f) } };
    }
}

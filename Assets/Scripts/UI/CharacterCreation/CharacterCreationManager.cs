using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// Top-level MonoBehaviour for the deep character creation system.
    /// Split-screen layout: left panel (40%) with tabbed controls, right panel (60%) with 3D preview.
    /// Replaces CharacterCreationUI for new character creation flow.
    /// </summary>
    public class CharacterCreationManager : MonoBehaviour
    {
        // ─── State ─────────────────────────────────────────────────────────
        private bool _visible = false;
        private int _currentTab = 0;
        private AppearanceData _data = new AppearanceData();
        private string _errorMessage = "";

        // ─── Undo/Redo ─────────────────────────────────────────────────────
        private readonly Stack<AppearanceData> _undoStack = new Stack<AppearanceData>();
        private readonly Stack<AppearanceData> _redoStack = new Stack<AppearanceData>();
        private AppearanceData _lastSnapshot;
        private float _snapshotTimer = 0f;
        private const float SnapshotInterval = 0.5f;

        // ─── Preview ───────────────────────────────────────────────────────
        private CharacterPreviewManager _preview;

        // ─── Callback ──────────────────────────────────────────────────────
        public Action<AppearanceData> OnCreateConfirmed;

        // ─── Tab definitions ───────────────────────────────────────────────
        private static readonly string[] TabNames =
        {
            "Race/Gender", "Face", "Body", "Skin", "Hair", "Eyes",
            "Tattoos", "Race Features", "Name/Skill", "Review"
        };

        // ─── Styles ────────────────────────────────────────────────────────
        private GUIStyle _headerStyle, _subheaderStyle, _bodyStyle, _buttonStyle,
                         _selectedButtonStyle, _labelStyle, _errorStyle,
                         _nameFieldStyle, _panelStyle, _descStyle, _tabStyle, _tabSelectedStyle;
        private bool _stylesInit = false;
        private Texture2D _darkBg, _panelBg, _selectedBg, _hoverBg;

        // ─── Data ──────────────────────────────────────────────────────────
        private static readonly string[] GenderNames = { "Male", "Female" };
        private static readonly string[] RaceNames = { "Human", "Sylvari", "Korathi", "Ashborn" };
        private static readonly string[] RaceDescriptions =
        {
            "Versatile and adaptive. Balanced stats with +1 to all attributes.",
            "Tall and graceful with pointed ears. +3 Agility, +2 Perception.",
            "Broad and enduring. Born of stone and storm. +3 Strength, +2 Vitality.",
            "Lithe and luminous, touched by the Ashfall. +3 Intelligence, +2 Agility."
        };

        private struct SkillOption
        {
            public int id;
            public string name;
            public string category;
            public string description;
        }

        private static readonly SkillOption[] StarterSkills =
        {
            new() { id = 1, name = "Swordsmanship", category = "Combat",
                    description = "Master of bladed weapons. Increases melee damage with swords." },
            new() { id = 4, name = "Marksmanship", category = "Combat",
                    description = "Precision with ranged weapons. Increases ranged hit chance." },
            new() { id = 7, name = "Herbalism", category = "Survival",
                    description = "Knowledge of plants and potions. Gather herbs and craft remedies." },
            new() { id = 10, name = "Mining", category = "Survival",
                    description = "Extract ore and stone from the earth. Gather minerals faster." },
            new() { id = 13, name = "Pathfinding", category = "Exploration",
                    description = "Navigate the wilderness with ease. Increased movement speed." },
            new() { id = 16, name = "Cartography", category = "Exploration",
                    description = "Map the unknown. Reveals more of the minimap as you explore." },
        };

        private Vector2 _nameScrollPos;
        private Vector2 _reviewScrollPos;

        // ─── Public API ────────────────────────────────────────────────────

        public void Show()
        {
            _visible = true;
            _currentTab = 0;
            _errorMessage = "";
            _data = new AppearanceData();
            _data.SetDefaultRaceFeatures();
            _undoStack.Clear();
            _redoStack.Clear();
            _lastSnapshot = _data.Clone();

            if (_preview == null)
            {
                var go = new GameObject("CharacterPreview");
                go.transform.SetParent(transform);
                _preview = go.AddComponent<CharacterPreviewManager>();
                _preview.Initialize();
            }
            _preview.UpdateAppearance(_data);
            _preview.SetFocusMode(CharacterPreviewManager.FocusMode.FullBody);
        }

        public void Hide()
        {
            _visible = false;
            if (_preview != null)
            {
                _preview.Cleanup();
                DestroyImmediate(_preview.gameObject);
                _preview = null;
            }
        }

        public bool IsVisible => _visible;

        // ─── MonoBehaviour ─────────────────────────────────────────────────

        private void Update()
        {
            if (!_visible) return;

            // Periodic snapshot for undo
            _snapshotTimer += Time.deltaTime;
            if (_snapshotTimer >= SnapshotInterval)
            {
                _snapshotTimer = 0f;
                TakeSnapshotIfChanged();
            }

            // Keyboard shortcuts
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        Redo();
                    else
                        Undo();
                }
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            InitStyles();

            // Full screen dark overlay
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _darkBg);

            float sw = Screen.width;
            float sh = Screen.height;
            float margin = 10f;

            // ── Left Panel (40%) ───────────────────────────────────────────
            float leftW = sw * 0.4f;
            Rect leftPanel = new Rect(margin, margin, leftW - margin * 2, sh - margin * 2);
            GUI.Box(leftPanel, "", _panelStyle);

            // Title
            GUI.Label(new Rect(leftPanel.x, leftPanel.y + 8, leftPanel.width, 32),
                "Create Your Character", _headerStyle);

            // ── Toolbar (Randomize, Reset, Undo, Redo) ─────────────────────
            float toolY = leftPanel.y + 44;
            float toolBtnW = 80f;
            float toolX = leftPanel.x + 10;

            if (GUI.Button(new Rect(toolX, toolY, toolBtnW, 24), "Randomize", _buttonStyle))
            {
                PushUndo();
                _data.Randomize();
                RefreshPreview();
            }
            toolX += toolBtnW + 4;

            if (GUI.Button(new Rect(toolX, toolY, 60, 24), "Reset", _buttonStyle))
            {
                PushUndo();
                int race = _data.Race;
                int gender = _data.Gender;
                _data = new AppearanceData { Race = race, Gender = gender };
                _data.SetDefaultRaceFeatures();
                RefreshPreview();
            }
            toolX += 64;

            GUI.enabled = _undoStack.Count > 0;
            if (GUI.Button(new Rect(toolX, toolY, 50, 24), "Undo", _buttonStyle))
                Undo();
            toolX += 54;
            GUI.enabled = _redoStack.Count > 0;
            if (GUI.Button(new Rect(toolX, toolY, 50, 24), "Redo", _buttonStyle))
                Redo();
            GUI.enabled = true;

            // ── Tab Buttons ────────────────────────────────────────────────
            float tabY = toolY + 30;
            float tabBtnW = (leftPanel.width - 20) / 5f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int col = i % 5;
                int row = i / 5;
                float tx = leftPanel.x + 10 + col * tabBtnW;
                float ty = tabY + row * 26;
                var style = (i == _currentTab) ? _tabSelectedStyle : _tabStyle;
                if (GUI.Button(new Rect(tx, ty, tabBtnW - 2, 24), TabNames[i], style))
                {
                    OnTabChanged(i);
                }
            }

            // ── Content Area ───────────────────────────────────────────────
            float contentY = tabY + 56;
            Rect contentArea = new Rect(leftPanel.x + 10, contentY,
                leftPanel.width - 20, leftPanel.yMax - contentY - 50);

            DrawCurrentTab(contentArea);

            // ── Error Message ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                GUI.Label(new Rect(leftPanel.x, leftPanel.yMax - 44, leftPanel.width, 20),
                    _errorMessage, _errorStyle);
            }

            // ── Navigation Buttons ─────────────────────────────────────────
            float navY = leftPanel.yMax - 24;
            if (_currentTab > 0 && GUI.Button(new Rect(leftPanel.x + 10, navY, 100, 24), "< Back", _buttonStyle))
            {
                _currentTab--;
                _errorMessage = "";
            }
            if (_currentTab < TabNames.Length - 1 &&
                GUI.Button(new Rect(leftPanel.xMax - 110, navY, 100, 24), "Next >", _buttonStyle))
            {
                if (ValidateCurrentTab())
                    _currentTab++;
            }
            if (_currentTab == TabNames.Length - 1 &&
                GUI.Button(new Rect(leftPanel.xMax - 110, navY, 100, 24), "Create!", _selectedButtonStyle))
            {
                SubmitCharacter();
            }

            // ── Right Panel (60%) — 3D Preview ────────────────────────────
            float rightX = leftW;
            float rightW = sw - leftW;
            Rect rightPanel = new Rect(rightX + margin, margin, rightW - margin * 2, sh - margin * 2);

            if (_preview != null && _preview.PreviewTexture != null)
            {
                // Draw preview texture scaled to fit
                float texAspect = (float)_preview.PreviewTexture.width / _preview.PreviewTexture.height;
                float panelAspect = rightPanel.width / rightPanel.height;

                Rect texRect;
                if (panelAspect > texAspect)
                {
                    float h = rightPanel.height;
                    float w = h * texAspect;
                    texRect = new Rect(rightPanel.x + (rightPanel.width - w) / 2, rightPanel.y, w, h);
                }
                else
                {
                    float w = rightPanel.width;
                    float h = w / texAspect;
                    texRect = new Rect(rightPanel.x, rightPanel.y + (rightPanel.height - h) / 2, w, h);
                }

                GUI.DrawTexture(texRect, _preview.PreviewTexture, ScaleMode.ScaleToFit);

                // Handle orbit input
                _preview.HandleOrbitInput(texRect);
            }
            else
            {
                GUI.Box(rightPanel, "Preview Loading...", _panelStyle);
            }
        }

        // ─── Tab Content Drawing ───────────────────────────────────────────

        private void DrawCurrentTab(Rect area)
        {
            switch (_currentTab)
            {
                case 0: DrawRaceGenderTab(area); break;
                case 1:
                    FaceSculptPanel.DrawFacePanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 2:
                    BodyMorphPanel.DrawBodyPanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 3:
                    SkinPanel.DrawSkinPanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 4:
                    HairPanel.DrawHairPanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 5:
                    EyePanel.DrawEyePanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 6:
                    DecalPanel.DrawDecalPanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 7:
                    RaceFeaturesPanel.DrawRaceFeaturesPanel(area, ref _data, _subheaderStyle, _labelStyle,
                        _buttonStyle, _selectedButtonStyle);
                    break;
                case 8: DrawNameSkillTab(area); break;
                case 9: DrawReviewTab(area); break;
            }
        }

        private void DrawRaceGenderTab(Rect area)
        {
            float y = area.y;

            // Gender selection
            GUI.Label(new Rect(area.x, y, 200, 24), "Gender:", _bodyStyle);
            y += 28;
            for (int i = 0; i < GenderNames.Length; i++)
            {
                var style = (_data.Gender == i) ? _selectedButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(area.x + i * 160, y, 150, 35), GenderNames[i], style))
                {
                    if (_data.Gender != i)
                    {
                        PushUndo();
                        _data.Gender = i;
                        // Reset facial hair if switching to female
                        if (i == 1) { _data.FacialHairStyle = 0; _data.FacialHairLength = 0f; }
                        RefreshPreview();
                    }
                }
            }
            y += 50;

            // Race selection
            GUI.Label(new Rect(area.x, y, 200, 24), "Race:", _bodyStyle);
            y += 28;
            for (int i = 0; i < RaceNames.Length; i++)
            {
                var style = (_data.Race == i) ? _selectedButtonStyle : _buttonStyle;
                if (GUI.Button(new Rect(area.x + i * 150, y, 140, 35), RaceNames[i], style))
                {
                    if (_data.Race != i)
                    {
                        PushUndo();
                        _data.Race = i;
                        _data.SetDefaultRaceFeatures();
                        RaceFeaturesPanel.ResetCache();
                        RefreshPreview();
                    }
                }
            }
            y += 50;

            // Race description
            GUI.Label(new Rect(area.x, y, area.width, 60), RaceDescriptions[_data.Race], _descStyle);
        }

        private void DrawNameSkillTab(Rect area)
        {
            _nameScrollPos = GUI.BeginScrollView(area, _nameScrollPos,
                new Rect(0, 0, area.width - 20, 400));

            float y = 10f;
            float w = area.width - 30;

            GUI.Label(new Rect(4, y, w, 24), "Choose your name wisely. It cannot be changed.", _descStyle);
            y += 40;

            GUI.Label(new Rect(4, y, 120, 24), "First Name:", _bodyStyle);
            _data.FirstName = GUI.TextField(new Rect(134, y, 250, 30), _data.FirstName, 16, _nameFieldStyle);
            y += 40;

            GUI.Label(new Rect(4, y, 120, 24), "Last Name:", _bodyStyle);
            _data.LastName = GUI.TextField(new Rect(134, y, 250, 30), _data.LastName, 16, _nameFieldStyle);
            y += 50;

            // Validation hint
            string hint = "";
            if (_data.FirstName.Length > 0 && _data.FirstName.Length < 2)
                hint = "First name must be at least 2 characters";
            else if (_data.FirstName.Length > 0 && !char.IsUpper(_data.FirstName[0]))
                hint = "Must start with a capital letter";
            if (!string.IsNullOrEmpty(hint))
                GUI.Label(new Rect(4, y, w, 20), hint, _errorStyle);
            y += 30;

            GUI.Label(new Rect(4, y, w, 24),
                $"Your character will be known as: {_data.FirstName} {_data.LastName}", _bodyStyle);
            y += 40;

            // Skill selection
            GUI.Label(new Rect(4, y, w, 24), "Starting Skill:", _subheaderStyle);
            y += 28;
            GUI.Label(new Rect(4, y, w, 20),
                "Choose your first skill. You'll learn more as you explore.", _descStyle);
            y += 28;

            for (int i = 0; i < StarterSkills.Length; i++)
            {
                var skill = StarterSkills[i];
                var style = (_data.SelectedSkill == i) ? _selectedButtonStyle : _buttonStyle;
                float boxH = 50;

                if (GUI.Button(new Rect(4, y, w, boxH), "", style))
                    _data.SelectedSkill = i;

                GUI.Label(new Rect(16, y + 4, 200, 22),
                    $"{skill.name} [{skill.category}]", _bodyStyle);
                GUI.Label(new Rect(16, y + 24, w - 24, 22),
                    skill.description, _descStyle);

                y += boxH + 4;
            }

            GUI.EndScrollView();
        }

        private void DrawReviewTab(Rect area)
        {
            _reviewScrollPos = GUI.BeginScrollView(area, _reviewScrollPos,
                new Rect(0, 0, area.width - 20, 400));

            float y = 10f;
            float w = area.width - 30;

            GUI.Label(new Rect(4, y, w, 28), "Review Your Character", _subheaderStyle);
            y += 36;

            string raceGender = $"{GenderNames[_data.Gender]} {RaceNames[_data.Race]}";
            GUI.Label(new Rect(4, y, w, 22), $"Name: {_data.FirstName} {_data.LastName}", _bodyStyle);
            y += 24;
            GUI.Label(new Rect(4, y, w, 22), $"Race/Gender: {raceGender}", _bodyStyle);
            y += 24;
            GUI.Label(new Rect(4, y, w, 22), $"Height: {_data.Height:F2}  Build: {_data.Build:F2}", _bodyStyle);
            y += 24;

            // Skin color swatch
            GUI.Label(new Rect(4, y, 80, 22), "Skin:", _bodyStyle);
            var prevColor = GUI.color;
            GUI.color = _data.SkinColor;
            GUI.DrawTexture(new Rect(90, y + 2, 40, 16), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 24;

            // Hair
            string[] hairNames = { "Short", "Medium", "Long", "Ponytail", "Braided", "Shaved", "Mohawk", "Bald" };
            string hairName = _data.HairStyle < hairNames.Length ? hairNames[_data.HairStyle] : "?";
            GUI.Label(new Rect(4, y, 80, 22), $"Hair: {hairName}", _bodyStyle);
            prevColor = GUI.color;
            GUI.color = _data.HairColor;
            GUI.DrawTexture(new Rect(180, y + 2, 40, 16), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 24;

            // Eyes
            GUI.Label(new Rect(4, y, 80, 22), "Eyes:", _bodyStyle);
            prevColor = GUI.color;
            GUI.color = _data.LeftEyeColor;
            GUI.DrawTexture(new Rect(90, y + 2, 40, 16), Texture2D.whiteTexture);
            if (!_data.MatchEyes)
            {
                GUI.color = _data.RightEyeColor;
                GUI.DrawTexture(new Rect(140, y + 2, 40, 16), Texture2D.whiteTexture);
            }
            GUI.color = prevColor;
            y += 24;

            // Decals
            GUI.Label(new Rect(4, y, w, 22), $"Decals: {_data.Decals.Count}", _bodyStyle);
            y += 24;

            // Skill
            if (_data.SelectedSkill >= 0 && _data.SelectedSkill < StarterSkills.Length)
            {
                var skill = StarterSkills[_data.SelectedSkill];
                GUI.Label(new Rect(4, y, w, 22),
                    $"Starting Skill: {skill.name} ({skill.category})", _bodyStyle);
            }
            y += 30;

            GUI.Label(new Rect(4, y, w, 30), RaceDescriptions[_data.Race], _descStyle);

            GUI.EndScrollView();
        }

        // ─── Tab Changes ───────────────────────────────────────────────────

        private void OnTabChanged(int newTab)
        {
            _errorMessage = "";
            _currentTab = newTab;

            // Set camera focus mode
            if (_preview != null)
            {
                switch (newTab)
                {
                    case 1: // Face
                    case 5: // Eyes
                        _preview.SetFocusMode(CharacterPreviewManager.FocusMode.Face);
                        break;
                    case 2: // Body
                    case 0: // Race/Gender
                    case 6: // Tattoos
                    case 8: // Name/Skill
                    case 9: // Review
                        _preview.SetFocusMode(CharacterPreviewManager.FocusMode.FullBody);
                        break;
                    case 3: // Skin
                    case 4: // Hair
                    case 7: // Race Features
                        _preview.SetFocusMode(CharacterPreviewManager.FocusMode.UpperBody);
                        break;
                }
            }

            // Reset HSV caches when switching tabs
            SkinPanel.ResetCache();
            HairPanel.ResetCache();
            EyePanel.ResetCache();
            DecalPanel.ResetCache();
            RaceFeaturesPanel.ResetCache();
        }

        // ─── Undo/Redo ─────────────────────────────────────────────────────

        private void PushUndo()
        {
            _undoStack.Push(_data.Clone());
            _redoStack.Clear();
            if (_undoStack.Count > 50)
            {
                // Keep stack manageable — convert to array, trim, rebuild
                var arr = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = Mathf.Min(arr.Length - 1, 40); i >= 0; i--)
                    _undoStack.Push(arr[i]);
            }
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            _redoStack.Push(_data.Clone());
            _data = _undoStack.Pop();
            _lastSnapshot = _data.Clone();
            RefreshPreview();
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            _undoStack.Push(_data.Clone());
            _data = _redoStack.Pop();
            _lastSnapshot = _data.Clone();
            RefreshPreview();
        }

        private void TakeSnapshotIfChanged()
        {
            // Simple check — compare a few key fields
            if (_lastSnapshot == null) { _lastSnapshot = _data.Clone(); return; }

            bool changed = _data.Height != _lastSnapshot.Height ||
                           _data.Build != _lastSnapshot.Build ||
                           _data.Gender != _lastSnapshot.Gender ||
                           _data.Race != _lastSnapshot.Race ||
                           _data.SkinColor != _lastSnapshot.SkinColor ||
                           _data.HairStyle != _lastSnapshot.HairStyle ||
                           _data.EyeSpacing != _lastSnapshot.EyeSpacing ||
                           _data.ShoulderWidth != _lastSnapshot.ShoulderWidth;

            if (changed)
            {
                PushUndo();
                _lastSnapshot = _data.Clone();
                RefreshPreview();
            }
        }

        private void RefreshPreview()
        {
            if (_preview != null)
                _preview.UpdateAppearance(_data);
        }

        // ─── Validation ────────────────────────────────────────────────────

        private bool ValidateCurrentTab()
        {
            _errorMessage = "";
            if (_currentTab == 8) // Name/Skill
            {
                if (string.IsNullOrWhiteSpace(_data.FirstName) || _data.FirstName.Length < 2)
                { _errorMessage = "First name must be at least 2 characters"; return false; }
                if (string.IsNullOrWhiteSpace(_data.LastName) || _data.LastName.Length < 2)
                { _errorMessage = "Last name must be at least 2 characters"; return false; }
                if (!char.IsUpper(_data.FirstName[0]) || !char.IsUpper(_data.LastName[0]))
                { _errorMessage = "Names must start with a capital letter"; return false; }
                if (_data.SelectedSkill < 0)
                { _errorMessage = "Please select a starting skill"; return false; }
            }
            return true;
        }

        private void SubmitCharacter()
        {
            // Validate name/skill tab
            int savedTab = _currentTab;
            _currentTab = 8;
            if (!ValidateCurrentTab())
            {
                _currentTab = savedTab;
                return;
            }
            _currentTab = savedTab;

            OnCreateConfirmed?.Invoke(_data);
        }

        // ─── Styles ────────────────────────────────────────────────────────

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _darkBg = MakeTex(1, 1, new Color(0.06f, 0.06f, 0.1f, 0.97f));
            _panelBg = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            _selectedBg = MakeTex(1, 1, new Color(0.2f, 0.35f, 0.6f, 0.8f));
            _hoverBg = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.25f, 0.8f));

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = new Color(0.9f, 0.85f, 0.7f);

            _subheaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
            };
            _subheaderStyle.normal.textColor = new Color(0.7f, 0.75f, 0.85f);

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, wordWrap = true, alignment = TextAnchor.UpperLeft
            };
            _bodyStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true, fontStyle = FontStyle.Italic
            };
            _descStyle.normal.textColor = new Color(0.65f, 0.65f, 0.7f);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _buttonStyle.normal.background = _panelBg;
            _buttonStyle.hover.background = _hoverBg;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.white;

            _selectedButtonStyle = new GUIStyle(_buttonStyle);
            _selectedButtonStyle.normal.background = _selectedBg;
            _selectedButtonStyle.fontStyle = FontStyle.Bold;

            _tabStyle = new GUIStyle(_buttonStyle) { fontSize = 11 };
            _tabSelectedStyle = new GUIStyle(_selectedButtonStyle) { fontSize = 11 };

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _errorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);

            _nameFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16 };
            _nameFieldStyle.normal.textColor = Color.white;

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelBg;
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }
}

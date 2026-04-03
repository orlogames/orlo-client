using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Character selection / lobby screen shown after login.
    /// Displays the player's character list with Play, Create New, and Delete options.
    /// Uses OnGUI immediate mode rendering (same pattern as LoginUI).
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        public Action<ulong, string> OnCharacterSelected; // (characterId, fullName)
        public Action OnCreateNew;

        private bool _visible = true;
        private int _selectedIndex = 0;
        private List<CharacterEntry> _characters = new();
        private int _maxCharacters = 4;
        private Vector2 _newsScroll;

        // News/tips for the right panel
        private static readonly string[] News =
        {
            "Welcome to Orlo — a physics-driven sci-fi fantasy MMORPG.",
            "",
            "CURRENT BUILD: Pre-Alpha",
            "  - Character creation with full customization",
            "  - Procedural terrain generation",
            "  - Real-time combat with 6 damage types",
            "  - Three-pool health system (Vitality/Stamina/Focus)",
            "  - Player economy with vendors and direct trade",
            "",
            "COMING SOON:",
            "  - Planet surface to orbit seamless transition",
            "  - Guild system with ranks and permissions",
            "  - Crafting with assembly + experimentation",
            "  - Player housing with furniture and tax",
            "",
            "Report bugs in Discord. Every bit of feedback helps!"
        };

        public struct CharacterEntry
        {
            public ulong id;
            public string firstName;
            public string lastName;
            public int level;
            public string zoneName;
            public int race;
        }

        public void Show() { _visible = true; _selectedIndex = 0; }
        public void Hide() { _visible = false; }

        public void SetCharacters(List<CharacterEntry> characters, int maxSlots)
        {
            _characters = characters;
            _maxCharacters = maxSlots;
            _selectedIndex = _characters.Count > 0 ? 0 : -1;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // Full-screen dark background
            GUI.color = new Color(0.05f, 0.04f, 0.08f, 0.98f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float totalW = Mathf.Min(Screen.width * 0.85f, 1200f);
            float totalH = Mathf.Min(Screen.height * 0.8f, 700f);
            float startX = (Screen.width - totalW) / 2f;
            float startY = (Screen.height - totalH) / 2f;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            GUI.Label(new Rect(startX, startY - 60, totalW, 50), "ORLO", titleStyle);

            // Left panel — Character List (60% width)
            float leftW = totalW * 0.58f;
            DrawCharacterList(new Rect(startX, startY, leftW, totalH));

            // Right panel — News & Info (40% width)
            float rightX = startX + leftW + 20;
            float rightW = totalW - leftW - 20;
            DrawNewsPanel(new Rect(rightX, startY, rightW, totalH));
        }

        private void DrawCharacterList(Rect area)
        {
            // Panel background
            GUI.color = new Color(0.08f, 0.07f, 0.12f, 0.9f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = area.y + 15;
            float padX = 20;

            // Header
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
            GUI.Label(new Rect(area.x + padX, y, area.width - padX * 2, 30),
                $"Your Characters ({_characters.Count}/{_maxCharacters})", headerStyle);
            y += 40;

            // Character entries
            if (_characters.Count == 0)
            {
                var emptyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic
                };
                emptyStyle.normal.textColor = new Color(0.5f, 0.5f, 0.6f);
                GUI.Label(new Rect(area.x, y, area.width, 80),
                    "No characters yet.\nCreate your first character to begin your journey.", emptyStyle);
                y += 100;
            }
            else
            {
                for (int i = 0; i < _characters.Count; i++)
                {
                    bool isSelected = (i == _selectedIndex);
                    DrawCharacterEntry(new Rect(area.x + padX, y, area.width - padX * 2, 70),
                        _characters[i], isSelected, i);
                    y += 78;
                }
            }

            // Buttons at bottom
            float btnY = area.y + area.height - 60;
            float btnW = 140;
            float btnH = 40;
            float btnX = area.x + padX;

            // Play button (only if character selected)
            GUI.enabled = _selectedIndex >= 0 && _selectedIndex < _characters.Count;
            var playStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(btnX, btnY, btnW + 20, btnH), "Enter World", playStyle))
            {
                var ch = _characters[_selectedIndex];
                string fullName = $"{ch.firstName} {ch.lastName}";
                OnCharacterSelected?.Invoke(ch.id, fullName);
                Hide();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // Create New button
            GUI.enabled = _characters.Count < _maxCharacters;
            btnX += btnW + 30;
            var createStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            GUI.backgroundColor = new Color(0.3f, 0.4f, 0.8f);
            if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "Create New", createStyle))
            {
                OnCreateNew?.Invoke();
                Hide();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void DrawCharacterEntry(Rect area, CharacterEntry ch, bool selected, int index)
        {
            // Entry background
            Color bgColor = selected
                ? new Color(0.15f, 0.2f, 0.35f, 0.9f)
                : new Color(0.1f, 0.1f, 0.15f, 0.7f);
            GUI.color = bgColor;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Selection border
            if (selected)
            {
                GUI.color = new Color(0.4f, 0.6f, 1f, 0.8f);
                float b = 2;
                GUI.DrawTexture(new Rect(area.x, area.y, area.width, b), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(area.x, area.y + area.height - b, area.width, b), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(area.x, area.y, b, area.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(area.x + area.width - b, area.y, b, area.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Click to select
            if (Event.current.type == EventType.MouseDown &&
                area.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                Event.current.Use();
            }

            // Double-click to enter
            if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 &&
                area.Contains(Event.current.mousePosition) && selected)
            {
                string fullName = $"{ch.firstName} {ch.lastName}";
                OnCharacterSelected?.Invoke(ch.id, fullName);
                Hide();
                Event.current.Use();
            }

            float x = area.x + 15;
            float y = area.y + 8;

            // Character name
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold
            };
            nameStyle.normal.textColor = selected ? new Color(0.9f, 0.95f, 1f) : new Color(0.7f, 0.75f, 0.85f);
            GUI.Label(new Rect(x, y, 300, 25), $"{ch.firstName} {ch.lastName}", nameStyle);

            // Level and zone
            var detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            detailStyle.normal.textColor = new Color(0.5f, 0.55f, 0.65f);

            string[] raceNames = { "Human", "Sylvari", "Korathi", "Ashborn" };
            string raceName = ch.race >= 0 && ch.race < raceNames.Length ? raceNames[ch.race] : "Unknown";
            string zone = string.IsNullOrEmpty(ch.zoneName) ? "Starter Plains" : ch.zoneName;

            GUI.Label(new Rect(x, y + 26, 400, 20),
                $"Level {ch.level} {raceName}  |  {zone}", detailStyle);
        }

        private void DrawNewsPanel(Rect area)
        {
            // Panel background
            GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.85f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float padX = 15;
            float y = area.y + 15;

            // Header
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.7f, 0.8f, 0.5f);
            GUI.Label(new Rect(area.x + padX, y, area.width - padX * 2, 25),
                "NEWS & CHANGELOG", headerStyle);
            y += 35;

            // News content
            var newsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, wordWrap = true
            };
            newsStyle.normal.textColor = new Color(0.6f, 0.62f, 0.7f);

            foreach (var line in News)
            {
                if (string.IsNullOrEmpty(line))
                {
                    y += 10;
                    continue;
                }

                float lineHeight = string.IsNullOrEmpty(line) ? 10 : 20;
                GUI.Label(new Rect(area.x + padX, y, area.width - padX * 2, lineHeight), line, newsStyle);
                y += lineHeight;
            }

            // Version info at bottom
            var versionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.LowerRight
            };
            versionStyle.normal.textColor = new Color(0.35f, 0.35f, 0.45f);
            GUI.Label(new Rect(area.x + padX, area.y + area.height - 30, area.width - padX * 2, 20),
                $"Client v{Application.version ?? "dev"}", versionStyle);
        }
    }
}

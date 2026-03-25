using UnityEngine;
using System;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Full-screen character creation with appearance customization,
    /// name entry, starting skill selection, and 3D preview.
    /// </summary>
    public class CharacterCreationUI : MonoBehaviour
    {
        // ─── State ──────────────────────────────────────────────────────────
        private bool visible = false;
        private int currentPage = 0; // 0=Race/Gender, 1=Appearance, 2=Name, 3=Skill, 4=Confirm

        // Identity
        private string firstName = "";
        private string lastName = "";

        // Appearance
        private int gender = 0;        // 0=Male, 1=Female
        private int race = 0;          // 0=Human, 1=Sylvari, 2=Korathi, 3=Ashborn
        private float height = 0.5f;
        private float build = 0.5f;
        private int eyeColor = 0;
        private int hairStyle = 0;
        private int hairColor = 1;
        private int skinTone = 2;
        private float faceShape = 0.5f;
        private float jawWidth = 0.5f;
        private float noseSize = 0.5f;
        private float earSize = 0.5f;
        private int facialMarking = 0;

        // Skill selection
        private int selectedSkill = -1;

        // Validation
        private string errorMessage = "";

        // Callback when creation is confirmed
        public Action<CharacterCreationData> OnCreateConfirmed;

        // ─── Data structures ────────────────────────────────────────────────
        private static readonly string[] GenderNames = { "Male", "Female" };
        private static readonly string[] RaceNames = { "Human", "Sylvari", "Korathi", "Ashborn" };
        private static readonly string[] RaceDescriptions = {
            "Versatile and adaptive. Balanced stats with +1 to all attributes.",
            "Tall and graceful with pointed ears. +3 Agility, +2 Perception.",
            "Broad and enduring. Born of stone and storm. +3 Strength, +2 Vitality.",
            "Lithe and luminous, touched by the Ashfall. +3 Intelligence, +2 Agility."
        };
        private static readonly string[] EyeColorNames = {
            "Brown", "Blue", "Green", "Hazel", "Grey", "Amber", "Violet", "Red"
        };
        private static readonly string[] HairStyleNames = {
            "Short", "Medium", "Long", "Ponytail", "Braided", "Shaved", "Mohawk", "Bald"
        };
        private static readonly string[] HairColorNames = {
            "Black", "Brown", "Blonde", "Red", "White", "Grey", "Blue Tint", "Green Tint"
        };
        private static readonly string[] SkinToneNames = {
            "Pale", "Fair", "Medium", "Tan", "Dark", "Deep", "Ashen", "Bark"
        };

        private struct SkillOption
        {
            public int id;
            public string name;
            public string category;
            public string description;
        }

        private static readonly SkillOption[] StarterSkills = {
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

        // ─── Styles ─────────────────────────────────────────────────────────
        private GUIStyle headerStyle, subheaderStyle, bodyStyle, buttonStyle,
                         selectedButtonStyle, sliderLabelStyle, errorStyle,
                         nameFieldStyle, panelStyle, descStyle;
        private bool stylesInit = false;
        private Texture2D darkBg, panelBg, selectedBg, hoverBg;

        // ─── Public API ─────────────────────────────────────────────────────
        public void Show() { visible = true; currentPage = 0; errorMessage = ""; }
        public void Hide() { visible = false; }
        public bool IsVisible => visible;

        private void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;

            darkBg = MakeTex(1, 1, new Color(0.06f, 0.06f, 0.1f, 0.97f));
            panelBg = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.15f, 0.9f));
            selectedBg = MakeTex(1, 1, new Color(0.2f, 0.35f, 0.6f, 0.8f));
            hoverBg = MakeTex(1, 1, new Color(0.15f, 0.15f, 0.25f, 0.8f));

            headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.9f, 0.85f, 0.7f);

            subheaderStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            subheaderStyle.normal.textColor = new Color(0.7f, 0.75f, 0.85f);

            bodyStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14, wordWrap = true, alignment = TextAnchor.UpperLeft
            };
            bodyStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            descStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 12, wordWrap = true, fontStyle = FontStyle.Italic
            };
            descStyle.normal.textColor = new Color(0.65f, 0.65f, 0.7f);

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            buttonStyle.normal.background = panelBg;
            buttonStyle.hover.background = hoverBg;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = selectedBg;
            selectedButtonStyle.fontStyle = FontStyle.Bold;

            sliderLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            sliderLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            errorStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);

            nameFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 18 };
            nameFieldStyle.normal.textColor = Color.white;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelBg;
        }

        private void OnGUI()
        {
            if (!visible) return;
            InitStyles();

            // Full screen dark overlay
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), darkBg);

            float cw = Mathf.Min(800f, Screen.width - 40f);
            float ch = Screen.height - 80f;
            float cx = (Screen.width - cw) / 2f;
            float cy = 40f;

            GUI.Box(new Rect(cx, cy, cw, ch), "", panelStyle);

            // Title
            GUI.Label(new Rect(cx, cy + 10, cw, 40), "Create Your Character", headerStyle);

            // Page indicator
            string[] pageNames = { "Race & Gender", "Appearance", "Name", "Starting Skill", "Confirm" };
            GUI.Label(new Rect(cx, cy + 50, cw, 24),
                $"Step {currentPage + 1} of 5: {pageNames[currentPage]}", subheaderStyle);

            // Content area
            Rect content = new Rect(cx + 30, cy + 90, cw - 60, ch - 170);

            switch (currentPage)
            {
                case 0: DrawRaceGenderPage(content); break;
                case 1: DrawAppearancePage(content); break;
                case 2: DrawNamePage(content); break;
                case 3: DrawSkillPage(content); break;
                case 4: DrawConfirmPage(content); break;
            }

            // Error message
            if (!string.IsNullOrEmpty(errorMessage))
            {
                GUI.Label(new Rect(cx, cy + ch - 70, cw, 24), errorMessage, errorStyle);
            }

            // Navigation buttons
            float btnY = cy + ch - 40;
            if (currentPage > 0 && GUI.Button(new Rect(cx + 30, btnY, 120, 30), "< Back", buttonStyle))
            {
                currentPage--;
                errorMessage = "";
            }
            if (currentPage < 4 && GUI.Button(new Rect(cx + cw - 150, btnY, 120, 30), "Next >", buttonStyle))
            {
                if (ValidateCurrentPage()) currentPage++;
            }
            if (currentPage == 4 && GUI.Button(new Rect(cx + cw - 150, btnY, 120, 30), "Create!", selectedButtonStyle))
            {
                SubmitCharacter();
            }
        }

        // ─── Pages ──────────────────────────────────────────────────────────

        private void DrawRaceGenderPage(Rect area)
        {
            float y = area.y;

            // Gender selection
            GUI.Label(new Rect(area.x, y, 200, 24), "Gender:", bodyStyle);
            y += 28;
            for (int i = 0; i < GenderNames.Length; i++)
            {
                var style = (gender == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(area.x + i * 160, y, 150, 35), GenderNames[i], style))
                    gender = i;
            }
            y += 50;

            // Race selection
            GUI.Label(new Rect(area.x, y, 200, 24), "Race:", bodyStyle);
            y += 28;
            for (int i = 0; i < RaceNames.Length; i++)
            {
                var style = (race == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(area.x + i * 170, y, 160, 35), RaceNames[i], style))
                    race = i;
            }
            y += 50;

            // Race description
            GUI.Label(new Rect(area.x, y, area.width, 60), RaceDescriptions[race], descStyle);
        }

        private void DrawAppearancePage(Rect area)
        {
            float y = area.y;
            float labelW = 120f;
            float sliderW = area.width - labelW - 10;

            // Height
            GUI.Label(new Rect(area.x, y, labelW, 20), $"Height: {height:F2}", sliderLabelStyle);
            height = GUI.HorizontalSlider(new Rect(area.x + labelW, y + 4, sliderW, 16), height, 0f, 1f);
            y += 28;

            // Build
            GUI.Label(new Rect(area.x, y, labelW, 20), $"Build: {build:F2}", sliderLabelStyle);
            build = GUI.HorizontalSlider(new Rect(area.x + labelW, y + 4, sliderW, 16), build, 0f, 1f);
            y += 28;

            // Face shape
            GUI.Label(new Rect(area.x, y, labelW, 20), $"Face: {faceShape:F2}", sliderLabelStyle);
            faceShape = GUI.HorizontalSlider(new Rect(area.x + labelW, y + 4, sliderW, 16), faceShape, 0f, 1f);
            y += 28;

            // Jaw
            GUI.Label(new Rect(area.x, y, labelW, 20), $"Jaw: {jawWidth:F2}", sliderLabelStyle);
            jawWidth = GUI.HorizontalSlider(new Rect(area.x + labelW, y + 4, sliderW, 16), jawWidth, 0f, 1f);
            y += 28;

            // Nose
            GUI.Label(new Rect(area.x, y, labelW, 20), $"Nose: {noseSize:F2}", sliderLabelStyle);
            noseSize = GUI.HorizontalSlider(new Rect(area.x + labelW, y + 4, sliderW, 16), noseSize, 0f, 1f);
            y += 28;

            // Ear size (prominent for Sylvari)
            string earLabel = race == 1 ? "Ear Points:" : "Ears:";
            GUI.Label(new Rect(area.x, y, labelW, 20), $"{earLabel} {earSize:F2}", sliderLabelStyle);
            earSize = GUI.HorizontalSlider(new Rect(area.x + labelW, y + 4, sliderW, 16), earSize, 0f, 1f);
            y += 36;

            // Eye color
            GUI.Label(new Rect(area.x, y, labelW, 20), "Eye Color:", sliderLabelStyle);
            int maxEyes = (race == 3) ? 8 : 7; // Ashborn gets Red
            for (int i = 0; i < maxEyes; i++)
            {
                var style = (eyeColor == i) ? selectedButtonStyle : buttonStyle;
                float bx = area.x + labelW + i * 80;
                if (bx + 75 > area.x + area.width) { y += 28; bx = area.x + labelW; }
                if (GUI.Button(new Rect(bx, y, 75, 24), EyeColorNames[i], style))
                    eyeColor = i;
            }
            y += 32;

            // Hair style
            GUI.Label(new Rect(area.x, y, labelW, 20), "Hair Style:", sliderLabelStyle);
            for (int i = 0; i < HairStyleNames.Length; i++)
            {
                var style = (hairStyle == i) ? selectedButtonStyle : buttonStyle;
                float bx = area.x + labelW + (i % 4) * 100;
                float by = y + (i / 4) * 28;
                if (GUI.Button(new Rect(bx, by, 95, 24), HairStyleNames[i], style))
                    hairStyle = i;
            }
            y += 64;

            // Hair color
            GUI.Label(new Rect(area.x, y, labelW, 20), "Hair Color:", sliderLabelStyle);
            for (int i = 0; i < HairColorNames.Length; i++)
            {
                var style = (hairColor == i) ? selectedButtonStyle : buttonStyle;
                float bx = area.x + labelW + (i % 4) * 100;
                float by = y + (i / 4) * 28;
                if (GUI.Button(new Rect(bx, by, 95, 24), HairColorNames[i], style))
                    hairColor = i;
            }
            y += 64;

            // Skin tone
            GUI.Label(new Rect(area.x, y, labelW, 20), "Skin Tone:", sliderLabelStyle);
            for (int i = 0; i < SkinToneNames.Length; i++)
            {
                var style = (skinTone == i) ? selectedButtonStyle : buttonStyle;
                float bx = area.x + labelW + (i % 4) * 100;
                float by = y + (i / 4) * 28;
                if (GUI.Button(new Rect(bx, by, 95, 24), SkinToneNames[i], style))
                    skinTone = i;
            }
            y += 64;

            // Facial markings
            GUI.Label(new Rect(area.x, y, labelW, 20), "Markings:", sliderLabelStyle);
            string[] markingNames = { "None", "1", "2", "3", "4", "5", "6", "7", "8" };
            for (int i = 0; i < 9; i++)
            {
                var style = (facialMarking == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(area.x + labelW + i * 50, y, 45, 24), markingNames[i], style))
                    facialMarking = i;
            }
        }

        private void DrawNamePage(Rect area)
        {
            float y = area.y + 20;

            GUI.Label(new Rect(area.x, y, area.width, 24), "Choose your name wisely. It cannot be changed.", descStyle);
            y += 40;

            GUI.Label(new Rect(area.x, y, 120, 24), "First Name:", bodyStyle);
            firstName = GUI.TextField(new Rect(area.x + 130, y, 300, 30), firstName, 16, nameFieldStyle);
            y += 40;

            GUI.Label(new Rect(area.x, y, 120, 24), "Last Name:", bodyStyle);
            lastName = GUI.TextField(new Rect(area.x + 130, y, 300, 30), lastName, 16, nameFieldStyle);
            y += 50;

            // Live validation hint
            string hint = "";
            if (firstName.Length > 0 && firstName.Length < 2) hint = "First name must be at least 2 characters";
            else if (firstName.Length > 0 && !char.IsUpper(firstName[0])) hint = "Must start with a capital letter";
            if (!string.IsNullOrEmpty(hint))
                GUI.Label(new Rect(area.x, y, area.width, 20), hint, errorStyle);

            y += 40;
            GUI.Label(new Rect(area.x, y, area.width, 30),
                $"Your character will be known as: {firstName} {lastName}", bodyStyle);
        }

        private void DrawSkillPage(Rect area)
        {
            float y = area.y;
            GUI.Label(new Rect(area.x, y, area.width, 24),
                "Choose your first skill. You'll learn more as you level up.", descStyle);
            y += 35;

            for (int i = 0; i < StarterSkills.Length; i++)
            {
                var skill = StarterSkills[i];
                var style = (selectedSkill == i) ? selectedButtonStyle : buttonStyle;
                float boxH = 56;

                if (GUI.Button(new Rect(area.x, y, area.width, boxH), "", style))
                    selectedSkill = i;

                GUI.Label(new Rect(area.x + 12, y + 4, 200, 22),
                    $"{skill.name} [{skill.category}]", bodyStyle);
                GUI.Label(new Rect(area.x + 12, y + 26, area.width - 24, 22),
                    skill.description, descStyle);

                y += boxH + 6;
            }
        }

        private void DrawConfirmPage(Rect area)
        {
            float y = area.y;

            GUI.Label(new Rect(area.x, y, area.width, 30), "Review Your Character", subheaderStyle);
            y += 40;

            string raceGender = $"{GenderNames[gender]} {RaceNames[race]}";
            GUI.Label(new Rect(area.x, y, area.width, 22), $"Name: {firstName} {lastName}", bodyStyle); y += 24;
            GUI.Label(new Rect(area.x, y, area.width, 22), $"Race/Gender: {raceGender}", bodyStyle); y += 24;
            GUI.Label(new Rect(area.x, y, area.width, 22), $"Height: {height:F2}  Build: {build:F2}", bodyStyle); y += 24;
            GUI.Label(new Rect(area.x, y, area.width, 22),
                $"Eyes: {EyeColorNames[eyeColor]}  Hair: {HairStyleNames[hairStyle]} ({HairColorNames[hairColor]})", bodyStyle); y += 24;
            GUI.Label(new Rect(area.x, y, area.width, 22), $"Skin: {SkinToneNames[skinTone]}", bodyStyle); y += 24;

            if (selectedSkill >= 0)
            {
                var skill = StarterSkills[selectedSkill];
                GUI.Label(new Rect(area.x, y, area.width, 22),
                    $"Starting Skill: {skill.name} ({skill.category})", bodyStyle);
            }
            y += 40;

            GUI.Label(new Rect(area.x, y, area.width, 30), RaceDescriptions[race], descStyle);
        }

        // ─── Validation ─────────────────────────────────────────────────────

        private bool ValidateCurrentPage()
        {
            errorMessage = "";
            switch (currentPage)
            {
                case 2: // Name page
                    if (string.IsNullOrWhiteSpace(firstName) || firstName.Length < 2)
                    { errorMessage = "First name must be at least 2 characters"; return false; }
                    if (string.IsNullOrWhiteSpace(lastName) || lastName.Length < 2)
                    { errorMessage = "Last name must be at least 2 characters"; return false; }
                    if (!char.IsUpper(firstName[0]) || !char.IsUpper(lastName[0]))
                    { errorMessage = "Names must start with a capital letter"; return false; }
                    break;
                case 3: // Skill page
                    if (selectedSkill < 0)
                    { errorMessage = "Please select a starting skill"; return false; }
                    break;
            }
            return true;
        }

        private void SubmitCharacter()
        {
            if (!ValidateCurrentPage()) return;

            var data = new CharacterCreationData
            {
                FirstName = firstName,
                LastName = lastName,
                Gender = gender,
                Race = race,
                Height = height,
                Build = build,
                EyeColor = eyeColor,
                HairStyle = hairStyle,
                HairColor = hairColor,
                SkinTone = skinTone,
                FaceShape = faceShape,
                JawWidth = jawWidth,
                NoseSize = noseSize,
                EarSize = earSize,
                FacialMarking = facialMarking,
                StartingSkillId = selectedSkill >= 0 ? StarterSkills[selectedSkill].id : 0
            };

            OnCreateConfirmed?.Invoke(data);
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }

    public struct CharacterCreationData
    {
        public string FirstName, LastName;
        public int Gender, Race;
        public float Height, Build, FaceShape, JawWidth, NoseSize, EarSize;
        public int EyeColor, HairStyle, HairColor, SkinTone, FacialMarking;
        public int StartingSkillId;
    }
}

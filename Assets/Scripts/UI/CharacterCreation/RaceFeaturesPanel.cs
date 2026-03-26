using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for race-specific features.
    /// Content changes based on selected race:
    ///   Human    — no unique features
    ///   Sylvari  — bioluminescent pattern, glow intensity, glow color
    ///   Korathi  — horn style, horn length, ridge prominence
    ///   Ashborn  — void marking pattern, marking intensity, eye glow intensity
    /// </summary>
    public static class RaceFeaturesPanel
    {
        private static Vector2 _scrollPos;

        // HSV for Sylvari glow color
        private static float _glowH, _glowS, _glowV;
        private static bool _glowHsvInit = false;
        private static Texture2D _glowHueBar, _glowSvSquare;

        private static readonly string[] SylvariPatterns =
        {
            "Vine Trails", "Leaf Veins", "Flower Bloom", "Bark Lines",
            "Root Web", "Moss Spots", "Petal Swirl", "Thorn Pattern"
        };

        private static readonly string[] KorathiHornStyles =
        {
            "Curved Ram", "Straight Spear", "Spiral", "Branching", "Crowned"
        };

        private static readonly string[] AshbornPatterns =
        {
            "Void Cracks", "Shadow Wisps", "Ember Lines", "Eclipse Rings",
            "Ash Trails", "Dark Veins", "Starfall", "Rift Marks"
        };

        public static void DrawRaceFeaturesPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();
            EnsureFeatureValues(ref data);

            float contentHeight = 500f;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, contentHeight));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 150f;
            float sliderW = w - labelW - 50f;

            string[] raceNames = { "Human", "Sylvari", "Korathi", "Ashborn" };
            GUI.Label(new Rect(4, y, w, 22), $"{raceNames[data.Race]} Features", headerStyle);
            y += 30f;

            switch (data.Race)
            {
                case 0: // Human
                    GUI.Label(new Rect(4, y, w, 40),
                        "Humans have no unique racial features.\n\nTheir adaptability is their greatest strength.",
                        labelStyle);
                    break;

                case 1: // Sylvari
                    DrawSylvariFeatures(ref y, w, labelW, sliderW, ref data, headerStyle, labelStyle,
                        buttonStyle, selectedButtonStyle);
                    break;

                case 2: // Korathi
                    DrawKorathiFeatures(ref y, w, labelW, sliderW, ref data, headerStyle, labelStyle,
                        buttonStyle, selectedButtonStyle);
                    break;

                case 3: // Ashborn
                    DrawAshbornFeatures(ref y, w, labelW, sliderW, ref data, headerStyle, labelStyle,
                        buttonStyle, selectedButtonStyle);
                    break;
            }

            GUI.EndScrollView();
        }

        private static void DrawSylvariFeatures(ref float y, float w, float labelW, float sliderW,
            ref AppearanceData data, GUIStyle headerStyle, GUIStyle labelStyle,
            GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            // Pattern selector
            GUI.Label(new Rect(4, y, w, 20), "Bioluminescent Pattern:", labelStyle);
            y += 24f;

            int currentPattern = data.RaceFeatureValues.Count > 0 ? (int)data.RaceFeatureValues[0] : 0;
            float btnW = (w - 16f) / 4f;
            for (int i = 0; i < SylvariPatterns.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float bx = 4 + col * (btnW + 4);
                float by = y + row * 32;
                var style = (currentPattern == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, btnW, 28), SylvariPatterns[i], style))
                {
                    if (data.RaceFeatureValues.Count > 0)
                        data.RaceFeatureValues[0] = i;
                }
            }
            y += 70f;

            // Glow intensity
            float glowIntensity = data.RaceFeatureValues.Count > 1 ? data.RaceFeatureValues[1] : 0.5f;
            GUI.Label(new Rect(4, y, labelW, 20), $"Glow Intensity: {glowIntensity:F2}", labelStyle);
            glowIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                glowIntensity, 0f, 1f);
            if (data.RaceFeatureValues.Count > 1) data.RaceFeatureValues[1] = glowIntensity;
            y += 28f;

            // Glow color picker
            GUI.Label(new Rect(4, y, w, 22), "Glow Color", headerStyle);
            y += 26f;

            if (!_glowHsvInit && data.RaceFeatureValues.Count >= 5)
            {
                Color glowColor = new Color(data.RaceFeatureValues[2], data.RaceFeatureValues[3],
                    data.RaceFeatureValues[4]);
                Color.RGBToHSV(glowColor, out _glowH, out _glowS, out _glowV);
                _glowHsvInit = true;
            }

            bool glowChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _glowH, ref _glowS, ref _glowV,
                _glowHueBar, _glowSvSquare, labelStyle);
            if (glowChanged && data.RaceFeatureValues.Count >= 5)
            {
                Color gc = Color.HSVToRGB(_glowH, _glowS, _glowV);
                data.RaceFeatureValues[2] = gc.r;
                data.RaceFeatureValues[3] = gc.g;
                data.RaceFeatureValues[4] = gc.b;
            }

            if (data.RaceFeatureValues.Count >= 5)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(data.RaceFeatureValues[2], data.RaceFeatureValues[3],
                    data.RaceFeatureValues[4]);
                GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }
            y += 30f;
        }

        private static void DrawKorathiFeatures(ref float y, float w, float labelW, float sliderW,
            ref AppearanceData data, GUIStyle headerStyle, GUIStyle labelStyle,
            GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            // Horn style selector
            GUI.Label(new Rect(4, y, w, 20), "Horn Style:", labelStyle);
            y += 24f;

            int currentHorn = data.RaceFeatureValues.Count > 0 ? (int)data.RaceFeatureValues[0] : 0;
            float btnW = (w - 16f) / 3f;
            for (int i = 0; i < KorathiHornStyles.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float bx = 4 + col * (btnW + 4);
                float by = y + row * 32;
                var style = (currentHorn == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, btnW, 28), KorathiHornStyles[i], style))
                {
                    if (data.RaceFeatureValues.Count > 0)
                        data.RaceFeatureValues[0] = i;
                }
            }
            y += 70f;

            // Horn length
            float hornLength = data.RaceFeatureValues.Count > 1 ? data.RaceFeatureValues[1] : 0.5f;
            GUI.Label(new Rect(4, y, labelW, 20), $"Horn Length: {hornLength:F2}", labelStyle);
            hornLength = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                hornLength, 0f, 1f);
            if (data.RaceFeatureValues.Count > 1) data.RaceFeatureValues[1] = hornLength;
            y += 28f;

            // Ridge prominence
            float ridgeProminence = data.RaceFeatureValues.Count > 2 ? data.RaceFeatureValues[2] : 0.5f;
            GUI.Label(new Rect(4, y, labelW, 20), $"Ridge Prominence: {ridgeProminence:F2}", labelStyle);
            ridgeProminence = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                ridgeProminence, 0f, 1f);
            if (data.RaceFeatureValues.Count > 2) data.RaceFeatureValues[2] = ridgeProminence;
            y += 28f;
        }

        private static void DrawAshbornFeatures(ref float y, float w, float labelW, float sliderW,
            ref AppearanceData data, GUIStyle headerStyle, GUIStyle labelStyle,
            GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            // Void marking pattern
            GUI.Label(new Rect(4, y, w, 20), "Void Marking Pattern:", labelStyle);
            y += 24f;

            int currentPattern = data.RaceFeatureValues.Count > 0 ? (int)data.RaceFeatureValues[0] : 0;
            float btnW = (w - 16f) / 4f;
            for (int i = 0; i < AshbornPatterns.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float bx = 4 + col * (btnW + 4);
                float by = y + row * 32;
                var style = (currentPattern == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, btnW, 28), AshbornPatterns[i], style))
                {
                    if (data.RaceFeatureValues.Count > 0)
                        data.RaceFeatureValues[0] = i;
                }
            }
            y += 70f;

            // Marking intensity
            float markingIntensity = data.RaceFeatureValues.Count > 1 ? data.RaceFeatureValues[1] : 0.5f;
            GUI.Label(new Rect(4, y, labelW, 20), $"Marking Intensity: {markingIntensity:F2}", labelStyle);
            markingIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                markingIntensity, 0f, 1f);
            if (data.RaceFeatureValues.Count > 1) data.RaceFeatureValues[1] = markingIntensity;
            y += 28f;

            // Eye glow intensity
            float eyeGlow = data.RaceFeatureValues.Count > 2 ? data.RaceFeatureValues[2] : 0.5f;
            GUI.Label(new Rect(4, y, labelW, 20), $"Eye Glow: {eyeGlow:F2}", labelStyle);
            eyeGlow = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                eyeGlow, 0f, 1f);
            if (data.RaceFeatureValues.Count > 2) data.RaceFeatureValues[2] = eyeGlow;
            y += 28f;
        }

        /// <summary>
        /// Ensure the feature values list has the correct number of entries for the race.
        /// </summary>
        private static void EnsureFeatureValues(ref AppearanceData data)
        {
            int needed = 0;
            switch (data.Race)
            {
                case 0: needed = 0; break;
                case 1: needed = 5; break; // pattern, intensity, R, G, B
                case 2: needed = 3; break; // horn style, horn length, ridge
                case 3: needed = 3; break; // pattern, intensity, eye glow
            }

            data.RaceFeatureSetId = (uint)data.Race;

            while (data.RaceFeatureValues.Count < needed)
                data.RaceFeatureValues.Add(0.5f);
        }

        private static void EnsureTextures()
        {
            if (_glowHueBar == null)
            {
                _glowHueBar = new Texture2D(20, 128, TextureFormat.RGB24, false);
                _glowHueBar.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 128; y++)
                {
                    Color c = Color.HSVToRGB((float)y / 128f, 1f, 1f);
                    for (int x = 0; x < 20; x++)
                        _glowHueBar.SetPixel(x, 127 - y, c);
                }
                _glowHueBar.Apply();
            }
            if (_glowSvSquare == null)
            {
                _glowSvSquare = new Texture2D(128, 128, TextureFormat.RGB24, false);
                _glowSvSquare.wrapMode = TextureWrapMode.Clamp;
                for (int x = 0; x < 128; x++)
                    for (int y = 0; y < 128; y++)
                        _glowSvSquare.SetPixel(x, y, Color.HSVToRGB(0, (float)x / 128f, (float)y / 128f));
                _glowSvSquare.Apply();
            }
        }

        public static void ResetCache()
        {
            _glowHsvInit = false;
        }
    }
}

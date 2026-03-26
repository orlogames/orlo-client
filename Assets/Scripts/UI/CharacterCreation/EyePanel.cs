using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for eye customization.
    /// HSV color picker for left/right eye, iris/pupil size, eye shape.
    /// </summary>
    public static class EyePanel
    {
        private static Vector2 _scrollPos;

        // HSV state for left eye
        private static float _leftH, _leftS, _leftV;
        private static bool _leftHsvInit = false;
        private static Texture2D _leftHueBar, _leftSvSquare;

        // HSV state for right eye
        private static float _rightH, _rightS, _rightV;
        private static bool _rightHsvInit = false;
        private static Texture2D _rightHueBar, _rightSvSquare;

        public static void DrawEyePanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();

            float contentHeight = data.MatchEyes ? 420f : 660f;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, contentHeight));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Left Eye Color ─────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Left Eye Color", headerStyle);
            y += 26f;

            if (!_leftHsvInit)
            {
                Color.RGBToHSV(data.LeftEyeColor, out _leftH, out _leftS, out _leftV);
                _leftHsvInit = true;
            }

            bool leftChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _leftH, ref _leftS, ref _leftV,
                _leftHueBar, _leftSvSquare, labelStyle);
            if (leftChanged)
            {
                data.LeftEyeColor = Color.HSVToRGB(_leftH, _leftS, _leftV);
                if (data.MatchEyes)
                    data.RightEyeColor = data.LeftEyeColor;
            }

            // Swatch
            var prevColor = GUI.color;
            GUI.color = data.LeftEyeColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            // ── Match Eyes Toggle ──────────────────────────────────────────
            bool oldMatch = data.MatchEyes;
            var matchStyle = data.MatchEyes ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect(4, y, 200, 26), data.MatchEyes ? "[X] Match Both Eyes" : "[ ] Match Both Eyes", matchStyle))
            {
                data.MatchEyes = !data.MatchEyes;
                if (data.MatchEyes)
                    data.RightEyeColor = data.LeftEyeColor;
            }
            y += 34f;

            // ── Right Eye Color (only if not matching) ─────────────────────
            if (!data.MatchEyes)
            {
                GUI.Label(new Rect(4, y, w, 22), "Right Eye Color", headerStyle);
                y += 26f;

                if (!_rightHsvInit)
                {
                    Color.RGBToHSV(data.RightEyeColor, out _rightH, out _rightS, out _rightV);
                    _rightHsvInit = true;
                }

                bool rightChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _rightH, ref _rightS, ref _rightV,
                    _rightHueBar, _rightSvSquare, labelStyle);
                if (rightChanged)
                    data.RightEyeColor = Color.HSVToRGB(_rightH, _rightS, _rightV);

                prevColor = GUI.color;
                GUI.color = data.RightEyeColor;
                GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
                GUI.color = prevColor;
                y += 30f;
            }

            // ── Eye Shape / Size Sliders ───────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Eye Properties", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Iris Size: {data.IrisSize:F2}", labelStyle);
            data.IrisSize = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.IrisSize, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Pupil Size: {data.PupilSize:F2}", labelStyle);
            data.PupilSize = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.PupilSize, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Eye Shape: {data.EyeShapeSlider:F2}", labelStyle);
            data.EyeShapeSlider = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.EyeShapeSlider, 0f, 1f);
            y += 24f;

            GUI.EndScrollView();
        }

        private static void EnsureTextures()
        {
            if (_leftHueBar == null) _leftHueBar = CreateHueBar();
            if (_leftSvSquare == null) _leftSvSquare = CreateSVSquare();
            if (_rightHueBar == null) _rightHueBar = CreateHueBar();
            if (_rightSvSquare == null) _rightSvSquare = CreateSVSquare();
        }

        private static Texture2D CreateHueBar()
        {
            var tex = new Texture2D(20, 128, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 128; y++)
            {
                Color c = Color.HSVToRGB((float)y / 128f, 1f, 1f);
                for (int x = 0; x < 20; x++)
                    tex.SetPixel(x, 127 - y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSVSquare()
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < 128; x++)
                for (int y = 0; y < 128; y++)
                    tex.SetPixel(x, y, Color.HSVToRGB(0, (float)x / 128f, (float)y / 128f));
            tex.Apply();
            return tex;
        }

        public static void ResetCache()
        {
            _leftHsvInit = false;
            _rightHsvInit = false;
        }
    }
}

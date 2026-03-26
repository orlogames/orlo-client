using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for hair customization.
    /// Style grid, length/thickness/curl sliders, color pickers, facial hair.
    /// </summary>
    public static class HairPanel
    {
        private static Vector2 _scrollPos;

        private static readonly string[] HairStyleNames =
        {
            "Short", "Medium", "Long", "Ponytail", "Braided", "Shaved", "Mohawk", "Bald"
        };

        private static readonly string[] FacialHairNames =
        {
            "None", "Stubble", "Goatee", "Full Beard", "Mustache"
        };

        // HSV state for hair color
        private static float _hairH, _hairS, _hairV;
        private static bool _hairHsvInit = false;
        private static Texture2D _hairHueBar, _hairSvSquare;

        // HSV state for highlight color
        private static float _hlH, _hlS, _hlV;
        private static bool _hlHsvInit = false;
        private static Texture2D _hlHueBar, _hlSvSquare;

        public static void DrawHairPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();

            float contentHeight = 700f;
            if (data.Gender == 0) contentHeight += 260f; // Facial hair section

            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, contentHeight));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Hair Style Grid (8 styles, 4x2) ───────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Hair Style", headerStyle);
            y += 26f;

            float btnW = (w - 16f) / 4f;
            for (int i = 0; i < HairStyleNames.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float bx = 4 + col * (btnW + 4);
                float by = y + row * 32;
                var style = (data.HairStyle == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, btnW, 28), HairStyleNames[i], style))
                    data.HairStyle = i;
            }
            y += 70f;

            // ── Length / Thickness / Curl Sliders ──────────────────────────
            GUI.Label(new Rect(4, y, labelW, 20), $"Length: {data.HairLength:F2}", labelStyle);
            data.HairLength = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.HairLength, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Thickness: {data.HairThickness:F2}", labelStyle);
            data.HairThickness = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.HairThickness, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Curl: {data.HairCurl:F2}", labelStyle);
            data.HairCurl = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.HairCurl, 0f, 1f);
            y += 32f;

            // ── Hair Color (HSV) ───────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Hair Color", headerStyle);
            y += 26f;

            if (!_hairHsvInit)
            {
                Color.RGBToHSV(data.HairColor, out _hairH, out _hairS, out _hairV);
                _hairHsvInit = true;
            }

            bool hairChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _hairH, ref _hairS, ref _hairV,
                _hairHueBar, _hairSvSquare, labelStyle);
            if (hairChanged)
                data.HairColor = Color.HSVToRGB(_hairH, _hairS, _hairV);

            // Swatch
            var prevColor = GUI.color;
            GUI.color = data.HairColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            // ── Highlight Color (HSV) ──────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Highlight Color", headerStyle);
            y += 26f;

            if (!_hlHsvInit)
            {
                Color.RGBToHSV(data.HairHighlightColor, out _hlH, out _hlS, out _hlV);
                _hlHsvInit = true;
            }

            bool hlChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _hlH, ref _hlS, ref _hlV,
                _hlHueBar, _hlSvSquare, labelStyle);
            if (hlChanged)
                data.HairHighlightColor = Color.HSVToRGB(_hlH, _hlS, _hlV);

            prevColor = GUI.color;
            GUI.color = data.HairHighlightColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            // ── Facial Hair (male only) ────────────────────────────────────
            if (data.Gender == 0)
            {
                GUI.Label(new Rect(4, y, w, 22), "Facial Hair", headerStyle);
                y += 26f;

                float fbtnW = (w - 16f) / 3f;
                for (int i = 0; i < FacialHairNames.Length; i++)
                {
                    int col = i % 3;
                    int row = i / 3;
                    float bx = 4 + col * (fbtnW + 4);
                    float by = y + row * 32;
                    var style = (data.FacialHairStyle == i) ? selectedButtonStyle : buttonStyle;
                    if (GUI.Button(new Rect(bx, by, fbtnW, 28), FacialHairNames[i], style))
                        data.FacialHairStyle = i;
                }
                y += 70f;

                if (data.FacialHairStyle > 0)
                {
                    GUI.Label(new Rect(4, y, labelW, 20), $"Length: {data.FacialHairLength:F2}", labelStyle);
                    data.FacialHairLength = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                        data.FacialHairLength, 0f, 1f);
                    y += 24f;
                }
            }

            GUI.EndScrollView();
        }

        private static void EnsureTextures()
        {
            if (_hairHueBar == null) _hairHueBar = CreateHueBar();
            if (_hairSvSquare == null) _hairSvSquare = CreateSVSquare();
            if (_hlHueBar == null) _hlHueBar = CreateHueBar();
            if (_hlSvSquare == null) _hlSvSquare = CreateSVSquare();
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
            _hairHsvInit = false;
            _hlHsvInit = false;
        }
    }
}

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

        private static readonly string[] MaleHairStyleNames =
        {
            "Short", "Medium", "Long", "Ponytail", "Mohawk", "Buzz", "Slicked", "Braided"
        };

        private static readonly string[] FemaleHairStyleNames =
        {
            "Short", "Medium", "Long", "Ponytail", "Braids", "Bun", "Curly", "Undercut"
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

        // HSV state for root color
        private static float _rootH, _rootS, _rootV;
        private static bool _rootHsvInit = false;
        private static Texture2D _rootHueBar, _rootSvSquare;

        // HSV state for eyebrow color
        private static float _browH, _browS, _browV;
        private static bool _browHsvInit = false;
        private static Texture2D _browHueBar, _browSvSquare;

        public static void DrawHairPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();

            float contentHeight = 1200f;
            if (data.Gender == 0) contentHeight += 260f; // Facial hair section

            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, contentHeight));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Hair Style Grid (8 styles per gender, 4x2) ──────────────────
            GUI.Label(new Rect(4, y, w, 22), "Hair Style", headerStyle);
            y += 26f;

            string[] styleNames = data.Gender == 0 ? MaleHairStyleNames : FemaleHairStyleNames;
            int styleOffset = data.Gender == 0 ? 0 : 8; // Male 0-7, Female 8-15

            float btnW = (w - 16f) / 4f;
            for (int i = 0; i < styleNames.Length; i++)
            {
                int styleIndex = styleOffset + i;
                int col = i % 4;
                int row = i / 4;
                float bx = 4 + col * (btnW + 4);
                float by = y + row * 32;
                var style = (data.HairStyle == styleIndex) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, btnW, 28), styleNames[i], style))
                    data.HairStyle = styleIndex;
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

            // ── Root Color (HSV) ──────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Root Color", headerStyle);
            y += 26f;

            if (!_rootHsvInit)
            {
                Color.RGBToHSV(data.HairRootColor, out _rootH, out _rootS, out _rootV);
                _rootHsvInit = true;
            }

            bool rootChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _rootH, ref _rootS, ref _rootV,
                _rootHueBar, _rootSvSquare, labelStyle);
            if (rootChanged)
                data.HairRootColor = Color.HSVToRGB(_rootH, _rootS, _rootV);

            prevColor = GUI.color;
            GUI.color = data.HairRootColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Root Blend: {data.HairRootBlend:F2}", labelStyle);
            data.HairRootBlend = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.HairRootBlend, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Greying: {data.HairGreying:F2}", labelStyle);
            data.HairGreying = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.HairGreying, 0f, 1f);
            y += 32f;

            // ── Eyebrows ──────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Eyebrows", headerStyle);
            y += 26f;

            if (!_browHsvInit)
            {
                Color.RGBToHSV(data.EyebrowColor, out _browH, out _browS, out _browV);
                _browHsvInit = true;
            }

            bool browChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _browH, ref _browS, ref _browV,
                _browHueBar, _browSvSquare, labelStyle);
            if (browChanged)
                data.EyebrowColor = Color.HSVToRGB(_browH, _browS, _browV);

            prevColor = GUI.color;
            GUI.color = data.EyebrowColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Thickness: {data.EyebrowThickness:F2}", labelStyle);
            data.EyebrowThickness = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.EyebrowThickness, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Arch: {data.EyebrowArch:F2}", labelStyle);
            data.EyebrowArch = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.EyebrowArch, 0f, 1f);
            y += 32f;

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
            if (_rootHueBar == null) _rootHueBar = CreateHueBar();
            if (_rootSvSquare == null) _rootSvSquare = CreateSVSquare();
            if (_browHueBar == null) _browHueBar = CreateHueBar();
            if (_browSvSquare == null) _browSvSquare = CreateSVSquare();
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
            _rootHsvInit = false;
            _browHsvInit = false;
        }
    }
}

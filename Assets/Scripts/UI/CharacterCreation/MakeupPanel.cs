using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for makeup customization (Dragon Age / BDO style).
    /// Eyeliner, eyeshadow, blush, lipstick — each with intensity slider + HSV color picker.
    /// </summary>
    public static class MakeupPanel
    {
        private static Vector2 _scrollPos;

        // HSV state for eyeliner
        private static float _elH, _elS, _elV;
        private static bool _elHsvInit = false;
        private static Texture2D _elHueBar, _elSvSquare;

        // HSV state for eyeshadow
        private static float _esH, _esS, _esV;
        private static bool _esHsvInit = false;
        private static Texture2D _esHueBar, _esSvSquare;

        // HSV state for blush
        private static float _blH, _blS, _blV;
        private static bool _blHsvInit = false;
        private static Texture2D _blHueBar, _blSvSquare;

        // HSV state for lipstick
        private static float _lsH, _lsS, _lsV;
        private static bool _lsHsvInit = false;
        private static Texture2D _lsHueBar, _lsSvSquare;

        public static void DrawMakeupPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();

            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, 1100));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Eyeliner ──────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Eyeliner", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Intensity: {data.EyelinerIntensity:F2}", labelStyle);
            data.EyelinerIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.EyelinerIntensity, 0f, 1f);
            y += 24f;

            if (data.EyelinerIntensity > 0f)
            {
                if (!_elHsvInit)
                {
                    Color.RGBToHSV(data.EyelinerColor, out _elH, out _elS, out _elV);
                    _elHsvInit = true;
                }

                bool changed = SkinPanel.DrawHSVPicker(ref y, w, ref _elH, ref _elS, ref _elV,
                    _elHueBar, _elSvSquare, labelStyle);
                if (changed)
                    data.EyelinerColor = Color.HSVToRGB(_elH, _elS, _elV);

                DrawColorSwatch(ref y, data.EyelinerColor);
            }

            // ── Eyeshadow ─────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Eyeshadow", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Intensity: {data.EyeshadowIntensity:F2}", labelStyle);
            data.EyeshadowIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.EyeshadowIntensity, 0f, 1f);
            y += 24f;

            if (data.EyeshadowIntensity > 0f)
            {
                if (!_esHsvInit)
                {
                    Color.RGBToHSV(data.EyeshadowColor, out _esH, out _esS, out _esV);
                    _esHsvInit = true;
                }

                bool changed = SkinPanel.DrawHSVPicker(ref y, w, ref _esH, ref _esS, ref _esV,
                    _esHueBar, _esSvSquare, labelStyle);
                if (changed)
                    data.EyeshadowColor = Color.HSVToRGB(_esH, _esS, _esV);

                DrawColorSwatch(ref y, data.EyeshadowColor);
            }

            // ── Blush ─────────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Blush", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Intensity: {data.BlushIntensity:F2}", labelStyle);
            data.BlushIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.BlushIntensity, 0f, 1f);
            y += 24f;

            if (data.BlushIntensity > 0f)
            {
                if (!_blHsvInit)
                {
                    Color.RGBToHSV(data.BlushColor, out _blH, out _blS, out _blV);
                    _blHsvInit = true;
                }

                bool changed = SkinPanel.DrawHSVPicker(ref y, w, ref _blH, ref _blS, ref _blV,
                    _blHueBar, _blSvSquare, labelStyle);
                if (changed)
                    data.BlushColor = Color.HSVToRGB(_blH, _blS, _blV);

                DrawColorSwatch(ref y, data.BlushColor);
            }

            // ── Lipstick ──────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Lipstick", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Intensity: {data.LipstickIntensity:F2}", labelStyle);
            data.LipstickIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.LipstickIntensity, 0f, 1f);
            y += 24f;

            if (data.LipstickIntensity > 0f)
            {
                if (!_lsHsvInit)
                {
                    Color.RGBToHSV(data.LipstickColor, out _lsH, out _lsS, out _lsV);
                    _lsHsvInit = true;
                }

                bool changed = SkinPanel.DrawHSVPicker(ref y, w, ref _lsH, ref _lsS, ref _lsV,
                    _lsHueBar, _lsSvSquare, labelStyle);
                if (changed)
                    data.LipstickColor = Color.HSVToRGB(_lsH, _lsS, _lsV);

                DrawColorSwatch(ref y, data.LipstickColor);
            }

            GUI.Label(new Rect(4, y, labelW, 20), $"Lip Gloss: {data.LipGloss:F2}", labelStyle);
            data.LipGloss = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.LipGloss, 0f, 1f);
            y += 24f;

            GUI.EndScrollView();
        }

        private static void DrawColorSwatch(ref float y, Color color)
        {
            var prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;
        }

        private static void EnsureTextures()
        {
            if (_elHueBar == null) _elHueBar = CreateHueBar();
            if (_elSvSquare == null) _elSvSquare = CreateSVSquare();
            if (_esHueBar == null) _esHueBar = CreateHueBar();
            if (_esSvSquare == null) _esSvSquare = CreateSVSquare();
            if (_blHueBar == null) _blHueBar = CreateHueBar();
            if (_blSvSquare == null) _blSvSquare = CreateSVSquare();
            if (_lsHueBar == null) _lsHueBar = CreateHueBar();
            if (_lsSvSquare == null) _lsSvSquare = CreateSVSquare();
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
            _elHsvInit = false;
            _esHsvInit = false;
            _blHsvInit = false;
            _lsHsvInit = false;
        }
    }
}

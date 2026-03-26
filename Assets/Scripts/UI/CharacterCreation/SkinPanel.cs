using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for skin customization.
    /// HSV color picker for skin, freckle controls, aging, roughness, texture variation.
    /// </summary>
    public static class SkinPanel
    {
        private static Vector2 _scrollPos;

        // HSV picker state
        private static Texture2D _hueBarTex;
        private static Texture2D _svSquareTex;
        private static Texture2D _freckleHueBarTex;
        private static Texture2D _freckleSvSquareTex;

        // Cached HSV values for skin
        private static float _skinH, _skinS, _skinV;
        private static bool _skinHsvInit = false;

        // Cached HSV values for freckle color
        private static float _freckleH, _freckleS, _freckleV;
        private static bool _freckleHsvInit = false;

        public static void DrawSkinPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();

            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, 650));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Skin Color (HSV Picker) ────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Skin Color", headerStyle);
            y += 26f;

            if (!_skinHsvInit)
            {
                Color.RGBToHSV(data.SkinColor, out _skinH, out _skinS, out _skinV);
                _skinHsvInit = true;
            }

            bool skinChanged = DrawHSVPicker(ref y, w, ref _skinH, ref _skinS, ref _skinV,
                _hueBarTex, _svSquareTex, labelStyle);
            if (skinChanged)
                data.SkinColor = Color.HSVToRGB(_skinH, _skinS, _skinV);

            // Color preview swatch
            var prevColor = GUI.color;
            GUI.color = data.SkinColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            // ── Freckles ───────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Freckles", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Density: {data.FreckleDensity:F2}", labelStyle);
            data.FreckleDensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.FreckleDensity, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Size: {data.FreckleSize:F2}", labelStyle);
            data.FreckleSize = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.FreckleSize, 0f, 1f);
            y += 24f;

            // Freckle color picker
            GUI.Label(new Rect(4, y, w, 20), "Freckle Color:", labelStyle);
            y += 22f;

            if (!_freckleHsvInit)
            {
                Color.RGBToHSV(data.FreckleColor, out _freckleH, out _freckleS, out _freckleV);
                _freckleHsvInit = true;
            }

            bool freckleChanged = DrawHSVPicker(ref y, w, ref _freckleH, ref _freckleS, ref _freckleV,
                _freckleHueBarTex, _freckleSvSquareTex, labelStyle);
            if (freckleChanged)
                data.FreckleColor = Color.HSVToRGB(_freckleH, _freckleS, _freckleV);

            prevColor = GUI.color;
            GUI.color = data.FreckleColor;
            GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
            GUI.color = prevColor;
            y += 30f;

            // ── Other Skin Controls ────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Skin Properties", headerStyle);
            y += 26f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Aging: {data.Aging:F2}", labelStyle);
            data.Aging = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16), data.Aging, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Roughness: {data.Roughness:F2}", labelStyle);
            data.Roughness = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.Roughness, 0f, 1f);
            y += 24f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Texture Var: {data.SkinTextureVariation:F2}", labelStyle);
            data.SkinTextureVariation = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.SkinTextureVariation, 0f, 1f);
            y += 24f;

            GUI.EndScrollView();
        }

        /// <summary>
        /// Draw an HSV color picker (hue bar + saturation/value square).
        /// Returns true if the color was changed.
        /// </summary>
        public static bool DrawHSVPicker(ref float y, float width, ref float h, ref float s, ref float v,
            Texture2D hueBarTex, Texture2D svSquareTex, GUIStyle labelStyle)
        {
            bool changed = false;
            float pickerSize = Mathf.Min(width - 40f, 160f);
            float hueBarWidth = 20f;

            // Saturation/Value square
            Rect svRect = new Rect(4, y, pickerSize, pickerSize);
            UpdateSVTexture(svSquareTex, h);
            GUI.DrawTexture(svRect, svSquareTex);

            // Draw crosshair on SV square
            float cx = svRect.x + s * svRect.width;
            float cy = svRect.y + (1f - v) * svRect.height;
            var oldColor = GUI.color;
            GUI.color = (v > 0.5f) ? Color.black : Color.white;
            GUI.DrawTexture(new Rect(cx - 4, cy - 1, 9, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 4, 3, 9), Texture2D.whiteTexture);
            GUI.color = oldColor;

            // Hue bar (vertical, to the right of the SV square)
            Rect hueRect = new Rect(svRect.xMax + 8, y, hueBarWidth, pickerSize);
            GUI.DrawTexture(hueRect, hueBarTex);

            // Draw hue indicator
            float hueY = hueRect.y + h * hueRect.height;
            oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(hueRect.x - 2, hueY - 1, hueBarWidth + 4, 3), Texture2D.whiteTexture);
            GUI.color = oldColor;

            // Input handling
            var e = Event.current;
            if (e != null)
            {
                if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                {
                    Vector2 mp = e.mousePosition;
                    if (svRect.Contains(mp))
                    {
                        s = Mathf.Clamp01((mp.x - svRect.x) / svRect.width);
                        v = 1f - Mathf.Clamp01((mp.y - svRect.y) / svRect.height);
                        changed = true;
                        e.Use();
                    }
                    else if (hueRect.Contains(mp))
                    {
                        h = Mathf.Clamp01((mp.y - hueRect.y) / hueRect.height);
                        changed = true;
                        e.Use();
                    }
                }
            }

            y += pickerSize + 8f;
            return changed;
        }

        private static void EnsureTextures()
        {
            if (_hueBarTex == null)
                _hueBarTex = CreateHueBarTexture(20, 128);
            if (_svSquareTex == null)
                _svSquareTex = CreateSVTexture(128, 128, 0f);
            if (_freckleHueBarTex == null)
                _freckleHueBarTex = CreateHueBarTexture(20, 128);
            if (_freckleSvSquareTex == null)
                _freckleSvSquareTex = CreateSVTexture(128, 128, 0f);
        }

        private static Texture2D CreateHueBarTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
            {
                float hue = (float)y / height;
                Color c = Color.HSVToRGB(hue, 1f, 1f);
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, height - 1 - y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSVTexture(int width, int height, float hue)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int x = 0; x < width; x++)
            {
                float sat = (float)x / width;
                for (int y = 0; y < height; y++)
                {
                    float val = (float)y / height;
                    tex.SetPixel(x, y, Color.HSVToRGB(hue, sat, val));
                }
            }
            tex.Apply();
            return tex;
        }

        private static void UpdateSVTexture(Texture2D tex, float hue)
        {
            if (tex == null) return;
            int w = tex.width, h = tex.height;
            for (int x = 0; x < w; x++)
            {
                float sat = (float)x / w;
                for (int y = 0; y < h; y++)
                {
                    float val = (float)y / h;
                    tex.SetPixel(x, y, Color.HSVToRGB(hue, sat, val));
                }
            }
            tex.Apply();
        }

        /// <summary>
        /// Call when switching away from skin panel to reinitialize HSV cache next time.
        /// </summary>
        public static void ResetCache()
        {
            _skinHsvInit = false;
            _freckleHsvInit = false;
        }
    }
}

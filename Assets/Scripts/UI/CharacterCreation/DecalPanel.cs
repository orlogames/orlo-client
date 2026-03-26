using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for body decals (tattoos/markings).
    /// Body region selector, decal type grid, per-decal position/rotation/scale/tint controls.
    /// </summary>
    public static class DecalPanel
    {
        private static Vector2 _scrollPos;
        private static int _selectedDecalIndex = -1;
        private static int _selectedRegion = 0;
        private static int _selectedDecalType = 1;

        // HSV for decal tint
        private static float _tintH, _tintS, _tintV;
        private static bool _tintHsvInit = false;
        private static Texture2D _tintHueBar, _tintSvSquare;

        private static readonly string[] RegionNames =
        {
            "Head", "Chest", "L.Arm", "R.Arm", "Back", "L.Leg", "R.Leg"
        };

        public static void DrawDecalPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            EnsureTextures();

            float contentHeight = 800f;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, contentHeight));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Body Region Selector ───────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Body Region", headerStyle);
            y += 26f;

            float rBtnW = (w - 28f) / 4f;
            for (int i = 0; i < RegionNames.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float bx = 4 + col * (rBtnW + 4);
                float by = y + row * 32;
                var style = (_selectedRegion == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, rBtnW, 28), RegionNames[i], style))
                    _selectedRegion = i;
            }
            y += 70f;

            // ── Decal Type Grid (12 preset IDs) ───────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Decal Pattern", headerStyle);
            y += 26f;

            float dBtnW = (w - 28f) / 4f;
            for (int i = 1; i <= 12; i++)
            {
                int col = (i - 1) % 4;
                int row = (i - 1) / 4;
                float bx = 4 + col * (dBtnW + 4);
                float by = y + row * 32;
                var style = (_selectedDecalType == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, dBtnW, 28), $"#{i}", style))
                    _selectedDecalType = i;
            }
            y += 100f;

            // ── Add / Remove Buttons ───────────────────────────────────────
            bool canAdd = data.Decals.Count < AppearanceData.MaxDecals;
            GUI.enabled = canAdd;
            if (GUI.Button(new Rect(4, y, 120, 28), "+ Add Decal", buttonStyle))
            {
                var entry = new AppearanceData.DecalEntry
                {
                    DecalId = (uint)_selectedDecalType,
                    BodyRegion = _selectedRegion,
                    PositionU = 0.5f,
                    PositionV = 0.5f,
                    Rotation = 0f,
                    Scale = 1f,
                    Tint = Color.white
                };
                data.Decals.Add(entry);
                _selectedDecalIndex = data.Decals.Count - 1;
                _tintHsvInit = false;
            }
            GUI.enabled = true;

            bool canRemove = _selectedDecalIndex >= 0 && _selectedDecalIndex < data.Decals.Count;
            GUI.enabled = canRemove;
            if (GUI.Button(new Rect(134, y, 120, 28), "- Remove", buttonStyle))
            {
                data.Decals.RemoveAt(_selectedDecalIndex);
                _selectedDecalIndex = Mathf.Min(_selectedDecalIndex, data.Decals.Count - 1);
                _tintHsvInit = false;
            }
            GUI.enabled = true;

            GUI.Label(new Rect(270, y, 200, 28),
                $"Decals: {data.Decals.Count}/{AppearanceData.MaxDecals}", labelStyle);
            y += 36f;

            // ── Decal List ─────────────────────────────────────────────────
            if (data.Decals.Count > 0)
            {
                GUI.Label(new Rect(4, y, w, 22), "Placed Decals", headerStyle);
                y += 26f;

                for (int i = 0; i < data.Decals.Count; i++)
                {
                    var d = data.Decals[i];
                    string regionName = d.BodyRegion < RegionNames.Length ? RegionNames[d.BodyRegion] : "?";
                    string label = $"#{d.DecalId} on {regionName}";
                    var style = (i == _selectedDecalIndex) ? selectedButtonStyle : buttonStyle;
                    if (GUI.Button(new Rect(4, y, w, 24), label, style))
                    {
                        _selectedDecalIndex = i;
                        _tintHsvInit = false;
                    }
                    y += 28f;
                }
                y += 8f;

                // ── Selected Decal Controls ────────────────────────────────
                if (_selectedDecalIndex >= 0 && _selectedDecalIndex < data.Decals.Count)
                {
                    var decal = data.Decals[_selectedDecalIndex];

                    GUI.Label(new Rect(4, y, w, 22), "Decal Properties", headerStyle);
                    y += 26f;

                    GUI.Label(new Rect(4, y, labelW, 20), $"Position U: {decal.PositionU:F2}", labelStyle);
                    decal.PositionU = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                        decal.PositionU, 0f, 1f);
                    y += 24f;

                    GUI.Label(new Rect(4, y, labelW, 20), $"Position V: {decal.PositionV:F2}", labelStyle);
                    decal.PositionV = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                        decal.PositionV, 0f, 1f);
                    y += 24f;

                    GUI.Label(new Rect(4, y, labelW, 20), $"Rotation: {decal.Rotation:F0}", labelStyle);
                    decal.Rotation = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                        decal.Rotation, 0f, 360f);
                    y += 24f;

                    GUI.Label(new Rect(4, y, labelW, 20), $"Scale: {decal.Scale:F2}", labelStyle);
                    decal.Scale = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                        decal.Scale, 0.1f, 3f);
                    y += 24f;

                    // Tint color picker
                    GUI.Label(new Rect(4, y, w, 20), "Tint Color:", labelStyle);
                    y += 22f;

                    if (!_tintHsvInit)
                    {
                        Color.RGBToHSV(decal.Tint, out _tintH, out _tintS, out _tintV);
                        _tintHsvInit = true;
                    }

                    bool tintChanged = SkinPanel.DrawHSVPicker(ref y, w, ref _tintH, ref _tintS, ref _tintV,
                        _tintHueBar, _tintSvSquare, labelStyle);
                    if (tintChanged)
                        decal.Tint = Color.HSVToRGB(_tintH, _tintS, _tintV);

                    var prevColor = GUI.color;
                    GUI.color = decal.Tint;
                    GUI.DrawTexture(new Rect(4, y, 60, 20), Texture2D.whiteTexture);
                    GUI.color = prevColor;
                    y += 30f;
                }
            }
            else
            {
                y += 10f;
                GUI.Label(new Rect(4, y, w, 22), "No decals placed. Select a region and pattern, then click Add.",
                    labelStyle);
                y += 30f;
            }

            GUI.EndScrollView();
        }

        private static void EnsureTextures()
        {
            if (_tintHueBar == null)
            {
                _tintHueBar = new Texture2D(20, 128, TextureFormat.RGB24, false);
                _tintHueBar.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 128; y++)
                {
                    Color c = Color.HSVToRGB((float)y / 128f, 1f, 1f);
                    for (int x = 0; x < 20; x++)
                        _tintHueBar.SetPixel(x, 127 - y, c);
                }
                _tintHueBar.Apply();
            }
            if (_tintSvSquare == null)
            {
                _tintSvSquare = new Texture2D(128, 128, TextureFormat.RGB24, false);
                _tintSvSquare.wrapMode = TextureWrapMode.Clamp;
                for (int x = 0; x < 128; x++)
                    for (int y = 0; y < 128; y++)
                        _tintSvSquare.SetPixel(x, y, Color.HSVToRGB(0, (float)x / 128f, (float)y / 128f));
                _tintSvSquare.Apply();
            }
        }

        public static void ResetCache()
        {
            _tintHsvInit = false;
            _selectedDecalIndex = -1;
        }
    }
}

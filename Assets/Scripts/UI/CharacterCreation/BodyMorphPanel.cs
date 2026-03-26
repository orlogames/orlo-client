using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for body morph sliders.
    /// Groups: Upper Body, Lower Body, Proportions.
    /// </summary>
    public static class BodyMorphPanel
    {
        private static bool _upperOpen = true;
        private static bool _lowerOpen = true;
        private static bool _proportionsOpen = true;
        private static Vector2 _scrollPos;

        public static void DrawBodyPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            float contentHeight = CalculateHeight();
            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, contentHeight));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 150f;
            float sliderW = w - labelW - 50f;

            // ── Upper Body ─────────────────────────────────────────────────
            _upperOpen = DrawGroupHeader("Upper Body", _upperOpen, ref y, w, buttonStyle);
            if (_upperOpen)
            {
                DrawSlider(ref y, labelW, sliderW, "Shoulder Width", ref data.ShoulderWidth, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Chest Depth", ref data.ChestDepth, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Arm Length", ref data.ArmLength, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Arm Thickness", ref data.ArmThickness, labelStyle);
                y += 8f;
            }

            // ── Lower Body ─────────────────────────────────────────────────
            _lowerOpen = DrawGroupHeader("Lower Body", _lowerOpen, ref y, w, buttonStyle);
            if (_lowerOpen)
            {
                DrawSlider(ref y, labelW, sliderW, "Hip Width", ref data.HipWidth, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Waist Width", ref data.WaistWidth, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Leg Length", ref data.LegLength, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Leg Thickness", ref data.LegThickness, labelStyle);
                y += 8f;
            }

            // ── Proportions ────────────────────────────────────────────────
            _proportionsOpen = DrawGroupHeader("Proportions", _proportionsOpen, ref y, w, buttonStyle);
            if (_proportionsOpen)
            {
                DrawSlider(ref y, labelW, sliderW, "Torso Length", ref data.TorsoLength, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Muscle Definition", ref data.MuscleDefinition, labelStyle);
                DrawSlider(ref y, labelW, sliderW, "Body Fat", ref data.BodyFat, labelStyle);
                y += 8f;
            }

            // Also show legacy height/build sliders at the bottom
            y += 10f;
            GUI.Label(new Rect(4, y, w, 20), "Overall", headerStyle);
            y += 26f;
            DrawSlider(ref y, labelW, sliderW, "Height", ref data.Height, labelStyle);
            DrawSlider(ref y, labelW, sliderW, "Build", ref data.Build, labelStyle);

            GUI.EndScrollView();
        }

        private static float CalculateHeight()
        {
            float h = 0f;
            h += 30f;
            if (_upperOpen) h += 4 * 24f + 8f;
            h += 30f;
            if (_lowerOpen) h += 4 * 24f + 8f;
            h += 30f;
            if (_proportionsOpen) h += 3 * 24f + 8f;
            h += 10f + 26f + 2 * 24f; // Overall section
            return h + 20f;
        }

        private static bool DrawGroupHeader(string label, bool isOpen, ref float y, float width,
            GUIStyle buttonStyle)
        {
            string prefix = isOpen ? "[-] " : "[+] ";
            if (GUI.Button(new Rect(0, y, width, 26), prefix + label, buttonStyle))
                isOpen = !isOpen;
            y += 30f;
            return isOpen;
        }

        private static void DrawSlider(ref float y, float labelW, float sliderW,
            string label, ref float value, GUIStyle labelStyle)
        {
            GUI.Label(new Rect(4, y, labelW, 20), $"{label}: {value:F2}", labelStyle);
            value = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16), value, 0f, 1f);
            y += 24f;
        }
    }
}

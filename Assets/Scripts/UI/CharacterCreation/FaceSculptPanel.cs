using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for detailed face sculpting via 26 blend-shape sliders.
    /// Groups: Eyes, Nose, Mouth, Jaw, Cheeks, Forehead, Other — all collapsible.
    /// </summary>
    public static class FaceSculptPanel
    {
        private static bool _eyesOpen = true;
        private static bool _noseOpen = true;
        private static bool _mouthOpen = true;
        private static bool _jawOpen = true;
        private static bool _cheeksOpen = true;
        private static bool _foreheadOpen = true;
        private static bool _neckHeadOpen = false;
        private static bool _otherOpen = false;

        private static Vector2 _scrollPos;

        /// <summary>
        /// Draw the face sculpting panel. Returns the Y advance used.
        /// </summary>
        public static void DrawFacePanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, CalculateHeight()));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Eyes (6 sliders) ───────────────────────────────────────────
            _eyesOpen = DrawGroupHeader("Eyes (6)", _eyesOpen, ref y, w, headerStyle, buttonStyle);
            if (_eyesOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Eye Spacing", ref data.EyeSpacing, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Eye Depth", ref data.EyeDepth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Eye Height", ref data.EyeHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Eye Width", ref data.EyeWidth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Eye Tilt", ref data.EyeTilt, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Brow Ridge", ref data.BrowRidge, labelStyle);
                y += 8f;
            }

            // ── Nose (6 sliders) ───────────────────────────────────────────
            _noseOpen = DrawGroupHeader("Nose (6)", _noseOpen, ref y, w, headerStyle, buttonStyle);
            if (_noseOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Bridge Width", ref data.NoseBridgeWidth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Bridge Height", ref data.NoseBridgeHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Tip Width", ref data.NoseTipWidth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Tip Height", ref data.NoseTipHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Nostril Flare", ref data.NoseNostrilFlare, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Nose Length", ref data.NoseLength, labelStyle);
                y += 8f;
            }

            // ── Mouth (6 sliders) ──────────────────────────────────────────
            _mouthOpen = DrawGroupHeader("Mouth (6)", _mouthOpen, ref y, w, headerStyle, buttonStyle);
            if (_mouthOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Upper Lip", ref data.LipFullnessUpper, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Lower Lip", ref data.LipFullnessLower, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Lip Width", ref data.LipWidth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Mouth Height", ref data.MouthHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Teeth Gap", ref data.TeethGap, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Overbite", ref data.Overbite, labelStyle);
                y += 8f;
            }

            // ── Jaw (4 sliders) ────────────────────────────────────────────
            _jawOpen = DrawGroupHeader("Jaw (4)", _jawOpen, ref y, w, headerStyle, buttonStyle);
            if (_jawOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Chin Height", ref data.ChinHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Chin Width", ref data.ChinWidth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Chin Depth", ref data.ChinDepth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Jaw Angle", ref data.JawAngle, labelStyle);
                y += 8f;
            }

            // ── Cheeks (2 sliders) ─────────────────────────────────────────
            _cheeksOpen = DrawGroupHeader("Cheeks (2)", _cheeksOpen, ref y, w, headerStyle, buttonStyle);
            if (_cheeksOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Cheekbone Height", ref data.CheekboneHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Cheekbone Width", ref data.CheekboneWidth, labelStyle);
                y += 8f;
            }

            // ── Forehead (3 sliders) ───────────────────────────────────────
            _foreheadOpen = DrawGroupHeader("Forehead (3)", _foreheadOpen, ref y, w, headerStyle, buttonStyle);
            if (_foreheadOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Forehead Slope", ref data.ForeheadSlope, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Forehead Height", ref data.ForeheadHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Brow Height", ref data.BrowHeight, labelStyle);
                y += 8f;
            }

            // ── Neck & Head (4 sliders) ────────────────────────────────────
            _neckHeadOpen = DrawGroupHeader("Neck & Head (4)", _neckHeadOpen, ref y, w, headerStyle, buttonStyle);
            if (_neckHeadOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Neck Length", ref data.NeckLength, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Neck Thickness", ref data.NeckThickness, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Head Size", ref data.HeadSize, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Ear Protrusion", ref data.EarProtrusion, labelStyle);
                y += 8f;
            }

            // ── Other (3 sliders) ──────────────────────────────────────────
            _otherOpen = DrawGroupHeader("Other (3)", _otherOpen, ref y, w, headerStyle, buttonStyle);
            if (_otherOpen)
            {
                DrawSlider(ref y, w, labelW, sliderW, "Temple Width", ref data.TempleWidth, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Crown Height", ref data.CrownHeight, labelStyle);
                DrawSlider(ref y, w, labelW, sliderW, "Jaw Roundness", ref data.JawRoundness, labelStyle);
                y += 8f;
            }

            GUI.EndScrollView();
        }

        private static float CalculateHeight()
        {
            float h = 0f;
            h += 30f; // Eyes header
            if (_eyesOpen) h += 6 * 24f + 8f;
            h += 30f; // Nose header
            if (_noseOpen) h += 6 * 24f + 8f;
            h += 30f; // Mouth header
            if (_mouthOpen) h += 6 * 24f + 8f;
            h += 30f; // Jaw header
            if (_jawOpen) h += 4 * 24f + 8f;
            h += 30f; // Cheeks header
            if (_cheeksOpen) h += 2 * 24f + 8f;
            h += 30f; // Forehead header
            if (_foreheadOpen) h += 3 * 24f + 8f;
            h += 30f; // Neck & Head header
            if (_neckHeadOpen) h += 4 * 24f + 8f;
            h += 30f; // Other header
            if (_otherOpen) h += 3 * 24f + 8f;
            return h + 20f;
        }

        private static bool DrawGroupHeader(string label, bool isOpen, ref float y, float width,
            GUIStyle headerStyle, GUIStyle buttonStyle)
        {
            string prefix = isOpen ? "[-] " : "[+] ";
            if (GUI.Button(new Rect(0, y, width, 26), prefix + label, buttonStyle))
                isOpen = !isOpen;
            y += 30f;
            return isOpen;
        }

        private static void DrawSlider(ref float y, float totalWidth, float labelW, float sliderW,
            string label, ref float value, GUIStyle labelStyle)
        {
            GUI.Label(new Rect(4, y, labelW, 20), $"{label}: {value:F2}", labelStyle);
            value = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16), value, 0f, 1f);
            y += 24f;
        }
    }
}

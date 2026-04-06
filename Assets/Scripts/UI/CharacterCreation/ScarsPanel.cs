using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// OnGUI panel for scars, birthmarks, and voice customization (Star Citizen style).
    /// Face scars, body scars, birthmarks — each with style selector + intensity/size slider.
    /// Voice type selection and pitch slider.
    /// </summary>
    public static class ScarsPanel
    {
        private static Vector2 _scrollPos;

        private static readonly string[] FaceScarNames =
        {
            "None", "Slash", "Burns", "Claw Marks", "Stitch",
            "Deep Cut", "Acid", "Shrapnel", "Battle Worn"
        };

        private static readonly string[] BodyScarNames =
        {
            "None", "Slash", "Burns", "Bullet Wounds",
            "Whip Marks", "Surgical", "Brand"
        };

        private static readonly string[] BirthmarkNames =
        {
            "None", "Port Wine", "Cafe Au Lait", "Mongolian Spot", "Macular"
        };

        private static readonly string[] VoiceTypeNames =
        {
            "Voice 1", "Voice 2", "Voice 3", "Voice 4"
        };

        public static void DrawScarsPanel(Rect area, ref AppearanceData data, GUIStyle headerStyle,
            GUIStyle labelStyle, GUIStyle buttonStyle, GUIStyle selectedButtonStyle)
        {
            _scrollPos = GUI.BeginScrollView(area, _scrollPos,
                new Rect(0, 0, area.width - 20, 700));

            float y = 0f;
            float w = area.width - 30f;
            float labelW = 140f;
            float sliderW = w - labelW - 50f;

            // ── Face Scars ────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Face Scars", headerStyle);
            y += 26f;

            float fbtnW = (w - 16f) / 3f;
            for (int i = 0; i < FaceScarNames.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float bx = 4 + col * (fbtnW + 4);
                float by = y + row * 32;
                var style = (data.FaceScarStyle == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, fbtnW, 28), FaceScarNames[i], style))
                    data.FaceScarStyle = i;
            }
            y += ((FaceScarNames.Length + 2) / 3) * 32 + 8;

            if (data.FaceScarStyle > 0)
            {
                GUI.Label(new Rect(4, y, labelW, 20), $"Intensity: {data.FaceScarIntensity:F2}", labelStyle);
                data.FaceScarIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                    data.FaceScarIntensity, 0f, 1f);
                y += 24f;
            }
            y += 8f;

            // ── Body Scars ────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Body Scars", headerStyle);
            y += 26f;

            for (int i = 0; i < BodyScarNames.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float bx = 4 + col * (fbtnW + 4);
                float by = y + row * 32;
                var style = (data.BodyScarStyle == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, fbtnW, 28), BodyScarNames[i], style))
                    data.BodyScarStyle = i;
            }
            y += ((BodyScarNames.Length + 2) / 3) * 32 + 8;

            if (data.BodyScarStyle > 0)
            {
                GUI.Label(new Rect(4, y, labelW, 20), $"Intensity: {data.BodyScarIntensity:F2}", labelStyle);
                data.BodyScarIntensity = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                    data.BodyScarIntensity, 0f, 1f);
                y += 24f;
            }
            y += 8f;

            // ── Birthmarks ────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Birthmarks", headerStyle);
            y += 26f;

            for (int i = 0; i < BirthmarkNames.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float bx = 4 + col * (fbtnW + 4);
                float by = y + row * 32;
                var style = (data.BirthmarkStyle == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, by, fbtnW, 28), BirthmarkNames[i], style))
                    data.BirthmarkStyle = i;
            }
            y += ((BirthmarkNames.Length + 2) / 3) * 32 + 8;

            if (data.BirthmarkStyle > 0)
            {
                GUI.Label(new Rect(4, y, labelW, 20), $"Size: {data.BirthmarkSize:F2}", labelStyle);
                data.BirthmarkSize = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                    data.BirthmarkSize, 0f, 1f);
                y += 24f;
            }
            y += 16f;

            // ── Voice ─────────────────────────────────────────────────────
            GUI.Label(new Rect(4, y, w, 22), "Voice", headerStyle);
            y += 26f;

            float vbtnW = (w - 16f) / 4f;
            for (int i = 0; i < VoiceTypeNames.Length; i++)
            {
                float bx = 4 + i * (vbtnW + 4);
                var style = (data.VoiceType == i) ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(new Rect(bx, y, vbtnW, 28), VoiceTypeNames[i], style))
                    data.VoiceType = i;
            }
            y += 36f;

            GUI.Label(new Rect(4, y, labelW, 20), $"Pitch: {data.VoicePitch:F2}", labelStyle);
            data.VoicePitch = GUI.HorizontalSlider(new Rect(labelW + 4, y + 4, sliderW, 16),
                data.VoicePitch, 0f, 1f);
            y += 24f;

            // Pitch description
            string pitchDesc = data.VoicePitch < 0.3f ? "Deep" :
                               data.VoicePitch > 0.7f ? "High" : "Normal";
            GUI.Label(new Rect(labelW + 4, y, 100, 20), pitchDesc, labelStyle);
            y += 24f;

            GUI.EndScrollView();
        }

        public static void ResetCache()
        {
            // No cached state for this panel
        }
    }
}

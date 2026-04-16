using UnityEngine;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// Draws clickable, draggable circles overlaid on the character preview when
    /// the Face (and similar) tabs are active. Each circle maps to one or two
    /// face-blendshape sliders so the user can sculpt the face directly on the
    /// 3D preview instead of hunting for sliders in the side panel.
    ///
    /// Drag right/left = X axis slider. Drag down/up = Y axis slider.
    /// All slider values stay clamped to [0,1].
    /// </summary>
    public static class FacePivotOverlay
    {
        // Which slider a drag axis controls. Kept compact rather than listing
        // every face slider — these are the ones that read well as direct manipulation.
        private enum Action
        {
            None,
            EyeSpacing, EyeHeight, EyeWidth, EyeTilt,
            BrowHeight, BrowRidge,
            ForeheadHeight, ForeheadSlope,
            NoseBridgeWidth, NoseBridgeHeight,
            NoseTipWidth, NoseTipHeight,
            LipWidth, LipFullnessUpper, MouthHeight,
            ChinHeight, ChinWidth, ChinDepth,
            CheekboneHeight, CheekboneWidth,
            JawAngle, JawRoundness,
        }

        private struct Pivot
        {
            public Vector2 uv;        // 0..1 within preview rect
            public string label;
            public Action xDrag;      // horizontal drag → this slider
            public Action yDrag;      // vertical drag (down = increase) → this slider
        }

        // Pivots are positioned for a face-framed camera (Face focus mode).
        // UV (0,0) = top-left of preview rect, (1,1) = bottom-right.
        private static readonly Pivot[] _pivots =
        {
            new Pivot { uv = new Vector2(0.50f, 0.18f), label = "Forehead",
                xDrag = Action.ForeheadSlope,    yDrag = Action.ForeheadHeight },
            new Pivot { uv = new Vector2(0.50f, 0.30f), label = "Brow",
                xDrag = Action.BrowRidge,        yDrag = Action.BrowHeight },
            new Pivot { uv = new Vector2(0.36f, 0.40f), label = "L Eye",
                xDrag = Action.EyeSpacing,       yDrag = Action.EyeHeight },
            new Pivot { uv = new Vector2(0.64f, 0.40f), label = "R Eye",
                xDrag = Action.EyeWidth,         yDrag = Action.EyeTilt },
            new Pivot { uv = new Vector2(0.20f, 0.55f), label = "Cheek L",
                xDrag = Action.CheekboneWidth,   yDrag = Action.CheekboneHeight },
            new Pivot { uv = new Vector2(0.80f, 0.55f), label = "Cheek R",
                xDrag = Action.CheekboneWidth,   yDrag = Action.CheekboneHeight },
            new Pivot { uv = new Vector2(0.50f, 0.50f), label = "Nose",
                xDrag = Action.NoseBridgeWidth,  yDrag = Action.NoseBridgeHeight },
            new Pivot { uv = new Vector2(0.50f, 0.62f), label = "Tip",
                xDrag = Action.NoseTipWidth,     yDrag = Action.NoseTipHeight },
            new Pivot { uv = new Vector2(0.50f, 0.74f), label = "Mouth",
                xDrag = Action.LipWidth,         yDrag = Action.LipFullnessUpper },
            new Pivot { uv = new Vector2(0.20f, 0.78f), label = "Jaw L",
                xDrag = Action.JawRoundness,     yDrag = Action.JawAngle },
            new Pivot { uv = new Vector2(0.80f, 0.78f), label = "Jaw R",
                xDrag = Action.JawRoundness,     yDrag = Action.JawAngle },
            new Pivot { uv = new Vector2(0.50f, 0.92f), label = "Chin",
                xDrag = Action.ChinWidth,        yDrag = Action.ChinHeight },
        };

        private static int _draggingIndex = -1;
        private static Vector2 _dragStartMouse;
        private static Vector2 _dragStartValues;

        private const float PIVOT_RADIUS = 11f;
        private const float DRAG_SENSITIVITY = 0.005f; // slider units per pixel

        private static Texture2D _circleTex;
        private static Texture2D _circleHotTex;
        private static GUIStyle _labelStyle;

        /// <summary>
        /// Draw the pivot overlay for the given preview rectangle and apply
        /// any drag interactions back to <paramref name="data"/>. Call this
        /// from OnGUI right after drawing the preview RenderTexture.
        /// </summary>
        public static void Draw(Rect previewRect, AppearanceData data)
        {
            if (data == null) return;
            EnsureResources();

            var e = Event.current;
            Vector2 mousePos = e != null ? e.mousePosition : Vector2.zero;

            // Compute pivot screen positions and draw the markers + labels.
            for (int i = 0; i < _pivots.Length; i++)
            {
                var p = _pivots[i];
                Vector2 c = PivotCenter(previewRect, p);
                Rect r = new Rect(c.x - PIVOT_RADIUS, c.y - PIVOT_RADIUS, PIVOT_RADIUS * 2, PIVOT_RADIUS * 2);

                bool active = (i == _draggingIndex);
                bool hovered = active || (e != null && r.Contains(mousePos));
                GUI.DrawTexture(r, hovered ? _circleHotTex : _circleTex);

                // Show label only when hovered/active to keep the face area uncluttered.
                if (hovered)
                {
                    var rect = new Rect(c.x - 50, c.y + PIVOT_RADIUS + 2, 100, 16);
                    GUI.Label(rect, p.label, _labelStyle);
                }
            }

            if (e == null) return;

            // Mouse-down on a pivot → start drag, capture initial values.
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = 0; i < _pivots.Length; i++)
                {
                    var p = _pivots[i];
                    Vector2 c = PivotCenter(previewRect, p);
                    if ((mousePos - c).sqrMagnitude <= PIVOT_RADIUS * PIVOT_RADIUS)
                    {
                        _draggingIndex = i;
                        _dragStartMouse = mousePos;
                        _dragStartValues = new Vector2(GetSlider(data, p.xDrag), GetSlider(data, p.yDrag));
                        e.Use();
                        return;
                    }
                }
            }
            else if (e.type == EventType.MouseDrag && _draggingIndex >= 0)
            {
                var p = _pivots[_draggingIndex];
                Vector2 delta = mousePos - _dragStartMouse;
                // Y is inverted because screen Y grows downward but most face sliders
                // (height) feel right when dragging up = increase.
                float newX = Mathf.Clamp01(_dragStartValues.x + delta.x * DRAG_SENSITIVITY);
                float newY = Mathf.Clamp01(_dragStartValues.y - delta.y * DRAG_SENSITIVITY);
                SetSlider(data, p.xDrag, newX);
                SetSlider(data, p.yDrag, newY);
                e.Use();
            }
            else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && _draggingIndex >= 0)
            {
                _draggingIndex = -1;
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        /// <summary>True while the user is actively dragging a pivot — used
        /// by the orbit camera to suppress its own click handling.</summary>
        public static bool IsDragging => _draggingIndex >= 0;

        // ─────────────────────────────────────────────────────────────────────

        private static Vector2 PivotCenter(Rect r, Pivot p)
        {
            return new Vector2(r.x + r.width * p.uv.x, r.y + r.height * p.uv.y);
        }

        private static void EnsureResources()
        {
            if (_circleTex == null)
            {
                _circleTex = MakeCircle(28,
                    new Color(0.4f, 0.7f, 1f, 0.45f),
                    new Color(0.15f, 0.4f, 0.7f, 0.95f));
                _circleHotTex = MakeCircle(28,
                    new Color(1f, 0.85f, 0.3f, 0.85f),
                    new Color(0.7f, 0.45f, 0.1f, 1f));
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                _labelStyle.normal.textColor = new Color(1f, 0.95f, 0.7f, 1f);
            }
        }

        private static Texture2D MakeCircle(int size, Color fill, Color edge)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float cx = (size - 1) * 0.5f;
            float r = (size * 0.5f) - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color col = Color.clear;
                    if (d < r * 0.65f) col = fill;
                    else if (d < r) col = edge;
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static float GetSlider(AppearanceData d, Action a)
        {
            switch (a)
            {
                case Action.EyeSpacing:        return d.EyeSpacing;
                case Action.EyeHeight:         return d.EyeHeight;
                case Action.EyeWidth:          return d.EyeWidth;
                case Action.EyeTilt:           return d.EyeTilt;
                case Action.BrowHeight:        return d.BrowHeight;
                case Action.BrowRidge:         return d.BrowRidge;
                case Action.ForeheadHeight:    return d.ForeheadHeight;
                case Action.ForeheadSlope:     return d.ForeheadSlope;
                case Action.NoseBridgeWidth:   return d.NoseBridgeWidth;
                case Action.NoseBridgeHeight:  return d.NoseBridgeHeight;
                case Action.NoseTipWidth:      return d.NoseTipWidth;
                case Action.NoseTipHeight:     return d.NoseTipHeight;
                case Action.LipWidth:          return d.LipWidth;
                case Action.LipFullnessUpper:  return d.LipFullnessUpper;
                case Action.MouthHeight:       return d.MouthHeight;
                case Action.ChinHeight:        return d.ChinHeight;
                case Action.ChinWidth:         return d.ChinWidth;
                case Action.ChinDepth:         return d.ChinDepth;
                case Action.CheekboneHeight:   return d.CheekboneHeight;
                case Action.CheekboneWidth:    return d.CheekboneWidth;
                case Action.JawAngle:          return d.JawAngle;
                case Action.JawRoundness:      return d.JawRoundness;
            }
            return 0.5f;
        }

        private static void SetSlider(AppearanceData d, Action a, float v)
        {
            switch (a)
            {
                case Action.EyeSpacing:        d.EyeSpacing = v; break;
                case Action.EyeHeight:         d.EyeHeight = v; break;
                case Action.EyeWidth:          d.EyeWidth = v; break;
                case Action.EyeTilt:           d.EyeTilt = v; break;
                case Action.BrowHeight:        d.BrowHeight = v; break;
                case Action.BrowRidge:         d.BrowRidge = v; break;
                case Action.ForeheadHeight:    d.ForeheadHeight = v; break;
                case Action.ForeheadSlope:     d.ForeheadSlope = v; break;
                case Action.NoseBridgeWidth:   d.NoseBridgeWidth = v; break;
                case Action.NoseBridgeHeight:  d.NoseBridgeHeight = v; break;
                case Action.NoseTipWidth:      d.NoseTipWidth = v; break;
                case Action.NoseTipHeight:     d.NoseTipHeight = v; break;
                case Action.LipWidth:          d.LipWidth = v; break;
                case Action.LipFullnessUpper:  d.LipFullnessUpper = v; break;
                case Action.MouthHeight:       d.MouthHeight = v; break;
                case Action.ChinHeight:        d.ChinHeight = v; break;
                case Action.ChinWidth:         d.ChinWidth = v; break;
                case Action.ChinDepth:         d.ChinDepth = v; break;
                case Action.CheekboneHeight:   d.CheekboneHeight = v; break;
                case Action.CheekboneWidth:    d.CheekboneWidth = v; break;
                case Action.JawAngle:          d.JawAngle = v; break;
                case Action.JawRoundness:      d.JawRoundness = v; break;
            }
        }
    }
}

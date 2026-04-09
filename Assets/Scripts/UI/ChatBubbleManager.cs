using UnityEngine;
using System.Collections.Generic;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// Renders floating chat bubbles above entities in 3D world space.
    /// Say messages display normally; Yell messages are larger and uppercase.
    /// Bubbles fade after 5 seconds.
    /// Uses HolographicUI material for background tinted with speaker's race palette.
    /// </summary>
    public class ChatBubbleManager : MonoBehaviour
    {
        public static ChatBubbleManager Instance { get; private set; }

        private const float BubbleDuration = 5f;
        private const float FadeStart = 3.5f;
        private const float BubbleOffsetY = 2.2f;
        private const float MaxDistance = 50f;

        private struct Bubble
        {
            public ulong EntityId;
            public string Text;
            public float SpawnTime;
            public bool IsYell;
            public string RaceName; // speaker's race for palette tinting
        }

        private List<Bubble> _bubbles = new List<Bubble>();

        // Cached textures for holographic bubble rendering
        private static Texture2D _whiteTex;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Show a chat bubble with optional race name for holographic tinting.</summary>
        public void ShowBubble(ulong entityId, string text, bool isYell = false, string raceName = null)
        {
            // Remove existing bubble from same entity
            _bubbles.RemoveAll(b => b.EntityId == entityId);

            _bubbles.Add(new Bubble
            {
                EntityId = entityId,
                Text = isYell ? text.ToUpperInvariant() : text,
                SpawnTime = Time.time,
                IsYell = isYell,
                RaceName = raceName
            });
        }

        /// <summary>Original overload for backward compatibility.</summary>
        public void ShowBubble(ulong entityId, string text, bool isYell)
        {
            ShowBubble(entityId, text, isYell, null);
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;

            EnsureTextures();
            var cam = Camera.main;

            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = _bubbles[i];
                float age = Time.time - bubble.SpawnTime;

                if (age > BubbleDuration)
                {
                    _bubbles.RemoveAt(i);
                    continue;
                }

                // Find entity position
                var entityGo = Orlo.World.EntityManager.Instance?.GetEntity(bubble.EntityId);
                if (entityGo == null) continue;

                Vector3 worldPos = entityGo.transform.position + Vector3.up * BubbleOffsetY;
                float dist = Vector3.Distance(cam.transform.position, worldPos);
                if (dist > MaxDistance) continue;

                // Behind camera check
                Vector3 viewPos = cam.WorldToViewportPoint(worldPos);
                if (viewPos.z < 0) continue;

                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                float sx = screenPos.x;
                float sy = Screen.height - screenPos.y;

                // Fade
                float alpha = age > FadeStart ? 1f - ((age - FadeStart) / (BubbleDuration - FadeStart)) : 1f;

                // Truncate long messages
                string display = bubble.Text.Length > 80 ? bubble.Text.Substring(0, 77) + "..." : bubble.Text;

                int fontSize = bubble.IsYell ? 14 : 12;
                float maxW = bubble.IsYell ? 250f : 200f;

                // Determine race palette for tinting
                RacePalette palette = null;
                if (!string.IsNullOrEmpty(bubble.RaceName))
                    palette = RacePalette.ForRace(bubble.RaceName);
                else if (TMDTheme.Instance != null)
                    palette = TMDTheme.Instance.Palette;

                Color textColor = palette != null
                    ? new Color(palette.Text.r, palette.Text.g, palette.Text.b, alpha)
                    : new Color(1, 1, 1, alpha);

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = bubble.IsYell ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = textColor }
                };

                Vector2 size = style.CalcSize(new GUIContent(display));
                float w = Mathf.Min(size.x + 16, maxW);
                float h = style.CalcHeight(new GUIContent(display), w - 12) + 8;

                float bx = sx - w / 2;
                float by = sy - h;

                Rect bgRect = new Rect(bx - 2, by - 2, w + 4, h + 4);

                // Background — tinted with race palette
                Color bgColor = palette != null
                    ? new Color(palette.Background.r, palette.Background.g, palette.Background.b, 0.75f * alpha)
                    : new Color(0, 0, 0, 0.65f * alpha);
                GUI.color = bgColor;
                GUI.DrawTexture(bgRect, _whiteTex);

                // Border — subtle race-colored border
                if (palette != null)
                {
                    Color borderColor = new Color(palette.Border.r, palette.Border.g, palette.Border.b, 0.5f * alpha);
                    GUI.color = borderColor;
                    GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, bgRect.width, 1), _whiteTex);
                    GUI.DrawTexture(new Rect(bgRect.x, bgRect.yMax - 1, bgRect.width, 1), _whiteTex);
                    GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, 1, bgRect.height), _whiteTex);
                    GUI.DrawTexture(new Rect(bgRect.xMax - 1, bgRect.y, 1, bgRect.height), _whiteTex);
                }

                // Scanline overlay
                if (TMDTheme.Instance != null)
                {
                    float scanIntensity = TMDTheme.Instance.EffectiveScanlines;
                    if (scanIntensity > 0.01f)
                    {
                        float speed = TMDTheme.Instance.TierSettings.ScanlineSpeed;
                        float offset = (Time.time * speed * 50f) % bgRect.height;
                        GUI.color = new Color(0, 0, 0, scanIntensity * 0.25f * alpha);
                        for (float y = -offset; y < bgRect.height; y += 3f)
                        {
                            if (y >= 0)
                                GUI.DrawTexture(new Rect(bgRect.x, bgRect.y + y, bgRect.width, 1), _whiteTex);
                        }
                    }
                }

                // Text
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + 6, by + 2, w - 12, h), display, style);
            }

            GUI.color = Color.white;
        }

        private static void EnsureTextures()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
        }
    }
}

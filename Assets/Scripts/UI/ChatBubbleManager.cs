using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Renders floating chat bubbles above entities in 3D world space.
    /// Say messages display normally; Yell messages are larger and uppercase.
    /// Bubbles fade after 5 seconds.
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
        }

        private List<Bubble> _bubbles = new List<Bubble>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ShowBubble(ulong entityId, string text, bool isYell = false)
        {
            // Remove existing bubble from same entity
            _bubbles.RemoveAll(b => b.EntityId == entityId);

            _bubbles.Add(new Bubble
            {
                EntityId = entityId,
                Text = isYell ? text.ToUpperInvariant() : text,
                SpawnTime = Time.time,
                IsYell = isYell
            });
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;

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

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = bubble.IsYell ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1, 1, 1, alpha) }
                };

                Vector2 size = style.CalcSize(new GUIContent(display));
                float w = Mathf.Min(size.x + 16, maxW);
                float h = style.CalcHeight(new GUIContent(display), w - 12) + 8;

                float bx = sx - w / 2;
                float by = sy - h;

                // Background
                GUI.color = new Color(0, 0, 0, 0.65f * alpha);
                GUI.DrawTexture(new Rect(bx - 2, by - 2, w + 4, h + 4), Texture2D.whiteTexture);

                // Text
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(new Rect(bx + 6, by + 2, w - 12, h), display, style);
            }

            GUI.color = Color.white;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Corner minimap — renders a top-down view with player position and markers.
    /// Data driven by server MinimapUpdate messages.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        private struct Marker
        {
            public Vector3 worldPos;
            public string type;
            public string label;
            public Color color;
        }

        private const float MAP_SIZE = 180f;
        private const float MAP_MARGIN = 16f;
        private const float MAP_RANGE = 192f; // World units visible on minimap

        private Texture2D minimapTexture;
        private Texture2D dotTexture;
        private List<Marker> markers = new List<Marker>();
        private Vector3 playerPosition;
        private float playerRotationY;
        private bool visible = true;

        private GUIStyle borderStyle;
        private GUIStyle labelStyle;
        private GUIStyle coordStyle;
        private bool stylesInitialized = false;

        /// <summary>
        /// Called when server sends MinimapUpdate
        /// </summary>
        public void OnMinimapUpdate(int cellX, int cellZ, int resolution, byte[] colorData)
        {
            if (colorData == null || colorData.Length == 0) return;

            if (minimapTexture == null || minimapTexture.width != resolution)
            {
                minimapTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                minimapTexture.filterMode = FilterMode.Bilinear;
                minimapTexture.wrapMode = TextureWrapMode.Clamp;
            }

            if (colorData.Length == resolution * resolution * 4)
            {
                minimapTexture.LoadRawTextureData(colorData);
                minimapTexture.Apply();
            }
        }

        /// <summary>
        /// Update markers from server data
        /// </summary>
        public void SetMarkers(List<Marker> newMarkers)
        {
            markers = newMarkers ?? new List<Marker>();
        }

        public void AddMarker(Vector3 pos, string type, string label, Color color)
        {
            markers.Add(new Marker { worldPos = pos, type = type, label = label, color = color });
        }

        public void ClearMarkers() => markers.Clear();

        public void UpdatePlayerPosition(Vector3 pos, float rotY)
        {
            playerPosition = pos;
            playerRotationY = rotY;
        }

        public void Toggle() => visible = !visible;

        private void Start()
        {
            // Generate a default green/brown minimap texture
            minimapTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            minimapTexture.filterMode = FilterMode.Bilinear;
            var colors = new Color[128 * 128];
            for (int i = 0; i < colors.Length; i++)
            {
                float nx = (i % 128) / 128f;
                float ny = (i / 128) / 128f;
                float noise = Mathf.PerlinNoise(nx * 4f, ny * 4f);
                colors[i] = Color.Lerp(
                    new Color(0.15f, 0.25f, 0.1f),
                    new Color(0.25f, 0.2f, 0.12f),
                    noise);
            }
            minimapTexture.SetPixels(colors);
            minimapTexture.Apply();

            // Dot texture for markers
            dotTexture = new Texture2D(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    dotTexture.SetPixel(x, y, dist < 3.5f ? Color.white : Color.clear);
                }
            dotTexture.Apply();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            borderStyle = new GUIStyle(GUI.skin.box);
            borderStyle.normal.background = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.15f, 0.8f));
            borderStyle.border = new RectOffset(2, 2, 2, 2);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 9;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            coordStyle = new GUIStyle(GUI.skin.label);
            coordStyle.fontSize = 10;
            coordStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            coordStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void OnGUI()
        {
            if (!visible) return;
            InitStyles();

            float x = Screen.width - MAP_SIZE - MAP_MARGIN;
            float y = MAP_MARGIN;

            // Border
            GUI.Box(new Rect(x - 3, y - 3, MAP_SIZE + 6, MAP_SIZE + 6 + 18), "", borderStyle);

            // Map texture
            if (minimapTexture != null)
            {
                GUI.DrawTexture(new Rect(x, y, MAP_SIZE, MAP_SIZE), minimapTexture);
            }

            // Draw markers
            foreach (var marker in markers)
            {
                Vector2 mapPos = WorldToMinimap(marker.worldPos, x, y);
                if (mapPos.x < x || mapPos.x > x + MAP_SIZE ||
                    mapPos.y < y || mapPos.y > y + MAP_SIZE)
                    continue;

                var oldColor = GUI.color;
                GUI.color = marker.color;

                float dotSize = marker.type == "player" ? 10f :
                                marker.type == "quest" ? 8f : 6f;
                GUI.DrawTexture(
                    new Rect(mapPos.x - dotSize / 2, mapPos.y - dotSize / 2, dotSize, dotSize),
                    dotTexture);
                GUI.color = oldColor;

                // Label for quest/POI markers
                if (!string.IsNullOrEmpty(marker.label) && marker.type != "player")
                {
                    labelStyle.normal.textColor = marker.color;
                    GUI.Label(new Rect(mapPos.x - 30, mapPos.y - 18, 60, 14), marker.label, labelStyle);
                }
            }

            // Player arrow (center dot, rotated indicator)
            float cx = x + MAP_SIZE / 2;
            float cy = y + MAP_SIZE / 2;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - 4, cy - 4, 8, 8), dotTexture);

            // Direction indicator
            float rad = playerRotationY * Mathf.Deg2Rad;
            float dirX = cx + Mathf.Sin(rad) * 10f;
            float dirY = cy - Mathf.Cos(rad) * 10f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(dirX - 2, dirY - 2, 4, 4), dotTexture);
            GUI.color = Color.white;

            // Coordinates below map
            string coords = $"({playerPosition.x:F0}, {playerPosition.z:F0})";
            GUI.Label(new Rect(x, y + MAP_SIZE + 2, MAP_SIZE, 16), coords, coordStyle);
        }

        private Vector2 WorldToMinimap(Vector3 worldPos, float mapX, float mapY)
        {
            float dx = worldPos.x - playerPosition.x;
            float dz = worldPos.z - playerPosition.z;

            float nx = (dx / MAP_RANGE + 0.5f) * MAP_SIZE + mapX;
            float ny = (-dz / MAP_RANGE + 0.5f) * MAP_SIZE + mapY;

            return new Vector2(nx, ny);
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }
}

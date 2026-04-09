using UnityEngine;
using System.Collections.Generic;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// Corner minimap + full-screen world map (V key).
    /// Features:
    /// - Fog of war that reveals as the player explores
    /// - Threshold city marker always visible even in unexplored areas
    /// - M key toggles minimap visibility
    /// - V key opens/closes full-screen world map
    /// - Terrain-colored minimap generated from chunk heightmap data
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public struct Marker
        {
            public Vector3 worldPos;
            public string type;     // "city", "quest", "poi", "player", "creature", "spawn"
            public string label;
            public Color color;
            public bool alwaysVisible; // Show even in unexplored fog
        }

        // Minimap corner display
        private const float MINIMAP_SIZE = 200f;
        private const float MINIMAP_MARGIN = 16f;
        private const float MINIMAP_RANGE = 256f; // World units visible on minimap

        // World map full-screen
        private const float WORLD_MAP_PADDING = 60f;

        // Fog of war — tracks explored areas in a grid
        private const int FOG_RESOLUTION = 512;    // Texture resolution
        private const float FOG_WORLD_SIZE = 4096f; // World area covered by fog texture
        private const float REVEAL_RADIUS = 40f;    // Radius revealed around player (world units)

        // Known locations (always shown on map)
        private static readonly Vector3 ThresholdPos = new Vector3(512f, 0f, 512f);

        private Texture2D _minimapTerrain;   // Terrain color data
        private Texture2D _fogTexture;       // Fog of war (white = explored, black = hidden)
        private Texture2D _dotTexture;       // Soft circle for markers
        private Texture2D _arrowTexture;     // Player direction arrow
        private Color[] _fogPixels;

        private List<Marker> _markers = new List<Marker>();
        private Vector3 _playerPosition;
        private float _playerRotationY;
        private bool _minimapVisible = true;
        private bool _worldMapOpen = false;

        private GUIStyle _borderStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _coordStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _worldMapBgStyle;
        private bool _stylesInitialized;

        // Fog of war state
        private Vector2Int _lastFogUpdateCell = new Vector2Int(int.MinValue, int.MinValue);

        private void Start()
        {
            // Generate terrain texture (procedural green/brown/rock)
            _minimapTerrain = GenerateTerrainTexture(256);

            // Initialize fog of war — fully hidden
            _fogTexture = new Texture2D(FOG_RESOLUTION, FOG_RESOLUTION, TextureFormat.RGBA32, false);
            _fogTexture.filterMode = FilterMode.Bilinear;
            _fogTexture.wrapMode = TextureWrapMode.Clamp;
            _fogPixels = new Color[FOG_RESOLUTION * FOG_RESOLUTION];
            for (int i = 0; i < _fogPixels.Length; i++)
                _fogPixels[i] = Color.black;
            _fogTexture.SetPixels(_fogPixels);
            _fogTexture.Apply();

            // Soft circle dot for markers
            _dotTexture = CreateSoftCircle(16);

            // Arrow texture for player direction
            _arrowTexture = CreateArrowTexture(24);

            // Add Threshold as a permanent marker
            _markers.Add(new Marker
            {
                worldPos = ThresholdPos,
                type = "city",
                label = "Threshold",
                color = new Color(1f, 0.85f, 0.3f), // Gold
                alwaysVisible = true
            });
        }

        private void Update()
        {
            // Auto-update player position from tagged Player object
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _playerPosition = player.transform.position;
                _playerRotationY = player.transform.eulerAngles.y;
            }

            // V = toggle world map
            if (Input.GetKeyDown(KeyCode.V) && !ChatUI.Instance?.IsInputActive == true)
                _worldMapOpen = !_worldMapOpen;

            // M = toggle minimap
            if (Input.GetKeyDown(KeyCode.M) && !ChatUI.Instance?.IsInputActive == true)
                _minimapVisible = !_minimapVisible;

            // Update fog of war based on player position
            UpdateFogOfWar();
        }

        /// <summary>
        /// Called when server sends MinimapUpdate
        /// </summary>
        public void OnMinimapUpdate(int cellX, int cellZ, int resolution, byte[] colorData)
        {
            if (colorData == null || colorData.Length == 0) return;

            if (_minimapTerrain == null || _minimapTerrain.width != resolution)
            {
                _minimapTerrain = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                _minimapTerrain.filterMode = FilterMode.Bilinear;
                _minimapTerrain.wrapMode = TextureWrapMode.Clamp;
            }

            if (colorData.Length == resolution * resolution * 4)
            {
                _minimapTerrain.LoadRawTextureData(colorData);
                _minimapTerrain.Apply();
            }
        }

        public void SetMarkers(List<Marker> newMarkers)
        {
            // Preserve always-visible markers (like Threshold)
            var permanent = _markers.FindAll(m => m.alwaysVisible);
            _markers = newMarkers ?? new List<Marker>();
            foreach (var p in permanent)
            {
                if (!_markers.Exists(m => m.label == p.label && m.alwaysVisible))
                    _markers.Add(p);
            }
        }

        public void AddMarker(Vector3 pos, string type, string label, Color color, bool alwaysVisible = false)
        {
            _markers.Add(new Marker
            {
                worldPos = pos, type = type, label = label,
                color = color, alwaysVisible = alwaysVisible
            });
        }

        public void ClearMarkers()
        {
            var permanent = _markers.FindAll(m => m.alwaysVisible);
            _markers.Clear();
            _markers.AddRange(permanent);
        }

        public void UpdatePlayerPosition(Vector3 pos, float rotY)
        {
            _playerPosition = pos;
            _playerRotationY = rotY;
        }

        public void Toggle() => _minimapVisible = !_minimapVisible;

        // ===== Fog of War =====

        private void UpdateFogOfWar()
        {
            // Convert player position to fog grid cell
            float fogScale = FOG_RESOLUTION / FOG_WORLD_SIZE;
            int cx = Mathf.FloorToInt((_playerPosition.x / FOG_WORLD_SIZE + 0.5f) * FOG_RESOLUTION);
            int cz = Mathf.FloorToInt((_playerPosition.z / FOG_WORLD_SIZE + 0.5f) * FOG_RESOLUTION);
            var cell = new Vector2Int(cx, cz);

            // Only update if player moved to a new cell
            if (cell == _lastFogUpdateCell) return;
            _lastFogUpdateCell = cell;

            // Reveal area around player
            int revealPixels = Mathf.CeilToInt(REVEAL_RADIUS * fogScale);
            bool changed = false;

            for (int dy = -revealPixels; dy <= revealPixels; dy++)
            {
                for (int dx = -revealPixels; dx <= revealPixels; dx++)
                {
                    int px = cx + dx;
                    int py = cz + dy;
                    if (px < 0 || px >= FOG_RESOLUTION || py < 0 || py >= FOG_RESOLUTION) continue;

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > revealPixels) continue;

                    int idx = py * FOG_RESOLUTION + px;
                    // Soft edge falloff
                    float reveal = Mathf.Clamp01(1f - (dist / revealPixels) * 0.3f);
                    if (_fogPixels[idx].r < reveal)
                    {
                        _fogPixels[idx] = new Color(reveal, reveal, reveal, 1f);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                _fogTexture.SetPixels(_fogPixels);
                _fogTexture.Apply();
            }
        }

        private bool IsExplored(Vector3 worldPos)
        {
            float u = (worldPos.x / FOG_WORLD_SIZE + 0.5f);
            float v = (worldPos.z / FOG_WORLD_SIZE + 0.5f);
            int px = Mathf.Clamp(Mathf.FloorToInt(u * FOG_RESOLUTION), 0, FOG_RESOLUTION - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(v * FOG_RESOLUTION), 0, FOG_RESOLUTION - 1);
            return _fogPixels[py * FOG_RESOLUTION + px].r > 0.3f;
        }

        // ===== GUI Rendering =====

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _borderStyle = new GUIStyle(GUI.skin.box);
            _borderStyle.normal.background = MakeTex(1, 1, new Color(0.05f, 0.05f, 0.08f, 0.85f));

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 10;
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.fontStyle = FontStyle.Bold;

            _coordStyle = new GUIStyle(GUI.skin.label);
            _coordStyle.fontSize = 11;
            _coordStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            _coordStyle.alignment = TextAnchor.MiddleCenter;

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 18;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(1f, 0.9f, 0.7f);
            _titleStyle.alignment = TextAnchor.MiddleCenter;

            _worldMapBgStyle = new GUIStyle(GUI.skin.box);
            _worldMapBgStyle.normal.background = MakeTex(1, 1, new Color(0.02f, 0.02f, 0.04f, 0.92f));
        }

        private void OnGUI()
        {
            InitStyles();

            if (_worldMapOpen)
                DrawWorldMap();
            else if (_minimapVisible)
                DrawMinimap();
        }

        // ===== Corner Minimap =====

        private void DrawMinimap()
        {
            float x = Screen.width - MINIMAP_SIZE - MINIMAP_MARGIN;
            float y = MINIMAP_MARGIN;

            var p = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // TMD panel border around minimap
            Rect borderRect = new Rect(x - 4, y - 4, MINIMAP_SIZE + 8, MINIMAP_SIZE + 8 + 20);
            TMDTheme.DrawPanel(borderRect);

            // Circular mask effect via texture clipping
            if (_minimapTerrain != null)
            {
                // Draw terrain centered on player
                GUI.DrawTexture(new Rect(x, y, MINIMAP_SIZE, MINIMAP_SIZE), _minimapTerrain);
            }

            // Draw fog overlay on minimap (race-tinted)
            DrawMinimapFog(x, y);

            // Draw markers
            DrawMarkers(x, y, MINIMAP_SIZE, MINIMAP_RANGE, false);

            // Player arrow (center) — race-colored
            DrawPlayerIndicator(x + MINIMAP_SIZE / 2, y + MINIMAP_SIZE / 2, 14f);

            // Coordinates below
            string coords = $"({_playerPosition.x:F0}, {_playerPosition.z:F0})";
            _coordStyle.normal.textColor = p.Text;
            GUI.Label(new Rect(x, y + MINIMAP_SIZE + 2, MINIMAP_SIZE, 18), coords, _coordStyle);

            // Hint text
            var hintStyle = new GUIStyle(_coordStyle);
            hintStyle.fontSize = 9;
            hintStyle.normal.textColor = p.TextDim;
            GUI.Label(new Rect(x, y - 16, MINIMAP_SIZE, 14), "V - World Map  |  M - Toggle", hintStyle);

            // TMD scanlines
            TMDTheme.DrawScanlines(new Rect(x, y, MINIMAP_SIZE, MINIMAP_SIZE));
        }

        private void DrawMinimapFog(float mapX, float mapY)
        {
            // Race-tinted fog of war
            var fp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // Calculate fog UV region centered on player
            float fogU = _playerPosition.x / FOG_WORLD_SIZE + 0.5f;
            float fogV = _playerPosition.z / FOG_WORLD_SIZE + 0.5f;
            float fogRange = MINIMAP_RANGE / FOG_WORLD_SIZE;

            // We need to draw a fog overlay — dark areas are unexplored
            // Since OnGUI can't easily do UV-offset textures, we'll create a cropped view
            // For performance, draw a semi-transparent dark overlay and punch holes for explored areas
            var oldColor = GUI.color;
            // Fog tinted toward race palette background
            GUI.color = new Color(fp.Background.r * 0.3f, fp.Background.g * 0.3f, fp.Background.b * 0.3f, 0.75f);

            // Draw fog as a grid of small cells
            int cells = 16;
            float cellSize = MINIMAP_SIZE / cells;
            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    // Map this minimap cell to world position
                    float worldX = _playerPosition.x + ((cx / (float)cells) - 0.5f) * MINIMAP_RANGE;
                    float worldZ = _playerPosition.z - ((cy / (float)cells) - 0.5f) * MINIMAP_RANGE;

                    // Check fog at this world position
                    float u = (worldX / FOG_WORLD_SIZE + 0.5f);
                    float v = (worldZ / FOG_WORLD_SIZE + 0.5f);
                    int px = Mathf.Clamp(Mathf.FloorToInt(u * FOG_RESOLUTION), 0, FOG_RESOLUTION - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt(v * FOG_RESOLUTION), 0, FOG_RESOLUTION - 1);
                    float explored = _fogPixels[py * FOG_RESOLUTION + px].r;

                    if (explored < 0.9f)
                    {
                        GUI.color = new Color(fp.Background.r * 0.3f, fp.Background.g * 0.3f, fp.Background.b * 0.3f, (1f - explored) * 0.8f);
                        GUI.DrawTexture(
                            new Rect(mapX + cx * cellSize, mapY + cy * cellSize, cellSize + 1, cellSize + 1),
                            Texture2D.whiteTexture);
                    }
                }
            }
            GUI.color = oldColor;
        }

        // ===== Full-Screen World Map =====

        private void DrawWorldMap()
        {
            float mapSize = Mathf.Min(Screen.width, Screen.height) - WORLD_MAP_PADDING * 2;
            float mapX = (Screen.width - mapSize) / 2;
            float mapY = (Screen.height - mapSize) / 2;

            var wp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // Dark background covering full screen
            GUI.color = new Color(0.02f, 0.02f, 0.04f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title with race primary color
            _titleStyle.normal.textColor = wp.Primary;
            GUI.Label(new Rect(mapX, mapY - 35, mapSize, 30), "VERIDIAN PRIME", _titleStyle);

            // Map border — TMD panel
            TMDTheme.DrawPanel(new Rect(mapX - 3, mapY - 3, mapSize + 6, mapSize + 6));

            // Draw terrain texture
            if (_minimapTerrain != null)
                GUI.DrawTexture(new Rect(mapX, mapY, mapSize, mapSize), _minimapTerrain);

            // Draw fog of war overlay
            DrawWorldMapFog(mapX, mapY, mapSize);

            // Draw markers (world map shows full FOG_WORLD_SIZE)
            DrawMarkers(mapX, mapY, mapSize, FOG_WORLD_SIZE, true);

            // Draw player position
            Vector2 playerMapPos = WorldToMapPos(_playerPosition, mapX, mapY, mapSize, FOG_WORLD_SIZE);
            DrawPlayerIndicator(playerMapPos.x, playerMapPos.y, 18f);

            // Coordinates
            string coords = $"Position: ({_playerPosition.x:F0}, {_playerPosition.z:F0})";
            _coordStyle.normal.textColor = wp.Text;
            GUI.Label(new Rect(mapX, mapY + mapSize + 8, mapSize, 20), coords, _coordStyle);

            // Close hint
            var hintStyle = new GUIStyle(_coordStyle);
            hintStyle.fontSize = 12;
            hintStyle.normal.textColor = wp.TextDim;
            GUI.Label(new Rect(mapX, mapY + mapSize + 28, mapSize, 20), "Press V to close", hintStyle);

            // TMD scanlines over the map
            TMDTheme.DrawScanlines(new Rect(mapX, mapY, mapSize, mapSize));
        }

        private void DrawWorldMapFog(float mapX, float mapY, float mapSize)
        {
            // Draw the fog texture directly over the map — race-tinted
            var wfp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;
            var oldColor = GUI.color;

            int cells = 64; // Higher resolution for world map
            float cellSize = mapSize / cells;

            for (int cy = 0; cy < cells; cy++)
            {
                for (int cx = 0; cx < cells; cx++)
                {
                    // Map cell to fog texture pixel
                    int fogX = Mathf.FloorToInt((cx / (float)cells) * FOG_RESOLUTION);
                    int fogY = Mathf.FloorToInt((1f - cy / (float)cells) * FOG_RESOLUTION); // Flip Y
                    fogX = Mathf.Clamp(fogX, 0, FOG_RESOLUTION - 1);
                    fogY = Mathf.Clamp(fogY, 0, FOG_RESOLUTION - 1);

                    float explored = _fogPixels[fogY * FOG_RESOLUTION + fogX].r;

                    if (explored < 0.9f)
                    {
                        GUI.color = new Color(wfp.Background.r * 0.3f, wfp.Background.g * 0.3f, wfp.Background.b * 0.3f, (1f - explored) * 0.85f);
                        GUI.DrawTexture(
                            new Rect(mapX + cx * cellSize, mapY + cy * cellSize, cellSize + 1, cellSize + 1),
                            Texture2D.whiteTexture);
                    }
                }
            }

            GUI.color = oldColor;
        }

        // ===== Marker Drawing =====

        private void DrawMarkers(float mapX, float mapY, float mapSize, float worldRange, bool isWorldMap)
        {
            foreach (var marker in _markers)
            {
                // Skip non-always-visible markers that are in fog
                if (!marker.alwaysVisible && !IsExplored(marker.worldPos))
                    continue;

                Vector2 pos;
                if (isWorldMap)
                    pos = WorldToMapPos(marker.worldPos, mapX, mapY, mapSize, worldRange);
                else
                    pos = WorldToMinimap(marker.worldPos, mapX, mapY);

                // Clamp to map bounds
                if (pos.x < mapX || pos.x > mapX + mapSize ||
                    pos.y < mapY || pos.y > mapY + mapSize)
                {
                    if (!isWorldMap) continue; // Skip off-minimap markers
                    // On world map, clamp to edge
                    pos.x = Mathf.Clamp(pos.x, mapX + 4, mapX + mapSize - 4);
                    pos.y = Mathf.Clamp(pos.y, mapY + 4, mapY + mapSize - 4);
                }

                var oldColor = GUI.color;
                GUI.color = marker.color;

                float dotSize = marker.type switch
                {
                    "city" => isWorldMap ? 18f : 12f,
                    "quest" => isWorldMap ? 12f : 8f,
                    "spawn" => isWorldMap ? 8f : 6f,
                    _ => isWorldMap ? 10f : 6f
                };

                // Draw marker dot
                GUI.DrawTexture(
                    new Rect(pos.x - dotSize / 2, pos.y - dotSize / 2, dotSize, dotSize),
                    _dotTexture);

                // Draw label
                if (!string.IsNullOrEmpty(marker.label))
                {
                    float labelWidth = isWorldMap ? 120f : 80f;
                    _labelStyle.normal.textColor = marker.color;
                    _labelStyle.fontSize = isWorldMap ? 12 : 9;

                    // Draw text shadow for readability
                    var shadowColor = _labelStyle.normal.textColor;
                    _labelStyle.normal.textColor = Color.black;
                    GUI.Label(new Rect(pos.x - labelWidth / 2 + 1, pos.y + dotSize / 2 + 1, labelWidth, 16),
                        marker.label, _labelStyle);
                    _labelStyle.normal.textColor = shadowColor;

                    // Actual text
                    _labelStyle.normal.textColor = marker.color;
                    GUI.Label(new Rect(pos.x - labelWidth / 2, pos.y + dotSize / 2, labelWidth, 16),
                        marker.label, _labelStyle);
                }

                GUI.color = oldColor;
            }
        }

        private void DrawPlayerIndicator(float cx, float cy, float size)
        {
            // Draw player arrow/chevron pointing in movement direction — race-colored
            var oldColor = GUI.color;
            var pp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // Glow behind arrow (race glow color)
            GUI.color = new Color(pp.Glow.r, pp.Glow.g, pp.Glow.b, 0.4f);
            float glowSize = size * 1.5f;
            GUI.DrawTexture(new Rect(cx - glowSize / 2, cy - glowSize / 2, glowSize, glowSize), _dotTexture);

            // Arrow rotated by player facing (race primary color)
            GUI.color = pp.Primary;
            var pivot = new Vector2(cx, cy);
            var matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(_playerRotationY, pivot);
            GUI.DrawTexture(new Rect(cx - size / 2, cy - size / 2, size, size), _arrowTexture);
            GUI.matrix = matrixBackup;

            GUI.color = oldColor;
        }

        // ===== Coordinate Transforms =====

        private Vector2 WorldToMinimap(Vector3 worldPos, float mapX, float mapY)
        {
            float dx = worldPos.x - _playerPosition.x;
            float dz = worldPos.z - _playerPosition.z;
            float nx = (dx / MINIMAP_RANGE + 0.5f) * MINIMAP_SIZE + mapX;
            float ny = (-dz / MINIMAP_RANGE + 0.5f) * MINIMAP_SIZE + mapY;
            return new Vector2(nx, ny);
        }

        private Vector2 WorldToMapPos(Vector3 worldPos, float mapX, float mapY, float mapSize, float worldRange)
        {
            // World map is centered at FOG_WORLD_SIZE/2 (world origin offset)
            float nx = (worldPos.x / worldRange + 0.5f) * mapSize + mapX;
            float ny = (-worldPos.z / worldRange + 0.5f) * mapSize + mapY;
            return new Vector2(nx, ny);
        }

        // ===== Texture Generation =====

        private Texture2D GenerateTerrainTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color[size * size];

            // Procedural terrain: mountains, plains, water
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    float ny = (float)y / size;

                    // Multi-octave noise for terrain
                    float h = Mathf.PerlinNoise(nx * 3f + 0.5f, ny * 3f + 0.5f) * 0.5f
                            + Mathf.PerlinNoise(nx * 7f + 1.3f, ny * 7f + 2.1f) * 0.3f
                            + Mathf.PerlinNoise(nx * 15f + 5.7f, ny * 15f + 3.2f) * 0.2f;

                    Color c;
                    if (h < 0.3f)
                        c = Color.Lerp(new Color(0.08f, 0.15f, 0.05f), new Color(0.12f, 0.22f, 0.08f), h / 0.3f);
                    else if (h < 0.5f)
                        c = Color.Lerp(new Color(0.12f, 0.22f, 0.08f), new Color(0.18f, 0.16f, 0.10f), (h - 0.3f) / 0.2f);
                    else if (h < 0.7f)
                        c = Color.Lerp(new Color(0.18f, 0.16f, 0.10f), new Color(0.22f, 0.18f, 0.12f), (h - 0.5f) / 0.2f);
                    else
                        c = Color.Lerp(new Color(0.22f, 0.18f, 0.12f), new Color(0.3f, 0.25f, 0.18f), (h - 0.7f) / 0.3f);

                    colors[y * size + x] = c;
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = dist < 0.7f ? 1f : Mathf.Clamp01((1f - dist) / 0.3f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateArrowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;
            // Draw a chevron/arrow pointing up
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - center) / center;
                    float ny = (y - center) / center;
                    // Arrow shape: triangle pointing up
                    bool inArrow = ny < 0 ?
                        (Mathf.Abs(nx) < (-ny * 0.6f + 0.1f)) :      // Top triangle
                        (Mathf.Abs(nx) < 0.15f && ny < 0.5f);          // Bottom stem
                    float alpha = inArrow ? 1f : 0f;
                    // Soften edges
                    if (inArrow)
                    {
                        float edgeDist = ny < 0 ?
                            Mathf.Abs(Mathf.Abs(nx) - (-ny * 0.6f + 0.1f)) :
                            Mathf.Abs(Mathf.Abs(nx) - 0.15f);
                        if (edgeDist < 0.1f)
                            alpha *= edgeDist / 0.1f + 0.5f;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }
}

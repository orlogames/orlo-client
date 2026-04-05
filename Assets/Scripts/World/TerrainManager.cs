using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Manages procedural terrain chunks streamed from the server.
    /// Builds meshes from server heightmap + splatmap data with vertex coloring.
    /// Splatmap channels: R=grass, G=rock, B=dirt, A=sand
    /// </summary>
    public class TerrainManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 64;
        [SerializeField] private int viewDistance = 3;

        // Biome colors for splatmap blending
        private static readonly Color GrassColor = new Color(0.25f, 0.45f, 0.15f);   // Green
        private static readonly Color RockColor = new Color(0.42f, 0.40f, 0.38f);    // Grey
        private static readonly Color DirtColor = new Color(0.45f, 0.32f, 0.18f);    // Brown
        private static readonly Color SandColor = new Color(0.76f, 0.70f, 0.50f);    // Yellow-tan
        private static readonly Color DefaultColor = new Color(0.30f, 0.42f, 0.20f); // Fallback green

        private readonly Dictionary<Vector2Int, TerrainChunkData> _chunks = new();
        private readonly Dictionary<Vector2Int, GameObject> _activeChunks = new();
        private Vector2Int _lastPlayerChunk;
        private Material _terrainMat;

        private struct TerrainChunkData
        {
            public int Resolution;
            public float[] Heightmap;
            public byte[] Splatmap; // 4 bytes per vertex: grass, rock, dirt, sand
            public ulong Seed;
        }

        private void Awake()
        {
            // Create terrain material — custom vertex-color shader (won't be stripped from builds)
            var terrainShader = Shader.Find("Orlo/TerrainVertexColor");
            if (terrainShader == null)
            {
                Debug.LogWarning("[TerrainManager] Orlo/TerrainVertexColor shader not found, falling back to Standard");
                terrainShader = Shader.Find("Standard");
            }
            _terrainMat = new Material(terrainShader);
            _terrainMat.color = Color.white;
            _terrainMat.SetFloat("_Glossiness", 0.05f);
            Debug.Log("[TerrainManager] Initialized with terrain material");
        }

        private void Update()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            var pos = player.transform.position;
            var currentChunk = new Vector2Int(
                Mathf.FloorToInt(pos.x / chunkSize),
                Mathf.FloorToInt(pos.z / chunkSize)
            );

            if (currentChunk != _lastPlayerChunk)
            {
                _lastPlayerChunk = currentChunk;
                UpdateVisibleChunks();
            }
        }

        private void UpdateVisibleChunks()
        {
            var needed = new HashSet<Vector2Int>();
            for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
                needed.Add(_lastPlayerChunk + new Vector2Int(x, z));

            // Remove chunks no longer needed
            var toRemove = new List<Vector2Int>();
            foreach (var kv in _activeChunks)
                if (!needed.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            foreach (var key in toRemove)
            {
                Destroy(_activeChunks[key]);
                _activeChunks.Remove(key);
            }

            // Create missing chunks
            foreach (var coord in needed)
            {
                if (!_activeChunks.ContainsKey(coord))
                {
                    if (_chunks.TryGetValue(coord, out var data))
                        _activeChunks[coord] = BuildTerrainMesh(coord, data);
                    else
                        _activeChunks[coord] = CreatePlaceholderChunk(coord);
                }
            }
        }

        /// <summary>
        /// Check if we already have server heightmap data for a chunk.
        /// Used by ChunkStreamer to avoid re-requesting chunks.
        /// </summary>
        public bool HasChunkData(Vector2Int coord) => _chunks.ContainsKey(coord);

        /// <summary>
        /// Called when server sends terrain heightmap + splatmap data.
        /// </summary>
        public void ApplyTerrainChunk(Vector2Int coord, int resolution, byte[] heightmapBytes, byte[] splatmapBytes, ulong seed)
        {
            float[] heightmap = UnpackHeightmapF16(heightmapBytes, resolution * resolution);

            var data = new TerrainChunkData
            {
                Resolution = resolution,
                Heightmap = heightmap,
                Splatmap = splatmapBytes,
                Seed = seed
            };
            _chunks[coord] = data;

            // Replace placeholder if this chunk is currently visible
            if (_activeChunks.TryGetValue(coord, out var existing))
            {
                Destroy(existing);
                _activeChunks[coord] = BuildTerrainMesh(coord, data);
            }

            Debug.Log($"[Terrain] Chunk {coord}: {resolution}x{resolution}, " +
                      $"splatmap={splatmapBytes?.Length ?? 0}B, seed={seed:X}");

            // Notify ChunkStreamer so it can trigger detail generation
            ChunkStreamer.Instance?.OnChunkDataReceived(coord, heightmap, resolution, seed);

            // Update loading screen progress
            UI.LoadingScreenUI.Instance?.UpdateProgress(_chunks.Count);
        }

        private GameObject BuildTerrainMesh(Vector2Int coord, TerrainChunkData data)
        {
            int res = data.Resolution;
            float step = (float)chunkSize / (res - 1);
            float worldX = coord.x * chunkSize;
            float worldZ = coord.y * chunkSize;

            var vertices = new Vector3[res * res];
            var uvs = new Vector2[res * res];
            var normals = new Vector3[res * res];
            var colors = new Color[res * res];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = z * res + x;
                    float height = data.Heightmap[idx];
                    vertices[idx] = new Vector3(x * step, height, z * step);
                    uvs[idx] = new Vector2((float)x / (res - 1), (float)z / (res - 1));

                    // Calculate vertex color from splatmap or height-based fallback
                    colors[idx] = GetVertexColor(data, idx, height, x, z, res, step);
                }
            }

            // Calculate normals from heightmap (finite difference method)
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float hL = x > 0 ? data.Heightmap[z * res + (x - 1)] : data.Heightmap[z * res + x];
                    float hR = x < res - 1 ? data.Heightmap[z * res + (x + 1)] : data.Heightmap[z * res + x];
                    float hD = z > 0 ? data.Heightmap[(z - 1) * res + x] : data.Heightmap[z * res + x];
                    float hU = z < res - 1 ? data.Heightmap[(z + 1) * res + x] : data.Heightmap[z * res + x];
                    normals[z * res + x] = new Vector3(hL - hR, 2f * step, hD - hU).normalized;
                }
            }

            // Build triangle indices
            var triangles = new int[(res - 1) * (res - 1) * 6];
            int tri = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    int bl = z * res + x;
                    int br = bl + 1;
                    int tl = bl + res;
                    int tr = tl + 1;
                    triangles[tri++] = bl;
                    triangles[tri++] = tl;
                    triangles[tri++] = br;
                    triangles[tri++] = br;
                    triangles[tri++] = tl;
                    triangles[tri++] = tr;
                }
            }

            var mesh = new Mesh();
            if (vertices.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;

            var go = new GameObject($"Terrain_{coord.x}_{coord.y}");
            go.transform.position = new Vector3(worldX, 0, worldZ);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = _terrainMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            return go;
        }

        /// <summary>
        /// Get vertex color from splatmap data or height/slope-based fallback.
        /// Splatmap format: 4 bytes per vertex [grass, rock, dirt, sand] (0-255 weights).
        /// </summary>
        private Color GetVertexColor(TerrainChunkData data, int idx, float height, int x, int z, int res, float step)
        {
            // Try splatmap first
            if (data.Splatmap != null && idx * 4 + 3 < data.Splatmap.Length)
            {
                float grass = data.Splatmap[idx * 4] / 255f;
                float rock = data.Splatmap[idx * 4 + 1] / 255f;
                float dirt = data.Splatmap[idx * 4 + 2] / 255f;
                float sand = data.Splatmap[idx * 4 + 3] / 255f;

                return GrassColor * grass + RockColor * rock + DirtColor * dirt + SandColor * sand;
            }

            // Fallback: derive color from height and slope
            float slope = 0;
            if (x > 0 && x < res - 1 && z > 0 && z < res - 1)
            {
                float dx = data.Heightmap[z * res + (x + 1)] - data.Heightmap[z * res + (x - 1)];
                float dz = data.Heightmap[(z + 1) * res + x] - data.Heightmap[(z - 1) * res + x];
                slope = Mathf.Sqrt(dx * dx + dz * dz) / (2f * step);
            }

            // Steep slopes → rock, low areas → sand, mid → grass, high → dirt
            if (slope > 0.6f) return RockColor;
            if (height < 1f) return SandColor;
            if (height > 40f) return Color.Lerp(DirtColor, RockColor, (height - 40f) / 40f);

            // Grass with slight height variation
            float grassBlend = Mathf.Clamp01(1f - slope * 1.5f);
            return Color.Lerp(DirtColor, GrassColor, grassBlend);
        }

        /// <summary>
        /// Apply terrain height deltas from TMD operation.
        /// The server sends packed float32 deltas for the entire chunk heightmap.
        /// We add these deltas to the existing heights and rebuild the mesh.
        /// </summary>
        public void ApplyTerrainModification(int chunkX, int chunkZ, byte[] heightDeltaBytes)
        {
            var coord = new Vector2Int(chunkX, chunkZ);

            if (!_chunks.TryGetValue(coord, out var data))
            {
                Debug.LogWarning($"[Terrain] Cannot apply TMD mod — chunk {coord} not loaded");
                return;
            }

            // Unpack float32 deltas
            int deltaCount = heightDeltaBytes.Length / 4;
            int vertexCount = data.Heightmap.Length;
            int count = Mathf.Min(deltaCount, vertexCount);

            for (int i = 0; i < count; i++)
            {
                float delta = System.BitConverter.ToSingle(heightDeltaBytes, i * 4);
                data.Heightmap[i] += delta;
            }

            // Store updated data back
            _chunks[coord] = data;

            // Rebuild mesh if this chunk is currently visible
            if (_activeChunks.TryGetValue(coord, out var existing))
            {
                Destroy(existing);
                _activeChunks[coord] = BuildTerrainMesh(coord, data);
            }

            Debug.Log($"[Terrain] Applied TMD modification to chunk {coord}: {count} vertices modified");
        }

        private GameObject CreatePlaceholderChunk(Vector2Int coord)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = $"ChunkPlaceholder_{coord.x}_{coord.y}";
            go.transform.position = new Vector3(
                coord.x * chunkSize + chunkSize * 0.5f,
                0,
                coord.y * chunkSize + chunkSize * 0.5f
            );
            go.transform.localScale = new Vector3(chunkSize * 0.1f, 1, chunkSize * 0.1f);

            // Use a dark green placeholder instead of default pink
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.15f, 0.25f, 0.10f); // Dark green
            mat.SetFloat("_Glossiness", 0.05f);
            mr.material = mat;

            return go;
        }

        private static float[] UnpackHeightmapF16(byte[] packed, int count)
        {
            var result = new float[count];
            for (int i = 0; i < count && i * 2 + 1 < packed.Length; i++)
            {
                ushort f16 = (ushort)(packed[i * 2] | (packed[i * 2 + 1] << 8));
                result[i] = HalfToFloat(f16);
            }
            return result;
        }

        private static float HalfToFloat(ushort half)
        {
            int sign = (half >> 15) & 1;
            int exp = (half >> 10) & 0x1F;
            int mant = half & 0x3FF;

            if (exp == 0) return sign == 1 ? -0f : 0f;
            if (exp == 31) return sign == 1 ? float.NegativeInfinity : float.PositiveInfinity;

            float value = Mathf.Pow(2, exp - 15) * (1f + mant / 1024f);
            return sign == 1 ? -value : value;
        }
    }
}

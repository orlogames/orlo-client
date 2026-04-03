using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Manages procedural terrain chunks streamed from the server.
    /// Builds meshes from server heightmap data, streams chunks based on player proximity.
    /// </summary>
    public class TerrainManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 64;       // World units per chunk (matches server CHUNK_SIZE)
        [SerializeField] private int viewDistance = 3;      // Chunks in each direction
        [SerializeField] private Material terrainMaterial;

        [Header("LOD")]
        [SerializeField] private int meshResolution = 64;   // Vertices per edge (matches server CHUNK_RESOLUTION)

        private readonly Dictionary<Vector2Int, TerrainChunkData> _chunks = new();
        private readonly Dictionary<Vector2Int, GameObject> _activeChunks = new();
        private Vector2Int _lastPlayerChunk;

        private struct TerrainChunkData
        {
            public int Resolution;
            public float[] Heightmap;
            public ulong Seed;
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
            {
                needed.Add(_lastPlayerChunk + new Vector2Int(x, z));
            }

            // Remove chunks no longer needed
            var toRemove = new List<Vector2Int>();
            foreach (var kv in _activeChunks)
            {
                if (!needed.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
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
                    {
                        _activeChunks[coord] = BuildTerrainMesh(coord, data);
                    }
                    else
                    {
                        // No server data yet — show placeholder
                        _activeChunks[coord] = CreatePlaceholderChunk(coord);
                    }
                }
            }
        }

        /// <summary>
        /// Called when server sends terrain heightmap data.
        /// </summary>
        public void ApplyTerrainChunk(Vector2Int coord, int resolution, byte[] heightmapBytes, ulong seed)
        {
            // Unpack float16 heightmap to floats
            float[] heightmap = UnpackHeightmapF16(heightmapBytes, resolution * resolution);

            var data = new TerrainChunkData
            {
                Resolution = resolution,
                Heightmap = heightmap,
                Seed = seed
            };
            _chunks[coord] = data;

            // Replace placeholder if this chunk is currently visible
            if (_activeChunks.TryGetValue(coord, out var existing))
            {
                Destroy(existing);
                _activeChunks[coord] = BuildTerrainMesh(coord, data);
            }

            Debug.Log($"[Terrain] Applied chunk {coord} ({resolution}x{resolution}, seed={seed:X})");

            // Update loading screen progress
            UI.LoadingScreenUI.Instance?.UpdateProgress(_chunks.Count);
        }

        private GameObject BuildTerrainMesh(Vector2Int coord, TerrainChunkData data)
        {
            int res = data.Resolution;
            float step = (float)chunkSize / (res - 1);
            float worldX = coord.x * chunkSize;
            float worldZ = coord.y * chunkSize;

            // Build vertices and triangles
            var vertices = new Vector3[res * res];
            var uvs = new Vector2[res * res];
            var normals = new Vector3[res * res];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = z * res + x;
                    float height = data.Heightmap[idx];
                    vertices[idx] = new Vector3(x * step, height, z * step);
                    uvs[idx] = new Vector2((float)x / (res - 1), (float)z / (res - 1));
                }
            }

            // Calculate normals from heightmap
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

            // Triangles
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

            var go = new GameObject($"Terrain_{coord.x}_{coord.y}");
            go.transform.position = new Vector3(worldX, 0, worldZ);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = terrainMaterial != null ? terrainMaterial : new Material(Shader.Find("Standard"));

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            return go;
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

            if (terrainMaterial != null)
                go.GetComponent<MeshRenderer>().material = terrainMaterial;

            return go;
        }

        /// <summary>
        /// Unpack float16 (IEEE 754 half-precision) bytes to float array.
        /// </summary>
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

            if (exp == 0) return sign == 1 ? -0f : 0f; // Zero/subnormal
            if (exp == 31) return sign == 1 ? float.NegativeInfinity : float.PositiveInfinity;

            float value = Mathf.Pow(2, exp - 15) * (1f + mant / 1024f);
            return sign == 1 ? -value : value;
        }
    }
}

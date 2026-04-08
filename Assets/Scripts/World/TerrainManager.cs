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
            // Generate procedural terrain textures (one-time, ~20ms)
            TerrainTextures.Initialize();

            // Try the new textured terrain shader first, fall back to vertex color
            var terrainShader = Resources.Load<Shader>("Shaders/TerrainTextured");
            if (terrainShader == null)
                terrainShader = Shader.Find("Orlo/TerrainTextured");

            bool useTextured = terrainShader != null;

            if (!useTextured)
            {
                // Fallback to vertex-color-only shader
                terrainShader = Resources.Load<Shader>("Shaders/TerrainVertexColor");
                if (terrainShader == null)
                    terrainShader = Shader.Find("Orlo/TerrainVertexColor");
                if (terrainShader == null)
                    terrainShader = Shader.Find("Legacy Shaders/Diffuse");
            }

            _terrainMat = new Material(terrainShader);
            _terrainMat.color = Color.white;
            _terrainMat.SetFloat("_Smoothness", 0.05f);

            if (useTextured)
            {
                // Assign procedural textures to the shader
                _terrainMat.SetTexture("_GrassTex", TerrainTextures.GrassTex);
                _terrainMat.SetTexture("_RockTex", TerrainTextures.RockTex);
                _terrainMat.SetTexture("_DirtTex", TerrainTextures.DirtTex);
                _terrainMat.SetTexture("_SandTex", TerrainTextures.SandTex);

                _terrainMat.SetTexture("_GrassNorm", TerrainTextures.GrassNorm);
                _terrainMat.SetTexture("_RockNorm", TerrainTextures.RockNorm);
                _terrainMat.SetTexture("_DirtNorm", TerrainTextures.DirtNorm);
                _terrainMat.SetTexture("_SandNorm", TerrainTextures.SandNorm);

                _terrainMat.SetFloat("_TexScale", 0.25f);      // 4m repeat (more visible detail)
                _terrainMat.SetFloat("_NormalStrength", 1.0f);
                _terrainMat.SetFloat("_DetailNoiseStrength", 0.08f);

                Debug.Log("[TerrainManager] Using textured terrain shader with procedural PBR textures");
            }

            Debug.Log($"[TerrainManager] Initialized with shader: {terrainShader?.name ?? "NULL"}");
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

            // Rebuild adjacent chunks so their edges stitch to this new data
            RebuildNeighborEdges(coord);

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

            // Stitch edges with adjacent chunks to eliminate seams
            StitchEdges(coord, data);

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
        /// Get vertex color as splatmap weights for the textured shader.
        /// R=grass, G=rock, B=dirt, A=sand — weights that the shader uses to blend textures.
        /// When splatmap data is available, uses it directly. Otherwise derives from height/slope.
        /// </summary>
        private Color GetVertexColor(TerrainChunkData data, int idx, float height, int x, int z, int res, float step)
        {
            // Try splatmap first — pass raw weights to shader
            if (data.Splatmap != null && idx * 4 + 3 < data.Splatmap.Length)
            {
                float grass = data.Splatmap[idx * 4] / 255f;
                float rock = data.Splatmap[idx * 4 + 1] / 255f;
                float dirt = data.Splatmap[idx * 4 + 2] / 255f;
                float sand = data.Splatmap[idx * 4 + 3] / 255f;

                // Normalize so weights always sum to 1.0 (prevents dark blending)
                float total = grass + rock + dirt + sand;
                if (total < 0.001f)
                {
                    grass = 1f; // Default to grass
                }
                else if (Mathf.Abs(total - 1f) > 0.01f)
                {
                    grass /= total;
                    rock /= total;
                    dirt /= total;
                    sand /= total;
                }

                return new Color(grass, rock, dirt, sand);
            }

            // Fallback: derive splatmap weights from height and slope
            float slope = 0;
            if (x > 0 && x < res - 1 && z > 0 && z < res - 1)
            {
                float dx = data.Heightmap[z * res + (x + 1)] - data.Heightmap[z * res + (x - 1)];
                float dz = data.Heightmap[(z + 1) * res + x] - data.Heightmap[(z - 1) * res + x];
                slope = Mathf.Sqrt(dx * dx + dz * dz) / (2f * step);
            }

            // Per-vertex noise for natural transitions
            float worldPosX = x * step;
            float worldPosZ = z * step;
            float noiseVal = Mathf.PerlinNoise(worldPosX * 0.03f + data.Seed * 0.001f,
                                                worldPosZ * 0.03f + data.Seed * 0.002f);

            // Compute blend weights based on terrain properties
            float grassW = 0f, rockW = 0f, dirtW = 0f, sandW = 0f;

            // Steep slopes → rock
            rockW = Mathf.Clamp01((slope - 0.3f) * 2f);

            // Low areas → sand
            sandW = Mathf.Clamp01((1f - height) * 0.8f) * (1f - rockW);

            // High areas → dirt/rock mix
            float highBlend = Mathf.Clamp01((height - 30f) / 20f);
            dirtW = highBlend * 0.6f * (1f - rockW);

            // Everything else → grass with noise variation
            grassW = Mathf.Clamp01(1f - rockW - sandW - dirtW);

            // Add noise-driven dirt patches into grassy areas
            if (grassW > 0.3f && noiseVal > 0.6f)
            {
                float dirtPatch = (noiseVal - 0.6f) * 2f * 0.4f;
                dirtW += dirtPatch * grassW;
                grassW -= dirtPatch * grassW;
            }

            // Normalize
            float total = grassW + rockW + dirtW + sandW;
            if (total > 0.001f)
            {
                grassW /= total;
                rockW /= total;
                dirtW /= total;
                sandW /= total;
            }
            else
            {
                grassW = 1f;
            }

            return new Color(grassW, rockW, dirtW, sandW);
        }

        /// <summary>
        /// When a new chunk arrives, rebuild any visible neighbors so their edges
        /// re-stitch against the new data. Fixes seams when chunks load out of order.
        /// </summary>
        private void RebuildNeighborEdges(Vector2Int coord)
        {
            Vector2Int[] dirs = {
                new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
            };
            foreach (var d in dirs)
            {
                var neighbor = coord + d;
                if (_activeChunks.TryGetValue(neighbor, out var neighborGo) &&
                    _chunks.TryGetValue(neighbor, out var neighborData))
                {
                    Destroy(neighborGo);
                    _activeChunks[neighbor] = BuildTerrainMesh(neighbor, neighborData);
                }
            }
        }

        /// <summary>
        /// Stitch chunk edges with adjacent chunks by averaging heights at shared borders.
        /// This eliminates black seam artifacts caused by floating-point height mismatches.
        /// </summary>
        private void StitchEdges(Vector2Int coord, TerrainChunkData data)
        {
            int res = data.Resolution;

            // Check each of 4 neighbors
            Vector2Int[] neighbors = {
                new(coord.x - 1, coord.y), // left  (x=0 edge)
                new(coord.x + 1, coord.y), // right (x=res-1 edge)
                new(coord.x, coord.y - 1), // bottom (z=0 edge)
                new(coord.x, coord.y + 1), // top    (z=res-1 edge)
            };

            for (int n = 0; n < 4; n++)
            {
                if (!_chunks.TryGetValue(neighbors[n], out var neighbor)) continue;
                if (neighbor.Resolution != res) continue;

                for (int i = 0; i < res; i++)
                {
                    int myIdx, theirIdx;
                    switch (n)
                    {
                        case 0: // left neighbor: my x=0 matches their x=res-1
                            myIdx = i * res;
                            theirIdx = i * res + (res - 1);
                            break;
                        case 1: // right neighbor: my x=res-1 matches their x=0
                            myIdx = i * res + (res - 1);
                            theirIdx = i * res;
                            break;
                        case 2: // bottom neighbor: my z=0 matches their z=res-1
                            myIdx = i;
                            theirIdx = (res - 1) * res + i;
                            break;
                        default: // top neighbor: my z=res-1 matches their z=0
                            myIdx = (res - 1) * res + i;
                            theirIdx = i;
                            break;
                    }

                    // Average the heights at shared edges
                    float avg = (data.Heightmap[myIdx] + neighbor.Heightmap[theirIdx]) * 0.5f;
                    data.Heightmap[myIdx] = avg;
                }
            }
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
            // Estimate height from a neighbor chunk's edge to avoid Y=0 gap
            float avgHeight = EstimateHeightFromNeighbors(coord);

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = $"ChunkPlaceholder_{coord.x}_{coord.y}";
            go.transform.position = new Vector3(
                coord.x * chunkSize + chunkSize * 0.5f,
                avgHeight,
                coord.y * chunkSize + chunkSize * 0.5f
            );
            go.transform.localScale = new Vector3(chunkSize * 0.1f, 1, chunkSize * 0.1f);

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = _terrainMat;

            return go;
        }

        /// <summary>
        /// Sample a neighbor chunk's edge to get approximate height for placeholders.
        /// </summary>
        private float EstimateHeightFromNeighbors(Vector2Int coord)
        {
            Vector2Int[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
            foreach (var d in dirs)
            {
                if (_chunks.TryGetValue(coord + d, out var neighbor))
                {
                    // Sample center of the shared edge
                    int res = neighbor.Resolution;
                    int mid = res / 2;
                    int idx;
                    if (d.x == 1)       idx = mid * res;             // neighbor's left edge
                    else if (d.x == -1) idx = mid * res + (res - 1); // neighbor's right edge
                    else if (d.y == 1)  idx = mid;                   // neighbor's bottom edge
                    else                idx = (res - 1) * res + mid; // neighbor's top edge
                    return neighbor.Heightmap[idx];
                }
            }
            return 5f; // Default settlement height
        }

        private static float[] UnpackHeightmapF16(byte[] packed, int count)
        {
            // Server packs as signed 16-bit fixed-point (value * 8).
            // Unpack: read int16, divide by 8 to get float height.
            // This deterministic format eliminates chunk edge seams.
            var result = new float[count];
            for (int i = 0; i < count && i * 2 + 1 < packed.Length; i++)
            {
                short fixedVal = (short)(packed[i * 2] | (packed[i * 2 + 1] << 8));
                result[i] = fixedVal / 8f;
            }
            return result;
        }
    }
}

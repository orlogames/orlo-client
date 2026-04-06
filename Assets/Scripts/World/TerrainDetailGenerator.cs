using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Adds procedural surface detail (trees, grass, rocks) to terrain chunks.
    /// Uses chunk seeds for deterministic placement. Respects slope and height.
    /// </summary>
    public class TerrainDetailGenerator : MonoBehaviour
    {
        public static TerrainDetailGenerator Instance { get; private set; }

        [Header("Tree Settings")]
        [SerializeField] private int treesPerChunk = 30;
        [SerializeField] private float minTreeHeight = 4f;
        [SerializeField] private float maxTreeHeight = 10f;
        [SerializeField] private float maxTreeSlope = 0.7f;
        [SerializeField] private float minTreeElevation = 2f;
        [SerializeField] private float maxTreeElevation = 80f;

        [Header("Rock Settings")]
        [SerializeField] private int rocksPerChunk = 15;
        [SerializeField] private float minRockRadius = 0.3f;
        [SerializeField] private float maxRockRadius = 1.5f;
        [SerializeField] private float maxRockSlope = 0.95f;

        [Header("Grass Settings")]
        [SerializeField] private int grassPatchesPerChunk = 200;
        [SerializeField] private float grassBladeWidth = 0.08f;
        [SerializeField] private float grassBladeHeight = 0.4f;
        [SerializeField] private float maxGrassSlope = 0.6f;
        [SerializeField] private float minGrassElevation = 1f;
        [SerializeField] private float maxGrassElevation = 60f;

        // Per-chunk detail tracking
        private readonly Dictionary<Vector2Int, ChunkDetails> _chunkDetails = new();

        private Material _treeTrunkMaterial;
        private Material _treeCanopyMaterial;
        private Material _rockMaterial;
        private Material _grassMaterial;
        private Mesh _grassBladeMesh;

        private struct ChunkDetails
        {
            public List<GameObject> Trees;
            public List<GameObject> Rocks;
            public Matrix4x4[] GrassMatrices;
            public int GrassCount;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            InitializeMaterials();
        }

        private void InitializeMaterials()
        {
            // Use Standard shader with EntityFallback as safe fallback
            var standard = Shader.Find("Standard");
            if (standard == null)
            {
                standard = Resources.Load<Shader>("Shaders/EntityFallback");
                if (standard == null) standard = Shader.Find("Legacy Shaders/Diffuse");
            }

            _treeTrunkMaterial = new Material(standard);
            _treeTrunkMaterial.color = new Color(0.35f, 0.22f, 0.1f);
            _treeTrunkMaterial.SetFloat("_Glossiness", 0.1f);

            _treeCanopyMaterial = new Material(standard);
            _treeCanopyMaterial.color = new Color(0.15f, 0.45f, 0.12f);
            _treeCanopyMaterial.SetFloat("_Glossiness", 0.05f);

            _rockMaterial = new Material(standard);
            _rockMaterial.color = new Color(0.5f, 0.48f, 0.45f);
            _rockMaterial.SetFloat("_Glossiness", 0.2f);

            _grassMaterial = new Material(standard);
            _grassMaterial.color = new Color(0.2f, 0.55f, 0.15f);
            _grassMaterial.EnableKeyword("_ALPHATEST_ON");
            _grassMaterial.SetFloat("_Mode", 1f); // Cutout
            _grassMaterial.renderQueue = 2450;
            _grassMaterial.enableInstancing = true;

            _grassBladeMesh = ProceduralMeshBuilder.BuildQuad(grassBladeWidth, grassBladeHeight);
        }

        private void Update()
        {
            // Render grass via GPU instancing each frame
            foreach (var kv in _chunkDetails)
            {
                var details = kv.Value;
                if (details.GrassMatrices != null && details.GrassCount > 0)
                {
                    int remaining = details.GrassCount;
                    int offset = 0;
                    while (remaining > 0)
                    {
                        int batch = Mathf.Min(remaining, 1023);
                        var batchMatrices = new Matrix4x4[batch];
                        System.Array.Copy(details.GrassMatrices, offset, batchMatrices, 0, batch);
                        Graphics.DrawMeshInstanced(_grassBladeMesh, 0, _grassMaterial, batchMatrices);
                        offset += batch;
                        remaining -= batch;
                    }
                }
            }
        }

        /// <summary>
        /// Generate detail objects for a terrain chunk.
        /// </summary>
        /// <param name="coord">Chunk coordinate</param>
        /// <param name="chunkWorldPos">World-space origin of chunk</param>
        /// <param name="chunkSize">Size of chunk in world units</param>
        /// <param name="heightmap">Flat heightmap array</param>
        /// <param name="resolution">Heightmap resolution per edge</param>
        /// <param name="seed">Chunk seed for determinism</param>
        public void GenerateDetails(Vector2Int coord, Vector3 chunkWorldPos, float chunkSize,
            float[] heightmap, int resolution, ulong seed)
        {
            // Clean up old details if regenerating
            RemoveDetails(coord);

            var rng = new System.Random((int)(seed & 0x7FFFFFFF));
            float step = chunkSize / (resolution - 1);

            var details = new ChunkDetails
            {
                Trees = new List<GameObject>(),
                Rocks = new List<GameObject>()
            };

            // --- Trees ---
            for (int i = 0; i < treesPerChunk; i++)
            {
                float lx = (float)rng.NextDouble() * chunkSize;
                float lz = (float)rng.NextDouble() * chunkSize;
                int gx = Mathf.Clamp(Mathf.FloorToInt(lx / step), 0, resolution - 2);
                int gz = Mathf.Clamp(Mathf.FloorToInt(lz / step), 0, resolution - 2);

                float height = SampleHeight(heightmap, resolution, lx / step, lz / step);
                float slope = SampleSlope(heightmap, resolution, gx, gz, step);

                if (slope > maxTreeSlope) continue;
                if (height < minTreeElevation || height > maxTreeElevation) continue;

                Vector3 worldPos = chunkWorldPos + new Vector3(lx, height, lz);
                float treeHeight = Mathf.Lerp(minTreeHeight, maxTreeHeight, (float)rng.NextDouble());
                int archetype = rng.Next(3); // 0=pine, 1=oak, 2=palm
                float yRot = (float)rng.NextDouble() * 360f;

                // Try loading real GLB model first, fall back to procedural
                var tree = TryLoadTreeModel(worldPos, treeHeight, archetype, yRot);
                if (tree == null)
                    tree = BuildTree(worldPos, treeHeight, archetype, rng);
                details.Trees.Add(tree);
            }

            // --- Rocks ---
            for (int i = 0; i < rocksPerChunk; i++)
            {
                float lx = (float)rng.NextDouble() * chunkSize;
                float lz = (float)rng.NextDouble() * chunkSize;

                float height = SampleHeight(heightmap, resolution, lx / step, lz / step);
                int gx = Mathf.Clamp(Mathf.FloorToInt(lx / step), 0, resolution - 2);
                int gz = Mathf.Clamp(Mathf.FloorToInt(lz / step), 0, resolution - 2);
                float slope = SampleSlope(heightmap, resolution, gx, gz, step);

                if (slope > maxRockSlope) continue;

                Vector3 worldPos = chunkWorldPos + new Vector3(lx, height, lz);
                float radius = Mathf.Lerp(minRockRadius, maxRockRadius, (float)rng.NextDouble());

                var rock = BuildRock(worldPos, radius, rng);
                details.Rocks.Add(rock);
            }

            // --- Grass (GPU instanced, no GameObjects) ---
            var grassList = new List<Matrix4x4>();
            for (int i = 0; i < grassPatchesPerChunk; i++)
            {
                float lx = (float)rng.NextDouble() * chunkSize;
                float lz = (float)rng.NextDouble() * chunkSize;

                float height = SampleHeight(heightmap, resolution, lx / step, lz / step);
                int gx = Mathf.Clamp(Mathf.FloorToInt(lx / step), 0, resolution - 2);
                int gz = Mathf.Clamp(Mathf.FloorToInt(lz / step), 0, resolution - 2);
                float slope = SampleSlope(heightmap, resolution, gx, gz, step);

                if (slope > maxGrassSlope) continue;
                if (height < minGrassElevation || height > maxGrassElevation) continue;

                Vector3 worldPos = chunkWorldPos + new Vector3(lx, height, lz);
                float yRot = (float)rng.NextDouble() * 360f;
                float scaleY = 0.7f + (float)rng.NextDouble() * 0.6f;

                // Two cross-blades per placement for volume
                var mat1 = Matrix4x4.TRS(worldPos,
                    Quaternion.Euler(0, yRot, 0),
                    new Vector3(1f, scaleY, 1f));
                var mat2 = Matrix4x4.TRS(worldPos,
                    Quaternion.Euler(0, yRot + 90f, 0),
                    new Vector3(1f, scaleY, 1f));

                grassList.Add(mat1);
                grassList.Add(mat2);
            }

            details.GrassMatrices = grassList.ToArray();
            details.GrassCount = grassList.Count;

            _chunkDetails[coord] = details;
        }

        /// <summary>
        /// Remove all detail objects for a chunk.
        /// </summary>
        public void RemoveDetails(Vector2Int coord)
        {
            if (!_chunkDetails.TryGetValue(coord, out var details)) return;

            if (details.Trees != null)
                foreach (var t in details.Trees)
                    if (t != null) Destroy(t);

            if (details.Rocks != null)
                foreach (var r in details.Rocks)
                    if (r != null) Destroy(r);

            _chunkDetails.Remove(coord);
        }

        // Map archetype to GLB asset IDs
        private static readonly string[][] TreeAssetIds = new[]
        {
            new[] { "tree_frontier_pine", "settlement_pine" },           // pine
            new[] { "tree_frontier_broadleaf" },                         // oak/broadleaf
            new[] { "tree_frontier_pine", "tree_frontier_broadleaf" },   // palm (fallback to pine/broadleaf)
        };

        private GameObject TryLoadTreeModel(Vector3 position, float height, int archetype, float yRotation)
        {
            if (AssetLoader.Instance == null) return null;

            var candidates = TreeAssetIds[Mathf.Clamp(archetype, 0, TreeAssetIds.Length - 1)];
            foreach (var assetId in candidates)
            {
                var go = AssetLoader.Instance.TryLoadModel(assetId);
                if (go != null)
                {
                    go.transform.position = position;
                    go.transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    // Scale to match desired tree height (models are ~2-3m, we want 4-10m)
                    float modelHeight = GetModelHeight(go);
                    if (modelHeight > 0.1f)
                    {
                        float scale = height / modelHeight;
                        go.transform.localScale = Vector3.one * scale;
                    }
                    go.name = $"Tree_{assetId}";
                    return go;
                }
            }
            return null;
        }

        private static float GetModelHeight(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 2f;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.size.y;
        }

        private GameObject BuildTree(Vector3 position, float height, int archetype, System.Random rng)
        {
            var parent = new GameObject("Tree");
            parent.transform.position = position;

            float trunkRadius = height * 0.05f;
            float trunkHeight = height * 0.6f;

            Mesh trunkMesh;
            Mesh canopyMesh;

            switch (archetype)
            {
                case 0: // Pine — tall narrow cone
                    trunkMesh = ProceduralMeshBuilder.BuildCylinder(trunkRadius * 0.5f, trunkRadius, trunkHeight, 6);
                    canopyMesh = ProceduralMeshBuilder.BuildCone(height * 0.25f, height * 0.5f, 8);

                    AddMeshChild(parent, "Trunk", trunkMesh, _treeTrunkMaterial, Vector3.zero);
                    AddMeshChild(parent, "Canopy", canopyMesh, _treeCanopyMaterial,
                        new Vector3(0, trunkHeight * 0.7f, 0));
                    break;

                case 1: // Oak — wide sphere cluster canopy
                    trunkMesh = ProceduralMeshBuilder.BuildCylinder(trunkRadius * 0.7f, trunkRadius, trunkHeight * 0.8f, 8);
                    AddMeshChild(parent, "Trunk", trunkMesh, _treeTrunkMaterial, Vector3.zero);

                    // Three overlapping spheres for canopy volume
                    float canopyR = height * 0.25f;
                    float canopyBase = trunkHeight * 0.65f;
                    var s1 = ProceduralMeshBuilder.BuildSphere(canopyR, 6, 8);
                    var s2 = ProceduralMeshBuilder.BuildSphere(canopyR * 0.85f, 6, 8);
                    var s3 = ProceduralMeshBuilder.BuildSphere(canopyR * 0.9f, 6, 8);

                    AddMeshChild(parent, "Canopy1", s1, _treeCanopyMaterial,
                        new Vector3(0, canopyBase + canopyR * 0.5f, 0));
                    AddMeshChild(parent, "Canopy2", s2, _treeCanopyMaterial,
                        new Vector3(-canopyR * 0.4f, canopyBase + canopyR * 0.2f, canopyR * 0.3f));
                    AddMeshChild(parent, "Canopy3", s3, _treeCanopyMaterial,
                        new Vector3(canopyR * 0.3f, canopyBase + canopyR * 0.3f, -canopyR * 0.2f));
                    break;

                case 2: // Palm — curved trunk with frond sphere
                    // Slightly tilted trunk
                    float tilt = 5f + (float)rng.NextDouble() * 10f;
                    trunkMesh = ProceduralMeshBuilder.BuildCylinder(trunkRadius * 0.4f, trunkRadius, trunkHeight, 6);
                    var trunkGo = AddMeshChild(parent, "Trunk", trunkMesh, _treeTrunkMaterial, Vector3.zero);
                    trunkGo.transform.localRotation = Quaternion.Euler(0, 0, tilt);

                    // Top frond cluster
                    float frondR = height * 0.2f;
                    var frondMesh = ProceduralMeshBuilder.BuildSphere(frondR, 5, 6);
                    float topX = Mathf.Sin(tilt * Mathf.Deg2Rad) * trunkHeight;
                    float topY = Mathf.Cos(tilt * Mathf.Deg2Rad) * trunkHeight;
                    AddMeshChild(parent, "Fronds", frondMesh, _treeCanopyMaterial,
                        new Vector3(topX, topY, 0));
                    break;
            }

            return parent;
        }

        private GameObject BuildRock(Vector3 position, float radius, System.Random rng)
        {
            // Build a jittered UV sphere for an organic rock shape
            var mesh = ProceduralMeshBuilder.BuildSphere(radius, 5, 6);
            var verts = mesh.vertices;

            float jitter = radius * 0.3f;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] += new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * jitter,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * jitter * 0.5f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * jitter
                );
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Rock");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * 20f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 15f);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = _rockMaterial;

            return go;
        }

        private GameObject AddMeshChild(GameObject parent, string name, Mesh mesh,
            Material material, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = localPosition;

            var mf = child.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            var mr = child.AddComponent<MeshRenderer>();
            mr.material = material;

            return child;
        }

        private float SampleHeight(float[] heightmap, int resolution, float fx, float fz)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, resolution - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, resolution - 2);
            float tx = fx - x0;
            float tz = fz - z0;

            float h00 = heightmap[z0 * resolution + x0];
            float h10 = heightmap[z0 * resolution + x0 + 1];
            float h01 = heightmap[(z0 + 1) * resolution + x0];
            float h11 = heightmap[(z0 + 1) * resolution + x0 + 1];

            return Mathf.Lerp(
                Mathf.Lerp(h00, h10, tx),
                Mathf.Lerp(h01, h11, tx),
                tz);
        }

        private float SampleSlope(float[] heightmap, int resolution, int gx, int gz, float step)
        {
            float hC = heightmap[gz * resolution + gx];
            float hR = gx < resolution - 1 ? heightmap[gz * resolution + gx + 1] : hC;
            float hU = gz < resolution - 1 ? heightmap[(gz + 1) * resolution + gx] : hC;

            float dx = (hR - hC) / step;
            float dz = (hU - hC) / step;

            // Slope as the dot product with up = 1 - normalized gradient magnitude
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}

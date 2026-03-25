using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// GPU-instanced vegetation rendering. Grass uses Graphics.DrawMeshInstanced
    /// with no GameObjects. Trees use 3 archetypes with LOD by distance.
    /// Wind is driven via Shader.SetGlobalFloat.
    /// </summary>
    public class ProceduralVegetation : MonoBehaviour
    {
        public static ProceduralVegetation Instance { get; private set; }

        [Header("Grass")]
        [SerializeField] private float grassViewDistance = 80f;
        [SerializeField] private float grassDensity = 1.5f;
        [SerializeField] private float grassBladeWidth = 0.06f;
        [SerializeField] private float grassBladeMinHeight = 0.25f;
        [SerializeField] private float grassBladeMaxHeight = 0.5f;

        [Header("Trees")]
        [SerializeField] private float treeViewDistance = 300f;
        [SerializeField] private float treeLODDistance = 120f;

        [Header("Wind")]
        [SerializeField] private float windSpeed = 1.2f;
        [SerializeField] private float windStrength = 0.15f;

        // Cached meshes
        private Mesh _grassBladeMesh;
        private Mesh _pineFullMesh;
        private Mesh _pineLODMesh;
        private Mesh _oakFullMesh;
        private Mesh _oakLODMesh;
        private Mesh _palmFullMesh;
        private Mesh _palmLODMesh;

        // Materials
        private Material _grassMaterial;
        private Material _treeTrunkMaterial;
        private Material _pineCanopyMaterial;
        private Material _oakCanopyMaterial;
        private Material _palmCanopyMaterial;

        // Per-chunk registration
        private readonly Dictionary<Vector2Int, ChunkVegetation> _chunks = new();

        public struct GrassPatch
        {
            public Vector3 Position;
            public float Rotation;
            public float Scale;
        }

        public enum TreeArchetype { Pine, Oak, Palm }

        public struct TreeInstance
        {
            public Vector3 Position;
            public float Height;
            public float YRotation;
            public TreeArchetype Archetype;
        }

        private struct ChunkVegetation
        {
            public Matrix4x4[] GrassMatrices;
            public int GrassCount;
            public List<TreeInstance> Trees;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            BuildMeshes();
            BuildMaterials();
        }

        private void BuildMeshes()
        {
            _grassBladeMesh = ProceduralMeshBuilder.BuildQuad(grassBladeWidth, grassBladeMinHeight);

            // --- Pine full: trunk cylinder + cone canopy merged ---
            {
                var trunk = ProceduralMeshBuilder.BuildCylinder(0.05f, 0.12f, 3f, 6);
                var canopy = ProceduralMeshBuilder.BuildCone(1.2f, 3.5f, 8);
                _pineFullMesh = ProceduralMeshBuilder.MergeMeshes(new[]
                {
                    (trunk, Matrix4x4.identity),
                    (canopy, Matrix4x4.Translate(new Vector3(0, 2.2f, 0)))
                });
            }
            // Pine LOD: just a cone
            _pineLODMesh = ProceduralMeshBuilder.BuildCone(1.0f, 5f, 4);

            // --- Oak full: trunk + 3 spheres ---
            {
                var trunk = ProceduralMeshBuilder.BuildCylinder(0.08f, 0.15f, 2.5f, 6);
                var s1 = ProceduralMeshBuilder.BuildSphere(1.5f, 5, 6);
                var s2 = ProceduralMeshBuilder.BuildSphere(1.2f, 5, 6);
                var s3 = ProceduralMeshBuilder.BuildSphere(1.3f, 5, 6);
                _oakFullMesh = ProceduralMeshBuilder.MergeMeshes(new[]
                {
                    (trunk, Matrix4x4.identity),
                    (s1, Matrix4x4.Translate(new Vector3(0, 3.5f, 0))),
                    (s2, Matrix4x4.Translate(new Vector3(-0.6f, 3.0f, 0.5f))),
                    (s3, Matrix4x4.Translate(new Vector3(0.5f, 3.2f, -0.3f)))
                });
            }
            // Oak LOD: single sphere
            _oakLODMesh = ProceduralMeshBuilder.BuildSphere(2f, 4, 5);

            // --- Palm full: trunk + top sphere ---
            {
                var trunk = ProceduralMeshBuilder.BuildCylinder(0.06f, 0.12f, 4f, 5);
                var fronds = ProceduralMeshBuilder.BuildSphere(1.0f, 4, 5);
                _palmFullMesh = ProceduralMeshBuilder.MergeMeshes(new[]
                {
                    (trunk, Matrix4x4.identity),
                    (fronds, Matrix4x4.Translate(new Vector3(0.3f, 4.2f, 0)))
                });
            }
            // Palm LOD: thin cone
            _palmLODMesh = ProceduralMeshBuilder.BuildCone(0.6f, 5f, 4);
        }

        private void BuildMaterials()
        {
            var standard = Shader.Find("Standard");

            _grassMaterial = new Material(standard);
            _grassMaterial.color = new Color(0.22f, 0.52f, 0.14f);
            _grassMaterial.enableInstancing = true;
            _grassMaterial.EnableKeyword("_ALPHATEST_ON");
            _grassMaterial.SetFloat("_Mode", 1f);
            _grassMaterial.renderQueue = 2450;

            _treeTrunkMaterial = new Material(standard);
            _treeTrunkMaterial.color = new Color(0.35f, 0.22f, 0.1f);
            _treeTrunkMaterial.enableInstancing = true;

            _pineCanopyMaterial = new Material(standard);
            _pineCanopyMaterial.color = new Color(0.1f, 0.35f, 0.08f);
            _pineCanopyMaterial.enableInstancing = true;

            _oakCanopyMaterial = new Material(standard);
            _oakCanopyMaterial.color = new Color(0.15f, 0.45f, 0.12f);
            _oakCanopyMaterial.enableInstancing = true;

            _palmCanopyMaterial = new Material(standard);
            _palmCanopyMaterial.color = new Color(0.18f, 0.5f, 0.15f);
            _palmCanopyMaterial.enableInstancing = true;
        }

        private void Update()
        {
            // Update global wind time for shaders
            Shader.SetGlobalFloat("_WindTime", Time.time * windSpeed);
            Shader.SetGlobalFloat("_WindStrength", windStrength);

            var cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;

            // Render grass via GPU instancing
            foreach (var kv in _chunks)
            {
                var chunk = kv.Value;
                if (chunk.GrassMatrices == null || chunk.GrassCount == 0) continue;

                int remaining = chunk.GrassCount;
                int offset = 0;
                while (remaining > 0)
                {
                    int batch = Mathf.Min(remaining, 1023);
                    var batchMatrices = new Matrix4x4[batch];
                    System.Array.Copy(chunk.GrassMatrices, offset, batchMatrices, 0, batch);
                    Graphics.DrawMeshInstanced(_grassBladeMesh, 0, _grassMaterial, batchMatrices);
                    offset += batch;
                    remaining -= batch;
                }

                // Render trees as instanced meshes
                if (chunk.Trees != null)
                {
                    RenderTreeInstances(chunk.Trees, camPos);
                }
            }
        }

        private void RenderTreeInstances(List<TreeInstance> trees, Vector3 camPos)
        {
            // Batch by archetype and LOD
            var pineFull = new List<Matrix4x4>();
            var pineLOD = new List<Matrix4x4>();
            var oakFull = new List<Matrix4x4>();
            var oakLOD = new List<Matrix4x4>();
            var palmFull = new List<Matrix4x4>();
            var palmLOD = new List<Matrix4x4>();

            foreach (var tree in trees)
            {
                float dist = Vector3.Distance(camPos, tree.Position);
                if (dist > treeViewDistance) continue;

                float scale = tree.Height / 6f; // Normalize to base mesh height ~6
                var mat = Matrix4x4.TRS(tree.Position,
                    Quaternion.Euler(0, tree.YRotation, 0),
                    Vector3.one * scale);

                bool useLOD = dist > treeLODDistance;

                switch (tree.Archetype)
                {
                    case TreeArchetype.Pine:
                        (useLOD ? pineLOD : pineFull).Add(mat);
                        break;
                    case TreeArchetype.Oak:
                        (useLOD ? oakLOD : oakFull).Add(mat);
                        break;
                    case TreeArchetype.Palm:
                        (useLOD ? palmLOD : palmFull).Add(mat);
                        break;
                }
            }

            DrawInstancedBatches(_pineFullMesh, _pineCanopyMaterial, pineFull);
            DrawInstancedBatches(_pineLODMesh, _pineCanopyMaterial, pineLOD);
            DrawInstancedBatches(_oakFullMesh, _oakCanopyMaterial, oakFull);
            DrawInstancedBatches(_oakLODMesh, _oakCanopyMaterial, oakLOD);
            DrawInstancedBatches(_palmFullMesh, _palmCanopyMaterial, palmFull);
            DrawInstancedBatches(_palmLODMesh, _palmCanopyMaterial, palmLOD);
        }

        private void DrawInstancedBatches(Mesh mesh, Material material, List<Matrix4x4> matrices)
        {
            if (matrices.Count == 0) return;

            int remaining = matrices.Count;
            int offset = 0;
            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, 1023);
                var arr = new Matrix4x4[batch];
                matrices.CopyTo(offset, arr, 0, batch);
                Graphics.DrawMeshInstanced(mesh, 0, material, arr);
                offset += batch;
                remaining -= batch;
            }
        }

        /// <summary>
        /// Register grass patches for a chunk. Called by ChunkStreamer or TerrainDetailGenerator.
        /// </summary>
        public void RegisterGrass(Vector2Int coord, List<GrassPatch> patches)
        {
            if (!_chunks.TryGetValue(coord, out var data))
            {
                data = new ChunkVegetation { Trees = new List<TreeInstance>() };
            }

            var matrices = new List<Matrix4x4>();
            foreach (var patch in patches)
            {
                float h = Mathf.Lerp(grassBladeMinHeight, grassBladeMaxHeight, patch.Scale);
                var mat1 = Matrix4x4.TRS(patch.Position,
                    Quaternion.Euler(0, patch.Rotation, 0),
                    new Vector3(1f, h / grassBladeMinHeight, 1f));
                var mat2 = Matrix4x4.TRS(patch.Position,
                    Quaternion.Euler(0, patch.Rotation + 90f, 0),
                    new Vector3(1f, h / grassBladeMinHeight, 1f));
                matrices.Add(mat1);
                matrices.Add(mat2);
            }

            data.GrassMatrices = matrices.ToArray();
            data.GrassCount = matrices.Count;
            _chunks[coord] = data;
        }

        /// <summary>
        /// Register tree instances for a chunk.
        /// </summary>
        public void RegisterTrees(Vector2Int coord, List<TreeInstance> trees)
        {
            if (!_chunks.TryGetValue(coord, out var data))
            {
                data = new ChunkVegetation { GrassMatrices = null, GrassCount = 0 };
            }
            data.Trees = trees;
            _chunks[coord] = data;
        }

        /// <summary>
        /// Remove all vegetation for a chunk.
        /// </summary>
        public void UnregisterChunk(Vector2Int coord)
        {
            _chunks.Remove(coord);
        }
    }
}

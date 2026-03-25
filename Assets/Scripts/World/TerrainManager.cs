using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Manages procedural terrain chunks streamed from the server.
    /// Chunks load/unload based on player proximity (expand-on-explore model).
    /// </summary>
    public class TerrainManager : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 256;    // World units per chunk
        [SerializeField] private int viewDistance = 3;     // Chunks in each direction
        [SerializeField] private Material terrainMaterial;

        private readonly Dictionary<Vector2Int, GameObject> _activeChunks = new();
        private Vector2Int _lastPlayerChunk;

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

            // Request new chunks from server
            foreach (var coord in needed)
            {
                if (!_activeChunks.ContainsKey(coord))
                {
                    RequestChunk(coord);
                }
            }
        }

        private void RequestChunk(Vector2Int coord)
        {
            // TODO: Send terrain chunk request to server
            // For now, generate a flat placeholder
            var chunk = CreatePlaceholderChunk(coord);
            _activeChunks[coord] = chunk;
        }

        private GameObject CreatePlaceholderChunk(Vector2Int coord)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = $"Chunk_{coord.x}_{coord.y}";
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
        /// Called when server sends terrain heightmap data.
        /// </summary>
        public void ApplyTerrainChunk(Vector2Int coord, int resolution, byte[] heightmap, ulong seed)
        {
            // TODO: Generate mesh from heightmap data, apply to chunk
            Debug.Log($"[Terrain] Received chunk {coord} ({resolution}x{resolution}, seed={seed})");
        }
    }
}

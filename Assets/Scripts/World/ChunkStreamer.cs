using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Background terrain chunk loading/unloading.
    /// Works alongside TerrainManager to process chunk load queues via coroutine
    /// and trigger TerrainDetailGenerator for surface detail.
    /// </summary>
    public class ChunkStreamer : MonoBehaviour
    {
        public static ChunkStreamer Instance { get; private set; }

        [Header("Streaming")]
        [SerializeField] private int viewDistance = 4;
        [SerializeField] private int chunkSize = 64;
        [SerializeField] private int chunksPerFrame = 2;
        [SerializeField] private float unloadDelay = 2f;

        private readonly HashSet<Vector2Int> _loadedChunks = new();
        private readonly Queue<Vector2Int> _loadQueue = new();
        private readonly Queue<(Vector2Int coord, float time)> _unloadQueue = new();
        private readonly HashSet<Vector2Int> _pendingLoad = new();

        private Vector2Int _lastPlayerChunk;
        private Transform _playerTransform;
        private Coroutine _loadCoroutine;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _loadCoroutine = StartCoroutine(ProcessLoadQueue());
            StartCoroutine(ProcessUnloadQueue());
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    _playerTransform = player.transform;
                else
                    return;
            }

            var pos = _playerTransform.position;
            var currentChunk = new Vector2Int(
                Mathf.FloorToInt(pos.x / chunkSize),
                Mathf.FloorToInt(pos.z / chunkSize));

            if (currentChunk != _lastPlayerChunk)
            {
                _lastPlayerChunk = currentChunk;
                UpdateChunkQueues(currentChunk);
            }
        }

        private void UpdateChunkQueues(Vector2Int center)
        {
            var needed = new HashSet<Vector2Int>();

            // Build sorted list by distance to center for priority loading
            var candidates = new List<(Vector2Int coord, float dist)>();

            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                for (int z = -viewDistance; z <= viewDistance; z++)
                {
                    var coord = center + new Vector2Int(x, z);
                    needed.Add(coord);

                    if (!_loadedChunks.Contains(coord) && !_pendingLoad.Contains(coord))
                    {
                        float dist = Mathf.Sqrt(x * x + z * z);
                        candidates.Add((coord, dist));
                    }
                }
            }

            // Sort by distance — load nearest first
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var (coord, _) in candidates)
            {
                _loadQueue.Enqueue(coord);
                _pendingLoad.Add(coord);
            }

            // Queue unloads for chunks no longer needed
            foreach (var loaded in _loadedChunks)
            {
                if (!needed.Contains(loaded))
                {
                    _unloadQueue.Enqueue((loaded, Time.time + unloadDelay));
                }
            }
        }

        private IEnumerator ProcessLoadQueue()
        {
            while (true)
            {
                int processed = 0;

                while (_loadQueue.Count > 0 && processed < chunksPerFrame)
                {
                    var coord = _loadQueue.Dequeue();
                    _pendingLoad.Remove(coord);

                    if (_loadedChunks.Contains(coord)) continue;

                    LoadChunk(coord);
                    _loadedChunks.Add(coord);
                    processed++;
                }

                yield return null;
            }
        }

        private IEnumerator ProcessUnloadQueue()
        {
            while (true)
            {
                while (_unloadQueue.Count > 0)
                {
                    var (coord, time) = _unloadQueue.Peek();
                    if (Time.time < time)
                        break;

                    _unloadQueue.Dequeue();

                    // Verify still outside view distance before unloading
                    if (_playerTransform != null)
                    {
                        var pos = _playerTransform.position;
                        var currentChunk = new Vector2Int(
                            Mathf.FloorToInt(pos.x / chunkSize),
                            Mathf.FloorToInt(pos.z / chunkSize));

                        int dx = Mathf.Abs(coord.x - currentChunk.x);
                        int dz = Mathf.Abs(coord.y - currentChunk.y);

                        if (dx <= viewDistance && dz <= viewDistance)
                            continue; // Player moved back, don't unload
                    }

                    UnloadChunk(coord);
                    _loadedChunks.Remove(coord);
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        private void LoadChunk(Vector2Int coord)
        {
            // TerrainManager handles mesh creation; we trigger detail generation
            var terrainManager = FindFirstObjectByType<TerrainManager>();
            if (terrainManager == null) return;

            // Request server data for chunk if not already cached
            // The TerrainManager already creates placeholder chunks
            // We trigger detail generation once the chunk has heightmap data
            TriggerDetailGeneration(coord);

            Debug.Log($"[ChunkStreamer] Loaded chunk {coord}");
        }

        private void UnloadChunk(Vector2Int coord)
        {
            // Remove terrain details
            var detailGen = TerrainDetailGenerator.Instance;
            if (detailGen != null)
                detailGen.RemoveDetails(coord);

            // Remove vegetation
            var vegetation = ProceduralVegetation.Instance;
            if (vegetation != null)
                vegetation.UnregisterChunk(coord);

            Debug.Log($"[ChunkStreamer] Unloaded chunk {coord}");
        }

        /// <summary>
        /// Called when TerrainManager receives heightmap data from the server.
        /// Triggers detail generation for the chunk.
        /// </summary>
        public void OnChunkDataReceived(Vector2Int coord, float[] heightmap, int resolution, ulong seed)
        {
            if (!_loadedChunks.Contains(coord)) return;

            Vector3 chunkWorldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

            var detailGen = TerrainDetailGenerator.Instance;
            if (detailGen != null)
            {
                detailGen.GenerateDetails(coord, chunkWorldPos, chunkSize, heightmap, resolution, seed);
            }
        }

        private void TriggerDetailGeneration(Vector2Int coord)
        {
            // Detail generation will be triggered when heightmap data arrives
            // via OnChunkDataReceived. This is a placeholder for local/offline terrain.
            Vector3 chunkWorldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);

            // For standalone testing without server, generate flat placeholder details
            var detailGen = TerrainDetailGenerator.Instance;
            if (detailGen != null)
            {
                // Only generate if we have actual heightmap data would be handled by OnChunkDataReceived
                // This path is for locally-generated terrain
            }
        }

        /// <summary>
        /// Check if a chunk coordinate is currently loaded.
        /// </summary>
        public bool IsChunkLoaded(Vector2Int coord) => _loadedChunks.Contains(coord);

        /// <summary>
        /// Get the current view distance in chunks.
        /// </summary>
        public int GetViewDistance() => viewDistance;

        /// <summary>
        /// Set view distance dynamically (e.g., from quality settings).
        /// </summary>
        public void SetViewDistance(int distance)
        {
            viewDistance = Mathf.Clamp(distance, 1, 16);
        }

        private void OnDestroy()
        {
            if (_loadCoroutine != null)
                StopCoroutine(_loadCoroutine);
        }
    }
}

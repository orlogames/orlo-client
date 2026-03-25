using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Level of detail switching based on camera distance.
    /// Register objects with multiple LOD meshes; the manager switches
    /// the active mesh renderer each frame based on configurable thresholds.
    /// </summary>
    public class LODManager : MonoBehaviour
    {
        public static LODManager Instance { get; private set; }

        [Header("Distance Thresholds")]
        [SerializeField] private float lod0Distance = 50f;
        [SerializeField] private float lod1Distance = 150f;
        [SerializeField] private float lod2Distance = 300f;

        private readonly List<LODEntry> _entries = new();
        private readonly Dictionary<int, int> _idToIndex = new();
        private int _nextId;

        private struct LODEntry
        {
            public int Id;
            public Transform Target;
            public MeshFilter MeshFilter;
            public MeshRenderer Renderer;
            public Mesh[] LODMeshes;   // index 0 = highest detail, up to 3
            public Material[] LODMaterials; // optional per-LOD materials (null = keep same)
            public int CurrentLOD;
            public bool Active;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!entry.Active || entry.Target == null)
                {
                    // Mark for cleanup
                    if (entry.Target == null && entry.Active)
                    {
                        entry.Active = false;
                        _entries[i] = entry;
                    }
                    continue;
                }

                float dist = Vector3.Distance(camPos, entry.Target.position);
                int desiredLOD = GetLODLevel(dist);

                // Clamp to available mesh count
                int maxLOD = entry.LODMeshes.Length - 1;
                desiredLOD = Mathf.Min(desiredLOD, maxLOD);

                if (desiredLOD != entry.CurrentLOD)
                {
                    entry.CurrentLOD = desiredLOD;

                    if (entry.MeshFilter != null && entry.LODMeshes[desiredLOD] != null)
                    {
                        entry.MeshFilter.mesh = entry.LODMeshes[desiredLOD];
                    }

                    if (entry.LODMaterials != null &&
                        desiredLOD < entry.LODMaterials.Length &&
                        entry.LODMaterials[desiredLOD] != null &&
                        entry.Renderer != null)
                    {
                        entry.Renderer.material = entry.LODMaterials[desiredLOD];
                    }

                    // Hide if beyond max distance
                    if (entry.Renderer != null)
                    {
                        entry.Renderer.enabled = dist <= lod2Distance * 1.2f;
                    }

                    _entries[i] = entry;
                }
            }
        }

        /// <summary>
        /// Register a GameObject for LOD management.
        /// </summary>
        /// <param name="target">The transform to measure distance from</param>
        /// <param name="lodMeshes">Array of meshes from highest to lowest detail (1-4 entries)</param>
        /// <param name="lodMaterials">Optional per-LOD materials (can be null)</param>
        /// <returns>Registration ID for later unregistration</returns>
        public int Register(Transform target, Mesh[] lodMeshes, Material[] lodMaterials = null)
        {
            if (target == null || lodMeshes == null || lodMeshes.Length == 0)
                return -1;

            var mf = target.GetComponent<MeshFilter>();
            var mr = target.GetComponent<MeshRenderer>();

            if (mf == null)
                mf = target.gameObject.AddComponent<MeshFilter>();
            if (mr == null)
                mr = target.gameObject.AddComponent<MeshRenderer>();

            int id = _nextId++;

            var entry = new LODEntry
            {
                Id = id,
                Target = target,
                MeshFilter = mf,
                Renderer = mr,
                LODMeshes = lodMeshes,
                LODMaterials = lodMaterials,
                CurrentLOD = 0,
                Active = true
            };

            // Set initial mesh
            mf.mesh = lodMeshes[0];

            _idToIndex[id] = _entries.Count;
            _entries.Add(entry);

            return id;
        }

        /// <summary>
        /// Unregister an object from LOD management.
        /// </summary>
        public void Unregister(int id)
        {
            if (!_idToIndex.TryGetValue(id, out int index)) return;

            var entry = _entries[index];
            entry.Active = false;
            _entries[index] = entry;
            _idToIndex.Remove(id);
        }

        /// <summary>
        /// Set custom distance thresholds.
        /// </summary>
        public void SetThresholds(float lod0, float lod1, float lod2)
        {
            lod0Distance = lod0;
            lod1Distance = lod1;
            lod2Distance = lod2;
        }

        /// <summary>
        /// Get LOD distances for external use (e.g., vegetation system).
        /// </summary>
        public float GetLOD0Distance() => lod0Distance;
        public float GetLOD1Distance() => lod1Distance;
        public float GetLOD2Distance() => lod2Distance;

        private int GetLODLevel(float distance)
        {
            if (distance <= lod0Distance) return 0;
            if (distance <= lod1Distance) return 1;
            if (distance <= lod2Distance) return 2;
            return 3;
        }

        /// <summary>
        /// Remove all inactive entries to free memory. Call periodically.
        /// </summary>
        public void Compact()
        {
            _idToIndex.Clear();
            var active = new List<LODEntry>();

            foreach (var entry in _entries)
            {
                if (entry.Active && entry.Target != null)
                {
                    _idToIndex[entry.Id] = active.Count;
                    active.Add(entry);
                }
            }

            _entries.Clear();
            _entries.AddRange(active);
        }
    }
}

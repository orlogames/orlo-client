using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Tracks all networked entities (other players, NPCs, objects, vehicles).
    /// Creates/destroys GameObjects based on server EntitySpawn/Despawn messages.
    /// </summary>
    public class EntityManager : MonoBehaviour
    {
        public static EntityManager Instance { get; private set; }

        [SerializeField] private GameObject defaultEntityPrefab;

        private readonly Dictionary<ulong, GameObject> _entities = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SpawnEntity(ulong entityId, uint entityType, string assetId,
            Vector3 position, Quaternion rotation)
        {
            if (_entities.ContainsKey(entityId))
            {
                Debug.LogWarning($"[Entity] Duplicate spawn for {entityId}");
                return;
            }

            // TODO: Resolve assetId to actual prefab from asset pipeline
            var go = defaultEntityPrefab != null
                ? Instantiate(defaultEntityPrefab, position, rotation)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            go.name = $"Entity_{entityId}_{entityType}";
            go.transform.SetPositionAndRotation(position, rotation);
            _entities[entityId] = go;
        }

        public void DespawnEntity(ulong entityId)
        {
            if (_entities.TryGetValue(entityId, out var go))
            {
                Destroy(go);
                _entities.Remove(entityId);
            }
        }

        public void MoveEntity(ulong entityId, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!_entities.TryGetValue(entityId, out var go)) return;

            // Interpolate towards target position
            go.transform.position = Vector3.Lerp(go.transform.position, position, 0.3f);
            go.transform.rotation = Quaternion.Slerp(go.transform.rotation, rotation, 0.3f);
        }

        public GameObject GetEntity(ulong entityId)
        {
            _entities.TryGetValue(entityId, out var go);
            return go;
        }
    }
}

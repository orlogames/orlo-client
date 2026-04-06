using System.Collections.Generic;
using UnityEngine;
using Orlo.Animation;

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

        /// <summary>
        /// Optional factory delegate for procedural entity creation.
        /// Set by ProceduralEntityFactory to override prefab-based spawning.
        /// </summary>
        public System.Func<uint, string, Vector3, Quaternion, GameObject> EntityFactory;

        private readonly Dictionary<ulong, GameObject> _entities = new();
        private readonly Dictionary<ulong, (float current, float max)> _entityHealth = new();
        private readonly Dictionary<ulong, Vector3> _entityPrevPos = new();

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

            // Use procedural factory if available, otherwise fall back to prefab/primitive
            var go = EntityFactory != null
                ? EntityFactory(entityType, assetId, position, rotation)
                : (defaultEntityPrefab != null
                    ? Instantiate(defaultEntityPrefab, position, rotation)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule));

            go.name = $"Entity_{entityId}_{entityType}";
            go.transform.SetPositionAndRotation(position, rotation);
            _entities[entityId] = go;
            _entityPrevPos[entityId] = position;

            // Attach procedural animation to humanoid entity types
            // 1 = player, 3 = humanoid NPC
            if (entityType == 1 || entityType == 3)
            {
                if (go.GetComponent<CharacterAnimator>() == null)
                    go.AddComponent<CharacterAnimator>();
            }
        }

        public void DespawnEntity(ulong entityId)
        {
            _entityHealth.Remove(entityId);
            _entityPrevPos.Remove(entityId);
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

            // Drive animation from network velocity
            var animator = go.GetComponent<CharacterAnimator>();
            if (animator != null)
            {
                // Use server velocity if non-zero, otherwise derive from position delta
                Vector3 animVel = velocity;
                if (animVel.sqrMagnitude < 0.01f && _entityPrevPos.TryGetValue(entityId, out var prev))
                {
                    float dt = Time.deltaTime;
                    if (dt > 0.001f)
                        animVel = (position - prev) / dt;
                }
                bool sprinting = animVel.magnitude > 7f; // run threshold
                animator.SetMovementState(animVel, true, sprinting);
            }
            _entityPrevPos[entityId] = position;
        }

        public GameObject GetEntity(ulong entityId)
        {
            _entities.TryGetValue(entityId, out var go);
            return go;
        }

        public void UpdateEntityHealth(ulong entityId, float current, float max)
        {
            if (max > 0)
                _entityHealth[entityId] = (current, max);
        }

        public bool TryGetEntityHealth(ulong entityId, out float current, out float max)
        {
            if (_entityHealth.TryGetValue(entityId, out var hp))
            {
                current = hp.current;
                max = hp.max;
                return true;
            }
            current = 0; max = 0;
            return false;
        }
    }
}

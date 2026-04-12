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
        private readonly HashSet<ulong> _lootEntities = new();
        private readonly Dictionary<ulong, string> _entityNames = new();

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

            // Attach procedural animation to humanoid entity types only
            // 1 = player, 3 = humanoid NPC
            // 4 = static prop — no animation, no character controller
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

        /// <summary>Spawn a loot entity with a glowing visual at the given position.</summary>
        public void SpawnLootEntity(ulong lootEntityId, Vector3 position, string lootName)
        {
            if (_entities.ContainsKey(lootEntityId))
            {
                Debug.LogWarning($"[Entity] Duplicate loot spawn for {lootEntityId}");
                return;
            }

            var go = new GameObject($"Loot_{lootEntityId}");
            go.transform.position = position;

            // Create a glowing sphere as the loot bag visual
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(go.transform, false);
            sphere.transform.localScale = Vector3.one * 0.4f;
            sphere.transform.localPosition = Vector3.up * 0.2f;

            // Set up glowing gold material
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = Orlo.Rendering.OrloShaders.CreateEmissive(
                    new Color(1f, 0.85f, 0.2f),
                    new Color(1f, 0.7f, 0.1f), 2f);
                mat.SetFloat("_Metallic", 0.8f);
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", 0.9f);
                renderer.material = mat;
            }

            // Add a point light for glow effect
            var light = new GameObject("LootGlow").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = Vector3.up * 0.5f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.3f);
            light.intensity = 1.5f;
            light.range = 4f;

            // Add the pulsing glow component
            go.AddComponent<LootPulse>();

            _entities[lootEntityId] = go;
            _lootEntities.Add(lootEntityId);
            _entityNames[lootEntityId] = lootName ?? "Loot";
        }

        public void DespawnLootEntity(ulong lootEntityId)
        {
            _lootEntities.Remove(lootEntityId);
            _entityNames.Remove(lootEntityId);
            DespawnEntity(lootEntityId);
        }

        public bool IsLootEntity(ulong entityId) => _lootEntities.Contains(entityId);

        /// <summary>Get all active loot entity IDs.</summary>
        public IEnumerable<ulong> GetLootEntityIds() => _lootEntities;

        /// <summary>Get display name for an entity (set on spawn or via name tracking).</summary>
        public string GetEntityName(ulong entityId)
        {
            return _entityNames.TryGetValue(entityId, out var name) ? name : null;
        }

        /// <summary>Set display name for an entity.</summary>
        public void SetEntityName(ulong entityId, string name)
        {
            if (!string.IsNullOrEmpty(name))
                _entityNames[entityId] = name;
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

        /// <summary>Get all entity IDs (for raycast targeting).</summary>
        public IEnumerable<KeyValuePair<ulong, GameObject>> GetAllEntities() => _entities;
    }

    /// <summary>
    /// Pulsing glow effect for loot entities on the ground.
    /// Scales the object up/down and pulses the light intensity.
    /// </summary>
    public class LootPulse : MonoBehaviour
    {
        private Light _light;
        private float _baseIntensity = 1.5f;
        private float _time;

        private void Start()
        {
            _light = GetComponentInChildren<Light>();
        }

        private void Update()
        {
            _time += Time.deltaTime;
            // Gentle bob up and down
            float bob = Mathf.Sin(_time * 2f) * 0.05f;
            var pos = transform.position;
            // Only modify the visual child, not root (to keep pickup distance stable)
            if (transform.childCount > 0)
                transform.GetChild(0).localPosition = new Vector3(0, 0.2f + bob, 0);

            // Pulse light
            if (_light != null)
                _light.intensity = _baseIntensity + Mathf.Sin(_time * 3f) * 0.5f;

            // Slow rotation
            transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Scene orchestrator driving time-of-day progression, weather, and NPC spawning.
    /// Singleton that coordinates SkyboxController, WeatherController, and entity spawning.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Time of Day")]
        [SerializeField] private float dayLengthSeconds = 600f;
        [SerializeField] private float startHour = 8f;

        [Header("Spawning")]
        [SerializeField] private float spawnCheckInterval = 2f;
        [SerializeField] private float maxSpawnDistance = 100f;
        [SerializeField] private float minSpawnDistance = 20f;
        [SerializeField] private float respawnCooldown = 30f;

        /// <summary>
        /// Current hour of the day (0-24 float).
        /// </summary>
        public float CurrentHour { get; private set; }

        /// <summary>
        /// Normalized time of day (0-1).
        /// </summary>
        public float NormalizedTime => CurrentHour / 24f;

        /// <summary>
        /// Whether it is currently daytime (6-18).
        /// </summary>
        public bool IsDaytime => CurrentHour >= 6f && CurrentHour < 18f;

        /// <summary>
        /// Whether it is currently nighttime (18-6).
        /// </summary>
        public bool IsNighttime => !IsDaytime;

        // Spawning
        [System.Serializable]
        public struct SpawnPoint
        {
            public Vector3 Position;
            public string Archetype;
            public float ActiveStartHour;
            public float ActiveEndHour;
            public uint EntityType;
            public string AssetId;

            public SpawnPoint(Vector3 position, string archetype, float startHour, float endHour,
                uint entityType = ProceduralEntityFactory.TYPE_NPC, string assetId = "")
            {
                Position = position;
                Archetype = archetype;
                ActiveStartHour = startHour;
                ActiveEndHour = endHour;
                EntityType = entityType;
                AssetId = string.IsNullOrEmpty(assetId) ? archetype : assetId;
            }
        }

        private struct SpawnState
        {
            public bool IsSpawned;
            public ulong EntityId;
            public float LastDespawnTime;
        }

        private readonly List<SpawnPoint> _spawnPoints = new();
        private readonly Dictionary<int, SpawnState> _spawnStates = new();
        private float _spawnCheckTimer;
        private ulong _nextLocalEntityId = 100000; // High range to avoid server ID collision
        private Transform _playerTransform;

        // References
        private SkyboxController _skyboxController;
        private WeatherController _weatherController;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            CurrentHour = startHour;
        }

        private void Start()
        {
            _skyboxController = FindFirstObjectByType<SkyboxController>();
            _weatherController = FindFirstObjectByType<WeatherController>();
        }

        private void Update()
        {
            UpdateTime();
            UpdateEnvironment();
            UpdateSpawning();
        }

        private void UpdateTime()
        {
            // Advance time of day
            float hoursPerSecond = 24f / dayLengthSeconds;
            CurrentHour += hoursPerSecond * Time.deltaTime;

            if (CurrentHour >= 24f)
                CurrentHour -= 24f;
        }

        private void UpdateEnvironment()
        {
            if (_skyboxController == null) return;

            // Compute sun/ambient colors from hour
            float normalizedTime = NormalizedTime;
            float sunHeight = Mathf.Max(0f, Mathf.Sin(normalizedTime * Mathf.PI * 2f - Mathf.PI * 0.5f));

            // Sun color shifts warmer at dawn/dusk
            float dawnDusk = 1f - Mathf.Abs(sunHeight - 0.3f) * 3f;
            dawnDusk = Mathf.Clamp01(dawnDusk);

            Color sunColor = Color.Lerp(
                new Color(0.2f, 0.2f, 0.4f), // Night
                new Color(1f, 0.95f, 0.85f),  // Day
                sunHeight);
            sunColor = Color.Lerp(sunColor, new Color(1f, 0.6f, 0.3f), dawnDusk * 0.5f);

            Color ambientColor = Color.Lerp(
                new Color(0.05f, 0.05f, 0.1f),
                new Color(0.4f, 0.45f, 0.5f),
                sunHeight);

            float fogDensity = Mathf.Lerp(0.005f, 0.001f, sunHeight);
            Color fogColor = Color.Lerp(
                new Color(0.1f, 0.1f, 0.15f),
                new Color(0.7f, 0.75f, 0.8f),
                sunHeight);

            _skyboxController.OnEnvironmentUpdate(
                normalizedTime,
                sunColor.r, sunColor.g, sunColor.b,
                ambientColor.r, ambientColor.g, ambientColor.b,
                fogDensity, fogColor.r, fogColor.g, fogColor.b,
                0f // weatherIntensity managed separately
            );
        }

        private void UpdateSpawning()
        {
            _spawnCheckTimer -= Time.deltaTime;
            if (_spawnCheckTimer > 0) return;
            _spawnCheckTimer = spawnCheckInterval;

            // Find player
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    _playerTransform = player.transform;
                else
                    return;
            }

            Vector3 playerPos = _playerTransform.position;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                var sp = _spawnPoints[i];
                if (!_spawnStates.TryGetValue(i, out var state))
                    state = new SpawnState();

                float dist = Vector3.Distance(playerPos, sp.Position);

                // Check if player is in range
                bool inRange = dist >= minSpawnDistance && dist <= maxSpawnDistance;

                // Check time window
                bool activeHours;
                if (sp.ActiveStartHour <= sp.ActiveEndHour)
                    activeHours = CurrentHour >= sp.ActiveStartHour && CurrentHour < sp.ActiveEndHour;
                else // Wraps midnight (e.g., 22-6)
                    activeHours = CurrentHour >= sp.ActiveStartHour || CurrentHour < sp.ActiveEndHour;

                if (state.IsSpawned)
                {
                    // Despawn if out of range or wrong hours
                    if (!inRange || !activeHours)
                    {
                        DespawnEntity(state.EntityId);
                        state.IsSpawned = false;
                        state.LastDespawnTime = Time.time;
                        _spawnStates[i] = state;
                    }
                }
                else
                {
                    // Spawn if conditions met and cooldown elapsed
                    if (inRange && activeHours &&
                        Time.time - state.LastDespawnTime >= respawnCooldown)
                    {
                        ulong entityId = SpawnEntity(sp);
                        if (entityId != 0)
                        {
                            state.IsSpawned = true;
                            state.EntityId = entityId;
                            _spawnStates[i] = state;
                        }
                    }
                }
            }
        }

        private ulong SpawnEntity(SpawnPoint sp)
        {
            ulong id = _nextLocalEntityId++;

            var factory = ProceduralEntityFactory.Instance;
            if (factory != null)
            {
                var go = factory.BuildEntity(sp.EntityType, sp.AssetId, sp.Position, Quaternion.identity);
                go.name = $"Spawned_{sp.Archetype}_{id}";

                // Register with EntityManager
                var em = EntityManager.Instance;
                if (em != null)
                {
                    // Use the direct entity tracking
                    em.SpawnEntity(id, sp.EntityType, sp.AssetId, sp.Position, Quaternion.identity);
                }
            }
            else
            {
                // Fallback — use EntityManager directly
                var em = EntityManager.Instance;
                if (em != null)
                    em.SpawnEntity(id, sp.EntityType, sp.AssetId, sp.Position, Quaternion.identity);
                else
                    return 0;
            }

            return id;
        }

        private void DespawnEntity(ulong entityId)
        {
            var em = EntityManager.Instance;
            if (em != null)
                em.DespawnEntity(entityId);
        }

        /// <summary>
        /// Register a spawn point for the director to manage.
        /// </summary>
        public int AddSpawnPoint(SpawnPoint spawnPoint)
        {
            int index = _spawnPoints.Count;
            _spawnPoints.Add(spawnPoint);
            return index;
        }

        /// <summary>
        /// Remove a spawn point by index.
        /// </summary>
        public void RemoveSpawnPoint(int index)
        {
            if (index < 0 || index >= _spawnPoints.Count) return;

            // Despawn if active
            if (_spawnStates.TryGetValue(index, out var state) && state.IsSpawned)
            {
                DespawnEntity(state.EntityId);
            }

            _spawnStates.Remove(index);
        }

        /// <summary>
        /// Set day length in real seconds.
        /// </summary>
        public void SetDayLength(float seconds)
        {
            dayLengthSeconds = Mathf.Max(10f, seconds);
        }

        /// <summary>
        /// Set the current hour directly (for server sync).
        /// </summary>
        public void SetCurrentHour(float hour)
        {
            CurrentHour = Mathf.Repeat(hour, 24f);
        }
    }
}

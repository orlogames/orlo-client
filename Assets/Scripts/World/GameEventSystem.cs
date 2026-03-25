using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Data-driven event system with proximity, time-of-day, and timer triggers.
    /// Named GameEventSystem to avoid conflict with UnityEngine.EventSystems.
    /// </summary>
    public class GameEventSystem : MonoBehaviour
    {
        public static GameEventSystem Instance { get; private set; }

        // Proximity triggers
        private readonly List<ProximityTrigger> _proximityTriggers = new();
        private readonly HashSet<int> _activeProximityTriggers = new();

        // Time-of-day triggers
        private readonly List<TimeTrigger> _timeTriggers = new();
        private readonly Dictionary<int, float> _lastTimeTriggered = new();

        // Timer triggers
        private readonly List<TimerTrigger> _timerTriggers = new();
        private readonly List<int> _expiredTimers = new();

        private Transform _playerTransform;
        private int _nextId;

        public struct ProximityTrigger
        {
            public int Id;
            public Vector3 Position;
            public float Radius;
            public Action OnEnter;
            public Action OnExit;
            public bool Active;
        }

        public struct TimeTrigger
        {
            public int Id;
            public float TriggerHour;    // 0-24
            public float HourTolerance;  // Window around trigger hour
            public Action OnTriggered;
            public bool Active;
            public bool Repeating;       // Fires every day or just once
        }

        public struct TimerTrigger
        {
            public int Id;
            public float RemainingTime;
            public float Interval;       // For repeating timers
            public Action OnTriggered;
            public bool Active;
            public bool Repeating;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            // Find player if needed
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    _playerTransform = player.transform;
            }

            CheckProximityTriggers();
            CheckTimeTriggers();
            UpdateTimerTriggers();
        }

        // ===== Proximity Triggers =====

        private void CheckProximityTriggers()
        {
            if (_playerTransform == null) return;
            Vector3 playerPos = _playerTransform.position;

            for (int i = 0; i < _proximityTriggers.Count; i++)
            {
                var trigger = _proximityTriggers[i];
                if (!trigger.Active) continue;

                float dist = Vector3.Distance(playerPos, trigger.Position);
                bool wasInside = _activeProximityTriggers.Contains(trigger.Id);
                bool isInside = dist <= trigger.Radius;

                if (isInside && !wasInside)
                {
                    _activeProximityTriggers.Add(trigger.Id);
                    trigger.OnEnter?.Invoke();
                }
                else if (!isInside && wasInside)
                {
                    _activeProximityTriggers.Remove(trigger.Id);
                    trigger.OnExit?.Invoke();
                }
            }
        }

        /// <summary>
        /// Register a proximity trigger.
        /// </summary>
        public int AddProximityTrigger(Vector3 position, float radius, Action onEnter, Action onExit = null)
        {
            int id = _nextId++;
            _proximityTriggers.Add(new ProximityTrigger
            {
                Id = id,
                Position = position,
                Radius = radius,
                OnEnter = onEnter,
                OnExit = onExit,
                Active = true
            });
            return id;
        }

        /// <summary>
        /// Remove a proximity trigger by ID.
        /// </summary>
        public void RemoveProximityTrigger(int id)
        {
            for (int i = 0; i < _proximityTriggers.Count; i++)
            {
                if (_proximityTriggers[i].Id == id)
                {
                    var t = _proximityTriggers[i];
                    t.Active = false;
                    _proximityTriggers[i] = t;
                    _activeProximityTriggers.Remove(id);
                    return;
                }
            }
        }

        // ===== Time-of-Day Triggers =====

        private void CheckTimeTriggers()
        {
            var director = GameDirector.Instance;
            if (director == null) return;

            float currentHour = director.CurrentHour;

            for (int i = 0; i < _timeTriggers.Count; i++)
            {
                var trigger = _timeTriggers[i];
                if (!trigger.Active) continue;

                float diff = Mathf.Abs(currentHour - trigger.TriggerHour);
                // Handle wrap-around (e.g., trigger at 23.5, current at 0.5)
                if (diff > 12f) diff = 24f - diff;

                if (diff <= trigger.HourTolerance)
                {
                    // Check if already triggered this cycle
                    if (_lastTimeTriggered.TryGetValue(trigger.Id, out float lastTime))
                    {
                        float elapsed = Time.time - lastTime;
                        // Don't re-trigger within 60 seconds (prevents double-fire)
                        if (elapsed < 60f) continue;
                    }

                    _lastTimeTriggered[trigger.Id] = Time.time;
                    trigger.OnTriggered?.Invoke();

                    if (!trigger.Repeating)
                    {
                        trigger.Active = false;
                        _timeTriggers[i] = trigger;
                    }
                }
            }
        }

        /// <summary>
        /// Register a time-of-day trigger.
        /// </summary>
        /// <param name="hour">Hour to trigger (0-24)</param>
        /// <param name="tolerance">Hour window tolerance (e.g., 0.5 = 30 min window)</param>
        /// <param name="onTriggered">Callback when triggered</param>
        /// <param name="repeating">Whether to fire every day</param>
        public int AddTimeTrigger(float hour, float tolerance, Action onTriggered, bool repeating = true)
        {
            int id = _nextId++;
            _timeTriggers.Add(new TimeTrigger
            {
                Id = id,
                TriggerHour = hour,
                HourTolerance = tolerance,
                OnTriggered = onTriggered,
                Active = true,
                Repeating = repeating
            });
            return id;
        }

        /// <summary>
        /// Remove a time trigger by ID.
        /// </summary>
        public void RemoveTimeTrigger(int id)
        {
            for (int i = 0; i < _timeTriggers.Count; i++)
            {
                if (_timeTriggers[i].Id == id)
                {
                    var t = _timeTriggers[i];
                    t.Active = false;
                    _timeTriggers[i] = t;
                    return;
                }
            }
        }

        // ===== Timer Triggers =====

        private void UpdateTimerTriggers()
        {
            _expiredTimers.Clear();

            for (int i = 0; i < _timerTriggers.Count; i++)
            {
                var timer = _timerTriggers[i];
                if (!timer.Active) continue;

                timer.RemainingTime -= Time.deltaTime;

                if (timer.RemainingTime <= 0f)
                {
                    timer.OnTriggered?.Invoke();

                    if (timer.Repeating)
                    {
                        timer.RemainingTime = timer.Interval;
                    }
                    else
                    {
                        timer.Active = false;
                        _expiredTimers.Add(i);
                    }
                }

                _timerTriggers[i] = timer;
            }
        }

        /// <summary>
        /// Add a one-shot timer trigger.
        /// </summary>
        /// <param name="delay">Seconds until trigger fires</param>
        /// <param name="onTriggered">Callback</param>
        public int AddTimer(float delay, Action onTriggered)
        {
            int id = _nextId++;
            _timerTriggers.Add(new TimerTrigger
            {
                Id = id,
                RemainingTime = delay,
                Interval = delay,
                OnTriggered = onTriggered,
                Active = true,
                Repeating = false
            });
            return id;
        }

        /// <summary>
        /// Add a repeating timer trigger.
        /// </summary>
        /// <param name="interval">Seconds between firings</param>
        /// <param name="onTriggered">Callback</param>
        /// <param name="startImmediately">If true, fires once immediately</param>
        public int AddRepeatingTimer(float interval, Action onTriggered, bool startImmediately = false)
        {
            int id = _nextId++;

            if (startImmediately)
                onTriggered?.Invoke();

            _timerTriggers.Add(new TimerTrigger
            {
                Id = id,
                RemainingTime = interval,
                Interval = interval,
                OnTriggered = onTriggered,
                Active = true,
                Repeating = true
            });
            return id;
        }

        /// <summary>
        /// Remove a timer by ID.
        /// </summary>
        public void RemoveTimer(int id)
        {
            for (int i = 0; i < _timerTriggers.Count; i++)
            {
                if (_timerTriggers[i].Id == id)
                {
                    var t = _timerTriggers[i];
                    t.Active = false;
                    _timerTriggers[i] = t;
                    return;
                }
            }
        }

        /// <summary>
        /// Remove all triggers of all types.
        /// </summary>
        public void ClearAll()
        {
            _proximityTriggers.Clear();
            _activeProximityTriggers.Clear();
            _timeTriggers.Clear();
            _lastTimeTriggered.Clear();
            _timerTriggers.Clear();
        }
    }
}

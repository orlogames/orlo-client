using System.Collections.Generic;
using UnityEngine;

namespace Orlo.AI
{
    /// <summary>
    /// Behavior tree tick result.
    /// </summary>
    public enum BTStatus
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// Context passed to every node during a tick.
    /// </summary>
    public class BTContext
    {
        public GameObject Entity;
        public float DeltaTime;
        public Dictionary<string, object> Blackboard = new();

        public T Get<T>(string key, T defaultValue = default)
        {
            if (Blackboard.TryGetValue(key, out var val) && val is T typed)
                return typed;
            return defaultValue;
        }

        public void Set(string key, object value)
        {
            Blackboard[key] = value;
        }
    }

    // ===================================================================
    // Base Node
    // ===================================================================

    /// <summary>
    /// Abstract base for all behavior tree nodes.
    /// </summary>
    public abstract class BTNode
    {
        public abstract BTStatus Tick(BTContext context);
        public virtual void Reset() { }
    }

    // ===================================================================
    // Composites
    // ===================================================================

    /// <summary>
    /// Selector: tries children in order, succeeds on first success.
    /// </summary>
    public class BTSelector : BTNode
    {
        private readonly List<BTNode> _children;
        private int _runningChild = -1;

        public BTSelector(params BTNode[] children)
        {
            _children = new List<BTNode>(children);
        }

        public override BTStatus Tick(BTContext context)
        {
            int startIndex = _runningChild >= 0 ? _runningChild : 0;

            for (int i = startIndex; i < _children.Count; i++)
            {
                var status = _children[i].Tick(context);

                switch (status)
                {
                    case BTStatus.Success:
                        _runningChild = -1;
                        return BTStatus.Success;
                    case BTStatus.Running:
                        _runningChild = i;
                        return BTStatus.Running;
                    case BTStatus.Failure:
                        continue;
                }
            }

            _runningChild = -1;
            return BTStatus.Failure;
        }

        public override void Reset()
        {
            _runningChild = -1;
            foreach (var child in _children)
                child.Reset();
        }
    }

    /// <summary>
    /// Sequence: runs children in order, fails on first failure.
    /// </summary>
    public class BTSequence : BTNode
    {
        private readonly List<BTNode> _children;
        private int _runningChild;

        public BTSequence(params BTNode[] children)
        {
            _children = new List<BTNode>(children);
        }

        public override BTStatus Tick(BTContext context)
        {
            for (int i = _runningChild; i < _children.Count; i++)
            {
                var status = _children[i].Tick(context);

                switch (status)
                {
                    case BTStatus.Failure:
                        _runningChild = 0;
                        return BTStatus.Failure;
                    case BTStatus.Running:
                        _runningChild = i;
                        return BTStatus.Running;
                    case BTStatus.Success:
                        continue;
                }
            }

            _runningChild = 0;
            return BTStatus.Success;
        }

        public override void Reset()
        {
            _runningChild = 0;
            foreach (var child in _children)
                child.Reset();
        }
    }

    /// <summary>
    /// Parallel: ticks all children every frame.
    /// Succeeds when requiredSuccesses children succeed.
    /// Fails when any child fails (or configurable).
    /// </summary>
    public class BTParallel : BTNode
    {
        private readonly List<BTNode> _children;
        private readonly int _requiredSuccesses;

        public BTParallel(int requiredSuccesses, params BTNode[] children)
        {
            _requiredSuccesses = requiredSuccesses;
            _children = new List<BTNode>(children);
        }

        public override BTStatus Tick(BTContext context)
        {
            int successes = 0;
            int failures = 0;

            foreach (var child in _children)
            {
                var status = child.Tick(context);
                if (status == BTStatus.Success) successes++;
                else if (status == BTStatus.Failure) failures++;
            }

            if (successes >= _requiredSuccesses)
                return BTStatus.Success;
            if (failures > _children.Count - _requiredSuccesses)
                return BTStatus.Failure;

            return BTStatus.Running;
        }

        public override void Reset()
        {
            foreach (var child in _children)
                child.Reset();
        }
    }

    // ===================================================================
    // Decorators
    // ===================================================================

    /// <summary>
    /// Inverts the result of a child node.
    /// </summary>
    public class BTInverter : BTNode
    {
        private readonly BTNode _child;

        public BTInverter(BTNode child)
        {
            _child = child;
        }

        public override BTStatus Tick(BTContext context)
        {
            var status = _child.Tick(context);
            switch (status)
            {
                case BTStatus.Success: return BTStatus.Failure;
                case BTStatus.Failure: return BTStatus.Success;
                default: return BTStatus.Running;
            }
        }

        public override void Reset() => _child.Reset();
    }

    /// <summary>
    /// Repeats a child node a given number of times (or forever if count <= 0).
    /// </summary>
    public class BTRepeater : BTNode
    {
        private readonly BTNode _child;
        private readonly int _maxCount;
        private int _currentCount;

        public BTRepeater(BTNode child, int count = 0)
        {
            _child = child;
            _maxCount = count;
        }

        public override BTStatus Tick(BTContext context)
        {
            var status = _child.Tick(context);

            if (status == BTStatus.Running)
                return BTStatus.Running;

            _currentCount++;

            if (_maxCount > 0 && _currentCount >= _maxCount)
            {
                _currentCount = 0;
                return BTStatus.Success;
            }

            _child.Reset();
            return BTStatus.Running;
        }

        public override void Reset()
        {
            _currentCount = 0;
            _child.Reset();
        }
    }

    /// <summary>
    /// Prevents a child from running again until a cooldown period has elapsed.
    /// </summary>
    public class BTCooldown : BTNode
    {
        private readonly BTNode _child;
        private readonly float _cooldownTime;
        private float _lastRunTime = -999f;

        public BTCooldown(BTNode child, float cooldownSeconds)
        {
            _child = child;
            _cooldownTime = cooldownSeconds;
        }

        public override BTStatus Tick(BTContext context)
        {
            if (Time.time - _lastRunTime < _cooldownTime)
                return BTStatus.Failure;

            var status = _child.Tick(context);

            if (status != BTStatus.Running)
                _lastRunTime = Time.time;

            return status;
        }

        public override void Reset()
        {
            _lastRunTime = -999f;
            _child.Reset();
        }
    }

    // ===================================================================
    // Action Nodes
    // ===================================================================

    /// <summary>
    /// Wait for a duration, then succeed.
    /// </summary>
    public class BTIdle : BTNode
    {
        private readonly float _duration;
        private float _elapsed;

        public BTIdle(float duration)
        {
            _duration = duration;
        }

        public override BTStatus Tick(BTContext context)
        {
            _elapsed += context.DeltaTime;
            if (_elapsed >= _duration)
            {
                _elapsed = 0f;
                return BTStatus.Success;
            }
            return BTStatus.Running;
        }

        public override void Reset() => _elapsed = 0f;
    }

    /// <summary>
    /// Move toward a target position stored in the blackboard.
    /// Succeeds when within arrivalDistance.
    /// </summary>
    public class BTMoveTo : BTNode
    {
        private readonly string _targetKey;
        private readonly float _speed;
        private readonly float _arrivalDistance;

        public BTMoveTo(string targetKey, float speed = 3f, float arrivalDistance = 1f)
        {
            _targetKey = targetKey;
            _speed = speed;
            _arrivalDistance = arrivalDistance;
        }

        public override BTStatus Tick(BTContext context)
        {
            if (context.Entity == null) return BTStatus.Failure;

            var target = context.Get<Vector3>(_targetKey);
            if (target == default) return BTStatus.Failure;

            var transform = context.Entity.transform;
            Vector3 direction = target - transform.position;
            direction.y = 0; // Stay on ground plane

            float distance = direction.magnitude;

            if (distance <= _arrivalDistance)
                return BTStatus.Success;

            Vector3 move = direction.normalized * _speed * context.DeltaTime;

            var cc = context.Entity.GetComponent<CharacterController>();
            if (cc != null)
            {
                // Apply gravity
                move.y = -9.81f * context.DeltaTime;
                cc.Move(move);
            }
            else
            {
                transform.position += move;
            }

            // Face movement direction
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction.normalized),
                    context.DeltaTime * 5f);
            }

            return BTStatus.Running;
        }
    }

    /// <summary>
    /// Rotate to look at a target position stored in the blackboard.
    /// </summary>
    public class BTLookAt : BTNode
    {
        private readonly string _targetKey;
        private readonly float _rotSpeed;

        public BTLookAt(string targetKey, float rotationSpeed = 5f)
        {
            _targetKey = targetKey;
            _rotSpeed = rotationSpeed;
        }

        public override BTStatus Tick(BTContext context)
        {
            if (context.Entity == null) return BTStatus.Failure;

            var target = context.Get<Vector3>(_targetKey);
            if (target == default) return BTStatus.Failure;

            var transform = context.Entity.transform;
            Vector3 direction = target - transform.position;
            direction.y = 0;

            if (direction.sqrMagnitude < 0.01f)
                return BTStatus.Success;

            Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, context.DeltaTime * _rotSpeed);

            float angle = Quaternion.Angle(transform.rotation, targetRot);
            return angle < 5f ? BTStatus.Success : BTStatus.Running;
        }
    }

    /// <summary>
    /// Set an animation state on the ProceduralAnimator.
    /// Succeeds immediately.
    /// </summary>
    public class BTPlayAnim : BTNode
    {
        private readonly World.AnimationState _state;

        public BTPlayAnim(World.AnimationState state)
        {
            _state = state;
        }

        public override BTStatus Tick(BTContext context)
        {
            if (context.Entity == null) return BTStatus.Failure;

            var animator = context.Entity.GetComponent<World.ProceduralAnimator>();
            if (animator != null)
                animator.SetState(_state);

            return BTStatus.Success;
        }
    }

    // ===================================================================
    // Condition Nodes
    // ===================================================================

    /// <summary>
    /// Succeeds if a target (blackboard Vector3) is within range of the entity.
    /// </summary>
    public class BTIsInRange : BTNode
    {
        private readonly string _targetKey;
        private readonly float _range;

        public BTIsInRange(string targetKey, float range)
        {
            _targetKey = targetKey;
            _range = range;
        }

        public override BTStatus Tick(BTContext context)
        {
            if (context.Entity == null) return BTStatus.Failure;

            var target = context.Get<Vector3>(_targetKey);
            if (target == default) return BTStatus.Failure;

            float dist = Vector3.Distance(context.Entity.transform.position, target);
            return dist <= _range ? BTStatus.Success : BTStatus.Failure;
        }
    }

    /// <summary>
    /// Succeeds if the current game hour is within [startHour, endHour).
    /// </summary>
    public class BTIsHour : BTNode
    {
        private readonly float _startHour;
        private readonly float _endHour;

        public BTIsHour(float startHour, float endHour)
        {
            _startHour = startHour;
            _endHour = endHour;
        }

        public override BTStatus Tick(BTContext context)
        {
            var director = World.GameDirector.Instance;
            if (director == null) return BTStatus.Failure;

            float hour = director.CurrentHour;

            bool inWindow;
            if (_startHour <= _endHour)
                inWindow = hour >= _startHour && hour < _endHour;
            else // wraps midnight
                inWindow = hour >= _startHour || hour < _endHour;

            return inWindow ? BTStatus.Success : BTStatus.Failure;
        }
    }

    /// <summary>
    /// Succeeds if the blackboard contains a non-null value for the given key.
    /// </summary>
    public class BTHasTarget : BTNode
    {
        private readonly string _targetKey;

        public BTHasTarget(string targetKey)
        {
            _targetKey = targetKey;
        }

        public override BTStatus Tick(BTContext context)
        {
            if (!context.Blackboard.TryGetValue(_targetKey, out var val))
                return BTStatus.Failure;
            if (val == null)
                return BTStatus.Failure;
            if (val is Vector3 v && v == default)
                return BTStatus.Failure;
            return BTStatus.Success;
        }
    }

    // ===================================================================
    // Behavior Tree Wrapper
    // ===================================================================

    /// <summary>
    /// Wrapper class that manages a root node and context for ticking.
    /// </summary>
    public class BehaviorTree
    {
        private readonly BTNode _root;
        private readonly BTContext _context;

        public BTContext Context => _context;

        public BehaviorTree(BTNode root, GameObject entity)
        {
            _root = root;
            _context = new BTContext
            {
                Entity = entity
            };
        }

        /// <summary>
        /// Tick the behavior tree.
        /// </summary>
        public BTStatus Tick(float deltaTime)
        {
            _context.DeltaTime = deltaTime;
            return _root.Tick(_context);
        }

        /// <summary>
        /// Reset the entire tree.
        /// </summary>
        public void Reset()
        {
            _root.Reset();
        }
    }
}

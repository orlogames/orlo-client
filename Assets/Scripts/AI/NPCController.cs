using UnityEngine;
using Orlo.World;

namespace Orlo.AI
{
    /// <summary>
    /// NPC archetype for behavior tree construction.
    /// </summary>
    public enum NPCArchetype
    {
        Villager,
        Guard,
        Bandit
    }

    /// <summary>
    /// MonoBehaviour managing NPC behavior trees.
    /// Builds archetype-based trees and drives movement via CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NPCController : MonoBehaviour
    {
        [Header("NPC Settings")]
        [SerializeField] private NPCArchetype archetype = NPCArchetype.Villager;
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Patrol")]
        [SerializeField] private Vector3[] patrolPoints;
        [SerializeField] private Vector3 homePosition;
        [SerializeField] private Vector3 workPosition;

        private BehaviorTree _tree;
        private CharacterController _cc;
        private ProceduralAnimator _animator;
        private float _verticalVelocity;
        private int _currentPatrolIndex;

        private void Start()
        {
            _cc = GetComponent<CharacterController>();
            _animator = GetComponent<ProceduralAnimator>();

            // Default home/work to spawn position if not set
            if (homePosition == Vector3.zero)
                homePosition = transform.position;
            if (workPosition == Vector3.zero)
                workPosition = transform.position + new Vector3(10f, 0, 5f);

            BuildBehaviorTree();
        }

        private void Update()
        {
            if (_tree == null) return;

            // Apply gravity
            if (_cc.isGrounded)
                _verticalVelocity = -1f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            _cc.Move(new Vector3(0, _verticalVelocity * Time.deltaTime, 0));

            // Tick behavior tree
            _tree.Tick(Time.deltaTime);
        }

        private void BuildBehaviorTree()
        {
            BTNode root;

            switch (archetype)
            {
                case NPCArchetype.Villager:
                    root = BuildVillagerTree();
                    break;
                case NPCArchetype.Guard:
                    root = BuildGuardTree();
                    break;
                case NPCArchetype.Bandit:
                    root = BuildBanditTree();
                    break;
                default:
                    root = BuildVillagerTree();
                    break;
            }

            _tree = new BehaviorTree(root, gameObject);

            // Set initial blackboard values
            _tree.Context.Set("homePosition", homePosition);
            _tree.Context.Set("workPosition", workPosition);
            _tree.Context.Set("walkSpeed", walkSpeed);
            _tree.Context.Set("runSpeed", runSpeed);

            if (patrolPoints != null && patrolPoints.Length > 0)
                _tree.Context.Set("patrolTarget", patrolPoints[0]);
        }

        /// <summary>
        /// Villager: work during day (8-17), go home at night.
        /// </summary>
        private BTNode BuildVillagerTree()
        {
            return new BTSelector(
                // Daytime: go to work, do work activities
                new BTSequence(
                    new BTIsHour(8f, 17f),
                    new BTSelector(
                        // If at work, do idle work animation
                        new BTSequence(
                            new BTIsInRange("workPosition", 2f),
                            new BTPlayAnim(AnimationState.Idle),
                            new BTIdle(5f)
                        ),
                        // Otherwise, walk to work
                        new BTSequence(
                            new SetBlackboardTarget("moveTarget", "workPosition"),
                            new BTPlayAnim(AnimationState.Walk),
                            new BTMoveTo("moveTarget", walkSpeed, 1.5f)
                        )
                    )
                ),
                // Evening/night: go home
                new BTSequence(
                    new BTInverter(new BTIsHour(8f, 17f)),
                    new BTSelector(
                        // If at home, idle
                        new BTSequence(
                            new BTIsInRange("homePosition", 2f),
                            new BTPlayAnim(AnimationState.Idle),
                            new BTIdle(8f)
                        ),
                        // Walk home
                        new BTSequence(
                            new SetBlackboardTarget("moveTarget", "homePosition"),
                            new BTPlayAnim(AnimationState.Walk),
                            new BTMoveTo("moveTarget", walkSpeed, 1.5f)
                        )
                    )
                ),
                // Fallback: idle in place
                new BTSequence(
                    new BTPlayAnim(AnimationState.Idle),
                    new BTIdle(3f)
                )
            );
        }

        /// <summary>
        /// Guard: patrol during day, stand guard at post at night.
        /// </summary>
        private BTNode BuildGuardTree()
        {
            return new BTSelector(
                // Daytime: patrol between points
                new BTSequence(
                    new BTIsHour(6f, 20f),
                    new BTSelector(
                        // If at patrol target, advance to next and idle briefly
                        new BTSequence(
                            new BTIsInRange("patrolTarget", 2f),
                            new BTPlayAnim(AnimationState.Idle),
                            new BTIdle(3f),
                            new AdvancePatrol(this)
                        ),
                        // Walk to patrol target
                        new BTSequence(
                            new SetBlackboardTarget("moveTarget", "patrolTarget"),
                            new BTPlayAnim(AnimationState.Walk),
                            new BTMoveTo("moveTarget", walkSpeed, 1.5f)
                        )
                    )
                ),
                // Night: go to guard post (home position) and stand
                new BTSequence(
                    new BTInverter(new BTIsHour(6f, 20f)),
                    new BTSelector(
                        // At post, stand alert
                        new BTSequence(
                            new BTIsInRange("homePosition", 2f),
                            new BTPlayAnim(AnimationState.Idle),
                            new BTIdle(5f),
                            new BTCooldown(
                                new RandomLookAround(), 4f
                            )
                        ),
                        // Walk to post
                        new BTSequence(
                            new SetBlackboardTarget("moveTarget", "homePosition"),
                            new BTPlayAnim(AnimationState.Walk),
                            new BTMoveTo("moveTarget", walkSpeed, 1.5f)
                        )
                    )
                ),
                // Fallback
                new BTSequence(
                    new BTPlayAnim(AnimationState.Idle),
                    new BTIdle(2f)
                )
            );
        }

        /// <summary>
        /// Bandit: hide during day, ambush at night.
        /// </summary>
        private BTNode BuildBanditTree()
        {
            return new BTSelector(
                // Daytime: hide at home position
                new BTSequence(
                    new BTIsHour(6f, 20f),
                    new BTSelector(
                        // At hideout, stay still
                        new BTSequence(
                            new BTIsInRange("homePosition", 2f),
                            new BTPlayAnim(AnimationState.Idle),
                            new BTIdle(10f)
                        ),
                        // Walk to hideout
                        new BTSequence(
                            new SetBlackboardTarget("moveTarget", "homePosition"),
                            new BTPlayAnim(AnimationState.Walk),
                            new BTMoveTo("moveTarget", walkSpeed, 1.5f)
                        )
                    )
                ),
                // Nighttime: prowl to ambush position, check for targets
                new BTSequence(
                    new BTInverter(new BTIsHour(6f, 20f)),
                    new BTSelector(
                        // If has a player target in range, run toward and attack
                        new BTSequence(
                            new BTHasTarget("playerTarget"),
                            new BTIsInRange("playerTarget", 30f),
                            new BTSelector(
                                // Close enough to attack
                                new BTSequence(
                                    new BTIsInRange("playerTarget", 3f),
                                    new BTPlayAnim(AnimationState.Attack),
                                    new BTIdle(1.5f)
                                ),
                                // Run toward player
                                new BTSequence(
                                    new SetBlackboardTarget("moveTarget", "playerTarget"),
                                    new BTPlayAnim(AnimationState.Run),
                                    new BTMoveTo("moveTarget", runSpeed, 2.5f)
                                )
                            )
                        ),
                        // No target — patrol work area (ambush route)
                        new BTSequence(
                            new CheckForPlayer(this),
                            new BTSelector(
                                new BTSequence(
                                    new BTIsInRange("patrolTarget", 2f),
                                    new BTIdle(2f),
                                    new AdvancePatrol(this)
                                ),
                                new BTSequence(
                                    new SetBlackboardTarget("moveTarget", "patrolTarget"),
                                    new BTPlayAnim(AnimationState.Walk),
                                    new BTMoveTo("moveTarget", walkSpeed * 0.7f, 1.5f)
                                )
                            )
                        )
                    )
                ),
                // Fallback
                new BTSequence(
                    new BTPlayAnim(AnimationState.Idle),
                    new BTIdle(3f)
                )
            );
        }

        /// <summary>
        /// Move toward a target using the CharacterController.
        /// Returns true when arrived within threshold distance.
        /// </summary>
        public bool MoveToward(Vector3 target, float speed)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0;

            float distance = direction.magnitude;
            if (distance < 1f)
                return true;

            Vector3 move = direction.normalized * speed * Time.deltaTime;
            move.y = _verticalVelocity * Time.deltaTime;
            _cc.Move(move);

            // Face direction
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction.normalized),
                    Time.deltaTime * 5f);
            }

            return false;
        }

        /// <summary>
        /// Advance to the next patrol point.
        /// </summary>
        public void NextPatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
            _tree?.Context.Set("patrolTarget", patrolPoints[_currentPatrolIndex]);
        }

        /// <summary>
        /// Set the NPC archetype and rebuild the tree.
        /// </summary>
        public void SetArchetype(NPCArchetype newArchetype)
        {
            archetype = newArchetype;
            BuildBehaviorTree();
        }

        /// <summary>
        /// Set patrol points.
        /// </summary>
        public void SetPatrolPoints(Vector3[] points)
        {
            patrolPoints = points;
            _currentPatrolIndex = 0;
            if (points != null && points.Length > 0 && _tree != null)
                _tree.Context.Set("patrolTarget", points[0]);
        }

        /// <summary>
        /// Set home and work positions.
        /// </summary>
        public void SetLocations(Vector3 home, Vector3 work)
        {
            homePosition = home;
            workPosition = work;
            if (_tree != null)
            {
                _tree.Context.Set("homePosition", home);
                _tree.Context.Set("workPosition", work);
            }
        }

        // ===================================================================
        // Custom BT Nodes for NPC-specific behavior
        // ===================================================================

        /// <summary>
        /// Copies a blackboard value to a target key.
        /// Used to redirect movement to different waypoints.
        /// </summary>
        private class SetBlackboardTarget : BTNode
        {
            private readonly string _destKey;
            private readonly string _sourceKey;

            public SetBlackboardTarget(string destKey, string sourceKey)
            {
                _destKey = destKey;
                _sourceKey = sourceKey;
            }

            public override BTStatus Tick(BTContext context)
            {
                if (context.Blackboard.TryGetValue(_sourceKey, out var val))
                {
                    context.Blackboard[_destKey] = val;
                    return BTStatus.Success;
                }
                return BTStatus.Failure;
            }
        }

        /// <summary>
        /// Advance the NPC's patrol index.
        /// </summary>
        private class AdvancePatrol : BTNode
        {
            private readonly NPCController _npc;

            public AdvancePatrol(NPCController npc) { _npc = npc; }

            public override BTStatus Tick(BTContext context)
            {
                _npc.NextPatrolPoint();
                return BTStatus.Success;
            }
        }

        /// <summary>
        /// Check if a player is within detection range and set playerTarget.
        /// Always returns Success (just updates blackboard).
        /// </summary>
        private class CheckForPlayer : BTNode
        {
            private readonly NPCController _npc;
            private const float DetectionRange = 25f;

            public CheckForPlayer(NPCController npc) { _npc = npc; }

            public override BTStatus Tick(BTContext context)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    float dist = Vector3.Distance(_npc.transform.position, player.transform.position);
                    if (dist <= DetectionRange)
                    {
                        context.Set("playerTarget", player.transform.position);
                        return BTStatus.Success;
                    }
                }

                // Clear target if out of range
                context.Blackboard.Remove("playerTarget");
                return BTStatus.Success;
            }
        }

        /// <summary>
        /// Make the NPC look around randomly (for guard alertness).
        /// </summary>
        private class RandomLookAround : BTNode
        {
            public override BTStatus Tick(BTContext context)
            {
                if (context.Entity == null) return BTStatus.Failure;

                float randomYaw = Random.Range(-60f, 60f);
                context.Entity.transform.Rotate(0, randomYaw * context.DeltaTime * 2f, 0);
                return BTStatus.Success;
            }
        }
    }
}

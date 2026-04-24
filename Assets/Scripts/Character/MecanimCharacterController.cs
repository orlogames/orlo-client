using UnityEngine;

namespace Orlo.Character
{
    /// <summary>
    /// Drives Unity's Mecanim Animator from player movement state.
    /// Replaces the broken ProceduralWalkCycle + CharacterAnimator system.
    ///
    /// Expects an Animator component with these parameters:
    ///   - Speed (float): 0 = idle, 0-5 = walk, 5+ = run
    ///   - Grounded (bool): true when on ground
    ///   - Jump (trigger): fires on jump
    ///   - VelocityY (float): vertical velocity for fall blending
    ///
    /// Animation clips come from Mixamo or Unity Asset Store (FBX).
    /// If no Animator/AnimatorController exists, creates a simple one
    /// via runtime AnimatorOverrideController.
    /// </summary>
    public class MecanimCharacterController : MonoBehaviour
    {
        private Animator _animator;
        private bool _initialized;

        // Animator parameter hashes (cached for performance)
        private static readonly int ParamSpeed = Animator.StringToHash("Speed");
        private static readonly int ParamGrounded = Animator.StringToHash("Grounded");
        private static readonly int ParamJump = Animator.StringToHash("Jump");
        private static readonly int ParamVelocityY = Animator.StringToHash("VelocityY");

        // Smoothed values for animation blending
        private float _smoothSpeed;
        private const float SpeedDampTime = 0.1f;

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!_initialized) { TryInitialize(); return; }
        }

        /// <summary>
        /// Called by PlayerController each frame with movement state.
        /// </summary>
        public void SetMovementState(Vector3 velocity, bool grounded, bool sprinting)
        {
            if (!_initialized || _animator == null) return;

            float speed = new Vector2(velocity.x, velocity.z).magnitude;

            // Smooth speed for animation blending (prevents jitter)
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, speed, Time.deltaTime / SpeedDampTime);

            _animator.SetFloat(ParamSpeed, _smoothSpeed);
            _animator.SetBool(ParamGrounded, grounded);
            _animator.SetFloat(ParamVelocityY, velocity.y);
        }

        /// <summary>
        /// Trigger jump animation.
        /// </summary>
        public void TriggerJump()
        {
            if (_animator != null)
                _animator.SetTrigger(ParamJump);
        }

        private void TryInitialize()
        {
            _animator = GetComponentInChildren<Animator>();

            if (_animator == null)
            {
                // Only create an Animator component when a valid controller is available.
                // Adding a blank Animator (no controller) to a GameObject that owns a
                // SkinnedMeshRenderer causes Unity to claim the bone Transforms and force
                // them to T-pose every frame, overriding whatever CharacterAnimator sets.
                // Until animation clips are wired in, CharacterAnimator handles all
                // procedural locomotion; we simply don't touch the Mecanim stack.
                var smr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    var controller = Resources.Load<RuntimeAnimatorController>("Animation/HumanoidController");
                    if (controller != null)
                    {
                        _animator = gameObject.AddComponent<Animator>();
                        _animator.applyRootMotion = false;
                        _animator.runtimeAnimatorController = controller;
                        Debug.Log("[MecanimChar] Loaded HumanoidController from Resources");
                    }
                    else
                    {
                        Debug.Log("[MecanimChar] No HumanoidController in Resources/Animation/ — " +
                                  "CharacterAnimator will handle procedural locomotion until clips are added");
                    }
                }
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _initialized = true;
            }
        }

        /// <summary>
        /// Check if the animator has a valid controller with animation clips.
        /// If not, the character will be in T-pose.
        /// </summary>
        public bool HasAnimations => _animator != null &&
                                      _animator.runtimeAnimatorController != null;

        /// <summary>
        /// Assign an animator controller at runtime.
        /// </summary>
        public void SetAnimatorController(RuntimeAnimatorController controller)
        {
            if (_animator == null) return;
            _animator.runtimeAnimatorController = controller;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Orlo.Animation
{
    /// <summary>
    /// Drives procedural animation on any GameObject with a bone hierarchy.
    /// Discovers bones by name from ProceduralCharacter or RuntimeRigBuilder.
    /// Reads movement state and computes per-bone rotations each frame.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public enum AnimState { Idle, Walk, Run, Jump, Fall }

        // Aliases for the canonical Unity Humanoid bone names we drive procedurally.
        // Entry 0 for each key is always the canonical name, so the existing lookup
        // behaviour remains the first attempt. Subsequent aliases cover SWG-style
        // Orlo rig names (root/spine1/larm/lforearm/lthigh/lshin/lankle/...),
        // generic Blender/Unity-bridge names (LeftArm, LeftForeArm), and the
        // underscore/lowercase variants Stepo's bone generator emits.
        private static readonly Dictionary<string, string[]> BoneAliases = new()
        {
            ["Hips"]          = new[] { "Hips", "hips", "root", "Root", "pelvis" },
            ["Spine"]         = new[] { "Spine", "spine", "spine1", "spine_01" },
            ["Chest"]         = new[] { "Chest", "chest", "spine2", "spine3", "spine_02", "spine_03" },
            ["Neck"]          = new[] { "Neck", "neck" },
            ["Head"]          = new[] { "Head", "head" },
            ["LeftUpperArm"]  = new[] { "LeftUpperArm", "l_upper_arm", "larm", "l_arm", "LeftArm" },
            ["LeftLowerArm"]  = new[] { "LeftLowerArm", "l_forearm", "lforearm", "LeftForeArm" },
            ["RightUpperArm"] = new[] { "RightUpperArm", "r_upper_arm", "rarm", "r_arm", "RightArm" },
            ["RightLowerArm"] = new[] { "RightLowerArm", "r_forearm", "rforearm", "RightForeArm" },
            ["LeftUpperLeg"]  = new[] { "LeftUpperLeg", "l_thigh", "lthigh", "LeftLeg" },
            ["LeftLowerLeg"]  = new[] { "LeftLowerLeg", "l_shin", "lshin", "LeftCalf" },
            ["LeftFoot"]      = new[] { "LeftFoot", "l_ankle", "lankle", "l_foot" },
            ["RightUpperLeg"] = new[] { "RightUpperLeg", "r_thigh", "rthigh", "RightLeg" },
            ["RightLowerLeg"] = new[] { "RightLowerLeg", "r_shin", "rshin", "RightCalf" },
            ["RightFoot"]     = new[] { "RightFoot", "r_ankle", "rankle", "r_foot" },
        };

        private AnimationProfile _profile = AnimationProfile.Humanoid;
        private AnimState _currentState = AnimState.Idle;
        private AnimState _targetState = AnimState.Idle;
        private float _phase;           // 0-1 walk cycle phase
        private float _stateBlend;      // 0-1 blend between current and target
        private float _idleTime;

        // Movement input (set externally)
        private Vector3 _velocity;
        private bool _grounded = true;
        private bool _sprinting;

        // Bone references (discovered by name)
        private Transform _hips, _spine, _chest, _neck, _head;
        private Transform _leftUpperArm, _leftLowerArm;
        private Transform _rightUpperArm, _rightLowerArm;
        private Transform _leftUpperLeg, _leftLowerLeg, _leftFoot;
        private Transform _rightUpperLeg, _rightLowerLeg, _rightFoot;

        // Bind-pose rotations (captured on init)
        private Dictionary<Transform, Quaternion> _bindPose = new();
        private bool _initialized;

        public AnimState CurrentState => _currentState;

        public void SetMovementState(Vector3 velocity, bool grounded, bool sprinting)
        {
            _velocity = velocity;
            _grounded = grounded;
            _sprinting = sprinting;
        }

        public void SetProfile(AnimationProfile profile) => _profile = profile ?? AnimationProfile.Humanoid;

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!_initialized) { TryInitialize(); return; }

            UpdateState();
            UpdatePhase();
            ApplyAnimation();
        }

        private void TryInitialize()
        {
            // Search for bones in children by name
            _hips = FindBone("Hips");
            if (_hips == null) return; // No skeleton yet

            _spine = FindBone("Spine");
            _chest = FindBone("Chest");
            _neck = FindBone("Neck");
            _head = FindBone("Head");
            _leftUpperArm = FindBone("LeftUpperArm");
            _leftLowerArm = FindBone("LeftLowerArm");
            _rightUpperArm = FindBone("RightUpperArm");
            _rightLowerArm = FindBone("RightLowerArm");
            _leftUpperLeg = FindBone("LeftUpperLeg");
            _leftLowerLeg = FindBone("LeftLowerLeg");
            _leftFoot = FindBone("LeftFoot");
            _rightUpperLeg = FindBone("RightUpperLeg");
            _rightLowerLeg = FindBone("RightLowerLeg");
            _rightFoot = FindBone("RightFoot");

            // Capture bind pose
            CaptureBindPose(_hips);
            CaptureBindPose(_spine);
            CaptureBindPose(_chest);
            CaptureBindPose(_neck);
            CaptureBindPose(_head);
            CaptureBindPose(_leftUpperArm);
            CaptureBindPose(_leftLowerArm);
            CaptureBindPose(_rightUpperArm);
            CaptureBindPose(_rightLowerArm);
            CaptureBindPose(_leftUpperLeg);
            CaptureBindPose(_leftLowerLeg);
            CaptureBindPose(_leftFoot);
            CaptureBindPose(_rightUpperLeg);
            CaptureBindPose(_rightLowerLeg);
            CaptureBindPose(_rightFoot);

            _initialized = true;

            // One-shot discovery report so Stepo can see exactly which bones the
            // alias resolver picked up (or didn't). Names come from the rigged GLB
            // — typically SWG-style "root" / "lthigh" / "larm" rather than the
            // canonical Unity Humanoid keys we ask for. SetBone already null-guards
            // so any "<null>" entries below are silently skipped during playback.
            Debug.Log(
                $"[CharacterAnimator] Initialized. Found bones: " +
                $"Hips={Name(_hips)}, Spine={Name(_spine)}, Chest={Name(_chest)}, " +
                $"Neck={Name(_neck)}, Head={Name(_head)}, " +
                $"LUpperArm={Name(_leftUpperArm)}, LLowerArm={Name(_leftLowerArm)}, " +
                $"RUpperArm={Name(_rightUpperArm)}, RLowerArm={Name(_rightLowerArm)}, " +
                $"LUpperLeg={Name(_leftUpperLeg)}, LLowerLeg={Name(_leftLowerLeg)}, LFoot={Name(_leftFoot)}, " +
                $"RUpperLeg={Name(_rightUpperLeg)}, RLowerLeg={Name(_rightLowerLeg)}, RFoot={Name(_rightFoot)}");
        }

        private static string Name(Transform t) => t == null ? "<null>" : t.name;

        private void UpdateState()
        {
            float speed = new Vector2(_velocity.x, _velocity.z).magnitude;

            AnimState newState;
            if (!_grounded)
                newState = _velocity.y > 1f ? AnimState.Jump : AnimState.Fall;
            else if (speed < _profile.WalkThreshold)
                newState = AnimState.Idle;
            else if (_sprinting || speed >= _profile.RunThreshold)
                newState = AnimState.Run;
            else
                newState = AnimState.Walk;

            if (newState != _targetState)
            {
                _targetState = newState;
                _stateBlend = 0f;
            }

            _stateBlend = Mathf.MoveTowards(_stateBlend, 1f, Time.deltaTime * _profile.StateBlendSpeed);
            if (_stateBlend >= 1f)
                _currentState = _targetState;
        }

        private void UpdatePhase()
        {
            float speed = new Vector2(_velocity.x, _velocity.z).magnitude;
            float cycleSpeed = _targetState == AnimState.Run ? _profile.RunCycleSpeed : _profile.WalkCycleSpeed;

            if (_targetState == AnimState.Walk || _targetState == AnimState.Run)
            {
                // Phase advances proportional to speed
                float speedFactor = Mathf.Clamp01(speed / 10f);
                _phase += cycleSpeed * speedFactor * Time.deltaTime;
                if (_phase > 1f) _phase -= 1f;
            }

            if (_targetState == AnimState.Idle)
                _idleTime += Time.deltaTime;
            else
                _idleTime = 0f;
        }

        private void ApplyAnimation()
        {
            // Compute target pose for current blend of states
            float t = _stateBlend;
            AnimState primary = t >= 0.5f ? _targetState : _currentState;

            switch (primary)
            {
                case AnimState.Idle:
                    ApplyIdle();
                    break;
                case AnimState.Walk:
                    ApplyLocomotion(_profile.WalkLegSwing, _profile.KneeBendWalk,
                        _profile.WalkArmSwing, _profile.ElbowBendWalk,
                        _profile.SpineTwist, 0f);
                    break;
                case AnimState.Run:
                    ApplyLocomotion(_profile.RunLegSwing, _profile.KneeBendRun,
                        _profile.RunArmSwing, _profile.ElbowBendRun,
                        _profile.SpineTwist * 1.5f, _profile.RunLean);
                    break;
                case AnimState.Jump:
                case AnimState.Fall:
                    ApplyAirborne();
                    break;
            }
        }

        private void ApplyIdle()
        {
            ProceduralWalkCycle.ComputeIdle(_idleTime, _profile.BreathRate,
                _profile.BreathAmount, _profile.IdleSwayAmount,
                out var chest, out var hips);

            SetBone(_hips, hips);
            SetBone(_chest, chest);
            SetBone(_spine, Quaternion.identity);
            SetBone(_head, Quaternion.identity);
            SetBone(_leftUpperArm, Quaternion.identity);
            SetBone(_leftLowerArm, Quaternion.identity);
            SetBone(_rightUpperArm, Quaternion.identity);
            SetBone(_rightLowerArm, Quaternion.identity);
            SetBone(_leftUpperLeg, Quaternion.identity);
            SetBone(_leftLowerLeg, Quaternion.identity);
            SetBone(_leftFoot, Quaternion.identity);
            SetBone(_rightUpperLeg, Quaternion.identity);
            SetBone(_rightLowerLeg, Quaternion.identity);
            SetBone(_rightFoot, Quaternion.identity);
        }

        private void ApplyLocomotion(float legSwing, float kneeBend,
            float armSwing, float elbowBend, float spineTwist, float lean)
        {
            // Left leg + right arm are in phase, right leg + left arm are offset by 0.5
            ProceduralWalkCycle.ComputeLeg(_phase, legSwing, kneeBend,
                out var lul, out var lll, out var lf);
            ProceduralWalkCycle.ComputeLeg(_phase + 0.5f, legSwing, kneeBend,
                out var rul, out var rll, out var rf);

            // Arms counter-swing (opposite phase to legs on same side)
            ProceduralWalkCycle.ComputeArm(_phase + 0.5f, armSwing, elbowBend,
                out var lua, out var lla);
            ProceduralWalkCycle.ComputeArm(_phase, armSwing, elbowBend,
                out var rua, out var rla);

            ProceduralWalkCycle.ComputeSpine(_phase, spineTwist, lean,
                out var hips, out var spine, out var chest);

            var head = ProceduralWalkCycle.ComputeHead(_phase, spineTwist);

            SetBone(_hips, hips);
            SetBone(_spine, spine);
            SetBone(_chest, chest);
            SetBone(_head, head);
            SetBone(_leftUpperArm, lua);
            SetBone(_leftLowerArm, lla);
            SetBone(_rightUpperArm, rua);
            SetBone(_rightLowerArm, rla);
            SetBone(_leftUpperLeg, lul);
            SetBone(_leftLowerLeg, lll);
            SetBone(_leftFoot, lf);
            SetBone(_rightUpperLeg, rul);
            SetBone(_rightLowerLeg, rll);
            SetBone(_rightFoot, rf);
        }

        private void ApplyAirborne()
        {
            ProceduralWalkCycle.ComputeJump(_velocity.y,
                _profile.JumpCrouch, _profile.FallArmSpread,
                out var ul, out var ll, out var ua, out var sp);

            SetBone(_hips, Quaternion.identity);
            SetBone(_spine, sp);
            SetBone(_chest, Quaternion.identity);
            SetBone(_head, Quaternion.identity);
            SetBone(_leftUpperArm, ua);
            SetBone(_leftLowerArm, Quaternion.identity);
            SetBone(_rightUpperArm, ua);
            SetBone(_rightLowerArm, Quaternion.identity);
            SetBone(_leftUpperLeg, ul);
            SetBone(_leftLowerLeg, ll);
            SetBone(_leftFoot, Quaternion.identity);
            SetBone(_rightUpperLeg, ul);
            SetBone(_rightLowerLeg, ll);
            SetBone(_rightFoot, Quaternion.identity);
        }

        private void SetBone(Transform bone, Quaternion offset)
        {
            if (bone == null) return;
            if (_bindPose.TryGetValue(bone, out var bind))
                bone.localRotation = Quaternion.Slerp(bone.localRotation, bind * offset,
                    Time.deltaTime * _profile.StateBlendSpeed);
        }

        private void CaptureBindPose(Transform bone)
        {
            if (bone != null)
                _bindPose[bone] = bone.localRotation;
        }

        private Transform FindBone(string name)
        {
            var all = GetComponentsInChildren<Transform>(true);

            // First attempt: preserve historical behaviour — exact-match on the requested
            // name. Anything that worked before this PR continues to resolve identically.
            foreach (var t in all)
                if (t.name == name) return t;

            // Second attempt: if the requested name has an alias set, iterate the aliases
            // in priority order and do a case-insensitive match against every descendant.
            if (BoneAliases.TryGetValue(name, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (alias == name) continue; // already tried as exact match
                    foreach (var t in all)
                        if (string.Equals(t.name, alias, System.StringComparison.OrdinalIgnoreCase))
                            return t;
                }
            }

            return null;
        }
    }
}

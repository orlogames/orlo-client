using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Animation states for procedural characters.
    /// </summary>
    public enum AnimationState
    {
        Idle,
        Walk,
        Run,
        Attack,
        Death,
        Emote
    }

    /// <summary>
    /// Direct bone rotation animation without animation clip files.
    /// Uses keyframe data defined as arrays of (boneName, rotation, time) tuples.
    /// Evaluates by finding surrounding keyframes and Quaternion.Slerp interpolation.
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float transitionDuration = 0.25f;
        [SerializeField] private float breathingAmplitude = 2f;
        [SerializeField] private float breathingSpeed = 1.5f;

        private ProceduralCharacter _character;
        private AnimationState _currentState = AnimationState.Idle;
        private AnimationState _previousState = AnimationState.Idle;
        private float _stateTime;
        private float _transitionProgress = 1f; // 1 = fully transitioned
        private float _globalTime;

        // Keyframe storage: state -> list of (boneName, rotation, time)
        private static readonly Dictionary<AnimationState, List<BoneKeyframe>> _keyframes = new();
        private static bool _keyframesInitialized;

        public struct BoneKeyframe
        {
            public string BoneName;
            public Quaternion Rotation;
            public float Time;

            public BoneKeyframe(string bone, float rx, float ry, float rz, float time)
            {
                BoneName = bone;
                Rotation = Quaternion.Euler(rx, ry, rz);
                Time = time;
            }
        }

        // Cached bone transforms for performance
        private readonly Dictionary<string, Transform> _boneCache = new();

        static ProceduralAnimator()
        {
            InitializeKeyframes();
        }

        private static void InitializeKeyframes()
        {
            if (_keyframesInitialized) return;
            _keyframesInitialized = true;

            // ===== IDLE =====
            // Subtle weight shift, duration 3.0s looping
            _keyframes[AnimationState.Idle] = new List<BoneKeyframe>
            {
                // Hips — slight sway
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 1f, 1.5f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, -1f, 3.0f),

                // Spine — slight lean
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -1f, 0, 0, 1.5f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 3.0f),

                // Head — occasional look
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -3f, 5f, 0, 1.0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, -3f, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 3.0f),

                // Arms relaxed
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0, 0, 5f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0, 0, 5f, 3.0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0, 0, -5f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0, 0, -5f, 3.0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -10f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -10f, 0, 0, 3.0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -10f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -10f, 0, 0, 3.0f),

                // Legs straight
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0, 0, 0, 3.0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0, 0, 0, 3.0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0, 0, 0, 3.0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0, 0, 0, 3.0f),
            };

            // ===== WALK =====
            // Standard walk cycle, duration 1.0s looping
            _keyframes[AnimationState.Walk] = new List<BoneKeyframe>
            {
                // Hips — bob and sway
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 2f, 2f, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, -2f, -2f, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 1.0f),

                // Spine counter-rotation
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -2f, -3f, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -2f, 3f, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 1.0f),

                // Left leg — forward stride
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, -25f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0f, 0, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 25f, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0f, 0, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, -25f, 0, 0, 1.0f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 15f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 5f, 0, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0f, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 35f, 0, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 15f, 0, 0, 1.0f),

                // Right leg — opposite phase
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 25f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0f, 0, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, -25f, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0f, 0, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 25f, 0, 0, 1.0f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 35f, 0, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 15f, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 5f, 0, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0f, 0, 0, 1.0f),

                // Arms swing opposite to legs
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 20f, 0, 8f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0f, 0, 5f, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, -20f, 0, 8f, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0f, 0, 5f, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 20f, 0, 8f, 1.0f),

                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -20f, 0, -8f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0f, 0, -5f, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 20f, 0, -8f, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0f, 0, -5f, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -20f, 0, -8f, 1.0f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -20f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -35f, 0, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -20f, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -10f, 0, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -20f, 0, 0, 1.0f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -20f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -10f, 0, 0, 0.25f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -20f, 0, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -35f, 0, 0, 0.75f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -20f, 0, 0, 1.0f),

                // Head stable
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 1.0f),
            };

            // ===== RUN =====
            // Faster walk with more extreme angles, duration 0.6s
            _keyframes[AnimationState.Run] = new List<BoneKeyframe>
            {
                // Hips
                new BoneKeyframe(ProceduralCharacter.BoneHips, -5f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -5f, 3f, 3f, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -5f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -5f, -3f, -3f, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -5f, 0, 0, 0.6f),

                // Spine lean forward
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -8f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -8f, -4f, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -8f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -8f, 4f, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -8f, 0, 0, 0.6f),

                // Left leg — wider stride
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, -40f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0f, 0, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 40f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0f, 0, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, -40f, 0, 0, 0.6f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 20f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 10f, 0, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 50f, 0, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 20f, 0, 0, 0.6f),

                // Right leg
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 40f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0f, 0, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, -40f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0f, 0, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 40f, 0, 0, 0.6f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 50f, 0, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 20f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 10f, 0, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0f, 0, 0, 0.6f),

                // Arms — pumping
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 35f, 0, 10f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0f, 0, 5f, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, -35f, 0, 10f, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0f, 0, 5f, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 35f, 0, 10f, 0.6f),

                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -35f, 0, -10f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0f, 0, -5f, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 35f, 0, -10f, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0f, 0, -5f, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -35f, 0, -10f, 0.6f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -60f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -45f, 0, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -60f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -80f, 0, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -60f, 0, 0, 0.6f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -60f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -80f, 0, 0, 0.15f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -60f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -45f, 0, 0, 0.45f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -60f, 0, 0, 0.6f),

                // Head
                new BoneKeyframe(ProceduralCharacter.BoneHead, -5f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -5f, 0, 0, 0.6f),
            };

            // ===== ATTACK =====
            // Sword swing, duration 0.8s, non-looping
            _keyframes[AnimationState.Attack] = new List<BoneKeyframe>
            {
                // Wind up
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 20f, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -5f, 30f, 0, 0.2f),
                // Strike
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 5f, -20f, 0, 0.4f),
                // Recovery
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 0.8f),

                // Right arm — main hand swing
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -80f, 30f, -30f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -90f, 40f, -20f, 0.2f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -30f, -40f, -10f, 0.4f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0f, 0f, -5f, 0.8f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -90f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -100f, 0, 0, 0.2f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -20f, 0, 0, 0.4f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -10f, 0, 0, 0.8f),

                // Left arm follows slightly
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 10f, 0, 15f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 15f, -10f, 15f, 0.2f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, -5f, 10f, 10f, 0.4f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0f, 0f, 5f, 0.8f),

                // Hips twist
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 10f, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 15f, 0, 0.2f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, -10f, 0, 0.4f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 0.8f),

                // Legs stable with slight lunge
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, -10f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, -15f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0f, 0, 0, 0.8f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 5f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 10f, 0, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0f, 0, 0, 0.8f),

                // Head tracks forward
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -5f, -10f, 0, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 0.8f),
            };

            // ===== DEATH =====
            // Collapse, duration 1.2s, non-looping
            _keyframes[AnimationState.Death] = new List<BoneKeyframe>
            {
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -10f, 0, 15f, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -20f, 0, 40f, 0.6f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -30f, 0, 70f, 0.9f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, -30f, 0, 85f, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -15f, 0, 0, 0.4f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -30f, 0, 0, 0.8f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, -40f, 0, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneHead, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -20f, 10f, 0, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -40f, 15f, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0, 0, 5f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 20f, 0, 30f, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 30f, 0, 60f, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 0, 0, -5f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 15f, 0, -25f, 0.5f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, 25f, 0, -50f, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -10f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -5f, 0, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -10f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, -5f, 0, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 15f, 0, 0, 0.6f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 20f, 0, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 10f, 0, 0, 0.6f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 15f, 0, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 30f, 0, 0, 0.8f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 45f, 0, 0, 1.2f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 25f, 0, 0, 0.8f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 40f, 0, 0, 1.2f),
            };

            // ===== EMOTE =====
            // Wave animation, duration 2.0s looping
            _keyframes[AnimationState.Emote] = new List<BoneKeyframe>
            {
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -150f, 0, -20f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperArm, -150f, 0, -20f, 2.0f),

                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, 15f, 0.3f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, -15f, 0.6f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, 15f, 0.9f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, -15f, 1.2f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, 0f, 1.5f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerArm, 0, 0, 0f, 2.0f),

                // Other bones idle
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHips, 0, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneSpine, 0, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -5f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneHead, -5f, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0, 0, 5f, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperArm, 0, 0, 5f, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -10f, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerArm, -10f, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftUpperLeg, 0, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightUpperLeg, 0, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneLeftLowerLeg, 0, 0, 0, 2.0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0, 0, 0, 0f),
                new BoneKeyframe(ProceduralCharacter.BoneRightLowerLeg, 0, 0, 0, 2.0f),
            };
        }

        private void Start()
        {
            _character = GetComponent<ProceduralCharacter>();
            if (_character == null)
            {
                Debug.LogWarning("[ProceduralAnimator] No ProceduralCharacter found on this GameObject");
                enabled = false;
                return;
            }

            CacheBoneTransforms();
        }

        private void CacheBoneTransforms()
        {
            string[] allBones = {
                ProceduralCharacter.BoneRoot, ProceduralCharacter.BoneHips,
                ProceduralCharacter.BoneSpine, ProceduralCharacter.BoneChest,
                ProceduralCharacter.BoneNeck, ProceduralCharacter.BoneHead,
                ProceduralCharacter.BoneLeftUpperArm, ProceduralCharacter.BoneLeftLowerArm,
                ProceduralCharacter.BoneLeftHand,
                ProceduralCharacter.BoneRightUpperArm, ProceduralCharacter.BoneRightLowerArm,
                ProceduralCharacter.BoneRightHand,
                ProceduralCharacter.BoneLeftUpperLeg, ProceduralCharacter.BoneLeftLowerLeg,
                ProceduralCharacter.BoneLeftFoot,
                ProceduralCharacter.BoneRightUpperLeg, ProceduralCharacter.BoneRightLowerLeg,
                ProceduralCharacter.BoneRightFoot
            };

            foreach (var boneName in allBones)
            {
                var bone = _character.GetBone(boneName);
                if (bone != null)
                    _boneCache[boneName] = bone;
            }
        }

        private void Update()
        {
            if (_character == null) return;

            float dt = Time.deltaTime;
            _stateTime += dt;
            _globalTime += dt;

            // Advance transition
            if (_transitionProgress < 1f)
            {
                _transitionProgress += dt / transitionDuration;
                if (_transitionProgress > 1f) _transitionProgress = 1f;
            }

            // Evaluate animation
            EvaluateAnimation();

            // Always apply breathing overlay
            ApplyBreathing();
        }

        /// <summary>
        /// Set the animation state with smooth transition.
        /// </summary>
        public void SetState(AnimationState state)
        {
            if (state == _currentState) return;

            _previousState = _currentState;
            _currentState = state;
            _stateTime = 0f;
            _transitionProgress = 0f;
        }

        /// <summary>
        /// Get the current animation state.
        /// </summary>
        public AnimationState GetState() => _currentState;

        private void EvaluateAnimation()
        {
            if (!_keyframes.TryGetValue(_currentState, out var currentFrames)) return;

            float duration = GetAnimationDuration(_currentState);
            bool loops = IsLooping(_currentState);

            float currentTime;
            if (loops)
            {
                currentTime = duration > 0 ? _stateTime % duration : 0f;
            }
            else
            {
                currentTime = Mathf.Min(_stateTime, duration);
            }

            // Group keyframes by bone
            var currentPoses = EvaluateKeyframes(currentFrames, currentTime);

            // If transitioning, blend with previous state
            if (_transitionProgress < 1f && _keyframes.TryGetValue(_previousState, out var prevFrames))
            {
                float prevDuration = GetAnimationDuration(_previousState);
                float prevTime = prevDuration > 0 ? _stateTime % prevDuration : 0f;
                var prevPoses = EvaluateKeyframes(prevFrames, prevTime);

                // Blend
                foreach (var kv in currentPoses)
                {
                    if (_boneCache.TryGetValue(kv.Key, out var bone))
                    {
                        Quaternion prevRot = prevPoses.TryGetValue(kv.Key, out var pr) ? pr : Quaternion.identity;
                        bone.localRotation = Quaternion.Slerp(prevRot, kv.Value, _transitionProgress);
                    }
                }

                // Apply any bones only in previous state
                foreach (var kv in prevPoses)
                {
                    if (!currentPoses.ContainsKey(kv.Key) && _boneCache.TryGetValue(kv.Key, out var bone))
                    {
                        bone.localRotation = Quaternion.Slerp(kv.Value, Quaternion.identity, _transitionProgress);
                    }
                }
            }
            else
            {
                // Apply directly
                foreach (var kv in currentPoses)
                {
                    if (_boneCache.TryGetValue(kv.Key, out var bone))
                    {
                        bone.localRotation = kv.Value;
                    }
                }
            }
        }

        private Dictionary<string, Quaternion> EvaluateKeyframes(List<BoneKeyframe> keyframes, float time)
        {
            var result = new Dictionary<string, Quaternion>();

            // Group by bone name and find surrounding keyframes
            // Since keyframes are sorted by bone then time in our definition,
            // we iterate and track per bone
            var boneFrames = new Dictionary<string, List<BoneKeyframe>>();
            foreach (var kf in keyframes)
            {
                if (!boneFrames.TryGetValue(kf.BoneName, out var list))
                {
                    list = new List<BoneKeyframe>();
                    boneFrames[kf.BoneName] = list;
                }
                list.Add(kf);
            }

            foreach (var kv in boneFrames)
            {
                var frames = kv.Value;
                if (frames.Count == 0) continue;

                if (frames.Count == 1)
                {
                    result[kv.Key] = frames[0].Rotation;
                    continue;
                }

                // Find surrounding keyframes
                int nextIdx = -1;
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].Time >= time)
                    {
                        nextIdx = i;
                        break;
                    }
                }

                if (nextIdx <= 0)
                {
                    // Before first or at first keyframe
                    result[kv.Key] = frames[0].Rotation;
                }
                else if (nextIdx < 0)
                {
                    // Past last keyframe
                    result[kv.Key] = frames[frames.Count - 1].Rotation;
                }
                else
                {
                    var prev = frames[nextIdx - 1];
                    var next = frames[nextIdx];
                    float range = next.Time - prev.Time;
                    float t = range > 0 ? (time - prev.Time) / range : 0f;
                    result[kv.Key] = Quaternion.Slerp(prev.Rotation, next.Rotation, t);
                }
            }

            return result;
        }

        private void ApplyBreathing()
        {
            if (!_boneCache.TryGetValue(ProceduralCharacter.BoneChest, out var chest)) return;

            // Don't breathe if dead
            if (_currentState == AnimationState.Death && _stateTime > 1.2f) return;

            float breathAngle = Mathf.Sin(_globalTime * breathingSpeed * Mathf.PI * 2f) * breathingAmplitude;
            chest.localRotation *= Quaternion.Euler(breathAngle, 0, 0);
        }

        private float GetAnimationDuration(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle: return 3.0f;
                case AnimationState.Walk: return 1.0f;
                case AnimationState.Run: return 0.6f;
                case AnimationState.Attack: return 0.8f;
                case AnimationState.Death: return 1.2f;
                case AnimationState.Emote: return 2.0f;
                default: return 1.0f;
            }
        }

        private bool IsLooping(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Attack:
                case AnimationState.Death:
                    return false;
                default:
                    return true;
            }
        }
    }
}

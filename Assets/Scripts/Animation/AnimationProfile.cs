namespace Orlo.Animation
{
    /// <summary>
    /// Tuning parameters for procedural animation. Different profiles for
    /// humanoids, quadrupeds, etc.
    /// </summary>
    public class AnimationProfile
    {
        // Walk cycle
        public float WalkCycleSpeed = 3.5f;       // Cycles per second at walk speed
        public float RunCycleSpeed = 5.0f;         // Cycles per second at run speed
        public float WalkThreshold = 0.3f;         // Min velocity to trigger walk
        public float RunThreshold = 7.0f;          // Velocity to trigger run

        // Leg swing (degrees)
        public float WalkLegSwing = 30f;
        public float RunLegSwing = 50f;
        public float KneeBendWalk = 20f;
        public float KneeBendRun = 35f;

        // Arm swing (degrees)
        public float WalkArmSwing = 20f;
        public float RunArmSwing = 40f;
        public float ElbowBendWalk = 10f;
        public float ElbowBendRun = 25f;

        // Spine
        public float SpineTwist = 4f;              // Degrees of counter-rotation
        public float RunLean = 10f;                 // Forward lean when running
        public float HeadBobAmount = 0.02f;         // Meters of vertical bob

        // Breathing (idle)
        public float BreathRate = 0.8f;             // Cycles per second
        public float BreathAmount = 1.5f;           // Degrees of chest expansion
        public float IdleSwayAmount = 1.0f;         // Degrees of hip sway

        // Jump
        public float JumpCrouch = 20f;              // Leg bend on launch
        public float FallArmSpread = 30f;           // Arm spread in air

        // Blending
        public float StateBlendSpeed = 8f;          // Lerp speed between states

        public static readonly AnimationProfile Humanoid = new();
    }
}

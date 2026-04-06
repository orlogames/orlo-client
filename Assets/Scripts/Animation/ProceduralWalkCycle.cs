using UnityEngine;

namespace Orlo.Animation
{
    /// <summary>
    /// Pure math functions for humanoid walk/run cycle.
    /// Phase is 0-1 per full stride (left foot forward to right foot forward and back).
    /// All rotations are local-space offsets from bind pose.
    /// </summary>
    public static class ProceduralWalkCycle
    {
        /// <summary>Compute leg rotations for one side. phase=0: leg forward, phase=0.5: leg back.</summary>
        public static void ComputeLeg(float phase, float swingDeg, float kneeDeg,
            out Quaternion upperLeg, out Quaternion lowerLeg, out Quaternion foot)
        {
            float swing = Mathf.Sin(phase * Mathf.PI * 2f) * swingDeg;
            // Knee bends when leg is behind (positive swing = forward, negative = behind)
            float knee = Mathf.Max(0f, -Mathf.Sin(phase * Mathf.PI * 2f)) * kneeDeg;
            // Foot stays level-ish
            float footAngle = -swing * 0.3f;

            upperLeg = Quaternion.Euler(swing, 0, 0);
            lowerLeg = Quaternion.Euler(knee, 0, 0);
            foot = Quaternion.Euler(footAngle, 0, 0);
        }

        /// <summary>Compute arm swing (opposite to leg). phase matches the opposite leg.</summary>
        public static void ComputeArm(float phase, float swingDeg, float elbowDeg,
            out Quaternion upperArm, out Quaternion lowerArm)
        {
            float swing = Mathf.Sin(phase * Mathf.PI * 2f) * swingDeg;
            float elbow = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * elbowDeg;

            upperArm = Quaternion.Euler(swing, 0, 0);
            lowerArm = Quaternion.Euler(-elbow, 0, 0);
        }

        /// <summary>Compute spine counter-rotation and forward lean.</summary>
        public static void ComputeSpine(float phase, float twistDeg, float leanDeg,
            out Quaternion hips, out Quaternion spine, out Quaternion chest)
        {
            float twist = Mathf.Sin(phase * Mathf.PI * 2f) * twistDeg;

            hips = Quaternion.Euler(0, -twist, 0);
            spine = Quaternion.Euler(leanDeg * 0.5f, twist * 0.5f, 0);
            chest = Quaternion.Euler(leanDeg * 0.5f, twist * 0.5f, 0);
        }

        /// <summary>Head stabilization — counters spine twist.</summary>
        public static Quaternion ComputeHead(float phase, float twistDeg)
        {
            float counterTwist = -Mathf.Sin(phase * Mathf.PI * 2f) * twistDeg;
            return Quaternion.Euler(0, counterTwist, 0);
        }

        /// <summary>Idle breathing and weight shift.</summary>
        public static void ComputeIdle(float time, float breathRate, float breathDeg, float swayDeg,
            out Quaternion chest, out Quaternion hips)
        {
            float breathPhase = time * breathRate * Mathf.PI * 2f;
            float swayPhase = time * breathRate * 0.5f * Mathf.PI * 2f;

            chest = Quaternion.Euler(-Mathf.Sin(breathPhase) * breathDeg, 0, 0);
            hips = Quaternion.Euler(0, 0, Mathf.Sin(swayPhase) * swayDeg);
        }

        /// <summary>Jump pose — crouch on launch, spread in air.</summary>
        public static void ComputeJump(float verticalVelocity, float crouchDeg, float armSpreadDeg,
            out Quaternion upperLeg, out Quaternion lowerLeg,
            out Quaternion upperArm, out Quaternion spine)
        {
            // Rising: legs tucked, arms up. Falling: legs dangling, arms spread
            float t = Mathf.Clamp01(verticalVelocity / 8f); // 0=falling, 1=rising
            float legBend = Mathf.Lerp(5f, crouchDeg, t);
            float armAngle = Mathf.Lerp(armSpreadDeg, -20f, t);

            upperLeg = Quaternion.Euler(-legBend, 0, 0);
            lowerLeg = Quaternion.Euler(legBend * 1.5f, 0, 0);
            upperArm = Quaternion.Euler(armAngle, 0, 0);
            spine = Quaternion.Euler(Mathf.Lerp(0, -10f, t), 0, 0);
        }
    }
}

using UnityEngine;

namespace OddsCab
{
    /// <summary>Distance-driven biped gait. A stopped path does not continue to tread in place.</summary>
    public static class PassengerLocomotion
    {
        public const float StrideMetres = 0.96f;

        public static float Phase(float distance, float offset)
        {
            return distance * (Mathf.PI * 2f / StrideMetres) + offset;
        }

        public static void Legs(float phase, float amplitude, out float leftHip, out float rightHip,
            out float leftKnee, out float rightKnee)
        {
            float swing = Mathf.Sin(phase), passing = Mathf.Cos(phase);
            leftHip = -swing * amplitude;
            rightHip = swing * amplitude;
            leftKnee = 3f + 42f * Mathf.Max(0f, passing);
            rightKnee = 3f + 42f * Mathf.Max(0f, -passing);
        }

        public static float FarewellWeight(float seconds)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((seconds - 0.18f) / 0.95f));
        }
    }
}

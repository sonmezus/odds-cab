using UnityEngine;

namespace OddsCab
{
    /// <summary>Portrait composition leaves the face above the question card while retaining shoulders and green seats.</summary>
    public static class TaxiCameraFraming
    {
        public const float CabFieldOfView = 34f;

        /// <param name="eye">Cached eye midpoint in taxi-local metres.</param>
        public static void SolveCab(Vector3 eye, float pullback, out Vector3 camera, out Vector3 focus)
        {
            float scale = Mathf.Clamp(pullback, 1f, 1.08f);
            float distance = 1.60f * scale;
            camera = eye + new Vector3(-0.035f, 0.015f, distance);
            focus = eye - Vector3.up * (0.25f * scale);
        }

        /// <summary>Square result portrait crops below the chest while keeping room above hair and hats.</summary>
        public static void SolveLivePortrait(Vector3 eye, out Vector3 camera, out Vector3 focus)
        {
            camera = eye + new Vector3(-.035f, .015f, 1.04f);
            focus = eye - Vector3.up * .03f;
        }
    }
}

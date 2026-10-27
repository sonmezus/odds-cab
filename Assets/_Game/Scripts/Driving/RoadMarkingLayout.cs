using UnityEngine;

namespace OddsCab
{
    /// <summary>One geometry contract shared by longitudinal paint and pedestrian crossings.</summary>
    public static class RoadMarkingLayout
    {
        public const int CrossingVariant = 3;
        public const int StripeCount = 6;
        public const float StripeSpacing = 1f, StripeLength = .55f, DashLength = 3f, PaintClearance = .55f;

        public static float StripeCenter(float chunkLength, int stripe) => chunkLength * .5f + (stripe - 2.5f) * StripeSpacing;
        public static bool DashClearsCrossing(int variant, float chunkLength, float dashCenter)
        {
            if (variant != CrossingVariant) return true;
            float first = StripeCenter(chunkLength, 0) - StripeLength * .5f - PaintClearance;
            float last = StripeCenter(chunkLength, StripeCount - 1) + StripeLength * .5f + PaintClearance;
            return dashCenter + DashLength * .5f <= first || dashCenter - DashLength * .5f >= last;
        }
    }
}

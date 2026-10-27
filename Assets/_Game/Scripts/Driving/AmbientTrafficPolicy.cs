using UnityEngine;

namespace OddsCab
{
    /// <summary>Route-space traffic with an explicit clear zone around the player's small taxi.</summary>
    public static class AmbientTrafficPolicy
    {
        public const float LaneCenter = 1.30f;
        public const float RecycleBehind = -38f;
        public const float RecycleAhead = 188f;

        public static int Population(int district, bool night, bool low)
        {
            int cap = district == 1 || district == 4 || district == 5 ? 3 : 6;
            if (low) cap = Mathf.Min(cap, 3);
            return night ? Mathf.Max(2, cap - 2) : cap;
        }

        public static bool BusAllowed(int district) => district == 0 || district == 2 || district == 3 || district == 6 || district == 7;
        public static float SafeLeadGap(float vehicleLength) => 12f + (3f + vehicleLength) * .5f;

        public static float LeadSpeed(float relativeZ, float taxiSpeed, float cruisingSpeed, float vehicleLength)
        {
            float buffer = SafeLeadGap(vehicleLength) + 15f;
            return relativeZ < buffer ? Mathf.Max(cruisingSpeed, taxiSpeed + (buffer - relativeZ) * .50f + 1.2f) : cruisingSpeed;
        }

        public static float KeepLeadClear(float z, float taxiZ, float vehicleLength)
            => Mathf.Max(z, taxiZ + SafeLeadGap(vehicleLength));

        public static float SpawnAhead(float furthestZ, float taxiZ, float vehicleLength)
            => Mathf.Min(taxiZ + RecycleAhead - 2f, Mathf.Max(taxiZ + 148f, furthestZ + 13f + vehicleLength));
    }
}

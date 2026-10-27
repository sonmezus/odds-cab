using UnityEngine;

namespace OddsCab
{
    /// <summary>Pure bounded motion rules shared by the street-life pools and regression checks.</summary>
    public static class DistrictLifeMotion
    {
        // Put a shopkeeper in the low-tier pool too; keep the complete eight-person cast in higher tiers.
        static readonly int[] CitizenIdentities = { 0, 7, 1, 3, 2, 5, 6, 4, 1, 7, 3, 6 };
        public static int CitizenIdentityForSlot(int slot) => CitizenIdentities[Mathf.Clamp(slot, 0, CitizenIdentities.Length - 1)];
        public static bool IsShopkeeper(int identity) => identity == 5 || identity == 7;

        public static float RecycleZ(float current, float taxi, int slot)
        {
            return current < taxi - 18f || current > taxi + 150f ? taxi + 35f + slot * 7.2f : current;
        }

        public static Vector3 CurveWorldPosition(Vector3 point, Vector4 origin, Vector4 amount)
        {
            float distance = Mathf.Max(0f, point.z - origin.z - origin.w);
            float near = Mathf.Min(distance, 90f);
            float bend = near * near + 180f * (distance - near);
            point.x += amount.x * bend;
            point.y += amount.y * bend;
            return point;
        }

        public static void FishArc(float progress, float height, float distance, out Vector3 point, out Vector3 tangent)
        {
            float u = Mathf.Clamp01(progress);
            float angle = u * Mathf.PI;
            point = new Vector3(distance * u * .45f, Mathf.Sin(angle) * height, distance * u);
            tangent = new Vector3(distance * .45f, Mathf.Cos(angle) * Mathf.PI * height, distance);
        }

        public static int CitizenCount(int district, bool night, int tier)
        {
            int desired;
            switch (district)
            {
                case 1: desired = night ? 4 : 12; break;
                case 2: desired = night ? 2 : 6; break;
                case 3: desired = night ? 3 : 10; break;
                case 4: desired = night ? 2 : 7; break;
                case 5: desired = night ? 3 : 10; break;
                case 6: desired = night ? 2 : 6; break;
                case 7: desired = night ? 4 : 9; break;
                default: desired = night ? 2 : 8; break;
            }
            return Mathf.Min(desired, tier <= 0 ? 5 : tier == 1 ? 8 : 12);
        }
    }
}

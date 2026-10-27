namespace OddsCab
{
    /// <summary>Global tuning constants for ODDS CAB.</summary>
    public static class GameConfig
    {
        public const int TotalStops = 512;
        public const int StopsPerDistrict = 64;
        public const int DistrictCount = TotalStops / StopsPerDistrict;
        public const int MiniatureEvery = 8;

        // World
        public const float ChunkLength = 48f;
        public const int ChunksAhead = 6;
        public const int ChunksBehind = 1;
        public const float StopSpacing = 150f;
        /// <summary>Distance driven from leaving one stop to the next curb; 120 m gives a roughly 10 s coastal drive at cruise.</summary>
        public const float StopApproachDistance = 120f;
        public const float RoadHalfWidth = 2.8f;   // narrow two-lane coastal road (reference); was 4
        // Taxi lane centres (x): cruising in the right lane, pulled in to the curb at a stop
        public const float DriveLaneX = 1.3f;
        public static readonly float CurbStopX = SideX(-1.5f);     // leaves room for the curb door to swing open past the bollards
        public const float SidewalkWidth = 3.5f;

        /// <summary>
        /// Side of the curb stops and the Bosphorus promenade: +1 = right, as on the Karaköy → Ortaköy coast road
        /// (Turkey drives on the right, left-hand-drive taxi). The street was first laid out with them on the left
        /// (-1); CityChunkStreamer mirrors that layout and everything placed beside it goes through SideX/SideYaw.
        /// </summary>
        public const float CurbSide = 1f;
        /// <summary>World X of a point authored for the original left-curb layout.</summary>
        public static float SideX(float leftLayoutX) => -CurbSide * leftLayoutX;
        /// <summary>Yaw (deg) of a heading authored for the original left-curb layout.</summary>
        public static float SideYaw(float leftLayoutYaw) => -CurbSide * leftLayoutYaw;
        public static UnityEngine.Vector3 SideV(UnityEngine.Vector3 leftLayout) => new UnityEngine.Vector3(SideX(leftLayout.x), leftLayout.y, leftLayout.z);

        // Taxi speeds (m/s). 12 m/s ~= 43 km/h
        public const float CruiseSpeed = 12f;
        public const float PuzzleCruiseSpeed = 5f;
        public const float SlowSpeed = 2.5f;
        public const float BoostSpeed = 17f;
        public const float Acceleration = 4f;
        public const float Braking = 5f;

        // Economy
        // Tuned so a full campaign earns roughly what the garage and passenger stories cost in total
        // (was 10 / 15 / 20 with a 12-coin streak bonus: ~17,700 coins and nothing left to spend them on).
        public const int BaseReward = 6;
        public const int MaxTimeBonus = 8;
        public const int MaxStreakBonus = 6;
        public const int HintCost = 25;
        public const int StartCoins = 60;
    }
}

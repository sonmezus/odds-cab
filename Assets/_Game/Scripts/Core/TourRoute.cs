using System.Collections.Generic;
using UnityEngine;

namespace OddsCab
{
    /// <summary>What the street of a district looks like (environment step of the tour plan).</summary>
    public enum TourRoadType { Coastal, Bazaar, Bridge, Hill, Neighbourhood }

    /// <summary>
    /// The real Istanbul tour the taxi drives, visiting the 8 districts in game order. The districts lie on both
    /// sides of the Bosphorus, so three legs cross on the Harem–Sirkeci car ferry; the last leg is the coast road
    /// Karaköy → Kabataş → Beşiktaş → Ortaköy. Points are (latitude, longitude) read off the map.
    /// </summary>
    public static class TourRoute
    {
        public struct Leg
        {
            /// <summary>Waypoints from the previous district to this one; a ferry leg is a straight crossing.</summary>
            public Vector2[] path;
            public bool ferry;
        }

        public struct District
        {
            public Vector2 pin;
            public TourRoadType road;
            /// <summary>Side of the water when driving the district's street: +1 right, -1 left, 0 none (inland).</summary>
            public int seaSide;
            /// <summary>Legs driven to reach this district from the previous one (empty for the start).</summary>
            public Leg[] approach;
        }

        static Leg Drive(params Vector2[] p) => new Leg { path = p };
        static Leg Ferry(Vector2 from, Vector2 to) => new Leg { path = new[] { from, to }, ferry = true };
        static Vector2 P(float lat, float lon) => new Vector2(lat, lon);

        // Piers and junctions used by several legs
        static readonly Vector2 Harem = P(41.0128f, 29.0098f), Sirkeci = P(41.0158f, 28.9795f);
        static readonly Vector2 Eminonu = P(41.0172f, 28.9728f), Karakoy = P(41.0228f, 28.9768f);

        public static readonly District[] Districts =
        {
            // 1 Kız Kulesi: Salacak shore road, the tower just offshore; south to Harem, the sea on the right
            new District { pin = P(41.0222f, 29.0071f), road = TourRoadType.Coastal, seaSide = 1, approach = new Leg[0] },
            // 2 Mısır Çarşısı: car ferry Harem -> Sirkeci, then the bazaar streets of Eminönü
            new District { pin = P(41.0166f, 28.9706f), road = TourRoadType.Bazaar, seaSide = 0, approach = new[] {
                Drive(P(41.0222f, 29.0071f), P(41.0190f, 29.0074f), P(41.0155f, 29.0085f), Harem),
                Ferry(Harem, Sirkeci),
                Drive(Sirkeci, P(41.0168f, 28.9760f), Eminonu, P(41.0166f, 28.9706f)) } },
            // 3 Galata Köprüsü: across the Golden Horn, water on both sides
            new District { pin = P(41.0201f, 28.9734f), road = TourRoadType.Bridge, seaSide = 0, approach = new[] {
                Drive(P(41.0166f, 28.9706f), Eminonu, P(41.0201f, 28.9734f)) } },
            // 4 Karaköy Rıhtımı: the quay east of the bridge, the Bosphorus on the right
            new District { pin = P(41.0240f, 28.9792f), road = TourRoadType.Coastal, seaSide = 1, approach = new[] {
                Drive(P(41.0201f, 28.9734f), Karakoy, P(41.0240f, 28.9792f)) } },
            // 5 Galata Kulesi: up the cobbled hill, the sea behind and below
            new District { pin = P(41.0256f, 28.9741f), road = TourRoadType.Hill, seaSide = 0, approach = new[] {
                Drive(P(41.0240f, 28.9792f), P(41.0246f, 28.9768f), P(41.0251f, 28.9752f), P(41.0256f, 28.9741f)) } },
            // 6 Yeldeğirmeni: back down, ferry Sirkeci -> Harem, south past Haydarpaşa into Kadıköy's streets
            new District { pin = P(40.9978f, 29.0275f), road = TourRoadType.Neighbourhood, seaSide = 0, approach = new[] {
                Drive(P(41.0256f, 28.9741f), Karakoy, P(41.0201f, 28.9734f), Eminonu, Sirkeci),
                Ferry(Sirkeci, Harem),
                Drive(Harem, P(41.0060f, 29.0140f), P(41.0010f, 29.0185f), P(40.9978f, 29.0275f)) } },
            // 7 Haliç Müzesi: ferry back, over the Atatürk Bridge to Kasımpaşa, along the Golden Horn (water on the left)
            new District { pin = P(41.0418f, 28.9492f), road = TourRoadType.Coastal, seaSide = -1, approach = new[] {
                Drive(P(40.9978f, 29.0275f), P(41.0010f, 29.0185f), P(41.0060f, 29.0140f), Harem),
                Ferry(Harem, Sirkeci),
                Drive(Sirkeci, Eminonu, P(41.0205f, 28.9620f), P(41.0258f, 28.9652f), P(41.0335f, 28.9640f),
                      P(41.0380f, 28.9560f), P(41.0418f, 28.9492f)) } },
            // 8 Ortaköy: Kasımpaşa -> Karaköy, then the coast road (the Google Maps route), the Bosphorus on the right
            new District { pin = P(41.0474f, 29.0266f), road = TourRoadType.Coastal, seaSide = 1, approach = new[] {
                Drive(P(41.0418f, 28.9492f), P(41.0380f, 28.9560f), P(41.0335f, 28.9640f), P(41.0270f, 28.9705f),
                      Karakoy, P(41.0250f, 28.9810f), P(41.0272f, 28.9845f), P(41.0330f, 28.9928f), P(41.0388f, 28.9995f),
                      P(41.0420f, 29.0065f), P(41.0444f, 29.0150f), P(41.0474f, 29.0266f)) } },
        };

        // ---------------------------------------------------------------- water, for the tour map (stylised)

        /// <summary>Sea of Marmara as a polygon (star-shaped around its first point).</summary>
        public static readonly Vector2[] Marmara =
        {
            P(40.990f, 28.990f),
            P(41.0158f, 28.9862f), P(41.0105f, 28.9800f), P(41.0050f, 28.9700f), P(41.0010f, 28.9550f), P(40.9980f, 28.9350f),
            P(40.9700f, 28.9350f), P(40.9700f, 29.0280f), P(40.9830f, 29.0250f), P(40.9900f, 29.0220f),
            P(40.9985f, 29.0170f), P(41.0080f, 29.0110f), P(41.0160f, 29.0080f), P(41.0215f, 29.0062f),
        };

        /// <summary>Bosphorus centre line south -> north with its width (km) at each point.</summary>
        public static readonly Vector3[] Bosphorus =
        {
            new Vector3(41.012f, 28.996f, 2.6f), new Vector3(41.022f, 28.993f, 2.2f), new Vector3(41.029f, 28.997f, 2.0f),
            new Vector3(41.036f, 29.006f, 1.8f), new Vector3(41.043f, 29.020f, 1.5f), new Vector3(41.050f, 29.037f, 1.3f),
            new Vector3(41.060f, 29.048f, 1.3f),
        };

        /// <summary>Golden Horn centre line from its mouth inland, with width (km).</summary>
        public static readonly Vector3[] GoldenHorn =
        {
            new Vector3(41.0185f, 28.9790f, 0.75f), new Vector3(41.0215f, 28.9700f, 0.45f), new Vector3(41.0250f, 28.9620f, 0.42f),
            new Vector3(41.0310f, 28.9550f, 0.40f), new Vector3(41.0375f, 28.9500f, 0.38f), new Vector3(41.0430f, 28.9420f, 0.35f),
            new Vector3(41.0460f, 28.9330f, 0.30f),
        };

        public const float MinLat = 40.990f, MaxLat = 41.052f, MinLon = 28.935f, MaxLon = 29.045f;
        const float KmPerLat = 111.0f, KmPerLon = 84.0f;   // at 41° N

        /// <summary>Kilometres east / north of the map's south-west corner.</summary>
        public static Vector2 ToKm(Vector2 latLon) => new Vector2((latLon.y - MinLon) * KmPerLon, (latLon.x - MinLat) * KmPerLat);
        public static Vector2 SizeKm => new Vector2((MaxLon - MinLon) * KmPerLon, (MaxLat - MinLat) * KmPerLat);

        /// <summary>The full tour as one polyline in km, each point tagged with the district it leads to and whether it is a ferry crossing.</summary>
        public static List<(Vector2 km, int district, bool ferry)> Polyline()
        {
            var pts = new List<(Vector2, int, bool)>();
            for (int d = 0; d < Districts.Length; d++)
            {
                if (d == 0) pts.Add((ToKm(Districts[0].pin), 0, false));
                foreach (var leg in Districts[d].approach)
                    for (int i = 1; i < leg.path.Length; i++)
                        pts.Add((ToKm(leg.path[i]), d, leg.ferry));
            }
            return pts;
        }

        /// <summary>True when the ferry to <paramref name="district"/> leaves from the Asian side (Harem), else from Sirkeci.</summary>
        public static bool FerryFromAsia(int district)
        {
            foreach (var leg in Districts[district].approach) if (leg.ferry) return leg.path[0] == Harem;
            return false;
        }

        /// <summary>True when the approach to <paramref name="district"/> includes a car-ferry crossing.</summary>
        public static bool ApproachHasFerry(int district)
        {
            foreach (var leg in Districts[district].approach) if (leg.ferry) return true;
            return false;
        }
    }
}

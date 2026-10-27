using System.Collections.Generic;
using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab
{
    /// <summary>
    /// Curved road ("curved world"): the street ahead bends into the real route's turns and climbs the Galata hill,
    /// while gameplay keeps driving a straight line. The bend happens in the vertex shader (Resources/Shaders/
    /// CurvedLit.shader, URP Lit + CurvedWorld.hlsl); this component feeds it the amount each frame and swaps the
    /// materials of the street, its passengers and landmarks onto that shader (<see cref="Convert"/>). The horizon
    /// vista, the taxi and UI keep their own materials and never bend.
    /// </summary>
    public class CurveBender : MonoBehaviour
    {
        const float StraightAhead = 28f;      // no bend this close in front of the taxi (stops, passengers, cameras)
        const float Exaggeration = 14f;        // real turns spread over a 9.6 km district would barely show
        const float MaxSideK = 0.0022f;       // ~7 m sideways at the end of the 112 m view
        const float HillRiseK = 0.0011f;      // Galata: the street climbs ~3.5 m by the end of the view
        const float CurvatureWindow = 0.08f;  // fraction of the district's road the turn rate is measured over

        static readonly int CurveOriginId = Shader.PropertyToID("_CurveOrigin");
        static readonly int CurveAmountId = Shader.PropertyToID("_CurveAmount");

        static Shader s_curved;
        static readonly Dictionary<Material, Material> s_map = new Dictionary<Material, Material>();
        static readonly List<(Material src, Material dst)> s_pairs = new List<(Material, Material)>();

        Transform _taxi;
        Vector2 _amount;
        float _syncTimer;
        static Vector2[][] s_roads;

        public static Shader CurvedShader => s_curved != null ? s_curved : (s_curved = Shader.Find("OddsCab/CurvedLit"));

        /// <summary>Test hook: forces the bend (sideways, rise), applied at once.</summary>
        public static Vector2? Override;

        public void Init(Transform taxi)
        {
            _taxi = taxi;
            Road(0); // Prepare the route samples before the first frame.
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(CurveAmountId, Vector4.zero);
        }

        void Update()
        {
            if (_taxi == null) return;
            Vector2 target = TargetAmount();
            // ease towards the new bend so the road never snaps between stops or districts
            _amount = Override ?? Vector2.MoveTowards(_amount, target, Time.deltaTime * 0.0006f);
            Shader.SetGlobalVector(CurveOriginId, new Vector4(_taxi.position.x, _taxi.position.y, _taxi.position.z, StraightAhead));
            Shader.SetGlobalVector(CurveAmountId, new Vector4(_amount.x, _amount.y, 0f, 0f));

            // Keep the bent copies in step with their source materials (district tints, night lamps, wet roads);
            // texture scrolling (the sea) every frame, everything else once a second
            _syncTimer -= Time.deltaTime;
            bool full = _syncTimer <= 0f;
            if (full) _syncTimer = 1f;
            for (int i = 0; i < s_pairs.Count; i++)
            {
                var (src, dst) = s_pairs[i];
                if (src == null || dst == null) continue;
                if (full) CopyLit(src, dst);
                else if (src.mainTextureOffset != dst.mainTextureOffset) dst.mainTextureOffset = src.mainTextureOffset;
            }
        }

        Vector2 TargetAmount()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State == GameState.FerryCrossing) return Vector2.zero;
            int stop = Mathf.Clamp(SaveSystem.Data != null ? SaveSystem.Data.currentStop : 1, 1, GameConfig.TotalStops);
            int district = StopPlanner.DistrictIndex(stop);
            float f = (stop - (district * GameConfig.StopsPerDistrict + 1) + 0.5f) / GameConfig.StopsPerDistrict;

            // turn rate of the real road here (rad per metre of game road), half of it is the parabola's k
            float turn = TurnPerFraction(district, f);
            float k = 0.5f * turn / (GameConfig.StopsPerDistrict * GameConfig.StopSpacing) * Exaggeration;
            // a gentle meander on long straights, so no stretch looks ruler-straight
            k += 0.0009f * Mathf.Sin(_taxi.position.z / 380f);
            float rise = CityChunkStreamer.CurrentRoad == TourRoadType.Hill ? HillRiseK : 0f;
            return new Vector2(Mathf.Clamp(k, -MaxSideK, MaxSideK), rise);
        }

        // ---------------------------------------------------------------- route curvature

        /// <summary>Road driven in district d, in km: the approach legs into it on land (district 0 uses its way out to Harem).</summary>
        static Vector2[] Road(int d)
        {
            if (s_roads == null)
            {
                s_roads = new Vector2[TourRoute.Districts.Length][];
                for (int i = 0; i < s_roads.Length; i++)
                {
                    var pts = new List<Vector2>();
                    var legs = i == 0 ? new[] { TourRoute.Districts[1].approach[0] } : TourRoute.Districts[i].approach;
                    foreach (var leg in legs)
                    {
                        if (leg.ferry) { pts.Clear(); continue; }   // the street starts where the ferry lands
                        foreach (var p in leg.path)
                        {
                            var km = TourRoute.ToKm(p);
                            if (pts.Count == 0 || (pts[pts.Count - 1] - km).sqrMagnitude > 1e-6f) pts.Add(km);
                        }
                    }
                    s_roads[i] = pts.ToArray();
                }
            }
            return s_roads[Mathf.Clamp(d, 0, s_roads.Length - 1)];
        }

        static Vector2 PointAt(Vector2[] road, float f)
        {
            if (road.Length < 2) return road.Length == 1 ? road[0] : Vector2.zero;
            float total = 0f;
            for (int i = 1; i < road.Length; i++) total += (road[i] - road[i - 1]).magnitude;
            float target = Mathf.Clamp01(f) * total;
            for (int i = 1; i < road.Length; i++)
            {
                float seg = (road[i] - road[i - 1]).magnitude;
                if (target <= seg) return Vector2.Lerp(road[i - 1], road[i], seg > 0f ? target / seg : 0f);
                target -= seg;
            }
            return road[road.Length - 1];
        }

        /// <summary>Signed heading change per unit of district fraction at f (+ = turning right / clockwise).</summary>
        static float TurnPerFraction(int district, float f)
        {
            var road = Road(district);
            if (road.Length < 3) return 0f;
            Vector2 a = PointAt(road, f - CurvatureWindow), b = PointAt(road, f), c = PointAt(road, f + CurvatureWindow);
            Vector2 d1 = b - a, d2 = c - b;
            if (d1.sqrMagnitude < 1e-8f || d2.sqrMagnitude < 1e-8f) return 0f;
            // compass headings (x = east, y = north): clockwise is a right turn
            float h1 = Mathf.Atan2(d1.x, d1.y), h2 = Mathf.Atan2(d2.x, d2.y);
            return Mathf.DeltaAngle(h1 * Mathf.Rad2Deg, h2 * Mathf.Rad2Deg) * Mathf.Deg2Rad / CurvatureWindow;
        }

        // ---------------------------------------------------------------- materials

        /// <summary>Puts every mesh under <paramref name="root"/> on the bending shader (shared copies per source material).</summary>
        public static void Convert(Transform root)
        {
            if (root == null || CurvedShader == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var c = Curved(mats[i]);
                    if (c != mats[i]) { mats[i] = c; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
            WorldRayTracingScene.Instance?.RegisterRoot(root);
        }

        static Material Curved(Material src)
        {
            if (src == null || src.shader == CurvedShader) return src;
            if (s_map.TryGetValue(src, out var dst)) return dst;
            dst = src;
            string shader = src.shader.name;
            if (shader == "Universal Render Pipeline/Lit")
            {
                dst = new Material(CurvedShader) { name = src.name + " (curved)" };
                CopyLit(src, dst);
                s_pairs.Add((src, dst));
            }
            else if (shader == "OddsCab/Bosphorus Water")
            {
                // the street sea bends with the road (the horizon sea keeps the unbent original)
                dst = new Material(src) { name = src.name + " (curved)" };
                dst.EnableKeyword("_CURVED");
                s_pairs.Add((src, dst));
            }
            else if (src.HasProperty("baseColorFactor") && src.renderQueue < 2500)
            {
                // glTFast PBR (opaque only): carry over colour, textures and the metal / roughness values
                dst = new Material(CurvedShader) { name = src.name + " (curved)" };
                dst.SetColor("_BaseColor", src.GetColor("baseColorFactor"));
                if (src.HasProperty("baseColorTexture")) dst.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
                if (src.HasProperty("metallicFactor")) dst.SetFloat("_Metallic", src.GetFloat("metallicFactor"));
                if (src.HasProperty("roughnessFactor")) dst.SetFloat("_Smoothness", 1f - src.GetFloat("roughnessFactor"));
                if (src.HasProperty("normalTexture") && src.GetTexture("normalTexture") != null)
                {
                    dst.SetTexture("_BumpMap", src.GetTexture("normalTexture"));
                    dst.EnableKeyword("_NORMALMAP");
                }
                if (src.HasProperty("emissiveFactor"))
                {
                    var e = src.GetColor("emissiveFactor");
                    if (e.maxColorComponent > 0.001f) { dst.SetColor("_EmissionColor", e); dst.EnableKeyword("_EMISSION"); }
                }
                // double-sided and alpha-masked (leaves, railings) carry over as cull off / alpha clip
                if (src.HasProperty("_CullMode")) dst.SetFloat("_Cull", src.GetFloat("_CullMode"));
                else if (src.HasProperty("_Cull")) dst.SetFloat("_Cull", src.GetFloat("_Cull"));
                if (src.IsKeywordEnabled("_ALPHATEST_ON") || src.renderQueue == 2450)
                {
                    dst.SetFloat("_AlphaClip", 1f);
                    dst.SetFloat("_Cutoff", src.HasProperty("alphaCutoff") ? src.GetFloat("alphaCutoff") : 0.5f);
                    dst.EnableKeyword("_ALPHATEST_ON");
                    dst.renderQueue = 2450;
                }
                dst.enableInstancing = true;
            }
            s_map[src] = dst;
            return dst;
        }

        static void CopyLit(Material src, Material dst)
        {
            bool curvedWater = dst.shader != null && dst.shader.name == "OddsCab/Bosphorus Water";
            dst.CopyPropertiesFromMaterial(src);
            // Unity's native copy includes keywords. Reading shaderKeywords creates a managed array.
            if (curvedWater) dst.EnableKeyword("_CURVED");
            dst.renderQueue = src.renderQueue;
            dst.enableInstancing = src.enableInstancing;
        }
    }
}

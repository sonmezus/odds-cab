using System.Collections.Generic;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Harem–Sirkeci car ferry the taxi crosses on between districts on opposite shores (TourRoute ferry legs).
    /// While it plays the street is hidden and this scene is put around the stopped taxi: open sea, a white ferry
    /// with a blue band, its deck house and funnel on one side, two other cars, a Turkish flag at the stern, gulls,
    /// and a wake streaming astern so the stationary ferry reads as under way towards the far shore in the vista.
    /// Built once from primitives, then only moved and shown.
    /// </summary>
    public class FerryCrossing : MonoBehaviour
    {
        const float DeckY = -0.02f, WaterY = -1.6f;
        const float HullLength = 26f, HullHalfWidth = 4.6f;

        Material _sea;
        bool _seaHasGpuWaves;
        ParticleSystem _wake;
        readonly List<Transform> _gulls = new List<Transform>();
        readonly List<Transform> _gullWings = new List<Transform>();
        Transform _flag;
        float _seaScroll;

        public static FerryCrossing Create(Material seaMaterial)
        {
            var go = new GameObject("CarFerryCrossing");
            var f = go.AddComponent<FerryCrossing>();
            f.Build(seaMaterial);
            go.SetActive(false);
            return f;
        }

        /// <summary>Shows the ferry with the taxi (at <paramref name="taxiPos"/>) parked on its deck.</summary>
        public void Begin(Vector3 taxiPos)
        {
            // taxi in the right-hand vehicle lane, the deck house on the left
            transform.position = new Vector3(taxiPos.x - 1.6f, 0f, taxiPos.z + 1.5f);
            gameObject.SetActive(true);
            if (_wake != null) { _wake.Clear(); _wake.Play(); }
        }

        public void End() => gameObject.SetActive(false);

        void Update()
        {
            // under way: the sea runs astern under the ferry
            if (!_seaHasGpuWaves && _sea != null)
            {
                _seaScroll += Time.deltaTime * 0.06f;
                _sea.mainTextureOffset = new Vector2(Time.time * 0.01f, -_seaScroll);
            }
            float t = Time.time;
            for (int i = 0; i < _gulls.Count; i++)
            {
                // gulls glide alongside in slow loops, flapping now and then
                float a = t * (0.35f + i * 0.07f) + i * 1.7f;
                _gulls[i].localPosition = new Vector3(Mathf.Cos(a) * (6f + i * 2f) - 2f, 7f + i * 1.3f + Mathf.Sin(a * 2.1f) * 0.6f, 4f + Mathf.Sin(a) * (9f + i * 3f));
                _gulls[i].localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, Mathf.Cos(a) * 18f);
                if (_gullWings[i * 2] == null) continue;   // imported gulls glide with their authored fixed wings
                float flap = Mathf.Max(0f, Mathf.Sin(t * 9f + i)) * (Mathf.Sin(t * 0.8f + i) > 0.2f ? 38f : 6f);
                _gullWings[i * 2].localRotation = Quaternion.Euler(0f, 0f, flap);
                _gullWings[i * 2 + 1].localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            if (_flag != null) _flag.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(t * 5f) * 9f, 0f);
        }

        void Build(Material seaMaterial)
        {
            var root = transform;
            Material white = GlbModelLoader.MakeMat(new Color(0.94f, 0.94f, 0.92f), 0.35f);
            Material blue = GlbModelLoader.MakeMat(new Color(0.10f, 0.27f, 0.55f), 0.4f);
            Material red = GlbModelLoader.MakeMat(new Color(0.62f, 0.12f, 0.10f), 0.3f);
            Material deck = GlbModelLoader.MakeMat(new Color(0.28f, 0.29f, 0.30f), 0.15f);
            Material paint = GlbModelLoader.MakeMat(new Color(0.95f, 0.80f, 0.20f), 0.2f);
            Material glass = GlbModelLoader.MakeMat(new Color(0.12f, 0.18f, 0.24f), 0.85f, 0.1f);
            Material steel = GlbModelLoader.MakeMat(new Color(0.55f, 0.57f, 0.60f), 0.5f, 0.4f);
            Material orange = GlbModelLoader.MakeMat(new Color(0.95f, 0.42f, 0.10f), 0.3f);
            Material flagRed = GlbModelLoader.MakeMat(new Color(0.85f, 0.07f, 0.12f), 0.2f);

            // Open sea all round, its own instance so it can scroll faster than the street's quay water
            _sea = seaMaterial != null ? new Material(seaMaterial) : GlbModelLoader.MakeMat(new Color(0.12f, 0.50f, 0.72f), 0.72f);
            _seaHasGpuWaves = _sea.HasProperty("_WaveA");
            if (_seaHasGpuWaves) _sea.SetVector("_Flow", new Vector4(0f, 2.5f, 0f, 0f));
            else _sea.mainTextureScale = new Vector2(24f, 40f);
            BosphorusWaterMesh.Create(root, "Sea", new Vector3(0f, WaterY + 0.05f, 30f), new Vector2(260f, 300f), _sea);

            // Hull: white topsides with a blue band, red below the waterline, bow and stern ramps
            float hullH = DeckY - (WaterY - 0.7f);
            // (top a little under the car deck plate, so the two never fight)
            Box(root, "Hull", new Vector3(0f, DeckY - 0.12f - hullH * 0.5f, 0f), new Vector3(HullHalfWidth * 2f, hullH, HullLength), white);
            Box(root, "Boot", new Vector3(0f, WaterY - 0.3f, 0f), new Vector3(HullHalfWidth * 2f + 0.04f, 0.6f, HullLength + 0.04f), red);
            foreach (int s in new[] { -1, 1 })
                Box(root, "Band", new Vector3(s * (HullHalfWidth + 0.01f), WaterY + 0.45f, 0f), new Vector3(0.04f, 0.35f, HullLength), blue);
            Box(root, "CarDeck", new Vector3(0f, DeckY - 0.05f, 0f), new Vector3(HullHalfWidth * 2f - 0.3f, 0.1f, HullLength - 0.2f), deck);
            foreach (int e in new[] { -1, 1 })
            {
                var ramp = BoxT(root, "Ramp", new Vector3(0f, 0.55f, e * (HullLength * 0.5f + 0.2f)), new Vector3(5.2f, 0.12f, 2.6f), steel);
                ramp.localRotation = Quaternion.Euler(e * -62f, 0f, 0f);   // raised for the crossing
            }
            // vehicle lane markings
            for (float z = -11f; z < 11f; z += 3f)
                Box(root, "LaneMark", new Vector3(1.6f + 1.25f, DeckY + 0.005f, z), new Vector3(0.1f, 0.01f, 1.6f), paint);

            // Bulwarks with railing posts and orange life rings along the sides
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * (HullHalfWidth - 0.08f);
                Box(root, "Bulwark", new Vector3(x, 0.5f, 0f), new Vector3(0.16f, 1.0f, HullLength), white);
                Box(root, "BulwarkTop", new Vector3(x, 1.02f, 0f), new Vector3(0.24f, 0.05f, HullLength), blue);
                for (float z = -10f; z <= 10f; z += 10f)
                    Box(root, "LifeRing", new Vector3(x + s * 0.1f, 0.62f, z + 2f), new Vector3(0.06f, 0.5f, 0.5f), orange);
            }

            // Deck house along the left side with a row of windows, wheelhouse and funnel on top
            Box(root, "DeckHouse", new Vector3(-3.15f, 1.4f, -1f), new Vector3(2.4f, 2.8f, 15f), white);
            Box(root, "DeckHouseWindows", new Vector3(-1.94f, 1.85f, -1f), new Vector3(0.04f, 0.7f, 13.5f), glass);
            Box(root, "UpperDeck", new Vector3(-2.6f, 2.84f, -1f), new Vector3(3.6f, 0.1f, 16f), blue);
            Box(root, "Wheelhouse", new Vector3(-2.9f, 3.65f, 4.5f), new Vector3(2.8f, 1.5f, 3.2f), white);
            Box(root, "WheelhouseGlass", new Vector3(-2.9f, 3.85f, 6.12f), new Vector3(2.5f, 0.6f, 0.04f), glass);
            Cyl(root, "Funnel", new Vector3(-3.0f, 4.0f, -4.5f), new Vector3(1.1f, 1.1f, 1.1f), white);
            Cyl(root, "FunnelBand", new Vector3(-3.0f, 4.7f, -4.5f), new Vector3(1.13f, 0.18f, 1.13f), blue);
            Cyl(root, "Mast", new Vector3(-2.9f, 5.2f, 5.2f), new Vector3(0.08f, 0.9f, 0.08f), steel);

            // Turkish flag at the stern
            Cyl(root, "FlagPole", new Vector3(-3.6f, 1.9f, -HullLength * 0.5f + 0.4f), new Vector3(0.06f, 1.8f, 0.06f), steel);
            _flag = new GameObject("Flag").transform;
            _flag.SetParent(root, false);
            _flag.localPosition = new Vector3(-3.6f, 3.25f, -HullLength * 0.5f + 0.4f);
            Box(_flag, "Cloth", new Vector3(0f, 0f, 0.6f), new Vector3(0.02f, 0.75f, 1.15f), flagRed);
            Box(_flag, "Crescent", new Vector3(0.015f, 0f, 0.42f), new Vector3(0.02f, 0.32f, 0.2f), white);

            // Two other cars waiting on deck
            ParkedCar(root, new Vector3(-0.1f, 0f, 6.5f), GlbModelLoader.MakeMat(new Color(0.75f, 0.15f, 0.14f), 0.55f), glass);
            ParkedCar(root, new Vector3(-0.1f, 0f, -7.2f),   // left lane, clear of the camera on the open side
                GlbModelLoader.MakeMat(new Color(0.85f, 0.86f, 0.88f), 0.6f), glass);

            // Wake: foam streaming astern from both quarters
            _wake = BuildWake(root);

            // Gulls
            var gullMat = GlbModelLoader.MakeMat(new Color(0.97f, 0.97f, 0.95f), 0.2f);
            var tipMat = GlbModelLoader.MakeMat(new Color(0.25f, 0.25f, 0.27f), 0.2f);
            for (int i = 0; i < 3; i++)
            {
                var g = new GameObject("Gull").transform;
                g.SetParent(root, false);
                _gulls.Add(g);
                if (GlbModelLoader.TryInstantiateEModel("Çevre_Martı", g, Vector3.zero, Quaternion.identity, 0.55f, out _))
                {
                    _gullWings.Add(null);
                    _gullWings.Add(null);
                    continue;
                }
                Box(g, "Body", Vector3.zero, new Vector3(0.14f, 0.12f, 0.45f), gullMat);
                for (int s = -1; s <= 1; s += 2)
                {
                    var wing = new GameObject("Wing").transform;
                    wing.SetParent(g, false);
                    Box(wing, "Feathers", new Vector3(s * 0.4f, 0f, 0f), new Vector3(0.75f, 0.03f, 0.22f), gullMat);
                    Box(wing, "Tip", new Vector3(s * 0.74f, 0f, 0f), new Vector3(0.1f, 0.031f, 0.18f), tipMat);
                    _gullWings.Add(wing);
                }
            }
        }

        void OnDestroy()
        {
            if (_sea != null) Destroy(_sea);
        }

        static void ParkedCar(Transform parent, Vector3 pos, Material body, Material glass)
        {
            var car = new GameObject("ParkedCar").transform;
            car.SetParent(parent, false);
            car.localPosition = pos;
            // the classic Istanbul saloon model (Rodin), facing the bow like the taxi
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Araba", car, Vector3.zero, Quaternion.identity, 1.42f, out _)) return;
            Box(car, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(1.75f, 0.7f, 4.1f), body);
            Box(car, "Cabin", new Vector3(0f, 1.15f, -0.2f), new Vector3(1.55f, 0.55f, 2.2f), glass);
            Box(car, "Roof", new Vector3(0f, 1.44f, -0.2f), new Vector3(1.5f, 0.05f, 2.0f), body);
            var tyre = GlbModelLoader.MakeMat(new Color(0.03f, 0.03f, 0.03f), 0.1f);
            foreach (float x in new[] { -0.82f, 0.82f })
                foreach (float z in new[] { -1.3f, 1.3f })
                {
                    var w = Cyl(car, "Wheel", new Vector3(x, 0.32f, z), new Vector3(0.62f, 0.12f, 0.62f), tyre);
                    w.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
        }

        static ParticleSystem BuildWake(Transform parent)
        {
            var go = new GameObject("Wake");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, WaterY + 0.08f, -HullLength * 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startColor = new Color(1f, 1f, 1f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.startRotation3D = false;
            var em = ps.emission; em.rateOverTime = 34f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(HullHalfWidth * 1.8f, 0.05f, 0.5f);
            // drifts astern and spreads, as the ferry draws away from it
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(-5.5f, -4.5f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDisc() };
            return ps;
        }

        static Texture2D SoftDisc()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FoamDisc" };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                    // broken, foamy edge rather than a clean dot
                    float n = Mathf.PerlinNoise(x * 0.21f, y * 0.21f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 1.6f) * Mathf.Lerp(0.45f, 1f, n)));
                }
            tex.Apply();
            return tex;
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat) => BoxT(parent, name, pos, scale, mat);

        static Transform BoxT(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat) => Prim(PrimitiveType.Cube, parent, name, pos, scale, mat);

        static Transform Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat) => Prim(PrimitiveType.Cylinder, parent, name, pos, scale, mat);

        static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>
    /// Prewarmed human, fisherman, cat, gull and fish pools. World positions recycle outside the near view;
    /// regular updates never instantiate, destroy, load assets, query physics or create materials.
    /// </summary>
    public sealed class DistrictLifeDirector : MonoBehaviour
    {
        static readonly int CurveOriginId = Shader.PropertyToID("_CurveOrigin");
        static readonly int CurveAmountId = Shader.PropertyToID("_CurveAmount");
        const int CitizenCapacity = 12, FisherCapacity = 4, CatCapacity = 4, GullCapacity = 6, FishCapacity = 4;
        sealed class Citizen
        {
            public GameObject root;
            public AmbientNpcAnimator motion;
            public AmbientNpcActivity activity;
            public float phase, speed;
            public int side;
            public int identity;
            public Transform book;
        }
        sealed class Fisher
        {
            public GameObject root;
            public AmbientNpcAnimator motion;
            public Transform rod;
            public LineRenderer line;
            public float phase;
        }
        sealed class Cat
        {
            public GameObject root;
            public Transform[] legs, tails;
            public Quaternion[] legRest, tailRest;
            public Transform head;
            public Quaternion headRest;
            public float phase;
        }
        sealed class Gull
        {
            public GameObject root;
            public Transform left, right;
            public Quaternion leftRest, rightRest;
            public float phase;
            public Vector3 perch;
        }
        sealed class Fish
        {
            public GameObject root, splash;
            public Renderer splashRenderer;
            public float nextJump, launched, splashStart = -10f;
            public Vector3 origin;
            public Vector3 splashPoint;
            public bool airborne;
        }

        readonly Citizen[] _citizens = new Citizen[CitizenCapacity];
        readonly Fisher[] _fishers = new Fisher[FisherCapacity];
        readonly Cat[] _cats = new Cat[CatCapacity];
        readonly Gull[] _gulls = new Gull[GullCapacity];
        readonly Fish[] _fish = new Fish[FishCapacity];
        readonly Material[] _splashMaterials = new Material[12];
        Transform _taxi;
        Material _rodMaterial, _corkMaterial, _lineMaterial, _paperMaterial;
        Mesh _splashMesh;
        int _district;
        float _waterSide = 1f, _waterY = -.35f;
        bool _ready, _streetVisible = true, _hasWater;

        public int CitizenPoolCount { get; private set; }
        public int ActiveCitizens { get; private set; }
        public int ActiveFishermen { get; private set; }

        public void Initialize(Transform taxi, int district)
        {
            if (_ready) return;
            _taxi = taxi;
            BuildMaterials();
            for (int i = 0; i < CitizenCapacity; i++)
            {
                int identity = DistrictLifeMotion.CitizenIdentityForSlot(i);
                if (!LivingIstanbulAssets.TryCreateCharacter(identity, transform, out var root, out var motion)) continue;
                var citizen = new Citizen { root = root, motion = motion, phase = i * 1.73f,
                    identity = identity, speed = .65f + (i % 5) * .095f, side = (i & 1) == 0 ? 1 : -1 };
                // Preserve the common human scale and vary stature without stretching limbs.
                root.transform.localScale *= .98f + (i % 4) * .015f;
                _citizens[i] = citizen;
                if (identity == 1) citizen.book = BuildBook(root.transform);
                CitizenPoolCount++;
                CurveBender.Convert(root.transform);
            }
            for (int i = 0; i < FisherCapacity; i++)
            {
                if (!LivingIstanbulAssets.TryCreateCharacter(i % 2 == 0 ? 4 : 0, transform, out var root, out var motion)) continue;
                var fisher = new Fisher { root = root, motion = motion, phase = i * 2.3f };
                fisher.rod = BuildFishingRod(root.transform);
                var lineObject = new GameObject("FishingLine");
                lineObject.transform.SetParent(transform, false);
                fisher.line = lineObject.AddComponent<LineRenderer>();
                fisher.line.useWorldSpace = true;
                fisher.line.positionCount = 3;
                fisher.line.startWidth = .004f;
                fisher.line.endWidth = .002f;
                fisher.line.sharedMaterial = _lineMaterial;
                fisher.line.shadowCastingMode = ShadowCastingMode.Off;
                fisher.line.receiveShadows = false;
                fisher.line.enabled = false;
                CurveBender.Convert(root.transform);
                _fishers[i] = fisher;
            }
            for (int i = 0; i < CatCapacity; i++)
            {
                var root = LivingIstanbulAssets.Create("IstanbulCat", transform);
                if (root == null) continue;
                var cat = new Cat { root = root, phase = i * 2.17f,
                    legs = new Transform[4], legRest = new Quaternion[4], tails = new Transform[3], tailRest = new Quaternion[3] };
                var parts = root.GetComponentsInChildren<Transform>(true);
                for (int p = 0; p < parts.Length; p++)
                {
                    var part = parts[p];
                    int leg = part.name == "LegFrontL" ? 0 : part.name == "LegFrontR" ? 1 :
                        part.name == "LegRearL" ? 2 : part.name == "LegRearR" ? 3 : -1;
                    if (leg >= 0) { cat.legs[leg] = part; cat.legRest[leg] = part.localRotation; }
                    if (part.name == "Head") { cat.head = part; cat.headRest = part.localRotation; }
                    for (int tail = 0; tail < 3; tail++)
                        if (part.name == "Tail" + tail) { cat.tails[tail] = part; cat.tailRest[tail] = part.localRotation; }
                }
                root.transform.localScale = Vector3.one * (.85f + i * .065f);
                CurveBender.Convert(root.transform);
                _cats[i] = cat;
            }
            for (int i = 0; i < GullCapacity; i++)
            {
                var root = LivingIstanbulAssets.Create("IstanbulGull", transform);
                if (root == null) continue;
                var gull = new Gull { root = root, phase = i * 1.97f };
                var parts = root.GetComponentsInChildren<Transform>(true);
                for (int p = 0; p < parts.Length; p++)
                {
                    if (parts[p].name == "WingL") { gull.left = parts[p]; gull.leftRest = parts[p].localRotation; }
                    if (parts[p].name == "WingR") { gull.right = parts[p]; gull.rightRest = parts[p].localRotation; }
                }
                _gulls[i] = gull;
            }
            _splashMesh = BuildSplashMesh();
            for (int i = 0; i < FishCapacity; i++)
            {
                var root = LivingIstanbulAssets.Create("BosphorusFish", transform);
                if (root == null) continue;
                var splash = new GameObject("FishWaterRing", typeof(MeshFilter), typeof(MeshRenderer));
                splash.transform.SetParent(transform, false);
                splash.GetComponent<MeshFilter>().sharedMesh = _splashMesh;
                var renderer = splash.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _splashMaterials[0];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _fish[i] = new Fish { root = root, splash = splash, splashRenderer = renderer };
                root.SetActive(false);
                splash.SetActive(false);
            }
            _ready = true;
            SetDistrict(district);
        }

        public void SetDistrict(int district)
        {
            _district = Mathf.Clamp(district, 0, TourRoute.Districts.Length - 1);
            var settings = TourRoute.Districts[_district];
            _waterSide = settings.seaSide == 0 ? GameConfig.CurbSide : settings.seaSide;
            _hasWater = settings.seaSide != 0 || settings.road == TourRoadType.Bridge;
            _waterY = BosphorusSeaState.MeanLevel;
            if (!_ready || _taxi == null) return;
            float origin = _taxi.position.z;
            for (int i = 0; i < _citizens.Length; i++)
            {
                var citizen = _citizens[i];
                if (citizen == null) continue;
                bool shopkeeper = DistrictLifeMotion.IsShopkeeper(citizen.identity);
                citizen.activity = citizen.identity == 1 ? AmbientNpcActivity.Reading :
                    shopkeeper || i % 6 == 3 || i % 6 == 4 ? AmbientNpcActivity.Conversation : AmbientNpcActivity.Walking;
                citizen.side = citizen.activity == AmbientNpcActivity.Conversation ? -(int)_waterSide : (i & 1) == 0 ? 1 : -1;
                float x = CitizenX(i, citizen.side);
                float z = origin + 12f + i * 8.1f;
                if (i % 6 == 4 && !shopkeeper) z = origin + 12f + (i - 1) * 8.1f + 1.05f;
                citizen.root.transform.position = new Vector3(x, .16f, z);
                citizen.root.transform.rotation = Quaternion.Euler(0f,
                    shopkeeper ? -citizen.side * 90f : citizen.activity == AmbientNpcActivity.Conversation ? (i % 6 == 3 ? 0f : 180f) : (i & 2) == 0 ? 0f : 180f, 0f);
                citizen.root.SetActive(false);
            }
            for (int i = 0; i < _fishers.Length; i++)
            {
                var fisher = _fishers[i];
                if (fisher == null) continue;
                float side = settings.road == TourRoadType.Bridge && (i & 1) != 0 ? -_waterSide : _waterSide;
                fisher.root.transform.position = new Vector3(side * 4.9f, .16f, origin + 20f + i * 22f);
                fisher.root.transform.rotation = Quaternion.Euler(0f, side * 90f, 0f);
                fisher.root.SetActive(false);
                fisher.line.enabled = false;
            }
            for (int i = 0; i < _cats.Length; i++)
                if (_cats[i] != null)
                {
                    _cats[i].root.transform.position = new Vector3(-_waterSide * (4.4f + (i & 1) * .8f), .18f, origin + 10f + i * 27f);
                    _cats[i].root.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f);
                }
            for (int i = 0; i < _fish.Length; i++)
                if (_fish[i] != null)
                {
                    _fish[i].airborne = false;
                    _fish[i].nextJump = Time.time + 1.6f + i * 2.2f;
                    _fish[i].splashStart = -10f;
                    _fish[i].root.SetActive(false);
                    _fish[i].splash.SetActive(false);
                }
            for (int i = 0; i < _gulls.Length; i++)
                if (_gulls[i] != null)
                    _gulls[i].perch = new Vector3(_waterSide * 5.8f, 1.15f, origin + 38f + i * 17f);
        }

        public void SetStreetVisible(bool visible)
        {
            _streetVisible = visible;
            if (!visible) HideAll();
        }

        float CitizenX(int index, int side)
        {
            if (TourRoute.Districts[_district].road == TourRoadType.Bridge) return side * 3.65f;
            if (DistrictLifeMotion.IsShopkeeper(DistrictLifeMotion.CitizenIdentityForSlot(index))) return side * 4.7f;
            if (index % 6 == 3 || index % 6 == 4) return side * 3.9f;
            return side * (3.55f + (index % 3) * .46f);
        }

        void LateUpdate()
        {
            if (!_ready || _taxi == null) return;
            var game = GameManager.Instance;
            bool visible = _streetVisible && game != null && game.Taxi != null && game.Taxi.ExteriorVisible
                && game.State != GameState.FerryCrossing && game.State != GameState.DistrictTransition;
            if (!visible) { HideAll(); return; }
            bool night = game.Weather != null && game.Weather.CurrentWeather == WeatherPreset.Night;
            float delta = Mathf.Min(Time.deltaTime, .1f), clock = Time.time;
            float taxiZ = _taxi.position.z;
            int count = DistrictLifeMotion.CitizenCount(_district, night, (int)DeviceTierDetector.CurrentTier);
            ActiveCitizens = 0;
            for (int i = 0; i < _citizens.Length; i++)
            {
                var citizen = _citizens[i];
                if (citizen == null) continue;
                bool show = i < count;
                Vector3 position = citizen.root.transform.position;
                position.z = DistrictLifeMotion.RecycleZ(position.z, taxiZ, i);
                if (citizen.activity == AmbientNpcActivity.Conversation && !DistrictLifeMotion.IsShopkeeper(citizen.identity) && i % 6 == 4 && _citizens[i - 1] != null)
                    position.z = _citizens[i - 1].root.transform.position.z + 1.05f;
                position.x = CitizenX(i, citizen.side);
                if (game.ActiveCurbPassenger != null && Mathf.Abs(position.z - game.ActiveCurbPassenger.transform.position.z) < 6f
                    && Mathf.Abs(position.x - game.ActiveCurbPassenger.transform.position.x) < 1f) show = false;
                if (citizen.root.activeSelf != show) citizen.root.SetActive(show);
                float travel = 0f;
                if (show)
                {
                    ActiveCitizens++;
                    if (citizen.activity == AmbientNpcActivity.Walking)
                    {
                        travel = citizen.speed * delta * (night ? .7f : 1f);
                        position += citizen.root.transform.forward * travel;
                    }
                    citizen.root.transform.position = position;
                    citizen.motion.Tick(delta, travel, citizen.activity);
                    if (citizen.book != null && citizen.motion.RightHand != null && citizen.motion.LeftHand != null)
                        citizen.book.SetPositionAndRotation((citizen.motion.RightHand.position + citizen.motion.LeftHand.position) * .5f
                            + citizen.root.transform.forward * .06f, citizen.root.transform.rotation * Quaternion.Euler(-12f, 0f, 0f));
                }
            }
            TickFishers(delta, clock, taxiZ, night);
            TickCats(delta, clock, taxiZ, night);
            TickGulls(clock, taxiZ, night);
            TickFish(clock, taxiZ, night);
        }

        void TickFishers(float delta, float clock, float taxiZ, bool night)
        {
            Vector4 curveOrigin = Shader.GetGlobalVector(CurveOriginId);
            Vector4 curveAmount = Shader.GetGlobalVector(CurveAmountId);
            int count = !_hasWater ? 0 : _district == 2 ? (night ? 2 : 4) : night ? 1 : 2;
            ActiveFishermen = 0;
            for (int i = 0; i < _fishers.Length; i++)
            {
                var fisher = _fishers[i];
                if (fisher == null) continue;
                bool show = i < count;
                if (fisher.root.activeSelf != show) fisher.root.SetActive(show);
                fisher.line.enabled = show;
                if (!show) continue;
                ActiveFishermen++;
                Vector3 position = fisher.root.transform.position;
                position.z = DistrictLifeMotion.RecycleZ(position.z, taxiZ, i + 2);
                fisher.root.transform.position = position;
                fisher.motion.Tick(delta, 0f, AmbientNpcActivity.Fishing);
                Vector3 grip = fisher.motion.RightHand != null ? fisher.motion.RightHand.position : position + Vector3.up;
                Vector3 aim = fisher.root.transform.forward + Vector3.up * (.47f + Mathf.Sin(clock * .3f + fisher.phase) * .045f);
                fisher.rod.SetPositionAndRotation(grip, Quaternion.LookRotation(aim));
                Vector3 tip = fisher.rod.TransformPoint(new Vector3(0f, 0f, 2.85f));
                Vector3 end = position + fisher.root.transform.forward * 4.5f;
                end.y = BosphorusSeaState.SampleHeight(end, clock) + .02f;
                fisher.line.SetPosition(0, DistrictLifeMotion.CurveWorldPosition(tip, curveOrigin, curveAmount));
                fisher.line.SetPosition(1, DistrictLifeMotion.CurveWorldPosition(Vector3.Lerp(tip, end, .55f) + Vector3.down * .2f, curveOrigin, curveAmount));
                fisher.line.SetPosition(2, DistrictLifeMotion.CurveWorldPosition(end, curveOrigin, curveAmount));
            }
        }

        void TickCats(float delta, float clock, float taxiZ, bool night)
        {
            int count = _district == 2 ? 0 : _district == 5 || _district == 4 || _district == 7 ? 3 : 2;
            for (int i = 0; i < _cats.Length; i++)
            {
                var cat = _cats[i];
                if (cat == null) continue;
                bool show = i < count;
                if (cat.root.activeSelf != show) cat.root.SetActive(show);
                if (!show) continue;
                bool walking = !night && Mathf.Sin(clock * .15f + cat.phase) > -.35f;
                Vector3 position = cat.root.transform.position;
                position.z = DistrictLifeMotion.RecycleZ(position.z, taxiZ, i + 1);
                if (walking) position += cat.root.transform.forward * (delta * .34f);
                cat.root.transform.position = position;
                float gait = clock * 6f + cat.phase;
                for (int leg = 0; leg < cat.legs.Length; leg++)
                    if (cat.legs[leg] != null) cat.legs[leg].localRotation = cat.legRest[leg]
                        * Quaternion.Euler(walking ? Mathf.Sin(gait + ((leg == 0 || leg == 3) ? 0f : Mathf.PI)) * 15f : 0f, 0f, 0f);
                for (int tail = 0; tail < cat.tails.Length; tail++)
                    if (cat.tails[tail] != null) cat.tails[tail].localRotation = cat.tailRest[tail]
                        * Quaternion.Euler(0f, Mathf.Sin(clock * .9f + cat.phase + tail * .5f) * (night ? 3f : 9f), 0f);
                if (cat.head != null) cat.head.localRotation = cat.headRest
                    * Quaternion.Euler(night ? 12f : Mathf.Sin(clock * .4f + cat.phase) * 4f, Mathf.Sin(clock * .3f + cat.phase) * 7f, 0f);
            }
        }

        void TickGulls(float clock, float taxiZ, bool night)
        {
            Vector4 curveOrigin = Shader.GetGlobalVector(CurveOriginId);
            Vector4 curveAmount = Shader.GetGlobalVector(CurveAmountId);
            for (int i = 0; i < _gulls.Length; i++)
            {
                var gull = _gulls[i];
                if (gull == null) continue;
                bool show = _hasWater && (!night || i < 2);
                if (gull.root.activeSelf != show) gull.root.SetActive(show);
                if (!show) continue;
                float phase = clock * .13f + gull.phase;
                gull.perch.z = DistrictLifeMotion.RecycleZ(gull.perch.z, taxiZ, i + 1);
                Vector3 position = night ? DistrictLifeMotion.CurveWorldPosition(gull.perch, curveOrigin, curveAmount)
                    : new Vector3(_waterSide * (18f + Mathf.Cos(phase) * 13f), 6f + i * .7f + Mathf.Sin(phase * 1.7f),
                        taxiZ + 36f + Mathf.Sin(phase) * 30f + i * 5f);
                gull.root.transform.position = position;
                gull.root.transform.rotation = night ? Quaternion.Euler(0f, -_waterSide * 90f, 0f)
                    : Quaternion.LookRotation(new Vector3(-_waterSide * Mathf.Sin(phase) * 13f,
                        Mathf.Cos(phase * 1.7f) * 1.7f, Mathf.Cos(phase) * 30f));
                float flap = night ? 0f : Mathf.Sin(clock * 5.2f + gull.phase) * 18f;
                if (gull.left != null) gull.left.localRotation = gull.leftRest * Quaternion.Euler(0f, night ? -70f : 0f, night ? -12f : -flap);
                if (gull.right != null) gull.right.localRotation = gull.rightRest * Quaternion.Euler(0f, night ? 70f : 0f, night ? 12f : flap);
            }
        }

        void TickFish(float clock, float taxiZ, bool night)
        {
            Vector4 curveOrigin = Shader.GetGlobalVector(CurveOriginId);
            Vector4 curveAmount = Shader.GetGlobalVector(CurveAmountId);
            for (int i = 0; i < _fish.Length; i++)
            {
                var fish = _fish[i];
                if (fish == null) continue;
                bool show = _hasWater && (_district == 2 || i < 2);
                if (!show)
                {
                    fish.root.SetActive(false); fish.splash.SetActive(false); fish.airborne = false;
                    continue;
                }
                if (!fish.airborne && clock >= fish.nextJump)
                {
                    fish.origin = new Vector3(_waterSide * (8.4f + i * 1.7f), _waterY, taxiZ + 17f + i * 12f);
                    fish.origin.y = BosphorusSeaState.SampleHeight(fish.origin, clock);
                    fish.launched = clock;
                    fish.airborne = true;
                    fish.root.SetActive(true);
                }
                if (fish.airborne)
                {
                    float progress = (clock - fish.launched) / .85f;
                    DistrictLifeMotion.FishArc(progress, .75f + i * .08f, 2.1f, out var point, out var tangent);
                    point.x *= _waterSide;
                    tangent.x *= _waterSide;
                    Vector3 unbent = fish.origin + point;
                    float curveDistance = Mathf.Clamp(unbent.z - curveOrigin.z - curveOrigin.w, 0f, 90f);
                    tangent.x += curveAmount.x * 2f * curveDistance * tangent.z;
                    tangent.y += curveAmount.y * 2f * curveDistance * tangent.z;
                    fish.root.transform.SetPositionAndRotation(DistrictLifeMotion.CurveWorldPosition(unbent, curveOrigin, curveAmount), Quaternion.LookRotation(tangent));
                    if (progress >= 1f)
                    {
                        fish.airborne = false;
                        fish.root.SetActive(false);
                        fish.nextJump = clock + (night ? 14f : 6.5f) + i * 1.3f;
                        fish.splashStart = clock;
                        fish.splashPoint = fish.origin + point;
                        fish.splashPoint.y = BosphorusSeaState.SampleHeight(fish.splashPoint, clock) + .025f;
                        fish.splash.SetActive(true);
                    }
                }
                float splash = (clock - fish.splashStart) / 1.05f;
                if (splash >= 0f && splash < 1f)
                {
                    Vector3 splashPoint = fish.splashPoint;
                    splashPoint.y = BosphorusSeaState.SampleHeight(splashPoint, clock) + .025f;
                    fish.splash.transform.position = DistrictLifeMotion.CurveWorldPosition(splashPoint, curveOrigin, curveAmount);
                    fish.splash.transform.localScale = Vector3.one * (.2f + splash * 1.2f);
                    fish.splashRenderer.sharedMaterial = _splashMaterials[Mathf.Min(11, (int)(splash * 12f))];
                }
                else if (fish.splash.activeSelf) fish.splash.SetActive(false);
            }
        }

        void HideAll()
        {
            ActiveCitizens = ActiveFishermen = 0;
            for (int i = 0; i < _citizens.Length; i++) if (_citizens[i] != null && _citizens[i].root.activeSelf) _citizens[i].root.SetActive(false);
            for (int i = 0; i < _fishers.Length; i++) if (_fishers[i] != null)
            { if (_fishers[i].root.activeSelf) _fishers[i].root.SetActive(false); _fishers[i].line.enabled = false; }
            for (int i = 0; i < _cats.Length; i++) if (_cats[i] != null && _cats[i].root.activeSelf) _cats[i].root.SetActive(false);
            for (int i = 0; i < _gulls.Length; i++) if (_gulls[i] != null && _gulls[i].root.activeSelf) _gulls[i].root.SetActive(false);
            for (int i = 0; i < _fish.Length; i++) if (_fish[i] != null)
            { _fish[i].root.SetActive(false); _fish[i].splash.SetActive(false); }
        }

        void BuildMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            _rodMaterial = new Material(lit) { name = "FishingRodGraphite", enableInstancing = true };
            _rodMaterial.SetColor("_BaseColor", new Color(.035f, .045f, .055f));
            _rodMaterial.SetFloat("_Smoothness", .63f);
            _corkMaterial = new Material(lit) { name = "FishingRodCork", enableInstancing = true };
            _corkMaterial.SetColor("_BaseColor", new Color(.44f, .28f, .12f));
            _paperMaterial = new Material(lit) { name = "BookPagesCream", enableInstancing = true };
            _paperMaterial.SetColor("_BaseColor", new Color(.86f, .81f, .64f));
            _lineMaterial = TransparentMaterial("FishingLine", new Color(.72f, .79f, .78f, .7f));
            for (int i = 0; i < _splashMaterials.Length; i++)
                _splashMaterials[i] = TransparentMaterial("FishSplashFade_" + i,
                    new Color(.62f, .88f, .91f, (1f - i / 12f) * .46f));
        }

        static Material TransparentMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name, renderQueue = 3000 };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        Transform BuildFishingRod(Transform parent)
        {
            var root = new GameObject("FishingRod").transform;
            root.SetParent(parent, false);
            RodPart(root, "CorkGrip", .18f, .36f, .026f, _corkMaterial);
            RodPart(root, "RodLower", .82f, 1.05f, .010f, _rodMaterial);
            RodPart(root, "RodUpper", 1.75f, .95f, .006f, _rodMaterial);
            RodPart(root, "RodTip", 2.52f, .66f, .0035f, _rodMaterial);
            return root;
        }

        Transform BuildBook(Transform parent)
        {
            var root = new GameObject("HeldBook").transform;
            root.SetParent(parent, false);
            BookPart(root, "PaperBlock", Vector3.zero, new Vector3(.23f, .035f, .29f), _paperMaterial);
            BookPart(root, "CoverFront", new Vector3(0f, .023f, 0f), new Vector3(.245f, .008f, .305f), _corkMaterial);
            BookPart(root, "CoverBack", new Vector3(0f, -.023f, 0f), new Vector3(.245f, .008f, .305f), _corkMaterial);
            return root;
        }

        static void BookPart(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }

        static void RodPart(Transform parent, string name, float center, float length, float radius, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(0f, 0f, center);
            part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            part.transform.localScale = new Vector3(radius * 2f, length * .5f, radius * 2f);
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }

        static Mesh BuildSplashMesh()
        {
            const int segments = 32;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = direction * .88f;
                vertices[i * 2 + 1] = direction;
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "FishSplashAnnulus", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }

        void OnDestroy()
        {
            if (_rodMaterial != null) Destroy(_rodMaterial);
            if (_corkMaterial != null) Destroy(_corkMaterial);
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_paperMaterial != null) Destroy(_paperMaterial);
            if (_splashMesh != null) Destroy(_splashMesh);
            for (int i = 0; i < _splashMaterials.Length; i++) if (_splashMaterials[i] != null) Destroy(_splashMaterials[i]);
        }
    }
}

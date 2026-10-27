using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Auto-drives the vintage yellow taxi along the coastal avenue (+Z):
    /// - Speeds up progressively with each consecutive correct answer (streak speed bonus)
    /// - Smoothly brakes at stops with audible warning
    /// - Features working chrome windshield wipers during Rain, and the exact Dark-Teal/Green Ribbed
    ///   Back-Seat Cab Interior + Yellow Window Arch from 08-animation.png Row 3 Panel 1.
    /// </summary>
    public class TaxiController : MonoBehaviour
    {
        public float Speed { get; private set; }
        public float TargetSpeed { get; set; } = GameConfig.CruiseSpeed;
        public float TargetLateralX { get; set; } = GameConfig.DriveLaneX;
        public float DistanceZ => transform.position.z;
        public int Kmh => Mathf.RoundToInt(Speed * 3.6f);
        public bool ExteriorVisible => _visual != null && _visual.gameObject.activeSelf;

        [Header("Route handling")]
        [SerializeField, Min(0.05f)] float _laneSmoothTime = 0.28f;
        [SerializeField, Min(0.1f)] float _maxLateralSpeed = 3.6f;
        [SerializeField, Range(1f, 35f)] float _maxSteeringAngle = 26f;
        [Header("Chassis response")]
        [SerializeField, Range(0f, 10f)] float _maxPitch = 3.2f;
        [SerializeField, Range(0f, 10f)] float _maxRoll = 3.8f;
        [SerializeField, Min(0.1f)] float _bodyResponse = 9f;
        TaxiMotion _motion;
        TaxiRoadFeel _roadFeel;
        TaxiSurfaceFeedback _surfaceFeedback;
        Vector3 _cabinRestPosition;
        public float RoadCameraHeave => _roadFeel.CameraHeave;
        public float RoadCameraRoll => _roadFeel.CameraRoll;
        public TaxiRoadSurfaceKind CurrentSurface => TaxiRoadSurface.Classify(transform.position.x, CityChunkStreamer.CurrentRoad);
        Vector3 _visualRestPosition;
        Quaternion _visualRestRotation;
        float _bodyPitch, _bodyRoll, _bodyYaw, _steeringAngle;

        Transform _visual;
        Transform _wiperL;
        Transform _wiperR;
        Renderer _brakeGlowL;
        Renderer _brakeGlowR;
        Renderer[] _taxiPaintRenderers;
        Renderer _taxiCosmeticRenderer;
        Material _brakeOnMat;
        Material _brakeOffMat;
        Material _taxiAccentMaterial;
        Color _taxiAccentBaseColor;
        GameObject _backSeatInterior;
        PassengerActor _driverActor;
        TaxiRig _rig;
        readonly System.Collections.Generic.Dictionary<Material, Color> _paintBase = new System.Collections.Generic.Dictionary<Material, Color>();

        bool _wipersActive;
        bool _doorOpen;
        bool _leftDoorOpen;
        float _boostTimer;
        float _slowTimer;
        readonly Light[] _headlights = new Light[2];

        public float EffectiveCruiseSpeed
        {
            get
            {
                int streak = SaveSystem.Data != null ? SaveSystem.Data.streak : 0;
                float bonus = Mathf.Min(6.5f, streak * 0.85f);
                return GameConfig.CruiseSpeed + bonus;
            }
        }

        public void Initialize()
        {
            var model = GlbModelLoader.InstantiateTaxi();
            model.transform.SetParent(transform, false);
            _visual = model.transform;
            _visualRestPosition = _visual.localPosition;
            _visualRestRotation = _visual.localRotation;
            BuildHeritageDetails();
            _taxiPaintRenderers = _visual.GetComponentsInChildren<Renderer>(true);

            _rig = TaxiRig.TryCreate(transform, _visual);
            BuildChromeWipersAndDoor(_visual);
            BuildBackSeatInterior();
            _cabinRestPosition = _backSeatInterior.transform.localPosition;
            _surfaceFeedback = TaxiSurfaceFeedback.Create(transform);
            SpawnDriverIfPresent(_visual);
            EnhanceTaxiPbrMaterials();
            ApplyTaxiCosmetic(SaveSystem.Data.equippedTaxiCosmetic);
            SetLayerRecursive(_visual, ExteriorLayer);
            ShowCabToMainCamera(false);

            transform.position = new Vector3(GameConfig.DriveLaneX, 0f, 0f);
            _motion.Reset();
            VfxDirector.Instance?.PrepareDrivingEffects(transform);
            gameObject.AddComponent<CurbPortraitLighting>().Initialize(this);
            BuildHeadlights();
            CabWeatherGlass.Create(this, _visual, _backSeatInterior.transform);
        }

        void BuildHeritageDetails()
        {
            var prefab = Resources.Load<GameObject>("ReferenceKit/Models/TaxiHeritageDetails");
            if (prefab == null) return;
            var details = Instantiate(prefab, _visual.Find("TaxiMesh") ?? _visual, false);
            details.name = "TaxiHeritageDetails";
        }

        void BuildHeadlights()
        {
            for (int i = 0; i < _headlights.Length; i++)
            {
                var light = new GameObject(i == 0 ? "TaxiHeadlightLeft" : "TaxiHeadlightRight").AddComponent<Light>();
                light.transform.SetParent(_visual, false);
                light.transform.localPosition = new Vector3(i == 0 ? -0.48f : 0.48f, 0.62f, 1.35f);
                light.transform.localRotation = Quaternion.Euler(8f, i == 0 ? -4f : 4f, 0f);
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.91f, 0.70f);
                light.range = 22f;
                light.spotAngle = 66f;
                light.innerSpotAngle = 36f;
                light.intensity = 7f;
                light.shadows = LightShadows.None;
                light.cullingMask = ~((1 << CabLayer) | (1 << ExteriorLayer) | (1 << HorizonCameraStack.VistaLayer));
                light.enabled = false;
                _headlights[i] = light;
            }
        }

        // The cab (interior + seated passenger) and the car body live on their own layers: the main camera sees
        // the body from outside and the cab only in the back-seat view, while the in-car webcam (PiP) always sees
        // the cab and never the body shell it sits inside.
        public const int ExteriorLayer = 28;
        public const int CabLayer = 29;

        public static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        static void ShowCabToMainCamera(bool show)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (show) cam.cullingMask |= 1 << CabLayer;
            else cam.cullingMask &= ~(1 << CabLayer);
        }

        public static Vector3 DriverSeatHQ = GameConfig.SideV(new Vector3(0.35f, 0.29f, 0.30f));   // left-hand drive with the curb on the right
        const float FrontSeatXHQ = 0.35f, FrontSeatZHQ = 0.16f, FrontSeatYHQ = 0.50f;

        /// <summary>
        /// The remade taxi's cabin has the rear bench but no front seats (seen through the windows and the open
        /// curb door). Two teal ribbed bucket seats as in the reference sheet.
        /// </summary>
        void BuildFrontSeatsHQ(Transform visualRoot)
        {
            var fabric = GlbModelLoader.MakeMat(new Color(0.24f, 0.44f, 0.40f), 0.18f);
            var rib = GlbModelLoader.MakeMat(new Color(0.18f, 0.35f, 0.32f), 0.15f);
            // Ribbed seat fabric photo (Resources/Textures/SeatFabric); the colours stay as a slight teal shift
            var seatTex = Resources.Load<Texture2D>("ReferenceKit/Textures/GreenSeatFabric");
            if (seatTex != null)
            {
                foreach (var (m, c) in new[] { (fabric, Color.white), (rib, new Color(0.77f, 0.85f, 0.78f)) })
                {
                    m.mainTexture = seatTex;
                    m.SetTexture("_BaseMap", seatTex);
                    m.mainTextureScale = new Vector2(0.6f, 0.6f);
                    m.color = c;
                    m.SetColor("_BaseColor", c);
                }
            }
            var piping = GlbModelLoader.MakeMat(new Color(0.86f, 0.82f, 0.70f), 0.25f);
            foreach (float x in new[] { FrontSeatXHQ, -FrontSeatXHQ })
            {
                var seat = new GameObject("FrontSeat").transform;
                seat.SetParent(visualRoot, false);
                seat.localPosition = new Vector3(x, FrontSeatYHQ, FrontSeatZHQ);
                MakeDoorPart(seat, "Cushion", new Vector3(0f, 0f, 0.12f), new Vector3(0.40f, 0.11f, 0.42f), fabric);
                MakeDoorPart(seat, "CushionPiping", new Vector3(0f, 0.035f, 0.335f), new Vector3(0.41f, 0.05f, 0.02f), piping);
                var back = new GameObject("Back").transform;
                back.SetParent(seat, false);
                back.localPosition = new Vector3(0f, 0.30f, -0.10f);
                back.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                MakeDoorPart(back, "Backrest", Vector3.zero, new Vector3(0.40f, 0.52f, 0.09f), fabric);
                for (int r = -2; r <= 2; r++)
                    MakeDoorPart(back, "Rib", new Vector3(r * 0.075f, 0f, 0.048f), new Vector3(0.035f, 0.46f, 0.012f), rib);
                MakeDoorPart(back, "Headrest", new Vector3(0f, 0.36f, -0.005f), new Vector3(0.24f, 0.17f, 0.08f), fabric);
                MakeDoorPart(back, "BackPiping", new Vector3(0f, 0.265f, 0f), new Vector3(0.41f, 0.02f, 0.095f), piping);
            }
        }

        void SpawnDriverIfPresent(Transform visualRoot)
        {
            var driverGo = GlbModelLoader.InstantiateDriver();
            if (driverGo.transform.childCount == 0)
            {
                Destroy(driverGo);
                return;
            }
            driverGo.transform.SetParent(visualRoot, false);
            driverGo.transform.localScale = Vector3.one * 0.72f;
            // Steering wheel on the side away from the curb, at about (-0.36, 1.14, 0.51) in the Fiat
            driverGo.transform.localPosition = _rig != null ? DriverSeatHQ : new Vector3(-0.34f, -0.12f, 0.22f);
            driverGo.transform.localRotation = Quaternion.identity;
            _driverActor = driverGo.AddComponent<PassengerActor>();
            _driverActor.Setup(PassengerCatalog.All[0], PassengerPose.DriverDriving);
        }

        void BuildChromeWipersAndDoor(Transform visualRoot)
        {
            var chromeMat = GlbModelLoader.MakeMat(new Color(0.92f, 0.94f, 0.96f), 0.95f, 1.0f);
            var yellowMat = GlbModelLoader.MakeMat(new Color(0.96f, 0.72f, 0.12f), 0.82f, 0.12f);
            _taxiAccentMaterial = yellowMat;
            _taxiAccentBaseColor = yellowMat.color;

            // Chrome windshield wipers on front windshield (the remade taxi has its own, and front seats are added)
            if (_rig == null)
            {
                _wiperL = MakeWiper(visualRoot, "ChromeWiper_L", new Vector3(-0.24f, 1.05f, 0.56f), chromeMat);
                _wiperR = MakeWiper(visualRoot, "ChromeWiper_R", new Vector3(0.20f, 1.05f, 0.56f), chromeMat);
            }
            else if (!GlbModelLoader.TaxiHasOwnSeats) BuildFrontSeatsHQ(visualRoot);

            // Rear vertical amber/red glowing tail & brake lights (matching foreground right of Station Arrival reference)
            _brakeOnMat = GlbModelLoader.MakeMat(new Color(1.0f, 0.36f, 0.08f), 0.90f);
            if (_brakeOnMat.HasProperty("_EmissionColor"))
            {
                _brakeOnMat.EnableKeyword("_EMISSION");
                _brakeOnMat.SetColor("_EmissionColor", new Color(1.0f, 0.32f, 0.05f) * 2.2f);
            }
            _brakeOffMat = GlbModelLoader.MakeMat(new Color(0.70f, 0.14f, 0.10f), 0.85f);
            if (_brakeOffMat.HasProperty("_EmissionColor"))
            {
                _brakeOffMat.EnableKeyword("_EMISSION");
                _brakeOffMat.SetColor("_EmissionColor", new Color(0.70f, 0.12f, 0.08f) * 0.45f);
            }

            // The remade taxi carries its own TAXI roof sign; the paint tint alone shows the equipped cosmetic
            if (_rig != null) return;
            var roofSign = new GameObject("TaxiCosmeticRoofSign");
            roofSign.transform.SetParent(visualRoot, false);
            roofSign.transform.localPosition = new Vector3(0f, 1.48f, 0.52f);
            var signBaseMat = GlbModelLoader.MakeMat(new Color(0.10f, 0.14f, 0.17f), 0.42f);
            MakeDoorPart(roofSign.transform, "SignBase", new Vector3(0f, -0.025f, 0f), new Vector3(0.36f, 0.05f, 0.16f), signBaseMat);
            MakeDoorPart(roofSign.transform, "MountLeft", new Vector3(-0.10f, -0.085f, 0f), new Vector3(0.035f, 0.09f, 0.035f), signBaseMat);
            MakeDoorPart(roofSign.transform, "MountRight", new Vector3(0.10f, -0.085f, 0f), new Vector3(0.035f, 0.09f, 0.035f), signBaseMat);
            var signLensMat = GlbModelLoader.MakeMat(TaxiCosmeticCatalog.Swatch(SaveSystem.Data.equippedTaxiCosmetic), 0.68f, 0.08f);
            var signLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            signLens.name = "ColorLens";
            signLens.transform.SetParent(roofSign.transform, false);
            signLens.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            signLens.transform.localScale = new Vector3(0.30f, 0.075f, 0.13f);
            Destroy(signLens.GetComponent<Collider>());
            _taxiCosmeticRenderer = signLens.GetComponent<Renderer>();
            _taxiCosmeticRenderer.sharedMaterial = signLensMat;
        }

        public void ApplyTaxiCosmetic(int id)
        {
            Color tint = TaxiCosmeticCatalog.Tint(id);
            if (_taxiPaintRenderers != null && GlbModelLoader.TaxiHasOwnSeats)
            {
                // Fiat taxi: flat-coloured materials (no textures), so the tint goes on the paint material only;
                // a property block would replace every part's colour (chrome, tyres, glass) with the tint.
                foreach (var renderer in _taxiPaintRenderers)
                {
                    if (renderer == null) continue;
                    foreach (var m in renderer.materials)
                    {
                        if (m == null || !m.name.StartsWith("taxi_paint")) continue;
                        string prop = m.HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
                        if (!_paintBase.TryGetValue(m, out var baseCol)) _paintBase[m] = baseCol = m.GetColor(prop);
                        m.SetColor(prop, baseCol * tint);
                    }
                }
            }
            else if (_taxiPaintRenderers != null)
            {
                var block = new MaterialPropertyBlock();
                foreach (var renderer in _taxiPaintRenderers)
                {
                    if (renderer == null) continue;
                    var material = renderer.sharedMaterial;
                    if (material == null) continue;
                    renderer.GetPropertyBlock(block);
                    if (material.HasProperty("_BaseColor")) block.SetColor("_BaseColor", tint);
                    if (material.HasProperty("_Color")) block.SetColor("_Color", tint);
                    // glTFast's own shader names its tint baseColorFactor; without this the paint never changed.
                    if (material.HasProperty("baseColorFactor")) block.SetColor("baseColorFactor", tint);
                    renderer.SetPropertyBlock(block);
                }
            }
            Color swatch = TaxiCosmeticCatalog.Swatch(id);
            if (_taxiAccentMaterial != null)
                _taxiAccentMaterial.color = id == 0 ? _taxiAccentBaseColor : swatch;
            if (_taxiCosmeticRenderer != null && _taxiCosmeticRenderer.sharedMaterial != null)
                _taxiCosmeticRenderer.sharedMaterial.color = swatch;
        }

        void EnhanceTaxiPbrMaterials()
        {
            if (_taxiPaintRenderers == null) return;

            foreach (var r in _taxiPaintRenderers)
            {
                if (r == null) continue;
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();

                    // Aged glossy paint: broad readable reflections rather than a polished yellow mirror.
                    if (n.StartsWith("taxi_paint") || n.Contains("body_paint"))
                    {
                        Color paint = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : m.color;
                        var clearcoat = Shader.Find("Universal Render Pipeline/Complex Lit");
                        if (clearcoat != null)
                        {
                            m.shader = clearcoat;
                            m.SetColor("_BaseColor", paint);
                            m.SetFloat("_ClearCoatMask", 1f);
                            m.SetFloat("_ClearCoatSmoothness", 0.88f);
                            m.EnableKeyword("_CLEARCOAT");
                        }
                        SetPbrResponse(m, 0.74f, 0.08f);
                        string prop = m.HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
                        if (m.HasProperty(prop))
                        {
                            Color c = m.GetColor(prop);
                            if (c.r > 0.65f && c.g > 0.40f && c.b < 0.25f)
                            {
                                c = new Color(0.961f, 0.722f, 0.122f, 1f);
                                m.SetColor(prop, c);
                            }
                        }
                    }
                    // 2. Chrome (Bumpers, Hubcaps, Mirror, Trim): 100% Mirror-smooth metal
                    else if (n.Contains("chrome") || n.Contains("mirror"))
                    {
                        SetPbrResponse(m, 0.88f, 1f);
                        string prop = m.HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
                        if (m.HasProperty(prop)) m.SetColor(prop, new Color(0.92f, 0.94f, 0.96f, 1f));
                    }
                    // 3. Glass: Clean tinted windshield and windows with high specular sheen
                    else if (n.Contains("glass"))
                    {
                        SetPbrResponse(m, 0.93f, 0f);
                    }
                    // 4. Headlights: warm glowing light interior
                    else if (n.Contains("headlight") || n.Contains("lights_inside"))
                    {
                        SetPbrResponse(m, 0.92f, 0f);
                        if (m.HasProperty("emissiveFactor"))
                            m.SetColor("emissiveFactor", new Color(1f, 0.89f, 0.61f) * 1.45f);
                        if (m.HasProperty("_EmissionColor"))
                        {
                            m.EnableKeyword("_EMISSION");
                            m.SetColor("_EmissionColor", new Color(1.0f, 0.91f, 0.69f) * 1.25f);
                        }
                    }
                    // 5. Tyres: Realistic deep matte rubber
                    else if (n.Contains("tire") || n.Contains("tyre"))
                    {
                        SetPbrResponse(m, 0.20f, 0f);
                    }
                    else if (n.Contains("seat"))
                    {
                        ApplyGreenSeatFabric(m);
                    }
                }
            }
        }

        static void SetPbrResponse(Material material, float smoothness, float metallic)
        {
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("roughnessFactor")) material.SetFloat("roughnessFactor", 1f - smoothness);
            if (material.HasProperty("metallicFactor")) material.SetFloat("metallicFactor", metallic);
        }

        static void ApplyGreenSeatFabric(Material material)
        {
            var fabric = Resources.Load<Texture2D>("ReferenceKit/Textures/GreenSeatFabric");
            material.name = "IstanbulGreenSeatFabric";
            SetPbrResponse(material, 0.22f, 0f);
            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("baseColorFactor")) material.SetColor("baseColorFactor", Color.white);
            if (fabric == null) return;
            material.mainTexture = fabric;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", fabric);
            if (material.HasProperty("baseColorTexture")) material.SetTexture("baseColorTexture", fabric);
            material.mainTextureScale = new Vector2(1.6f, 1f);
            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", new Vector2(1.6f, 1f));
        }

        void MakeDoorPart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Transform MakeWiper(Transform parent, string name, Vector3 pivotPos, Material mat)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pivotPos;
            pivot.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);

            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(pivot.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            blade.transform.localScale = new Vector3(0.022f, 0.32f, 0.022f);
            Destroy(blade.GetComponent<Collider>());
            blade.GetComponent<Renderer>().sharedMaterial = mat;
            return pivot.transform;
        }

        /// <summary>Taxi-local centre of the entire rear bench and cabin.</summary>
        public const float BackSeatCenterX = 0.19f;

        /// <summary>Rear-right seating position, directly in front of the authored right headrest (+X).</summary>
        public static readonly Vector3 RearRightPassengerPosition = new Vector3(BackSeatCenterX + .303f, -.04f, -.74f);

        void BuildBackSeatInterior()
        {
            if (!TaxiCabinPresentation.Available) { BuildLegacyBackSeatInterior(); return; }
            _backSeatInterior = new GameObject("BackSeatCabInterior");
            _backSeatInterior.transform.SetParent(transform, false);
            TaxiCabinPresentation.TryCreate(_backSeatInterior.transform);
            BuildCabinPortraitLights();
            SetLayerRecursive(_backSeatInterior.transform, CabLayer);
        }

        void BuildCabinPortraitLights()
        {
            // A broad warm key and restrained cool fill keep the face readable
            // against the sage upholstery without bleaching either surface.
            const float cx = BackSeatCenterX;
            CreateCabinLight("CabinFillLight", new Vector3(cx - .12f, 1.25f, .35f),
                new Color(.80f, .89f, 1f), .38f, 3.2f, false);
            CreateCabinLight("CabinKeyLight", new Vector3(cx + .31f, 1.35f, .25f),
                new Color(1f, .94f, .84f), .70f, 3f, true);
            CreateCabinLight("CabinRimLight", new Vector3(cx - .25f, 1.3f, -1.05f),
                new Color(.69f, .84f, 1f), .28f, 1.1f, false);
        }

        void CreateCabinLight(string name, Vector3 position, Color color, float intensity, float range, bool spotlight)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(_backSeatInterior.transform, false);
            light.transform.localPosition = position;
            light.type = spotlight ? LightType.Spot : LightType.Point;
            if (spotlight)
            {
                Vector3 focus = RearRightPassengerPosition + new Vector3(0f, 1.04f, .05f);
                light.transform.localRotation = Quaternion.LookRotation(focus - position);
                light.spotAngle = 66f;
                light.innerSpotAngle = 38f;
            }
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << CabLayer;
        }

        void BuildLegacyBackSeatInterior()
        {
            // Back-seat cab interior (reference: passenger portrait in the rear seat, eye-level camera):
            // - Camera looks from the front row (+Z) toward the rear seat (-Z), so +X is screen-left.
            // - The user's ribbed green fabric on the bench and the two small headrests.
            // - Warm beige headliner on top, yellow-painted window frames and pillars, open side and rear windows
            //   showing the street behind the taxi.
            _backSeatInterior = new GameObject("BackSeatCabInterior");
            _backSeatInterior.transform.SetParent(transform, false);

            var leatherMat = GlbModelLoader.MakeMat(new Color(.105f, .30f, .255f), .22f);
            var seamMat = GlbModelLoader.MakeMat(new Color(.035f, .12f, .105f), .24f);
            var headlinerMat = GlbModelLoader.MakeMat(new Color(.74f, .65f, .48f), .16f);
            var paintMat = GlbModelLoader.MakeMat(new Color(.94f, .59f, .10f), .66f, .08f);
            var doorCardMat = GlbModelLoader.MakeMat(new Color(.075f, .21f, .18f), .36f);
            var chromeMat = GlbModelLoader.MakeMat(new Color(.58f, .65f, .61f), .68f, .75f);
            var domeLightMat = GlbModelLoader.MakeMat(new Color(1f, 0.93f, 0.78f), 0.5f);
            if (domeLightMat.HasProperty("_EmissionColor"))
            {
                domeLightMat.EnableKeyword("_EMISSION");
                domeLightMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 1.4f);
            }

            const float cx = BackSeatCenterX, half = 0.55f;
            float wallL = cx - half, wallR = cx + half;

            // Rear bench: cushion, slightly reclined backrest with tufted seams, two headrests on posts
            MakeInteriorPartReturn("Floor", new Vector3(cx, 0f, -0.3f), new Vector3(half * 2f, 0.08f, 2.6f), leatherMat);
            MakeInteriorPartReturn("SeatCushion", new Vector3(cx, 0.30f, -0.78f), new Vector3(half * 2f, 0.22f, 0.62f), leatherMat);
            MakeInteriorPartReturn("SeatCushionEdge", new Vector3(cx, 0.34f, -0.48f), new Vector3(half * 2f, 0.16f, 0.06f), seamMat);
            // Backrest ends at shoulder height; the headrests rise from it on either side of the passenger's head
            var back = MakeInteriorPartReturn("SeatBackrest", new Vector3(cx, 0.64f, -1.12f), new Vector3(half * 2f, 0.62f, 0.2f), leatherMat);
            back.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            for (int s = -2; s <= 2; s++)
            {
                var seam = MakeInteriorPartReturn("BackrestSeam", new Vector3(cx + s * 0.22f, 0.64f, -1.01f), new Vector3(0.008f, 0.54f, 0.012f), seamMat);
                seam.localRotation = back.localRotation;
            }
            // Rounded bolster along the top of the backrest (a capsule lying across the seat)
            var bolster = MakeInteriorPrimitive(PrimitiveType.Capsule, "BackrestBolster", new Vector3(cx, 0.95f, -1.16f), new Vector3(0.2f, half, 0.2f), leatherMat);
            bolster.localRotation = back.localRotation * Quaternion.Euler(0f, 0f, 90f);
            foreach (float hx in new[] { cx - 0.31f, cx + 0.31f })
            {
                // pillow-shaped headrests on chrome posts
                var hr = MakeInteriorPrimitive(PrimitiveType.Sphere, "Headrest", new Vector3(hx, 1.02f, -1.2f), new Vector3(0.23f, 0.17f, 0.09f), leatherMat);
                hr.localRotation = back.localRotation;
                foreach (float px in new[] { -0.06f, 0.06f })
                    MakeInteriorPrimitive(PrimitiveType.Cylinder, "HeadrestPost", new Vector3(hx + px, 0.93f, -1.19f), new Vector3(0.014f, 0.035f, 0.014f), chromeMat);
            }

            // Rear window: parcel shelf below, painted C-pillars either side, open glass area to the street
            MakeInteriorPartReturn("RearBulkhead", new Vector3(cx, 0.86f, -1.32f), new Vector3(half * 2f, 0.2f, 0.06f), leatherMat);
            MakeInteriorPartReturn("ParcelShelf", new Vector3(cx, 0.97f, -1.42f), new Vector3(half * 2f, 0.04f, 0.32f), seamMat);
            MakeInteriorPartReturn("CPillarL", new Vector3(wallL + 0.07f, 1.2f, -1.40f), new Vector3(0.14f, 0.5f, 0.34f), headlinerMat);
            MakeInteriorPartReturn("CPillarR", new Vector3(wallR - 0.07f, 1.2f, -1.40f), new Vector3(0.14f, 0.5f, 0.34f), headlinerMat);

            // Headliner with a warm dome light
            MakeInteriorPartReturn("Headliner", new Vector3(cx, 1.48f, -0.3f), new Vector3(half * 2f + 0.1f, 0.08f, 2.7f), headlinerMat);
            MakeInteriorPartReturn("HeadlinerRear", new Vector3(cx, 1.43f, -1.45f), new Vector3(half * 2f, 0.06f, 0.2f), paintMat);
            MakeInteriorPartReturn("DomeLight", new Vector3(cx, 1.435f, -0.15f), new Vector3(0.22f, 0.02f, 0.12f), domeLightMat);
            BuildCabinPortraitLights();

            // Side doors: door card with armrest and handle, yellow sill and roof rail framing an open window
            foreach (float wx in new[] { wallL, wallR })
            {
                float inward = wx < cx ? 1f : -1f;
                MakeInteriorPartReturn("DoorCard", new Vector3(wx, 0.5f, -0.45f), new Vector3(0.06f, 0.84f, 2.0f), doorCardMat);
                MakeInteriorPartReturn("Armrest", new Vector3(wx + inward * 0.06f, 0.62f, -0.55f), new Vector3(0.1f, 0.06f, 0.5f), leatherMat);
                MakeInteriorPartReturn("DoorHandle", new Vector3(wx + inward * 0.04f, 0.78f, -0.15f), new Vector3(0.02f, 0.03f, 0.12f), chromeMat);
                MakeInteriorPartReturn("WindowSill", new Vector3(wx, 0.94f, -0.45f), new Vector3(0.09f, 0.05f, 2.0f), paintMat);
                MakeInteriorPartReturn("RoofRail", new Vector3(wx, 1.41f, -0.45f), new Vector3(0.09f, 0.07f, 2.0f), paintMat);
                MakeInteriorPartReturn("BPillar", new Vector3(wx, 1.17f, 0.45f), new Vector3(0.09f, 0.48f, 0.14f), paintMat);
            }

            // Shoulder seatbelt across the passenger, as in the webcam reference
            var beltMat = GlbModelLoader.MakeMat(new Color(0.07f, 0.07f, 0.075f), 0.25f);
            // (seated passengers' chest front is at about z = -0.40, so the strap lies just in front of it;
            // its top end stops at shoulder height, clear of the face)
            var belt = MakeInteriorPartReturn("SeatBelt", new Vector3(cx + 0.01f, 0.68f, -0.365f), new Vector3(0.030f, 0.35f, 0.008f), beltMat);
            belt.localRotation = Quaternion.Euler(0f, 0f, -38f);

            SetLayerRecursive(_backSeatInterior.transform, CabLayer);
        }

        Transform MakeInteriorPrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = MakeInteriorPartReturn(name, pos, scale, mat);
            var src = GameObject.CreatePrimitive(type);
            t.GetComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            Destroy(src);
            return t;
        }

        /// <summary>Buttoned leather: diamond quilting with darker stitch grooves, a soft sheen on each pillow, buttons.</summary>
        static Texture2D MakeTuftedLeatherTexture()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, name = "TuftedLeather" };
            var baseCol = new Color(0.085f, 0.058f, 0.045f);
            var rng = new System.Random(3);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // diamond cell coordinates
                    float u = (x + 0.5f) / s * 2f, v = (y + 0.5f) / s * 2f;
                    float a = (u + v) % 1f, b = (u - v + 2f) % 1f;
                    float da = Mathf.Abs(a - 0.5f) * 2f, db = Mathf.Abs(b - 0.5f) * 2f;   // 0 centre .. 1 seam
                    float edge = Mathf.Max(da, db);
                    float pillow = 1f - edge * edge;                       // puffed centre
                    float st = Mathf.Clamp01((edge - 0.86f) / 0.12f);
                    float seam = st * st * (3f - 2f * st);                  // stitched groove
                    float button = 1f - Mathf.SmoothStep(0.0f, 0.12f, Mathf.Max(1f - da, 1f - db) < 0.1f ? 0f : 1f);
                    float grain = 0.94f + (float)rng.NextDouble() * 0.08f;
                    float lum = (0.75f + 0.55f * pillow) * (1f - seam * 0.6f) * grain;
                    var c = baseCol * lum;
                    if (da > 0.92f && db > 0.92f) c *= 0.35f;              // button dimple at the cell corners
                    tex.SetPixel(x, y, c);
                }
            tex.Apply(true);
            return tex;
        }

        Transform MakeInteriorPartReturn(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_backSeatInterior.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            // The roof would otherwise put the whole cabin (and the passenger) in the sun's shadow
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static void MakeChildBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        public System.Action<bool> OnExteriorVisibilityChanged;

        public void SetExteriorVisible(bool visible)
        {
            if (_visual != null && _visual.gameObject.activeSelf != visible)
                _visual.gameObject.SetActive(visible);
            ShowCabToMainCamera(!visible);
            SynthAudio.Instance?.SetCabInterior(!visible);
            OnExteriorVisibilityChanged?.Invoke(visible);
        }

        public void SetWipersActive(bool active)
        {
            _wipersActive = active;
            if (!active)
            {
                if (_wiperL != null) _wiperL.localRotation = Quaternion.Euler(-28f, 0f, -35f);
                if (_wiperR != null) _wiperR.localRotation = Quaternion.Euler(-28f, 0f, -35f);
            }
        }

        public void SetDoorOpen(bool open)
        {
            if (_doorOpen != open)
            {
                _doorOpen = open;
                SynthAudio.Instance?.PlayDoor(open);
            }
            if (!open && _leftDoorOpen)
                SetLeftDoorOpen(false);
        }

        public void SetLeftDoorOpen(bool open)
        {
            if (_leftDoorOpen != open)
            {
                _leftDoorOpen = open;
                SynthAudio.Instance?.PlayDoor(open);
            }
        }

        public void TriggerBoost(float duration = 2.4f)
        {
            _boostTimer = Mathf.Max(0f, duration);
            _slowTimer = 0f;
        }

        public void TriggerSlowdown(float duration = 2.2f)
        {
            _slowTimer = Mathf.Max(0f, duration);
            _boostTimer = 0f;
        }

        public void StopImmediately()
        {
            Speed = 0f;
            TargetSpeed = 0f;
            _boostTimer = 0f;
            _slowTimer = 0f;
            ResetHandling(0f);
        }

        public void WarpToStop(int stopIndex)
        {
            float z = Mathf.Max(0f, (stopIndex - 1) * GameConfig.StopSpacing - 45f);
            transform.position = new Vector3(GameConfig.DriveLaneX, 0f, z);
            TargetLateralX = GameConfig.DriveLaneX;
            _boostTimer = _slowTimer = 0f;
            Speed = EffectiveCruiseSpeed * 0.6f;
            ResetHandling(Speed);
        }

        void ResetHandling(float speed)
        {
            _motion.Reset(speed);
            _roadFeel.Reset();
            _bodyPitch = _bodyRoll = _bodyYaw = _steeringAngle = 0f;
            if (_visual == null) return;
            _visual.SetLocalPositionAndRotation(_visualRestPosition, _visualRestRotation);
            if (_rig != null) { _rig.Tick(_leftDoorOpen, 0f, 0f, 0f); _rig.SetSuspension(0f, 0f, 0f, 0f); }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) { _surfaceFeedback?.Tick(0f, in _roadFeel, !ExteriorVisible, 0f); return; }
            float desired = Mathf.Max(0f, TargetSpeed);
            if (_boostTimer > 0f)
            {
                _boostTimer = Mathf.Max(0f, _boostTimer - dt);
                desired = Mathf.Max(desired, EffectiveCruiseSpeed + 4.5f);
            }
            else if (_slowTimer > 0f)
            {
                _slowTimer = Mathf.Max(0f, _slowTimer - dt);
                desired = Mathf.Min(desired, GameConfig.SlowSpeed);
            }

            bool isBraking = desired < Speed - 0.5f;
            Vector3 pos = transform.position;
            _motion.Step(ref pos, desired, TargetLateralX, _laneSmoothTime, _maxLateralSpeed, dt);
            Speed = _motion.Speed;
            transform.position = pos;
            _roadFeel.Step(pos.x, pos.z, Speed, _motion.LateralVelocity, CityChunkStreamer.CurrentRoad, dt);
            _surfaceFeedback?.Tick(Speed, in _roadFeel, !ExteriorVisible, dt);
            if (_backSeatInterior != null)
                _backSeatInterior.transform.localPosition = _cabinRestPosition + Vector3.up * (_roadFeel.Heave * .35f);

            float response = 1f - Mathf.Exp(-_bodyResponse * dt);
            float yaw = Mathf.Atan2(_motion.LateralVelocity, Mathf.Max(2f, Speed)) * Mathf.Rad2Deg;
            _bodyYaw = Mathf.Lerp(_bodyYaw, Mathf.Clamp(yaw, -12f, 12f), response);
            _steeringAngle = Mathf.Lerp(_steeringAngle, Mathf.Clamp(yaw * 1.5f,
                -_maxSteeringAngle, _maxSteeringAngle), response);
            _bodyPitch = Mathf.Lerp(_bodyPitch, Mathf.Clamp(-_motion.Acceleration * 0.55f,
                -_maxPitch, _maxPitch), response);
            _bodyRoll = Mathf.Lerp(_bodyRoll, Mathf.Clamp(-_motion.LateralAcceleration * 0.35f,
                -_maxRoll, _maxRoll), response);

            SynthAudio.Instance?.UpdateEngineSound(Speed, isBraking);
            VfxDirector.Instance?.UpdateDrivingVfx(transform, Kmh, ExteriorVisible);
            var weather = WeatherDirector.Instance;
            bool headlamps = ExteriorVisible && weather != null &&
                (weather.CurrentWeather == WeatherPreset.Night || weather.CurrentWeather == WeatherPreset.Rain
                 || weather.CurrentWeather == WeatherPreset.FoggyMorning);
            for (int i = 0; i < _headlights.Length; i++)
                if (_headlights[i] != null && _headlights[i].enabled != headlamps) _headlights[i].enabled = headlamps;

            if (_visual != null && _visual.gameObject.activeSelf)
            {
                Vector3 bodyPosition = _visualRestPosition;
                bodyPosition.y += _roadFeel.Heave;
                _visual.SetLocalPositionAndRotation(bodyPosition,
                    _visualRestRotation * Quaternion.Euler(_bodyPitch + _roadFeel.Pitch, _bodyYaw, _bodyRoll + _roadFeel.Roll));

                // Animate chrome windshield wipers back and forth during Rain
                if (_rig != null)
                {
                    _rig.Tick(_leftDoorOpen, Speed, _steeringAngle, dt);
                    _rig.SetSuspension(_roadFeel.FrontLeft, _roadFeel.FrontRight, _roadFeel.RearLeft, _roadFeel.RearRight);
                }

                if (_wipersActive && _wiperL != null && _wiperR != null)
                {
                    float sweep = Mathf.Sin(Time.time * 6.5f) * 42f;
                    _wiperL.localRotation = Quaternion.Euler(-28f, 0f, sweep);
                    _wiperR.localRotation = Quaternion.Euler(-28f, 0f, sweep);
                }

                // Rear amber/red glowing tail & brake lights when braking or stopped at station
                bool tailGlow = isBraking || Speed < 1.5f || _leftDoorOpen || _doorOpen;
                if (_brakeGlowL != null && _brakeGlowR != null)
                {
                    var m = tailGlow ? _brakeOnMat : _brakeOffMat;
                    if (_brakeGlowL.sharedMaterial != m) _brakeGlowL.sharedMaterial = m;
                    if (_brakeGlowR.sharedMaterial != m) _brakeGlowR.sharedMaterial = m;
                }
            }
        }
    }
}

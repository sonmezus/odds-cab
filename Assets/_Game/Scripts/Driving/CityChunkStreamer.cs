using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Faithful 3D recreation of 03-city.png and 07-material.png:
    /// - Procedural textures matching 07-material.png:
    ///   1) Weathered cream stucco plaster
    ///   2) Corrugated terracotta barrel roof tiles
    ///   3) Deeply furrowed brown tree bark with moss accents
    ///   4) Woven navy-blue canvas fabric
    ///   5) Square-cut cobblestone & limestone ashlar sidewalk blocks
    ///   6) Blue-and-white striped awning & blue-and-white Iznik ceramic frieze
    /// - Exact 3-building attached townhouse row from 03-city.png:
    ///   - Left Cream Building: Stone side staircase with pots, corner quoins, blue-shuttered arched windows,
    ///     wrought-iron flower balcony, blue-and-white striped cafe awning, bistro table & chairs, climbing bougainvillea, terracotta roof
    ///   - Middle Coral-Terracotta Building: Arched teal-green panelled door in stone arch, terracotta-red awning over blue shopfront,
    ///     upper arched windows with ivy/vines, rooftop wooden pergola & terrace railing
    ///   - Right Cream Building: Blue-and-white patterned ceramic tile frieze panels, central arched window & balcony,
    ///     solid Mediterranean-blue awning over warm glowing arched shopfront, rooftop stone balustrade
    /// - Exact street objects from 03-city.png:
    ///   - Swan-neck cast-iron streetlamp with hanging hexagonal warm lantern
    ///   - Slatted wooden bench with cast-iron scroll legs
    ///   - Yellow-circle topped stop sign with white rectangular schedule board
    ///   - Dark-green ribbed cylindrical trash bin with domed lid
    ///   - 4 planter types (terracotta amphora jar, stone flower trough, terracotta olive pot, long stone flower box)
    /// - Exact 3 tree types from 03-city.png:
    ///   1) Gnarled leaning Mediterranean Plane/Olive tree with olive-green canopy & base rocks
    ///   2) Tall conical dark-green Italian Cypress spire with base stones
    ///   3) Multi-stemmed pink-blossoming Judas/Bougainvillea tree with fallen pink petals
    /// - Coastal vista with turquoise sea, white sailboats, and distant hillside town with tower.
    /// </summary>
    public class CityChunkStreamer : MonoBehaviour
    {
        const int VisibleChunkCapacity = GameConfig.ChunksBehind + GameConfig.ChunksAhead + 1;
        const int PreparedChunkCapacity = VisibleChunkCapacity + StreetVariants;
        readonly Dictionary<int, GameObject> _activeChunks = new Dictionary<int, GameObject>(VisibleChunkCapacity);
        // Prefer the seeded variant, then reuse another prepared variant when that pool is occupied.
        const int StreetVariants = 6;
        readonly Dictionary<int, Queue<GameObject>> _variantPools = new Dictionary<int, Queue<GameObject>>(StreetVariants);
        readonly Dictionary<GameObject, int> _chunkVariant = new Dictionary<GameObject, int>(PreparedChunkCapacity);
        readonly HashSet<Mesh> _generatedMeshes = new HashSet<Mesh>();
        readonly HashSet<Mesh> _layoutMeshes = new HashSet<Mesh>();
        bool _initialized, _buildingLayout, _preparingStreetLayout, _streetLayoutReady;
        int _layoutPreparationVersion;
        TourRoadType _preparedRoad;
        int _preparedDistrict = -1;
        DistrictLifeDirector _districtLife;
        public int PreparedChunkCount => _chunkVariant.Count;

        /// <summary>Deterministic variant per chunk; never the same variant twice in a row.</summary>
        static int VariantForChunk(int idx)
        {
            int v = (int)(((uint)(idx * 2654435761u)) >> 7) % StreetVariants;
            int prev = idx > 0 ? (int)(((uint)((idx - 1) * 2654435761u)) >> 7) % StreetVariants : -1;
            if (v == prev) v = (v + 1) % StreetVariants;
            return v;
        }
        
        readonly List<RoadsidePedestrianVisual> _roadsidePedestrians = new List<RoadsidePedestrianVisual>();
        readonly List<int> _chunkKeysToRemove = new List<int>(VisibleChunkCapacity);
        int _lastCenterChunk = int.MinValue;

        Transform _taxi;

        // Materials with procedural textures from 07-material.png & 03-city.png
        Material _roadMat, _laneMat, _sidewalkCobbleMat, _limestoneMat, _curbStoneMat, _seaMat;
        bool _seaHasGpuWaves;
        Material _wetPuddleMat;
        Material _kerbBeigeMat, _kerbGreyMat, _gutterMat, _weedMat, _crosswalkMat;
        readonly List<MeshRenderer> _wetPuddleRenderers = new List<MeshRenderer>();
        bool _roadWet;
        Material _creamPlasterMat, _coralPlasterMat, _whitePlasterMat, _terracottaRoofMat;
        Material _blueShutterMat, _tealDoorMat, _navyCanvasMat, _redAwningMat, _stripedAwningMat, _iznikFriezeMat;
        Material _warmShopGlassMat, _darkGlassMat, _shopWindowGlassMat, _ironMat, _benchWoodMat, _pergolaWoodMat;
        Material _stopYellowMat, _stopBoardWhiteMat, _trashGreenMat, _lampGlowMat;
        Material _terracottaPotMat, _oliveBarkMat, _gnarledBarkMat;
        Material _oliveLeafMat, _cypressLeafMat, _pinkBlossomMat, _bougainvilleaVineMat;
        Material _pedestrianSkinMat, _pedestrianTrousersMat;
        Material _ferryBlueMat, _sailMat;
        Material[] _pedestrianCoatMats;
        Material[] _carMats;

        GameObject _distantHorizonVista;
        Transform _horizonFarLayer, _horizonMidLayer;
        bool _usesReferenceHorizon;
        Transform _referenceSea;
        ReferenceShoreline _referenceShoreline;
        float _referenceSeaLevel = float.NaN;
        Transform _vistaFerry;
        float? _vistaFerryBaseY;
        Quaternion _vistaFerryRestRotation;
        readonly List<Transform> _vistaBoats = new List<Transform>();
        readonly List<Vector3> _vistaBoatsBase = new List<Vector3>();
        readonly List<float> _vistaBoatsPhase = new List<float>();
        readonly List<Quaternion> _vistaBoatsRestRotation = new List<Quaternion>(3);

        // District identity: one landmark per district in the horizon vista + a light plaster tint.
        readonly GameObject[] _districtLandmarks = new GameObject[8];
        readonly List<Transform> _windmillRotors = new List<Transform>();
        int _district = -1;
        public int District => _district;
        /// <summary>Street type and water side (+1 right, -1 left, 0 none) of the current district (TourRoute).</summary>
        public static TourRoadType CurrentRoad { get; private set; } = TourRoadType.Coastal;
        public static int CurrentSeaSide { get; private set; } = 1;
        /// <summary>True when the curb (right) pavement is an open promenade or bridge footway rather than a furnished building side.</summary>
        public static bool CurbIsOpen => CurrentRoad == TourRoadType.Bridge || (CurrentRoad == TourRoadType.Coastal && CurrentSeaSide > 0);
        bool _streetHidden;
        public Material SeaMaterial => _seaMat;

        /// <summary>Hides the streamed street (chunks and pavement walkers), e.g. while the taxi crosses on the car ferry;
        /// the horizon vista stays. Chunks streamed in meanwhile stay hidden until the street is shown again.</summary>
        public void SetStreetVisible(bool visible)
        {
            _streetHidden = !visible;
            foreach (var kv in _activeChunks) kv.Value.SetActive(visible);
            _districtLife?.SetStreetVisible(visible);
            foreach (var p in _roadsidePedestrians) if (!visible) p.Root.gameObject.SetActive(false);
        }
        Material _hillOliveMat;
        readonly Material[] _districtMuralMaterials = new Material[2];

        sealed class RoadsidePedestrianVisual
        {
            public Transform Root;
            public Transform LeftLeg, RightLeg, LeftArm, RightArm;
            public float StepPhase;
        }

        public void Initialize(Transform taxi, int initialDistrict = 0)
        {
            if (_initialized) return;
            _taxi = taxi;
            gameObject.AddComponent<CurveBender>().Init(taxi);
            CreatePaletteAndProceduralTextures();
            ApplyPhotoTextures();
            BuildDistantCoastalHorizon();
            gameObject.AddComponent<BosphorusSeaState>().Initialize(_seaMat);
            CreateLampLightPool();
            _initialized = true;
            SetDistrict(initialDistrict);
            _districtLife = gameObject.AddComponent<DistrictLifeDirector>();
            _districtLife.Initialize(taxi, initialDistrict);
        }

        void CreatePaletteAndProceduralTextures()
        {
            _roadMat = GlbModelLoader.MakeMat(new Color(0.21f, 0.22f, 0.25f), 0.28f, 0.05f);
            _wetPuddleMat = GlbModelLoader.MakeMat(new Color(0.045f, 0.065f, 0.085f), 0.62f, 0.015f);
            _laneMat = GlbModelLoader.MakeMat(new Color(0.94f, 0.92f, 0.86f), 0.3f);

            // 07-material.png #6: Cobblestone & Ashlar Limestone sidewalk blocks
            _sidewalkCobbleMat = GlbModelLoader.MakeMat(new Color(0.82f, 0.77f, 0.69f), 0.24f);
            _sidewalkCobbleMat.mainTexture = MakeCobblestoneTexture();
            _sidewalkCobbleMat.mainTextureScale = new Vector2(4f, 24f);

            _limestoneMat = GlbModelLoader.MakeMat(new Color(0.91f, 0.85f, 0.75f), 0.22f);
            _curbStoneMat = GlbModelLoader.MakeMat(new Color(0.62f, 0.59f, 0.54f), 0.25f);
            _kerbBeigeMat = GlbModelLoader.MakeMat(new Color(0.90f, 0.82f, 0.66f), 0.2f);
            _kerbGreyMat = GlbModelLoader.MakeMat(new Color(0.62f, 0.62f, 0.64f), 0.2f);
            _gutterMat = GlbModelLoader.MakeMat(new Color(0.52f, 0.51f, 0.50f), 0.15f);
            _weedMat = GlbModelLoader.MakeMat(new Color(0.42f, 0.55f, 0.20f), 0.1f);
            _crosswalkMat = GlbModelLoader.MakeMat(new Color(0.90f, 0.86f, 0.74f), 0.25f);
            var waterShader = Resources.Load<Shader>("BosphorusWater") ?? Shader.Find("OddsCab/Bosphorus Water");
            if (waterShader != null)
            {
                _seaMat = new Material(waterShader);
                _seaHasGpuWaves = true;
                _seaMat.name = "BosphorusSea";
                var ripples = Resources.Load<Texture2D>("Textures/BosphorusWater");
                if (ripples != null) _seaMat.SetTexture("_MainTex", ripples);
                _seaMat.SetColor("_NearColor", new Color(0.06f, 0.38f, 0.47f));
                _seaMat.SetColor("_DeepColor", new Color(0.02f, 0.15f, 0.32f));
                _seaMat.SetColor("_SkyColor", new Color(0.66f, 0.76f, 0.88f));
            }
            else
            {
                // Keep the simple scrolling-water fallback if the custom shader is unavailable.
                _seaMat = GlbModelLoader.MakeMat(new Color(0.08f, 0.44f, 0.65f), 0.92f, 0.05f);
                _seaMat.mainTexture = MakeSeaRippleTexture();
                _seaMat.mainTextureScale = new Vector2(6f, 10f);
            }
            _hillOliveMat = GlbModelLoader.MakeMat(new Color(0.42f, 0.52f, 0.36f), 0.12f);

            // 07-material.png #2: Weathered Cream & Coral Stucco Plaster
            var plasterTex = MakeWeatheredPlasterTexture();
            _creamPlasterMat = GlbModelLoader.MakeMat(new Color(0.95f, 0.88f, 0.76f), 0.18f);
            _creamPlasterMat.mainTexture = plasterTex;
            _coralPlasterMat = GlbModelLoader.MakeMat(new Color(0.89f, 0.52f, 0.41f), 0.18f);
            _coralPlasterMat.mainTexture = plasterTex;
            _whitePlasterMat = GlbModelLoader.MakeMat(new Color(0.96f, 0.93f, 0.86f), 0.20f);
            _whitePlasterMat.mainTexture = plasterTex;

            // 07-material.png #3: Corrugated Terracotta Barrel Roof Tiles
            _terracottaRoofMat = GlbModelLoader.MakeMat(new Color(0.82f, 0.36f, 0.21f), 0.28f);
            _terracottaRoofMat.mainTexture = MakeTerracottaTileTexture();
            _terracottaRoofMat.mainTextureScale = new Vector2(6f, 4f);

            // 03-city.png Architectural Accents: Mediterranean Blue Shutters, Teal-Green Arched Door, Striped & Solid Awnings
            _blueShutterMat = GlbModelLoader.MakeMat(new Color(0.20f, 0.44f, 0.68f), 0.32f);
            _tealDoorMat = GlbModelLoader.MakeMat(new Color(0.18f, 0.42f, 0.38f), 0.35f);

            // 07-material.png #5: Woven Navy-Blue Canvas Fabric
            _navyCanvasMat = GlbModelLoader.MakeMat(new Color(0.16f, 0.38f, 0.62f), 0.22f);
            _redAwningMat = GlbModelLoader.MakeMat(new Color(0.82f, 0.31f, 0.24f), 0.22f);
            _stripedAwningMat = GlbModelLoader.MakeMat(Color.white, 0.24f);
            _stripedAwningMat.mainTexture = MakeBlueWhiteStripedTexture();

            // Blue-and-white patterned ceramic frieze on Right Building
            _iznikFriezeMat = GlbModelLoader.MakeMat(Color.white, 0.45f);
            _iznikFriezeMat.mainTexture = MakeIznikTileFriezeTexture();

            // Warm interior shopfront glow & upper window glass
            _warmShopGlassMat = GlbModelLoader.MakeMat(new Color(0.98f, 0.78f, 0.42f), 0.75f);
            if (_warmShopGlassMat.HasProperty("_EmissionColor"))
            {
                _warmShopGlassMat.EnableKeyword("_EMISSION");
                _warmShopGlassMat.SetColor("_EmissionColor", new Color(0.85f, 0.58f, 0.22f));
            }
            _darkGlassMat = GlbModelLoader.MakeMat(new Color(0.22f, 0.28f, 0.35f), 0.8f);
            _shopWindowGlassMat = GlbModelLoader.MakeMat(new Color(0.58f, 0.78f, 0.86f, 0.16f), 0.82f);
            _shopWindowGlassMat.SetFloat("_Surface", 1f);
            _shopWindowGlassMat.SetFloat("_Blend", 0f);
            _shopWindowGlassMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _shopWindowGlassMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _shopWindowGlassMat.SetFloat("_ZWrite", 0f);
            _shopWindowGlassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _shopWindowGlassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Street furniture materials (03-city.png middle row)
            _ironMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.16f, 0.18f), 0.55f, 0.45f);
            _benchWoodMat = GlbModelLoader.MakeMat(new Color(0.62f, 0.38f, 0.21f), 0.32f);
            _pergolaWoodMat = GlbModelLoader.MakeMat(new Color(0.52f, 0.33f, 0.19f), 0.28f);
            _stopYellowMat = GlbModelLoader.MakeMat(new Color(0.96f, 0.74f, 0.14f), 0.6f);
            _stopBoardWhiteMat = GlbModelLoader.MakeMat(new Color(0.95f, 0.94f, 0.90f), 0.4f);
            _trashGreenMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.29f, 0.20f), 0.48f, 0.25f);
            _lampGlowMat = GlbModelLoader.MakeMat(new Color(1.0f, 0.88f, 0.48f), 0.85f);
            if (_lampGlowMat.HasProperty("_EmissionColor"))
            {
                _lampGlowMat.EnableKeyword("_EMISSION");
                _lampGlowMat.SetColor("_EmissionColor", new Color(0.95f, 0.72f, 0.28f));
            }

            // 07-material.png #4: Furrowed Tree Bark & Planter Terracotta
            _terracottaPotMat = GlbModelLoader.MakeMat(new Color(0.78f, 0.40f, 0.24f), 0.28f);
            var barkTex = MakeTreeBarkTexture();
            _oliveBarkMat = GlbModelLoader.MakeMat(new Color(0.46f, 0.35f, 0.25f), 0.16f);
            _oliveBarkMat.mainTexture = barkTex;
            _gnarledBarkMat = GlbModelLoader.MakeMat(new Color(0.68f, 0.61f, 0.52f), 0.18f);
            _gnarledBarkMat.mainTexture = barkTex;

            // 3 Distinct Tree Foliage Colors matching 03-city.png bottom-left
            _oliveLeafMat = GlbModelLoader.MakeMat(new Color(0.48f, 0.58f, 0.22f), 0.22f);        // Tree 1: Olive-yellow-green Plane/Olive canopy
            _cypressLeafMat = GlbModelLoader.MakeMat(new Color(0.18f, 0.36f, 0.20f), 0.18f);      // Tree 2: Tall dark-green Cypress
            _pinkBlossomMat = GlbModelLoader.MakeMat(new Color(0.88f, 0.32f, 0.58f), 0.26f);      // Tree 3: Pink-magenta Judas/Bougainvillea blossom tree
            _bougainvilleaVineMat = GlbModelLoader.MakeMat(new Color(0.82f, 0.18f, 0.46f), 0.28f);// Climbing magenta bougainvillea on facades

            _pedestrianSkinMat = GlbModelLoader.MakeMat(new Color(0.66f, 0.43f, 0.31f), 0.24f);
            _pedestrianTrousersMat = GlbModelLoader.MakeMat(new Color(0.12f, 0.17f, 0.24f), 0.20f);
            _pedestrianCoatMats = new[]
            {
                GlbModelLoader.MakeMat(new Color(0.12f, 0.42f, 0.43f), 0.22f),
                GlbModelLoader.MakeMat(new Color(0.72f, 0.35f, 0.18f), 0.22f),
                GlbModelLoader.MakeMat(new Color(0.70f, 0.58f, 0.24f), 0.22f),
                GlbModelLoader.MakeMat(new Color(0.34f, 0.31f, 0.52f), 0.22f)
            };

            _carMats = new[]
            {
                GlbModelLoader.MakeMat(new Color(0.85f, 0.28f, 0.25f), 0.6f),
                GlbModelLoader.MakeMat(new Color(0.26f, 0.52f, 0.78f), 0.6f),
                GlbModelLoader.MakeMat(new Color(0.90f, 0.90f, 0.88f), 0.6f),
                GlbModelLoader.MakeMat(new Color(0.28f, 0.62f, 0.45f), 0.6f),
            };

        }

        // ---------------------------------------------------------------- Procedural Textures (07-material.png)

        static Texture2D MakeWeatheredPlasterTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.14f, y * 0.14f) * 0.7f
                            + Mathf.PerlinNoise(x * 0.35f + 7f, y * 0.35f + 3f) * 0.3f;
                    float patch = n < 0.38f ? 0.91f : 1.0f;
                    tex.SetPixel(x, y, new Color(patch, patch * 0.99f, patch * 0.97f, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeTerracottaTileTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // Barrel tile ribs along X and overlapping horizontal courses along Y
                    float rib = Mathf.Sin((x / 8f) * Mathf.PI * 2f) * 0.14f + 0.88f;
                    bool courseOverlap = (y % 16) < 2;
                    float wear = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.12f;
                    float v = (courseOverlap ? 0.68f : rib) - wear;
                    tex.SetPixel(x, y, new Color(v, v * 0.92f, v * 0.86f, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeCobblestoneTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                int row = y / 16;
                int xShift = (row % 2) * 8;
                for (int x = 0; x < s; x++)
                {
                    int gx = (x + xShift) % 16;
                    int gy = y % 16;
                    bool mortar = gx < 2 || gy < 2;
                    float noise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.12f;
                    float v = mortar ? 0.72f : (0.92f - noise);
                    tex.SetPixel(x, y, new Color(v, v * 0.98f, v * 0.95f, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeTreeBarkTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float furrow = Mathf.PerlinNoise(x * 0.45f, y * 0.08f);
                    float v = Mathf.Lerp(0.65f, 1.05f, furrow);
                    tex.SetPixel(x, y, new Color(v, v * 0.98f, v * 0.92f, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeBlueWhiteStripedTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color blue = new Color(0.22f, 0.46f, 0.70f, 1f);
            Color cream = new Color(0.96f, 0.93f, 0.86f, 1f);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool isBlue = ((x / 8) % 2) == 0;
                    tex.SetPixel(x, y, isBlue ? blue : cream);
                }
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeIznikTileFriezeTexture()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color cream = new Color(0.95f, 0.92f, 0.84f, 1f);
            Color cobalt = new Color(0.18f, 0.42f, 0.68f, 1f);
            Color gold = new Color(0.84f, 0.68f, 0.34f, 1f);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x % 16) - 7.5f;
                    float dy = (y % 16) - 7.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool border = x < 3 || x > s - 4 || y < 3 || y > s - 4;
                    if (border) tex.SetPixel(x, y, gold);
                    else if ((r > 4.2f && r < 6.4f) || r < 2.0f) tex.SetPixel(x, y, cobalt);
                    else tex.SetPixel(x, y, cream);
                }
            }
            tex.Apply();
            return tex;
        }

        // ---------------------------------------------------------------- Distant Coastal Horizon (03-city.png Right Side)

        void BuildDistantCoastalHorizon()
        {
            // The avenue runs straight down to the Bosphorus (reference: taxi on a street that opens onto the strait):
            // - the street ends in a small waterfront plaza; the world camera is clipped there (HorizonCameraStack)
            // - open water across the whole view with a ferry crossing it
            // - far shore right of centre: city hill crowned by the mosque, Galata Tower further right
            // - suspension bridge far left over the water, hazy hills behind
            // Everything except the district landmarks lives on the vista layer, drawn by the vista camera.
            _distantHorizonVista = new GameObject("CoastalHorizonVista");
            _distantHorizonVista.transform.SetParent(transform, false);
            _horizonFarLayer = CreateHorizonLayer("HorizonFarLayer");
            _horizonMidLayer = CreateHorizonLayer("HorizonMidLayer");
            _usesReferenceHorizon = ReferenceShoreline.TryCreate(_horizonFarLayer, BosphorusSeaState.CoastalLevel, out _referenceShoreline);
            if (_usesReferenceHorizon)
            {
                BuildReferenceCoastalHorizon();
                return;
            }
            var bridgeSteelMat = GlbModelLoader.MakeMat(new Color(0.58f, 0.62f, 0.68f), 0.55f, 0.45f);
            var domeLeadMat = GlbModelLoader.MakeMat(new Color(0.46f, 0.52f, 0.58f), 0.42f, 0.25f);

            BuildWaterfrontEnd(_distantHorizonVista.transform);
            BuildVistaSea(_distantHorizonVista.transform);

            // Hazy hills behind the bridge (left) and behind the city (right)
            MakeSphere(_horizonFarLayer, "DistantHill_Left", new Vector3(-80f, VistaSeaY(330f) - 4f, 330f), new Vector3(150f, 30f, 40f), _hillOliveMat);
            // 1. Suspension bridge far left, standing on the water
            BuildBosphorusSuspensionBridge(_horizonFarLayer, new Vector3(-30f, VistaSeaY(285f) - 0.2f, 285f), bridgeSteelMat, 22f);

            // 2. Layered city across the water (reference "uzak plan"): a waterfront row, the main hill just right of
            // the road's vanishing point with the grand mosque on its crest, a smaller hill toward the bridge and a
            // wide back ridge, all packed with houses and dotted with smaller mosques. The vista fog hazes the
            // farther layers. Primitives are merged per material.
            var hillCenter = new Vector3(30f, VistaSeaY(232f) - 2f, 232f);
            BuildSkylineCity(_horizonMidLayer, _horizonFarLayer, hillCenter, domeLeadMat);
            BuildIstanbulMosqueSkyline(_horizonMidLayer, new Vector3(8f, hillCenter.y + 13f, 228f), domeLeadMat, 20f);

            // 3. Galata Tower on the right shoulder of the city hill
            BuildGalataTower(_horizonMidLayer, new Vector3(30f, hillCenter.y + 9f, 222f), domeLeadMat, 13f);

            // 4. Ferry and small boats out on the strait
            _ferryBlueMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.32f, 0.54f), 0.45f);
            _vistaFerry = BuildBosphorusFerry(_horizonMidLayer, new Vector3(4f, VistaSeaY(158f) - 0.1f, 158f), _ferryBlueMat, 9f);
            _vistaFerryBaseY = _vistaFerry.localPosition.y;
            _vistaFerryRestRotation = _vistaFerry.localRotation;
            _sailMat = GlbModelLoader.MakeMat(new Color(0.98f, 0.96f, 0.92f), 0.3f);
            Vector3[] boats = { new Vector3(-16f, 0f, 150f), new Vector3(20f, 0f, 185f), new Vector3(-30f, 0f, 195f) };
            _vistaBoats.Clear();
            _vistaBoatsBase.Clear();
            _vistaBoatsPhase.Clear();
            _vistaBoatsRestRotation.Clear();
            for (int i = 0; i < boats.Length; i++)
            {
                var p = boats[i];
                p.y = VistaSeaY(p.z) - 0.2f;
                var boat = i == 2 ? BuildSailboat(_horizonMidLayer, p, _sailMat) : BuildMotorboat(_horizonMidLayer, p, _ferryBlueMat);
                boat.localScale = Vector3.one * 1.8f;
                _vistaBoats.Add(boat);
                _vistaBoatsBase.Add(p);
                _vistaBoatsPhase.Add(i * 1.85f);
                _vistaBoatsRestRotation.Add(boat.localRotation);
            }
            SetLayerRecursive(_distantHorizonVista.transform, HorizonCameraStack.VistaLayer);

            // 5. District landmarks (only the current district's one is active). Most stand in the coastal water
            // inside the world camera's range, so they stay on the default layer.
            BuildDistrictLandmarks(_horizonMidLayer, domeLeadMat);
        }

        void BuildReferenceCoastalHorizon()
        {
            // One-metre near water resolves the visible wave curvature. The coarse far surface
            // has an exact matching hole and stitched boundary, with no overlapping sea layers.
            _referenceSea = BosphorusOceanMesh.Create(_distantHorizonVista.transform,
                "Reference_OpenBosphorus", new Vector3(0f, BosphorusSeaState.CoastalLevel, 0f), _seaMat);
            _referenceSeaLevel = BosphorusSeaState.CoastalLevel;
            _ferryBlueMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.32f, 0.54f), 0.45f);
            _sailMat = GlbModelLoader.MakeMat(new Color(0.98f, 0.96f, 0.92f), 0.3f);
            _vistaFerry = BuildBosphorusFerry(_horizonMidLayer,
                new Vector3(-24f, -0.51f, 130f), _ferryBlueMat, 4.6f);
            _vistaFerryBaseY = _vistaFerry.localPosition.y;
            _vistaFerryRestRotation = _vistaFerry.localRotation;

            _vistaBoats.Clear();
            _vistaBoatsBase.Clear();
            _vistaBoatsPhase.Clear();
            _vistaBoatsRestRotation.Clear();
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = i == 0 ? new Vector3(-24f, -0.45f, 96f)
                    : i == 1 ? new Vector3(-46f, -0.45f, 165f)
                    : new Vector3(-74f, -0.45f, 188f);
                Transform boat = i == 2 ? BuildSailboat(_horizonMidLayer, position, _sailMat)
                    : BuildMotorboat(_horizonMidLayer, position, _ferryBlueMat);
                boat.localScale = Vector3.one * (i == 2 ? 0.85f : 1.15f);
                _vistaBoats.Add(boat);
                _vistaBoatsBase.Add(position);
                _vistaBoatsPhase.Add(i * 1.85f);
                _vistaBoatsRestRotation.Add(boat.localRotation);
            }
            SetLayerRecursive(_distantHorizonVista.transform, HorizonCameraStack.VistaLayer);
            // The reference matte already contains the mosque, tower and low residential skyline.
            // The legacy district cluster was built on raised hills and cannot stand on this water.
        }

        void UpdateReferenceSeaLevel()
        {
            if (!_usesReferenceHorizon) return;
            float level = BosphorusSeaState.MeanLevel;
            if (Mathf.Abs(level - _referenceSeaLevel) < 0.001f) return;
            _referenceSeaLevel = level;
            Vector3 position = _referenceSea.localPosition;
            position.y = level;
            _referenceSea.localPosition = position;
            _referenceShoreline.SetWaterLevel(level);
        }

        // The fallback geometry uses the same physical waterline. Sea perspective comes from the
        // elevated camera and the distant shore, never from pitching the ocean towards the camera.
        const float VistaQuayEdgeZ = 128f;
        static float VistaSeaY(float z) => BosphorusSeaState.CoastalLevel;

        void BuildVistaSea(Transform parent)
        {
            // Coastal strip left of the plaza (continues the streamed chunks' sea up to the quay line)
            BosphorusWaterMesh.Create(parent, "VistaSea_Coast", new Vector3(-60f, BosphorusSeaState.CoastalLevel, (96f + VistaQuayEdgeZ) * 0.5f), new Vector2(106f, VistaQuayEdgeZ - 96f), _seaMat);
            const float len = 400f;
            BosphorusWaterMesh.Create(parent, "VistaSea_Open",
                new Vector3(0f, BosphorusSeaState.CoastalLevel, VistaQuayEdgeZ + len * 0.5f), new Vector2(700f, len), _seaMat);
        }

        /// <summary>
        /// The avenue's last stretch: road and sidewalks run into a stone waterfront promenade with a railing, the
        /// right-hand townhouse row ends at the water. Starts a little before the world camera's far clip so the
        /// streamed street hands over to it seamlessly.
        /// </summary>
        void BuildWaterfrontEnd(Transform parent)
        {
            var root = new GameObject("VistaWaterfront").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, 0f, 96f);
            float quay = VistaQuayEdgeZ - 96f, roadEnd = quay - 9f;
            float swX = LayoutRoadHalf + GameConfig.SidewalkWidth * 0.5f;

            MakeBox(root, "Road", new Vector3(0f, -0.05f, roadEnd * 0.5f), new Vector3(GameConfig.RoadHalfWidth * 2f, 0.1f, roadEnd), _roadMat);
            for (float z = 3f; z < roadEnd - 2f; z += 8f)
                MakeBox(root, "Dash", new Vector3(0f, 0.01f, z), new Vector3(0.16f, 0.02f, 3.0f), _laneMat);
            MakeBox(root, "SidewalkR", new Vector3(swX, 0.08f, roadEnd * 0.5f), new Vector3(GameConfig.SidewalkWidth, 0.16f, roadEnd), _sidewalkCobbleMat);
            MakeBox(root, "SidewalkL", new Vector3(-swX, 0.08f, roadEnd * 0.5f), new Vector3(GameConfig.SidewalkWidth, 0.16f, roadEnd), _sidewalkCobbleMat);
            BuildCoastalPromenadeRailing(root, -LayoutRoadHalf - GameConfig.SidewalkWidth + 0.25f, roadEnd);

            // Promenade across the end of the street, quay wall and railing along the water
            float plazaW = 70f, plazaX = 27.5f;
            MakeBox(root, "Promenade", new Vector3(plazaX, 0.08f, (roadEnd + quay) * 0.5f), new Vector3(plazaW, 0.16f, quay - roadEnd), _sidewalkCobbleMat);
            MakeBox(root, "QuayWall", new Vector3(plazaX, -0.5f, quay - 0.3f), new Vector3(plazaW, 1.2f, 0.6f), _limestoneMat);
            MakeBox(root, "QuayRail", new Vector3(plazaX, 0.95f, quay - 0.45f), new Vector3(plazaW, 0.06f, 0.06f), _ironMat);
            for (float x = plazaX - plazaW * 0.5f + 1f; x < plazaX + plazaW * 0.5f; x += 2.4f)
                MakeBox(root, "QuayPost", new Vector3(x, 0.55f, quay - 0.45f), new Vector3(0.08f, 0.8f, 0.08f), _ironMat);
            for (float x = -4f; x < 30f; x += 9f)
            {
                BuildSwanNeckStreetlamp(root, new Vector3(x, 0.16f, quay - 1.6f));
                BuildTree(root, new Vector3(x + 4.5f, 0.16f, quay - 3.2f), 0);
            }

            // Last townhouse row on the right and the terraces behind it, ending at the promenade
            BuildThreeBuildingTownhouseRow(root, 10.5f, 4);
            BuildBackdropTerraces(root, 5, roadEnd);
            BuildTree(root, new Vector3(6.4f, 0.16f, 6f), 1);
            // The promenade spans the road end; only pieces beside the road move in
            var promenade = root.Find("Promenade");
            if (promenade != null) promenade.SetParent(null, false);
            PullSidesToRoad(root);
            if (promenade != null) promenade.SetParent(root, false);
            CombineByMaterial(root);
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        Transform CreateHorizonLayer(string name)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(_distantHorizonVista.transform, false);
            return layer.transform;
        }

        // ---------------------------------------------------------------- District identity

        // Plaster tints per district (cream, coral). Applied to the shared materials: no extra draw calls.
        static readonly Color[] DistrictCream =
        {
            new Color(0.95f, 0.88f, 0.76f), new Color(0.95f, 0.82f, 0.62f), new Color(0.93f, 0.90f, 0.83f), new Color(0.88f, 0.87f, 0.83f),
            new Color(0.95f, 0.86f, 0.74f), new Color(0.96f, 0.92f, 0.78f), new Color(0.95f, 0.84f, 0.80f), new Color(0.86f, 0.82f, 0.76f),
        };
        static readonly Color[] DistrictCoral =
        {
            new Color(0.89f, 0.52f, 0.41f), new Color(0.86f, 0.45f, 0.30f), new Color(0.80f, 0.56f, 0.48f), new Color(0.70f, 0.42f, 0.37f),
            new Color(0.88f, 0.60f, 0.45f), new Color(0.84f, 0.66f, 0.40f), new Color(0.84f, 0.48f, 0.50f), new Color(0.74f, 0.44f, 0.38f),
        };

        /// <summary>Shows the district's landmark on the horizon and tints the plaster palette.</summary>
        public void SetDistrict(int district)
        {
            district = Mathf.Clamp(district, 0, _districtLandmarks.Length - 1);
            if (district == _district && (!_initialized || !NeedsLayoutForDistrict(district))) return;
            // A synchronous startup/debug request supersedes an outstanding transition iterator.
            _layoutPreparationVersion++;
            _preparingStreetLayout = false;
            ApplyDistrictProperties(district);
            if (!_initialized) return;
            if (NeedsLayoutForDistrict(district)) PrepareStreetLayout();
            else MirrorActiveStreet();
        }

        /// <summary>True when an initialized street needs cold layout work for this district.</summary>
        public bool NeedsLayoutForDistrict(int district)
        {
            district = Mathf.Clamp(district, 0, _districtLandmarks.Length - 1);
            return _initialized && (!_streetLayoutReady || _preparingStreetLayout || _chunkVariant.Count == 0 ||
                _preparedRoad != TourRoute.Districts[district].road || _preparedDistrict != district);
        }

        /// <summary>
        /// Transition-only preparation: hide the old street, yield for the loading presentation,
        /// then prepare at most one chunk per frame. The finished street respects SetStreetVisible.
        /// Startup and debug tools can continue using synchronous SetDistrict.
        /// </summary>
        public IEnumerator SetDistrictOverFrames(int district)
        {
            district = Mathf.Clamp(district, 0, _districtLandmarks.Length - 1);
            if (district == _district && (!_initialized || !NeedsLayoutForDistrict(district))) yield break;
            int version = ++_layoutPreparationVersion;
            ApplyDistrictProperties(district);
            if (!_initialized) yield break;
            if (!NeedsLayoutForDistrict(district))
            {
                MirrorActiveStreet();
                yield break;
            }

            _preparingStreetLayout = true;
            bool complete = false;
            try
            {
                BeginStreetLayout(out int baseKey, out int min, out int max);
                // Old renderers are inactive immediately; deferred destruction and UI get a frame first.
                yield return null;
                for (int index = min; index <= max; index++)
                {
                    if (version != _layoutPreparationVersion) yield break;
                    PrepareChunk(baseKey + VariantForChunk(index));
                    yield return null;
                }
                for (int variant = 0; variant < StreetVariants; variant++)
                {
                    if (version != _layoutPreparationVersion) yield break;
                    PrepareChunk(baseKey + variant);
                    yield return null;
                }
                if (version != _layoutPreparationVersion) yield break;
                CompleteStreetLayout();
                complete = true;
            }
            finally
            {
                if (version == _layoutPreparationVersion && !complete)
                {
                    _preparingStreetLayout = false;
                    _streetLayoutReady = false;
                    ReleaseStreetLayout();
                    _lastCenterChunk = int.MinValue;
                }
            }
        }

        void ApplyDistrictProperties(int district)
        {
            _district = district;
            _districtLife?.SetDistrict(district);
            CurrentRoad = TourRoute.Districts[district].road;
            CurrentSeaSide = TourRoute.Districts[district].seaSide;
            for (int i = 0; i < _districtLandmarks.Length; i++)
                if (_districtLandmarks[i] != null) _districtLandmarks[i].SetActive(i == district);
            if (_photoPlaster)
            {
                SetTint(_creamPlasterMat, DistrictCream[district], CreamNeutral);
                SetTint(_coralPlasterMat, DistrictCoral[district], CoralNeutral);
            }
            else
            {
                if (_creamPlasterMat != null) _creamPlasterMat.color = DistrictCream[district];
                if (_coralPlasterMat != null) _coralPlasterMat.color = DistrictCoral[district];
            }
        }

        void MirrorActiveStreet()
        {
            Vector3 scale = StreetMirrorScale;
            foreach (var chunk in _activeChunks.Values) chunk.transform.localScale = scale;
        }

        void BuildDistrictLandmarks(Transform vista, Material leadMat)
        {
            var stoneMat = _curbStoneMat;
            Transform Root(int i, string name)
            {
                var go = new GameObject("DistrictLandmark_" + i + "_" + name);
                go.transform.SetParent(vista, false);
                _districtLandmarks[i] = go;
                return go.transform;
            }

            // Hero landmark models (Hyper-generated, Resources/EModel/Landmark_*) stand big on the water side so each
            // district reads differently at a glance; the primitive versions stay as fallbacks.
            bool Hero(Transform root, string model, Vector3 pos, float yaw, float height)
                => GlbModelLoader.TryInstantiateEModel(model, root, pos, Quaternion.Euler(0f, yaw, 0f), height, out _);

            // 0 Sahil: Maiden's Tower on its islet
            var r0 = Root(0, "KizKulesi");
            if (!Hero(r0, "Landmark_KizKulesi", new Vector3(-14f, -0.4f, 70f), 200f, 13f))
                BuildMaidensTower(r0, new Vector3(-11f, 0f, 68f), leadMat, false);
            CombineByMaterial(r0);

            // 1 Çarşı: waterfront spice bazaar (long domed hall on a quay), in the open water left of the hill town
            var r1 = Root(1, "MisirCarsisi");
            var bz = new Vector3(-21f, 0f, 70f);
            MakeBox(r1, "BazaarQuay", bz + new Vector3(0f, 0.25f, 0f), new Vector3(15f, 0.7f, 6f), _curbStoneMat);
            MakeBox(r1, "BazaarHall", bz + new Vector3(0f, 2.1f, 0f), new Vector3(12f, 3.0f, 4f), _creamPlasterMat);
            MakeBox(r1, "BazaarCornice", bz + new Vector3(0f, 3.7f, 0f), new Vector3(12.4f, 0.3f, 4.3f), _limestoneMat);
            for (int k = 0; k < 6; k++)
                MakeSphere(r1, "BazaarDome", bz + new Vector3(-5f + k * 2f, 4.0f, 0f), new Vector3(1.7f, 1.3f, 1.7f), leadMat);
            MakeSphere(r1, "BazaarMainDome", bz + new Vector3(0f, 4.6f, 0f), new Vector3(3.0f, 2.4f, 3.0f), leadMat);
            for (int k = 0; k < 4; k++)
            {
                MakeBox(r1, "BazaarArch", bz + new Vector3(-4.5f + k * 3f, 1.7f, -2.03f), new Vector3(1.3f, 1.8f, 0.06f), _darkGlassMat);
                MakeBox(r1, "BazaarAwning", bz + new Vector3(-4.5f + k * 3f, 1.0f, -2.6f), new Vector3(1.6f, 0.12f, 1.0f), k % 2 == 0 ? _redAwningMat : _navyCanvasMat);
            }
            CombineByMaterial(r1);

            // 2 Köprü: low Galata-style bridge across the water with lamp posts
            var r2 = Root(2, "GalataKoprusu");
            MakeBox(r2, "BridgeDeck", new Vector3(-24f, 1.3f, 84f), new Vector3(36f, 0.55f, 2.6f), stoneMat);
            MakeBox(r2, "BridgeRailN", new Vector3(-24f, 1.85f, 85.2f), new Vector3(36f, 0.5f, 0.12f), _ironMat);
            MakeBox(r2, "BridgeRailS", new Vector3(-24f, 1.85f, 82.8f), new Vector3(36f, 0.5f, 0.12f), _ironMat);
            for (float bx = -40f; bx <= -8f; bx += 4f)
            {
                MakeCylinder(r2, "BridgePier", new Vector3(bx, 0.4f, 84f), new Vector3(1.1f, 0.6f, 1.1f), stoneMat);
                MakeCylinder(r2, "BridgeLampPost", new Vector3(bx, 2.6f, 85.1f), new Vector3(0.1f, 0.8f, 0.1f), _ironMat);
                MakeSphere(r2, "BridgeLamp", new Vector3(bx, 3.5f, 85.1f), new Vector3(0.35f, 0.35f, 0.35f), _lampGlowMat);
            }
            CombineByMaterial(r2);

            // 3 Liman: Karaköy ferry pier in front, quay with container stacks and two harbour cranes behind
            var r3 = Root(3, "Liman");
            Hero(r3, "Landmark_KarakoyIskele", new Vector3(-13f, 0.1f, 60f), 160f, 8f);
            MakeBox(r3, "Quay", new Vector3(-20f, 0.25f, 74f), new Vector3(16f, 0.7f, 7f), stoneMat);
            Material[] boxes = { _navyCanvasMat, _redAwningMat, _tealDoorMat, _stopYellowMat };
            for (int k = 0; k < 10; k++)
            {
                int col = k % 5, layer = k / 5;
                MakeBox(r3, "Container", new Vector3(-25f + col * 2.4f, 1.15f + layer * 1.1f, 75.5f - layer * 0.3f), new Vector3(2.2f, 1.05f, 1.05f), boxes[(k * 3 + layer) % boxes.Length]);
            }
            for (int c = 0; c < 2; c++)
            {
                float cx = -16f - c * 7f;
                MakeBox(r3, "CraneLegL", new Vector3(cx - 1.2f, 3.6f, 72.5f), new Vector3(0.35f, 6.6f, 0.35f), _coralPlasterMat);
                MakeBox(r3, "CraneLegR", new Vector3(cx + 1.2f, 3.6f, 72.5f), new Vector3(0.35f, 6.6f, 0.35f), _coralPlasterMat);
                MakeBox(r3, "CraneTop", new Vector3(cx, 7.0f, 72.5f), new Vector3(2.8f, 0.8f, 0.8f), _whitePlasterMat);
                MakeBox(r3, "CraneBoom", new Vector3(cx, 7.4f, 69.8f), new Vector3(0.45f, 0.45f, 7.5f), _coralPlasterMat);
            }
            CombineByMaterial(r3);

            // 4 Kule: waterfront clock tower (Dolmabahçe-style)
            var r4 = Root(4, "SaatKulesi");
            var ct = new Vector3(-10.5f, 0f, 62f);
            if (Hero(r4, "Landmark_SaatKulesi", ct, 180f, 12f)) ct = new Vector3(0f, -100f, 0f);   // model in place: park the primitive one out of sight
            MakeBox(r4, "ClockBase", ct + new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 1.8f, 2.8f), _limestoneMat);
            MakeBox(r4, "ClockShaft", ct + new Vector3(0f, 3.6f, 0f), new Vector3(2.0f, 3.8f, 2.0f), _whitePlasterMat);
            MakeBox(r4, "ClockBelt", ct + new Vector3(0f, 5.6f, 0f), new Vector3(2.3f, 0.3f, 2.3f), _limestoneMat);
            MakeBox(r4, "ClockStage", ct + new Vector3(0f, 6.6f, 0f), new Vector3(1.7f, 1.8f, 1.7f), _creamPlasterMat);
            for (int f = 0; f < 4; f++)
            {
                var face = MakeCylinderReturn(r4, "ClockFace", ct + Quaternion.Euler(0f, f * 90f, 0f) * new Vector3(0f, 6.7f, -0.87f), new Vector3(1.0f, 0.03f, 1.0f), _stopBoardWhiteMat);
                face.localRotation = Quaternion.Euler(0f, f * 90f, 0f) * Quaternion.Euler(90f, 0f, 0f);
            }
            MakeSphere(r4, "ClockDome", ct + new Vector3(0f, 7.7f, 0f), new Vector3(1.5f, 1.1f, 1.5f), leadMat);
            MakeCylinder(r4, "ClockFinial", ct + new Vector3(0f, 8.6f, 0f), new Vector3(0.12f, 0.5f, 0.12f), _stopYellowMat);
            CombineByMaterial(r4);

            // 5 Değirmen: three windmills on the far hill with turning sails (sails are not merged so they can spin)
            // (on the far shore's left shoulder, drawn by the vista camera at twice the size)
            var r5 = Root(5, "Degirmen");
            r5.localScale = Vector3.one * 2f;
            var bodies = new GameObject("WindmillBodies").transform;
            bodies.SetParent(r5, false);
            Vector3[] mills = { new Vector3(-12f, 9.6f, 114f), new Vector3(-8f, 10.6f, 115f), new Vector3(-4f, 11.6f, 116f) };
            foreach (var m in mills)
            {
                MakeCylinder(bodies, "MillBody", m + new Vector3(0f, 0.2f, 0f), new Vector3(2.4f, 2.6f, 2.4f), _whitePlasterMat);
                MakeSphere(bodies, "MillCap", m + new Vector3(0f, 2.9f, 0f), new Vector3(2.6f, 1.9f, 2.6f), _terracottaRoofMat);
                MakeBox(bodies, "MillDoor", m + new Vector3(0f, -1.4f, -1.2f), new Vector3(0.7f, 1.2f, 0.08f), _tealDoorMat);
                var rotor = new GameObject("MillSails").transform;
                rotor.SetParent(r5, false);
                rotor.localPosition = m + new Vector3(0f, 2.7f, -1.4f);
                for (int b = 0; b < 4; b++)
                {
                    var arm = new GameObject("SailArm").transform;
                    arm.SetParent(rotor, false);
                    arm.localRotation = Quaternion.Euler(0f, 0f, b * 90f);
                    MakeBox(arm, "Sail", new Vector3(0f, 1.25f, 0f), new Vector3(0.45f, 2.4f, 0.06f), _benchWoodMat);
                }
                _windmillRotors.Add(rotor);
            }
            CombineByMaterial(bodies);
            SetLayerRecursive(r5, HorizonCameraStack.VistaLayer);

            // 6 Müze: domed museum (Hagia Sophia-like) with four minarets on the far shore, left of the mosque
            var r6 = Root(6, "Muze");
            r6.localScale = Vector3.one * 1.8f;
            var museumMat = GlbModelLoader.MakeMat(new Color(0.86f, 0.62f, 0.54f), 0.2f);
            var mc = new Vector3(-8f, 11f, 125f);
            MakeBox(r6, "MuseumBody", mc + new Vector3(0f, 1.4f, 0f), new Vector3(8f, 2.8f, 6f), museumMat);
            MakeCylinder(r6, "MuseumDrum", mc + new Vector3(0f, 3.2f, 0f), new Vector3(4.2f, 0.5f, 4.2f), museumMat);
            MakeSphere(r6, "MuseumDome", mc + new Vector3(0f, 3.7f, 0f), new Vector3(4.6f, 3.0f, 4.6f), leadMat);
            MakeSphere(r6, "HalfDomeW", mc + new Vector3(-2.9f, 2.9f, 0f), new Vector3(2.6f, 1.8f, 2.6f), leadMat);
            MakeSphere(r6, "HalfDomeE", mc + new Vector3(2.9f, 2.9f, 0f), new Vector3(2.6f, 1.8f, 2.6f), leadMat);
            Vector3[] corners = { new Vector3(-4.6f, 0f, -3.4f), new Vector3(4.6f, 0f, -3.4f), new Vector3(-4.6f, 0f, 3.4f), new Vector3(4.6f, 0f, 3.4f) };
            foreach (var cc in corners)
            {
                MakeCylinder(r6, "MuseumMinaret", mc + cc + new Vector3(0f, 3.4f, 0f), new Vector3(0.45f, 3.4f, 0.45f), _whitePlasterMat);
                MakeCylinder(r6, "MinaretCap", mc + cc + new Vector3(0f, 7.2f, 0f), new Vector3(0.3f, 0.5f, 0.3f), leadMat);
            }
            CombineByMaterial(r6);
            SetLayerRecursive(r6, HorizonCameraStack.VistaLayer);

            // 7 Gece: Maiden's Tower lit up and a string of lights along the far shore
            var r7 = Root(7, "GeceIsiklari");
            if (!Hero(r7, "Landmark_OrtakoyCamii", new Vector3(-15f, 0.2f, 72f), 190f, 12f))
                BuildMaidensTower(r7, new Vector3(-11f, 0f, 68f), leadMat, true);
            for (float lx = -44f; lx <= -6f; lx += 2.2f)
                MakeSphere(r7, "ShoreLight", new Vector3(lx, 1.0f + Mathf.Sin(lx * 0.7f) * 0.15f, 93f), new Vector3(0.45f, 0.45f, 0.45f), _lampGlowMat);
            CombineByMaterial(r7);

            for (int i = 0; i < _districtLandmarks.Length; i++) _districtLandmarks[i].SetActive(false);
        }

        void BuildMaidensTower(Transform parent, Vector3 p, Material leadMat, bool lit)
        {
            MakeSphere(parent, "Islet", p + new Vector3(0f, -0.2f, 0f), new Vector3(6f, 1.3f, 4.6f), _curbStoneMat);
            MakeBox(parent, "TowerBase", p + new Vector3(0f, 0.9f, 0f), new Vector3(3.6f, 1.4f, 3.0f), _limestoneMat);
            MakeBox(parent, "Annex", p + new Vector3(1.9f, 1.4f, 0.3f), new Vector3(1.8f, 1.2f, 1.8f), _whitePlasterMat);
            MakeBox(parent, "AnnexRoof", p + new Vector3(1.9f, 2.1f, 0.3f), new Vector3(2.0f, 0.25f, 2.0f), _terracottaRoofMat);
            MakeCylinder(parent, "TowerShaft", p + new Vector3(-0.3f, 3.6f, 0f), new Vector3(1.5f, 2.0f, 1.5f), _whitePlasterMat);
            MakeCylinder(parent, "TowerGallery", p + new Vector3(-0.3f, 5.7f, 0f), new Vector3(1.9f, 0.18f, 1.9f), _limestoneMat);
            MakeCylinder(parent, "TowerTop", p + new Vector3(-0.3f, 6.3f, 0f), new Vector3(1.3f, 0.5f, 1.3f), lit ? _lampGlowMat : _whitePlasterMat);
            MakeSphere(parent, "TowerCone", p + new Vector3(-0.3f, 7.2f, 0f), new Vector3(1.2f, 1.6f, 1.2f), leadMat);
            MakeCylinder(parent, "TowerFinial", p + new Vector3(-0.3f, 8.2f, 0f), new Vector3(0.1f, 0.4f, 0.1f), _stopYellowMat);
            for (int w = 0; w < 3; w++)
                MakeBox(parent, "TowerWindow", p + new Vector3(-0.3f, 2.8f + w * 1.0f, -0.76f), new Vector3(0.3f, 0.5f, 0.05f), lit ? _lampGlowMat : _darkGlassMat);
        }

        // ---------------------------------------------------------------- Shared low-poly shapes

        static Mesh s_roofMesh, s_blobMesh;

        /// <summary>Hip roof on a 1 x 1 footprint, 1 high: ridge along X, sloped faces all round (flat shaded).</summary>
        static Mesh RoofMesh
        {
            get
            {
                if (s_roofMesh != null) return s_roofMesh;
                Vector3 a = new Vector3(-0.5f, 0f, -0.5f), b = new Vector3(0.5f, 0f, -0.5f), c = new Vector3(0.5f, 0f, 0.5f), d = new Vector3(-0.5f, 0f, 0.5f);
                Vector3 r0 = new Vector3(-0.22f, 1f, 0f), r1 = new Vector3(0.22f, 1f, 0f);
                var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
                void Face(params Vector3[] p) { int i = v.Count; v.AddRange(p); for (int k = 1; k + 1 < p.Length; k++) { t.Add(i); t.Add(i + k); t.Add(i + k + 1); } }
                Face(a, r0, r1, b);      // front slope
                Face(c, r1, r0, d);      // back slope
                Face(b, r1, c);          // right hip
                Face(d, r0, a);          // left hip
                Face(a, b, c, d);        // underside (eaves seen from below)
                s_roofMesh = new Mesh { name = "HipRoof" };
                s_roofMesh.SetVertices(v); s_roofMesh.SetTriangles(t, 0);
                var uv = new Vector2[v.Count]; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(v[i].x + 0.5f, v[i].z + v[i].y);
                s_roofMesh.uv = uv;
                s_roofMesh.RecalculateNormals(); s_roofMesh.RecalculateTangents(); s_roofMesh.RecalculateBounds();
                return s_roofMesh;
            }
        }

        /// <summary>Smooth low-res sphere (10 x 7, ~120 triangles) for tree crowns and cypresses far away.</summary>
        static Mesh BlobMesh
        {
            get
            {
                if (s_blobMesh != null) return s_blobMesh;
                const int seg = 10, rings = 7;
                var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
                for (int r = 0; r <= rings; r++)
                {
                    float phi = Mathf.PI * r / rings;
                    for (int s = 0; s <= seg; s++)
                    {
                        float th = 2f * Mathf.PI * s / seg;
                        v.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)) * 0.5f);
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int i0 = r * (seg + 1) + s, i1 = i0 + seg + 1;
                        t.Add(i0); t.Add(i0 + 1); t.Add(i1);
                        t.Add(i1); t.Add(i0 + 1); t.Add(i1 + 1);
                    }
                s_blobMesh = new Mesh { name = "Blob" };
                s_blobMesh.SetVertices(v); s_blobMesh.SetTriangles(t, 0);
                s_blobMesh.RecalculateNormals(); s_blobMesh.RecalculateBounds();
                return s_blobMesh;
            }
        }

        static Transform MakeMeshPart(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        // ---------------------------------------------------------------- Horizon: layered city skyline

        Material[] _skyWalls;
        Material _skyRoof, _skyRoofDark, _skyLit, _skyTree;
        const float SkyWindowTile = 0.9f;

        /// <summary>
        /// Facade texture for the distant houses: one window bay per tile (plaster with a dark window and a
        /// lighter sill), so a single box reads as a multi-storey house with rows of windows.
        /// </summary>
        static Texture2D MakeSkyFacadeTexture()
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "SkyFacade", wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool window = x >= 10 && x < 22 && y >= 9 && y < 25;
                    bool sill = x >= 9 && x < 23 && y >= 7 && y < 9;
                    var c = window ? new Color(0.24f, 0.26f, 0.32f) : sill ? new Color(1f, 1f, 1f) : new Color(0.93f, 0.93f, 0.93f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply(true);
            return tex;
        }

        void EnsureSkyMaterials()
        {
            if (_skyWalls != null) return;
            var facade = MakeSkyFacadeTexture();
            // Board "uzak plan" palette: cream, ochre, apricot, coral, pale pink, white
            Color[] walls =
            {
                new Color(0.96f, 0.89f, 0.74f), new Color(0.90f, 0.70f, 0.42f), new Color(0.96f, 0.78f, 0.58f),
                new Color(0.88f, 0.56f, 0.44f), new Color(0.94f, 0.80f, 0.76f), new Color(0.97f, 0.95f, 0.90f),
            };
            _skyWalls = new Material[walls.Length];
            for (int i = 0; i < walls.Length; i++)
            {
                var m = GlbModelLoader.MakeMat(walls[i], 0.15f);
                m.mainTexture = facade; m.SetTexture("_BaseMap", facade);
                s_tileMetres[m] = SkyWindowTile;
                _skyWalls[i] = m;
            }
            _skyRoof = GlbModelLoader.MakeMat(new Color(0.74f, 0.33f, 0.20f), 0.2f);
            _skyRoofDark = GlbModelLoader.MakeMat(new Color(0.55f, 0.27f, 0.19f), 0.2f);
            _skyTree = GlbModelLoader.MakeMat(new Color(0.20f, 0.33f, 0.18f), 0.1f);
            _skyLit = GlbModelLoader.MakeMat(new Color(1f, 0.82f, 0.45f), 0.6f);
            _skyLit.EnableKeyword("_EMISSION");
            _skyLit.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.3f) * 1.3f);
        }

        // Quantised house sizes, so the metric-UV mesh cache stays small (one mesh per size, not per house)
        static readonly float[] SkyWidths = { 1.8f, 2.7f, 3.6f, 4.5f };
        static readonly float[] SkyHeights = { 1.8f, 2.7f, 3.6f, 4.5f };

        void BuildSkylineCity(Transform mid, Transform far, Vector3 mainHill, Material domeMat)
        {
            EnsureSkyMaterials();
            var rng = new System.Random(20261025);

            // Back ridge: wide and higher, behind everything (hazed most by the vista fog)
            var backC = new Vector3(70f, VistaSeaY(292f) - 5f, 292f);
            var back = HillTown(far, "BackRidge", backC, new Vector3(100f, 26f, 30f), 1.35f, 0.55f, rng);
            // Left hill between the bridge and the grand mosque
            var leftC = new Vector3(-22f, VistaSeaY(252f) - 3f, 252f);
            HillTown(mid, "LeftHill", leftC, new Vector3(30f, 11f, 18f), 1.0f, 0.8f, rng);
            // Main hill
            var mainRad = new Vector3(68f, 16f, 26f);
            HillTown(mid, "MainHill", mainHill, mainRad, 1.0f, 0.95f, rng);
            // Waterfront row along the far shore below the main hill
            Waterfront(mid, new Vector3(36f, VistaSeaY(204f), 204f), 112f, rng);

            // Smaller mosques along the crests
            Vector3 Crest(Vector3 c, Vector3 r, float x, float lift)
            {
                float k = Mathf.Clamp01(Mathf.Abs(x - c.x) / r.x);
                float zf = 0.35f;
                float y = c.y + r.y * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k - zf * zf));
                return new Vector3(x, y + lift, c.z - r.z * zf);
            }
            BuildPrimitiveMosque(mid, Crest(mainHill, mainRad, 52f, -0.5f), domeMat, 9f, 2);
            BuildPrimitiveMosque(far, Crest(backC, new Vector3(100f, 26f, 30f), 34f, -0.5f), domeMat, 15f, 4);
            BuildPrimitiveMosque(far, Crest(backC, new Vector3(100f, 26f, 30f), 98f, -0.5f), domeMat, 13f, 2);
            BuildPrimitiveMosque(mid, Crest(leftC, new Vector3(30f, 11f, 18f), -20f, -0.4f), domeMat, 8f, 2);
        }

        /// <summary>
        /// A hill (flattened ellipsoid) with houses packed onto its front face: rows climb the slope, each row is
        /// laid twice with a stagger so the houses overlap like a dense old town; textured facades give the
        /// windows, a few windows glow, roofs are terracotta, cypresses and tree crowns break it up.
        /// </summary>
        /// <param name="s">House size multiplier (hills sit 200-300 m away, so houses are larger than life).</param>
        /// <param name="density">Share of row positions that get a house (the rest show the green hillside).</param>
        Transform HillTown(Transform parent, string name, Vector3 c, Vector3 rad, float s, float density, System.Random rng)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            MakeSphere(root, "Hill", c, rad * 2f, _hillOliveMat);
            float rowStep = 1.25f * s;
            int rows = Mathf.FloorToInt(rad.y * 0.86f / rowStep);
            for (int row = 0; row < rows; row++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    float y = c.y + 0.2f * s + row * rowStep + pass * rowStep * 0.5f;
                    float k = Mathf.Clamp01((y - c.y) / rad.y);
                    float shrink = Mathf.Sqrt(1f - k * k);
                    float halfW = rad.x * shrink * 0.9f;
                    float zRow = c.z - rad.z * shrink * 0.96f + pass * 0.9f * s;
                    float x = c.x - halfW + (float)rng.NextDouble() * 1.5f * s;
                    while (x < c.x + halfW)
                    {
                        float w = SkyWidths[rng.Next(SkyWidths.Length)] * 0.5f * s;
                        if (rng.NextDouble() > density)
                        {
                            // round tree crowns (a cheap low-res blob, not a box: boxes read as voxels)
                            if (rng.NextDouble() < 0.55)
                            {
                                float cs = (1.2f + (float)rng.NextDouble() * 0.7f) * s;
                                MakeMeshPart(root, "TreeCrown", BlobMesh, new Vector3(x + w * 0.5f, y + 0.8f * s, zRow), new Vector3(cs, cs * 0.85f, cs), _skyTree);
                            }
                            x += w;
                            continue;
                        }
                        // varied widths (not one grid of equal tiles), heights quantised for the UV mesh cache
                        w *= 0.8f + (float)rng.NextDouble() * 0.6f;
                        float h = SkyHeights[rng.Next(SkyHeights.Length)] * 0.5f * s + 0.6f * s;
                        SkyHouse(root, new Vector3(x + w * 0.5f, y, zRow), w, h, 1.6f * s, s, rng);
                        if (rng.NextDouble() < 0.07)
                            MakeMeshPart(root, "Cypress", BlobMesh, new Vector3(x + w + 0.2f * s, y + 1.4f * s, zRow - 0.9f * s), new Vector3(0.55f, 3.2f, 0.55f) * s, _skyTree);
                        x += w + (rng.NextDouble() < 0.2 ? 0.3f * s : 0f);
                    }
                }
            }
            CombineByMaterial(root);
            return root;
        }

        void SkyHouse(Transform root, Vector3 baseFront, float w, float h, float depth, float s, System.Random rng)
        {
            var wall = _skyWalls[rng.Next(_skyWalls.Length)];
            var p = new Vector3(baseFront.x, baseFront.y + h * 0.5f, baseFront.z);
            MakeBox(root, "House", p, new Vector3(w, h, depth), wall);
            // sloped terracotta hip roof with eaves overhanging the walls
            if (rng.NextDouble() < 0.92)
                MakeMeshPart(root, "Roof", RoofMesh, new Vector3(p.x, baseFront.y + h, p.z), new Vector3(w + 0.3f * s, Mathf.Min(w, depth) * 0.42f, depth + 0.3f * s),
                    rng.NextDouble() < 0.7 ? _skyRoof : _skyRoofDark);
            // a few lit windows for the golden hour
            if (rng.NextDouble() < 0.18)
            {
                float wx = p.x + ((float)rng.NextDouble() - 0.5f) * (w - 0.6f * s);
                float wy = baseFront.y + SkyWindowTile * (0.5f + rng.Next(Mathf.Max(1, Mathf.FloorToInt(h / SkyWindowTile)))) + 0.05f;
                MakeBox(root, "LitWindow", new Vector3(wx, wy, p.z - depth * 0.5f - 0.02f), new Vector3(0.36f, 0.48f, 0.04f) * (SkyWindowTile / 0.9f), _skyLit);
            }
        }

        /// <summary>Continuous row of 3-5 storey waterfront buildings standing on a quay at the water's edge.</summary>
        void Waterfront(Transform parent, Vector3 centre, float length, System.Random rng)
        {
            var root = new GameObject("FarWaterfront").transform;
            root.SetParent(parent, false);
            MakeBox(root, "Quay", new Vector3(centre.x, centre.y + 0.4f, centre.z + 3f), new Vector3(length + 4f, 1.4f, 8f), _limestoneMat);
            float x = centre.x - length * 0.5f;
            while (x < centre.x + length * 0.5f)
            {
                float w = SkyWidths[rng.Next(SkyWidths.Length)] * 0.75f;
                float h = SkyHeights[rng.Next(SkyHeights.Length)] * 0.75f + 2.4f;
                SkyHouse(root, new Vector3(x + w * 0.5f, centre.y + 1.1f, centre.z + 1.8f), w, h, 3f, 1.4f, rng);
                x += w;
            }
            CombineByMaterial(root);
        }

        /// <summary>
        /// Two terraces of simple houses climbing behind the street-side townhouses, so the gaps between
        /// the facades show more city instead of empty fog. Built from boxes and merged per material.
        /// </summary>
        void BuildBackdropTerraces(Transform parent, int variant, float L)
        {
            var root = new GameObject("BackdropTerraces").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(7717 + variant * 53);
            Material[] walls = { _creamPlasterMat, _coralPlasterMat, _whitePlasterMat, _creamPlasterMat, _limestoneMat };

            // Ground behind the street houses and a raised second terrace with a stone retaining wall.
            MakeBox(root, "BackGround", new Vector3(24f, 0.05f, L * 0.5f), new Vector3(24f, 0.2f, L), _hillOliveMat);
            MakeBox(root, "Terrace2", new Vector3(27.5f, 1.6f, L * 0.5f), new Vector3(9f, 3.2f, L), _hillOliveMat);
            MakeBox(root, "RetainingWall", new Vector3(23.05f, 1.6f, L * 0.5f), new Vector3(0.3f, 3.2f, L), _limestoneMat);

            // Row 1 stays below the street houses' roofs (it only fills the gaps); row 2 peeks over them.
            BackdropRow(root, rng, walls, 16.0f, 0.16f, 3.0f, 4.2f, 6.2f, L);
            BackdropRow(root, rng, walls, 26.0f, 3.2f, 3.5f, 4.0f, 6.0f, L);
            CombineByMaterial(root);
        }

        void BackdropRow(Transform root, System.Random rng, Material[] walls, float x, float baseY, float depth, float minH, float maxH, float L)
        {
            float z = (float)rng.NextDouble() * 1.5f;
            while (z < L - 1f)
            {
                float w = Mathf.Min(3.5f + (float)rng.NextDouble() * 2.5f, L - z);
                float h = minH + (float)rng.NextDouble() * (maxH - minH);
                float dx = (float)rng.NextDouble() * 1.2f;
                var wall = walls[rng.Next(walls.Length)];
                MakeBox(root, "BackHouse", new Vector3(x + dx, baseY + h * 0.5f, z + w * 0.5f), new Vector3(depth, h, w), wall);
                MakeMeshPart(root, "BackRoof", RoofMesh, new Vector3(x + dx, baseY + h, z + w * 0.5f), new Vector3(w + 0.4f, Mathf.Min(w, depth) * 0.45f, depth + 0.4f), _terracottaRoofMat)
                    .localRotation = Quaternion.Euler(0f, 90f, 0f);   // ridge along the street
                // Small windows facing the street keep the boxes from reading as blank blocks.
                int perFloor = Mathf.Max(1, Mathf.FloorToInt(w / 1.7f));
                for (float wy = baseY + 1.6f; wy < baseY + h - 0.9f; wy += 2.2f)
                    for (int k = 0; k < perFloor; k++)
                    {
                        float wz = z + (k + 0.5f) * w / perFloor;
                        MakeBox(root, "BackWindow", new Vector3(x + dx - depth * 0.5f - 0.02f, wy, wz), new Vector3(0.05f, 0.95f, 0.6f), _darkGlassMat);
                    }
                if (rng.NextDouble() < 0.3)
                    MakeSphere(root, "BackCypress", new Vector3(x + dx - depth * 0.5f - 0.8f, baseY + 2.4f, z + w + 0.1f), new Vector3(1.2f, 4.8f, 1.2f), _cypressLeafMat);
                z += w + (rng.NextDouble() < 0.25 ? 0.6f : 0f);
            }
        }

        /// <summary>
        /// Merges every primitive under <paramref name="root"/> into one mesh per material.
        /// Hundreds of small skyline boxes become a handful of draw calls on mobile.
        /// </summary>
        static bool IsUnderImportedModel(Transform t, Transform stop)
        {
            for (var p = t; p != null && p != stop; p = p.parent)
                if (p.name.StartsWith("EModel_")) return true;
            return false;
        }

        void CombineByMaterial(Transform root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toRemove = new List<GameObject>();
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            foreach (var mf in filters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                // Keep authored detail culling intact instead of destroying LOD renderer references.
                if (mf.GetComponentInParent<LODGroup>() != null) continue;
                // Keep weather-controlled geometry and previous deferred-destruction sources out of merges.
                if (!mf.gameObject.activeSelf || mf.name.StartsWith("WetAsphaltPatch_") ||
                    mf.name == "NightWindow" || mf.name == "LanternGlow") continue;
                // Multi-material meshes (imported GLB props) stay as they are.
                if (mf.sharedMesh.subMeshCount != 1 || mr.sharedMaterials.Length != 1) continue;
                // Imported model meshes are not CPU-readable in player builds; only merge our own primitives.
                if (!mf.sharedMesh.isReadable || IsUnderImportedModel(mf.transform, root)) continue;
                if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                toRemove.Add(mf.gameObject);
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "Combined_" + kv.Key.name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                TrackGeneratedMesh(mesh);
                var go = new GameObject("Combined_" + kv.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            foreach (var g in toRemove)
            {
                var source = g.GetComponent<MeshFilter>().sharedMesh;
                if (_generatedMeshes.Remove(source))
                {
                    _layoutMeshes.Remove(source);
                    Object.Destroy(source);
                }
                g.SetActive(false);
                Object.Destroy(g);
            }
        }

        void TrackGeneratedMesh(Mesh mesh)
        {
            _generatedMeshes.Add(mesh);
            if (_buildingLayout) _layoutMeshes.Add(mesh);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _districtMuralMaterials.Length; i++)
                if (_districtMuralMaterials[i] != null) Object.Destroy(_districtMuralMaterials[i]);
            foreach (var mesh in _generatedMeshes)
                if (mesh != null) Object.Destroy(mesh);
            _generatedMeshes.Clear();
            _layoutMeshes.Clear();
        }

        void BuildGalataTower(Transform parent, Vector3 pos, Material roofLeadMat, float height = 12.8f)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_GalataKulesi", parent, pos, Quaternion.identity, height, out var eTower))
            {
                eTower.name = "GalataTower_Landmark";
                MakeSphere(eTower.transform, "GalataHillSlope", new Vector3(0f, -2.2f, 0f), new Vector3(16f, 5.5f, 14f), _hillOliveMat);
                return;
            }

            var tower = new GameObject("GalataTower_Landmark");
            tower.transform.SetParent(parent, false);
            tower.transform.localPosition = pos;
            tower.transform.localScale = Vector3.one * (height / 12.8f);

            // Dedicated verdant hillside slope grounding the tower into the Istanbul coastline terrain
            MakeSphere(tower.transform, "GalataHillSlope", new Vector3(0f, -2.2f, 0f), new Vector3(16f, 5.5f, 14f), _hillOliveMat);

            // Broad stone foundation plinth
            MakeCylinder(tower.transform, "StonePlinth", new Vector3(0f, 0.45f, 0f), new Vector3(5.4f, 0.9f, 5.4f), _curbStoneMat);

            // Cylindrical stone shaft + stone belt courses + corbelled observation balcony
            MakeCylinder(tower.transform, "StoneShaft", new Vector3(0f, 3.65f, 0f), new Vector3(4.2f, 5.5f, 4.2f), _limestoneMat);
            MakeCylinder(tower.transform, "BeltCourse1", new Vector3(0f, 4.35f, 0f), new Vector3(4.45f, 0.18f, 4.45f), _curbStoneMat);
            MakeCylinder(tower.transform, "ObservationBalcony", new Vector3(0f, 6.6f, 0f), new Vector3(4.85f, 0.28f, 4.85f), _limestoneMat);
            MakeCylinder(tower.transform, "UpperGalleryDrum", new Vector3(0f, 7.6f, 0f), new Vector3(4.0f, 1.1f, 4.0f), _creamPlasterMat);

            // Warm glowing arched observation gallery windows around the upper drum
            for (int w = 0; w < 8; w++)
            {
                float ang = w * 45f * Mathf.Deg2Rad;
                Vector3 wp = new Vector3(Mathf.Sin(ang) * 2.02f, 7.55f, Mathf.Cos(ang) * 2.02f);
                var win = MakeBoxReturn(tower.transform, "GalleryWindow_" + w, wp, new Vector3(0.45f, 0.92f, 0.12f), _lampGlowMat);
                win.localRotation = Quaternion.Euler(0f, w * 45f, 0f);
            }

            // Tall conical lead roof + brass finial
            MakeCylinder(tower.transform, "ConeBase", new Vector3(0f, 9.2f, 0f), new Vector3(3.8f, 0.65f, 3.8f), roofLeadMat);
            MakeCylinder(tower.transform, "ConeMid", new Vector3(0f, 10.4f, 0f), new Vector3(2.6f, 0.75f, 2.6f), roofLeadMat);
            MakeCylinder(tower.transform, "ConeTop", new Vector3(0f, 11.6f, 0f), new Vector3(1.3f, 0.75f, 1.3f), roofLeadMat);
            MakeCylinder(tower.transform, "ConeFinial", new Vector3(0f, 12.8f, 0f), new Vector3(0.18f, 0.65f, 0.18f), _stopYellowMat);
            CombineByMaterial(tower.transform);
        }

        void BuildBosphorusSuspensionBridge(Transform parent, Vector3 centerPos, Material steelMat, float height = 14.8f)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Köprü", parent, centerPos, Quaternion.identity, height, out var eBridge))
            {
                eBridge.name = "BosphorusSuspensionBridge";
                return;
            }

            var bridge = new GameObject("BosphorusSuspensionBridge");
            bridge.transform.SetParent(parent, false);
            bridge.transform.localPosition = centerPos;
            bridge.transform.localScale = Vector3.one * (height / 14.8f);

            // Bridge roadway deck spanning across the Bosphorus
            MakeBox(bridge.transform, "RoadDeck", new Vector3(0f, 5.2f, 0f), new Vector3(28f, 0.42f, 1.6f), steelMat);

            // Two tall H-frame bridge towers
            float[] towerXs = { -6.8f, 6.8f };
            for (int t = 0; t < towerXs.Length; t++)
            {
                float tx = towerXs[t];
                MakeBox(bridge.transform, "TowerLegF_" + t, new Vector3(tx, 7.8f, -0.55f), new Vector3(0.55f, 15.6f, 0.45f), steelMat);
                MakeBox(bridge.transform, "TowerLegB_" + t, new Vector3(tx, 7.8f, 0.55f), new Vector3(0.55f, 15.6f, 0.45f), steelMat);
                MakeBox(bridge.transform, "CrossStrutTop_" + t, new Vector3(tx, 14.8f, 0f), new Vector3(0.48f, 0.45f, 1.35f), steelMat);
                MakeBox(bridge.transform, "CrossStrutMid_" + t, new Vector3(tx, 10.2f, 0f), new Vector3(0.45f, 0.38f, 1.35f), steelMat);
                MakeBox(bridge.transform, "CrossStrutLow_" + t, new Vector3(tx, 5.4f, 0f), new Vector3(0.48f, 0.42f, 1.45f), steelMat);
            }

            // Main catenary cables & vertical suspender cables
            for (int s = -10; s <= 10; s++)
            {
                float x = s * 1.25f;
                float norm = (Mathf.Abs(x) <= 6.8f)
                    ? Mathf.Pow(x / 6.8f, 2f)
                    : Mathf.Pow((13.5f - Mathf.Abs(x)) / 6.7f, 2f);
                float cableY = Mathf.Lerp(5.8f, 14.9f, Mathf.Clamp01(norm));
                MakeBox(bridge.transform, "CableNode_" + s, new Vector3(x, cableY, -0.55f), new Vector3(1.32f, 0.14f, 0.12f), steelMat);
                float h = Mathf.Max(0.4f, cableY - 5.2f);
                MakeBox(bridge.transform, "Suspender_" + s, new Vector3(x, 5.2f + h * 0.5f, -0.55f), new Vector3(0.06f, h, 0.06f), steelMat);
            }
        }

        void BuildIstanbulMosqueSkyline(Transform parent, Vector3 pos, Material domeMat, float height = 10.5f)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Süleymaniye", parent, pos, Quaternion.identity, height, out var eMosque))
            {
                eMosque.name = "IstanbulGrandMosque_Suleymaniye";
                return;
            }

            BuildPrimitiveMosque(parent, pos, domeMat, height, 4);
        }

        /// <summary>Procedural Ottoman mosque: cascading domes and 2 or 4 pencil minarets (10.5 m tall at scale 1).</summary>
        void BuildPrimitiveMosque(Transform parent, Vector3 pos, Material domeMat, float height, int minarets)
        {
            var mosque = new GameObject("IstanbulGrandMosque");
            mosque.transform.SetParent(parent, false);
            mosque.transform.localPosition = pos;
            mosque.transform.localScale = Vector3.one * (height / 10.5f);

            // Platform, sanctuary hall, drum + central dome, four semi-domes and corner domes (cascading silhouette)
            MakeBox(mosque.transform, "Platform", new Vector3(0f, 0.3f, 0f), new Vector3(9.6f, 0.6f, 7.6f), _limestoneMat);
            MakeBox(mosque.transform, "SanctuaryBase", new Vector3(0f, 1.6f, 0f), new Vector3(6.8f, 2.4f, 5.6f), _creamPlasterMat);
            MakeCylinder(mosque.transform, "DomeDrum", new Vector3(0f, 3.2f, 0f), new Vector3(3.6f, 0.45f, 3.6f), _creamPlasterMat);
            MakeSphere(mosque.transform, "GrandCentralDome", new Vector3(0f, 3.6f, 0f), new Vector3(4.2f, 3.4f, 4.2f), domeMat);
            MakeSphere(mosque.transform, "SemiDomeL", new Vector3(-2.3f, 2.8f, 0f), new Vector3(2.6f, 1.9f, 2.6f), domeMat);
            MakeSphere(mosque.transform, "SemiDomeR", new Vector3(2.3f, 2.8f, 0f), new Vector3(2.6f, 1.9f, 2.6f), domeMat);
            MakeSphere(mosque.transform, "SemiDomeF", new Vector3(0f, 2.8f, -2.0f), new Vector3(2.6f, 1.9f, 2.2f), domeMat);
            MakeSphere(mosque.transform, "SemiDomeB", new Vector3(0f, 2.8f, 2.0f), new Vector3(2.6f, 1.9f, 2.2f), domeMat);
            Vector3[] corner = { new Vector3(-2.7f, 2.9f, -2.1f), new Vector3(2.7f, 2.9f, -2.1f), new Vector3(-2.7f, 2.9f, 2.1f), new Vector3(2.7f, 2.9f, 2.1f) };
            for (int i = 0; i < corner.Length; i++)
                MakeSphere(mosque.transform, "CornerDome_" + i, corner[i], new Vector3(1.2f, 1.0f, 1.2f), domeMat);
            MakeCylinder(mosque.transform, "DomeFinial", new Vector3(0f, 5.6f, 0f), new Vector3(0.12f, 0.55f, 0.12f), _stopYellowMat);

            // 4 tall slender minarets with balconies (şerefe) and conical lead spires
            Vector3[] minaretOffsets =
            {
                new Vector3(-4.2f, 0f, -2.4f),
                new Vector3(4.2f, 0f, -2.4f),
                new Vector3(-3.4f, 0f, 2.4f),
                new Vector3(3.4f, 0f, 2.4f)
            };
            for (int m = 0; m < Mathf.Min(minarets, minaretOffsets.Length); m++)
            {
                Vector3 mp = minaretOffsets[m];
                // Slender pencil minarets (Ottoman), taller than the dome
                MakeCylinder(mosque.transform, "MinaretShaft_" + m, mp + new Vector3(0f, 4.6f, 0f), new Vector3(0.30f, 4.6f, 0.30f), _creamPlasterMat);
                MakeCylinder(mosque.transform, "Balcony1_" + m, mp + new Vector3(0f, 5.0f, 0f), new Vector3(0.52f, 0.12f, 0.52f), _limestoneMat);
                MakeCylinder(mosque.transform, "Balcony2_" + m, mp + new Vector3(0f, 7.2f, 0f), new Vector3(0.48f, 0.12f, 0.48f), _limestoneMat);
                MakeCylinder(mosque.transform, "MinaretSpire_" + m, mp + new Vector3(0f, 10.0f, 0f), new Vector3(0.24f, 0.9f, 0.24f), domeMat);
            }
            CombineByMaterial(mosque.transform);
        }

        Transform BuildBosphorusFerry(Transform parent, Vector3 pos, Material ferryBlueMat, float height = 3.6f, string model = "Çevre_Vapur")
        {
            if (GlbModelLoader.TryInstantiateEModel(model, parent, pos, Quaternion.Euler(0f, 72f, 0f), height, out var eFerry)
                || GlbModelLoader.TryInstantiateEModel("Çevre_Vapur", parent, pos, Quaternion.Euler(0f, 72f, 0f), height, out eFerry))
            {
                eFerry.name = "BosphorusFerry_Vapur";
                return eFerry.transform;
            }

            var ferry = new GameObject("BosphorusFerry_Vapur");
            ferry.transform.SetParent(parent, false);
            ferry.transform.localPosition = pos;
            ferry.transform.localRotation = Quaternion.Euler(0f, 72f, 0f);
            ferry.transform.localScale = Vector3.one * (height / 3.6f);

            MakeBox(ferry.transform, "HullWhite", new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.70f, 7.6f), _whitePlasterMat);
            MakeBox(ferry.transform, "HullBlueStripe", new Vector3(0f, 0.28f, 0f), new Vector3(2.46f, 0.24f, 7.64f), ferryBlueMat);
            MakeBox(ferry.transform, "LowerDeckCabin", new Vector3(0f, 0.98f, 0f), new Vector3(2.1f, 0.65f, 6.2f), _whitePlasterMat);
            MakeBox(ferry.transform, "UpperDeckCabin", new Vector3(0f, 1.55f, -0.2f), new Vector3(1.85f, 0.55f, 4.8f), _whitePlasterMat);
            MakeBox(ferry.transform, "Wheelhouse", new Vector3(0f, 2.02f, 1.2f), new Vector3(1.45f, 0.42f, 1.2f), _whitePlasterMat);
            MakeCylinder(ferry.transform, "Funnel", new Vector3(0f, 2.35f, -0.4f), new Vector3(0.48f, 0.62f, 0.48f), _stopYellowMat);
            MakeCylinder(ferry.transform, "FunnelTop", new Vector3(0f, 2.85f, -0.4f), new Vector3(0.50f, 0.16f, 0.50f), _ironMat);

            for (int w = -4; w <= 4; w++)
            {
                MakeBox(ferry.transform, "CabinWinL_" + w, new Vector3(-1.06f, 1.0f, w * 0.62f), new Vector3(0.05f, 0.26f, 0.36f), _lampGlowMat);
                MakeBox(ferry.transform, "CabinWinR_" + w, new Vector3(1.06f, 1.0f, w * 0.62f), new Vector3(0.05f, 0.26f, 0.36f), _lampGlowMat);
            }
            return ferry.transform;
        }

        Transform BuildMotorboat(Transform parent, Vector3 pos, Material trimMat)
        {
            var mb = new GameObject("Motorboat");
            mb.transform.SetParent(parent, false);
            mb.transform.localPosition = pos;
            mb.transform.localRotation = Quaternion.Euler(0f, 65f, 0f);
            MakeBox(mb.transform, "Hull", new Vector3(0f, 0.20f, 0f), new Vector3(1.1f, 0.36f, 2.6f), _whitePlasterMat);
            MakeBox(mb.transform, "Stripe", new Vector3(0f, 0.22f, 0f), new Vector3(1.14f, 0.12f, 2.62f), trimMat);
            MakeBox(mb.transform, "Cabin", new Vector3(0f, 0.52f, -0.1f), new Vector3(0.86f, 0.34f, 1.35f), _whitePlasterMat);
            return mb.transform;
        }

        Transform BuildSailboat(Transform parent, Vector3 pos, Material sailMat)
        {
            var boat = new GameObject("Sailboat");
            boat.transform.SetParent(parent, false);
            boat.transform.localPosition = pos;

            MakeBox(boat.transform, "Hull", new Vector3(0f, 0.25f, 0f), new Vector3(1.1f, 0.45f, 3.2f), _whitePlasterMat);
            MakeCylinder(boat.transform, "Mast", new Vector3(0f, 2.2f, 0f), new Vector3(0.08f, 2.0f, 0.08f), _pergolaWoodMat);
            MakeBox(boat.transform, "MainSail", new Vector3(0f, 2.3f, -0.35f), new Vector3(0.06f, 2.8f, 1.35f), sailMat);
            MakeBox(boat.transform, "JibSail", new Vector3(0f, 2.0f, 0.75f), new Vector3(0.06f, 2.1f, 0.75f), sailMat);
            return boat.transform;
        }

        // ---------------------------------------------------------------- Public Controls

        public void SetRoadWetness(bool wet)
        {
            _roadWet = wet;
            if (_roadMat != null)
            {
                // Wet asphalt stays dielectric. A broad sheen plus localized puddles gives the road
                // readable reflections without making the whole surface look like polished metal.
                _roadMat.color = wet ? new Color(0.13f, 0.15f, 0.18f) : new Color(0.21f, 0.22f, 0.25f);
                _roadMat.SetFloat("_Smoothness", wet ? 0.76f : 0.28f);
                _roadMat.SetFloat("_Metallic", 0.025f);
            }
            for (int i = 0; i < _wetPuddleRenderers.Count; i++)
                if (_wetPuddleRenderers[i] != null) _wetPuddleRenderers[i].enabled = wet;
            if (_sidewalkCobbleMat != null)
            {
                _sidewalkCobbleMat.color = wet ? new Color(0.74f, 0.69f, 0.62f) : new Color(0.82f, 0.77f, 0.69f);
                _sidewalkCobbleMat.SetFloat("_Smoothness", wet ? 0.42f : 0.24f);
                _sidewalkCobbleMat.SetFloat("_Metallic", 0.02f);
            }
        }

        public void SetStreetlightsLit(bool lit)
        {
            _lampsLit = lit;
            for (int i = _lanternGlows.Count - 1; i >= 0; i--)
                if (_lanternGlows[i] == null) _lanternGlows.RemoveAt(i); else _lanternGlows[i].enabled = lit;
            if (_lampGlowMat == null) return;
            _lampGlowMat.color = lit ? new Color(1.0f, 0.92f, 0.55f) : new Color(0.95f, 0.84f, 0.48f);
            if (_lampGlowMat.HasProperty("_EmissionColor"))
            {
                _lampGlowMat.EnableKeyword("_EMISSION");
                _lampGlowMat.SetColor("_EmissionColor", new Color(1.0f, 0.78f, 0.32f) * (lit ? 1.8f : 0.85f));
            }
        }

        // ---------------------------------------------------------------- Traffic & Pedestrians

void SpawnRoadsidePedestrianPool()
        {
            int pedCount = Mathf.Clamp(DeviceTierDetector.CurrentTier == DeviceTier.Low ? 2 : 4, 2, 4);
            for (int i = 0; i < pedCount; i++)
            {
                var visual = new RoadsidePedestrianVisual();
                var root = new GameObject("RoadsidePedestrian_Silhouette_" + i);
                root.transform.SetParent(transform, false);
                root.transform.localScale = Vector3.one * 0.88f;
                // Match the district's mirrored street and stay within the bridge footway.
                root.transform.localPosition = new Vector3(PedestrianSideX(i), 0.16f, 44f + i * 26f);
                root.transform.localRotation = Quaternion.Euler(0f, CurrentSeaSide < 0 ? 105f : -105f, 0f);
                visual.Root = root.transform;
                visual.StepPhase = i * 1.73f;

                var coat = _pedestrianCoatMats[i % _pedestrianCoatMats.Length];
                CreatePedestrianPart(root.transform, PrimitiveType.Capsule, "Coat", new Vector3(0f, 1.05f, 0f), new Vector3(0.42f, 0.55f, 0.27f), coat);
                CreatePedestrianPart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.78f, 0.02f), new Vector3(0.30f, 0.34f, 0.30f), _pedestrianSkinMat);
                visual.LeftLeg = CreatePedestrianPart(root.transform, PrimitiveType.Capsule, "LeftLeg", new Vector3(-0.14f, 0.48f, 0f), new Vector3(0.16f, 0.48f, 0.17f), _pedestrianTrousersMat);
                visual.RightLeg = CreatePedestrianPart(root.transform, PrimitiveType.Capsule, "RightLeg", new Vector3(0.14f, 0.48f, 0f), new Vector3(0.16f, 0.48f, 0.17f), _pedestrianTrousersMat);
                visual.LeftArm = CreatePedestrianPart(root.transform, PrimitiveType.Capsule, "LeftArm", new Vector3(-0.30f, 1.08f, 0f), new Vector3(0.13f, 0.38f, 0.14f), coat, new Vector3(0f, 0f, -12f));
                visual.RightArm = CreatePedestrianPart(root.transform, PrimitiveType.Capsule, "RightArm", new Vector3(0.30f, 1.08f, 0f), new Vector3(0.13f, 0.38f, 0.14f), coat, new Vector3(0f, 0f, 12f));
                CurveBender.Convert(root.transform);
                _roadsidePedestrians.Add(visual);
            }
        }

        static void RemoveVisualCollider(GameObject visual)
        {
            if (!visual.TryGetComponent<Collider>(out var collider)) return;
            // Destruction is deferred until the frame ends. Disable first so mirrored street setup
            // cannot register a negative-scale primitive with PhysX during that interval.
            collider.enabled = false;
            Object.Destroy(collider);
        }

        static Transform CreatePedestrianPart(Transform parent, PrimitiveType primitive, string name, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler = default)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            RemoveVisualCollider(part);
            return part.transform;
        }

        void Update()
        {
            if (_taxi == null) return;
            RefreshChunks(false);
            if (!_seaHasGpuWaves && _seaMat != null && _seaMat.mainTexture != null)
                _seaMat.mainTextureOffset = new Vector2(Time.time * 0.012f, Time.time * 0.02f);
            for (int i = 0; i < _windmillRotors.Count; i++)
                if (_windmillRotors[i] != null && _windmillRotors[i].gameObject.activeInHierarchy)
                    _windmillRotors[i].Rotate(0f, 0f, Time.deltaTime * (38f + i * 9f), Space.Self);
            UpdateLampLights();
            if (_distantHorizonVista != null)
            {
                // The vista lies ahead of the taxi; the back-seat camera looks backwards through the rear window,
                // where only the street the taxi has driven along belongs.
                bool inBackSeat = GameManager.Instance != null && GameManager.Instance.Taxi != null && !GameManager.Instance.Taxi.ExteriorVisible;
                if (_distantHorizonVista.activeSelf == inBackSeat)
                    _distantHorizonVista.SetActive(!inBackSeat);
                _distantHorizonVista.transform.position = new Vector3(0f, 0f, _taxi.position.z);
                // the vista is authored with the sea on the left, like the street
                _distantHorizonVista.transform.localScale = new Vector3(CurrentSeaSide < 0 ? 1f : -1f, 1f, 1f);
                UpdateReferenceSeaLevel();
                if (_vistaFerry != null)
                {
                    // Crossing the strait one way at a steady ferry pace, wrapping round out of sight, with a slow
                    // gentle swell (heights from a fixed base: adding to the current height made it hop about)
                    if (_vistaFerryBaseY == null) _vistaFerryBaseY = _vistaFerry.localPosition.y;
                    float phase = Time.time * 0.035f;
                    float ferryX = _usesReferenceHorizon ? -57f + Mathf.Cos(phase) * 35f
                        : Mathf.Repeat(Time.time * 2.2f, 170f) - 82f;
                    float ferryZ = _usesReferenceHorizon ? 130f + Mathf.Sin(phase) * 8f
                        : _vistaFerry.localPosition.z;
                    float heading = _usesReferenceHorizon
                        ? Mathf.Atan2(-35f * Mathf.Sin(phase), 8f * Mathf.Cos(phase)) * Mathf.Rad2Deg - 72f
                        : 0f;
                    float seaEnergy = BosphorusSeaState.Instance != null
                        ? (BosphorusSeaState.Instance.Current.amplitudeA + BosphorusSeaState.Instance.Current.amplitudeB) / .18f : 1f;
                    float roll = Mathf.Sin(Time.time * 0.55f) * 0.6f * seaEnergy;
                    float pitch = Mathf.Cos(Time.time * 0.45f) * 0.3f * seaEnergy;
                    float ferryY = _usesReferenceHorizon ? _referenceSeaLevel - 0.16f : _vistaFerryBaseY.Value;
                    _vistaFerry.localPosition = new Vector3(ferryX, ferryY + Mathf.Sin(Time.time * 0.6f) * 0.04f * seaEnergy, ferryZ);
                    _vistaFerry.localRotation = _vistaFerryRestRotation * Quaternion.Euler(pitch, heading, roll);
                }
                for (int i = 0; i < _vistaBoats.Count; i++)
                {
                    if (_vistaBoats[i] == null) continue;
                    float ph = _vistaBoatsPhase[i];
                    Vector3 bp = _vistaBoatsBase[i];
                    float seaEnergy = BosphorusSeaState.Instance != null
                        ? (BosphorusSeaState.Instance.Current.amplitudeA + BosphorusSeaState.Instance.Current.amplitudeB) / .18f : 1f;
                    float bob = Mathf.Sin(Time.time * 1.1f + ph) * 0.045f * seaEnergy;
                    float boatRoll = Mathf.Sin(Time.time * 1.0f + ph) * 1.7f * seaEnergy;
                    float boatPitch = Mathf.Cos(Time.time * 0.85f + ph) * 0.8f * seaEnergy;
                    float drift = Mathf.Sin(Time.time * 0.25f + ph) * 1.5f;
                    float waterLevelOffset = _usesReferenceHorizon ? _referenceSeaLevel + 0.35f : 0f;
                    _vistaBoats[i].localPosition = new Vector3(bp.x + drift, bp.y + waterLevelOffset + bob, bp.z);
                    _vistaBoats[i].localRotation = _vistaBoatsRestRotation[i] * Quaternion.Euler(boatPitch, 0f, boatRoll);
                }

                // Keep the skyline in view while giving the mid-city and far bridge
                // distinct bounded travel parallax. Coastal boats are streamed in world space below.
                float travel = _taxi.position.z;
                _horizonFarLayer.localPosition = new Vector3(Mathf.Sin(travel * 0.0011f) * 0.45f, 0f, Mathf.Sin(travel * 0.0018f) * 1.4f);
                _horizonMidLayer.localPosition = new Vector3(Mathf.Sin(travel * 0.0017f) * 0.9f, 0f, Mathf.Sin(travel * 0.0032f) * 2.8f);
            }
        }

void UpdateRoadsidePedestrians()
        {
            if (GameManager.Instance == null || GameManager.Instance.Taxi == null) return;
            float taxiZ = _taxi.position.z;
            int kmh = GameManager.Instance.Taxi.Kmh;
            int streak = SaveSystem.Data != null ? SaveSystem.Data.streak : 0;

            int visibleCount = Mathf.Clamp(2 + streak + (kmh > 42 ? 2 : 0), 2, _roadsidePedestrians.Count);

            for (int i = 0; i < _roadsidePedestrians.Count; i++)
            {
                var ped = _roadsidePedestrians[i];
                Vector3 pedestrianPosition = ped.Root.localPosition;
                pedestrianPosition.x = PedestrianSideX(i);
                ped.Root.localPosition = pedestrianPosition;
                ped.Root.localRotation = Quaternion.Euler(0f, CurrentSeaSide < 0 ? 105f : -105f, 0f);
                bool shouldShow = i < visibleCount && GameManager.Instance.Taxi.ExteriorVisible && !_streetHidden
                    && GameManager.Instance.State != GameState.MainMenu;
                if (ped.Root.gameObject.activeSelf != shouldShow)
                    ped.Root.gameObject.SetActive(shouldShow);

                if (shouldShow && ped.Root.position.z < taxiZ + 32f)
                {
                    float newZ = taxiZ + 70f + i * 22f;
                    ped.Root.localPosition = new Vector3(PedestrianSideX(i), 0.16f, newZ);
                }

                if (shouldShow)
                {
                    float step = Mathf.Sin(Time.time * 4.2f + ped.StepPhase) * 18f;
                    ped.LeftLeg.localRotation = Quaternion.Euler(step, 0f, 0f);
                    ped.RightLeg.localRotation = Quaternion.Euler(-step, 0f, 0f);
                    ped.LeftArm.localRotation = Quaternion.Euler(-step * 0.65f, 0f, -12f);
                    ped.RightArm.localRotation = Quaternion.Euler(step * 0.65f, 0f, 12f);
                }
            }
        }

        static Vector3 StreetMirrorScale => CurrentSeaSide < 0 ? Vector3.one : new Vector3(-1f, 1f, 1f);

        static float PedestrianSideX(int index)
        {
            float offset = CurrentRoad == TourRoadType.Bridge
                ? GameConfig.RoadHalfWidth + BridgeFootway * 0.5f
                : 4.55f - SideShift + (index % 2) * 0.45f;
            return offset * (CurrentSeaSide < 0 ? -1f : 1f);
        }

        /// <summary>Cold district work belongs to the explicit transition, never a distance-streaming frame.
        /// One visible window plus one spare of each variant bounds memory to at most fourteen street chunks.</summary>
        void PrepareStreetLayout()
        {
            _preparingStreetLayout = true;
            bool complete = false;
            try
            {
                BeginStreetLayout(out int baseKey, out int min, out int max);
                for (int index = min; index <= max; index++) PrepareChunk(baseKey + VariantForChunk(index));
                for (int variant = 0; variant < StreetVariants; variant++) PrepareChunk(baseKey + variant);
                CompleteStreetLayout();
                complete = true;
            }
            finally
            {
                if (!complete)
                {
                    _preparingStreetLayout = false;
                    _streetLayoutReady = false;
                    ReleaseStreetLayout();
                    _lastCenterChunk = int.MinValue;
                }
            }
        }

        void BeginStreetLayout(out int baseKey, out int min, out int max)
        {
            _streetLayoutReady = false;
            ReleaseStreetLayout();
            _preparedRoad = CurrentRoad;
            _preparedDistrict = _district;
            baseKey = (int)_preparedRoad * 100;
            for (int variant = 0; variant < StreetVariants; variant++)
                _variantPools.Add(baseKey + variant, new Queue<GameObject>(PreparedChunkCapacity));

            int center = Mathf.Max(0, Mathf.FloorToInt(_taxi.position.z / GameConfig.ChunkLength));
            min = center - GameConfig.ChunksBehind;
            max = center + GameConfig.ChunksAhead;
        }

        void CompleteStreetLayout()
        {
            _streetLayoutReady = true;
            _preparingStreetLayout = false;
            _lastCenterChunk = int.MinValue;
            RefreshChunks(true);
        }

        void PrepareChunk(int key)
        {
            // Track only this chunk's generated meshes; the transition yields with this flag cleared.
            _buildingLayout = true;
            try
            {
                GameObject chunk = BuildChunkTemplate(key);
                CurveBender.Convert(chunk.transform);
                WorldRayTracingScene.Instance?.RegisterRoot(chunk.transform);
                chunk.SetActive(false);
                _chunkVariant.Add(chunk, key);
                _variantPools[key].Enqueue(chunk);
            }
            finally { _buildingLayout = false; }
        }

        void ReleaseStreetLayout()
        {
            RemoveStreetRenderers(_wetPuddleRenderers);
            RemoveStreetRenderers(_lanternGlows);
            RemoveStreetRenderers(_windowGlows);
            foreach (var chunk in _chunkVariant.Keys)
            {
                if (chunk == null) continue;
                chunk.SetActive(false);
                WorldRayTracingScene.Instance?.UnregisterRoot(chunk.transform);
                Object.Destroy(chunk);
            }
            foreach (var mesh in _layoutMeshes)
            {
                _generatedMeshes.Remove(mesh);
                if (mesh != null) Object.Destroy(mesh);
            }
            _layoutMeshes.Clear();
            _activeChunks.Clear();
            _variantPools.Clear();
            _chunkVariant.Clear();
        }

        void RemoveStreetRenderers<T>(List<T> renderers) where T : Renderer
        {
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                var renderer = renderers[i];
                bool remove = renderer == null;
                if (!remove)
                    foreach (var chunk in _chunkVariant.Keys)
                        if (chunk != null && renderer.transform.IsChildOf(chunk.transform))
                        {
                            remove = true;
                            break;
                        }
                if (remove) renderers.RemoveAt(i);
            }
        }

        GameObject TakePreparedChunk(int preferredKey, int avoidKey)
        {
            if (preferredKey != avoidKey && _variantPools.TryGetValue(preferredKey, out var preferred) && preferred.Count > 0)
                return preferred.Dequeue();
            int baseKey = (int)_preparedRoad * 100;
            for (int offset = 1; offset <= StreetVariants; offset++)
            {
                int key = baseKey + (preferredKey % 100 + offset) % StreetVariants;
                if (key != avoidKey && _variantPools.TryGetValue(key, out var pool) && pool.Count > 0)
                    return pool.Dequeue();
            }
            // A repeated neighbour is preferable to an empty road; this also supports arbitrary debug warps.
            for (int variant = 0; variant < StreetVariants; variant++)
                if (_variantPools.TryGetValue(baseKey + variant, out var fallbackPool) && fallbackPool.Count > 0)
                    return fallbackPool.Dequeue();
            return null;
        }

        void RefreshChunks(bool immediate)
        {
            if (_preparingStreetLayout) return;
            int centerChunk = Mathf.Max(0, Mathf.FloorToInt(_taxi.position.z / GameConfig.ChunkLength));
            if (!immediate && centerChunk == _lastCenterChunk) return;
            _lastCenterChunk = centerChunk;

            // The taxi's rear and chase camera start behind Z=0. Keep the full behind
            // window there too, so a new tour never exposes sea beneath the road.
            int minChunk = centerChunk - GameConfig.ChunksBehind;
            int maxChunk = centerChunk + GameConfig.ChunksAhead;

            _chunkKeysToRemove.Clear();
            foreach (var kv in _activeChunks)
            {
                if (kv.Key < minChunk || kv.Key > maxChunk)
                {
                    kv.Value.SetActive(false);
                    int v = _chunkVariant.TryGetValue(kv.Value, out var vv) ? vv : 0;
                    _variantPools[v].Enqueue(kv.Value);
                    _chunkKeysToRemove.Add(kv.Key);
                }
            }
            for (int i = 0; i < _chunkKeysToRemove.Count; i++)
            {
                _activeChunks.Remove(_chunkKeysToRemove[i]);
            }

            for (int idx = minChunk; idx <= maxChunk; idx++)
            {
                if (_activeChunks.ContainsKey(idx)) continue;
                int variant = (int)CurrentRoad * 100 + VariantForChunk(idx);
                int previousKey = _activeChunks.TryGetValue(idx - 1, out var previous)
                    ? _chunkVariant[previous] : -1;
                GameObject chunk = TakePreparedChunk(variant, previousKey);
                if (chunk == null) continue;
                chunk.transform.position = new Vector3(0f, 0f, idx * GameConfig.ChunkLength);
                // Chunks are authored with the water on the left; mirror them unless this district's water is on the left
                chunk.transform.localScale = StreetMirrorScale;
                chunk.SetActive(!_streetHidden);
                _activeChunks[idx] = chunk;
            }
        }

        // ---------------------------------------------------------------- Exact 03-city.png Chunk Builder

        GameObject BuildChunkTemplate(int key)
        {
            // key = street type * 100 + variant (see RefreshChunks). Everything is authored with the water / promenade on
            // the left (-x) and the curb buildings on the right; the chunk is mirrored per district when placed.
            var road = (TourRoadType)(key / 100);
            int variant = key % 100;
            bool coastal = road == TourRoadType.Coastal, bridge = road == TourRoadType.Bridge;
            var root = new GameObject("StreetChunk");
            root.transform.SetParent(transform, false);
            float L = GameConfig.ChunkLength; // 48m per chunk

            // 1. Asphalt Road + Dashed Center Line (built at the real width; everything beside it is laid out for
            // LayoutRoadHalf and pulled in by PullSidesToRoad at the end)
            // Galata's hill streets are cobbled, without lane paint
            bool cobbled = road == TourRoadType.Hill;
            MakeBox(root.transform, "Road", new Vector3(0f, -0.05f, L * 0.5f), new Vector3(GameConfig.RoadHalfWidth * 2f, 0.1f, L), cobbled ? _hillCobbleMat : _roadMat);
            BuildWetPuddlePatches(root.transform, variant, L);
            if (!cobbled)
                for (float z = 3f; z < L; z += 8f)
                    if (RoadMarkingLayout.DashClearsCrossing(variant, L, z))
                        MakeBox(root.transform, "Dash", new Vector3(0f, 0.01f, z), new Vector3(0.16f, 0.02f, RoadMarkingLayout.DashLength), _laneMat);
            // A zebra crossing appears in one seeded street module, so it is visible often enough to
            // establish a lived-in street without repeating every 48 m chunk.
            // Road tile reference: wide, worn cream stripes across the road
            if (variant == RoadMarkingLayout.CrossingVariant)
                for (int stripe = 0; stripe < RoadMarkingLayout.StripeCount; stripe++)
                    MakeBox(root.transform, "CrosswalkStripe_" + stripe,
                        new Vector3(0f, 0.012f, RoadMarkingLayout.StripeCenter(L, stripe)),
                        new Vector3(GameConfig.RoadHalfWidth * 1.55f, 0.02f, RoadMarkingLayout.StripeLength), _crosswalkMat);

            // 2. Beige stone-paved sidewalks + chunky cut-stone kerbs (road tile reference)
            // (a bridge has a narrow footway each side instead)
            float swWidth = bridge ? BridgeFootway : GameConfig.SidewalkWidth;
            float swX = LayoutRoadHalf + swWidth * 0.5f;
            MakeBox(root.transform, "SidewalkR", new Vector3(swX, 0.08f, L * 0.5f), new Vector3(swWidth, 0.16f, L), _sidewalkCobbleMat);
            MakeBox(root.transform, "SidewalkL", new Vector3(-swX, 0.08f, L * 0.5f), new Vector3(swWidth, 0.16f, L), _sidewalkCobbleMat);
            BuildStoneKerb(root.transform, 1f, variant, L);
            BuildStoneKerb(root.transform, -1f, variant, L);

            if (bridge)
            {
                BuildBridgeSides(root.transform, variant, L);
                PullSidesToRoad(root.transform);
                return root;
            }
            if (!coastal)
                BuildFarSideStreet(root.transform, road, variant, L);

            // 3. Bosphorus Sea on the Left + Wrought-Iron Coastal Promenade Railing
            if (coastal)
            {
                if (!_usesReferenceHorizon)
                    BosphorusWaterMesh.Create(root.transform, "BosphorusSea", new Vector3(-28f, BosphorusSeaState.CoastalLevel, L * 0.5f), new Vector2(42f, L), _seaMat, curveBounds: true);
                BuildCoastalPromenadeRailing(root.transform, -LayoutRoadHalf - GameConfig.SidewalkWidth + 0.25f, L);
            }

            // Boats belong to streamed world chunks, so they pass by naturally instead of being
            // parented to the taxi-relative horizon. The old motorboat at x=-6.4 was on the sidewalk.
            if (!coastal || _usesReferenceHorizon) { }
            else if (variant == 0 || variant == 2)
            {
                if (variant == 0)
                    BuildBosphorusFerry(root.transform, new Vector3(-19f, -0.15f, L * 0.56f), _ferryBlueMat, 5.5f, "Çevre_Vapur2");
                else
                    BuildMotorboat(root.transform, new Vector3(-13f, -0.22f, L * 0.46f), _ferryBlueMat);
            }
            else if (variant == 4)
            {
                BuildSailboat(root.transform, new Vector3(-25f, -0.2f, L * 0.62f), _sailMat);
            }

            // 4. Two Complete 3-Building Attached Townhouse Trios per 48m Chunk (exact match to 03-city.png top scene!)
            BuildThreeBuildingTownhouseRow(root.transform, 12f, variant * 2);
            BuildThreeBuildingTownhouseRow(root.transform, 36f, variant * 2 + 1);

            // 4b. Terraced backdrop houses behind the street row (dense hillside look, fills the gaps between houses).
            BuildBackdropTerraces(root.transform, variant, L);

            // 5. Street Furniture & Tree Types along the Sidewalk
            for (int block = 0; block < 2; block++)
            {
                float bz = block * 24f;

                // ---- BİNALAR TARAFI (Right Sidewalk: x > 0) ----
                // Bina tarafındaki saksı ve ağaç sayısı azaltıldı; daha seyrek ve düzenli yerleştirildi:
                if (block == 0)
                {
                    // Cafe evi önü: ağaç OLMASIN, sadece 1 adet saksı:
                    BuildTerracottaAmphoraPot(root.transform, new Vector3(4.55f, 0.16f, bz + 4.2f));
                    // Blok ortası: 1 adet taş saksı:
                    BuildStonePlanterBox(root.transform, new Vector3(4.52f, 0.16f, bz + 14.5f), false);
                    // 48m chunk başına bina tarafında sadece 1 adet ağaç (blok sonu, duvara yakın x=6.4f):
                    BuildTree(root.transform, new Vector3(6.4f, 0.16f, bz + 22.0f), variant % 3);
                    // Sokak lambası bina tarafında: 48m chunk başına sadece 1 adet (block 0'da):
                    BuildSwanNeckStreetlamp(root.transform, new Vector3(4.15f, 0.16f, bz + 16.0f));
                }
                else
                {
                    // İkinci 24m blok: seyrek ve düzenli 1 adet saksı, ağaç yok:
                    BuildStonePlanterBox(root.transform, new Vector3(4.52f, 0.16f, bz + 12.0f), false);
                }

                // Yolcular sahil tarafından alındığı için bina tarafındaki taksi durağı direkleri kaldırıldı!

                // Kaldırım kenarındaki dekoratif dökme demir bariyer babaları.
                float[] bollardOffsets = { 2.8f, 8.2f, 11.8f, 14.4f, 18.8f, 22.2f };
                for (int k = 0; k < bollardOffsets.Length; k++)
                {
                    // Set back from the curb so the taxi's open door clears them at a stop
                    BuildCastIronBollard(root.transform, new Vector3(4.35f, 0.16f, bz + bollardOffsets[k]));
                    BuildCastIronBollard(root.transform, new Vector3(-4.35f, 0.16f, bz + bollardOffsets[k]));
                }

                if (!coastal) continue;

                // ---- SAHİL TARAFI (Left Coastal Promenade: x < 0) ----
                // 1) Yeni Çevre_Bank modeli: Sahil kenarında (denize bakacak şekilde).
                // Korkuluktan (-7.15f) doğal oturma/yürüme mesafesi (1.50m) bırakacak şekilde -5.65f'ye yerleştirildi:
                if (block == 0)
                {
                    BuildSlattedBench(root.transform, new Vector3(-5.65f, 0.16f, bz + 7.5f), -90f);
                    BuildRibbedGreenTrashBin(root.transform, new Vector3(-5.65f, 0.16f, bz + 9.6f));
                }

                // 2) Sahil tarafında ağaç sayısı düşürüldü: 48m chunk başına sadece 1 adet (block 1'de):
                if (block == 1)
                {
                    BuildTree(root.transform, new Vector3(-5.4f, 0.16f, bz + 18.0f), (variant + 1) % 3);
                }

                // 3) Sahil tarafında SAKSI OLMASIN (kaldırıldı)!

                // 4) Sahil tarafında sokak lambası seyreltildi: 48m chunk başına sadece 1 adet (block 1'de):
                if (block == 1)
                {
                    // Back from the curb: on the narrow road the stop camera stands right beside the kerb
                    BuildSwanNeckStreetlamp(root.transform, new Vector3(-4.9f, 0.16f, bz + 8.0f));
                }
            }

            AnimatedStreetDetails.Populate(root.transform, variant, coastal);
            PullSidesToRoad(root.transform);
            return root;
        }

        // ---------------------------------------------------------------- Night: lanterns, lamp light, lit windows

        bool _lampsLit, _windowsLit;
        readonly List<Renderer> _lanternGlows = new List<Renderer>();
        readonly List<Renderer> _windowGlows = new List<Renderer>();
        const int LampCapacity = 6;
        readonly Light[] _lampLights = new Light[LampCapacity];
        readonly float[] _lampDistances = new float[LampCapacity];
        readonly Vector3[] _lampPositions = new Vector3[LampCapacity];
        Material _lanternGlowMatCache, _windowGlowMatCache;

        Material _lanternGlowMat => _lanternGlowMatCache != null ? _lanternGlowMatCache
            : (_lanternGlowMatCache = GlowMat(new Color(1.0f, 0.86f, 0.55f), new Color(1.0f, 0.70f, 0.30f) * 1.4f));
        Material _windowGlowMat => _windowGlowMatCache != null ? _windowGlowMatCache
            : (_windowGlowMatCache = GlowMat(new Color(1.0f, 0.62f, 0.28f), new Color(1.0f, 0.52f, 0.16f) * 3.4f));

        static Material GlowMat(Color baseColor, Color emission)
        {
            var m = GlbModelLoader.MakeMat(baseColor, 0.1f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_Cull", 0f);
            return m;
        }

        // Road-facing windows of the three house models, measured on the model at 10 m tall: centre across the
        // facade, centre height, width, height (m). The facade depth (+z, towards the road) is per model.
        static readonly Dictionary<string, (float z, Vector4[] windows)> HouseWindows = new Dictionary<string, (float, Vector4[])>
        {
            { "Çevre_Bina_Mavi_Ev", (1.62f, new[] { new Vector4(0.33f, 5.8f, 1.2f, 2.4f), new Vector4(0.27f, 1.93f, 3.5f, 2.2f) }) },
            { "Çevre_Bina_Pempe_Ev", (1.22f, new[] { new Vector4(2.2f, 5.03f, 0.93f, 2.07f), new Vector4(-0.07f, 5.03f, 1.17f, 2.07f),
                                                    new Vector4(-2.1f, 5.1f, 0.87f, 1.5f), new Vector4(-0.57f, 7.6f, 0.8f, 1.17f),
                                                    new Vector4(-1.07f, 1.67f, 3.2f, 1.67f) }) },
            { "Çevre_Bina_Kafe_Ev", (1.87f, new[] { new Vector4(0.77f, 5.7f, 1.0f, 1.9f), new Vector4(-2.33f, 5.77f, 1.0f, 1.8f),
                                                   new Vector4(0.37f, 2.03f, 2.7f, 2.07f) }) },
        };

        /// <summary>Warm glowing panes over a house's road-facing windows, shown at night; about three in four are lit.</summary>
        void AddNightWindows(GameObject house, string model, System.Random rng)
        {
            if (house == null || !HouseWindows.TryGetValue(model, out var spec)) return;
            var rs = house.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float k = b.size.y / 10f;   // the windows were measured on the model at 10 m
            Quaternion rot = house.transform.rotation;
            foreach (var w in spec.windows)
            {
                if (rng.NextDouble() > 0.75) continue;
                var pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                pane.name = "NightWindow";
                RemoveVisualCollider(pane);
                Vector3 local = new Vector3(w.x * k, 0f, (spec.z + 0.04f) * k);
                pane.transform.position = new Vector3(b.center.x, b.min.y + w.y * k, b.center.z) + rot * local;
                pane.transform.rotation = rot;
                pane.transform.localScale = new Vector3(w.z * k * 0.82f, w.w * k * 0.82f, 1f);
                pane.transform.SetParent(house.transform, true);
                var pr = pane.GetComponent<Renderer>();
                pr.sharedMaterial = _windowGlowMat;
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pr.enabled = _windowsLit;
                _windowGlows.Add(pr);
            }
        }

        /// <summary>Lit windows (night only) and the shop glass glowing brighter.</summary>
        public void SetWindowsLit(bool lit)
        {
            _windowsLit = lit;
            ReferenceCityAssets.SetNightMood(lit);
            for (int i = _windowGlows.Count - 1; i >= 0; i--)
                if (_windowGlows[i] == null) _windowGlows.RemoveAt(i); else _windowGlows[i].enabled = lit;
            if (_warmShopGlassMat != null && _warmShopGlassMat.HasProperty("_EmissionColor"))
                _warmShopGlassMat.SetColor("_EmissionColor", new Color(1f, 0.66f, 0.30f) * (lit ? 1.6f : 0.35f));
        }

        /// <summary>
        /// A few real point lights follow the lit lanterns closest ahead of the taxi (pools of light on the pavement),
        /// a handful only for mobile: 3 on low-end devices, 6 otherwise.
        /// </summary>
        void CreateLampLightPool()
        {
            for (int i = 0; i < LampCapacity; i++)
            {
                var l = new GameObject("LampLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.74f, 0.42f);
                l.range = 9f;
                l.intensity = 7f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lampLights[i] = l;
            }
        }

        void UpdateLampLights()
        {
            int count = DeviceTierDetector.CurrentTier == DeviceTier.Low ? 3 : LampCapacity;
            bool on = _lampsLit && !_streetHidden && _taxi != null;
            int used = 0;
            if (on)
            {
                float tz = _taxi.position.z;
                // nearest lanterns from a little behind the taxi to ~40 m ahead (the bend starts beyond that)
                for (int i = 0; i < count; i++) _lampDistances[i] = float.PositiveInfinity;
                for (int lantern = 0; lantern < _lanternGlows.Count; lantern++)
                {
                    var g = _lanternGlows[lantern];
                    if (g == null || !g.gameObject.activeInHierarchy) continue;
                    Vector3 position = g.transform.position;
                    float dz = position.z - tz;
                    if (dz <= -12f || dz >= 40f) continue;
                    float distance = Mathf.Abs(dz - 8f);
                    int insertion = count - 1;
                    if (distance >= _lampDistances[insertion]) continue;
                    while (insertion > 0 && distance < _lampDistances[insertion - 1])
                    {
                        _lampDistances[insertion] = _lampDistances[insertion - 1];
                        _lampPositions[insertion] = _lampPositions[insertion - 1];
                        insertion--;
                    }
                    _lampDistances[insertion] = distance;
                    _lampPositions[insertion] = position;
                    used = Mathf.Min(used + 1, count);
                }
                for (int i = 0; i < used; i++)
                {
                    _lampLights[i].transform.position = _lampPositions[i] + Vector3.down * 0.3f;
                    _lampLights[i].enabled = true;
                }
            }
            for (int i = used; i < LampCapacity; i++)
                if (_lampLights[i] != null) _lampLights[i].enabled = false;
        }

        // ---------------------------------------------------------------- Landmarks passed on the way, in real order

        enum Pass { KizKulesi, Vapur, Suleymaniye, GalataKulesiFar, GalataKulesiNear, KarakoyIskele, SaatKulesi, Ciragan, OrtakoyCamii, BogazKoprusu, HalicKoprusu, Windmill }

        /// <summary>Per district: at which stop (1–64 within the district) which landmark passes by, in the order they
        /// stand along the real road (TourRoute).</summary>
        static readonly (int stop, Pass what)[][] PassBy =
        {
            new[] { (3, Pass.KizKulesi), (24, Pass.KizKulesi), (52, Pass.Vapur) },                                       // Salacak → Harem
            new[] { (4, Pass.Suleymaniye), (36, Pass.Suleymaniye) },                                                     // Eminönü
            new[] { (4, Pass.GalataKulesiFar), (30, Pass.Vapur), (50, Pass.GalataKulesiFar) },                           // Galata Bridge
            new[] { (4, Pass.KarakoyIskele), (26, Pass.Vapur), (44, Pass.GalataKulesiFar) },                             // Karaköy quay
            new[] { (6, Pass.GalataKulesiNear), (40, Pass.GalataKulesiNear) },                                           // up the Galata hill
            new[] { (10, Pass.Windmill), (42, Pass.Windmill) },                                                          // Yeldeğirmeni
            new[] { (6, Pass.HalicKoprusu), (34, Pass.Vapur) },                                                          // along the Golden Horn
            new[] { (3, Pass.KarakoyIskele), (16, Pass.SaatKulesi), (32, Pass.Ciragan), (50, Pass.OrtakoyCamii), (60, Pass.BogazKoprusu) }, // coast road
        };

        readonly List<Transform> _passing = new List<Transform>();

        /// <summary>Called when a stop is scheduled: puts the landmark the real road passes here beside the street ahead.</summary>
        public void OnStopScheduled(int stop, float taxiZ)
        {
            for (int i = _passing.Count - 1; i >= 0; i--)
                if (_passing[i] == null || _passing[i].position.z < taxiZ - 60f) { if (_passing[i] != null) Destroy(_passing[i].gameObject); _passing.RemoveAt(i); }
            int district = Mathf.Clamp(_district, 0, PassBy.Length - 1);
            int inDistrict = stop - district * GameConfig.StopsPerDistrict;
            foreach (var (at, what) in PassBy[district])
                if (at == inDistrict)
                {
                    var t = BuildPassingLandmark(what, taxiZ + 100f);   // just inside the view, under the haze
                    if (t == null) continue;
                    CurveBender.Convert(t);
                    _passing.Add(t);
                }
        }

        Transform BuildPassingLandmark(Pass what, float z)
        {
            // water side (the bridge has water on both: use the right), or the far (left) side behind the houses
            int water = CurrentSeaSide != 0 ? CurrentSeaSide : 1;
            var root = new GameObject("Passing_" + what).transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, z);
            bool ok = true;
            Vector3 W(float x, float y, float dz) => new Vector3(water * x, y, dz);
            Quaternion FacingRoad(float side) => Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            switch (what)
            {
                case Pass.KizKulesi:
                    ok = GlbModelLoader.TryInstantiateEModel("Landmark_KizKulesi", root, W(26f, -0.4f, 0f), FacingRoad(water), 14f, out _);
                    break;
                case Pass.Vapur:
                    ok = GlbModelLoader.TryInstantiateEModel("Çevre_Vapur", root, W(17f, -0.3f, 0f), Quaternion.Euler(0f, 0f, 0f), 7f, out _);
                    break;
                case Pass.KarakoyIskele:
                    ok = GlbModelLoader.TryInstantiateEModel("Landmark_KarakoyIskele", root, W(13f, 0.1f, 0f), FacingRoad(water), 8f, out _);
                    break;
                case Pass.SaatKulesi:   // Dolmabahçe clock tower, between the road and the water
                    ok = GlbModelLoader.TryInstantiateEModel("Landmark_SaatKulesi", root, W(12f, 0.16f, 0f), FacingRoad(water), 13f, out _);
                    break;
                case Pass.OrtakoyCamii:
                    ok = GlbModelLoader.TryInstantiateEModel("Landmark_OrtakoyCamii", root, W(16f, 0.2f, 0f), FacingRoad(water), 15f, out _);
                    break;
                case Pass.BogazKoprusu:   // the Bosphorus Bridge spanning the water beyond Ortaköy
                    ok = GlbModelLoader.TryInstantiateEModel("Çevre_Köprü", root, W(46f, -0.3f, 20f), Quaternion.identity, 26f, out _);   // ~80 m long, reaching out from this shore
                    break;
                case Pass.HalicKoprusu:
                    ok = GlbModelLoader.TryInstantiateEModel("Çevre_Köprü", root, W(21f, -0.3f, 10f), Quaternion.identity, 9f, out _);   // ~28 m across the water from the shore
                    break;
                case Pass.Suleymaniye:   // on the hill above the bazaar roofs
                    ok = GlbModelLoader.TryInstantiateEModel("Çevre_Süleymaniye", root, new Vector3(-24f, 3f, 10f), Quaternion.Euler(0f, 90f, 0f), 26f, out _);
                    break;
                case Pass.GalataKulesiFar:
                    ok = GlbModelLoader.TryInstantiateEModel("Çevre_GalataKulesi", root, new Vector3(-24f, 4f, 15f), Quaternion.Euler(0f, 90f, 0f), 30f, out _);
                    break;
                case Pass.GalataKulesiNear:   // right above the hill street
                    ok = GlbModelLoader.TryInstantiateEModel("Çevre_GalataKulesi", root, new Vector3(-16f, 2f, 10f), Quaternion.Euler(0f, 90f, 0f), 34f, out _);
                    break;
                case Pass.Ciragan:
                    BuildCiraganFacade(root, W(14f, 0.16f, 0f), water);
                    break;
                case Pass.Windmill:
                    BuildWindmill(root, new Vector3(-17f, 0.16f, 0f));
                    break;
            }
            if (!ok) { Destroy(root.gameObject); return null; }
            return root;
        }

        /// <summary>Çırağan Palace: long white waterfront facade with paired columns and a central pediment.</summary>
        void BuildCiraganFacade(Transform root, Vector3 pos, int side)
        {
            if (GlbModelLoader.TryInstantiateEModel("Landmark_Ciragan", root, pos + new Vector3(side * 3f, 0f, 0f), Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f), 12f, out var palace))
            {
                palace.transform.localScale *= 1.6f;   // a long palace front rather than the model's compact block
                palace.transform.localScale = new Vector3(palace.transform.localScale.x * 1.8f, palace.transform.localScale.y, palace.transform.localScale.z);
                return;
            }
            var p = new GameObject("CiraganPalace").transform;
            p.SetParent(root, false);
            p.localPosition = pos;
            const float len = 40f;
            MakeBox(p, "Plinth", new Vector3(0f, 0.4f, 0f), new Vector3(7f, 0.8f, len), _limestoneMat);
            MakeBox(p, "Body", new Vector3(0f, 4.2f, 0f), new Vector3(6f, 6.8f, len), _whitePlasterMat);
            MakeBox(p, "Cornice", new Vector3(0f, 7.8f, 0f), new Vector3(6.4f, 0.4f, len + 0.4f), _limestoneMat);
            MakeBox(p, "Pediment", new Vector3(0f, 8.8f, 0f), new Vector3(6.2f, 1.6f, 9f), _whitePlasterMat);
            float face = -side * 3.05f;   // the facade looking at the road
            for (float z = -18f; z <= 18f; z += 3f)
            {
                MakeCylinder(p, "Column", new Vector3(face, 4.2f, z - 0.35f), new Vector3(0.35f, 3.4f, 0.35f), _whitePlasterMat);
                MakeCylinder(p, "Column", new Vector3(face, 4.2f, z + 0.35f), new Vector3(0.35f, 3.4f, 0.35f), _whitePlasterMat);
                MakeBox(p, "Window", new Vector3(face, 5.5f, z + 1.5f), new Vector3(0.05f, 2.0f, 0.9f), _darkGlassMat);
                MakeBox(p, "Window", new Vector3(face, 2.5f, z + 1.5f), new Vector3(0.05f, 1.8f, 0.9f), _darkGlassMat);
            }
            CombineByMaterial(p);
        }

        void BuildWindmill(Transform root, Vector3 pos)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_YelDeğirmeni", root, pos, Quaternion.Euler(0f, 90f, 0f), 13f, out _)) return;
            var m = new GameObject("Windmill").transform;
            m.SetParent(root, false);
            m.localPosition = pos;
            m.localScale = Vector3.one * 2.6f;
            MakeCylinder(m, "MillBody", new Vector3(0f, 1.3f, 0f), new Vector3(2.4f, 2.6f, 2.4f), _whitePlasterMat);
            MakeSphere(m, "MillCap", new Vector3(0f, 4.0f, 0f), new Vector3(2.6f, 1.9f, 2.6f), _terracottaRoofMat);
            var rotor = new GameObject("MillSails").transform;
            rotor.SetParent(m, false);
            rotor.localPosition = new Vector3(1.4f, 3.8f, 0f);
            rotor.localRotation = Quaternion.Euler(0f, 90f, 0f);
            for (int b = 0; b < 4; b++)
            {
                var arm = new GameObject("SailArm").transform;
                arm.SetParent(rotor, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, b * 90f);
                MakeBox(arm, "Sail", new Vector3(0f, 1.25f, 0f), new Vector3(0.45f, 2.4f, 0.06f), _benchWoodMat);
            }
            _windmillRotors.Add(rotor);
        }

        // ---------------------------------------------------------------- District street types (TourRoute)

        const float BridgeFootway = 1.9f;
        Material _hillCobbleMatCache, _bridgeSteelMat;
        Material[] _spiceMats;

        /// <summary>Darker, rougher setts for the Galata hill streets.</summary>
        Material _hillCobbleMat
        {
            get
            {
                if (_hillCobbleMatCache == null)
                {
                    _hillCobbleMatCache = new Material(_sidewalkCobbleMat) { name = "HillCobble" };
                    var c = new Color(0.62f, 0.58f, 0.55f);
                    _hillCobbleMatCache.color = c;
                    if (_hillCobbleMatCache.HasProperty("_BaseColor")) _hillCobbleMatCache.SetColor("_BaseColor", c);
                }
                return _hillCobbleMatCache;
            }
        }

        /// <summary>
        /// Buildings on the far (left) pavement too, for the inland districts: the bazaar of Eminönü, the Galata hill,
        /// the streets of Yeldeğirmeni. Built as the curb side mirrored, so the far row faces the road.
        /// </summary>
        void BuildFarSideStreet(Transform root, TourRoadType road, int variant, float L)
        {
            var far = new GameObject("FarSide").transform;
            far.SetParent(root, false);
            far.localScale = new Vector3(-1f, 1f, 1f);
            BuildThreeBuildingTownhouseRow(far, 12f, variant * 2 + 7);
            BuildThreeBuildingTownhouseRow(far, 36f, variant * 2 + 8);
            BuildBackdropTerraces(far, (variant + 3) % StreetVariants, L);
            BuildTerracottaAmphoraPot(far, new Vector3(4.55f, 0.16f, 30f));
            BuildSwanNeckStreetlamp(far, new Vector3(4.15f, 0.16f, 40f));

            switch (road)
            {
                case TourRoadType.Bazaar:
                    // Spice and produce stalls under awnings along both pavements
                    for (int i = 0; i < 3; i++) BuildMarketStall(far, new Vector3(6.3f, 0.16f, 6f + i * 16f), variant + i);
                    BuildMarketStall(root, new Vector3(6.6f, 0.16f, 20f), variant + 5);
                    break;
                case TourRoadType.Hill:
                    // Stepped side alleys climbing away from the street
                    BuildSideStoneStaircase(far, new Vector3(6.2f, 0.16f, 24f));
                    BuildTree(far, new Vector3(6.4f, 0.16f, 8f), 1);
                    break;
                case TourRoadType.Neighbourhood:
                    // Leafy residential street: a tree every 12 m on the far pavement (not the curb side, where the
                    // chase and stop cameras pass right under the canopies)
                    for (float z = 6f; z < L; z += 12f)
                        BuildTree(far, new Vector3(5.4f, 0.16f, z), (variant + (int)z) % 3);
                    break;
            }
        }

        /// <summary>Market stall against the facades: wooden counter, heaped spice / produce trays, striped awning.</summary>
        void BuildMarketStall(Transform parent, Vector3 pos, int seed)
        {
            if (ReferenceCityAssets.TryCreate("MarketDisplay", parent, pos, Quaternion.Euler(0f, -90f, 0f), .9f, out _)) return;
            if (_spiceMats == null)
            {
                Color[] spice = { new Color(0.78f, 0.22f, 0.10f), new Color(0.93f, 0.62f, 0.12f), new Color(0.85f, 0.75f, 0.20f),
                                  new Color(0.45f, 0.30f, 0.16f), new Color(0.40f, 0.55f, 0.18f), new Color(0.60f, 0.12f, 0.16f) };
                _spiceMats = new Material[spice.Length];
                for (int i = 0; i < spice.Length; i++) _spiceMats[i] = GlbModelLoader.MakeMat(spice[i], 0.1f);
            }
            var stall = new GameObject("MarketStall").transform;
            stall.SetParent(parent, false);
            stall.localPosition = pos;
            MakeBox(stall, "Counter", new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.9f, 3.2f), _benchWoodMat);
            for (int i = 0; i < 6; i++)
            {
                float z = -1.25f + i * 0.5f;
                MakeBox(stall, "Tray", new Vector3(-0.15f, 0.93f, z), new Vector3(0.62f, 0.06f, 0.44f), _benchWoodMat);
                MakeSphere(stall, "Heap", new Vector3(-0.15f, 0.98f, z), new Vector3(0.5f, 0.2f, 0.36f), _spiceMats[(seed + i) % _spiceMats.Length]);
            }
            foreach (float z in new[] { -1.5f, 1.5f })
                MakeBox(stall, "AwningPole", new Vector3(-0.55f, 1.25f, z), new Vector3(0.05f, 2.5f, 0.05f), _ironMat);
            var awning = MakeBoxReturn(stall, "Awning", new Vector3(-0.1f, 2.45f, 0f), new Vector3(1.6f, 0.05f, 3.5f),
                seed % 2 == 0 ? _stripedAwningMat : _redAwningMat);
            awning.localRotation = Quaternion.Euler(0f, 0f, -12f);   // sloping down towards the street
        }

        /// <summary>
        /// Galata Bridge: water on both sides below the deck, railings along narrow footways, lamps, and anglers
        /// casting over the rail.
        /// </summary>
        void BuildBridgeSides(Transform root, int variant, float L)
        {
            if (_bridgeSteelMat == null) _bridgeSteelMat = GlbModelLoader.MakeMat(new Color(0.42f, 0.45f, 0.48f), 0.4f, 0.3f);
            const float waterY = BosphorusSeaState.BridgeLevel - .05f;
            float deckHalf = GameConfig.RoadHalfWidth + BridgeFootway + 0.15f;
            MakeBox(root, "BridgeDeck", new Vector3(0f, -1.55f, L * 0.5f), new Vector3(deckHalf * 2f, 2.9f, L), _bridgeSteelMat);
            float railX = LayoutRoadHalf + BridgeFootway - 0.12f;
            for (int s = -1; s <= 1; s += 2)
            {
                // each side in its own container (mirrored for the right), so PullSidesToRoad moves it as a whole
                var side = new GameObject(s < 0 ? "BridgeSideL" : "BridgeSideR").transform;
                side.SetParent(root, false);
                side.localScale = new Vector3(s, 1f, 1f);   // +x points away from the road on both sides
                if (!_usesReferenceHorizon)
                    BosphorusWaterMesh.Create(side, "Water", new Vector3(30f, waterY + 0.05f, L * 0.5f), new Vector2(50f, L), _seaMat, curveBounds: true);
                BuildCoastalPromenadeRailing(side, railX, L);
                BuildSwanNeckStreetlamp(side, new Vector3(LayoutRoadHalf + 0.3f, 0.16f, s < 0 ? 10f : 34f));
                var rng = new System.Random(311 + variant * 17 + s);
                for (int i = 0; i < 3; i++)
                    BuildAngler(side, new Vector3(railX - 0.5f, 0.16f, 4f + i * 15f + (float)rng.NextDouble() * 5f), i + variant);
            }
            if (!_usesReferenceHorizon && variant % 2 == 0)
                BuildBosphorusFerry(root, new Vector3(-22f, waterY + 0.2f, L * 0.5f), _ferryBlueMat);
        }

        /// <summary>Angler at the bridge rail (+x is the water side): coat, cap, rod over the rail, line to the water.</summary>
        void BuildAngler(Transform parent, Vector3 pos, int seed)
        {
            var a = new GameObject("Angler").transform;
            a.SetParent(parent, false);
            a.localPosition = pos;
            a.localRotation = Quaternion.Euler(0f, 90f, 0f);   // facing the water
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Balıkçı", a, Vector3.zero, Quaternion.Euler(0f, 90f + (seed % 2) * 20f, 0f), 1.75f, out _)) return;
            var coat = _pedestrianCoatMats[seed % _pedestrianCoatMats.Length];
            CreatePedestrianPart(a, PrimitiveType.Capsule, "Legs", new Vector3(0f, 0.45f, 0f), new Vector3(0.3f, 0.45f, 0.22f), _pedestrianTrousersMat);
            CreatePedestrianPart(a, PrimitiveType.Capsule, "Coat", new Vector3(0f, 1.1f, 0f), new Vector3(0.44f, 0.42f, 0.3f), coat);
            CreatePedestrianPart(a, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0.02f), new Vector3(0.24f, 0.27f, 0.24f), _pedestrianSkinMat);
            CreatePedestrianPart(a, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 1.76f, 0.02f), new Vector3(0.26f, 0.04f, 0.26f), coat);
            CreatePedestrianPart(a, PrimitiveType.Capsule, "Arms", new Vector3(0f, 1.18f, 0.22f), new Vector3(0.12f, 0.24f, 0.12f), coat, new Vector3(70f, 0f, 0f));
            // 3.2 m rod rising out over the rail, then the line straight down to the water
            CreatePedestrianPart(a, PrimitiveType.Cylinder, "Rod", new Vector3(0f, 1.95f, 1.6f), new Vector3(0.025f, 1.6f, 0.025f), _ironMat, new Vector3(62f, 0f, 0f));
            CreatePedestrianPart(a, PrimitiveType.Cylinder, "Line", new Vector3(0f, 0.15f, 3.0f), new Vector3(0.006f, 2.5f, 0.006f), _laneMat);
        }

        /// <summary>
        /// Road tile reference kerb, one side: a grey concrete gutter on the asphalt, a row of chunky beige cut-stone
        /// kerb blocks of uneven length (a few weathered grey ones) standing just proud of the pavement, a second row
        /// of smaller setts behind it, and weed tufts in some joints. Built in its own container, merged per material.
        /// </summary>
        void BuildStoneKerb(Transform parent, float side, int variant, float L)
        {
            var root = new GameObject(side > 0 ? "KerbR" : "KerbL").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(4409 + variant * 97 + (side > 0 ? 1 : 0));
            float edge = LayoutRoadHalf;

            MakeBox(root, "Gutter", new Vector3(side * (edge - 0.07f), 0.006f, L * 0.5f), new Vector3(0.14f, 0.012f, L), _gutterMat);

            void Row(float x0, float width, float height, float minLen, float maxLen, float greyChance, float tuftChance)
            {
                float z = (float)rng.NextDouble() * minLen;
                while (z < L - 0.05f)
                {
                    float len = Mathf.Min(minLen + (float)rng.NextDouble() * (maxLen - minLen), L - z);
                    float h = height + ((float)rng.NextDouble() - 0.5f) * 0.02f;
                    var mat = rng.NextDouble() < greyChance ? _kerbGreyMat : _kerbBeigeMat;
                    MakeBox(root, "Kerb", new Vector3(side * (x0 + width * 0.5f), h * 0.5f, z + len * 0.5f),
                        new Vector3(width - 0.012f, h, len - 0.014f), mat);
                    if (rng.NextDouble() < tuftChance)
                    {
                        float tx = side * (x0 + (float)rng.NextDouble() * width);
                        // a few thin blades fanning out of the joint
                        for (int k = 0; k < 4; k++)
                        {
                            float bh = 0.025f + (float)rng.NextDouble() * 0.03f;
                            var blade = MakeBoxReturn(root, "Weed", new Vector3(tx + ((float)rng.NextDouble() - 0.5f) * 0.04f, h - 0.005f + bh * 0.5f, z + len + ((float)rng.NextDouble() - 0.5f) * 0.02f),
                                new Vector3(0.006f, bh, 0.006f), _weedMat);
                            blade.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 50f, 0f, ((float)rng.NextDouble() - 0.5f) * 50f);
                        }
                    }
                    z += len;
                }
            }
            Row(edge, 0.30f, 0.21f, 0.38f, 0.85f, 0.14f, 0.18f);         // kerb
            Row(edge + 0.30f, 0.26f, 0.175f, 0.22f, 0.45f, 0.18f, 0.10f); // setts behind it
            CombineByMaterial(root);
        }

        // The street modules (sidewalks, facades, furniture, sea edge) are laid out around a road of this half
        // width; PullSidesToRoad then moves each side in to meet the real, narrower road (GameConfig).
        const float LayoutRoadHalf = 4f;
        public static float SideShift => LayoutRoadHalf - GameConfig.RoadHalfWidth;

        /// <summary>
        /// Moves every direct child that lies beside the road (judged by its rendered centre, so containers such
        /// as the townhouse rows and backdrop terraces move as a whole) towards the road by <see cref="SideShift"/>.
        /// Must run before CombineByMaterial on the same root.
        /// </summary>
        static void PullSidesToRoad(Transform root)
        {
            float shift = SideShift;
            if (Mathf.Abs(shift) < 0.001f) return;
            const float edge = LayoutRoadHalf - 0.6f;   // bollards stand at 3.92, puddles stay within the road
            foreach (Transform child in root)
            {
                float cx = child.localPosition.x;
                var rs = child.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    cx = root.InverseTransformPoint(b.center).x;
                }
                if (cx > edge) child.localPosition -= new Vector3(shift, 0f, 0f);
                else if (cx < -edge) child.localPosition += new Vector3(shift, 0f, 0f);
            }
        }

        void BuildWetPuddlePatches(Transform parent, int variant, float chunkLength)
        {
            if (_wetPuddleMat == null) return;
            var puddleRoot = new GameObject("RainPuddlePatches").transform;
            puddleRoot.SetParent(parent, false);
            // A fixed seed keeps each street module coherent when pooled, while different variants
            // retain distinct puddle placement. Patches are hidden outside rainy weather.
            var rng = new System.Random(8123 + variant * 7919);
            int count = 7;
            for (int p = 0; p < count; p++)
            {
                float x = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble()) * (GameConfig.RoadHalfWidth - 0.8f);
                float z = Mathf.Lerp(2.2f, chunkLength - 2.2f, (p + (float)rng.NextDouble() * 0.55f) / count);
                float radiusX = Mathf.Lerp(0.22f, 0.72f, (float)rng.NextDouble());
                float radiusZ = Mathf.Lerp(0.55f, 1.75f, (float)rng.NextDouble());
                var puddle = new GameObject("WetAsphaltPatch_" + p);
                puddle.transform.SetParent(puddleRoot, false);
                puddle.transform.localPosition = new Vector3(x, 0.008f, z);

                const int segments = 20;
                var vertices = new Vector3[segments + 1];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[segments * 3];
                vertices[0] = Vector3.zero;
                uv[0] = new Vector2(0.5f, 0.5f);
                for (int i = 0; i < segments; i++)
                {
                    float angle = (i / (float)segments) * Mathf.PI * 2f;
                    float roughness = Mathf.Lerp(0.86f, 1.12f, (float)rng.NextDouble());
                    float px = Mathf.Cos(angle) * radiusX * roughness;
                    float pz = Mathf.Sin(angle) * radiusZ * roughness;
                    vertices[i + 1] = new Vector3(px, 0f, pz);
                    uv[i + 1] = new Vector2(0.5f + px / (radiusX * 2f), 0.5f + pz / (radiusZ * 2f));
                    int tri = i * 3;
                    triangles[tri] = 0;
                    triangles[tri + 1] = (i + 1) % segments + 1;
                    triangles[tri + 2] = i + 1;
                }
                var mesh = new Mesh { name = "IrregularWetAsphaltPatch" };
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                TrackGeneratedMesh(mesh);
                var filter = puddle.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = puddle.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _wetPuddleMat;
                renderer.enabled = _roadWet;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.receiveShadows = false;
                _wetPuddleRenderers.Add(renderer);
            }
        }

        bool BuildReferenceTownhouseRow(Transform parent, float centerZ, int seed, System.Random random)
        {
            string districtAsset = "Districts/DistrictBlock_" + Mathf.Clamp(_district, 0, 7) + "_" + (seed % 3);
            bool districtAvailable = ReferenceCityAssets.Available(districtAsset);
            bool rowAvailable = districtAvailable || ReferenceCityAssets.Available("MansionRow");
            if (!rowAvailable && (!ReferenceCityAssets.Available("MansionCream") ||
                !ReferenceCityAssets.Available("MansionCoral") || !ReferenceCityAssets.Available("MansionBlue"))) return false;
            var row = new GameObject("Reference_DistrictShopfronts").transform;
            row.SetParent(parent, false);
            row.localPosition = new Vector3(9.4f, 0.16f, centerZ);
            // District chunks mirror across the road. Compensate along the row's street axis so
            // shop lettering, book spines and art remain readable from either pavement.
            row.localScale = new Vector3(1f, 1f, ShopfrontDepthSign(parent.localToWorldMatrix.determinant, StreetMirrorScale.x));
            bool combined = ReferenceCityAssets.TryCreate(districtAvailable ? districtAsset : "MansionRow", row,
                new Vector3(-1.95f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 1f, out _);
            for (int i = 0; !combined && i < 3; i++)
            {
                int style = (i + seed / 3) % 3;
                string asset = style == 0 ? "MansionCream" : style == 1 ? "MansionCoral" : "MansionBlue";
                float scale = 0.97f + (float)random.NextDouble() * 0.04f;
                ReferenceCityAssets.TryCreate(asset, row, new Vector3(-1.95f, 0f, (i - 1) * 6.28f),
                    Quaternion.Euler(0f, -90f, 0f), scale, out _);
            }
            ReferenceCityAssets.TryCreate("CafeSet", row, new Vector3(-3.30f, 0f, -3.2f),
                Quaternion.Euler(0f, 90f, 0f), 0.90f, out _);
            if ((seed & 1) == 0)
                ReferenceCityAssets.TryCreate("PlanterOlive", row, new Vector3(-3.00f, 0f, 4.0f),
                    Quaternion.identity, 1f, out _);
            if ((_district == 4 || _district == 5) && (seed & 1) != 0) BuildDistrictArtExhibit(row);
            return true;
        }

        static float ShopfrontDepthSign(float parentDeterminant, float chunkXScale)
            => parentDeterminant * chunkXScale < 0f ? -1f : 1f;

        void BuildDistrictArtExhibit(Transform row)
        {
            int index = _district == 4 ? 0 : 1;
            var material = _districtMuralMaterials[index];
            if (material == null)
            {
                material = GlbModelLoader.MakeMat(Color.white, 0.12f);
                material.name = index == 0 ? "GalataMusicStreetArt" : "YeldegirmeniSeaStreetArt";
                material.mainTexture = Resources.Load<Texture2D>(index == 0
                    ? "ReferenceKit/Districts/Surfaces/MuralGalata" : "ReferenceKit/Districts/Surfaces/MuralYeldegirmeni");
                _districtMuralMaterials[index] = material;
            }
            const float z = 4.2f;
            MakeBox(row, "StreetExhibitionOakFrame", new Vector3(-3.20f, 1.01f, z), new Vector3(0.08f, 1.24f, 1.24f), _pergolaWoodMat);
            for (int side = -1; side <= 1; side += 2)
                MakeBox(row, "StreetExhibitionFoot", new Vector3(-3.20f, 0.22f, z + side * 0.43f), new Vector3(0.16f, 0.44f, 0.12f), _pergolaWoodMat);
            var mesh = new Mesh { name = "OriginalDistrictStreetMural" };
            mesh.vertices = new[] { new Vector3(-3.25f, .46f, z-.55f), new Vector3(-3.25f, .46f, z+.55f),
                new Vector3(-3.25f, 1.56f, z+.55f), new Vector3(-3.25f, 1.56f, z-.55f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            TrackGeneratedMesh(mesh);
            var art = new GameObject("DistrictStreetArtExhibit", typeof(MeshFilter), typeof(MeshRenderer));
            art.transform.SetParent(row, false);
            art.GetComponent<MeshFilter>().sharedMesh = mesh;
            art.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------------- 3-Building Attached Townhouse Row (03-city.png Top Scene)

        void BuildThreeBuildingTownhouseRow(Transform parent, float centerZ, int seed)
        {
            var rng = new System.Random(9001 + seed * 31);
            if (BuildReferenceTownhouseRow(parent, centerZ, seed, rng)) return;
            // Every third row uses the hand-built procedural facades (striped cafe, coral house, Iznik-tile house)
            // instead of the three GLB houses, so the street doesn't repeat the same trio every 24 m.
            bool proceduralRow = seed % 3 == 2;
            var row = new GameObject("TownhouseRow_03City");
            row.transform.SetParent(parent, false);
            row.transform.localPosition = new Vector3(9.4f, 0.16f, centerZ);

            // ---- Stone Staircase on the left side of Building 1 (with potted plants & balustrade)
            BuildSideStoneStaircase(row.transform, new Vector3(-1.4f, 0f, -9.8f));

            // First try the 3 dedicated 3D Istanbul Townhouse GLBs from Resources/EModel:
            // 1) Çevre_Bina_Kafe_Ev (Cafe House, z = -5.8)
            // 2) Çevre_Bina_Pempe_Ev (Pink House, z = +0.4)
            // 3) Çevre_Bina_Mavi_Ev  (Blue House, z = +6.2)
            // GLB rows: the three house models in a seeded order with slightly different heights.
            string[] models = { "Çevre_Bina_Kafe_Ev", "Çevre_Bina_Pempe_Ev", "Çevre_Bina_Mavi_Ev" };
            for (int i = 2; i > 0; i--) { int j = rng.Next(i + 1); (models[i], models[j]) = (models[j], models[i]); }
            float[] slotZ = { -5.8f, 0.4f, 6.2f };
            bool hasB1 = false, hasB2 = false, hasB3 = false;
            GameObject b1Go = null, b2Go = null, b3Go = null;
            if (!proceduralRow)
            {
                hasB1 = GlbModelLoader.TryInstantiateEModel(models[0], row.transform, new Vector3(-0.2f, 0f, slotZ[0]), Quaternion.Euler(0f, -90f, 0f), 7.3f + (float)rng.NextDouble() * 0.7f, out b1Go);
                hasB2 = GlbModelLoader.TryInstantiateEModel(models[1], row.transform, new Vector3(-0.1f, 0f, slotZ[1]), Quaternion.Euler(0f, -90f, 0f), 7.3f + (float)rng.NextDouble() * 0.7f, out b2Go);
                hasB3 = GlbModelLoader.TryInstantiateEModel(models[2], row.transform, new Vector3(-0.15f, 0f, slotZ[2]), Quaternion.Euler(0f, -90f, 0f), 7.3f + (float)rng.NextDouble() * 0.7f, out b3Go);
            }

            if (hasB1 && hasB2 && hasB3)
            {
                b1Go.name = "B1_" + models[0];
                b2Go.name = "B2_" + models[1];
                b3Go.name = "B3_" + models[2];
                AddNightWindows(b1Go, models[0], rng);
                AddNightWindows(b2Go, models[1], rng);
                AddNightWindows(b3Go, models[2], rng);
                foreach (var h in new[] { b1Go, b2Go, b3Go }) VaryHouse(h, rng);
                // a thin wisp of chimney steam from one roof in about every other row
                if (rng.NextDouble() < 0.5)
                {
                    var house = new[] { b1Go, b2Go, b3Go }[rng.Next(3)];
                    var rs = house.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                        AmbientFx.CreateChimneySteam(row.transform, row.transform.InverseTransformPoint(new Vector3(b.center.x + 0.6f, b.max.y - 0.3f, b.center.z + 1.2f)));
                    }
                }
                for (int i = 0; i < models.Length; i++)
                {
                    if (models[i] != "Çevre_Bina_Pempe_Ev") continue;
                    BuildWallBougainvillea(row.transform, new Vector3(-2.9f, 0f, slotZ[i]));
                    break;
                }
                BuildStreetLife(row.transform, slotZ, rng);
                return;
            }

            // ================================================================
            // BUILDING 1 (Left, z = -6.2): Cream Plaster & Limestone Quoins,
            // Blue-Shuttered Arched Windows, Flower Balcony, Blue-and-White Striped Cafe Awning, Bistro Table
            // ================================================================
            var b1 = new GameObject("B1_CreamStripedCafe");
            b1.transform.SetParent(row.transform, false);
            b1.transform.localPosition = new Vector3(0f, 0f, -5.8f);

            MakeBox(b1.transform, "FacadeBody", new Vector3(0f, 3.6f, 0f), new Vector3(5.2f, 7.2f, 6.8f), _creamPlasterMat);
            // Limestone Corner Quoins
            for (int q = 0; q < 7; q++)
            {
                float qy = 0.55f + q * 1.0f;
                float qz = (q % 2 == 0) ? 0.55f : 0.38f;
                MakeBox(b1.transform, "QuoinL_" + q, new Vector3(-2.55f, qy, -3.2f), new Vector3(0.24f, 0.44f, qz), _limestoneMat);
                MakeBox(b1.transform, "QuoinR_" + q, new Vector3(-2.55f, qy, 3.2f), new Vector3(0.24f, 0.44f, qz), _limestoneMat);
            }
            // Sloped Terracotta Barrel-Tile Roof + Chimney
            MakeBox(b1.transform, "Cornice", new Vector3(-0.1f, 7.25f, 0f), new Vector3(5.6f, 0.28f, 7.0f), _limestoneMat);
            var roof1 = MakeBoxReturn(b1.transform, "TerracottaRoof", new Vector3(-0.2f, 7.85f, 0f), new Vector3(5.8f, 0.95f, 7.1f), _terracottaRoofMat);
            roof1.localRotation = Quaternion.Euler(0f, 0f, 8f);
            MakeBox(b1.transform, "Chimney", new Vector3(0.5f, 8.6f, 2.4f), new Vector3(0.65f, 1.1f, 0.65f), _creamPlasterMat);

            // Upper Floor: Left Shuttered Window with Wrought-Iron Flower Balcony + Right Arched Window
            BuildBlueShutteredWindow(b1.transform, new Vector3(-2.62f, 4.95f, -1.15f), true);
            BuildArchedWindowWithKeystone(b1.transform, new Vector3(-2.62f, 4.95f, 1.65f));

            // Ground Floor: Warm glowing Blue-Panelled Cafe Shopfront + Blue-and-White Striped Awning + Bistro Table
            BuildShopfront(b1.transform, new Vector3(-2.58f, 1.65f, 0.2f), 4.6f);
            BuildStripedBlueWhiteAwning(b1.transform, new Vector3(-3.35f, 3.15f, 0.2f), 5.0f);
            BuildBistroTableAndChairs(b1.transform, new Vector3(-3.55f, 0f, -0.8f));
            // Climbing bougainvillea uses the generated, textured wall-vine model instead of pink sphere clusters.
            BuildWallBougainvillea(b1.transform, new Vector3(-2.9f, 0f, -2.9f));
            BuildTerracottaAmphoraPot(b1.transform, new Vector3(-3.05f, 0f, -2.7f));
            BuildTerracottaOlivePot(b1.transform, new Vector3(-3.05f, 0f, 2.6f));

            // ================================================================
            // BUILDING 2 (Middle, z = 0.4): Warm Terracotta-Coral Plaster,
            // Arched Teal-Green Door, Solid Red Awning, Trailing Ivy, Rooftop Wooden Pergola
            // ================================================================
            var b2 = new GameObject("B2_CoralTerracottaMiddle");
            b2.transform.SetParent(row.transform, false);
            b2.transform.localPosition = new Vector3(0.15f, 0f, 0.4f);

            MakeBox(b2.transform, "CoralBody", new Vector3(0f, 3.9f, 0f), new Vector3(5.0f, 7.8f, 5.6f), _coralPlasterMat);
            MakeBox(b2.transform, "Cornice", new Vector3(-0.1f, 7.85f, 0f), new Vector3(5.4f, 0.26f, 5.8f), _limestoneMat);

            // Rooftop Wooden Pergola + Wrought-Iron Terrace Railing + Rooftop Cypress
            BuildRooftopWoodenPergola(b2.transform, new Vector3(-0.4f, 7.95f, 0f));

            // Upper Floor: Two Arched Windows in Cream Stone Surrounds + Wrought-Iron Balconies + Trailing Vines
            BuildArchedWindowWithBalcony(b2.transform, new Vector3(-2.52f, 5.15f, -1.25f));
            BuildArchedWindowWithBalcony(b2.transform, new Vector3(-2.52f, 5.15f, 1.35f));
            MakeSphere(b2.transform, "TrailingVineL", new Vector3(-2.62f, 6.8f, -1.8f), new Vector3(0.45f, 1.8f, 0.65f), _bougainvilleaVineMat);
            MakeSphere(b2.transform, "TrailingVineR", new Vector3(-2.62f, 6.7f, 0.8f), new Vector3(0.45f, 1.9f, 0.65f), _oliveLeafMat);

            // Ground Floor Left: Arched Teal-Green Panelled Wooden Door in Limestone Arch Surround
            BuildArchedTealDoor(b2.transform, new Vector3(-2.52f, 1.55f, -1.35f));
            // Ground Floor Right: Solid Terracotta-Red Awning over Blue-Framed Shop Window
            BuildShopfront(b2.transform, new Vector3(-2.52f, 1.60f, 1.25f), 2.5f);
            BuildSolidAwning(b2.transform, new Vector3(-3.20f, 3.08f, 1.25f), 2.7f, _redAwningMat);
            BuildTerracottaOlivePot(b2.transform, new Vector3(-2.95f, 0f, -0.1f));

            // ================================================================
            // BUILDING 3 (Right, z = +6.2): Cream-White Stone with Blue-and-White
            // Ceramic Tile Frieze Panels, Solid Blue Awning, Arched Shopfront & Stone Roof Balustrade
            // ================================================================
            var b3 = new GameObject("B3_IznikFriezeRight");
            b3.transform.SetParent(row.transform, false);
            b3.transform.localPosition = new Vector3(0.05f, 0f, 6.2f);

            MakeBox(b3.transform, "WhiteStoneBody", new Vector3(0f, 3.75f, 0f), new Vector3(5.1f, 7.5f, 6.0f), _whitePlasterMat);
            MakeBox(b3.transform, "Cornice", new Vector3(-0.1f, 7.55f, 0f), new Vector3(5.5f, 0.28f, 6.2f), _limestoneMat);
            // Rooftop Carved Stone Balustrade + Flower Boxes
            MakeBox(b3.transform, "RoofBalustrade", new Vector3(-2.45f, 8.0f, 0f), new Vector3(0.24f, 0.65f, 5.8f), _limestoneMat);
            MakeSphere(b3.transform, "RoofFlowers", new Vector3(-2.45f, 8.4f, 0f), new Vector3(0.45f, 0.35f, 4.2f), _pinkBlossomMat);

            // Blue-and-White Iznik Ceramic Tile Frieze Panels on Upper Facade
            MakeBox(b3.transform, "IznikFriezePanelL", new Vector3(-2.56f, 6.35f, -1.65f), new Vector3(0.08f, 1.25f, 1.85f), _iznikFriezeMat);
            MakeBox(b3.transform, "IznikFriezePanelR", new Vector3(-2.56f, 6.35f, 1.65f), new Vector3(0.08f, 1.25f, 1.85f), _iznikFriezeMat);

            // Central Upper Arched Window with Wrought-Iron Balcony
            BuildArchedWindowWithBalcony(b3.transform, new Vector3(-2.56f, 5.05f, 0f));

            // Ground Floor: Solid Mediterranean-Blue Awning over Arched Blue-Framed Shopfront
            BuildArchedShopfront(b3.transform, new Vector3(-2.55f, 1.65f, 0f), 4.0f);
            BuildSolidAwning(b3.transform, new Vector3(-3.28f, 3.12f, 0f), 4.3f, _navyCanvasMat);
            BuildTerracottaOlivePot(b3.transform, new Vector3(-3.0f, 0f, -2.2f));
            BuildStonePlanterBox(b3.transform, new Vector3(-3.0f, 0f, 2.2f), true);

            // Procedural facades are hundreds of primitives: merge per material for mobile.
            CombineByMaterial(row.transform);
        }

        void BuildWallBougainvillea(Transform parent, Vector3 localPosition)
        {
            if (GlbModelLoader.TryInstantiateEModel(
                "Çevre_Begonvil_Duvar", parent, localPosition, Quaternion.Euler(0f, -90f, 0f), 2.15f, out var vine))
            {
                vine.name = "Hyper_ClimbingBougainvillea";
            }
        }

        // ---------------------------------------------------------------- Architectural Sub-Elements (03-city.png Middle Row)

        void BuildSideStoneStaircase(Transform parent, Vector3 pos)
        {
            var stairs = new GameObject("StoneStaircase");
            stairs.transform.SetParent(parent, false);
            stairs.transform.localPosition = pos;

            for (int s = 0; s < 8; s++)
            {
                MakeBox(stairs.transform, "Step_" + s, new Vector3(s * 0.42f, 0.20f + s * 0.34f, 0f), new Vector3(0.55f, 0.40f + s * 0.34f, 2.2f), _limestoneMat);
            }
            // Stepped stone balustrade wall + terracotta pots on the steps
            MakeBox(stairs.transform, "StairWall", new Vector3(1.2f, 1.4f, -1.15f), new Vector3(3.6f, 2.6f, 0.32f), _limestoneMat);
            BuildTerracottaAmphoraPot(stairs.transform, new Vector3(-0.3f, 0f, -1.35f));
            BuildTerracottaOlivePot(stairs.transform, new Vector3(0.8f, 0.9f, -1.15f));
        }

        void BuildBlueShutteredWindow(Transform parent, Vector3 pos, bool withFlowerBalcony)
        {
            var w = new GameObject("BlueShutteredWindow");
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;

            MakeBox(w.transform, "StoneFrame", Vector3.zero, new Vector3(0.16f, 1.95f, 1.35f), _limestoneMat);
            MakeBox(w.transform, "WarmGlass", new Vector3(-0.05f, 0f, 0f), new Vector3(0.10f, 1.65f, 0.95f), _warmShopGlassMat);
            // Left & right Mediterranean blue louvered shutters
            MakeBox(w.transform, "ShutterL", new Vector3(-0.08f, 0f, -0.68f), new Vector3(0.08f, 1.68f, 0.42f), _blueShutterMat);
            MakeBox(w.transform, "ShutterR", new Vector3(-0.08f, 0f, 0.68f), new Vector3(0.08f, 1.68f, 0.42f), _blueShutterMat);

            if (withFlowerBalcony)
            {
                MakeBox(w.transform, "BalconySlab", new Vector3(-0.28f, -1.02f, 0f), new Vector3(0.55f, 0.14f, 1.75f), _limestoneMat);
                MakeBox(w.transform, "IronRailing", new Vector3(-0.48f, -0.62f, 0f), new Vector3(0.06f, 0.68f, 1.68f), _ironMat);
                MakeSphere(w.transform, "BalconyFlowers", new Vector3(-0.42f, -0.55f, 0f), new Vector3(0.38f, 0.38f, 1.45f), _pinkBlossomMat);
            }
        }

        void BuildArchedWindowWithKeystone(Transform parent, Vector3 pos)
        {
            var w = new GameObject("ArchedWindow");
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;

            MakeBox(w.transform, "StoneSurround", Vector3.zero, new Vector3(0.16f, 1.85f, 1.25f), _limestoneMat);
            MakeCylinder(w.transform, "ArchTop", new Vector3(0f, 0.92f, 0f), new Vector3(0.16f, 0.08f, 1.25f), _limestoneMat);
            MakeBox(w.transform, "Glass", new Vector3(-0.05f, 0f, 0f), new Vector3(0.10f, 1.55f, 0.88f), _warmShopGlassMat);
            MakeBox(w.transform, "WindowFrame", new Vector3(-0.08f, 0f, 0f), new Vector3(0.08f, 1.58f, 0.10f), _blueShutterMat);
        }

        void BuildArchedWindowWithBalcony(Transform parent, Vector3 pos)
        {
            BuildArchedWindowWithKeystone(parent, pos);
            var bal = new GameObject("WroughtIronBalcony");
            bal.transform.SetParent(parent, false);
            bal.transform.localPosition = pos;
            MakeBox(bal.transform, "Slab", new Vector3(-0.26f, -0.98f, 0f), new Vector3(0.52f, 0.14f, 1.48f), _limestoneMat);
            MakeBox(bal.transform, "Railing", new Vector3(-0.45f, -0.60f, 0f), new Vector3(0.06f, 0.64f, 1.42f), _ironMat);
            MakeSphere(bal.transform, "FlowerBox", new Vector3(-0.42f, -0.52f, 0f), new Vector3(0.36f, 0.34f, 1.15f), _bougainvilleaVineMat);
        }

        void BuildArchedTealDoor(Transform parent, Vector3 pos)
        {
            var d = new GameObject("ArchedTealDoor");
            d.transform.SetParent(parent, false);
            d.transform.localPosition = pos;

            MakeBox(d.transform, "StoneArchSurround", Vector3.zero, new Vector3(0.22f, 3.0f, 1.65f), _limestoneMat);
            MakeBox(d.transform, "TealDoorPanel", new Vector3(-0.06f, -0.1f, 0f), new Vector3(0.14f, 2.65f, 1.18f), _tealDoorMat);
            MakeSphere(d.transform, "BrassKnocker", new Vector3(-0.15f, 0.05f, 0f), new Vector3(0.14f, 0.14f, 0.14f), _stopYellowMat);
            // Flanking warm wall lanterns
            MakeSphere(d.transform, "WallLanternL", new Vector3(-0.22f, 0.65f, -0.92f), new Vector3(0.22f, 0.28f, 0.22f), _lampGlowMat);
            MakeSphere(d.transform, "WallLanternR", new Vector3(-0.22f, 0.65f, 0.92f), new Vector3(0.22f, 0.28f, 0.22f), _lampGlowMat);
        }

        void BuildShopfront(Transform parent, Vector3 pos, float widthZ)
        {
            var sf = new GameObject("BlueShopfront");
            sf.transform.SetParent(parent, false);
            sf.transform.localPosition = pos;

            float openingWidth = Mathf.Max(1.4f, widthZ - 0.42f);
            float sideZ = widthZ * 0.5f - 0.08f;

            // The wood frame surrounds a recessed, lit interior instead of hiding it behind a flat panel.
            MakeBox(sf.transform, "FrameLeft", new Vector3(-0.08f, 0.02f, -sideZ), new Vector3(0.20f, 2.82f, 0.16f), _blueShutterMat);
            MakeBox(sf.transform, "FrameRight", new Vector3(-0.08f, 0.02f, sideZ), new Vector3(0.20f, 2.82f, 0.16f), _blueShutterMat);
            MakeBox(sf.transform, "FrameHeader", new Vector3(-0.08f, 1.34f, 0f), new Vector3(0.20f, 0.18f, widthZ), _blueShutterMat);
            MakeBox(sf.transform, "LowerWainscot", new Vector3(-0.08f, -0.98f, 0f), new Vector3(0.20f, 0.70f, widthZ - 0.16f), _blueShutterMat);
            MakeBox(sf.transform, "StoneSill", new Vector3(-0.13f, -1.35f, 0f), new Vector3(0.34f, 0.16f, widthZ + 0.10f), _limestoneMat);

            MakeBox(sf.transform, "RecessedInterior", new Vector3(0.045f, 0.20f, 0f), new Vector3(0.07f, 1.88f, openingWidth), _darkGlassMat);
            MakeBox(sf.transform, "WarmDisplayBack", new Vector3(-0.005f, 0.27f, 0f), new Vector3(0.04f, 1.52f, openingWidth - 0.12f), _warmShopGlassMat);
            MakeBox(sf.transform, "DisplayShelfLower", new Vector3(-0.035f, -0.42f, 0f), new Vector3(0.12f, 0.07f, openingWidth - 0.16f), _benchWoodMat);
            MakeBox(sf.transform, "DisplayShelfUpper", new Vector3(-0.035f, 0.38f, 0f), new Vector3(0.12f, 0.07f, openingWidth - 0.16f), _benchWoodMat);

            // Small ceramics and packages sit behind a pale glass pane, giving the facade a readable shop window at street distance.
            Material[] displayMats = { _stopYellowMat, _tealDoorMat, _redAwningMat, _whitePlasterMat };
            int itemCount = Mathf.Clamp(Mathf.FloorToInt(openingWidth / 0.62f), 2, 6);
            for (int i = 0; i < itemCount; i++)
            {
                float z = (i - (itemCount - 1) * 0.5f) * (openingWidth / (itemCount + 0.4f));
                var displayMat = displayMats[i % displayMats.Length];
                float lowerHeight = 0.30f + (i % 2) * 0.08f;
                MakeBox(sf.transform, "DisplayPackageLower_" + i, new Vector3(-0.07f, -0.17f + lowerHeight * 0.5f, z), new Vector3(0.10f, lowerHeight, 0.23f), displayMat);
                if (i % 2 == 0)
                    MakeCylinder(sf.transform, "CeramicVaseUpper_" + i, new Vector3(-0.065f, 0.62f, z), new Vector3(0.10f, 0.22f, 0.10f), displayMat);
            }

            MakeBox(sf.transform, "GlassPane", new Vector3(-0.145f, 0.20f, 0f), new Vector3(0.025f, 1.95f, openingWidth), _shopWindowGlassMat);
        }

        void BuildArchedShopfront(Transform parent, Vector3 pos, float widthZ)
        {
            BuildShopfront(parent, pos, widthZ);
            var arch = new GameObject("ShopArchHeader");
            arch.transform.SetParent(parent, false);
            arch.transform.localPosition = pos + new Vector3(0f, 1.48f, 0f);
            MakeBox(arch.transform, "StoneArchHeader", Vector3.zero, new Vector3(0.22f, 0.45f, widthZ + 0.25f), _limestoneMat);
        }

        void BuildStripedBlueWhiteAwning(Transform parent, Vector3 pos, float widthZ)
        {
            var aw = MakeBoxReturn(parent, "BlueWhiteStripedAwning", pos, new Vector3(1.55f, 0.16f, widthZ), _stripedAwningMat);
            aw.localRotation = Quaternion.Euler(0f, 0f, 16f);
        }

        void BuildSolidAwning(Transform parent, Vector3 pos, float widthZ, Material mat)
        {
            var aw = MakeBoxReturn(parent, "SolidAwning", pos, new Vector3(1.42f, 0.16f, widthZ), mat);
            aw.localRotation = Quaternion.Euler(0f, 0f, 16f);
        }

        void BuildRooftopWoodenPergola(Transform parent, Vector3 pos)
        {
            var p = new GameObject("RooftopWoodenPergola");
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;

            MakeBox(p.transform, "TerraceRailing", new Vector3(-1.85f, 0.38f, 0f), new Vector3(0.08f, 0.72f, 5.2f), _ironMat);
            MakeBox(p.transform, "PostFL", new Vector3(-1.6f, 0.95f, -1.8f), new Vector3(0.16f, 1.9f, 0.16f), _pergolaWoodMat);
            MakeBox(p.transform, "PostFR", new Vector3(-1.6f, 0.95f, 1.8f), new Vector3(0.16f, 1.9f, 0.16f), _pergolaWoodMat);
            MakeBox(p.transform, "PostBL", new Vector3(0.8f, 0.95f, -1.8f), new Vector3(0.16f, 1.9f, 0.16f), _pergolaWoodMat);
            MakeBox(p.transform, "PostBR", new Vector3(0.8f, 0.95f, 1.8f), new Vector3(0.16f, 1.9f, 0.16f), _pergolaWoodMat);
            MakeBox(p.transform, "PergolaRoof", new Vector3(-0.4f, 1.95f, 0f), new Vector3(2.8f, 0.22f, 4.2f), _terracottaRoofMat);
            MakeSphere(p.transform, "RoofShrub", new Vector3(-1.6f, 0.45f, -2.1f), new Vector3(0.75f, 1.45f, 0.75f), _cypressLeafMat);
        }

        // Facade palette of the reference board (bina cepheleri): cream, honey ochre, sand, rose, pale peach, white.
        // Applied as a multiplier on the whole textured model, so values stay near white to keep awnings and
        // shutters readable.
        static readonly Color[] HouseTints =
        {
            new Color(1.00f, 1.00f, 1.00f), new Color(1.02f, 0.84f, 0.60f), new Color(1.00f, 0.93f, 0.80f),
            new Color(1.02f, 0.74f, 0.68f), new Color(1.02f, 0.84f, 0.72f), new Color(1.08f, 1.08f, 1.06f),
            new Color(0.92f, 0.90f, 0.82f),
        };
        static MaterialPropertyBlock s_houseBlock;

        /// <summary>
        /// Makes each of the three house models look like a different building: a facade tint from the board's
        /// palette and, for half of them, a mirror along the street so the side stair, balcony and door swap ends.
        /// </summary>
        static void VaryHouse(GameObject house, System.Random rng)
        {
            if (rng.NextDouble() < 0.5)
            {
                // holder is turned -90 deg about Y, so its local X runs along the street
                var s = house.transform.localScale; s.x = -s.x; house.transform.localScale = s;
            }
            var tint = HouseTints[rng.Next(HouseTints.Length)];
            if (s_houseBlock == null) s_houseBlock = new MaterialPropertyBlock();
            foreach (var r in house.GetComponentsInChildren<Renderer>())
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                r.GetPropertyBlock(s_houseBlock);
                if (m.HasProperty("baseColorFactor")) s_houseBlock.SetColor("baseColorFactor", m.GetColor("baseColorFactor") * tint);
                if (m.HasProperty("_BaseColor")) s_houseBlock.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                r.SetPropertyBlock(s_houseBlock);
            }
        }

        // ---------------------------------------------------------------- Street life (reference: shops & cafes)

        Material _clothMat, _chalkMat, _chalkTextMat, _parasolRedMat, _parasolCreamMat, _flowerRedMat, _rattanMat;
        Material[] _signMats;

        void EnsureStreetLifeMaterials()
        {
            if (_clothMat != null) return;
            _clothMat = GlbModelLoader.MakeMat(new Color(0.95f, 0.93f, 0.88f), 0.2f);
            _chalkMat = GlbModelLoader.MakeMat(new Color(0.12f, 0.13f, 0.12f), 0.15f);
            _chalkTextMat = GlbModelLoader.MakeMat(new Color(0.92f, 0.90f, 0.82f), 0.1f);
            _parasolRedMat = GlbModelLoader.MakeMat(new Color(0.78f, 0.22f, 0.18f), 0.2f);
            _parasolCreamMat = GlbModelLoader.MakeMat(new Color(0.94f, 0.88f, 0.74f), 0.2f);
            _flowerRedMat = GlbModelLoader.MakeMat(new Color(0.86f, 0.14f, 0.22f), 0.25f);
            _rattanMat = GlbModelLoader.MakeMat(new Color(0.55f, 0.36f, 0.20f), 0.25f);
            _signMats = new[]
            {
                GlbModelLoader.MakeMat(new Color(0.12f, 0.24f, 0.42f), 0.3f), GlbModelLoader.MakeMat(new Color(0.12f, 0.38f, 0.30f), 0.3f),
                GlbModelLoader.MakeMat(new Color(0.62f, 0.16f, 0.14f), 0.3f), _pergolaWoodMat,
            };
        }

        /// <summary>
        /// Life in front of a row of three houses (row-local, facades at x = -2.6, pavement toward -x):
        /// a cafe terrace with parasol tables, a chalkboard menu, flower pots by the doors, and a hanging shop
        /// sign with a wall lantern on every house (lanterns glow in the evening with the street lamps).
        /// Merged per material.
        /// </summary>
        void BuildStreetLife(Transform row, float[] slotZ, System.Random rng)
        {
            EnsureStreetLifeMaterials();
            var root = new GameObject("StreetLife").transform;
            root.SetParent(row, false);

            int cafe = rng.Next(2) == 0 ? 0 : 2;
            float cz = slotZ[cafe];
            int tables = 2 + rng.Next(2);
            var parasol = rng.Next(2) == 0 ? _parasolRedMat : _parasolCreamMat;
            for (int t = 0; t < tables; t++)
                CafeTable(root, new Vector3(-3.75f, 0f, cz - 1.9f + t * 1.9f), parasol, rng);
            ChalkboardMenu(root, new Vector3(-4.45f, 0f, cz + (cafe == 0 ? 2.9f : -2.9f)));
            // low planter boxes framing the terrace
            FlowerPlanter(root, new Vector3(-4.55f, 0f, cz - 3.0f));
            FlowerPlanter(root, new Vector3(-4.55f, 0f, cz + 3.0f + (tables == 3 ? 0.9f : 0f)));

            for (int i = 0; i < slotZ.Length; i++)
            {
                float z = slotZ[i];
                HangingSign(root, new Vector3(-2.75f, 2.75f, z + (i == cafe ? -2.4f : 1.6f)), _signMats[rng.Next(_signMats.Length)]);
                WallLantern(root, new Vector3(-2.72f, 2.2f, z - 1.2f));
                if (i != cafe)
                {
                    PotCluster(root, new Vector3(-3.05f, 0f, z + 0.6f), rng);
                    if (rng.NextDouble() < 0.5) PotCluster(root, new Vector3(-3.05f, 0f, z - 2.4f), rng);
                }
            }
            CombineByMaterial(root);
        }

        void CafeTable(Transform parent, Vector3 pos, Material parasolMat, System.Random rng)
        {
            var t = new GameObject("CafeTable").transform; t.SetParent(parent, false); t.localPosition = pos;
            MakeCylinder(t, "Top", new Vector3(0f, 0.74f, 0f), new Vector3(0.7f, 0.025f, 0.7f), _clothMat);
            MakeCylinder(t, "Cloth", new Vector3(0f, 0.66f, 0f), new Vector3(0.74f, 0.08f, 0.74f), _clothMat);
            MakeCylinder(t, "Pedestal", new Vector3(0f, 0.35f, 0f), new Vector3(0.06f, 0.35f, 0.06f), _ironMat);
            MakeCylinder(t, "Foot", new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.02f, 0.4f), _ironMat);
            MakeSphere(t, "TablePosy", new Vector3(0.1f, 0.84f, 0.05f), new Vector3(0.12f, 0.14f, 0.12f), rng.NextDouble() < 0.5 ? _pinkBlossomMat : _flowerRedMat);
            foreach (float side in new[] { -1f, 1f })
            {
                // bistro chair facing the table: seat, back, two leg pairs
                var c = new GameObject("Chair").transform; c.SetParent(t, false);
                c.localPosition = new Vector3(0.12f * side, 0f, 0.62f * side);
                c.localRotation = Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f);
                MakeBox(c, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.42f, 0.05f, 0.40f), _rattanMat);
                MakeBox(c, "Back", new Vector3(0f, 0.72f, -0.19f), new Vector3(0.42f, 0.46f, 0.04f), _rattanMat);
                MakeBox(c, "LegsF", new Vector3(0f, 0.22f, 0.17f), new Vector3(0.38f, 0.44f, 0.03f), _ironMat);
                MakeBox(c, "LegsB", new Vector3(0f, 0.22f, -0.17f), new Vector3(0.38f, 0.44f, 0.03f), _ironMat);
            }
            // parasol over the table
            MakeCylinder(t, "ParasolPole", new Vector3(0f, 1.3f, 0f), new Vector3(0.04f, 1.3f, 0.04f), _benchWoodMat);
            MakeSphere(t, "ParasolCanopy", new Vector3(0f, 2.45f, 0f), new Vector3(2.0f, 0.45f, 2.0f), parasolMat);
        }

        void ChalkboardMenu(Transform parent, Vector3 pos)
        {
            var b = new GameObject("ChalkboardMenu").transform; b.SetParent(parent, false); b.localPosition = pos;
            foreach (float side in new[] { -1f, 1f })
            {
                var leaf = MakeBoxReturn(b, "Board", new Vector3(0.14f * side, 0.5f, 0f), new Vector3(0.04f, 0.95f, 0.55f), _benchWoodMat);
                leaf.localRotation = Quaternion.Euler(0f, 0f, 12f * side);
                var slate = MakeBoxReturn(b, "Slate", new Vector3(0.163f * side, 0.52f, 0f), new Vector3(0.02f, 0.78f, 0.45f), _chalkMat);
                slate.localRotation = leaf.localRotation;
                // chalk lines standing in for the handwritten menu
                for (int l = 0; l < 4; l++)
                {
                    var line = MakeBoxReturn(b, "ChalkLine", new Vector3(0.175f * side, 0.74f - l * 0.13f, 0.02f * (l % 2)),
                        new Vector3(0.01f, 0.025f, l == 0 ? 0.3f : 0.22f), _chalkTextMat);
                    line.localRotation = leaf.localRotation;
                }
            }
        }

        void FlowerPlanter(Transform parent, Vector3 pos)
        {
            MakeBox(parent, "PlanterBox", pos + new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.44f, 1.1f), _limestoneMat);
            for (int i = 0; i < 4; i++)
                MakeSphere(parent, "PlanterFlowers", pos + new Vector3(0f, 0.5f, -0.4f + i * 0.27f), new Vector3(0.34f, 0.26f, 0.32f),
                    i % 2 == 0 ? _pinkBlossomMat : _oliveLeafMat);
        }

        void PotCluster(Transform parent, Vector3 pos, System.Random rng)
        {
            int n = 2 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                float s = 0.7f + (float)rng.NextDouble() * 0.5f;
                var p = pos + new Vector3(-(float)rng.NextDouble() * 0.3f, 0f, i * 0.42f);
                MakeCylinder(parent, "Pot", p + new Vector3(0f, 0.17f * s, 0f), new Vector3(0.32f, 0.17f, 0.32f) * s, _terracottaPotMat);
                // leafy bush spilling over the rim, flowers dotted on top (a single coloured ball read as a mushroom)
                MakeSphere(parent, "PotPlant", p + new Vector3(0f, 0.40f * s, 0f), new Vector3(0.46f, 0.36f, 0.46f) * s, _cypressLeafMat);
                MakeSphere(parent, "PotPlant", p + new Vector3(0.06f, 0.52f * s, -0.05f), new Vector3(0.3f, 0.3f, 0.3f) * s, _oliveLeafMat);
                var bloom = rng.NextDouble() < 0.5 ? _pinkBlossomMat : _flowerRedMat;
                for (int f = 0; f < 4; f++)
                {
                    float a = f * 1.57f + (float)rng.NextDouble();
                    MakeSphere(parent, "PotBloom", p + new Vector3(Mathf.Cos(a) * 0.15f, 0.55f + (f % 2) * 0.08f, Mathf.Sin(a) * 0.15f) * s,
                        new Vector3(0.12f, 0.1f, 0.12f) * s, bloom);
                }
            }
        }

        void HangingSign(Transform parent, Vector3 pos, Material boardMat)
        {
            MakeBox(parent, "SignBracket", pos + new Vector3(-0.35f, 0.38f, 0f), new Vector3(0.7f, 0.04f, 0.04f), _ironMat);
            MakeBox(parent, "SignBoard", pos + new Vector3(-0.42f, 0.05f, 0f), new Vector3(0.05f, 0.5f, 0.62f), boardMat);
            MakeBox(parent, "SignFrame", pos + new Vector3(-0.42f, 0.05f, 0f), new Vector3(0.04f, 0.56f, 0.68f), _stopYellowMat);
        }

        void WallLantern(Transform parent, Vector3 pos)
        {
            MakeBox(parent, "LanternArm", pos + new Vector3(-0.12f, 0.12f, 0f), new Vector3(0.24f, 0.03f, 0.03f), _ironMat);
            MakeBox(parent, "LanternCage", pos + new Vector3(-0.24f, -0.02f, 0f), new Vector3(0.16f, 0.24f, 0.16f), _ironMat);
            MakeBox(parent, "LanternGlow", pos + new Vector3(-0.24f, -0.02f, 0f), new Vector3(0.17f, 0.2f, 0.12f), _lampGlowMat);
        }

        void BuildBistroTableAndChairs(Transform parent, Vector3 pos)
        {
            if (ReferenceCityAssets.TryCreate("CafeSet", parent, pos, Quaternion.Euler(0f, 90f, 0f), 1f, out _)) return;
            var b = new GameObject("CafeBistroTable");
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;

            MakeCylinder(b.transform, "TableTop", new Vector3(0f, 0.68f, 0f), new Vector3(0.72f, 0.04f, 0.72f), _limestoneMat);
            MakeCylinder(b.transform, "TablePedestal", new Vector3(0f, 0.34f, 0f), new Vector3(0.10f, 0.34f, 0.10f), _ironMat);
            MakeBox(b.transform, "Chair1", new Vector3(0f, 0.42f, -0.55f), new Vector3(0.42f, 0.46f, 0.42f), _benchWoodMat);
            MakeBox(b.transform, "Chair2", new Vector3(0f, 0.42f, 0.55f), new Vector3(0.42f, 0.46f, 0.42f), _benchWoodMat);
        }

        // ---------------------------------------------------------------- Exact Street Objects (03-city.png Middle Row)

        void BuildSwanNeckStreetlamp(Transform parent, Vector3 pos)
        {
            if (ReferenceCityAssets.TryCreate("StreetLamp", parent, pos,
                Quaternion.Euler(0f, pos.x >= 0f ? 180f : 0f, 0f), 1.04f, out var referenceLamp))
            {
                var referenceRenderers = referenceLamp.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < referenceRenderers.Length; i++)
                    if (referenceRenderers[i].sharedMaterial != null &&
                        referenceRenderers[i].sharedMaterial.name.Contains("RK_Glow"))
                        _lanternGlows.Add(referenceRenderers[i]);
                return;
            }
            Quaternion lampRot = Quaternion.Euler(0f, pos.x >= 0f ? -90f : 90f, 0f);
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Sokak_Lambası", parent, pos, lampRot, 4.1f, out var eLamp))
            {
                eLamp.name = "SwanNeckStreetlamp";
                // The model's lantern hangs 0.75 m out over the road at 2.45 m; a warm glow inside it lights up when
                // the lamps are on (and the nearest ones get a real light, see UpdateLampLights)
                var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glow.name = "LanternGlow";
                RemoveVisualCollider(glow);
                glow.transform.position = parent.TransformPoint(pos + new Vector3(pos.x >= 0f ? -0.75f : 0.75f, 2.45f, 0f));
                glow.transform.localScale = new Vector3(0.22f, 0.32f, 0.22f);   // inside the glass, not a blob over the frame
                glow.transform.SetParent(eLamp.transform, true);
                var gr = glow.GetComponent<Renderer>();
                gr.sharedMaterial = _lanternGlowMat;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gr.enabled = _lampsLit;
                _lanternGlows.Add(gr);
                return;
            }

            // Swan-neck cast-iron post with curved scroll bracket & hanging hexagonal warm lantern
            var lamp = new GameObject("SwanNeckStreetlamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = pos;

            MakeCylinder(lamp.transform, "FlutedBase", new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.42f, 0.28f), _ironMat);
            MakeCylinder(lamp.transform, "Pole", new Vector3(0f, 2.0f, 0f), new Vector3(0.13f, 1.85f, 0.13f), _ironMat);
            MakeBox(lamp.transform, "SwanNeckArm", new Vector3(-0.35f, 3.85f, 0f), new Vector3(0.78f, 0.10f, 0.10f), _ironMat);
            MakeCylinder(lamp.transform, "LanternCap", new Vector3(-0.68f, 3.70f, 0f), new Vector3(0.36f, 0.08f, 0.36f), _ironMat);
            MakeCylinder(lamp.transform, "HexLanternGlow", new Vector3(-0.68f, 3.48f, 0f), new Vector3(0.28f, 0.20f, 0.28f), _lampGlowMat);
        }

        void BuildSlattedBench(Transform parent, Vector3 pos, float yAngle = 0f)
        {
            if (ReferenceCityAssets.TryCreate("Bench", parent, pos, Quaternion.Euler(0f, yAngle, 0f), 1f, out _)) return;
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Bank", parent, pos, Quaternion.Euler(0f, yAngle, 0f), 0.95f, out var eBench))
            {
                eBench.name = "EModel_Cevre_Bank";
                return;
            }

            // Fallback: Slatted wooden park bench with black cast-iron armrests & legs
            var bench = new GameObject("SlattedWoodenBench");
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = pos;
            bench.transform.localRotation = Quaternion.Euler(0f, yAngle + 90f, 0f);

            MakeBox(bench.transform, "WoodSeat", new Vector3(0f, 0.42f, 0f), new Vector3(0.56f, 0.08f, 1.85f), _benchWoodMat);
            MakeBox(bench.transform, "WoodBackrest", new Vector3(0.24f, 0.74f, 0f), new Vector3(0.08f, 0.48f, 1.85f), _benchWoodMat);
            MakeBox(bench.transform, "IronLegL", new Vector3(0f, 0.32f, -0.82f), new Vector3(0.58f, 0.62f, 0.08f), _ironMat);
            MakeBox(bench.transform, "IronLegR", new Vector3(0f, 0.32f, 0.82f), new Vector3(0.58f, 0.62f, 0.08f), _ironMat);
        }

        void BuildRibbedGreenTrashBin(Transform parent, Vector3 pos)
        {
            if (ReferenceCityAssets.TryCreate("StreetBin", parent, pos, Quaternion.identity, 1f, out _)) return;
            if (ReferenceCityAssets.TryCreate("TrashCan", parent, pos, Quaternion.identity, 1f, out _)) return;
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Çöp_Kutusu", parent, pos, Quaternion.identity, 0.92f, out var eBin))
            {
                eBin.name = "RibbedGreenTrashBin";
                return;
            }

            // Dark-green ribbed cylindrical trash bin with domed lid from 03-city.png middle row
            var bin = new GameObject("RibbedGreenTrashBin");
            bin.transform.SetParent(parent, false);
            bin.transform.localPosition = pos;

            MakeCylinder(bin.transform, "RibbedBody", new Vector3(0f, 0.42f, 0f), new Vector3(0.44f, 0.42f, 0.44f), _trashGreenMat);
            MakeCylinder(bin.transform, "Rim", new Vector3(0f, 0.82f, 0f), new Vector3(0.48f, 0.05f, 0.48f), _trashGreenMat);
            MakeSphere(bin.transform, "DomedLid", new Vector3(0f, 0.86f, 0f), new Vector3(0.44f, 0.22f, 0.44f), _trashGreenMat);
        }

        void BuildCastIronBollard(Transform parent, Vector3 pos)
        {
            if (ReferenceCityAssets.TryCreate("Bollard", parent, pos, Quaternion.identity, 0.92f, out _)) return;
            var bollard = new GameObject("CastIronBollard");
            bollard.transform.SetParent(parent, false);
            bollard.transform.localPosition = pos;

            MakeCylinder(bollard.transform, "Base", new Vector3(0f, 0.08f, 0f), new Vector3(0.20f, 0.08f, 0.20f), _ironMat);
            MakeCylinder(bollard.transform, "Shaft", new Vector3(0f, 0.38f, 0f), new Vector3(0.12f, 0.34f, 0.12f), _ironMat);
            MakeSphere(bollard.transform, "TopCap", new Vector3(0f, 0.74f, 0f), new Vector3(0.15f, 0.15f, 0.15f), _ironMat);
        }

        void BuildTerracottaAmphoraPot(Transform parent, Vector3 pos)
        {
            if (ReferenceCityAssets.TryCreate("PlanterFlowers", parent, pos, Quaternion.identity, 0.90f, out _)) return;
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Begonvilli_Saksı", parent, pos, Quaternion.Euler(0f, -90f, 0f), 0.92f, out var ePot))
            {
                ePot.name = "TerracottaAmphoraPot";
                return;
            }

            var pot = new GameObject("TerracottaAmphoraPot");
            pot.transform.SetParent(parent, false);
            pot.transform.localPosition = pos;

            MakeSphere(pot.transform, "Belly", new Vector3(0f, 0.34f, 0f), new Vector3(0.58f, 0.56f, 0.58f), _terracottaPotMat);
            MakeCylinder(pot.transform, "Rim", new Vector3(0f, 0.60f, 0f), new Vector3(0.48f, 0.06f, 0.48f), _terracottaPotMat);
            MakeSphere(pot.transform, "GreenBush", new Vector3(0f, 0.86f, 0f), new Vector3(0.75f, 0.58f, 0.75f), _oliveLeafMat);
        }

        void BuildTerracottaOlivePot(Transform parent, Vector3 pos)
        {
            if (ReferenceCityAssets.TryCreate("PlanterOlive", parent, pos, Quaternion.identity, 0.90f, out _)) return;
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Begonvilli_Saksı", parent, pos, Quaternion.Euler(0f, 90f, 0f), 0.86f, out var ePot))
            {
                ePot.name = "TerracottaOlivePot";
                return;
            }

            var pot = new GameObject("TerracottaOlivePot");
            pot.transform.SetParent(parent, false);
            pot.transform.localPosition = pos;

            MakeCylinder(pot.transform, "Pot", new Vector3(0f, 0.25f, 0f), new Vector3(0.44f, 0.25f, 0.44f), _terracottaPotMat);
            MakeCylinder(pot.transform, "Stem", new Vector3(0f, 0.62f, 0f), new Vector3(0.08f, 0.22f, 0.08f), _oliveBarkMat);
            MakeSphere(pot.transform, "Crown", new Vector3(0f, 0.92f, 0f), new Vector3(0.65f, 0.55f, 0.65f), _oliveLeafMat);
        }

        void BuildStonePlanterBox(Transform parent, Vector3 pos, bool longBox)
        {
            if (ReferenceCityAssets.TryCreate("PlanterFlowers", parent, pos, Quaternion.identity, longBox ? 1.15f : .90f, out _)) return;
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Begonvilli_Saksı", parent, pos, Quaternion.Euler(0f, pos.x >= 0f ? -90f : 90f, 0f), longBox ? 1.12f : 0.98f, out var eBox))
            {
                eBox.name = "StonePlanterBox";
                return;
            }

            var box = new GameObject("StonePlanterBox");
            box.transform.SetParent(parent, false);
            box.transform.localPosition = pos;

            float len = longBox ? 1.85f : 1.25f;
            MakeBox(box.transform, "StoneTrough", new Vector3(0f, 0.24f, 0f), new Vector3(0.62f, 0.48f, len), _limestoneMat);
            MakeSphere(box.transform, "Foliage", new Vector3(0f, 0.56f, 0f), new Vector3(0.68f, 0.42f, len * 0.9f), _oliveLeafMat);
            MakeSphere(box.transform, "PinkFlowers", new Vector3(0f, 0.68f, 0.15f), new Vector3(0.55f, 0.36f, len * 0.65f), _pinkBlossomMat);
        }

        void BuildCoastalPromenadeRailing(Transform parent, float xPos, float lengthZ)
        {
            string railing = DistrictRailingResource(_district);
            if (!ReferenceCityAssets.Available(railing)) railing = "Railing";
            if (ReferenceCityAssets.Available(railing))
            {
                for (float z = 1.2f; z < lengthZ; z += 2.4f)
                    ReferenceCityAssets.TryCreate(railing, parent, new Vector3(xPos, 0.16f, z),
                        Quaternion.Euler(0f, 90f, 0f), 1f, out _);
                return;
            }
            var rail = new GameObject("CoastalPromenadeRailing");
            rail.transform.SetParent(parent, false);

            if (GlbModelLoader.LoadEModelPrefab("Çevre_Çit") != null)
            {
                for (float z = 4.0f; z < lengthZ; z += 8.0f)
                {
                    if (GlbModelLoader.TryInstantiateEModel("Çevre_Çit", rail.transform, new Vector3(xPos, 0.16f, z), Quaternion.Euler(0f, 90f, 0f), 1.05f, out var seg))
                    {
                        seg.transform.localScale = new Vector3(2.65f, 1.0f, 1.0f);
                    }
                }
                return;
            }

            MakeBox(rail.transform, "TopBar", new Vector3(xPos, 0.96f, lengthZ * 0.5f), new Vector3(0.08f, 0.06f, lengthZ), _ironMat);
            for (float z = 2f; z < lengthZ; z += 4f)
            {
                MakeBox(rail.transform, "Post", new Vector3(xPos, 0.54f, z), new Vector3(0.10f, 0.84f, 0.10f), _ironMat);
            }
        }

        // Shared 2.4 m native modules keep every quay joint aligned while districts retain
        // their own ironwork: delicate coast, bridge braces, working harbor, stone hill, arches.
        public static string DistrictRailingResource(int district)
        {
            switch (Mathf.Clamp(district, 0, 7))
            {
                case 2: return "RailingBridge";
                case 3: case 5: return "RailingKarakoy";
                case 4: return "RailingStone";
                case 6: return "RailingBridge";
                case 7: return "RailingOrtakoy";
                default: return "RailingSalacak";
            }
        }

        // ---------------------------------------------------------------- Exact 3 Tree Types (03-city.png Bottom-Left Row)

        void BuildTree(Transform parent, Vector3 pos, int treeType)
        {
            string referenceTree = treeType == 1 ? "Cypress" : treeType == 2 ? "FlowerTree" : "PlaneTree";
            float referenceScale = treeType == 0 ? 0.88f : treeType == 1 ? 0.84f : 0.92f;
            if (ReferenceCityAssets.TryCreate(referenceTree, parent, pos,
                Quaternion.Euler(0f, Mathf.Repeat(pos.z * 17f, 360f), 0f), referenceScale, out _)) return;
            if (treeType == 1)
            {
                if (GlbModelLoader.TryInstantiateEModel("Çevre_Selvi", parent, pos, Quaternion.identity, 5.2f, out var eCypress))
                {
                    eCypress.name = "Tree_1_Selvi";
                    return;
                }
            }
            else
            {
                Quaternion rot = Quaternion.Euler(0f, treeType == 0 ? -90f : 90f, 0f);
                float h = treeType == 0 ? 4.2f : 3.6f;
                if (GlbModelLoader.TryInstantiateEModel("Çevre_Zakkum_Ağacı", parent, pos, rot, h, out var eZakkum))
                {
                    eZakkum.name = "Tree_" + treeType + "_Zakkum";
                    return;
                }
            }

            var tree = new GameObject("Tree_" + treeType);
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = pos;

            if (treeType == 0)
            {
                // Tree 1 (Left in 03-city.png): Gnarled leaning Mediterranean Plane/Olive tree with olive-green canopy & base rocks
                var trunk1 = MakeCylinderReturn(tree.transform, "GnarledTrunkMain", new Vector3(0f, 1.35f, 0f), new Vector3(0.52f, 1.45f, 0.52f), _gnarledBarkMat);
                trunk1.localRotation = Quaternion.Euler(6f, 0f, -12f);
                var branchL = MakeCylinderReturn(tree.transform, "BranchL", new Vector3(-0.55f, 2.55f, -0.25f), new Vector3(0.28f, 0.85f, 0.28f), _gnarledBarkMat);
                branchL.localRotation = Quaternion.Euler(-14f, 0f, 28f);
                var branchR = MakeCylinderReturn(tree.transform, "BranchR", new Vector3(0.45f, 2.65f, 0.25f), new Vector3(0.26f, 0.85f, 0.26f), _gnarledBarkMat);
                branchR.localRotation = Quaternion.Euler(12f, 0f, -24f);

                // Lush multi-clustered olive-green canopy
                MakeSphere(tree.transform, "CanopyCenter", new Vector3(-0.15f, 3.65f, 0f), new Vector3(2.95f, 1.45f, 2.95f), _oliveLeafMat);
                MakeSphere(tree.transform, "CanopyLeft", new Vector3(-1.15f, 3.25f, -0.4f), new Vector3(2.10f, 1.15f, 2.10f), _oliveLeafMat);
                MakeSphere(tree.transform, "CanopyRight", new Vector3(0.85f, 3.40f, 0.45f), new Vector3(2.15f, 1.20f, 2.15f), _oliveLeafMat);
                // Base rocks & low shrubs
                MakeSphere(tree.transform, "BaseRock", new Vector3(0.38f, 0.16f, -0.35f), new Vector3(0.55f, 0.32f, 0.48f), _limestoneMat);
                MakeSphere(tree.transform, "BaseShrub", new Vector3(-0.42f, 0.24f, 0.28f), new Vector3(0.75f, 0.45f, 0.75f), _cypressLeafMat);
            }
            else if (treeType == 1)
            {
                // Tree 2 (Middle in 03-city.png): Tall Conical Dark-Green Italian Cypress spire with base stones
                MakeCylinder(tree.transform, "Trunk", new Vector3(0f, 0.55f, 0f), new Vector3(0.24f, 0.55f, 0.24f), _oliveBarkMat);
                MakeSphere(tree.transform, "CypressTier1", new Vector3(0f, 1.75f, 0f), new Vector3(1.18f, 1.65f, 1.18f), _cypressLeafMat);
                MakeSphere(tree.transform, "CypressTier2", new Vector3(0f, 2.95f, 0f), new Vector3(0.96f, 1.65f, 0.96f), _cypressLeafMat);
                MakeSphere(tree.transform, "CypressTier3", new Vector3(0f, 4.05f, 0f), new Vector3(0.68f, 1.45f, 0.68f), _cypressLeafMat);
                MakeSphere(tree.transform, "CypressPeak", new Vector3(0f, 4.95f, 0f), new Vector3(0.36f, 0.95f, 0.36f), _cypressLeafMat);
                MakeSphere(tree.transform, "BaseStone", new Vector3(0.28f, 0.14f, -0.25f), new Vector3(0.42f, 0.26f, 0.42f), _limestoneMat);
            }
            else
            {
                // Tree 3 (Right in 03-city.png): Multi-stemmed pink-blossoming Judas/Bougainvillea tree with fallen petals
                var stemL = MakeCylinderReturn(tree.transform, "StemL", new Vector3(-0.18f, 1.15f, 0f), new Vector3(0.18f, 1.15f, 0.18f), _oliveBarkMat);
                stemL.localRotation = Quaternion.Euler(0f, 0f, 12f);
                var stemR = MakeCylinderReturn(tree.transform, "StemR", new Vector3(0.18f, 1.15f, 0.08f), new Vector3(0.18f, 1.15f, 0.18f), _oliveBarkMat);
                stemR.localRotation = Quaternion.Euler(8f, 0f, -12f);

                MakeSphere(tree.transform, "PinkCrownMain", new Vector3(0f, 2.75f, 0f), new Vector3(2.25f, 1.55f, 2.25f), _pinkBlossomMat);
                MakeSphere(tree.transform, "PinkCrownL", new Vector3(-0.68f, 2.45f, -0.25f), new Vector3(1.45f, 1.15f, 1.45f), _bougainvilleaVineMat);
                MakeSphere(tree.transform, "PinkCrownR", new Vector3(0.68f, 2.55f, 0.25f), new Vector3(1.45f, 1.15f, 1.45f), _pinkBlossomMat);
                MakeSphere(tree.transform, "BaseFlowers", new Vector3(0f, 0.18f, 0f), new Vector3(0.95f, 0.32f, 0.95f), _oliveLeafMat);
            }
        }

        // ---------------------------------------------------------------- Primitive Helpers

        static Texture2D MakeSeaRippleTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "SeaRipples" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * Mathf.PI * 2f, v = y / (float)n * Mathf.PI * 2f;
                    float h = Mathf.Sin(v * 3f + Mathf.Sin(u * 2f) * 0.8f) * 0.5f + Mathf.Sin(v * 7f + u) * 0.25f;
                    float g = 0.9f + h * 0.12f;           // mostly the base colour, gentle light/dark bands
                    if (h > 0.62f) g = 1.12f;             // sparse bright glints
                    px[y * n + x] = new Color(g, g, g, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            MakeBoxReturn(parent, name, pos, scale, mat);
        }

        static Transform MakeBoxReturn(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveVisualCollider(go);
            ApplyMetricUVs(go, scale, mat);
            // Long pieces (road, pavements, sea, terraces) need vertices along their length, or the curved road
            // (CurveBender) would only bend them at their ends
            if (scale.z > 12f) SubdivideAlongZ(go.GetComponent<MeshFilter>(), Mathf.CeilToInt(scale.z / 6f));
            return go.transform;
        }

        static readonly Dictionary<(Mesh, int), Mesh> s_zSplitMeshes = new Dictionary<(Mesh, int), Mesh>();

        /// <summary>Cuts each side face of a box mesh that runs along local Z into <paramref name="n"/> strips
        /// (positions, normals, tangents and UVs interpolated, so textures stay continuous).</summary>
        static void SubdivideAlongZ(MeshFilter mf, int n)
        {
            var src = mf.sharedMesh;
            if (src == null || n < 2) return;
            if (s_zSplitMeshes.TryGetValue((src, n), out var cached)) { mf.sharedMesh = cached; return; }
            Vector3[] v = src.vertices, nm = src.normals; Vector4[] tg = src.tangents; Vector2[] uv = src.uv;
            int[] tris = src.triangles;
            var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<Vector4>(); var U = new List<Vector2>(); var I = new List<int>();
            // each face is a quad made of two consecutive triangles (Unity's cube does not keep a face's four vertices together)
            for (int q = 0; q + 5 < tris.Length; q += 6)
            {
                var quad = new List<int>();
                for (int k = 0; k < 6; k++) if (!quad.Contains(tris[q + k])) quad.Add(tris[q + k]);
                Vector3 normal = nm[tris[q]];
                if (Mathf.Abs(normal.z) > 0.5f || quad.Count != 4)
                {
                    // end caps stay as they are
                    for (int k = 0; k < 6; k++)
                    {
                        int i = tris[q + k];
                        I.Add(V.Count);
                        V.Add(v[i]); N.Add(nm[i]); T.Add(tg.Length > 0 ? tg[i] : Vector4.zero); U.Add(uv[i]);
                    }
                    continue;
                }
                // the two corners at -z and their partners at +z
                var near = new List<int>(); var far = new List<int>();
                foreach (int i in quad) (v[i].z < 0f ? near : far).Add(i);
                if (near.Count != 2 || far.Count != 2) continue;
                int Partner(int i) { foreach (int j in far) if (Mathf.Abs(v[j].x - v[i].x) < 1e-4f && Mathf.Abs(v[j].y - v[i].y) < 1e-4f) return j; return far[0]; }
                int a0 = near[0], a1 = near[1], b0 = Partner(a0), b1 = Partner(a1);
                int baseIdx = V.Count;
                for (int s = 0; s <= n; s++)
                {
                    float t = (float)s / n;
                    foreach (var (p, e) in new[] { (a0, b0), (a1, b1) })
                    {
                        V.Add(Vector3.Lerp(v[p], v[e], t)); N.Add(nm[p]);
                        T.Add(tg.Length > 0 ? tg[p] : Vector4.zero); U.Add(Vector2.Lerp(uv[p], uv[e], t));
                    }
                }
                // winding: keep the face's outward normal
                bool flip = Vector3.Dot(Vector3.Cross(v[a1] - v[a0], v[b0] - v[a0]), normal) < 0f;
                for (int s = 0; s < n; s++)
                {
                    int r0 = baseIdx + s * 2, r1 = r0 + 2;   // (r0 = a0 side, r0 + 1 = a1 side)
                    if (!flip) { I.Add(r0); I.Add(r0 + 1); I.Add(r1); I.Add(r1); I.Add(r0 + 1); I.Add(r1 + 1); }
                    else { I.Add(r0); I.Add(r1); I.Add(r0 + 1); I.Add(r1); I.Add(r1 + 1); I.Add(r0 + 1); }
                }
            }
            var mesh = new Mesh { name = src.name + "_z" + n };
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetTangents(T); mesh.SetUVs(0, U); mesh.SetTriangles(I, 0);
            mesh.RecalculateBounds();
            s_zSplitMeshes[(src, n)] = mesh;
            mf.sharedMesh = mesh;
        }

        static void MakeSphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveVisualCollider(go);
            ApplyMetricUVs(go, scale, mat);
        }

        // ---------------------------------------------------------------- Photo textures (Resources/Textures)

        // Metres covered by one repeat of each photo texture. A primitive's own UVs stretch 0..1 over every face
        // whatever its size, so for these materials the UVs are re-projected in metres (box projection on the
        // scaled primitive): a 48 m road and a 0.3 m curb then show the texture at the same real size.
        static readonly Dictionary<Material, float> s_tileMetres = new Dictionary<Material, float>();
        static readonly Dictionary<(Mesh, Vector3, float), Mesh> s_metricMeshes = new Dictionary<(Mesh, Vector3, float), Mesh>();

        static void ApplyMetricUVs(GameObject go, Vector3 scale, Material mat)
        {
            if (mat == null || !s_tileMetres.TryGetValue(mat, out float tile)) return;
            var mf = go.GetComponent<MeshFilter>();
            var key = (mf.sharedMesh, scale, tile);
            if (!s_metricMeshes.TryGetValue(key, out var mesh))
            {
                mesh = Object.Instantiate(mf.sharedMesh);
                var v = mesh.vertices; var n = mesh.normals; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 p = Vector3.Scale(v[i], scale) / tile;
                    Vector3 a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                    uv[i] = a.x >= a.y && a.x >= a.z ? new Vector2(p.z, p.y)
                          : a.y >= a.z ? new Vector2(p.x, p.z)
                          : new Vector2(p.x, p.y);
                }
                mesh.uv = uv;
                mesh.RecalculateTangents();
                s_metricMeshes[key] = mesh;
            }
            mf.sharedMesh = mesh;
        }

        /// <summary>Puts a photo texture on <paramref name="mat"/>. The colour becomes a tint relative to
        /// <paramref name="neutral"/> (the colour the texture already shows), so existing palette colours and the
        /// district tints still shift the hue without darkening the photo.</summary>
        static bool UsePhoto(Material mat, string name, float tileMetres, Color? neutral = null)
        {
            var tex = Resources.Load<Texture2D>("Textures/" + name);
            if (tex == null || mat == null) return false;
            mat.mainTexture = tex;
            mat.SetTexture("_BaseMap", tex);
            mat.mainTextureScale = Vector2.one;
            if (neutral.HasValue) SetTint(mat, mat.color, neutral.Value);
            else SetTint(mat, Color.white, Color.white);
            s_tileMetres[mat] = tileMetres;
            return true;
        }

        static void SetTint(Material mat, Color wanted, Color neutral)
        {
            var c = new Color(Mathf.Clamp(wanted.r / Mathf.Max(0.05f, neutral.r), 0f, 1.25f),
                              Mathf.Clamp(wanted.g / Mathf.Max(0.05f, neutral.g), 0f, 1.25f),
                              Mathf.Clamp(wanted.b / Mathf.Max(0.05f, neutral.b), 0f, 1.25f), 1f);
            mat.color = c;
            mat.SetColor("_BaseColor", c);
        }

        void ApplyPhotoTextures()
        {
            // a touch lighter than the photo: in the street's late light the asphalt went almost black
            if (UsePhoto(_roadMat, "Asphalt", 3f)) SetTint(_roadMat, new Color(1.18f, 1.16f, 1.12f), Color.white);
            // Road tile reference: beige stone paving on the pavements (the dark cobble photo read as mud)
            if (UsePhoto(_sidewalkCobbleMat, "Limestone", 1.3f)) SetTint(_sidewalkCobbleMat, new Color(0.92f, 0.88f, 0.82f), Color.white);
            if (UsePhoto(_kerbBeigeMat, "Limestone", 0.9f)) SetTint(_kerbBeigeMat, new Color(1f, 0.97f, 0.9f), Color.white);
            if (UsePhoto(_kerbGreyMat, "Limestone", 0.9f)) SetTint(_kerbGreyMat, new Color(0.66f, 0.68f, 0.72f), Color.white);
            UsePhoto(_limestoneMat, "Limestone", 1.6f);
            if (UsePhoto(_curbStoneMat, "Limestone", 1.2f)) SetTint(_curbStoneMat, new Color(0.78f, 0.76f, 0.72f), Color.white);
            _photoPlaster = UsePhoto(_creamPlasterMat, "PlasterCream", 2.5f, CreamNeutral);
            UsePhoto(_coralPlasterMat, "PlasterCoral", 2.5f, CoralNeutral);
            UsePhoto(_whitePlasterMat, "PlasterCream", 2.5f, new Color(0.96f, 0.93f, 0.86f) * 1.02f);
            UsePhoto(_iznikFriezeMat, "Iznik", 0.6f);
            UsePhoto(_terracottaRoofMat, "RoofTile", 1.4f);
            UsePhoto(_benchWoodMat, "OakWood", 1f);
            UsePhoto(_pergolaWoodMat, "OakWood", 1f, new Color(0.62f, 0.38f, 0.21f));
            UsePhoto(_ironMat, "CastIron", 0.8f);
            UsePhoto(_oliveBarkMat, "Bark", 1f);
            UsePhoto(_gnarledBarkMat, "Bark", 1f, new Color(0.46f, 0.35f, 0.25f));
            UsePhoto(_stripedAwningMat, "AwningFabric", 1.2f);
            UsePhoto(_darkGlassMat, "WindowGlass", 1.5f, new Color(0.55f, 0.72f, 0.82f));
            ApplyReferenceSurface(_roadMat, "Asphalt", 2.8f, 0.38f, 0.32f);
            ApplyReferenceSurface(_sidewalkCobbleMat, "Cobble", 1.8f, 0.48f, 0.27f);
            ApplyReferenceSurface(_kerbBeigeMat, "Limestone", 0.9f, 0.32f, 0.22f);
            ApplyReferenceSurface(_kerbGreyMat, "Limestone", 0.9f, 0.32f, 0.22f);
            ApplyReferenceSurface(_curbStoneMat, "Limestone", 1.2f, 0.32f, 0.22f);
            // Keep the animated BosphorusWater shader if active; otherwise fall back to photo texture:
            if (_seaMat == null || !_seaMat.shader.name.Contains("Bosphorus Water"))
            {
                var waterTex = Resources.Load<Texture2D>("Textures/BosphorusWater");
                if (waterTex != null)
                {
                    var sea = GlbModelLoader.MakeMat(Color.white, 0.86f, 0.02f);
                    _seaMat = sea;
                    // Slightly deeper than the photo: the Bosphorus reads darker than a shallow turquoise bay
                    if (UsePhoto(_seaMat, "BosphorusWater", 7f)) SetTint(_seaMat, new Color(0.62f, 0.78f, 0.92f), Color.white);
                }
            }
        }

        static void ApplyReferenceSurface(Material material, string name, float repeatMetres,
            float normalStrength, float smoothness)
        {
            if (material == null) return;
            var color = Resources.Load<Texture2D>("ReferenceKit/Surfaces/" + name);
            var normal = Resources.Load<Texture2D>("ReferenceKit/Normals/" + name);
            if (color == null || normal == null) return;
            material.SetTexture("_BaseMap", color);
            material.mainTexture = color;
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", normalStrength);
            material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_NORMALMAP");
            s_tileMetres[material] = repeatMetres;
        }

        // Average colour each plaster photo already has; the district tints are expressed relative to these
        static readonly Color CreamNeutral = new Color(0.93f, 0.84f, 0.68f);
        static readonly Color CoralNeutral = new Color(0.90f, 0.53f, 0.40f);
        bool _photoPlaster;

        static void MakeCylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            MakeCylinderReturn(parent, name, pos, scale, mat);
        }

        static Transform MakeCylinderReturn(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            RemoveVisualCollider(go);
            ApplyMetricUVs(go, scale, mat);
            return go.transform;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using OddsCab.Puzzles;

namespace OddsCab.Dioramas
{
    /// <summary>
    /// 3D rotating-road route puzzle (ROTA BULMACASI, design pool F54). Replaces the old fixed diorama whose
    /// invisible full-screen button solved it on any tap.
    /// Tap a tile to turn it a quarter; when the roads join the taxi drives to the pin and the stop is solved.
    /// Losing condition: the stop timer (GameManager). Shared PBR miniatures retain clear touch affordances.
    /// </summary>
    public class RouteTilePuzzleController : MonoBehaviour
    {
        public const float TileSize = 0.56f;

        RouteTileLayout _layout;
        PuzzleData _puzzle;
        readonly List<Transform> _tileRoots = new List<Transform>();
        readonly List<Renderer> _roadRenderers = new List<Renderer>();
        readonly List<int> _roadOwner = new List<int>();
        float[] _shownAngle;
        Transform[] _roads;
        Transform _gridRoot;
        Transform _taxi;
        Transform _pin;
        bool _solved;
        bool _driving;
        float _driveT;
        List<Vector3> _drivePoints;

        Material _baseMat, _grassMat, _roadMat, _roadSolvedMat, _laneMat, _taxiMat, _pinMat, _rimMat, _treeMat, _seaMat;

        public bool IsDriving => _driving;

        public void BuildMaterials()
        {
            if (_baseMat != null) return;
            _baseMat = DioramaPresentation.Surface("Limestone", new Color(0.86f, 0.80f, 0.68f), 0.2f);
            _grassMat = GlbModelLoader.MakeMat(new Color(0.48f, 0.66f, 0.40f), 0.15f);
            _roadMat = DioramaPresentation.Surface("Asphalt", new Color(0.24f, 0.26f, 0.30f), 0.3f);
            _roadSolvedMat = GlbModelLoader.MakeMat(new Color(0.36f, 0.86f, 0.74f), 0.35f);
            _laneMat = GlbModelLoader.MakeMat(new Color(0.96f, 0.94f, 0.86f), 0.3f);
            _taxiMat = GlbModelLoader.MakeMat(new Color(0.98f, 0.76f, 0.16f), 0.5f);
            _pinMat = GlbModelLoader.MakeMat(new Color(1.0f, 0.80f, 0.20f), 0.6f, 0.2f);
            _rimMat = DioramaPresentation.Surface("WoodOak", new Color(0.62f, 0.56f, 0.48f), 0.2f);
            _treeMat = GlbModelLoader.MakeMat(new Color(0.30f, 0.52f, 0.30f), 0.1f);
            _seaMat = GlbModelLoader.MakeMat(new Color(0.16f, 0.50f, 0.70f), 0.8f);
            DioramaPresentation.AddLighting(transform, 4.5f);
        }

        public void LoadPuzzle(PuzzleData puzzle)
        {
            BuildMaterials();
            EnsurePhysicsRaycaster();
            _puzzle = puzzle;
            _solved = false;
            _driving = false;
            // A retry after a wrong answer or time-out gets a fresh scramble.
            int seed = puzzle.answerValue + SaveSystem.Data.totalWrong * 7919;
            _layout = RouteTileLayout.Generate(seed, puzzle.difficulty);
            Rebuild();
        }

        static void EnsurePhysicsRaycaster()
        {
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<PhysicsRaycaster>();
        }

        /// <summary>World-space centre of the grid (the camera frames this).</summary>
        public Vector3 GridCenterWorld => transform.position + new Vector3(0f, 0.1f, 0f);

        public float GridDepth => (_layout != null ? _layout.Height + 2 : 6) * TileSize;

        void Rebuild()
        {
            if (_gridRoot != null) Destroy(_gridRoot.gameObject);
            _tileRoots.Clear();
            _roadRenderers.Clear();
            _roadOwner.Clear();

            _gridRoot = new GameObject("RouteTileGrid").transform;
            _gridRoot.SetParent(transform, false);

            int w = _layout.Width, h = _layout.Height;
            _shownAngle = new float[w * h];
            _roads = new Transform[w * h];

            // Bosphorus-blue water all around so the board doesn't float in empty sky
            Box(_gridRoot, "Water", new Vector3(0f, -0.22f, 0f), new Vector3(w * TileSize + .7f, 0.1f, h * TileSize + 1.55f), _seaMat, false);
            DioramaPresentation.AddWater(_gridRoot,w*TileSize+.7f,h*TileSize+1.55f,-.155f);
            // Stone plinth under the grid
            Box(_gridRoot, "Plinth", new Vector3(0f, -0.06f, 0f), new Vector3(w * TileSize + 0.24f, 0.12f, h * TileSize + 0.24f), _rimMat, false);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = _layout.Index(x, y);
                    var tile = new GameObject($"Tile_{x}_{y}").transform;
                    tile.SetParent(_gridRoot, false);
                    tile.localPosition = CellLocal(x, y);
                    var kind = _layout.Kind[idx];

                    var baseGo = Box(tile, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(TileSize * 0.94f, 0.06f, TileSize * 0.94f),
                        kind == RouteTileKind.Empty ? _grassMat : _baseMat, true);

                    if (kind == RouteTileKind.Empty)
                    {
                        // a little tree so empty squares read as "no road here"
                        if (!ReferenceCityAssets.TryCreate("Cypress_LOD1", tile, new Vector3(0f,.065f,0f), Quaternion.identity, .055f, out var tree))
                        {
                            Cyl(tile, "Trunk", new Vector3(0f, 0.12f, 0f), new Vector3(0.05f, 0.08f, 0.05f), _baseMat);
                            Sphere(tile, "Crown", new Vector3(0f, 0.26f, 0f), new Vector3(0.22f, 0.22f, 0.22f), _treeMat);
                        }
                        Box(tile,"TreePlanter",new Vector3(0f,.085f,0f),new Vector3(.22f,.055f,.22f),_baseMat,false);
                    }
                    else
                    {
                        var road = new GameObject("Road").transform;
                        road.SetParent(tile, false);
                        _roads[idx] = road;
                        float rw = TileSize * 0.40f, rh = 0.02f, half = TileSize * 0.47f;
                        if (kind == RouteTileKind.Straight)
                        {
                            AddRoad(road, idx, new Vector3(0f, 0.07f, 0f), new Vector3(rw, rh, half * 2f));
                            Box(road, "Lane", new Vector3(0f, 0.082f, 0f), new Vector3(0.03f, 0.005f, half * 1.4f), _laneMat, false);
                        }
                        else
                        {
                            // Corner: arms towards North (+z) and East (+x) at rotation 0
                            AddRoad(road, idx, new Vector3(0f, 0.07f, half * 0.5f + rw * 0.25f), new Vector3(rw, rh, half + rw * 0.5f));
                            AddRoad(road, idx, new Vector3(half * 0.5f + rw * 0.25f, 0.07f, 0f), new Vector3(half + rw * 0.5f, rh, rw));
                        }
                        float angle = _layout.Rotation[idx] * 90f;
                        _shownAngle[idx] = angle;
                        road.localRotation = Quaternion.Euler(0f, angle, 0f);
                    }

                    var tap = baseGo.AddComponent<RouteTileTap>();
                    tap.Owner = this;
                    tap.Index = idx;
                    _tileRoots.Add(tile);
                }
            }

            // Entry stub + taxi below the start column, pin above the goal column
            Vector3 entry = CellLocal(_layout.StartX, -1);
            Box(_gridRoot, "EntryRoad", entry + new Vector3(0f, 0.04f, 0.1f), new Vector3(TileSize * 0.40f, 0.02f, TileSize * 0.8f), _roadMat, false);
            _taxi = BuildTaxi(_gridRoot, entry + new Vector3(0f, 0.08f, -0.05f));

            Vector3 exit = CellLocal(_layout.GoalX, h);
            Box(_gridRoot, "ExitPad", exit + new Vector3(0f, 0.02f, -0.05f), new Vector3(TileSize * 0.8f, 0.04f, TileSize * 0.8f), _baseMat, false);
            _pin = BuildPin(_gridRoot, exit + new Vector3(0f, 0.05f, -0.05f));
            BuildBorder(w, h);
            DioramaPresentation.SetLayer(_gridRoot);
        }

        void BuildBorder(int width, int height)
        {
            float x = width * TileSize * .5f + .11f;
            float z = height * TileSize * .5f + .11f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < height + 1; row++)
                    Box(_gridRoot,"QuayBlock",new Vector3(side*x,.015f,-z+row*TileSize),new Vector3(.16f,.17f,TileSize*.96f),_baseMat,false);
                for (int column = 0; column < width + 1; column++)
                {
                    if (column == _layout.StartX + 1 && side < 0 || column == _layout.GoalX + 1 && side > 0) continue;
                    Box(_gridRoot,"QuayBlock",new Vector3(-x+column*TileSize,.015f,side*z),new Vector3(TileSize*.96f,.17f,.16f),_baseMat,false);
                }
                ReferenceCityAssets.TryCreate("StreetLamp", _gridRoot, new Vector3(side*x,.1f,z), Quaternion.identity, .11f, out var lamp);
                ReferenceCityAssets.TryCreate("PlanterFlowers", _gridRoot, new Vector3(side*x,.10f,-z), Quaternion.identity, .20f, out var planter);
            }
        }

        Vector3 CellLocal(int x, int y)
        {
            float ox = -(_layout.Width - 1) * 0.5f * TileSize;
            float oz = -(_layout.Height - 1) * 0.5f * TileSize;
            return new Vector3(ox + x * TileSize, 0f, oz + y * TileSize);
        }

        void AddRoad(Transform parent, int owner, Vector3 pos, Vector3 scale)
        {
            var go = Box(parent, "RoadPiece", pos, scale, _roadMat, false);
            _roadRenderers.Add(go.GetComponent<Renderer>());
            _roadOwner.Add(owner);
        }

        public void OnTileTapped(int index)
        {
            if (_solved || _driving || _layout == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.PuzzleActive) return;
            if (_layout.Kind[index] == RouteTileKind.Empty) return;

            _layout.RotateTile(index);
            SynthAudio.Instance?.PlayClick();
            if (_layout.IsSolved()) BeginSolvedDrive();
        }

        /// <summary>Hint: turn one wrong tile on the solution path into place.</summary>
        public bool RevealOneTile()
        {
            if (_layout == null || _solved) return false;
            foreach (var idx in _layout.SolutionPath)
            {
                var k = _layout.Kind[idx];
                if (RouteTileLayout.Connections(k, _layout.Rotation[idx]) != RouteTileLayout.Connections(k, _layout.SolvedRotation[idx]))
                {
                    _layout.Rotation[idx] = _layout.SolvedRotation[idx];
                    if (_layout.IsSolved()) BeginSolvedDrive();
                    return true;
                }
            }
            return false;
        }

        void BeginSolvedDrive()
        {
            _solved = true;
            var route = _layout.TraceRoute();
            var routeSet = new HashSet<int>(route);
            for (int i = 0; i < _roadRenderers.Count; i++)
                if (routeSet.Contains(_roadOwner[i])) _roadRenderers[i].sharedMaterial = _roadSolvedMat;

            _drivePoints = new List<Vector3> { _taxi.localPosition };
            foreach (var idx in route)
                _drivePoints.Add(CellLocal(idx % _layout.Width, idx / _layout.Width) + new Vector3(0f, 0.08f, 0f));
            _drivePoints.Add(CellLocal(_layout.GoalX, _layout.Height) + new Vector3(0f, 0.08f, -0.05f));
            _driveT = 0f;
            _driving = true;
        }

        void Update()
        {
            if (_layout == null || _tileRoots.Count == 0) return;

            // Smooth quarter turns
            for (int i = 0; i < _tileRoots.Count; i++)
            {
                if (_layout.Kind[i] == RouteTileKind.Empty) continue;
                float target = _layout.Rotation[i] * 90f;
                float delta = Mathf.DeltaAngle(_shownAngle[i], target);
                if (Mathf.Abs(delta) > 0.1f)
                {
                    _shownAngle[i] += Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), 900f * Time.deltaTime);
                    var road = _roads[i];
                    if (road != null) road.localRotation = Quaternion.Euler(0f, _shownAngle[i], 0f);
                }
            }

            if (_pin != null)
                _pin.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);

            if (_driving && _drivePoints != null && _drivePoints.Count > 1)
            {
                _driveT = Mathf.MoveTowards(_driveT, 1f, Time.deltaTime / Mathf.Max(0.8f, 0.16f * _drivePoints.Count));
                float s = _driveT * (_drivePoints.Count - 1);
                int a = Mathf.Clamp(Mathf.FloorToInt(s), 0, _drivePoints.Count - 2);
                Vector3 p0 = _drivePoints[a], p1 = _drivePoints[a + 1];
                _taxi.localPosition = Vector3.Lerp(p0, p1, s - a);
                Vector3 dir = p1 - p0;
                if (dir.sqrMagnitude > 0.0001f) _taxi.localRotation = Quaternion.Slerp(_taxi.localRotation, Quaternion.LookRotation(dir), Time.deltaTime * 14f);
                if (_driveT >= 1f)
                {
                    _driving = false;
                    GameManager.Instance?.SubmitChoiceAnswer(_puzzle != null ? _puzzle.correct : 0);
                }
            }
        }

        // ---------------------------------------------------------------- primitives

        Transform BuildTaxi(Transform parent, Vector3 pos)
        {
            var t = new GameObject("MiniTaxi").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            var model = GlbModelLoader.InstantiateTaxi();
            if (model != null && model.GetComponentInChildren<Renderer>() != null)
            {
                model.transform.SetParent(t,false);
                model.transform.localPosition=Vector3.zero;
                model.transform.localRotation=Quaternion.identity;
                model.transform.localScale=Vector3.one*.11f;
                return t;
            }
            if (model != null) Destroy(model);
            Box(t, "Body", new Vector3(0f, 0.06f, 0f), new Vector3(0.18f, 0.08f, 0.30f), _taxiMat, false);
            Box(t, "Cabin", new Vector3(0f, 0.13f, -0.01f), new Vector3(0.15f, 0.07f, 0.16f), _taxiMat, false);
            Box(t, "Sign", new Vector3(0f, 0.18f, -0.01f), new Vector3(0.07f, 0.03f, 0.03f), _laneMat, false);
            return t;
        }

        Transform BuildPin(Transform parent, Vector3 pos)
        {
            var t = new GameObject("TargetPin").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            Cyl(t, "Ring", new Vector3(0f, 0.01f, 0f), new Vector3(0.36f, 0.01f, 0.36f), _roadSolvedMat);
            Cyl(t, "Stem", new Vector3(0f, 0.16f, 0f), new Vector3(0.04f, 0.14f, 0.04f), _pinMat);
            Sphere(t, "Head", new Vector3(0f, 0.36f, 0f), new Vector3(0.18f, 0.18f, 0.18f), _pinMat);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Destroy(go.GetComponent<Collider>());
            else
            {
                // Taller hit box so taps on the road surface or small trees still count
                var bc = go.GetComponent<BoxCollider>();
                bc.size = new Vector3(1f, 6f, 1f);
                bc.center = new Vector3(0f, 2.5f, 0f);
            }
            return go;
        }

        static void Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>());
        }

        static void Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>());
        }
    }

    /// <summary>Receives a tap on one tile through the EventSystem (works for touch and mouse).</summary>
    public class RouteTileTap : MonoBehaviour, IPointerClickHandler
    {
        public RouteTilePuzzleController Owner;
        public int Index;

        public void OnPointerClick(PointerEventData eventData) => Owner?.OnTileTapped(Index);
    }
}

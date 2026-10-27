using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab.Dioramas
{
    /// <summary>
    /// 3D Miniature Diorama #2 (from 04-puzzles.png middle-left):
    /// Stone arch bridge with wooden drawbridge deck, brass pulley towers, and counterweight pan.
    /// Player toggles weights to match the exact counterweight required to lower/balance the bridge.
    /// </summary>
    public class BridgeDioramaController : MonoBehaviour
    {
        Transform _bridgeDeck;
        Transform _weightPan;
        Transform _miniTaxi;
        int _selectedMask;
        PuzzleData _puzzle;
        float _targetDeckAngle = 28f;
        float _targetPanY = 1.45f;
        float _taxiHeight = 1.42f;
        LineRenderer _rope;
        bool _authoredStage;

        public int SelectedMask => _selectedMask;

        public int CurrentWeightSum
        {
            get
            {
                if (_puzzle?.ints == null) return 0;
                int sum = 0;
                for (int i = 0; i < _puzzle.ints.Length; i++)
                    if ((_selectedMask & (1 << i)) != 0) sum += _puzzle.ints[i];
                return sum;
            }
        }

        public void BuildGeometry()
        {
            if (DioramaPresentation.TryStage("BalanceBridgeStage", transform, out var stage))
            {
                _authoredStage = true;
                _bridgeDeck = DioramaPresentation.Find(stage.transform, "DeckPivot");
                _weightPan = DioramaPresentation.Find(stage.transform, "PanPivot");
                BuildMiniTaxi();
                BuildRope();
                DioramaPresentation.AddLighting(transform);
                return;
            }
            var stoneMat = GlbModelLoader.MakeMat(new Color(0.84f, 0.75f, 0.62f), 0.25f);
            var waterMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.56f, 0.74f), 0.85f);
            var woodMat = GlbModelLoader.MakeMat(new Color(0.50f, 0.32f, 0.18f), 0.3f);
            var brassMat = GlbModelLoader.MakeMat(new Color(0.88f, 0.68f, 0.22f), 0.75f, 0.55f);
            var taxiMat = GlbModelLoader.MakeMat(new Color(0.98f, 0.76f, 0.14f), 0.65f);

            // River bed & water (compact portrait footprint)
            MakePart("Water", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(3.5f, 0.24f, 1.8f), waterMat);
            // Left and Right stone abutments
            MakePart("PierLeft", PrimitiveType.Cube, new Vector3(-1.28f, 0.70f, 0f), new Vector3(0.95f, 1.15f, 1.5f), stoneMat);
            MakePart("PierRight", PrimitiveType.Cube, new Vector3(1.28f, 0.70f, 0f), new Vector3(0.95f, 1.15f, 1.5f), stoneMat);

            // Pulley towers
            MakePart("TowerLeft", PrimitiveType.Cylinder, new Vector3(-0.85f, 1.80f, -0.45f), new Vector3(0.15f, 0.80f, 0.15f), woodMat);
            MakePart("TowerRight", PrimitiveType.Cylinder, new Vector3(0.85f, 1.80f, -0.45f), new Vector3(0.15f, 0.80f, 0.15f), woodMat);
            MakePart("TopBeam", PrimitiveType.Cube, new Vector3(0f, 2.50f, -0.45f), new Vector3(1.95f, 0.11f, 0.11f), brassMat);

            // Hinged wooden bridge deck
            var pivot = new GameObject("BridgePivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = new Vector3(-0.80f, 1.24f, 0f);
            _bridgeDeck = pivot.transform;

            var deck = MakePart("WoodDeck", PrimitiveType.Cube, new Vector3(0.80f, 0f, 0f), new Vector3(1.60f, 0.14f, 1.05f), woodMat);
            deck.transform.SetParent(_bridgeDeck, false);

            // Brass counterweight cylinder
            var pan = MakePart("CounterweightPan", PrimitiveType.Cylinder, new Vector3(1.08f, 1.40f, -0.45f), new Vector3(0.42f, 0.28f, 0.42f), brassMat);
            _weightPan = pan.transform;

            // Mini taxi waiting on the left pier
            var taxi = MakePart("MiniTaxi", PrimitiveType.Cube, new Vector3(-1.28f, 1.42f, 0f), new Vector3(0.52f, 0.30f, 0.36f), taxiMat);
            _miniTaxi = taxi.transform;
            // A scaled copy of the real taxi rides the bridge instead of a yellow box.
            var model = GlbModelLoader.InstantiateTaxi();
            if (model != null && model.GetComponentInChildren<Renderer>() != null)
            {
                var holder = new GameObject("MiniTaxiModel").transform;
                holder.SetParent(transform, false);
                holder.localPosition = taxi.transform.localPosition + new Vector3(0f, -0.15f, 0f);
                model.transform.SetParent(holder, false);
                model.transform.localScale = Vector3.one * 0.30f;
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // taxi front (+Z) towards the far pier (+X)
                Destroy(taxi);
                _miniTaxi = holder;
                _taxiHeight = 1.27f;
            }
            BuildRope();
            DioramaPresentation.AddLighting(transform);
        }

        void BuildMiniTaxi()
        {
            var holder = new GameObject("MiniTaxiModel").transform;
            holder.SetParent(transform, false);
            holder.localPosition = new Vector3(-1.28f, 1.27f, 0f);
            var model = GlbModelLoader.InstantiateTaxi();
            if (model != null)
            {
                model.transform.SetParent(holder, false);
                model.transform.localScale = Vector3.one * .30f;
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            _miniTaxi = holder;
            _taxiHeight = 1.27f;
        }

        void BuildRope()
        {
            var child = new GameObject("MovingPulleyRope");
            child.transform.SetParent(transform, false);
            _rope = child.AddComponent<LineRenderer>();
            _rope.useWorldSpace = false;
            _rope.positionCount = 4;
            _rope.widthMultiplier = .025f;
            _rope.numCapVertices = 2;
            _rope.numCornerVertices = 2;
            _rope.sharedMaterial = DioramaPresentation.Surface("Rope", new Color(.53f,.37f,.16f), .35f);
        }

        public void LoadPuzzle(PuzzleData puzzle)
        {
            _puzzle = puzzle;
            _selectedMask = 0;
            _miniTaxi.localPosition = new Vector3(-1.28f, _taxiHeight, 0f);
            UpdateVisualTargets();
        }

        public void ToggleWeight(int index)
        {
            _selectedMask ^= (1 << index);
            SynthAudio.Instance?.PlayClick();
            UpdateVisualTargets();
        }

        public void ClearWeights()
        {
            _selectedMask = 0;
            SynthAudio.Instance?.PlayClick();
            UpdateVisualTargets();
        }

        public void ApplyHintMask(int correctMask)
        {
            // Turn on one weight that belongs to the solution and clear any wrong weight
            _selectedMask &= correctMask;
            for (int i = 0; i < 8; i++)
            {
                int bit = 1 << i;
                if ((correctMask & bit) != 0 && (_selectedMask & bit) == 0)
                {
                    _selectedMask |= bit;
                    break;
                }
            }
            UpdateVisualTargets();
        }

        void UpdateVisualTargets()
        {
            if (_puzzle == null) return;
            float ratio = Mathf.Clamp((float)CurrentWeightSum / Mathf.Max(1, _puzzle.answerValue), 0f, 1.5f);
            // At ratio == 1.0, deck is flat (0 deg). Underweight stays raised (+28 deg); overweight dips (-14 deg).
            _targetDeckAngle = Mathf.Clamp((1f - ratio) * 28f, -14f, 28f);
            _targetPanY = Mathf.Lerp(1.65f, 0.85f, ratio / 1.5f);
        }

        void Update()
        {
            if (_bridgeDeck != null)
            {
                // The imported frame mirrors X before the stage's 180-degree yaw.
                // Negate local roll so a raised deck still rises toward root +X.
                Quaternion targetRot = Quaternion.Euler(0f, 0f, _authoredStage ? -_targetDeckAngle : _targetDeckAngle);
                _bridgeDeck.localRotation = Quaternion.Slerp(_bridgeDeck.localRotation, targetRot, Time.deltaTime * 6f);
            }
            if (_weightPan != null)
            {
                Vector3 p = _weightPan.localPosition;
                p.y = Mathf.Lerp(p.y, _targetPanY, Time.deltaTime * 6f);
                _weightPan.localPosition = p;
            }
            if (_miniTaxi != null && Mathf.Abs(_targetDeckAngle) < 0.5f)
            {
                float y = _taxiHeight;
                _miniTaxi.localPosition = Vector3.MoveTowards(_miniTaxi.localPosition, new Vector3(1.22f, y, 0f), Time.deltaTime * 1.8f);
            }
            if (_rope != null && _bridgeDeck != null && _weightPan != null)
            {
                var deckAttachment = _authoredStage ? new Vector3(-1.5f,.1f,.44f) : new Vector3(1.5f,.1f,-.44f);
                _rope.SetPosition(0, transform.InverseTransformPoint(_bridgeDeck.TransformPoint(deckAttachment)));
                _rope.SetPosition(1, new Vector3(-.85f,2.49f,-.60f));
                _rope.SetPosition(2, new Vector3(1.08f,2.49f,-.45f));
                _rope.SetPosition(3, transform.InverseTransformPoint(_weightPan.position) + new Vector3(0f,.25f,0f));
            }
        }

        GameObject MakePart(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }

    /// <summary>
    /// 3D Isometric Istanbul Coastal Route Puzzle Diorama ("ROTA BULMACASI / TAKSİYİ HEDEFE GÖTÜR",
    /// matching media_1792765160263_5464a1fc.jpg):
    /// - Bottom-Left Start Curve: Miniature Yellow Taxi on a glowing mint-green concentric start ring
    /// - Left Branch: Blocked by a Red-and-White Striped Roadwork Barrier (🚧), Triangular Warning Sign (⚠),
    ///   2 Orange-and-White Traffic Cones & 2 Flashing Red Warning Beacon Lights leading to a Stone Archway Gate
    /// - Center-Right Correct Branch: Glowing Mint-Green Route Ribbon with animated white forward chevrons (>>>)
    ///   leading across an Arched Stone Bridge (with warm vintage streetlamps) to the Upper-Right Destination Plaza
    /// - Destination Target: Concentric Glowing Golden Target Rings + Giant 3D Golden Map-Pin (📍) in front of Galata Tower
    /// - Background Skyline: 4-Minaret Mosque (upper-left), Bosphorus Suspension Bridge + Ferry (upper-center),
    ///   Galata Tower (upper-right), and harbor motorboat + coastal Mediterranean houses.
    /// </summary>
    public class RouteDioramaController : MonoBehaviour
    {
        Transform _miniTaxi;
        Transform _goldenTargetPin;
        Transform _startRingOuter;
        Transform _targetRingOuter;
        readonly Transform[] _routeChevrons = new Transform[7];
        readonly Renderer[] _beaconRenderers = new Renderer[2];
        Transform _ferryCenter;

        bool _isDrivingRoute;
        bool _isDrivingBlocked;
        float _driveProgress;
        bool _completedCallbackFired;

        static readonly Vector3[] CorrectWaypoints =
        {
            new Vector3(-0.78f, 0.28f, -1.78f),
            new Vector3(-0.22f, 0.28f, -1.42f),
            new Vector3(0.28f, 0.28f, -1.02f),
            new Vector3(0.32f, 0.30f, -0.05f),
            new Vector3(0.76f, 0.36f, 0.72f),
            new Vector3(1.25f, 0.30f, 1.45f),
        };

        public bool IsAnimating => _isDrivingRoute || _isDrivingBlocked;
        public float DriveProgress => _driveProgress;

        public void BuildGeometry()
        {
            var waterMat = GlbModelLoader.MakeMat(new Color(0.09f, 0.42f, 0.64f), 0.88f, 0.15f);
            var stoneMat = GlbModelLoader.MakeMat(new Color(0.84f, 0.75f, 0.62f), 0.28f);
            var darkStoneMat = GlbModelLoader.MakeMat(new Color(0.58f, 0.50f, 0.42f), 0.25f);
            var asphaltMat = GlbModelLoader.MakeMat(new Color(0.22f, 0.25f, 0.30f), 0.35f);
            var laneWhiteMat = GlbModelLoader.MakeMat(new Color(0.92f, 0.94f, 0.96f), 0.3f);
            var roofTerraMat = GlbModelLoader.MakeMat(new Color(0.78f, 0.34f, 0.22f), 0.28f);
            var wallCreamMat = GlbModelLoader.MakeMat(new Color(0.94f, 0.86f, 0.72f), 0.25f);
            var foliageMat = GlbModelLoader.MakeMat(new Color(0.20f, 0.52f, 0.24f), 0.22f);
            var foliageLightMat = GlbModelLoader.MakeMat(new Color(0.32f, 0.64f, 0.28f), 0.22f);
            var trunkMat = GlbModelLoader.MakeMat(new Color(0.42f, 0.26f, 0.15f), 0.2f);
            var mintGlowMat = MakeEmissive(new Color(0.16f, 0.98f, 0.72f), 2.4f);
            var mintSoftMat = MakeEmissive(new Color(0.12f, 0.86f, 0.64f), 1.6f);
            var chevronWhiteMat = MakeEmissive(new Color(0.92f, 1.0f, 0.98f), 2.6f);
            var goldGlowMat = MakeEmissive(new Color(1.0f, 0.82f, 0.18f), 2.3f);
            var warmLampMat = MakeEmissive(new Color(1.0f, 0.86f, 0.42f), 2.5f);
            var redAlertMat = MakeEmissive(new Color(1.0f, 0.18f, 0.12f), 2.6f);
            var orangeConeMat = GlbModelLoader.MakeMat(new Color(0.96f, 0.38f, 0.10f), 0.4f);
            var ironMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.16f, 0.20f), 0.5f, 0.4f);

            // 1. Reflective Turquoise Bosphorus Water Basin
            MakePart("BosphorusWaterBasin", PrimitiveType.Cube, new Vector3(0f, -0.08f, 1.2f), new Vector3(11.5f, 0.16f, 12.0f), waterMat);

            // 2. Coastal Stone Quay Platforms & Harbor Cove
            // Main left/center peninsula land platform
            MakePart("LeftQuayPlatform", PrimitiveType.Cube, new Vector3(-0.95f, 0.10f, -0.35f), new Vector3(2.85f, 0.28f, 4.20f), stoneMat);
            // Center island fork platform
            MakePart("CenterForkPlatform", PrimitiveType.Cube, new Vector3(0.05f, 0.10f, -0.55f), new Vector3(1.55f, 0.28f, 2.55f), stoneMat);
            // Lower-right harbor peninsula platform (leaving a water cove at (0.48, -1.55) for the moored boat!)
            MakePart("RightHarborPlatform", PrimitiveType.Cube, new Vector3(1.25f, 0.10f, -1.45f), new Vector3(1.65f, 0.28f, 1.85f), stoneMat);
            // Upper-right Galata destination plaza platform across the water channel
            MakePart("GalataPlazaPlatform", PrimitiveType.Cube, new Vector3(1.55f, 0.12f, 1.85f), new Vector3(2.25f, 0.32f, 2.15f), stoneMat);
            // Upper-left Mosque hill platform
            MakePart("MosqueHillPlatform", PrimitiveType.Cube, new Vector3(-1.85f, 0.22f, 2.85f), new Vector3(2.80f, 0.48f, 2.40f), stoneMat);

            // 3. Branching Asphalt Road Network + White Dashed Center Lines
            // Start curve (bottom-left)
            MakeRoadSegment("Road_Start", new Vector3(-0.78f, 0.25f, -1.65f), new Vector3(0.68f, 0.03f, 1.15f), 18f, asphaltMat, laneWhiteMat);
            // Left branch (leading toward stone archway, blocked by roadwork)
            MakeRoadSegment("Road_LeftBranch1", new Vector3(-0.82f, 0.25f, -0.45f), new Vector3(0.64f, 0.03f, 1.45f), -14f, asphaltMat, laneWhiteMat);
            MakeRoadSegment("Road_LeftBranch2", new Vector3(-1.12f, 0.25f, 0.72f), new Vector3(0.62f, 0.03f, 1.25f), -18f, asphaltMat, laneWhiteMat);
            // Center-right correct branch (curving around harbor cove and heading north to the stone bridge)
            MakeRoadSegment("Road_Correct1", new Vector3(-0.22f, 0.25f, -1.38f), new Vector3(0.64f, 0.03f, 1.28f), 56f, asphaltMat, laneWhiteMat);
            MakeRoadSegment("Road_Correct2", new Vector3(0.30f, 0.25f, -0.52f), new Vector3(0.64f, 0.03f, 1.45f), 4f, asphaltMat, laneWhiteMat);
            // Right dead-end harbor branch
            MakeRoadSegment("Road_RightBranch", new Vector3(1.08f, 0.25f, -0.98f), new Vector3(0.58f, 0.03f, 1.35f), 68f, asphaltMat, laneWhiteMat);

            // 4. Arched Stone Bridge ("Taş Kemerli Köprü") spanning the Bosphorus channel to Galata Plaza
            var bridgeRoot = new GameObject("ArchedStoneBridge");
            bridgeRoot.transform.SetParent(transform, false);
            bridgeRoot.transform.localPosition = new Vector3(0.76f, 0.30f, 0.72f);
            bridgeRoot.transform.localRotation = Quaternion.Euler(0f, 34f, 0f);
            MakeChild(bridgeRoot.transform, "BridgeDeck", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.74f, 0.08f, 1.65f), stoneMat);
            MakeChild(bridgeRoot.transform, "BridgeRoad", PrimitiveType.Cube, new Vector3(0f, 0.085f, 0f), new Vector3(0.56f, 0.02f, 1.62f), asphaltMat);
            MakeChild(bridgeRoot.transform, "BridgeParapetL", PrimitiveType.Cube, new Vector3(-0.33f, 0.14f, 0f), new Vector3(0.07f, 0.14f, 1.65f), wallCreamMat);
            MakeChild(bridgeRoot.transform, "BridgeParapetR", PrimitiveType.Cube, new Vector3(0.33f, 0.14f, 0f), new Vector3(0.07f, 0.14f, 1.65f), wallCreamMat);
            MakeChild(bridgeRoot.transform, "BridgePierCenter", PrimitiveType.Cube, new Vector3(0f, -0.14f, 0f), new Vector3(0.74f, 0.32f, 0.26f), darkStoneMat);
            MakeChild(bridgeRoot.transform, "BridgePierSouth", PrimitiveType.Cube, new Vector3(0f, -0.14f, -0.68f), new Vector3(0.74f, 0.32f, 0.24f), darkStoneMat);
            MakeChild(bridgeRoot.transform, "BridgePierNorth", PrimitiveType.Cube, new Vector3(0f, -0.14f, 0.68f), new Vector3(0.74f, 0.32f, 0.24f), darkStoneMat);
            // 4 warm streetlamps on the bridge parapets
            BuildStreetLamp(bridgeRoot.transform, new Vector3(-0.33f, 0.22f, -0.45f), ironMat, warmLampMat);
            BuildStreetLamp(bridgeRoot.transform, new Vector3(0.33f, 0.22f, -0.45f), ironMat, warmLampMat);
            BuildStreetLamp(bridgeRoot.transform, new Vector3(-0.33f, 0.22f, 0.45f), ironMat, warmLampMat);
            BuildStreetLamp(bridgeRoot.transform, new Vector3(0.33f, 0.22f, 0.45f), ironMat, warmLampMat);

            // 5. Glowing Mint-Green Route Ribbon + Animated White Forward Chevrons (>>>)
            // Start concentric glowing mint rings under the yellow taxi
            _startRingOuter = MakePart("StartRingOuter", PrimitiveType.Cylinder, new Vector3(-0.78f, 0.265f, -1.78f), new Vector3(0.78f, 0.008f, 0.78f), mintGlowMat).transform;
            MakePart("StartRingInnerDark", PrimitiveType.Cylinder, new Vector3(-0.78f, 0.268f, -1.78f), new Vector3(0.58f, 0.009f, 0.58f), asphaltMat);
            MakePart("StartRingCore", PrimitiveType.Cylinder, new Vector3(-0.78f, 0.271f, -1.78f), new Vector3(0.42f, 0.010f, 0.42f), mintSoftMat);

            // Glowing mint path segments connecting the 6 waypoints from start taxi across the stone bridge to the golden target pin
            for (int i = 0; i < CorrectWaypoints.Length - 1; i++)
            {
                Vector3 a = CorrectWaypoints[i];
                Vector3 b = CorrectWaypoints[i + 1];
                Vector3 mid = (a + b) * 0.5f;
                mid.y += 0.012f;
                float len = Vector3.Distance(a, b);
                Quaternion rot = Quaternion.LookRotation(b - a);
                var seg = MakePart("MintRouteSeg_" + i, PrimitiveType.Cube, mid, new Vector3(0.24f, 0.014f, len + 0.08f), mintGlowMat);
                seg.transform.localRotation = rot;
            }

            // 7 White forward chevrons (>>>) distributed along the glowing mint path
            for (int c = 0; c < _routeChevrons.Length; c++)
            {
                var chevRoot = new GameObject("RouteChevron_" + c);
                chevRoot.transform.SetParent(transform, false);
                var leftWing = MakeChild(chevRoot.transform, "WingL", PrimitiveType.Cube, new Vector3(-0.045f, 0f, -0.02f), new Vector3(0.035f, 0.016f, 0.13f), chevronWhiteMat);
                leftWing.localRotation = Quaternion.Euler(0f, 38f, 0f);
                var rightWing = MakeChild(chevRoot.transform, "WingR", PrimitiveType.Cube, new Vector3(0.045f, 0f, -0.02f), new Vector3(0.035f, 0.016f, 0.13f), chevronWhiteMat);
                rightWing.localRotation = Quaternion.Euler(0f, -38f, 0f);
                _routeChevrons[c] = chevRoot.transform;
            }

            // 6. Roadwork Barrier (🚧), Warning Triangle Sign (⚠), 2 Orange Cones & 2 Flashing Red Beacons on Left Branch
            var barrierRoot = new GameObject("RoadworkBarrier_LeftFork");
            barrierRoot.transform.SetParent(transform, false);
            barrierRoot.transform.localPosition = new Vector3(-0.48f, 0.26f, -0.28f);
            barrierRoot.transform.localRotation = Quaternion.Euler(0f, -16f, 0f);

            // Barrier A-frame legs & striped board
            MakeChild(barrierRoot.transform, "LegL", PrimitiveType.Cube, new Vector3(-0.28f, 0.16f, 0f), new Vector3(0.04f, 0.32f, 0.04f), trunkMat);
            MakeChild(barrierRoot.transform, "LegR", PrimitiveType.Cube, new Vector3(0.28f, 0.16f, 0f), new Vector3(0.04f, 0.32f, 0.04f), trunkMat);
            MakeChild(barrierRoot.transform, "BoardWhite", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(0.68f, 0.16f, 0.04f), laneWhiteMat);
            for (int s = -2; s <= 2; s++)
            {
                var stripe = MakeChild(barrierRoot.transform, "RedStripe_" + s, PrimitiveType.Cube, new Vector3(s * 0.12f, 0.24f, -0.005f), new Vector3(0.06f, 0.15f, 0.045f), orangeConeMat);
                stripe.localRotation = Quaternion.Euler(0f, 0f, 22f);
            }
            // Triangular Roadwork Warning Sign (⚠) in the center of the barrier
            var triLeft = MakeChild(barrierRoot.transform, "TriBorderL", PrimitiveType.Cube, new Vector3(-0.08f, 0.26f, -0.03f), new Vector3(0.035f, 0.24f, 0.02f), redAlertMat);
            triLeft.localRotation = Quaternion.Euler(0f, 0f, -30f);
            var triRight = MakeChild(barrierRoot.transform, "TriBorderR", PrimitiveType.Cube, new Vector3(0.08f, 0.26f, -0.03f), new Vector3(0.035f, 0.24f, 0.02f), redAlertMat);
            triRight.localRotation = Quaternion.Euler(0f, 0f, 30f);
            MakeChild(barrierRoot.transform, "TriBorderBot", PrimitiveType.Cube, new Vector3(0f, 0.16f, -0.03f), new Vector3(0.24f, 0.035f, 0.02f), redAlertMat);
            MakeChild(barrierRoot.transform, "TriInnerWhite", PrimitiveType.Cube, new Vector3(0f, 0.23f, -0.025f), new Vector3(0.12f, 0.12f, 0.015f), laneWhiteMat);
            MakeChild(barrierRoot.transform, "WorkerSilhouette", PrimitiveType.Cube, new Vector3(0f, 0.22f, -0.035f), new Vector3(0.04f, 0.07f, 0.018f), ironMat);

            // 2 Flashing Red Beacon Lights on top of the barrier
            _beaconRenderers[0] = MakeChild(barrierRoot.transform, "BeaconL", PrimitiveType.Sphere, new Vector3(-0.26f, 0.36f, 0f), new Vector3(0.09f, 0.09f, 0.09f), redAlertMat).GetComponent<Renderer>();
            _beaconRenderers[1] = MakeChild(barrierRoot.transform, "BeaconR", PrimitiveType.Sphere, new Vector3(0.26f, 0.36f, 0f), new Vector3(0.09f, 0.09f, 0.09f), redAlertMat).GetComponent<Renderer>();

            // 2 Orange-and-White Traffic Cones flanking the barrier
            BuildTrafficCone(barrierRoot.transform, new Vector3(-0.42f, 0f, -0.08f), orangeConeMat, laneWhiteMat);
            BuildTrafficCone(barrierRoot.transform, new Vector3(0.42f, 0f, -0.08f), orangeConeMat, laneWhiteMat);

            // 7. Destination Target on Upper-Right Plaza: Concentric Golden Rings + Giant 3D Golden Map-Pin (📍)
            Vector3 targetPos = CorrectWaypoints[CorrectWaypoints.Length - 1];
            _targetRingOuter = MakePart("TargetGoldRingOuter", PrimitiveType.Cylinder, targetPos + new Vector3(0f, -0.01f, 0f), new Vector3(0.82f, 0.009f, 0.82f), goldGlowMat).transform;
            MakePart("TargetGoldRingInnerDark", PrimitiveType.Cylinder, targetPos + new Vector3(0f, -0.006f, 0f), new Vector3(0.60f, 0.010f, 0.60f), stoneMat);
            MakePart("TargetGoldRingCore", PrimitiveType.Cylinder, targetPos + new Vector3(0f, -0.002f, 0f), new Vector3(0.38f, 0.012f, 0.38f), goldGlowMat);

            var pinRoot = new GameObject("GoldenTargetMapPin");
            pinRoot.transform.SetParent(transform, false);
            pinRoot.transform.localPosition = targetPos + new Vector3(0f, 0.52f, 0f);
            _goldenTargetPin = pinRoot.transform;
            MakeChild(_goldenTargetPin, "PinHead", PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0f), new Vector3(0.46f, 0.46f, 0.20f), goldGlowMat);
            MakeChild(_goldenTargetPin, "PinCone", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.20f, 0.24f, 0.14f), goldGlowMat);
            var hole = MakeChild(_goldenTargetPin, "PinHole", PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(0.18f, 0.12f, 0.18f), darkStoneMat);
            hole.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 8. Miniature Yellow Taxi waiting on the Start Ring
            _miniTaxi = BuildMiniYellowTaxi(CorrectWaypoints[0]);

            // 9. Iconic Istanbul Landmarks & Mediterranean Houses matching media_1792765160263_5464a1fc.jpg
            // Upper-Right Galata Tower behind the Golden Map-Pin
            BuildGalataTowerLandmark(new Vector3(1.68f, 0.28f, 2.18f), stoneMat, darkStoneMat, warmLampMat);
            // Upper-Left 4-Minaret Mosque on the peninsular hill
            BuildFourMinaretMosque(new Vector3(-1.68f, 0.46f, 2.95f), wallCreamMat, darkStoneMat, warmLampMat);
            // Upper-Center Bosphorus Suspension Bridge spanning the strait
            BuildBosphorusSuspensionBridge(new Vector3(0.45f, 0.0f, 4.65f), roofTerraMat, warmLampMat);
            // Bosphorus Ferry #1 sailing in the center strait + Ferry #2 in the right channel + Harbor Motorboat in cove
            _ferryCenter = BuildBosphorusFerry("Ferry_CenterStrait", new Vector3(0.18f, 0.02f, 2.45f), -18f, laneWhiteMat, goldGlowMat, warmLampMat);
            BuildBosphorusFerry("Ferry_RightChannel", new Vector3(1.75f, 0.02f, 0.15f), -35f, laneWhiteMat, goldGlowMat, warmLampMat);
            BuildHarborMotorboat(new Vector3(0.48f, 0.01f, -1.52f), laneWhiteMat, roofTerraMat, trunkMat);

            // Left Stone Archway Gate ("Kemerli Kapı") & Mediterranean terracotta-roofed houses
            BuildStoneArchwayGate(new Vector3(-1.12f, 0.26f, 1.12f), -18f, stoneMat, roofTerraMat);
            BuildMediterraneanHouse(new Vector3(-1.58f, 0.26f, -1.25f), new Vector3(0.68f, 0.52f, 0.68f), wallCreamMat, roofTerraMat, warmLampMat);
            BuildMediterraneanHouse(new Vector3(-1.65f, 0.26f, 0.05f), new Vector3(0.64f, 0.56f, 0.64f), wallCreamMat, roofTerraMat, warmLampMat);
            BuildMediterraneanHouse(new Vector3(-0.35f, 0.26f, 0.95f), new Vector3(0.48f, 0.44f, 0.48f), wallCreamMat, roofTerraMat, warmLampMat);
            BuildMediterraneanHouse(new Vector3(1.18f, 0.26f, -1.78f), new Vector3(0.78f, 0.58f, 0.72f), wallCreamMat, roofTerraMat, warmLampMat);

            // Lush Mediterranean trees & warm vintage streetlamps along the roads
            Vector3[] treePositions =
            {
                new Vector3(-1.22f, 0.26f, -1.75f), new Vector3(-1.25f, 0.26f, -0.65f),
                new Vector3(-0.18f, 0.26f, -0.82f), new Vector3(-0.15f, 0.26f, 0.15f),
                new Vector3(-0.68f, 0.26f, 1.55f), new Vector3(-1.15f, 0.42f, 2.15f),
                new Vector3(-2.25f, 0.42f, 2.25f), new Vector3(1.15f, 0.28f, 1.92f),
                new Vector3(0.52f, 0.25f, -1.95f), new Vector3(1.62f, 0.25f, -1.25f),
            };
            for (int t = 0; t < treePositions.Length; t++)
            {
                BuildTree(treePositions[t], (t % 2 == 0) ? foliageMat : foliageLightMat, trunkMat);
            }

            Vector3[] lampPositions =
            {
                new Vector3(-1.08f, 0.26f, -1.48f),
                new Vector3(0.02f, 0.26f, -1.05f),
                new Vector3(0.62f, 0.26f, -0.75f),
                new Vector3(1.02f, 0.28f, 1.65f),
                new Vector3(1.55f, 0.28f, 1.35f),
            };
            for (int l = 0; l < lampPositions.Length; l++)
            {
                BuildStreetLamp(transform, lampPositions[l], ironMat, warmLampMat);
            }
        }

        public void LoadPuzzle(PuzzleData puzzle)
        {
            _isDrivingRoute = false;
            _isDrivingBlocked = false;
            _driveProgress = 0f;
            _completedCallbackFired = false;
            PositionTaxiOnRoute(0f);
        }

        public void TriggerCorrectRouteDrive()
        {
            if (_isDrivingRoute) return;
            _isDrivingBlocked = false;
            _isDrivingRoute = true;
            _driveProgress = 0f;
            _completedCallbackFired = false;
            SynthAudio.Instance?.PlayClick();
        }

        public void TriggerBlockedRoadworkAttempt()
        {
            if (_isDrivingRoute || _isDrivingBlocked) return;
            _isDrivingBlocked = true;
            _driveProgress = 0f;
            SynthAudio.Instance?.PlayBrakeWarning();
        }

        void PositionTaxiOnRoute(float normalizedT)
        {
            if (_miniTaxi == null) return;
            float scaled = Mathf.Clamp01(normalizedT) * (CorrectWaypoints.Length - 1);
            int idx = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, CorrectWaypoints.Length - 2);
            float frac = scaled - idx;
            Vector3 pos = Vector3.Lerp(CorrectWaypoints[idx], CorrectWaypoints[idx + 1], frac);
            Vector3 dir = (CorrectWaypoints[idx + 1] - CorrectWaypoints[idx]).normalized;
            _miniTaxi.localPosition = pos;
            if (dir.sqrMagnitude > 0.001f)
                _miniTaxi.localRotation = Quaternion.LookRotation(dir);
        }

        void Update()
        {
            float t = Time.time;

            // Animate the 7 white forward chevrons (>>>) flowing along the glowing mint-green route
            for (int i = 0; i < _routeChevrons.Length; i++)
            {
                if (_routeChevrons[i] == null) continue;
                float u = Mathf.Repeat((i / (float)_routeChevrons.Length) + t * 0.28f, 1f);
                float scaled = u * (CorrectWaypoints.Length - 1);
                int idx = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, CorrectWaypoints.Length - 2);
                float frac = scaled - idx;
                Vector3 pos = Vector3.Lerp(CorrectWaypoints[idx], CorrectWaypoints[idx + 1], frac);
                pos.y += 0.022f;
                Vector3 dir = (CorrectWaypoints[idx + 1] - CorrectWaypoints[idx]).normalized;
                _routeChevrons[i].localPosition = pos;
                if (dir.sqrMagnitude > 0.001f)
                    _routeChevrons[i].localRotation = Quaternion.LookRotation(dir);
            }

            // Bob & rotate the Giant 3D Golden Map-Pin (📍) at the destination plaza
            if (_goldenTargetPin != null)
            {
                Vector3 basePos = CorrectWaypoints[CorrectWaypoints.Length - 1] + new Vector3(0f, 0.52f + Mathf.Sin(t * 3.2f) * 0.055f, 0f);
                _goldenTargetPin.localPosition = basePos;
                _goldenTargetPin.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.8f) * 16f, 0f);
            }

            // Pulse start & target rings
            if (_startRingOuter != null)
            {
                float s = 0.78f + Mathf.Sin(t * 3.6f) * 0.04f;
                _startRingOuter.localScale = new Vector3(s, 0.008f, s);
            }
            if (_targetRingOuter != null)
            {
                float s = 0.82f + Mathf.Sin(t * 3.6f + 1.2f) * 0.05f;
                _targetRingOuter.localScale = new Vector3(s, 0.009f, s);
            }

            // Flash the 2 red warning beacon lights on top of the roadwork barrier
            for (int b = 0; b < _beaconRenderers.Length; b++)
            {
                if (_beaconRenderers[b] == null) continue;
                float pulse = 0.65f + 0.35f * Mathf.Sin(t * 8f + b * 3.14f);
                _beaconRenderers[b].transform.localScale = Vector3.one * (0.085f + pulse * 0.025f);
            }

            // Gentle Bosphorus Ferry motion
            if (_ferryCenter != null)
            {
                _ferryCenter.localPosition = new Vector3(0.18f + Mathf.Sin(t * 0.45f) * 0.18f, 0.02f, 2.45f);
            }

            // Drive miniature yellow taxi along the glowing mint-green route to the Golden Map-Pin target
            if (_isDrivingRoute)
            {
                _driveProgress = Mathf.MoveTowards(_driveProgress, 1f, Time.deltaTime * 0.85f);
                PositionTaxiOnRoute(_driveProgress);
                if (_driveProgress >= 1f && !_completedCallbackFired)
                {
                    _completedCallbackFired = true;
                    GameManager.Instance?.SubmitChoiceAnswer(1);
                }
            }
            else if (_isDrivingBlocked)
            {
                _driveProgress = Mathf.MoveTowards(_driveProgress, 1f, Time.deltaTime * 1.6f);
                Vector3 blockedTarget = new Vector3(-0.62f, 0.28f, -0.72f);
                float ping = Mathf.Sin(_driveProgress * Mathf.PI);
                _miniTaxi.localPosition = Vector3.Lerp(CorrectWaypoints[0], blockedTarget, ping);
                _miniTaxi.localRotation = Quaternion.Euler(0f, -12f, 0f);
                if (_driveProgress >= 1f)
                {
                    _isDrivingBlocked = false;
                    _driveProgress = 0f;
                    PositionTaxiOnRoute(0f);
                }
            }
        }

        Transform BuildMiniYellowTaxi(Vector3 startPos)
        {
            var taxiMat = GlbModelLoader.MakeMat(new Color(0.98f, 0.78f, 0.12f), 0.72f, 0.15f);
            var glassMat = GlbModelLoader.MakeMat(new Color(0.14f, 0.20f, 0.28f), 0.85f);
            var wheelMat = GlbModelLoader.MakeMat(new Color(0.10f, 0.11f, 0.13f), 0.3f);
            var tailRedMat = MakeEmissive(new Color(1.0f, 0.20f, 0.12f), 2.4f);
            var signMat = MakeEmissive(new Color(1.0f, 0.90f, 0.32f), 2.0f);

            var root = new GameObject("RouteMiniYellowTaxi");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = startPos;
            root.transform.localRotation = Quaternion.Euler(0f, 34f, 0f);

            MakeChild(root.transform, "LowerBody", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.32f, 0.12f, 0.56f), taxiMat);
            MakeChild(root.transform, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.02f), new Vector3(0.26f, 0.11f, 0.32f), taxiMat);
            MakeChild(root.transform, "Windows", PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.02f), new Vector3(0.27f, 0.08f, 0.33f), glassMat);
            MakeChild(root.transform, "TaksiSign", PrimitiveType.Cube, new Vector3(0f, 0.255f, 0.0f), new Vector3(0.13f, 0.045f, 0.06f), signMat);
            MakeChild(root.transform, "TaillightL", PrimitiveType.Cube, new Vector3(-0.10f, 0.09f, -0.28f), new Vector3(0.06f, 0.035f, 0.02f), tailRedMat);
            MakeChild(root.transform, "TaillightR", PrimitiveType.Cube, new Vector3(0.10f, 0.09f, -0.28f), new Vector3(0.06f, 0.035f, 0.02f), tailRedMat);

            Vector3[] wPos =
            {
                new Vector3(-0.16f, 0.04f, 0.16f), new Vector3(0.16f, 0.04f, 0.16f),
                new Vector3(-0.16f, 0.04f, -0.16f), new Vector3(0.16f, 0.04f, -0.16f),
            };
            for (int i = 0; i < 4; i++)
            {
                var w = MakeChild(root.transform, "Wheel_" + i, PrimitiveType.Cylinder, wPos[i], new Vector3(0.09f, 0.025f, 0.09f), wheelMat);
                w.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return root.transform;
        }

        void MakeRoadSegment(string name, Vector3 pos, Vector3 scale, float yaw, Material roadMat, Material lineMat)
        {
            var seg = MakePart(name, PrimitiveType.Cube, pos, scale, roadMat);
            seg.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            MakeChild(seg.transform, "CenterDash", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.045f, 0.1f, 0.65f), lineMat);
        }

        void BuildTrafficCone(Transform parent, Vector3 localPos, Material orangeMat, Material whiteMat)
        {
            var cone = new GameObject("TrafficCone");
            cone.transform.SetParent(parent, false);
            cone.transform.localPosition = localPos;
            MakeChild(cone.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.14f, 0.03f, 0.14f), orangeMat);
            MakeChild(cone.transform, "Lower", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.10f, 0.05f, 0.10f), orangeMat);
            MakeChild(cone.transform, "Band", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.075f, 0.025f, 0.075f), whiteMat);
            MakeChild(cone.transform, "Tip", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), new Vector3(0.045f, 0.04f, 0.045f), orangeMat);
        }

        void BuildGalataTowerLandmark(Vector3 pos, Material stoneMat, Material coneRoofMat, Material winMat)
        {
            var g = new GameObject("GalataTower_Diorama");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            MakeChild(g.transform, "TowerShaft", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.45f, 0.46f), stoneMat);
            MakeChild(g.transform, "GalleryRing", PrimitiveType.Cylinder, new Vector3(0f, 0.90f, 0f), new Vector3(0.54f, 0.04f, 0.54f), coneRoofMat);
            MakeChild(g.transform, "UpperDrum", PrimitiveType.Cylinder, new Vector3(0f, 1.02f, 0f), new Vector3(0.42f, 0.10f, 0.42f), stoneMat);
            MakeChild(g.transform, "ConicalRoof", PrimitiveType.Cylinder, new Vector3(0f, 1.24f, 0f), new Vector3(0.26f, 0.16f, 0.26f), coneRoofMat);
            MakeChild(g.transform, "RoofSpire", PrimitiveType.Cylinder, new Vector3(0f, 1.46f, 0f), new Vector3(0.04f, 0.10f, 0.04f), coneRoofMat);
            for (int w = 0; w < 5; w++)
            {
                float a = (-60f + w * 30f) * Mathf.Deg2Rad;
                MakeChild(g.transform, "Win_" + w, PrimitiveType.Cube, new Vector3(Mathf.Sin(a) * 0.23f, 0.72f, -Mathf.Cos(a) * 0.23f), new Vector3(0.04f, 0.08f, 0.04f), winMat);
            }
        }

        void BuildFourMinaretMosque(Vector3 pos, Material stoneMat, Material domeMat, Material winMat)
        {
            var m = new GameObject("FourMinaretMosque_Diorama");
            m.transform.SetParent(transform, false);
            m.transform.localPosition = pos;
            MakeChild(m.transform, "SanctuaryBase", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(1.05f, 0.48f, 0.85f), stoneMat);
            MakeChild(m.transform, "MainDome", PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f), new Vector3(0.68f, 0.52f, 0.68f), domeMat);
            MakeChild(m.transform, "HalfDomeL", PrimitiveType.Sphere, new Vector3(-0.34f, 0.40f, 0f), new Vector3(0.42f, 0.34f, 0.42f), domeMat);
            MakeChild(m.transform, "HalfDomeR", PrimitiveType.Sphere, new Vector3(0.34f, 0.40f, 0f), new Vector3(0.42f, 0.34f, 0.42f), domeMat);
            Vector3[] minPos =
            {
                new Vector3(-0.62f, 0.58f, -0.45f), new Vector3(0.62f, 0.58f, -0.45f),
                new Vector3(-0.68f, 0.64f, 0.45f), new Vector3(0.68f, 0.64f, 0.45f),
            };
            for (int i = 0; i < 4; i++)
            {
                MakeChild(m.transform, "Minaret_" + i, PrimitiveType.Cylinder, minPos[i], new Vector3(0.07f, 0.58f, 0.07f), stoneMat);
                MakeChild(m.transform, "MinaretSpire_" + i, PrimitiveType.Cylinder, minPos[i] + new Vector3(0f, 0.64f, 0f), new Vector3(0.04f, 0.12f, 0.04f), domeMat);
            }
            for (int w = -2; w <= 2; w++)
            {
                MakeChild(m.transform, "GlowWin_" + w, PrimitiveType.Cube, new Vector3(w * 0.18f, 0.24f, -0.43f), new Vector3(0.06f, 0.11f, 0.02f), winMat);
            }
        }

        void BuildBosphorusSuspensionBridge(Vector3 pos, Material towerMat, Material lightMat)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Köprü", transform, pos, Quaternion.identity, 1.55f, out var eBridge))
            {
                eBridge.name = "BosphorusBridge_Diorama";
                return;
            }

            var b = new GameObject("BosphorusBridge_Diorama");
            b.transform.SetParent(transform, false);
            b.transform.localPosition = pos;
            MakeChild(b.transform, "BridgeDeck", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(4.8f, 0.05f, 0.22f), towerMat);
            MakeChild(b.transform, "TowerL", PrimitiveType.Cube, new Vector3(-1.15f, 0.72f, 0f), new Vector3(0.12f, 1.44f, 0.24f), towerMat);
            MakeChild(b.transform, "TowerR", PrimitiveType.Cube, new Vector3(1.15f, 0.72f, 0f), new Vector3(0.12f, 1.44f, 0.24f), towerMat);
            for (int i = -8; i <= 8; i++)
            {
                float x = i * 0.25f;
                MakeChild(b.transform, "DeckLight_" + i, PrimitiveType.Sphere, new Vector3(x, 0.59f, -0.11f), new Vector3(0.05f, 0.05f, 0.05f), lightMat);
            }
        }

        Transform BuildBosphorusFerry(string name, Vector3 pos, float yaw, Material whiteMat, Material yellowMat, Material winMat)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Vapur", transform, pos, Quaternion.Euler(0f, yaw + 90f, 0f), 0.42f, out var eFerry))
            {
                eFerry.name = name;
                return eFerry.transform;
            }

            var f = new GameObject(name);
            f.transform.SetParent(transform, false);
            f.transform.localPosition = pos;
            f.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            MakeChild(f.transform, "Hull", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.82f, 0.12f, 0.28f), whiteMat);
            MakeChild(f.transform, "Cabin1", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.64f, 0.09f, 0.22f), winMat);
            MakeChild(f.transform, "Cabin2", PrimitiveType.Cube, new Vector3(-0.04f, 0.22f, 0f), new Vector3(0.46f, 0.07f, 0.18f), whiteMat);
            MakeChild(f.transform, "Funnel", PrimitiveType.Cylinder, new Vector3(-0.06f, 0.30f, 0f), new Vector3(0.08f, 0.07f, 0.08f), yellowMat);
            return f.transform;
        }

        void BuildHarborMotorboat(Vector3 pos, Material whiteMat, Material redMat, Material woodMat)
        {
            var boat = new GameObject("HarborMotorboat");
            boat.transform.SetParent(transform, false);
            boat.transform.localPosition = pos;
            boat.transform.localRotation = Quaternion.Euler(0f, -32f, 0f);
            MakeChild(boat.transform, "Hull", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.08f, 0.42f), whiteMat);
            MakeChild(boat.transform, "Deck", PrimitiveType.Cube, new Vector3(0f, 0.09f, -0.02f), new Vector3(0.16f, 0.06f, 0.22f), woodMat);
            MakeChild(boat.transform, "Roof", PrimitiveType.Cube, new Vector3(0f, 0.13f, -0.02f), new Vector3(0.17f, 0.025f, 0.24f), redMat);
        }

        void BuildStoneArchwayGate(Vector3 pos, float yaw, Material stoneMat, Material roofMat)
        {
            var g = new GameObject("StoneArchwayGate");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            MakeChild(g.transform, "PillarL", PrimitiveType.Cube, new Vector3(-0.26f, 0.24f, 0f), new Vector3(0.18f, 0.48f, 0.26f), stoneMat);
            MakeChild(g.transform, "PillarR", PrimitiveType.Cube, new Vector3(0.26f, 0.24f, 0f), new Vector3(0.18f, 0.48f, 0.26f), stoneMat);
            MakeChild(g.transform, "ArchBeam", PrimitiveType.Cube, new Vector3(0f, 0.44f, 0f), new Vector3(0.72f, 0.14f, 0.28f), stoneMat);
            MakeChild(g.transform, "Roof", PrimitiveType.Cube, new Vector3(0f, 0.54f, 0f), new Vector3(0.78f, 0.08f, 0.34f), roofMat);
        }

        void BuildMediterraneanHouse(Vector3 pos, Vector3 size, Material wallMat, Material roofMat, Material winMat)
        {
            string hModel = (pos.x < -1.0f) ? "Çevre_Bina_Pempe_Ev" : (pos.x > 0.5f ? "Çevre_Bina_Kafe_Ev" : "Çevre_Bina_Mavi_Ev");
            if (GlbModelLoader.TryInstantiateEModel(hModel, transform, pos, Quaternion.Euler(0f, 180f, 0f), size.y * 1.25f, out var eHouse))
            {
                eHouse.name = "MedHouse_" + hModel;
                return;
            }

            var h = new GameObject("MedHouse");
            h.transform.SetParent(transform, false);
            h.transform.localPosition = pos;
            MakeChild(h.transform, "Walls", PrimitiveType.Cube, new Vector3(0f, size.y * 0.5f, 0f), size, wallMat);
            MakeChild(h.transform, "Roof", PrimitiveType.Cube, new Vector3(0f, size.y + 0.06f, 0f), new Vector3(size.x + 0.08f, 0.12f, size.z + 0.08f), roofMat);
            MakeChild(h.transform, "Win1", PrimitiveType.Cube, new Vector3(-size.x * 0.22f, size.y * 0.55f, -size.z * 0.51f), new Vector3(0.09f, 0.12f, 0.02f), winMat);
            MakeChild(h.transform, "Win2", PrimitiveType.Cube, new Vector3(size.x * 0.22f, size.y * 0.55f, -size.z * 0.51f), new Vector3(0.09f, 0.12f, 0.02f), winMat);
        }

        void BuildTree(Vector3 pos, Material foliageMat, Material trunkMat)
        {
            string treeModel = (Mathf.Abs(pos.x) > 1.1f) ? "Çevre_Selvi" : "Çevre_Zakkum_Ağacı";
            if (GlbModelLoader.TryInstantiateEModel(treeModel, transform, pos, Quaternion.identity, 0.52f, out var eTree))
            {
                eTree.name = "DioramaTree_" + treeModel;
                return;
            }

            var tr = new GameObject("DioramaTree");
            tr.transform.SetParent(transform, false);
            tr.transform.localPosition = pos;
            MakeChild(tr.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 0.10f, 0f), new Vector3(0.06f, 0.10f, 0.06f), trunkMat);
            MakeChild(tr.transform, "Crown1", PrimitiveType.Sphere, new Vector3(0f, 0.26f, 0f), new Vector3(0.28f, 0.24f, 0.28f), foliageMat);
            MakeChild(tr.transform, "Crown2", PrimitiveType.Sphere, new Vector3(0.06f, 0.34f, 0.04f), new Vector3(0.20f, 0.18f, 0.20f), foliageMat);
        }

        void BuildStreetLamp(Transform parent, Vector3 pos, Material postMat, Material bulbMat)
        {
            if (GlbModelLoader.TryInstantiateEModel("Çevre_Sokak_Lambası", parent, pos, Quaternion.identity, 0.38f, out var eLamp))
            {
                eLamp.name = "StreetLamp";
                return;
            }

            var l = new GameObject("StreetLamp");
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos;
            MakeChild(l.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, 0f), new Vector3(0.03f, 0.14f, 0.03f), postMat);
            MakeChild(l.transform, "Lantern", PrimitiveType.Sphere, new Vector3(0f, 0.30f, 0f), new Vector3(0.08f, 0.09f, 0.08f), bulbMat);
        }

        static Material MakeEmissive(Color c, float intensity)
        {
            var mat = GlbModelLoader.MakeMat(c, 0.85f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", c * intensity);
            return mat;
        }

        GameObject MakePart(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static Transform MakeChild(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go.transform;
        }
    }
}

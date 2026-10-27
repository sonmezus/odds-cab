using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using OddsCab.Puzzles;
using OddsCab.Dioramas;

namespace OddsCab
{
    public enum GameState
    {
        MainMenu,
        DrivingToStop,
        PassengerBoarding,
        PuzzleActive,
        PuzzleResult,
        /// <summary>On the Harem–Sirkeci car ferry between districts on opposite shores (FerryCrossing).</summary>
        FerryCrossing,
        DistrictTransition,
    }

    /// <summary>
    /// Central coordinator for ODDS CAB (Portrait Mode):
    /// - Drives the 512-stop progression loop
    /// - Orchestrates the exact 4-row animation sequence from 08-animation.png:
    ///   - Row 1: Waiting (1.1) -> NoticingTaxi with /// marks (1.2) -> WavingBackView with teal backpack (1.3) -> ApproachingDoor (1.4)
    ///   - Row 2: Clean side-view 4-pose walking cycle into the taxi door
    ///   - Row 3: DisappointedInCab (3.1) -> SteppingOutOfDoor (3.2) -> StandingSadBesideDoor (3.3) -> WalkingAway with teal backpack (3.4)
    ///   - Row 4: 4 Close-up expression portraits (Curious, Thoughtful, Satisfied, Disappointed).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.MainMenu;
        public TaxiController Taxi { get; private set; }
        public CityChunkStreamer City { get; private set; }
        public WeatherDirector Weather { get; private set; }
        public BellDioramaController BellDiorama { get; private set; }
        public BridgeDioramaController BridgeDiorama { get; private set; }
        public TorchBridgeDioramaController TorchBridge { get; private set; }
        public RouteTilePuzzleController RouteTiles { get; private set; }
        public UiDirector Ui { get; private set; }

        public int CurrentStop => SaveSystem.Data.currentStop;
        public PuzzleData ActivePuzzle { get; private set; }
        public PassengerProfile ActivePassengerProfile { get; private set; }
        public PassengerActor ActiveCabPassenger => _cabPassenger;
        public PassengerActor ActiveCurbPassenger => _curbPassenger;
        public float TimeRemaining { get; private set; }
        public float TotalTimeForPuzzle { get; private set; }
        public bool[] DisabledOptions { get; private set; } = new bool[4];
        public bool LastAnswerWasCorrect { get; private set; } = true;
        public bool IsWaitingForPickupConfirm { get; private set; }
        public RenderTexture PassengerLiveTexture { get; private set; }
        public Camera PassengerLiveCamera => _passengerLiveCam;

        Camera _cam;
        Camera _passengerLiveCam;
        PassengerActor _curbPassenger;
        PassengerActor _cabPassenger;
        PassengerActor _walkingAwayPassenger;
        float _nextStopZ;
        bool _brakeWarningPlayed;
        int _disembarkStage; // 0=none, 1=Panel3.1(InCab), 2=Panel3.2(StepOut), 3=Panel3.3(StandBesideDoor), 4=Panel3.4(WalkAway)
        int _timingGateIndex;
        Vector3 _camVel;
        Vector3 _cabCamLocal;
        bool _camInCab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBootstrap()
        {
            if (Instance != null || FindAnyObjectByType<GameManager>() != null) return;
            var go = new GameObject("OddsCab_GameManager");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Application.runInBackground = true;

            SaveSystem.Load();
            Loc.Lang = (Language)Mathf.Clamp(SaveSystem.Data.language, 0, 1);
            DeviceTierDetector.ApplyFromSave();

            SetupEnvironmentAndCamera();
            new GameObject("SynthAudio").AddComponent<SynthAudio>();
            SynthAudio.Instance?.SetSpokenGuideEnabled(SaveSystem.Data.spokenGuideEnabled);
            new GameObject("VfxDirector").AddComponent<VfxDirector>();

            var taxiGo = new GameObject("Taxi");
            Taxi = taxiGo.AddComponent<TaxiController>();
            Taxi.Initialize();

            SetupPassengerLiveCamera();

            var cityGo = new GameObject("CityStreamer");
            City = cityGo.AddComponent<CityChunkStreamer>();
            City.Initialize(Taxi.transform,
                StopPlanner.DistrictIndex(Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops)));

            var weatherGo = new GameObject("WeatherDirector");
            Weather = weatherGo.AddComponent<WeatherDirector>();

            BuildDioramaStage();

            var uiGo = new GameObject("UiDirector");
            Ui = uiGo.AddComponent<UiDirector>();
            Ui.Initialize(this);

            Weather.ApplyWeatherForStop(SaveSystem.Data.currentStop);
            City.SetDistrict(StopPlanner.DistrictIndex(Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops)));
            EnterMainMenu();
            WorldRayTracingScene.Attach(Taxi.transform);
        }

        void SetupEnvironmentAndCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                _cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.42f, 0.76f, 0.95f);
            _cam.fieldOfView = 48f;
            _cam.nearClipPlane = 0.05f;
            // Quality was selected before this camera existed on a bootstrap-only scene.
            DeviceTierDetector.ApplyCameraAntialiasing(_cam);
            HorizonCameraStack.Attach(_cam);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.76f, 0.85f, 0.95f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 155f;
            RenderSettings.ambientLight = new Color(0.72f, 0.68f, 0.64f);

            var sun = WeatherDirector.FindDirectionalSun();
            if (sun == null)
            {
                var sunGo = new GameObject("Directional Sun");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1.0f, 0.9372549f, 0.8f);
                sun.intensity = 1.25f;
                sunGo.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            }
            sun.shadows = LightShadows.Soft;
            sun.GetUniversalAdditionalLightData().softShadowQuality = SoftShadowQuality.UsePipelineSettings;
            RenderSettings.sun = sun;
        }

        void SetupPassengerLiveCamera()
        {
            if (Taxi == null) return;
            PassengerLiveTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
            PassengerLiveTexture.name = "PassengerLiveCameraRT";
            PassengerLiveTexture.Create();

            // In-car webcam (reference: dashboard camera on the passenger): head, shoulders and seatbelt, the
            // dark seat and the street through the rear window behind. It sees the cab layer and the street, never
            // the car body it sits inside or the vista ahead of the taxi.
            var pCamGo = new GameObject("PassengerLiveCamera");
            pCamGo.transform.SetParent(Taxi.transform, false);
            TaxiCameraFraming.SolveLivePortrait(TaxiController.RearRightPassengerPosition + Vector3.up * 1.04f,
                out var cameraLocal, out var focusLocal);
            pCamGo.transform.localPosition = cameraLocal;
            pCamGo.transform.localRotation = Quaternion.LookRotation(focusLocal - cameraLocal);
            _passengerLiveCam = pCamGo.AddComponent<Camera>();
            _passengerLiveCam.targetTexture = PassengerLiveTexture;
            _passengerLiveCam.clearFlags = CameraClearFlags.Skybox;
            _passengerLiveCam.cullingMask = ~((1 << TaxiController.ExteriorLayer) | (1 << HorizonCameraStack.VistaLayer));
            _passengerLiveCam.fieldOfView = TaxiCameraFraming.CabFieldOfView;
            _passengerLiveCam.nearClipPlane = 0.05f;
            _passengerLiveCam.farClipPlane = 80f;
            _passengerLiveCam.enabled = false;
        }

        public void SetPassengerLiveCamActive(bool active)
        {
            if (_passengerLiveCam != null && _passengerLiveCam.enabled != active)
                _passengerLiveCam.enabled = active;
        }

        void BuildDioramaStage()
        {
            var bellGo = new GameObject("BellDioramaStage");
            bellGo.transform.position = new Vector3(0f, -40f, 0f);
            BellDiorama = bellGo.AddComponent<BellDioramaController>();
            BellDiorama.BuildGeometry();
            bellGo.SetActive(false);

            var bridgeGo = new GameObject("BridgeDioramaStage");
            bridgeGo.transform.position = new Vector3(0f, -40f, 0f);
            BridgeDiorama = bridgeGo.AddComponent<BridgeDioramaController>();
            BridgeDiorama.BuildGeometry();
            bridgeGo.SetActive(false);

            var torchBridgeGo = new GameObject("TorchBridgeDioramaStage");
            torchBridgeGo.transform.position = new Vector3(0f, -40f, 0f);
            TorchBridge = torchBridgeGo.AddComponent<TorchBridgeDioramaController>();
            TorchBridge.BuildGeometry();
            torchBridgeGo.SetActive(false);

            // Rotating-road route puzzle (F54). The old fixed route diorama is no longer built.
            var routeGo = new GameObject("RouteTilePuzzleStage");
            routeGo.transform.position = new Vector3(0f, -40f, 0f);
            RouteTiles = routeGo.AddComponent<RouteTilePuzzleController>();
            RouteTiles.BuildMaterials();
            routeGo.SetActive(false);
        }

        public void EnterMainMenu()
        {
            bool leavingFerry = State == GameState.FerryCrossing;
            StopAllCoroutines();
            if (leavingFerry && _ferry != null)
            {
                _ferry.End();
                Ui.ShowFerryBanner(false, City.District);
            }
            _districtTransitionRunning = false;
            Ui.ShowDistrictLoading(false);
            if (City.NeedsLayoutForDistrict(City.District)) City.SetDistrict(City.District);
            City.SetStreetVisible(true);
            State = GameState.MainMenu;
            if (Weather != null && !Weather.UserOverrodeWeather)
                Weather.SetWeather(WeatherPreset.GoldenSunset, false);
            _disembarkStage = 0;
            Taxi.SetExteriorVisible(true);
            Taxi.SetDoorOpen(false);
            Taxi.SetLeftDoorOpen(false);
            Taxi.TargetLateralX = GameConfig.DriveLaneX;
            Taxi.TargetSpeed = Taxi.EffectiveCruiseSpeed * 0.45f;
            VfxDirector.Instance?.HideStopReceiverRings();
            VfxDirector.Instance?.SetAccelerationCelebrationActive(false);
            VfxDirector.Instance?.SetWrongAnswerRewindActive(false);
            BellDiorama.gameObject.SetActive(false);
            BridgeDiorama.gameObject.SetActive(false);
            TorchBridge.gameObject.SetActive(false);
            RouteTiles.gameObject.SetActive(false);
            Ui.RefreshForState();
            if (SaveSystem.Data.hasPendingDistrictFinale)
                Ui.ShowDistrictFinale(SaveSystem.Data.pendingDistrictFinale);
        }

        public void StartOrContinueDriving()
        {
            SynthAudio.Instance?.PlayClick();
            if (State == GameState.MainMenu) SynthAudio.Instance?.PlayGuide(GuideCue.Welcome);
            if (SaveSystem.Data.campaignComplete || SaveSystem.Data.currentStop > GameConfig.TotalStops)
                SaveSystem.StartNewTour();
            ScheduleNextStop();
        }

        FerryCrossing _ferry;
        float _ferryT;
        bool _ferrySkip;
        bool _districtTransitionRunning;
        const float FerrySeconds = 7f;

        /// <summary>Skips the rest of the car-ferry crossing (button on its banner).</summary>
        public void SkipFerry() => _ferrySkip = true;

        void ScheduleNextStop()
        {
            if (_districtTransitionRunning) return;
            // A new district across the water: cross on the car ferry first, then carry on in the new district
            int nextDistrict = StopPlanner.DistrictIndex(Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops));
            if (State != GameState.FerryCrossing && City.District >= 0 && nextDistrict != City.District && TourRoute.ApproachHasFerry(nextDistrict))
            {
                StartCoroutine(FerryCrossingRoutine(nextDistrict));
                return;
            }
            if (City.NeedsLayoutForDistrict(nextDistrict))
            {
                StartCoroutine(DistrictTransitionRoutine(nextDistrict));
                return;
            }
            _disembarkStage = 0;
            IsWaitingForPickupConfirm = false;
            SetPassengerLiveCamActive(false);
            if (_cam != null) _camVel = Vector3.zero;
            Taxi.SetExteriorVisible(true);
            Taxi.SetDoorOpen(false);
            Taxi.SetLeftDoorOpen(false);
            Taxi.TargetLateralX = GameConfig.DriveLaneX;
            VfxDirector.Instance?.SetAccelerationCelebrationActive(false);
            VfxDirector.Instance?.SetWrongAnswerRewindActive(false);
            BellDiorama.gameObject.SetActive(false);
            BridgeDiorama.gameObject.SetActive(false);
            TorchBridge.gameObject.SetActive(false);
            RouteTiles.gameObject.SetActive(false);

            if (_walkingAwayPassenger != null)
            {
                Destroy(_walkingAwayPassenger.gameObject);
                _walkingAwayPassenger = null;
            }

            int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
            Weather?.ApplyWeatherForStop(stop);
            City.SetDistrict(StopPlanner.DistrictIndex(stop));
            City.OnStopScheduled(stop, Taxi.DistanceZ);   // the real landmark this stretch of road passes, if any
            if ((stop - 1) % GameConfig.StopsPerDistrict == 0)
                SynthAudio.Instance?.PlayGuide(GuideCue.DistrictArrival);
            ActivePassengerProfile = PassengerCatalog.ForStop(stop);
            ActivePuzzle = StopPlanner.GetPuzzleForStop(stop);
            _timingGateIndex = 0;
            // Re-order choice options after every wrong answer so a retry can't be passed by memorising the slot.
            ShuffleChoiceOptions(ActivePuzzle, stop * 9973 + SaveSystem.Data.totalWrong * 7919);

            _nextStopZ = Taxi.DistanceZ + GameConfig.StopApproachDistance;
            _brakeWarningPlayed = false;

            VfxDirector.Instance?.ShowStopReceiverRings(new Vector3(GameConfig.SideX(-2.20f), 0f, _nextStopZ));
            SpawnCurbPassenger(ActivePassengerProfile, _nextStopZ);

            State = GameState.DrivingToStop;
            Taxi.TargetSpeed = Taxi.EffectiveCruiseSpeed;
            Ui.RefreshForState();
        }

        IEnumerator DistrictTransitionRoutine(int district)
        {
            _districtTransitionRunning = true;
            State = GameState.DistrictTransition;
            Taxi.StopImmediately();
            City.SetStreetVisible(false);
            Ui.RefreshForState();
            Ui.ShowDistrictLoading(true);
            yield return null;
            yield return City.SetDistrictOverFrames(district);
            City.SetStreetVisible(true);
            Ui.ShowDistrictLoading(false);
            _districtTransitionRunning = false;
            ScheduleNextStop();
        }

        IEnumerator FerryCrossingRoutine(int district)
        {
            State = GameState.FerryCrossing;
            _disembarkStage = 0;
            IsWaitingForPickupConfirm = false;
            SetPassengerLiveCamActive(false);
            if (_cabPassenger != null) { Destroy(_cabPassenger.gameObject); _cabPassenger = null; }
            if (_walkingAwayPassenger != null) { Destroy(_walkingAwayPassenger.gameObject); _walkingAwayPassenger = null; }
            VfxDirector.Instance?.SetAccelerationCelebrationActive(false);
            VfxDirector.Instance?.SetWrongAnswerRewindActive(false);
            VfxDirector.Instance?.HideStopReceiverRings();
            BellDiorama.gameObject.SetActive(false);
            BridgeDiorama.gameObject.SetActive(false);
            TorchBridge.gameObject.SetActive(false);
            RouteTiles.gameObject.SetActive(false);
            Taxi.SetExteriorVisible(true);
            Taxi.SetDoorOpen(false);
            Taxi.SetLeftDoorOpen(false);
            Taxi.StopImmediately();
            Taxi.TargetLateralX = Taxi.transform.position.x;

            // The street gives way to open water; the vista ahead shows the far shore with the new district's landmark
            if (_ferry == null) _ferry = FerryCrossing.Create(City.SeaMaterial);
            City.SetStreetVisible(false);
            _ferry.Begin(Taxi.transform.position);
            _camVel = Vector3.zero;
            _ferryT = 0f;
            _ferrySkip = false;
            Ui.RefreshForState();
            Ui.ShowFerryBanner(true, district);
            yield return null;
            yield return City.SetDistrictOverFrames(district);

            float t = 0f;
            while (t < FerrySeconds && !_ferrySkip)
            {
                t += Time.deltaTime;
                _ferryT = t / FerrySeconds;
                yield return null;
            }

            Ui.ShowFerryBanner(false, district);
            _ferry.End();
            City.SetStreetVisible(true);
            Taxi.TargetLateralX = GameConfig.DriveLaneX;
            _camVel = Vector3.zero;
            State = GameState.DrivingToStop;   // (not FerryCrossing, so ScheduleNextStop carries on normally)
            ScheduleNextStop();
        }

        static void ShuffleChoiceOptions(PuzzleData p, int seed)
        {
            if (p == null) return;
            if (p.kind == PuzzleKind.Letters)
            {
                p.lettersTR = ShuffleString(p.lettersTR, seed);
                p.lettersEN = ShuffleString(p.lettersEN, seed + 1);
                return;
            }
            if (p.optionsTR == null || p.optionsTR.Length != 4) return;
            if (p.kind == PuzzleKind.Ordering)
            {
                ShuffleOrderingOptions(p, seed);
                return;
            }
            if (p.kind != PuzzleKind.Choice) return;
            bool hasEN = p.optionsEN != null && p.optionsEN.Length == 4;
            var rng = new System.Random(seed);
            int[] order = { 0, 1, 2, 3 };
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var tr = new string[4];
            var en = hasEN ? new string[4] : p.optionsEN;
            int newCorrect = p.correct;
            for (int slot = 0; slot < 4; slot++)
            {
                tr[slot] = p.optionsTR[order[slot]];
                if (hasEN) en[slot] = p.optionsEN[order[slot]];
                if (order[slot] == p.correct) newCorrect = slot;
            }
            p.optionsTR = tr;
            p.optionsEN = en;
            p.correct = newCorrect;
        }

        static string ShuffleString(string s, int seed)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var rng = new System.Random(seed);
            var c = s.ToCharArray();
            for (int i = c.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (c[i], c[j]) = (c[j], c[i]); }
            return new string(c);
        }

        /// <summary>Letters puzzles: the word the player built from tiles.</summary>
        public void SubmitLettersAttempt(string word)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Letters) return;
            if (word == ActivePuzzle.Answer) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        static void ShuffleOrderingOptions(PuzzleData p, int seed)
        {
            if (p.ints == null || p.ints.Length != 4) return;
            bool hasEN = p.optionsEN != null && p.optionsEN.Length == 4;
            var rng = new System.Random(seed);
            int[] order = { 0, 1, 2, 3 };   // order[newSlot] = oldSlot
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var newSlotOf = new int[4];
            var tr = new string[4];
            var en = hasEN ? new string[4] : p.optionsEN;
            for (int slot = 0; slot < 4; slot++)
            {
                tr[slot] = p.optionsTR[order[slot]];
                if (hasEN) en[slot] = p.optionsEN[order[slot]];
                newSlotOf[order[slot]] = slot;
            }
            p.optionsTR = tr;
            p.optionsEN = en;
            p.ints = p.ints.Select(old => newSlotOf[old]).ToArray();
            p.correct = p.ints[0];
        }

        /// <summary>Ordering puzzles: the four tapped option indices, in tap order.</summary>
        public void SubmitOrderAttempt(int[] taps)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Ordering) return;
            var want = ActivePuzzle.ints;
            bool ok = taps != null && want != null && taps.Length == want.Length;
            for (int i = 0; ok && i < want.Length; i++) ok = taps[i] == want[i];
            if (ok) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        /// <summary>Shikaku: each submitted cell must belong to the unique rectangle containing its clue.</summary>
        public void SubmitGridAttempt(int[] regionOwners)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Grid) return;
            var expected = ActivePuzzle.gridSolutionOwners;
            bool ok = regionOwners != null && expected != null && regionOwners.Length == expected.Length;
            for (int i = 0; ok && i < expected.Length; i++) ok = regionOwners[i] == expected[i];
            if (ok) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        public void SubmitCompareAttempt(int changedCell)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Compare) return;
            if (changedCell == ActivePuzzle.correct) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        public void SubmitTimingAttempt(float markerPosition)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Timing) return;
            var targets = ActivePuzzle.timingTargets;
            if (targets == null || targets.Length == 0) return;
            int tolerance = ActivePuzzle.timingTolerance;
            int position = Mathf.RoundToInt(Mathf.Clamp01(markerPosition) * 1000f);
            if (Mathf.Abs(position - targets[_timingGateIndex]) > tolerance)
            {
                Ui.ShowToast(Loc.T("timing_missed"));
                StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
                return;
            }

            _timingGateIndex++;
            if (_timingGateIndex >= targets.Length)
            {
                OnPuzzleSolved();
                return;
            }
            Ui.RefreshTimingUi(_timingGateIndex);
        }

        public void SubmitMiniMask(int selectedMask)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null ||
                (ActivePuzzle.kind != PuzzleKind.Valve && ActivePuzzle.kind != PuzzleKind.Lantern)) return;
            if (selectedMask == ActivePuzzle.answerValue) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        public void SubmitAkariAttempt(int lampMask)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Akari) return;
            var solutions = ActivePuzzle.akariSolutions;
            bool solved = solutions != null && solutions.Length > 0 && CountSetBits(lampMask) == 2;
            for (int i = 0; solved && i < solutions.Length; i++)
                if (lampMask == solutions[i]) { OnPuzzleSolved(); return; }
            StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        public void SubmitDoorCycleAttempt(int time)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.DoorCycle) return;
            if (time == ActivePuzzle.answerValue) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        public void SubmitValveCauseAttempt(int valveSide, int steps)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.ValveCause) return;
            int requiredSteps = ActivePuzzle.ints != null && ActivePuzzle.ints.Length > 0 ? ActivePuzzle.ints[0] : 3;
            if (valveSide == ActivePuzzle.answerValue && steps == requiredSteps) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        static int CountSetBits(int value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }

        public void SubmitShadowAttempt(int direction)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null || ActivePuzzle.kind != PuzzleKind.Shadow) return;
            if (direction == ActivePuzzle.correct) OnPuzzleSolved();
            else StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        void SpawnCurbPassenger(PassengerProfile profile, float stopZ)
        {
            if (_curbPassenger != null) Destroy(_curbPassenger.gameObject);
            var go = GlbModelLoader.InstantiatePassenger(profile);
            // Spawn on the curb-side coastal cobblestone promenade in front of the open door arrival point
            // (matching Station Arrival reference image with Bosphorus Ferry, Bridge & Mosque in background)
            go.transform.position = new Vector3(GameConfig.SideX(-4.85f + CityChunkStreamer.SideShift), 0.00f, stopZ + 0.60f);
            go.transform.rotation = Quaternion.Euler(0f, GameConfig.SideYaw(135f), 0f);
            _curbPassenger = go.AddComponent<PassengerActor>();
            _curbPassenger.Setup(profile, PassengerPose.Waiting);
            CurveBender.Convert(go.transform);   // waits on the pavement ahead, which bends with the route
        }

        void SpawnCabPassenger(PassengerProfile profile)
        {
            if (_cabPassenger != null) Destroy(_cabPassenger.gameObject);
            var go = GlbModelLoader.InstantiatePassenger(profile);
            go.transform.SetParent(Taxi.transform, false);
            // Seated naturally on top of the rear bench cushion (y = -0.04f places hips directly on seat).
            // On the cab layer the passenger is never drawn from outside, so one size fits every view.
            go.transform.localPosition = TaxiController.RearRightPassengerPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.82f;
            _cabPassenger = go.AddComponent<PassengerActor>();
            _cabPassenger.Setup(profile, PassengerPose.SittingInCab);
            TaxiController.SetLayerRecursive(go.transform, TaxiController.CabLayer);
        }

        public bool IsMelodyDemoPaused =>
            State == GameState.PuzzleActive &&
            ActivePuzzle != null &&
            ActivePuzzle.kind == PuzzleKind.Bells &&
            BellDiorama != null &&
            BellDiorama.IsPlayingSequence;

        void Update()
        {
            UpdateCamera();

            if (State == GameState.DrivingToStop)
            {
                float dist = _nextStopZ - Taxi.DistanceZ;

                // Row 1 progression from 08-animation.png + Station Arrival reference image:
                // 1) dist > 22m: Panel 1.1 Waiting (calm breathing beside streetlamp)
                // 2) 22m >= dist > 12m: Panel 1.2 NoticingTaxi (3 black marks /// by head spotting approaching taxi)
                // 3) 12m >= dist > 4.2m: Panel 1.3 WavingBackView (3/4 back view showing teal backpack, right arm raised high waving)
                // 4) dist <= 4.2m: Panel 1.4 ApproachingDoor (stepping forward beside the open left door on the cobblestones, waving warmly)
                if (_curbPassenger != null)
                {
                    if (dist <= 22f && dist > 12f && _curbPassenger.Pose != PassengerPose.NoticingTaxi)
                    {
                        _curbPassenger.SetPose(PassengerPose.NoticingTaxi, _curbPassenger.transform.localPosition, Quaternion.Euler(0f, GameConfig.SideYaw(138f), 0f));
                    }
                    else if (dist <= 12f && dist > 4.2f && _curbPassenger.Pose != PassengerPose.WavingBackView)
                    {
                        _curbPassenger.SetPose(PassengerPose.WavingBackView, _curbPassenger.transform.localPosition, Quaternion.Euler(0f, GameConfig.SideYaw(148f), 0f));
                    }
                    else if (dist <= 4.2f && _curbPassenger.Pose != PassengerPose.ApproachingDoor)
                    {
                        Vector3 greetPos = new Vector3(GameConfig.SideX(-4.85f + CityChunkStreamer.SideShift), 0.00f, _nextStopZ + 0.60f);
                        _curbPassenger.SetPose(PassengerPose.ApproachingDoor, greetPos, Quaternion.Euler(0f, GameConfig.SideYaw(135f), 0f));
                        if (SynthAudio.Instance != null)
                        {
                            float greetingDuration = SynthAudio.Instance.PlayPassengerVoice(CurrentStop % 5, 0.65f);
                            if (greetingDuration > 0f) _curbPassenger.Speak(greetingDuration);
                        }
                    }
                }

                if (dist < 18f)
                {
                    if (!_brakeWarningPlayed)
                    {
                        _brakeWarningPlayed = true;
                        SynthAudio.Instance?.PlayBrakeWarning();
                        SynthAudio.Instance?.PlayBrakeSqueal();
                        VfxDirector.Instance?.PlayBrakeDecel(Taxi.transform.position);
                    }
                    Taxi.TargetLateralX = GameConfig.CurbStopX;
                    Taxi.TargetSpeed = Mathf.Lerp(1.2f, Taxi.EffectiveCruiseSpeed, Mathf.Clamp01(dist / 18f));
                }

                if (dist <= 2.2f)
                {
                    Taxi.SetLeftDoorOpen(true);
                }

                if (dist <= 0.6f)
                    StartCoroutine(BoardPassengerAndStartPuzzle());
            }
            else if (State == GameState.PuzzleActive)
            {
                bool routeDriving = ActivePuzzle != null && ActivePuzzle.kind == PuzzleKind.Route && RouteTiles != null && RouteTiles.IsDriving;
                if (!IsMelodyDemoPaused && !routeDriving && TimeRemaining > 0f)
                {
                    TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
                    if (TimeRemaining <= 0f)
                    {
                        StartCoroutine(PassengerDisembarkUnpaidRoutine(true));
                    }
                }
            }
        }

        public void ConfirmPassengerPickup()
        {
            if (!IsWaitingForPickupConfirm) return;
            SynthAudio.Instance?.PlayClick();
            SynthAudio.Instance?.PlayGuide(GuideCue.Pickup);
            IsWaitingForPickupConfirm = false;
            Ui.RefreshForState();
        }

        IEnumerator BoardPassengerAndStartPuzzle()
        {
            State = GameState.PassengerBoarding;
            Taxi.StopImmediately();
            // Snap taxi cleanly to curb stop position so it doesn't overshoot the passenger!
            Vector3 stopPos = Taxi.transform.position;
            stopPos.z = _nextStopZ;
            stopPos.x = GameConfig.SideX(-2.40f);
            Taxi.transform.position = stopPos;
            Taxi.SetLeftDoorOpen(true);

            // Previous satisfied passenger (who rode in the back seat until this stop) finishes their trip
            if (_cabPassenger != null)
            {
                Destroy(_cabPassenger.gameObject);
                _cabPassenger = null;
            }

            // Station Arrival Framing (matching Station Arrival reference image):
            // Taxi is stopped at 0 km/sa with left passenger door open, passenger standing right beside the open door on the cobblestones waving,
            // and the bottom "YOLCUYU AL / ▶ DEVAM ET" card is displayed!
            if (_curbPassenger != null)
            {
                Vector3 greetPos = new Vector3(GameConfig.SideX(-4.85f + CityChunkStreamer.SideShift), 0.00f, _nextStopZ + 0.60f);
                _curbPassenger.SetPose(PassengerPose.ApproachingDoor, greetPos, Quaternion.Euler(0f, GameConfig.SideYaw(135f), 0f));
            }

            IsWaitingForPickupConfirm = true;
            Ui.RefreshForState();

            while (IsWaitingForPickupConfirm)
            {
                yield return null;
            }

            // Row 2 of 08-animation.png: 4 consecutive walking cycle master poses in a clean side view,
            // naturally alternating arms and legs with teal backpack and zero extra limbs!
            if (_curbPassenger != null)
            {
                Vector3 startPos = _curbPassenger.transform.position;
                float doorX = Taxi.transform.position.x + GameConfig.SideX(-0.76f);
                Vector3 doorPos = new Vector3(doorX, 0.16f, Taxi.DistanceZ - 0.45f);
                Quaternion walkRot = Quaternion.LookRotation((doorPos - startPos).sqrMagnitude > 0.001f ? (doorPos - startPos).normalized : Vector3.right);
                _curbPassenger.SetPose(PassengerPose.WalkingToCab, startPos, walkRot);

                float walkDur = 0.55f;
                float t = 0f;
                while (t < walkDur)
                {
                    t += Time.deltaTime;
                    Vector3 cur = Vector3.Lerp(startPos, doorPos, Mathf.SmoothStep(0f, 1f, t / walkDur));
                    _curbPassenger.UpdateBaseTransform(cur, walkRot);
                    yield return null;
                }

                VfxDirector.Instance?.PlayPickup(doorPos);
                Destroy(_curbPassenger.gameObject);
                _curbPassenger = null;
            }

            VfxDirector.Instance?.HideStopReceiverRings();
            SynthAudio.Instance?.PlayPickup();
            SpawnCabPassenger(ActivePassengerProfile);
            Taxi.SetLeftDoorOpen(false);
            Taxi.SetDoorOpen(false);
            Taxi.TargetLateralX = GameConfig.DriveLaneX;
            Ui.ShowToast(Loc.F("passenger_in", ActivePassengerProfile.DisplayName));

            yield return new WaitForSeconds(0.30f);

            DisabledOptions = new bool[4];
            TotalTimeForPuzzle = Mathf.Max(15f, ActivePuzzle.seconds);
            TimeRemaining = TotalTimeForPuzzle;

            if (ActivePuzzle.kind == PuzzleKind.Bells)
            {
                Taxi.SetExteriorVisible(true);
                BellDiorama.gameObject.SetActive(true);
                BellDiorama.LoadPuzzle(ActivePuzzle);
            }
            else if (ActivePuzzle.kind == PuzzleKind.Bridge)
            {
                Taxi.SetExteriorVisible(true);
                BridgeDiorama.gameObject.SetActive(true);
                BridgeDiorama.LoadPuzzle(ActivePuzzle);
            }
            else if (ActivePuzzle.kind == PuzzleKind.TorchBridge)
            {
                Taxi.SetExteriorVisible(true);
                TorchBridge.gameObject.SetActive(true);
                TorchBridge.LoadPuzzle(ActivePuzzle);
            }
            else if (ActivePuzzle.kind == PuzzleKind.Route)
            {
                Taxi.SetExteriorVisible(true);
                RouteTiles.gameObject.SetActive(true);
                RouteTiles.LoadPuzzle(ActivePuzzle);
            }
            else
            {
                // Turn camera to the back seat (shows dark-green ribbed back seat + yellow window arch)
                Taxi.SetExteriorVisible(false);
            }

            Taxi.TargetSpeed = GameConfig.PuzzleCruiseSpeed;
            State = GameState.PuzzleActive;
            Ui.RefreshForState();
            SynthAudio.Instance?.PlayGuide(GuideCue.QuestionIntro);
            // A short nonverbal cue accompanies the question; jaw timing follows its actual pitch.
            if (_cabPassenger != null)
            {
                if (SynthAudio.Instance != null)
                    _cabPassenger.Speak(SynthAudio.Instance.PlayPassengerVoice(CurrentStop % 5, 0.65f));
            }
        }

        public void SubmitChoiceAnswer(int optionIndex)
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null) return;
            if (ActivePuzzle.kind == PuzzleKind.Bells)
                BellDiorama.RingBellOnce(optionIndex + 1);

            if (optionIndex == ActivePuzzle.correct)
            {
                OnPuzzleSolved();
            }
            else
            {
                DisabledOptions[Mathf.Clamp(optionIndex, 0, 3)] = true;
                StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
            }
        }

        public void SubmitBridgeAttempt()
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null) return;
            if (BridgeDiorama.CurrentWeightSum == ActivePuzzle.answerValue)
                OnPuzzleSolved();
            else
                StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
        }

        public void SubmitTorchBridgeMove()
        {
            if (State != GameState.PuzzleActive || ActivePuzzle?.kind != PuzzleKind.TorchBridge
                || TorchBridge == null || !TorchBridge.CanCross) return;
            StartCoroutine(TorchBridgeMoveRoutine());
        }

        IEnumerator TorchBridgeMoveRoutine()
        {
            yield return TorchBridge.CrossSelected();
            if (State != GameState.PuzzleActive || ActivePuzzle?.kind != PuzzleKind.TorchBridge) yield break;
            Ui.RefreshTorchBridgeUi();
            if (TorchBridge.ElapsedMinutes > ActivePuzzle.answerValue)
                StartCoroutine(PassengerDisembarkUnpaidRoutine(false));
            else if (TorchBridge.IsComplete)
                OnPuzzleSolved();
        }

        void OnPuzzleSolved()
        {
            LastAnswerWasCorrect = true;
            _disembarkStage = 0;
            SynthAudio.Instance?.PlayCorrect();
            VfxDirector.Instance?.PlaySuccess(Taxi.transform.position);

            // Row 4.3: Passenger reacts with Satisfied cheering smile & stays in the back seat until the next stop!
            if (_cabPassenger != null)
                _cabPassenger.SetPose(PassengerPose.Satisfied, _cabPassenger.transform.localPosition, Quaternion.identity);

            // Switch camera outside behind the accelerating yellow taxi with golden road trails, spinning 3D coins & chevron path (media_1792765036993_fce7d4e5.jpg)
            BellDiorama.gameObject.SetActive(false);
            BridgeDiorama.gameObject.SetActive(false);
            TorchBridge.gameObject.SetActive(false);
            RouteTiles.gameObject.SetActive(false);
            Taxi.SetExteriorVisible(true);
            Taxi.TargetLateralX = 0f;
            VfxDirector.Instance?.SetAccelerationCelebrationActive(true);

            // Immediately snap camera position outside behind the accelerating taxi so it never flies through passenger mesh
            if (_cam != null && Taxi != null)
            {
                _cam.transform.position = Taxi.transform.position + new Vector3(0f, 2.15f, -5.15f);
                _camVel = Vector3.zero;
            }
            SetPassengerLiveCamActive(true);

            float timeRatio = Mathf.Clamp01(TimeRemaining / Mathf.Max(1f, TotalTimeForPuzzle));
            int timeBonus = Mathf.RoundToInt(timeRatio * GameConfig.MaxTimeBonus);
            int reward = GameConfig.BaseReward + timeBonus + Mathf.Min(GameConfig.MaxStreakBonus, SaveSystem.Data.streak);

            var data = SaveSystem.Data;
            data.coins += reward;
            data.streak++;
            if (data.streak > data.bestStreak) data.bestStreak = data.streak;
            data.totalCorrect++;
            int solvedStop = data.currentStop;
            SaveSystem.RecordDistrictAnswer(ActivePuzzle, solvedStop);
            if (!data.solvedStops.Contains(data.currentStop))
                data.solvedStops.Add(data.currentStop);
            data.currentStop = Mathf.Min(GameConfig.TotalStops + 1, data.currentStop + 1);
            if (solvedStop == GameConfig.TotalStops)
            {
                data.campaignsCompleted++;
                data.campaignComplete = true;
            }
            if (solvedStop % GameConfig.StopsPerDistrict == 0)
            {
                StartCoroutine(CaptureAlbumPhotoSoon((solvedStop - 1) / GameConfig.StopsPerDistrict));
                data.hasPendingDistrictFinale = true;
                data.pendingDistrictFinale = (solvedStop - 1) / GameConfig.StopsPerDistrict;
            }
            SaveSystem.Save();

            Taxi.TriggerBoost(2.6f);
            SynthAudio.Instance?.PlayHorn();   // a cheerful toot as the happy passenger is whisked off
            Taxi.TargetSpeed = Taxi.EffectiveCruiseSpeed;

            State = GameState.PuzzleResult;
            Ui.ShowPuzzleResult(reward, true, false);
            if (data.hasPendingDistrictFinale)
                Ui.ShowDistrictFinale(data.pendingDistrictFinale);
        }

        /// <summary>The district's album photo: the chase camera a moment after the last stop, taxi speeding off.</summary>
        IEnumerator CaptureAlbumPhotoSoon(int district)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            Ui.CaptureDistrictPhoto(district);
        }

        public void ContinueAfterDistrictFinale()
        {
            if (!SaveSystem.Data.hasPendingDistrictFinale) return;
            SaveSystem.Data.hasPendingDistrictFinale = false;
            SaveSystem.Data.pendingDistrictFinale = -1;
            SaveSystem.Save();
            Ui.OpenDistrictFinale(false);
            if (State == GameState.PuzzleResult)
                ContinueToNextStop();
        }

        IEnumerator PassengerDisembarkUnpaidRoutine(bool timedOut)
        {
            if (State != GameState.PuzzleActive) yield break;
            LastAnswerWasCorrect = false;
            State = GameState.PuzzleResult;

            SynthAudio.Instance?.PlayWrong();
            VfxDirector.Instance?.PlayError(Taxi.transform.position);

            var data = SaveSystem.Data;
            data.streak = 0;
            data.totalWrong++;
            SaveSystem.Save();

            Taxi.TriggerSlowdown(2.5f);
            Taxi.TargetSpeed = 0f;

            // ================================================================
            // ROW 3 PANEL 3.1: Seated in dark-green ribbed back seat inside yellow window frame,
            // resting chin on hand with disappointed expression
            // ================================================================
            _disembarkStage = 1;
            BellDiorama.gameObject.SetActive(false);
            BridgeDiorama.gameObject.SetActive(false);
            TorchBridge.gameObject.SetActive(false);
            RouteTiles.gameObject.SetActive(false);
            Taxi.SetExteriorVisible(false);
            if (_cabPassenger != null)
                _cabPassenger.SetPose(PassengerPose.DisappointedInCab, _cabPassenger.transform.localPosition, Quaternion.identity);

            Ui.ShowPuzzleResult(0, false, timedOut);
            yield return new WaitForSeconds(0.35f);

            // ================================================================
            // STAGE 2 (media_1792765107419_582e5035.jpg & Row 3 Panel 3.2):
            // Camera switches to the low rear-left 3/4 street angle; left passenger door opens,
            // 3 glowing cyan rewind triangles (◀◀◀) + S-curve street ribbons appear on the yellow taxi,
            // and the passenger steps out onto the left cobblestone sidewalk
            // ================================================================
            _disembarkStage = 2;
            // pull in to the curb so the passenger steps straight out onto the promenade
            Taxi.TargetLateralX = GameConfig.CurbStopX;
            Taxi.SetExteriorVisible(true);
            Taxi.SetLeftDoorOpen(true);
            VfxDirector.Instance?.SetWrongAnswerRewindActive(true);

            if (_cabPassenger != null)
            {
                Destroy(_cabPassenger.gameObject);
                _cabPassenger = null;
            }

            if (_walkingAwayPassenger != null) Destroy(_walkingAwayPassenger.gameObject);
            var walkGo = GlbModelLoader.InstantiatePassenger(ActivePassengerProfile);
            Vector3 stepOutPos = new Vector3(GameConfig.CurbStopX + GameConfig.SideX(-1.0f), 0.18f, Taxi.DistanceZ - 0.35f);
            Quaternion stepOutRot = Quaternion.Euler(0f, GameConfig.SideYaw(-65f), 0f);
            walkGo.transform.position = stepOutPos;
            walkGo.transform.rotation = stepOutRot;
            _walkingAwayPassenger = walkGo.AddComponent<PassengerActor>();
            _walkingAwayPassenger.Setup(ActivePassengerProfile, PassengerPose.SteppingOutOfDoor);

            yield return new WaitForSeconds(0.35f);

            // ================================================================
            // STAGE 3 (Row 3 Panel 3.3): Standing beside the open door in 3/4 rear profile
            // ================================================================
            _disembarkStage = 3;
            Vector3 standPos = new Vector3(GameConfig.CurbStopX + GameConfig.SideX(-1.4f), 0.16f, Taxi.DistanceZ - 0.65f);
            Quaternion standRot = Quaternion.Euler(0f, GameConfig.SideYaw(18f), 0f);
            if (_walkingAwayPassenger != null)
                _walkingAwayPassenger.SetPose(PassengerPose.StandingSadBesideDoor, standPos, standRot);

            yield return new WaitForSeconds(0.30f);

            // ================================================================
            // STAGE 4: Door closes; passenger walks away along the left sidewalk in 3/4 rear view,
            // carrying their own prop (cane, briefcase, pastry box, ukulele; no suitcase — none of them travels with one).
            // ================================================================
            _disembarkStage = 4;
            Taxi.SetLeftDoorOpen(false);
            Vector3 sidewalkStart = new Vector3(GameConfig.SideX(-3.15f), 0.16f, Taxi.DistanceZ - 1.05f);
            Vector3 sidewalkEnd = new Vector3(GameConfig.SideX(-3.4f), 0.16f, Taxi.DistanceZ + 2.05f);
            Quaternion walkAwayRot = Quaternion.LookRotation(sidewalkEnd - sidewalkStart);
            if (_walkingAwayPassenger != null)
                _walkingAwayPassenger.SetPose(PassengerPose.WalkingAway, sidewalkStart, walkAwayRot);

            float t = 0f;
            while (t < 2.4f && State == GameState.PuzzleResult)
            {
                t += Time.deltaTime;
                if (_walkingAwayPassenger != null)
                {
                    Vector3 p = Vector3.Lerp(sidewalkStart, sidewalkEnd, t / 2.4f);
                    _walkingAwayPassenger.UpdateBaseTransform(p, walkAwayRot);
                }
                yield return null;
            }
        }

        public void UseRewindHint()
        {
            if (State != GameState.PuzzleActive || ActivePuzzle == null) return;
            var data = SaveSystem.Data;
            if (data.coins < GameConfig.HintCost)
            {
                Ui.ShowToast(Loc.T("no_coins"));
                return;
            }

            data.coins -= GameConfig.HintCost;
            SaveSystem.Save();
            SynthAudio.Instance?.PlayHint();
            VfxDirector.Instance?.PlayRewind(Taxi.transform.position);
            TimeRemaining = Mathf.Min(TotalTimeForPuzzle, TimeRemaining + 10f);

            if (ActivePuzzle.kind == PuzzleKind.Bridge)
            {
                BridgeDiorama.ApplyHintMask(ActivePuzzle.correct);
            }
            else if (ActivePuzzle.kind == PuzzleKind.TorchBridge)
            {
                TorchBridge.SelectOptimalHint();
                Ui.RefreshTorchBridgeUi();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Akari)
            {
                Ui.RevealAkariHint();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.DoorCycle)
            {
                Ui.RevealDoorCycleHint();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.ValveCause)
            {
                Ui.RevealValveCauseHint();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Ordering)
            {
                Ui.RevealNextOrderStep();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Route)
            {
                RouteTiles.RevealOneTile();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Letters)
            {
                Ui.RevealNextLetter();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Grid)
            {
                Ui.RevealGridHint();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Compare)
            {
                Ui.RevealCompareHint();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Timing)
            {
                ActivePuzzle.timingTolerance = Mathf.Min(220, ActivePuzzle.timingTolerance + 24);
                Ui.RefreshTimingUi(_timingGateIndex);
                Ui.ShowToast(Loc.T("timing_hint"));
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Valve || ActivePuzzle.kind == PuzzleKind.Lantern)
            {
                Ui.RevealMiniMaskHint();
                return;
            }
            else if (ActivePuzzle.kind == PuzzleKind.Shadow)
            {
                Ui.RevealShadowHint();
                return;
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i != ActivePuzzle.correct && !DisabledOptions[i])
                    {
                        DisabledOptions[i] = true;
                        break;
                    }
                }
                if (ActivePuzzle.kind == PuzzleKind.Bells)
                    BellDiorama.ReplaySequence();
            }

            Ui.RefreshPuzzleOptions();
        }

        public void ContinueToNextStop()
        {
            if (State != GameState.PuzzleResult) return;
            SynthAudio.Instance?.PlayClick();

            // If LastAnswerWasCorrect is true, _cabPassenger STAYS in the car until the next stop!
            if (!LastAnswerWasCorrect && _cabPassenger != null)
            {
                Destroy(_cabPassenger.gameObject);
                _cabPassenger = null;
            }

            if (SaveSystem.Data.currentStop > GameConfig.TotalStops)
            {
                // Journey finished: the save keeps the finished campaign until the player picks a new tour.
                EnterMainMenu();
                Ui.ShowCampaignFinale();
                return;
            }

            ScheduleNextStop();
        }

        // Side-dependent offsets here are authored for the original left-curb layout and mirrored by GameConfig.SideV.
        // Curb-side camera behind the taxi's rear-left corner (clear of promenade trees/lamps at x <= -4.2):
        // waiting passenger in the left third, taxi with its open door in the right third.
        // Camera stands just inside the curb behind the stopped taxi (about 0.6 m from the curb edge)
        static Vector3 StationCamOffset(float pullback) => GameConfig.SideV(new Vector3(CityChunkStreamer.CurbIsOpen ? -0.7f : -0.1f, 1.6f, -7.0f * pullback));
        // (on a building-side pavement the camera keeps to the road edge, clear of the lamps and planters)
        /// <summary>Clamp between two X limits authored for the original left-curb layout.</summary>
        static float ClampSide(float x, float leftMin, float leftMax)
        {
            float a = GameConfig.SideX(leftMin), b = GameConfig.SideX(leftMax);
            return Mathf.Clamp(x, Mathf.Min(a, b), Mathf.Max(a, b));
        }
        static readonly Vector3 StationLookOffset = GameConfig.SideV(new Vector3(-1.3f, 1.2f, 0.6f));

        void UpdateCamera()
        {
            if (_cam == null || Taxi == null) return;
            if (_passengerLiveCam != null && _passengerLiveCam.enabled && _cabPassenger != null)
            {
                _cabPassenger.TryGetPortraitAnchor(out Vector3 liveEyeWorld);
                TaxiCameraFraming.SolveLivePortrait(Taxi.transform.InverseTransformPoint(liveEyeWorld),
                    out var liveCameraLocal, out var liveFocusLocal);
                _passengerLiveCam.transform.localPosition = liveCameraLocal;
                _passengerLiveCam.transform.localRotation = Quaternion.LookRotation(liveFocusLocal - liveCameraLocal);
            }
            float windowAspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
            const float portraitAspect = 9f / 16f;
            if (windowAspect > portraitAspect + 0.01f && _cam.targetTexture == null)
            {
                float width = portraitAspect / windowAspect;
                _cam.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else _cam.rect = new Rect(0f, 0f, 1f, 1f);
            float pullback = Mathf.Clamp(portraitAspect / Mathf.Max(0.38f, Mathf.Min(windowAspect, portraitAspect)), 1f, 1.35f);
            var taxi = Taxi.transform;
            Vector3 tp = taxi.position, targetPos, focus;
            bool freeCam = false, vehicleRelative = true, inCab = false;

            if (State == GameState.FerryCrossing)
            {
                float k = Mathf.SmoothStep(0f, 1f, _ferryT);
                targetPos = Vector3.Lerp(tp + new Vector3(0.8f, 5.2f, -12f * pullback), tp + new Vector3(4.6f, 2.4f, -6.5f * pullback), k)
                            + new Vector3(0f, Mathf.Sin(Time.time * 0.9f) * 0.12f, 0f);
                focus = tp + new Vector3(Mathf.Lerp(-0.6f, -1.8f, k), 0.7f, Mathf.Lerp(10f, 9f, k));
                freeCam = true;
                vehicleRelative = false;
            }
            else if (State == GameState.MainMenu)
            {
                targetPos = tp + new Vector3(-CityChunkStreamer.CurrentSeaSide * 3.1f, 2.1f, 5.8f * pullback);
                focus = tp + new Vector3(0f, 1.05f, 0f);
            }
            else if (State == GameState.PassengerBoarding && IsWaitingForPickupConfirm)
            {
                targetPos = tp + StationCamOffset(pullback);
                focus = tp + StationLookOffset;
            }
            else if (State == GameState.PassengerBoarding && _curbPassenger != null)
            {
                Vector3 mid = Vector3.Lerp(_curbPassenger.transform.position, tp, 0.4f);
                targetPos = mid + GameConfig.SideV(new Vector3(-0.2f, 1.25f, -3.6f * pullback));
                focus = mid + new Vector3(0f, 0.85f, 0f);
            }
            else if (State == GameState.PuzzleResult && LastAnswerWasCorrect)
            {
                targetPos = tp + new Vector3(0f, 2.15f, -5.65f * pullback);
                focus = tp + new Vector3(0f, 1.05f, 12f);
            }
            else if (State == GameState.PuzzleResult && !LastAnswerWasCorrect && _disembarkStage >= 2)
            {
                Vector3 subject = tp + GameConfig.SideV(new Vector3(-0.8f, 0f, 0f));
                if (_walkingAwayPassenger != null) subject = Vector3.Lerp(tp, _walkingAwayPassenger.transform.position, 0.55f);
                targetPos = subject + new Vector3(-GameConfig.CurbSide * 0.5f, 2.6f, -7.2f * pullback);
                focus = subject + new Vector3(0f, -0.1f, 3f);
            }
            else if (State == GameState.PuzzleActive && ActivePuzzle != null && ActivePuzzle.kind == PuzzleKind.Route)
            {
                RouteTileLayout.SizeForDifficulty(ActivePuzzle.difficulty, out int gw, out int gh);
                float halfW = gw * RouteTilePuzzleController.TileSize * 0.5f + 0.15f;
                float halfD = (gh + 2) * RouteTilePuzzleController.TileSize * 0.5f;
                float distance = Mathf.Max(halfW / 0.245f, halfD * 0.85f / 0.40f) * 1.16f;
                Vector3 center = RouteTiles.GridCenterWorld;
                const float elevation = 55f * Mathf.Deg2Rad;
                targetPos = center + new Vector3(0f, distance * Mathf.Sin(elevation), -distance * Mathf.Cos(elevation));
                focus = center + new Vector3(0f, 0f, halfD * 0.32f);
                vehicleRelative = false;
                freeCam = true;
            }
            else if (State == GameState.PuzzleActive && ActivePuzzle != null && ActivePuzzle.kind == PuzzleKind.TorchBridge)
            {
                targetPos = new Vector3(0f, -26.7f, -10f * pullback);
                focus = new Vector3(0f, -46f, 0f);
                vehicleRelative = false;
                freeCam = true;
            }
            else if (State == GameState.PuzzleActive && ActivePuzzle != null && (ActivePuzzle.kind == PuzzleKind.Bells || ActivePuzzle.kind == PuzzleKind.Bridge))
            {
                targetPos = new Vector3(0f, -36.5f, -7.8f * pullback);
                focus = new Vector3(0f, -39.65f, 0f);
                vehicleRelative = false;
                freeCam = true;
            }
            else if ((State == GameState.PuzzleActive || State == GameState.PuzzleResult) && _cabPassenger != null && !Taxi.ExteriorVisible)
            {
                // Use the rig's cached eye anchor; differing passenger heights retain the same composition.
                _cabPassenger.TryGetPortraitAnchor(out Vector3 eyeWorld);
                TaxiCameraFraming.SolveCab(taxi.InverseTransformPoint(eyeWorld), pullback, out var cameraLocal, out var focusLocal);
                targetPos = taxi.TransformPoint(cameraLocal);
                focus = taxi.TransformPoint(focusLocal);
                inCab = true;
            }
            else
            {
                Vector3 drivePos = tp + new Vector3(-CityChunkStreamer.CurrentSeaSide * 0.9f, 3.1f, -8.1f * pullback);
                Vector3 driveLook = tp + new Vector3(CityChunkStreamer.CurrentSeaSide * 0.95f, 0.85f, 11f);
                float distance = _nextStopZ - Taxi.DistanceZ;
                float approach = State == GameState.DrivingToStop && distance < 20f
                    ? Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01((distance - 0.6f) / 19.4f)) : 0f;
                targetPos = Vector3.Lerp(drivePos, tp + StationCamOffset(pullback), approach);
                focus = Vector3.Lerp(driveLook, tp + StationLookOffset, approach);
            }

            if (vehicleRelative && State != GameState.MainMenu)
                targetPos += taxi.up * Taxi.RoadCameraHeave * (inCab ? .35f : .75f);
            if (!freeCam) targetPos.x = ClampSide(targetPos.x, -6.6f + CityChunkStreamer.SideShift, 2.6f);
            if (vehicleRelative)
            {
                // All taxi shots share local smoothing. Vehicle motion cannot drag the camera into the subject.
                if (!_camInCab) { _cabCamLocal = taxi.InverseTransformPoint(_cam.transform.position); _camVel = Vector3.zero; }
                _cabCamLocal = Vector3.SmoothDamp(_cabCamLocal, taxi.InverseTransformPoint(targetPos), ref _camVel, 0.16f);
                _cam.transform.position = taxi.TransformPoint(_cabCamLocal);
            }
            else
            {
                if (_camInCab) _camVel = Vector3.zero;
                _cam.transform.position = Vector3.SmoothDamp(_cam.transform.position, targetPos, ref _camVel, 0.16f);
            }
            _camInCab = vehicleRelative;
            Vector3 cameraPosition = _cam.transform.position;
            if (!freeCam) cameraPosition.x = ClampSide(cameraPosition.x, -6.8f + CityChunkStreamer.SideShift, 2.8f);
            _cam.transform.position = cameraPosition;
            // Aim from the actual smoothed position, keeping the taxi/passenger framed throughout transitions.
            Vector3 toFocus = focus - cameraPosition;
            if (toFocus.sqrMagnitude > 0.001f)
            {
                Quaternion aimed = Quaternion.LookRotation(toFocus);
                if (vehicleRelative && State != GameState.MainMenu)
                    aimed *= Quaternion.AngleAxis(Taxi.RoadCameraRoll * (inCab ? .35f : .75f), Vector3.forward);
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, aimed, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            float fov = inCab ? TaxiCameraFraming.CabFieldOfView : State == GameState.DrivingToStop ? Mathf.Lerp(48f, 51f, Mathf.Clamp01(Taxi.Speed / 22f)) : 48f;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }
    }
}

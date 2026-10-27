using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    /// <summary>
    /// Builds and drives the Portrait UI screens (05-interface.png + 08-animation.png):
    /// 1. Weather Post-Processing Screen Filter Overlay (Sunny, Golden Sunset, Misty Morning, Rain lens droplets)
    /// 2. Main Menu (ODDS CAB logo, 512 DURAK pill, yellow OYNA button, dark-blue AYARLAR button)
    /// 3. Top Driving HUD bar + Tappable Live Weather Badge + Bottom Coastal Weather Badge
    /// 4. Question / Miniature "Probability Shift" Card + 4 Passenger Expression Close-Up Badges
    ///    (Curious, Thoughtful, Satisfied, Disappointed)
    /// 5. Settings Modal (AYARLAR, Müzik & Efektler sliders, Türkçe / English radio toggle, Graphics tier selector, OYUNA DÖN button)
    /// </summary>
    public partial class UiDirector : MonoBehaviour
    {
        static readonly Color ColNavy = new Color(0.035f, 0.085f, 0.14f, 0.98f);
        static readonly Color ColNavySoft = new Color(0.09f, 0.19f, 0.27f, 0.97f);
        static readonly Color ColYellow = new Color(1f, 0.78f, 0.12f, 1f);
        static readonly Color ColCream = new Color(1f, 0.94f, 0.82f, 1f);
        static readonly Color ColTeal = new Color(0.17f, 0.80f, 0.76f, 1f);
        static readonly Color ColTextDark = new Color(0.025f, 0.07f, 0.12f, 1f);
        static readonly Color ColRed = new Color(0.88f, 0.28f, 0.28f, 1f);
        static readonly Color ColGreen = new Color(0.22f, 0.74f, 0.44f, 1f);
        static readonly Color[] GridRegionColors =
        {
            new Color(0.30f, 0.78f, 0.69f), new Color(0.96f, 0.75f, 0.18f),
            new Color(0.48f, 0.72f, 0.92f), new Color(0.94f, 0.56f, 0.42f),
            new Color(0.72f, 0.62f, 0.88f)
        };

        GameManager _gm;
        CanvasScaler _scaler;
        RectTransform _portraitRoot;

        Image _weatherFilterOverlay;
        GameObject _menuPanel;
        GameObject _hudBar;
        GameObject _coastalBadgeObj;
        GameObject _stationArrivalCard;
        GameObject _puzzlePanel;
        GameObject _settingsModal;
        GameObject _toastBox;

        // Station Arrival ("YOLCUYU AL / ▶ DEVAM ET") refs
        UiText _stationArrivalTitleText;
        UiText _stationArrivalBtnText;
        Sprite _mapPinSprite;
        Sprite _speedoGaugeSprite;
        Sprite _stopwatchSprite;
        Sprite _driverHandSprite;

        // Menu refs (media_1792765197031_224281b2.jpg)
        UiText _menuTitleLine1Shadow;
        UiText _menuTitleLine1;
        UiText _menuTitleLine2Shadow;
        UiText _menuTitleLine2;
        UiText _menuStopsBadgeText;
        Image _menuProgressFill;
        readonly Image[] _menuRoutePinPortraits = new Image[5];
        UiText _menuNextStopHeaderText;
        UiText _menuPlayBtnText;
        UiText _menuSettingsBtnText;
        UiText _menuDistrictSubText;
        Sprite _maidenTowerPostcardSprite;
        Image _menuPostcardImg;
        RawImage _pipLiveRawImage;
        GameObject _photoModal;
        GameObject _chatModal;
        GameObject _journeyMapModal;
        GameObject _districtFinaleModal;
        GameObject _taxiShopModal;
        GameObject _puzzleModal;
        UiText _chatTitleText;
        UiText _chatSubText;
        UiText _chatSpeakerText;
        UiText _chatDialogueText;
        UiText _chatChapterText;
        UiText _chatNextButtonText;
        UiText _journeyMapTitleText;
        UiText _journeyMapSubText;
        UiText _journeyMapButtonText;
        UiText _districtFinaleTitleText;
        UiText _districtFinaleSubText;
        UiText _districtFinaleHeadingText;
        UiText _districtFinaleContinueText;
        UnityEngine.UI.ScrollRect _districtFinaleLogScrollRect;
        RectTransform _districtFinaleLogContent;
        UiText _puzzleArchiveStatsText;
        UnityEngine.UI.ScrollRect _puzzleArchiveScrollRect;
        RectTransform _puzzleArchiveContent;
        int _activeDistrictFinale = -1;
        readonly UiText[] _journeyMapDistrictTexts = new UiText[GameConfig.DistrictCount];
        UiText _taxiShopButtonText;
        UiText _taxiShopTitleText;
        UiText _taxiShopSubText;
        UiText _taxiShopCoinsText;
        readonly UiText[] _taxiShopOptionTexts = new UiText[TaxiCosmeticCatalog.Count];
        readonly Image[] _taxiShopSwatches = new Image[TaxiCosmeticCatalog.Count];
        PassengerId _chatPassengerId;
        int _currentChatIndex;

        // HUD & Weather badge refs
        UiText _hudStopText;
        Image _hudCenterIcon;
        RectTransform _hudCenterIconRect;
        UiText _hudSpeedText;
        UiText _hudCoinText;
        UiText _hudWeatherText;
        UiText _coastalBadgeText;
        UiText _toastText;
        float _toastTimer;

        // Route Puzzle ("ROTA BULMACASI / TAKSİYİ HEDEFE GÖTÜR") overlay refs (media_1792765160263_5464a1fc.jpg)
        GameObject _routePuzzleOverlay;
        Image _routePassengerPortraitImg;
        UiText _routeTitleText;
        UiText _routeSubText;
        RectTransform _routeDriverHandRect;

        // Expression close-up strip (Row 4 of 08-animation.png: Curious, Thoughtful, Satisfied, Disappointed)
        GameObject _expressionStrip;
        readonly Image[] _exprChipImages = new Image[4];
        readonly Image[] _exprPortraitImages = new Image[4];
        readonly UiText[] _exprChipTexts = new UiText[4];
        readonly Dictionary<string, Sprite[]> _exprPortraitCache = new Dictionary<string, Sprite[]>();
        string _lastPortraitResourcePath;

        // Puzzle card refs
        UiText _timerText;
        Image _timerFill;
        UiText _categoryBadgeText;
        UiText _promptText;
        GameObject _choiceGrid;
        readonly Button[] _choiceButtons = new Button[4];
        readonly UiText[] _choiceLabels = new UiText[4];
        readonly Image[] _choiceImages = new Image[4];
        readonly Image[] _choiceGlowFrames = new Image[4];
        RectTransform _driverHandRect;
        int _selectedChoiceIndex = -1;
        readonly List<int> _orderTaps = new List<int>();
        UiText _routeHintText;
        GameObject _lettersGroup;
        readonly List<Image> _letterSlotImages = new List<Image>();
        readonly List<UiText> _letterSlotTexts = new List<UiText>();
        readonly List<Button> _letterTileButtons = new List<Button>();
        readonly List<Image> _letterTileImages = new List<Image>();
        readonly List<UiText> _letterTileTexts = new List<UiText>();
        readonly List<int> _letterPicks = new List<int>();
        const int MaxLetterSlots = 7;
        const int MaxLetterTiles = 8;
        GameObject _gridGroup;
        UiText _gridStatusText;
        Button _gridClearButton;
        UiText _gridClearText;
        Button _gridSubmitButton;
        UiText _gridSubmitText;
        readonly List<Button> _gridCellButtons = new List<Button>();
        readonly List<Image> _gridCellImages = new List<Image>();
        readonly List<UiText> _gridCellTexts = new List<UiText>();
        int[] _gridWorkingOwners = System.Array.Empty<int>();
        int _gridSelectedClue = -1;
        int _gridFirstCorner = -1;
        GameObject _compareGroup;
        UiText _compareBeforeTitle;
        UiText _compareAfterTitle;
        readonly List<GameObject> _compareBeforeCells = new List<GameObject>();
        readonly List<UiText> _compareBeforeLabels = new List<UiText>();
        readonly List<Button> _compareAfterButtons = new List<Button>();
        readonly List<Image> _compareAfterImages = new List<Image>();
        readonly List<UiText> _compareAfterLabels = new List<UiText>();
        bool _compareHintRevealed;
        GameObject _timingGroup;
        UiText _timingProgressText;
        RectTransform _timingTrackRect;
        RectTransform _timingTargetRect;
        RectTransform _timingMarkerRect;
        Button _timingStopButton;
        UiText _timingStopText;
        float _timingMarkerPosition;
        int _timingDisplayedGate;
        GameObject _miniGroup;
        UiText _miniStatusText;
        readonly Button[] _miniButtons = new Button[4];
        readonly Image[] _miniButtonImages = new Image[4];
        readonly UiText[] _miniButtonLabels = new UiText[4];
        Button _miniSubmitButton;
        UiText _miniSubmitText;
        int _miniSelectedMask;
        bool _shadowHintRevealed;
        GameObject _orderResetRow;
        UiText _orderResetText;
        GameObject _bellExtraRow;
        UiText _bellReplayBtnText;
        GameObject _bridgeGroup;
        readonly List<Button> _weightButtons = new List<Button>();
        readonly List<UiText> _weightLabels = new List<UiText>();
        readonly List<Image> _weightImages = new List<Image>();
        UiText _bridgeStatusText;
        UiText _bridgeLiftBtnText;
        UiText _bridgeClearBtnText;
        GameObject _torchBridgeGroup;
        UiText _torchBridgeStatusText;
        readonly Button[] _torchTravelerButtons = new Button[4];
        readonly Image[] _torchTravelerImages = new Image[4];
        readonly UiText[] _torchTravelerLabels = new UiText[4];
        UiText _torchClearText;
        Button _torchCrossButton;
        UiText _torchCrossText;
        Button _rewindBtn;
        UiText _rewindBtnText;

        // Result overlay & Top-Right Picture-in-Picture (PiP) Passenger Reaction (media_1792765036993_fce7d4e5.jpg & media_1792765107419_582e5035.jpg)
        GameObject _resultBox;
        UiText _resultTitleShadow;
        UiText _resultTitleText;
        UiText _wrongSubLabel;
        GameObject _wrongCrossBadgeObj;
        RectTransform _stopPillRect;
        GameObject _rewardPillObj;
        UiText _resultSubStatusText;
        UiText _resultExplainText;
        Image _resultContinueBtnImg;
        GameObject _retryIconObj;
        UiText _resultContinueText;
        GameObject _confettiGroup;
        GameObject _explainCard;
        UiText _explainTitleText;
        UiText _explainBodyText;
        GameObject _pipReactionCard;
        Image _pipPortraitImage;
        GameObject _pipExcitementRays;
        Sprite _stackedCoinsSprite;
        Sprite _wrongCrossBadgeSprite;
        Sprite _retryArrowSprite;

        // Settings refs
        UiText _settingsTitleText;
        UiText _musicLabelText;
        UiText _sfxLabelText;
        Slider _musicSlider;
        Slider _sfxSlider;
        Image _trRadioDot;
        Image _enRadioDot;
        UiText _graphicsLabelText;
        UiText _spokenGuideText;
        Image _spokenGuideImage;
        readonly Image[] _tierBtnImages = new Image[5];
        readonly UiText[] _tierBtnTexts = new UiText[5];
        UiText _backToGameBtnText;

        public void Initialize(GameManager gm)
        {
            _gm = gm;

            EnsureEventSystem();
            UiText.EnsureSymbolFallbacks();
            if (UiText.FullSymbolFont != null)
                UiText.FullSymbolFont.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÇĞİÖŞÜçğıöşü0123456789:/.?!%+- ·♪");
            var canvas = CreateCanvas();
            var rootGo = MakePanel(canvas.transform, "PortraitSafeArea", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rootGo.GetComponent<Image>().raycastTarget = false;
            _portraitRoot = rootGo.GetComponent<RectTransform>();
            UpdatePortraitSafeArea();

            _mapPinSprite = CreateMapPinSprite();
            _speedoGaugeSprite = CreateSpeedoGaugeSprite();
            _stopwatchSprite = CreateStopwatchSprite();
            _driverHandSprite = CreateDriverHandSprite();
            _stackedCoinsSprite = CreateStackedCoinsSprite();
            _wrongCrossBadgeSprite = CreateWrongCrossBadgeSprite();
            _retryArrowSprite = CreateRetryArrowSprite();
            _maidenTowerPostcardSprite = CreateMaidensTowerPostcardSprite();

            BuildWeatherPostProcessOverlay(_portraitRoot);
            BuildHudBar(_portraitRoot);
            BuildCoastalWeatherBadge(_portraitRoot);
            BuildStationArrivalCard(_portraitRoot);
            BuildMainMenu(_portraitRoot);
            BuildPuzzlePanel(_portraitRoot);
            BuildRoutePuzzleOverlay(_portraitRoot);
            BuildPostAnswerResultAndPip(_portraitRoot);
            BuildSettingsModal(_portraitRoot);
            BuildJourneyMapModal(_portraitRoot);
            BuildDistrictFinaleModal(_portraitRoot);
            BuildTaxiShopModal(_portraitRoot);
            BuildPhotoModal(_portraitRoot);
            BuildChatModal(_portraitRoot);
            BuildPuzzleModal(_portraitRoot);
            BuildFerryBanner(_portraitRoot);
            BuildToast(_portraitRoot);
            SetupUiTweens();
            ApplyDisplayStyles();
            BuildQuitConfirm(_portraitRoot);
            ApplyReferencePresentation();
            ApplyMobilePresentation();
            BuildDistrictLoading(_portraitRoot);

            Loc.Changed += RefreshAllLocalizedText;
            RefreshAllLocalizedText();
            RefreshWeatherDisplay();
            RefreshForState();
        }

        void OnDestroy()
        {
            Loc.Changed -= RefreshAllLocalizedText;
            ReleaseReferencePresentation();
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        Canvas CreateCanvas()
        {
            var go = new GameObject("OddsCab_Canvas");
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            // Overlay, not camera space: the weather Volume (fog grading, vignette, bloom) and rain streaks in front of
            // the camera must only touch the 3D scene, never wash out or warp the HUD and puzzle cards.
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;

            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1080f, 1920f);
            _scaler.matchWidthOrHeight = 0f;

            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        void BuildWeatherPostProcessOverlay(Transform root)
        {
            var overlayGo = MakePanel(root, "WeatherPostProcessOverlay", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _weatherFilterOverlay = overlayGo.GetComponent<Image>();
            _weatherFilterOverlay.raycastTarget = false;
            // Disabled: a full-screen tinted image washed out the whole 3D scene (taxi included).
            // Weather grading now comes from a URP Volume driven by WeatherDirector.
            overlayGo.SetActive(false);
        }

        // ---------------------------------------------------------------- 1. Main Menu (media_1792765197031_224281b2.jpg)

        void BuildMainMenu(Transform root)
        {
            _menuPanel = MakePanel(root, "MainMenuPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _menuPanel.GetComponent<Image>().raycastTarget = false;

            // 1. Top-Left 3D Layered Logo: "512 DURAKLIK" (Cream-White) + "YOLCULUK" (Taxi-Yellow) + Checkered Taxi Pattern
            _menuTitleLine1Shadow = MakeLabel(_menuPanel.transform, "TitleLine1Shadow", "512 DURAKLIK", 78, FontStyle.Bold, ColNavy, TextAnchor.MiddleLeft);
            SetRect(_menuTitleLine1Shadow.rectTransform, new Vector2(0.42f, 0.925f), new Vector2(0.42f, 0.925f), new Vector2(5f, -7f), new Vector2(740f, 100f));
            _menuTitleLine1Shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);

            _menuTitleLine1 = MakeLabel(_menuPanel.transform, "TitleLine1", "512 DURAKLIK", 78, FontStyle.Bold, ColCream, TextAnchor.MiddleLeft);
            SetRect(_menuTitleLine1.rectTransform, new Vector2(0.42f, 0.925f), new Vector2(0.42f, 0.925f), Vector2.zero, new Vector2(740f, 100f));
            _menuTitleLine1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);

            _menuTitleLine2Shadow = MakeLabel(_menuPanel.transform, "TitleLine2Shadow", "YOLCULUK", 88, FontStyle.Bold, ColNavy, TextAnchor.MiddleLeft);
            SetRect(_menuTitleLine2Shadow.rectTransform, new Vector2(0.38f, 0.872f), new Vector2(0.38f, 0.872f), new Vector2(6f, -8f), new Vector2(660f, 110f));
            _menuTitleLine2Shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);

            _menuTitleLine2 = MakeLabel(_menuPanel.transform, "TitleLine2", "YOLCULUK", 88, FontStyle.Bold, ColYellow, TextAnchor.MiddleLeft);
            SetRect(_menuTitleLine2.rectTransform, new Vector2(0.38f, 0.872f), new Vector2(0.38f, 0.872f), Vector2.zero, new Vector2(660f, 110f));
            _menuTitleLine2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);

            // Poster logo (reference): Lilita One, cream "512 DURAKLIK" over a bigger taxi-yellow "YOLCULUK", both with a
            // thick navy outline and a deep drop shadow (the shadow labels above are only the fallback without the font)
            var titleMat = DisplayMaterial(new Color(0.06f, 0.11f, 0.22f), 0.28f, new Color(0.02f, 0.05f, 0.11f, 0.85f), new Vector2(0.45f, -0.85f), 0.08f);
            if (titleMat != null)
            {
                _menuTitleLine1Shadow.gameObject.SetActive(false);
                _menuTitleLine2Shadow.gameObject.SetActive(false);
                StyleDisplay(_menuTitleLine1, new Color(1f, 0.99f, 0.95f), new Color(0.95f, 0.88f, 0.74f), titleMat);
                StyleDisplay(_menuTitleLine2, new Color(1f, 0.90f, 0.25f), new Color(0.98f, 0.62f, 0.08f), titleMat);
                _menuTitleLine1.fontSize = 92;
                _menuTitleLine2.fontSize = 112;
                SetRect(_menuTitleLine1.rectTransform, new Vector2(0.44f, 0.928f), new Vector2(0.44f, 0.928f), Vector2.zero, new Vector2(820f, 110f));
                SetRect(_menuTitleLine2.rectTransform, new Vector2(0.40f, 0.868f), new Vector2(0.40f, 0.868f), Vector2.zero, new Vector2(760f, 130f));
            }

            // Little chequered flag after "YOLCULUK": navy and yellow squares tilted with the title
            var checkerGroup = new GameObject("TitleCheckerPattern", typeof(RectTransform));
            checkerGroup.transform.SetParent(_menuPanel.transform, false);
            SetRect(checkerGroup.GetComponent<RectTransform>(), new Vector2(0.64f, 0.874f), new Vector2(0.64f, 0.874f), Vector2.zero, new Vector2(76f, 76f));
            checkerGroup.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -8f);
            MakePanel(checkerGroup.transform, "ChkBorder", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            for (int cy = 0; cy < 3; cy++)
                for (int cx = 0; cx < 3; cx++)
                    MakePanel(checkerGroup.transform, $"Chk_{cx}_{cy}", (cx + cy) % 2 == 0 ? ColYellow : ColCream, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2((cx - 1f) * 23f, (cy - 1f) * 23f), new Vector2(22f, 22f));

            // Compact top-right Settings Gear button so player can access Settings from Main Menu
            var setBtn = MakeButton(_menuPanel.transform, "SettingsButton", ColNavy, new Vector2(0.92f, 0.945f), new Vector2(96f, 96f), () => OpenSettings(true));
            _menuSettingsBtnText = MakeLabel(setBtn.transform, "Label", "", 44, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            MakeIcon(setBtn.transform, "Gear", GearSprite, new Vector2(56f, 56f), Color.white);
            Stretch(_menuSettingsBtnText.rectTransform);

            // 2. Top-Left Progress Pill ("📍 130 / 512" + Turquoise Progress Bar)
            var badge = MakePanel(_menuPanel.transform, "StopsProgressPill", ColNavy, new Vector2(0.295f, 0.808f), new Vector2(0.295f, 0.808f), Vector2.zero, new Vector2(510f, 94f));
            var badgePin = MakePanel(badge.transform, "YellowPinIcon", ColYellow, new Vector2(0.095f, 0.5f), new Vector2(0.095f, 0.5f), Vector2.zero, new Vector2(44f, 54f));
            badgePin.GetComponent<Image>().sprite = _mapPinSprite;

            _menuStopsBadgeText = MakeLabel(badge.transform, "StopsText", "130 / 512", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_menuStopsBadgeText.rectTransform, new Vector2(0.48f, 0.64f), new Vector2(0.48f, 0.64f), Vector2.zero, new Vector2(320f, 48f));

            var progTrack = MakePanel(badge.transform, "ProgressTrack", ColNavySoft, new Vector2(0.54f, 0.27f), new Vector2(0.54f, 0.27f), Vector2.zero, new Vector2(376f, 18f));
            var progFillGo = MakePanel(progTrack.transform, "ProgressFill", ColTeal, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _menuProgressFill = progFillGo.GetComponent<Image>();
            _menuProgressFill.type = Image.Type.Simple;
            SetProgressFill(0f);

            var mapBtn = MakeButton(_menuPanel.transform, "JourneyMapButton", ColNavy, new Vector2(0.70f, 0.808f), new Vector2(176f, 86f), () => OpenJourneyMap(true));
            _journeyMapButtonText = MakeLabel(mapBtn.transform, "Txt", Loc.T("journey_map_button"), 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_journeyMapButtonText.rectTransform);
            var garageBtn = MakeButton(_menuPanel.transform, "TaxiGarageButton", ColNavy, new Vector2(0.86f, 0.808f), new Vector2(150f, 86f), () => OpenTaxiShop(true));
            _taxiShopButtonText = MakeLabel(garageBtn.transform, "Txt", Loc.T("taxi_shop_button"), 25, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_taxiShopButtonText.rectTransform);

            // 3. Winding route Kız Kulesi -> Ortaköy over the street and the hill, taxi at the current stop
            // The route remains available in its dedicated map screen. Keep the hero street unobstructed.

            // 4. Bottom "SONRAKİ DURAK" (NEXT STOP) Card with Postcard & 3 Interactive Activity Buttons
            var nextStopCard = MakePanel(_menuPanel.transform, "NextStopCard", ColCream, new Vector2(0.5f, 0.172f), new Vector2(0.5f, 0.172f), Vector2.zero, new Vector2(960f, 224f));

            // Left Raised Postcard Photo Frame (Dynamically bound to upcoming district landmark)
            var photoFrame = MakePanel(nextStopCard.transform, "PostcardFrame", Color.white, new Vector2(0.245f, 0.54f), new Vector2(0.245f, 0.54f), Vector2.zero, new Vector2(416f, 244f));
            var photoImg = MakePanel(photoFrame.transform, "PostcardImg", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(396f, 224f));
            _menuPostcardImg = photoImg.GetComponent<Image>();
            _menuPostcardImg.sprite = _maidenTowerPostcardSprite;

            // Right Header: "SONRAKİ DURAK"
            _menuNextStopHeaderText = MakeLabel(nextStopCard.transform, "NextStopHeader", "SONRAKİ DURAK", 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleLeft);
            SetRect(_menuNextStopHeaderText.rectTransform, new Vector2(0.695f, 0.76f), new Vector2(0.695f, 0.76f), Vector2.zero, new Vector2(460f, 48f));

            // Middle Line of Right Side: Dark-Navy/Yellow Map-Pin Icon + Dynamic Landmark/Passenger
            var kizPinOuter = MakePanel(nextStopCard.transform, "LandmarkPinOuter", ColTextDark, new Vector2(0.485f, 0.46f), new Vector2(0.485f, 0.46f), Vector2.zero, new Vector2(36f, 44f));
            kizPinOuter.GetComponent<Image>().sprite = _mapPinSprite;
            var kizPinInner = MakePanel(kizPinOuter.transform, "LandmarkPinInner", ColYellow, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(24f, 28f));
            kizPinInner.GetComponent<Image>().sprite = _mapPinSprite;

            _menuDistrictSubText = MakeLabel(nextStopCard.transform, "LandmarkName", "KIZ KULESİ", 26, FontStyle.Bold, ColTextDark, TextAnchor.MiddleLeft);
            SetRect(_menuDistrictSubText.rectTransform, new Vector2(0.725f, 0.46f), new Vector2(0.725f, 0.46f), Vector2.zero, new Vector2(390f, 44f));

            // Bottom-Right 3 Interactive Taxi Activity Buttons (Blue Camera 📷, Green Chat 💬, Purple Puzzle 🧩)
            var fotoBtn = MakeButton(nextStopCard.transform, "ActBtn_Foto", new Color(0.20f, 0.62f, 0.94f, 1f), new Vector2(0.560f, 0.18f), new Vector2(104f, 38f), () => OpenPhotoModal(true));
            var fotoTxt = MakeLabel(fotoBtn.transform, "Txt", "FOTO", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(fotoTxt.rectTransform);

            var sohbetBtn = MakeButton(nextStopCard.transform, "ActBtn_Sohbet", new Color(0.24f, 0.78f, 0.56f, 1f), new Vector2(0.700f, 0.18f), new Vector2(116f, 38f), () => OpenChatModal(true));
            var sohbetTxt = MakeLabel(sohbetBtn.transform, "Txt", "SOHBET", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(sohbetTxt.rectTransform);

            var bulmacaBtn = MakeButton(nextStopCard.transform, "ActBtn_Bulmaca", new Color(0.58f, 0.36f, 0.88f, 1f), new Vector2(0.855f, 0.18f), new Vector2(126f, 38f), () => OpenPuzzleModal(true));
            var bulmacaTxt = MakeLabel(bulmacaBtn.transform, "Txt", Loc.T("puzzle_archive_button"), 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(bulmacaTxt.rectTransform);

            // 5. Bottom Full-Width Taxi-Yellow "▶ YOLA ÇIK" Pill Button
            var playBtn = MakeButton(_menuPanel.transform, "PlayButton", ColYellow, new Vector2(0.5f, 0.055f), new Vector2(960f, 134f), () => _gm.StartOrContinueDriving());
            _menuPlayBtnText = MakeLabel(playBtn.transform, "Label", "►   YOLA ÇIK", 52, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_menuPlayBtnText.rectTransform);
        }

        // ---------------------------------------------------------------- 2. Top Driving HUD Bar + Weather Badges

        void BuildHudBar(Transform root)
        {
            _hudBar = MakePanel(root, "HudBar", Color.clear, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, 195f));
            _hudBar.GetComponent<Image>().raycastTarget = false;

            var pill = MakePanel(_hudBar.transform, "TopPill", ColNavy, new Vector2(0.43f, 0.68f), new Vector2(0.43f, 0.68f), Vector2.zero, new Vector2(820f, 108f));

            // Left compartment: Yellow map-pin icon + "128 / 512"
            var stopPin = MakePanel(pill.transform, "StopPinIcon", ColYellow, new Vector2(0.068f, 0.5f), new Vector2(0.068f, 0.5f), Vector2.zero, new Vector2(36f, 44f));
            stopPin.GetComponent<Image>().sprite = _mapPinSprite;
            _hudStopText = MakeLabel(pill.transform, "StopCounter", "1 / 512", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_hudStopText.rectTransform, new Vector2(0.225f, 0.5f), new Vector2(0.225f, 0.5f), Vector2.zero, new Vector2(210f, 76f));

            // Vertical divider 1
            MakePanel(pill.transform, "Divider1", new Color(1f, 1f, 1f, 0.16f), new Vector2(0.365f, 0.5f), new Vector2(0.365f, 0.5f), Vector2.zero, new Vector2(2f, 58f));

            // Center compartment: Turquoise speedometer gauge arc above "0 km/sa" (or Stopwatch + "00:25" during Route Puzzle)
            var speedoIcon = MakePanel(pill.transform, "SpeedoIcon", ColTeal, new Vector2(0.515f, 0.69f), new Vector2(0.515f, 0.69f), Vector2.zero, new Vector2(68f, 36f));
            _hudCenterIcon = speedoIcon.GetComponent<Image>();
            _hudCenterIcon.sprite = _speedoGaugeSprite;
            _hudCenterIconRect = speedoIcon.GetComponent<RectTransform>();
            _hudSpeedText = MakeLabel(pill.transform, "SpeedText", "0 km/sa", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(_hudSpeedText.rectTransform, new Vector2(0.515f, 0.30f), new Vector2(0.515f, 0.30f), Vector2.zero, new Vector2(230f, 52f));

            // Vertical divider 2
            MakePanel(pill.transform, "Divider2", new Color(1f, 1f, 1f, 0.16f), new Vector2(0.665f, 0.5f), new Vector2(0.665f, 0.5f), Vector2.zero, new Vector2(2f, 58f));

            // Right compartment: Gold coin badge with "$" + coin count
            var coinBadge = MakePanel(pill.transform, "CoinBadge", ColYellow, new Vector2(0.735f, 0.5f), new Vector2(0.735f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            _hudCoinBadge = coinBadge.GetComponent<RectTransform>();
            var coinDollar = MakeLabel(coinBadge.transform, "DollarSign", "$", 28, FontStyle.Bold, new Color(0.58f, 0.36f, 0.04f, 1f), TextAnchor.MiddleCenter);
            Stretch(coinDollar.rectTransform);

            _hudCoinText = MakeLabel(pill.transform, "CoinText", "60", 36, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_hudCoinText.rectTransform, new Vector2(0.865f, 0.5f), new Vector2(0.865f, 0.5f), Vector2.zero, new Vector2(150f, 76f));

            var pauseBtn = MakeButton(_hudBar.transform, "PauseBtn", ColNavy, new Vector2(0.90f, 0.68f), new Vector2(108f, 108f), () => OpenSettings(true));
            var pauseLbl = MakeLabel(pauseBtn.transform, "II", "II", 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(pauseLbl.rectTransform);

            // Top HUD Live Weather Badge (tap to cycle & preview all 4 weather post-processing filters)
            var weatherBtn = MakeButton(_hudBar.transform, "TopWeatherBadge", ColNavySoft, new Vector2(0.43f, 0.12f), new Vector2(440f, 52f),
                () => _gm.Weather?.CycleWeatherPreview());
            _hudWeatherText = MakeLabel(weatherBtn.transform, "WeatherLabel", "Güneşli", 25, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
            Stretch(_hudWeatherText.rectTransform);
        }

        void BuildCoastalWeatherBadge(Transform root)
        {
            // Bottom Coastal Weather Badge during driving (tap to preview all 4 weather filters)
            var btn = MakeButton(root, "BottomCoastalWeatherBadge", ColNavy, new Vector2(0.5f, 0.055f), new Vector2(880f, 72f),
                () => _gm.Weather?.CycleWeatherPreview());
            _coastalBadgeObj = btn.gameObject;
            _coastalBadgeText = MakeLabel(btn.transform, "CoastalText", "", 27, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            Stretch(_coastalBadgeText.rectTransform);
        }

        void BuildStationArrivalCard(Transform root)
        {
            // Exact Station Arrival Bottom Card (matching Station Arrival reference image):
            // Dark-navy rounded card with turquoise map-pin icon + "YOLCUYU AL" and warm cream "▶ DEVAM ET" button!
            _stationArrivalCard = MakePanel(root, "StationArrivalCard", ColNavy, new Vector2(0.5f, 0.115f), new Vector2(0.5f, 0.115f), Vector2.zero, new Vector2(920f, 288f));

            var pinIcon = MakePanel(_stationArrivalCard.transform, "TurquoisePinIcon", ColTeal, new Vector2(0.265f, 0.72f), new Vector2(0.265f, 0.72f), Vector2.zero, new Vector2(56f, 68f));
            pinIcon.GetComponent<Image>().sprite = _mapPinSprite;

            _stationArrivalTitleText = MakeLabel(_stationArrivalCard.transform, "PickupTitle", "YOLCUYU AL", 48, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_stationArrivalTitleText.rectTransform, new Vector2(0.60f, 0.72f), new Vector2(0.60f, 0.72f), Vector2.zero, new Vector2(540f, 80f));

            var continueBtn = MakeButton(_stationArrivalCard.transform, "PickupContinueBtn", ColCream, new Vector2(0.5f, 0.28f), new Vector2(836f, 126f),
                () => _gm.ConfirmPassengerPickup());
            _stationArrivalBtnText = MakeLabel(continueBtn.transform, "Label", "►   DEVAM ET", 44, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_stationArrivalBtnText.rectTransform);

            _stationArrivalCard.SetActive(false);
        }

        // ---------------------------------------------------------------- 3. Question Card + 4 Expression Close-Up Badges

        void BuildPuzzlePanel(Transform root)
        {
            // Question Card positioned in the lower-middle of the portrait screen (media_1792764991705_654c83ca.jpg),
            // leaving the upper half (y = 0.55..0.90) completely clear for the passenger in the back seat & Bosphorus rear window!
            // An opaque card cleanly crops the seated passenger at chest level.
            _puzzlePanel = MakePanel(root, "PuzzleCard", new Color(ColNavy.r, ColNavy.g, ColNavy.b, 1f), new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(910f, 800f));

            // 4 Small Expression Close-Up Portraits docked neatly at the bottom edge of the screen (Row 4 of 08-animation.png)
            _expressionStrip = MakePanel(_puzzlePanel.transform, "ExpressionCloseUpStrip", ColNavySoft, new Vector2(0.5f, -0.095f), new Vector2(0.5f, -0.095f), Vector2.zero, new Vector2(910f, 108f));
            for (int i = 0; i < 4; i++)
            {
                float x = 0.132f + i * 0.245f;
                var chip = MakePanel(_expressionStrip.transform, "ExprChip_" + i, ColNavy, new Vector2(x, 0.5f), new Vector2(x, 0.5f), Vector2.zero, new Vector2(214f, 94f));
                _exprChipImages[i] = chip.GetComponent<Image>();

                var portraitFrame = MakePanel(chip.transform, "PortraitFrame", ColCream, new Vector2(0.24f, 0.5f), new Vector2(0.24f, 0.5f), Vector2.zero, new Vector2(82f, 82f));
                var portraitImgGo = MakePanel(portraitFrame.transform, "PortraitImg", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
                _exprPortraitImages[i] = portraitImgGo.GetComponent<Image>();

                _exprChipTexts[i] = MakeLabel(chip.transform, "Label", "", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
                SetRect(_exprChipTexts[i].rectTransform, new Vector2(0.71f, 0.5f), new Vector2(0.71f, 0.5f), Vector2.zero, new Vector2(108f, 80f));
            }
            _expressionStrip.SetActive(false); // Hidden: question card matches clean reference image!

            // Turquoise Stopwatch Icon + Timer text + rounded teal progress bar (matching top of Question Card in reference image)
            var swIcon = MakePanel(_puzzlePanel.transform, "StopwatchIcon", ColTeal, new Vector2(0.095f, 0.905f), new Vector2(0.095f, 0.905f), Vector2.zero, new Vector2(54f, 62f));
            swIcon.GetComponent<Image>().sprite = _stopwatchSprite;

            _timerText = MakeLabel(_puzzlePanel.transform, "TimerText", "00:25", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_timerText.rectTransform, new Vector2(0.25f, 0.905f), new Vector2(0.25f, 0.905f), Vector2.zero, new Vector2(200f, 54f));

            var barBg = MakePanel(_puzzlePanel.transform, "TimerBarBg", ColNavySoft, new Vector2(0.64f, 0.905f), new Vector2(0.64f, 0.905f), Vector2.zero, new Vector2(510f, 24f));
            var fillGo = MakePanel(barBg.transform, "TimerBarFill", ColTeal, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _timerFill = fillGo.GetComponent<Image>();
            _timerFill.type = Image.Type.Filled;
            _timerFill.fillMethod = Image.FillMethod.Horizontal;
            _timerFill.fillAmount = 1f;

            // Subtle Category & "Probability Shift" header pill
            _categoryBadgeText = MakeLabel(_puzzlePanel.transform, "CategoryBadge", "", 22, FontStyle.Bold, new Color(0.24f, 0.78f, 0.72f, 0.85f), TextAnchor.MiddleCenter);
            SetRect(_categoryBadgeText.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(860f, 34f));

            // Question prompt ("Bir saatte kaç dakika vardır?")
            _promptText = MakeLabel(_puzzlePanel.transform, "Prompt", "Bir saatte kaç dakika vardır?", 44, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            SetRect(_promptText.rectTransform, new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), Vector2.zero, new Vector2(840f, 190f));

            // 2x2 Cream option buttons with warm-white glowing selection border
            _choiceGrid = MakePanel(_puzzlePanel.transform, "ChoiceGrid", Color.clear, new Vector2(0.5f, 0.31f), new Vector2(0.5f, 0.31f), Vector2.zero, new Vector2(840f, 340f));
            Vector2[] anchors =
            {
                new Vector2(0.25f, 0.74f),
                new Vector2(0.75f, 0.74f),
                new Vector2(0.25f, 0.24f),
                new Vector2(0.75f, 0.24f),
            };
            for (int i = 0; i < 4; i++)
            {
                int captured = i;
                var glowGo = MakePanel(_choiceGrid.transform, "OptionGlow_" + i, new Color(1f, 0.98f, 0.90f, 0f), anchors[i], anchors[i], Vector2.zero, new Vector2(418f, 162f));
                glowGo.GetComponent<Image>().raycastTarget = false;
                _choiceGlowFrames[i] = glowGo.GetComponent<Image>();

                var btn = MakeButton(_choiceGrid.transform, "Option_" + i, ColCream, anchors[i], new Vector2(400f, 146f),
                    () => OnOptionButtonClicked(captured));
                _choiceButtons[i] = btn;
                _choiceImages[i] = btn.GetComponent<Image>();
                _choiceLabels[i] = MakeLabel(btn.transform, "Label", "", 42, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(_choiceLabels[i].rectTransform, 16f);
            }

            // Bell miniature replay button
            _bellExtraRow = MakePanel(_puzzlePanel.transform, "BellRow", Color.clear, new Vector2(0.27f, 0.055f), new Vector2(0.27f, 0.055f), Vector2.zero, new Vector2(420f, 74f));
            var replayBtn = MakeButton(_bellExtraRow.transform, "ReplayBellBtn", ColNavySoft, new Vector2(0.5f, 0.5f), new Vector2(400f, 70f),
                () => _gm.BellDiorama.ReplaySequence());
            _bellReplayBtnText = MakeLabel(replayBtn.transform, "Label", "♪ TEKRAR DİNLE", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_bellReplayBtnText.rectTransform);

            // Ordering puzzles: reset the taps (same slot as the bell replay row; only one is ever shown)
            _orderResetRow = MakePanel(_puzzlePanel.transform, "OrderResetRow", Color.clear, new Vector2(0.27f, 0.055f), new Vector2(0.27f, 0.055f), Vector2.zero, new Vector2(420f, 74f));
            var resetBtn = MakeButton(_orderResetRow.transform, "OrderResetBtn", ColNavySoft, new Vector2(0.5f, 0.5f), new Vector2(400f, 70f),
                OnSecondaryRowClicked);
            _orderResetText = MakeLabel(resetBtn.transform, "Label", "SIFIRLA", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_orderResetText.rectTransform);
            _orderResetRow.SetActive(false);

            // Letter tiles (F26): answer slots on top, tappable tiles in two rows of four below.
            _lettersGroup = MakePanel(_puzzlePanel.transform, "LettersGroup", Color.clear, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(860f, 380f));
            _lettersGroup.GetComponent<Image>().raycastTarget = false;
            for (int i = 0; i < MaxLetterSlots; i++)
            {
                var slot = MakePanel(_lettersGroup.transform, "Slot_" + i, ColNavySoft, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(104f, 96f));
                slot.GetComponent<Image>().raycastTarget = false;
                _letterSlotImages.Add(slot.GetComponent<Image>());
                var t = MakeLabel(slot.transform, "Letter", "", 52, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform);
                _letterSlotTexts.Add(t);
            }
            for (int i = 0; i < MaxLetterTiles; i++)
            {
                int captured = i;
                float x = 0.125f + (i % 4) * 0.25f;
                float y = i < 4 ? 0.50f : 0.17f;
                var btn = MakeButton(_lettersGroup.transform, "Tile_" + i, ColCream, new Vector2(x, y), new Vector2(190f, 112f),
                    () => OnLetterTileClicked(captured));
                _letterTileButtons.Add(btn);
                _letterTileImages.Add(btn.GetComponent<Image>());
                var t = MakeLabel(btn.transform, "Letter", "", 56, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform);
                _letterTileTexts.Add(t);
            }
            _lettersGroup.SetActive(false);

            // Shikaku small grid: select a clue, then tap two opposite corners of its rectangle.
            _gridGroup = MakePanel(_puzzlePanel.transform, "ShikakuGrid", Color.clear, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(850f, 430f));
            _gridGroup.GetComponent<Image>().raycastTarget = false;
            _gridStatusText = MakeLabel(_gridGroup.transform, "GridStatus", "", 23, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_gridStatusText.rectTransform, new Vector2(0.5f, 0.96f), new Vector2(0.5f, 0.96f), Vector2.zero, new Vector2(820f, 42f));
            for (int i = 0; i < 25; i++)
            {
                int captured = i;
                var btn = MakeButton(_gridGroup.transform, "GridCell_" + i, ColCream, new Vector2(0.5f, 0.5f), new Vector2(140f, 72f),
                    () => OnGridCellClicked(captured));
                _gridCellButtons.Add(btn);
                _gridCellImages.Add(btn.GetComponent<Image>());
                var label = MakeLabel(btn.transform, "Label", "", 30, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
                _gridCellTexts.Add(label);
            }
            _gridGroup.SetActive(false);

            // Before/after observation task: static BEFORE and tappable AFTER snapshots.
            _compareGroup = MakePanel(_puzzlePanel.transform, "BeforeAfterGroup", Color.clear, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(850f, 430f));
            _compareGroup.GetComponent<Image>().raycastTarget = false;
            _compareBeforeTitle = MakeLabel(_compareGroup.transform, "BeforeTitle", "BEFORE", 23, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_compareBeforeTitle.rectTransform, new Vector2(0.27f, 0.92f), new Vector2(0.27f, 0.92f), Vector2.zero, new Vector2(360f, 40f));
            _compareAfterTitle = MakeLabel(_compareGroup.transform, "AfterTitle", "AFTER", 23, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_compareAfterTitle.rectTransform, new Vector2(0.73f, 0.92f), new Vector2(0.73f, 0.92f), Vector2.zero, new Vector2(380f, 40f));
            for (int i = 0; i < 16; i++)
            {
                int captured = i;
                var beforeCell = MakePanel(_compareGroup.transform, "BeforeCell_" + i, ColNavySoft, new Vector2(0.27f, 0.5f), new Vector2(0.27f, 0.5f), Vector2.zero, new Vector2(66f, 62f));
                _compareBeforeCells.Add(beforeCell);
                var beforeLabel = MakeLabel(beforeCell.transform, "Label", "", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(beforeLabel.rectTransform);
                _compareBeforeLabels.Add(beforeLabel);

                var afterButton = MakeButton(_compareGroup.transform, "AfterCell_" + i, ColCream, new Vector2(0.73f, 0.5f), new Vector2(66f, 62f),
                    () => _gm.SubmitCompareAttempt(captured));
                _compareAfterButtons.Add(afterButton);
                _compareAfterImages.Add(afterButton.GetComponent<Image>());
                var afterLabel = MakeLabel(afterButton.transform, "Label", "", 24, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(afterLabel.rectTransform);
                _compareAfterLabels.Add(afterLabel);
            }
            _compareGroup.SetActive(false);

            // Timing gates (F24/O34): stop a sweeping marker inside each successive target window.
            _timingGroup = MakePanel(_puzzlePanel.transform, "TimingGateGroup", Color.clear, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(850f, 360f));
            _timingGroup.GetComponent<Image>().raycastTarget = false;
            _timingProgressText = MakeLabel(_timingGroup.transform, "TimingProgress", "", 28, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_timingProgressText.rectTransform, new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.92f), Vector2.zero, new Vector2(800f, 54f));
            var timingTrack = MakePanel(_timingGroup.transform, "TimingTrack", ColNavySoft, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(720f, 86f));
            _timingTrackRect = timingTrack.GetComponent<RectTransform>();
            var target = MakePanel(timingTrack.transform, "TimingTarget", new Color(0.22f, 0.74f, 0.44f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 78f));
            _timingTargetRect = target.GetComponent<RectTransform>();
            // Match the target's center anchors: both use (normalizedPosition - 0.5) * track width.
            var marker = MakePanel(timingTrack.transform, "TimingMarker", ColYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 74f));
            _timingMarkerRect = marker.GetComponent<RectTransform>();
            _timingStopButton = MakeButton(_puzzlePanel.transform, "TimingStopBtn", ColYellow, new Vector2(0.5f, 0.14f), new Vector2(500f, 90f), StopTimingMarker);
            _timingStopText = MakeLabel(_timingStopButton.transform, "Label", "", 30, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_timingStopText.rectTransform);
            _timingGroup.SetActive(false);
            _timingStopButton.gameObject.SetActive(false);

            // Compact touch miniatures for the water valves, lantern bridge and shadow lamp.
            _miniGroup = MakePanel(_puzzlePanel.transform, "MiniGameGroup", Color.clear, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(850f, 360f));
            _miniGroup.GetComponent<Image>().raycastTarget = false;
            _miniStatusText = MakeLabel(_miniGroup.transform, "MiniStatus", "", 25, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_miniStatusText.rectTransform, new Vector2(0.5f, 0.90f), new Vector2(0.5f, 0.90f), Vector2.zero, new Vector2(820f, 60f));
            for (int i = 0; i < _miniButtons.Length; i++)
            {
                int captured = i;
                var button = MakeButton(_miniGroup.transform, "MiniOption_" + i, ColCream, new Vector2(0.17f + i * 0.22f, 0.50f), new Vector2(140f, 132f),
                    () => OnMiniOptionTapped(captured));
                _miniButtons[i] = button;
                _miniButtonImages[i] = button.GetComponent<Image>();
                _miniButtonLabels[i] = MakeLabel(button.transform, "Label", "", 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(_miniButtonLabels[i].rectTransform);
            }
            _miniSubmitButton = MakeButton(_puzzlePanel.transform, "MiniSubmitBtn", ColYellow, new Vector2(0.5f, 0.14f), new Vector2(500f, 90f), SubmitMiniMask);
            _miniSubmitText = MakeLabel(_miniSubmitButton.transform, "Label", "", 30, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_miniSubmitText.rectTransform);
            _miniGroup.SetActive(false);
            _miniSubmitButton.gameObject.SetActive(false);

            // Bridge miniature counterweight tray
            _bridgeGroup = MakePanel(_puzzlePanel.transform, "BridgeGroup", Color.clear, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(920f, 380f));
            _bridgeStatusText = MakeLabel(_bridgeGroup.transform, "BridgeStatus", "0 / 10 kg", 36, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_bridgeStatusText.rectTransform, new Vector2(0.5f, 0.86f), new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(600f, 54f));

            for (int i = 0; i < 6; i++)
            {
                int captured = i;
                float x = 0.12f + i * 0.152f;
                var wBtn = MakeButton(_bridgeGroup.transform, "Weight_" + i, ColCream, new Vector2(x, 0.54f), new Vector2(128f, 128f),
                    () =>
                    {
                        _gm.BridgeDiorama.ToggleWeight(captured);
                        RefreshBridgeUi();
                    });
                _weightButtons.Add(wBtn);
                _weightImages.Add(wBtn.GetComponent<Image>());
                var wLbl = MakeLabel(wBtn.transform, "Label", "", 32, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(wLbl.rectTransform);
                _weightLabels.Add(wLbl);
            }

            var clearBtn = MakeButton(_bridgeGroup.transform, "ClearBtn", ColNavySoft, new Vector2(0.28f, 0.16f), new Vector2(340f, 96f),
                () =>
                {
                    _gm.BridgeDiorama.ClearWeights();
                    RefreshBridgeUi();
                });
            _bridgeClearBtnText = MakeLabel(clearBtn.transform, "Label", "TEMİZLE", 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_bridgeClearBtnText.rectTransform);

            var liftBtn = MakeButton(_bridgeGroup.transform, "LiftBtn", ColYellow, new Vector2(0.70f, 0.16f), new Vector2(420f, 96f),
                () => _gm.SubmitBridgeAttempt());
            _bridgeLiftBtnText = MakeLabel(liftBtn.transform, "Label", "KÖPRÜYÜ KALDIR", 32, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_bridgeLiftBtnText.rectTransform);

            // O31: choose one or two travelers from the torch's bank, then animate their crossing.
            _torchBridgeGroup = MakePanel(_puzzlePanel.transform, "TorchBridgeGroup", Color.clear,
                new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(920f, 390f));
            _torchBridgeGroup.GetComponent<Image>().raycastTarget = false;
            _torchBridgeStatusText = MakeLabel(_torchBridgeGroup.transform, "TorchBridgeStatus", "", 29, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_torchBridgeStatusText.rectTransform, new Vector2(0.5f, 0.91f), new Vector2(0.5f, 0.91f), Vector2.zero, new Vector2(880f, 58f));
            for (int i = 0; i < _torchTravelerButtons.Length; i++)
            {
                int captured = i;
                float x = 0.14f + i * 0.24f;
                var travelerButton = MakeButton(_torchBridgeGroup.transform, "Traveler_" + (i + 1), ColCream,
                    new Vector2(x, 0.58f), new Vector2(154f, 126f), () => ToggleTorchTraveler(captured));
                _torchTravelerButtons[i] = travelerButton;
                _torchTravelerImages[i] = travelerButton.GetComponent<Image>();
                _torchTravelerLabels[i] = MakeLabel(travelerButton.transform, "CrossingTime", "", 30, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(_torchTravelerLabels[i].rectTransform);
            }
            var torchClearButton = MakeButton(_torchBridgeGroup.transform, "TorchClearBtn", ColNavySoft,
                new Vector2(0.27f, 0.16f), new Vector2(330f, 86f), ClearTorchSelection);
            _torchClearText = MakeLabel(torchClearButton.transform, "Label", "", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_torchClearText.rectTransform);
            _torchCrossButton = MakeButton(_torchBridgeGroup.transform, "TorchCrossBtn", ColYellow,
                new Vector2(0.72f, 0.16f), new Vector2(450f, 86f), () => _gm.SubmitTorchBridgeMove());
            _torchCrossText = MakeLabel(_torchCrossButton.transform, "Label", "", 27, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_torchCrossText.rectTransform);
            _torchBridgeGroup.SetActive(false);

            // Rewind Hint button
            _rewindBtn = MakeButton(_puzzlePanel.transform, "RewindBtn", ColNavySoft, new Vector2(0.73f, 0.055f), new Vector2(400f, 70f),
                () => _gm.UseRewindHint());
            _rewindBtnText = MakeLabel(_rewindBtn.transform, "Label", "İpucu  -20", 28, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            Stretch(_rewindBtnText.rectTransform);

            _gridClearButton = MakeButton(_puzzlePanel.transform, "GridClearBtn", ColNavySoft, new Vector2(0.5f, 0.055f), new Vector2(250f, 68f), ClearGrid);
            _gridClearText = MakeLabel(_gridClearButton.transform, "Label", "", 25, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_gridClearText.rectTransform);
            _gridSubmitButton = MakeButton(_puzzlePanel.transform, "GridSubmitBtn", ColYellow, new Vector2(0.83f, 0.055f), new Vector2(250f, 68f), SubmitGrid);
            _gridSubmitText = MakeLabel(_gridSubmitButton.transform, "Label", "", 25, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_gridSubmitText.rectTransform);
            _gridClearButton.gameObject.SetActive(false);
            _gridSubmitButton.gameObject.SetActive(false);
        }

        void BuildRoutePuzzleOverlay(Transform root)
        {
            // Full-Screen Portrait Overlay for the 3D Istanbul Coastal Route Puzzle ("ROTA BULMACASI / TAKSİYİ HEDEFE GÖTÜR",
            // matching media_1792765160263_5464a1fc.jpg):
            // - Top dark-navy banner with rounded back-seat Passenger Portrait on the left + "ROTA BULMACASI" & "📍 TAKSİYİ HEDEFE GÖTÜR" pill on the right
            // - Interactive touch hotspots over the 3D route branches + Driver's Hand & Index Finger at the bottom guiding the yellow taxi!
            _routePuzzleOverlay = MakePanel(root, "RoutePuzzleOverlay", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _routePuzzleOverlay.GetComponent<Image>().raycastTarget = false;

            // 1. Upper Banner Card ("ROTA BULMACASI / 📍 TAKSİYİ HEDEFE GÖTÜR")
            var banner = MakePanel(_routePuzzleOverlay.transform, "RouteBannerCard", ColNavy, new Vector2(0.5f, 0.805f), new Vector2(0.5f, 0.805f), Vector2.zero, new Vector2(960f, 250f));

            // Left rounded square portrait frame showing the Passenger seated in the taxi back seat
            var portraitBorder = MakePanel(banner.transform, "PassengerPortraitBorder", new Color(0.28f, 0.22f, 0.18f, 1f), new Vector2(0.145f, 0.5f), new Vector2(0.145f, 0.5f), Vector2.zero, new Vector2(218f, 218f));
            var portraitBg = MakePanel(portraitBorder.transform, "BackSeatBg", new Color(0.22f, 0.16f, 0.12f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(206f, 206f));
            // Subtle Bosphorus window glimpse on the right side of the passenger portrait
            MakePanel(portraitBg.transform, "WindowGlimpse", new Color(0.42f, 0.72f, 0.92f, 0.55f), new Vector2(0.86f, 0.55f), new Vector2(0.86f, 0.55f), Vector2.zero, new Vector2(46f, 110f));
            var portraitImgGo = MakePanel(portraitBg.transform, "PassengerPortraitImg", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(202f, 202f));
            _routePassengerPortraitImg = portraitImgGo.GetComponent<Image>();

            // Right top headline: "ROTA BULMACASI"
            _routeTitleText = MakeLabel(banner.transform, "RouteTitle", "ROTA BULMACASI", 52, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_routeTitleText.rectTransform, new Vector2(0.615f, 0.69f), new Vector2(0.615f, 0.69f), Vector2.zero, new Vector2(640f, 84f));

            // Right bottom pill: Turquoise Map-Pin icon + "TAKSİYİ HEDEFE GÖTÜR"
            var subPillBorder = MakePanel(banner.transform, "RouteSubPillBorder", new Color(0.24f, 0.38f, 0.52f, 0.85f), new Vector2(0.615f, 0.30f), new Vector2(0.615f, 0.30f), Vector2.zero, new Vector2(640f, 88f));
            var subPill = MakePanel(subPillBorder.transform, "RouteSubPill", ColNavySoft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(632f, 80f));
            var pinIcon = MakePanel(subPill.transform, "TurquoisePin", ColTeal, new Vector2(0.088f, 0.5f), new Vector2(0.088f, 0.5f), Vector2.zero, new Vector2(44f, 54f));
            pinIcon.GetComponent<Image>().sprite = _mapPinSprite;
            _routeSubText = MakeLabel(subPill.transform, "RouteSubText", "TAKSİYİ HEDEFE GÖTÜR", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_routeSubText.rectTransform, new Vector2(0.56f, 0.5f), new Vector2(0.56f, 0.5f), Vector2.zero, new Vector2(510f, 68f));

            // 2. Tiles are tapped directly in 3D (EventSystem + PhysicsRaycaster). The old invisible full-screen
            //    buttons that solved the puzzle on any tap are gone. Only a hint button sits at the bottom.
            var routeHintBtn = MakeButton(_routePuzzleOverlay.transform, "RouteHintBtn", ColNavySoft, new Vector2(0.5f, 0.045f), new Vector2(460f, 96f),
                () => _gm.UseRewindHint());
            _routeHintText = MakeLabel(routeHintBtn.transform, "Label", "İpucu  -20", 32, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            Stretch(_routeHintText.rectTransform);

            _routePuzzleOverlay.SetActive(false);
        }

        void BuildPostAnswerResultAndPip(Transform root)
        {
            // 1. Top-Right Picture-in-Picture (PiP) Passenger Reaction Window (media_1792765036993_fce7d4e5.jpg)
            _pipReactionCard = MakePanel(root, "PipPassengerReactionCard", Color.white, new Vector2(0.79f, 0.785f), new Vector2(0.79f, 0.785f), Vector2.zero, new Vector2(356f, 356f));
            var pipInner = MakePanel(_pipReactionCard.transform, "PipInnerBg", new Color(0.18f, 0.14f, 0.12f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(336f, 336f));
            var pipImgGo = MakePanel(pipInner.transform, "PipPortraitImg", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 330f));
            _pipPortraitImage = pipImgGo.GetComponent<Image>();

            // Live Real-Time 3D Passenger Camera Feed Window (replaces static portrait with real-time 3D character)
            var liveRawGo = new GameObject("PipLiveRawImage", typeof(RectTransform));
            liveRawGo.transform.SetParent(pipInner.transform, false);
            Stretch(liveRawGo.GetComponent<RectTransform>());
            _pipLiveRawImage = liveRawGo.AddComponent<RawImage>();
            _pipLiveRawImage.color = Color.white;

            // "● CANLI" (LIVE) Camera Badge
            var liveBadge = MakePanel(_pipReactionCard.transform, "LiveBadge", new Color(0.85f, 0.16f, 0.20f, 0.95f), new Vector2(0.28f, 0.92f), new Vector2(0.28f, 0.92f), Vector2.zero, new Vector2(96f, 30f));
            var liveLbl = MakeLabel(liveBadge.transform, "LiveLbl", "● CANLI", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(liveLbl.rectTransform);

            // 3 Bright Yellow Excitement Rays (\ | /) on upper-left of passenger's head inside PiP window
            _pipExcitementRays = new GameObject("PipExcitementRays", typeof(RectTransform));
            _pipExcitementRays.transform.SetParent(pipInner.transform, false);
            SetRect(_pipExcitementRays.GetComponent<RectTransform>(), new Vector2(0.16f, 0.76f), new Vector2(0.16f, 0.76f), Vector2.zero, new Vector2(80f, 80f));
            float[] rayAngles = { 28f, 5f, -18f };
            Vector2[] rayOffsets = { new Vector2(-6f, 22f), new Vector2(-12f, 0f), new Vector2(-6f, -22f) };
            for (int r = 0; r < 3; r++)
            {
                var ray = MakePanel(_pipExcitementRays.transform, "Ray_" + r, ColYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), rayOffsets[r], new Vector2(32f, 9f));
                ray.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, rayAngles[r]);
            }
            _pipReactionCard.SetActive(false);

            // 2. Bottom Post-Answer Result Card ("DOĞRU CEVAP!", "129 / 512 DURAK", "+25 JETON", "▶ DEVAM ET")
            _resultBox = MakePanel(root, "PostAnswerResultCard", ColNavy, new Vector2(0.5f, 0.195f), new Vector2(0.5f, 0.195f), Vector2.zero, new Vector2(940f, 610f));

            // Festive Gold Sparkles & Turquoise/Gold Confetti around the top title
            _confettiGroup = new GameObject("ConfettiGroup", typeof(RectTransform));
            _confettiGroup.transform.SetParent(_resultBox.transform, false);
            Stretch(_confettiGroup.GetComponent<RectTransform>());
            Vector2[] confPos =
            {
                new Vector2(-410f, 220f), new Vector2(-370f, 255f), new Vector2(-425f, 150f), new Vector2(-380f, 95f),
                new Vector2(405f, 225f), new Vector2(365f, 260f), new Vector2(420f, 155f), new Vector2(385f, 95f),
                new Vector2(-355f, 35f), new Vector2(360f, 35f)
            };
            for (int c = 0; c < confPos.Length; c++)
            {
                Color col = (c % 2 == 0) ? ColYellow : ColTeal;
                var chip = MakePanel(_confettiGroup.transform, "Confetti_" + c, col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), confPos[c], new Vector2(24f, 11f));
                chip.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -35f + c * 28f);
            }

            // 3D Layered Banner Headline ("DOĞRU CEVAP!" or "YANLIŞ CEVAP")
            _resultTitleShadow = MakeLabel(_resultBox.transform, "ResultTitleShadow", "DOĞRU CEVAP!", 64, FontStyle.Bold, new Color(0.12f, 0.62f, 0.52f, 1f), TextAnchor.MiddleCenter);
            SetRect(_resultTitleShadow.rectTransform, new Vector2(0.5f, 0.835f), new Vector2(0.5f, 0.835f), new Vector2(0f, -7f), new Vector2(880f, 88f));

            _resultTitleText = MakeLabel(_resultBox.transform, "ResultTitle", "DOĞRU CEVAP!", 64, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
            SetRect(_resultTitleText.rectTransform, new Vector2(0.5f, 0.835f), new Vector2(0.5f, 0.835f), Vector2.zero, new Vector2(880f, 88f));

            // Wrong Answer Red Cross Badge (⊗) on the left + "YOLCU İNDİ" turquoise subtitle on the right (media_1792765107419_582e5035.jpg)
            _wrongCrossBadgeObj = MakePanel(_resultBox.transform, "WrongCrossBadge", Color.white, new Vector2(0.175f, 0.785f), new Vector2(0.175f, 0.785f), Vector2.zero, new Vector2(148f, 148f));
            _wrongCrossBadgeObj.GetComponent<Image>().sprite = _wrongCrossBadgeSprite;
            _wrongCrossBadgeObj.SetActive(false);

            _wrongSubLabel = MakeLabel(_resultBox.transform, "WrongSubLabel", "YOLCU İNDİ", 42, FontStyle.Bold, ColTeal, TextAnchor.MiddleLeft);
            SetRect(_wrongSubLabel.rectTransform, new Vector2(0.61f, 0.715f), new Vector2(0.61f, 0.715f), Vector2.zero, new Vector2(560f, 64f));
            _wrongSubLabel.gameObject.SetActive(false);

            // Row 1 Pill: Yellow Map-Pin Icon + "129 / 512 DURAK" (or "128 / 512 DURAĞA DÖN")
            var stopPill = MakePanel(_resultBox.transform, "StopPill", ColNavySoft, new Vector2(0.5f, 0.625f), new Vector2(0.5f, 0.625f), Vector2.zero, new Vector2(680f, 84f));
            _stopPillRect = stopPill.GetComponent<RectTransform>();
            var stopPin = MakePanel(stopPill.transform, "StopPin", ColYellow, new Vector2(0.16f, 0.5f), new Vector2(0.16f, 0.5f), Vector2.zero, new Vector2(42f, 52f));
            stopPin.GetComponent<Image>().sprite = _mapPinSprite;
            _resultSubStatusText = MakeLabel(stopPill.transform, "ResultSubStatus", "129 / 512 DURAK", 36, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_resultSubStatusText.rectTransform, new Vector2(0.59f, 0.5f), new Vector2(0.59f, 0.5f), Vector2.zero, new Vector2(480f, 68f));

            // Row 2 Pill: Stacked Gold "$" Coins Icon + "+25 JETON"
            _rewardPillObj = MakePanel(_resultBox.transform, "RewardPill", ColNavySoft, new Vector2(0.5f, 0.435f), new Vector2(0.5f, 0.435f), Vector2.zero, new Vector2(650f, 94f));
            var coinsImg = MakePanel(_rewardPillObj.transform, "StackedCoinsIcon", Color.white, new Vector2(0.20f, 0.5f), new Vector2(0.20f, 0.5f), Vector2.zero, new Vector2(112f, 82f));
            coinsImg.GetComponent<Image>().sprite = _stackedCoinsSprite;
            _rewardCoinsIcon = coinsImg.GetComponent<RectTransform>();
            _resultExplainText = MakeLabel(_rewardPillObj.transform, "ResultExplain", "+25 JETON", 44, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_resultExplainText.rectTransform, new Vector2(0.63f, 0.5f), new Vector2(0.63f, 0.5f), Vector2.zero, new Vector2(420f, 76f));

            // Bottom Golden-Yellow "▶ DEVAM ET" / Cream-Gold "↺ TEKRAR DENE" Button
            var nextBtn = MakeButton(_resultBox.transform, "ContinueBtn", ColYellow, new Vector2(0.5f, 0.175f), new Vector2(840f, 126f),
                () => _gm.ContinueToNextStop());
            _resultContinueBtnImg = nextBtn.GetComponent<Image>();
            _retryIconObj = MakePanel(nextBtn.transform, "RetryArrowIcon", ColTextDark, new Vector2(0.27f, 0.5f), new Vector2(0.27f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
            _retryIconObj.GetComponent<Image>().sprite = _retryArrowSprite;
            _retryIconObj.SetActive(false);

            _resultContinueText = MakeLabel(nextBtn.transform, "Label", "►   DEVAM ET", 44, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_resultContinueText.rectTransform);

            // "NEDEN?" explanation card, sits just above the result card; shown only after a correct answer
            _explainCard = MakePanel(_resultBox.transform, "ExplainCard", new Color(0.06f, 0.13f, 0.21f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 122f), new Vector2(940f, 220f));
            _explainCard.GetComponent<Image>().raycastTarget = false;
            _explainTitleText = MakeLabel(_explainCard.transform, "ExplainTitle", "NEDEN?", 34, FontStyle.Bold, ColTeal, TextAnchor.UpperLeft);
            SetRect(_explainTitleText.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(860f, 46f));
            _explainBodyText = MakeLabel(_explainCard.transform, "ExplainBody", "", 38, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
            SetRect(_explainBodyText.rectTransform, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(860f, 140f));
            _explainBodyText.resizeTextForBestFit = true;
            _explainBodyText.resizeTextMinSize = 24;
            _explainBodyText.resizeTextMaxSize = 38;
            _explainBodyText.verticalOverflow = VerticalWrapMode.Truncate;
            _explainCard.SetActive(false);

            _resultBox.SetActive(false);
        }

        // ---------------------------------------------------------------- 4. Settings Modal (05-interface.png Panel 4)

        void BuildSettingsModal(Transform root)
        {
            var backdrop = MakePanel(root, "SettingsModal", new Color(0.02f, 0.05f, 0.09f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _settingsModal = backdrop;

            var card = MakePanel(backdrop.transform, "SettingsCard", new Color(0.06f, 0.13f, 0.21f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 1280f));

            _settingsTitleText = MakeLabel(card.transform, "Title", "AYARLAR", 56, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_settingsTitleText.rectTransform, new Vector2(0.5f, 0.90f), new Vector2(0.5f, 0.90f), Vector2.zero, new Vector2(760f, 80f));

            // Music slider
            _musicLabelText = MakeLabel(card.transform, "MusicLabel", "♪  Müzik", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_musicLabelText.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(760f, 54f));
            _musicSlider = MakeSlider(card.transform, "MusicSlider", new Vector2(0.5f, 0.72f), new Vector2(760f, 44f), SaveSystem.Data.musicVolume, v =>
            {
                SaveSystem.Data.musicVolume = v;
                SaveSystem.Save();
                SynthAudio.Instance?.ApplyVolumes();
            });

            // Effects slider
            _sfxLabelText = MakeLabel(card.transform, "SfxLabel", "♫  Efektler", 38, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_sfxLabelText.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(0.5f, 0.61f), Vector2.zero, new Vector2(760f, 54f));
            _sfxSlider = MakeSlider(card.transform, "SfxSlider", new Vector2(0.5f, 0.55f), new Vector2(760f, 44f), SaveSystem.Data.sfxVolume, v =>
            {
                SaveSystem.Data.sfxVolume = v;
                SaveSystem.Save();
                SynthAudio.Instance?.ApplyVolumes();
            });

            var guideButton = MakeButton(card.transform, "SpokenGuide", ColTeal, new Vector2(0.5f, 0.477f), new Vector2(760f, 54f), () =>
            {
                SaveSystem.Data.spokenGuideEnabled = !SaveSystem.Data.spokenGuideEnabled;
                SynthAudio.Instance?.SetSpokenGuideEnabled(SaveSystem.Data.spokenGuideEnabled);
                SaveSystem.Save();
                RefreshSettingsUi();
            });
            _spokenGuideImage = guideButton.GetComponent<Image>();
            _spokenGuideText = MakeLabel(guideButton.transform, "Label", "", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_spokenGuideText.rectTransform);

            // Language radio rows (Türkçe / English)
            var trRow = MakeButton(card.transform, "LangTR", ColNavySoft, new Vector2(0.5f, 0.40f), new Vector2(760f, 78f), () => SetLanguage(Language.TR));
            _trRadioDot = MakePanel(trRow.transform, "Dot", ColTeal, new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.5f), Vector2.zero, new Vector2(34f, 34f)).GetComponent<Image>();
            var trLbl = MakeLabel(trRow.transform, "Label", "Türkçe", 36, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(trLbl.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), Vector2.zero, new Vector2(580f, 60f));

            var enRow = MakeButton(card.transform, "LangEN", ColNavySoft, new Vector2(0.5f, 0.32f), new Vector2(760f, 78f), () => SetLanguage(Language.EN));
            _enRadioDot = MakePanel(enRow.transform, "Dot", ColTeal, new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.5f), Vector2.zero, new Vector2(34f, 34f)).GetComponent<Image>();
            var enLbl = MakeLabel(enRow.transform, "Label", "English", 36, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(enLbl.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), Vector2.zero, new Vector2(580f, 60f));

            // Persisted device quality selector, including the explicit Ultra request.
            _graphicsLabelText = MakeLabel(card.transform, "GraphicsLabel", "Grafik", 34, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            SetRect(_graphicsLabelText.rectTransform, new Vector2(0.5f, 0.235f), new Vector2(0.5f, 0.235f), Vector2.zero, new Vector2(760f, 48f));

            int[] tierValues = { -1, 0, 1, 2, 3 };
            for (int i = 0; i < 5; i++)
            {
                int tierVal = tierValues[i];
                float x = 0.14f + i * 0.18f;
                var qBtn = MakeButton(card.transform, "Tier_" + i, ColNavySoft, new Vector2(x, 0.175f), new Vector2(148f, 68f), () =>
                {
                    SaveSystem.Data.qualityTier = tierVal;
                    SaveSystem.Save();
                    DeviceTierDetector.ApplyFromSave();
                    RefreshSettingsUi();
                });
                _tierBtnImages[i] = qBtn.GetComponent<Image>();
                _tierBtnTexts[i] = MakeLabel(qBtn.transform, "Label", "", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(_tierBtnTexts[i].rectTransform);
            }

            // Back to game button (teal pill matching 05-interface.png Panel 4)
            var backBtn = MakeButton(card.transform, "BackBtn", ColTeal, new Vector2(0.5f, 0.065f), new Vector2(760f, 108f), () => OpenSettings(false));
            _backToGameBtnText = MakeLabel(backBtn.transform, "Label", "←  OYUNA DÖN", 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_backToGameBtnText.rectTransform);

            _settingsModal.SetActive(false);
        }

        void BuildToast(Transform root)
        {
            _toastBox = MakePanel(root, "Toast", ColNavy, new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.84f), Vector2.zero, new Vector2(820f, 80f));
            _toastText = MakeLabel(_toastBox.transform, "Text", "", 32, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            Stretch(_toastText.rectTransform);
            _toastBox.SetActive(false);
        }

        // ---------------------------------------------------------------- Interactive Taxi Activity Modals (Foto / Sohbet / Bulmaca)

        void BuildPhotoModal(Transform root)
        {
            var backdrop = MakePanel(root, "PhotoModalBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _photoModal = backdrop;

            var card = MakePanel(backdrop.transform, "PhotoCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1340f));

            var title = MakeLabel(card.transform, "Title", "TAKSİ FOTOĞRAF ALBÜMÜ", 48, FontStyle.Bold, ColYellow, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f), Vector2.zero, new Vector2(820f, 64f));

            var sub = MakeLabel(card.transform, "Sub", "Her semtin son durağında yolcun bir hatıra fotoğrafı çeker", 24, FontStyle.Normal, ColCream, TextAnchor.MiddleLeft);
            SetRect(sub.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(820f, 44f));

            BuildPhotoAlbumGrid(card.transform);

            var closeBtn = MakeButton(card.transform, "CloseBtn", ColYellow, new Vector2(0.5f, 0.055f), new Vector2(820f, 96f), () => OpenPhotoModal(false));
            var closeTxt = MakeLabel(closeBtn.transform, "Txt", Loc.T("close_btn"), 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(closeTxt.rectTransform);

            _photoModal.SetActive(false);
        }

        public void OpenPhotoModal(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (open) DismissTransientToast();
            if (_photoModal != null) _photoModal.SetActive(open);
            if (open) RefreshPhotoAlbum();
        }

        void BuildPuzzleModal(Transform root)
        {
            var backdrop = MakePanel(root, "PuzzleModalBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _puzzleModal = backdrop;

            var card = MakePanel(backdrop.transform, "PuzzleCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 1340f));

            var title = MakeLabel(card.transform, "Title", Loc.T("practice_title"), 48, FontStyle.Bold, new Color(0.58f, 0.36f, 0.88f, 1f), TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f), Vector2.zero, new Vector2(820f, 64f));

            var sub = MakeLabel(card.transform, "Sub", Loc.T("practice_sub"), 24, FontStyle.Normal, ColCream, TextAnchor.MiddleLeft);
            SetRect(sub.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(820f, 44f));

            var statsPill = MakePanel(card.transform, "StatsPill", ColNavySoft, new Vector2(0.5f, 0.80f), new Vector2(0.5f, 0.80f), Vector2.zero, new Vector2(820f, 76f));
            int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
            _puzzleArchiveStatsText = MakeLabel(statsPill.transform, "Txt", "", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_puzzleArchiveStatsText.rectTransform);

            var journal = MakePanel(card.transform, "PuzzleAnswerArchive", ColNavySoft, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f), Vector2.zero, new Vector2(840f, 710f));
            var heading = MakeLabel(journal.transform, "Heading", Loc.T("practice_archive_heading"), 29, FontStyle.Bold, ColTeal, TextAnchor.MiddleLeft);
            SetRect(heading.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), Vector2.zero, new Vector2(760f, 52f));
            var viewport = MakePanel(journal.transform, "ArchiveViewport", ColNavySoft, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(800f, 620f));
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _puzzleArchiveScrollRect = viewport.AddComponent<ScrollRect>();
            _puzzleArchiveScrollRect.viewport = viewport.GetComponent<RectTransform>();
            _puzzleArchiveScrollRect.horizontal = false;
            _puzzleArchiveScrollRect.vertical = true;
            _puzzleArchiveScrollRect.movementType = ScrollRect.MovementType.Clamped;
            _puzzleArchiveScrollRect.scrollSensitivity = 42f;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _puzzleArchiveContent = content.GetComponent<RectTransform>();
            _puzzleArchiveContent.anchorMin = new Vector2(0f, 1f);
            _puzzleArchiveContent.anchorMax = new Vector2(1f, 1f);
            _puzzleArchiveContent.pivot = new Vector2(0.5f, 1f);
            _puzzleArchiveContent.offsetMin = new Vector2(10f, 0f);
            _puzzleArchiveContent.offsetMax = new Vector2(-10f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(8, 8, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var sizeFitter = content.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _puzzleArchiveScrollRect.content = _puzzleArchiveContent;

            var closeBtn = MakeButton(card.transform, "CloseBtn", ColYellow, new Vector2(0.5f, 0.055f), new Vector2(820f, 96f), () => OpenPuzzleModal(false));
            var closeTxt = MakeLabel(closeBtn.transform, "Txt", Loc.T("close_btn"), 34, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(closeTxt.rectTransform);

            _puzzleModal.SetActive(false);
        }

        void RefreshPuzzleArchive()
        {
            if (_puzzleArchiveContent == null || _puzzleArchiveStatsText == null) return;
            int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
            _puzzleArchiveStatsText.text = Loc.F("practice_stats", SaveSystem.Data.totalCorrect, SaveSystem.Data.bestStreak, stop, GameConfig.TotalStops);

            for (int i = _puzzleArchiveContent.childCount - 1; i >= 0; i--)
                Destroy(_puzzleArchiveContent.GetChild(i).gameObject);

            var entries = new List<DistrictAnswerRecord>(SaveSystem.Data.districtAnswerLog);
            entries.RemoveAll(entry => entry == null);
            entries.Sort((a, b) => b.stop.CompareTo(a.stop));
            if (entries.Count == 0)
            {
                AddPuzzleArchiveEntry(Loc.T("practice_archive_empty"), 110f);
            }
            else
            {
                foreach (var entry in entries)
                {
                    var category = (PuzzleCategory)Mathf.Clamp(entry.category, 0, Enum.GetValues(typeof(PuzzleCategory)).Length - 1);
                    string row = string.Format("#{0} · {1}\n{2}\n→ {3}", entry.stop, Loc.T("category_" + category),
                        Loc.Pick(entry.promptTR, entry.promptEN), Loc.Pick(entry.answerTR, entry.answerEN));
                    AddPuzzleArchiveEntry(row, 260f);
                }
            }

            Canvas.ForceUpdateCanvases();
            if (_puzzleArchiveScrollRect != null) _puzzleArchiveScrollRect.verticalNormalizedPosition = 1f;
        }

        void AddPuzzleArchiveEntry(string content, float rowHeight)
        {
            var row = new GameObject("PuzzleArchiveEntry", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_puzzleArchiveContent, false);
            var image = row.GetComponent<Image>();
            image.color = ColNavy;
            image.raycastTarget = false;
            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = rowHeight;
            layout.preferredHeight = rowHeight;
            var label = MakeLabel(row.transform, "Text", content, 28, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16f, 8f);
            label.rectTransform.offsetMax = new Vector2(-16f, -8f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            float readableHeight = Mathf.Max(rowHeight, label.GetPreferredValues(content, 830f, Mathf.Infinity).y + 28f);
            layout.minHeight = layout.preferredHeight = readableHeight;
        }

        public void OpenPuzzleModal(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (open) DismissTransientToast();
            if (_puzzleModal == null) return;
            if (open) RefreshPuzzleArchive();
            _puzzleModal.SetActive(open);
        }

        // ---------------------------------------------------------------- State & Updates

        public void ShowToast(string message, float duration = 2.0f)
        {
            _toastText.text = message;
            _toastBox.SetActive(true);
            _toastTimer = duration;
        }

        void DismissTransientToast()
        {
            _toastTimer = 0f;
            if (_toastBox != null) _toastBox.SetActive(false);
        }

        public void RefreshWeatherDisplay()
        {
            if (_gm == null || _gm.Weather == null) return;

            string wLabel = _gm.Weather.CurrentWeatherLabel;
            if (_hudWeatherText != null)
                _hudWeatherText.text = wLabel;

            if (_coastalBadgeText != null)
            {
                int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
                int dist = StopPlanner.DistrictIndex(stop);
                _coastalBadgeText.text = Loc.DistrictName(dist) + "  ·  " + wLabel;
            }
        }

        void RefreshExpressionCloseUps()
        {
            if (_expressionStrip != null)
                _expressionStrip.SetActive(false);
        }

        Sprite[] GetOrRenderExpressionPortraits(PassengerProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.resourceModelName)) return null;
            string portraitKey = profile.resourceFaceModelName ?? profile.resourceModelNameV2 ?? profile.resourceModelName;
            if (_exprPortraitCache.TryGetValue(portraitKey, out var cached)) return cached;

            var sprites = new Sprite[4];
            var stage = new GameObject("PortraitRenderStage");
            stage.transform.position = new Vector3(0f, -450f, 0f);

            var model = GlbModelLoader.InstantiatePassenger(profile);
            model.transform.SetParent(stage.transform, false);
            // Several bone poses render in one frame; each capture must rebuild its skinning matrices.
            var portraitSkins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < portraitSkins.Length; i++)
                portraitSkins[i].forceMatrixRecalculationPerRender = true;
            PassengerActor.ApplyStaticExpressionDeform(model, PassengerExpression.Curious);
            var portraitActor = model.GetComponent<PassengerActor>();
            portraitActor.TryGetPortraitAnchor(out var eye);
            float hairAboveEye = .24f;
            for (int i = 0; i < portraitSkins.Length; i++)
                hairAboveEye = Mathf.Max(hairAboveEye, portraitSkins[i].bounds.max.y - eye.y);
            const float portraitFov = 32f;
            // Include 3 cm above the tallest hairstyle while aiming 5 cm below the eyes.
            float portraitDistance = Mathf.Max(1.15f, (hairAboveEye + .08f) / Mathf.Tan(portraitFov * .5f * Mathf.Deg2Rad));
            var camGo = new GameObject("PortraitCam");
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.position = eye + model.transform.forward * portraitDistance;
            camGo.transform.rotation = Quaternion.LookRotation(eye - model.transform.up * .05f - camGo.transform.position, model.transform.up);

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.965f, 0.945f, 0.905f, 1f);
            cam.fieldOfView = portraitFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 6f;

            const int portraitSize = 256;
            var rt = new RenderTexture(portraitSize, portraitSize, 16, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var prevActive = RenderTexture.active;
            for (int i = 0; i < 4; i++)
            {
                PassengerActor.ApplyStaticExpressionDeform(model, (PassengerExpression)i);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(portraitSize, portraitSize, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, portraitSize, portraitSize), 0, 0);
                tex.Apply();
                sprites[i] = Sprite.Create(tex, new Rect(0, 0, portraitSize, portraitSize), new Vector2(0.5f, 0.5f), 100f);
            }
            RenderTexture.active = prevActive;
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            // Immediate: several passengers can be rendered in one frame (campaign finale), and a deferred Destroy
            // would leave the previous model standing in front of the next portrait camera.
            DestroyImmediate(stage);

            _exprPortraitCache[portraitKey] = sprites;
            return sprites;
        }

        public void OpenSettings(bool open)
        {
            SynthAudio.Instance?.PlayClick();
            if (open) DismissTransientToast();
            _settingsModal.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            if (open)
            {
                _puzzlePanel.SetActive(false);
                if (_resultBox != null) _resultBox.SetActive(false);
                if (_pipReactionCard != null) _pipReactionCard.SetActive(false);
                RefreshSettingsUi();
            }
            else
            {
                RefreshForState();
            }
        }

        void SetLanguage(Language lang)
        {
            SynthAudio.Instance?.PlayClick();
            SaveSystem.Data.language = (int)lang;
            SaveSystem.Save();
            Loc.Lang = lang;
            RefreshSettingsUi();
        }

        public void RefreshForState()
        {
            ResetLiveLabelCache();
            var st = _gm.State;
            bool waitingPickup = (st == GameState.PassengerBoarding && _gm.IsWaitingForPickupConfirm);
            _menuPanel.SetActive(st == GameState.MainMenu);
            _hudBar.SetActive(st != GameState.MainMenu);
            _coastalBadgeObj.SetActive(st == GameState.DrivingToStop);
            if (_stationArrivalCard != null)
            {
                _stationArrivalCard.SetActive(waitingPickup);
                if (waitingPickup)
                {
                    _stationArrivalTitleText.text = Loc.T("pickup_passenger");
                    _stationArrivalBtnText.text = "►   " + Loc.T("continue_btn");
                }
            }
            bool isRoutePuzzle = (st == GameState.PuzzleActive && _gm.ActivePuzzle != null && _gm.ActivePuzzle.kind == PuzzleKind.Route);
            _puzzlePanel.SetActive(st == GameState.PuzzleActive && !isRoutePuzzle);
            if (_routePuzzleOverlay != null) _routePuzzleOverlay.SetActive(isRoutePuzzle);
            if (_resultBox != null) _resultBox.SetActive(st == GameState.PuzzleResult);
            if (_pipReactionCard != null) _pipReactionCard.SetActive(st == GameState.PuzzleResult);

            RefreshWeatherDisplay();

            if (st == GameState.MainMenu)
            {
                int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
                if (_menuTitleLine1Shadow != null) _menuTitleLine1Shadow.text = "ODDS";
                if (_menuTitleLine1 != null) _menuTitleLine1.text = "ODDS";
                if (_menuTitleLine2Shadow != null) _menuTitleLine2Shadow.text = "CAB";
                if (_menuTitleLine2 != null) _menuTitleLine2.text = "CAB";

                bool toured = SaveSystem.Data.campaignComplete;
                _menuStopsBadgeText.text = toured ? Loc.F("campaign_done_badge", SaveSystem.Data.campaignsCompleted)
                                                  : $"{stop} / {GameConfig.TotalStops}";
                if (_menuProgressFill != null)
                    SetProgressFill(toured ? 1f : (float)(stop - 1) / GameConfig.TotalStops);

                // Dynamically connect next stop information to actual current stop data
                int distIdx = StopPlanner.DistrictIndex(stop);
                if (_menuNextStopHeaderText != null)
                    _menuNextStopHeaderText.text = Loc.F("menu_next_stop", stop);
                if (_menuDistrictSubText != null)
                    _menuDistrictSubText.text = Loc.Upper(Loc.DistrictLandmark(distIdx));
                _menuPlayBtnText.text = "►   " + Loc.T(toured ? "new_tour" : "play");
                RefreshMenuRoute();
            }
            else if (st == GameState.PuzzleActive)
            {
                _toastBox.SetActive(false);
                _resultBox.SetActive(false);
                PopulatePuzzleCard();
            }
        }

        void RefreshSettingsUi()
        {
            _trRadioDot.color = Loc.IsTR ? ColTeal : new Color(1f, 1f, 1f, 0.18f);
            _enRadioDot.color = !Loc.IsTR ? ColTeal : new Color(1f, 1f, 1f, 0.18f);

            int savedTier = SaveSystem.Data.qualityTier;
            _spokenGuideText.text = Loc.T(SaveSystem.Data.spokenGuideEnabled ? "guide_on" : "guide_off");
            _spokenGuideImage.color = SaveSystem.Data.spokenGuideEnabled ? ColTeal : ColNavySoft;
            int[] tierValues = { -1, 0, 1, 2, 3 };
            string[] keys = { "q_auto", "q_low", "q_med", "q_high", "q_ultra" };
            for (int i = 0; i < 5; i++)
            {
                _tierBtnTexts[i].text = Loc.T(keys[i]);
                _tierBtnImages[i].color = (savedTier == tierValues[i]) ? ColTeal : ColNavySoft;
            }
        }

        void RefreshAllLocalizedText()
        {
            _menuSettingsBtnText.text = "";   // the gear is an icon
            _settingsTitleText.text = Loc.T("settings");
            _musicLabelText.text = "♪  " + Loc.T("music");
            _sfxLabelText.text = "♫  " + Loc.T("effects");
            _graphicsLabelText.text = "•  " + Loc.T("graphics");
            _backToGameBtnText.text = "←  " + Loc.T("back_to_game");
            _bellReplayBtnText.text = "♪  " + Loc.T("listen_again");
            _bridgeLiftBtnText.text = Loc.T("lift_bridge");
            _bridgeClearBtnText.text = Loc.T("clear");
            if (_chatTitleText != null) _chatTitleText.text = Loc.T("chat_title");
            if (_chatSubText != null) _chatSubText.text = Loc.T("chat_sub");
            if (_journeyMapButtonText != null) _journeyMapButtonText.text = Loc.T("journey_map_button");
            if (_taxiShopButtonText != null) _taxiShopButtonText.text = Loc.T("taxi_shop_button");
            if (_districtFinaleHeadingText != null) _districtFinaleHeadingText.text = Loc.T("district_finale_log");
            if (_districtFinaleContinueText != null) _districtFinaleContinueText.text = Loc.T("district_finale_continue");
            RefreshJourneyMap();
            RefreshDistrictFinale();
            RefreshTaxiShop();
            if (_stationArrivalTitleText != null) _stationArrivalTitleText.text = Loc.T("pickup_passenger");
            if (_stationArrivalBtnText != null) _stationArrivalBtnText.text = "►   " + Loc.T("continue_btn");
            if (_routeTitleText != null) _routeTitleText.text = Loc.T("route_puzzle_title");
            if (_routeSubText != null) _routeSubText.text = Loc.T("route_puzzle_sub");

            RefreshSettingsUi();
            RefreshWeatherDisplay();
            RefreshExpressionCloseUps();
            RefreshPassengerStory();
            RefreshForState();
        }

        void SetProgressFill(float progress)
        {
            if (_menuProgressFill == null) return;
            RectTransform fillRect = _menuProgressFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        void UpdatePortraitSafeArea()
        {
            if (_portraitRoot == null || _scaler == null) return;
            Rect safe = Screen.safeArea;
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height && _lastSafeArea == safe) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = safe;
            float sw = Mathf.Max(1f, Screen.width);
            float sh = Mathf.Max(1f, Screen.height);
            Rect frame = MobileUiLayout.PortraitFrame(sw, sh, safe);
            // Scale against the usable portrait frame, including notches and landscape insets.
            // This keeps every card in the same 1080-wide coordinate space after orientation changes.
            _scaler.matchWidthOrHeight = 0f;
            _scaler.referenceResolution = new Vector2(MobileUiLayout.CanvasReferenceWidth(sw, frame.width), 1920f);
            float xMin = frame.xMin / sw, xMax = frame.xMax / sw;
            float yMin = frame.yMin / sh, yMax = frame.yMax / sh;

            _portraitRoot.anchorMin = new Vector2(xMin, yMin);
            _portraitRoot.anchorMax = new Vector2(xMax, yMax);
            _portraitRoot.offsetMin = Vector2.zero;
            _portraitRoot.offsetMax = Vector2.zero;
        }

        void Update()
        {
            UpdatePortraitSafeArea();
            UpdateRewardJuice();
            AnimateCampaignConfetti();
            HandleBackButton();
            UpdateScreenSleep();
            TickDistrictLoading();

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0f) _toastBox.SetActive(false);
            }

            if (_hudBar.activeSelf && _gm.Taxi != null)
            {
                int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
                if (stop != _lastHudStop)
                {
                    _lastHudStop = stop;
                    _hudStopText.SetText("{0} / {1}", (float)stop, (float)GameConfig.TotalStops);
                }
                int coins = _coinShown >= 0 ? _coinShown : SaveSystem.Data.coins;
                if (coins != _lastHudCoins)
                {
                    _lastHudCoins = coins;
                    _hudCoinText.SetText("{0}", coins);
                }

                bool isRouteActive = (_gm.State == GameState.PuzzleActive && _gm.ActivePuzzle != null && _gm.ActivePuzzle.kind == PuzzleKind.Route);
                if (isRouteActive)
                {
                    // Top HUD bar center compartment shows Turquoise Stopwatch Icon + "00:25" (media_1792765160263_5464a1fc.jpg)
                    if (_hudCenterIcon != null && _hudCenterIcon.sprite != _stopwatchSprite)
                    {
                        _hudCenterIcon.sprite = _stopwatchSprite;
                        SetRect(_hudCenterIconRect, new Vector2(0.435f, 0.5f), new Vector2(0.435f, 0.5f), Vector2.zero, new Vector2(48f, 54f));
                        SetRect(_hudSpeedText.rectTransform, new Vector2(0.56f, 0.5f), new Vector2(0.56f, 0.5f), Vector2.zero, new Vector2(180f, 64f));
                        _hudSpeedText.fontSize = 34;
                        _lastHudTimer = -1;
                    }
                    int sec = Mathf.CeilToInt(_gm.TimeRemaining);
                    if (sec != _lastHudTimer)
                    {
                        _lastHudTimer = sec;
                        _hudSpeedText.SetText("{0:00}:{1:00}", (float)(sec / 60), (float)(sec % 60));
                    }
                }
                else
                {
                    if (_hudCenterIcon != null && _hudCenterIcon.sprite != _speedoGaugeSprite)
                    {
                        _hudCenterIcon.sprite = _speedoGaugeSprite;
                        SetRect(_hudCenterIconRect, new Vector2(0.515f, 0.69f), new Vector2(0.515f, 0.69f), Vector2.zero, new Vector2(68f, 36f));
                        SetRect(_hudSpeedText.rectTransform, new Vector2(0.515f, 0.30f), new Vector2(0.515f, 0.30f), Vector2.zero, new Vector2(230f, 52f));
                        _hudSpeedText.fontSize = 30;
                        _lastHudSpeed = -1;
                    }
                    int speed = _gm.Taxi.Kmh;
                    if (speed != _lastHudSpeed || _lastHudTurkish != Loc.IsTR)
                    {
                        _lastHudSpeed = speed;
                        _lastHudTurkish = Loc.IsTR;
                        _hudSpeedText.SetText(Loc.IsTR ? "{0} km/sa" : "{0} km/h", speed);
                    }
                }
            }

            if (_puzzlePanel.activeSelf)
            {
                RefreshExpressionCloseUps();
            }

            if (_gm.State == GameState.PuzzleActive)
            {
                if (_gm.IsMelodyDemoPaused)
                {
                    if (!_timerDemoShown) _timerText.text = "♪ DEMO";
                    _timerDemoShown = true;
                    _timerFill.color = ColYellow;
                }
                else
                {
                    int sec = Mathf.CeilToInt(_gm.TimeRemaining);
                    if (sec != _lastTimerSecond || _timerDemoShown)
                    {
                        _lastTimerSecond = sec;
                        _timerDemoShown = false;
                        _timerText.SetText("{0:00}:{1:00}", (float)(sec / 60), (float)(sec % 60));
                        if (sec > 0 && sec <= 5) SynthAudio.Instance?.PlayTimerWarning();
                    }
                    _timerFill.color = sec <= 5 ? Color.Lerp(ColRed, ColYellow, Mathf.PingPong(Time.unscaledTime * 2f, 1f)) : ColTeal;
                }
                _timerFill.fillAmount = Mathf.Clamp01(_gm.TimeRemaining / Mathf.Max(1f, _gm.TotalTimeForPuzzle));

                // Animate the Taxi Driver's Hand hovering / tapping the option button (media_1792764991705_654c83ca.jpg)
                // Highlight choice glow frame
                int targetIdx = _selectedChoiceIndex >= 0 ? _selectedChoiceIndex : -1;
                for (int i = 0; i < 4; i++)
                {
                    if (_choiceGlowFrames[i] != null)
                    {
                        bool glow = (i == targetIdx) && !_gm.DisabledOptions[i];
                        _choiceGlowFrames[i].color = glow
                            ? new Color(1.0f, 0.97f, 0.86f, 0.88f)
                            : new Color(1f, 0.98f, 0.90f, 0f);
                    }
                }

                if (_gm.ActivePuzzle != null && _gm.ActivePuzzle.kind == PuzzleKind.Timing && _timingMarkerRect != null)
                {
                    float speed = 0.34f + Mathf.Clamp(_gm.ActivePuzzle.difficulty, 0, 2) * 0.13f;
                    _timingMarkerPosition = Mathf.PingPong(Time.time * speed, 1f);
                    _timingMarkerRect.anchoredPosition = new Vector2((_timingMarkerPosition - 0.5f) * _timingTrackRect.rect.width, 0f);
                }
            }
        }

        // ---------------------------------------------------------------- UI Widget & Icon Builders

        GameObject MakePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            SetRect(rt, anchorMin, anchorMax, anchoredPos, size);
            // Rounded corners (reference cards and pills); small pieces get a proportionally smaller radius so a
            // checker square or a thin bar doesn't turn into a circle. Icons that set their own sprite override this.
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            float minSide = anchorMin == anchorMax ? Mathf.Min(size.x, size.y) : 400f;
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, 32f / Mathf.Max(1f, minSide * 0.30f));
            if (anchorMin == anchorMax && size.x >= 140f && size.y >= 66f && color.a >= 0.6f)
            {
                go.AddComponent<UiSurface>();
                var rim = go.AddComponent<Outline>();
                rim.effectColor = new Color(0.58f, 0.84f, 0.90f, color.r < 0.3f ? 0.20f : 0.14f);
                rim.effectDistance = new Vector2(1.2f, -1.2f);
                rim.useGraphicAlpha = true;
            }
            return go;
        }

        UiText MakeLabel(Transform parent, string name, string text, int fontSize, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UiText));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<UiText>();
            t.text = text;
            t.fontSize = parent.GetComponent<UnityEngine.UI.Button>() != null ? Mathf.Max(32, fontSize) : fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string name, Color bgColor, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            size = MobileUiLayout.ButtonSize(size);
            var go = MakePanel(parent, name, bgColor, anchor, anchor, Vector2.zero, size);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(onClick);
            var colours = btn.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colours.pressedColor = new Color(0.88f, 0.91f, 0.94f, 1f);
            colours.selectedColor = Color.white;
            colours.disabledColor = new Color(0.48f, 0.53f, 0.57f, 0.60f);
            colours.fadeDuration = 0.08f;
            btn.colors = colours;
            if (size.x >= 140f && size.y >= 66f)
            {
                var shadow = go.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.015f, 0.03f, 0.05f, 0.30f);
                shadow.effectDistance = new Vector2(0f, -5f);
            }
            go.AddComponent<UiPressScale>();
            return btn;
        }

        Slider MakeSlider(Transform parent, string name, Vector2 anchor, Vector2 size, float initial, UnityEngine.Events.UnityAction<float> onChanged)
        {
            size.y = Mathf.Max(MobileUiLayout.TapHeight, size.y);
            var root = MakePanel(parent, name, Color.clear, anchor, anchor, Vector2.zero, size);
            var track = MakePanel(root.transform, "Track", ColNavySoft, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
            track.GetComponent<Image>().raycastTarget = false;
            var fillArea = MakePanel(root.transform, "FillArea", Color.clear, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
            fillArea.GetComponent<Image>().raycastTarget = false;
            var fill = MakePanel(fillArea.transform, "Fill", ColTeal, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.GetComponent<Image>().raycastTarget = false;
            var handleArea = MakePanel(root.transform, "HandleArea", Color.clear,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            handleArea.GetComponent<Image>().raycastTarget = false;
            var handle = MakePanel(handleArea.transform, "Handle", ColCream, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
            if (s_discSprite == null) s_discSprite = CreateDiscSprite();
            handle.GetComponent<Image>().sprite = s_discSprite;

            var s = root.AddComponent<Slider>();
            s.minValue = 0f;
            s.maxValue = 1f;
            s.fillRect = fill.GetComponent<RectTransform>();
            s.handleRect = handle.GetComponent<RectTransform>();
            s.targetGraphic = handle.GetComponent<Image>();
            s.SetValueWithoutNotify(initial);
            s.onValueChanged.AddListener(onChanged);
            return s;
        }

        static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }
    }
}

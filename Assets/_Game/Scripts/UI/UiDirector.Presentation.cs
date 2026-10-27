using TMPro;
using UnityEngine;

namespace OddsCab
{
    public partial class UiDirector
    {
        int _lastScreenWidth = -1, _lastScreenHeight = -1;
        Rect _lastSafeArea;
        int _lastHudStop = -1, _lastHudCoins = -1, _lastHudSpeed = -1, _lastHudTimer = -1, _lastTimerSecond = -1;
        bool _lastHudTurkish, _timerDemoShown;
        Sprite _referencePostcard;
        Sprite _filmLogoSprite;

        void ApplyFilmBranding()
        {
            var texture = Resources.Load<Texture2D>("UI/Brand/OddsCabFilmLogo");
            if (texture == null) return;
            // The production PNG keeps its genuine alpha; this rect only removes its outer safe margin.
            _filmLogoSprite = Sprite.Create(texture, new Rect(128f, 100f, 1280f, 812f),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            _filmLogoSprite.name = "ODDS CAB — film title";
            var logo = new GameObject("OddsCabBrandLogo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            logo.transform.SetParent(_menuPanel.transform, false);
            var image = logo.GetComponent<UnityEngine.UI.Image>();
            image.sprite = _filmLogoSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetRect((RectTransform)logo.transform, new Vector2(.465f, .889f),
                new Vector2(.465f, .889f), Vector2.zero, new Vector2(540f, 343f));
            _menuTitleLine1.gameObject.SetActive(false);
            _menuTitleLine2.gameObject.SetActive(false);
            _menuTitleLine1Shadow.gameObject.SetActive(false);
            _menuTitleLine2Shadow.gameObject.SetActive(false);
            var checker = _menuPanel.transform.Find("TitleCheckerPattern");
            if (checker != null) checker.gameObject.SetActive(false);
        }

        void ResetLiveLabelCache()
        {
            _lastHudStop = _lastHudCoins = _lastHudSpeed = _lastHudTimer = _lastTimerSecond = -1;
            _timerDemoShown = false;
        }

        static void StyleBody(UiText text, Color colour)
        {
            if (text == null) return;
            TMP_FontAsset font = UiText.FullSymbolFont != null ? UiText.FullSymbolFont : TMP_Settings.defaultFontAsset;
            if (font != null)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
            text.enableVertexGradient = false;
            text.fontStyle = FontStyle.Bold;
            text.characterSpacing = 0.2f;
            text.color = colour;
        }

        static RectTransform ChildRect(Transform parent, string path)
        {
            Transform child = parent != null ? parent.Find(path) : null;
            return child != null ? child as RectTransform : null;
        }

        static void Place(Transform parent, string path, Vector2 anchor, Vector2 size)
        {
            var rect = ChildRect(parent, path);
            if (rect != null) SetRect(rect, anchor, anchor, Vector2.zero, size);
        }

        void ApplyReferencePresentation()
        {
            // The logo occupies the sky. The street, taxi and Bosphorus stay visible through the central half.
            _menuTitleLine1.alignment = _menuTitleLine2.alignment = TextAnchor.MiddleCenter;
            _menuTitleLine1.fontSize = 138f;
            _menuTitleLine2.fontSize = 154f;
            SetRect(_menuTitleLine1.rectTransform, new Vector2(0.48f, 0.925f), new Vector2(0.48f, 0.925f), Vector2.zero, new Vector2(820f, 170f));
            SetRect(_menuTitleLine2.rectTransform, new Vector2(0.48f, 0.848f), new Vector2(0.48f, 0.848f), Vector2.zero, new Vector2(820f, 178f));
            SetRect(_menuTitleLine1Shadow.rectTransform, new Vector2(0.48f, 0.925f), new Vector2(0.48f, 0.925f), new Vector2(5f, -7f), new Vector2(820f, 170f));
            SetRect(_menuTitleLine2Shadow.rectTransform, new Vector2(0.48f, 0.848f), new Vector2(0.48f, 0.848f), new Vector2(6f, -8f), new Vector2(820f, 178f));
            Place(_menuPanel.transform, "TitleCheckerPattern", new Vector2(0.70f, 0.855f), new Vector2(76f, 76f));
            ApplyFilmBranding();
            Place(_menuPanel.transform, "StopsProgressPill", new Vector2(0.295f, 0.75f), new Vector2(510f, 88f));
            Place(_menuPanel.transform, "JourneyMapButton", new Vector2(0.70f, 0.75f), new Vector2(176f, 84f));
            Place(_menuPanel.transform, "TaxiGarageButton", new Vector2(0.86f, 0.75f), new Vector2(150f, 84f));
            Place(_menuPanel.transform, "NextStopCard", new Vector2(0.5f, 0.194f), new Vector2(960f, 212f));
            Place(_menuPanel.transform, "PlayButton", new Vector2(0.5f, 0.075f), new Vector2(960f, 132f));
            Place(_menuPanel.transform, "NextStopCard/PostcardFrame", new Vector2(0.205f, 0.54f), new Vector2(370f, 228f));
            Place(_menuPanel.transform, "NextStopCard/PostcardFrame/PostcardImg", new Vector2(0.5f, 0.5f), new Vector2(350f, 208f));
            Place(_menuPanel.transform, "NextStopCard/NextStopHeader", new Vector2(0.70f, 0.77f), new Vector2(480f, 48f));
            Place(_menuPanel.transform, "NextStopCard/LandmarkPinOuter", new Vector2(0.435f, 0.48f), new Vector2(36f, 44f));
            Place(_menuPanel.transform, "NextStopCard/LandmarkName", new Vector2(0.70f, 0.48f), new Vector2(440f, 44f));
            Place(_menuPanel.transform, "NextStopCard/ActBtn_Foto", new Vector2(0.495f, 0.17f), new Vector2(130f, 48f));
            Place(_menuPanel.transform, "NextStopCard/ActBtn_Sohbet", new Vector2(0.680f, 0.17f), new Vector2(155f, 48f));
            Place(_menuPanel.transform, "NextStopCard/ActBtn_Bulmaca", new Vector2(0.875f, 0.17f), new Vector2(155f, 48f));
            StyleBody(_menuStopsBadgeText, ColCream);
            StyleBody(_menuNextStopHeaderText, ColTextDark);
            StyleBody(_menuDistrictSubText, ColTextDark);
            _menuDistrictSubText.resizeTextForBestFit = true;
            _menuDistrictSubText.resizeTextMinSize = 20;
            _menuDistrictSubText.resizeTextMaxSize = 29;
            if (_menuPostcardImg != null)
            {
                _referencePostcard = CreateReferencePostcard();
                if (_referencePostcard != null) _menuPostcardImg.sprite = _referencePostcard;
            }

            // One compact HUD; weather remains on its discreet bottom pill, rather than a second header row.
            Transform weather = _hudBar.transform.Find("TopWeatherBadge");
            if (weather != null) weather.gameObject.SetActive(false);
            var pause = ChildRect(_hudBar.transform, "PauseBtn");
            if (pause != null)
            {
                pause.GetComponent<UnityEngine.UI.Image>().sprite = s_discSprite;
                Transform oldLabel = pause.Find("II");
                if (oldLabel != null) oldLabel.gameObject.SetActive(false);
                var left = MakePanel(pause, "PauseStemLeft", ColCream, new Vector2(0.39f, 0.5f), new Vector2(0.39f, 0.5f), Vector2.zero, new Vector2(14f, 46f));
                var right = MakePanel(pause, "PauseStemRight", ColCream, new Vector2(0.61f, 0.5f), new Vector2(0.61f, 0.5f), Vector2.zero, new Vector2(14f, 46f));
                left.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                right.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }
            if (_hudCoinBadge != null)
            {
                var coin = _hudCoinBadge.GetComponent<UnityEngine.UI.Image>();
                coin.sprite = _flyCoinSprite;
                coin.type = UnityEngine.UI.Image.Type.Simple;
                coin.color = Color.white;
                Transform dollar = _hudCoinBadge.Find("DollarSign");
                if (dollar != null) dollar.gameObject.SetActive(false);
            }
            Place(_portraitRoot, "BottomCoastalWeatherBadge", new Vector2(0.5f, 0.038f), new Vector2(560f, 56f));
            _coastalBadgeText.fontSize = 23f;
            _coastalBadgeText.color = ColCream;
            Place(_portraitRoot, "StationArrivalCard", new Vector2(0.5f, 0.125f), new Vector2(960f, 300f));
            Place(_stationArrivalCard.transform, "TurquoisePinIcon", new Vector2(0.235f, 0.73f), new Vector2(54f, 68f));
            Place(_stationArrivalCard.transform, "PickupTitle", new Vector2(0.60f, 0.73f), new Vector2(570f, 80f));
            Place(_stationArrivalCard.transform, "PickupContinueBtn", new Vector2(0.5f, 0.29f), new Vector2(866f, 126f));
            StyleBody(_stationArrivalTitleText, ColCream);
            _stationArrivalTitleText.fontSize = 49f;

            Place(_portraitRoot, "PipPassengerReactionCard", new Vector2(0.79f, 0.795f), new Vector2(320f, 320f));
            Place(_pipReactionCard.transform, "PipInnerBg", new Vector2(0.5f, 0.5f), new Vector2(304f, 304f));
            Place(_pipReactionCard.transform, "PipInnerBg/PipPortraitImg", new Vector2(0.5f, 0.5f), new Vector2(298f, 298f));
            Transform live = _pipReactionCard.transform.Find("LiveBadge");
            if (live != null) live.gameObject.SetActive(false);
            var pipFrame = _pipReactionCard.transform.Find("PipInnerBg");
            if (pipFrame != null && pipFrame.GetComponent<UnityEngine.UI.Mask>() == null)
                pipFrame.gameObject.AddComponent<UnityEngine.UI.Mask>();

            // Label text stays crisp; only the logo and celebration headings use the expressive display face.
            StyleBody(_promptText, ColCream);
            for (int i = 0; i < _choiceLabels.Length; i++) StyleBody(_choiceLabels[i], ColTextDark);
            StyleBody(_hudStopText, ColCream);
            StyleBody(_hudSpeedText, ColCream);
            StyleBody(_hudCoinText, ColCream);
            StyleBody(_timerText, ColCream);
            StyleBody(_categoryBadgeText, ColTeal);
            _categoryBadgeText.fontSize = 21f;
            _categoryBadgeText.resizeTextForBestFit = true;
            _categoryBadgeText.resizeTextMinSize = 17;
            _categoryBadgeText.resizeTextMaxSize = 21;
            AddCardShadow(_puzzlePanel);
            AddCardShadow(_settingsModal.transform.Find("SettingsCard").gameObject);
        }

        Sprite CreateReferencePostcard()
        {
            // Baked from the existing landmark GLB. UI initialization never needs
            // an auxiliary render, GPU readback, or changes to global lighting.
            var texture = Resources.Load<Texture2D>("ReferenceKit/Textures/MaidensTowerPostcard");
            if (texture == null) return null;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "MaidensTowerPostcard";
            return sprite;
        }

        void ReleaseReferencePresentation()
        {
            if (_referencePostcard != null) Destroy(_referencePostcard);
            if (_filmLogoSprite != null) Destroy(_filmLogoSprite);
        }
    }
}

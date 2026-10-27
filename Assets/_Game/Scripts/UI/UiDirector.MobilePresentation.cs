using UnityEngine;

namespace OddsCab
{
    public partial class UiDirector
    {
        void ResizeModal(GameObject backdrop, string name, float height)
        {
            if (backdrop == null) return;
            var card = ChildRect(backdrop.transform, name);
            if (card == null) return;
            card.sizeDelta = new Vector2(MobileUiLayout.PanelWidth, height);
            var close = ChildRect(card, "CloseBtn");
            if (close != null) close.sizeDelta = new Vector2(920f, MobileUiLayout.TapHeight);
            var title = ChildRect(card, "Title");
            if (title != null)
            {
                title.sizeDelta = new Vector2(920f, 88f);
                var text = title.GetComponent<UiText>();
                if (text != null) { text.resizeTextForBestFit = true; text.resizeTextMinSize = 40; text.resizeTextMaxSize = 54; }
            }
            var sub = ChildRect(card, "Sub");
            if (sub != null)
            {
                sub.sizeDelta = new Vector2(920f, 88f);
                var text = sub.GetComponent<UiText>();
                if (text != null) text.fontSize = 30;
            }
        }

        void ApplyMobilePresentation()
        {
            ResizeModal(_settingsModal, "SettingsCard", 1540f);
            ResizeModal(_journeyMapModal, "JourneyMapCard", 1500f);
            ResizeModal(_districtFinaleModal, "DistrictFinaleCard", 1500f);
            ResizeModal(_taxiShopModal, "TaxiShopCard", 1500f);
            ResizeModal(_photoModal, "PhotoCard", 1500f);
            ResizeModal(_chatModal, "ChatCard", 1500f);
            ResizeModal(_puzzleModal, "PuzzleCard", 1500f);
            ResizeModal(_quitConfirm, "QuitCard", 580f);
            if (_campaignFinaleModal != null) ResizeModal(_campaignFinaleModal, "CampaignFinaleCard", 1540f);
            var settings = ChildRect(_settingsModal.transform, "SettingsCard");
            Place(settings, "Title", new Vector2(.5f, .945f), new Vector2(900f, 80f));
            _settingsTitleText.fontSize = 62;
            Place(settings, "MusicLabel", new Vector2(.5f, .852f), new Vector2(880f, 58f));
            Place(settings, "MusicSlider", new Vector2(.5f, .792f), new Vector2(880f, 144f));
            Place(settings, "SfxLabel", new Vector2(.5f, .705f), new Vector2(880f, 58f));
            Place(settings, "SfxSlider", new Vector2(.5f, .645f), new Vector2(880f, 144f));
            Place(settings, "SpokenGuide", new Vector2(.5f, .532f), new Vector2(880f, 144f));
            Place(settings, "LangTR", new Vector2(.5f, .425f), new Vector2(880f, 144f));
            Place(settings, "LangEN", new Vector2(.5f, .32f), new Vector2(880f, 144f));
            Place(settings, "GraphicsLabel", new Vector2(.5f, .245f), new Vector2(880f, 48f));
            for (int i = 0; i < _tierBtnImages.Length; i++)
            {
                var center = MobileUiLayout.QualityButtonCenter(i);
                SetRect(_tierBtnImages[i].rectTransform, center, center, Vector2.zero, new Vector2(168f, 144f));
                _tierBtnTexts[i].fontSize = 32;
            }
            Place(settings, "BackBtn", new Vector2(.5f, .065f), new Vector2(880f, 144f));
            _spokenGuideText.fontSize = 34;
            var chat = ChildRect(_chatModal.transform, "ChatCard");
            Place(chat, "StatusPill", new Vector2(.5f, .79f), new Vector2(920f, 96f));
            Place(chat, "DialogueBox", new Vector2(.5f, .51f), new Vector2(920f, 600f));
            Place(chat, "DialogueBox/DialogueTxt", new Vector2(.5f, .43f), new Vector2(840f, 450f));
            Place(chat, "DialogueBox/ChapterTxt", new Vector2(.5f, .93f), new Vector2(840f, 56f));
            Place(chat, "NextChatBtn", new Vector2(.5f, .20f), new Vector2(920f, 144f));
            _chatDialogueText.fontSize = 34;
            _chatChapterText.fontSize = 28;
            _chatSpeakerText.fontSize = 32;
            var archive = ChildRect(_puzzleModal.transform, "PuzzleCard");
            Place(archive, "StatsPill", new Vector2(.5f, .79f), new Vector2(920f, 100f));
            Place(archive, "PuzzleAnswerArchive", new Vector2(.5f, .46f), new Vector2(940f, 820f));
            Place(archive, "PuzzleAnswerArchive/ArchiveViewport", new Vector2(.5f, .46f), new Vector2(900f, 710f));
            Place(archive, "PuzzleAnswerArchive/Heading", new Vector2(.5f, .95f), new Vector2(860f, 52f));
            _puzzleArchiveStatsText.fontSize = 30;
            var finale = ChildRect(_districtFinaleModal.transform, "DistrictFinaleCard");
            Place(finale, "Summary", new Vector2(.5f, .865f), new Vector2(920f, 80f));
            Place(finale, "AnswerJournal", new Vector2(.5f, .49f), new Vector2(940f, 920f));
            Place(finale, "AnswerJournal/JournalViewport", new Vector2(.5f, .46f), new Vector2(900f, 800f));
            Place(finale, "AnswerJournal/JournalHeading", new Vector2(.5f, .95f), new Vector2(860f, 52f));
            Place(finale, "ContinueBtn", new Vector2(.5f, .075f), new Vector2(920f, 144f));
            _districtFinaleSubText.fontSize = 30;
            Place(_menuPanel.transform, "SettingsButton", new Vector2(.92f, .945f), new Vector2(144f, 144f));
            Place(_menuPanel.transform, "SettingsButton/Gear", new Vector2(.5f, .5f), new Vector2(80f, 80f));
            Place(_menuPanel.transform, "StopsProgressPill", new Vector2(.267f, .75f), new Vector2(490f, 132f));
            Place(_menuPanel.transform, "JourneyMapButton", new Vector2(.64f, .75f), new Vector2(214f, 144f));
            Place(_menuPanel.transform, "TaxiGarageButton", new Vector2(.865f, .75f), new Vector2(224f, 144f));
            Place(_menuPanel.transform, "NextStopCard", new Vector2(.5f, .23f), new Vector2(1016f, 350f));
            Place(_menuPanel.transform, "PlayButton", new Vector2(.5f, .075f), new Vector2(1016f, 160f));
            Place(_menuPanel.transform, "NextStopCard/PostcardFrame", new Vector2(.18f, .5f), new Vector2(310f, 270f));
            Place(_menuPanel.transform, "NextStopCard/PostcardFrame/PostcardImg", new Vector2(.5f, .5f), new Vector2(290f, 220f));
            Place(_menuPanel.transform, "NextStopCard/NextStopHeader", new Vector2(.68f, .83f), new Vector2(560f, 60f));
            Place(_menuPanel.transform, "NextStopCard/LandmarkPinOuter", new Vector2(.40f, .62f), new Vector2(40f, 48f));
            Place(_menuPanel.transform, "NextStopCard/LandmarkName", new Vector2(.70f, .62f), new Vector2(540f, 52f));
            Place(_menuPanel.transform, "NextStopCard/ActBtn_Foto", new Vector2(.48f, .235f), new Vector2(176f, 144f));
            Place(_menuPanel.transform, "NextStopCard/ActBtn_Sohbet", new Vector2(.685f, .235f), new Vector2(176f, 144f));
            Place(_menuPanel.transform, "NextStopCard/ActBtn_Bulmaca", new Vector2(.89f, .235f), new Vector2(176f, 144f));
            _journeyMapButtonText.fontSize = _taxiShopButtonText.fontSize = 32;
            _menuDistrictSubText.resizeTextMinSize = 28;
            _menuDistrictSubText.resizeTextMaxSize = 36;
            _menuNextStopHeaderText.fontSize = 36;
            Place(_hudBar.transform, "TopPill", new Vector2(.425f, .68f), new Vector2(820f, 144f));
            Place(_hudBar.transform, "PauseBtn", new Vector2(.925f, .68f), new Vector2(144f, 144f));
            Place(_portraitRoot, "BottomCoastalWeatherBadge", new Vector2(.5f, .05f), new Vector2(760f, 144f));
            _coastalBadgeText.fontSize = 32;
            Place(_portraitRoot, "StationArrivalCard", new Vector2(.5f, .14f), new Vector2(1016f, 350f));
            Place(_stationArrivalCard.transform, "PickupContinueBtn", new Vector2(.5f, .29f), new Vector2(920f, 144f));
            _resultBox.GetComponent<RectTransform>().sizeDelta = new Vector2(1016f, 700f);
            var routeBanner = ChildRect(_routePuzzleOverlay.transform, "RouteBannerCard");
            if (routeBanner != null) routeBanner.sizeDelta = new Vector2(1016f, 290f);
            Place(_routePuzzleOverlay.transform, "RouteHintBtn", new Vector2(.5f, .05f), new Vector2(620f, 144f));
            if (_ferrySkipButton != null) _ferrySkipButton.GetComponent<RectTransform>().sizeDelta = new Vector2(620f, 144f);
        }
    }
}

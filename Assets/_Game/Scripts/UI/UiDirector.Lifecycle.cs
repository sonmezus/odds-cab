using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OddsCab
{
    /// <summary>
    /// Android device behaviour: the back button / gesture (Input System maps it to Escape) closes the top window,
    /// pauses a running ride or asks before quitting from the main menu; the screen stays awake while playing; the
    /// game pauses and saves when the app goes to the background (a phone call must not run the puzzle clock out).
    /// </summary>
    public partial class UiDirector
    {
        GameObject _quitConfirm;
        UiText _quitTitleText, _quitYesText, _quitNoText;

        void BuildQuitConfirm(Transform root)
        {
            _quitConfirm = MakePanel(root, "QuitConfirmBackdrop", new Color(0.02f, 0.05f, 0.09f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var card = MakePanel(_quitConfirm.transform, "QuitCard", ColNavy, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f));
            AddCardShadow(card);
            _quitTitleText = MakeLabel(card.transform, "Title", "", 48, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
            SetRect(_quitTitleText.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(760f, 120f));
            var yes = MakeButton(card.transform, "QuitYes", ColCream, new Vector2(0.27f, 0.24f), new Vector2(330f, 110f), () => Application.Quit());
            _quitYesText = MakeLabel(yes.transform, "Txt", "", 38, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_quitYesText.rectTransform);
            var no = MakeButton(card.transform, "QuitNo", ColYellow, new Vector2(0.73f, 0.24f), new Vector2(330f, 110f), () => ShowQuitConfirm(false));
            _quitNoText = MakeLabel(no.transform, "Txt", "", 38, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_quitNoText.rectTransform);
            StyleHeading(_quitTitleText, new Color(1f, 0.98f, 0.92f), new Color(0.94f, 0.86f, 0.70f));
            StyleButtonLabel(_quitYesText, ColTextDark);
            StyleButtonLabel(_quitNoText, ColTextDark);
            _quitConfirm.SetActive(false);
        }

        void ShowQuitConfirm(bool show)
        {
            if (_quitConfirm == null) return;
            if (show)
            {
                _quitTitleText.text = Loc.IsTR ? "Oyundan çıkmak\nistiyor musun?" : "Quit the game?";
                _quitYesText.text = Loc.IsTR ? "ÇIK" : "QUIT";
                _quitNoText.text = Loc.IsTR ? "KAL" : "STAY";
                _quitConfirm.transform.SetAsLastSibling();
            }
            _quitConfirm.SetActive(show);
        }

        /// <summary>Called every frame from Update: Android back = Escape in the Input System.</summary>
        void HandleBackButton()
        {
            if (_gm != null && _gm.State == GameState.DistrictTransition) return;
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            SynthAudio.Instance?.PlayClick();

            // 1. the top-most open window closes (in the order they stack)
            if (_quitConfirm != null && _quitConfirm.activeSelf) { ShowQuitConfirm(false); return; }
            if (_settingsModal != null && _settingsModal.activeSelf) { OpenSettings(false); return; }
            if (_journeyMapModal != null && _journeyMapModal.activeSelf) { OpenJourneyMap(false); return; }
            if (_taxiShopModal != null && _taxiShopModal.activeSelf) { OpenTaxiShop(false); return; }
            if (_photoModal != null && _photoModal.activeSelf) { OpenPhotoModal(false); return; }
            if (_chatModal != null && _chatModal.activeSelf) { OpenChatModal(false); return; }
            if (_puzzleModal != null && _puzzleModal.activeSelf) { OpenPuzzleModal(false); return; }
            // (the district / campaign finale cards wait for their own continue button: they award progress)

            // 2. main menu: ask before leaving; anywhere in a ride: pause
            if (_gm.State == GameState.MainMenu) ShowQuitConfirm(true);
            else OpenSettings(true);
        }

        /// <summary>Screen awake while playing (the puzzle timer runs while the player thinks), normal timeout in menus.</summary>
        void UpdateScreenSleep()
        {
            int want = _gm != null && _gm.State != GameState.MainMenu ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (Screen.sleepTimeout != want) Screen.sleepTimeout = want;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            SaveSystem.Save();
            // mid-ride the game comes back paused (settings card = pause menu), so the clock hasn't run out meanwhile
            if (_gm != null && _gm.State != GameState.MainMenu && _settingsModal != null && !_settingsModal.activeSelf)
                OpenSettings(true);
        }

        void OnApplicationQuit() => SaveSystem.Save();
    }
}

using UnityEngine;
using UnityEngine.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        GameObject _valveCauseGroup;
        UiText _valveCauseStatusText;
        Button _valveCauseValveButton;
        UiText _valveCauseValveText;
        Image _valveCauseFountainImage;
        Image _valveCauseWheelImage;
        UiText _valveCauseFountainText;
        UiText _valveCauseWheelText;
        UiText _valveCauseBridgeText;
        Button _valveCauseStepButton;
        UiText _valveCauseStepText;
        Button _valveCauseSubmitButton;
        UiText _valveCauseSubmitText;
        int _valveCauseSide; // 0 = fountain (left), 1 = wheel (right)
        int _valveCauseSteps;
        bool _valveCauseChanged;

        void EnsureValveCauseUi()
        {
            if (_valveCauseGroup != null) return;
            _valveCauseGroup = MakePanel(_puzzlePanel.transform, "ValveCausePuzzleGroup", Color.clear,
                new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.34f), Vector2.zero, new Vector2(900f, 470f));
            _valveCauseGroup.GetComponent<Image>().raycastTarget = false;
            _valveCauseStatusText = MakeLabel(_valveCauseGroup.transform, "ValveCauseHeader", "", 24, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_valveCauseStatusText.rectTransform, new Vector2(0.5f, 0.96f), new Vector2(0.5f, 0.96f), Vector2.zero, new Vector2(860f, 54f));

            _valveCauseValveButton = MakeButton(_valveCauseGroup.transform, "ValveSwitch", ColNavySoft,
                new Vector2(0.5f, 0.78f), new Vector2(520f, 84f), ToggleValveCause);
            _valveCauseValveText = MakeLabel(_valveCauseValveButton.transform, "Label", "", 25, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_valveCauseValveText.rectTransform);

            var fountain = MakePanel(_valveCauseGroup.transform, "FountainChannel", ColCream,
                new Vector2(0.27f, 0.51f), new Vector2(0.27f, 0.51f), Vector2.zero, new Vector2(330f, 124f));
            _valveCauseFountainImage = fountain.GetComponent<Image>();
            _valveCauseFountainText = MakeLabel(fountain.transform, "WaterAmount", "", 24, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_valveCauseFountainText.rectTransform);

            var wheel = MakePanel(_valveCauseGroup.transform, "WheelChannel", ColCream,
                new Vector2(0.73f, 0.51f), new Vector2(0.73f, 0.51f), Vector2.zero, new Vector2(330f, 124f));
            _valveCauseWheelImage = wheel.GetComponent<Image>();
            _valveCauseWheelText = MakeLabel(wheel.transform, "WaterAmount", "", 24, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_valveCauseWheelText.rectTransform);

            _valveCauseBridgeText = MakeLabel(_valveCauseGroup.transform, "BridgeState", "", 27, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_valveCauseBridgeText.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(780f, 60f));

            _valveCauseStepButton = MakeButton(_puzzlePanel.transform, "ValveCauseStepBtn", ColNavySoft,
                new Vector2(0.28f, 0.14f), new Vector2(400f, 84f), AdvanceValveCauseStep);
            _valveCauseStepText = MakeLabel(_valveCauseStepButton.transform, "Label", "", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_valveCauseStepText.rectTransform);
            _valveCauseSubmitButton = MakeButton(_puzzlePanel.transform, "ValveCauseSubmitBtn", ColYellow,
                new Vector2(0.72f, 0.14f), new Vector2(400f, 84f), SubmitValveCause);
            _valveCauseSubmitText = MakeLabel(_valveCauseSubmitButton.transform, "Label", "", 26, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_valveCauseSubmitText.rectTransform);

            _valveCauseGroup.SetActive(false);
            _valveCauseStepButton.gameObject.SetActive(false);
            _valveCauseSubmitButton.gameObject.SetActive(false);
        }

        void ToggleValveCause()
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.ValveCause
                || _valveCauseChanged || _valveCauseSteps != 0) return;
            _valveCauseSide = 1 - _valveCauseSide;
            _valveCauseChanged = true;
            SynthAudio.Instance?.PlayClick();
            RefreshValveCauseUi();
        }

        void AdvanceValveCauseStep()
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.ValveCause || _valveCauseSteps >= 3) return;
            _valveCauseSteps++;
            SynthAudio.Instance?.PlayClick();
            RefreshValveCauseUi();
        }

        void SubmitValveCause()
        {
            _gm.SubmitValveCauseAttempt(_valveCauseSide, _valveCauseSteps);
        }

        public void RevealValveCauseHint()
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.ValveCause) return;
            _valveCauseSide = _gm.ActivePuzzle.answerValue;
            _valveCauseChanged = true;
            _valveCauseSteps = 0;
            RefreshValveCauseUi();
            ShowToast(Loc.T("valve_cause_hint"));
        }

        void RefreshValveCauseUi()
        {
            var puzzle = _gm.ActivePuzzle;
            if (puzzle == null || puzzle.kind != PuzzleKind.ValveCause) return;
            int required = puzzle.ints != null && puzzle.ints.Length > 0 ? puzzle.ints[0] : 3;
            int fountainWater = _valveCauseSide == 0 ? _valveCauseSteps : 0;
            int wheelWater = _valveCauseSide == 1 ? _valveCauseSteps : 0;
            bool bridgeOpen = wheelWater >= required;
            _valveCauseStatusText.text = Loc.F("valve_cause_progress", _valveCauseSteps, required);
            _valveCauseValveText.text = Loc.T(_valveCauseSide == 0 ? "valve_cause_left" : "valve_cause_right");
            _valveCauseValveButton.interactable = !_valveCauseChanged && _valveCauseSteps == 0;
            _valveCauseFountainText.text = Loc.F("valve_cause_fountain", fountainWater, required);
            _valveCauseWheelText.text = Loc.F("valve_cause_wheel", wheelWater, required);
            _valveCauseFountainImage.color = _valveCauseSide == 0 ? new Color(0.70f, 0.88f, 0.91f, 1f) : ColCream;
            _valveCauseWheelImage.color = _valveCauseSide == 1 ? new Color(0.70f, 0.88f, 0.91f, 1f) : ColCream;
            _valveCauseBridgeText.text = Loc.T(bridgeOpen ? "valve_cause_bridge_open" : "valve_cause_bridge_closed");
            _valveCauseStepText.text = _valveCauseSteps >= required
                ? Loc.T("valve_cause_steps_done")
                : Loc.F("valve_cause_step", _valveCauseSteps + 1, required);
            _valveCauseStepButton.interactable = _valveCauseSteps < required;
            _valveCauseSubmitButton.interactable = _valveCauseSteps >= required;
            _valveCauseSubmitText.text = Loc.T("grid_submit");
        }
    }
}

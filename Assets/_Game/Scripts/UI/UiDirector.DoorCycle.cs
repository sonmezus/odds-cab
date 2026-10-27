using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        GameObject _doorCycleGroup;
        UiText _doorCycleStatusText;
        UiText[] _doorCycleDoorTexts;
        Image[] _doorCycleDoorImages;
        Slider _doorCycleSlider;
        UiText _doorCycleTimeText;
        Button _doorCycleSubmitButton;
        UiText _doorCycleSubmitText;
        readonly List<UiText> _doorCycleTicks = new List<UiText>();
        int _doorCycleTime;

        void EnsureDoorCycleUi()
        {
            if (_doorCycleGroup != null) return;
            _doorCycleGroup = MakePanel(_puzzlePanel.transform, "DoorCyclePuzzleGroup", Color.clear,
                new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(900f, 520f));
            _doorCycleGroup.GetComponent<Image>().raycastTarget = false;
            _doorCycleStatusText = MakeLabel(_doorCycleGroup.transform, "DoorCycleHeader", "", 26, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_doorCycleStatusText.rectTransform, new Vector2(0.5f, 0.97f), new Vector2(0.5f, 0.97f), Vector2.zero, new Vector2(850f, 56f));

            _doorCycleDoorTexts = new UiText[2];
            _doorCycleDoorImages = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 0.30f : 0.70f;
                var door = MakePanel(_doorCycleGroup.transform, "DoorIndicator_" + i, ColNavySoft,
                    new Vector2(x, 0.74f), new Vector2(x, 0.74f), Vector2.zero, new Vector2(300f, 118f));
                _doorCycleDoorImages[i] = door.GetComponent<Image>();
                _doorCycleDoorTexts[i] = MakeLabel(door.transform, "State", "", 27, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(_doorCycleDoorTexts[i].rectTransform);
            }

            var sliderGo = new GameObject("DoorCycleTimeSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderGo.transform.SetParent(_doorCycleGroup.transform, false);
            sliderGo.GetComponent<Image>().color = Color.clear;
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            SetRect(sliderRect, new Vector2(0.5f, 0.50f), new Vector2(0.5f, 0.50f), Vector2.zero, new Vector2(800f, MobileUiLayout.TapHeight));
            _doorCycleSlider = sliderGo.GetComponent<Slider>();
            _doorCycleSlider.direction = Slider.Direction.LeftToRight;
            _doorCycleSlider.minValue = 0f;
            _doorCycleSlider.maxValue = 12f;
            _doorCycleSlider.wholeNumbers = true;

            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(sliderGo.transform, false);
            var trackRect = trackGo.GetComponent<RectTransform>();
            SetRect(trackRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 18f));
            var trackImage = trackGo.GetComponent<Image>();
            trackImage.color = new Color(0.12f, 0.19f, 0.28f, 1f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = ColTeal;

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            SetRect(handleArea.GetComponent<RectTransform>(), new Vector2(0f, .5f), new Vector2(1f, .5f), Vector2.zero, Vector2.zero);
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handleRect = handleGo.GetComponent<RectTransform>();
            SetRect(handleRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = ColYellow;
            if (s_discSprite == null) s_discSprite = CreateDiscSprite();
            handleImage.sprite = s_discSprite;
            _doorCycleSlider.targetGraphic = handleImage;
            _doorCycleSlider.fillRect = fillRect;
            _doorCycleSlider.handleRect = handleRect;
            _doorCycleSlider.onValueChanged.AddListener(OnDoorCycleTimeChanged);

            _doorCycleTimeText = MakeLabel(_doorCycleGroup.transform, "DoorCycleTime", "", 30, FontStyle.Bold, ColYellow, TextAnchor.MiddleCenter);
            SetRect(_doorCycleTimeText.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(300f, 56f));
            for (int i = 0; i <= 12; i++)
            {
                var tick = MakeLabel(_doorCycleGroup.transform, "TimeTick_" + i, i.ToString(), 28, FontStyle.Bold, ColCream, TextAnchor.MiddleCenter);
                float x = 0.08f + 0.84f * i / 12f;
                SetRect(tick.rectTransform, new Vector2(x, 0.43f), new Vector2(x, 0.43f), Vector2.zero, new Vector2(38f, 38f));
                _doorCycleTicks.Add(tick);
            }

            _doorCycleSubmitButton = MakeButton(_puzzlePanel.transform, "DoorCycleSubmitBtn", ColYellow,
                new Vector2(0.5f, 0.14f), new Vector2(500f, 86f), SubmitDoorCycleTime);
            _doorCycleSubmitText = MakeLabel(_doorCycleSubmitButton.transform, "Label", "", 28, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_doorCycleSubmitText.rectTransform);
            _doorCycleGroup.SetActive(false);
            _doorCycleSubmitButton.gameObject.SetActive(false);
        }

        void OnDoorCycleTimeChanged(float value)
        {
            _doorCycleTime = Mathf.RoundToInt(value);
            RefreshDoorCycleUi();
        }

        void SubmitDoorCycleTime() => _gm.SubmitDoorCycleAttempt(_doorCycleTime);

        public void RevealDoorCycleHint()
        {
            var puzzle = _gm.ActivePuzzle;
            if (puzzle == null || puzzle.kind != PuzzleKind.DoorCycle) return;
            _doorCycleTime = puzzle.answerValue;
            _doorCycleSlider.SetValueWithoutNotify(_doorCycleTime);
            RefreshDoorCycleUi();
            ShowToast(Loc.T("door_cycle_hint"));
        }

        void RefreshDoorCycleUi()
        {
            var puzzle = _gm.ActivePuzzle;
            if (puzzle == null || puzzle.kind != PuzzleKind.DoorCycle) return;
            _doorCycleTime = Mathf.Clamp(_doorCycleTime, 0, 12);
            _doorCycleSlider.SetValueWithoutNotify(_doorCycleTime);
            int[] periods = puzzle.doorPeriods ?? puzzle.ints;
            int a = periods != null && periods.Length > 0 ? periods[0] : 3;
            int b = periods != null && periods.Length > 1 ? periods[1] : 4;
            bool aOpen = _doorCycleTime % a == 0;
            bool bOpen = _doorCycleTime % b == 0;
            _doorCycleDoorTexts[0].text = $"A · {a}  |  {Loc.T(aOpen ? "door_open" : "door_closed")}";
            _doorCycleDoorTexts[1].text = $"B · {b}  |  {Loc.T(bOpen ? "door_open" : "door_closed")}";
            _doorCycleDoorImages[0].color = aOpen ? ColTeal : ColNavySoft;
            _doorCycleDoorImages[1].color = bOpen ? ColTeal : ColNavySoft;
            _doorCycleTimeText.text = $"t = {_doorCycleTime}";
            _doorCycleStatusText.text = Loc.T("door_cycle_status");
            _doorCycleSubmitText.text = Loc.T("grid_submit");
            for (int i = 0; i < _doorCycleTicks.Count; i++)
                _doorCycleTicks[i].color = i == _doorCycleTime ? ColYellow : ColCream;
        }
    }
}

using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        const float CardBottomMargin = 40f;
        void CardRect(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect != null) SetRect(rect, new Vector2(x, 0f), new Vector2(x, 0f), new Vector2(0f, y), new Vector2(width, height));
        }
        void ApplyPuzzleCardLayout(PuzzleData puzzle)
        {
            float height = MobileUiLayout.CardHeight(puzzle.kind);
            bool compact = height == MobileUiLayout.ChoiceHeight;
            var card = _puzzlePanel.GetComponent<RectTransform>();
            CardRect(card, .5f, CardBottomMargin + height * .5f, MobileUiLayout.PanelWidth, height);
            CardRect(_timerText.rectTransform, .25f, height - 76f, 210f, 64f);
            CardRect(ChildRect(card, "StopwatchIcon"), .095f, height - 76f, 62f, 70f);
            CardRect(ChildRect(card, "TimerBarBg"), .65f, height - 76f, 560f, 28f);
            CardRect(_categoryBadgeText.rectTransform, .5f, height - 138f, 930f, 40f);
            _categoryBadgeText.resizeTextMinSize = 24; _categoryBadgeText.resizeTextMaxSize = 28;
            _promptText.resizeTextForBestFit = true;
            _promptText.resizeTextMinSize = 32; _promptText.resizeTextMaxSize = compact ? 48 : 40;
            _promptText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _promptText.verticalOverflow = VerticalWrapMode.Truncate;
            CardRect(_promptText.rectTransform, .5f, compact ? 625f : height - (puzzle.kind == PuzzleKind.Grid ? 240f : 256f), 930f, puzzle.kind == PuzzleKind.Grid ? 140f : 180f);
            if (puzzle.kind == PuzzleKind.Akari || puzzle.kind == PuzzleKind.ValveCause)
                CardRect(_promptText.rectTransform, .5f, 962f, 930f, 220f);
            CardRect(_choiceGrid.GetComponent<RectTransform>(), .5f, 340f, 940f, 360f);
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                var anchor = new Vector2((i & 1) == 0 ? .25f : .75f, i < 2 ? .74f : .24f);
                SetRect(_choiceButtons[i].GetComponent<RectTransform>(), anchor, anchor, Vector2.zero, new Vector2(444f, 166f));
                SetRect(_choiceGlowFrames[i].rectTransform, anchor, anchor, Vector2.zero, new Vector2(460f, 182f));
                _choiceLabels[i].resizeTextForBestFit = true; _choiceLabels[i].resizeTextMinSize = 32;
                _choiceLabels[i].resizeTextMaxSize = 54; _choiceLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
            }
            CardRect(_rewindBtn.GetComponent<RectTransform>(), puzzle.kind == PuzzleKind.Grid ? .17f : .74f, MobileUiLayout.FooterCenter,
                puzzle.kind == PuzzleKind.Grid ? 280f : 440f, MobileUiLayout.TapHeight);
            _rewindBtnText.fontSize = 32;
            CardRect(_bellExtraRow.GetComponent<RectTransform>(), .26f, MobileUiLayout.FooterCenter, 440f, MobileUiLayout.TapHeight);
            CardRect(_orderResetRow.GetComponent<RectTransform>(), .26f, MobileUiLayout.FooterCenter, 440f, MobileUiLayout.TapHeight);
            SizeCenteredChild(_bellExtraRow.transform, "ReplayBellBtn", 440f, MobileUiLayout.TapHeight);
            SizeCenteredChild(_orderResetRow.transform, "OrderResetBtn", 440f, MobileUiLayout.TapHeight);
            CardRect(_gridClearButton.GetComponent<RectTransform>(), .5f, MobileUiLayout.FooterCenter, 280f, MobileUiLayout.TapHeight);
            CardRect(_gridSubmitButton.GetComponent<RectTransform>(), .83f, MobileUiLayout.FooterCenter, 280f, MobileUiLayout.TapHeight);
            CardRect(_lettersGroup.GetComponent<RectTransform>(), .5f, 480f, 920f, 430f);
            for (int i = 0; i < _letterTileButtons.Count; i++)
            {
                var anchor = new Vector2(.125f + (i % 4) * .25f, i < 4 ? .55f : .18f);
                SetRect(_letterTileButtons[i].GetComponent<RectTransform>(), anchor, anchor, Vector2.zero, new Vector2(210f, 144f));
            }
            CardRect(_gridGroup.GetComponent<RectTransform>(), .5f, 558f, 920f, 720f);
            SetRect(_gridStatusText.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, -22f), new Vector2(860f, 34f));
            _gridStatusText.fontSize = 28;
            CardRect(_compareGroup.GetComponent<RectTransform>(), .5f, 510f, 944f, 600f);
            CardRect(_bridgeGroup.GetComponent<RectTransform>(), .5f, 500f, 940f, 500f);
            int weightCount = Mathf.Clamp(puzzle.ints != null ? puzzle.ints.Length : 1, 1, _weightButtons.Count);
            bool twoWeightRows = weightCount > 5;
            for (int i = 0; i < _weightButtons.Count; i++)
            {
                var anchor = twoWeightRows
                    ? new Vector2(.167f + i % 3 * .333f, i < 3 ? .80f : .48f)
                    : new Vector2((i + .5f) / weightCount, .59f);
                SetRect(_weightButtons[i].GetComponent<RectTransform>(), anchor, anchor, Vector2.zero,
                    new Vector2(twoWeightRows ? 190f : Mathf.Max(144f, Mathf.Min(184f, 940f / weightCount - 16f)), 144f));
            }
            Place(_bridgeGroup.transform, "BridgeStatus", new Vector2(.5f, 1.06f), new Vector2(880f, 56f));
            Place(_bridgeGroup.transform, "ClearBtn", new Vector2(.26f, .13f), new Vector2(400f, 144f));
            Place(_bridgeGroup.transform, "LiftBtn", new Vector2(.73f, .13f), new Vector2(440f, 144f));
            CardRect(_torchBridgeGroup.GetComponent<RectTransform>(), .5f, 540f, 940f, 420f);
            CardRect(_miniGroup.GetComponent<RectTransform>(), .5f, 540f, 940f, 330f);
            CardRect(_timingGroup.GetComponent<RectTransform>(), .5f, 540f, 940f, 360f);
            CardRect(_miniSubmitButton.GetComponent<RectTransform>(), .5f, 252f, 540f, MobileUiLayout.TapHeight);
            CardRect(_timingStopButton.GetComponent<RectTransform>(), .5f, 252f, 540f, MobileUiLayout.TapHeight);
            if (_akariGroup != null)
            {
                CardRect(_akariGroup.GetComponent<RectTransform>(), .5f, 588f, 920f, 500f);
                for (int i = 0; i < _akariCells.Length; i++)
                {
                    var anchor = new Vector2(.30f + (i % 3) * .20f, .79f - (i / 3) * .32f);
                    SetRect(_akariCells[i].GetComponent<RectTransform>(), anchor, anchor, Vector2.zero, new Vector2(164f, 144f));
                }
                SetRect(_akariStatusText.rectTransform, new Vector2(.5f, .97f), new Vector2(.5f, .97f), Vector2.zero, new Vector2(860f, 32f));
                _akariStatusText.fontSize = 28;
                CardRect(_akariClearButton.GetComponent<RectTransform>(), .25f, 252f, 420f, MobileUiLayout.TapHeight);
                CardRect(_akariSubmitButton.GetComponent<RectTransform>(), .74f, 252f, 440f, MobileUiLayout.TapHeight);
            }
            if (_doorCycleGroup != null)
            {
                CardRect(_doorCycleGroup.GetComponent<RectTransform>(), .5f, 560f, 940f, 500f);
                Place(_doorCycleGroup.transform, "DoorCycleHeader", new Vector2(.5f, .95f), new Vector2(880f, 56f));
                Place(_doorCycleGroup.transform, "DoorCycleTime", new Vector2(.5f, .16f), new Vector2(360f, 56f));
                for (int i = 0; i < _doorCycleTicks.Count; i++)
                {
                    SetRect(_doorCycleTicks[i].rectTransform, new Vector2(.08f + .84f * i / 12f, .32f),
                        new Vector2(.08f + .84f * i / 12f, .32f), Vector2.zero, new Vector2(48f, 42f));
                    _doorCycleTicks[i].fontSize = 28;
                }
                CardRect(_doorCycleSubmitButton.GetComponent<RectTransform>(), .5f, 252f, 540f, MobileUiLayout.TapHeight);
            }
            if (_valveCauseGroup != null)
            {
                CardRect(_valveCauseGroup.GetComponent<RectTransform>(), .5f, 560f, 940f, 500f);
                Place(_valveCauseGroup.transform, "ValveCauseHeader", new Vector2(.5f, .95f), new Vector2(880f, 54f));
                Place(_valveCauseGroup.transform, "ValveSwitch", new Vector2(.5f, .73f), new Vector2(600f, 144f));
                Place(_valveCauseGroup.transform, "FountainChannel", new Vector2(.27f, .42f), new Vector2(380f, 124f));
                Place(_valveCauseGroup.transform, "WheelChannel", new Vector2(.73f, .42f), new Vector2(380f, 124f));
                Place(_valveCauseGroup.transform, "BridgeState", new Vector2(.5f, .15f), new Vector2(860f, 60f));
                CardRect(_valveCauseStepButton.GetComponent<RectTransform>(), .26f, 252f, 440f, MobileUiLayout.TapHeight);
                CardRect(_valveCauseSubmitButton.GetComponent<RectTransform>(), .74f, 252f, 440f, MobileUiLayout.TapHeight);
            }
        }
        static void SizeCenteredChild(Transform parent, string path, float width, float height)
        {
            var rect = ChildRect(parent, path);
            if (rect != null) SetRect(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, height));
        }
    }
}

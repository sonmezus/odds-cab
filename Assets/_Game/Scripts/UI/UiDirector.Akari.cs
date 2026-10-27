using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        GameObject _akariGroup;
        UiText _akariStatusText;
        Button[] _akariCells;
        Image[] _akariCellImages;
        UiText[] _akariCellLabels;
        Button _akariClearButton;
        UiText _akariClearText;
        Button _akariSubmitButton;
        UiText _akariSubmitText;
        int _akariLampMask;

        void EnsureAkariUi()
        {
            if (_akariGroup != null) return;
            _akariGroup = MakePanel(_puzzlePanel.transform, "AkariPuzzleGroup", Color.clear,
                new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.33f), Vector2.zero, new Vector2(850f, 520f));
            _akariGroup.GetComponent<Image>().raycastTarget = false;
            _akariStatusText = MakeLabel(_akariGroup.transform, "AkariStatus", "", 25, FontStyle.Bold, ColTeal, TextAnchor.MiddleCenter);
            SetRect(_akariStatusText.rectTransform, new Vector2(0.5f, 0.95f), new Vector2(0.5f, 0.95f), Vector2.zero, new Vector2(820f, 44f));

            _akariCells = new Button[9];
            _akariCellImages = new Image[9];
            _akariCellLabels = new UiText[9];
            for (int cell = 0; cell < 9; cell++)
            {
                int captured = cell;
                int x = cell % 3;
                int y = cell / 3;
                var button = MakeButton(_akariGroup.transform, "AkariCell_" + cell, ColCream,
                    // 3x3 board of square-ish cells with a gap between rows (rows used to overlap and read as columns)
                    new Vector2(0.30f + x * 0.20f, 0.74f - y * 0.27f), new Vector2(156f, 98f),
                    () => ToggleAkariLamp(captured));
                _akariCells[cell] = button;
                _akariCellImages[cell] = button.GetComponent<Image>();
                _akariCellLabels[cell] = MakeLabel(button.transform, "CellLabel", "", 30, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
                Stretch(_akariCellLabels[cell].rectTransform);
                if (cell == 4)
                {
                    button.interactable = false;
                    _akariCellImages[cell].color = ColNavySoft;
                }
            }

            _akariClearButton = MakeButton(_puzzlePanel.transform, "AkariClearBtn", ColNavySoft,
                new Vector2(0.23f, 0.14f), new Vector2(330f, 86f), ClearAkariLamps);
            _akariClearText = MakeLabel(_akariClearButton.transform, "Label", "", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(_akariClearText.rectTransform);
            _akariSubmitButton = MakeButton(_puzzlePanel.transform, "AkariSubmitBtn", ColYellow,
                new Vector2(0.70f, 0.14f), new Vector2(430f, 86f), SubmitAkariLamps);
            _akariSubmitText = MakeLabel(_akariSubmitButton.transform, "Label", "", 28, FontStyle.Bold, ColTextDark, TextAnchor.MiddleCenter);
            Stretch(_akariSubmitText.rectTransform);
            _akariGroup.SetActive(false);
            _akariClearButton.gameObject.SetActive(false);
            _akariSubmitButton.gameObject.SetActive(false);
        }

        void ToggleAkariLamp(int cell)
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.Akari || cell == 4) return;
            int bit = 1 << cell;
            if ((_akariLampMask & bit) == 0 && CountSetBits(_akariLampMask) >= 2) return;
            _akariLampMask ^= bit;
            SynthAudio.Instance?.PlayClick();
            RefreshAkariUi();
        }

        void ClearAkariLamps()
        {
            _akariLampMask = 0;
            SynthAudio.Instance?.PlayClick();
            RefreshAkariUi();
        }

        void SubmitAkariLamps()
        {
            if (CountSetBits(_akariLampMask) != 2) return;
            _gm.SubmitAkariAttempt(_akariLampMask);
        }

        public void RevealAkariHint()
        {
            var puzzle = _gm.ActivePuzzle;
            if (puzzle == null || puzzle.kind != PuzzleKind.Akari || puzzle.akariSolutions == null) return;
            int target = puzzle.akariSolutions[0];
            foreach (var solution in puzzle.akariSolutions)
            {
                if ((_akariLampMask & solution) == _akariLampMask) { target = solution; break; }
            }
            int missing = target & ~_akariLampMask;
            if (missing != 0 && CountSetBits(_akariLampMask) < 2)
            {
                for (int cell = 0; cell < 9; cell++)
                {
                    if ((missing & (1 << cell)) == 0) continue;
                    _akariLampMask |= 1 << cell;
                    break;
                }
            }
            RefreshAkariUi();
            ShowToast(Loc.T("akari_hint"));
        }

        void RefreshAkariUi()
        {
            var puzzle = _gm.ActivePuzzle;
            if (puzzle == null || puzzle.kind != PuzzleKind.Akari) return;
            int litMask = GetAkariLitMask(_akariLampMask);
            int litCount = CountSetBits(litMask & ~(1 << 4));
            int lampCount = CountSetBits(_akariLampMask);
            _akariStatusText.text = Loc.F("akari_status", lampCount, litCount);
            _akariClearText.text = Loc.T("grid_clear");
            _akariSubmitText.text = Loc.T("grid_submit");
            _akariSubmitButton.interactable = lampCount == 2;
            for (int cell = 0; cell < 9; cell++)
            {
                if (cell == 4)
                {
                    _akariCellLabels[cell].text = Loc.T("akari_wall");
                    _akariCellLabels[cell].color = Color.white;
                    continue;
                }
                bool hasLamp = (_akariLampMask & (1 << cell)) != 0;
                bool isLit = (litMask & (1 << cell)) != 0;
                _akariCellLabels[cell].text = hasLamp ? "●" : "";
                _akariCellLabels[cell].color = ColTextDark;
                _akariCellImages[cell].color = hasLamp ? ColYellow : (isLit ? new Color(1f, 0.91f, 0.62f, 1f) : ColCream);
            }
        }

        static int GetAkariLitMask(int lamps)
        {
            int lit = lamps;
            for (int cell = 0; cell < 9; cell++)
            {
                if ((lamps & (1 << cell)) == 0) continue;
                int x = cell % 3;
                int y = cell / 3;
                foreach (var direction in AkariDirections)
                {
                    int nx = x + direction.x;
                    int ny = y + direction.y;
                    while (nx >= 0 && nx < 3 && ny >= 0 && ny < 3)
                    {
                        int next = ny * 3 + nx;
                        if (next == 4) break;
                        lit |= 1 << next;
                        nx += direction.x;
                        ny += direction.y;
                    }
                }
            }
            return lit;
        }

        static readonly Vector2Int[] AkariDirections =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        static int CountSetBits(int value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }
    }
}

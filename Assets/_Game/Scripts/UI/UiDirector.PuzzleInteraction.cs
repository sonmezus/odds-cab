using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using OddsCab.Puzzles;

namespace OddsCab
{
    public partial class UiDirector
    {
        void PopulatePuzzleCard()
        {
            var p = _gm.ActivePuzzle;
            if (p == null) return;

            if (p.kind == PuzzleKind.Route)
            {
                if (_routeTitleText != null) _routeTitleText.text = Loc.T("route_puzzle_title");
                if (_routeSubText != null) _routeSubText.text = Loc.T("route_puzzle_sub");
                if (_routeHintText != null) _routeHintText.text = Loc.F("hint", GameConfig.HintCost);
                if (_routePassengerPortraitImg != null && _gm.ActivePassengerProfile != null)
                {
                    var sprites = GetOrRenderExpressionPortraits(_gm.ActivePassengerProfile);
                    if (sprites != null && sprites.Length > 0)
                        _routePassengerPortraitImg.sprite = sprites[(int)PassengerExpression.Curious];
                }
                return;
            }

            _selectedChoiceIndex = -1;
            _orderTaps.Clear();

            string catKey = "category_" + p.category;
            string diffKey = "difficulty_" + Mathf.Clamp(p.difficulty, 0, 2);
            int streak = SaveSystem.Data.streak;
            string streakPart = streak > 0 ? $"  ·  {Loc.F("streak", streak)}" : "";
            _categoryBadgeText.text = $"{_gm.ActivePassengerProfile.DisplayName}  ·  {Loc.T(catKey)} ({Loc.T(diffKey)}){streakPart}";
            _promptText.text = p.Prompt;
            _rewindBtn.gameObject.SetActive(true);
            _rewindBtnText.text = Loc.F("hint", GameConfig.HintCost);

            bool isBridge = p.kind == PuzzleKind.Bridge;
            bool isTorchBridge = p.kind == PuzzleKind.TorchBridge;
            bool isAkari = p.kind == PuzzleKind.Akari;
            bool isDoorCycle = p.kind == PuzzleKind.DoorCycle;
            bool isValveCause = p.kind == PuzzleKind.ValveCause;
            bool isBells = p.kind == PuzzleKind.Bells;

            if (isAkari) EnsureAkariUi();
            if (isDoorCycle) EnsureDoorCycleUi();
            if (isValveCause) EnsureValveCauseUi();
            ApplyPuzzleCardLayout(p);   // after the lazily built groups exist, so they get resized too

            bool isLetters = p.kind == PuzzleKind.Letters;
            bool isGrid = p.kind == PuzzleKind.Grid;
            bool isCompare = p.kind == PuzzleKind.Compare;
            bool isTiming = p.kind == PuzzleKind.Timing;
            bool isValve = p.kind == PuzzleKind.Valve;
            bool isLantern = p.kind == PuzzleKind.Lantern;
            bool isShadow = p.kind == PuzzleKind.Shadow;
            bool isMiniGame = isValve || isLantern || isShadow;
            _choiceGrid.SetActive(!isBridge && !isTorchBridge && !isAkari && !isDoorCycle && !isValveCause && !isLetters && !isGrid && !isCompare && !isTiming && !isMiniGame);
            _bellExtraRow.SetActive(isBells);
            _bridgeGroup.SetActive(isBridge);
            if (_torchBridgeGroup != null) _torchBridgeGroup.SetActive(isTorchBridge);
            if (_akariGroup != null) _akariGroup.SetActive(isAkari);
            if (_akariClearButton != null) _akariClearButton.gameObject.SetActive(isAkari);
            if (_akariSubmitButton != null) _akariSubmitButton.gameObject.SetActive(isAkari);
            if (_doorCycleGroup != null) _doorCycleGroup.SetActive(isDoorCycle);
            if (_doorCycleSubmitButton != null) _doorCycleSubmitButton.gameObject.SetActive(isDoorCycle);
            if (_valveCauseGroup != null) _valveCauseGroup.SetActive(isValveCause);
            if (_valveCauseStepButton != null) _valveCauseStepButton.gameObject.SetActive(isValveCause);
            if (_valveCauseSubmitButton != null) _valveCauseSubmitButton.gameObject.SetActive(isValveCause);
            if (_lettersGroup != null) _lettersGroup.SetActive(isLetters);
            if (_gridGroup != null) _gridGroup.SetActive(isGrid);
            if (_compareGroup != null) _compareGroup.SetActive(isCompare);
            if (_timingGroup != null) _timingGroup.SetActive(isTiming);
            if (_timingStopButton != null) _timingStopButton.gameObject.SetActive(isTiming);
            if (_miniGroup != null) _miniGroup.SetActive(isMiniGame);
            if (_miniSubmitButton != null) _miniSubmitButton.gameObject.SetActive(isValve || isLantern);
            if (_gridClearButton != null) _gridClearButton.gameObject.SetActive(isGrid);
            if (_gridSubmitButton != null) _gridSubmitButton.gameObject.SetActive(isGrid);
            if (_rewindBtn != null)
            {
                var rewindRect = _rewindBtn.GetComponent<RectTransform>();
                if (isGrid)
                {
                    CardRect(rewindRect, .17f, MobileUiLayout.FooterCenter, 280f, MobileUiLayout.TapHeight);
                    _rewindBtnText.fontSize = 32;
                }
                else
                {
                    CardRect(rewindRect, .74f, MobileUiLayout.FooterCenter, 440f, MobileUiLayout.TapHeight);
                    _rewindBtnText.fontSize = 32;
                }
            }
            if (_gridClearText != null) _gridClearText.text = Loc.T("grid_clear");
            if (_gridSubmitText != null) _gridSubmitText.text = Loc.T("grid_submit");
            _compareHintRevealed = false;
            _miniSelectedMask = 0;
            _akariLampMask = 0;
            _doorCycleTime = 0;
            _valveCauseSide = 0;
            _valveCauseSteps = 0;
            _valveCauseChanged = false;
            _shadowHintRevealed = false;
            _letterPicks.Clear();
            bool isOrdering = p.kind == PuzzleKind.Ordering;
            if (_orderResetRow != null) _orderResetRow.SetActive(isOrdering || isLetters);
            if (_orderResetText != null) _orderResetText.text = Loc.T(isLetters ? "letters_delete" : "order_reset");
            if (isLetters)
            {
                RefreshLetters();
                return;
            }

            if (isGrid)
            {
                _gridWorkingOwners = new int[p.gridClues.Length];
                for (int i = 0; i < _gridWorkingOwners.Length; i++) _gridWorkingOwners[i] = -1;
                _gridStatusText.color = ColTeal;
                _gridStatusText.text = "";
                _gridSelectedClue = -1;
                _gridFirstCorner = -1;
                RefreshGridUi();
                return;
            }

            if (isCompare)
            {
                RefreshCompareUi();
                return;
            }

            if (isTorchBridge)
            {
                RefreshTorchBridgeUi();
                return;
            }

            if (isAkari)
            {
                RefreshAkariUi();
                return;
            }

            if (isDoorCycle)
            {
                RefreshDoorCycleUi();
                return;
            }

            if (isValveCause)
            {
                RefreshValveCauseUi();
                return;
            }

            if (isTiming)
            {
                RefreshTimingUi(0);
                return;
            }

            if (isMiniGame)
            {
                RefreshMiniGame();
                return;
            }

            if (isBridge)
            {
                for (int i = 0; i < _weightButtons.Count; i++)
                {
                    bool active = p.ints != null && i < p.ints.Length;
                    _weightButtons[i].gameObject.SetActive(active);
                    if (active) _weightLabels[i].text = p.ints[i] + "\nkg";
                }
                RefreshBridgeUi();
            }
            else
            {
                RefreshPuzzleOptions();
            }
        }

        void OnOptionButtonClicked(int optionIndex)
        {
            var ap = _gm.ActivePuzzle;
            if (ap != null && ap.kind == PuzzleKind.Ordering)
            {
                if (_orderTaps.Contains(optionIndex)) return;
                _orderTaps.Add(optionIndex);
                SynthAudio.Instance?.PlayClick();
                RefreshPuzzleOptions();
                if (_orderTaps.Count == 4) _gm.SubmitOrderAttempt(_orderTaps.ToArray());
                return;
            }
            _selectedChoiceIndex = optionIndex;
            RefreshPuzzleOptions();
            _gm.SubmitChoiceAnswer(optionIndex);
        }

        public void RefreshPuzzleOptions()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind == PuzzleKind.Bridge || p.kind == PuzzleKind.Grid || p.kind == PuzzleKind.Compare ||
                p.kind == PuzzleKind.Timing || p.kind == PuzzleKind.TorchBridge || p.kind == PuzzleKind.Akari || p.kind == PuzzleKind.DoorCycle
                || p.kind == PuzzleKind.ValveCause || p.kind == PuzzleKind.Valve || p.kind == PuzzleKind.Lantern || p.kind == PuzzleKind.Shadow) return;
            var opts = p.Options;
            if (p.kind == PuzzleKind.Ordering)
            {
                for (int i = 0; i < 4; i++)
                {
                    int pos = _orderTaps.IndexOf(i);
                    string name = (opts != null && i < opts.Length) ? opts[i] : "";
                    _choiceButtons[i].interactable = pos < 0;
                    _choiceLabels[i].text = pos >= 0 ? $"{pos + 1}.  {name}" : name;
                    _choiceImages[i].color = pos >= 0 ? new Color(0.70f, 0.90f, 0.86f, 1f) : ColCream;
                    if (_choiceGlowFrames[i] != null) _choiceGlowFrames[i].color = new Color(1f, 0.98f, 0.90f, 0f);
                }
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                bool disabled = _gm.DisabledOptions[i];
                bool selected = (_selectedChoiceIndex == i);
                _choiceButtons[i].interactable = !disabled;
                _choiceLabels[i].text = (opts != null && i < opts.Length) ? opts[i] : "";
                _choiceImages[i].color = disabled
                    ? new Color(0.45f, 0.45f, 0.48f, 0.55f)
                    : (selected ? new Color(1.0f, 0.98f, 0.92f, 1f) : ColCream);
                if (_choiceGlowFrames[i] != null)
                {
                    _choiceGlowFrames[i].color = selected
                        ? new Color(1.0f, 0.96f, 0.82f, 0.92f)
                        : new Color(1f, 0.98f, 0.90f, 0f);
                }
            }
        }

        void OnSecondaryRowClicked()
        {
            var p = _gm.ActivePuzzle;
            SynthAudio.Instance?.PlayClick();
            if (p != null && p.kind == PuzzleKind.Letters)
            {
                if (_letterPicks.Count > 0) _letterPicks.RemoveAt(_letterPicks.Count - 1);
                RefreshLetters();
                return;
            }
            _orderTaps.Clear();
            RefreshPuzzleOptions();
        }

        string BuiltWord()
        {
            var p = _gm.ActivePuzzle;
            var tiles = p?.Letters ?? "";
            var sb = new System.Text.StringBuilder();
            foreach (var i in _letterPicks) if (i < tiles.Length) sb.Append(tiles[i]);
            return sb.ToString();
        }

        void OnLetterTileClicked(int index)
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Letters || _gm.State != GameState.PuzzleActive) return;
            string answer = p.Answer ?? "";
            if (_letterPicks.Contains(index) || _letterPicks.Count >= answer.Length) return;
            _letterPicks.Add(index);
            SynthAudio.Instance?.PlayClick();
            RefreshLetters();
            if (_letterPicks.Count == answer.Length) _gm.SubmitLettersAttempt(BuiltWord());
        }

        void RefreshLetters()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Letters) return;
            string answer = p.Answer ?? "";
            string tiles = p.Letters ?? "";
            string built = BuiltWord();

            // Slots: centred row, one per answer letter
            int n = Mathf.Min(answer.Length, MaxLetterSlots);
            float slotW = 112f;
            for (int i = 0; i < MaxLetterSlots; i++)
            {
                bool on = i < n;
                _letterSlotImages[i].gameObject.SetActive(on);
                if (!on) continue;
                var rt = _letterSlotImages[i].rectTransform;
                rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * slotW, 0f);
                _letterSlotTexts[i].text = i < built.Length ? built[i].ToString() : "";
                _letterSlotImages[i].color = i < built.Length ? new Color(0.20f, 0.52f, 0.50f, 1f) : ColNavySoft;
            }

            for (int i = 0; i < MaxLetterTiles; i++)
            {
                bool on = i < tiles.Length;
                _letterTileButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                bool used = _letterPicks.Contains(i);
                _letterTileTexts[i].text = tiles[i].ToString();
                _letterTileButtons[i].interactable = !used;
                _letterTileImages[i].color = used ? new Color(0.45f, 0.45f, 0.48f, 0.45f) : ColCream;
            }
        }

        /// <summary>Hint for letter puzzles: keep the correct prefix and place the next correct letter.</summary>
        public void RevealNextLetter()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Letters) return;
            string answer = p.Answer ?? "";
            string tiles = p.Letters ?? "";
            string built = BuiltWord();
            int k = 0;
            while (k < built.Length && k < answer.Length && built[k] == answer[k]) k++;
            if (_letterPicks.Count > k) _letterPicks.RemoveRange(k, _letterPicks.Count - k);
            if (k < answer.Length)
            {
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (!_letterPicks.Contains(i) && tiles[i] == answer[k]) { _letterPicks.Add(i); break; }
                }
            }
            RefreshLetters();
            if (_letterPicks.Count == answer.Length) _gm.SubmitLettersAttempt(BuiltWord());
        }

        /// <summary>Hint for ordering puzzles: keep the correct prefix of the taps and place the next correct item.</summary>
        public void RevealNextOrderStep()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Ordering || p.ints == null) return;
            int k = 0;
            while (k < _orderTaps.Count && k < p.ints.Length && _orderTaps[k] == p.ints[k]) k++;
            if (_orderTaps.Count > k) _orderTaps.RemoveRange(k, _orderTaps.Count - k);
            if (k < p.ints.Length) _orderTaps.Add(p.ints[k]);
            RefreshPuzzleOptions();
            if (_orderTaps.Count == 4) _gm.SubmitOrderAttempt(_orderTaps.ToArray());
        }

        void RefreshBridgeUi()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Bridge) return;
            int sum = _gm.BridgeDiorama.CurrentWeightSum;
            _bridgeStatusText.text = $"{sum} / {p.answerValue} kg";

            int mask = _gm.BridgeDiorama.SelectedMask;
            for (int i = 0; i < _weightButtons.Count; i++)
            {
                bool on = (mask & (1 << i)) != 0;
                _weightImages[i].color = on ? ColTeal : ColCream;
            }
        }

        void ToggleTorchTraveler(int index)
        {
            if (_gm.State != GameState.PuzzleActive || _gm.ActivePuzzle?.kind != PuzzleKind.TorchBridge) return;
            _gm.TorchBridge.ToggleTraveler(index);
            RefreshTorchBridgeUi();
        }

        void ClearTorchSelection()
        {
            if (_gm.State != GameState.PuzzleActive || _gm.ActivePuzzle?.kind != PuzzleKind.TorchBridge) return;
            SynthAudio.Instance?.PlayClick();
            _gm.TorchBridge.ClearSelection();
            RefreshTorchBridgeUi();
        }

        public void RefreshTorchBridgeUi()
        {
            var puzzle = _gm.ActivePuzzle;
            var controller = _gm.TorchBridge;
            if (puzzle == null || puzzle.kind != PuzzleKind.TorchBridge || controller == null) return;

            string bank = Loc.T(controller.TorchOnRightBank ? "torch_right_bank" : "torch_left_bank");
            _torchBridgeStatusText.text = Loc.F("torch_status", bank, controller.ElapsedMinutes, controller.TargetMinutes);
            for (int i = 0; i < _torchTravelerButtons.Length; i++)
            {
                int minutes = puzzle.ints != null && i < puzzle.ints.Length ? puzzle.ints[i] : i + 1;
                _torchTravelerLabels[i].text = Loc.F("torch_traveler_time", minutes);
                _torchTravelerImages[i].color = controller.IsSelected(i) ? ColTeal : ColCream;
                _torchTravelerButtons[i].interactable = controller.CanSelectTraveler(i);
            }
            _torchCrossButton.interactable = controller.CanCross;
            _torchCrossText.text = controller.CanCross
                ? Loc.F("torch_cross_minutes", controller.SelectedCrossingMinutes)
                : Loc.T("torch_cross");
            _torchClearText.text = Loc.T("torch_clear");
        }

        void OnGridCellClicked(int cell)
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Grid || _gm.State != GameState.PuzzleActive) return;
            _gridStatusText.color = ColTeal;
            int clueOwner = ClueOwnerAtCell(cell);
            if (_gridSelectedClue < 0)
            {
                if (clueOwner >= 0 && !RegionIsPlaced(clueOwner))
                {
                    // A one-cell rectangle has no corners to choose separately.
                    if (p.gridClues[cell] == 1 && _gridWorkingOwners[cell] < 0)
                    {
                        _gridWorkingOwners[cell] = clueOwner;
                        _gridFirstCorner = -1;
                        _gridStatusText.text = Loc.T("grid_region_set");
                    }
                    else
                    {
                        _gridSelectedClue = clueOwner;
                        _gridFirstCorner = -1;
                        _gridStatusText.text = Loc.T("grid_pick_corners");
                    }
                }
                else
                {
                    _gridStatusText.text = Loc.T("grid_select_clue");
                }
                SynthAudio.Instance?.PlayClick();
                RefreshGridUi();
                return;
            }

            if (_gridFirstCorner < 0)
            {
                _gridFirstCorner = cell;
                _gridStatusText.text = Loc.T("grid_pick_second_corner");
                SynthAudio.Instance?.PlayClick();
                RefreshGridUi();
                return;
            }

            int clueCell = CellForClueOwner(_gridSelectedClue);
            int w = p.gridWidth;
            int x0 = _gridFirstCorner % w, y0 = _gridFirstCorner / w;
            int x1 = cell % w, y1 = cell / w;
            int minX = Mathf.Min(x0, x1), maxX = Mathf.Max(x0, x1);
            int minY = Mathf.Min(y0, y1), maxY = Mathf.Max(y0, y1);
            int area = (maxX - minX + 1) * (maxY - minY + 1);
            bool containsClue = clueCell % w >= minX && clueCell % w <= maxX && clueCell / w >= minY && clueCell / w <= maxY;
            int expectedArea = p.gridClues[clueCell];
            bool free = true;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (_gridWorkingOwners[y * w + x] >= 0) free = false;

            if (containsClue && area == expectedArea && free)
            {
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++) _gridWorkingOwners[y * w + x] = _gridSelectedClue;
                _gridStatusText.text = Loc.T("grid_region_set");
                _gridSelectedClue = -1;
                _gridFirstCorner = -1;
            }
            else
            {
                _gridStatusText.text = Loc.T("grid_invalid_rectangle");
                _gridFirstCorner = -1;
            }
            SynthAudio.Instance?.PlayClick();
            RefreshGridUi();
        }

        int ClueOwnerAtCell(int cell)
        {
            var clues = _gm.ActivePuzzle.gridClues;
            int owner = 0;
            for (int i = 0; i < clues.Length; i++)
                if (clues[i] > 0)
                {
                    if (i == cell) return owner;
                    owner++;
                }
            return -1;
        }

        int CellForClueOwner(int clueOwner)
        {
            var clues = _gm.ActivePuzzle.gridClues;
            int owner = 0;
            for (int i = 0; i < clues.Length; i++)
                if (clues[i] > 0)
                {
                    if (owner == clueOwner) return i;
                    owner++;
                }
            return -1;
        }

        bool RegionIsPlaced(int owner)
        {
            for (int i = 0; i < _gridWorkingOwners.Length; i++)
                if (_gridWorkingOwners[i] == owner) return true;
            return false;
        }

        void RefreshGridUi()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Grid || p.gridClues == null) return;
            int w = p.gridWidth, h = p.gridHeight;
            Vector2 pitch = MobileUiLayout.GridPitch(w, h);
            for (int i = 0; i < _gridCellButtons.Count; i++)
            {
                bool active = i < w * h;
                _gridCellButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                int x = i % w, y = i / w;
                var rect = _gridCellButtons[i].GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2((x - (w - 1) * 0.5f) * pitch.x, ((h - 1) * 0.5f - y) * pitch.y);
                rect.sizeDelta = new Vector2(pitch.x - 8f, pitch.y - 8f);
                _gridCellTexts[i].fontSize = 40;
                int clueValue = p.gridClues[i];
                int region = _gridWorkingOwners.Length > i ? _gridWorkingOwners[i] : -1;
                _gridCellTexts[i].text = clueValue > 0 ? clueValue.ToString() : (region >= 0 ? "•" : "");
                _gridCellImages[i].color = clueValue > 0 && region >= 0
                    ? GridRegionColors[region % GridRegionColors.Length]
                    : (clueValue > 0 ? ColYellow : (region >= 0 ? GridRegionColors[region % GridRegionColors.Length] : ColCream));
                _gridCellButtons[i].interactable = region < 0 || clueValue > 0;
                // Locked cells still belong to the same visibly colored rectangle.
                var colors = _gridCellButtons[i].colors;
                colors.disabledColor = Color.white;
                _gridCellButtons[i].colors = colors;
            }
            if (_gridClearText != null) _gridClearText.text = Loc.T("grid_clear");
            if (_gridSubmitText != null) _gridSubmitText.text = Loc.T("grid_submit");
            if (_gridWorkingOwners.Length > 0 && Array.IndexOf(_gridWorkingOwners, -1) < 0)
                _gridStatusText.text = Loc.T("grid_ready");
            if (_gridStatusText != null && string.IsNullOrEmpty(_gridStatusText.text)) _gridStatusText.text = Loc.T("grid_select_clue");
        }

        void ClearGrid()
        {
            for (int i = 0; i < _gridWorkingOwners.Length; i++) _gridWorkingOwners[i] = -1;
            _gridSelectedClue = -1;
            _gridFirstCorner = -1;
            _gridStatusText.color = ColTeal;
            _gridStatusText.text = Loc.T("grid_select_clue");
            RefreshGridUi();
        }

        void SubmitGrid()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Grid || _gm.State != GameState.PuzzleActive || _gridWorkingOwners.Length == 0) return;
            SynthAudio.Instance?.PlayClick();
            int missing = 0;
            bool onlySingles = true;
            for (int i = 0; i < _gridWorkingOwners.Length; i++)
                if (_gridWorkingOwners[i] < 0)
                {
                    missing++;
                    if (p.gridClues[i] != 1) onlySingles = false;
                }
            if (missing > 0)
            {
                _gridStatusText.color = ColRed;
                _gridStatusText.text = Loc.F(onlySingles ? "grid_fill_singles" : "grid_fill_all", missing);
                return;
            }
            _gm.SubmitGridAttempt((int[])_gridWorkingOwners.Clone());
        }

        /// <summary>Fill one complete solution rectangle as the paid hint for a Shikaku puzzle.</summary>
        public void RevealGridHint()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Grid || p.gridSolutionOwners == null) return;
            int owner = -1;
            for (int i = 0; i < p.gridSolutionOwners.Length; i++)
                if (_gridWorkingOwners[i] != p.gridSolutionOwners[i]) { owner = p.gridSolutionOwners[i]; break; }
            if (owner < 0) return;
            var conflicts = new HashSet<int> { owner };
            for (int i = 0; i < p.gridSolutionOwners.Length; i++)
                if (p.gridSolutionOwners[i] == owner && _gridWorkingOwners[i] >= 0)
                    conflicts.Add(_gridWorkingOwners[i]);
            for (int i = 0; i < _gridWorkingOwners.Length; i++)
                if (conflicts.Contains(_gridWorkingOwners[i]) && p.gridSolutionOwners[i] != _gridWorkingOwners[i])
                    _gridWorkingOwners[i] = -1;
            for (int i = 0; i < p.gridSolutionOwners.Length; i++)
                if (p.gridSolutionOwners[i] == owner) _gridWorkingOwners[i] = owner;
            _gridSelectedClue = -1;
            _gridFirstCorner = -1;
            _gridStatusText.color = ColTeal;
            _gridStatusText.text = Loc.T("grid_hint_applied");
            RefreshGridUi();
        }

        void RefreshCompareUi()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Compare || p.compareBefore == null || p.compareAfter == null) return;
            int count = p.compareBefore.Length;
            int size = Mathf.RoundToInt(Mathf.Sqrt(count));
            const float beforePitch = 72f;
            const float afterPitch = 144f;
            _compareBeforeTitle.text = Loc.T("compare_before");
            _compareAfterTitle.text = Loc.T("compare_after");
            SetRect(_compareBeforeTitle.rectTransform, new Vector2(.18f, .975f), new Vector2(.18f, .975f), Vector2.zero, new Vector2(300f, 32f));
            SetRect(_compareAfterTitle.rectTransform, new Vector2(.66f, .975f), new Vector2(.66f, .975f), Vector2.zero, new Vector2(540f, 32f));
            _compareBeforeTitle.fontSize = _compareAfterTitle.fontSize = 28;
            for (int i = 0; i < _compareBeforeCells.Count; i++)
            {
                bool active = i < count;
                _compareBeforeCells[i].SetActive(active);
                _compareAfterButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                int x = i % size, y = i / size;
                var beforeRect = _compareBeforeCells[i].GetComponent<RectTransform>();
                var afterRect = _compareAfterButtons[i].GetComponent<RectTransform>();
                var beforeAnchor = new Vector2(.18f, .5f);
                var afterAnchor = new Vector2(.66f, .5f);
                SetRect(beforeRect, beforeAnchor, beforeAnchor,
                    new Vector2((x - (size - 1) * .5f) * beforePitch, ((size - 1) * .5f - y) * beforePitch - 32f), new Vector2(beforePitch - 8f, beforePitch - 8f));
                SetRect(afterRect, afterAnchor, afterAnchor,
                    new Vector2((x - (size - 1) * .5f) * afterPitch, ((size - 1) * .5f - y) * afterPitch - 32f), new Vector2(afterPitch - 8f, afterPitch - 8f));
                _compareBeforeLabels[i].fontSize = 30;
                _compareAfterLabels[i].fontSize = 44;
                _compareBeforeLabels[i].text = ((char)('A' + (p.compareBefore[i] - 1) % 26)).ToString();
                _compareAfterLabels[i].text = ((char)('A' + (p.compareAfter[i] - 1) % 26)).ToString();
                _compareAfterImages[i].color = _compareHintRevealed && i == p.correct ? ColTeal : ColCream;
            }
        }

        public void RevealCompareHint()
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.Compare) return;
            _compareHintRevealed = true;
            RefreshCompareUi();
            ShowToast(Loc.T("compare_hint"));
        }

        public void RefreshTimingUi(int gateIndex)
        {
            var p = _gm.ActivePuzzle;
            if (p == null || p.kind != PuzzleKind.Timing || p.timingTargets == null || p.timingTargets.Length == 0) return;
            _timingDisplayedGate = Mathf.Clamp(gateIndex, 0, p.timingTargets.Length - 1);
            _timingProgressText.text = Loc.F("timing_progress", _timingDisplayedGate + 1, p.timingTargets.Length);
            _timingStopText.text = Loc.T("timing_stop");
            float normalizedTarget = p.timingTargets[_timingDisplayedGate] / 1000f;
            _timingTargetRect.anchoredPosition = new Vector2((normalizedTarget - 0.5f) * _timingTrackRect.rect.width, 0f);
            _timingTargetRect.sizeDelta = new Vector2(Mathf.Max(34f, p.timingTolerance * 2f / 1000f * _timingTrackRect.rect.width), 78f);
            _timingMarkerPosition = 0f;
        }

        void StopTimingMarker()
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.Timing) return;
            _gm.SubmitTimingAttempt(_timingMarkerPosition);
        }

        void RefreshMiniGame()
        {
            var p = _gm.ActivePuzzle;
            if (p == null) return;
            bool isValve = p.kind == PuzzleKind.Valve;
            bool isLantern = p.kind == PuzzleKind.Lantern;
            bool isShadow = p.kind == PuzzleKind.Shadow;
            int count = isValve ? Mathf.Min(_miniButtons.Length, p.ints != null ? p.ints.Length : 3) : 4;
            for (int i = 0; i < _miniButtons.Length; i++)
            {
                bool active = i < count;
                _miniButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                _miniButtonLabels[i].text = isValve ? p.ints[i].ToString() : (p.Options != null ? p.Options[i] : (i + 1).ToString());
                bool selected = isShadow ? (_shadowHintRevealed && i == p.correct) : (_miniSelectedMask & (1 << i)) != 0;
                _miniButtonImages[i].color = selected ? ColTeal : ColCream;
                _miniButtonLabels[i].color = ColTextDark;
            }
            _miniSubmitText.text = Loc.T("mini_check");
            if (isShadow) _miniStatusText.text = Loc.T("shadow_choose");
            else if (isValve)
            {
                int litres = 0;
                for (int i = 0; i < count; i++) if ((_miniSelectedMask & (1 << i)) != 0) litres += p.ints[i];
                _miniStatusText.text = Loc.F("valve_status", litres);
            }
            else
            {
                int lit = 0;
                for (int i = 0; i < 4; i++) if ((_miniSelectedMask & (1 << i)) != 0) lit++;
                _miniStatusText.text = Loc.F("lantern_status", lit);
            }
        }

        void OnMiniOptionTapped(int option)
        {
            var p = _gm.ActivePuzzle;
            if (p == null) return;
            if (p.kind == PuzzleKind.Shadow)
            {
                _gm.SubmitShadowAttempt(option);
                return;
            }
            if (p.kind != PuzzleKind.Valve && p.kind != PuzzleKind.Lantern) return;
            _miniSelectedMask ^= 1 << option;
            RefreshMiniGame();
        }

        void SubmitMiniMask()
        {
            _gm.SubmitMiniMask(_miniSelectedMask);
        }

        public void RevealMiniMaskHint()
        {
            var p = _gm.ActivePuzzle;
            if (p == null || (p.kind != PuzzleKind.Valve && p.kind != PuzzleKind.Lantern)) return;
            for (int i = 0; i < (p.kind == PuzzleKind.Valve && p.ints != null ? p.ints.Length : 4); i++)
            {
                bool current = (_miniSelectedMask & (1 << i)) != 0;
                bool target = (p.answerValue & (1 << i)) != 0;
                if (current != target)
                {
                    _miniSelectedMask ^= 1 << i;
                    break;
                }
            }
            RefreshMiniGame();
            ShowToast(Loc.T("mini_hint_applied"));
        }

        public void RevealShadowHint()
        {
            if (_gm.ActivePuzzle == null || _gm.ActivePuzzle.kind != PuzzleKind.Shadow) return;
            _shadowHintRevealed = true;
            RefreshMiniGame();
            ShowToast(Loc.T("shadow_hint"));
        }

        public void ShowPuzzleResult(int coinsEarned, bool correct, bool timedOut)
        {
            _puzzlePanel.SetActive(false);
            if (_routePuzzleOverlay != null) _routePuzzleOverlay.SetActive(false);
            _resultBox.SetActive(true);
            // Only show top-right PiP portrait on Correct Answer; on Wrong Answer keep Galata Tower & ◀◀◀ rewind arrows clear!
            if (_pipReactionCard != null) _pipReactionCard.SetActive(correct);

            int stop = Mathf.Clamp(SaveSystem.Data.currentStop, 1, GameConfig.TotalStops);
            _resultSubStatusText.color = Color.white;

            if (correct)
            {
                string title = Loc.T("correct");
                _resultTitleText.alignment = TextAnchor.MiddleCenter;
                SetRect(_resultTitleText.rectTransform, new Vector2(0.5f, 0.835f), new Vector2(0.5f, 0.835f), Vector2.zero, new Vector2(880f, 88f));
                _resultTitleText.color = ColCream;
                _resultTitleText.text = title;

                if (_resultTitleShadow != null)
                {
                    _resultTitleShadow.alignment = TextAnchor.MiddleCenter;
                    SetRect(_resultTitleShadow.rectTransform, new Vector2(0.5f, 0.835f), new Vector2(0.5f, 0.835f), new Vector2(0f, -7f), new Vector2(880f, 88f));
                    _resultTitleShadow.color = new Color(0.12f, 0.62f, 0.52f, 1f);
                    _resultTitleShadow.text = title;
                }

                if (_wrongCrossBadgeObj != null) _wrongCrossBadgeObj.SetActive(false);
                if (_wrongSubLabel != null) _wrongSubLabel.gameObject.SetActive(false);
                if (_confettiGroup != null) _confettiGroup.SetActive(true);

                if (_stopPillRect != null)
                    SetRect(_stopPillRect, new Vector2(0.5f, 0.625f), new Vector2(0.5f, 0.625f), Vector2.zero, new Vector2(680f, 84f));
                _resultSubStatusText.text = Loc.F("stop_pill_fmt", stop, GameConfig.TotalStops);

                if (_rewardPillObj != null) _rewardPillObj.SetActive(true);
                _resultExplainText.color = Color.white;
                _resultExplainText.text = Loc.F("reward_jeton_fmt", coinsEarned);

                if (_resultContinueBtnImg != null) _resultContinueBtnImg.color = ColYellow;
                if (_retryIconObj != null) _retryIconObj.SetActive(false);
                _resultContinueText.text = "►   " + Loc.T("continue_btn");

                string why = _gm.ActivePuzzle != null ? _gm.ActivePuzzle.Explain : null;
                bool showWhy = !string.IsNullOrWhiteSpace(why);
                if (_explainCard != null) _explainCard.SetActive(showWhy);
                if (showWhy)
                {
                    _explainTitleText.text = Loc.T("why_label");
                    _explainBodyText.text = why;
                }
                PlayRewardTweens(coinsEarned);
            }
            else
            {
                // The answer is not revealed on a wrong answer: the stop is retried with re-ordered options.
                if (_explainCard != null) _explainCard.SetActive(false);
                // Exact Wrong Answer Card layout (matching media_1792765107419_582e5035.jpg):
                // Glowing Red Circular ⊗ Badge on the left + "YANLIŞ CEVAP" & turquoise "YOLCU İNDİ" on the right,
                // "128 / 512 DURAĞA DÖN" stop pill in the middle, and warm cream "↺ TEKRAR DENE" button at the bottom!
                string title = timedOut ? Loc.T("time_up") : Loc.T("wrong");
                _resultTitleText.alignment = TextAnchor.MiddleLeft;
                SetRect(_resultTitleText.rectTransform, new Vector2(0.61f, 0.835f), new Vector2(0.61f, 0.835f), Vector2.zero, new Vector2(560f, 84f));
                _resultTitleText.color = ColCream;
                _resultTitleText.text = title;

                if (_resultTitleShadow != null)
                {
                    _resultTitleShadow.alignment = TextAnchor.MiddleLeft;
                    SetRect(_resultTitleShadow.rectTransform, new Vector2(0.61f, 0.835f), new Vector2(0.61f, 0.835f), new Vector2(0f, -6f), new Vector2(560f, 84f));
                    _resultTitleShadow.color = new Color(0.05f, 0.10f, 0.16f, 1f);
                    _resultTitleShadow.text = title;
                }

                if (_wrongCrossBadgeObj != null) _wrongCrossBadgeObj.SetActive(true);
                if (_wrongSubLabel != null)
                {
                    _wrongSubLabel.gameObject.SetActive(true);
                    _wrongSubLabel.text = Loc.T("passenger_disembarked_sub");
                }
                if (_confettiGroup != null) _confettiGroup.SetActive(false);

                if (_stopPillRect != null)
                    SetRect(_stopPillRect, new Vector2(0.5f, 0.49f), new Vector2(0.5f, 0.49f), Vector2.zero, new Vector2(680f, 88f));
                _resultSubStatusText.text = Loc.F("return_to_stop_fmt", stop, GameConfig.TotalStops);

                if (_rewardPillObj != null) _rewardPillObj.SetActive(false);

                if (_resultContinueBtnImg != null) _resultContinueBtnImg.color = ColCream;
                if (_retryIconObj != null) _retryIconObj.SetActive(true);
                _resultContinueText.text = "      " + Loc.T("try_again_btn");
            }

            // Populate Top-Right Picture-in-Picture (PiP) Real-Time 3D Passenger Camera Window
            if (correct && _gm.ActivePassengerProfile != null)
            {
                if (_gm.PassengerLiveTexture != null && _pipLiveRawImage != null)
                {
                    _pipLiveRawImage.texture = _gm.PassengerLiveTexture;
                    _pipLiveRawImage.gameObject.SetActive(true);
                    if (_pipPortraitImage != null) _pipPortraitImage.gameObject.SetActive(false);
                    _gm.SetPassengerLiveCamActive(true);
                }
                else
                {
                    var sprites = GetOrRenderExpressionPortraits(_gm.ActivePassengerProfile);
                    int exprIdx = (int)PassengerExpression.Satisfied;
                    if (_pipPortraitImage != null && sprites != null && exprIdx < sprites.Length)
                    {
                        _pipPortraitImage.gameObject.SetActive(true);
                        _pipPortraitImage.sprite = sprites[exprIdx];
                    }
                    if (_pipLiveRawImage != null) _pipLiveRawImage.gameObject.SetActive(false);
                }
            }
            else
            {
                if (_pipLiveRawImage != null) _pipLiveRawImage.gameObject.SetActive(false);
                _gm?.SetPassengerLiveCamActive(false);
            }
            if (_pipExcitementRays != null)
                _pipExcitementRays.SetActive(correct);

            RefreshExpressionCloseUps();
        }
    }
}

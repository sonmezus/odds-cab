using System;

namespace OddsCab.Puzzles
{
    public enum PuzzleCategory { Soru, Bilmece, Hesap, Kelime, Cikarim, Gozlem, Minyatur }

    public enum PuzzleKind
    {
        Choice = 0,     // 4-option card in the cab
        Bells = 1,      // miniature: four bells, tap the next bell in the hidden rule
        Bridge = 2,     // miniature: pick counterweights that exactly balance the bridge
        Route = 3,      // miniature: 3D Istanbul coastal route puzzle (ROTA BULMACASI / TAKSİYİ HEDEFE GÖTÜR)
        Ordering = 4,   // tap the four options in the right order; ints = correct order as option indices
        Letters = 5,    // build the answer word from letter tiles (answerTR/EN, lettersTR/EN)
        Grid = 6,       // Shikaku: cover the grid with non-overlapping rectangles matching clue areas
        Compare = 7,    // identify the single changed tile between two snapshots
        Timing = 8,     // tap the moving marker inside successive timing gates
        Valve = 9,      // set water valves to reach the requested probability
        Lantern = 10,   // light a valid lantern path across a bridge
        Shadow = 11,    // choose the lamp position that casts the requested shadow
        TorchBridge = 12, // cross a bridge with up to two travelers and one shared torch
        Akari = 13,    // place lamps so every open cell is lit without lamps seeing each other
        DoorCycle = 14, // choose the first positive shared opening time for periodic doors
        ValveCause = 15, // divert three deterministic water steps to the wheel to open the bridge
    }

    /// <summary>A fully resolved puzzle for one stop. TR and EN text are both present.</summary>
    [Serializable]
    public class PuzzleData
    {
        public string id;
        public int stop;
        public PuzzleCategory category;
        public PuzzleKind kind;
        public string mechanicId;
        public string[] sourceIds;
        public int difficulty;          // 0 easy, 1 medium, 2 hard
        public int seconds = 30;

        public string promptTR;
        public string promptEN;
        public string[] optionsTR;
        public string[] optionsEN;
        public int correct;
        public string explainTR;
        public string explainEN;

        /// <summary>Miniature parameters. Bells: sequence + answer. Bridge: target, weights, solution mask.</summary>
        public int[] ints;
        public int answerValue;

        /// <summary>Letters puzzles: the word to build and the tiles offered (may include decoy letters).</summary>
        public string answerTR;
        public string answerEN;
        public string lettersTR;
        public string lettersEN;

        /// <summary>Shikaku grid: dimensions, clue area per cell (0 = empty), and solution region per cell.</summary>
        public int gridWidth;
        public int gridHeight;
        public int[] gridClues;
        public int[] gridSolutionOwners;
        public int[] compareBefore;
        public int[] compareAfter;
        /// <summary>Timing puzzle target positions in permille (0..1000), one gate per tap.</summary>
        public int[] timingTargets;
        public int timingTolerance;
        /// <summary>Akari board: bitmasks for all accepted lamp placements.</summary>
        public int[] akariSolutions;
        /// <summary>Periodic door puzzle: opening intervals for doors A and B.</summary>
        public int[] doorPeriods;
        public string Answer => Loc.Pick(answerTR, answerEN);
        public string Letters => Loc.IsTR || string.IsNullOrEmpty(lettersEN) ? lettersTR : lettersEN;

        public string Prompt => Loc.Pick(promptTR, promptEN);
        public string Explain => Loc.Pick(explainTR, explainEN);
        public string[] Options => Loc.IsTR || optionsEN == null || optionsEN.Length == 0 ? optionsTR : optionsEN;
    }
}

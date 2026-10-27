using System;
using System.Collections.Generic;

namespace OddsCab.Puzzles
{
    /// <summary>Generated Shikaku board with a unique rectangle tiling for its numbered clues.</summary>
    public sealed class ShikakuLayout
    {
        public int Width { get; }
        public int Height { get; }
        public int[] Clues { get; }
        public int[] Owners { get; }

        ShikakuLayout(int width, int height, int[] clues, int[] owners)
        {
            Width = width;
            Height = height;
            Clues = clues;
            Owners = owners;
        }

        public static ShikakuLayout Generate(int seed, int difficulty)
        {
            int w = 4 + (difficulty >= 1 ? 1 : 0);
            int h = w;
            var rng = new Random(seed);
            for (int attempt = 0; attempt < 80; attempt++)
            {
                var regions = new List<Rect>();
                var occupied = new bool[w * h];
                if (!Partition(w, h, occupied, regions, rng, 0)) continue;
                var clues = new int[w * h];
                for (int i = 0; i < regions.Count; i++)
                {
                    var r = regions[i];
                    int x = r.X + rng.Next(r.W);
                    int y = r.Y + rng.Next(r.H);
                    clues[y * w + x] = r.W * r.H;
                }

                var solution = new int[w * h];
                if (CountSolutions(w, h, clues, solution, 2) == 1)
                    return new ShikakuLayout(w, h, clues, solution);
            }

            // A guaranteed unique fallback keeps startup deterministic even if random boards are ambiguous.
            var fallbackClues = new int[w * h];
            var fallbackOwners = new int[w * h];
            for (int y = 0; y < h; y++)
            {
                fallbackClues[y * w] = w;
                for (int x = 0; x < w; x++) fallbackOwners[y * w + x] = y;
            }
            return new ShikakuLayout(w, h, fallbackClues, fallbackOwners);
        }

        struct Rect
        {
            public int X, Y, W, H;
            public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        }

        static bool Partition(int w, int h, bool[] occupied, List<Rect> regions, Random rng, int used)
        {
            int first = Array.IndexOf(occupied, false);
            if (first < 0) return true;
            int x = first % w, y = first / w;
            var choices = new List<Rect>();
            for (int rh = 1; rh <= h - y; rh++)
                for (int rw = 1; rw <= w - x; rw++)
                {
                    if (rw * rh > 7 || (rw == 1 && rh == 1 && used < w * h - 2)) continue;
                    var rect = new Rect(x, y, rw, rh);
                    if (Fits(rect, w, occupied)) choices.Add(rect);
                }
            Shuffle(choices, rng);
            foreach (var rect in choices)
            {
                Fill(rect, w, occupied, true);
                regions.Add(rect);
                if (Partition(w, h, occupied, regions, rng, used + rect.W * rect.H)) return true;
                regions.RemoveAt(regions.Count - 1);
                Fill(rect, w, occupied, false);
            }
            return false;
        }

        static bool Fits(Rect r, int w, bool[] occupied)
        {
            for (int y = r.Y; y < r.Y + r.H; y++)
                for (int x = r.X; x < r.X + r.W; x++)
                    if (occupied[y * w + x]) return false;
            return true;
        }

        static void Fill(Rect r, int w, bool[] occupied, bool value)
        {
            for (int y = r.Y; y < r.Y + r.H; y++)
                for (int x = r.X; x < r.X + r.W; x++) occupied[y * w + x] = value;
        }

        static void Shuffle<T>(IList<T> items, Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        static int CountSolutions(int w, int h, int[] clues, int[] owners, int limit)
        {
            var clueCells = new List<int>();
            for (int i = 0; i < clues.Length; i++) if (clues[i] > 0) clueCells.Add(i);
            if (clueCells.Count == 0) return 0;
            var candidates = new List<int>[clueCells.Count];
            for (int i = 0; i < clueCells.Count; i++)
            {
                candidates[i] = CandidateMasks(w, h, clues, clueCells[i], clues[clueCells[i]]);
                if (candidates[i].Count == 0) return 0;
            }
            int[] chosen = new int[clueCells.Count];
            return Search(w * h, candidates, chosen, 0, owners, limit);
        }

        static List<int> CandidateMasks(int w, int h, int[] clues, int clueCell, int area)
        {
            var result = new List<int>();
            int cx = clueCell % w, cy = clueCell / w;
            for (int rh = 1; rh <= h; rh++)
                for (int rw = 1; rw <= w; rw++)
                {
                    if (rw * rh != area) continue;
                    for (int y = Math.Max(0, cy - rh + 1); y <= cy && y + rh <= h; y++)
                        for (int x = Math.Max(0, cx - rw + 1); x <= cx && x + rw <= w; x++)
                        {
                            int mask = 0;
                            bool containsOtherClue = false;
                            for (int yy = y; yy < y + rh; yy++)
                                for (int xx = x; xx < x + rw; xx++)
                                {
                                    int cell = yy * w + xx;
                                    mask |= 1 << cell;
                                    if (cell != clueCell && clues[cell] > 0) containsOtherClue = true;
                                }
                            if (!containsOtherClue && !result.Contains(mask)) result.Add(mask);
                        }
                }
            return result;
        }

        static int Search(int cellCount, List<int>[] candidates, int[] chosen, int usedClues, int[] owners, int limit)
        {
            if (usedClues == candidates.Length)
            {
                int fullMask = (1 << cellCount) - 1;
                int covered = 0;
                for (int i = 0; i < chosen.Length; i++) covered |= chosen[i];
                if (covered != fullMask) return 0;
                for (int i = 0; i < owners.Length; i++) owners[i] = -1;
                for (int region = 0; region < chosen.Length; region++)
                    for (int cell = 0; cell < cellCount; cell++)
                        if ((chosen[region] & (1 << cell)) != 0) owners[cell] = region;
                return 1;
            }

            int best = -1, bestCount = int.MaxValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (chosen[i] != 0) continue;
                int available = 0;
                foreach (int mask in candidates[i])
                    if (Compatible(mask, chosen, candidates.Length)) available++;
                if (available < bestCount) { best = i; bestCount = available; }
                if (bestCount == 0) return 0;
            }
            if (best < 0) return 0;

            int solutions = 0;
            foreach (int mask in candidates[best])
            {
                if (!Compatible(mask, chosen, candidates.Length)) continue;
                chosen[best] = mask;
                solutions += Search(cellCount, candidates, chosen, usedClues + 1, owners, limit - solutions);
                chosen[best] = 0;
                if (solutions >= limit) return solutions;
            }
            return solutions;
        }

        static bool Compatible(int mask, int[] chosen, int clueCount)
        {
            for (int i = 0; i < clueCount; i++)
                if (chosen[i] != 0 && (chosen[i] & mask) != 0) return false;
            return true;
        }
    }
}

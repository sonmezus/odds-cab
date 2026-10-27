using System;
using System.Collections.Generic;

namespace OddsCab.Puzzles
{
    public enum RouteTileKind { Empty = 0, Straight = 1, Corner = 2 }

    /// <summary>
    /// Pure, seeded layout for the rotating-road route puzzle (design pool F54 "Döner kavşak").
    /// The taxi enters the bottom row from the south at <see cref="StartX"/>; the target sits north of the
    /// top row at <see cref="GoalX"/>. The player rotates tiles a quarter turn per tap until the roads join.
    /// Directions: 0 = North (+y), 1 = East (+x), 2 = South, 3 = West. Row 0 is the bottom row.
    /// </summary>
    public class RouteTileLayout
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int StartX { get; private set; }
        public int GoalX { get; private set; }
        public RouteTileKind[] Kind { get; private set; }
        public int[] Rotation { get; private set; }        // current quarter turns (clockwise seen from above)
        public int[] SolvedRotation { get; private set; }  // one valid solution
        public List<int> SolutionPath { get; private set; } // cell indices from start to goal

        static readonly int[] Dx = { 0, 1, 0, -1 };
        static readonly int[] Dy = { 1, 0, -1, 0 };

        public int Index(int x, int y) => y * Width + x;

        /// <summary>Connection mask (bit d = open towards direction d) for a tile kind and rotation.</summary>
        public static int Connections(RouteTileKind kind, int rot)
        {
            int baseMask;
            switch (kind)
            {
                case RouteTileKind.Straight: baseMask = (1 << 0) | (1 << 2); break; // N-S
                case RouteTileKind.Corner: baseMask = (1 << 0) | (1 << 1); break;   // N-E
                default: return 0;
            }
            int r = ((rot % 4) + 4) % 4;
            int mask = 0;
            for (int d = 0; d < 4; d++)
                if ((baseMask & (1 << d)) != 0) mask |= 1 << ((d + r) % 4);
            return mask;
        }

        public void RotateTile(int index)
        {
            if (Kind[index] == RouteTileKind.Empty) return;
            Rotation[index] = (Rotation[index] + 1) % 4;
        }

        /// <summary>Follows the road from the start; returns the visited cells if it reaches the goal, else null.</summary>
        public List<int> TraceRoute()
        {
            var path = new List<int>();
            int x = StartX, y = 0, cameFrom = 2; // entering the bottom row from the south
            for (int guard = 0; guard <= Width * Height; guard++)
            {
                int idx = Index(x, y);
                int mask = Connections(Kind[idx], Rotation[idx]);
                if ((mask & (1 << cameFrom)) == 0) return null;
                int exit = -1;
                for (int d = 0; d < 4; d++)
                    if (d != cameFrom && (mask & (1 << d)) != 0) { exit = d; break; }
                if (exit < 0) return null;
                path.Add(idx);
                if (y == Height - 1 && x == GoalX && exit == 0) return path;
                int nx = x + Dx[exit], ny = y + Dy[exit];
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) return null;
                if (path.Contains(Index(nx, ny))) return null;
                x = nx; y = ny; cameFrom = (exit + 2) % 4;
            }
            return null;
        }

        public bool IsSolved() => TraceRoute() != null;

        public static void SizeForDifficulty(int diff, out int w, out int h)
        {
            if (diff <= 0) { w = 3; h = 3; }
            else if (diff == 1) { w = 4; h = 4; }
            else { w = 4; h = 5; }
        }

        public static RouteTileLayout Generate(int seed, int diff)
        {
            SizeForDifficulty(diff, out int w, out int h);
            var rng = new Random(seed);
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var layout = TryGenerate(rng, w, h, diff);
                if (layout != null) return layout;
            }
            // Fallback: a straight column (never expected in practice).
            var fb = new RouteTileLayout { Width = w, Height = h, StartX = 0, GoalX = 0 };
            fb.Kind = new RouteTileKind[w * h];
            fb.Rotation = new int[w * h];
            fb.SolvedRotation = new int[w * h];
            fb.SolutionPath = new List<int>();
            for (int y = 0; y < h; y++) { int i = fb.Index(0, y); fb.Kind[i] = RouteTileKind.Straight; fb.Rotation[i] = 1; fb.SolutionPath.Add(i); }
            return fb;
        }

        static RouteTileLayout TryGenerate(Random rng, int w, int h, int diff)
        {
            var L = new RouteTileLayout { Width = w, Height = h };
            L.StartX = rng.Next(w);
            int minLen = h + (diff <= 0 ? 1 : diff == 1 ? 3 : 5);

            // Random self-avoiding walk from the start cell to any top-row cell (then exits north).
            var visited = new bool[w * h];
            var path = new List<int>();
            if (!Walk(L, rng, L.StartX, 0, visited, path, minLen)) return null;

            L.Kind = new RouteTileKind[w * h];
            L.SolvedRotation = new int[w * h];
            for (int k = 0; k < path.Count; k++)
            {
                int idx = path[k];
                int x = idx % w, y = idx / w;
                int inDir = k == 0 ? 2 : DirTo(path[k - 1], idx, w);   // side we enter from
                int outDir = k == path.Count - 1 ? 0 : DirFrom(idx, path[k + 1], w);
                SetTileFor(L, idx, inDir, outDir);
            }
            L.GoalX = path[path.Count - 1] % w;
            L.SolutionPath = path;

            // Decoy tiles off the path: harder levels have fewer empty squares to rule out.
            double emptyChance = diff <= 0 ? 0.35 : diff == 1 ? 0.2 : 0.1;
            for (int i = 0; i < w * h; i++)
            {
                if (path.Contains(i)) continue;
                if (rng.NextDouble() < emptyChance) { L.Kind[i] = RouteTileKind.Empty; continue; }
                L.Kind[i] = rng.Next(2) == 0 ? RouteTileKind.Straight : RouteTileKind.Corner;
                L.SolvedRotation[i] = rng.Next(4);
            }

            // Scramble: every road tile starts at a random rotation; the puzzle must not start solved.
            L.Rotation = new int[w * h];
            for (int i = 0; i < w * h; i++)
                L.Rotation[i] = L.Kind[i] == RouteTileKind.Empty ? 0 : rng.Next(4);
            int wrongOnPath = 0;
            foreach (var i in path)
                if (Connections(L.Kind[i], L.Rotation[i]) != Connections(L.Kind[i], L.SolvedRotation[i])) wrongOnPath++;
            int needWrong = diff <= 0 ? 2 : diff == 1 ? 4 : 5;
            for (int k = 0; wrongOnPath < Math.Min(needWrong, path.Count) && k < path.Count; k++)
            {
                int i = path[k];
                if (Connections(L.Kind[i], L.Rotation[i]) == Connections(L.Kind[i], L.SolvedRotation[i]))
                {
                    L.Rotation[i] = (L.SolvedRotation[i] + 1) % 4;
                    wrongOnPath++;
                }
            }
            if (L.IsSolved()) return null;

            // The stored solution must actually solve the layout.
            var check = (int[])L.Rotation.Clone();
            L.Rotation = (int[])L.SolvedRotation.Clone();
            bool ok = L.IsSolved();
            L.Rotation = check;
            return ok ? L : null;
        }

        static bool Walk(RouteTileLayout L, Random rng, int x, int y, bool[] visited, List<int> path, int minLen)
        {
            int w = L.Width, h = L.Height;
            int idx = y * w + x;
            visited[idx] = true;
            path.Add(idx);
            if (y == h - 1 && path.Count >= minLen) return true;

            int[] dirs = { 0, 1, 3, 2 };
            for (int i = dirs.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (dirs[i], dirs[j]) = (dirs[j], dirs[i]); }
            foreach (var d in dirs)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int n = ny * w + nx;
                if (visited[n]) continue;
                if (Walk(L, rng, nx, ny, visited, path, minLen)) return true;
            }
            visited[idx] = false;
            path.RemoveAt(path.Count - 1);
            return false;
        }

        static int DirFrom(int from, int to, int w)
        {
            int dx = to % w - from % w, dy = to / w - from / w;
            if (dy == 1) return 0;
            if (dx == 1) return 1;
            if (dy == -1) return 2;
            return 3;
        }

        static int DirTo(int from, int to, int w) => (DirFrom(from, to, w) + 2) % 4; // side of 'to' facing 'from'

        static void SetTileFor(RouteTileLayout L, int idx, int a, int b)
        {
            if ((a + 2) % 4 == b)
            {
                L.Kind[idx] = RouteTileKind.Straight;
                L.SolvedRotation[idx] = (a == 0 || a == 2) ? 0 : 1;
                return;
            }
            L.Kind[idx] = RouteTileKind.Corner;
            int mask = (1 << a) | (1 << b);
            for (int r = 0; r < 4; r++)
                if (Connections(RouteTileKind.Corner, r) == mask) { L.SolvedRotation[idx] = r; return; }
        }
    }
}

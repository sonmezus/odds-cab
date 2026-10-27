using System;
using System.Collections.Generic;
using System.Linq;

namespace OddsCab.Puzzles
{
    /// <summary>
    /// Seeded miniature generators that replace one-off authored items, so every miniature mechanic recurs across
    /// the campaign with a difficulty that grows. Each one checks its own answer is unique before returning.
    /// </summary>
    public static partial class PuzzleGenerators
    {
        // ---------------------------------------------------------------- water valves (subset sum, unique)

        static readonly int[][] ValveSets =
        {
            new[] { 1, 2, 4 }, new[] { 1, 3, 5 }, new[] { 2, 3, 7 }, new[] { 1, 4, 6 },
            new[] { 1, 2, 5, 9 }, new[] { 2, 3, 6, 11 }, new[] { 1, 3, 7, 12 }, new[] { 2, 5, 8, 13 },
        };

        /// <summary>Open exactly the valves whose sizes add up to the wheel's need. Medium/hard use four valves.</summary>
        public static PuzzleData WaterValveSum(Random r, int diff)
        {
            var pool = ValveSets.Where(s => diff == 0 ? s.Length == 3 : s.Length == 4).ToArray();
            for (int guard = 0; guard < 60; guard++)
            {
                var sizes = pool[r.Next(pool.Length)];
                int n = sizes.Length;
                int mask = 1 + r.Next((1 << n) - 1);
                if (diff >= 1 && CountBitsG(mask) < 2) continue;   // single-valve answers are too easy past band 0
                int target = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).Sum(i => sizes[i]);
                int ways = 0;
                for (int m = 1; m < (1 << n); m++)
                    if (Enumerable.Range(0, n).Where(i => (m & (1 << i)) != 0).Sum(i => sizes[i]) == target) ways++;
                if (ways != 1) continue;
                string list = string.Join(", ", sizes);
                var parts = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).Select(i => sizes[i].ToString());
                return new PuzzleData
                {
                    id = $"GEN-VALVE2-{list.Replace(", ", "")}-{mask}", category = PuzzleCategory.Minyatur, kind = PuzzleKind.Valve,
                    difficulty = diff, seconds = 40 + diff * 10, answerValue = mask, ints = sizes,
                    promptTR = $"Su çarkı tam {target} litre isterse döner; fazlası taşar. Vanalar {list} litre akıtır. Hangi vanaları açmalısın?",
                    promptEN = $"The water wheel turns only with exactly {target} litres; any more overflows. The valves give {list} litres. Which valves do you open?",
                    explainTR = $"{string.Join(" + ", parts)} = {target}. Bu toplamı veren başka vana seçimi yok.",
                    explainEN = $"{string.Join(" + ", parts)} = {target}. No other set of valves adds up to it.",
                };
            }
            return WaterValve(r, diff);
        }

        static int CountBitsG(int v) { int c = 0; while (v != 0) { v &= v - 1; c++; } return c; }

        // ---------------------------------------------------------------- lantern logic (unique lit set)

        // One rule about lanterns A..D: a test on a 4-bit mask plus its TR/EN text.
        struct LanternRule { public Func<int, bool> ok; public string tr, en; }

        static IEnumerable<LanternRule> LanternRules()
        {
            string[] L = { "A", "B", "C", "D" };
            bool On(int m, int i) => (m & (1 << i)) != 0;
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                if (i == j) continue;
                int a = i, b = j;
                yield return new LanternRule { ok = m => !On(m, a) || On(m, b), tr = $"{L[a]} yanarsa {L[b]} de yanar", en = $"if {L[a]} is lit, {L[b]} is lit too" };
                if (a < b)
                {
                    yield return new LanternRule { ok = m => !(On(m, a) && On(m, b)), tr = $"{L[a]} ile {L[b]} birlikte yanmaz", en = $"{L[a]} and {L[b]} are never lit together" };
                    yield return new LanternRule { ok = m => On(m, a) || On(m, b), tr = $"{L[a]} ya da {L[b]} yanık olmalı", en = $"{L[a]} or {L[b]} must be lit" };
                }
            }
            for (int i = 0; i < 4; i++)
            {
                int a = i;
                yield return new LanternRule { ok = m => !On(m, a), tr = $"{L[a]} sönük kalır", en = $"{L[a]} stays dark" };
            }
        }

        /// <summary>
        /// Light the lantern set that obeys every rule. Rules are drawn until exactly one set of lanterns fits, so the
        /// puzzle has a single answer; harder stops need more rules to reach it.
        /// </summary>
        public static PuzzleData LanternLogic(Random r, int diff)
        {
            var all = LanternRules().ToList();
            int wantCount = diff == 0 ? 2 : (diff == 1 ? 2 + r.Next(2) : 3);
            for (int guard = 0; guard < 200; guard++)
            {
                var chosen = new List<LanternRule>();
                var countRule = new LanternRule { ok = m => CountBitsG(m) == wantCount, tr = $"Tam {wantCount} fener yanmalı", en = $"Exactly {wantCount} lanterns are lit" };
                chosen.Add(countRule);
                var candidates = Enumerable.Range(0, 16).Where(countRule.ok).ToList();
                foreach (var rule in all.OrderBy(_ => r.Next()))
                {
                    var next = candidates.Where(rule.ok).ToList();
                    if (next.Count == 0 || next.Count == candidates.Count) continue;   // must cut, never empty
                    chosen.Add(rule);
                    candidates = next;
                    if (candidates.Count == 1) break;
                }
                if (candidates.Count != 1) continue;
                int minRules = diff == 0 ? 2 : (diff == 1 ? 3 : 4);
                if (chosen.Count < minRules) continue;
                int answer = candidates[0];
                string lit = string.Join(", ", Enumerable.Range(0, 4).Where(i => (answer & (1 << i)) != 0).Select(i => "ABCD"[i].ToString()));
                return new PuzzleData
                {
                    id = $"GEN-LANTERN2-{answer}-{string.Join("|", chosen.Select(c => c.tr)).GetHashCode():X}",
                    category = PuzzleCategory.Minyatur, kind = PuzzleKind.Lantern,
                    difficulty = diff, seconds = 45 + diff * 15, answerValue = answer,
                    optionsTR = new[] { "A", "B", "C", "D" }, optionsEN = new[] { "A", "B", "C", "D" },
                    promptTR = "Köprü fenerleri: " + string.Join(". ", chosen.Select(c => c.tr)) + ". Hangi fenerler yanmalı?",
                    promptEN = "Bridge lanterns: " + string.Join(". ", chosen.Select(c => c.en)) + ". Which lanterns are lit?",
                    explainTR = $"Bütün kurallara uyan tek düzen: {lit}.",
                    explainEN = $"The only pattern that obeys every rule: {lit}.",
                };
            }
            return LanternBridge(r, diff);
        }

        // ---------------------------------------------------------------- periodic doors (first shared opening)

        /// <summary>Two gates open every a and b steps; pick the first moment both are open (the LCM, at most 12).</summary>
        public static PuzzleData DoorCycleGen(Random r, int diff)
        {
            var pairs = new List<(int a, int b)>();
            for (int a = 2; a <= 6; a++)
            for (int b = a + 1; b <= 12; b++)
            {
                int l = Lcm(a, b);
                if (l > 12) continue;
                bool divides = b % a == 0;
                if (diff == 0 && !divides) continue;            // easy: one period divides the other
                if (diff >= 1 && divides) continue;             // medium/hard: a real common multiple
                if (diff == 2 && l < 10) continue;
                pairs.Add((a, b));
            }
            var (pa, pb) = pairs[r.Next(pairs.Count)];
            int ans = Lcm(pa, pb);
            return new PuzzleData
            {
                id = $"GEN-DOORCYC-{pa}-{pb}", category = PuzzleCategory.Minyatur, kind = PuzzleKind.DoorCycle,
                difficulty = diff, seconds = 40 + diff * 10, doorPeriods = new[] { pa, pb }, answerValue = ans,
                answerTR = ans.ToString(), answerEN = ans.ToString(),
                promptTR = $"İskeledeki A kapısı her {pa}, B kapısı her {pb} saniyede bir açılıyor. İkisi şimdi (t=0) açık. İkisinin birlikte açık olduğu bir sonraki an ne zaman?",
                promptEN = $"Pier gate A opens every {pa} seconds and gate B every {pb}. Both are open now (t=0). When are they next open together?",
                explainTR = $"{pa} ve {pb} sayılarının ortak katlarının en küçüğü {ans}.",
                explainEN = $"The least common multiple of {pa} and {pb} is {ans}.",
            };
        }

        static int Lcm(int a, int b) => a / Gcd(a, b) * b;

        // ---------------------------------------------------------------- torch bridge (river crossing, optimal time)

        static readonly int[][] TorchSets =
        {
            new[] { 1, 2, 4, 6 }, new[] { 1, 3, 4, 5 }, new[] { 1, 2, 3, 8 }, new[] { 1, 2, 4, 5 }, new[] { 2, 3, 4, 6 },   // easy
            new[] { 1, 2, 5, 8 }, new[] { 1, 2, 6, 9 }, new[] { 2, 3, 5, 8 },          // medium
            new[] { 1, 2, 5, 10 }, new[] { 1, 3, 6, 12 }, new[] { 1, 2, 7, 10 }, new[] { 2, 3, 7, 11 },   // hard: the classic trick
        };

        /// <summary>Four travellers, one torch, two per crossing: get everyone across within the optimal time.</summary>
        public static PuzzleData TorchBridgeGen(Random r, int diff)
        {
            int lo = diff == 0 ? 0 : diff == 1 ? 5 : 8;
            int hi = diff == 0 ? 5 : diff == 1 ? 8 : TorchSets.Length;
            var t = TorchSets[lo + r.Next(hi - lo)];
            int best = TorchOptimal(t);
            string times = $"{t[0]}, {t[1]}, {t[2]} ve {t[3]}";
            return new PuzzleData
            {
                id = $"GEN-TORCH-{string.Join("-", t)}", category = PuzzleCategory.Minyatur, kind = PuzzleKind.TorchBridge,
                difficulty = diff, seconds = 70 + diff * 15, ints = t, answerValue = best,
                answerTR = best + " dk", answerEN = best + " min",
                promptTR = $"Dört yolcu köprüyü {times} dakikada geçiyor. Köprüde en fazla iki kişi olur ve fener şart; ikili, yavaş olanın hızında yürür. Herkesi en fazla {best} dakikada karşıya geçir.",
                promptEN = $"Four travellers cross in {t[0]}, {t[1]}, {t[2]} and {t[3]} minutes. At most two on the bridge, the torch must go along, and a pair walks at the slower pace. Get everyone across within {best} minutes.",
                explainTR = $"En kısa süre {best} dakika. İki yavaş yolcuyu birlikte geçirmek çoğu zaman kazandırır.",
                explainEN = $"The best possible time is {best} minutes. Sending the two slow travellers together usually pays off.",
            };
        }

        /// <summary>Shortest total time to move all four across (Dijkstra over bank states).</summary>
        static int TorchOptimal(int[] t)
        {
            const int all = 15;
            var dist = new Dictionary<int, int> { [0] = 0 };   // state = right-bank mask | torchRight << 4
            var open = new SortedSet<(int d, int s)> { (0, 0) };
            while (open.Count > 0)
            {
                var (d, s) = open.Min; open.Remove(open.Min);
                if (d > dist[s]) continue;
                int right = s & all; bool torchRight = (s & 16) != 0;
                if (right == all && torchRight) return d;
                int bank = torchRight ? right : (~right & all);
                for (int a = 0; a < 4; a++)
                for (int b = a; b < 4; b++)
                {
                    int move = (1 << a) | (1 << b);
                    if ((bank & move) != move) continue;
                    int cost = Math.Max(t[a], t[b]);
                    int nr = torchRight ? right & ~move : right | move;
                    int ns = nr | (torchRight ? 0 : 16);
                    int nd = d + cost;
                    if (!dist.TryGetValue(ns, out int old) || nd < old) { dist[ns] = nd; open.Add((nd, ns)); }
                }
            }
            return t.Sum();
        }
    }
}

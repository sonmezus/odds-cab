using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OddsCab.Puzzles
{
    [Serializable]
    internal class HandwrittenItem
    {
        public string id;
        public string category;
        public string mechanicId;
        public string puzzleKind;
        public int difficulty;
        public int durationSeconds;
        public string promptTR;
        public string promptEN;
        public string correctAnswerTR;
        public string correctAnswerEN;
        public string[] distractorsTR;
        public string[] distractorsEN;
        public string explanationTR;
        public string explanationEN;
        public string[] sourceIds;
        public string lettersTR;
        public string lettersEN;
        public int[] ints;
        public int answerValue;
        public int[] akariSolutions;
    }

    [Serializable]
    internal class HandwrittenFile
    {
        public int schemaVersion;
        public HandwrittenItem[] items;
    }

    /// <summary>
    /// Deterministically plans all 512 stops once (cheap, cached) and hands out one puzzle per stop.
    ///
    /// Category mix per 64-stop district follows the design pool (icerik-havuzu.json,
    /// proposed_campaign_distribution): Soru 8 · Bilmece 8 · Hesap 10 · Kelime 9 · Çıkarım 11 · Gözlem 8 · Minyatür 10
    /// = 64 × 8 districts = 64 / 64 / 80 / 72 / 88 / 64 / 80 over the campaign.
    /// Categories are interleaved so the same kind never comes twice in a row.
    ///
    /// Within each category and difficulty band, handwritten items appear once in stop order before any reuse.
    /// Remaining slots use the category's seeded generators (rotated). A band without generators re-uses its
    /// least-used items from that same difficulty band.
    /// </summary>
    public static class StopPlanner
    {
        static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }

        static bool IsValidAkariSolutionMask(int lamps)
        {
            if (lamps <= 0 || (lamps & ~0x1ff) != 0 || (lamps & (1 << 4)) != 0 || CountBits(lamps) != 2)
                return false;
            int lit = lamps;
            for (int cell = 0; cell < 9; cell++)
            {
                if ((lamps & (1 << cell)) == 0) continue;
                int x = cell % 3;
                int y = cell / 3;
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0;
                    int dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int nx = x + dx;
                    int ny = y + dy;
                    while (nx >= 0 && nx < 3 && ny >= 0 && ny < 3)
                    {
                        int next = ny * 3 + nx;
                        if (next == 4) break;
                        if ((lamps & (1 << next)) != 0) return false;
                        lit |= 1 << next;
                        nx += dx;
                        ny += dy;
                    }
                }
            }
            return (lit & 0x1ef) == 0x1ef;
        }

        static bool IsValidDoorCycle(int[] periods, int answer)
        {
            if (periods == null || periods.Length != 2 || periods.Any(x => x < 2 || x > 12) || answer <= 0)
                return false;
            int limit = periods[0] * periods[1];
            for (int t = 1; t <= limit; t++)
                if (t % periods[0] == 0 && t % periods[1] == 0) return answer == t;
            return false;
        }

        // Order matches PuzzleCategory; Minyatur last.
        static readonly PuzzleCategory[] PlanCategories =
        {
            PuzzleCategory.Soru, PuzzleCategory.Bilmece, PuzzleCategory.Hesap, PuzzleCategory.Kelime,
            PuzzleCategory.Cikarim, PuzzleCategory.Gozlem, PuzzleCategory.Minyatur
        };
        public static readonly int[] PerDistrictTargets = { 8, 8, 10, 9, 11, 8, 10 };

        enum SlotKind { Handwritten, Generator, Miniature, AuthoredMiniature, StopOne }

        struct Slot
        {
            public PuzzleCategory cat;
            public SlotKind kind;
            public int itemIndex;   // handwritten index into _handwritten
            public int genIndex;    // generator rotation index
            public int miniIndex;   // miniature rotation index
        }

        static List<HandwrittenItem> _handwritten;
        static Slot[] _plan;   // index = stop (1..512), [0] unused

        static void EnsureLoaded()
        {
            if (_handwritten != null) return;
            _handwritten = new List<HandwrittenItem>();
            var ta = Resources.Load<TextAsset>("Puzzles/handwritten");
            if (ta != null && !string.IsNullOrEmpty(ta.text))
            {
                var parsed = JsonUtility.FromJson<HandwrittenFile>(ta.text);
                if (parsed != null && parsed.schemaVersion != 1)
                    Debug.LogWarning("[OddsCab] Unsupported handwritten puzzle schema version: " + parsed.schemaVersion);
                if (parsed?.schemaVersion == 1 && parsed.items != null)
                {
                    var seenIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var it in parsed.items)
                    {
                        PuzzleKind itemKind = PuzzleKind.Choice;
                        bool validKind = it != null && (string.IsNullOrWhiteSpace(it.puzzleKind)
                            || Enum.TryParse(it.puzzleKind, true, out itemKind));
                        bool isAuthoredMiniature = itemKind == PuzzleKind.Bells || itemKind == PuzzleKind.TorchBridge
                            || itemKind == PuzzleKind.Akari || itemKind == PuzzleKind.DoorCycle || itemKind == PuzzleKind.ValveCause;
                        bool isCustomMiniWithoutOptions = itemKind == PuzzleKind.TorchBridge || itemKind == PuzzleKind.Akari
                            || itemKind == PuzzleKind.DoorCycle || itemKind == PuzzleKind.ValveCause;
                        bool validCategory = it != null && Enum.TryParse(it.category, true, out PuzzleCategory category)
                            && (category != PuzzleCategory.Minyatur || isAuthoredMiniature);
                        validKind &= itemKind == PuzzleKind.Choice || itemKind == PuzzleKind.Letters
                            || (isAuthoredMiniature && validCategory);
                        bool validOptions = itemKind == PuzzleKind.TorchBridge
                            ? it != null && it.ints != null && it.ints.Length == 4
                                && it.ints.All(x => x > 0) && it.answerValue > 0
                                && !string.IsNullOrWhiteSpace(it.correctAnswerTR) && !string.IsNullOrWhiteSpace(it.correctAnswerEN)
                            : itemKind == PuzzleKind.Akari
                            ? it != null && it.ints != null && it.ints.Length == 2
                                && it.ints.All(IsValidAkariSolutionMask)
                                && it.ints[0] != it.ints[1]
                                && !string.IsNullOrWhiteSpace(it.correctAnswerTR) && !string.IsNullOrWhiteSpace(it.correctAnswerEN)
                            : itemKind == PuzzleKind.DoorCycle
                            ? it != null && IsValidDoorCycle(it.ints, it.answerValue)
                                && !string.IsNullOrWhiteSpace(it.correctAnswerTR) && !string.IsNullOrWhiteSpace(it.correctAnswerEN)
                            : itemKind == PuzzleKind.ValveCause
                            ? it != null && it.ints != null && it.ints.Length == 1 && it.ints[0] == 3 && it.answerValue == 1
                                && !string.IsNullOrWhiteSpace(it.correctAnswerTR) && !string.IsNullOrWhiteSpace(it.correctAnswerEN)
                            : it != null && it.distractorsTR != null && it.distractorsTR.Length == 3
                                && it.distractorsEN != null && it.distractorsEN.Length == 3
                                && !string.IsNullOrWhiteSpace(it.correctAnswerTR) && !string.IsNullOrWhiteSpace(it.correctAnswerEN)
                                && (itemKind != PuzzleKind.Letters || (!string.IsNullOrWhiteSpace(it.lettersTR) && !string.IsNullOrWhiteSpace(it.lettersEN)))
                                && (itemKind != PuzzleKind.Bells || (it.ints != null && it.ints.Length > 0 && it.answerValue >= 1 && it.answerValue <= 4));
                        bool validText = it != null && !string.IsNullOrWhiteSpace(it.id)
                            && !string.IsNullOrWhiteSpace(it.promptTR) && !string.IsNullOrWhiteSpace(it.promptEN)
                            && !string.IsNullOrWhiteSpace(it.explanationTR) && !string.IsNullOrWhiteSpace(it.explanationEN)
                            && it.difficulty >= 0 && it.difficulty <= 2 && it.durationSeconds > 0;
                        if (!validCategory || !validKind || !validOptions || !validText || !seenIds.Add(it.id))
                        {
                            Debug.LogWarning("[OddsCab] Skipping malformed or duplicate handwritten item " + it?.id);
                            continue;
                        }
                        if (!isCustomMiniWithoutOptions &&
                            (it.distractorsTR.Any(string.IsNullOrWhiteSpace) || it.distractorsEN.Any(string.IsNullOrWhiteSpace)
                            || (itemKind == PuzzleKind.Choice && it.distractorsTR.Concat(new[] { it.correctAnswerTR }).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                            || (itemKind == PuzzleKind.Choice && it.distractorsEN.Concat(new[] { it.correctAnswerEN }).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)))
                        {
                            Debug.LogWarning("[OddsCab] Skipping item with empty or duplicate answers " + it.id);
                            continue;
                        }
                        if (it.sourceIds == null) it.sourceIds = Array.Empty<string>();
                        if (it.mechanicId == null) it.mechanicId = string.Empty;
                        _handwritten.Add(it);
                    }
                }
            }
        }

        public static int DistrictIndex(int stop) => Mathf.Clamp((stop - 1) / GameConfig.StopsPerDistrict, 0, GameConfig.DistrictCount - 1);

        /// <summary>
        /// Each district still opens easy and closes hard, but the bands shift across the campaign: district 1 has
        /// 32 easy / 24 medium / 8 hard stops, district 8 has 4 easy / 24 medium / 36 hard. So the journey gets
        /// harder overall instead of repeating the same 20/26/18 curve eight times.
        /// </summary>
        public static int DifficultyForStop(int stop)
        {
            int s = Mathf.Clamp(stop, 1, GameConfig.TotalStops);
            int local = (s - 1) % GameConfig.StopsPerDistrict;
            int district = (s - 1) / GameConfig.StopsPerDistrict;
            int easy = 32 - 4 * district;
            int hard = 8 + 4 * district;
            if (local < easy) return 0;
            if (local < GameConfig.StopsPerDistrict - hard) return 1;
            return 2;
        }

        public static PuzzleCategory CategoryForStop(int stop)
        {
            EnsurePlan();
            return _plan[Mathf.Clamp(stop, 1, GameConfig.TotalStops)].cat;
        }

        public static bool IsMiniatureStop(int stop) => CategoryForStop(stop) == PuzzleCategory.Minyatur;

        // ---------------------------------------------------------------- planning

        static void EnsurePlan()
        {
            if (_plan != null) return;
            EnsureLoaded();
            int total = GameConfig.TotalStops;
            var plan = new Slot[total + 1];

            // 1) Category per stop: interleave each district's quota (largest remaining share first, no repeats).
            int prev = -1;
            int miniCat = Array.IndexOf(PlanCategories, PuzzleCategory.Minyatur);
            for (int d = 0; d < GameConfig.DistrictCount; d++)
            {
                var left = (int[])PerDistrictTargets.Clone();
                var jitterRng = new System.Random(4099 + d * 131);
                for (int k = 0; k < GameConfig.StopsPerDistrict; k++)
                {
                    int stop = d * GameConfig.StopsPerDistrict + k + 1;
                    int remaining = GameConfig.StopsPerDistrict - k;
                    int best = -1;
                    float bestScore = float.MinValue;
                    for (int c = 0; c < left.Length; c++)
                    {
                        if (left[c] == 0) continue;
                        // How far behind its even pace this category is; avoid repeating the previous one.
                        // Small seeded jitter keeps the order from becoming a fixed, predictable cycle.
                        float score = left[c] / (float)remaining - (c == prev ? 10f : 0f) + (float)jitterRng.NextDouble() * 0.06f;
                        // The first stops of the journey teach the basics: no miniature before stop 5.
                        if (c == miniCat && stop < 5) score -= 5f;
                        if (score > bestScore) { bestScore = score; best = c; }
                    }
                    if (stop == 1) best = Array.IndexOf(PlanCategories, PuzzleCategory.Soru);
                    if (left[best] == 0) best = Array.FindIndex(left, x => x > 0);
                    left[best]--;
                    prev = best;
                    plan[stop].cat = PlanCategories[best];
                }
            }

            EnsureAuthoredDifficultyCapacity(plan);

            // 2) Within each category and difficulty band, exhaust authored items in stop order before reuse.
            int miniCounter = 0;
            var genCounter = new Dictionary<PuzzleCategory, int>();
            var useCount = new int[_handwritten.Count];

            foreach (var cat in PlanCategories)
            {
                if (cat == PuzzleCategory.Minyatur) continue;
                for (int diff = 0; diff <= 2; diff++)
                {
                    var slots = new List<int>();
                    for (int s = 1; s <= total; s++)
                        if (s != 1 && plan[s].cat == cat && DifficultyForStop(s) == diff) slots.Add(s);

                    var items = new List<int>();
                    for (int i = 0; i < _handwritten.Count; i++)
                        if (ParseCategory(_handwritten[i].category) == cat && _handwritten[i].difficulty == diff && useCount[i] == 0)
                            items.Add(i);

                    var assigned = new HashSet<int>();
                    if (items.Count > 0 && slots.Count > 0)
                    {
                        int n = Mathf.Min(items.Count, slots.Count);
                        for (int j = 0; j < n; j++)
                        {
                            int s = slots[j];
                            plan[s].kind = SlotKind.Handwritten;
                            plan[s].itemIndex = items[j];
                            useCount[items[j]]++;
                            assigned.Add(s);
                        }
                    }

                    foreach (var s in slots)
                    {
                        if (assigned.Contains(s)) continue;
                        if (HasGenerators(cat, diff))
                        {
                            genCounter.TryGetValue(cat, out int g);
                            plan[s].kind = SlotKind.Generator;
                            plan[s].genIndex = g;
                            genCounter[cat] = g + 1;
                        }
                        else
                        {
                            int reuse = LeastUsedItem(cat, diff, useCount);
                            if (reuse >= 0)
                            {
                                plan[s].kind = SlotKind.Handwritten;
                                plan[s].itemIndex = reuse;
                                useCount[reuse]++;
                            }
                            else
                            {
                                plan[s].kind = SlotKind.Generator; // falls back to a generic generator below
                                genCounter.TryGetValue(cat, out int g);
                                plan[s].genIndex = g;
                                genCounter[cat] = g + 1;
                            }
                        }
                    }
                }
            }

            for (int s = 1; s <= total; s++)
            {
                if (plan[s].cat == PuzzleCategory.Minyatur)
                {
                    plan[s].kind = SlotKind.Miniature;
                    plan[s].miniIndex = miniCounter++;
                }
            }

            // Curated miniatures occupy evenly spaced slots in their own difficulty band, leaving the rest
            // to the existing rotation.
            var miniatureSlots = Enumerable.Range(1, total).Where(s => plan[s].cat == PuzzleCategory.Minyatur).ToList();
            var authoredMiniatures = Enumerable.Range(0, _handwritten.Count)
                .Where(i => ParseCategory(_handwritten[i].category) == PuzzleCategory.Minyatur
                    && (string.Equals(_handwritten[i].puzzleKind, nameof(PuzzleKind.Bells), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(_handwritten[i].puzzleKind, nameof(PuzzleKind.TorchBridge), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(_handwritten[i].puzzleKind, nameof(PuzzleKind.Akari), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(_handwritten[i].puzzleKind, nameof(PuzzleKind.DoorCycle), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(_handwritten[i].puzzleKind, nameof(PuzzleKind.ValveCause), StringComparison.OrdinalIgnoreCase)))
                .OrderBy(i => _handwritten[i].id, StringComparer.Ordinal)
                .ToList();
            for (int diff = 0; diff <= 2; diff++)
            {
                var itemsInBand = authoredMiniatures.Where(i => _handwritten[i].difficulty == diff).ToList();
                var slotsInBand = miniatureSlots.Where(s => DifficultyForStop(s) == diff).ToList();
                for (int j = 0; j < itemsInBand.Count && slotsInBand.Count > 0; j++)
                {
                    int slotIdx = Mathf.Clamp((int)((j + 0.5f) * slotsInBand.Count / itemsInBand.Count), 0, slotsInBand.Count - 1);
                    int stop = slotsInBand[slotIdx];
                    plan[stop].kind = SlotKind.AuthoredMiniature;
                    plan[stop].itemIndex = itemsInBand[j];
                }
            }
            plan[1].kind = SlotKind.StopOne;
            _plan = plan;
        }

        static int LeastUsedItem(PuzzleCategory cat, int diff, int[] useCount)
        {
            int best = -1, bestUse = int.MaxValue, bestGap = int.MaxValue;
            // Prefer the matching difficulty pool so authored content never leaks into a different band.
            for (int i = 0; i < _handwritten.Count; i++)
            {
                if (ParseCategory(_handwritten[i].category) != cat) continue;
                if (_handwritten[i].difficulty != diff) continue;
                int gap = Mathf.Abs(_handwritten[i].difficulty - diff);
                if (useCount[i] < bestUse || (useCount[i] == bestUse && gap < bestGap))
                {
                    best = i; bestUse = useCount[i]; bestGap = gap;
                }
            }
            if (best >= 0) return best;

            // Defensive fallback for a category/difficulty combination with no authored items.
            for (int i = 0; i < _handwritten.Count; i++)
            {
                if (ParseCategory(_handwritten[i].category) != cat) continue;
                int gap = Mathf.Abs(_handwritten[i].difficulty - diff);
                if (useCount[i] < bestUse || (useCount[i] == bestUse && gap < bestGap))
                {
                    best = i; bestUse = useCount[i]; bestGap = gap;
                }
            }
            return best;
        }

        static void EnsureAuthoredDifficultyCapacity(Slot[] plan)
        {
            int authoredEasyRiddles = _handwritten.Count(x => ParseCategory(x.category) == PuzzleCategory.Bilmece && x.difficulty == 0);
            int easyRiddleSlots = Enumerable.Range(2, GameConfig.TotalStops - 1)
                .Count(stop => plan[stop].cat == PuzzleCategory.Bilmece && DifficultyForStop(stop) == 0);
            int deficit = authoredEasyRiddles - easyRiddleSlots;
            if (deficit <= 0) return;

            // The authored pool has more easy riddles than the seeded category order initially provides.
            // Trade a few easy slots from generator-backed categories with medium riddle slots in the same
            // district, preserving every district quota and the per-district difficulty totals.
            var donorCategories = new[] { PuzzleCategory.Hesap, PuzzleCategory.Kelime, PuzzleCategory.Cikarim, PuzzleCategory.Gozlem };
            for (int district = 0; district < GameConfig.DistrictCount && deficit > 0; district++)
            {
                int districtStart = district * GameConfig.StopsPerDistrict + 1;
                int easyEnd = 32 - 4 * district, mediumEnd = GameConfig.StopsPerDistrict - (8 + 4 * district);
                for (int mediumLocal = easyEnd; mediumLocal < mediumEnd && deficit > 0; mediumLocal++)
                {
                    int mediumStop = districtStart + mediumLocal;
                    if (plan[mediumStop].cat != PuzzleCategory.Bilmece) continue;
                    for (int easyLocal = district == 0 ? 1 : 0; easyLocal < easyEnd && deficit > 0; easyLocal++)
                    {
                        int easyStop = districtStart + easyLocal;
                        var donor = plan[easyStop].cat;
                        if (!donorCategories.Contains(donor)) continue;
                        if (!CategoryFitsAtStop(plan, easyStop, PuzzleCategory.Bilmece)
                            || !CategoryFitsAtStop(plan, mediumStop, donor)) continue;

                        plan[easyStop].cat = PuzzleCategory.Bilmece;
                        plan[mediumStop].cat = donor;
                        deficit--;
                        break;
                    }
                }
            }

            if (deficit > 0)
                Debug.LogWarning("[OddsCab] Could not place all authored easy riddles in easy stops; remaining: " + deficit);
        }

        static bool CategoryFitsAtStop(Slot[] plan, int stop, PuzzleCategory category)
        {
            if (stop > 1 && plan[stop - 1].cat == category) return false;
            if (stop < GameConfig.TotalStops && plan[stop + 1].cat == category) return false;
            return true;
        }

        // ---------------------------------------------------------------- generators per category

        static PuzzleGenerators.Generator[] GeneratorsFor(PuzzleCategory cat, int diff)
        {
            if (!PuzzleGenerators.ByCategory.TryGetValue(cat, out var list) || list == null) return Array.Empty<PuzzleGenerators.Generator>();
            // The Soru generator only asks everyday unit facts ("kaç dakika?"), too easy past the first band:
            // medium and hard Soru stops use handwritten knowledge questions instead.
            if (cat == PuzzleCategory.Soru && diff > 0) return Array.Empty<PuzzleGenerators.Generator>();
            return list.Where(x => x.minDiff <= diff).Select(x => x.gen).ToArray();
        }

        static bool HasGenerators(PuzzleCategory cat, int diff) => GeneratorsFor(cat, diff).Length > 0;

        // ---------------------------------------------------------------- public entry point

        public static PuzzleData GetPuzzleForStop(int stop)
        {
            EnsurePlan();
            stop = Mathf.Clamp(stop, 1, GameConfig.TotalStops);
            var rng = new System.Random(stop * 7919 + 104729);
            int diff = DifficultyForStop(stop);
            var slot = _plan[stop];
            PuzzleData p;

            switch (slot.kind)
            {
                case SlotKind.StopOne:
                    // Stop 1 matches the reference UI screen ("Bir saatte kaç dakika vardır?")
                    p = PuzzleGenerators.UnitFact(rng, 0);
                    p.mechanicId = "F01";
                    break;

                case SlotKind.Miniature:
                    // Nine miniature interactions rotate through the 80 campaign miniature stops (~9 visits each).
                    // The shadow lamp only appears while its one-step rule is still new (easy band).
                    switch (slot.miniIndex % 9)
                    {
                        case 0: p = PuzzleGenerators.Bells(rng, diff); break;
                        case 1: p = PuzzleGenerators.Bridge(rng, diff); break;
                        case 2: p = PuzzleGenerators.Route(rng, diff); break;
                        case 3: p = PuzzleGenerators.WaterValveSum(rng, diff); break;
                        case 4: p = PuzzleGenerators.LanternLogic(rng, diff); break;
                        case 5: p = PuzzleGenerators.TorchBridgeGen(rng, diff); break;
                        case 6: p = PuzzleGenerators.DoorCycleGen(rng, diff); break;
                        case 7: p = diff == 0 ? PuzzleGenerators.ShadowLamp(rng, diff) : PuzzleGenerators.Route(rng, diff); break;
                        default: p = PuzzleGenerators.BakeryClock(rng, diff); break;
                    }
                    break;

                case SlotKind.AuthoredMiniature:
                {
                    var item = _handwritten[slot.itemIndex];
                    if (Enum.TryParse(item.puzzleKind, true, out PuzzleKind authoredKind) && authoredKind == PuzzleKind.TorchBridge)
                    {
                        p = new PuzzleData
                        {
                            id = item.id,
                            category = PuzzleCategory.Minyatur,
                            kind = PuzzleKind.TorchBridge,
                            difficulty = item.difficulty,
                            seconds = item.durationSeconds,
                            promptTR = item.promptTR,
                            promptEN = item.promptEN,
                            answerTR = item.correctAnswerTR,
                            answerEN = item.correctAnswerEN,
                            ints = item.ints,
                            answerValue = item.answerValue,
                            explainTR = item.explanationTR,
                            explainEN = item.explanationEN,
                            mechanicId = item.mechanicId,
                            sourceIds = item.sourceIds,
                        };
                    }
                    else if (Enum.TryParse(item.puzzleKind, true, out authoredKind) && authoredKind == PuzzleKind.Akari)
                    {
                        p = new PuzzleData
                        {
                            id = item.id,
                            category = PuzzleCategory.Minyatur,
                            kind = PuzzleKind.Akari,
                            difficulty = item.difficulty,
                            seconds = item.durationSeconds,
                            promptTR = item.promptTR,
                            promptEN = item.promptEN,
                            answerTR = item.correctAnswerTR,
                            answerEN = item.correctAnswerEN,
                            akariSolutions = item.ints,
                            explainTR = item.explanationTR,
                            explainEN = item.explanationEN,
                            mechanicId = item.mechanicId,
                            sourceIds = item.sourceIds,
                        };
                    }
                    else if (Enum.TryParse(item.puzzleKind, true, out authoredKind) && authoredKind == PuzzleKind.DoorCycle)
                    {
                        p = new PuzzleData
                        {
                            id = item.id,
                            category = PuzzleCategory.Minyatur,
                            kind = PuzzleKind.DoorCycle,
                            difficulty = item.difficulty,
                            seconds = item.durationSeconds,
                            promptTR = item.promptTR,
                            promptEN = item.promptEN,
                            answerTR = item.correctAnswerTR,
                            answerEN = item.correctAnswerEN,
                            doorPeriods = item.ints,
                            answerValue = item.answerValue,
                            explainTR = item.explanationTR,
                            explainEN = item.explanationEN,
                            mechanicId = item.mechanicId,
                            sourceIds = item.sourceIds,
                        };
                    }
                    else if (Enum.TryParse(item.puzzleKind, true, out authoredKind) && authoredKind == PuzzleKind.ValveCause)
                    {
                        p = new PuzzleData
                        {
                            id = item.id,
                            category = PuzzleCategory.Minyatur,
                            kind = PuzzleKind.ValveCause,
                            difficulty = item.difficulty,
                            seconds = item.durationSeconds,
                            promptTR = item.promptTR,
                            promptEN = item.promptEN,
                            answerTR = item.correctAnswerTR,
                            answerEN = item.correctAnswerEN,
                            ints = item.ints,
                            answerValue = item.answerValue,
                            explainTR = item.explanationTR,
                            explainEN = item.explanationEN,
                            mechanicId = item.mechanicId,
                            sourceIds = item.sourceIds,
                        };
                    }
                    else
                    {
                        int answer = item.answerValue;
                        p = new PuzzleData
                        {
                            id = item.id,
                            category = PuzzleCategory.Minyatur,
                            kind = PuzzleKind.Bells,
                            difficulty = item.difficulty,
                            seconds = item.durationSeconds,
                            promptTR = item.promptTR,
                            promptEN = item.promptEN,
                            optionsTR = new[] { "1", "2", "3", "4" },
                            optionsEN = new[] { "1", "2", "3", "4" },
                            correct = Mathf.Clamp(answer - 1, 0, 3),
                            ints = item.ints,
                            answerValue = answer,
                            explainTR = item.explanationTR,
                            explainEN = item.explanationEN,
                            mechanicId = item.mechanicId,
                            sourceIds = item.sourceIds,
                        };
                    }
                    break;
                }

                case SlotKind.Handwritten:
                {
                    var item = _handwritten[slot.itemIndex];
                    if (Enum.TryParse(item.puzzleKind, true, out PuzzleKind itemKind) && itemKind == PuzzleKind.Letters)
                    {
                        p = new PuzzleData
                        {
                            id = item.id, category = ParseCategory(item.category), kind = PuzzleKind.Letters,
                            difficulty = item.difficulty, seconds = item.durationSeconds,
                            promptTR = item.promptTR, promptEN = item.promptEN,
                            answerTR = item.correctAnswerTR, answerEN = item.correctAnswerEN,
                            lettersTR = item.lettersTR, lettersEN = item.lettersEN,
                            explainTR = item.explanationTR, explainEN = item.explanationEN,
                        };
                    }
                    else
                    {
                        var distract = new List<string[]>
                        {
                            new[] { item.distractorsTR[0], item.distractorsEN[0] },
                            new[] { item.distractorsTR[1], item.distractorsEN[1] },
                            new[] { item.distractorsTR[2], item.distractorsEN[2] },
                        };
                        p = PuzzleGenerators.Choice(
                            rng, item.id, ParseCategory(item.category), item.difficulty, item.durationSeconds,
                            item.promptTR, item.promptEN,
                            new[] { item.correctAnswerTR, item.correctAnswerEN },
                            distract, item.explanationTR, item.explanationEN);
                    }
                    p.mechanicId = item.mechanicId;
                    p.sourceIds = item.sourceIds;
                    break;
                }

                default:
                {
                    var gens = GeneratorsFor(slot.cat, diff);
                    if (gens.Length == 0) gens = GeneratorsFor(PuzzleCategory.Hesap, diff);
                    p = gens[slot.genIndex % gens.Length](rng, diff);
                    break;
                }
            }

            p.stop = stop;
            return p;
        }

        static PuzzleCategory ParseCategory(string s)
        {
            if (Enum.TryParse<PuzzleCategory>(s, true, out var c)) return c;
            return PuzzleCategory.Soru;
        }
    }
}

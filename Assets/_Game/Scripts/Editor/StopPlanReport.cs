using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OddsCab.Puzzles;
using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>Exports an auditable roster and summary from the real StopPlanner output.</summary>
    [InitializeOnLoad]
    public static class StopPlanReport
    {
        const string RequestRelativePath = ".game-dev/requests/stop-plan-audit.request";
        const string DefaultReportRelativePath = ".game-dev/audits/stop-plan-512-audit.json";
        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string RequestPath => Path.Combine(ProjectRoot, RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));

        [Serializable]
        sealed class AuthoredPoolFile { public int schemaVersion; public AuthoredPoolItem[] items; }
        [Serializable]
        sealed class AuthoredPoolItem { public string id; public string category; public int difficulty; public string[] sourceIds; }
        [Serializable]
        sealed class StopRow
        {
            public int stop, district, difficulty;
            public string category, kind, id, mechanicId;
            public string[] sourceIds;
            public string promptTR, promptEN, explainTR, explainEN;
        }
        [Serializable]
        sealed class CountRow { public string key; public int count; }
        [Serializable]
        sealed class AuthoredUseRow { public string id, category; public string[] sourceIds; public int useCount; public int[] stops; }
        [Serializable]
        sealed class RepeatAuditRow { public string category; public int difficulty; public int poolSize; public int uniqueSeenAtFirstRepeat; public int firstRepeatStop; public bool poolExhaustedBeforeRepeat; }
        [Serializable]
        sealed class AuditReport
        {
            public string schema = "odds_cab.stop_plan_audit.v1";
            public string generatedUtc;
            public int totalStops;
            public int[] districtCategoryTargets;
            public CountRow[] categoryCounts, difficultyCounts, categoryDifficultyCounts, kindCounts, mechanicCounts;
            public int uniqueAuthoredPoolItems, authoredPoolItemsAssigned, authoredPoolItemsNeverAssigned;
            public string[] authoredPoolItemsNeverAssignedIds;
            public AuthoredUseRow[] authoredUses;
            public RepeatAuditRow[] authoredRepeatAudit;
            public StopRow[] stops;
            public string[] issues;
            public bool passesStructureAndDistribution;
        }

        static StopPlanReport()
        {
            EditorApplication.update -= PollForAuditRequest;
            EditorApplication.update += PollForAuditRequest;
        }

        static void PollForAuditRequest()
        {
            if (!File.Exists(RequestPath)) return;
            string reportPath = null;
            try { reportPath = File.ReadAllText(RequestPath).Trim(); }
            catch (IOException) { return; }
            if (string.IsNullOrWhiteSpace(reportPath)) reportPath = DefaultReportRelativePath;
            try
            {
                string resolvedPath = Path.IsPathRooted(reportPath)
                    ? reportPath
                    : Path.Combine(ProjectRoot, reportPath.Replace('/', Path.DirectorySeparatorChar));
                ExportStopPlanAudit(resolvedPath);
                File.Delete(RequestPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[OddsCab] Stop plan audit failed: " + e);
                try { File.Delete(RequestPath); }
                catch (IOException) { }
            }
        }

        [MenuItem("OddsCab/Export 512-Stop Audit")]
        public static void ExportDefaultStopPlanAudit()
        {
            string path = Path.Combine(ProjectRoot, DefaultReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ExportStopPlanAudit(path);
            Debug.Log("[OddsCab] Exported 512-stop audit: " + path);
        }

        static void ExportStopPlanAudit(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var categoryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var difficultyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var categoryDifficultyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var kindCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var mechanicCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var authoredUses = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var rows = new StopRow[GameConfig.TotalStops];
            var issues = new List<string>();
            PuzzleCategory? previousCategory = null;
            var miniStops = new List<int>();

            for (int stop = 1; stop <= GameConfig.TotalStops; stop++)
            {
                var puzzle = StopPlanner.GetPuzzleForStop(stop);
                var plannedCategory = StopPlanner.CategoryForStop(stop);
                int difficulty = StopPlanner.DifficultyForStop(stop);
                if (puzzle == null)
                {
                    issues.Add("Stop " + stop + ": planner returned null.");
                    continue;
                }

                string category = puzzle.category.ToString();
                string kind = puzzle.kind.ToString();
                AddCount(categoryCounts, category);
                AddCount(difficultyCounts, difficulty.ToString());
                AddCount(categoryDifficultyCounts, category + "|" + difficulty);
                AddCount(kindCounts, kind);
                AddCount(mechanicCounts, string.IsNullOrWhiteSpace(puzzle.mechanicId) ? "<missing>" : puzzle.mechanicId);

                if (puzzle.category != plannedCategory) issues.Add("Stop " + stop + ": category " + category + " does not match planned " + plannedCategory + ".");
                if (puzzle.difficulty != difficulty) issues.Add("Stop " + stop + ": puzzle difficulty " + puzzle.difficulty + " does not match stop difficulty " + difficulty + ".");
                if (string.IsNullOrWhiteSpace(puzzle.id)) issues.Add("Stop " + stop + ": missing puzzle id.");
                if (string.IsNullOrWhiteSpace(puzzle.promptTR) || string.IsNullOrWhiteSpace(puzzle.promptEN)) issues.Add("Stop " + stop + ": missing bilingual prompt.");
                if (string.IsNullOrWhiteSpace(puzzle.explainTR) || string.IsNullOrWhiteSpace(puzzle.explainEN)) issues.Add("Stop " + stop + ": missing bilingual explanation.");
                if (puzzle.category != PuzzleCategory.Minyatur && string.IsNullOrWhiteSpace(puzzle.mechanicId)) issues.Add("Stop " + stop + ": missing mechanic mapping.");
                if (puzzle.category == PuzzleCategory.Minyatur) miniStops.Add(stop);
                if (previousCategory.HasValue && previousCategory.Value == puzzle.category) issues.Add("Stops " + (stop - 1) + " and " + stop + ": category repeats consecutively.");
                previousCategory = puzzle.category;

                if (puzzle.kind == PuzzleKind.Choice || puzzle.kind == PuzzleKind.Bells || puzzle.kind == PuzzleKind.Route)
                {
                    ValidateOptions(puzzle.optionsTR, puzzle.correct, stop, "TR", issues);
                    ValidateOptions(puzzle.optionsEN, puzzle.correct, stop, "EN", issues);
                }
                if (puzzle.kind == PuzzleKind.Letters)
                {
                    if (!CanSpell(puzzle.answerTR, puzzle.lettersTR) || !CanSpell(puzzle.answerEN, puzzle.lettersEN))
                        issues.Add("Stop " + stop + ": letter tiles cannot spell the answer.");
                }
                if (stop < 5 && puzzle.category == PuzzleCategory.Minyatur) issues.Add("Stop " + stop + ": miniature appears before stop 5.");

                var sourceIds = puzzle.sourceIds ?? Array.Empty<string>();
                if (sourceIds.Length > 0 && !string.IsNullOrWhiteSpace(puzzle.id))
                {
                    if (!authoredUses.TryGetValue(puzzle.id, out var uses)) authoredUses[puzzle.id] = uses = new List<int>();
                    uses.Add(stop);
                }
                rows[stop - 1] = new StopRow
                {
                    stop = stop,
                    district = StopPlanner.DistrictIndex(stop) + 1,
                    difficulty = difficulty,
                    category = category,
                    kind = kind,
                    id = puzzle.id,
                    mechanicId = puzzle.mechanicId,
                    sourceIds = sourceIds,
                    promptTR = puzzle.promptTR,
                    promptEN = puzzle.promptEN,
                    explainTR = puzzle.explainTR,
                    explainEN = puzzle.explainEN
                };
            }

            var pool = LoadAuthoredPool();
            var poolById = pool.Where(x => !string.IsNullOrWhiteSpace(x.id)).GroupBy(x => x.id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var authoredUseRows = authoredUses.OrderBy(x => x.Key, StringComparer.Ordinal).Select(kv =>
            {
                var item = poolById.TryGetValue(kv.Key, out var found) ? found : null;
                return new AuthoredUseRow
                {
                    id = kv.Key,
                    category = item?.category ?? "<not in current JSON>",
                    sourceIds = item?.sourceIds ?? Array.Empty<string>(),
                    useCount = kv.Value.Count,
                    stops = kv.Value.ToArray()
                };
            }).ToArray();
            var assignedIds = new HashSet<string>(authoredUses.Keys, StringComparer.Ordinal);
            var unused = poolById.Keys.Where(id => !assignedIds.Contains(id)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (unused.Length > 0)
                issues.Add("Authored pool items never assigned to any stop: " + string.Join(", ", unused) + ".");
            var repeatAudits = BuildRepeatAudits(rows, poolById, issues);

            for (int d = 0; d < GameConfig.DistrictCount; d++)
            {
                for (int c = 0; c < StopPlanner.PerDistrictTargets.Length; c++)
                {
                    string name = ((PuzzleCategory)c).ToString();
                    int count = rows.Count(x => x != null && x.district == d + 1 && x.category == name);
                    if (count != StopPlanner.PerDistrictTargets[c]) issues.Add("District " + (d + 1) + ": " + name + " count " + count + " != target " + StopPlanner.PerDistrictTargets[c] + ".");
                }
                int[] difficultyTargets = { 20, 26, 18 };
                for (int diff = 0; diff < difficultyTargets.Length; diff++)
                {
                    int count = rows.Count(x => x != null && x.district == d + 1 && x.difficulty == diff);
                    if (count != difficultyTargets[diff]) issues.Add("District " + (d + 1) + ": difficulty " + diff + " count " + count + " != target " + difficultyTargets[diff] + ".");
                }
            }
            if (miniStops.Count != 80) issues.Add("Campaign miniature total " + miniStops.Count + " != 80.");

            var report = new AuditReport
            {
                generatedUtc = DateTime.UtcNow.ToString("o"),
                totalStops = GameConfig.TotalStops,
                districtCategoryTargets = StopPlanner.PerDistrictTargets,
                categoryCounts = ToCounts(categoryCounts),
                difficultyCounts = ToCounts(difficultyCounts),
                categoryDifficultyCounts = ToCounts(categoryDifficultyCounts),
                kindCounts = ToCounts(kindCounts),
                mechanicCounts = ToCounts(mechanicCounts),
                uniqueAuthoredPoolItems = poolById.Count,
                authoredPoolItemsAssigned = poolById.Count - unused.Length,
                authoredPoolItemsNeverAssigned = unused.Length,
                authoredPoolItemsNeverAssignedIds = unused,
                authoredUses = authoredUseRows,
                authoredRepeatAudit = repeatAudits,
                stops = rows,
                issues = issues.ToArray(),
                passesStructureAndDistribution = issues.Count == 0
            };
            File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        static AuthoredPoolItem[] LoadAuthoredPool()
        {
            var asset = Resources.Load<TextAsset>("Puzzles/handwritten");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return Array.Empty<AuthoredPoolItem>();
            var parsed = JsonUtility.FromJson<AuthoredPoolFile>(asset.text);
            if (parsed == null || parsed.schemaVersion != 1 || parsed.items == null) return Array.Empty<AuthoredPoolItem>();
            return parsed.items;
        }

        static RepeatAuditRow[] BuildRepeatAudits(StopRow[] rows, Dictionary<string, AuthoredPoolItem> pool, List<string> issues)
        {
            var result = new List<RepeatAuditRow>();
            foreach (var category in Enum.GetNames(typeof(PuzzleCategory)))
            {
                for (int difficulty = 0; difficulty <= 2; difficulty++)
                {
                    var ids = pool.Values.Where(x => string.Equals(x.category, category, StringComparison.OrdinalIgnoreCase)
                        && x.difficulty == difficulty).Select(x => x.id).Distinct(StringComparer.Ordinal).ToArray();
                    if (ids.Length == 0) continue;
                    var idSet = new HashSet<string>(ids, StringComparer.Ordinal);
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    int firstRepeatStop = 0, uniqueAtRepeat = 0;
                    for (int i = 0; i < rows.Length; i++)
                    {
                        var row = rows[i];
                        if (row == null || row.category != category || !idSet.Contains(row.id)) continue;
                        if (!seen.Add(row.id) && firstRepeatStop == 0)
                        {
                            firstRepeatStop = row.stop;
                            uniqueAtRepeat = seen.Count;
                        }
                    }
                    bool exhausted = firstRepeatStop == 0 || uniqueAtRepeat >= ids.Length;
                    result.Add(new RepeatAuditRow
                    {
                        category = category,
                        difficulty = difficulty,
                        poolSize = ids.Length,
                        uniqueSeenAtFirstRepeat = uniqueAtRepeat,
                        firstRepeatStop = firstRepeatStop,
                        poolExhaustedBeforeRepeat = exhausted
                    });
                    if (!exhausted) issues.Add(category + " difficulty " + difficulty + ": authored content repeated at stop " + firstRepeatStop + " before all " + ids.Length + " pool items appeared.");
                }
            }
            return result.ToArray();
        }

        static bool CanSpell(string answer, string tiles)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(tiles)) return false;
            var available = new Dictionary<char, int>();
            foreach (char c in tiles) available[c] = available.TryGetValue(c, out int n) ? n + 1 : 1;
            foreach (char c in answer)
            {
                if (!available.TryGetValue(c, out int n) || n == 0) return false;
                available[c] = n - 1;
            }
            return true;
        }

        static void ValidateOptions(string[] options, int correct, int stop, string language, List<string> issues)
        {
            if (options == null || options.Length != 4)
            {
                issues.Add("Stop " + stop + ": " + language + " options length is not 4.");
                return;
            }
            if (options.Any(string.IsNullOrWhiteSpace) || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                issues.Add("Stop " + stop + ": " + language + " options are empty or duplicated.");
            if (correct < 0 || correct >= options.Length) issues.Add("Stop " + stop + ": correct option index is out of range.");
        }

        static void AddCount(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        static CountRow[] ToCounts(Dictionary<string, int> counts) => counts.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new CountRow { key = x.Key, count = x.Value }).ToArray();
    }
}

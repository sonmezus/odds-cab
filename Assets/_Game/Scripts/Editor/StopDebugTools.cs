using OddsCab.Puzzles;
using UnityEditor;
using UnityEngine;

namespace OddsCab.Editor
{
    /// <summary>
    /// Designer/QA helpers to visit a specific puzzle type without replaying the campaign.
    /// The real save stop is remembered and can be restored.
    /// </summary>
    public static class StopDebugTools
    {
        const string SavedStopKey = "OddsCab.Debug.SavedStop";

        [MenuItem("OddsCab/Debug/Jump To Next Ordering Stop")]
        public static void JumpToNextOrdering() => JumpToNext(PuzzleKind.Ordering);

        [MenuItem("OddsCab/Debug/Jump To Next Bells Stop")]
        public static void JumpToNextBells() => JumpToNext(PuzzleKind.Bells);

        [MenuItem("OddsCab/Debug/Jump To Next Bridge Stop")]
        public static void JumpToNextBridge() => JumpToNext(PuzzleKind.Bridge);

        [MenuItem("OddsCab/Debug/Jump To Next Route Stop")]
        public static void JumpToNextRoute() => JumpToNext(PuzzleKind.Route);

        [MenuItem("OddsCab/Debug/Jump To Next Letters Stop")]
        public static void JumpToNextLetters() => JumpToNext(PuzzleKind.Letters);

        static void JumpToNext(PuzzleKind kind)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OddsCab] Stop play mode first."); return; }
            SaveSystem.Load();
            int from = SaveSystem.Data.currentStop;
            if (!EditorPrefs.HasKey(SavedStopKey)) EditorPrefs.SetInt(SavedStopKey, from);
            for (int i = 1; i <= GameConfig.TotalStops; i++)
            {
                int stop = (from - 1 + i) % GameConfig.TotalStops + 1;
                if (StopPlanner.GetPuzzleForStop(stop).kind == kind)
                {
                    SaveSystem.Data.currentStop = stop;
                    SaveSystem.Save();
                    Debug.Log($"[OddsCab] Save moved to stop {stop} ({kind}). Real stop {EditorPrefs.GetInt(SavedStopKey)} is remembered.");
                    return;
                }
            }
        }

        [MenuItem("OddsCab/Debug/Return To Saved Stop")]
        public static void ReturnToSavedStop()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[OddsCab] Stop play mode first."); return; }
            if (!EditorPrefs.HasKey(SavedStopKey)) { Debug.Log("[OddsCab] No remembered stop."); return; }
            SaveSystem.Load();
            SaveSystem.Data.currentStop = EditorPrefs.GetInt(SavedStopKey);
            SaveSystem.Save();
            EditorPrefs.DeleteKey(SavedStopKey);
            Debug.Log($"[OddsCab] Save returned to stop {SaveSystem.Data.currentStop}.");
        }
    }
}

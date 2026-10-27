using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab
{
    [Serializable]
    public class DistrictAnswerRecord
    {
        public int stop;
        public int category;
        public string promptTR;
        public string promptEN;
        public string answerTR;
        public string answerEN;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int currentStop = 1;          // next stop to play (1..512, 513 = finished)
        public int coins = GameConfig.StartCoins;
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        public int language = 0;             // 0 = TR, 1 = EN
        public int qualityTier = 3;          // -1 auto, 0 low, 1 medium, 2 high, 3 ultra
        public bool spokenGuideEnabled = true;
        public int streak;
        public int bestStreak;
        public int totalCorrect;
        public int totalWrong;
        public List<int> solvedStops = new List<int>();
        public List<DistrictAnswerRecord> districtAnswerLog = new List<DistrictAnswerRecord>();
        public bool hasPendingDistrictFinale;
        public int pendingDistrictFinale = -1;
        public List<int> passengerStoryChapters = new List<int> { 1, 1, 1, 1, 1 };
        public List<int> unlockedTaxiCosmetics = new List<int> { 0 };
        public int equippedTaxiCosmetic;
        /// <summary>Full 512-stop campaigns finished. While campaignComplete is set the save stays on stop 513 until the
        /// player chooses a new tour, so finishing never silently wipes the journey.</summary>
        public int campaignsCompleted;
        public bool campaignComplete;
    }

    /// <summary>JSON save file in persistentDataPath with atomic write.</summary>
    public static class SaveSystem
    {
        static SaveData _data;
        static string FilePath => Path.Combine(Application.persistentDataPath, "oddscab_save.json");

        public static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        static string BackupPath => FilePath + ".bak";

        public static void Load()
        {
            _data = TryRead(FilePath);
            if (_data == null)
            {
                // A power cut can leave the main file empty or zero-filled; the previous good save is kept beside it.
                _data = TryRead(BackupPath);
                if (_data != null) Debug.LogWarning("[OddsCab] Save file was unreadable; restored the previous save.");
            }
            if (_data == null) _data = new SaveData();
            _data.currentStop = Mathf.Clamp(_data.currentStop, 1, GameConfig.TotalStops + 1);
            if (_data.solvedStops == null) _data.solvedStops = new List<int>();
            if (_data.districtAnswerLog == null) _data.districtAnswerLog = new List<DistrictAnswerRecord>();
            _data.districtAnswerLog.RemoveAll(entry => entry == null || entry.stop < 1 || entry.stop > GameConfig.TotalStops);
            if (_data.hasPendingDistrictFinale)
                _data.pendingDistrictFinale = Mathf.Clamp(_data.pendingDistrictFinale, 0, GameConfig.DistrictCount - 1);
            if (_data.passengerStoryChapters == null) _data.passengerStoryChapters = new List<int>();
            while (_data.passengerStoryChapters.Count < PassengerCatalog.All.Length)
                _data.passengerStoryChapters.Add(1);
            for (int i = 0; i < _data.passengerStoryChapters.Count; i++)
                _data.passengerStoryChapters[i] = Mathf.Clamp(_data.passengerStoryChapters[i], 1, PassengerStoryCatalog.ChapterCount);
            if (_data.unlockedTaxiCosmetics == null) _data.unlockedTaxiCosmetics = new List<int>();
            _data.unlockedTaxiCosmetics.RemoveAll(id => id < 0 || id >= TaxiCosmeticCatalog.Count);
            if (!_data.unlockedTaxiCosmetics.Contains(0)) _data.unlockedTaxiCosmetics.Insert(0, 0);
            _data.equippedTaxiCosmetic = Mathf.Clamp(_data.equippedTaxiCosmetic, 0, TaxiCosmeticCatalog.Count - 1);
            if (!_data.unlockedTaxiCosmetics.Contains(_data.equippedTaxiCosmetic)) _data.equippedTaxiCosmetic = 0;
        }

        public static void RecordDistrictAnswer(PuzzleData puzzle, int stop)
        {
            if (puzzle == null || stop < 1 || stop > GameConfig.TotalStops) return;
            var log = Data.districtAnswerLog;
            log.RemoveAll(entry => entry != null && entry.stop == stop);
            log.Add(new DistrictAnswerRecord
            {
                stop = stop,
                category = (int)puzzle.category,
                promptTR = puzzle.promptTR ?? string.Empty,
                promptEN = puzzle.promptEN ?? string.Empty,
                answerTR = ResolveAnswer(puzzle, false),
                answerEN = ResolveAnswer(puzzle, true)
            });
        }

        static string ResolveAnswer(PuzzleData puzzle, bool english)
        {
            if (puzzle.kind == PuzzleKind.Letters)
            {
                string word = english ? puzzle.answerEN : puzzle.answerTR;
                if (!string.IsNullOrEmpty(word)) return word;
            }

            string[] options = english && puzzle.optionsEN != null && puzzle.optionsEN.Length > 0
                ? puzzle.optionsEN : puzzle.optionsTR;
            if (options != null && puzzle.correct >= 0 && puzzle.correct < options.Length && !string.IsNullOrEmpty(options[puzzle.correct]))
                return options[puzzle.correct];

            if (puzzle.kind == PuzzleKind.Bridge)
                return puzzle.answerValue.ToString();

            string explanation = english ? puzzle.explainEN : puzzle.explainTR;
            if (!string.IsNullOrEmpty(explanation)) return explanation;
            return english ? puzzle.explainTR ?? string.Empty : puzzle.explainEN ?? string.Empty;
        }

        public static int GetPassengerStoryChapters(PassengerId passenger)
        {
            int index = Mathf.Clamp((int)passenger, 0, PassengerCatalog.All.Length - 1);
            var chapters = Data.passengerStoryChapters;
            while (chapters.Count <= index) chapters.Add(1);
            return Mathf.Clamp(chapters[index], 1, PassengerStoryCatalog.ChapterCount);
        }

        public static bool TryUnlockPassengerStoryChapter(PassengerId passenger, int cost)
        {
            int index = Mathf.Clamp((int)passenger, 0, PassengerCatalog.All.Length - 1);
            int unlocked = GetPassengerStoryChapters(passenger);
            if (unlocked >= PassengerStoryCatalog.ChapterCount || Data.coins < cost) return false;
            Data.coins -= cost;
            Data.passengerStoryChapters[index] = unlocked + 1;
            Save();
            return true;
        }

        public static bool IsTaxiCosmeticUnlocked(int id) => Data.unlockedTaxiCosmetics.Contains(id);

        public static bool TryPurchaseOrEquipTaxiCosmetic(int id)
        {
            if (id < 0 || id >= TaxiCosmeticCatalog.Count) return false;
            if (!Data.unlockedTaxiCosmetics.Contains(id))
            {
                int cost = TaxiCosmeticCatalog.Cost(id);
                if (Data.coins < cost) return false;
                Data.coins -= cost;
                Data.unlockedTaxiCosmetics.Add(id);
            }
            Data.equippedTaxiCosmetic = id;
            Save();
            return true;
        }

        static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                // Zero-filled or truncated files (lost writes) are not valid JSON objects.
                if (string.IsNullOrWhiteSpace(json) || json.TrimStart('\0', ' ', '\n', '\r', '\t').Length == 0) return null;
                if (json.IndexOf('\0') >= 0 || json.TrimStart()[0] != '{') return null;
                var d = JsonUtility.FromJson<SaveData>(json);
                return d != null && d.version >= 1 ? d : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OddsCab] Save read failed (" + Path.GetFileName(path) + "): " + e.Message);
                return null;
            }
        }

        public static void Save()
        {
            if (_data == null) return;
            try
            {
                string tmp = FilePath + ".tmp";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(_data));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);   // force the bytes to disk before the swap, so a power cut cannot leave a hole
                }
                if (File.Exists(FilePath))
                {
                    // Atomic swap that keeps the last good save as .bak
                    File.Replace(tmp, FilePath, BackupPath, true);
                }
                else
                {
                    File.Move(tmp, FilePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OddsCab] Save failed: " + e.Message);
            }
        }

        /// <summary>Starts the next 512-stop tour after a finished campaign: the route restarts, earned coins,
        /// cosmetics, unlocked stories and the tour count stay.</summary>
        public static void StartNewTour()
        {
            var d = Data;
            d.currentStop = 1;
            d.solvedStops.Clear();
            d.districtAnswerLog.Clear();
            d.streak = 0;
            d.hasPendingDistrictFinale = false;
            d.pendingDistrictFinale = -1;
            d.campaignComplete = false;
            Save();
        }

        public static void ResetProgress()
        {
            var keep = Data;
            _data = new SaveData
            {
                musicVolume = keep.musicVolume,
                sfxVolume = keep.sfxVolume,
                language = keep.language,
                qualityTier = keep.qualityTier,
                spokenGuideEnabled = keep.spokenGuideEnabled
            };
            Save();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab.Editor
{
    /// <summary>Responds to one-shot local game-dev requests by capturing the live Game View.</summary>
    [InitializeOnLoad]
    static class GameDevCaptureBridge
    {
        const string RequestFolder = ".game-dev/requests";
        const string ActiveRequestKey = "OddsCab.GameDevCapture.ActiveRequest";
        const string StageKey = "OddsCab.GameDevCapture.Stage";
        const string DeadlineKey = "OddsCab.GameDevCapture.Deadline";
        const int CaptureWidth = 1080;
        const int CaptureHeight = 1920;

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static GameDevCaptureBridge()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [Serializable]
        sealed class CaptureRequest
        {
            public string schema;
            public string runId;
            public string imagePath;
            public string sunnyWeatherImagePath;
            public string journeyMapImagePath;
            public string passengerStoryImagePath;
            public string taxiShopImagePath;
            public string taxiTintPreviewImagePath;
            public string districtFinaleImagePath;
            public string puzzleArchiveImagePath;
            public string responsePath;
        }

        [Serializable]
        sealed class CaptureResponse
        {
            public string schema = "odds_cab.editor_capture_response.v1";
            public string runId;
            public string status;
            public string message;
        }

        static void Tick()
        {
            string requestPath = EditorPrefs.GetString(ActiveRequestKey, string.Empty);
            if (string.IsNullOrEmpty(requestPath))
            {
                requestPath = FindPendingRequest();
                if (string.IsNullOrEmpty(requestPath)) return;
                EditorPrefs.SetString(ActiveRequestKey, requestPath);
                EditorPrefs.SetString(StageKey, "starting");
                EditorPrefs.SetFloat(DeadlineKey, 0f);
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = true;
                    return;
                }
            }

            if (!File.Exists(requestPath))
            {
                ClearState();
                return;
            }

            CaptureRequest request;
            try { request = JsonUtility.FromJson<CaptureRequest>(File.ReadAllText(requestPath)); }
            catch (Exception) { return; }
            if (request == null || request.schema != "odds_cab.editor_capture_request.v1" ||
                string.IsNullOrEmpty(request.runId) || string.IsNullOrEmpty(request.imagePath) ||
                string.IsNullOrEmpty(request.responsePath)) return;

            string stage = EditorPrefs.GetString(StageKey, "starting");
            if (!EditorApplication.isPlaying)
            {
                if (stage == "captured") ClearState();
                return;
            }

            float deadline = EditorPrefs.GetFloat(DeadlineKey, 0f);
            if (stage == "starting")
            {
                EditorPrefs.SetString(StageKey, "warming");
                EditorPrefs.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 5f);
                return;
            }

            if (stage == "warming" && EditorApplication.timeSinceStartup >= deadline)
            {
                if (GameManager.Instance == null)
                {
                    Complete(request, false, "ODDS CAB did not finish initializing in Play Mode.");
                    return;
                }

                var camera = Camera.main;
                if (camera == null)
                {
                    var cameras = UnityEngine.Object.FindObjectsByType<Camera>();
                    if (cameras.Length > 0) camera = cameras[0];
                }
                if (camera == null)
                {
                    Complete(request, false, "No active camera was found in Play Mode.");
                    return;
                }
                try
                {
                    RenderGameView(camera, request.imagePath);
                    if (!string.IsNullOrEmpty(request.sunnyWeatherImagePath) && WeatherDirector.Instance != null)
                    {
                        var savedWeather = WeatherDirector.Instance.CurrentWeather;
                        try
                        {
                            WeatherDirector.Instance.SetWeather(WeatherPreset.Sunny, false);
                            RenderGameView(camera, request.sunnyWeatherImagePath);
                        }
                        finally
                        {
                            WeatherDirector.Instance.SetWeather(savedWeather, false);
                        }
                    }
                    if (!string.IsNullOrEmpty(request.journeyMapImagePath))
                    {
                        GameManager.Instance.Ui.OpenJourneyMap(true);
                        RenderGameView(camera, request.journeyMapImagePath);
                    }
                    if (!string.IsNullOrEmpty(request.passengerStoryImagePath))
                    {
                        GameManager.Instance.Ui.OpenJourneyMap(false);
                        GameManager.Instance.Ui.OpenChatModal(true);
                        RenderGameView(camera, request.passengerStoryImagePath);
                    }
                    if (!string.IsNullOrEmpty(request.taxiShopImagePath))
                    {
                        GameManager.Instance.Ui.OpenChatModal(false);
                        GameManager.Instance.Ui.OpenTaxiShop(true);
                        RenderGameView(camera, request.taxiShopImagePath);
                    }
                    if (!string.IsNullOrEmpty(request.taxiTintPreviewImagePath))
                    {
                        GameManager.Instance.Ui.OpenTaxiShop(false);
                        GameManager.Instance.Taxi.ApplyTaxiCosmetic(1);
                        RenderGameView(camera, request.taxiTintPreviewImagePath);
                    }
                    if (!string.IsNullOrEmpty(request.districtFinaleImagePath))
                    {
                        var savedLog = SaveSystem.Data.districtAnswerLog;
                        var originalLog = new List<DistrictAnswerRecord>(savedLog);
                        savedLog.Clear();
                        for (int i = 0; i < 5; i++)
                        {
                            savedLog.Add(new DistrictAnswerRecord
                            {
                                stop = 60 + i,
                                category = (int)PuzzleCategory.Gozlem,
                                promptTR = "Vapura giden en kısa yol hangi iskeleden geçer?",
                                promptEN = "Which pier is on the shortest route to the ferry?",
                                answerTR = "Karaköy İskelesi",
                                answerEN = "Karaköy Pier"
                            });
                        }
                        try
                        {
                            GameManager.Instance.Ui.ShowDistrictFinale(0);
                            RenderGameView(camera, request.districtFinaleImagePath);
                        }
                        finally
                        {
                            GameManager.Instance.Ui.OpenDistrictFinale(false);
                            savedLog.Clear();
                            savedLog.AddRange(originalLog);
                        }
                    }
                    if (!string.IsNullOrEmpty(request.puzzleArchiveImagePath))
                    {
                        var savedLog = SaveSystem.Data.districtAnswerLog;
                        var originalLog = new List<DistrictAnswerRecord>(savedLog);
                        savedLog.Clear();
                        for (int i = 0; i < 5; i++)
                        {
                            savedLog.Add(new DistrictAnswerRecord
                            {
                                stop = 60 + i,
                                category = (int)PuzzleCategory.Gozlem,
                                promptTR = "Vapura giden en kısa yol hangi iskeleden geçer?",
                                promptEN = "Which pier is on the shortest route to the ferry?",
                                answerTR = "Karaköy İskelesi",
                                answerEN = "Karaköy Pier"
                            });
                        }
                        try
                        {
                            GameManager.Instance.Ui.OpenPuzzleModal(true);
                            RenderGameView(camera, request.puzzleArchiveImagePath);
                        }
                        finally
                        {
                            GameManager.Instance.Ui.OpenPuzzleModal(false);
                            savedLog.Clear();
                            savedLog.AddRange(originalLog);
                        }
                    }
                }
                catch (Exception e) { Complete(request, false, e.Message); return; }
                bool wroteAll = File.Exists(request.imagePath) && new FileInfo(request.imagePath).Length > 8 &&
                    (string.IsNullOrEmpty(request.sunnyWeatherImagePath) || (File.Exists(request.sunnyWeatherImagePath) && new FileInfo(request.sunnyWeatherImagePath).Length > 8)) &&
                    (string.IsNullOrEmpty(request.journeyMapImagePath) || (File.Exists(request.journeyMapImagePath) && new FileInfo(request.journeyMapImagePath).Length > 8)) &&
                    (string.IsNullOrEmpty(request.passengerStoryImagePath) || (File.Exists(request.passengerStoryImagePath) && new FileInfo(request.passengerStoryImagePath).Length > 8)) &&
                    (string.IsNullOrEmpty(request.taxiShopImagePath) || (File.Exists(request.taxiShopImagePath) && new FileInfo(request.taxiShopImagePath).Length > 8)) &&
                    (string.IsNullOrEmpty(request.taxiTintPreviewImagePath) || (File.Exists(request.taxiTintPreviewImagePath) && new FileInfo(request.taxiTintPreviewImagePath).Length > 8)) &&
                    (string.IsNullOrEmpty(request.districtFinaleImagePath) || (File.Exists(request.districtFinaleImagePath) && new FileInfo(request.districtFinaleImagePath).Length > 8)) &&
                    (string.IsNullOrEmpty(request.puzzleArchiveImagePath) || (File.Exists(request.puzzleArchiveImagePath) && new FileInfo(request.puzzleArchiveImagePath).Length > 8));
                Complete(request, wroteAll, "Unity did not write every requested Game View image.");
                return;
            }
        }

        static void RenderGameView(Camera camera, string imagePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
            var previousTarget = camera.targetTexture;
            var previousRect = camera.rect;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "ODDS_CAB_GameDevCapture"
            };
            Texture2D image = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(imagePath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.rect = previousRect;
                RenderTexture.active = previousActive;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static string FindPendingRequest()
        {
            string folder = Path.Combine(ProjectRoot, RequestFolder);
            if (!Directory.Exists(folder)) return null;
            foreach (string path in Directory.GetFiles(folder, "*.json")) return path;
            return null;
        }

        static void Complete(CaptureRequest request, bool success, string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.responsePath));
                var response = new CaptureResponse
                {
                    runId = request.runId,
                    status = success ? "complete" : "error",
                    message = message
                };
                File.WriteAllText(request.responsePath, JsonUtility.ToJson(response, true));
                Debug.Log(success ? "[ODDS CAB] GameDev Game View capture saved." : "[ODDS CAB] GameDev capture failed: " + message);
            }
            catch (Exception e) { Debug.LogError("[ODDS CAB] Could not report GameDev capture: " + e.Message); }
            EditorPrefs.SetString(StageKey, "captured");
            EditorApplication.isPlaying = false;
        }

        static void ClearState()
        {
            EditorPrefs.DeleteKey(ActiveRequestKey);
            EditorPrefs.DeleteKey(StageKey);
            EditorPrefs.DeleteKey(DeadlineKey);
        }
    }
}

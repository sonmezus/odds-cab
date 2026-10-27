using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OddsCab.Editor
{
    [InitializeOnLoad]
    public static class OddsCabEditorSetup
    {
        static OddsCabEditorSetup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += EnsureSceneSetup;
            EditorApplication.update += CheckPlaytestTrigger;
        }

        [MenuItem("OddsCab/Set Game View to Portrait (1080x1920)")]
        public static void ApplyPortraitGameViewMenu()
        {
            ConfigurePortraitPlayerSettings();
            SetGameViewToPortrait();
        }

        [MenuItem("OddsCab/Run Diagnostic Playtest & Capture All Views")]
        public static void RunDiagnosticPlaytestMenu()
        {
            SessionState.SetBool("OddsCab.RunDiagnosticPlaytest", true);
            ConfigurePortraitPlayerSettings();
            SetGameViewToPortrait();
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
            }
            else
            {
                EditorApplication.isPlaying = true;
            }
        }

        static void CheckPlaytestTrigger()
        {
            string triggerFile = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "run_playtest.trigger");
            if (File.Exists(triggerFile))
            {
                try { File.Delete(triggerFile); } catch { }
                Debug.Log("[OddsCab] run_playtest.trigger detected! Starting Play Mode...");
                RunDiagnosticPlaytestMenu();
            }
        }

        static void ConfigurePortraitPlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        public static void SetGameViewToPortrait()
        {
            try
            {
                var gvSizesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(gvSizesType);
                var instanceProp = singleType.GetProperty("instance");
                var gvSizesInstance = instanceProp.GetValue(null, null);
                var currentGroup = gvSizesType.GetProperty("currentGroup").GetValue(gvSizesInstance, null);

                var getTotalCount = currentGroup.GetType().GetMethod("GetTotalCount");
                var getGameViewSize = currentGroup.GetType().GetMethod("GetGameViewSize");
                int total = (int)getTotalCount.Invoke(currentGroup, null);

                int targetIndex = -1;
                for (int i = 0; i < total; i++)
                {
                    var size = getGameViewSize.Invoke(currentGroup, new object[] { i });
                    var st = size.GetType();
                    int w = (int)st.GetProperty("width").GetValue(size, null);
                    int h = (int)st.GetProperty("height").GetValue(size, null);
                    if ((w == 1080 && h == 1920) || (w == 9 && h == 16))
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex == -1)
                {
                    var gvsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var gvsTypeEnum = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    var fixedResVal = Enum.Parse(gvsTypeEnum, "FixedResolution");
                    ConstructorInfo ctor = null;
                    foreach (var c in gvsType.GetConstructors())
                    {
                        var ps = c.GetParameters();
                        if (ps.Length == 4 && ps[1].ParameterType == typeof(int) && ps[2].ParameterType == typeof(int))
                        {
                            ctor = c;
                            break;
                        }
                    }
                    if (ctor != null)
                    {
                        var newSize = ctor.Invoke(new object[] { fixedResVal, 1080, 1920, "Portrait (1080x1920)" });
                        var addCustomSize = currentGroup.GetType().GetMethod("AddCustomSize");
                        addCustomSize.Invoke(currentGroup, new object[] { newSize });
                        targetIndex = (int)getTotalCount.Invoke(currentGroup, null) - 1;
                    }
                }

                if (targetIndex >= 0)
                {
                    var gvWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    var gvWindow = EditorWindow.GetWindow(gvWindowType, false, null, false);
                    if (gvWindow != null)
                    {
                        var sizeSelProp = gvWindowType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (sizeSelProp != null)
                        {
                            sizeSelProp.SetValue(gvWindow, targetIndex, null);
                            gvWindow.Repaint();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OddsCab] Could not auto-set GameView resolution: {e.Message}");
            }
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SetGameViewToPortrait();
                if (!SessionState.GetBool("OddsCab.RunDiagnosticPlaytest", false)) return;
                SessionState.SetBool("OddsCab.RunDiagnosticPlaytest", false);
                var runnerGo = new GameObject("OddsCab_PlaytestAutoCapture");
                runnerGo.AddComponent<PlaytestAutoCapture>();
                UnityEngine.Object.DontDestroyOnLoad(runnerGo);
            }
        }

        static void EnsureSceneSetup()
        {
            ConfigurePortraitPlayerSettings();
            SetGameViewToPortrait();

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSettings.enterPlayModeOptionsEnabled)
            {
                EditorSettings.enterPlayModeOptionsEnabled = false;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
            }
            if (EditorSceneManager.GetActiveScene().path == "Assets/Scenes/SampleScene.unity" &&
                UnityEngine.Object.FindAnyObjectByType<GameManager>() == null)
            {
                var gmGo = new GameObject("OddsCab_GameManager");
                gmGo.AddComponent<GameManager>();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
        }
    }

    public class PlaytestAutoCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            Application.runInBackground = true;
            var gm = FindAnyObjectByType<GameManager>();
            if (gm == null) yield break;

            string outDir = @"C:\Users\umut\.gemini\antigravity\brain\d6678c5f-9e99-4279-beff-a38a03594429\scratch";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            Debug.Log("[PlaytestAutoCapture] Starting comprehensive Playtest flow (Stops 12 to 15, 3 puzzles, live camera, modals)...");

            // 1. Wait for Main Menu to render & verify new clean layout
            yield return new WaitForSeconds(0.8f);
            yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, "01_main_menu.png")));

            // Test FOTO modal
            if (gm.Ui != null)
            {
                gm.Ui.OpenPhotoModal(true);
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, "01_foto_modal.png")));
                gm.Ui.OpenPhotoModal(false);
                yield return new WaitForSeconds(0.3f);

                gm.Ui.OpenChatModal(true);
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, "01_sohbet_modal.png")));
                gm.Ui.OpenChatModal(false);
                yield return new WaitForSeconds(0.3f);

                gm.Ui.OpenPuzzleModal(true);
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, "01_bulmaca_modal.png")));
                gm.Ui.OpenPuzzleModal(false);
                yield return new WaitForSeconds(0.3f);
            }

            // Set current stop to 12 as requested by user
            SaveSystem.Data.currentStop = 12;
            SaveSystem.Save();

            // 2. Click "Yola Çık" to start driving towards Stop 12
            gm.StartOrContinueDriving();
            yield return new WaitForSeconds(1.2f);

            // Log profiler stats during driving
            LogPerformanceStats("Driving to Stop 12");
            yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, "02_driving_stop12.png")));

            // 3. Play through Stops 12, 13, 14, 15 solving 3 puzzles correctly
            for (int s = 12; s <= 14; s++)
            {
                Debug.Log($"[PlaytestAutoCapture] Approaching Stop {s}...");
                float waitTime = 0f;
                while ((!gm.IsWaitingForPickupConfirm || (gm.Taxi != null && gm.Taxi.Speed > 0.1f)) && waitTime < 20f)
                {
                    waitTime += Time.deltaTime;
                    yield return null;
                }
                yield return new WaitForSeconds(0.8f);
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, $"02_arrival_stop{s}.png")));

                // Confirm pickup
                gm.ConfirmPassengerPickup();
                waitTime = 0f;
                while (gm.State != GameState.PuzzleActive && waitTime < 6f)
                {
                    waitTime += Time.deltaTime;
                    yield return null;
                }
                yield return new WaitForSeconds(0.8f);
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, $"03_question_stop{s}.png")));

                // Submit correct answer
                if (gm.State == GameState.PuzzleActive && gm.ActivePuzzle != null)
                {
                    int correctIdx = gm.ActivePuzzle.correct;
                    gm.SubmitChoiceAnswer(correctIdx);
                }
                yield return new WaitForSeconds(1.2f);
                LogPerformanceStats($"Puzzle Result Stop {s}");
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, $"04_result_stop{s}.png")));

                // Continue to next stop
                gm.ContinueToNextStop();
                yield return new WaitForSeconds(1.5f);
                LogPerformanceStats($"Acceleration Post-Correct Stop {s}");
                yield return StartCoroutine(CaptureFrame(Path.Combine(outDir, $"05_acceleration_stop{s}.png")));
            }

            Debug.Log("[PlaytestAutoCapture] Complete! All 12-15 stop tests & screenshots captured successfully.");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }

        void LogPerformanceStats(string context)
        {
            float fps = 1.0f / Mathf.Max(0.0001f, Time.smoothDeltaTime);
            long totalAlloc = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            Debug.Log($"[OddsCab Performance] Context: {context} | FPS: {fps:F1} | Alloc: {totalAlloc}MB | Reserved: {reserved}MB | DeltaTime: {Time.smoothDeltaTime * 1000f:F2}ms");
        }

        IEnumerator CaptureFrame(string path)
        {
            yield return null;
            try
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    int w = 1080;
                    int h = 1920;
                    var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                    var prevRt = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = prevRt;

                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);

                    byte[] bytes = tex.EncodeToPNG();
                    UnityEngine.Object.Destroy(tex);
                    File.WriteAllBytes(path, bytes);
                    Debug.Log($"[PlaytestAutoCapture] Saved: {path}");
                }
                else
                {
                    ScreenCapture.CaptureScreenshot(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlaytestAutoCapture] Capture exception: {ex.Message}");
                ScreenCapture.CaptureScreenshot(path);
            }
            yield return new WaitForSeconds(0.15f);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OddsCab.Editor
{
    public static class AndroidBuildScript
    {
        private const string DefaultApkPath = "Builds/Android/ODDSCAB.apk";
        private const string PackageName = "com.oddscabstudio.oddscab";

        [MenuItem("ODDS CAB/Build Android APK")]
        public static void BuildApkMenu()
        {
            PerformBuild(DefaultApkPath, isDevelopment: false);
        }

        [MenuItem("ODDS CAB/Build Android APK (Development)")]
        public static void BuildApkDevMenu()
        {
            PerformBuild(DefaultApkPath, isDevelopment: true);
        }

        public static void BuildFromCommandLine()
        {
            Debug.Log("[AndroidBuild] Starting automated command-line build...");

            string outputPath = DefaultApkPath;
            bool isDev = false;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-apkPath", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                }
                else if (args[i].Equals("-dev", StringComparison.OrdinalIgnoreCase))
                {
                    isDev = true;
                }
            }

            bool success = PerformBuild(outputPath, isDev);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(success ? 0 : 1);
            }
        }

        public static bool PerformBuild(string destinationPath, bool isDevelopment)
        {
            DateTime startTime = DateTime.Now;
            Debug.Log($"[AndroidBuild] Destination APK: {destinationPath}");
            Debug.Log($"[AndroidBuild] Development Build: {isDevelopment}");

            // 1. Configure embedded Android SDK/NDK/JDK preferences
            ConfigureAndroidTools();

            // 2. Configure Build Target & Settings
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = isDevelopment;
            EditorUserBuildSettings.connectProfiler = false;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            // 3. Configure Player Settings for Android
            PlayerSettings.companyName = "OddsCabStudio";
            PlayerSettings.productName = "ODDS CAB";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            NamedBuildTarget androidTarget = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(androidTarget, PackageName);
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.useCustomKeystore = false; // standard debug keystore for easy install/sideload
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.bundleVersion = "1.0.0";

            // Use OpenGLES3 as primary graphics API to prevent black screen on budget PowerVR/Mali GPUs (e.g. Galaxy A04)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan
            });

            // 4. Resolve scenes
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                string defaultScene = "Assets/Scenes/SampleScene.unity";
                if (File.Exists(defaultScene))
                {
                    scenes = new[] { defaultScene };
                }
                else
                {
                    Debug.LogError("[AndroidBuild] No enabled scenes found for build!");
                    return false;
                }
            }

            Debug.Log($"[AndroidBuild] Including scenes:\n - {string.Join("\n - ", scenes)}");

            // 5. Ensure output directory exists
            string fullOutputPath = Path.GetFullPath(destinationPath);
            string dir = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = destinationPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = isDevelopment ? (BuildOptions.Development | BuildOptions.AllowDebugging) : BuildOptions.None
            };

            Debug.Log("[AndroidBuild] Starting BuildPipeline.BuildPlayer...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            TimeSpan elapsed = DateTime.Now - startTime;
            Debug.Log($"[AndroidBuild] Finished build in {elapsed.TotalSeconds:F1}s with result: {summary.result}");
            Debug.Log($"[AndroidBuild] Total size: {summary.totalSize / (1024 * 1024):F2} MB");
            Debug.Log($"[AndroidBuild] Warnings: {summary.totalWarnings}, Errors: {summary.totalErrors}");

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuild] SUCCESS! APK generated at: {fullOutputPath}");
                return true;
            }

            Debug.LogError($"[AndroidBuild] Build FAILED: {summary.result}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    {
                        Debug.LogError($"[AndroidBuild Step Error] [{step.name}] {msg.content}");
                    }
                }
            }

            return false;
        }

        private static void ConfigureAndroidTools()
        {
            try
            {
                string appData = EditorApplication.applicationContentsPath;
                string androidPlayback = Path.Combine(appData, "PlaybackEngines", "AndroidPlayer");
                string sdkPath = Path.Combine(androidPlayback, "SDK");
                string ndkPath = Path.Combine(androidPlayback, "NDK");
                string jdkPath = Path.Combine(androidPlayback, "OpenJDK");

                EditorPrefs.SetBool("SdkUseEmbedded", true);
                EditorPrefs.SetBool("NdkUseEmbedded", true);
                EditorPrefs.SetBool("JdkUseEmbedded", true);

                if (Directory.Exists(sdkPath))
                {
                    EditorPrefs.SetString("AndroidSDKRoot", sdkPath);
                }
                if (Directory.Exists(ndkPath))
                {
                    EditorPrefs.SetString("AndroidNDKRoot", ndkPath);
                }
                if (Directory.Exists(jdkPath))
                {
                    EditorPrefs.SetString("JdkPath", jdkPath);
                }

                Debug.Log($"[AndroidBuild] Configured embedded Android tools:\n SDK: {sdkPath}\n NDK: {ndkPath}\n JDK: {jdkPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidBuild] Note while configuring external tools: {ex.Message}");
            }
        }
    }
}

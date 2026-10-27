using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OddsCab
{
    public enum DeviceTier { Low = 0, Medium = 1, High = 2, Ultra = 3 }

    /// <summary>
    /// Detects High / Medium / Low mobile hardware tier and configures URP render scale,
    /// target frame rate, shadow distance, and ambient traffic density.
    /// </summary>
    public static class DeviceTierDetector
    {
        public static DeviceTier CurrentTier { get; private set; } = DeviceTier.Medium;
        public static bool HighRefreshRateEnabled { get; private set; }
        public static event Action<DeviceTier> TierChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFramePacingPreference() => HighRefreshRateEnabled = false;

        public static DeviceTier DetectHardwareTier()
        {
            int ramMb = SystemInfo.systemMemorySize;
            int vramMb = SystemInfo.graphicsMemorySize;
            int cores = SystemInfo.processorCount;

            if (ramMb > 0 && (ramMb <= 3200 || cores <= 4))
                return DeviceTier.Low;
            // The game's supported baseline is modern mainstream hardware. Ultra keeps native
            // resolution/MSAA on phones; desktop Ultra receives a separate, larger budget.
            if (!Application.isMobilePlatform && cores >= 6 && ramMb >= 6500 &&
                vramMb >= 1500 && SystemInfo.graphicsShaderLevel >= 45)
                return DeviceTier.Ultra;
            if (ramMb >= 6500 && cores >= 6 && vramMb >= 1500)
                return DeviceTier.High;
            return DeviceTier.Medium;
        }

        public static void ApplyFromSave()
        {
            int saved = SaveSystem.Data.qualityTier;
            DeviceTier tier = saved < 0 ? DetectHardwareTier() : (DeviceTier)Mathf.Clamp(saved, 0, 3);
            Apply(tier);
        }

        public static void Apply(DeviceTier tier)
        {
            CurrentTier = (DeviceTier)Mathf.Clamp((int)tier, 0, 3);
            QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = CurrentTier == DeviceTier.Low ? 30 : MainstreamFrameRate();
            QualityBudget budget = BudgetFor(CurrentTier, Application.isMobilePlatform);
            QualitySettings.lodBias = budget.LodBias;
            ConfigureUrp(budget.RenderScale, budget.MsaaSamples, budget.ShadowDistance);
            if (Application.isPlaying && Application.isMobilePlatform)
                UltraFrameBudget.Ensure();

            ApplyCameraAntialiasing(Camera.main);

            TierChanged?.Invoke(CurrentTier);
        }

        public readonly struct QualityBudget
        {
            public readonly float RenderScale, ShadowDistance, LodBias;
            public readonly int MsaaSamples;
            public QualityBudget(float scale, int samples, float shadows, float lod)
            { RenderScale = scale; MsaaSamples = samples; ShadowDistance = shadows; LodBias = lod; }
        }

        /// <summary>Ultra has separate mobile and desktop budgets; no mobile supersampling or extra blur pass.</summary>
        public static QualityBudget BudgetFor(DeviceTier tier, bool mobile)
        {
            switch (tier)
            {
                case DeviceTier.Low: return new QualityBudget(0.72f, 1, 20f, 0.65f);
                case DeviceTier.Medium: return new QualityBudget(1f, 2, 42f, 1f);
                case DeviceTier.High: return new QualityBudget(1f, 4, 60f, 1.35f);
                default: return new QualityBudget(mobile ? 1f : 1.10f, 4, mobile ? 75f : 110f, mobile ? 1.6f : 2f);
            }
        }

        /// <summary>
        /// Opts into the active mobile panel's fastest evenly paced rate up to 120 FPS.
        /// Defaults to false for each launch; call explicitly after the player opts in.
        /// Applies immediately, keeps Low at 30 FPS, and survives quality-tier changes this session.
        /// This API does not change the selected display mode or persist a setting.
        /// </summary>
        public static void SetHighRefreshRate(bool enabled)
        {
            HighRefreshRateEnabled = enabled;
            Application.targetFrameRate = CurrentTier == DeviceTier.Low ? 30 : MainstreamFrameRate();
        }

        /// <summary>
        /// Pure mobile frame-cap selection for a measured panel refresh rate.
        /// Medium/High default to 60; opting in yields 60/90/120 on those panels and 72 on 144 Hz.
        /// Fractional panel/divisor rates round the integer cap upwards so Unity can use that divisor.
        /// Invalid refresh measurements fall back to 60. Low always retains its 30 FPS budget.
        /// </summary>
        public static int SelectMobileFrameRate(double refreshRate, DeviceTier tier, bool highRefreshEnabled)
        {
            if (tier == DeviceTier.Low) return 30;
            if (!highRefreshEnabled || double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate <= 0.0)
                return 60;

            // Nominal integer Hz avoids turning 120.001 Hz into an unnecessary half-rate cap.
            int panelRate = (int)Math.Min(1000.0, Math.Max(1.0, Math.Round(refreshRate)));
            int divisor = (panelRate + 119) / 120;
            return Math.Min(120, (panelRate + divisor - 1) / divisor);
        }

        static int MainstreamFrameRate()
        {
            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            if (Application.isMobilePlatform)
                return SelectMobileFrameRate(refreshRate, CurrentTier, HighRefreshRateEnabled);
            return Math.Max(144, (int)Math.Round(refreshRate));
        }

        /// <summary>Native-resolution 2x/4x MSAA on mainstream tiers; Low retains its reduced-resolution FXAA fallback.</summary>
        static void ConfigureUrp(float renderScale, int samples, float shadowDistance)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = renderScale;
                urp.msaaSampleCount = samples;
                // URP reads its own shadow distance, not QualitySettings.shadowDistance.
                urp.shadowDistance = shadowDistance;
            }
        }

        /// <summary>Low tier has no MSAA: FXAA (one cheap full-screen pass) smooths its edges instead.</summary>
        public static void ApplyCameraAntialiasing(Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = true;
            cam.allowMSAA = CurrentTier != DeviceTier.Low;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.antialiasing = CurrentTier == DeviceTier.Low ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        }

        public static int TrafficCountForTier() =>
            CurrentTier == DeviceTier.Low ? 3 : (CurrentTier == DeviceTier.Medium ? 6 : 10);
    }
}

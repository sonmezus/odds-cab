using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OddsCab
{
    /// <summary>
    /// Mobile Ultra retains detail and 4x MSAA. Sustained frame pressure reduces pixel and shadow
    /// work in small, infrequent steps; stable pacing restores it. This is a measured pacing heuristic,
    /// not a thermal sensor or a replacement for native-device profiling.
    /// </summary>
    public sealed class UltraFrameBudget : MonoBehaviour
    {
        static UltraFrameBudget s_instance;
        float _elapsed, _totalFrameTime, _pressureSeconds, _stableSeconds, _warmup;
        int _frames;
        DeviceTier _lastTier = (DeviceTier)(-1);
        public float ObservedFrameMilliseconds { get; private set; }
        public float CurrentRenderScale { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        public static void Ensure()
        {
            if (s_instance != null) { s_instance._lastTier = (DeviceTier)(-1); return; }
            s_instance = FindAnyObjectByType<UltraFrameBudget>();
            if (s_instance != null) { s_instance._lastTier = (DeviceTier)(-1); return; }
            var root = new GameObject("UltraFrameBudget");
            s_instance = root.AddComponent<UltraFrameBudget>();
            DontDestroyOnLoad(root);
        }

        void Update()
        {
            DeviceTier tier = DeviceTierDetector.CurrentTier;
            if (_lastTier != tier)
            {
                _lastTier = tier;
                _warmup = 5f;
                _elapsed = _totalFrameTime = _pressureSeconds = _stableSeconds = 0f;
                _frames = 0;
                CurrentRenderScale = DeviceTierDetector.BudgetFor(tier, true).RenderScale;
            }
            if (!Application.isMobilePlatform || tier != DeviceTier.Ultra) return;
            var game = GameManager.Instance;
            if (game == null || game.State != GameState.DrivingToStop || Time.timeScale <= 0f)
            { _warmup = 2f; return; }
            float dt = Time.unscaledDeltaTime;
            if (_warmup > 0f) { _warmup -= dt; return; }
            // Ignore a suspend, editor hitch or explicit loading frame rather than degrading on it.
            if (dt <= 0f || dt > 0.1f) return;
            _totalFrameTime += dt;
            _elapsed += dt;
            _frames++;
            if (_elapsed < 1f) return;
            ObservedFrameMilliseconds = _totalFrameTime * 1000f / Mathf.Max(1, _frames);
            bool pressured = ObservedFrameMilliseconds > 20f;
            _pressureSeconds = pressured ? _pressureSeconds + _elapsed : 0f;
            _stableSeconds = ObservedFrameMilliseconds < 17.6f ? _stableSeconds + _elapsed : 0f;
            float next = CurrentRenderScale;
            if (_pressureSeconds >= 3f) { next = Mathf.Max(0.88f, next - 0.04f); _pressureSeconds = 0f; }
            else if (_stableSeconds >= 8f) { next = Mathf.Min(1f, next + 0.02f); _stableSeconds = 0f; }
            if (!Mathf.Approximately(next, CurrentRenderScale) && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                CurrentRenderScale = next;
                urp.renderScale = next;
                urp.shadowDistance = Mathf.Lerp(48f, 75f, Mathf.InverseLerp(0.88f, 1f, next));
            }
            _elapsed = _totalFrameTime = 0f;
            _frames = 0;
        }
    }
}

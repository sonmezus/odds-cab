using UnityEngine;

namespace OddsCab
{
    public readonly struct SeaWaveState
    {
        public readonly float amplitudeA, amplitudeB, speed, foam;
        public SeaWaveState(float a, float b, float velocity, float crests)
        { amplitudeA = a; amplitudeB = b; speed = velocity; foam = crests; }
    }

    /// <summary>One bounded sea/wave contract for the GPU, boats, fishing lines and wildlife.</summary>
    public sealed class BosphorusSeaState : MonoBehaviour
    {
        public const float CoastalLevel = -.27f, BridgeLevel = -2.75f, MaximumDisplacement = .37f;
        public static BosphorusSeaState Instance { get; private set; }
        public static float MeanLevel => CityChunkStreamer.CurrentRoad == TourRoadType.Bridge ? BridgeLevel : CoastalLevel;
        public SeaWaveState Current { get; private set; }
        static readonly int WaveA = Shader.PropertyToID("_WaveA"), WaveB = Shader.PropertyToID("_WaveB"), Speed = Shader.PropertyToID("_WaveSpeed"), Foam = Shader.PropertyToID("_FoamStrength");
        Material _water;

        public static SeaWaveState ForWeather(WeatherPreset weather)
        {
            switch (weather)
            {
                case WeatherPreset.Night: return new SeaWaveState(.032f, .018f, .33f, .07f);
                case WeatherPreset.FoggyMorning: return new SeaWaveState(.048f, .028f, .40f, .10f);
                case WeatherPreset.GoldenSunset: return new SeaWaveState(.075f, .045f, .56f, .13f);
                case WeatherPreset.Rain: return new SeaWaveState(.155f, .085f, 1.04f, .30f);
                default: return new SeaWaveState(.115f, .065f, .78f, .19f);
            }
        }

        public void Initialize(Material water)
        {
            Instance = this; _water = water;
            Current = ForWeather(WeatherDirector.Instance != null ? WeatherDirector.Instance.CurrentWeather : WeatherPreset.Sunny);
            Apply();
        }

        void Update()
        {
            if (_water == null) return;
            var target = ForWeather(WeatherDirector.Instance != null ? WeatherDirector.Instance.CurrentWeather : WeatherPreset.Sunny);
            float t = 1f - Mathf.Exp(-Time.deltaTime * .85f);
            var state = Current;
            Current = new SeaWaveState(Mathf.Lerp(state.amplitudeA, target.amplitudeA, t), Mathf.Lerp(state.amplitudeB, target.amplitudeB, t),
                Mathf.Lerp(state.speed, target.speed, t), Mathf.Lerp(state.foam, target.foam, t));
            Apply();
        }

        void Apply()
        {
            _water.SetVector(WaveA, new Vector4(1f, .35f, Current.amplitudeA, 18f));
            _water.SetVector(WaveB, new Vector4(-.4f, 1f, Current.amplitudeB, 12f));
            _water.SetFloat(Speed, Current.speed); _water.SetFloat(Foam, Current.foam);
        }

        public static float SampleHeight(Vector3 position, float clock)
        {
            var state = Instance != null ? Instance.Current : ForWeather(WeatherPreset.Sunny);
            return MeanLevel + Wave(position, clock, state.amplitudeA, 18f, new Vector2(1f, .35f), state.speed)
                + Wave(position, clock, state.amplitudeB, 12f, new Vector2(-.4f, 1f), state.speed);
        }

        static float Wave(Vector3 position, float clock, float amplitude, float wavelength, Vector2 direction, float speed)
        {
            direction.Normalize(); float k = Mathf.PI * 2f / wavelength;
            return amplitude * Mathf.Sin(k * (position.x * direction.x + position.z * direction.y - Mathf.Sqrt(9.81f / k) * speed * clock));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Instance = null;
    }
}


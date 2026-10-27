using UnityEngine;

namespace OddsCab
{
    public readonly struct IstanbulSoundscapeLevels
    {
        public readonly float Crowd, Market, Cafe, Sea, Harbor;
        public IstanbulSoundscapeLevels(float crowd, float market, float cafe, float sea, float harbor)
        { Crowd = crowd; Market = market; Cafe = cafe; Sea = sea; Harbor = harbor; }
    }

    /// <summary>Allocation-free district mix policy. Night always yields to water and distant city air.</summary>
    public static class IstanbulSoundscape
    {
        static readonly IstanbulSoundscapeLevels[] Districts =
        {
            new IstanbulSoundscapeLevels(.42f, .04f, .30f, 1f, .70f),
            new IstanbulSoundscapeLevels(1f, 1f, .64f, .30f, .25f),
            new IstanbulSoundscapeLevels(.54f, .12f, .16f, 1f, 1f),
            new IstanbulSoundscapeLevels(.76f, .18f, 1f, .90f, .80f),
            new IstanbulSoundscapeLevels(.65f, .12f, .82f, .10f, .12f),
            new IstanbulSoundscapeLevels(.43f, .08f, .90f, .08f, .08f),
            new IstanbulSoundscapeLevels(.22f, .03f, .28f, .80f, .72f),
            new IstanbulSoundscapeLevels(.88f, .52f, 1f, .95f, .82f)
        };

        public static IstanbulSoundscapeLevels Evaluate(int district, bool nearWater, WeatherPreset weather,
            GameState state = GameState.DrivingToStop)
        {
            var scene = Districts[Mathf.Clamp(district, 0, Districts.Length - 1)];
            // Diorama puzzles use an exterior camera too, so cabin muffling alone cannot
            // keep the waterfront from competing with questions and musical clues.
            bool puzzleFocus = state == GameState.PuzzleActive || state == GameState.PuzzleResult;
            float sea = !puzzleFocus && nearWater ? scene.Sea : 0f;
            float harbor = puzzleFocus ? 0f : nearWater ? scene.Harbor :
                weather == WeatherPreset.Night ? .12f : .05f;
            if (weather == WeatherPreset.Night)
                return new IstanbulSoundscapeLevels(0f, 0f, scene.Cafe * .025f,
                    sea, harbor);
            float crowdWeather = weather == WeatherPreset.Rain ? .28f :
                weather == WeatherPreset.FoggyMorning ? .38f : 1f;
            return new IstanbulSoundscapeLevels(scene.Crowd * crowdWeather, scene.Market * crowdWeather,
                scene.Cafe * (weather == WeatherPreset.Rain ? .52f : .85f),
                sea, harbor);
        }

        public static float ExteriorLowpass(WeatherPreset weather, float cabinAmount)
        {
            float outside = weather == WeatherPreset.FoggyMorning ? 4600f :
                weather == WeatherPreset.Rain ? 10500f : 16500f;
            return Mathf.Lerp(outside, 1900f, Mathf.Clamp01(cabinAmount));
        }
    }
}

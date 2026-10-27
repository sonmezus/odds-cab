using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>Small weather-matched HDR reflection environments for painted metal, glass and wet stone.</summary>
    public static class ReferenceEnvironmentLighting
    {
        static readonly Cubemap[] Maps = new Cubemap[5];

        public static void Apply(WeatherPreset weather)
        {
            int index = Mathf.Clamp((int)weather, 0, Maps.Length - 1);
            if (Maps[index] == null) Maps[index] = Build(weather);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = Maps[index];
            RenderSettings.reflectionIntensity = weather == WeatherPreset.Night ? 0.72f : 0.85f;
        }

        static Cubemap Build(WeatherPreset weather)
        {
            const int size = 32;
            bool night = weather == WeatherPreset.Night;
            bool sunset = weather == WeatherPreset.GoldenSunset;
            bool overcast = weather == WeatherPreset.Rain || weather == WeatherPreset.FoggyMorning;
            Color sky = night ? new Color(0.055f, 0.085f, 0.17f) :
                overcast ? new Color(0.30f, 0.39f, 0.48f) : new Color(0.30f, 0.51f, 0.75f);
            Color horizon = night ? new Color(0.12f, 0.13f, 0.18f) :
                sunset ? new Color(0.97f, 0.76f, 0.50f) : new Color(0.80f, 0.84f, 0.86f);
            Color ground = night ? new Color(0.035f, 0.04f, 0.055f) : new Color(0.20f, 0.18f, 0.14f);
            Vector3 sunDirection = new Vector3(-0.65f, sunset ? 0.35f : 0.65f, -0.7f).normalized;
            Color sunlight = night ? new Color(0.3f, 0.38f, 0.55f) :
                sunset ? new Color(3.2f, 2.2f, 1.2f) : new Color(3.8f, 3.5f, 2.8f);
            var map = new Cubemap(size, TextureFormat.RGBAHalf, true)
            {
                name = "ReferenceReflections_" + weather,
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Trilinear
            };
            var pixels = new Color[size * size];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = ((x + 0.5f) / size) * 2f - 1f;
                        float v = ((y + 0.5f) / size) * 2f - 1f;
                        Vector3 direction = FaceDirection(face, u, v).normalized;
                        float horizonBand = Mathf.Exp(-Mathf.Abs(direction.y) * 4.2f);
                        Color color = direction.y >= 0f
                            ? Color.Lerp(sky, horizon, horizonBand)
                            : Color.Lerp(ground, horizon * 0.55f, horizonBand);
                        float sun = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(direction, sunDirection)), 160f);
                        if (overcast) sun *= 0.12f;
                        color += sunlight * sun;
                        color.a = 1f;
                        pixels[y * size + x] = color;
                    }
                map.SetPixels(pixels, (CubemapFace)face);
            }
            map.Apply(true, true);
            return map;
        }

        static Vector3 FaceDirection(int face, float u, float v)
        {
            switch (face)
            {
                case 0: return new Vector3(1f, -v, -u);
                case 1: return new Vector3(-1f, -v, u);
                case 2: return new Vector3(u, 1f, v);
                case 3: return new Vector3(u, -1f, -v);
                case 4: return new Vector3(u, -v, 1f);
                default: return new Vector3(-u, -v, -1f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            for (int i = 0; i < Maps.Length; i++)
            {
                if (Maps[i] != null) Object.Destroy(Maps[i]);
                Maps[i] = null;
            }
        }
    }
}

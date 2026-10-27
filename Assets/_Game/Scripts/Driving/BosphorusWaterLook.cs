using UnityEngine;

namespace OddsCab
{
    /// <summary>Shared, seam-free ripple normals and per-weather Bosphorus colour. Generated once at startup.</summary>
    public static class BosphorusWaterLook
    {
        static Texture2D s_normal;
        static readonly int NormalId = Shader.PropertyToID("_NormalMap");
        static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        static readonly int NearId = Shader.PropertyToID("_NearColor");
        static readonly int DeepId = Shader.PropertyToID("_DeepColor");
        static readonly int SkyId = Shader.PropertyToID("_SkyColor");
        static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        static readonly int GlitterId = Shader.PropertyToID("_SunReflection");
        static readonly int GlossId = Shader.PropertyToID("_GlossPower");
        static readonly int RipplesId = Shader.PropertyToID("_RippleStrength");
        static readonly int DeepDistanceId = Shader.PropertyToID("_DeepDistance");

        public static void Apply(Material material, WeatherPreset weather)
        {
            if (material == null || !material.HasProperty(NormalId)) return;
            if (s_normal == null) s_normal = BuildRippleNormal();
            material.SetTexture(NormalId, s_normal);
            material.SetFloat(NormalScaleId, 6.8f);
            material.SetFloat(GlossId, 220f);
            material.SetFloat(GlitterId, weather == WeatherPreset.GoldenSunset ? 2.0f
                : weather == WeatherPreset.Night ? 0.55f
                : weather == WeatherPreset.FoggyMorning ? 0.7f : 1.65f);
            material.SetFloat(RipplesId, weather == WeatherPreset.Rain ? 1.25f : .85f);
            material.SetFloat(DeepDistanceId, 280f);
            var state = BosphorusSeaState.ForWeather(weather);
            material.SetVector("_WaveA", new Vector4(1f, .35f, state.amplitudeA, 18f));
            material.SetVector("_WaveB", new Vector4(-.4f, 1f, state.amplitudeB, 12f));
            material.SetFloat("_WaveSpeed", state.speed);
            material.SetFloat("_FoamStrength", state.foam);
            switch (weather)
            {
                case WeatherPreset.Night:
                    material.SetColor(NearId, new Color(0.018f, 0.13f, 0.20f));
                    material.SetColor(DeepId, new Color(0.012f, 0.055f, 0.12f));
                    material.SetColor(SkyId, new Color(0.08f, 0.17f, 0.30f));
                    material.SetColor(HorizonId, new Color(0.12f, 0.19f, 0.28f));
                    break;
                case WeatherPreset.Rain:
                case WeatherPreset.FoggyMorning:
                    material.SetColor(NearId, new Color(0.075f, 0.30f, 0.37f));
                    material.SetColor(DeepId, new Color(0.025f, 0.16f, 0.25f));
                    material.SetColor(SkyId, new Color(0.22f, 0.38f, 0.46f));
                    material.SetColor(HorizonId, new Color(0.38f, 0.49f, 0.54f));
                    break;
                case WeatherPreset.GoldenSunset:
                    material.SetColor(NearId, new Color(0.065f, 0.38f, 0.39f));
                    material.SetColor(DeepId, new Color(0.045f, 0.22f, 0.31f));
                    material.SetColor(SkyId, new Color(0.22f, 0.40f, 0.53f));
                    material.SetColor(HorizonId, new Color(0.64f, 0.57f, 0.43f));
                    break;
                default:
                    material.SetColor(NearId, new Color(0.055f, 0.42f, 0.45f));
                    material.SetColor(DeepId, new Color(0.035f, 0.25f, 0.36f));
                    material.SetColor(SkyId, new Color(0.20f, 0.43f, 0.58f));
                    material.SetColor(HorizonId, new Color(0.50f, 0.64f, 0.66f));
                    break;
            }
        }

        static Texture2D BuildRippleNormal()
        {
            const int size = 128;
            var heights = new float[size * size];
            var pixels = new Color32[heights.Length];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    heights[y * size + x] = TileNoise(u, v, 4f, 3f, 11.7f) * 0.58f
                        + TileNoise(u, v, 9f, 7f, 32.1f) * 0.28f
                        + TileNoise(u, v, 21f, 17f, 71.4f) * 0.14f;
                }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size]) * 9f;
                    float dz = (heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x]) * 9f;
                    Vector3 normal = new Vector3(-dx, -dz, 1f).normalized;
                    pixels[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f,
                        normal.z, 1f);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "BosphorusRippleNormals_Runtime",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static float TileNoise(float u, float v, float fx, float fy, float seed)
        {
            float x = u * fx, y = v * fy;
            float sx = u * u * (3f - 2f * u), sy = v * v * (3f - 2f * v);
            float a = Mathf.Lerp(Mathf.PerlinNoise(x + seed, y + seed),
                Mathf.PerlinNoise(x - fx + seed, y + seed), sx);
            float b = Mathf.Lerp(Mathf.PerlinNoise(x + seed, y - fy + seed),
                Mathf.PerlinNoise(x - fx + seed, y - fy + seed), sx);
            return Mathf.Lerp(a, b, sy);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            if (s_normal != null) Object.Destroy(s_normal);
            s_normal = null;
        }
    }
}

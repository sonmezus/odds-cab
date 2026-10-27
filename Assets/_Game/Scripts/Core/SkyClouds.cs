using UnityEngine;

namespace OddsCab
{
    /// <summary>
    /// Soft billowing cloud masses on the vista layer, with separate sunlit-top and sky-bounce colours per weather.
    /// Bounded slow drift follows the taxi without visible wraps; clear-weather clouds remain ivory rather than orange strips.
    /// </summary>
    public class SkyClouds : MonoBehaviour
    {
        struct Cloud { public Transform t; public Vector3 basePos; public float drift; public Material mat; }

        Cloud[] _clouds;
        Texture2D[] _textures;
        Transform _follow;
        static readonly int TopId = Shader.PropertyToID("_TopColor");
        static readonly int BottomId = Shader.PropertyToID("_BottomColor");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        public static SkyClouds Create(Transform parent)
        {
            var go = new GameObject("SunsetClouds");
            go.transform.SetParent(parent, false);
            var sc = go.AddComponent<SkyClouds>();
            sc.Build();
            return sc;
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("Shaders/ReferenceCloud") ?? Shader.Find("OddsCab/Reference Cloud");
            if (shader == null) { enabled = false; return; }
            var referenceCloud = Resources.Load<Texture2D>("ReferenceKit/Textures/ReferenceCumulus");
            if (referenceCloud == null)
            {
                _textures = new Texture2D[3];
                for (int i = 0; i < _textures.Length; i++) _textures[i] = MakeCloudTexture(11 + i * 7);
            }
            // Layered banks at different elevations retain open sky and complement the high cirrus veil.
            Vector4[] spec =
            {
                new Vector4(-160f, 107f, 540f, 110f), new Vector4(-18f, 148f, 610f, 62f),
                new Vector4(92f, 86f, 550f, 146f), new Vector4(260f, 116f, 570f, 88f),
                new Vector4(-220f, 103f, -530f, 126f), new Vector4(20f, 87f, -560f, 102f),
                new Vector4(245f, 128f, -540f, 92f), new Vector4(130f, 167f, -640f, 59f),
                new Vector4(-560f, 84f, -110f, 132f), new Vector4(570f, 109f, 140f, 118f),
            };
            _clouds = new Cloud[spec.Length];
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var quad = temporary.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temporary);
            for (int i = 0; i < spec.Length; i++)
            {
                var go = new GameObject("Cloud_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = HorizonCameraStack.VistaLayer;
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                Texture2D texture = referenceCloud != null ? referenceCloud : _textures[i % _textures.Length];
                var mat = new Material(shader) { mainTexture = texture, renderQueue = 2990 - i };
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                float w = spec[i].w, h = w * ((i % 3) == 0 ? 0.31f : (i % 3) == 1 ? 0.48f : 0.36f);
                go.transform.localScale = new Vector3((i & 1) == 0 ? w : -w, h, 1f);
                float yaw = Mathf.Atan2(spec[i].x, spec[i].z) * Mathf.Rad2Deg;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, (i % 3 - 1) * 1.2f);
                _clouds[i] = new Cloud { t = go.transform, basePos = new Vector3(spec[i].x, spec[i].y, spec[i].z), drift = i * 1.37f, mat = mat };
            }
        }

        public void SetWeather(WeatherPreset preset)
        {
            if (_clouds == null) return;
            gameObject.SetActive(preset != WeatherPreset.FoggyMorning);
            Color top = preset switch
            {
                WeatherPreset.GoldenSunset => new Color(1.00f, 0.94f, 0.85f),
                WeatherPreset.Rain => new Color(0.61f, 0.67f, 0.73f),
                WeatherPreset.Night => new Color(0.20f, 0.25f, 0.37f),
                _ => new Color(1f, 0.99f, 0.96f),
            };
            Color bottom = preset switch
            {
                WeatherPreset.GoldenSunset => new Color(0.64f, 0.71f, 0.80f),
                WeatherPreset.Rain => new Color(0.28f, 0.35f, 0.43f),
                WeatherPreset.Night => new Color(0.07f, 0.10f, 0.18f),
                _ => new Color(0.65f, 0.73f, 0.82f),
            };
            float opacity = preset == WeatherPreset.Night ? 0.40f : preset == WeatherPreset.Rain ? 0.84f : 0.92f;
            foreach (var cl in _clouds)
            {
                cl.mat.SetColor(TopId, top);
                cl.mat.SetColor(BottomId, bottom);
                cl.mat.SetFloat(OpacityId, opacity);
            }
        }

        void LateUpdate()
        {
            if (_clouds == null) return;
            if (_follow == null)
            {
                var gm = GameManager.Instance;
                if (gm == null || gm.Taxi == null) return;
                _follow = gm.Taxi.transform;
            }
            float z = _follow.position.z, t = Time.time;
            foreach (var cl in _clouds)
            {
                float x = cl.basePos.x + Mathf.Sin(t * 0.005f + cl.drift) * 8f;
                cl.t.position = new Vector3(x, cl.basePos.y, z + cl.basePos.z);
            }
        }

        /// <summary>
        /// Overlapping soft ellipses and fBm form cloud density. Red stores self-shading, alpha stores coverage.
        /// </summary>
        /// <summary>HLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).</summary>
        static float Edge(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        static Texture2D MakeCloudTexture(int seed)
        {
            const int w = 192, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, name = "CloudDensity" + seed, filterMode = FilterMode.Trilinear };
            float ox = seed * 13.7f, oy = seed * 5.3f;
            float Fbm(float x, float y)
            {
                float s = 0f, a = 0.5f, f = 1f;
                for (int o = 0; o < 5; o++) { s += a * Mathf.PerlinNoise(x * f + ox, y * f + oy); f *= 2.1f; a *= 0.5f; }
                return s;
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float inside = 0f;
                    for (int puff = 0; puff < 7; puff++)
                    {
                        float phase = seed + puff * 2.31f;
                        float cx = 0.14f + puff * 0.115f;
                        float cy = 0.39f + Mathf.Sin(phase) * 0.11f;
                        float rx = 0.12f + (Mathf.Cos(phase * 0.7f) * 0.5f + 0.5f) * 0.075f;
                        float ry = 0.18f + (Mathf.Sin(phase * 1.3f) * 0.5f + 0.5f) * 0.13f;
                        float dx = (u - cx) / rx, dy = (v - cy) / ry;
                        inside = Mathf.Max(inside, 1f - Edge(0.45f, 1.25f, dx * dx + dy * dy));
                    }
                    float puffs = Fbm(u * 9f, v * 5f);
                    float a = Mathf.Clamp01(inside * (0.65f + puffs * 0.55f)) * Edge(0f, 0.08f, Mathf.Min(u, 1f - u));
                    var c = new Color(Mathf.Clamp01(v * 1.5f + (puffs - 0.5f) * 0.25f), 0f, 0f, 1f);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply(true, true);
            return tex;
        }

        void OnDestroy()
        {
            if (_clouds != null)
                foreach (var cloud in _clouds) if (cloud.mat != null) Destroy(cloud.mat);
            if (_textures != null)
                foreach (var texture in _textures) if (texture != null) Destroy(texture);
        }
    }
}

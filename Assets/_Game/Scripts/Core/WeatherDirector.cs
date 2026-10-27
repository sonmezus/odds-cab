using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OddsCab
{
    public enum WeatherPreset
    {
        Sunny = 0,
        GoldenSunset = 1,
        FoggyMorning = 2,
        Rain = 3,
        Night = 4
    }

    /// <summary>
    /// Coastal weather with a layered sky, native pooled rain, moist windows, wet roads and URP grading.
    /// The cab glass owns its bounded overlay; ambient lighting keeps the dense fog readable.
    /// </summary>
    public class WeatherDirector : MonoBehaviour
    {
        public static WeatherDirector Instance { get; private set; }

        public WeatherPreset CurrentWeather { get; private set; } = WeatherPreset.Sunny;
        public bool UserOverrodeWeather { get; private set; }

        Light _sun;
        Camera _cam;

        IstanbulRainVolume _rainVolume;

        GameObject _fogRibbonsRoot;
        readonly List<Transform> _fogRibbons = new List<Transform>();

        readonly Sprite[] _filterSprites = new Sprite[5];

        // Weather colour grading through URP post-processing (replaces the old full-screen UI tint image).
        // Mobile budget: colour adjustments + white balance + vignette are folded into URP's single uber/LUT pass;
        // restrained bloom on Medium/High; motion blur from the scene's default volume is switched off.
        Volume _weatherVolume;
        ColorAdjustments _colorAdj;
        WhiteBalance _whiteBalance;
        Vignette _vignette;
        Bloom _bloom;
        readonly VolumeProfile[] _weatherProfiles = new VolumeProfile[5];

        // Literal-colour reference gradient sky for clear weathers;
        // fog and rain keep a solid colour that matches the fog so the horizon blends.
        Material _skyMat;
        Renderer _skyDome;
        public bool UsesReferenceSkyDome => _skyDome != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            _cam = Camera.main;
            _sun = FindDirectionalSun();

            Build3DRainParticles();
            BuildWeatherCloudLayers();
            Build3DFogLayers();
            BuildPostProcessFilterSprites();
            BuildWeatherVolume();
            var skyShader = Resources.Load<Shader>("Shaders/ReferenceSky");
            if (skyShader != null && skyShader.isSupported)
                _skyMat = new Material(skyShader) { name = "ReferenceSky_Runtime" };
            else
            {
                var skyAsset = Resources.Load<Material>("OddsCabSky");
                if (skyAsset != null) _skyMat = new Material(skyAsset);
            }
            if (_skyMat != null && _skyMat.shader.name == "OddsCab/Reference Sky" && _cam != null)
                BuildReferenceSkyDome();
        }

        void BuildReferenceSkyDome()
        {
            // Draw through URP's ordinary background pass. Native skybox rendering does not execute
            // SRPDefaultUnlit passes, and an overlay camera cannot own the stack's skybox clear.
            var dome = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dome.name = "ReferenceSkyDome";
            dome.layer = HorizonCameraStack.VistaLayer;
            dome.transform.SetParent(_cam.transform, false);
            dome.transform.localScale = Vector3.one * 600f;
            var collider = dome.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            _skyDome = dome.GetComponent<Renderer>();
            _skyDome.sharedMaterial = _skyMat;
            _skyDome.shadowCastingMode = ShadowCastingMode.Off;
            _skyDome.receiveShadows = false;
            _skyDome.lightProbeUsage = LightProbeUsage.Off;
            _skyDome.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _skyDome.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        internal static Light FindDirectionalSun()
        {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && sun.isActiveAndEnabled) return sun;

            // This startup-only search cannot select the taxi's cabin, headlights, or street lights.
            var lights = FindObjectsByType<Light>();
            sun = null;
            for (int i = 0; i < lights.Length; i++)
            {
                Light candidate = lights[i];
                if (candidate.type != LightType.Directional || !candidate.isActiveAndEnabled) continue;
                if (sun == null || candidate.intensity > sun.intensity) sun = candidate;
            }
            return sun;
        }

        void ApplySky(bool useSky, Color skyTint, Color ground, float exposure, float atmosphere)
        {
            if (_cam == null) return;
            if (_skyDome != null) _skyDome.enabled = useSky;
            if (!useSky || _skyMat == null)
            {
                _cam.clearFlags = CameraClearFlags.SolidColor;
                return;
            }
            _skyMat.SetColor("_SkyTint", skyTint);
            _skyMat.SetColor("_GroundColor", ground);
            _skyMat.SetFloat("_Exposure", exposure);
            if (_skyMat.HasProperty("_HorizonColor"))
            {
                bool sunset = CurrentWeather == WeatherPreset.GoldenSunset;
                _skyMat.SetColor("_ZenithColor", sunset
                    ? new Color(0.12f, 0.36f, 0.66f) : new Color(0.10f, 0.41f, 0.72f));
                _skyMat.SetColor("_HorizonColor", sunset
                    ? new Color(0.97f, 0.74f, 0.62f) : new Color(0.79f, 0.88f, 0.94f));
                _skyMat.SetFloat("_HorizonFalloff", sunset ? 14f : 18f);
            }
            else _skyMat.SetFloat("_AtmosphereThickness", atmosphere);
            RenderSettings.skybox = _skyMat;
            if (_sun != null) RenderSettings.sun = _sun;
            _cam.clearFlags = _skyDome != null ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        }

        void BuildWeatherVolume()
        {
            if (_cam != null)
            {
                var camData = _cam.GetUniversalAdditionalCameraData();
                if (camData != null) camData.renderPostProcessing = true;
            }

            // Mobile: no motion blur. Touch the scene volume's runtime instance only (never the shared asset).
            foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
            {
                if (v == null || !v.HasInstantiatedProfile() && v.sharedProfile == null) continue;
                var prof = v.profile;
                if (prof != null && prof.TryGet<MotionBlur>(out var mb)) mb.active = false;
            }

            var go = new GameObject("WeatherVolume");
            go.transform.SetParent(transform, false);
            if (_cam != null)
            {
                var camData = _cam.GetUniversalAdditionalCameraData();
                camData.volumeLayerMask |= 1 << go.layer;
            }
            _weatherVolume = go.AddComponent<Volume>();
            _weatherVolume.isGlobal = true;
            _weatherVolume.priority = 10f;
            // Keep a separate runtime profile per preset so every weather owns its grading values
            // instead of mutating one shared profile and carrying settings across transitions.
            for (int i = 0; i < _weatherProfiles.Length; i++)
            {
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "WeatherProfile_" + (WeatherPreset)i + "_Runtime";
                profile.Add<ColorAdjustments>(true);
                profile.Add<WhiteBalance>(true);
                profile.Add<Vignette>(true);
                profile.Add<Bloom>(true);
                // Cool shadows / warm highlights: the colour contrast of the reference posters (blue-violet shade
                // under golden light) instead of one orange wash; part of the same LUT pass, free on mobile
                var smh = profile.Add<ShadowsMidtonesHighlights>(true);
                smh.shadows.Override(new Vector4(0.96f, 1.0f, 1.07f, 0.01f));
                smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
                smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));
                // Neutral tonemapping rolls off the bright sunset sky and lamp highlights instead of clipping
                // them flat; it shares URP's single LUT pass with the colour grading, so it costs nothing extra.
                // (ACES was compared on the sunset stop: darker overall with crushed shadows.) Neutral flattens
                // a little, which SetGrade makes up with extra exposure and saturation.
                var tm = profile.Add<Tonemapping>(true);
                tm.mode.Override(TonemappingMode.Neutral);
                _weatherProfiles[i] = profile;
            }
            BindWeatherProfile(CurrentWeather);
            ApplyTierToVolume();
            DeviceTierDetector.TierChanged += OnTierChanged;
        }

        SkyClouds _skyClouds;

        void BuildWeatherCloudLayers()
        {
            _skyClouds = SkyClouds.Create(transform);
            _skyClouds.SetWeather(WeatherPreset.Sunny);
        }
        void ApplyCloudWeather(WeatherPreset preset)
        {
            if (_skyClouds != null) _skyClouds.SetWeather(preset);
        }

        void BindWeatherProfile(WeatherPreset preset)
        {
            if (_weatherVolume == null || _weatherProfiles[(int)preset] == null) return;
            var profile = _weatherProfiles[(int)preset];
            _weatherVolume.sharedProfile = profile;
            profile.TryGet(out _colorAdj);
            profile.TryGet(out _whiteBalance);
            profile.TryGet(out _vignette);
            profile.TryGet(out _bloom);
        }

        void OnTierChanged(DeviceTier tier) => ApplyTierToVolume();

        void OnDestroy()
        {
            DeviceTierDetector.TierChanged -= OnTierChanged;
            for (int i = 0; i < _weatherProfiles.Length; i++)
                if (_weatherProfiles[i] != null) Destroy(_weatherProfiles[i]);
            if (_skyMat != null) Destroy(_skyMat);
        }

        float _bloomTarget = 0.4f;

        void ApplyTierToVolume()
        {
            if (_bloom == null) return;
            // Intensity 0 makes URP skip the bloom pass entirely (and overrides any scene-volume bloom).
            DeviceTier tier = DeviceTierDetector.CurrentTier;
            float intensity = tier >= DeviceTier.High ? _bloomTarget :
                tier == DeviceTier.Medium ? Mathf.Min(_bloomTarget * 0.5f, 0.35f) : 0f;
            _bloom.intensity.Override(intensity);
            _bloom.highQualityFiltering.Override(tier >= DeviceTier.High);
            _bloom.downscale.Override(tier == DeviceTier.Medium ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
        }

        /// <summary>
        /// Gradient ambient (sky / horizon / ground). The scene defaulted to skybox ambient, which is never
        /// re-baked at runtime, so the per-weather ambient colours had no effect at all.
        /// </summary>
        static void SetAmbient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        void SetGrade(float exposure, float contrast, float saturation, Color filter, float temperature, float tint, float vignette, float bloom)
        {
            if (_colorAdj == null) return;
            // Reference grading is authored directly; hidden exposure/saturation boosts clipped yellow paint.
            _colorAdj.postExposure.Override(exposure);
            _colorAdj.contrast.Override(contrast);
            _colorAdj.saturation.Override(saturation);
            _colorAdj.colorFilter.Override(filter);
            _whiteBalance.temperature.Override(temperature);
            _whiteBalance.tint.Override(tint);
            _vignette.intensity.Override(vignette);
            _vignette.smoothness.Override(0.45f);
            _bloomTarget = bloom;
            _bloom.threshold.Override(0.93f);
            _bloom.scatter.Override(0.55f);
            ApplyTierToVolume();
        }

        void Build3DRainParticles()
        {
            _rainVolume = IstanbulRainVolume.Create(transform);
        }

        void Build3DFogLayers()
        {
            _fogRibbonsRoot = new GameObject("Weather_3DBosphorusFogLayers");
            _fogRibbonsRoot.transform.SetParent(transform, false);
            var fogMat = GlbModelLoader.MakeMat(new Color(0.86f, 0.90f, 0.95f), 0.1f);

            for (int i = 0; i < 6; i++)
            {
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = "FogLayer_" + i;
                band.transform.SetParent(_fogRibbonsRoot.transform, false);
                Destroy(band.GetComponent<Collider>());
                band.GetComponent<Renderer>().sharedMaterial = fogMat;

                float x = (i % 2 == 0) ? -14f : 0.5f;
                float y = 0.55f + (i % 3) * 0.85f;
                float z = 8f + i * 14f;
                band.transform.localPosition = new Vector3(x, y, z);
                band.transform.localScale = new Vector3(16f, 0.12f, 6.5f);
                _fogRibbons.Add(band.transform);
            }
            _fogRibbonsRoot.SetActive(false);
        }

        void BuildPostProcessFilterSprites()
        {
            // Generate 4 procedural post-processing screen filter textures (128x256 portrait)
            _filterSprites[(int)WeatherPreset.Sunny] = CreateFilterSprite(WeatherPreset.Sunny);
            _filterSprites[(int)WeatherPreset.GoldenSunset] = CreateFilterSprite(WeatherPreset.GoldenSunset);
            _filterSprites[(int)WeatherPreset.FoggyMorning] = CreateFilterSprite(WeatherPreset.FoggyMorning);
            _filterSprites[(int)WeatherPreset.Rain] = CreateFilterSprite(WeatherPreset.Rain);
            _filterSprites[(int)WeatherPreset.Night] = CreateFilterSprite(WeatherPreset.Night);
        }

        static Sprite CreateFilterSprite(WeatherPreset preset)
        {
            const int w = 128;
            const int h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                float ny = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / (w - 1);
                    float edge = Mathf.Clamp01(Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f)) * 1.45f);
                    Color c = Color.clear;

                    switch (preset)
                    {
                        case WeatherPreset.Sunny:
                            // Clear Mediterranean turquoise sky top & warm gold sun-kissed edges
                            if (ny > 0.65f)
                                c = new Color(0.08f, 0.72f, 0.88f, (ny - 0.65f) * 0.22f);
                            else
                                c = new Color(1.0f, 0.82f, 0.28f, edge * 0.08f);
                            break;

                        case WeatherPreset.GoldenSunset:
                            // Warm golden hour toning: red-indigo shadows at bottom/edges, amber-gold horizon haze
                            float horizonBand = Mathf.Exp(-Mathf.Pow((ny - 0.58f) * 4.2f, 2f));
                            Color indigoShadow = new Color(0.28f, 0.12f, 0.38f, edge * 0.24f);
                            Color amberGlow = new Color(1.0f, 0.62f, 0.18f, horizonBand * 0.26f);
                            c = Color.Lerp(indigoShadow, amberGlow, horizonBand);
                            break;

                        case WeatherPreset.FoggyMorning:
                            // Multilayered procedural morning mist bands drifting across the horizon + pearly spread
                            float band1 = Mathf.Exp(-Mathf.Pow((ny - 0.52f) * 7.5f, 2f));
                            float band2 = Mathf.Exp(-Mathf.Pow((ny - 0.68f) * 9.0f, 2f));
                            float band3 = Mathf.Exp(-Mathf.Pow((ny - 0.38f) * 8.0f, 2f));
                            float mistAlpha = Mathf.Clamp01((band1 + band2 * 0.8f + band3 * 0.6f) * 0.22f + edge * 0.12f);
                            c = new Color(0.88f, 0.92f, 0.96f, mistAlpha);
                            break;

                        case WeatherPreset.Rain:
                            // Cool slate-blue overcast atmosphere with soft depth vignette and natural fine rain streaks
                            float skyCool = Mathf.Clamp01((ny - 0.45f) * 1.8f) * 0.16f;
                            // Natural fine continuous rain streaks using high-frequency noise without grid dots or rings
                            float streakNoise = Mathf.PerlinNoise(nx * 32f, ny * 4f);
                            float fineRain = (streakNoise > 0.72f) ? ((streakNoise - 0.72f) * 0.55f) : 0f;
                            float lensMist = edge * 0.18f;
                            float rainAlpha = Mathf.Clamp01(skyCool + fineRain + lensMist);
                            c = new Color(0.55f, 0.68f, 0.82f, rainAlpha);
                            break;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        public Sprite CurrentFilterSprite => _filterSprites[(int)CurrentWeather];

        public string CurrentWeatherLabel
        {
            get
            {
                switch (CurrentWeather)
                {
                    case WeatherPreset.Sunny: return Loc.T("weather_sunny");
                    case WeatherPreset.GoldenSunset: return Loc.T("weather_sunset");
                    case WeatherPreset.FoggyMorning: return Loc.T("weather_fog");
                    case WeatherPreset.Rain: return Loc.T("weather_rain");
                    case WeatherPreset.Night: return Loc.T("weather_night");
                    default: return Loc.T("weather_sunny");
                }
            }
        }

        public void ApplyWeatherForStop(int stopIndex)
        {
            if (UserOverrodeWeather) return;
            SetWeather(WeatherForStop(stopIndex), false);
        }

        /// <summary>
        /// The journey is mostly driven at golden hour (environment reference board): each district opens with a
        /// short warm afternoon, has one passing shower, and spends the rest (about three quarters of all stops)
        /// in the golden sunset. The first district gets a longer sunny opening; the last one (Ortaköy, "Gece")
        /// turns to night. Fog stays available from the weather filter only.
        /// Weather changes in blocks of eight stops instead of flickering every two.
        /// </summary>
        public static WeatherPreset WeatherForStop(int stopIndex)
        {
            int s = Mathf.Clamp(stopIndex, 1, GameConfig.TotalStops);
            int district = (s - 1) / GameConfig.StopsPerDistrict;
            int local = (s - 1) % GameConfig.StopsPerDistrict;
            if (district == GameConfig.DistrictCount - 1)
                return local < 24 ? WeatherPreset.GoldenSunset : WeatherPreset.Night;
            if (local < (district == 0 ? 16 : 8)) return WeatherPreset.Sunny;
            int rainStart = 24 + (district * 8) % 24;   // the shower moves through the evening from district to district
            if (local >= rainStart && local < rainStart + 8) return WeatherPreset.Rain;
            return WeatherPreset.GoldenSunset;
        }

        public void CycleWeatherPreview()
        {
            UserOverrodeWeather = true;
            int next = ((int)CurrentWeather + 1) % 5;
            SetWeather((WeatherPreset)next, true);
        }

        public void SetWeather(WeatherPreset preset, bool manual)
        {
            CurrentWeather = preset;
            if (_cam == null) _cam = Camera.main;
            if (_sun == null) _sun = FindDirectionalSun();
            BindWeatherProfile(preset);

            var gm = GameManager.Instance;
            bool isRain = preset == WeatherPreset.Rain;
            bool isFog = preset == WeatherPreset.FoggyMorning;
            bool isSunset = preset == WeatherPreset.GoldenSunset;


            if (_rainVolume != null) _rainVolume.SetRaining(isRain);
            ApplyCloudWeather(preset);
            // The drifting fog ribbon boxes drew a hard horizontal band across the screen; fog now comes from
            // RenderSettings fog + post grading only.
            if (_fogRibbonsRoot != null) _fogRibbonsRoot.SetActive(false);

            if (gm != null)
            {
                gm.Taxi?.SetWipersActive(isRain);
                gm.City?.SetRoadWetness(isRain || isFog);
                gm.City?.SetStreetlightsLit(isFog || isSunset || isRain || preset == WeatherPreset.Night);
                gm.City?.SetWindowsLit(preset == WeatherPreset.Night);
            }

            switch (preset)
            {
                case WeatherPreset.Sunny:
                    // Warm Mediterranean afternoon: low golden sun, rich warm facades, clear azure sky,
                    // soft distant horizon perspective, punchy directional shadows with cool sky bounce fill.
                    if (_cam != null) _cam.backgroundColor = new Color(0.40f, 0.68f, 0.92f);
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Linear;
                    RenderSettings.fogColor = new Color(0.69f, 0.79f, 0.87f);
                    // Pushed back so the Galata Tower and minaret skyline stay crisp and readable
                    RenderSettings.fogStartDistance = 120f;
                    RenderSettings.fogEndDistance = 410f;
                    SetGrade(0.12f, 9f, 10f, new Color(1f, 0.99f, 0.97f), 6f, 0f, 0.11f, 0.28f);
                    ApplySky(true, new Color(0.18f, 0.57f, 0.83f), new Color(0.40f, 0.45f, 0.47f), 1.0f, 0.95f);
                    SetAmbient(new Color(0.53f, 0.66f, 0.80f), new Color(0.51f, 0.49f, 0.45f), new Color(0.32f, 0.30f, 0.27f));
                    if (_sun != null)
                    {
                        _sun.color = new Color(1.0f, 0.9372549f, 0.8f); // #FFEFCC
                        _sun.intensity = 1.48f;
                        _sun.transform.rotation = Quaternion.Euler(38f, GameConfig.SideYaw(135f), 0f);
                    }
                    break;

                case WeatherPreset.GoldenSunset:
                    // Golden hour: sun low over the strait, glowing amber horizon, raking light carving facades,
                    // deep violet-blue shadows, and warm glowing streetlamps & storefronts.
                    if (_cam != null) _cam.backgroundColor = new Color(0.96f, 0.62f, 0.42f);
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Linear;
                    RenderSettings.fogColor = new Color(0.90f, 0.79f, 0.66f);
                    RenderSettings.fogStartDistance = 120f;
                    RenderSettings.fogEndDistance = 430f;
                    SetGrade(0.15f, 10f, 8f, new Color(1f, 0.985f, 0.95f), 12f, 2f, 0.14f, 0.40f);
                    ApplySky(true, new Color(0.23f, 0.56f, 0.78f), new Color(0.37f, 0.40f, 0.45f), 1.0f, 1.15f);
                    SetAmbient(new Color(0.51f, 0.57f, 0.74f), new Color(0.56f, 0.48f, 0.40f), new Color(0.31f, 0.27f, 0.26f));
                    if (_sun != null)
                    {
                        _sun.color = new Color(1.0f, 0.87f, 0.65f);
                        _sun.intensity = 1.50f;
                        _sun.transform.rotation = Quaternion.Euler(24f, GameConfig.SideYaw(142f), 0f);
                    }
                    break;

                case WeatherPreset.FoggyMorning:
                    // THREE.FogExp2 equivalent: ExponentialSquared dense coastal morning fog + illuminated streetlamps
                    if (_cam != null) _cam.backgroundColor = new Color(0.78f, 0.83f, 0.88f);
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.80f, 0.85f, 0.90f);
                    RenderSettings.fogDensity = 0.012f;
                    SetGrade(0.08f, -5f, -13f, new Color(0.95f, 0.99f, 1f), -6f, 1f, 0.14f, 0.24f);
                    ApplySky(false, default, default, 0f, 0f);
                    SetAmbient(new Color(0.72f, 0.76f, 0.82f), new Color(0.68f, 0.72f, 0.78f), new Color(0.48f, 0.50f, 0.54f));
                    if (_sun != null)
                    {
                        _sun.color = new Color(0.90f, 0.92f, 0.96f);
                        _sun.intensity = 0.88f;
                        _sun.transform.rotation = Quaternion.Euler(28f, GameConfig.SideYaw(-25f), 0f);
                    }
                    break;

                case WeatherPreset.Rain:
                    if (_cam != null) _cam.backgroundColor = new Color(0.32f, 0.40f, 0.50f);
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.38f, 0.46f, 0.56f);
                    RenderSettings.fogDensity = 0.011f;
                    SetGrade(-0.08f, 10f, -10f, new Color(0.91f, 0.97f, 1f), -12f, 0f, 0.18f, 0.34f);
                    ApplySky(false, default, default, 0f, 0f);
                    SetAmbient(new Color(0.42f, 0.47f, 0.56f), new Color(0.38f, 0.43f, 0.51f), new Color(0.26f, 0.29f, 0.34f));
                    if (_sun != null)
                    {
                        _sun.color = new Color(0.65f, 0.72f, 0.82f);
                        _sun.intensity = 0.62f;
                        _sun.transform.rotation = Quaternion.Euler(25f, GameConfig.SideYaw(-20f), 0f);
                    }
                    break;

                case WeatherPreset.Night:
                    // Ortaköy by night: navy sky, cool moonlight from behind the taxi, warm lit street lamps.
                    if (_cam != null) _cam.backgroundColor = new Color(0.05f, 0.08f, 0.17f);
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Linear;
                    RenderSettings.fogColor = new Color(0.08f, 0.11f, 0.21f);
                    RenderSettings.fogStartDistance = 45f;
                    RenderSettings.fogEndDistance = 200f;
                    SetGrade(0.02f, 8f, 2f, new Color(0.95f, 0.98f, 1f), -8f, 0f, 0.16f, 0.45f);
                    ApplySky(false, default, default, 0f, 0f);
                    SetAmbient(new Color(0.25f, 0.31f, 0.44f), new Color(0.27f, 0.30f, 0.38f), new Color(0.18f, 0.19f, 0.24f));
                    if (_sun != null)
                    {
                        _sun.color = new Color(0.64f, 0.73f, 0.95f);
                        _sun.intensity = 0.38f;
                        _sun.transform.rotation = Quaternion.Euler(38f, GameConfig.SideYaw(150f), 0f);
                    }
                    break;
            }

            if (_skyMat != null && _sun != null)
            {
                Vector3 sunDirection = -_sun.transform.forward;
                _skyMat.SetVector("_SunDirection", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
                _skyMat.SetColor("_SunColor", preset == WeatherPreset.GoldenSunset ? new Color(3f, 2.1f, 1.1f) : new Color(3.2f, 2.9f, 2.3f));
                _skyMat.SetFloat("_CloudCoverage", preset == WeatherPreset.GoldenSunset ? 0.27f : 0.34f);
            }
            ReferenceEnvironmentLighting.Apply(preset);
            if (gm != null && gm.City != null) BosphorusWaterLook.Apply(gm.City.SeaMaterial, preset);
            SynthAudio.Instance?.SetWeather(preset);

            if (gm != null && gm.Ui != null)
            {
                gm.Ui.RefreshWeatherDisplay();
                if (manual)
                    gm.Ui.ShowToast(CurrentWeatherLabel, 1.4f);
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Taxi == null) return;

            Vector3 taxiPos = gm.Taxi.transform.position;
            float speed = gm.Taxi.Speed;

            // Animate 3D drifting morning fog bands along the Bosphorus water and coastal road
            if (_fogRibbonsRoot != null && _fogRibbonsRoot.activeSelf)
            {
                _fogRibbonsRoot.transform.position = new Vector3(0f, 0f, taxiPos.z);
                for (int i = 0; i < _fogRibbons.Count; i++)
                {
                    var b = _fogRibbons[i];
                    Vector3 lp = b.localPosition;
                    lp.x += Mathf.Sin(Time.time * 0.45f + i) * 0.6f * Time.deltaTime;
                    b.localPosition = lp;
                }
            }
        }
    }
}

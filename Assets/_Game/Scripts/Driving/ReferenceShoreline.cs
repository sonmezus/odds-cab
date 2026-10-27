using UnityEngine;
using UnityEngine.Rendering;

namespace OddsCab
{
    /// <summary>
    /// Distant Istanbul matte plates, placed across real flat water. The source contains only land and
    /// buildings; its transparent sky and sea let the live sky, waves and weather remain visible.
    /// </summary>
    public sealed class ReferenceShoreline : MonoBehaviour
    {
        const float PlateWidth = 360f;
        const float PlateImageHeight = 180f;
        // The generated 1774 x 887 plate's last opaque quay row is 806 from the image's top.
        const float WaterlineV = 1f - 806f / 887f;
        const float BottomV = 0.075f;
        const float TopV = 0.535f;
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int AtmosphereId = Shader.PropertyToID("_Atmosphere");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        Material _material;
        Mesh _mesh;
        Material _ridgeMaterial, _bridgeMaterial, _lightMaterial;
        Mesh _ridgeMesh, _bridgeMesh, _lightMesh;
        GameObject _nativeShore;
        readonly System.Collections.Generic.List<MeshRenderer> _eveningLights =
            new System.Collections.Generic.List<MeshRenderer>(2);
        WeatherPreset _weather;
        Color _fogColor;
        bool _weatherApplied;

        public static bool TryCreate(Transform parent, float waterLevel, out ReferenceShoreline shoreline)
        {
            shoreline = null;
            var texture = Resources.Load<Texture2D>("ReferenceKit/Textures/BosphorusShoreline");
            var shader = Resources.Load<Shader>("Shaders/ReferenceShoreline");
            if (texture == null || shader == null || !shader.isSupported) return false;

            var root = new GameObject("Reference_BosphorusShoreline");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.up * waterLevel;
            shoreline = root.AddComponent<ReferenceShoreline>();
            shoreline._material = new Material(shader)
            {
                name = "Reference_BosphorusShoreline_Runtime",
                hideFlags = HideFlags.DontSave
            };
            shoreline._material.SetTexture(BaseMapId, texture);
            // Only the painted plate blends smoothly. Native terrain and bridge still write depth.
            shoreline._material.SetFloat("_EdgeFeather", 0.08f);
            shoreline._material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            shoreline._material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            shoreline._material.SetFloat("_ZWrite", 0f);
            shoreline._material.SetFloat("_AlphaToMask", 0f);
            shoreline._material.SetOverrideTag("RenderType", "Transparent");
            shoreline._material.renderQueue = (int)RenderQueue.Transparent - 10;
            shoreline._mesh = BuildPlateMesh();
            shoreline.BuildDepthGeometry(shader);
            shoreline.CreatePlate("FarShore_Forward", new Vector3(-28f, 0f, 440f), Quaternion.identity);
            // A second bank supports the menu's front three-quarter view across the strait. The plates
            // are separated by open water rather than joined into a vertical wall around the street.
            // The front menu looks diagonally backwards: its horizon ray reaches the opposite bank
            // around X=320, Z=-600 after the coastal parent is mirrored. Keep this plate perpendicular
            // to that ray. Mobile uses the same apparent size inside its single camera's 600 m clip.
            float rearScale = Application.isMobilePlatform ? 0.75f : 1f;
            shoreline.CreatePlate("FarShore_AcrossStrait", new Vector3(-320f, 0f, -600f) * rearScale,
                Quaternion.Euler(0f, -151.93f, 0f), rearScale);
            shoreline.RefreshWeather();
            return true;
        }

        void BuildDepthGeometry(Shader shader)
        {
            _nativeShore = Resources.Load<GameObject>("ReferenceKit/FilmShoreTerrain");
            if (_nativeShore == null) _ridgeMesh = IstanbulSkylineGeometry.BuildRidges();
            _bridgeMesh = IstanbulSkylineGeometry.BuildBridge(false);
            _lightMesh = IstanbulSkylineGeometry.BuildBridge(true);
            _ridgeMaterial = MakeVertexMaterial(shader, "Istanbul_DistantRidges");
            // glTF COLOR_0 is already linear, unlike the locally authored display-RGB bridge.
            _ridgeMaterial.SetFloat("_VertexColorsLinear", _nativeShore != null ? 1f : 0f);
            _bridgeMaterial = MakeVertexMaterial(shader, "Istanbul_BridgeSteel");
            _lightMaterial = MakeVertexMaterial(shader, "Istanbul_EveningLights");
        }

        static Material MakeVertexMaterial(Shader shader, string name)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            material.SetTexture(BaseMapId, Texture2D.whiteTexture);
            material.SetFloat("_UseVertexColor", 1f);
            return material;
        }

        static Mesh BuildPlateMesh()
        {
            float bottom = (BottomV - WaterlineV) * PlateImageHeight;
            float top = (TopV - WaterlineV) * PlateImageHeight;
            var mesh = new Mesh
            {
                name = "ReferenceShoreline_CroppedSkyQuad",
                hideFlags = HideFlags.DontSave,
                vertices = new[]
                {
                    new Vector3(-PlateWidth * 0.5f, bottom, 0f),
                    new Vector3(PlateWidth * 0.5f, bottom, 0f),
                    new Vector3(-PlateWidth * 0.5f, top, 0f),
                    new Vector3(PlateWidth * 0.5f, top, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, BottomV), new Vector2(1f, BottomV),
                    new Vector2(0f, TopV), new Vector2(1f, TopV)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        void CreatePlate(string name, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            var plate = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            plate.layer = HorizonCameraStack.VistaLayer;
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = position;
            plate.transform.localRotation = rotation;
            plate.transform.localScale = new Vector3(scale, scale, 1f);
            plate.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = plate.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            if (_nativeShore != null)
            {
                var shore = Instantiate(_nativeShore, plate.transform, false);
                shore.name = "Istanbul_EditableNativeMainland";
                // glTFast converts handedness by negating X. Restore authored plate coordinates
                // so the native approaches meet the procedural bridge's -182 / +42 m endpoints.
                shore.transform.localScale = new Vector3(-1f, 1f, 1f);
                foreach (var part in shore.GetComponentsInChildren<Transform>(true))
                    part.gameObject.layer = HorizonCameraStack.VistaLayer;
                foreach (var part in shore.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = part.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = _ridgeMaterial;
                    part.sharedMaterials = materials;
                    part.shadowCastingMode = ShadowCastingMode.Off;
                    part.receiveShadows = false;
                    part.lightProbeUsage = LightProbeUsage.Off;
                    part.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
            }
            else CreateDepthLayer(plate.transform, "Istanbul_LayeredHills", _ridgeMesh, _ridgeMaterial);
            CreateDepthLayer(plate.transform, "Istanbul_BosphorusBridge", _bridgeMesh, _bridgeMaterial);
            _eveningLights.Add(CreateDepthLayer(plate.transform, "Istanbul_BridgeLights", _lightMesh, _lightMaterial));
        }

        static MeshRenderer CreateDepthLayer(Transform parent, string name, Mesh mesh, Material material)
        {
            var layer = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            layer.layer = HorizonCameraStack.VistaLayer;
            layer.transform.SetParent(parent, false);
            layer.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        public void SetWaterLevel(float waterLevel)
        {
            Vector3 position = transform.localPosition;
            if (Mathf.Abs(position.y - waterLevel) < 0.001f) return;
            position.y = waterLevel;
            transform.localPosition = position;
        }

        void LateUpdate() => RefreshWeather();

        void RefreshWeather()
        {
            if (_material == null) return;
            WeatherPreset weather = WeatherDirector.Instance != null
                ? WeatherDirector.Instance.CurrentWeather : WeatherPreset.Sunny;
            Color fogColor = RenderSettings.fogColor;
            if (_weatherApplied && weather == _weather && fogColor == _fogColor) return;
            _weather = weather;
            _fogColor = fogColor;
            _weatherApplied = true;
            Color tint;
            float exposure, saturation, haze;
            switch (weather)
            {
                case WeatherPreset.GoldenSunset:
                    tint = new Color(1f, 0.95f, 0.87f);
                    exposure = 0.96f; saturation = 0.92f; haze = 0.23f;
                    break;
                case WeatherPreset.FoggyMorning:
                    tint = new Color(0.93f, 0.97f, 1f);
                    exposure = 0.92f; saturation = 0.45f; haze = 0.71f;
                    break;
                case WeatherPreset.Rain:
                    tint = new Color(0.76f, 0.85f, 0.97f);
                    exposure = 0.64f; saturation = 0.59f; haze = 0.54f;
                    break;
                case WeatherPreset.Night:
                    tint = new Color(0.43f, 0.58f, 0.86f);
                    exposure = 0.22f; saturation = 0.60f; haze = 0.42f;
                    break;
                default:
                    tint = new Color(0.98f, 0.99f, 1f);
                    exposure = 0.97f; saturation = 0.92f; haze = 0.24f;
                    break;
            }
            Color atmosphere = fogColor;
            atmosphere.a = haze;
            _material.SetColor(TintId, tint);
            _material.SetColor(AtmosphereId, atmosphere);
            _material.SetFloat(ExposureId, exposure);
            _material.SetFloat(SaturationId, saturation);
            // Depth bands stay behind the detailed town. Their extra haze makes distant hills
            // recede while the bridge keeps an open, readable profile in every weather preset.
            Color depthAtmosphere = Color.Lerp(fogColor, new Color(0.46f, 0.59f, 0.67f), 0.38f);
            ApplyDepthWeather(_ridgeMaterial, tint, depthAtmosphere, exposure, saturation * 0.88f,
                Mathf.Lerp(haze, 1f, 0.28f));
            ApplyDepthWeather(_bridgeMaterial, tint, depthAtmosphere, exposure, saturation * 0.88f,
                Mathf.Lerp(haze, 1f, 0.12f));
            bool evening = weather == WeatherPreset.Night || weather == WeatherPreset.GoldenSunset;
            for (int i = 0; i < _eveningLights.Count; i++) _eveningLights[i].enabled = evening;
            ApplyDepthWeather(_lightMaterial, Color.white, fogColor,
                weather == WeatherPreset.Night ? 1.5f : 0.72f, 1f, haze * 0.55f);
        }

        static void ApplyDepthWeather(Material material, Color tint, Color fog, float exposure,
            float saturation, float haze)
        {
            // These generated colors enter the same linear shader as the decoded matte texture.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) fog = fog.linear;
            fog.a = haze;
            material.SetColor(TintId, tint);
            material.SetColor(AtmosphereId, fog);
            material.SetFloat(ExposureId, exposure);
            material.SetFloat(SaturationId, saturation);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
            if (_ridgeMaterial != null) Destroy(_ridgeMaterial);
            if (_bridgeMaterial != null) Destroy(_bridgeMaterial);
            if (_lightMaterial != null) Destroy(_lightMaterial);
            if (_ridgeMesh != null) Destroy(_ridgeMesh);
            if (_bridgeMesh != null) Destroy(_bridgeMesh);
            if (_lightMesh != null) Destroy(_lightMesh);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;

namespace OddsCab
{
    /// <summary>Near-field opaque geometry for genuine GPU triangle rays. Mobile keeps raster/probe lighting.</summary>
    public sealed class WorldRayTracingScene : MonoBehaviour
    {
        sealed class Entry
        {
            public Renderer renderer;
            public Transform transform;
            public int[] handles;
            public Matrix4x4 matrix;
            public bool visible;
        }
        public static WorldRayTracingScene Instance { get; private set; }
        public static bool ShouldTrace(bool mobile, DeviceTier tier, bool hardware)
            => ShouldTrace(mobile, tier, hardware, hardware);
        public static bool ShouldTrace(bool mobile, DeviceTier tier, bool supportsHardware, bool hardwareBackendAvailable)
            => !mobile && tier == DeviceTier.Ultra && supportsHardware && hardwareBackendAvailable;
        public bool Enabled => isActiveAndEnabled && _context != null && Shader != null && _nativeShader != null && _nativeAccel != null && ShouldTrace(Application.isMobilePlatform,
            DeviceTierDetector.CurrentTier, SystemInfo.supportsRayTracing, RayTracingContext.IsBackendSupported(RayTracingBackend.Hardware));
        public string Backend => Enabled ? "Hardware triangle rays" : "Raster / reflection probes";
        public bool NativeBindingAvailable => _nativeAccel != null;
        public int RegisteredRenderers => _entries.Count;
        public int ActiveRenderers { get; private set; }
        public IRayTracingShader Shader { get; private set; }
        readonly List<Entry> _entries = new List<Entry>(2048);
        readonly HashSet<Renderer> _registered = new HashSet<Renderer>();
        RayTracingContext _context;
        IRayTracingAccelStruct _accel;
        RayTracingShader _nativeShader;
        RayTracingAccelerationStructure _nativeAccel;
        bool _topologyDirty;
        Transform _taxi;
        ReflectionProbe _probe;
        int _probeRequest = -1, _lastDistrict = -1;
        WeatherPreset _probeWeather = (WeatherPreset)(-1);
        Vector3 _probePosition;
        float _probeCooldown;

        public static void Attach(Transform taxi)
        {
            if (Instance != null) return;
            var root = new GameObject("WorldReflectionAndRayTracing");
            var scene = root.AddComponent<WorldRayTracingScene>();
            Instance = scene; scene._taxi = taxi;
            scene.BuildProbe();
            scene.InitializeHardware();
        }

        void OnEnable()
        {
            DeviceTierDetector.TierChanged += OnTierChanged;
            if (_taxi != null) OnTierChanged(DeviceTierDetector.CurrentTier);
        }

        void OnDisable() => DeviceTierDetector.TierChanged -= OnTierChanged;

        void OnTierChanged(DeviceTier tier)
        {
            if (_probe == null && tier != DeviceTier.Low) BuildProbe();
            if (tier == DeviceTier.Ultra) InitializeHardware();
        }

        void InitializeHardware()
        {
            if (_context != null || !ShouldTrace(Application.isMobilePlatform, DeviceTierDetector.CurrentTier,
                SystemInfo.supportsRayTracing, RayTracingContext.IsBackendSupported(RayTracingBackend.Hardware))) return;
            var shaderObject = Resources.Load<RayTracingShader>("Shaders/WorldContactRays");
            if (shaderObject == null) return;
            var resources = new RayTracingResources();
            if (!resources.LoadFromRenderPipelineResources()) return;
            try
            {
                _context = new RayTracingContext(RayTracingBackend.Hardware, resources);
                _nativeShader = shaderObject;
                Shader = _context.CreateRayTracingShader(shaderObject);
                _accel = _context.CreateAccelerationStructure(new AccelerationStructureOptions { buildFlags = BuildFlags.PreferFastTrace });
                // URT's hardware class is internal, but its native AS getter is public. Resolve once,
                // then bypass the package's string-concatenating binding method on every rendered frame.
                var nativeProperty = _accel.GetType().GetProperty("accelStruct", BindingFlags.Public | BindingFlags.Instance);
                if (nativeProperty != null) _nativeAccel = nativeProperty.GetValue(_accel) as RayTracingAccelerationStructure;
                if (_nativeAccel == null)
                {
                    ReleaseRayTracing();
                    Debug.LogWarning("[OddsCab] Native hardware AS access unavailable; reflection probes remain active.");
                    return;
                }
                var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
                for (int i = 0; i < renderers.Length; i++) Register(renderers[i]);
                WarmBuild();
            }
            catch (Exception error)
            {
                ReleaseRayTracing();
                Debug.LogWarning("[OddsCab] Hardware contact rays unavailable: " + error.Message);
            }
        }

        void BuildProbe()
        {
            if (_probe != null || DeviceTierDetector.CurrentTier == DeviceTier.Low) return;
            var root = new GameObject("IstanbulLocalReflectionProbe"); root.transform.SetParent(transform, false);
            _probe = root.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            _probe.resolution = Application.isMobilePlatform ? 128 : 256;
            _probe.hdr = true; _probe.boxProjection = true; _probe.size = new Vector3(70f, 30f, 90f);
            _probe.blendDistance = 15f; _probe.nearClipPlane = .35f; _probe.farClipPlane = 350f;
            _probe.cullingMask = ~((1 << TaxiController.CabLayer) | (1 << TaxiController.ExteriorLayer));
            _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        }

        void Update()
        {
            if (_probe == null || _taxi == null || DeviceTierDetector.CurrentTier == DeviceTier.Low) return;
            _probeCooldown -= Time.deltaTime;
            var weather = WeatherDirector.Instance != null ? WeatherDirector.Instance.CurrentWeather : WeatherPreset.Sunny;
            var game = GameManager.Instance;
            int district = game != null && game.City != null ? game.City.District : 0;
            if (_probeCooldown > 0f || (_probeRequest >= 0 && !_probe.IsFinishedRendering(_probeRequest))) return;
            if (district == _lastDistrict && weather == _probeWeather && (_taxi.position - _probePosition).sqrMagnitude < 32f * 32f) return;
            _lastDistrict = district; _probeWeather = weather; _probePosition = _taxi.position;
            _probe.transform.position = _probePosition + Vector3.up * 1.4f;
            _probeRequest = _probe.RenderProbe(); _probeCooldown = 2f;
        }

        public void RegisterRoot(Transform root)
        {
            if (_accel == null || root == null) return;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].renderer != null) continue;
                var dead = _entries[i];
                for (int h = 0; h < dead.handles.Length; h++) _accel.RemoveInstance(dead.handles[h]);
                _registered.Remove(dead.renderer); _entries.RemoveAt(i); _topologyDirty = true;
            }
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) Register(renderers[i]);
        }

        void Register(Renderer renderer)
        {
            if (renderer == null || _registered.Contains(renderer) || _entries.Count >= 2048 || renderer is LineRenderer
                || renderer is ParticleSystemRenderer || renderer is SkinnedMeshRenderer) return;
            if (renderer.gameObject.layer == HorizonCameraStack.VistaLayer || renderer.name.Contains("Water") || renderer.name.Contains("Sea")) return;
            for (var ancestor = renderer.transform; ancestor != null; ancestor = ancestor.parent)
                if (ancestor.name.Contains("_LOD1")) return;
            var materials = renderer.sharedMaterials;
            bool opaque = false;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].renderQueue < 2450) opaque = true;
            if (!opaque) return;
            Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) return;
            var entry = new Entry { renderer = renderer, transform = renderer.transform, matrix = renderer.localToWorldMatrix };
            int count = Mathf.Min(mesh.subMeshCount, materials.Length);
            var handles = new List<int>(count);
            for (int sub = 0; sub < count; sub++)
            {
                if (materials[sub] == null || materials[sub].renderQueue >= 2450) continue;
                handles.Add(_accel.AddInstance(new MeshInstanceDesc(mesh, sub) { localToWorldMatrix = entry.matrix,
                    enableTriangleCulling = false, mask = 0, instanceID = (uint)_entries.Count }));
            }
            if (handles.Count == 0) return;
            entry.handles = handles.ToArray(); _entries.Add(entry); _registered.Add(renderer); _topologyDirty = true;
        }

        public void UnregisterRoot(Transform root)
        {
            if (_accel == null || root == null) return;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.transform != null && !entry.transform.IsChildOf(root)) continue;
                for (int h = 0; h < entry.handles.Length; h++) _accel.RemoveInstance(entry.handles[h]);
                _registered.Remove(entry.renderer); _entries.RemoveAt(i); _topologyDirty = true;
            }
        }

        public void WarmBuild()
        {
            if (_accel == null) return;
            var cmd = CommandBufferPool.Get("Warm Istanbul ray geometry");
            try
            {
                var camera = Camera.main;
                if (camera != null) UpdateInstances(camera);
                // The hardware backend owns its native build/trace scratch storage.
                _accel.Build(cmd, null);
                BindScene(cmd);
                Graphics.ExecuteCommandBuffer(cmd);
                _topologyDirty = false;
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        bool UpdateInstances(Camera camera)
        {
            bool dirty = _topologyDirty; ActiveRenderers = 0;
            Vector3 origin = _taxi != null ? _taxi.position : camera.transform.position;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                bool visible = entry.renderer != null && entry.renderer.enabled && entry.renderer.gameObject.activeInHierarchy
                    && (camera.cullingMask & (1 << entry.renderer.gameObject.layer)) != 0
                    && entry.renderer.bounds.SqrDistance(origin) < 28f * 28f;
                if (entry.visible != visible)
                {
                    entry.visible = visible; dirty = true;
                    for (int h = 0; h < entry.handles.Length; h++) _accel.UpdateInstanceMask(entry.handles[h], visible ? 255u : 0u);
                }
                if (!visible) continue;
                ActiveRenderers++;
                Matrix4x4 matrix = entry.renderer.localToWorldMatrix;
                if (matrix == entry.matrix) continue;
                entry.matrix = matrix; dirty = true;
                for (int h = 0; h < entry.handles.Length; h++) _accel.UpdateInstanceTransform(entry.handles[h], matrix);
            }
            return dirty;
        }

        void BindScene(CommandBuffer cmd)
        {
            cmd.SetRayTracingShaderPass(_nativeShader, "RayTracing");
            cmd.SetRayTracingAccelerationStructure(_nativeShader, SceneAccelId, _nativeAccel);
        }

        public void Dispatch(CommandBuffer cmd, Camera camera, RenderTargetIdentifier depth, RenderTargetIdentifier normals,
            RenderTargetIdentifier output, int width, int height, int fullWidth, int fullHeight)
        {
            if (!Enabled || cmd == null || camera == null) return;
            if (UpdateInstances(camera)) { _accel.Build(cmd, null); _topologyDirty = false; }
            BindScene(cmd);
            Vector3 origin = _taxi != null ? _taxi.position : camera.transform.position;
            Shader.SetTextureParam(cmd, DepthId, depth); Shader.SetTextureParam(cmd, NormalsId, normals); Shader.SetTextureParam(cmd, OutputId, output);
            Shader.SetMatrixParam(cmd, InvVpId, (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix).inverse);
            Shader.SetVectorParam(cmd, SizeId, new Vector4(width, height, fullWidth, fullHeight));
            Shader.SetVectorParam(cmd, OriginId, origin);
            var sun = RenderSettings.sun;
            Shader.SetVectorParam(cmd, SunId, sun != null ? -sun.transform.forward : Vector3.up);
            Shader.Dispatch(cmd, null, (uint)width, (uint)height, 1);
        }

        static readonly int DepthId = UnityEngine.Shader.PropertyToID("_SceneDepth"), NormalsId = UnityEngine.Shader.PropertyToID("_SceneNormals"), OutputId = UnityEngine.Shader.PropertyToID("_ContactOutput"),
            InvVpId = UnityEngine.Shader.PropertyToID("_InverseViewProjection"), SizeId = UnityEngine.Shader.PropertyToID("_RayDimensions"), OriginId = UnityEngine.Shader.PropertyToID("_RoadOrigin"), SunId = UnityEngine.Shader.PropertyToID("_SunDirection"),
            SceneAccelId = UnityEngine.Shader.PropertyToID("_SceneaccelStruct");

        void ReleaseRayTracing()
        {
            _nativeAccel = null; _nativeShader = null; Shader = null;
            _accel?.Dispose(); _accel = null;
            _context?.Dispose(); _context = null;
            _entries.Clear(); _registered.Clear();
            ActiveRenderers = 0; _topologyDirty = false;
        }

        void OnDestroy()
        {
            DeviceTierDetector.TierChanged -= OnTierChanged;
            ReleaseRayTracing();
            if (Instance == this) Instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => Instance = null;
    }
}


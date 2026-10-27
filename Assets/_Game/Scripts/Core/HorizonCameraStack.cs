using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OddsCab
{
    /// <summary>
    /// Draws the distant Bosphorus vista (layer <see cref="VistaLayer"/>) with its own base camera and stacks the
    /// world camera on top of it with a short far clip. The street therefore always opens onto the sea at the same
    /// distance, wherever the streamed chunks end, and the vista gets a lighter fog than the street.
    /// </summary>
    public class HorizonCameraStack : MonoBehaviour
    {
        public const int VistaLayer = 30;
        public const float WorldFarClip = 172f;
        // The far shore sits 150–300 m away; with the street's fog it would wash out completely.
        const float VistaFogScale = 2.4f;
        static readonly int FogParamsId = Shader.PropertyToID("unity_FogParams");

        Camera _world, _vista;
        UniversalAdditionalCameraData _worldData, _vistaData;

        public static void Attach(Camera world)
        {
            if (world == null || world.GetComponentInChildren<HorizonCameraStack>() != null) return;

            // Keep the mobile vista in the world camera to avoid a second cull and stack render target work.
            if (Application.isMobilePlatform)
            {
                world.farClipPlane = 600f;
                world.cullingMask |= (1 << VistaLayer);
                // Share one camera with the distant shore without submitting all six ahead chunks.
                // Fog hides geometry visually but does not cull its renderers or vertex work.
                var layerDistances = new float[32];
                for (int layer = 0; layer < layerDistances.Length; layer++)
                    layerDistances[layer] = WorldFarClip;
                layerDistances[VistaLayer] = world.farClipPlane;
                world.layerCullDistances = layerDistances;
                world.layerCullSpherical = true;
                return;
            }

            var go = new GameObject("VistaCamera");
            go.transform.SetParent(world.transform, false);
            var stack = go.AddComponent<HorizonCameraStack>();
            stack._world = world;
            var vista = stack._vista = go.AddComponent<Camera>();
            vista.cullingMask = 1 << VistaLayer;
            vista.nearClipPlane = 1f;
            vista.farClipPlane = 1200f;
            vista.depth = world.depth - 1f;

            world.cullingMask &= ~(1 << VistaLayer);
            world.farClipPlane = WorldFarClip;

            var worldData = stack._worldData = world.GetUniversalAdditionalCameraData();
            var vistaData = stack._vistaData = vista.GetUniversalAdditionalCameraData();
            vistaData.renderType = CameraRenderType.Base;
            vistaData.renderShadows = false;
            vistaData.renderPostProcessing = false;   // the world camera's post pass grades the whole stack
            vistaData.antialiasing = worldData.antialiasing;
            vistaData.antialiasingQuality = worldData.antialiasingQuality;
            worldData.renderType = CameraRenderType.Overlay;   // overlays clear depth by default: the world always wins
            vistaData.cameraStack.Add(world);
            stack.Sync();
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        void LateUpdate() => Sync();

        /// <summary>Weather and menus keep driving the world camera; the base camera mirrors what it shows.</summary>
        void Sync()
        {
            if (_world == null || _vista == null) return;
            _vista.fieldOfView = _world.fieldOfView;
            _vista.allowHDR = _world.allowHDR;
            _vista.allowMSAA = _world.allowMSAA;
            // The base camera of a stack owns the anti-aliasing for the whole stack
            var vd = _vistaData;
            var wd = _worldData;
            if (vd.antialiasing != wd.antialiasing) vd.antialiasing = wd.antialiasing;
            if (vd.antialiasingQuality != wd.antialiasingQuality) vd.antialiasingQuality = wd.antialiasingQuality;
            // An overlay camera reports "Nothing"; only a solid-colour sky (weather fallback) is worth copying.
            bool drawnSky = WeatherDirector.Instance != null && WeatherDirector.Instance.UsesReferenceSkyDome;
            _vista.clearFlags = drawnSky || _world.clearFlags == CameraClearFlags.SolidColor
                ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            _vista.backgroundColor = _world.backgroundColor;
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.Linear) return;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            if (cam == _vista) SetLinearFog(start * VistaFogScale, end * VistaFogScale);
            else SetLinearFog(start, end);   // world camera and any other (e.g. the in-car webcam)
        }

        static void SetLinearFog(float start, float end)
        {
            float d = RenderSettings.fogDensity, range = Mathf.Max(0.01f, end - start);
            Shader.SetGlobalVector(FogParamsId, new Vector4(d * 0.8493218f, d * 1.442695f, -1f / range, end / range));
        }
    }
}

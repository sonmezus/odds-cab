using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace OddsCab
{
    /// <summary>URP Render Graph pass for GPU triangle-traced ambient/contact shading, before transparent surfaces.</summary>
    public sealed class WorldContactRayTracingFeature : ScriptableRendererFeature
    {
        ContactPass _pass;
        public override void Create()
        {
            _pass?.Dispose();
            _pass = new ContactPass { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var scene = WorldRayTracingScene.Instance;
            if (scene == null || !scene.Enabled || renderingData.cameraData.camera != Camera.main) return;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing) => _pass?.Dispose();

        sealed class ContactPass : ScriptableRenderPass
        {
            Material _composite;
            public ContactPass()
            {
                var shader = Resources.Load<Shader>("Shaders/RayContactComposite");
                if (shader != null) _composite = CoreUtils.CreateEngineMaterial(shader);
            }
            static readonly int AoId = Shader.PropertyToID("_RayContactTexture"), DepthId = Shader.PropertyToID("_FullDepth");
            sealed class PassData
            {
                public TextureHandle depth, normals, output, color;
                public Camera camera;
                public Material composite;
                public WorldRayTracingScene scene;
                public int width, height, fullWidth, fullHeight;
            }
            public void Dispose() { CoreUtils.Destroy(_composite); _composite = null; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var scene = WorldRayTracingScene.Instance;
                var cameraData = frameData.Get<UniversalCameraData>();
                var resources = frameData.Get<UniversalResourceData>();
                if (scene == null || !scene.Enabled || cameraData.camera != Camera.main || !resources.cameraNormalsTexture.IsValid()) return;
                if (_composite == null) return;
                var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
                int fullWidth = descriptor.width, fullHeight = descriptor.height;
                descriptor.name = "IstanbulTriangleRayContacts";
                descriptor.width = Mathf.Max(1, fullWidth / 4); descriptor.height = Mathf.Max(1, fullHeight / 4);
                descriptor.colorFormat = GraphicsFormat.R16G16_SFloat; descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None; descriptor.enableRandomWrite = true;
                descriptor.clearBuffer = false;
                var output = graph.CreateTexture(descriptor);
                using (var builder = graph.AddUnsafePass<PassData>("Istanbul triangle ray contacts", out var pass))
                {
                    pass.scene = scene; pass.camera = cameraData.camera; pass.composite = _composite;
                    pass.depth = resources.cameraDepthTexture; pass.normals = resources.cameraNormalsTexture;
                    pass.color = resources.activeColorTexture; pass.output = output;
                    pass.width = descriptor.width; pass.height = descriptor.height; pass.fullWidth = fullWidth; pass.fullHeight = fullHeight;
                    builder.UseTexture(pass.depth, AccessFlags.Read); builder.UseTexture(pass.normals, AccessFlags.Read);
                    builder.UseTexture(pass.output, AccessFlags.ReadWrite); builder.UseTexture(pass.color, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true); builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        data.scene.Dispatch(cmd, data.camera, data.depth, data.normals, data.output, data.width, data.height, data.fullWidth, data.fullHeight);
                        cmd.SetGlobalTexture(AoId, data.output); cmd.SetGlobalTexture(DepthId, data.depth);
                        cmd.SetRenderTarget(data.color);
                        cmd.DrawProcedural(Matrix4x4.identity, data.composite, 0, MeshTopology.Triangles, 3);
                    });
                }
            }
        }
    }
}

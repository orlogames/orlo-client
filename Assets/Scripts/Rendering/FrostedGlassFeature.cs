using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Orlo.Rendering
{
    /// <summary>
    /// URP ScriptableRendererFeature that provides a Gaussian-blurred copy of the opaque scene
    /// for frosted glass UI panels. Creates a _FrostedBlurTexture global shader property.
    ///
    /// Add this to the URP Renderer Data asset (or create it programmatically via URPSetup).
    /// The FrostedGlass shader samples _CameraOpaqueTexture and applies its own blur kernel,
    /// but this feature provides a pre-blurred version for higher quality at lower per-fragment cost.
    ///
    /// Unity 6000.4 migration: the compatibility-mode OnCameraSetup/Execute overrides were
    /// removed from ScriptableRenderPass, so this pass now records through the Render Graph
    /// API (RecordRenderGraph + AddUnsafePass, which preserves the original CommandBuffer
    /// ping-pong blur logic verbatim). Blur temp targets are transient render-graph textures
    /// instead of persistently allocated RTHandles.
    /// </summary>
    public class FrostedGlassFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            [Range(1, 8)] public int downsampleFactor = 2;
            [Range(1, 5)] public int blurIterations = 2;
            [Range(0.5f, 5f)] public float blurRadius = 1.5f;
        }

        public Settings settings = new Settings();
        private FrostedGlassPass _pass;

        public override void Create()
        {
            _pass = new FrostedGlassPass(settings);
            _pass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Only render for game cameras, not preview/reflection
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Cleanup();
        }

        private class FrostedGlassPass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private Material _blurMaterial;

            private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
            private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
            private static readonly int FrostedBlurTextureId = Shader.PropertyToID("_FrostedBlurTexture");

            private class PassData
            {
                public TextureHandle source;
                public TextureHandle tempA;
                public TextureHandle tempB;
                public Material blurMaterial;
                public int blurIterations;
                public float blurRadius;
            }

            public FrostedGlassPass(Settings settings)
            {
                _settings = settings;
                profilingSampler = new ProfilingSampler("FrostedGlassBlur");
                // We sample the camera color target, so the renderer must not be
                // drawing directly to the backbuffer when this pass runs.
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                EnsureBlurMaterial();
                if (_blurMaterial == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                if (resourceData.isActiveTargetBackBuffer) return;

                var desc = cameraData.cameraTargetDescriptor;
                desc.width = Mathf.Max(1, desc.width / _settings.downsampleFactor);
                desc.height = Mathf.Max(1, desc.height / _settings.downsampleFactor);
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                TextureHandle tempA = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_FrostedBlurA", false, FilterMode.Bilinear);
                TextureHandle tempB = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_FrostedBlurB", false, FilterMode.Bilinear);

                using (var builder = renderGraph.AddUnsafePass<PassData>("FrostedGlassBlur", out var passData, profilingSampler))
                {
                    passData.source = resourceData.activeColorTexture;
                    passData.tempA = tempA;
                    passData.tempB = tempB;
                    passData.blurMaterial = _blurMaterial;
                    passData.blurIterations = _settings.blurIterations;
                    passData.blurRadius = _settings.blurRadius;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.UseTexture(tempA, AccessFlags.ReadWrite);
                    builder.UseTexture(tempB, AccessFlags.ReadWrite);

                    // The blurred result is consumed by UI shaders outside this graph
                    // via the _FrostedBlurTexture global — the graph can't see that
                    // dependency, so the pass must not be culled and needs permission
                    // to write global shader state.
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) => ExecutePass(data, ctx));
                }
            }

            private static void ExecutePass(PassData data, UnsafeGraphContext ctx)
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                // Copy opaque texture to temp A (downsampled)
                Blitter.BlitCameraTexture(cmd, data.source, data.tempA);

                // Ping-pong Gaussian blur
                for (int i = 0; i < data.blurIterations; i++)
                {
                    float radius = data.blurRadius * (i + 1);

                    // Horizontal pass
                    data.blurMaterial.SetFloat(BlurRadiusId, radius);
                    data.blurMaterial.SetVector(BlurDirectionId, new Vector4(1, 0, 0, 0));
                    Blitter.BlitCameraTexture(cmd, data.tempA, data.tempB, data.blurMaterial, 0);

                    // Vertical pass
                    data.blurMaterial.SetVector(BlurDirectionId, new Vector4(0, 1, 0, 0));
                    Blitter.BlitCameraTexture(cmd, data.tempB, data.tempA, data.blurMaterial, 0);
                }

                // Set as global texture for FrostedGlass shader to sample
                cmd.SetGlobalTexture(FrostedBlurTextureId, data.tempA);
            }

            public void Cleanup()
            {
                if (_blurMaterial != null)
                    CoreUtils.Destroy(_blurMaterial);
            }

            private void EnsureBlurMaterial()
            {
                if (_blurMaterial != null) return;

                var shader = Resources.Load<Shader>("Shaders/FrostedGlassBlur");
                if (shader == null)
                {
                    // Fallback: use a simple blit (no blur, but won't crash)
                    shader = Shader.Find("Hidden/Universal Render Pipeline/Blit");
                }
                if (shader != null)
                    _blurMaterial = CoreUtils.CreateEngineMaterial(shader);
            }
        }
    }
}

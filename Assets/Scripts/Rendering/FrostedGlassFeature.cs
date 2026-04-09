using UnityEngine;
using UnityEngine.Rendering;
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
            private RTHandle _blurTempA;
            private RTHandle _blurTempB;
            private Material _blurMaterial;

            private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
            private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");

            public FrostedGlassPass(Settings settings)
            {
                _settings = settings;
                profilingSampler = new ProfilingSampler("FrostedGlassBlur");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.width /= _settings.downsampleFactor;
                desc.height /= _settings.downsampleFactor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                RenderingUtils.ReAllocateHandleIfNeeded(ref _blurTempA, desc, FilterMode.Bilinear, name: "_FrostedBlurA");
                RenderingUtils.ReAllocateHandleIfNeeded(ref _blurTempB, desc, FilterMode.Bilinear, name: "_FrostedBlurB");

                EnsureBlurMaterial();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_blurMaterial == null) return;

                var cmd = CommandBufferPool.Get("FrostedGlassBlur");

                // Copy opaque texture to temp A (downsampled)
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, source, _blurTempA);

                // Ping-pong Gaussian blur
                for (int i = 0; i < _settings.blurIterations; i++)
                {
                    float radius = _settings.blurRadius * (i + 1);

                    // Horizontal pass
                    _blurMaterial.SetFloat(BlurRadiusId, radius);
                    _blurMaterial.SetVector(BlurDirectionId, new Vector4(1, 0, 0, 0));
                    Blitter.BlitCameraTexture(cmd, _blurTempA, _blurTempB, _blurMaterial, 0);

                    // Vertical pass
                    _blurMaterial.SetVector(BlurDirectionId, new Vector4(0, 1, 0, 0));
                    Blitter.BlitCameraTexture(cmd, _blurTempB, _blurTempA, _blurMaterial, 0);
                }

                // Set as global texture for FrostedGlass shader to sample
                cmd.SetGlobalTexture("_FrostedBlurTexture", _blurTempA);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Cleanup()
            {
                _blurTempA?.Release();
                _blurTempB?.Release();
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

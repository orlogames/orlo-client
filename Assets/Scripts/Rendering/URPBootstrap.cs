using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Orlo.Rendering
{
    /// <summary>
    /// Runtime safety net: ensures URP pipeline is active when the game starts.
    /// If the build preprocessor (URPSetup) properly configured assets, this is a no-op.
    /// If somehow URP isn't set, this creates a minimal pipeline at runtime.
    /// </summary>
    public static class URPBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureURP()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                Debug.Log("[URPBootstrap] URP pipeline already active: " +
                    GraphicsSettings.currentRenderPipeline.name);
                return;
            }

            Debug.LogWarning("[URPBootstrap] No render pipeline active! Creating runtime URP...");

            // Create minimal URP renderer
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();

            // Create URP asset with renderer
            var urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            urpAsset.name = "RuntimeURPAsset";

            // Assign pipeline — this activates URP immediately
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            Debug.Log("[URPBootstrap] Runtime URP pipeline created and assigned. " +
                "Note: build preprocessor should handle this — check URPSetup.cs");
        }
    }
}

using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// God rays effect placeholder for URP migration.
    /// The old OnRenderImage-based implementation doesn't work with URP.
    /// TODO: Implement as URP ScriptableRendererFeature + ScriptableRenderPass.
    /// For now, this component does nothing — URP's bloom provides some light glow.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GodRaysEffect : MonoBehaviour
    {
        [Header("Ray Quality")]
        [SerializeField] private int numSamples = 64;
        [SerializeField] private float rayWeight = 0.75f;
        [SerializeField] private float rayDecay = 0.96f;
        [SerializeField] private float rayExposure = 1.0f;
        [SerializeField] private float rayThreshold = 0.65f;
        [SerializeField] private float rayIntensity = 1.3f;
        [SerializeField] private float maxIntensity = 1.5f;

        /// <summary>
        /// CloudRenderer calls this to modulate ray visibility with cloud density.
        /// </summary>
        public void SetCloudFactor(float factor) { }

        private void Awake()
        {
            Debug.Log("[GodRays] URP migration: god rays disabled pending ScriptableRendererFeature implementation. " +
                      "URP Bloom provides partial sun glow effect.");
        }
    }
}

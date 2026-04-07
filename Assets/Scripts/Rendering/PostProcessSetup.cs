using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Orlo.Rendering
{
    /// <summary>
    /// URP post-processing setup via Volume system.
    /// Creates a global Volume with bloom, color grading, vignette, tonemapping, and SSAO.
    /// Replaces the old OnRenderImage-based post-processing.
    /// Attach to the main camera (auto-attached by GameBootstrap).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostProcessSetup : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private float bloomThreshold = 0.7f;
        [SerializeField] private float bloomIntensity = 0.45f;

        [Header("Color Grading")]
        [SerializeField] private float warmth = 0.12f;
        [SerializeField] private float contrast = 1.08f;
        [SerializeField] private float saturation = 1.1f;
        [SerializeField] private float vignetteIntensity = 0.25f;

        private Volume _volume;
        private VolumeProfile _profile;

        private void Awake()
        {
            var cam = GetComponent<Camera>();
            cam.allowHDR = true;

            // Ensure camera has URP additional data for post-processing
            var urpCamData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (urpCamData == null)
                urpCamData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = true;

            SetupVolume();
        }

        private void SetupVolume()
        {
            // Create a global Volume on this GameObject
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100;

            // Create a runtime VolumeProfile
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = _profile;

            // --- Tonemapping (ACES Filmic for cinematic look) ---
            var tonemapping = _profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            // --- Bloom ---
            var bloom = _profile.Add<Bloom>(true);
            bloom.threshold.Override(bloomThreshold);
            bloom.intensity.Override(bloomIntensity);
            bloom.scatter.Override(0.7f);

            // --- Color Adjustments (replaces our custom color grading) ---
            var colorAdj = _profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.Override((contrast - 1f) * 100f);   // URP uses -100 to 100
            colorAdj.saturation.Override((saturation - 1f) * 100f); // URP uses -100 to 100
            // Warmth: shift post-exposure slightly and use color filter
            colorAdj.colorFilter.Override(new Color(1f, 1f - warmth * 0.3f, 1f - warmth * 0.6f));

            // --- White Balance (warm shift) ---
            var whiteBalance = _profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(warmth * 30f); // Positive = warmer

            // --- Vignette ---
            var vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.4f);

            // --- Film Grain (subtle cinematic texture) ---
            var filmGrain = _profile.Add<FilmGrain>(true);
            filmGrain.intensity.Override(0.15f);
            filmGrain.type.Override(FilmGrainLookup.Medium1);

            Debug.Log("[PostProcess] URP Volume post-processing configured: " +
                      $"Bloom(t={bloomThreshold},i={bloomIntensity}), " +
                      $"ACES Tonemapping, " +
                      $"ColorGrading(warmth={warmth},contrast={contrast},sat={saturation}), " +
                      $"Vignette({vignetteIntensity})");
        }

        /// <summary>
        /// Update bloom settings at runtime (e.g., for weather changes).
        /// </summary>
        public void SetBloom(float threshold, float intensity)
        {
            if (_profile != null && _profile.TryGet<Bloom>(out var bloom))
            {
                bloom.threshold.Override(threshold);
                bloom.intensity.Override(intensity);
            }
        }

        /// <summary>
        /// Update color grading at runtime (e.g., for time-of-day changes).
        /// </summary>
        public void SetColorGrading(float newWarmth, float newContrast, float newSaturation)
        {
            if (_profile != null)
            {
                if (_profile.TryGet<ColorAdjustments>(out var colorAdj))
                {
                    colorAdj.contrast.Override((newContrast - 1f) * 100f);
                    colorAdj.saturation.Override((newSaturation - 1f) * 100f);
                }
                if (_profile.TryGet<WhiteBalance>(out var wb))
                {
                    wb.temperature.Override(newWarmth * 30f);
                }
            }
        }

        private void OnDestroy()
        {
            if (_profile != null)
                DestroyImmediate(_profile);
        }
    }
}

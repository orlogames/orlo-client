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
        [SerializeField] private float bloomThreshold = 0.5f;
        [SerializeField] private float bloomIntensity = 0.8f;

        [Header("Color Grading")]
        [SerializeField] private float warmth = 0.2f;
        [SerializeField] private float contrast = 1.08f;
        [SerializeField] private float saturation = 1.1f;
        [SerializeField] private float vignetteIntensity = 0.35f;

        [Header("Fog")]
        [SerializeField] private Color fogColorDay = new Color(0.6f, 0.5f, 0.35f, 1f);
        [SerializeField] private Color fogColorNight = new Color(0.03f, 0.03f, 0.06f, 1f);
        [SerializeField] private float fogDensity = 0.025f;

        [Header("Depth of Field")]
        [SerializeField] private bool enableDOF = true;
        [SerializeField] private float dofFocusDistance = 20f;

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
            bloom.scatter.Override(0.8f);

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

            // --- Fog (URP uses RenderSettings fog, controlled here) ---
            SetupFog();

            // --- Depth of Field (subtle background blur) ---
            if (enableDOF)
            {
                var dof = _profile.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Bokeh);
                dof.focusDistance.Override(dofFocusDistance);
                dof.focalLength.Override(50f);
                dof.aperture.Override(5.6f);
            }

            // --- Lift Gamma Gain (subtle warm shadows, cool highlights) ---
            var lgg = _profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.04f, 0.02f, -0.02f, 0f));   // Warm shadows
            lgg.gamma.Override(new Vector4(0.01f, 0.005f, -0.005f, 0f)); // Slight warm mid
            lgg.gain.Override(new Vector4(-0.01f, -0.01f, 0.02f, 0f));   // Cool highlights

            Debug.Log("[PostProcess] URP Volume configured: Bloom, ACES, ColorGrading, " +
                      $"Vignette, FilmGrain, Fog, LiftGammaGain" +
                      (enableDOF ? ", DOF" : ""));
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

        private void SetupFog()
        {
            // Unity fog works with URP via RenderSettings
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColorDay;
        }

        /// <summary>
        /// Update fog color for time-of-day transitions.
        /// Called by SkyboxController during time animation.
        /// </summary>
        public void SetFogForTimeOfDay(float timeOfDay)
        {
            // Interpolate fog color between day and night
            float nightness = 0f;
            if (timeOfDay < 0.15f)
                nightness = 1f - (timeOfDay / 0.15f);     // Night → dawn
            else if (timeOfDay > 0.85f)
                nightness = (timeOfDay - 0.85f) / 0.15f;  // Dusk → night

            RenderSettings.fogColor = Color.Lerp(fogColorDay, fogColorNight, nightness);
        }

        /// <summary>
        /// Set fog density for weather changes.
        /// Clear: 0.005, Normal: 0.012, Foggy: 0.03, Storm: 0.05
        /// </summary>
        public void SetFogDensity(float density)
        {
            RenderSettings.fogDensity = density;
        }

        private void OnDestroy()
        {
            if (_profile != null)
                DestroyImmediate(_profile);
        }
    }
}

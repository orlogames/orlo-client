using UnityEngine;
using UnityEngine.Rendering;

namespace Orlo.UI.Settings
{
    /// <summary>
    /// Singleton MonoBehaviour that owns the current GameSettings instance.
    /// Loads on Awake, applies settings to Unity subsystems, and saves on demand.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        public GameSettings Current { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        // ── Public API ─────────────────────────────────────────────────

        public void LoadSettings()
        {
            Current = GameSettings.Load();
            ApplyAll();
        }

        public void SaveSettings()
        {
            Current.Save();
        }

        public void ApplyAll()
        {
            ApplyGraphics();
            ApplyAudio();
            ApplyControls();
        }

        public void ApplyGraphics()
        {
            // Resolution & display mode
            Resolution[] resolutions = Screen.resolutions;
            int resIdx = Current.resolutionIndex;
            if (resIdx < 0 || resIdx >= resolutions.Length)
                resIdx = resolutions.Length - 1;

            Resolution res = resolutions[resIdx];

            FullScreenMode fsMode;
            switch (Current.displayMode)
            {
                case DisplayMode.Fullscreen: fsMode = FullScreenMode.ExclusiveFullScreen; break;
                case DisplayMode.Borderless: fsMode = FullScreenMode.FullScreenWindow; break;
                case DisplayMode.Windowed:   fsMode = FullScreenMode.Windowed; break;
                default:                     fsMode = FullScreenMode.ExclusiveFullScreen; break;
            }

            Screen.SetResolution(res.width, res.height, fsMode);

            // VSync
            QualitySettings.vSyncCount = Current.vSync ? 1 : 0;

            // FPS cap
            switch (Current.fpsCap)
            {
                case FpsCap.Unlimited: Application.targetFrameRate = -1;  break;
                case FpsCap.Cap144:    Application.targetFrameRate = 144; break;
                case FpsCap.Cap120:    Application.targetFrameRate = 120; break;
                case FpsCap.Cap60:     Application.targetFrameRate = 60;  break;
                case FpsCap.Cap30:     Application.targetFrameRate = 30;  break;
            }

            // Quality preset (maps to Unity quality levels if available)
            int qualityLevel = Current.qualityPreset switch
            {
                QualityPreset.Ultra  => Mathf.Min(3, QualitySettings.names.Length - 1),
                QualityPreset.High   => Mathf.Min(2, QualitySettings.names.Length - 1),
                QualityPreset.Medium => Mathf.Min(1, QualitySettings.names.Length - 1),
                QualityPreset.Low    => 0,
                _                    => QualitySettings.GetQualityLevel()
            };
            QualitySettings.SetQualityLevel(qualityLevel, true);

            // Shadow quality
            switch (Current.shadowQuality)
            {
                case ShadowQualitySetting.Ultra:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 200f;
                    break;
                case ShadowQualitySetting.High:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowDistance = 150f;
                    break;
                case ShadowQualitySetting.Medium:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowDistance = 80f;
                    break;
                case ShadowQualitySetting.Low:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 40f;
                    break;
                case ShadowQualitySetting.Off:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    break;
            }

            Debug.Log($"[SettingsManager] Graphics applied — {Current.displayMode} {res.width}x{res.height}, " +
                      $"VSync={Current.vSync}, FPS={Current.fpsCap}, Shadows={Current.shadowQuality}");
        }

        public void ApplyAudio()
        {
            // Master volume controls the AudioListener global volume
            AudioListener.volume = Current.masterVolume / 100f;

            // Individual channel volumes would be applied through an AudioMixer in production.
            // For now we log the intent — the AudioMixer hookup happens when the mixer asset exists.
            Debug.Log($"[SettingsManager] Audio applied — Master={Current.masterVolume}, " +
                      $"Music={Current.musicVolume}, SFX={Current.sfxVolume}, " +
                      $"Ambient={Current.ambientVolume}, Voice={Current.voiceChatVolume}");
        }

        public void ApplyControls()
        {
            // Mouse sensitivity and invert-Y are read directly from Current by the player controller.
            Debug.Log($"[SettingsManager] Controls applied — Sensitivity={Current.mouseSensitivity:F2}, InvertY={Current.invertY}");
        }

        public void ApplyNetwork()
        {
            // Network overlay toggles are read directly by the HUD.
            Debug.Log($"[SettingsManager] Network display applied — Ping={Current.showPing}, FPS={Current.showFps}, Stats={Current.showNetworkStats}");
        }

        public void ApplySocial()
        {
            Debug.Log($"[SettingsManager] Social applied — ChatFilter={Current.chatFilter}, " +
                      $"Proximity={Current.proximityChat}, PartyInvites={Current.allowPartyInvites}, " +
                      $"Whispers={Current.allowWhispers}, Online={Current.showOnlineStatus}");
        }

        /// <summary>
        /// Returns the preferred Graphics API as a launch argument string.
        /// The launcher should read this and pass it when starting the game.
        /// </summary>
        public string GetGraphicsApiLaunchArg()
        {
            return Current.graphicsApi switch
            {
                GraphicsApi.Vulkan    => "-force-vulkan",
                GraphicsApi.DirectX12 => "-force-d3d12",
                GraphicsApi.DirectX11 => "-force-d3d11",
                _                     => ""
            };
        }
    }
}

using System;
using UnityEngine;

namespace Orlo.UI.Settings
{
    /// <summary>
    /// Serializable settings data class. Persisted as JSON via PlayerPrefs.
    /// All fields have sensible defaults for first launch.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        private const string PlayerPrefsKey = "OrloGameSettings";

        // ── Graphics ────────────────────────────────────────────────────
        public GraphicsApi graphicsApi = GraphicsApi.Vulkan;
        public int resolutionIndex = -1; // -1 = current/native
        public DisplayMode displayMode = DisplayMode.Fullscreen;
        public QualityPreset qualityPreset = QualityPreset.High;
        public bool vSync = true;
        public FpsCap fpsCap = FpsCap.Unlimited;
        public int renderDistanceChunks = 16;
        public ShadowQualitySetting shadowQuality = ShadowQualitySetting.High;
        public AntiAliasingSetting antiAliasing = AntiAliasingSetting.TAA;
        public int vegetationDensity = 75; // 0-100

        // ── Audio ───────────────────────────────────────────────────────
        public int masterVolume = 80;
        public int musicVolume = 60;
        public int sfxVolume = 80;
        public int ambientVolume = 50;
        public int voiceChatVolume = 70;

        // ── Network ────────────────────────────────────────────────────
        public bool showPing = true;
        public bool showFps = false;
        public bool showNetworkStats = false;

        // ── Social ─────────────────────────────────────────────────────
        public bool chatFilter = true;
        public bool proximityChat = true;
        public SocialFilter allowPartyInvites = SocialFilter.Everyone;
        public SocialFilter allowWhispers = SocialFilter.Everyone;
        public bool showOnlineStatus = true;

        // ── Controls ───────────────────────────────────────────────────
        public float mouseSensitivity = 1.0f;
        public bool invertY = false;

        // ── Gameplay ───────────────────────────────────────────────────
        public bool showDamageNumbers = true;
        public bool showEntityNames = true;
        public bool autoLoot = false;
        public bool screenShake = true;

        // ── Accessibility ──────────────────────────────────────────────
        public int colorblindMode = 0;          // 0=Normal, 1=Protanopia, 2=Deuteranopia, 3=Tritanopia
        public float uiScale = 1.0f;            // 0.75 - 2.0
        public float fontSizeMultiplier = 1.0f;  // 0.75 - 2.0
        public bool flashEffects = true;

        // ── Persistence ────────────────────────────────────────────────

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public static GameSettings FromJson(string json)
        {
            var settings = new GameSettings();
            JsonUtility.FromJsonOverwrite(json, settings);
            return settings;
        }

        public void Save()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, ToJson());
            PlayerPrefs.Save();
            Debug.Log("[GameSettings] Settings saved.");
        }

        public static GameSettings Load()
        {
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                string json = PlayerPrefs.GetString(PlayerPrefsKey);
                try
                {
                    var settings = FromJson(json);
                    Debug.Log("[GameSettings] Settings loaded from PlayerPrefs.");
                    return settings;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameSettings] Failed to parse saved settings, using defaults: {e.Message}");
                }
            }

            Debug.Log("[GameSettings] No saved settings found, using defaults.");
            return new GameSettings();
        }
    }

    // ── Enums ───────────────────────────────────────────────────────────

    public enum GraphicsApi
    {
        Vulkan,
        DirectX12,
        DirectX11
    }

    public enum DisplayMode
    {
        Fullscreen,
        Borderless,
        Windowed
    }

    public enum QualityPreset
    {
        Ultra,
        High,
        Medium,
        Low,
        Custom
    }

    public enum FpsCap
    {
        Unlimited,
        Cap144,
        Cap120,
        Cap60,
        Cap30
    }

    public enum ShadowQualitySetting
    {
        Ultra,
        High,
        Medium,
        Low,
        Off
    }

    public enum AntiAliasingSetting
    {
        TAA,
        FXAA,
        Off
    }

    public enum SocialFilter
    {
        Everyone,
        Friends,
        Nobody
    }
}

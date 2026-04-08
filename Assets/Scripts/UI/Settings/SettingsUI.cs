using System;
using UnityEngine;
using Orlo.UI;

namespace Orlo.UI.Settings
{
    /// <summary>
    /// In-game settings panel with 5 tabs.
    /// Uses OnGUI for rapid prototyping — consistent with other Orlo UI scripts.
    /// Toggle with F10 or call Toggle() from the main menu.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private enum Tab { Graphics, Audio, Network, Social, Controls, Gameplay, Access }

        private bool _visible;
        private Tab _activeTab = Tab.Graphics;
        private Vector2 _scrollPos;

        // Cached resolution list
        private Resolution[] _resolutions;
        private string[] _resolutionLabels;

        // Graphics API change tracking
        private GraphicsApi _initialGraphicsApi;
        private bool _graphicsApiChanged;

        // Shortcut ref
        private SettingsManager Mgr => SettingsManager.Instance;
        private GameSettings S => Mgr != null ? Mgr.Current : null;

        // ── Layout constants ────────────────────────────────────────────
        private const float WinW = 700f;
        private const float WinH = 520f;
        private const float TabBarH = 30f;
        private const float PaddingX = 16f;
        private const float RowH = 28f;
        private const float LabelW = 200f;
        private const float ControlW = 260f;

        private void Awake()
        {
            RefreshResolutionList();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                Toggle();
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible)
            {
                RefreshResolutionList();
                _initialGraphicsApi = S != null ? S.graphicsApi : GraphicsApi.Vulkan;
                _graphicsApiChanged = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Save on close
                if (Mgr != null) Mgr.SaveSettings();
            }
        }

        public bool IsVisible => _visible;

        public void Show()
        {
            _visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            _visible = false;
            if (Mgr != null) Mgr.SaveSettings();
        }

        // ── Resolution helpers ──────────────────────────────────────────

        private void RefreshResolutionList()
        {
            _resolutions = Screen.resolutions;
            _resolutionLabels = new string[_resolutions.Length];
            for (int i = 0; i < _resolutions.Length; i++)
            {
                var r = _resolutions[i];
                _resolutionLabels[i] = $"{r.width} x {r.height} @ {r.refreshRateRatio.value:F0}Hz";
            }
        }

        // ── OnGUI ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible || S == null) return;

            float x = (Screen.width - WinW) / 2f;
            float y = (Screen.height - WinH) / 2f;

            // Dim overlay
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Window background
            GUI.color = new Color(0.12f, 0.12f, 0.16f, 0.97f);
            GUI.DrawTexture(new Rect(x, y, WinW, WinH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(x, y, WinW, 32), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(x + 12, y, 200, 32), "Settings", titleStyle);

            // Close button
            if (GUI.Button(new Rect(x + WinW - 36, y + 4, 28, 24), "X"))
            {
                _visible = false;
                if (Mgr != null) Mgr.SaveSettings();
            }

            float contentY = y + 34;

            // ── Tab bar ─────────────────────────────────────────────────
            DrawTabBar(x, contentY);
            contentY += TabBarH + 4;

            // ── Tab content ─────────────────────────────────────────────
            Rect contentRect = new Rect(x + 2, contentY, WinW - 4, WinH - (contentY - y) - 4);
            GUI.color = new Color(0.14f, 0.14f, 0.18f, 1f);
            GUI.DrawTexture(contentRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(contentRect.x + PaddingX, contentRect.y + 8,
                                          contentRect.width - PaddingX * 2, contentRect.height - 16));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            switch (_activeTab)
            {
                case Tab.Graphics: DrawGraphicsTab(); break;
                case Tab.Audio:    DrawAudioTab();    break;
                case Tab.Network:  DrawNetworkTab();  break;
                case Tab.Social:   DrawSocialTab();   break;
                case Tab.Controls: DrawControlsTab(); break;
                case Tab.Gameplay: DrawGameplayTab(); break;
                case Tab.Access:   DrawAccessibilityTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ── Tab bar ─────────────────────────────────────────────────────

        private void DrawTabBar(float x, float y)
        {
            Tab[] tabs = (Tab[])Enum.GetValues(typeof(Tab));
            float tabW = WinW / tabs.Length;

            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = _activeTab == tabs[i];
                Rect tabRect = new Rect(x + i * tabW, y, tabW, TabBarH);

                GUI.color = active
                    ? new Color(0.2f, 0.25f, 0.4f, 1f)
                    : new Color(0.1f, 0.1f, 0.14f, 1f);
                GUI.DrawTexture(tabRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var tabStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = active ? Color.white : new Color(0.6f, 0.6f, 0.6f) }
                };

                if (GUI.Button(tabRect, tabs[i].ToString(), tabStyle))
                {
                    _activeTab = tabs[i];
                    _scrollPos = Vector2.zero;
                }
            }
        }

        // ── Graphics tab ────────────────────────────────────────────────

        private void DrawGraphicsTab()
        {
            SectionHeader("Display");

            // Graphics API
            int apiIdx = (int)S.graphicsApi;
            int newApiIdx = DrawDropdown("Graphics API", apiIdx, new[] { "Vulkan", "DirectX 12", "DirectX 11" });
            if (newApiIdx != apiIdx)
            {
                S.graphicsApi = (GraphicsApi)newApiIdx;
                _graphicsApiChanged = S.graphicsApi != _initialGraphicsApi;
            }

            if (_graphicsApiChanged)
            {
                var noteStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, fontStyle = FontStyle.Italic,
                    normal = { textColor = Color.yellow },
                    wordWrap = true
                };
                GUILayout.Label("Graphics API change requires a game relaunch to take effect.", noteStyle);
                GUILayout.Space(4);
            }

            // Resolution
            int resIdx = S.resolutionIndex;
            if (resIdx < 0 || resIdx >= _resolutionLabels.Length)
                resIdx = _resolutionLabels.Length - 1;
            int newResIdx = DrawDropdown("Resolution", resIdx, _resolutionLabels);
            if (newResIdx != resIdx)
            {
                S.resolutionIndex = newResIdx;
                Mgr.ApplyGraphics();
            }

            // Display mode
            int dmIdx = (int)S.displayMode;
            int newDmIdx = DrawDropdown("Display Mode", dmIdx, new[] { "Fullscreen", "Borderless", "Windowed" });
            if (newDmIdx != dmIdx)
            {
                S.displayMode = (DisplayMode)newDmIdx;
                Mgr.ApplyGraphics();
            }

            SectionHeader("Quality");

            // Quality preset
            int qIdx = (int)S.qualityPreset;
            int newQIdx = DrawDropdown("Quality Preset", qIdx, new[] { "Ultra", "High", "Medium", "Low", "Custom" });
            if (newQIdx != qIdx)
            {
                S.qualityPreset = (QualityPreset)newQIdx;
                Mgr.ApplyGraphics();
            }

            // VSync
            bool newVSync = DrawToggle("VSync", S.vSync);
            if (newVSync != S.vSync)
            {
                S.vSync = newVSync;
                Mgr.ApplyGraphics();
            }

            // FPS cap
            int fpsIdx = (int)S.fpsCap;
            int newFpsIdx = DrawDropdown("FPS Cap", fpsIdx, new[] { "Unlimited", "144", "120", "60", "30" });
            if (newFpsIdx != fpsIdx)
            {
                S.fpsCap = (FpsCap)newFpsIdx;
                Mgr.ApplyGraphics();
            }

            // Render distance
            int newRd = DrawSliderInt("Render Distance", S.renderDistanceChunks, 4, 32, "chunks");
            if (newRd != S.renderDistanceChunks)
            {
                S.renderDistanceChunks = newRd;
            }

            // Shadow quality
            int shIdx = (int)S.shadowQuality;
            int newShIdx = DrawDropdown("Shadow Quality", shIdx, new[] { "Ultra", "High", "Medium", "Low", "Off" });
            if (newShIdx != shIdx)
            {
                S.shadowQuality = (ShadowQualitySetting)newShIdx;
                S.qualityPreset = QualityPreset.Custom;
                Mgr.ApplyGraphics();
            }

            // Anti-aliasing
            int aaIdx = (int)S.antiAliasing;
            int newAaIdx = DrawDropdown("Anti-Aliasing", aaIdx, new[] { "TAA", "FXAA", "Off" });
            if (newAaIdx != aaIdx)
            {
                S.antiAliasing = (AntiAliasingSetting)newAaIdx;
                S.qualityPreset = QualityPreset.Custom;
            }

            // Vegetation density
            int newVeg = DrawSliderInt("Vegetation Density", S.vegetationDensity, 0, 100, "%");
            if (newVeg != S.vegetationDensity)
            {
                S.vegetationDensity = newVeg;
                S.qualityPreset = QualityPreset.Custom;
            }
        }

        // ── Audio tab ───────────────────────────────────────────────────

        private void DrawAudioTab()
        {
            SectionHeader("Volume");

            S.masterVolume = DrawSliderInt("Master Volume", S.masterVolume, 0, 100, "%");
            S.musicVolume = DrawSliderInt("Music Volume", S.musicVolume, 0, 100, "%");
            S.sfxVolume = DrawSliderInt("SFX Volume", S.sfxVolume, 0, 100, "%");
            S.ambientVolume = DrawSliderInt("Ambient Volume", S.ambientVolume, 0, 100, "%");
            S.voiceChatVolume = DrawSliderInt("Voice Chat Volume", S.voiceChatVolume, 0, 100, "%");

            SectionHeader("Options");

            bool newMute = DrawToggle("Mute When Minimized", PlayerPrefs.GetInt("MuteMinimized", 1) == 1);
            PlayerPrefs.SetInt("MuteMinimized", newMute ? 1 : 0);

            bool newCombatMusic = DrawToggle("Combat Music", PlayerPrefs.GetInt("CombatMusic", 1) == 1);
            PlayerPrefs.SetInt("CombatMusic", newCombatMusic ? 1 : 0);

            // Apply master volume immediately
            Mgr.ApplyAudio();
        }

        // ── Network tab ─────────────────────────────────────────────────

        private void DrawNetworkTab()
        {
            SectionHeader("Overlays");

            S.showPing = DrawToggle("Show Ping", S.showPing);
            S.showFps = DrawToggle("Show FPS", S.showFps);
            S.showNetworkStats = DrawToggle("Show Network Stats", S.showNetworkStats);

            Mgr.ApplyNetwork();
        }

        // ── Social tab ──────────────────────────────────────────────────

        private void DrawSocialTab()
        {
            SectionHeader("Chat");

            S.chatFilter = DrawToggle("Profanity Filter", S.chatFilter);
            S.proximityChat = DrawToggle("Proximity Chat", S.proximityChat);

            int bubbleIdx = PlayerPrefs.GetInt("ChatBubbles", 0);
            int newBubbleIdx = DrawDropdown("Chat Bubbles", bubbleIdx, new[] { "All", "Party Only", "Off" });
            if (newBubbleIdx != bubbleIdx) PlayerPrefs.SetInt("ChatBubbles", newBubbleIdx);

            SectionHeader("Privacy");

            int wIdx = (int)S.allowWhispers;
            int newWIdx = DrawDropdown("Who Can Whisper", wIdx, new[] { "Anyone", "Friends", "Nobody" });
            if (newWIdx != wIdx) S.allowWhispers = (SocialFilter)newWIdx;

            int piIdx = (int)S.allowPartyInvites;
            int newPiIdx = DrawDropdown("Who Can Invite", piIdx, new[] { "Anyone", "Friends+Guild", "Friends", "Nobody" });
            if (newPiIdx != piIdx) S.allowPartyInvites = (SocialFilter)newPiIdx;

            int tradeIdx = PlayerPrefs.GetInt("AllowTrade", 0);
            int newTradeIdx = DrawDropdown("Who Can Trade", tradeIdx, new[] { "Anyone", "Friends", "Nobody" });
            if (newTradeIdx != tradeIdx) PlayerPrefs.SetInt("AllowTrade", newTradeIdx);

            int inspectIdx = PlayerPrefs.GetInt("AllowInspect", 0);
            int newInspectIdx = DrawDropdown("Who Can Inspect", inspectIdx, new[] { "Anyone", "Friends", "Nobody" });
            if (newInspectIdx != inspectIdx) PlayerPrefs.SetInt("AllowInspect", newInspectIdx);

            SectionHeader("Display");

            S.showOnlineStatus = DrawToggle("Show Online Status", S.showOnlineStatus);

            int nameIdx = PlayerPrefs.GetInt("ShowNames", 0);
            int newNameIdx = DrawDropdown("Show Player Names", nameIdx, new[] { "All", "Friends+Guild", "Party", "Off" });
            if (newNameIdx != nameIdx) PlayerPrefs.SetInt("ShowNames", newNameIdx);

            bool showTitles = DrawToggle("Show Titles", PlayerPrefs.GetInt("ShowTitles", 1) == 1);
            PlayerPrefs.SetInt("ShowTitles", showTitles ? 1 : 0);

            bool showGuildTags = DrawToggle("Show Guild Tags", PlayerPrefs.GetInt("ShowGuildTags", 1) == 1);
            PlayerPrefs.SetInt("ShowGuildTags", showGuildTags ? 1 : 0);

            bool blockMail = DrawToggle("Block Stranger Mail", PlayerPrefs.GetInt("BlockStrangerMail", 0) == 1);
            PlayerPrefs.SetInt("BlockStrangerMail", blockMail ? 1 : 0);

            Mgr.ApplySocial();

            // Sync to server
            Network.NetworkManager.Instance?.Send(
                Network.PacketBuilder.SettingsSyncRequest());
        }

        // ── Controls tab ────────────────────────────────────────────────

        private void DrawControlsTab()
        {
            SectionHeader("Mouse");

            float newSens = DrawSliderFloat("Mouse Sensitivity", S.mouseSensitivity, 0.1f, 5.0f);
            if (Mathf.Abs(newSens - S.mouseSensitivity) > 0.001f)
            {
                S.mouseSensitivity = newSens;
                Mgr.ApplyControls();
            }

            bool newInvert = DrawToggle("Invert Y Axis", S.invertY);
            if (newInvert != S.invertY)
            {
                S.invertY = newInvert;
                Mgr.ApplyControls();
            }

            SectionHeader("Key Bindings");

            if (Orlo.UI.KeybindingUI.Instance != null)
                Orlo.UI.KeybindingUI.Instance.DrawKeybindingPanel();
            else
            {
                var placeholderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                    alignment = TextAnchor.MiddleLeft
                };
                GUILayout.Label("Key bindings initializing...", placeholderStyle);
            }
        }

        // ── Gameplay tab ────────────────────────────────────────────────

        private void DrawGameplayTab()
        {
            SectionHeader("Combat");

            S.showDamageNumbers = DrawToggle("Show Damage Numbers", S.showDamageNumbers);
            S.screenShake = DrawToggle("Screen Shake", S.screenShake);

            bool targetOfTarget = DrawToggle("Target of Target", PlayerPrefs.GetInt("TargetOfTarget", 0) == 1);
            PlayerPrefs.SetInt("TargetOfTarget", targetOfTarget ? 1 : 0);

            bool actionBarLock = DrawToggle("Action Bar Lock", PlayerPrefs.GetInt("ActionBarLock", 0) == 1);
            PlayerPrefs.SetInt("ActionBarLock", actionBarLock ? 1 : 0);

            SectionHeader("Interface");

            S.showEntityNames = DrawToggle("Show Entity Names", S.showEntityNames);
            S.autoLoot = DrawToggle("Auto-Loot", S.autoLoot);

            int lootMode = PlayerPrefs.GetInt("LootMode", 0);
            int newLootMode = DrawDropdown("Loot Mode", lootMode, new[] { "Auto", "Manual", "Area" });
            if (newLootMode != lootMode) PlayerPrefs.SetInt("LootMode", newLootMode);

            int nameDistIdx = PlayerPrefs.GetInt("NameDistance", 1);
            int newNameDist = DrawDropdown("Name Display Distance", nameDistIdx, new[] { "Near", "Medium", "Far" });
            if (newNameDist != nameDistIdx) PlayerPrefs.SetInt("NameDistance", newNameDist);

            int clockFmt = PlayerPrefs.GetInt("ClockFormat", 0);
            int newClockFmt = DrawDropdown("Clock Format", clockFmt, new[] { "12h", "24h" });
            if (newClockFmt != clockFmt) PlayerPrefs.SetInt("ClockFormat", newClockFmt);

            SectionHeader("Visuals");

            bool showHelmet = DrawToggle("Show Helmet", PlayerPrefs.GetInt("ShowHelmet", 1) == 1);
            PlayerPrefs.SetInt("ShowHelmet", showHelmet ? 1 : 0);

            bool showCloak = DrawToggle("Show Cloak", PlayerPrefs.GetInt("ShowCloak", 1) == 1);
            PlayerPrefs.SetInt("ShowCloak", showCloak ? 1 : 0);

            bool clickToMove = DrawToggle("Click-to-Move", PlayerPrefs.GetInt("ClickToMove", 0) == 1);
            PlayerPrefs.SetInt("ClickToMove", clickToMove ? 1 : 0);
        }

        // ── Accessibility tab ───────────────────────────────────────────

        private void DrawAccessibilityTab()
        {
            SectionHeader("Vision");

            // Colorblind mode dropdown
            int cbIdx = S.colorblindMode;
            int newCbIdx = DrawDropdown("Colorblind Mode", cbIdx,
                new[] { "Normal", "Protanopia (Red-blind)", "Deuteranopia (Green-blind)", "Tritanopia (Blue-blind)" });
            if (newCbIdx != cbIdx)
            {
                S.colorblindMode = newCbIdx;
                if (AccessibilityManager.Instance != null)
                    AccessibilityManager.Instance.ColorblindMode = (ColorblindMode)newCbIdx;
            }

            // Flash effects toggle
            bool newFlash = DrawToggle("Flash Effects", S.flashEffects);
            if (newFlash != S.flashEffects)
            {
                S.flashEffects = newFlash;
                if (AccessibilityManager.Instance != null)
                    AccessibilityManager.Instance.FlashEffectsEnabled = newFlash;
            }

            SectionHeader("Interface Scaling");

            // UI scale slider
            float newScale = DrawSliderFloat("UI Scale", S.uiScale, 0.75f, 2.0f);
            if (Mathf.Abs(newScale - S.uiScale) > 0.001f)
            {
                S.uiScale = newScale;
                if (AccessibilityManager.Instance != null)
                    AccessibilityManager.Instance.UIScale = newScale;
            }

            // Font size multiplier slider
            float newFont = DrawSliderFloat("Font Size", S.fontSizeMultiplier, 0.75f, 2.0f);
            if (Mathf.Abs(newFont - S.fontSizeMultiplier) > 0.001f)
            {
                S.fontSizeMultiplier = newFont;
                if (AccessibilityManager.Instance != null)
                    AccessibilityManager.Instance.FontSizeMultiplier = newFont;
            }

            SectionHeader("Combat");

            // Screen shake (also in Gameplay, mirrored here for convenience)
            bool newShake = DrawToggle("Screen Shake", S.screenShake);
            if (newShake != S.screenShake)
            {
                S.screenShake = newShake;
            }

            SectionHeader("Motion & Feedback");

            bool reducedMotion = DrawToggle("Reduced Motion", PlayerPrefs.GetInt("ReducedMotion", 0) == 1);
            PlayerPrefs.SetInt("ReducedMotion", reducedMotion ? 1 : 0);

            bool highContrast = DrawToggle("High Contrast", PlayerPrefs.GetInt("HighContrast", 0) == 1);
            PlayerPrefs.SetInt("HighContrast", highContrast ? 1 : 0);

            bool subtitles = DrawToggle("Subtitles", PlayerPrefs.GetInt("Subtitles", 1) == 1);
            PlayerPrefs.SetInt("Subtitles", subtitles ? 1 : 0);

            bool dmgNumbers = DrawToggle("Damage Numbers", S.showDamageNumbers);
            if (dmgNumbers != S.showDamageNumbers) S.showDamageNumbers = dmgNumbers;

            // Color preview section
            SectionHeader("Color Preview");
            DrawColorPreview();
        }

        private void DrawColorPreview()
        {
            var am = AccessibilityManager.Instance;
            if (am == null) return;

            float swatchW = 40f;
            float swatchH = 18f;
            float gap = 6f;

            GUILayout.BeginHorizontal();

            // Health
            DrawColorSwatch("VIT", am.RemapColor(new Color(0.85f, 0.15f, 0.15f)), swatchW, swatchH);
            GUILayout.Space(gap);
            // Stamina
            DrawColorSwatch("STAM", am.RemapColor(new Color(0.15f, 0.75f, 0.25f)), swatchW, swatchH);
            GUILayout.Space(gap);
            // Focus
            DrawColorSwatch("FOC", am.RemapColor(new Color(0.25f, 0.45f, 0.95f)), swatchW, swatchH);
            GUILayout.Space(gap);
            // Enemy
            DrawColorSwatch("Enemy", am.RemapColor(new Color(1f, 0.4f, 0.4f)), swatchW, swatchH);
            GUILayout.Space(gap);
            // Friendly
            DrawColorSwatch("Ally", am.RemapColor(new Color(0.5f, 1f, 0.5f)), swatchW, swatchH);

            GUILayout.EndHorizontal();
        }

        private void DrawColorSwatch(string label, Color color, float w, float h)
        {
            GUILayout.BeginVertical(GUILayout.Width(w + 10));

            var lblStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUILayout.Label(label, lblStyle, GUILayout.Width(w + 10), GUILayout.Height(14));

            Rect r = GUILayoutUtility.GetRect(w, h);
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.EndVertical();
        }

        // ── Drawing helpers ─────────────────────────────────────────────

        private void SectionHeader(string text)
        {
            GUILayout.Space(8);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.8f, 1f) }
            };
            GUILayout.Label(text, style);

            // Separator line
            Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.Space(4);
        }

        private bool DrawToggle(string label, bool value)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowH));
            GUILayout.Label(label, SettingLabel(), GUILayout.Width(LabelW));
            bool result = GUILayout.Toggle(value, value ? "On" : "Off", GUILayout.Width(ControlW));
            GUILayout.EndHorizontal();
            return result;
        }

        private int DrawDropdown(string label, int selectedIndex, string[] options)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowH));
            GUILayout.Label(label, SettingLabel(), GUILayout.Width(LabelW));

            // Left arrow
            if (GUILayout.Button("<", GUILayout.Width(24), GUILayout.Height(22)))
            {
                selectedIndex = (selectedIndex - 1 + options.Length) % options.Length;
            }

            var valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUILayout.Label(options[Mathf.Clamp(selectedIndex, 0, options.Length - 1)],
                           valueStyle, GUILayout.Width(ControlW - 56), GUILayout.Height(22));

            // Right arrow
            if (GUILayout.Button(">", GUILayout.Width(24), GUILayout.Height(22)))
            {
                selectedIndex = (selectedIndex + 1) % options.Length;
            }

            GUILayout.EndHorizontal();
            return selectedIndex;
        }

        private int DrawSliderInt(string label, int value, int min, int max, string suffix = "")
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowH));
            GUILayout.Label(label, SettingLabel(), GUILayout.Width(LabelW));
            float newVal = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(ControlW - 60));
            int result = Mathf.RoundToInt(newVal);
            GUILayout.Label($"{result}{suffix}", ValueLabel(), GUILayout.Width(50));
            GUILayout.EndHorizontal();
            return result;
        }

        private float DrawSliderFloat(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowH));
            GUILayout.Label(label, SettingLabel(), GUILayout.Width(LabelW));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(ControlW - 60));
            result = Mathf.Round(result * 100f) / 100f; // 2 decimal places
            GUILayout.Label($"{result:F2}", ValueLabel(), GUILayout.Width(50));
            GUILayout.EndHorizontal();
            return result;
        }

        // ── Style helpers ───────────────────────────────────────────────

        private GUIStyle SettingLabel()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
        }

        private GUIStyle ValueLabel()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
        }
    }
}

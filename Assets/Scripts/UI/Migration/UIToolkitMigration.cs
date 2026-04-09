using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI.Migration
{
    /// <summary>
    /// Central registry tracking which UI components have been migrated from
    /// IMGUI (OnGUI) to UI Toolkit. During the incremental migration, each
    /// component checks this registry to decide whether to run its legacy
    /// OnGUI code or defer to the new UI Toolkit implementation.
    ///
    /// In the editor, migration flags are persisted via PlayerPrefs so
    /// developers can toggle individual components on/off for testing.
    /// In release builds, all flags are hardcoded to their shipped state.
    /// </summary>
    public static class UIToolkitMigration
    {
        private const string PrefsPrefix = "Orlo.UIMigration.";

        /// <summary>
        /// Every OnGUI-based UI script in the project, keyed by a stable
        /// component name. Default value is false (not yet migrated).
        /// </summary>
        private static readonly Dictionary<string, bool> _migrationStatus = new()
        {
            // Pre-game screens
            { "SplashScreen",               false },
            { "ConnectionStatusUI",         false },
            { "LoginUI",                    false },
            { "CharacterSelectUI",          false },
            { "CharacterCreationUI",        false },
            { "CharacterCreationManager",   false },
            { "LoadingScreen",              false },
            { "LoadingScreenUI",            false },

            // Lobby
            { "EnterWorldButton",           false },
            { "LobbyBackground",           false },
            { "NewsTicker",                 false },
            { "WelcomeOverlay",             false },

            // Core HUD
            { "GameHUD",                    false },
            { "CombatHUD",                  false },
            { "CombatBarUI",               false },
            { "CombatFeedback",            false },
            { "ChatUI",                     false },
            { "ChatBubbleManager",         false },
            { "MinimapUI",                  false },
            { "QuestTrackerHUD",           false },
            { "NotificationUI",             false },
            { "HUDLayout",                  false },
            { "ScreenEffects",              false },
            { "TooltipSystem",              false },
            { "ProgressiveDisclosure",     false },

            // Windows
            { "InventoryUI",                false },
            { "EquipmentUI",                false },
            { "CraftingUI",                 false },
            { "SkillTreeUI",                false },
            { "CharacterSheet",             false },
            { "QuestLogUI",                 false },
            { "QuestDialogUI",              false },
            { "GuildUI",                    false },
            { "FriendsUI",                  false },
            { "PartyUI",                    false },
            { "MailUI",                     false },
            { "ShopUI",                     false },
            { "SettingsUI",                 false },
            { "KeybindingUI",               false },
            { "TMDUI",                      false },
            { "EmoteUI",                    false },
            { "MainMenuUI",                 false },
            { "BulletinBoardUI",           false },
            { "LFGBoardUI",                false },
            { "LeaderboardUI",              false },
            { "SurveyUI",                   false },
            { "PlayerProfileUI",           false },
            { "GatheringUI",                false },
            { "AdminPanel",                 false },
            { "CreatureBrowserUI",         false },

            // Panels (sub-windows)
            { "CharacterPanelUI",          false },
            { "CombatPanelUI",             false },
            { "SkillsPanelUI",             false },
            { "QuestJournalUI",            false },

            // TMD overlays
            { "TMDUpgradeSystem",          false },
            { "DotGridOverlay",            false },
            { "PrecursorDetector",         false },
        };

        /// <summary>
        /// Returns true if the named component should use its UI Toolkit
        /// implementation instead of the legacy OnGUI path.
        /// Unknown component names return false (safe fallback to IMGUI).
        /// </summary>
        public static bool IsUsingUIToolkit(string componentName)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Release builds: use the hardcoded dictionary values directly.
            // Once a component is shipped as migrated, flip its default above.
            return _migrationStatus.TryGetValue(componentName, out bool v) && v;
#else
            // Editor / dev builds: check PlayerPrefs override first.
            if (PlayerPrefs.HasKey(PrefsPrefix + componentName))
                return PlayerPrefs.GetInt(PrefsPrefix + componentName, 0) == 1;

            return _migrationStatus.TryGetValue(componentName, out bool val) && val;
#endif
        }

        /// <summary>
        /// Toggle a component's migration state. In editor/dev builds this
        /// persists to PlayerPrefs so the setting survives domain reloads.
        /// </summary>
        public static void SetMigrated(string componentName, bool migrated)
        {
            _migrationStatus[componentName] = migrated;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlayerPrefs.SetInt(PrefsPrefix + componentName, migrated ? 1 : 0);
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// Returns a read-only snapshot of all registered components and
        /// their current migration state (respects PlayerPrefs overrides
        /// in editor builds).
        /// </summary>
        public static IReadOnlyDictionary<string, bool> GetAllStatus()
        {
            var snapshot = new Dictionary<string, bool>(_migrationStatus.Count);
            foreach (var kvp in _migrationStatus)
            {
                snapshot[kvp.Key] = IsUsingUIToolkit(kvp.Key);
            }
            return snapshot;
        }

        /// <summary>
        /// Resets all PlayerPrefs overrides back to the hardcoded defaults.
        /// Editor only.
        /// </summary>
        public static void ResetAllOverrides()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var key in _migrationStatus.Keys)
            {
                PlayerPrefs.DeleteKey(PrefsPrefix + key);
            }
            PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// Returns the total number of registered components.
        /// </summary>
        public static int TotalComponents => _migrationStatus.Count;

        /// <summary>
        /// Returns how many components are currently flagged as migrated.
        /// </summary>
        public static int MigratedCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _migrationStatus)
                {
                    if (IsUsingUIToolkit(kvp.Key)) count++;
                }
                return count;
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Orlo.UI.Migration;

namespace Orlo.Editor
{
    /// <summary>
    /// Editor window showing the migration status of every OnGUI-based UI
    /// component. Accessible via Window > Orlo > UI Migration Status.
    ///
    /// Features:
    ///   - Checklist of all registered OnGUI scripts with toggle switches
    ///   - Color-coded status (green = migrated, red = legacy IMGUI)
    ///   - Progress bar showing overall migration completion
    ///   - Search filter
    ///   - Category grouping matching the migration phases
    ///   - Reset button to clear all PlayerPrefs overrides
    /// </summary>
    public class UIToolkitMigrationWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private string _searchFilter = "";
        private bool _showOnlyLegacy;
        private bool _showOnlyMigrated;

        // Category groupings for visual organization
        private static readonly (string Category, string[] Components)[] Categories = new[]
        {
            ("Pre-Game Screens", new[] {
                "SplashScreen", "ConnectionStatusUI", "LoginUI",
                "CharacterSelectUI", "CharacterCreationUI", "CharacterCreationManager",
                "LoadingScreen", "LoadingScreenUI"
            }),
            ("Lobby", new[] {
                "EnterWorldButton", "LobbyBackground", "NewsTicker", "WelcomeOverlay"
            }),
            ("Core HUD", new[] {
                "GameHUD", "CombatHUD", "CombatBarUI", "CombatFeedback",
                "ChatUI", "ChatBubbleManager", "MinimapUI", "QuestTrackerHUD",
                "NotificationUI", "HUDLayout", "ScreenEffects", "TooltipSystem",
                "ProgressiveDisclosure"
            }),
            ("Windows", new[] {
                "InventoryUI", "EquipmentUI", "CraftingUI", "SkillTreeUI",
                "CharacterSheet", "QuestLogUI", "QuestDialogUI", "GuildUI",
                "FriendsUI", "PartyUI", "MailUI", "ShopUI", "SettingsUI",
                "KeybindingUI", "TMDUI", "EmoteUI", "MainMenuUI",
                "BulletinBoardUI", "LFGBoardUI", "LeaderboardUI", "SurveyUI",
                "PlayerProfileUI", "GatheringUI", "AdminPanel", "CreatureBrowserUI"
            }),
            ("Panels", new[] {
                "CharacterPanelUI", "CombatPanelUI", "SkillsPanelUI", "QuestJournalUI"
            }),
            ("TMD Overlays", new[] {
                "TMDUpgradeSystem", "DotGridOverlay", "PrecursorDetector"
            }),
        };

        [MenuItem("Window/Orlo/UI Migration Status")]
        public static void ShowWindow()
        {
            var window = GetWindow<UIToolkitMigrationWindow>("UI Migration");
            window.minSize = new Vector2(380, 500);
        }

        private void OnGUI()
        {
            var allStatus = UIToolkitMigration.GetAllStatus();
            int total = UIToolkitMigration.TotalComponents;
            int migrated = UIToolkitMigration.MigratedCount;
            float progress = total > 0 ? (float)migrated / total : 0f;

            // Header
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("IMGUI to UI Toolkit Migration", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Progress bar
            Rect progressRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.ProgressBar(progressRect, progress,
                $"{migrated} / {total} migrated ({progress * 100f:F0}%)");
            EditorGUILayout.Space(4);

            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _searchFilter = EditorGUILayout.TextField(_searchFilter,
                EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));

            GUILayout.FlexibleSpace();

            bool wasLegacy = _showOnlyLegacy;
            _showOnlyLegacy = GUILayout.Toggle(_showOnlyLegacy, "Legacy Only",
                EditorStyles.toolbarButton, GUILayout.Width(80));
            if (_showOnlyLegacy && !wasLegacy) _showOnlyMigrated = false;

            bool wasMigrated = _showOnlyMigrated;
            _showOnlyMigrated = GUILayout.Toggle(_showOnlyMigrated, "Migrated Only",
                EditorStyles.toolbarButton, GUILayout.Width(90));
            if (_showOnlyMigrated && !wasMigrated) _showOnlyLegacy = false;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Component list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var (category, components) in Categories)
            {
                var filtered = FilterComponents(components, allStatus);
                if (filtered.Count == 0) continue;

                // Category header with migration count
                int catMigrated = filtered.Count(c => allStatus.TryGetValue(c, out bool v) && v);
                Color catColor = catMigrated == filtered.Count
                    ? new Color(0.3f, 0.8f, 0.3f)
                    : catMigrated > 0
                        ? new Color(0.9f, 0.75f, 0.2f)
                        : new Color(0.7f, 0.7f, 0.7f);

                EditorGUILayout.Space(4);
                var headerStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold
                };
                GUI.contentColor = catColor;
                EditorGUILayout.LabelField($"{category}  ({catMigrated}/{filtered.Count})",
                    EditorStyles.boldLabel);
                GUI.contentColor = Color.white;

                EditorGUI.indentLevel++;

                foreach (string comp in filtered)
                {
                    bool isMigrated = allStatus.TryGetValue(comp, out bool v) && v;
                    DrawComponentRow(comp, isMigrated);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();

            // Footer buttons
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset All Overrides", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Reset Migration Overrides",
                    "This will clear all PlayerPrefs migration overrides and revert " +
                    "to the hardcoded defaults. Continue?", "Reset", "Cancel"))
                {
                    UIToolkitMigration.ResetAllOverrides();
                    Repaint();
                }
            }

            if (GUILayout.Button("Migrate All", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Migrate All Components",
                    "This will flag ALL components as migrated. Only use this if " +
                    "every UI Toolkit implementation is ready. Continue?", "Migrate All", "Cancel"))
                {
                    foreach (var kvp in allStatus)
                        UIToolkitMigration.SetMigrated(kvp.Key, true);
                    Repaint();
                }
            }

            if (GUILayout.Button("Revert All", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Revert All to IMGUI",
                    "This will flag ALL components back to legacy IMGUI. Continue?",
                    "Revert All", "Cancel"))
                {
                    foreach (var kvp in allStatus)
                        UIToolkitMigration.SetMigrated(kvp.Key, false);
                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawComponentRow(string componentName, bool isMigrated)
        {
            EditorGUILayout.BeginHorizontal();

            // Color-coded status indicator
            Color statusColor = isMigrated
                ? new Color(0.2f, 0.85f, 0.2f)  // Green
                : new Color(0.85f, 0.25f, 0.25f); // Red

            var dot = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fixedWidth = 20,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.contentColor = statusColor;
            EditorGUILayout.LabelField("\u25CF", dot, GUILayout.Width(20));
            GUI.contentColor = Color.white;

            // Component name
            EditorGUILayout.LabelField(componentName, GUILayout.MinWidth(180));

            // Status label
            string statusText = isMigrated ? "UI Toolkit" : "IMGUI";
            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = statusColor }
            };
            EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(70));

            // Toggle button
            string btnLabel = isMigrated ? "Revert" : "Migrate";
            if (GUILayout.Button(btnLabel, EditorStyles.miniButton, GUILayout.Width(60)))
            {
                UIToolkitMigration.SetMigrated(componentName, !isMigrated);
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private List<string> FilterComponents(string[] components,
            IReadOnlyDictionary<string, bool> allStatus)
        {
            var result = new List<string>();
            string filter = _searchFilter?.Trim().ToLowerInvariant() ?? "";

            foreach (string comp in components)
            {
                // Text filter
                if (!string.IsNullOrEmpty(filter) &&
                    !comp.ToLowerInvariant().Contains(filter))
                    continue;

                // Status filter
                bool isMigrated = allStatus.TryGetValue(comp, out bool v) && v;
                if (_showOnlyLegacy && isMigrated) continue;
                if (_showOnlyMigrated && !isMigrated) continue;

                result.Add(comp);
            }
            return result;
        }
    }
}

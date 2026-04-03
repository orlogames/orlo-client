using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Rebindable keybinding system. Displays in SettingsUI Controls tab.
    /// Stores bindings in PlayerPrefs as JSON. Other scripts call
    /// KeybindingUI.GetKey("action") instead of hardcoding KeyCode.
    /// </summary>
    public class KeybindingUI : MonoBehaviour
    {
        public static KeybindingUI Instance { get; private set; }

        private const string PrefsKey = "OrloKeybindings";

        // ── Binding Data ────────────────────────────────────────────────

        [Serializable]
        public class KeyBinding
        {
            public string action;
            public string displayName;
            public KeyCode key;
        }

        [Serializable]
        private class BindingList
        {
            public List<KeyBinding> bindings = new();
        }

        private readonly List<KeyBinding> _bindings = new();
        private readonly Dictionary<string, KeyCode> _lookup = new();

        // Rebind state
        private string _rebindingAction;
        private bool _waitingForKey;

        // Scroll position for the settings panel
        private Vector2 _scrollPos;

        // ── Default Bindings ────────────────────────────────────────────

        private static readonly KeyBinding[] Defaults = new[]
        {
            new KeyBinding { action = "move_forward",  displayName = "Move Forward",    key = KeyCode.W },
            new KeyBinding { action = "move_back",     displayName = "Move Backward",   key = KeyCode.S },
            new KeyBinding { action = "move_left",     displayName = "Strafe Left",     key = KeyCode.A },
            new KeyBinding { action = "move_right",    displayName = "Strafe Right",    key = KeyCode.D },
            new KeyBinding { action = "jump",          displayName = "Jump",            key = KeyCode.Space },
            new KeyBinding { action = "sprint",        displayName = "Sprint",          key = KeyCode.LeftShift },
            new KeyBinding { action = "interact",      displayName = "Interact",        key = KeyCode.E },
            new KeyBinding { action = "action_1",      displayName = "Action Bar 1",    key = KeyCode.Alpha1 },
            new KeyBinding { action = "action_2",      displayName = "Action Bar 2",    key = KeyCode.Alpha2 },
            new KeyBinding { action = "action_3",      displayName = "Action Bar 3",    key = KeyCode.Alpha3 },
            new KeyBinding { action = "action_4",      displayName = "Action Bar 4",    key = KeyCode.Alpha4 },
            new KeyBinding { action = "action_5",      displayName = "Action Bar 5",    key = KeyCode.Alpha5 },
            new KeyBinding { action = "action_6",      displayName = "Action Bar 6",    key = KeyCode.Alpha6 },
            new KeyBinding { action = "action_7",      displayName = "Action Bar 7",    key = KeyCode.Alpha7 },
            new KeyBinding { action = "action_8",      displayName = "Action Bar 8",    key = KeyCode.Alpha8 },
            new KeyBinding { action = "action_9",      displayName = "Action Bar 9",    key = KeyCode.Alpha9 },
            new KeyBinding { action = "action_10",     displayName = "Action Bar 10",   key = KeyCode.Alpha0 },
            new KeyBinding { action = "inventory",     displayName = "Inventory",       key = KeyCode.I },
            new KeyBinding { action = "character",     displayName = "Character Sheet", key = KeyCode.C },
            new KeyBinding { action = "profile",       displayName = "Profile",         key = KeyCode.P },
            new KeyBinding { action = "leaderboard",   displayName = "Leaderboard",     key = KeyCode.L },
            new KeyBinding { action = "tmd",           displayName = "TMD Tool",        key = KeyCode.T },
            new KeyBinding { action = "map",           displayName = "Map",             key = KeyCode.M },
            new KeyBinding { action = "chat",          displayName = "Chat",            key = KeyCode.Return },
            new KeyBinding { action = "settings",      displayName = "Settings",        key = KeyCode.F10 },
            new KeyBinding { action = "escape",        displayName = "Menu / Cancel",   key = KeyCode.Escape },
        };

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadBindings();
        }

        private void OnGUI()
        {
            // Capture rebind key
            if (_waitingForKey && Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None)
            {
                SetBinding(_rebindingAction, Event.current.keyCode);
                _waitingForKey = false;
                _rebindingAction = null;
                Event.current.Use();
            }
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Get the KeyCode bound to an action. Returns KeyCode.None if not found.
        /// Other scripts should use this instead of hardcoded KeyCode values.
        /// </summary>
        public static KeyCode GetKey(string action)
        {
            if (Instance == null) return KeyCode.None;
            return Instance._lookup.TryGetValue(action, out var key) ? key : KeyCode.None;
        }

        /// <summary>
        /// Check if the key for an action was pressed this frame.
        /// Convenience wrapper around Input.GetKeyDown.
        /// </summary>
        public static bool GetKeyDown(string action)
        {
            var key = GetKey(action);
            return key != KeyCode.None && UnityEngine.Input.GetKeyDown(key);
        }

        /// <summary>
        /// Check if the key for an action is held this frame.
        /// </summary>
        public static bool GetKeyHeld(string action)
        {
            var key = GetKey(action);
            return key != KeyCode.None && UnityEngine.Input.GetKey(key);
        }

        /// <summary>
        /// Get the display name for a key bound to an action.
        /// </summary>
        public static string GetKeyName(string action)
        {
            var key = GetKey(action);
            return key != KeyCode.None ? key.ToString() : "Unbound";
        }

        /// <summary>
        /// Set a binding programmatically and save.
        /// </summary>
        public void SetBinding(string action, KeyCode newKey)
        {
            foreach (var b in _bindings)
            {
                if (b.action == action)
                {
                    b.key = newKey;
                    break;
                }
            }
            RebuildLookup();
            SaveBindings();
        }

        /// <summary>
        /// Reset all bindings to defaults and save.
        /// </summary>
        public void ResetToDefaults()
        {
            _bindings.Clear();
            foreach (var d in Defaults)
                _bindings.Add(new KeyBinding { action = d.action, displayName = d.displayName, key = d.key });
            RebuildLookup();
            SaveBindings();
        }

        // ── Settings Panel Drawing ──────────────────────────────────────
        // Called by SettingsUI.DrawControlsTab() instead of placeholder text.

        /// <summary>Draw the keybinding list inside the Controls settings tab.</summary>
        public void DrawKeybindingPanel()
        {
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(12),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.8f, 1f) }
            };

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(11),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            var keyStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = UIScaler.ScaledFontSize(11),
                alignment = TextAnchor.MiddleCenter
            };

            float rowH = 24f;
            float labelW = 160f;
            float keyW = 120f;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(280));

            string lastCategory = "";
            foreach (var b in _bindings)
            {
                // Category headers
                string category = GetCategory(b.action);
                if (category != lastCategory)
                {
                    GUILayout.Space(6);
                    GUILayout.Label(category, headerStyle);
                    lastCategory = category;
                }

                GUILayout.BeginHorizontal(GUILayout.Height(rowH));

                GUILayout.Label(b.displayName, labelStyle, GUILayout.Width(labelW));

                bool isRebinding = _waitingForKey && _rebindingAction == b.action;
                string btnText = isRebinding ? "Press a key..." : b.key.ToString();

                if (isRebinding)
                {
                    var waitStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = UIScaler.ScaledFontSize(11),
                        fontStyle = FontStyle.Italic,
                        normal = { textColor = Color.yellow }
                    };
                    GUILayout.Button(btnText, waitStyle, GUILayout.Width(keyW), GUILayout.Height(20));
                }
                else
                {
                    if (GUILayout.Button(btnText, keyStyle, GUILayout.Width(keyW), GUILayout.Height(20)))
                    {
                        _waitingForKey = true;
                        _rebindingAction = b.action;
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Reset to Defaults", GUILayout.Width(140), GUILayout.Height(24)))
            {
                ResetToDefaults();
            }

            GUILayout.EndScrollView();
        }

        /// <summary>True while waiting for user to press a key for rebinding.</summary>
        public bool IsRebinding => _waitingForKey;

        // ── Persistence ─────────────────────────────────────────────────

        private void SaveBindings()
        {
            var list = new BindingList();
            foreach (var b in _bindings)
                list.bindings.Add(new KeyBinding { action = b.action, displayName = b.displayName, key = b.key });
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
            Debug.Log("[KeybindingUI] Bindings saved.");
        }

        private void LoadBindings()
        {
            _bindings.Clear();

            if (PlayerPrefs.HasKey(PrefsKey))
            {
                try
                {
                    var list = JsonUtility.FromJson<BindingList>(PlayerPrefs.GetString(PrefsKey));
                    if (list?.bindings != null && list.bindings.Count > 0)
                    {
                        foreach (var b in list.bindings)
                            _bindings.Add(b);

                        // Merge any new defaults not in saved bindings
                        foreach (var d in Defaults)
                        {
                            bool found = false;
                            foreach (var b in _bindings)
                            {
                                if (b.action == d.action) { found = true; break; }
                            }
                            if (!found)
                                _bindings.Add(new KeyBinding { action = d.action, displayName = d.displayName, key = d.key });
                        }

                        RebuildLookup();
                        Debug.Log($"[KeybindingUI] Loaded {_bindings.Count} bindings from PlayerPrefs.");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[KeybindingUI] Failed to load bindings: {e.Message}");
                }
            }

            // Fall back to defaults
            ResetToDefaults();
        }

        private void RebuildLookup()
        {
            _lookup.Clear();
            foreach (var b in _bindings)
                _lookup[b.action] = b.key;
        }

        private static string GetCategory(string action)
        {
            if (action.StartsWith("move_") || action == "jump" || action == "sprint")
                return "Movement";
            if (action.StartsWith("action_"))
                return "Action Bar";
            if (action == "interact" || action == "tmd")
                return "Interaction";
            return "Interface";
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Manages draggable, lockable HUD window positions.
    /// Each HUD element registers with a key and default position.
    /// Right-click + drag to move windows. /hudlock to toggle lock.
    /// Positions persist via PlayerPrefs.
    /// </summary>
    public class HUDLayout : MonoBehaviour
    {
        public static HUDLayout Instance { get; private set; }

        private const string PrefsPrefix = "HUD_";
        private const float TitleBarHeight = 16f;

        private readonly Dictionary<string, WindowState> _windows = new();
        private string _draggingWindow;
        private Vector2 _dragOffset;
        private bool _locked = true;
        private bool _showLockHint;
        private float _lockHintTimer;

        public bool IsLocked => _locked;

        private struct WindowState
        {
            public float X, Y;
            public float Width, Height;
            public string Label;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _locked = PlayerPrefs.GetInt("HUD_Locked", 1) == 1;
        }

        /// <summary>
        /// Register a HUD element. Call once during setup.
        /// Returns the current saved position (or default if first time).
        /// </summary>
        public Vector2 Register(string key, string label, float defaultX, float defaultY, float width, float height)
        {
            float x = PlayerPrefs.GetFloat(PrefsPrefix + key + "_X", defaultX);
            float y = PlayerPrefs.GetFloat(PrefsPrefix + key + "_Y", defaultY);

            // Clamp to screen bounds
            x = Mathf.Clamp(x, 0, Screen.width - width);
            y = Mathf.Clamp(y, 0, Screen.height - height);

            _windows[key] = new WindowState
            {
                X = x, Y = y,
                Width = width, Height = height,
                Label = label
            };

            return new Vector2(x, y);
        }

        /// <summary>Get the current position of a registered HUD window.</summary>
        public Vector2 GetPosition(string key)
        {
            if (_windows.TryGetValue(key, out var state))
                return new Vector2(state.X, state.Y);
            return Vector2.zero;
        }

        /// <summary>Update window size if content changes.</summary>
        public void UpdateSize(string key, float width, float height)
        {
            if (_windows.TryGetValue(key, out var state))
            {
                state.Width = width;
                state.Height = height;
                _windows[key] = state;
            }
        }

        public void ToggleLock()
        {
            _locked = !_locked;
            PlayerPrefs.SetInt("HUD_Locked", _locked ? 1 : 0);
            PlayerPrefs.Save();
            _showLockHint = true;
            _lockHintTimer = 2f;
        }

        /// <summary>Reset all windows to their default positions.</summary>
        public void ResetLayout()
        {
            var keys = new List<string>(_windows.Keys);
            foreach (var key in keys)
            {
                PlayerPrefs.DeleteKey(PrefsPrefix + key + "_X");
                PlayerPrefs.DeleteKey(PrefsPrefix + key + "_Y");
            }
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            if (_showLockHint)
            {
                _lockHintTimer -= Time.deltaTime;
                if (_lockHintTimer <= 0) _showLockHint = false;

                string msg = _locked ? "HUD Locked" : "HUD Unlocked - Right-click drag to move windows";
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = _locked ? Color.green : Color.yellow }
                };
                GUI.Label(new Rect(Screen.width / 2 - 200, 40, 400, 24), msg, style);
            }

            if (_locked) return;

            // Draw drag handles on all registered windows
            foreach (var kvp in _windows)
            {
                var state = kvp.Value;
                Rect titleRect = new Rect(state.X, state.Y - TitleBarHeight, state.Width, TitleBarHeight);

                // Title bar background
                GUI.color = new Color(0.2f, 0.4f, 0.8f, 0.6f);
                GUI.DrawTexture(titleRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(titleRect, state.Label, labelStyle);
            }

            // Handle dragging with right mouse button
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                foreach (var kvp in _windows)
                {
                    var state = kvp.Value;
                    Rect fullRect = new Rect(state.X, state.Y - TitleBarHeight, state.Width, state.Height + TitleBarHeight);
                    if (fullRect.Contains(e.mousePosition))
                    {
                        _draggingWindow = kvp.Key;
                        _dragOffset = e.mousePosition - new Vector2(state.X, state.Y);
                        e.Use();
                        break;
                    }
                }
            }
            else if (e.type == EventType.MouseDrag && e.button == 1 && _draggingWindow != null)
            {
                if (_windows.TryGetValue(_draggingWindow, out var state))
                {
                    state.X = e.mousePosition.x - _dragOffset.x;
                    state.Y = e.mousePosition.y - _dragOffset.y;

                    // Clamp to screen
                    state.X = Mathf.Clamp(state.X, 0, Screen.width - state.Width);
                    state.Y = Mathf.Clamp(state.Y, TitleBarHeight, Screen.height - state.Height);

                    _windows[_draggingWindow] = state;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && e.button == 1 && _draggingWindow != null)
            {
                // Save position
                if (_windows.TryGetValue(_draggingWindow, out var state))
                {
                    PlayerPrefs.SetFloat(PrefsPrefix + _draggingWindow + "_X", state.X);
                    PlayerPrefs.SetFloat(PrefsPrefix + _draggingWindow + "_Y", state.Y);
                    PlayerPrefs.Save();
                }
                _draggingWindow = null;
                e.Use();
            }
        }
    }
}

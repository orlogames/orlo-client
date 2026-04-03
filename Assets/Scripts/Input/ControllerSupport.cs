using UnityEngine;

namespace Orlo.Input
{
    /// <summary>
    /// Detects gamepad connection and provides unified controller input mapping.
    /// When a controller is active, UI scripts can check IsControllerActive to
    /// show controller glyphs instead of keyboard hints.
    ///
    /// Button mapping:
    ///   A = Interact/Confirm, B = Cancel/Back, X = Action, Y = Toggle Inventory
    ///   Left Stick = Movement, Right Stick = Camera
    ///   LT = Block/Aim, RT = Attack/Use
    ///   D-Pad = UI Navigation
    ///   Start = Settings, Select = Map/Minimap
    /// </summary>
    public class ControllerSupport : MonoBehaviour
    {
        public static ControllerSupport Instance { get; private set; }

        /// <summary>True when a gamepad is connected and was the last input device used.</summary>
        public bool IsControllerActive { get; private set; }

        // Deadzone for sticks
        private const float StickDeadzone = 0.15f;

        // Tracking last input source
        private float _lastKeyboardTime;
        private float _lastControllerTime;

        // Cached joystick names (refreshed periodically)
        private string[] _joystickNames;
        private float _refreshTimer;
        private const float RefreshInterval = 2f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshJoysticks();
        }

        private void Update()
        {
            // Periodic joystick detection refresh
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0;
                RefreshJoysticks();
            }

            // Track input source switching
            if (AnyKeyboardInput())
                _lastKeyboardTime = Time.unscaledTime;
            if (AnyControllerInput())
                _lastControllerTime = Time.unscaledTime;

            IsControllerActive = HasGamepad() && _lastControllerTime > _lastKeyboardTime;
        }

        // ── Joystick Detection ──────────────────────────────────────────

        private void RefreshJoysticks()
        {
            _joystickNames = UnityEngine.Input.GetJoystickNames();
        }

        public bool HasGamepad()
        {
            if (_joystickNames == null) return false;
            foreach (var name in _joystickNames)
            {
                if (!string.IsNullOrEmpty(name))
                    return true;
            }
            return false;
        }

        // ── Mapped Inputs ───────────────────────────────────────────────
        // These return true on the frame the button is pressed (GetKeyDown equivalent).

        /// <summary>A button — Interact, Confirm in menus.</summary>
        public bool ButtonA => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0);

        /// <summary>B button — Cancel, Back.</summary>
        public bool ButtonB => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1);

        /// <summary>X button — Action (context-sensitive).</summary>
        public bool ButtonX => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2);

        /// <summary>Y button — Toggle Inventory.</summary>
        public bool ButtonY => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3);

        /// <summary>Left Bumper.</summary>
        public bool BumperLeft => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4);

        /// <summary>Right Bumper.</summary>
        public bool BumperRight => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton5);

        /// <summary>Start button — Open Settings.</summary>
        public bool ButtonStart => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton7);

        /// <summary>Select/Back button — Toggle Map/Minimap.</summary>
        public bool ButtonSelect => IsControllerActive && UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton6);

        // ── Stick Axes ──────────────────────────────────────────────────

        /// <summary>Left stick horizontal (-1 left, +1 right). Used for strafe.</summary>
        public float LeftStickX => ApplyDeadzone(UnityEngine.Input.GetAxis("Horizontal"));

        /// <summary>Left stick vertical (-1 back, +1 forward). Used for forward/back.</summary>
        public float LeftStickY => ApplyDeadzone(UnityEngine.Input.GetAxis("Vertical"));

        /// <summary>Right stick horizontal. Used for camera yaw.</summary>
        public float RightStickX => ApplyDeadzone(GetAxisSafe("RightStickX", "Mouse X"));

        /// <summary>Right stick vertical. Used for camera pitch.</summary>
        public float RightStickY => ApplyDeadzone(GetAxisSafe("RightStickY", "Mouse Y"));

        // ── Triggers ────────────────────────────────────────────────────

        /// <summary>Left trigger (0-1). Block/Aim.</summary>
        public float LeftTrigger => Mathf.Max(0, GetAxisSafe("LeftTrigger", ""));

        /// <summary>Right trigger (0-1). Attack/Use.</summary>
        public float RightTrigger => Mathf.Max(0, GetAxisSafe("RightTrigger", ""));

        /// <summary>Left trigger pressed this frame (threshold 0.5).</summary>
        public bool LeftTriggerDown => IsControllerActive && LeftTrigger > 0.5f;

        /// <summary>Right trigger pressed this frame (threshold 0.5).</summary>
        public bool RightTriggerDown => IsControllerActive && RightTrigger > 0.5f;

        // ── D-Pad ───────────────────────────────────────────────────────

        /// <summary>D-Pad Up — UI navigation up.</summary>
        public bool DPadUp => IsControllerActive && GetDPadAxis("DPadY", "") > 0.5f;

        /// <summary>D-Pad Down — UI navigation down.</summary>
        public bool DPadDown => IsControllerActive && GetDPadAxis("DPadY", "") < -0.5f;

        /// <summary>D-Pad Left — UI navigation left.</summary>
        public bool DPadLeft => IsControllerActive && GetDPadAxis("DPadX", "") < -0.5f;

        /// <summary>D-Pad Right — UI navigation right.</summary>
        public bool DPadRight => IsControllerActive && GetDPadAxis("DPadX", "") > 0.5f;

        // ── Glyph Helpers ───────────────────────────────────────────────

        /// <summary>
        /// Get the display string for an action. Returns controller glyph when
        /// controller is active, keyboard key otherwise.
        /// </summary>
        public string GetGlyph(string action)
        {
            if (!IsControllerActive)
                return GetKeyboardGlyph(action);

            return action switch
            {
                "interact"  => "[A]",
                "cancel"    => "[B]",
                "action"    => "[X]",
                "inventory" => "[Y]",
                "attack"    => "[RT]",
                "block"     => "[LT]",
                "settings"  => "[Start]",
                "map"       => "[Select]",
                _ => $"[{action}]"
            };
        }

        private string GetKeyboardGlyph(string action)
        {
            return action switch
            {
                "interact"  => "[E]",
                "cancel"    => "[Esc]",
                "action"    => "[F]",
                "inventory" => "[I]",
                "attack"    => "[LMB]",
                "block"     => "[RMB]",
                "settings"  => "[F10]",
                "map"       => "[M]",
                _ => $"[{action}]"
            };
        }

        // ── Utility ─────────────────────────────────────────────────────

        private static float ApplyDeadzone(float value)
        {
            return Mathf.Abs(value) < StickDeadzone ? 0f : value;
        }

        private static float GetAxisSafe(string primary, string fallback)
        {
            try { return UnityEngine.Input.GetAxis(primary); }
            catch { /* axis not defined in InputManager */ }

            if (!string.IsNullOrEmpty(fallback))
            {
                try { return UnityEngine.Input.GetAxis(fallback); }
                catch { /* fallback not defined either */ }
            }

            return 0f;
        }

        private static float GetDPadAxis(string primary, string fallback)
        {
            return GetAxisSafe(primary, fallback);
        }

        private static bool AnyKeyboardInput()
        {
            return UnityEngine.Input.anyKeyDown
                && !UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0)
                && !UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1)
                && !UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2)
                && !UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3);
        }

        private static bool AnyControllerInput()
        {
            // Any joystick button or significant stick movement
            for (int i = 0; i <= 19; i++)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0 + i))
                    return true;
            }

            if (Mathf.Abs(UnityEngine.Input.GetAxis("Horizontal")) > StickDeadzone) return true;
            if (Mathf.Abs(UnityEngine.Input.GetAxis("Vertical")) > StickDeadzone) return true;

            return false;
        }
    }
}

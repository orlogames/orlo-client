using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.Input
{
    /// <summary>
    /// Unified input management for UI Toolkit. Implements:
    /// - Modal stack: prevents input reaching lower layers when a modal is open
    /// - Window z-order management with focus tracking
    /// - Focus system: Tab navigation, Escape closes top window
    /// - Gamepad support: D-pad focus, A=confirm, B=back
    /// - Chat input capture flag to prevent game input during typing
    ///
    /// Attach to a UIDocument's root VisualElement. Call Register/Unregister as windows open/close.
    /// </summary>
    public class UIInputLayer
    {
        /// <summary>Represents a registered UI layer (window, modal, panel).</summary>
        public class LayerEntry
        {
            public string Id;
            public VisualElement Root;
            public bool IsModal;
            public int ZOrder;
            public Action OnClose;

            /// <summary>Whether this layer can receive input right now.</summary>
            public bool InputEnabled { get; internal set; } = true;
        }

        // The modal/window stack, ordered by z-order (highest = top)
        private readonly List<LayerEntry> _stack = new List<LayerEntry>();

        // The UIDocument root we listen on for global keyboard events
        private readonly VisualElement _documentRoot;

        // Chat input capture: when true, keyboard input is routed to chat, not game
        private bool _chatCaptured;

        // Events
        public event Action<LayerEntry> OnLayerPushed;
        public event Action<LayerEntry> OnLayerPopped;
        public event Action<bool> OnChatCaptureChanged;
        public event Action OnAllWindowsClosed;

        // Current z-order counter
        private int _nextZOrder = 100;

        /// <summary>True when chat text field has keyboard focus.</summary>
        public bool IsChatCaptured
        {
            get => _chatCaptured;
            set
            {
                if (_chatCaptured == value) return;
                _chatCaptured = value;
                OnChatCaptureChanged?.Invoke(_chatCaptured);
            }
        }

        /// <summary>True when any modal is on the stack.</summary>
        public bool HasModal
        {
            get
            {
                for (int i = 0; i < _stack.Count; i++)
                    if (_stack[i].IsModal) return true;
                return false;
            }
        }

        /// <summary>True when any window/modal is open.</summary>
        public bool HasAnyWindow => _stack.Count > 0;

        /// <summary>The topmost layer entry, or null if stack is empty.</summary>
        public LayerEntry TopLayer => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        /// <summary>Read-only view of the current stack (bottom to top).</summary>
        public IReadOnlyList<LayerEntry> Stack => _stack;

        public UIInputLayer(VisualElement documentRoot)
        {
            _documentRoot = documentRoot;
            _documentRoot.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Register a window/panel/modal as an input layer.
        /// Modal layers block input to everything below them.
        /// Returns the LayerEntry for later reference.
        /// </summary>
        public LayerEntry Register(string id, VisualElement root, bool isModal = false, Action onClose = null)
        {
            // Check for duplicate
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].Id == id)
                {
                    BringToFront(_stack[i]);
                    return _stack[i];
                }
            }

            var entry = new LayerEntry
            {
                Id = id,
                Root = root,
                IsModal = isModal,
                ZOrder = _nextZOrder++,
                OnClose = onClose,
            };

            _stack.Add(entry);
            RefreshInputState();
            ApplyZOrder(entry);

            // Focus the first focusable element in the new layer
            root.schedule.Execute(() => FocusFirstIn(root)).ExecuteLater(16);

            OnLayerPushed?.Invoke(entry);
            return entry;
        }

        /// <summary>Unregister a layer by id.</summary>
        public void Unregister(string id)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].Id != id) continue;
                var entry = _stack[i];
                _stack.RemoveAt(i);
                RefreshInputState();
                OnLayerPopped?.Invoke(entry);

                if (_stack.Count == 0)
                    OnAllWindowsClosed?.Invoke();
                else
                    FocusFirstIn(TopLayer.Root);

                return;
            }
        }

        /// <summary>Close and unregister the topmost layer.</summary>
        public void PopTop()
        {
            if (_stack.Count == 0) return;
            var top = _stack[_stack.Count - 1];
            top.OnClose?.Invoke();
            Unregister(top.Id);
        }

        /// <summary>Bring a layer to the top of the z-order stack.</summary>
        public void BringToFront(LayerEntry entry)
        {
            _stack.Remove(entry);
            entry.ZOrder = _nextZOrder++;
            _stack.Add(entry);
            RefreshInputState();
            ApplyZOrder(entry);
        }

        /// <summary>Close all layers (e.g., pressing Escape repeatedly or a "close all" keybind).</summary>
        public void CloseAll()
        {
            while (_stack.Count > 0)
                PopTop();
        }

        /// <summary>
        /// Check if a given layer can receive input right now.
        /// A layer is blocked if there's a modal above it.
        /// </summary>
        public bool CanReceiveInput(string id)
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].Id == id)
                    return _stack[i].InputEnabled;
            }
            return false;
        }

        /// <summary>
        /// Should game input (movement, camera, abilities) be suppressed?
        /// True when any window is open or chat is captured.
        /// </summary>
        public bool ShouldSuppressGameInput => HasAnyWindow || IsChatCaptured;

        // --- Gamepad Support ---

        /// <summary>
        /// Call from a MonoBehaviour.Update() to process gamepad input for UI navigation.
        /// Uses Unity's legacy Input for broad compatibility; swap to InputSystem actions if preferred.
        /// </summary>
        public void ProcessGamepadInput()
        {
            if (_stack.Count == 0) return;

            // B button = back/cancel (joystick button 1)
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                PopTop();
                return;
            }

            // A button = confirm/submit (joystick button 0)
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                SimulateSubmitOnFocused();
                return;
            }

            // D-pad navigation
            ProcessDpadNavigation();
        }

        // --- Internal ---

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Escape closes the topmost layer
            if (evt.keyCode == KeyCode.Escape && _stack.Count > 0)
            {
                PopTop();
                evt.StopPropagation();
                return;
            }

            // Tab cycles focus within the top layer
            if (evt.keyCode == KeyCode.Tab && _stack.Count > 0)
            {
                var top = TopLayer;
                if (top != null)
                {
                    CycleFocus(top.Root, evt.shiftKey);
                    evt.StopPropagation();
                }
            }
        }

        /// <summary>
        /// Refresh which layers can receive input based on modal state.
        /// Everything below the topmost modal is disabled.
        /// </summary>
        private void RefreshInputState()
        {
            // Find the highest modal index
            int highestModal = -1;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].IsModal)
                {
                    highestModal = i;
                    break;
                }
            }

            for (int i = 0; i < _stack.Count; i++)
            {
                bool enabled = highestModal < 0 || i >= highestModal;
                _stack[i].InputEnabled = enabled;
                _stack[i].Root.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
                _stack[i].Root.SetEnabled(enabled);
            }
        }

        private void ApplyZOrder(LayerEntry entry)
        {
            // Bring to front in the visual tree
            entry.Root.BringToFront();
        }

        /// <summary>Focus the first focusable child within a container.</summary>
        private static void FocusFirstIn(VisualElement container)
        {
            if (container == null) return;

            var focusable = FindFirstFocusable(container);
            if (focusable is Focusable f)
                f.Focus();
        }

        private static VisualElement FindFirstFocusable(VisualElement parent)
        {
            if (parent.focusable && parent.tabIndex >= 0 && parent.enabledInHierarchy)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindFirstFocusable(parent[i]);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>Cycle Tab focus among focusable elements in a container.</summary>
        private void CycleFocus(VisualElement container, bool reverse)
        {
            var focusables = new List<VisualElement>();
            CollectFocusables(container, focusables);
            if (focusables.Count == 0) return;

            // Sort by tabIndex
            focusables.Sort((a, b) => a.tabIndex.CompareTo(b.tabIndex));

            // Find current focused
            var panel = container.panel;
            var current = panel?.focusController?.focusedElement as VisualElement;
            int idx = current != null ? focusables.IndexOf(current) : -1;

            int next;
            if (reverse)
                next = idx <= 0 ? focusables.Count - 1 : idx - 1;
            else
                next = idx >= focusables.Count - 1 ? 0 : idx + 1;

            if (focusables[next] is Focusable f)
                f.Focus();
        }

        private static void CollectFocusables(VisualElement parent, List<VisualElement> result)
        {
            if (parent.focusable && parent.tabIndex >= 0 && parent.enabledInHierarchy)
                result.Add(parent);

            for (int i = 0; i < parent.childCount; i++)
                CollectFocusables(parent[i], result);
        }

        private void SimulateSubmitOnFocused()
        {
            if (_stack.Count == 0) return;
            var panel = TopLayer.Root.panel;
            var focused = panel?.focusController?.focusedElement as VisualElement;
            if (focused == null) return;

            // Simulate a click on the focused element
            using (var clickEvt = new NavigationSubmitEvent())
            {
                focused.SendEvent(clickEvt);
            }
        }

        // D-pad navigation repeat timing
        private float _dpadRepeatTimer;
        private const float DpadRepeatDelay = 0.4f;
        private const float DpadRepeatRate = 0.12f;
        private bool _dpadHeld;

        private void ProcessDpadNavigation()
        {
            float h = UnityEngine.Input.GetAxisRaw("DPadX");
            float v = UnityEngine.Input.GetAxisRaw("DPadY");

            bool anyPressed = Mathf.Abs(h) > 0.5f || Mathf.Abs(v) > 0.5f;

            if (!anyPressed)
            {
                _dpadHeld = false;
                _dpadRepeatTimer = 0f;
                return;
            }

            bool shouldNavigate = false;
            if (!_dpadHeld)
            {
                _dpadHeld = true;
                _dpadRepeatTimer = DpadRepeatDelay;
                shouldNavigate = true;
            }
            else
            {
                _dpadRepeatTimer -= Time.unscaledDeltaTime;
                if (_dpadRepeatTimer <= 0f)
                {
                    _dpadRepeatTimer = DpadRepeatRate;
                    shouldNavigate = true;
                }
            }

            if (!shouldNavigate || _stack.Count == 0) return;

            // Determine direction and use Tab-like cycling
            // Vertical: up = shift-tab (reverse), down = tab (forward)
            if (Mathf.Abs(v) > 0.5f)
            {
                CycleFocus(TopLayer.Root, v > 0f);
            }
            else if (Mathf.Abs(h) > 0.5f)
            {
                // Horizontal: could be used for tab switching or left/right within a row
                CycleFocus(TopLayer.Root, h < 0f);
            }
        }
    }
}

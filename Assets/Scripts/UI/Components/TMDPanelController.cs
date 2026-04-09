using UnityEngine;
using UnityEngine.UIElements;
using Orlo.UI.TMD;

namespace Orlo.UI.Components
{
    /// <summary>
    /// Controller for TMDPanel.uxml instances.
    /// Handles drag-to-move, close button, scanline effect toggling based on TMD tier,
    /// and position persistence via PlayerPrefs.
    ///
    /// Attach to a MonoBehaviour that owns a UIDocument, or call Bind() manually
    /// with the instantiated VisualElement root.
    /// </summary>
    public class TMDPanelController
    {
        private VisualElement _root;
        private VisualElement _header;
        private VisualElement _scanlineOverlay;
        private Label _titleLabel;
        private Button _closeButton;
        private VisualElement _contentContainer;

        // Drag state
        private bool _isDragging;
        private Vector2 _dragStartMousePos;
        private Vector2 _dragStartPanelPos;
        private bool _dragEnabled = true;

        // Persistence
        private string _persistenceKey;

        // Callbacks
        public System.Action OnClose;
        public System.Action<Vector2> OnDragEnd;

        /// <summary>The root panel VisualElement.</summary>
        public VisualElement Root => _root;

        /// <summary>The content container where child elements should be added.</summary>
        public VisualElement Content => _contentContainer;

        /// <summary>Whether the panel header drag handle is enabled.</summary>
        public bool DragEnabled
        {
            get => _dragEnabled;
            set => _dragEnabled = value;
        }

        /// <summary>
        /// Bind the controller to an instantiated TMDPanel template root.
        /// </summary>
        /// <param name="panelRoot">The root VisualElement from TMDPanel.uxml instantiation.</param>
        /// <param name="title">Window title text.</param>
        /// <param name="persistenceKey">Optional key for saving/restoring position via PlayerPrefs.
        /// Pass null to disable persistence.</param>
        public void Bind(VisualElement panelRoot, string title = "Panel", string persistenceKey = null)
        {
            _root = panelRoot.Q<VisualElement>("tmd-panel") ?? panelRoot;
            _header = _root.Q<VisualElement>("panel-header");
            _titleLabel = _root.Q<Label>("panel-title");
            _closeButton = _root.Q<Button>("panel-close-btn");
            _scanlineOverlay = _root.Q<VisualElement>("scanline-overlay");
            _contentContainer = _root.Q<VisualElement>("panel-content");
            _persistenceKey = persistenceKey;

            if (_titleLabel != null)
                _titleLabel.text = title;

            // Close button
            if (_closeButton != null)
                _closeButton.clicked += HandleClose;

            // Drag via header
            if (_header != null)
            {
                _header.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _header.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _header.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }

            // Restore saved position
            RestorePosition();

            // Apply initial scanline state
            RefreshScanlineOverlay();
        }

        /// <summary>
        /// Unbind callbacks. Call when destroying the panel.
        /// </summary>
        public void Unbind()
        {
            if (_closeButton != null)
                _closeButton.clicked -= HandleClose;

            if (_header != null)
            {
                _header.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _header.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                _header.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            }
        }

        /// <summary>
        /// Update scanline overlay based on current TMD tier and Precursor interference.
        /// Call this each frame or when tier/interference changes.
        /// </summary>
        public void RefreshScanlineOverlay()
        {
            if (_scanlineOverlay == null) return;

            var theme = TMDTheme.Instance;
            if (theme == null)
            {
                _scanlineOverlay.style.display = DisplayStyle.None;
                return;
            }

            float intensity = theme.EffectiveScanlines;
            if (intensity <= 0.01f)
            {
                _scanlineOverlay.style.display = DisplayStyle.None;
            }
            else
            {
                _scanlineOverlay.style.display = DisplayStyle.Flex;
                _scanlineOverlay.style.opacity = intensity;
            }
        }

        /// <summary>
        /// Show or hide the entire panel.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Show or hide the header bar. Useful for panels that are embedded
        /// inside other layouts and don't need their own title/close.
        /// </summary>
        public void SetHeaderVisible(bool visible)
        {
            if (_header == null) return;
            _header.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Set the title text.
        /// </summary>
        public void SetTitle(string title)
        {
            if (_titleLabel != null)
                _titleLabel.text = title;
        }

        /// <summary>
        /// Apply race palette colors to the panel. Call when race changes.
        /// </summary>
        public void ApplyPalette(RacePalette palette)
        {
            if (palette == null || _root == null) return;

            _root.style.backgroundColor = palette.PanelBackground;
            _root.style.borderTopColor = palette.Border;
            _root.style.borderBottomColor = palette.Border;
            _root.style.borderLeftColor = palette.Border;
            _root.style.borderRightColor = palette.Border;

            if (_header != null)
            {
                _header.style.borderBottomColor = palette.Border;
            }

            if (_titleLabel != null)
            {
                _titleLabel.style.color = palette.Text;
            }
        }

        // -- Drag Handling --

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!_dragEnabled || evt.button != 0) return;

            _isDragging = true;
            _dragStartMousePos = evt.position;
            _dragStartPanelPos = new Vector2(
                _root.resolvedStyle.left,
                _root.resolvedStyle.top
            );

            _header.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;

            Vector2 delta = (Vector2)evt.position - _dragStartMousePos;
            float newX = _dragStartPanelPos.x + delta.x;
            float newY = _dragStartPanelPos.y + delta.y;

            // Clamp to parent bounds
            var parent = _root.parent;
            if (parent != null)
            {
                float maxX = parent.resolvedStyle.width - _root.resolvedStyle.width;
                float maxY = parent.resolvedStyle.height - _root.resolvedStyle.height;
                newX = Mathf.Clamp(newX, 0, Mathf.Max(0, maxX));
                newY = Mathf.Clamp(newY, 0, Mathf.Max(0, maxY));
            }

            _root.style.left = newX;
            _root.style.top = newY;

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;

            _isDragging = false;
            _header.ReleasePointer(evt.pointerId);

            SavePosition();
            OnDragEnd?.Invoke(new Vector2(_root.resolvedStyle.left, _root.resolvedStyle.top));

            evt.StopPropagation();
        }

        // -- Persistence --

        private void SavePosition()
        {
            if (string.IsNullOrEmpty(_persistenceKey) || _root == null) return;

            PlayerPrefs.SetFloat($"UIPanel_{_persistenceKey}_X", _root.resolvedStyle.left);
            PlayerPrefs.SetFloat($"UIPanel_{_persistenceKey}_Y", _root.resolvedStyle.top);
            PlayerPrefs.Save();
        }

        private void RestorePosition()
        {
            if (string.IsNullOrEmpty(_persistenceKey) || _root == null) return;

            string keyX = $"UIPanel_{_persistenceKey}_X";
            string keyY = $"UIPanel_{_persistenceKey}_Y";

            if (PlayerPrefs.HasKey(keyX) && PlayerPrefs.HasKey(keyY))
            {
                _root.style.left = PlayerPrefs.GetFloat(keyX);
                _root.style.top = PlayerPrefs.GetFloat(keyY);
            }
        }

        // -- Close --

        private void HandleClose()
        {
            if (OnClose != null)
            {
                OnClose.Invoke();
            }
            else
            {
                // Default: hide the panel
                SetVisible(false);
            }
        }
    }
}

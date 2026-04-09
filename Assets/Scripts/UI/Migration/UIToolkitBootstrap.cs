using UnityEngine;
using UnityEngine.UIElements;

namespace Orlo.UI.Migration
{
    /// <summary>
    /// Initializes the UI Toolkit runtime infrastructure needed for migrated
    /// components. Attaches to a persistent GameObject (survives scene loads)
    /// and creates the root UIDocument + PanelSettings.
    ///
    /// Sort order is set so UI Toolkit renders BELOW IMGUI during the
    /// migration period. IMGUI always renders on top by Unity design, so
    /// we use a low sort order for the toolkit layer. Once all components
    /// are migrated and IMGUI is removed, the sort order becomes irrelevant.
    ///
    /// Also handles DPI / resolution changes by listening for screen
    /// dimension changes each frame and updating the PanelSettings scale.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UIToolkitBootstrap : MonoBehaviour
    {
        public static UIToolkitBootstrap Instance { get; private set; }

        /// <summary>
        /// The root UIDocument that migrated components attach their
        /// VisualElements to.
        /// </summary>
        public UIDocument RootDocument { get; private set; }

        /// <summary>
        /// The PanelSettings asset driving the root document.
        /// </summary>
        public PanelSettings RootPanelSettings { get; private set; }

        /// <summary>
        /// The root VisualElement. Migrated components add children here.
        /// </summary>
        public VisualElement Root => RootDocument?.rootVisualElement;

        [Header("Panel Settings")]
        [Tooltip("Path inside Resources/ to a PanelSettings asset. " +
                 "If not found, one is created at runtime.")]
        [SerializeField] private string _panelSettingsResourcePath = "UI/OrloPanel";

        [Tooltip("Sort order for the UI Toolkit panel. Lower = renders first " +
                 "(behind IMGUI). Keep this low during migration.")]
        [SerializeField] private int _sortOrder = -100;

        [Tooltip("Reference DPI for scale calculations. 96 = standard desktop.")]
        [SerializeField] private float _referenceDpi = 96f;

        // Resolution tracking
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private float _lastDpi;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePanelSettings();
            InitializeRootDocument();
            CacheScreenState();
        }

        private void InitializePanelSettings()
        {
            // Try loading a designer-authored PanelSettings from Resources.
            var loaded = Resources.Load<PanelSettings>(_panelSettingsResourcePath);
            if (loaded != null)
            {
                RootPanelSettings = loaded;
                RootPanelSettings.sortingOrder = _sortOrder;
                return;
            }

            // No asset found — create one at runtime. This is fine for early
            // migration work; a proper asset should be committed later.
            RootPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            RootPanelSettings.name = "OrloPanel (Runtime)";
            RootPanelSettings.sortingOrder = _sortOrder;

            // Scale mode: constant physical size keeps UI consistent across
            // resolutions and DPI values.
            RootPanelSettings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            RootPanelSettings.referenceDpi = _referenceDpi;

            // Default theme — let USS handle actual styling. We just need
            // a valid PanelSettings to attach UIDocuments.
            Debug.Log($"[UIToolkitBootstrap] No PanelSettings found at Resources/{_panelSettingsResourcePath}. " +
                      "Created runtime instance. Commit a proper asset for production.");
        }

        private void InitializeRootDocument()
        {
            // Create the UIDocument component on this same GameObject.
            RootDocument = gameObject.AddComponent<UIDocument>();
            RootDocument.panelSettings = RootPanelSettings;

            // The root VisualElement is the top-level container. Migrated
            // components will add their subtrees as children.
            var root = RootDocument.rootVisualElement;

            // Make root fill the entire screen and pass through picking
            // for areas not covered by migrated UI elements.
            root.style.flexGrow = 1;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            // Name for easier debugging in UI Toolkit Debugger.
            root.name = "orlo-migration-root";
        }

        private void Update()
        {
            // Detect resolution or DPI changes and refresh panel settings.
            if (Screen.width != _lastScreenWidth ||
                Screen.height != _lastScreenHeight ||
                !Mathf.Approximately(Screen.dpi, _lastDpi))
            {
                OnScreenChanged();
                CacheScreenState();
            }
        }

        private void OnScreenChanged()
        {
            // PanelSettings with ConstantPhysicalSize handles DPI natively,
            // but if we need to notify migrated components of a resize they
            // can listen to GeometryChangedEvent on the root. Log for now.
            Debug.Log($"[UIToolkitBootstrap] Screen changed: {Screen.width}x{Screen.height} @ {Screen.dpi} DPI");

            // Force panel re-evaluation by toggling a harmless style.
            if (Root != null)
            {
                Root.MarkDirtyRepaint();
            }
        }

        private void CacheScreenState()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastDpi = Screen.dpi;
        }

        /// <summary>
        /// Creates a named container VisualElement for a migrated component.
        /// The container is added to the root and returned so the component
        /// can populate it with its UI Toolkit layout.
        /// </summary>
        /// <param name="componentName">
        /// Stable name matching the UIToolkitMigration registry key.
        /// </param>
        /// <returns>A VisualElement parented to the root, ready for content.</returns>
        public VisualElement CreateComponentContainer(string componentName)
        {
            if (Root == null)
            {
                Debug.LogError("[UIToolkitBootstrap] Root not initialized. Cannot create container.");
                return null;
            }

            var container = new VisualElement
            {
                name = $"migration-{componentName.ToLowerInvariant()}",
                pickingMode = PickingMode.Ignore,
            };

            // Full-size by default; individual components constrain themselves.
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.right = 0;
            container.style.bottom = 0;

            Root.Add(container);
            return container;
        }

        /// <summary>
        /// Removes a component's container from the root. Called when a
        /// component is toggled back to IMGUI during testing.
        /// </summary>
        public void RemoveComponentContainer(string componentName)
        {
            if (Root == null) return;
            string targetName = $"migration-{componentName.ToLowerInvariant()}";
            var existing = Root.Q(targetName);
            existing?.RemoveFromHierarchy();
        }
    }
}

using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Bootstraps the UI Toolkit layer. During migration, IMGUI scripts check
    /// UIToolkitRoot.IsActive(key) before rendering their OnGUI content.
    /// As each component is migrated to UI Toolkit, its key is registered here
    /// and the IMGUI fallback path is skipped.
    /// </summary>
    public class UIToolkitRoot : MonoBehaviour
    {
        public static UIToolkitRoot Instance { get; private set; }

        private readonly System.Collections.Generic.HashSet<string> _migratedComponents
            = new System.Collections.Generic.HashSet<string>();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Returns true if this component key has been migrated to UI Toolkit.
        /// IMGUI scripts call this to decide whether to skip their OnGUI path.
        /// </summary>
        public static bool IsActive(string componentKey)
        {
            return Instance != null && Instance._migratedComponents.Contains(componentKey);
        }

        /// <summary>
        /// Called by a migrated UI component to disable its IMGUI fallback.
        /// Typically called in the migrated component's Awake() or Start().
        /// </summary>
        public static void Register(string componentKey)
        {
            Instance?._migratedComponents.Add(componentKey);
        }

        /// <summary>
        /// Deregisters a component key, re-enabling its IMGUI fallback.
        /// Useful for hot-reloading or debugging during migration.
        /// </summary>
        public static void Unregister(string componentKey)
        {
            Instance?._migratedComponents.Remove(componentKey);
        }
    }
}

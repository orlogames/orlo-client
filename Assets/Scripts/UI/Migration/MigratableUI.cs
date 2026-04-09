using UnityEngine;

namespace Orlo.UI.Migration
{
    /// <summary>
    /// Base class for all OnGUI-based UI scripts that will be incrementally
    /// migrated to UI Toolkit. Subclasses override <see cref="OnGUILegacy"/>
    /// instead of Unity's OnGUI(). When the component is marked as migrated
    /// in <see cref="UIToolkitMigration"/>, the legacy rendering is skipped
    /// entirely and the UI Toolkit implementation takes over.
    ///
    /// Usage:
    ///   1. Change your class to inherit from MigratableUI instead of MonoBehaviour.
    ///   2. Rename your OnGUI() to OnGUILegacy().
    ///   3. Override MigrationKey to return the component's registry name.
    ///   4. (Optional) Override OnUIToolkitActive() to perform any setup
    ///      needed when the UI Toolkit version activates.
    /// </summary>
    public abstract class MigratableUI : MonoBehaviour
    {
        /// <summary>
        /// The key used to look up this component in <see cref="UIToolkitMigration"/>.
        /// Must match one of the keys registered in the migration dictionary.
        /// </summary>
        protected abstract string MigrationKey { get; }

        /// <summary>
        /// Cached migration state. Refreshed each frame to allow hot-toggling
        /// in the editor without a domain reload.
        /// </summary>
        private bool _isMigrated;

        /// <summary>
        /// True when the UI Toolkit version is active for this component.
        /// Useful for subclasses that need to adjust Update() behavior.
        /// </summary>
        protected bool IsMigratedToUIToolkit => _isMigrated;

        /// <summary>
        /// Unity's OnGUI entry point. Sealed so subclasses cannot accidentally
        /// bypass the migration check by overriding OnGUI directly.
        /// </summary>
        private void OnGUI()
        {
            // Re-check every OnGUI call. This is cheap (dictionary lookup +
            // possible PlayerPrefs read in editor) and allows live toggling.
            _isMigrated = UIToolkitMigration.IsUsingUIToolkit(MigrationKey);

            if (_isMigrated)
            {
                // UI Toolkit is handling this component — skip all IMGUI work.
                OnUIToolkitActive();
                return;
            }

            OnGUILegacy();
        }

        /// <summary>
        /// Override this with your existing OnGUI implementation.
        /// This is only called when the component has NOT been migrated.
        /// </summary>
        protected abstract void OnGUILegacy();

        /// <summary>
        /// Called each OnGUI frame when the component IS migrated. Override
        /// to perform any bridge work (e.g., forwarding IMGUI events that
        /// UI Toolkit doesn't natively capture). Default does nothing.
        /// </summary>
        protected virtual void OnUIToolkitActive() { }

        /// <summary>
        /// Convenience: toggles this component's migration state at runtime.
        /// </summary>
        public void ToggleMigration()
        {
            bool current = UIToolkitMigration.IsUsingUIToolkit(MigrationKey);
            UIToolkitMigration.SetMigrated(MigrationKey, !current);
        }
    }
}

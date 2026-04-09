using UnityEngine;
using UnityEngine.UIElements;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// Syncs TMDTheme singleton state (race + tier) to UI Toolkit stylesheet variables
    /// by swapping StyleSheet assets on the root UIDocument at runtime.
    ///
    /// TMDTheme exposes no events — we poll Palette.RaceName and Tier each Update()
    /// and swap sheets only when the values change.
    /// </summary>
    public class TMDStyleBridge : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;

        [Header("Race Themes")]
        [SerializeField] private StyleSheet _solariSheet;
        [SerializeField] private StyleSheet _vaelSheet;
        [SerializeField] private StyleSheet _korrathSheet;
        [SerializeField] private StyleSheet _thyrenSheet;

        [Header("Tier Themes")]
        [SerializeField] private StyleSheet _tier1Sheet;
        [SerializeField] private StyleSheet _tier2Sheet;
        [SerializeField] private StyleSheet _tier3Sheet;
        [SerializeField] private StyleSheet _tier4Sheet;
        [SerializeField] private StyleSheet _tier5Sheet;

        private StyleSheet _activeRaceSheet;
        private StyleSheet _activeTierSheet;

        // Last-known values for change detection (polling — TMDTheme has no events)
        private string _lastRaceName;
        private int _lastTier;

        private StyleSheet[] _raceSheets;
        private StyleSheet[] _tierSheets;

        private void Start()
        {
            _raceSheets = new StyleSheet[]
            {
                _solariSheet,   // index 0
                _vaelSheet,     // index 1
                _korrathSheet,  // index 2
                _thyrenSheet,   // index 3
            };

            _tierSheets = new StyleSheet[]
            {
                _tier1Sheet,    // index 0  (Tier 1)
                _tier2Sheet,    // index 1  (Tier 2)
                _tier3Sheet,    // index 2  (Tier 3)
                _tier4Sheet,    // index 3  (Tier 4)
                _tier5Sheet,    // index 4  (Tier 5)
            };

            ApplyCurrentTheme();
        }

        private void Update()
        {
            if (TMDTheme.Instance == null) return;

            string currentRace = TMDTheme.Instance.Palette?.RaceName;
            int currentTier = TMDTheme.Instance.Tier;

            bool raceChanged = currentRace != _lastRaceName;
            bool tierChanged = currentTier != _lastTier;

            if (raceChanged) ApplyRaceSheet(RaceNameToIndex(currentRace));
            if (tierChanged) ApplyTierSheet(currentTier - 1); // Tier is 1-based; array is 0-based

            if (raceChanged) _lastRaceName = currentRace;
            if (tierChanged) _lastTier = currentTier;
        }

        private void ApplyCurrentTheme()
        {
            if (TMDTheme.Instance == null || _document == null) return;

            string raceName = TMDTheme.Instance.Palette?.RaceName;
            int tier = TMDTheme.Instance.Tier;

            ApplyRaceSheet(RaceNameToIndex(raceName));
            ApplyTierSheet(tier - 1); // Tier is 1-based; array is 0-based

            _lastRaceName = raceName;
            _lastTier = tier;
        }

        private void ApplyRaceSheet(int index)
        {
            if (_document == null) return;
            var root = _document.rootVisualElement;
            if (root == null) return;

            if (_activeRaceSheet != null)
                root.styleSheets.Remove(_activeRaceSheet);

            if (index >= 0 && index < _raceSheets.Length && _raceSheets[index] != null)
            {
                root.styleSheets.Add(_raceSheets[index]);
                _activeRaceSheet = _raceSheets[index];
            }
            else
            {
                _activeRaceSheet = null;
                Debug.LogWarning($"[TMDStyleBridge] No race sheet found for index {index}");
            }
        }

        private void ApplyTierSheet(int index)
        {
            if (_document == null) return;
            var root = _document.rootVisualElement;
            if (root == null) return;

            if (_activeTierSheet != null)
                root.styleSheets.Remove(_activeTierSheet);

            if (index >= 0 && index < _tierSheets.Length && _tierSheets[index] != null)
            {
                root.styleSheets.Add(_tierSheets[index]);
                _activeTierSheet = _tierSheets[index];
            }
            else
            {
                _activeTierSheet = null;
                Debug.LogWarning($"[TMDStyleBridge] No tier sheet found for index {index}");
            }
        }

        /// <summary>
        /// Maps race name string (from RacePalette.RaceName) to sheet array index.
        /// Matches the switch in RacePalette.ForRace() for consistency.
        /// </summary>
        private static int RaceNameToIndex(string raceName)
        {
            if (string.IsNullOrEmpty(raceName)) return 0; // default: Solari
            switch (raceName.ToLowerInvariant())
            {
                case "solari":  return 0;
                case "vael":    return 1;
                case "korrath": return 2;
                case "thyren":  return 3;
                default:        return 0; // unknown race falls back to Solari
            }
        }
    }
}

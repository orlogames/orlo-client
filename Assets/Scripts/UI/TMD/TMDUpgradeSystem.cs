using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Manages TMD tier progression and persistence.
    /// Stores the current tier in PlayerPrefs, provides upgrade API,
    /// and triggers visual feedback on tier change.
    /// </summary>
    public class TMDUpgradeSystem : MonoBehaviour
    {
        public static TMDUpgradeSystem Instance { get; private set; }

        private const string TierPrefsKey = "TMD_Tier";
        private const float FlashDuration = 0.4f;

        private int _currentTier = 1;
        private float _flashTimer;
        private Color _flashColor = Color.clear;

        /// <summary>Current TMD tier (1-5).</summary>
        public static int CurrentTier => Instance != null ? Instance._currentTier : 1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            UpdateFlash();
        }

        /// <summary>Load saved tier from PlayerPrefs and apply to TMDTheme.</summary>
        public void Initialize()
        {
            _currentTier = PlayerPrefs.GetInt(TierPrefsKey, 1);
            _currentTier = Mathf.Clamp(_currentTier, 1, 5);

            if (TMDTheme.Instance != null)
                TMDTheme.Instance.SetTier(_currentTier);

            Debug.Log($"[TMDUpgrade] Initialized at tier {_currentTier}");
        }

        /// <summary>
        /// Upgrade to the next tier. Persists to PlayerPrefs, updates TMDTheme,
        /// and triggers a brief screen flash in the race Glow color.
        /// </summary>
        public void UpgradeTier()
        {
            if (_currentTier >= 5)
            {
                Debug.Log("[TMDUpgrade] Already at maximum tier (5)");
                return;
            }

            _currentTier++;
            PlayerPrefs.SetInt(TierPrefsKey, _currentTier);
            PlayerPrefs.Save();

            if (TMDTheme.Instance != null)
            {
                TMDTheme.Instance.SetTier(_currentTier);

                // Trigger upgrade flash in race Glow color
                _flashColor = TMDTheme.Instance.Palette.Glow;
                _flashTimer = FlashDuration;
            }

            // Play upgrade sound
            if (TMDSoundDesigner.Instance != null)
                TMDSoundDesigner.Instance.PlaySelect();

            Debug.Log($"[TMDUpgrade] Upgraded to tier {_currentTier}");
        }

        /// <summary>Force-set a specific tier (e.g., from server sync). Triggers flash.</summary>
        public void SetTier(int tier)
        {
            tier = Mathf.Clamp(tier, 1, 5);
            if (tier == _currentTier) return;

            bool isUpgrade = tier > _currentTier;
            _currentTier = tier;
            PlayerPrefs.SetInt(TierPrefsKey, _currentTier);
            PlayerPrefs.Save();

            if (TMDTheme.Instance != null)
            {
                TMDTheme.Instance.SetTier(_currentTier);

                if (isUpgrade)
                {
                    _flashColor = TMDTheme.Instance.Palette.Glow;
                    _flashTimer = FlashDuration;
                }
            }

            Debug.Log($"[TMDUpgrade] Tier set to {_currentTier}");
        }

        private void UpdateFlash()
        {
            if (_flashTimer > 0f)
                _flashTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (_flashTimer <= 0f) return;

            float alpha = Mathf.Clamp01(_flashTimer / FlashDuration) * 0.35f;
            Color drawColor = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);

            GUI.color = drawColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

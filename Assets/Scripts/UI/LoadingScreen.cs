using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Full-screen loading screen with zone name, lore quote, progress bar, and gameplay tip.
    /// Static Show/Hide API. Uses OnGUI for rendering.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        private bool _visible;
        private string _zoneName = "";
        private float _progress; // 0-1
        private string _loreQuote = "";
        private string _tip = "";
        private float _dustTimer;

        private static readonly string[] LoreQuotes =
        {
            "The Precursors vanished eons ago, but their cities remain — buried beneath millennia of sediment.",
            "What we call mountains are the ruins of towers that once scraped the heavens.",
            "The Convergence is not merely energy. It is memory, crystallized and restless.",
            "A Solari proverb: The light remembers what the eye forgets.",
            "Vael scholars believe the root networks span entire continents, whispering data older than stone.",
            "Korrath metallurgists forge with conviction. They say the metal knows its purpose before the smith does.",
            "The Thyren do not speak of the Void. They listen to it.",
            "Every pyramid tip visible on the surface is the apex of a buried skyscraper.",
            "Veridian Prime's forests grow from the humus of a dead civilization.",
            "The Threshold settlement was built on a nexus — a point where Convergence bleeds through the crust.",
            "Harvesting a resource without understanding its attributes is like reading without comprehension.",
            "A master crafter does not simply assemble components. They coax potential from raw matter.",
            "The TMD — Terrain Manipulation Device — was the Precursors' most common tool. Billions were made.",
            "Criminal Rating is not a punishment. It is a reputation, earned through choices.",
            "Cantinas are more than rest stops. Strain fades in their presence, and secrets are traded over drinks.",
            "Player vendors form the backbone of the economy. No NPC auction house exists — by design.",
            "The three health pools represent body, endurance, and will. Lose any one, and you falter.",
            "Experimentation during crafting is where legends are born — or credits are lost.",
            "Some say the Awakened class is a myth. Others simply never speak of it.",
            "Every footstep is logged. Every kill counted. The universe remembers what you do.",
            "Reinforced terrain cannot be dug through. Claim your land, or lose it.",
            "The orbital zone connects planets without loading screens. Look up, and you may see ships passing.",
        };

        private static readonly string[] Tips =
        {
            "Hold RMB to orbit the camera. LMB+RMB enables auto-run.",
            "Press I to open your inventory. Double-click items to auto-equip.",
            "Use /w <name> to whisper another player. /r replies to the last whisper.",
            "Press M to toggle the minimap. V opens the full world map.",
            "Crafting has two phases: Assembly determines the base tier, Experimentation improves stats.",
            "Resource quality matters! Higher attributes produce better crafted items.",
            "Use the TMD (press T) to dig, fill, smooth, scan, or reinforce terrain.",
            "Visit a cantina or use /rest to reduce Strain.",
            "Press O to open your Friends list. Right-click friends for quick actions.",
            "Press G to open the Guild panel. Guilds share a bank and ranks.",
            "Check the Bulletin Board in settlements for trade offers and group requests.",
            "Press Period (.) to open the Emote wheel. Custom emotes use /e or /me.",
            "Your criminal rating increases from attacking innocent players. Be careful in contested zones.",
            "Item condition degrades with use. Repair at vendors before gear breaks completely.",
            "Press P to view your Player Profile. Other players can inspect you too.",
            "The Leaderboard (L key) shows top players in combat, crafting, and exploration.",
            "Exploration contracts reward cartography XP for discovering new areas.",
            "Player vendors let you sell items while offline. Place one at your home!",
            "Use the LFG Board to find groups for dungeons, PvP, or crafting sessions.",
            "F10 opens Settings. Customize graphics, controls, social privacy, and accessibility.",
            "Party members share XP with a bonus per member. Cooperation pays!",
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Show loading screen for a zone transition.</summary>
        public static void Show(string zoneName)
        {
            if (Instance == null)
            {
                var go = new GameObject("LoadingScreen");
                go.AddComponent<LoadingScreen>();
            }
            Instance._visible = true;
            Instance._zoneName = zoneName ?? "Loading";
            Instance._progress = 0f;
            Instance._loreQuote = LoreQuotes[Random.Range(0, LoreQuotes.Length)];
            Instance._tip = Tips[Random.Range(0, Tips.Length)];
            Instance._dustTimer = 0f;
        }

        /// <summary>Hide loading screen.</summary>
        public static void Hide()
        {
            if (Instance != null)
                Instance._visible = false;
        }

        /// <summary>Update progress (0-1).</summary>
        public static void SetProgress(float progress)
        {
            if (Instance != null)
                Instance._progress = Mathf.Clamp01(progress);
        }

        /// <summary>Set zone-specific loading data from server.</summary>
        public void SetLoadingData(string zoneName, string loreQuote, string tip)
        {
            if (!string.IsNullOrEmpty(zoneName)) _zoneName = zoneName;
            if (!string.IsNullOrEmpty(loreQuote)) _loreQuote = loreQuote;
            if (!string.IsNullOrEmpty(tip)) _tip = tip;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            _dustTimer += Time.deltaTime;

            // Full-screen dark background with subtle gradient
            GUI.color = new Color(0.03f, 0.02f, 0.06f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Slightly lighter band in middle for depth
            GUI.color = new Color(0.06f, 0.04f, 0.1f, 0.3f);
            GUI.DrawTexture(new Rect(0, Screen.height * 0.3f, Screen.width, Screen.height * 0.4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;

            // Zone name (large, center)
            var zoneStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(0, centerY - 80, Screen.width, 50), _zoneName, zoneStyle);

            // Lore quote (italic, below zone name)
            var loreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.7f) }
            };
            GUI.Label(new Rect(Screen.width * 0.15f, centerY - 20, Screen.width * 0.7f, 60), $"\"{_loreQuote}\"", loreStyle);

            // Progress bar
            float barW = Screen.width * 0.4f;
            float barH = 6f;
            float barX = (Screen.width - barW) / 2f;
            float barY = centerY + 60;

            // Bar background
            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            // Bar fill (subtle animation)
            float displayProgress = _progress;
            float shimmer = Mathf.Sin(_dustTimer * 2f) * 0.02f;
            GUI.color = new Color(0.3f + shimmer, 0.5f + shimmer, 1f, 0.9f);
            GUI.DrawTexture(new Rect(barX, barY, barW * displayProgress, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Progress percentage
            var pctStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.5f, 0.5f, 0.6f) }
            };
            GUI.Label(new Rect(barX, barY + 10, barW, 18), $"{_progress * 100:F0}%", pctStyle);

            // Gameplay tip (bottom)
            var tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.55f, 0.55f, 0.5f) }
            };
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height - 60, Screen.width * 0.8f, 40),
                $"TIP: {_tip}", tipStyle);

            // Animated dust motes (simple procedural particles)
            DrawDustMotes();
        }

        private void DrawDustMotes()
        {
            // Simple seed-based pseudo-random particles
            int moteCount = 12;
            for (int i = 0; i < moteCount; i++)
            {
                float seed = i * 137.3f;
                float x = (Mathf.PerlinNoise(seed, _dustTimer * 0.1f)) * Screen.width;
                float y = (Mathf.PerlinNoise(seed + 50, _dustTimer * 0.08f)) * Screen.height;
                float size = 1.5f + Mathf.Sin(seed + _dustTimer * 0.5f) * 0.5f;
                float alpha = 0.08f + Mathf.Sin(_dustTimer * 0.3f + seed) * 0.04f;

                GUI.color = new Color(0.6f, 0.5f, 0.3f, alpha);
                GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}

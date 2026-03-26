using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Full-screen loading overlay shown during world spawn / terrain chunk loading.
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        private bool _visible;
        private string _statusText = "Loading...";
        private float _progress; // 0-1
        private int _chunksLoaded;
        private int _chunksTotal;
        private Texture2D _bgTex;

        private static readonly string[] Tips =
        {
            "Every resource has 11 measurable quality attributes.",
            "Your Terrain Manipulation Device can dig, fill, scan, and reinforce terrain.",
            "Crafted items are always superior to looted items at equivalent tier.",
            "Underground resources have higher quality ceilings.",
            "The Convergence merged technology with primordial energy. Nobody understands it yet.",
            "Gravity varies per planet — adjust your combat style accordingly.",
            "Item decay creates demand. Crafters are heroes.",
            "Explore Nexus Points for concentrated Convergence energy.",
            "Some say there are hidden abilities waiting to be discovered...",
            "Player-run shops and vendors drive the economy. No NPC auction houses."
        };

        private int _currentTip;
        private float _tipTimer;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            _bgTex.Apply();

            _currentTip = Random.Range(0, Tips.Length);
        }

        public void Show(int totalChunks)
        {
            _visible = true;
            _chunksTotal = totalChunks;
            _chunksLoaded = 0;
            _progress = 0;
            _statusText = "Entering world...";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void UpdateProgress(int chunksLoaded, string status = null)
        {
            _chunksLoaded = chunksLoaded;
            _progress = _chunksTotal > 0 ? (float)chunksLoaded / _chunksTotal : 0;
            if (status != null) _statusText = status;
        }

        public void Hide()
        {
            _visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!_visible) return;
            _tipTimer += Time.deltaTime;
            if (_tipTimer > 6f)
            {
                _tipTimer = 0;
                _currentTip = (_currentTip + 1) % Tips.Length;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 50), "ORLO", titleStyle);

            // Status
            var statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 30), _statusText, statusStyle);

            // Progress bar
            float barW = Screen.width * 0.4f;
            float barH = 8;
            float barX = (Screen.width - barW) / 2;
            float barY = Screen.height * 0.55f;

            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
            var fillTex = new Texture2D(1, 1);
            fillTex.SetPixel(0, 0, new Color(0.3f, 0.7f, 1f));
            fillTex.Apply();
            GUI.DrawTexture(new Rect(barX, barY, barW * _progress, barH), fillTex);

            // Chunks counter
            var counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(new Rect(0, barY + 15, Screen.width, 20),
                $"Terrain chunks: {_chunksLoaded} / {_chunksTotal}", counterStyle);

            // Tip
            var tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.5f) },
                wordWrap = true
            };
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.75f,
                Screen.width * 0.7f, 60), Tips[_currentTip], tipStyle);
        }
    }
}

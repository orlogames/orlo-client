using UnityEngine;

namespace Orlo.UI
{
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance { get; private set; }

        private bool _visible;
        private float _progress;
        private int _chunksLoaded, _chunksTotal;
        private string _zoneName = "VERIDIAN PRIME";
        private string _loreQuote;
        private string _statusText = "Loading...";
        private int _currentTip;
        private float _tipTimer;
        private Texture2D _bgTex, _whiteTex;

        // Particle dust motes
        private Vector2[] _motePos;
        private Vector2[] _moteVel;
        private float[] _moteAlpha;
        private float[] _moteSize;
        private const int MoteCount = 40;

        // Nebula washes
        private Vector2[] _nebulaPos;
        private Color[] _nebulaColor;
        private float[] _nebulaRadius;
        private Texture2D _nebulaTex;

        private static readonly string[] LoreQuotes =
        {
            "The Precursors walked these plains long before the mountains remembered their names.",
            "Every grain of sand was once a tower. Every river carves through forgotten halls.",
            "The TMD doesn't just move earth \u2014 it peels back time.",
            "Threshold was built on optimism and salvage. Mostly salvage.",
            "The Convergence hums beneath everything. Some hear it in their sleep.",
            "Out past the Iron Ridge, the ground remembers shapes that shouldn't exist.",
            "Crafters say the best resources shift \u2014 never the same world twice.",
            "The Vael hear the roots. The Korrath feel the forge. We just dig.",
            "First rule of the frontier: trust your TMD more than your map.",
            "Stars don't fall here. They were already underground.",
            "The old cities weren't buried \u2014 the planet grew over them.",
            "In Threshold, everyone starts as a stranger. That's the point.",
            "The deepest veins hold materials that sing when you extract them.",
            "They say the Awakened can feel the Convergence like a heartbeat.",
            "Trade routes don't last. The terrain shifts, and so do the traders.",
            "Armor degrades. Weapons break. Only knowledge persists.",
            "The frontier doesn't care about your reputation. Only your preparation.",
            "Some settlements vanish between visits. The land reclaims everything.",
            "Veridian Prime breathes. Listen at night and you'll hear it.",
            "We are not the first to walk here. We may not be the last."
        };

        private static readonly string[] Tips =
        {
            "Hold T to open the Terrain Manipulation Device",
            "Right-click and hold to orbit the camera around your character",
            "Press Tab to cycle through nearby targets",
            "Crafting has two phases: Assembly determines tier, Experimentation refines stats",
            "Resource quality varies by location and spawn cycle \u2014 survey before you gather",
            "Your TMD can dig, fill, smooth, scan, and reinforce terrain",
            "Items degrade with use. Visit a repair vendor or craft replacements",
            "Press I to open your inventory. Double-click items to equip them",
            "The minimap (M key) reveals fog of war as you explore",
            "Player vendors let you sell items while offline",
            "Join a guild at the Guild Registrar in any major settlement",
            "Different planets have different gravity \u2014 adjust your combat style",
            "Dodge doesn't exist. Position and timing are your defense",
            "Press P to view your character stats and skill progress",
            "The Codex (J key) tracks everything you've discovered",
            "Chat channels: /say /yell /w /g /p \u2014 press Enter to type",
            "Land claims protect your terrain modifications from other players",
            "Criminal actions in safe zones will flag you for PvP"
        };

        private static readonly string[] StatusMessages =
        { "Loading terrain...", "Streaming chunks...", "Preparing world..." };

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _bgTex = MakeSolid(new Color(0.04f, 0.04f, 0.09f));
            _whiteTex = MakeSolid(Color.white);
            _nebulaTex = MakeSoftCircle(64);

            _currentTip = Random.Range(0, Tips.Length);
            _loreQuote = LoreQuotes[Random.Range(0, LoreQuotes.Length)];

            InitMotes();
            InitNebulae();
        }

        private void InitMotes()
        {
            _motePos = new Vector2[MoteCount];
            _moteVel = new Vector2[MoteCount];
            _moteAlpha = new float[MoteCount];
            _moteSize = new float[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                _motePos[i] = new Vector2(Random.value, Random.value);
                _moteVel[i] = new Vector2(Random.Range(-0.005f, 0.005f), Random.Range(-0.003f, 0.003f));
                _moteAlpha[i] = Random.Range(0.05f, 0.2f);
                _moteSize[i] = Random.Range(1.5f, 3.5f);
            }
        }

        private void InitNebulae()
        {
            _nebulaPos = new Vector2[] { new(0.25f, 0.3f), new(0.7f, 0.6f), new(0.5f, 0.8f) };
            _nebulaColor = new[]
            {
                new Color(0.15f, 0.1f, 0.3f, 0.06f),
                new Color(0.1f, 0.2f, 0.35f, 0.05f),
                new Color(0.2f, 0.1f, 0.2f, 0.04f)
            };
            _nebulaRadius = new float[] { 0.35f, 0.3f, 0.25f };
        }

        public void Show(int totalChunks)
        {
            _visible = true;
            _chunksTotal = Mathf.Max(totalChunks, 1);
            _chunksLoaded = 0;
            _progress = 0;
            _statusText = StatusMessages[0];
            _loreQuote = LoreQuotes[Random.Range(0, LoreQuotes.Length)];
            _currentTip = Random.Range(0, Tips.Length);
            _tipTimer = 0;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OnChunkLoaded()
        {
            _chunksLoaded++;
            _progress = Mathf.Clamp01((float)_chunksLoaded / _chunksTotal);
            _statusText = StatusMessages[Mathf.Min((int)(_progress * StatusMessages.Length), StatusMessages.Length - 1)];
            if (_chunksLoaded >= _chunksTotal) Hide();
        }

        public void UpdateProgress(int chunksLoaded, string status = null)
        {
            _chunksLoaded = chunksLoaded;
            _progress = _chunksTotal > 0 ? Mathf.Clamp01((float)chunksLoaded / _chunksTotal) : 0;
            _statusText = status ?? StatusMessages[Mathf.Min((int)(_progress * StatusMessages.Length), StatusMessages.Length - 1)];
            if (_chunksLoaded >= _chunksTotal && _chunksTotal > 0) Hide();
        }

        public void SetZoneName(string name) { if (!string.IsNullOrEmpty(name)) _zoneName = name.ToUpper(); }
        public void SetLoreQuote(string quote) { if (!string.IsNullOrEmpty(quote)) _loreQuote = quote; }

        public void Hide()
        {
            _visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!_visible) return;
            float dt = Time.unscaledDeltaTime;

            _tipTimer += dt;
            if (_tipTimer > 6f) { _tipTimer = 0; _currentTip = (_currentTip + 1) % Tips.Length; }

            float t = Time.unscaledTime;
            for (int i = 0; i < MoteCount; i++)
            {
                float px = Mathf.PerlinNoise(i * 1.7f, t * 0.08f) - 0.5f;
                float py = Mathf.PerlinNoise(i * 2.3f + 100f, t * 0.06f) - 0.5f;
                _motePos[i] += (_moteVel[i] + new Vector2(px, py) * 0.002f) * dt * 10f;
                if (_motePos[i].x < -0.02f) _motePos[i].x = 1.02f;
                if (_motePos[i].x > 1.02f) _motePos[i].x = -0.02f;
                if (_motePos[i].y < -0.02f) _motePos[i].y = 1.02f;
                if (_motePos[i].y > 1.02f) _motePos[i].y = -0.02f;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            float w = Screen.width, h = Screen.height;

            // Background
            GUI.DrawTexture(new Rect(0, 0, w, h), _bgTex);

            // Nebula washes
            var prevColor = GUI.color;
            for (int i = 0; i < _nebulaPos.Length; i++)
            {
                float r = _nebulaRadius[i] * Mathf.Max(w, h);
                float nx = _nebulaPos[i].x * w - r * 0.5f;
                float ny = _nebulaPos[i].y * h - r * 0.5f;
                GUI.color = _nebulaColor[i];
                GUI.DrawTexture(new Rect(nx, ny, r, r), _nebulaTex);
            }

            // Dust motes
            for (int i = 0; i < MoteCount; i++)
            {
                GUI.color = new Color(0.7f, 0.75f, 0.9f, _moteAlpha[i]);
                float s = _moteSize[i];
                GUI.DrawTexture(new Rect(_motePos[i].x * w - s, _motePos[i].y * h - s, s * 2, s * 2), _whiteTex);
            }
            GUI.color = prevColor;

            // Zone name — glow passes then main
            float zoneY = h * 0.22f;
            var zoneGlow = MakeStyle(28, FontStyle.Bold, new Color(0.4f, 0.5f, 0.8f, 0.15f));
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (dx != 0 || dy != 0)
                        GUI.Label(new Rect(dx, zoneY + dy, w, 40), _zoneName, zoneGlow);

            var zoneStyle = MakeStyle(28, FontStyle.Bold, new Color(0.8f, 0.85f, 0.95f));
            GUI.Label(new Rect(0, zoneY, w, 40), _zoneName, zoneStyle);

            // Lore quote
            var loreStyle = MakeStyle(14, FontStyle.Italic, new Color(0.6f, 0.65f, 0.75f), true);
            GUI.Label(new Rect(w * 0.15f, h * 0.42f, w * 0.7f, 60), $"\"{_loreQuote}\"", loreStyle);

            // Progress bar
            float barW = 400f, barH = 6f;
            float barX = (w - barW) * 0.5f, barY = h * 0.62f;
            DrawRect(barX, barY, barW, barH, new Color(0.1f, 0.1f, 0.15f));
            float fillW = barW * _progress;
            if (fillW > 0)
            {
                DrawRect(barX, barY, fillW, barH, new Color(0.3f, 0.5f, 0.9f));
                // Leading glow
                if (fillW > 2)
                    DrawRect(barX + fillW - 4, barY - 1, 8, barH + 2, new Color(0.85f, 0.92f, 1f, 0.7f));
            }

            // Percentage above bar
            var pctStyle = MakeStyle(14, FontStyle.Normal, new Color(0.7f, 0.75f, 0.85f));
            GUI.Label(new Rect(0, barY - 24, w, 20), $"{Mathf.RoundToInt(_progress * 100)}%", pctStyle);

            // Status below bar
            var statStyle = MakeStyle(12, FontStyle.Normal, new Color(0.45f, 0.5f, 0.6f));
            GUI.Label(new Rect(0, barY + barH + 6, w, 20), _statusText, statStyle);

            // Tip at bottom
            string tip = Tips[_currentTip];
            float tipY = h * 0.82f;
            var tipPrefixStyle = MakeStyle(13, FontStyle.Normal, new Color(0.6f, 0.7f, 0.9f));
            var tipBodyStyle = MakeStyle(13, FontStyle.Normal, new Color(0.5f, 0.5f, 0.6f), true);
            GUI.Label(new Rect(0, tipY, w, 20), "TIP:", tipPrefixStyle);
            GUI.Label(new Rect(w * 0.1f, tipY + 20, w * 0.8f, 40), tip, tipBodyStyle);
        }

        private GUIStyle MakeStyle(int size, FontStyle font, Color color, bool wrap = false)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = font,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = wrap,
                normal = { textColor = color }
            };
        }

        private void DrawRect(float x, float y, float rw, float rh, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, rw, rh), _whiteTex);
            GUI.color = prev;
        }

        private static Texture2D MakeSolid(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
        }

        private static Texture2D MakeSoftCircle(int res)
        {
            var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float half = res * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - d * d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }
    }
}

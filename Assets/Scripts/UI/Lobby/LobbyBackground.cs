using UnityEngine;

namespace Orlo.UI.Lobby
{
    /// <summary>
    /// Animated procedural space backdrop rendered behind all lobby screens (login,
    /// character select). Draws a star field, nebula clouds, a planet with atmosphere,
    /// and drifting particle dust entirely through OnGUI.
    /// </summary>
    public class LobbyBackground : MonoBehaviour
    {
        public static LobbyBackground Instance { get; private set; }

        private bool _visible;

        // --- Stars ---
        private struct Star
        {
            public Vector2 pos;       // normalized 0-1
            public float brightness;
            public float twinklePhase;
            public float twinkleSpeed;
            public float size;
        }

        private const int StarCount = 240;
        private Star[] _stars;

        // --- Nebula ---
        private struct NebulaBlob
        {
            public Vector2 pos;       // normalized 0-1
            public Color color;
            public float radius;      // normalized to screen height
            public Vector2 driftDir;
            public float driftSpeed;
        }

        private const int NebulaCount = 4;
        private NebulaBlob[] _nebulae;

        // --- Dust ---
        private struct DustMote
        {
            public Vector2 pos;       // normalized 0-1
            public float speed;
            public float alpha;
            public float size;
            public float angle;       // drift direction in radians
        }

        private const int DustCount = 36;
        private DustMote[] _dust;

        // --- Planet ---
        private Vector2 _planetCenter;   // normalized
        private float _planetRadius;     // normalized to screen height
        private const int AtmosphereRings = 12;

        // --- Textures ---
        private Texture2D _pixelTex;
        private Texture2D _softCircleTex;
        private bool _texturesBuilt;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildTextures();
            InitStars();
            InitNebulae();
            InitDust();
            InitPlanet();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_pixelTex != null) Destroy(_pixelTex);
            if (_softCircleTex != null) Destroy(_softCircleTex);
        }

        public void Show() { _visible = true; }
        public void Hide() { _visible = false; }

        // ---------------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------------

        private void BuildTextures()
        {
            _pixelTex = new Texture2D(1, 1);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();

            // 32x32 soft circle for nebula blobs and dust
            int size = 32;
            _softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d * d); // quadratic falloff
                    _softCircleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            _softCircleTex.Apply();
            _softCircleTex.wrapMode = TextureWrapMode.Clamp;
            _texturesBuilt = true;
        }

        private void InitStars()
        {
            _stars = new Star[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                _stars[i].pos = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f));
                _stars[i].brightness = Random.Range(0.25f, 1f);
                _stars[i].twinklePhase = Random.Range(0f, Mathf.PI * 2f);
                _stars[i].twinkleSpeed = Random.Range(0.8f, 3f);
                _stars[i].size = Random.Range(1f, 2.5f);
            }
        }

        private void InitNebulae()
        {
            _nebulae = new NebulaBlob[NebulaCount];

            // Deep blue
            _nebulae[0] = new NebulaBlob
            {
                pos = new Vector2(0.25f, 0.35f),
                color = new Color(0.08f, 0.12f, 0.35f, 0.12f),
                radius = 0.35f,
                driftDir = new Vector2(0.3f, 0.1f).normalized,
                driftSpeed = 0.003f
            };
            // Purple
            _nebulae[1] = new NebulaBlob
            {
                pos = new Vector2(0.65f, 0.25f),
                color = new Color(0.2f, 0.06f, 0.3f, 0.1f),
                radius = 0.3f,
                driftDir = new Vector2(-0.2f, 0.15f).normalized,
                driftSpeed = 0.004f
            };
            // Teal
            _nebulae[2] = new NebulaBlob
            {
                pos = new Vector2(0.4f, 0.7f),
                color = new Color(0.04f, 0.18f, 0.22f, 0.1f),
                radius = 0.28f,
                driftDir = new Vector2(0.15f, -0.1f).normalized,
                driftSpeed = 0.0025f
            };
            // Faint warm accent
            _nebulae[3] = new NebulaBlob
            {
                pos = new Vector2(0.8f, 0.6f),
                color = new Color(0.15f, 0.05f, 0.2f, 0.07f),
                radius = 0.22f,
                driftDir = new Vector2(-0.1f, -0.2f).normalized,
                driftSpeed = 0.002f
            };
        }

        private void InitDust()
        {
            _dust = new DustMote[DustCount];
            for (int i = 0; i < DustCount; i++)
            {
                _dust[i].pos = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f));
                _dust[i].speed = Random.Range(0.002f, 0.008f);
                _dust[i].alpha = Random.Range(0.08f, 0.25f);
                _dust[i].size = Random.Range(2f, 5f);
                _dust[i].angle = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private void InitPlanet()
        {
            _planetCenter = new Vector2(0.78f, 0.72f);
            _planetRadius = 0.18f;
        }

        // ---------------------------------------------------------------
        // Update
        // ---------------------------------------------------------------

        private void Update()
        {
            if (!_visible) return;

            float dt = Time.deltaTime;

            // Drift nebulae (wrap around)
            for (int i = 0; i < NebulaCount; i++)
            {
                _nebulae[i].pos += _nebulae[i].driftDir * _nebulae[i].driftSpeed * dt;
                if (_nebulae[i].pos.x < -0.3f) _nebulae[i].pos.x += 1.6f;
                if (_nebulae[i].pos.x > 1.3f) _nebulae[i].pos.x -= 1.6f;
                if (_nebulae[i].pos.y < -0.3f) _nebulae[i].pos.y += 1.6f;
                if (_nebulae[i].pos.y > 1.3f) _nebulae[i].pos.y -= 1.6f;
            }

            // Drift dust motes
            for (int i = 0; i < DustCount; i++)
            {
                float dx = Mathf.Cos(_dust[i].angle) * _dust[i].speed * dt;
                float dy = Mathf.Sin(_dust[i].angle) * _dust[i].speed * dt;
                _dust[i].pos.x += dx;
                _dust[i].pos.y += dy;
                // Wrap
                if (_dust[i].pos.x < -0.02f) _dust[i].pos.x = 1.02f;
                if (_dust[i].pos.x > 1.02f) _dust[i].pos.x = -0.02f;
                if (_dust[i].pos.y < -0.02f) _dust[i].pos.y = 1.02f;
                if (_dust[i].pos.y > 1.02f) _dust[i].pos.y = -0.02f;
            }
        }

        // ---------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------

        private void OnGUI()
        {
            if (!_visible || !_texturesBuilt) return;

            float sw = Screen.width;
            float sh = Screen.height;
            float t = Time.time;

            DrawBackground(sw, sh);
            DrawNebulae(sw, sh, t);
            DrawStars(sw, sh, t);
            DrawPlanet(sw, sh, t);
            DrawDust(sw, sh);

            GUI.color = Color.white;
        }

        private void DrawBackground(float sw, float sh)
        {
            // Deep navy-black gradient (solid dark fill)
            GUI.color = new Color(0.02f, 0.025f, 0.06f, 1f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixelTex);

            // Slight vertical gradient: darker at top, marginally lighter at bottom
            GUI.color = new Color(0.03f, 0.04f, 0.08f, 0.4f);
            GUI.DrawTexture(new Rect(0, sh * 0.5f, sw, sh * 0.5f), _pixelTex);
        }

        private void DrawStars(float sw, float sh, float t)
        {
            for (int i = 0; i < StarCount; i++)
            {
                ref var s = ref _stars[i];
                float twinkle = (Mathf.Sin(t * s.twinkleSpeed + s.twinklePhase) + 1f) * 0.5f;
                float a = s.brightness * Mathf.Lerp(0.4f, 1f, twinkle);

                GUI.color = new Color(0.9f, 0.92f, 1f, a);
                float sz = s.size;
                float x = s.pos.x * sw;
                float y = s.pos.y * sh;
                GUI.DrawTexture(new Rect(x - sz * 0.5f, y - sz * 0.5f, sz, sz), _pixelTex);

                // Brighter stars get a small cross
                if (s.brightness > 0.8f)
                {
                    GUI.color = new Color(0.85f, 0.88f, 1f, a * 0.35f);
                    float armLen = sz * 2f;
                    GUI.DrawTexture(new Rect(x - armLen * 0.5f, y - 0.5f, armLen, 1f), _pixelTex);
                    GUI.DrawTexture(new Rect(x - 0.5f, y - armLen * 0.5f, 1f, armLen), _pixelTex);
                }
            }
        }

        private void DrawNebulae(float sw, float sh, float t)
        {
            for (int i = 0; i < NebulaCount; i++)
            {
                ref var n = ref _nebulae[i];
                float cx = n.pos.x * sw;
                float cy = n.pos.y * sh;
                float r = n.radius * sh;

                // Pulsing brightness
                float pulse = 1f + Mathf.Sin(t * 0.3f + i * 1.7f) * 0.15f;

                // Draw soft circle multiple times at slight offsets for more organic shape
                for (int j = 0; j < 3; j++)
                {
                    float ox = Mathf.Sin(t * 0.2f + j * 2.1f + i) * r * 0.1f;
                    float oy = Mathf.Cos(t * 0.15f + j * 1.7f + i) * r * 0.08f;
                    float scale = 1f + j * 0.15f;
                    float drawR = r * scale;

                    Color c = n.color;
                    c.a *= pulse * (1f - j * 0.25f);
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(cx - drawR + ox, cy - drawR + oy, drawR * 2f, drawR * 2f), _softCircleTex);
                }
            }
        }

        private void DrawPlanet(float sw, float sh, float t)
        {
            float cx = _planetCenter.x * sw;
            float cy = _planetCenter.y * sh;
            float r = _planetRadius * sh;

            // Atmosphere glow rings (outermost first)
            for (int i = AtmosphereRings; i >= 1; i--)
            {
                float ringScale = 1f + i * 0.06f;
                float ringR = r * ringScale;
                float a = 0.03f / (i * 0.5f);

                // Blue-green atmosphere tint
                GUI.color = new Color(0.15f, 0.4f, 0.5f, a);
                GUI.DrawTexture(new Rect(cx - ringR, cy - ringR, ringR * 2f, ringR * 2f), _softCircleTex);
            }

            // Planet body (dark sphere)
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 1f);
            GUI.DrawTexture(new Rect(cx - r, cy - r, r * 2f, r * 2f), _softCircleTex);

            // Solid dark core (sharper)
            float coreR = r * 0.85f;
            GUI.color = new Color(0.02f, 0.025f, 0.04f, 1f);
            GUI.DrawTexture(new Rect(cx - coreR, cy - coreR, coreR * 2f, coreR * 2f), _softCircleTex);

            // Terminator shadow (dark overlay on the right side to imply rotation)
            float shadowShift = r * 0.25f;
            float shadowR = r * 0.95f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(cx - shadowR + shadowShift, cy - shadowR, shadowR * 2f, shadowR * 2f), _softCircleTex);

            // Lit rim on left edge (atmosphere catch light)
            float rimWidth = r * 0.08f;
            float rimHeight = r * 1.4f;
            GUI.color = new Color(0.2f, 0.5f, 0.6f, 0.2f);
            GUI.DrawTexture(new Rect(cx - r - rimWidth * 0.5f, cy - rimHeight * 0.5f, rimWidth * 3f, rimHeight), _softCircleTex);

            // Subtle surface markings (two faint bands)
            float bandY1 = cy - r * 0.15f;
            float bandY2 = cy + r * 0.25f;
            GUI.color = new Color(0.06f, 0.08f, 0.1f, 0.15f);
            GUI.DrawTexture(new Rect(cx - r * 0.7f, bandY1, r * 1.2f, r * 0.06f), _softCircleTex);
            GUI.color = new Color(0.05f, 0.07f, 0.09f, 0.1f);
            GUI.DrawTexture(new Rect(cx - r * 0.6f, bandY2, r * 1.0f, r * 0.05f), _softCircleTex);

            // Bright atmosphere edge highlight (top-left crescent)
            float highlightR = r * 1.02f;
            GUI.color = new Color(0.3f, 0.6f, 0.7f, 0.08f);
            GUI.DrawTexture(new Rect(cx - highlightR - r * 0.05f, cy - highlightR - r * 0.05f,
                highlightR * 2f, highlightR * 2f), _softCircleTex);
        }

        private void DrawDust(float sw, float sh)
        {
            for (int i = 0; i < DustCount; i++)
            {
                ref var d = ref _dust[i];
                float x = d.pos.x * sw;
                float y = d.pos.y * sh;

                GUI.color = new Color(0.6f, 0.65f, 0.8f, d.alpha);
                float sz = d.size;
                GUI.DrawTexture(new Rect(x - sz * 0.5f, y - sz * 0.5f, sz, sz), _softCircleTex);
            }
        }
    }
}

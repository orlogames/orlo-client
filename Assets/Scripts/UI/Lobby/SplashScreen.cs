using System;
using UnityEngine;

namespace Orlo.UI.Lobby
{
    /// <summary>
    /// Splash screen that plays on game startup. Three phases: logo fade, procedural
    /// wormhole intro, and title hold. First launch plays full sequence; subsequent
    /// launches skip straight to the title phase.
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        public static SplashScreen Instance { get; private set; }

        public Action OnComplete;

        private enum Phase { Logo, Intro, Title, Done }

        private bool _visible;
        private Phase _phase;
        private float _phaseTimer;
        private float _titlePulseTimer;
        private bool _introSeen;

        // Wormhole particles
        private struct WormholeParticle
        {
            public Vector2 dir;    // normalized direction from center
            public float dist;     // current distance from center (0-1)
            public float speed;    // base speed multiplier
            public float brightness;
        }

        private const int WormholeCount = 280;
        private WormholeParticle[] _wormhole;

        // Timing
        private const float LogoDuration = 2f;
        private const float LogoFadeIn = 0.4f;
        private const float LogoFadeOut = 0.4f;
        private const float IntroDuration = 5f;
        private const float IntroTitleReveal = 3.5f; // seconds into intro when ORLO starts appearing
        private const float IntroFlashTime = 4.2f;

        // Cached styles and textures
        private GUIStyle _logoStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _versionStyle;
        private Texture2D _pixelTex;
        private bool _stylesBuilt;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _pixelTex = new Texture2D(1, 1);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();

            InitWormhole();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_pixelTex != null) Destroy(_pixelTex);
        }

        public void Show()
        {
            _visible = true;
            _introSeen = PlayerPrefs.GetInt("intro_seen", 0) == 1;

            if (_introSeen)
            {
                _phase = Phase.Title;
                _phaseTimer = 0f;
            }
            else
            {
                _phase = Phase.Logo;
                _phaseTimer = 0f;
            }
            _titlePulseTimer = 0f;
        }

        public void Hide()
        {
            _visible = false;
            _phase = Phase.Done;
        }

        private void InitWormhole()
        {
            _wormhole = new WormholeParticle[WormholeCount];
            for (int i = 0; i < WormholeCount; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                _wormhole[i].dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _wormhole[i].dist = UnityEngine.Random.Range(0.3f, 1.0f);
                _wormhole[i].speed = UnityEngine.Random.Range(0.08f, 0.25f);
                _wormhole[i].brightness = UnityEngine.Random.Range(0.5f, 1.0f);
            }
        }

        private void Update()
        {
            if (!_visible || _phase == Phase.Done) return;

            _phaseTimer += Time.deltaTime;
            _titlePulseTimer += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Logo:
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        Complete();
                        return;
                    }
                    if (_phaseTimer >= LogoDuration)
                    {
                        _phase = Phase.Intro;
                        _phaseTimer = 0f;
                        InitWormhole();
                    }
                    break;

                case Phase.Intro:
                    if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
                    {
                        SkipToTitle();
                        return;
                    }
                    UpdateWormhole();
                    if (_phaseTimer >= IntroDuration)
                    {
                        SkipToTitle();
                    }
                    break;

                case Phase.Title:
                    if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
                    {
                        Complete();
                    }
                    break;
            }
        }

        private void SkipToTitle()
        {
            _phase = Phase.Title;
            _phaseTimer = 0f;
            _titlePulseTimer = 0f;
            PlayerPrefs.SetInt("intro_seen", 1);
            PlayerPrefs.Save();
        }

        private void Complete()
        {
            _visible = false;
            _phase = Phase.Done;
            PlayerPrefs.SetInt("intro_seen", 1);
            PlayerPrefs.Save();
            OnComplete?.Invoke();
        }

        private void UpdateWormhole()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < WormholeCount; i++)
            {
                // Particles accelerate as they approach center
                float accel = 1f + (1f - _wormhole[i].dist) * 3f;
                _wormhole[i].dist -= _wormhole[i].speed * accel * dt;
                if (_wormhole[i].dist <= 0.01f)
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    _wormhole[i].dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    _wormhole[i].dist = UnityEngine.Random.Range(0.85f, 1.0f);
                    _wormhole[i].speed = UnityEngine.Random.Range(0.08f, 0.25f);
                    _wormhole[i].brightness = UnityEngine.Random.Range(0.5f, 1.0f);
                }
            }
        }

        private void BuildStyles()
        {
            _logoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 96,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.75f, 0.8f) }
            };

            _versionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.LowerLeft,
                normal = { textColor = new Color(0.4f, 0.4f, 0.45f) }
            };

            _stylesBuilt = true;
        }

        private void OnGUI()
        {
            if (!_visible || _phase == Phase.Done) return;
            if (!_stylesBuilt) BuildStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // Full-screen dark background
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixelTex);
            GUI.color = Color.white;

            switch (_phase)
            {
                case Phase.Logo: DrawLogo(sw, sh); break;
                case Phase.Intro: DrawIntro(sw, sh); break;
                case Phase.Title: DrawTitle(sw, sh); break;
            }
        }

        private void DrawLogo(float sw, float sh)
        {
            float alpha;
            if (_phaseTimer < LogoFadeIn)
                alpha = _phaseTimer / LogoFadeIn;
            else if (_phaseTimer > LogoDuration - LogoFadeOut)
                alpha = (LogoDuration - _phaseTimer) / LogoFadeOut;
            else
                alpha = 1f;

            alpha = Mathf.Clamp01(alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0, sh * 0.45f, sw, 60f), "ORLOGAMES", _logoStyle);
            GUI.color = Color.white;
        }

        private void DrawIntro(float sw, float sh)
        {
            float cx = sw * 0.5f;
            float cy = sh * 0.5f;
            float maxRadius = Mathf.Max(sw, sh) * 0.55f;

            // Draw wormhole particles
            for (int i = 0; i < WormholeCount; i++)
            {
                ref var p = ref _wormhole[i];
                float x = cx + p.dir.x * p.dist * maxRadius;
                float y = cy + p.dir.y * p.dist * maxRadius;

                // Particles get brighter and larger as they approach center
                float proximity = 1f - p.dist;
                float size = Mathf.Lerp(1f, 4f, proximity);
                float a = p.brightness * Mathf.Lerp(0.3f, 1f, proximity);

                // Slight blue-white tint
                GUI.color = new Color(0.8f + proximity * 0.2f, 0.85f + proximity * 0.15f, 1f, a);
                GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), _pixelTex);
            }

            // Draw streaks (elongated toward center) for faster particles
            for (int i = 0; i < WormholeCount; i += 3)
            {
                ref var p = ref _wormhole[i];
                if (p.dist > 0.6f) continue;
                float proximity = 1f - p.dist;
                float streakLen = Mathf.Lerp(2f, 20f, proximity);
                float x = cx + p.dir.x * p.dist * maxRadius;
                float y = cy + p.dir.y * p.dist * maxRadius;
                float ex = x + p.dir.x * streakLen;
                float ey = y + p.dir.y * streakLen;

                float a = p.brightness * proximity * 0.5f;
                GUI.color = new Color(0.9f, 0.92f, 1f, a);
                // Approximate streak as thin rect along direction
                float dx = ex - x;
                float dy = ey - y;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                GUI.DrawTexture(new Rect(Mathf.Min(x, ex), Mathf.Min(y, ey),
                    Mathf.Max(2f, Mathf.Abs(dx)), Mathf.Max(2f, Mathf.Abs(dy))), _pixelTex);
            }

            // Title reveal near end of intro
            if (_phaseTimer > IntroTitleReveal)
            {
                float revealAlpha = Mathf.Clamp01((_phaseTimer - IntroTitleReveal) / 0.8f);

                // Flash effect
                if (_phaseTimer > IntroFlashTime && _phaseTimer < IntroFlashTime + 0.3f)
                {
                    float flashAlpha = 1f - ((_phaseTimer - IntroFlashTime) / 0.3f);
                    GUI.color = new Color(1f, 1f, 1f, flashAlpha * 0.6f);
                    GUI.DrawTexture(new Rect(0, 0, sw, sh), _pixelTex);
                }

                DrawGlowText("ORLO", new Rect(0, sh * 0.4f, sw, 120f), _titleStyle, revealAlpha);
            }

            GUI.color = Color.white;
        }

        private void DrawTitle(float sw, float sh)
        {
            // Subtle glow pulse on title
            float pulse = 0.85f + Mathf.Sin(_titlePulseTimer * 1.5f) * 0.15f;
            DrawGlowText("ORLO", new Rect(0, sh * 0.35f, sw, 120f), _titleStyle, pulse);

            // "Press Any Key" fading in and out
            float promptAlpha = (Mathf.Sin(_titlePulseTimer * 2.5f) + 1f) * 0.5f;
            promptAlpha = Mathf.Lerp(0.2f, 0.9f, promptAlpha);
            // Delay appearance slightly
            if (_phaseTimer > 0.5f)
            {
                float fadeIn = Mathf.Clamp01((_phaseTimer - 0.5f) / 0.5f);
                GUI.color = new Color(0.7f, 0.75f, 0.8f, promptAlpha * fadeIn);
                GUI.Label(new Rect(0, sh * 0.58f, sw, 30f), "Press Any Key to Continue", _promptStyle);
            }

            // Version number bottom-left
            string version = Application.version;
            GUI.color = new Color(0.4f, 0.4f, 0.45f, 0.8f);
            GUI.Label(new Rect(12f, sh - 30f, 200f, 24f), $"v{version}", _versionStyle);

            GUI.color = Color.white;
        }

        /// <summary>
        /// Draws text with a soft glow by rendering it multiple times at slight offsets
        /// with reduced alpha, then the main text on top.
        /// </summary>
        private void DrawGlowText(string text, Rect rect, GUIStyle style, float alpha)
        {
            // Outer glow layers
            Color glowColor = new Color(0.6f, 0.7f, 1f, alpha * 0.08f);
            for (int i = 0; i < 3; i++)
            {
                float offset = (i + 1) * 3f;
                GUI.color = glowColor;
                GUI.Label(new Rect(rect.x - offset, rect.y - offset, rect.width + offset * 2, rect.height + offset * 2), text, style);
            }

            // Inner glow
            GUI.color = new Color(0.7f, 0.8f, 1f, alpha * 0.2f);
            GUI.Label(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), text, style);
            GUI.Label(new Rect(rect.x + 1, rect.y - 1, rect.width + 2, rect.height + 2), text, style);
            GUI.Label(new Rect(rect.x - 1, rect.y + 1, rect.width + 2, rect.height + 2), text, style);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width + 2, rect.height + 2), text, style);

            // Main text
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, text, style);
        }
    }
}

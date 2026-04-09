using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;
using Orlo.UI.Settings;
using Orlo.UI.TMD;
using ProtoAuth = Orlo.Proto.Auth;

namespace Orlo.UI
{
    /// <summary>
    /// Main game HUD showing connection info, target frame, XP bar, currency,
    /// buff/debuff icons, and compass heading.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        private float _rtt;
        private string _lastNotification;
        private float _notificationTimer;

        // ── Target Frame ──────────────────────────────────────────────
        private bool _hasTarget;
        private string _targetName = "";
        private int _targetLevel;
        private float _targetHealth;
        private float _targetMaxHealth = 1;
        private bool _targetHostile;

        // ── XP Bar ────────────────────────────────────────────────────
        private int _playerLevel = 1;
        private ulong _currentXp;
        private ulong _xpToNext = 1;

        // ── Currency ──────────────────────────────────────────────────
        private long _credits;

        // ── Buffs/Debuffs ─────────────────────────────────────────────
        public struct BuffIcon
        {
            public string Name;
            public float Duration;      // remaining
            public float MaxDuration;
            public bool IsDebuff;
            public Color IconColor;
        }
        private readonly List<BuffIcon> _buffs = new();

        // ── Compass ───────────────────────────────────────────────────
        private float _compassHeading; // 0-360

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (PacketHandler.Instance != null)
                PacketHandler.Instance.OnPong += OnPong;
        }

        private void OnDisable()
        {
            if (PacketHandler.Instance != null)
                PacketHandler.Instance.OnPong -= OnPong;
        }

        private void Update()
        {
            if (_notificationTimer > 0)
                _notificationTimer -= Time.deltaTime;

            // Update compass from camera
            if (Camera.main != null)
            {
                _compassHeading = Camera.main.transform.eulerAngles.y;
            }

            // Tick buff durations
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var b = _buffs[i];
                b.Duration -= Time.deltaTime;
                if (b.Duration <= 0)
                    _buffs.RemoveAt(i);
                else
                    _buffs[i] = b;
            }
        }

        private void OnPong(ProtoAuth.Pong pong)
        {
            _rtt = (float)(Time.realtimeSinceStartup * 1000 - pong.ClientTime.Ms);
        }

        // ── Public read-only accessors for UIToolkit views ─────────────
        public float  RTT            => _rtt;
        public bool   HasTarget      => _hasTarget;
        public string TargetName     => _targetName;
        public int    TargetLevel    => _targetLevel;
        public float  TargetHealth   => _targetHealth;
        public float  TargetMaxHealth => _targetMaxHealth;
        public bool   TargetHostile  => _targetHostile;
        public long   Credits        => _credits;
        public ulong  CurrentXP      => _currentXp;
        public ulong  XPToNext       => _xpToNext;
        public int    PlayerLevel    => _playerLevel;

        // ── Public API ──────────────────────────────────────────────────

        public void ShowNotification(string text, float duration = 5f)
        {
            _lastNotification = text;
            _notificationTimer = duration;
        }

        public void SetTarget(string name, int level, float health, float maxHealth, bool hostile)
        {
            _hasTarget = true;
            _targetName = name;
            _targetLevel = level;
            _targetHealth = health;
            _targetMaxHealth = maxHealth > 0 ? maxHealth : 1;
            _targetHostile = hostile;
        }

        public void ClearTarget()
        {
            _hasTarget = false;
        }

        public void SetXP(int level, ulong currentXp, ulong xpToNext)
        {
            _playerLevel = level;
            _currentXp = currentXp;
            _xpToNext = xpToNext > 0 ? xpToNext : 1;
        }

        public void SetCredits(long credits)
        {
            _credits = credits;
        }

        public void AddBuff(string name, float duration, bool isDebuff, Color color)
        {
            // Replace existing buff of same name
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Name == name)
                {
                    _buffs[i] = new BuffIcon { Name = name, Duration = duration, MaxDuration = duration, IsDebuff = isDebuff, IconColor = color };
                    return;
                }
            }
            _buffs.Add(new BuffIcon { Name = name, Duration = duration, MaxDuration = duration, IsDebuff = isDebuff, IconColor = color });
        }

        public void RemoveBuff(string name)
        {
            _buffs.RemoveAll(b => b.Name == name);
        }

        // ── OnGUI ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (UIToolkitRoot.IsActive("GameHUD")) return;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(14),
                normal = { textColor = Color.white }
            };

            float y = 10;

            // Connection status (top-left)
            bool connected = NetworkManager.Instance != null && NetworkManager.Instance.IsConnected;
            GUI.Label(new Rect(10, y, 300, 20),
                connected ? $"<color=lime>Connected</color> | RTT: {_rtt:F1}ms" : "<color=red>Disconnected</color>",
                style);
            y += 22;

            // Player position
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var pos = player.transform.position;
                GUI.Label(new Rect(10, y, 400, 20),
                    $"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", style);
                y += 22;
            }

            // ── Compass heading (top center) ────────────────────────
            DrawCompass();

            // ── Target frame (top center-right) ─────────────────────
            if (_hasTarget)
                DrawTargetFrame();

            // ── Currency (top-right) ────────────────────────────────
            DrawCurrency();

            // ── Mail notification icon ──────────────────────────────
            if (MailUI.UnreadCount > 0)
            {
                var mp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;
                float mailX = Screen.width - 160;
                float mailY = 10;
                GUI.color = new Color(mp.Accent.r, mp.Accent.g, mp.Accent.b, 0.9f);
                GUI.DrawTexture(new Rect(mailX, mailY, 18, 18), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var mailStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
                mailStyle.fontSize = 10;
                mailStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(mailX, mailY, 18, 18), MailUI.UnreadCount.ToString(), mailStyle);
            }

            // ── Buff/Debuff icons (below health bars) ───────────────
            DrawBuffs();

            // ── XP bar (bottom of screen) ───────────────────────────
            DrawXPBar();

            // ── Content reveal notification ─────────────────────────
            if (_notificationTimer > 0 && !string.IsNullOrEmpty(_lastNotification))
            {
                var np = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;
                float alpha = Mathf.Min(1f, _notificationTimer);
                Rect notifRect = new Rect(Screen.width / 2 - 200, 50, 400, 50);
                GUI.color = new Color(1, 1, 1, alpha);
                TMDTheme.DrawPanel(notifRect);
                var notifStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.TitleStyle) : new GUIStyle(GUI.skin.label);
                notifStyle.fontSize = 18;
                notifStyle.alignment = TextAnchor.MiddleCenter;
                notifStyle.normal.textColor = new Color(np.Primary.r, np.Primary.g, np.Primary.b, alpha);
                GUI.Label(notifRect, _lastNotification, notifStyle);
                GUI.color = Color.white;
            }
        }

        // ── Compass ─────────────────────────────────────────────────────

        private void DrawCompass()
        {
            float compassW = 300f;
            float compassH = 24f;
            float cx = (Screen.width - compassW) / 2f;
            float cy = 8f;

            var p = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // Background — TMD panel
            TMDTheme.DrawPanel(new Rect(cx, cy, compassW, compassH));

            // Center indicator — race primary
            GUI.color = new Color(p.Primary.r, p.Primary.g, p.Primary.b, 0.9f);
            GUI.DrawTexture(new Rect(cx + compassW / 2f - 1, cy, 2, compassH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Cardinal directions
            string[] cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            float[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };

            var cardStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            for (int i = 0; i < cardinals.Length; i++)
            {
                float diff = Mathf.DeltaAngle(_compassHeading, angles[i]);
                float normalized = diff / 180f; // -1 to 1
                float screenX = cx + compassW / 2f + normalized * (compassW / 2f);

                if (screenX > cx + 10 && screenX < cx + compassW - 10)
                {
                    bool isCardinal = i % 2 == 0;
                    var s = isCardinal ? cardStyle : subStyle;
                    if (cardinals[i] == "N") s = new GUIStyle(s) { normal = { textColor = new Color(1f, 0.4f, 0.3f) } };
                    GUI.Label(new Rect(screenX - 15, cy, 30, compassH), cardinals[i], s);
                }
            }

            // Degree readout below — race text
            var degStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = p.TextDim }
            };
            GUI.Label(new Rect(cx, cy + compassH, compassW, 14), $"{_compassHeading:F0}", degStyle);
        }

        // ── Target Frame ────────────────────────────────────────────────

        private void DrawTargetFrame()
        {
            float frameW = 220f;
            float frameH = 48f;
            float fx = Screen.width / 2f + 60f;
            float fy = 50f;

            var tp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // TMD panel background
            Rect frameRect = new Rect(fx, fy, frameW, frameH);
            TMDTheme.DrawPanel(frameRect);

            // Border color based on hostility (colorblind-safe), overlaid on TMD border
            var am = AccessibilityManager.Instance;
            Color borderColor = _targetHostile ? tp.Danger : tp.Success;
            if (am != null) borderColor = am.RemapColor(borderColor);
            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(fx, fy, frameW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(fx, fy + frameH - 2, frameW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(fx, fy, 2, frameH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(fx + frameW - 2, fy, 2, frameH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Name + Level (colorblind-safe)
            Color nameColor = _targetHostile ? new Color(1f, 0.4f, 0.4f) : new Color(0.5f, 1f, 0.5f);
            if (am != null) nameColor = am.RemapColor(nameColor);
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(12), fontStyle = FontStyle.Bold,
                normal = { textColor = nameColor }
            };
            GUI.Label(new Rect(fx + 6, fy + 3, frameW - 50, 18), _targetName, nameStyle);

            var lvlStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            GUI.Label(new Rect(fx + frameW - 50, fy + 3, 44, 18), $"Lv.{_targetLevel}", lvlStyle);

            // Health bar — TMD styled
            float barX = fx + 6;
            float barY = fy + 24;
            float barW = frameW - 12;
            float barH = 14f;
            float fill = Mathf.Clamp01(_targetHealth / _targetMaxHealth);

            Color hpColor = fill > 0.5f ? tp.Success :
                            fill > 0.25f ? new Color(0.9f, 0.7f, 0.1f) :
                            tp.Danger;
            TMDTheme.DrawProgressBar(new Rect(barX, barY, barW, barH), fill, hpColor);
            GUI.color = Color.white;

            var hpStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
            hpStyle.fontSize = 10;
            hpStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(barX, barY, barW, barH), $"{_targetHealth:F0} / {_targetMaxHealth:F0}", hpStyle);

            // Scanlines on target frame
            TMDTheme.DrawScanlines(frameRect);
        }

        // ── Currency ────────────────────────────────────────────────────

        private void DrawCurrency()
        {
            float cw = 140f;
            float ch = 22f;
            float cx = Screen.width - cw - 10;
            float cy = 10;

            var cp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            TMDTheme.DrawPanel(new Rect(cx, cy, cw, ch));

            var credStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
            credStyle.fontSize = 12;
            credStyle.alignment = TextAnchor.MiddleRight;
            credStyle.normal.textColor = cp.Primary;
            GUI.Label(new Rect(cx + 4, cy, cw - 8, ch), $"{_credits:N0} cr", credStyle);
        }

        // ── Buffs/Debuffs ───────────────────────────────────────────────

        private void DrawBuffs()
        {
            if (_buffs.Count == 0) return;

            var bp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // Draw below the health pools area (bottom-left, above combat bar)
            float startX = 14f;
            float startY = Screen.height - 90f;
            float iconSize = 28f;
            float spacing = 4f;

            for (int i = 0; i < _buffs.Count; i++)
            {
                var b = _buffs[i];
                float x = startX + i * (iconSize + spacing);
                float y = startY;

                Rect iconRect = new Rect(x, y, iconSize, iconSize);

                // Icon background — race-tinted
                GUI.color = b.IsDebuff
                    ? new Color(bp.Danger.r * 0.4f, bp.Danger.g * 0.1f, bp.Danger.b * 0.1f, 0.85f)
                    : new Color(bp.Primary.r * 0.1f, bp.Primary.g * 0.2f, bp.Primary.b * 0.4f, 0.85f);
                GUI.DrawTexture(iconRect, Texture2D.whiteTexture);

                // Race-colored border
                GUI.color = b.IsDebuff ? bp.Danger : bp.Border;
                GUI.DrawTexture(new Rect(x, y, iconSize, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x, y + iconSize - 1, iconSize, 1), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x, y, 1, iconSize), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x + iconSize - 1, y, 1, iconSize), Texture2D.whiteTexture);

                // Colored fill
                GUI.color = new Color(b.IconColor.r, b.IconColor.g, b.IconColor.b, 0.6f);
                GUI.DrawTexture(new Rect(x + 2, y + 2, iconSize - 4, iconSize - 4), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Duration remaining (seconds) as overlay
                if (b.Duration < 60f)
                {
                    var durStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 9, alignment = TextAnchor.LowerCenter,
                        normal = { textColor = Color.white }
                    };
                    GUI.Label(new Rect(x, y, iconSize, iconSize), $"{b.Duration:F0}", durStyle);
                }

                // Cooldown sweep (dark overlay from top, proportional to time elapsed)
                if (b.MaxDuration > 0)
                {
                    float elapsed = 1f - Mathf.Clamp01(b.Duration / b.MaxDuration);
                    GUI.color = new Color(0, 0, 0, 0.5f);
                    GUI.DrawTexture(new Rect(x, y, iconSize, iconSize * elapsed), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // Tooltip on hover
                if (iconRect.Contains(Event.current.mousePosition))
                {
                    float tipW = 120f;
                    Rect tipRect = new Rect(x, y - 24, tipW, 20);
                    TMDTheme.DrawPanel(tipRect);
                    var tipStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
                    tipStyle.fontSize = 11;
                    tipStyle.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(tipRect, b.Name, tipStyle);
                }
            }
        }

        // ── XP Bar ──────────────────────────────────────────────────────

        private void DrawXPBar()
        {
            float barH = 14f;
            float barY = Screen.height - barH;
            float barW = Screen.width;

            var xp = TMDTheme.Instance != null ? TMDTheme.Instance.Palette : RacePalette.Solari;

            // XP bar — TMD styled with race glow
            float pct = _xpToNext > 0 ? (float)_currentXp / _xpToNext : 0;
            TMDTheme.DrawProgressBar(new Rect(0, barY, barW, barH), pct, xp.Primary);
            GUI.color = Color.white;

            // Label
            var xpStyle = TMDTheme.Instance != null ? new GUIStyle(TMDTheme.LabelStyle) : new GUIStyle(GUI.skin.label);
            xpStyle.fontSize = 10;
            xpStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, barY, barW, barH),
                $"Lv.{_playerLevel}  |  XP: {_currentXp:N0} / {_xpToNext:N0}  ({pct * 100:F1}%)", xpStyle);
        }
    }
}

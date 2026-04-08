using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// SWG-style icon toolbar along the bottom-right of the screen.
    /// Each button is a small square with a Unicode icon + tooltip.
    /// Pressing the Menu button (or Escape) opens a full-screen overlay
    /// with Resume, Exit to Lobby, Credits, Quit.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private bool _menuOpen;
        private bool _creditsOpen;
        private Texture2D _pixel;
        private string _hoveredTooltip = "";
        private float _tooltipX, _tooltipY;

        // Icon bar layout
        private const float IconSize = 32f;
        private const float IconGap = 3f;
        private const float BarPadRight = 10f;
        private const float BarPadBottom = 8f;

        private struct ToolbarButton
        {
            public string Icon;     // Unicode character
            public string Tooltip;  // Hover text
            public string Hotkey;   // Keyboard shortcut display
            public System.Action OnClick;
        }

        private ToolbarButton[] _buttons;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void Start()
        {
            _buttons = new[]
            {
                new ToolbarButton { Icon = "\u263A", Tooltip = "Character",  Hotkey = "P",   OnClick = () => PlayerProfileUI.Instance?.Toggle() },
                new ToolbarButton { Icon = "\u2692", Tooltip = "Inventory",  Hotkey = "I",   OnClick = () => InventoryUI.Instance?.Toggle() },
                new ToolbarButton { Icon = "\u2726", Tooltip = "Skills",     Hotkey = "K",   OnClick = () => {} },
                new ToolbarButton { Icon = "\u2694", Tooltip = "Combat",     Hotkey = "C",   OnClick = () => {} },
                new ToolbarButton { Icon = "\u2638", Tooltip = "Quests",     Hotkey = "J",   OnClick = () => {} },
                new ToolbarButton { Icon = "\u25CB", Tooltip = "Map",        Hotkey = "M",   OnClick = () => FindFirstObjectByType<MinimapUI>()?.Toggle() },
                new ToolbarButton { Icon = "\u2691", Tooltip = "Guild",      Hotkey = "G",   OnClick = () => GuildUI.Instance?.Toggle() },
                new ToolbarButton { Icon = "\u263B", Tooltip = "Friends",    Hotkey = "O",   OnClick = () => FriendsUI.Instance?.Toggle() },
                new ToolbarButton { Icon = "\u2709", Tooltip = "Mail",       Hotkey = "N",   OnClick = () => MailUI.Instance?.Toggle() },
                new ToolbarButton { Icon = "\u266B", Tooltip = "Emotes",     Hotkey = ".",   OnClick = () => EmoteUI.Instance?.Toggle() },
                new ToolbarButton { Icon = "\u2699", Tooltip = "Settings",   Hotkey = null,  OnClick = OpenSettings },
                new ToolbarButton { Icon = "\u2630", Tooltip = "Menu",       Hotkey = "Esc", OnClick = () => { if (_menuOpen) CloseMenu(); else OpenMenu(); } },
            };
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_creditsOpen)
                    _creditsOpen = false;
                else if (_menuOpen)
                    CloseMenu();
                else
                    OpenMenu();
            }
        }

        private void OpenMenu()
        {
            _menuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseMenu()
        {
            _menuOpen = false;
            _creditsOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OpenSettings()
        {
            var settings = FindFirstObjectByType<Settings.SettingsUI>();
            if (settings == null)
            {
                var go = new GameObject("SettingsUI");
                settings = go.AddComponent<Settings.SettingsUI>();
            }
            settings.Show();
        }

        private void OnGUI()
        {
            DrawIconBar();

            if (_menuOpen)
            {
                if (_creditsOpen)
                    DrawCredits();
                else
                    DrawMenuOverlay();
            }
        }

        // ─── SWG-Style Icon Bar ────────────────────────────────────────────

        private void DrawIconBar()
        {
            if (_buttons == null) return;

            int count = _buttons.Length;
            float totalW = count * IconSize + (count - 1) * IconGap;
            float barX = Screen.width - totalW - BarPadRight;
            float barY = Screen.height - IconSize - BarPadBottom;

            // Bar background (slightly wider/taller than icons)
            float bgPad = 4f;
            GUI.color = new Color(0.03f, 0.03f, 0.07f, 0.8f);
            GUI.DrawTexture(new Rect(barX - bgPad, barY - bgPad, totalW + bgPad * 2, IconSize + bgPad * 2), _pixel);
            GUI.color = Color.white;

            // Top border line
            GUI.color = new Color(0.25f, 0.3f, 0.5f, 0.5f);
            GUI.DrawTexture(new Rect(barX - bgPad, barY - bgPad, totalW + bgPad * 2, 1), _pixel);
            GUI.color = Color.white;

            _hoveredTooltip = "";

            for (int i = 0; i < count; i++)
            {
                float x = barX + i * (IconSize + IconGap);
                Rect iconRect = new Rect(x, barY, IconSize, IconSize);
                DrawIconButton(iconRect, _buttons[i], i);
            }

            // Tooltip above the bar
            if (!string.IsNullOrEmpty(_hoveredTooltip))
            {
                var tipStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                Vector2 tipSize = tipStyle.CalcSize(new GUIContent(_hoveredTooltip));
                float tipW = tipSize.x + 12;
                float tipH = 20;
                float tipX = _tooltipX - tipW / 2f;
                float tipY = barY - tipH - 6;

                // Clamp to screen
                tipX = Mathf.Clamp(tipX, 4, Screen.width - tipW - 4);

                GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.92f);
                GUI.DrawTexture(new Rect(tipX, tipY, tipW, tipH), _pixel);
                GUI.color = new Color(0.3f, 0.4f, 0.6f, 0.6f);
                DrawBorder(new Rect(tipX, tipY, tipW, tipH), 1);
                GUI.color = Color.white;

                GUI.Label(new Rect(tipX, tipY, tipW, tipH), _hoveredTooltip, tipStyle);
            }
        }

        private void DrawIconButton(Rect rect, ToolbarButton btn, int index)
        {
            Vector2 mouse = Event.current.mousePosition;
            bool hovered = rect.Contains(mouse);

            // Separator line before Settings (visual grouping)
            if (index == 10) // Settings is index 10
            {
                GUI.color = new Color(0.2f, 0.25f, 0.4f, 0.5f);
                GUI.DrawTexture(new Rect(rect.x - IconGap / 2f - 1, rect.y + 4, 1, IconSize - 8), _pixel);
                GUI.color = Color.white;
            }

            // Background
            Color bg = hovered
                ? new Color(0.18f, 0.22f, 0.38f, 0.95f)
                : new Color(0.07f, 0.07f, 0.12f, 0.7f);
            GUI.color = bg;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;

            // Hover border
            if (hovered)
            {
                GUI.color = new Color(0.4f, 0.55f, 0.9f, 0.7f);
                DrawBorder(rect, 1);
                GUI.color = Color.white;

                _hoveredTooltip = btn.Hotkey != null ? $"{btn.Tooltip} [{btn.Hotkey}]" : btn.Tooltip;
                _tooltipX = rect.x + rect.width / 2f;
                _tooltipY = rect.y;
            }

            // Icon
            var iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = hovered ? Color.white : new Color(0.6f, 0.65f, 0.75f) }
            };
            GUI.Label(rect, btn.Icon, iconStyle);

            // Click
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                btn.OnClick?.Invoke();
        }

        // ─── Full-Screen Menu Overlay ──────────────────────────────────────

        private void DrawMenuOverlay()
        {
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float panelW = 320, panelH = 320;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _pixel);
            GUI.color = new Color(0.3f, 0.4f, 0.7f, 0.6f);
            DrawBorder(new Rect(px, py, panelW, panelH), 1);
            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(px, py + 16, panelW, 36), "ORLO", titleStyle);

            float bw = 220, bh = 42, gap = 10;
            float bx = px + (panelW - bw) / 2f;
            float by = py + 70;

            if (DrawMenuButton(new Rect(bx, by, bw, bh), "Resume"))
                CloseMenu();
            by += bh + gap;

            if (DrawMenuButton(new Rect(bx, by, bw, bh), "Exit to Lobby"))
            {
                CloseMenu();
                Network.NetworkManager.Instance?.Disconnect();
                FindFirstObjectByType<GameBootstrap>()?.ShowLoginAfterLogout();
            }
            by += bh + gap;

            if (DrawMenuButton(new Rect(bx, by, bw, bh), "Credits"))
                _creditsOpen = true;
            by += bh + gap;

            if (DrawMenuButton(new Rect(bx, by, bw, bh), "Quit", true))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            // Version
            var verStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.LowerCenter,
                normal = { textColor = new Color(0.35f, 0.35f, 0.45f) }
            };
            GUI.Label(new Rect(px, py + panelH - 24, panelW, 20), $"v{Application.version}", verStyle);
        }

        private bool DrawMenuButton(Rect rect, string label, bool red = false)
        {
            Vector2 mouse = Event.current.mousePosition;
            bool hovered = rect.Contains(mouse);

            Color bg = red
                ? (hovered ? new Color(0.4f, 0.12f, 0.12f, 0.95f) : new Color(0.25f, 0.08f, 0.08f, 0.9f))
                : (hovered ? new Color(0.18f, 0.22f, 0.38f, 0.95f) : new Color(0.1f, 0.1f, 0.18f, 0.9f));

            GUI.color = bg;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = hovered ? Color.white : new Color(0.8f, 0.82f, 0.9f) }
            };
            GUI.Label(rect, label, style);

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        // ─── Credits ───────────────────────────────────────────────────────

        private void DrawCredits()
        {
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixel);
            GUI.color = Color.white;

            float panelW = 400, panelH = 360;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.06f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _pixel);
            GUI.color = new Color(0.3f, 0.4f, 0.7f, 0.6f);
            DrawBorder(new Rect(px, py, panelW, panelH), 1);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(px, py + 16, panelW, 30), "Credits", titleStyle);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.75f, 0.78f, 0.85f) }
            };

            string credits =
                "ORLO\n" +
                "A physics-driven sci-fi fantasy MMORPG\n\n" +
                "Design & Development\n" +
                "JD\n\n" +
                "AI Engineering\n" +
                "Claude (Anthropic)\n\n" +
                "Built with Unity 6, C++20, Protocol Buffers\n" +
                "Hosted on Hetzner Cloud\n\n" +
                "\u00A9 2026 OrloGames";

            GUI.Label(new Rect(px + 20, py + 56, panelW - 40, panelH - 100), credits, bodyStyle);

            float bw = 120, bh = 34;
            if (DrawMenuButton(new Rect(px + (panelW - bw) / 2f, py + panelH - 50, bw, bh), "Back"))
                _creditsOpen = false;
        }

        // ─── Helpers ───────────────────────────────────────────────────────

        private void DrawBorder(Rect area, float w)
        {
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, w), _pixel);
            GUI.DrawTexture(new Rect(area.x, area.y + area.height - w, area.width, w), _pixel);
            GUI.DrawTexture(new Rect(area.x, area.y, w, area.height), _pixel);
            GUI.DrawTexture(new Rect(area.x + area.width - w, area.y, w, area.height), _pixel);
        }
    }
}

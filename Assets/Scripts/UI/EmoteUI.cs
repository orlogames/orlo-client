using UnityEngine;

namespace Orlo.UI
{
    /// <summary>
    /// Emote wheel/grid. Toggle with Period (.) key.
    /// 6x5 grid of 30 emotes. Click to perform. Custom emote input at bottom.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class EmoteUI : MonoBehaviour
    {
        public static EmoteUI Instance { get; private set; }

        private bool _visible;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private string _customEmote = "";

        private const float WinW = 320f;
        private const float WinH = 340f;
        private const int Cols = 6;
        private const int Rows = 5;
        private const float BtnW = 48f;
        private const float BtnH = 32f;
        private const float Pad = 3f;

        private static readonly string[] Emotes = {
            "Wave",    "Bow",     "Clap",    "Dance",   "Cheer",   "Salute",
            "Laugh",   "Cry",     "Angry",   "Shrug",   "Nod",     "Shake",
            "Point",   "Flex",    "Sit",     "Stand",   "Kneel",   "Meditate",
            "Taunt",   "Facepalm","Thank",   "Beg",     "Flirt",   "Challenge",
            "Mourn",   "Victory", "Confused","Scared",  "Yawn",    "Stretch"
        };

        private static readonly string[] EmoteIds = {
            "wave",    "bow",     "clap",    "dance",   "cheer",   "salute",
            "laugh",   "cry",     "angry",   "shrug",   "nod",     "shake",
            "point",   "flex",    "sit",     "stand",   "kneel",   "meditate",
            "taunt",   "facepalm","thank",   "beg",     "flirt",   "challenge",
            "mourn",   "victory", "confused","scared",  "yawn",    "stretch"
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Period) && !ChatUI.Instance?.IsInputActive == true)
                _visible = !_visible;
        }

        public void Toggle() { _visible = !_visible; }

        private void OnGUI()
        {
            if (!_visible) return;

            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, WinW, WinH);

            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, WinW - 28, 28);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(_windowPos.x, _windowPos.y, WinW, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "Emotes", TitleStyle());
            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            { _visible = false; return; }
            HandleDrag(titleBar);

            float startY = _windowPos.y + 34;
            float startX = _windowPos.x + 8;

            // Emote grid
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int idx = row * Cols + col;
                    if (idx >= Emotes.Length) break;

                    float bx = startX + col * (BtnW + Pad);
                    float by = startY + row * (BtnH + Pad);

                    Rect btnRect = new Rect(bx, by, BtnW, BtnH);

                    // Hover effect
                    bool hover = btnRect.Contains(Event.current.mousePosition);
                    GUI.color = hover ? new Color(0.2f, 0.3f, 0.5f, 0.9f) : new Color(0.12f, 0.15f, 0.2f, 0.8f);
                    GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    var labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 9,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = hover ? new Color(1f, 0.85f, 0.3f) : Color.white }
                    };

                    if (GUI.Button(btnRect, Emotes[idx], labelStyle))
                    {
                        // Get current target entity ID
                        ulong targetId = 0;
                        var targeting = Orlo.Player.TargetingSystem.Instance;
                        if (targeting != null)
                            targetId = targeting.TargetEntityId;

                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.EmoteRequestExtended(EmoteIds[idx], targetId));
                        _visible = false;
                    }
                }
            }

            // Custom emote input
            float inputY = startY + Rows * (BtnH + Pad) + 8;
            GUI.Label(new Rect(startX, inputY, 60, 20), "Custom:", SmallLabel());
            _customEmote = GUI.TextField(new Rect(startX + 64, inputY, WinW - 140, 20), _customEmote, SmallInputStyle());
            if (GUI.Button(new Rect(_windowPos.x + WinW - 68, inputY, 56, 20), "Send"))
            {
                if (!string.IsNullOrEmpty(_customEmote.Trim()))
                {
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.EmoteRequestExtended(_customEmote.Trim(), 0));
                    _customEmote = "";
                    _visible = false;
                }
            }

            GUI.color = Color.white;
        }

        private void HandleDrag(Rect titleBar)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            { _dragging = true; _dragOffset = e.mousePosition - _windowPos; e.Use(); }
            if (_dragging && e.type == EventType.MouseDrag)
            { _windowPos = e.mousePosition - _dragOffset; e.Use(); }
            if (e.type == EventType.MouseUp) _dragging = false;
        }

        private GUIStyle TitleStyle() => new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
        private GUIStyle SmallLabel() => new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
        private GUIStyle SmallInputStyle() => new GUIStyle(GUI.skin.textField) { fontSize = 11, normal = { textColor = Color.white }, focused = { textColor = Color.white } };
    }
}

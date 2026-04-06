using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Chat window with channel support, whispers, and fade behavior.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        private const int MaxMessages = 100;
        private const float WindowW = 400f;
        private const float WindowH = 250f;
        private const float FadeDelay = 5f;
        private const float FadedAlpha = 0.5f;

        private struct ChatEntry
        {
            public string Sender;
            public string Channel;
            public string Message;
            public float Timestamp;
            public Color ChannelColor;
        }

        private enum Channel { Global, Zone, Party }

        private static readonly Dictionary<string, Color> ChannelColors = new Dictionary<string, Color>
        {
            { "Global", Color.green },
            { "Zone", Color.cyan },
            { "Party", new Color(0.3f, 0.3f, 1f) },
            { "Whisper", Color.magenta },
            { "System", Color.yellow }
        };

        private List<ChatEntry> _messages = new List<ChatEntry>();
        private Vector2 _scrollPos;
        private string _inputText = "";
        private bool _inputFocused;
        private Channel _activeChannel = Channel.Global;
        private float _lastActivityTime;
        private bool _hovered;
        private string _inputControlName = "ChatInput";

        private void Awake()
        {
            _lastActivityTime = Time.time;
            AddSystemMessage("Welcome to Orlo! Type /w <name> <message> to whisper.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (_inputFocused && !string.IsNullOrEmpty(_inputText))
                {
                    SendMessage();
                }
                else
                {
                    _inputFocused = true;
                    _lastActivityTime = Time.time;
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) && _inputFocused)
            {
                _inputFocused = false;
            }
        }

        public void AddSystemMessage(string text)
        {
            AddEntry("System", "System", text, Color.yellow);
        }

        public void ReceiveMessage(string sender, string channel, string message)
        {
            Color color = ChannelColors.ContainsKey(channel) ? ChannelColors[channel] : Color.white;
            AddEntry(sender, channel, message, color);
        }

        private void AddEntry(string sender, string channel, string message, Color color)
        {
            _messages.Add(new ChatEntry
            {
                Sender = sender,
                Channel = channel,
                Message = message,
                Timestamp = Time.time,
                ChannelColor = color
            });

            if (_messages.Count > MaxMessages)
                _messages.RemoveAt(0);

            _lastActivityTime = Time.time;
            // Auto-scroll to bottom
            _scrollPos.y = float.MaxValue;
        }

        private void SendMessage()
        {
            string text = _inputText.Trim();
            _inputText = "";
            _inputFocused = false;

            if (string.IsNullOrEmpty(text)) return;

            // Slash commands
            if (text.StartsWith("/"))
            {
                HandleSlashCommand(text);
                return;
            }

            // Send to server
            string channel = _activeChannel.ToString();
            int channelId = _activeChannel switch
            {
                Channel.Global => 2,
                Channel.Zone => 1,
                Channel.Party => 3,
                _ => 2
            };

            var data = Network.PacketBuilder.ChatSend(channelId, text);
            Network.NetworkManager.Instance?.Send(data);

            // Show locally immediately
            AddEntry("You", channel, text, ChannelColors[channel]);
        }

        private void HandleSlashCommand(string text)
        {
            string[] parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/w":
                case "/whisper":
                case "/tell":
                    if (parts.Length >= 3)
                    {
                        string target = parts[1];
                        string msg = string.Join(" ", parts, 2, parts.Length - 2);
                        var data = Network.PacketBuilder.ChatSend(4, msg, target); // 4 = whisper
                        Network.NetworkManager.Instance?.Send(data);
                        AddEntry("You", "Whisper", $"-> {target}: {msg}", Color.magenta);
                    }
                    else
                        AddSystemMessage("Usage: /w <name> <message>");
                    break;

                case "/g":
                    if (parts.Length >= 2)
                    {
                        string msg = string.Join(" ", parts, 1, parts.Length - 1);
                        var data = Network.PacketBuilder.ChatSend(2, msg);
                        Network.NetworkManager.Instance?.Send(data);
                        AddEntry("You", "Global", msg, ChannelColors["Global"]);
                    }
                    break;

                case "/p":
                    if (parts.Length >= 2)
                    {
                        string msg = string.Join(" ", parts, 1, parts.Length - 1);
                        var data = Network.PacketBuilder.ChatSend(3, msg);
                        Network.NetworkManager.Instance?.Send(data);
                        AddEntry("You", "Party", msg, ChannelColors["Party"]);
                    }
                    break;

                case "/tp":
                case "/teleport":
                    if (parts.Length >= 4 && AdminCheck())
                    {
                        if (float.TryParse(parts[1], out float x) &&
                            float.TryParse(parts[2], out float y) &&
                            float.TryParse(parts[3], out float z))
                        {
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.AdminTeleport(x, y, z));
                            AddSystemMessage($"Teleporting to ({x}, {y}, {z})...");
                        }
                        else
                            AddSystemMessage("Usage: /tp <x> <y> <z>");
                    }
                    break;

                case "/setspeed":
                case "/speed":
                    if (parts.Length >= 2 && AdminCheck())
                    {
                        if (float.TryParse(parts[1], out float speed))
                        {
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.AdminSetSpeed(speed));
                            AddSystemMessage($"Speed set to {speed} m/s");
                        }
                        else
                            AddSystemMessage("Usage: /setspeed <number>");
                    }
                    break;

                case "/fly":
                    if (AdminCheck())
                    {
                        var admin = AdminPanel.Instance;
                        bool newState = admin != null ? !admin.FlyEnabled : true;
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.AdminSetFly(newState));
                        AddSystemMessage(newState ? "Fly mode enabled" : "Fly mode disabled");
                    }
                    break;

                case "/god":
                    if (AdminCheck())
                    {
                        var admin = AdminPanel.Instance;
                        bool newState = admin != null ? !admin.GodMode : true;
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.AdminGodMode(newState));
                        AddSystemMessage(newState ? "God mode enabled" : "God mode disabled");
                    }
                    break;

                case "/spawn":
                    if (parts.Length >= 2 && AdminCheck())
                    {
                        string creatureType = parts[1];
                        var player = GameObject.FindWithTag("Player");
                        if (player != null)
                        {
                            var pos = player.transform.position;
                            Network.NetworkManager.Instance?.Send(
                                Network.PacketBuilder.AdminSpawnCreature(creatureType, pos.x, pos.y, pos.z));
                            AddSystemMessage($"Spawning {creatureType}...");
                        }
                    }
                    else if (AdminCheck())
                        AddSystemMessage("Usage: /spawn <creature_type>");
                    break;

                case "/creatures":
                    if (AdminCheck())
                    {
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.AdminListCreatures());
                        AddSystemMessage("Requesting creature list...");
                        // CreatureBrowserUI will open when response arrives
                        var browser = FindFirstObjectByType<CreatureBrowserUI>();
                        if (browser == null)
                        {
                            var go = new GameObject("CreatureBrowserUI");
                            go.AddComponent<CreatureBrowserUI>();
                        }
                    }
                    break;

                case "/pos":
                    var p = GameObject.FindWithTag("Player");
                    if (p != null)
                    {
                        var pos = p.transform.position;
                        AddSystemMessage($"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                    }
                    break;

                case "/hudlock":
                    if (HUDLayout.Instance != null)
                    {
                        HUDLayout.Instance.ToggleLock();
                        AddSystemMessage(HUDLayout.Instance.IsLocked
                            ? "HUD locked."
                            : "HUD unlocked. Right-click drag to move windows. /hudlock to lock.");
                    }
                    break;

                case "/hudreset":
                    if (HUDLayout.Instance != null)
                    {
                        HUDLayout.Instance.ResetLayout();
                        AddSystemMessage("HUD positions reset to defaults. Restart to apply.");
                    }
                    break;

                case "/help":
                    AddSystemMessage("Commands: /w /g /p /tp /setspeed /fly /god /spawn /creatures /pos /hudlock /hudreset /help");
                    break;

                default:
                    AddSystemMessage($"Unknown command: {cmd}. Type /help for commands.");
                    break;
            }
        }

        private bool AdminCheck()
        {
            var admin = AdminPanel.Instance;
            if (admin == null || !admin.IsAdmin)
            {
                AddSystemMessage("You must be an admin to use this command.");
                return false;
            }
            return true;
        }

        private const string HUD_KEY = "Chat";
        private bool _hudRegistered;

        private void OnGUI()
        {
            // Register with HUDLayout for draggable positioning
            if (!_hudRegistered && HUDLayout.Instance != null)
            {
                HUDLayout.Instance.Register(HUD_KEY, "Chat", 10f, Screen.height - WindowH - 10f, WindowW, WindowH);
                _hudRegistered = true;
            }

            float x, y;
            if (HUDLayout.Instance != null)
            {
                var pos = HUDLayout.Instance.GetPosition(HUD_KEY);
                x = pos.x;
                y = pos.y;
            }
            else
            {
                x = 10f;
                y = Screen.height - WindowH - 10f;
            }

            // Check hover
            Rect fullRect = new Rect(x, y, WindowW, WindowH);
            _hovered = fullRect.Contains(Event.current.mousePosition);

            // Fade logic
            float timeSinceActivity = Time.time - _lastActivityTime;
            float alpha = (_hovered || _inputFocused || timeSinceActivity < FadeDelay) ? 1f : FadedAlpha;
            GUI.color = new Color(1, 1, 1, alpha);

            // Background
            GUI.color = new Color(0, 0, 0, 0.7f * alpha);
            GUI.DrawTexture(fullRect, Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, alpha);

            // Title bar
            Rect titleBar = new Rect(x, y, WindowW, 20);
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.95f * alpha);
            GUI.DrawTexture(titleBar, Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(titleBar, "  Chat", BoldLabel());

            float contentY = y + 22;

            // Channel selector buttons
            float btnW = 60f;
            float btnX = x + 4;
            float btnY = contentY;
            Channel[] channels = { Channel.Global, Channel.Zone, Channel.Party };
            foreach (var ch in channels)
            {
                bool selected = _activeChannel == ch;
                Color btnColor = ChannelColors[ch.ToString()];
                GUI.color = selected ? new Color(btnColor.r, btnColor.g, btnColor.b, 0.8f * alpha) : new Color(0.2f, 0.2f, 0.2f, 0.8f * alpha);
                GUI.DrawTexture(new Rect(btnX, btnY, btnW, 18), Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, alpha);
                if (GUI.Button(new Rect(btnX, btnY, btnW, 18), ch.ToString(), SmallLabelCentered()))
                {
                    _activeChannel = ch;
                    _lastActivityTime = Time.time;
                }
                btnX += btnW + 4;
            }

            float msgAreaY = btnY + 22;
            float inputH = 22f;
            float msgAreaH = WindowH - (msgAreaY - y) - inputH - 6;

            // Message log
            Rect msgArea = new Rect(x + 2, msgAreaY, WindowW - 4, msgAreaH);
            float totalH = _messages.Count * 16f;
            _scrollPos = GUI.BeginScrollView(msgArea, _scrollPos, new Rect(0, 0, WindowW - 24, Mathf.Max(totalH, msgAreaH)));

            for (int i = 0; i < _messages.Count; i++)
            {
                ChatEntry entry = _messages[i];
                float lineY = i * 16f;

                // Channel prefix
                string prefix = $"[{entry.Channel}]";
                string line = entry.Sender == "System"
                    ? $"{prefix} {entry.Message}"
                    : $"{prefix} {entry.Sender}: {entry.Message}";

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(entry.ChannelColor.r, entry.ChannelColor.g, entry.ChannelColor.b, alpha) },
                    wordWrap = false
                };
                GUI.Label(new Rect(2, lineY, WindowW - 28, 16), line, style);
            }

            GUI.EndScrollView();

            // Input field
            float inputY = y + WindowH - inputH - 4;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f * alpha);
            GUI.DrawTexture(new Rect(x + 2, inputY, WindowW - 4, inputH), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, alpha);

            GUI.SetNextControlName(_inputControlName);
            _inputText = GUI.TextField(new Rect(x + 4, inputY + 1, WindowW - 8, inputH - 2), _inputText, SmallInputStyle());

            if (_inputFocused)
            {
                GUI.FocusControl(_inputControlName);
            }

            // Reset GUI color
            GUI.color = Color.white;
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private GUIStyle SmallInputStyle()
        {
            return new GUIStyle(GUI.skin.textField)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                focused = { textColor = Color.white }
            };
        }
    }
}

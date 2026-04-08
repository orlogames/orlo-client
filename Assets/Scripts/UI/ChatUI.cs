using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Chat window with 14 channel support, whispers, chat bubbles, and fade behavior.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        public static ChatUI Instance { get; private set; }

        private const int MaxMessages = 200;
        private const float WindowW = 440f;
        private const float WindowH = 280f;
        private const float FadeDelay = 5f;
        private const float FadedAlpha = 0.5f;

        public struct ChatEntry
        {
            public string Sender;
            public string Channel;
            public string Message;
            public float Timestamp;
            public Color ChannelColor;
            public ulong SenderEntityId;
        }

        public enum ChatChannel
        {
            Say = 0, Yell = 1, Zone = 2, Trade = 3, LFG = 4,
            Guild = 5, Officer = 6, Party = 7, Whisper = 8, Circle = 9,
            System = 10, Global = 11, Proximity = 12, Emote = 13
        }

        public static readonly Dictionary<string, Color> ChannelColors = new Dictionary<string, Color>
        {
            { "Say",      Color.white },
            { "Yell",     new Color(1f, 0.6f, 0f) },
            { "Zone",     Color.yellow },
            { "Trade",    Color.green },
            { "LFG",      Color.cyan },
            { "Guild",    new Color(0f, 0.8f, 0.6f) },
            { "Officer",  new Color(0.7f, 0.3f, 0.9f) },
            { "Party",    new Color(0.3f, 0.5f, 1f) },
            { "Whisper",  new Color(1f, 0.5f, 0.8f) },
            { "Circle",   new Color(0.5f, 0.8f, 0.5f) },
            { "System",   new Color(1f, 0.3f, 0.3f) },
            { "Global",   Color.green },
            { "Proximity", Color.white },
            { "Emote",    new Color(1f, 0.6f, 0.3f) }
        };

        // Channel tabs displayed at the top
        private static readonly string[] ChannelTabs = {
            "Say", "Yell", "Zone", "Trade", "LFG",
            "Guild", "Officer", "Party", "Whisper", "Circle"
        };

        // Channel ID mapping for PacketBuilder
        private static readonly Dictionary<string, int> ChannelIds = new Dictionary<string, int>
        {
            { "Say", 0 }, { "Yell", 1 }, { "Zone", 2 }, { "Trade", 3 },
            { "LFG", 4 }, { "Guild", 5 }, { "Officer", 6 }, { "Party", 7 },
            { "Whisper", 8 }, { "Circle", 9 }, { "System", 10 }, { "Global", 11 }
        };

        private List<ChatEntry> _messages = new List<ChatEntry>();
        private Vector2 _scrollPos;
        private string _inputText = "";
        private bool _inputFocused;
        private string _activeChannelName = "Say";
        private float _lastActivityTime;
        private bool _hovered;
        private string _inputControlName = "ChatInput";
        private bool _pendingSend;

        // Filter: which channels to display
        private HashSet<string> _visibleChannels = new HashSet<string>(ChannelTabs) { "System", "Global", "Proximity", "Emote" };

        // Last whisper sender for /r reply
        private string _lastWhisperFrom = "";

        // Who response display
        private string _whoResults = "";
        private float _whoTimer;

        // Rate limit feedback
        private float _rateLimitTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _lastActivityTime = Time.time;
            AddSystemMessage("Welcome to Orlo! Type /help for a list of commands.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!_inputFocused)
                {
                    _inputFocused = true;
                    _lastActivityTime = Time.time;
                }
                else
                {
                    _pendingSend = true;
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape) && _inputFocused)
            {
                _inputFocused = false;
                _inputText = "";
            }

            if (_rateLimitTimer > 0) _rateLimitTimer -= Time.deltaTime;
            if (_whoTimer > 0) _whoTimer -= Time.deltaTime;
        }

        public bool IsInputActive => _inputFocused;

        // ---- Public API ----

        public void AddSystemMessage(string text)
        {
            AddEntry("System", "System", text, ChannelColors["System"]);
        }

        public void ReceiveMessage(string sender, string channel, string message, ulong senderEntityId = 0)
        {
            Color color = ChannelColors.ContainsKey(channel) ? ChannelColors[channel] : Color.white;
            AddEntry(sender, channel, message, color, senderEntityId);

            // Track last whisper sender for /r
            if (channel == "Whisper" && sender != "You")
                _lastWhisperFrom = sender;

            // Trigger chat bubble for Say/Yell
            if ((channel == "Say" || channel == "Yell") && senderEntityId > 0)
            {
                ChatBubbleManager.Instance?.ShowBubble(senderEntityId, message, channel == "Yell");
            }
        }

        public void ShowRateLimitWarning()
        {
            if (_rateLimitTimer <= 0)
            {
                AddEntry("System", "System", "You are sending messages too fast.", ChannelColors["System"]);
                _rateLimitTimer = 3f;
            }
        }

        public void ShowWhoResults(string results)
        {
            _whoResults = results;
            _whoTimer = 10f;
            AddSystemMessage(results);
        }

        public void ShowRollResult(string roller, int result, int min, int max)
        {
            AddEntry("System", "System", $"{roller} rolls {result} ({min}-{max})", new Color(1f, 0.85f, 0.3f));
        }

        private void AddEntry(string sender, string channel, string message, Color color, ulong senderEntityId = 0)
        {
            _messages.Add(new ChatEntry
            {
                Sender = sender,
                Channel = channel,
                Message = message,
                Timestamp = Time.time,
                ChannelColor = color,
                SenderEntityId = senderEntityId
            });

            if (_messages.Count > MaxMessages)
                _messages.RemoveAt(0);

            _lastActivityTime = Time.time;
            _scrollPos.y = float.MaxValue;
        }

        private void SendMessage()
        {
            string text = _inputText.Trim();
            _inputText = "";
            _inputFocused = false;

            if (string.IsNullOrEmpty(text)) return;

            if (text.StartsWith("/"))
            {
                HandleSlashCommand(text);
                return;
            }

            string channel = _activeChannelName;
            int channelId = ChannelIds.ContainsKey(channel) ? ChannelIds[channel] : 0;

            var data = Network.PacketBuilder.ChatSend(channelId, text);
            Network.NetworkManager.Instance?.Send(data);

            Color col = ChannelColors.ContainsKey(channel) ? ChannelColors[channel] : Color.white;
            AddEntry("You", channel, text, col);
        }

        private void HandleSlashCommand(string text)
        {
            string[] parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                // Channel shortcuts
                case "/s":
                case "/say":
                    if (parts.Length >= 2)
                        SendToChannel("Say", 0, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Say";
                    break;
                case "/y":
                case "/yell":
                    if (parts.Length >= 2)
                        SendToChannel("Yell", 1, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Yell";
                    break;
                case "/zone":
                    if (parts.Length >= 2)
                        SendToChannel("Zone", 2, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Zone";
                    break;
                case "/trade":
                    if (parts.Length >= 2)
                        SendToChannel("Trade", 3, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Trade";
                    break;
                case "/lfg":
                    if (parts.Length >= 2)
                        SendToChannel("LFG", 4, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "LFG";
                    break;
                case "/g":
                case "/guild":
                    if (parts.Length >= 2)
                        SendToChannel("Guild", 5, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Guild";
                    break;
                case "/o":
                case "/officer":
                    if (parts.Length >= 2)
                        SendToChannel("Officer", 6, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Officer";
                    break;
                case "/p":
                case "/party":
                    if (parts.Length >= 2)
                        SendToChannel("Party", 7, string.Join(" ", parts, 1, parts.Length - 1));
                    else _activeChannelName = "Party";
                    break;

                // Whisper
                case "/w":
                case "/whisper":
                case "/tell":
                    if (parts.Length >= 3)
                    {
                        string target = parts[1];
                        string msg = string.Join(" ", parts, 2, parts.Length - 2);
                        var data = Network.PacketBuilder.ChatSend(8, msg, target);
                        Network.NetworkManager.Instance?.Send(data);
                        AddEntry("You", "Whisper", $"-> {target}: {msg}", ChannelColors["Whisper"]);
                    }
                    else
                        AddSystemMessage("Usage: /w <name> <message>");
                    break;

                // Reply to last whisper
                case "/r":
                case "/reply":
                    if (string.IsNullOrEmpty(_lastWhisperFrom))
                    {
                        AddSystemMessage("No one to reply to.");
                    }
                    else if (parts.Length >= 2)
                    {
                        string msg = string.Join(" ", parts, 1, parts.Length - 1);
                        var data = Network.PacketBuilder.ChatSend(8, msg, _lastWhisperFrom);
                        Network.NetworkManager.Instance?.Send(data);
                        AddEntry("You", "Whisper", $"-> {_lastWhisperFrom}: {msg}", ChannelColors["Whisper"]);
                    }
                    else
                        AddSystemMessage($"Usage: /r <message> (replying to {_lastWhisperFrom})");
                    break;

                // Social / utility
                case "/who":
                {
                    string filter = parts.Length >= 2 ? parts[1] : "";
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.WhoRequest(filter));
                    AddSystemMessage("Searching...");
                    break;
                }
                case "/roll":
                {
                    int min = 1, max = 100;
                    if (parts.Length >= 3 && int.TryParse(parts[1], out min) && int.TryParse(parts[2], out max)) { }
                    else if (parts.Length >= 2 && int.TryParse(parts[1], out max)) { min = 1; }
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.RollRequest(min, max));
                    break;
                }
                case "/e":
                case "/me":
                case "/emote":
                    if (parts.Length >= 2)
                    {
                        string emoteText = string.Join(" ", parts, 1, parts.Length - 1);
                        Network.NetworkManager.Instance?.Send(Network.PacketBuilder.EmoteRequestExtended(emoteText, 0));
                        AddEntry("You", "Emote", emoteText, ChannelColors["Emote"]);
                    }
                    break;

                // Admin commands (unchanged from before)
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
                    break;

                case "/creatures":
                    if (AdminCheck())
                    {
                        Network.NetworkManager.Instance?.Send(
                            Network.PacketBuilder.AdminListCreatures());
                        AddSystemMessage("Requesting creature list...");
                        var browser = FindFirstObjectByType<CreatureBrowserUI>();
                        if (browser == null)
                        {
                            var go = new GameObject("CreatureBrowserUI");
                            go.AddComponent<CreatureBrowserUI>();
                        }
                    }
                    break;

                case "/pos":
                    var pp = GameObject.FindWithTag("Player");
                    if (pp != null)
                    {
                        var pos = pp.transform.position;
                        AddSystemMessage($"Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                    }
                    break;

                case "/respawn":
                case "/home":
                case "/threshold":
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.AdminTeleport(512f, 20f, 510f));
                    AddSystemMessage("Returning to Threshold...");
                    break;

                case "/hudlock":
                    if (HUDLayout.Instance != null)
                    {
                        HUDLayout.Instance.ToggleLock();
                        AddSystemMessage(HUDLayout.Instance.IsLocked ? "HUD locked." : "HUD unlocked.");
                    }
                    break;

                case "/hudreset":
                    if (HUDLayout.Instance != null)
                    {
                        HUDLayout.Instance.ResetLayout();
                        AddSystemMessage("HUD positions reset.");
                    }
                    break;

                case "/mail":
                    MailUI.Instance?.Toggle();
                    break;

                case "/help":
                    AddSystemMessage("Chat: /s /y /zone /trade /lfg /g /o /p /w <name> /r /who /roll /e /me");
                    AddSystemMessage("Admin: /tp /fly /speed /god /spawn /creatures /respawn /pos /hudlock /hudreset");
                    AddSystemMessage("UI: /mail");
                    break;

                default:
                    AddSystemMessage($"Unknown command: {cmd}. Type /help.");
                    break;
            }
        }

        private void SendToChannel(string channelName, int channelId, string msg)
        {
            var data = Network.PacketBuilder.ChatSend(channelId, msg);
            Network.NetworkManager.Instance?.Send(data);
            Color col = ChannelColors.ContainsKey(channelName) ? ChannelColors[channelName] : Color.white;
            AddEntry("You", channelName, msg, col);
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

        // ---- OnGUI ----

        private const string HUD_KEY = "Chat";
        private bool _hudRegistered;

        private void OnGUI()
        {
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

            Rect fullRect = new Rect(x, y, WindowW, WindowH);
            _hovered = fullRect.Contains(Event.current.mousePosition);

            float timeSinceActivity = Time.time - _lastActivityTime;
            float alpha = (_hovered || _inputFocused || timeSinceActivity < FadeDelay) ? 1f : FadedAlpha;

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

            // Channel tabs (two rows if needed)
            float tabW = 42f;
            float tabX = x + 2;
            float tabY = contentY;
            for (int i = 0; i < ChannelTabs.Length; i++)
            {
                string tab = ChannelTabs[i];
                bool selected = _activeChannelName == tab;
                Color tabColor = ChannelColors.ContainsKey(tab) ? ChannelColors[tab] : Color.white;

                GUI.color = selected
                    ? new Color(tabColor.r, tabColor.g, tabColor.b, 0.7f * alpha)
                    : new Color(0.15f, 0.15f, 0.15f, 0.8f * alpha);
                GUI.DrawTexture(new Rect(tabX, tabY, tabW, 16), Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, alpha);

                if (GUI.Button(new Rect(tabX, tabY, tabW, 16), tab, TabLabel()))
                {
                    _activeChannelName = tab;
                    _lastActivityTime = Time.time;
                }

                tabX += tabW + 2;
                if (tabX + tabW > x + WindowW - 2)
                {
                    tabX = x + 2;
                    tabY += 18;
                }
            }

            float msgAreaY = tabY + 20;
            float inputH = 22f;
            float msgAreaH = WindowH - (msgAreaY - y) - inputH - 6;

            // Message log (filtered by visible channels)
            Rect msgArea = new Rect(x + 2, msgAreaY, WindowW - 4, msgAreaH);
            int visibleCount = 0;
            for (int i = 0; i < _messages.Count; i++)
                if (_visibleChannels.Contains(_messages[i].Channel)) visibleCount++;

            float totalH = visibleCount * 16f;
            _scrollPos = GUI.BeginScrollView(msgArea, _scrollPos, new Rect(0, 0, WindowW - 24, Mathf.Max(totalH, msgAreaH)));

            float lineY = 0;
            for (int i = 0; i < _messages.Count; i++)
            {
                ChatEntry entry = _messages[i];
                if (!_visibleChannels.Contains(entry.Channel)) continue;

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
                lineY += 16f;
            }

            GUI.EndScrollView();

            // Input field
            float inputY = y + WindowH - inputH - 4;
            float sendBtnW = 50f;
            float inputW = WindowW - sendBtnW - 10;

            // Channel indicator
            Color chColor = ChannelColors.ContainsKey(_activeChannelName) ? ChannelColors[_activeChannelName] : Color.white;
            GUI.color = new Color(chColor.r, chColor.g, chColor.b, 0.6f * alpha);
            GUI.DrawTexture(new Rect(x + 2, inputY, 3, inputH), Texture2D.whiteTexture);

            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f * alpha);
            GUI.DrawTexture(new Rect(x + 5, inputY, inputW - 3, inputH), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, alpha);

            GUI.SetNextControlName(_inputControlName);
            _inputText = GUI.TextField(new Rect(x + 7, inputY + 1, inputW - 5, inputH - 2), _inputText, SmallInputStyle());

            if (Event.current.type == EventType.Repaint)
            {
                bool guiFocused = GUI.GetNameOfFocusedControl() == _inputControlName;
                if (guiFocused && !_inputFocused)
                    _inputFocused = true;
            }

            var sendRect = new Rect(x + inputW + 4, inputY, sendBtnW, inputH);
            GUI.color = new Color(0.2f, 0.7f, 0.3f, 0.95f * alpha);
            if (GUI.Button(sendRect, "Send"))
            {
                _pendingSend = true;
            }
            GUI.color = new Color(1, 1, 1, alpha);

            if (_inputFocused)
            {
                GUI.FocusControl(_inputControlName);
            }

            if (_pendingSend)
            {
                _pendingSend = false;
                if (!string.IsNullOrEmpty(_inputText.Trim()))
                {
                    SendMessage();
                    _lastActivityTime = Time.time;
                }
                else
                {
                    _inputFocused = false;
                    _inputText = "";
                }
            }

            GUI.color = Color.white;
        }

        // ---- Style helpers ----

        private GUIStyle TabLabel()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
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

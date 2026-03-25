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

            if (string.IsNullOrEmpty(text)) return;

            // Whisper command
            if (text.StartsWith("/w "))
            {
                string remainder = text.Substring(3).TrimStart();
                int spaceIdx = remainder.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    string target = remainder.Substring(0, spaceIdx);
                    string msg = remainder.Substring(spaceIdx + 1);
                    AddEntry("You", "Whisper", $"-> {target}: {msg}", Color.magenta);
                    Debug.Log($"[ChatUI] Whisper to {target}: {msg}");
                }
                else
                {
                    AddSystemMessage("Usage: /w <name> <message>");
                }
                return;
            }

            string channel = _activeChannel.ToString();
            AddEntry("You", channel, text, ChannelColors[channel]);
            Debug.Log($"[ChatUI] [{channel}] {text}");
        }

        private void OnGUI()
        {
            float x = 10f;
            float y = Screen.height - WindowH - 10f;

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

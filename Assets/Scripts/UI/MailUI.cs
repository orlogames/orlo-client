using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Mail system UI. Opens near mailbox objects or via /mail command.
    /// Tabs: Inbox, Compose. Supports attachments, credits, and COD.
    /// Uses OnGUI for rapid prototyping.
    /// </summary>
    public class MailUI : MonoBehaviour
    {
        public static MailUI Instance { get; private set; }

        public static bool IsNearMailbox;

        private enum Tab { Inbox, Compose }
        private enum View { List, Read }

        private bool _visible;
        private Tab _activeTab = Tab.Inbox;
        private View _inboxView = View.List;
        private Vector2 _windowPos;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _scrollPos;

        private const float WinW = 480f;
        private const float WinH = 440f;

        // ---- Mail Data ----

        public struct MailEntry
        {
            public ulong MailId;
            public string Sender;
            public string Subject;
            public string Body;
            public string Date;
            public bool Unread;
            public bool HasAttachments;
            public long Credits;
            public long CodPrice;
            public List<MailAttachment> Attachments;
        }

        public struct MailAttachment
        {
            public string ItemName;
            public int StackCount;
            public Color RarityColor;
        }

        private List<MailEntry> _inbox = new List<MailEntry>();
        private int _selectedMailIndex = -1;
        private static int _unreadCount;
        public static int UnreadCount => _unreadCount;

        // Compose state
        private string _composeTo = "";
        private string _composeSubject = "";
        private string _composeBody = "";
        private long _composeCredits;
        private bool _composeCod;
        private long _composeCodPrice;
        private string _composeCreditsStr = "0";
        private string _composeCodStr = "0";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _windowPos = new Vector2(Screen.width / 2f - WinW / 2f, Screen.height / 2f - WinH / 2f);
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible)
            {
                _activeTab = Tab.Inbox;
                _inboxView = View.List;
                Network.NetworkManager.Instance?.Send(Network.PacketBuilder.MailListRequest());
            }
        }

        // ---- Public API ----

        public void SetInbox(List<MailEntry> inbox)
        {
            _inbox = inbox ?? new List<MailEntry>();
            _unreadCount = 0;
            foreach (var m in _inbox)
                if (m.Unread) _unreadCount++;
        }

        public void SetMailContent(ulong mailId, string body, List<MailAttachment> attachments, long credits, long codPrice)
        {
            for (int i = 0; i < _inbox.Count; i++)
            {
                if (_inbox[i].MailId == mailId)
                {
                    var m = _inbox[i];
                    m.Body = body;
                    m.Attachments = attachments;
                    m.Credits = credits;
                    m.CodPrice = codPrice;
                    m.Unread = false;
                    _inbox[i] = m;
                    break;
                }
            }
            _unreadCount = 0;
            foreach (var m in _inbox) if (m.Unread) _unreadCount++;
        }

        public void OnCollectResult(bool success, string message)
        {
            if (success)
                ChatUI.Instance?.AddSystemMessage("Attachments collected.");
            else
                ChatUI.Instance?.AddSystemMessage($"Cannot collect: {message}");
        }

        public void NotifyNewMail()
        {
            _unreadCount++;
            ChatUI.Instance?.AddSystemMessage("You have new mail!");
        }

        // ---- OnGUI ----

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

            GUI.Label(new Rect(_windowPos.x + 8, _windowPos.y, 200, 28), "Mail", TitleStyle());
            if (GUI.Button(new Rect(_windowPos.x + WinW - 28, _windowPos.y + 2, 24, 24), "X"))
            { _visible = false; return; }
            HandleDrag(titleBar);

            float cy = _windowPos.y + 32;
            float cx = _windowPos.x + 8;
            float cw = WinW - 16;

            // Tab bar
            Tab[] tabs = { Tab.Inbox, Tab.Compose };
            float tabW = cw / 2f;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool sel = _activeTab == tabs[i];
                GUI.color = sel ? new Color(0.2f, 0.3f, 0.5f, 0.9f) : new Color(0.12f, 0.12f, 0.18f, 0.9f);
                GUI.DrawTexture(new Rect(cx + i * tabW, cy, tabW - 2, 22), Texture2D.whiteTexture);
                GUI.color = Color.white;
                string label = tabs[i] == Tab.Inbox && _unreadCount > 0 ? $"Inbox ({_unreadCount})" : tabs[i].ToString();
                if (GUI.Button(new Rect(cx + i * tabW, cy, tabW - 2, 22), label, SmallCentered()))
                    _activeTab = tabs[i];
            }
            cy += 26;

            Rect contentRect = new Rect(cx, cy, cw, WinH - (cy - _windowPos.y) - 8);

            switch (_activeTab)
            {
                case Tab.Inbox:
                    if (_inboxView == View.List) DrawInboxList(contentRect);
                    else DrawMailRead(contentRect);
                    break;
                case Tab.Compose:
                    DrawCompose(contentRect);
                    break;
            }

            GUI.color = Color.white;
        }

        private void DrawInboxList(Rect area)
        {
            float totalH = _inbox.Count * 28f;
            _scrollPos = GUI.BeginScrollView(area, _scrollPos, new Rect(0, 0, area.width - 16, Mathf.Max(totalH, area.height)));

            float y = 0;
            for (int i = 0; i < _inbox.Count; i++)
            {
                var mail = _inbox[i];
                Rect row = new Rect(0, y, area.width - 16, 26);

                // Highlight on hover
                bool hover = row.Contains(Event.current.mousePosition);
                GUI.color = hover ? new Color(0.15f, 0.2f, 0.3f, 0.8f) : new Color(0.1f, 0.1f, 0.12f, 0.5f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Unread indicator
                if (mail.Unread)
                {
                    GUI.color = new Color(0.3f, 0.6f, 1f);
                    GUI.DrawTexture(new Rect(4, y + 8, 6, 6), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // Attachment icon
                if (mail.HasAttachments)
                    GUI.Label(new Rect(14, y + 2, 14, 20), "[A]", DimLabel());

                float nameX = mail.HasAttachments ? 30 : 14;
                GUI.Label(new Rect(nameX, y + 2, 100, 20), mail.Sender, mail.Unread ? BoldSmall() : SmallLabel());
                GUI.Label(new Rect(nameX + 104, y + 2, 200, 20), mail.Subject, SmallLabel());
                GUI.Label(new Rect(area.width - 80, y + 2, 60, 20), mail.Date, DimLabel());

                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    _selectedMailIndex = i;
                    _inboxView = View.Read;
                    Network.NetworkManager.Instance?.Send(
                        Network.PacketBuilder.MailReadRequest(mail.MailId));
                    Event.current.Use();
                }

                y += 26;
            }

            GUI.EndScrollView();
        }

        private void DrawMailRead(Rect area)
        {
            if (_selectedMailIndex < 0 || _selectedMailIndex >= _inbox.Count)
            {
                _inboxView = View.List;
                return;
            }

            var mail = _inbox[_selectedMailIndex];
            float y = area.y + 4;
            float x = area.x + 4;
            float w = area.width - 8;

            // Back button
            if (GUI.Button(new Rect(x, y, 60, 20), "< Back"))
            {
                _inboxView = View.List;
                return;
            }
            y += 24;

            // Subject
            GUI.Label(new Rect(x, y, w, 22), mail.Subject, BoldSmall());
            y += 22;

            // Sender + date
            GUI.Label(new Rect(x, y, 200, 18), $"From: {mail.Sender}", SmallLabel());
            GUI.Label(new Rect(x + 200, y, 200, 18), mail.Date, DimLabel());
            y += 22;

            // Separator
            GUI.color = new Color(0.3f, 0.3f, 0.4f);
            GUI.DrawTexture(new Rect(x, y, w, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += 4;

            // Body
            var bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            float bodyH = bodyStyle.CalcHeight(new GUIContent(mail.Body ?? ""), w);
            GUI.Label(new Rect(x, y, w, bodyH), mail.Body ?? "(no content)", bodyStyle);
            y += bodyH + 8;

            // Attachments
            if (mail.Attachments != null && mail.Attachments.Count > 0)
            {
                GUI.Label(new Rect(x, y, 200, 18), "Attachments:", SectionHeader());
                y += 20;
                foreach (var att in mail.Attachments)
                {
                    GUI.color = att.RarityColor;
                    GUI.DrawTexture(new Rect(x, y + 2, 4, 14), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x + 8, y, 200, 18), $"{att.ItemName} x{att.StackCount}", SmallLabel());
                    y += 18;
                }
            }

            // Credits
            if (mail.Credits > 0)
            {
                var credStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
                GUI.Label(new Rect(x, y, 200, 18), $"Credits: {mail.Credits:N0}", credStyle);
                y += 20;
            }

            // COD
            if (mail.CodPrice > 0)
            {
                var codStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.5f, 0.3f) } };
                GUI.Label(new Rect(x, y, 200, 18), $"COD: {mail.CodPrice:N0} credits", codStyle);
                y += 20;
            }

            // Action buttons
            float btnY = area.y + area.height - 30;
            if ((mail.Attachments != null && mail.Attachments.Count > 0) || mail.Credits > 0)
            {
                string collectLabel = mail.CodPrice > 0 ? $"Accept COD ({mail.CodPrice:N0})" : "Collect";
                if (GUI.Button(new Rect(x, btnY, 120, 24), collectLabel))
                    Network.NetworkManager.Instance?.Send(Network.PacketBuilder.MailCollect(mail.MailId));
            }

            if (GUI.Button(new Rect(x + 130, btnY, 60, 24), "Delete"))
                Network.NetworkManager.Instance?.Send(Network.PacketBuilder.MailDelete(mail.MailId));
        }

        private void DrawCompose(Rect area)
        {
            float y = area.y + 4;
            float x = area.x + 4;
            float w = area.width - 8;

            GUI.Label(new Rect(x, y, 50, 20), "To:", SmallLabel());
            _composeTo = GUI.TextField(new Rect(x + 54, y, w - 54, 20), _composeTo, SmallInputStyle());
            y += 24;

            GUI.Label(new Rect(x, y, 50, 20), "Subject:", SmallLabel());
            _composeSubject = GUI.TextField(new Rect(x + 54, y, w - 54, 20), _composeSubject, SmallInputStyle());
            y += 24;

            GUI.Label(new Rect(x, y, 50, 20), "Body:", SmallLabel());
            y += 20;
            _composeBody = GUI.TextArea(new Rect(x, y, w, 120), _composeBody, 500);
            y += 124;

            // Credits
            GUI.Label(new Rect(x, y, 60, 20), "Credits:", SmallLabel());
            _composeCreditsStr = GUI.TextField(new Rect(x + 64, y, 80, 20), _composeCreditsStr, SmallInputStyle());
            long.TryParse(_composeCreditsStr, out _composeCredits);
            y += 24;

            // COD
            _composeCod = GUI.Toggle(new Rect(x, y, 80, 20), _composeCod, "COD");
            if (_composeCod)
            {
                _composeCodStr = GUI.TextField(new Rect(x + 84, y, 80, 20), _composeCodStr, SmallInputStyle());
                long.TryParse(_composeCodStr, out _composeCodPrice);
            }
            y += 24;

            // Attachment slots (placeholder — 5 empty boxes)
            GUI.Label(new Rect(x, y, 100, 18), "Attachments:", DimLabel());
            y += 20;
            for (int i = 0; i < 5; i++)
            {
                GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.8f);
                GUI.DrawTexture(new Rect(x + i * 44, y, 40, 40), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            y += 46;

            // Send
            GUI.enabled = !string.IsNullOrEmpty(_composeTo) && !string.IsNullOrEmpty(_composeSubject);
            if (GUI.Button(new Rect(x, y, 80, 26), "Send"))
            {
                Network.NetworkManager.Instance?.Send(
                    Network.PacketBuilder.SendMail(_composeTo, _composeSubject, _composeBody,
                        _composeCredits, _composeCod ? _composeCodPrice : 0));
                _composeTo = ""; _composeSubject = ""; _composeBody = "";
                _composeCreditsStr = "0"; _composeCodStr = "0"; _composeCod = false;
                ChatUI.Instance?.AddSystemMessage("Mail sent!");
            }
            GUI.enabled = true;
        }

        // ---- Helpers ----

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
        private GUIStyle SmallCentered() => new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        private GUIStyle DimLabel() => new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.5f, 0.5f, 0.6f) } };
        private GUIStyle BoldSmall() => new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        private GUIStyle SectionHeader() => new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.8f, 1f) } };
        private GUIStyle SmallInputStyle() => new GUIStyle(GUI.skin.textField) { fontSize = 11, normal = { textColor = Color.white }, focused = { textColor = Color.white } };
    }
}

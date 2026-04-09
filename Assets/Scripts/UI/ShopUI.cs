using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;
using Orlo.UI.TMD;

namespace Orlo.UI
{
    /// <summary>
    /// Shop/vendor UI — opened by interacting with vendor NPCs.
    /// Shows items for sale with buy/sell buttons and wallet balance.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        private RacePalette P => TMDTheme.Instance?.Palette ?? RacePalette.Solari;

        private bool _visible;
        private string _shopName = "";
        private string _dialogue = "";
        private ulong _npcEntityId;
        private long _walletBalance;
        private string _statusMessage = "";
        private Vector2 _scrollPos;

        public struct ShopItemData
        {
            public string ItemId, Name, Description;
            public long BuyPrice, SellPrice;
            public int Stock;
            public int Category, Rarity;
        }

        private List<ShopItemData> _items = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Show(ulong npcEntityId, string npcName, string dialogue, long balance)
        {
            _npcEntityId = npcEntityId;
            _shopName = npcName;
            _dialogue = dialogue;
            _walletBalance = balance;
            _visible = true;
            _statusMessage = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            _visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetItems(List<ShopItemData> items) { _items = items; }
        public void UpdateBalance(long balance) { _walletBalance = balance; }
        public void SetStatus(string msg) { _statusMessage = msg; }

        private void Update()
        {
            if (_visible && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            float w = 500, h = 550;
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);

            // TMD glassmorphic panel
            TMDTheme.DrawPanel(rect);

            float cx = rect.x + 16;
            float cy = rect.y + 12;
            float pw = w - 32;

            // Shop name in race Primary
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = P.Primary }
            };
            GUI.Label(new Rect(cx, cy, pw, 26), _shopName, titleStyle);
            cy += 28;

            // Dialogue
            var dialogueStyle = new GUIStyle(TMDTheme.LabelStyle)
            {
                fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter
            };
            dialogueStyle.normal.textColor = P.TextDim;
            GUI.Label(new Rect(cx, cy, pw, 20), $"\"{_dialogue}\"", dialogueStyle);
            cy += 24;

            // Wallet in race Accent
            var walletStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleRight,
                normal = { textColor = P.Accent }
            };
            GUI.Label(new Rect(cx, cy, pw, 20), $"Balance: {_walletBalance:N0} creds", walletStyle);
            cy += 24;

            // Separator
            GUI.color = new Color(P.Border.r, P.Border.g, P.Border.b, 0.5f);
            GUI.DrawTexture(new Rect(cx, cy, pw, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            cy += 4;

            // Items list
            float listH = h - (cy - rect.y) - 70;
            Rect listRect = new Rect(cx, cy, pw, listH);
            float totalItemH = _items.Count * 54;

            _scrollPos = GUI.BeginScrollView(listRect, _scrollPos, new Rect(0, 0, pw - 16, totalItemH));
            float iy = 0;
            int selectedIdx = -1;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                Rect itemRect = new Rect(0, iy, pw - 16, 50);
                bool hover = itemRect.Contains(Event.current.mousePosition);

                // Row background with race-colored selection
                if (hover)
                    GUI.color = new Color(P.Primary.r, P.Primary.g, P.Primary.b, 0.12f);
                else
                    GUI.color = i % 2 == 0 ? new Color(P.Background.r, P.Background.g, P.Background.b, 0.3f) : Color.clear;
                if (GUI.color.a > 0)
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Item name with rarity color
                var nameStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    fontStyle = FontStyle.Bold
                };
                nameStyle.normal.textColor = GetRarityColor(item.Rarity);
                GUI.Label(new Rect(4, iy + 4, 240, 18), item.Name, nameStyle);

                // Description
                var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                descStyle.normal.textColor = P.TextDim;
                GUI.Label(new Rect(4, iy + 22, 240, 16), item.Description, descStyle);

                // Stock
                if (item.Stock >= 0)
                {
                    GUI.color = P.TextDim;
                    GUI.Label(new Rect(250, iy + 12, 30, 18), $"x{item.Stock}");
                    GUI.color = Color.white;
                }

                // Buy button via TMD
                bool canBuy = _walletBalance >= item.BuyPrice && item.Stock != 0;
                GUI.enabled = canBuy;
                if (TMDTheme.DrawButton(new Rect(290, iy + 6, 80, 38), $"Buy\n{item.BuyPrice}c"))
                {
                    if (canBuy) BuyItem(item.ItemId);
                }
                GUI.enabled = true;

                // Sell button via TMD
                if (TMDTheme.DrawButton(new Rect(376, iy + 6, 70, 38), $"Sell\n{item.SellPrice}c"))
                {
                    SellItem(item.ItemId);
                }

                iy += 54;
            }
            GUI.EndScrollView();

            cy += listH + 4;

            // Status
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var statusStyle = new GUIStyle(TMDTheme.LabelStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                statusStyle.normal.textColor = P.Success;
                GUI.Label(new Rect(cx, cy, pw, 18), _statusMessage, statusStyle);
                cy += 20;
            }

            // Close button via TMD
            if (TMDTheme.DrawButton(new Rect(cx, rect.yMax - 38, pw, 30), "Close (Esc)"))
                Hide();

            // Scanline overlay
            TMDTheme.DrawScanlines(rect);
        }

        private void BuyItem(string itemId)
        {
            var data = PacketBuilder.ShopBuy(_npcEntityId, itemId, 1);
            NetworkManager.Instance.Send(data);
        }

        private void SellItem(string itemId)
        {
            var data = PacketBuilder.ShopSell(_npcEntityId, itemId, 1);
            NetworkManager.Instance.Send(data);
        }

        private Color GetRarityColor(int rarity)
        {
            return rarity switch
            {
                1 => new Color(0.2f, 0.8f, 0.2f),   // Uncommon - green
                2 => new Color(0.2f, 0.5f, 1.0f),   // Rare - blue
                3 => new Color(0.7f, 0.3f, 0.9f),   // Epic - purple
                4 => new Color(1.0f, 0.6f, 0.1f),   // Legendary - orange
                _ => Color.white                      // Common - white
            };
        }
    }
}

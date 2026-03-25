using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// Shop/vendor UI — opened by interacting with vendor NPCs.
    /// Shows items for sale with buy/sell buttons and wallet balance.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

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
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Space(10);

            // Header
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(_shopName, titleStyle);

            var dialogueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"\"{_dialogue}\"", dialogueStyle);
            GUILayout.Space(5);

            // Wallet
            var walletStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };
            GUILayout.Label($"Balance: {_walletBalance:N0} creds", walletStyle);
            GUILayout.Space(5);

            // Items list
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(380));

            foreach (var item in _items)
            {
                GUILayout.BeginHorizontal("box");

                // Item info
                GUILayout.BeginVertical(GUILayout.Width(280));
                var nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = GetRarityColor(item.Rarity);
                GUILayout.Label(item.Name, nameStyle);

                var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                GUILayout.Label(item.Description, descStyle);
                GUILayout.EndVertical();

                // Stock
                string stockText = item.Stock < 0 ? "" : $"x{item.Stock}";
                GUILayout.Label(stockText, GUILayout.Width(30));

                // Buy button
                GUILayout.BeginVertical(GUILayout.Width(80));
                bool canBuy = _walletBalance >= item.BuyPrice && item.Stock != 0;
                GUI.enabled = canBuy;
                if (GUILayout.Button($"Buy\n{item.BuyPrice}c", GUILayout.Height(40)))
                {
                    BuyItem(item.ItemId);
                }
                GUI.enabled = true;
                GUILayout.EndVertical();

                // Sell button
                GUILayout.BeginVertical(GUILayout.Width(60));
                if (GUILayout.Button($"Sell\n{item.SellPrice}c", GUILayout.Height(40)))
                {
                    SellItem(item.ItemId);
                }
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUILayout.EndScrollView();

            // Status
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.green }
                };
                GUILayout.Label(_statusMessage, statusStyle);
            }

            // Close button
            GUILayout.Space(5);
            if (GUILayout.Button("Close (Esc)", GUILayout.Height(30)))
                Hide();

            GUILayout.EndArea();
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

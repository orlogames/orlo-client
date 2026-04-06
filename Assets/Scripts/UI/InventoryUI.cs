using UnityEngine;
using System.Collections.Generic;
using Orlo.Network;
using Orlo.Proto.Inventory;

namespace Orlo.UI
{
    /// <summary>
    /// Inventory screen with 8x5 grid, equipment panel, tooltips, and context menu.
    /// Receives real inventory data from server via SetItems/AddItem/RemoveItem/UpdateItem.
    /// Falls back to test items when not connected (dev mode).
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        private bool _visible;
        private Vector2 _windowPos = new Vector2(200, 100);
        private bool _dragging;
        private Vector2 _dragOffset;

        // Grid
        private const int Columns = 8;
        private const int Rows = 5;
        private const int TotalSlots = Columns * Rows;
        private const float SlotSize = 48f;
        private const float SlotPadding = 4f;

        // Equipment slots
        private static readonly string[] EquipSlotNames = {
            "Head", "Chest", "Legs", "Feet", "Weapon", "Shield", "Ring", "Amulet"
        };

        // Context menu
        private bool _contextMenuOpen;
        private int _contextMenuSlot = -1;
        private Vector2 _contextMenuPos;
        private static readonly string[] ContextOptions = { "Use", "Equip", "Drop", "Split Stack" };

        // Tooltip
        private int _hoverSlot = -1;

        // Double-click detection
        private float _lastClickTime;
        private int _lastClickSlot = -1;
        private const float DoubleClickThreshold = 0.3f;

        // Auto-equip tooltip
        private float _autoEquipTooltipTimer;
        private string _autoEquipTooltipText;

        // Item data — populated by server or test data
        public struct ItemSlot
        {
            public bool Occupied;
            public uint ItemId;
            public string Name;
            public string Description;
            public Color RarityColor;
            public int Rarity;         // 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary
            public int Category;       // Maps to ItemCategory enum
            public float Weight;
            public int StackCount;
            public float Condition;    // 0.0 = broken, 1.0 = pristine
            public float MaxCondition;
            public string CraftedBy;
            public uint[] ResourceAttrs; // 11 quality attributes (0-1000), null if not a resource
        }

        private ItemSlot[] _slots;
        private ItemSlot[] _equipSlots;
        private float _currentWeight;
        private float _maxWeight = 100f;
        private bool _serverSynced; // true once we receive server data

        // Pending equip/unequip tracking — client is a dumb terminal,
        // we show a pending indicator until the server confirms via EquipmentChanged.
        private HashSet<int> _pendingInventorySlots = new HashSet<int>();   // inventory slots with pending equip
        private HashSet<int> _pendingEquipSlots = new HashSet<int>();       // equipment slots with pending unequip
        private float _pendingPulseTimer;

        // Equipment slot context menu
        private bool _equipContextOpen;
        private int _equipContextSlot = -1;
        private Vector2 _equipContextPos;

        // Resource attribute labels (11 attrs)
        private static readonly string[] AttrLabels = {
            "CN", "TH", "TN", "ML", "RE", "DN", "PR", "RS", "DC", "FL", "HR"
        };

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _slots = new ItemSlot[TotalSlots];
            _equipSlots = new ItemSlot[EquipSlotNames.Length];
            _serverSynced = false;

            // Seed placeholder items for dev mode (replaced when server data arrives)
            AddTestItem(0, "Iron Sword", "A sturdy blade.", new Color(0.4f, 0.6f, 1f), 3.5f, 1);
            AddTestItem(1, "Health Potion", "Restores 50 HP.", Color.green, 0.2f, 5);
            AddTestItem(4, "Dragon Scale", "Rare crafting material.", new Color(1f, 0.5f, 0f), 1.0f, 3);
            AddTestItem(8, "Wooden Shield", "Basic protection.", Color.white, 4.0f, 1);
            AddTestItem(12, "Gold Ring", "Shiny.", Color.yellow, 0.1f, 1);

            _equipSlots[0] = new ItemSlot { Occupied = true, Name = "Iron Helm", Description = "Basic head armor.",
                RarityColor = Color.white, Weight = 2f, StackCount = 1, Condition = 0.85f, MaxCondition = 1f };
            _equipSlots[4] = new ItemSlot { Occupied = true, Name = "Iron Sword", Description = "Equipped weapon.",
                RarityColor = new Color(0.4f, 0.6f, 1f), Weight = 3.5f, StackCount = 1, Condition = 1f, MaxCondition = 1f };

            RecalcWeight();
        }

        private void AddTestItem(int slot, string name, string desc, Color rarity, float weight, int stack)
        {
            _slots[slot] = new ItemSlot
            {
                Occupied = true, Name = name, Description = desc,
                RarityColor = rarity, Weight = weight, StackCount = stack,
                Condition = 1f, MaxCondition = 1f
            };
        }

        // ─── Public API for server sync ─────────────────────────────────────

        /// <summary>
        /// Replace all inventory slots with server-provided data (sent on login, zone change).
        /// </summary>
        public void SetItems(List<ItemSlot> items, List<ItemSlot> equipment, float totalWeight, float maxWeight)
        {
            _serverSynced = true;

            // Full resync — clear all pending states
            _pendingInventorySlots.Clear();
            _pendingEquipSlots.Clear();

            // Clear all slots
            _slots = new ItemSlot[TotalSlots];
            _equipSlots = new ItemSlot[EquipSlotNames.Length];

            foreach (var item in items)
            {
                int idx = (int)item.ItemId; // slot_index is packed into ItemId field by handler
                if (idx >= 0 && idx < TotalSlots)
                    _slots[idx] = item;
            }

            foreach (var eq in equipment)
            {
                int idx = eq.Category; // equip slot index packed into Category by handler
                if (idx >= 0 && idx < _equipSlots.Length)
                    _equipSlots[idx] = eq;
            }

            _currentWeight = totalWeight;
            _maxWeight = maxWeight > 0 ? maxWeight : 100f;
        }

        /// <summary>Add or update item at a specific inventory slot.</summary>
        // Flash effect when new items arrive
        private float _flashTimer;
        private const float FlashDuration = 1.5f;

        /// <summary>Trigger an inventory icon flash to indicate new items arrived.</summary>
        public void FlashNewItem()
        {
            _flashTimer = FlashDuration;
        }

        public void AddItem(int slotIndex, ItemSlot item, float totalWeight)
        {
            _serverSynced = true;
            if (slotIndex >= 0 && slotIndex < TotalSlots)
            {
                _slots[slotIndex] = item;
                _currentWeight = totalWeight;
            }
        }

        /// <summary>Remove item from a specific inventory slot (or reduce stack).</summary>
        public void RemoveItem(int slotIndex, uint quantityRemoved, float totalWeight)
        {
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            if (quantityRemoved >= (uint)_slots[slotIndex].StackCount)
            {
                _slots[slotIndex] = default;
            }
            else
            {
                var s = _slots[slotIndex];
                s.StackCount -= (int)quantityRemoved;
                _slots[slotIndex] = s;
            }
            _currentWeight = totalWeight;
        }

        /// <summary>Update item in a specific slot (e.g., condition changed).</summary>
        public void UpdateItem(int slotIndex, ItemSlot item)
        {
            if (slotIndex >= 0 && slotIndex < TotalSlots)
                _slots[slotIndex] = item;
        }

        /// <summary>
        /// Update equipment slot (equip/unequip). Only called from PacketHandler
        /// when the server sends an EquipmentChanged packet — never from client-side logic.
        /// Clears any pending state for the affected slots.
        /// </summary>
        public void UpdateEquipment(int equipSlot, ItemSlot item, int inventorySlot, ItemSlot inventoryItem)
        {
            if (equipSlot >= 0 && equipSlot < _equipSlots.Length)
                _equipSlots[equipSlot] = item;
            if (inventorySlot >= 0 && inventorySlot < TotalSlots)
                _slots[inventorySlot] = inventoryItem;

            // Clear pending state — server has confirmed the change
            _pendingInventorySlots.Remove(inventorySlot);
            _pendingEquipSlots.Remove(equipSlot);

            RecalcWeight();
        }

        /// <summary>Handle item move confirmation from server.</summary>
        public void ConfirmMove(int fromSlot, int toSlot, ItemSlot fromItem, ItemSlot toItem)
        {
            if (fromSlot >= 0 && fromSlot < TotalSlots)
                _slots[fromSlot] = fromItem;
            if (toSlot >= 0 && toSlot < TotalSlots)
                _slots[toSlot] = toItem;
            RecalcWeight();
        }

        // ─── Private helpers ────────────────────────────────────────────────

        private void RecalcWeight()
        {
            _currentWeight = 0;
            foreach (var s in _slots) if (s.Occupied) _currentWeight += s.Weight * s.StackCount;
            foreach (var s in _equipSlots) if (s.Occupied) _currentWeight += s.Weight * s.StackCount;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                _visible = !_visible;
                _contextMenuOpen = false;
                _equipContextOpen = false;
            }

            _pendingPulseTimer += Time.deltaTime;

            if (_visible && Input.GetMouseButtonDown(0))
            {
                // Close context menus if clicking outside them
                if (_contextMenuOpen)
                {
                    Rect cmRect = new Rect(_contextMenuPos.x, _contextMenuPos.y, 120, ContextOptions.Length * 24);
                    if (!cmRect.Contains(Event.current != null ? Event.current.mousePosition : (Vector2)Input.mousePosition))
                        _contextMenuOpen = false;
                }
                if (_equipContextOpen)
                {
                    Rect ecRect = new Rect(_equipContextPos.x, _equipContextPos.y, 120, 24 + 4);
                    if (!ecRect.Contains(Event.current != null ? Event.current.mousePosition : (Vector2)Input.mousePosition))
                        _equipContextOpen = false;
                }
            }
        }

        private void OnGUI()
        {
            // Flash indicator when inventory is closed and new items arrived
            if (_flashTimer > 0)
            {
                _flashTimer -= Time.deltaTime;
                float pulse = Mathf.Abs(Mathf.Sin(_flashTimer * 4f));
                float s = UIScaler.Scale;
                var iconRect = new Rect(Screen.width - 140 * s, Screen.height - 40 * s, 130 * s, 30 * s);
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.5f + pulse * 0.5f);
                GUI.DrawTexture(iconRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                var flashStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = UIScaler.ScaledFontSize(13),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(iconRect, "NEW ITEM [I]", flashStyle);
            }

            if (!_visible) return;

            float equipPanelW = 120f;
            float gridW = Columns * (SlotSize + SlotPadding) + SlotPadding;
            float gridH = Rows * (SlotSize + SlotPadding) + SlotPadding;
            float windowW = equipPanelW + gridW + 30f;
            float windowH = gridH + 80f; // title bar + weight bar
            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, windowW, windowH);

            // Dark background
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            string title = _serverSynced ? "Inventory" : "Inventory (Dev)";
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, windowW - 24, 24);
            DrawTitleBar(titleBar, title);
            HandleDrag(titleBar);

            // Close button
            if (GUI.Button(new Rect(_windowPos.x + windowW - 24, _windowPos.y, 24, 24), "X"))
            {
                _visible = false;
                return;
            }

            float contentY = _windowPos.y + 28;

            // Equipment panel (left)
            float eqX = _windowPos.x + 8;
            float eqY = contentY;
            for (int i = 0; i < EquipSlotNames.Length; i++)
            {
                Rect slotRect = new Rect(eqX, eqY, equipPanelW - 16, SlotSize * 0.6f);
                bool isPending = _pendingEquipSlots.Contains(i);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

                if (_equipSlots[i].Occupied)
                {
                    // Dim the slot if a pending unequip is in-flight
                    float alpha = isPending ? 0.3f + 0.15f * Mathf.Sin(_pendingPulseTimer * 4f) : 1f;
                    GUI.color = _equipSlots[i].RarityColor * alpha;
                    Rect inner = new Rect(slotRect.x + 2, slotRect.y + 2, slotRect.width - 4, slotRect.height - 4);
                    GUI.DrawTexture(inner, Texture2D.whiteTexture);
                    GUI.color = new Color(0, 0, 0, alpha);
                    GUI.Label(inner, isPending ? "..." : _equipSlots[i].Name, SmallLabelCentered());

                    // Condition bar on equipment
                    if (_equipSlots[i].MaxCondition > 0)
                    {
                        float condPct = Mathf.Clamp01(_equipSlots[i].Condition / _equipSlots[i].MaxCondition);
                        Rect condBar = new Rect(slotRect.x + 2, slotRect.y + slotRect.height - 5, slotRect.width - 4, 3);
                        GUI.color = new Color(0.1f, 0.1f, 0.1f);
                        GUI.DrawTexture(condBar, Texture2D.whiteTexture);
                        GUI.color = condPct > 0.5f ? Color.green : (condPct > 0.2f ? Color.yellow : Color.red);
                        GUI.DrawTexture(new Rect(condBar.x, condBar.y, condBar.width * condPct, condBar.height), Texture2D.whiteTexture);
                    }

                    // Right-click to open unequip context menu
                    if (slotRect.Contains(Event.current.mousePosition) &&
                        Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                        !isPending)
                    {
                        _equipContextOpen = true;
                        _equipContextSlot = i;
                        _equipContextPos = Event.current.mousePosition;
                        Event.current.Use();
                    }
                }
                else
                {
                    GUI.color = new Color(1, 1, 1, 0.3f);
                    GUI.Label(slotRect, EquipSlotNames[i], SmallLabelCentered());
                }

                GUI.color = Color.white;
                eqY += SlotSize * 0.6f + 3;
            }

            // Inventory grid (right)
            float gx0 = _windowPos.x + equipPanelW + 12;
            float gy0 = contentY;
            _hoverSlot = -1;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int idx = row * Columns + col;
                    float sx = gx0 + col * (SlotSize + SlotPadding);
                    float sy = gy0 + row * (SlotSize + SlotPadding);
                    Rect slotRect = new Rect(sx, sy, SlotSize, SlotSize);
                    bool isPending = _pendingInventorySlots.Contains(idx);

                    // Slot background
                    GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                    GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

                    if (_slots[idx].Occupied)
                    {
                        // Dim the slot if a pending equip is in-flight
                        float alpha = isPending ? 0.3f + 0.15f * Mathf.Sin(_pendingPulseTimer * 4f) : 1f;

                        // Item colored box
                        Rect inner = new Rect(sx + 3, sy + 3, SlotSize - 6, SlotSize - 6);
                        GUI.color = _slots[idx].RarityColor * 0.6f * alpha;
                        GUI.DrawTexture(inner, Texture2D.whiteTexture);
                        GUI.color = new Color(1, 1, 1, alpha);
                        GUI.Label(inner, isPending ? "..." : _slots[idx].Name, SmallLabelCentered());

                        // Mini item type icon (top-left corner)
                        Color iconColor;
                        string iconChar;
                        GetItemTypeIcon(_slots[idx], out iconColor, out iconChar);
                        Rect iconBg = new Rect(sx + 3, sy + 3, 14, 14);
                        GUI.color = iconColor * 0.8f * alpha;
                        GUI.DrawTexture(iconBg, Texture2D.whiteTexture);
                        GUI.color = new Color(1, 1, 1, alpha);
                        GUI.Label(iconBg, iconChar, MiniIconLabel());

                        // Stack count
                        if (_slots[idx].StackCount > 1 && !isPending)
                        {
                            GUI.color = Color.yellow;
                            GUI.Label(new Rect(sx + SlotSize - 18, sy + SlotSize - 16, 16, 14),
                                _slots[idx].StackCount.ToString(), SmallLabel());
                            GUI.color = Color.white;
                        }

                        // Condition bar (bottom of slot)
                        if (_slots[idx].MaxCondition > 0 && _slots[idx].Condition < _slots[idx].MaxCondition)
                        {
                            float condPct = Mathf.Clamp01(_slots[idx].Condition / _slots[idx].MaxCondition);
                            Rect condBar = new Rect(sx + 3, sy + SlotSize - 6, SlotSize - 6, 3);
                            GUI.color = new Color(0.1f, 0.1f, 0.1f);
                            GUI.DrawTexture(condBar, Texture2D.whiteTexture);
                            GUI.color = condPct > 0.5f ? Color.green : (condPct > 0.2f ? Color.yellow : Color.red);
                            GUI.DrawTexture(new Rect(condBar.x, condBar.y, condBar.width * condPct, condBar.height), Texture2D.whiteTexture);
                        }
                    }

                    // Hover & click detection
                    if (slotRect.Contains(Event.current.mousePosition))
                    {
                        _hoverSlot = idx;

                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                            _slots[idx].Occupied && !isPending)
                        {
                            // Double-click detection for auto-equip
                            float now = Time.realtimeSinceStartup;
                            if (_lastClickSlot == idx && (now - _lastClickTime) < DoubleClickThreshold)
                            {
                                AutoEquipItem(idx);
                                _lastClickSlot = -1;
                                _lastClickTime = 0;
                                Event.current.Use();
                            }
                            else
                            {
                                _lastClickSlot = idx;
                                _lastClickTime = now;
                            }
                        }

                        // Right-click context menu (blocked while pending)
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                            _slots[idx].Occupied && !isPending)
                        {
                            _contextMenuOpen = true;
                            _contextMenuSlot = idx;
                            _contextMenuPos = Event.current.mousePosition;
                            Event.current.Use();
                        }
                    }
                }
            }

            GUI.color = Color.white;

            // Weight bar
            float barY = gy0 + gridH + 4;
            float barW = gridW;
            Rect barBg = new Rect(gx0, barY, barW, 16);
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);

            float pct = Mathf.Clamp01(_currentWeight / _maxWeight);
            GUI.color = pct > 0.8f ? Color.red : (pct > 0.5f ? Color.yellow : Color.green);
            GUI.DrawTexture(new Rect(gx0, barY, barW * pct, 16), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(barBg, $"Weight: {_currentWeight:F1} / {_maxWeight:F1}", SmallLabelCentered());

            // Auto-equip tooltip
            if (_autoEquipTooltipTimer > 0)
            {
                _autoEquipTooltipTimer -= Time.deltaTime;
                float tooltipAlpha = Mathf.Clamp01(_autoEquipTooltipTimer * 2f);
                Vector2 mp = Event.current.mousePosition;
                Rect tipRect = new Rect(mp.x + 12, mp.y - 20, 90, 20);
                GUI.color = new Color(0.1f, 0.4f, 0.1f, 0.85f * tooltipAlpha);
                GUI.DrawTexture(tipRect, Texture2D.whiteTexture);
                GUI.color = new Color(1, 1, 1, tooltipAlpha);
                GUI.Label(tipRect, _autoEquipTooltipText, SmallLabelCentered());
                GUI.color = Color.white;
            }

            // Tooltip
            if (_hoverSlot >= 0 && _slots[_hoverSlot].Occupied && !_contextMenuOpen)
            {
                DrawTooltip(_slots[_hoverSlot]);
            }

            // Context menu (inventory)
            if (_contextMenuOpen && _contextMenuSlot >= 0)
            {
                DrawContextMenu();
            }

            // Context menu (equipment — right-click to unequip)
            if (_equipContextOpen && _equipContextSlot >= 0)
            {
                DrawEquipContextMenu();
            }
        }

        private void DrawTooltip(ItemSlot item)
        {
            Vector2 mp = Event.current.mousePosition;
            bool hasAttrs = item.ResourceAttrs != null && item.ResourceAttrs.Length == 11;
            bool hasCond = item.MaxCondition > 0;
            bool hasCrafter = !string.IsNullOrEmpty(item.CraftedBy);

            // Calculate tooltip height dynamically
            float th = 72f;
            if (hasCond) th += 18f;
            if (hasCrafter) th += 16f;
            if (hasAttrs) th += 14f * 6 + 4f; // 6 rows of 2 attrs each (11 attrs = 6 rows)

            float tw = hasAttrs ? 220f : 180f;
            Rect bg = new Rect(mp.x + 16, mp.y, tw, th);

            // Clamp to screen
            if (bg.xMax > Screen.width) bg.x = mp.x - tw - 8;
            if (bg.yMax > Screen.height) bg.y = Screen.height - th;

            GUI.color = new Color(0, 0, 0, 0.92f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);

            float y = bg.y + 2;

            // Name
            GUI.color = item.RarityColor;
            GUI.Label(new Rect(bg.x + 4, y, tw - 8, 18), item.Name, SmallLabel());
            y += 18;

            // Description
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            GUI.Label(new Rect(bg.x + 4, y, tw - 8, 18), item.Description, SmallLabel());
            y += 18;

            // Weight + Stack
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(bg.x + 4, y, tw - 8, 18), $"Weight: {item.Weight:F1}  Stack: {item.StackCount}", SmallLabel());
            y += 18;

            // Condition/Durability bar
            if (hasCond)
            {
                float condPct = Mathf.Clamp01(item.Condition / item.MaxCondition);
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(bg.x + 4, y, 60, 14), "Durability:", SmallLabel());

                Rect condBarBg = new Rect(bg.x + 66, y + 3, tw - 74, 8);
                GUI.color = new Color(0.2f, 0.2f, 0.2f);
                GUI.DrawTexture(condBarBg, Texture2D.whiteTexture);
                GUI.color = condPct > 0.5f ? Color.green : (condPct > 0.2f ? Color.yellow : Color.red);
                GUI.DrawTexture(new Rect(condBarBg.x, condBarBg.y, condBarBg.width * condPct, condBarBg.height), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(condBarBg.x, condBarBg.y - 2, condBarBg.width, 12),
                    $"{condPct * 100:F0}%", SmallLabelCentered());
                y += 18;
            }

            // Crafted by
            if (hasCrafter)
            {
                GUI.color = new Color(0.5f, 0.8f, 1f);
                GUI.Label(new Rect(bg.x + 4, y, tw - 8, 14), $"Crafted by: {item.CraftedBy}", SmallLabel());
                y += 16;
            }

            // Resource quality attributes (11 attrs in a grid)
            if (hasAttrs)
            {
                y += 2;
                GUI.color = new Color(0.9f, 0.8f, 0.4f);
                GUI.Label(new Rect(bg.x + 4, y, tw - 8, 12), "Quality Attributes:", SmallLabel());
                y += 14;

                float halfW = (tw - 12) / 2f;
                for (int i = 0; i < 11; i++)
                {
                    float ax = (i % 2 == 0) ? bg.x + 4 : bg.x + 4 + halfW;
                    float ay = y + (i / 2) * 14;

                    float attrPct = item.ResourceAttrs[i] / 1000f;
                    string label = $"{AttrLabels[i]}: {item.ResourceAttrs[i]}";

                    // Small quality bar
                    GUI.color = new Color(0.2f, 0.2f, 0.2f);
                    Rect attrBarBg = new Rect(ax + 42, ay + 3, halfW - 48, 7);
                    GUI.DrawTexture(attrBarBg, Texture2D.whiteTexture);

                    GUI.color = Color.Lerp(Color.red, Color.green, attrPct);
                    GUI.DrawTexture(new Rect(attrBarBg.x, attrBarBg.y, attrBarBg.width * attrPct, attrBarBg.height), Texture2D.whiteTexture);

                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    GUI.Label(new Rect(ax, ay, 40, 12), label, SmallLabel());
                }
            }

            GUI.color = Color.white;
        }

        private void DrawContextMenu()
        {
            float cmW = 120, cmH = ContextOptions.Length * 24 + 4;
            Rect bg = new Rect(_contextMenuPos.x, _contextMenuPos.y, cmW, cmH);

            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            for (int i = 0; i < ContextOptions.Length; i++)
            {
                Rect btn = new Rect(_contextMenuPos.x + 2, _contextMenuPos.y + 2 + i * 24, cmW - 4, 22);
                if (GUI.Button(btn, ContextOptions[i]))
                {
                    HandleContextAction(i, _contextMenuSlot);
                    _contextMenuOpen = false;
                }
            }
        }

        private void DrawEquipContextMenu()
        {
            float cmW = 120, cmH = 28;
            Rect bg = new Rect(_equipContextPos.x, _equipContextPos.y, cmW, cmH);

            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect btn = new Rect(_equipContextPos.x + 2, _equipContextPos.y + 2, cmW - 4, 22);
            if (GUI.Button(btn, "Unequip"))
            {
                Debug.Log($"[InventoryUI] Unequip slot {_equipContextSlot}: {_equipSlots[_equipContextSlot].Name}");
                if (_serverSynced && !_pendingEquipSlots.Contains(_equipContextSlot))
                {
                    // Map equip slot index to proto enum and send
                    var protoSlot = MapEquipIndexToProto(_equipContextSlot);
                    if (protoSlot != Orlo.Proto.Inventory.EquipmentSlot.None)
                    {
                        var data = PacketBuilder.UnequipItem(protoSlot);
                        NetworkManager.Instance?.Send(data);
                        _pendingEquipSlots.Add(_equipContextSlot);
                    }
                }
                _equipContextOpen = false;
            }
        }

        private Orlo.Proto.Inventory.EquipmentSlot MapEquipIndexToProto(int index)
        {
            return index switch
            {
                0 => Orlo.Proto.Inventory.EquipmentSlot.Head,
                1 => Orlo.Proto.Inventory.EquipmentSlot.Chest,
                2 => Orlo.Proto.Inventory.EquipmentSlot.Legs,
                3 => Orlo.Proto.Inventory.EquipmentSlot.Feet,
                4 => Orlo.Proto.Inventory.EquipmentSlot.RightHand,
                5 => Orlo.Proto.Inventory.EquipmentSlot.Gloves,
                6 => Orlo.Proto.Inventory.EquipmentSlot.LeftBracer,
                7 => Orlo.Proto.Inventory.EquipmentSlot.RightBracer,
                _ => Orlo.Proto.Inventory.EquipmentSlot.None
            };
        }

        private void HandleContextAction(int action, int slot)
        {
            if (slot < 0 || slot >= TotalSlots || !_slots[slot].Occupied) return;

            switch (action)
            {
                case 0: // Use
                    Debug.Log($"[InventoryUI] Use item in slot {slot}: {_slots[slot].Name}");
                    // TODO: Send UseItem packet when proto message is defined
                    break;
                case 1: // Equip
                    Debug.Log($"[InventoryUI] Equip request for slot {slot}: {_slots[slot].Name}");
                    if (_serverSynced && !_pendingInventorySlots.Contains(slot))
                    {
                        // Send equip request to server — do NOT modify local state.
                        // Client is a dumb terminal: wait for EquipmentChanged from server.
                        var data = PacketBuilder.EquipItem((uint)slot);
                        NetworkManager.Instance?.Send(data);
                        _pendingInventorySlots.Add(slot);
                    }
                    break;
                case 2: // Drop
                    Debug.Log($"[InventoryUI] Drop item in slot {slot}: {_slots[slot].Name}");
                    if (_serverSynced)
                    {
                        var data = PacketBuilder.DropItem((uint)slot, (uint)_slots[slot].StackCount);
                        NetworkManager.Instance?.Send(data);
                    }
                    break;
                case 3: // Split Stack
                    Debug.Log($"[InventoryUI] Split stack in slot {slot}: {_slots[slot].Name}");
                    // TODO: Open split dialog to choose quantity and target slot
                    break;
            }
        }

        /// <summary>Clear all pending states. Called on disconnect or full inventory resync.</summary>
        public void ClearPendingStates()
        {
            _pendingInventorySlots.Clear();
            _pendingEquipSlots.Clear();
        }

        private void DrawTitleBar(Rect rect, string title)
        {
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, "  " + title, BoldLabel());
        }

        private void HandleDrag(Rect titleBar)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - _windowPos;
                e.Use();
            }
            if (_dragging && e.type == EventType.MouseDrag)
            {
                _windowPos = e.mousePosition - _dragOffset;
                e.Use();
            }
            if (e.type == EventType.MouseUp)
                _dragging = false;
        }

        // ─── Rarity color helper ────────────────────────────────────────────

        public static Color GetRarityColor(int rarity)
        {
            Color baseColor = rarity switch
            {
                1 => new Color(0.2f, 0.8f, 0.2f),   // Uncommon - green
                2 => new Color(0.2f, 0.5f, 1.0f),   // Rare - blue
                3 => new Color(0.7f, 0.3f, 0.9f),   // Epic - purple
                4 => new Color(1.0f, 0.6f, 0.1f),   // Legendary - orange
                _ => Color.white                      // Common - white
            };
            // Apply colorblind remapping
            if (AccessibilityManager.Instance != null)
                baseColor = AccessibilityManager.Instance.RemapColor(baseColor);
            return baseColor;
        }

        // ─── Double-click auto-equip ────────────────────────────────────────

        /// <summary>
        /// Auto-equip an inventory item by double-clicking. Determines the target
        /// equipment slot from the item name/type and sends an EquipItemRequest.
        /// </summary>
        private void AutoEquipItem(int inventorySlot)
        {
            if (inventorySlot < 0 || inventorySlot >= TotalSlots) return;
            if (!_slots[inventorySlot].Occupied) return;
            if (!_serverSynced) return;
            if (_pendingInventorySlots.Contains(inventorySlot)) return;

            var targetSlot = ResolveEquipSlotFromItem(_slots[inventorySlot]);
            if (targetSlot == Orlo.Proto.Inventory.EquipmentSlot.None)
            {
                Debug.Log($"[InventoryUI] Cannot auto-equip '{_slots[inventorySlot].Name}' — no matching slot");
                return;
            }

            Debug.Log($"[InventoryUI] Auto-equip slot {inventorySlot}: {_slots[inventorySlot].Name} -> {targetSlot}");
            var data = PacketBuilder.EquipItem((uint)inventorySlot, targetSlot);
            NetworkManager.Instance?.Send(data);
            _pendingInventorySlots.Add(inventorySlot);

            _autoEquipTooltipText = "Equipping...";
            _autoEquipTooltipTimer = 1.2f;
        }

        /// <summary>
        /// Resolve the best equipment slot for an item based on its name keywords.
        /// </summary>
        private static Orlo.Proto.Inventory.EquipmentSlot ResolveEquipSlotFromItem(ItemSlot item)
        {
            string name = item.Name.ToLowerInvariant();

            // Head
            if (name.Contains("helmet") || name.Contains("helm") || name.Contains("head") || name.Contains("hood"))
                return Orlo.Proto.Inventory.EquipmentSlot.Head;

            // Chest
            if (name.Contains("chest") || name.Contains("chestplate") || name.Contains("armor") || name.Contains("vest") || name.Contains("tunic"))
                return Orlo.Proto.Inventory.EquipmentSlot.Chest;

            // Legs
            if (name.Contains("legs") || name.Contains("leggings") || name.Contains("pants") || name.Contains("greaves"))
                return Orlo.Proto.Inventory.EquipmentSlot.Legs;

            // Feet
            if (name.Contains("boots") || name.Contains("feet") || name.Contains("shoes"))
                return Orlo.Proto.Inventory.EquipmentSlot.Feet;

            // Gloves
            if (name.Contains("gloves") || name.Contains("hands") || name.Contains("gauntlets"))
                return Orlo.Proto.Inventory.EquipmentSlot.Gloves;

            // Shoulders
            if (name.Contains("shoulder") || name.Contains("pauldron"))
                return Orlo.Proto.Inventory.EquipmentSlot.Shoulders;

            // Belt
            if (name.Contains("belt") || name.Contains("sash"))
                return Orlo.Proto.Inventory.EquipmentSlot.Belt;

            // Backpack / Cloak
            if (name.Contains("backpack") || name.Contains("back") || name.Contains("cloak") || name.Contains("cape"))
                return Orlo.Proto.Inventory.EquipmentSlot.Backpack;

            // Bracer / Wrist
            if (name.Contains("bracer") || name.Contains("wrist") || name.Contains("bracelet"))
                return Orlo.Proto.Inventory.EquipmentSlot.LeftBracer;

            // Two-handed weapons
            if (name.Contains("rifle") || name.Contains("hammer") || name.Contains("staff") || name.Contains("greataxe") || name.Contains("greatsword"))
                return Orlo.Proto.Inventory.EquipmentSlot.TwoHands;

            // Right-hand weapons (melee)
            if (name.Contains("sword") || name.Contains("blade") || name.Contains("weapon") || name.Contains("knife") || name.Contains("axe") || name.Contains("dagger") || name.Contains("mace"))
                return Orlo.Proto.Inventory.EquipmentSlot.RightHand;

            // Left-hand weapons (ranged / off-hand)
            if (name.Contains("pistol") || name.Contains("gun") || name.Contains("ranged") || name.Contains("shield"))
                return Orlo.Proto.Inventory.EquipmentSlot.LeftHand;

            return Orlo.Proto.Inventory.EquipmentSlot.None;
        }

        // ─── Mini item type icons ───────────────────────────────────────────

        /// <summary>
        /// Get a color and character icon for an item based on its name/category.
        /// </summary>
        private static void GetItemTypeIcon(ItemSlot item, out Color color, out string icon)
        {
            string name = item.Name.ToLowerInvariant();

            // Weapons (orange, "W")
            if (name.Contains("sword") || name.Contains("blade") || name.Contains("weapon") ||
                name.Contains("knife") || name.Contains("pistol") || name.Contains("gun") ||
                name.Contains("rifle") || name.Contains("hammer") || name.Contains("staff") ||
                name.Contains("axe") || name.Contains("dagger") || name.Contains("mace") ||
                name.Contains("bow"))
            {
                color = new Color(1.0f, 0.6f, 0.2f); // orange
                icon = "W";
                return;
            }

            // Armor (steel blue, "A")
            if (name.Contains("helmet") || name.Contains("helm") || name.Contains("chest") ||
                name.Contains("armor") || name.Contains("legs") || name.Contains("leggings") ||
                name.Contains("boots") || name.Contains("gloves") || name.Contains("shield") ||
                name.Contains("shoulder") || name.Contains("bracer") || name.Contains("belt") ||
                name.Contains("backpack") || name.Contains("cloak") || name.Contains("hood") ||
                name.Contains("gauntlets") || name.Contains("greaves"))
            {
                color = new Color(0.45f, 0.6f, 0.8f); // steel blue
                icon = "A";
                return;
            }

            // Consumables (green, "C")
            if (name.Contains("potion") || name.Contains("food") || name.Contains("drink") ||
                name.Contains("stim") || name.Contains("med") || name.Contains("salve") ||
                name.Contains("elixir") || name.Contains("pill"))
            {
                color = new Color(0.3f, 0.8f, 0.3f); // green
                icon = "C";
                return;
            }

            // Tools (purple, "T")
            if (name.Contains("tool") || name.Contains("wrench") || name.Contains("scanner") ||
                name.Contains("tmd") || name.Contains("survey") || name.Contains("pick") ||
                name.Contains("harvester"))
            {
                color = new Color(0.7f, 0.4f, 0.9f); // purple
                icon = "T";
                return;
            }

            // Resources (brown, "R")
            if (name.Contains("ore") || name.Contains("scale") || name.Contains("bone") ||
                name.Contains("hide") || name.Contains("wood") || name.Contains("crystal") ||
                name.Contains("fragment") || name.Contains("sinew") || name.Contains("mineral") ||
                item.ResourceAttrs != null)
            {
                color = new Color(0.6f, 0.4f, 0.2f); // brown
                icon = "R";
                return;
            }

            // Default — misc (grey, "?")
            color = new Color(0.5f, 0.5f, 0.5f);
            icon = "?";
        }

        // ─── Shared styles ──────────────────────────────────────────────────

        private GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle MiniIconLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
    }
}

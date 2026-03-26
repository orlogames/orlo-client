using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Inventory screen with 8x5 grid, equipment panel, tooltips, and context menu.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
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

        // Placeholder item data
        private struct ItemSlot
        {
            public bool Occupied;
            public string Name;
            public string Description;
            public Color RarityColor;
            public float Weight;
            public int StackCount;
        }

        private ItemSlot[] _slots;
        private ItemSlot[] _equipSlots;
        private float _currentWeight;
        private float _maxWeight = 100f;

        private void Awake()
        {
            _slots = new ItemSlot[TotalSlots];
            _equipSlots = new ItemSlot[EquipSlotNames.Length];

            // Seed some placeholder items for testing
            AddTestItem(0, "Iron Sword", "A sturdy blade.", new Color(0.4f, 0.6f, 1f), 3.5f, 1);
            AddTestItem(1, "Health Potion", "Restores 50 HP.", Color.green, 0.2f, 5);
            AddTestItem(4, "Dragon Scale", "Rare crafting material.", new Color(1f, 0.5f, 0f), 1.0f, 3);
            AddTestItem(8, "Wooden Shield", "Basic protection.", Color.white, 4.0f, 1);
            AddTestItem(12, "Gold Ring", "Shiny.", Color.yellow, 0.1f, 1);

            _equipSlots[0] = new ItemSlot { Occupied = true, Name = "Iron Helm", Description = "Basic head armor.", RarityColor = Color.white, Weight = 2f, StackCount = 1 };
            _equipSlots[4] = new ItemSlot { Occupied = true, Name = "Iron Sword", Description = "Equipped weapon.", RarityColor = new Color(0.4f, 0.6f, 1f), Weight = 3.5f, StackCount = 1 };

            RecalcWeight();
        }

        private void AddTestItem(int slot, string name, string desc, Color rarity, float weight, int stack)
        {
            _slots[slot] = new ItemSlot { Occupied = true, Name = name, Description = desc, RarityColor = rarity, Weight = weight, StackCount = stack };
        }

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
            }

            if (_visible && Input.GetMouseButtonDown(0) && _contextMenuOpen)
            {
                // Close context menu if clicking outside it
                Rect cmRect = new Rect(_contextMenuPos.x, _contextMenuPos.y, 120, ContextOptions.Length * 24);
                if (!cmRect.Contains(Event.current != null ? Event.current.mousePosition : (Vector2)Input.mousePosition))
                    _contextMenuOpen = false;
            }
        }

        private void OnGUI()
        {
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
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, windowW - 24, 24);
            DrawTitleBar(titleBar, "Inventory");
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
            var labelStyle = SmallLabel();
            for (int i = 0; i < EquipSlotNames.Length; i++)
            {
                Rect slotRect = new Rect(eqX, eqY, equipPanelW - 16, SlotSize * 0.6f);
                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

                if (_equipSlots[i].Occupied)
                {
                    GUI.color = _equipSlots[i].RarityColor;
                    Rect inner = new Rect(slotRect.x + 2, slotRect.y + 2, slotRect.width - 4, slotRect.height - 4);
                    GUI.DrawTexture(inner, Texture2D.whiteTexture);
                    GUI.color = Color.black;
                    GUI.Label(inner, _equipSlots[i].Name, SmallLabelCentered());
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

                    // Slot background
                    GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                    GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

                    if (_slots[idx].Occupied)
                    {
                        // Item colored box
                        Rect inner = new Rect(sx + 3, sy + 3, SlotSize - 6, SlotSize - 6);
                        GUI.color = _slots[idx].RarityColor * 0.6f;
                        GUI.DrawTexture(inner, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        GUI.Label(inner, _slots[idx].Name, SmallLabelCentered());

                        // Stack count
                        if (_slots[idx].StackCount > 1)
                        {
                            GUI.color = Color.yellow;
                            GUI.Label(new Rect(sx + SlotSize - 18, sy + SlotSize - 16, 16, 14),
                                _slots[idx].StackCount.ToString(), SmallLabel());
                            GUI.color = Color.white;
                        }
                    }

                    // Hover & click detection
                    if (slotRect.Contains(Event.current.mousePosition))
                    {
                        _hoverSlot = idx;

                        // Right-click context menu
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && _slots[idx].Occupied)
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

            // Tooltip
            if (_hoverSlot >= 0 && _slots[_hoverSlot].Occupied && !_contextMenuOpen)
            {
                DrawTooltip(_slots[_hoverSlot]);
            }

            // Context menu
            if (_contextMenuOpen && _contextMenuSlot >= 0)
            {
                DrawContextMenu();
            }
        }

        private void DrawTooltip(ItemSlot item)
        {
            Vector2 mp = Event.current.mousePosition;
            float tw = 180, th = 72;
            Rect bg = new Rect(mp.x + 16, mp.y, tw, th);

            GUI.color = new Color(0, 0, 0, 0.92f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);

            GUI.color = item.RarityColor;
            GUI.Label(new Rect(bg.x + 4, bg.y + 2, tw - 8, 18), item.Name, SmallLabel());

            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            GUI.Label(new Rect(bg.x + 4, bg.y + 20, tw - 8, 18), item.Description, SmallLabel());

            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(bg.x + 4, bg.y + 38, tw - 8, 18), $"Weight: {item.Weight:F1}", SmallLabel());
            GUI.Label(new Rect(bg.x + 4, bg.y + 54, tw - 8, 18), $"Stack: {item.StackCount}", SmallLabel());

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
                    Debug.Log($"[InventoryUI] {ContextOptions[i]} on slot {_contextMenuSlot}: {_slots[_contextMenuSlot].Name}");
                    _contextMenuOpen = false;
                }
            }
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

        // Shared styles
        private GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
    }
}

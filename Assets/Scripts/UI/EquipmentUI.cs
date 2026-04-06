using UnityEngine;
using System.Collections.Generic;
using Orlo.Network;
using ProtoInventory = Orlo.Proto.Inventory;

namespace Orlo.UI
{
    /// <summary>
    /// Paper-doll equipment screen with 15 SWG-style equipment slots arranged
    /// around a character silhouette. Toggle with 'C' key.
    /// Uses OnGUI for rapid prototyping — matches InventoryUI pattern.
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        public static EquipmentUI Instance { get; private set; }

        private bool _visible;
        private Vector2 _windowPos = new Vector2(300, 60);
        private bool _dragging;
        private Vector2 _dragOffset;

        // Slot definitions — IDs match proto EquipmentSlot enum values (1-15)
        public enum SlotId
        {
            Head = 1,
            Chest = 2,
            Legs = 3,
            Feet = 4,
            Gloves = 5,
            LeftBracer = 6,
            RightBracer = 7,
            LeftBicep = 8,
            RightBicep = 9,
            Shoulders = 10,
            Belt = 11,
            Backpack = 12,
            LeftWrist = 13,
            RightWrist = 14,
            LeftHand = 15,
            RightHand = 16,
            TwoHands = 17
        }

        private static readonly SlotId[] AllSlots = {
            SlotId.Head, SlotId.Chest, SlotId.Legs, SlotId.Feet, SlotId.Gloves,
            SlotId.LeftBracer, SlotId.RightBracer, SlotId.LeftBicep, SlotId.RightBicep,
            SlotId.Shoulders, SlotId.Belt, SlotId.Backpack, SlotId.LeftWrist,
            SlotId.RightWrist, SlotId.RightHand
        };

        private static readonly Dictionary<SlotId, string> SlotLabels = new()
        {
            { SlotId.Head, "Head" },
            { SlotId.Chest, "Chest" },
            { SlotId.Legs, "Legs" },
            { SlotId.Feet, "Feet" },
            { SlotId.Gloves, "Gloves" },
            { SlotId.LeftBracer, "L.Bracer" },
            { SlotId.RightBracer, "R.Bracer" },
            { SlotId.LeftBicep, "L.Bicep" },
            { SlotId.RightBicep, "R.Bicep" },
            { SlotId.Shoulders, "Shoulders" },
            { SlotId.Belt, "Belt" },
            { SlotId.Backpack, "Backpack" },
            { SlotId.LeftWrist, "L.Wrist" },
            { SlotId.RightWrist, "R.Wrist" },
            { SlotId.RightHand, "R.Hand" }
        };

        // Slot layout — relative positions within the paper-doll area
        // Grid is based on a 5-column, 7-row layout with column width 72 and row height 56
        private const float ColW = 72f;
        private const float RowH = 56f;
        private const float SlotW = 64f;
        private const float SlotH = 48f;

        // (col, row) for each slot — center column is 2
        private static readonly Dictionary<SlotId, Vector2> SlotPositions = new()
        {
            //                               col   row
            { SlotId.Head,        new Vector2(2,    0) },     // center top
            { SlotId.LeftBicep,   new Vector2(0,    1) },     // left
            { SlotId.Shoulders,   new Vector2(2,    1) },     // center
            { SlotId.RightBicep,  new Vector2(4,    1) },     // right
            { SlotId.LeftBracer,  new Vector2(0,    2) },     // left
            { SlotId.Chest,       new Vector2(2,    2) },     // center
            { SlotId.RightBracer, new Vector2(4,    2) },     // right
            { SlotId.LeftWrist,   new Vector2(0,    3) },     // left
            { SlotId.Belt,        new Vector2(2,    3) },     // center
            { SlotId.RightWrist,  new Vector2(4,    3) },     // right
            { SlotId.Gloves,      new Vector2(0,    4) },     // left
            { SlotId.Legs,        new Vector2(2,    4) },     // center
            { SlotId.Backpack,    new Vector2(4,    4) },     // right
            { SlotId.Feet,        new Vector2(2,    5) },     // center bottom
            { SlotId.RightHand,    new Vector2(2,    6) },     // below feet
        };

        // Equipment data — keyed by slot ID
        private Dictionary<SlotId, InventoryUI.ItemSlot> _equipped = new();

        // Context menu
        private bool _contextMenuOpen;
        private SlotId _contextMenuSlot;
        private Vector2 _contextMenuPos;

        // Pending state — slots waiting for server confirmation.
        // Client is a dumb terminal: all equipment mutations go through the server.
        private HashSet<SlotId> _pendingSlots = new HashSet<SlotId>();
        private float _pendingPulseTimer;

        // Selection highlight for equip-from-inventory
        private SlotId _selectedSlot = 0;

        // Hover
        private SlotId _hoverSlot = 0;

        // Stats panel
        private float _totalArmor;
        private float _kineticResist;
        private float _energyResist;
        private float _thermalResist;
        private float _weaponDamage;
        private float _totalWeight;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Seed dev-mode placeholder items
            _equipped[SlotId.Head] = new InventoryUI.ItemSlot
            {
                Occupied = true, Name = "Iron Helm", Description = "Basic head armor. +12 Armor.",
                RarityColor = Color.white, Weight = 2f, StackCount = 1, Condition = 0.85f, MaxCondition = 1f
            };
            _equipped[SlotId.RightHand] = new InventoryUI.ItemSlot
            {
                Occupied = true, Name = "Iron Sword", Description = "A sturdy blade. 15-22 Kinetic damage.",
                RarityColor = new Color(0.4f, 0.6f, 1f), Weight = 3.5f, StackCount = 1, Condition = 1f, MaxCondition = 1f
            };
            _equipped[SlotId.Chest] = new InventoryUI.ItemSlot
            {
                Occupied = true, Name = "Padded Vest", Description = "Light torso armor. +18 Armor.",
                RarityColor = new Color(0.2f, 0.8f, 0.2f), Weight = 3f, StackCount = 1, Condition = 0.92f, MaxCondition = 1f
            };

            RecalcStats();
        }

        // ─── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Server-confirmed equip: update display for a specific slot.
        /// ONLY called from PacketHandler when the server sends EquipmentChanged.
        /// Never call this from client-side user actions.
        /// </summary>
        public void ServerEquipItem(SlotId slot, InventoryUI.ItemSlot item)
        {
            item.Occupied = true;
            _equipped[slot] = item;
            _pendingSlots.Remove(slot);
            RecalcStats();
        }

        /// <summary>Server-confirmed equip using integer slot ID (matches proto enum).</summary>
        public void ServerEquipItem(int slotId, InventoryUI.ItemSlot item)
        {
            if (System.Enum.IsDefined(typeof(SlotId), slotId))
                ServerEquipItem((SlotId)slotId, item);
        }

        /// <summary>
        /// Server-confirmed unequip: remove item from display.
        /// ONLY called from PacketHandler when the server sends EquipmentChanged with empty item.
        /// </summary>
        public void ServerUnequipItem(SlotId slot)
        {
            _equipped.Remove(slot);
            _pendingSlots.Remove(slot);
            RecalcStats();
        }

        /// <summary>Server-confirmed unequip using integer slot ID.</summary>
        public void ServerUnequipItem(int slotId)
        {
            if (System.Enum.IsDefined(typeof(SlotId), slotId))
                ServerUnequipItem((SlotId)slotId);
        }

        /// <summary>Clear all equipment (on zone change, death, etc.).</summary>
        public void ClearAll()
        {
            _equipped.Clear();
            _pendingSlots.Clear();
            RecalcStats();
        }

        /// <summary>Set all equipment from server data (login, zone change). Clears pending states.</summary>
        public void SetEquipment(Dictionary<int, InventoryUI.ItemSlot> items)
        {
            _equipped.Clear();
            _pendingSlots.Clear();
            foreach (var kv in items)
            {
                if (System.Enum.IsDefined(typeof(SlotId), kv.Key))
                {
                    var item = kv.Value;
                    item.Occupied = true;
                    _equipped[(SlotId)kv.Key] = item;
                }
            }
            RecalcStats();
        }

        /// <summary>Clear all pending states (on disconnect, full resync, etc.).</summary>
        public void ClearPendingStates()
        {
            _pendingSlots.Clear();
        }

        /// <summary>Check if a slot is waiting for server confirmation.</summary>
        public bool IsSlotPending(SlotId slot) => _pendingSlots.Contains(slot);

        /// <summary>Get the currently selected empty slot (for equip-from-inventory).</summary>
        public SlotId GetSelectedSlot() => _selectedSlot;

        /// <summary>Check if a slot has an item equipped.</summary>
        public bool IsSlotOccupied(SlotId slot) => _equipped.ContainsKey(slot);

        /// <summary>Get all equipped items as a dictionary keyed by proto slot ID (int).</summary>
        public Dictionary<int, InventoryUI.ItemSlot> GetEquippedItems()
        {
            var result = new Dictionary<int, InventoryUI.ItemSlot>();
            foreach (var kv in _equipped)
                result[(int)kv.Key] = kv.Value;
            return result;
        }

        public bool IsVisible => _visible;

        public void Toggle() { _visible = !_visible; _contextMenuOpen = false; }
        public void Show() { _visible = true; }
        public void Hide() { _visible = false; _contextMenuOpen = false; }

        // ─── Internals ──────────────────────────────────────────────────────

        private void RecalcStats()
        {
            _totalArmor = 0f;
            _kineticResist = 0f;
            _energyResist = 0f;
            _thermalResist = 0f;
            _weaponDamage = 0f;
            _totalWeight = 0f;

            foreach (var kv in _equipped)
            {
                if (!kv.Value.Occupied) continue;
                _totalWeight += kv.Value.Weight;

                // Parse stats from description (simple pattern: "+NN Armor", "NN-NN damage")
                // In production these would come from item stat fields
                string desc = kv.Value.Description ?? "";
                if (desc.Contains("Armor"))
                {
                    // Try to extract armor value
                    var match = System.Text.RegularExpressions.Regex.Match(desc, @"\+(\d+)\s*Armor");
                    if (match.Success) _totalArmor += float.Parse(match.Groups[1].Value);
                }
                if (desc.Contains("damage"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(desc, @"(\d+)-(\d+)");
                    if (match.Success)
                    {
                        float lo = float.Parse(match.Groups[1].Value);
                        float hi = float.Parse(match.Groups[2].Value);
                        _weaponDamage += (lo + hi) / 2f;
                    }
                }
            }

            // Derive resistances from total armor (simplified)
            _kineticResist = _totalArmor * 0.4f;
            _energyResist = _totalArmor * 0.3f;
            _thermalResist = _totalArmor * 0.2f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                _visible = !_visible;
                _contextMenuOpen = false;
            }

            _pendingPulseTimer += Time.deltaTime;

            if (_visible && Input.GetMouseButtonDown(0) && _contextMenuOpen)
            {
                Rect cmRect = new Rect(_contextMenuPos.x, _contextMenuPos.y, 120, 28);
                if (!cmRect.Contains(Input.mousePosition))
                    _contextMenuOpen = false;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // Window dimensions
            float paperDollW = 5 * ColW + 16; // 5 columns + padding
            float paperDollH = 7 * RowH + 8;  // 7 rows + padding
            float statsW = 160f;
            float windowW = paperDollW + statsW + 24;
            float windowH = paperDollH + 36; // title bar
            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, windowW, windowH);

            // Dark background
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, windowW - 24, 24);
            DrawTitleBar(titleBar, "Equipment");
            HandleDrag(titleBar);

            // Close button
            if (GUI.Button(new Rect(_windowPos.x + windowW - 24, _windowPos.y, 24, 24), "X"))
            {
                _visible = false;
                return;
            }

            float contentX = _windowPos.x + 8;
            float contentY = _windowPos.y + 28;

            // ─── Paper doll silhouette (subtle body outline) ────────────
            DrawSilhouette(contentX, contentY, paperDollW, paperDollH);

            // ─── Equipment slots ────────────────────────────────────────
            _hoverSlot = 0;

            foreach (var slotId in AllSlots)
            {
                if (!SlotPositions.TryGetValue(slotId, out var gridPos)) continue;

                float sx = contentX + gridPos.x * ColW + (ColW - SlotW) / 2f;
                float sy = contentY + gridPos.y * RowH + (RowH - SlotH) / 2f;
                Rect slotRect = new Rect(sx, sy, SlotW, SlotH);

                DrawEquipSlot(slotRect, slotId);
            }

            // ─── Stats panel (right side) ───────────────────────────────
            float statsX = contentX + paperDollW + 8;
            float statsY = contentY;
            DrawStatsPanel(statsX, statsY, statsW, paperDollH);

            // ─── Tooltip ────────────────────────────────────────────────
            if (_hoverSlot != 0 && _equipped.ContainsKey(_hoverSlot) && !_contextMenuOpen)
            {
                DrawTooltip(_equipped[_hoverSlot]);
            }

            // ─── Context menu ───────────────────────────────────────────
            if (_contextMenuOpen)
            {
                DrawContextMenu();
            }
        }

        private void DrawEquipSlot(Rect rect, SlotId slotId)
        {
            bool occupied = _equipped.TryGetValue(slotId, out var item) && item.Occupied;
            bool selected = _selectedSlot == slotId;
            bool isPending = _pendingSlots.Contains(slotId);

            // Slot background
            if (selected && !occupied)
                GUI.color = new Color(0.3f, 0.5f, 0.3f, 0.9f); // highlight selected empty slot
            else
                GUI.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Border — pulse yellow if pending
            if (isPending)
            {
                float pulse = 0.4f + 0.3f * Mathf.Sin(_pendingPulseTimer * 4f);
                GUI.color = new Color(0.8f, 0.7f, 0.2f, pulse);
            }
            else
            {
                GUI.color = occupied ? new Color(0.4f, 0.4f, 0.5f) : new Color(0.25f, 0.25f, 0.3f);
            }
            DrawBorder(rect, 1);

            if (occupied)
            {
                // Dim the slot if a pending unequip/equip is in-flight
                float alpha = isPending ? 0.3f + 0.15f * Mathf.Sin(_pendingPulseTimer * 4f) : 1f;

                // Rarity-colored inner box
                Rect inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 16);
                Color rarityCol = item.RarityColor;
                if (AccessibilityManager.Instance != null)
                    rarityCol = AccessibilityManager.Instance.RemapColor(rarityCol);
                GUI.color = rarityCol * 0.5f * alpha;
                GUI.DrawTexture(inner, Texture2D.whiteTexture);

                // Item name (show "..." while pending)
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.Label(inner, isPending ? "..." : item.Name, SmallLabelCentered());

                // Condition bar
                if (item.MaxCondition > 0 && !isPending)
                {
                    float condPct = Mathf.Clamp01(item.Condition / item.MaxCondition);
                    Rect condBar = new Rect(rect.x + 2, rect.y + rect.height - 14, rect.width - 4, 3);
                    GUI.color = new Color(0.1f, 0.1f, 0.1f);
                    GUI.DrawTexture(condBar, Texture2D.whiteTexture);
                    GUI.color = condPct > 0.5f ? Color.green : (condPct > 0.2f ? Color.yellow : Color.red);
                    GUI.DrawTexture(new Rect(condBar.x, condBar.y, condBar.width * condPct, condBar.height), Texture2D.whiteTexture);
                }

                // Slot label below condition bar
                GUI.color = new Color(0.6f, 0.6f, 0.6f, alpha);
                Rect labelRect = new Rect(rect.x, rect.y + rect.height - 12, rect.width, 12);
                GUI.Label(labelRect, SlotLabels[slotId], TinyLabelCentered());
            }
            else
            {
                // Empty slot — show slot name
                GUI.color = new Color(1, 1, 1, 0.25f);
                GUI.Label(rect, SlotLabels[slotId], SmallLabelCentered());
            }

            GUI.color = Color.white;

            // Interaction — blocked while pending
            if (rect.Contains(Event.current.mousePosition))
            {
                _hoverSlot = slotId;

                if (Event.current.type == EventType.MouseDown && !isPending)
                {
                    if (Event.current.button == 1 && occupied)
                    {
                        // Right-click — context menu
                        _contextMenuOpen = true;
                        _contextMenuSlot = slotId;
                        _contextMenuPos = Event.current.mousePosition;
                        Event.current.Use();
                    }
                    else if (Event.current.button == 0 && !occupied)
                    {
                        // Left-click empty slot — toggle selection
                        _selectedSlot = (_selectedSlot == slotId) ? 0 : slotId;
                        Event.current.Use();
                    }
                }
            }
        }

        private void DrawSilhouette(float x, float y, float w, float h)
        {
            // Draw subtle body-shaped outline behind the slots
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 0.4f);

            float cx = x + w / 2f;

            // Head circle area
            Rect head = new Rect(cx - 20, y + 4, 40, 36);
            GUI.DrawTexture(head, Texture2D.whiteTexture);

            // Torso
            Rect torso = new Rect(cx - 30, y + 38, 60, RowH * 3.2f);
            GUI.DrawTexture(torso, Texture2D.whiteTexture);

            // Arms (left and right)
            Rect leftArm = new Rect(cx - 58, y + 50, 30, RowH * 2.5f);
            GUI.DrawTexture(leftArm, Texture2D.whiteTexture);
            Rect rightArm = new Rect(cx + 28, y + 50, 30, RowH * 2.5f);
            GUI.DrawTexture(rightArm, Texture2D.whiteTexture);

            // Legs
            Rect leftLeg = new Rect(cx - 22, y + RowH * 3.5f, 20, RowH * 2.5f);
            GUI.DrawTexture(leftLeg, Texture2D.whiteTexture);
            Rect rightLeg = new Rect(cx + 2, y + RowH * 3.5f, 20, RowH * 2.5f);
            GUI.DrawTexture(rightLeg, Texture2D.whiteTexture);

            GUI.color = Color.white;
        }

        private void DrawStatsPanel(float x, float y, float w, float h)
        {
            // Stats panel background
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.8f);
            Rect bg = new Rect(x, y, w, h);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float ly = y + 4;
            float lx = x + 6;
            float lw = w - 12;

            // Section: Defense
            GUI.color = new Color(0.9f, 0.8f, 0.4f);
            GUI.Label(new Rect(lx, ly, lw, 16), "DEFENSE", SmallLabelBold());
            ly += 18;

            DrawStatRow(lx, ly, lw, "Total Armor", _totalArmor.ToString("F0")); ly += 16;
            DrawStatRow(lx, ly, lw, "Kinetic Resist", $"{_kineticResist:F0}"); ly += 16;
            DrawStatRow(lx, ly, lw, "Energy Resist", $"{_energyResist:F0}"); ly += 16;
            DrawStatRow(lx, ly, lw, "Thermal Resist", $"{_thermalResist:F0}"); ly += 16;

            ly += 8;

            // Section: Offense
            GUI.color = new Color(0.9f, 0.4f, 0.4f);
            GUI.Label(new Rect(lx, ly, lw, 16), "OFFENSE", SmallLabelBold());
            ly += 18;

            DrawStatRow(lx, ly, lw, "Weapon DPS", _weaponDamage > 0 ? $"{_weaponDamage:F1}" : "--"); ly += 16;

            ly += 8;

            // Section: Encumbrance
            GUI.color = new Color(0.5f, 0.8f, 1f);
            GUI.Label(new Rect(lx, ly, lw, 16), "ENCUMBRANCE", SmallLabelBold());
            ly += 18;

            DrawStatRow(lx, ly, lw, "Equip Weight", $"{_totalWeight:F1}"); ly += 16;
            DrawStatRow(lx, ly, lw, "Slots Used", $"{_equipped.Count} / 15"); ly += 16;

            ly += 12;

            // Equipped items list
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(lx, ly, lw, 16), "EQUIPPED", SmallLabelBold());
            ly += 18;

            foreach (var kv in _equipped)
            {
                if (!kv.Value.Occupied) continue;
                if (ly + 14 > y + h - 4) break; // overflow guard

                Color rarityCol = kv.Value.RarityColor;
                if (AccessibilityManager.Instance != null)
                    rarityCol = AccessibilityManager.Instance.RemapColor(rarityCol);

                GUI.color = rarityCol;
                GUI.Label(new Rect(lx, ly, lw, 13), $"{SlotLabels[kv.Key]}: {kv.Value.Name}", TinyLabel());
                ly += 14;
            }

            GUI.color = Color.white;
        }

        private void DrawStatRow(float x, float y, float w, string label, string value)
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(x, y, w * 0.65f, 14), label, TinyLabel());
            GUI.color = Color.white;
            GUI.Label(new Rect(x + w * 0.65f, y, w * 0.35f, 14), value, TinyLabelRight());
        }

        private void DrawTooltip(InventoryUI.ItemSlot item)
        {
            Vector2 mp = Event.current.mousePosition;
            bool hasCond = item.MaxCondition > 0;
            bool hasCrafter = !string.IsNullOrEmpty(item.CraftedBy);

            float th = 56f;
            if (hasCond) th += 18f;
            if (hasCrafter) th += 16f;

            float tw = 200f;
            Rect bg = new Rect(mp.x + 16, mp.y, tw, th);
            if (bg.xMax > Screen.width) bg.x = mp.x - tw - 8;
            if (bg.yMax > Screen.height) bg.y = Screen.height - th;

            GUI.color = new Color(0, 0, 0, 0.92f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);

            float y = bg.y + 2;

            GUI.color = item.RarityColor;
            GUI.Label(new Rect(bg.x + 4, y, tw - 8, 18), item.Name, SmallLabel());
            y += 18;

            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            GUI.Label(new Rect(bg.x + 4, y, tw - 8, 18), item.Description, SmallLabel());
            y += 18;

            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(bg.x + 4, y, tw - 8, 18), $"Weight: {item.Weight:F1}", SmallLabel());
            y += 18;

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

            if (hasCrafter)
            {
                GUI.color = new Color(0.5f, 0.8f, 1f);
                GUI.Label(new Rect(bg.x + 4, y, tw - 8, 14), $"Crafted by: {item.CraftedBy}", SmallLabel());
                y += 16;
            }

            GUI.color = Color.white;
        }

        private void DrawContextMenu()
        {
            float cmW = 120, cmH = 28;
            Rect bg = new Rect(_contextMenuPos.x, _contextMenuPos.y, cmW, cmH);

            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect btn = new Rect(_contextMenuPos.x + 2, _contextMenuPos.y + 2, cmW - 4, 22);
            if (GUI.Button(btn, "Unequip"))
            {
                HandleUnequip(_contextMenuSlot);
                _contextMenuOpen = false;
            }
        }

        private void HandleUnequip(SlotId slot)
        {
            if (!_equipped.ContainsKey(slot)) return;
            if (_pendingSlots.Contains(slot)) return; // Already pending

            Debug.Log($"[EquipmentUI] Unequip request for slot {slot}: {_equipped[slot].Name}");

            // Send unequip packet to server — do NOT modify local state.
            // Client is a dumb terminal: wait for EquipmentChanged from server.
            var protoSlot = MapSlotToProto(slot);
            if (protoSlot != ProtoInventory.EquipmentSlot.None)
            {
                var data = PacketBuilder.UnequipItem(protoSlot);
                NetworkManager.Instance?.Send(data);
                _pendingSlots.Add(slot);
            }
        }

        private ProtoInventory.EquipmentSlot MapSlotToProto(SlotId slot)
        {
            return slot switch
            {
                SlotId.Head        => ProtoInventory.EquipmentSlot.Head,
                SlotId.Chest       => ProtoInventory.EquipmentSlot.Chest,
                SlotId.Legs        => ProtoInventory.EquipmentSlot.Legs,
                SlotId.Feet        => ProtoInventory.EquipmentSlot.Feet,
                SlotId.Gloves      => ProtoInventory.EquipmentSlot.Gloves,
                SlotId.LeftBracer  => ProtoInventory.EquipmentSlot.LeftBracer,
                SlotId.RightBracer => ProtoInventory.EquipmentSlot.RightBracer,
                SlotId.LeftBicep   => ProtoInventory.EquipmentSlot.LeftBicep,
                SlotId.RightBicep  => ProtoInventory.EquipmentSlot.RightBicep,
                SlotId.Shoulders   => ProtoInventory.EquipmentSlot.Shoulders,
                SlotId.Belt        => ProtoInventory.EquipmentSlot.Belt,
                SlotId.Backpack    => ProtoInventory.EquipmentSlot.Backpack,
                SlotId.LeftWrist   => ProtoInventory.EquipmentSlot.LeftWrist,
                SlotId.RightWrist  => ProtoInventory.EquipmentSlot.RightWrist,
                SlotId.RightHand    => ProtoInventory.EquipmentSlot.RightHand,
                _                  => ProtoInventory.EquipmentSlot.None
            };
        }

        // ─── Drawing helpers ────────────────────────────────────────────────

        private void DrawBorder(Rect rect, float thickness)
        {
            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            // Right
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
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

        // ─── Shared styles (matching InventoryUI) ───────────────────────────

        private GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle SmallLabelBold()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private GUIStyle TinyLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 8, normal = { textColor = Color.white }, wordWrap = false };
        }

        private GUIStyle TinyLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private GUIStyle TinyLabelRight()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
    }
}

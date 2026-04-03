using UnityEngine;
using System.Collections.Generic;
using Orlo.Network;

namespace Orlo.UI
{
    /// <summary>
    /// Two-phase crafting UI: Assembly (select recipe + resources) then Experimentation (allocate points).
    /// Matches the server's CraftingSystem phases: Assembly -> Experimentation -> Finalize.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        public static CraftingUI Instance { get; private set; }

        private bool _visible;
        private Vector2 _windowPos = new Vector2(100, 60);
        private bool _dragging;
        private Vector2 _dragOffset;

        // ─── Phase tracking ──────────────────────────────────────────────
        private enum CraftPhase { RecipeSelect, Assembly, Experimenting, Finalizing, Complete }
        private CraftPhase _phase = CraftPhase.RecipeSelect;

        // ─── Recipe list ─────────────────────────────────────────────────
        private Vector2 _recipeScroll;
        private int _selectedRecipe = -1;
        private int _selectedStation;
        private List<RecipeData> _recipes = new();

        private static readonly string[] StationNames = { "All", "Workbench", "Forge", "Lab", "Kitchen" };

        // ─── Assembly state ──────────────────────────────────────────────
        private int[] _selectedResourceSlots;       // Index into _inventoryResources per slot
        private List<ResourceEntry> _inventoryResources = new();
        private Vector2 _resourceScroll;
        private int _activeSlotPicking = -1;        // Which slot is being filled
        private string _assemblyTier = "";          // Server response: Amazing/Great/Good/Decent/Barely
        private bool _assemblyWaiting;

        // ─── Experimentation state ───────────────────────────────────────
        private int _experimentPointsTotal;
        private int _experimentPointsRemaining;
        private int _experimentRound;
        private int _experimentMaxRounds = 8;
        private int[] _pointAllocation;             // Points per stat category this round
        private List<ExperimentCategory> _experimentCategories = new();
        private List<string> _experimentLog = new();
        private Vector2 _experimentLogScroll;
        private bool _experimentWaiting;

        // ─── Result ──────────────────────────────────────────────────────
        private CraftResultData _craftResult;
        private string _statusMessage = "";
        private float _statusTimer;

        // ─── Data Structures ─────────────────────────────────────────────

        public struct RecipeData
        {
            public uint RecipeId;
            public string Name;
            public string Description;
            public int Station;                     // CraftingStation enum
            public string Profession;               // Weaponsmith, Armorsmith, etc.
            public int SkillRequired;
            public ResourceSlot[] ResourceSlots;
            public string[] OutputStats;            // Stat names the item can have
        }

        public struct ResourceSlot
        {
            public string SlotName;                 // "Primary Metal", "Binding Agent", etc.
            public int RequiredClass;               // ResourceClass enum value
            public string RequiredClassName;        // Human-readable
            public string[] AttributeWeights;       // Which attributes matter for this slot
        }

        public struct ResourceEntry
        {
            public uint SlotIndex;                  // Inventory slot
            public uint ItemId;
            public string Name;
            public ulong SpawnId;
            public int ResourceClass;
            public uint[] Attributes;               // 11 quality attributes (0-1000)
            public int Quantity;
        }

        public struct ExperimentCategory
        {
            public string Name;                     // "Damage", "Armor", "Speed", etc.
            public float CurrentValue;
            public float MaxValue;
        }

        public struct CraftResultData
        {
            public string ItemName;
            public string CraftedBy;
            public string AssemblyTier;
            public Dictionary<string, float> Stats;
            public float Condition;
        }

        // ─── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C) && !Input.GetKey(KeyCode.LeftControl)
                && !Input.GetKey(KeyCode.LeftShift))
            {
                if (_visible && _phase == CraftPhase.RecipeSelect)
                {
                    Hide();
                }
                else if (!_visible)
                {
                    Show();
                }
            }

            if (_visible && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_phase == CraftPhase.Assembly)
                    _phase = CraftPhase.RecipeSelect;
                else if (_phase == CraftPhase.RecipeSelect)
                    Hide();
            }

            if (_statusTimer > 0)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0) _statusMessage = "";
            }
        }

        // ─── Public API ──────────────────────────────────────────────────

        public void Show()
        {
            _visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RequestRecipeList();
        }

        public void Hide()
        {
            _visible = false;
            _phase = CraftPhase.RecipeSelect;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Called by PacketHandler when server sends recipe list.
        /// </summary>
        public void SetRecipes(List<RecipeData> recipes)
        {
            _recipes = recipes;
            _selectedRecipe = -1;
        }

        /// <summary>
        /// Called by PacketHandler when server sends available resources for crafting.
        /// </summary>
        public void SetInventoryResources(List<ResourceEntry> resources)
        {
            _inventoryResources = resources;
        }

        /// <summary>
        /// Called by PacketHandler with assembly result from server.
        /// </summary>
        public void OnAssemblyResult(string tier, int experimentPoints, List<ExperimentCategory> categories)
        {
            _assemblyWaiting = false;
            _assemblyTier = tier;
            _experimentPointsTotal = experimentPoints;
            _experimentPointsRemaining = experimentPoints;
            _experimentRound = 0;
            _experimentCategories = categories;
            _pointAllocation = new int[categories.Count];
            _experimentLog.Clear();
            _experimentLog.Add($"Assembly: {tier} quality");
            _experimentLog.Add($"Experiment points: {experimentPoints}");
            _phase = CraftPhase.Experimenting;
        }

        /// <summary>
        /// Called by PacketHandler with experiment round result.
        /// </summary>
        public void OnExperimentResult(int categoryIndex, float newValue, bool success, bool critical,
            string message)
        {
            _experimentWaiting = false;

            if (categoryIndex >= 0 && categoryIndex < _experimentCategories.Count)
            {
                var cat = _experimentCategories[categoryIndex];
                cat.CurrentValue = newValue;
                _experimentCategories[categoryIndex] = cat;
            }

            string prefix = critical ? "[CRITICAL] " : success ? "[Success] " : "[Failure] ";
            _experimentLog.Add($"R{_experimentRound}: {prefix}{message}");
        }

        /// <summary>
        /// Called by PacketHandler when crafting completes after finalization.
        /// </summary>
        public void OnCraftComplete(CraftResultData result)
        {
            _craftResult = result;
            _phase = CraftPhase.Complete;
            _experimentWaiting = false;
            _assemblyWaiting = false;
        }

        /// <summary>
        /// Called by PacketHandler on crafting failure/error.
        /// </summary>
        public void OnCraftError(string error)
        {
            _statusMessage = error;
            _statusTimer = 5f;
            _assemblyWaiting = false;
            _experimentWaiting = false;
            _phase = CraftPhase.RecipeSelect;
        }

        /// <summary>
        /// Called by PacketHandler for simple CraftProgress (legacy path).
        /// </summary>
        public void OnCraftProgress(uint recipeId, float progress)
        {
            // For the legacy simple crafting path — show as assembly waiting
            _assemblyWaiting = true;
        }

        /// <summary>
        /// Called by PacketHandler for simple CraftComplete (legacy path).
        /// </summary>
        public void OnSimpleCraftComplete(bool success, string itemName)
        {
            _assemblyWaiting = false;
            if (success)
            {
                _statusMessage = $"Crafted: {itemName}";
                _statusTimer = 4f;
            }
            else
            {
                _statusMessage = "Crafting failed!";
                _statusTimer = 4f;
            }
            _phase = CraftPhase.RecipeSelect;
        }

        // ─── OnGUI ──────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;

            float windowW = 720, windowH = 520;
            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, windowW, windowH);

            // Background
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            string title = _phase switch
            {
                CraftPhase.Assembly => "Crafting - Assembly",
                CraftPhase.Experimenting => "Crafting - Experimentation",
                CraftPhase.Complete => "Crafting - Complete",
                _ => "Crafting"
            };
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, windowW - 24, 26);
            DrawTitleBar(titleBar, title);
            HandleDrag(titleBar);

            if (GUI.Button(new Rect(_windowPos.x + windowW - 24, _windowPos.y, 24, 26), "X"))
            {
                Hide();
                return;
            }

            float cx = _windowPos.x + 8;
            float cy = _windowPos.y + 32;

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUI.color = new Color(1f, 0.8f, 0.2f);
                GUI.Label(new Rect(cx, cy, windowW - 16, 18), _statusMessage, SmallLabelCentered());
                GUI.color = Color.white;
                cy += 20;
            }

            switch (_phase)
            {
                case CraftPhase.RecipeSelect:
                    DrawRecipeSelectPhase(cx, cy, windowW - 16, windowH - (cy - _windowPos.y) - 8);
                    break;
                case CraftPhase.Assembly:
                    DrawAssemblyPhase(cx, cy, windowW - 16, windowH - (cy - _windowPos.y) - 8);
                    break;
                case CraftPhase.Experimenting:
                    DrawExperimentPhase(cx, cy, windowW - 16, windowH - (cy - _windowPos.y) - 8);
                    break;
                case CraftPhase.Complete:
                    DrawCompletePhase(cx, cy, windowW - 16, windowH - (cy - _windowPos.y) - 8);
                    break;
            }
        }

        // ─── Phase 0: Recipe Selection ───────────────────────────────────

        private void DrawRecipeSelectPhase(float x, float y, float w, float h)
        {
            // Station filter tabs
            GUI.Label(new Rect(x, y, 50, 20), "Station:", SmallLabel());
            for (int i = 0; i < StationNames.Length; i++)
            {
                bool selected = i == _selectedStation;
                GUI.color = selected ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
                GUI.DrawTexture(new Rect(x + 54 + i * 76, y, 72, 20), Texture2D.whiteTexture);
                GUI.color = selected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                if (GUI.Button(new Rect(x + 54 + i * 76, y, 72, 20), StationNames[i], SmallLabelCentered()))
                {
                    _selectedStation = i;
                    _selectedRecipe = -1;
                }
            }
            GUI.color = Color.white;
            y += 26;

            // Left panel: recipe list
            float listW = 180, listH = h - 32;
            Rect listArea = new Rect(x, y, listW, listH);
            GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            GUI.DrawTexture(listArea, Texture2D.whiteTexture);
            GUI.color = Color.white;

            int visibleCount = 0;
            foreach (var r in _recipes)
                if (_selectedStation == 0 || r.Station == _selectedStation) visibleCount++;

            _recipeScroll = GUI.BeginScrollView(listArea, _recipeScroll,
                new Rect(0, 0, listW - 20, visibleCount * 28));
            int visIdx = 0;
            for (int i = 0; i < _recipes.Count; i++)
            {
                if (_selectedStation > 0 && _recipes[i].Station != _selectedStation)
                    continue;

                Rect btn = new Rect(2, visIdx * 28, listW - 24, 26);
                bool isSel = i == _selectedRecipe;
                GUI.color = isSel ? new Color(0.2f, 0.4f, 0.7f, 0.9f) : new Color(0.12f, 0.12f, 0.14f, 0.9f);
                GUI.DrawTexture(btn, Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (GUI.Button(btn, _recipes[i].Name, SmallLabel()))
                    _selectedRecipe = i;
                visIdx++;
            }
            GUI.EndScrollView();

            // Right panel: recipe detail + start assembly button
            float detailX = x + listW + 10;
            float detailW = w - listW - 10;
            float detailY = y;

            if (_selectedRecipe >= 0 && _selectedRecipe < _recipes.Count)
            {
                RecipeData r = _recipes[_selectedRecipe];

                GUI.Label(new Rect(detailX, detailY, detailW, 22), r.Name, BoldLabel());
                detailY += 24;

                if (!string.IsNullOrEmpty(r.Profession))
                {
                    GUI.color = new Color(0.6f, 0.8f, 1f);
                    GUI.Label(new Rect(detailX, detailY, detailW, 18),
                        $"Profession: {r.Profession}  |  Skill Req: {r.SkillRequired}", SmallLabel());
                    GUI.color = Color.white;
                    detailY += 20;
                }

                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(detailX, detailY, detailW, 18),
                    $"Station: {GetStationName(r.Station)}", SmallLabel());
                GUI.color = Color.white;
                detailY += 22;

                GUI.Label(new Rect(detailX, detailY, detailW, 40), r.Description, SmallLabel());
                detailY += 44;

                // Resource slots preview
                if (r.ResourceSlots != null && r.ResourceSlots.Length > 0)
                {
                    GUI.Label(new Rect(detailX, detailY, detailW, 20), "Required Resources:", BoldLabel());
                    detailY += 22;

                    foreach (var slot in r.ResourceSlots)
                    {
                        GUI.color = new Color(0.8f, 0.8f, 0.6f);
                        string weights = slot.AttributeWeights != null
                            ? string.Join(", ", slot.AttributeWeights) : "";
                        GUI.Label(new Rect(detailX + 8, detailY, detailW - 8, 18),
                            $"{slot.SlotName}: {slot.RequiredClassName}", SmallLabel());
                        detailY += 16;
                        if (!string.IsNullOrEmpty(weights))
                        {
                            GUI.color = new Color(0.5f, 0.5f, 0.5f);
                            GUI.Label(new Rect(detailX + 16, detailY, detailW - 16, 16),
                                $"Weights: {weights}", TinyLabel());
                            detailY += 16;
                        }
                    }
                    GUI.color = Color.white;
                    detailY += 8;
                }

                // Output stats preview
                if (r.OutputStats != null && r.OutputStats.Length > 0)
                {
                    GUI.Label(new Rect(detailX, detailY, detailW, 20), "Possible Stats:", SmallLabel());
                    detailY += 18;
                    GUI.color = new Color(0.6f, 0.9f, 0.6f);
                    GUI.Label(new Rect(detailX + 8, detailY, detailW - 8, 18),
                        string.Join(", ", r.OutputStats), SmallLabel());
                    GUI.color = Color.white;
                    detailY += 24;
                }

                // Assemble button
                if (GUI.Button(new Rect(detailX, detailY, 160, 32), "Begin Assembly"))
                {
                    BeginAssembly(_selectedRecipe);
                }
            }
            else
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f);
                GUI.Label(new Rect(detailX, detailY + 60, detailW, 20),
                    "Select a recipe to begin crafting", SmallLabelCentered());
                GUI.color = Color.white;
            }
        }

        // ─── Phase 1: Assembly ───────────────────────────────────────────

        private void DrawAssemblyPhase(float x, float y, float w, float h)
        {
            if (_selectedRecipe < 0 || _selectedRecipe >= _recipes.Count) return;
            RecipeData r = _recipes[_selectedRecipe];

            // Back button
            if (GUI.Button(new Rect(x, y, 80, 22), "< Back"))
            {
                _phase = CraftPhase.RecipeSelect;
                return;
            }

            GUI.Label(new Rect(x + 90, y, 200, 22), $"Assembling: {r.Name}", BoldLabel());
            y += 28;

            float leftW = w * 0.5f;
            float rightW = w * 0.5f - 4;

            // Left: Resource slots
            GUI.Label(new Rect(x, y, leftW, 20), "Resource Slots", BoldLabel());
            y += 22;

            if (r.ResourceSlots != null)
            {
                for (int i = 0; i < r.ResourceSlots.Length; i++)
                {
                    var slot = r.ResourceSlots[i];
                    float slotY = y + i * 64;

                    // Slot background
                    Rect slotRect = new Rect(x, slotY, leftW - 8, 58);
                    bool isActive = _activeSlotPicking == i;
                    GUI.color = isActive ? new Color(0.15f, 0.25f, 0.4f, 0.9f)
                        : new Color(0.08f, 0.08f, 0.1f, 0.9f);
                    GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    // Slot name + required class
                    GUI.Label(new Rect(x + 4, slotY + 2, leftW - 80, 18), slot.SlotName, SmallLabel());
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    GUI.Label(new Rect(x + 4, slotY + 18, leftW - 80, 16),
                        $"Needs: {slot.RequiredClassName}", TinyLabel());
                    GUI.color = Color.white;

                    // Selected resource display
                    bool hasResource = _selectedResourceSlots != null && i < _selectedResourceSlots.Length
                        && _selectedResourceSlots[i] >= 0;
                    if (hasResource)
                    {
                        int resIdx = _selectedResourceSlots[i];
                        if (resIdx < _inventoryResources.Count)
                        {
                            var res = _inventoryResources[resIdx];
                            GUI.color = new Color(0.5f, 1f, 0.5f);
                            GUI.Label(new Rect(x + 4, slotY + 34, leftW - 80, 18),
                                $"{res.Name} (x{res.Quantity})", SmallLabel());
                            GUI.color = Color.white;
                        }
                    }
                    else
                    {
                        GUI.color = new Color(0.8f, 0.4f, 0.4f);
                        GUI.Label(new Rect(x + 4, slotY + 34, leftW - 80, 18), "Empty", SmallLabel());
                        GUI.color = Color.white;
                    }

                    // Pick / Clear buttons
                    if (GUI.Button(new Rect(x + leftW - 72, slotY + 4, 60, 22),
                        isActive ? "Cancel" : "Pick"))
                    {
                        _activeSlotPicking = isActive ? -1 : i;
                    }
                    if (hasResource)
                    {
                        if (GUI.Button(new Rect(x + leftW - 72, slotY + 30, 60, 22), "Clear"))
                        {
                            _selectedResourceSlots[i] = -1;
                        }
                    }
                }

                y += r.ResourceSlots.Length * 64 + 8;
            }

            // Right: Resource picker (visible when a slot is being filled)
            float rightX = x + leftW + 4;
            float rightY = _windowPos.y + 60;

            GUI.Label(new Rect(rightX, rightY, rightW, 20), "Available Resources", BoldLabel());
            rightY += 22;

            Rect resourceListRect = new Rect(rightX, rightY, rightW, h - 100);
            GUI.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            GUI.DrawTexture(resourceListRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _resourceScroll = GUI.BeginScrollView(resourceListRect, _resourceScroll,
                new Rect(0, 0, rightW - 20, _inventoryResources.Count * 50));

            for (int ri = 0; ri < _inventoryResources.Count; ri++)
            {
                var res = _inventoryResources[ri];
                float ry = ri * 50;

                Rect resBg = new Rect(2, ry, rightW - 24, 48);
                GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
                GUI.DrawTexture(resBg, Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Resource name + quantity
                GUI.Label(new Rect(4, ry + 2, rightW - 80, 18),
                    $"{res.Name} x{res.Quantity}", SmallLabel());

                // Quality preview (show top 3 attributes)
                if (res.Attributes != null && res.Attributes.Length >= 7)
                {
                    GUI.color = new Color(0.5f, 0.7f, 0.5f);
                    GUI.Label(new Rect(4, ry + 18, rightW - 80, 14),
                        $"CN:{res.Attributes[0]} TN:{res.Attributes[2]} PR:{res.Attributes[6]}",
                        TinyLabel());

                    GUI.Label(new Rect(4, ry + 32, rightW - 80, 14),
                        $"ML:{res.Attributes[3]} RS:{res.Attributes[7]} DC:{res.Attributes[8]}",
                        TinyLabel());
                    GUI.color = Color.white;
                }

                // Select button
                if (_activeSlotPicking >= 0)
                {
                    if (GUI.Button(new Rect(rightW - 70, ry + 10, 52, 26), "Use"))
                    {
                        if (_selectedResourceSlots != null && _activeSlotPicking < _selectedResourceSlots.Length)
                        {
                            _selectedResourceSlots[_activeSlotPicking] = ri;
                            _activeSlotPicking = -1;
                        }
                    }
                }
            }

            GUI.EndScrollView();

            // Assemble button (bottom)
            bool allSlotsFilled = AllSlotsFilled();
            GUI.enabled = allSlotsFilled && !_assemblyWaiting;
            if (GUI.Button(new Rect(x, y, 160, 32), _assemblyWaiting ? "Assembling..." : "Assemble"))
            {
                SendAssembleRequest();
            }
            GUI.enabled = true;

            if (!allSlotsFilled)
            {
                GUI.color = new Color(0.7f, 0.5f, 0.2f);
                GUI.Label(new Rect(x + 170, y + 6, 300, 20),
                    "Fill all resource slots to assemble", SmallLabel());
                GUI.color = Color.white;
            }
        }

        // ─── Phase 2: Experimentation ────────────────────────────────────

        private void DrawExperimentPhase(float x, float y, float w, float h)
        {
            // Assembly tier banner
            Color tierColor = GetTierColor(_assemblyTier);
            GUI.color = tierColor;
            GUI.Label(new Rect(x, y, w, 22), $"Assembly Tier: {_assemblyTier}", BoldLabel());
            GUI.color = Color.white;
            y += 26;

            // Points remaining
            GUI.Label(new Rect(x, y, w * 0.5f, 20),
                $"Experiment Points: {_experimentPointsRemaining}/{_experimentPointsTotal}", SmallLabel());
            GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 20),
                $"Round: {_experimentRound}/{_experimentMaxRounds}", SmallLabel());
            y += 24;

            float leftW = w * 0.55f;
            float rightW = w * 0.45f - 4;
            float rightX = x + leftW + 4;

            // Left: stat categories with allocation sliders
            GUI.Label(new Rect(x, y, leftW, 20), "Stat Categories", BoldLabel());
            y += 22;

            int totalAllocated = 0;
            if (_pointAllocation != null)
            {
                for (int i = 0; i < _experimentCategories.Count; i++)
                {
                    var cat = _experimentCategories[i];
                    float catY = y + i * 50;

                    // Category background
                    Rect catRect = new Rect(x, catY, leftW - 8, 46);
                    GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
                    GUI.DrawTexture(catRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    // Name + current value
                    GUI.Label(new Rect(x + 4, catY + 2, leftW * 0.5f, 18), cat.Name, SmallLabel());
                    GUI.color = new Color(0.6f, 0.9f, 0.6f);
                    GUI.Label(new Rect(x + leftW * 0.5f, catY + 2, leftW * 0.3f, 18),
                        $"{cat.CurrentValue:F1}/{cat.MaxValue:F1}", SmallLabel());
                    GUI.color = Color.white;

                    // Value bar
                    float barX = x + 4;
                    float barY = catY + 20;
                    float barW = leftW - 100;
                    float barH = 10;
                    GUI.color = new Color(0.15f, 0.15f, 0.15f);
                    GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
                    float fill = cat.MaxValue > 0 ? cat.CurrentValue / cat.MaxValue : 0;
                    GUI.color = Color.Lerp(new Color(0.3f, 0.5f, 0.8f), new Color(0.2f, 0.9f, 0.3f), fill);
                    GUI.DrawTexture(new Rect(barX, barY, barW * fill, barH), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    // Point allocation buttons: -, value, +
                    int alloc = i < _pointAllocation.Length ? _pointAllocation[i] : 0;
                    float btnX = x + leftW - 92;
                    float btnY = catY + 22;

                    GUI.enabled = alloc > 0 && !_experimentWaiting;
                    if (GUI.Button(new Rect(btnX, btnY, 22, 20), "-"))
                    {
                        _pointAllocation[i]--;
                    }
                    GUI.enabled = true;

                    GUI.Label(new Rect(btnX + 24, btnY, 24, 20), $"{alloc}", SmallLabelCentered());

                    int roundTotal = GetRoundAllocation();
                    GUI.enabled = roundTotal < 3 && _experimentPointsRemaining - roundTotal > 0
                        && !_experimentWaiting;
                    if (GUI.Button(new Rect(btnX + 52, btnY, 22, 20), "+"))
                    {
                        _pointAllocation[i]++;
                    }
                    GUI.enabled = true;

                    totalAllocated = GetRoundAllocation();
                }

                y += _experimentCategories.Count * 50 + 8;
            }

            // Risk warning
            if (totalAllocated > 1)
            {
                GUI.color = totalAllocated >= 3 ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.7f, 0.2f);
                string risk = totalAllocated >= 3 ? "HIGH RISK - High variance!" : "Moderate risk";
                GUI.Label(new Rect(x, y, leftW, 18), $"Allocating {totalAllocated} pts: {risk}", SmallLabel());
                GUI.color = Color.white;
                y += 20;
            }

            // Experiment / Finalize buttons
            bool canExperiment = totalAllocated > 0 && _experimentPointsRemaining > 0
                && _experimentRound < _experimentMaxRounds && !_experimentWaiting;
            GUI.enabled = canExperiment;
            if (GUI.Button(new Rect(x, y, 140, 30), _experimentWaiting ? "Experimenting..." : "Experiment"))
            {
                SendExperimentRequest();
            }
            GUI.enabled = true;

            GUI.enabled = !_experimentWaiting;
            if (GUI.Button(new Rect(x + 150, y, 140, 30), "Finalize Item"))
            {
                SendFinalizeRequest();
            }
            GUI.enabled = true;

            // Right: Experiment log
            GUI.Label(new Rect(rightX, _windowPos.y + 60, rightW, 20), "Experiment Log", BoldLabel());
            float logY = _windowPos.y + 82;
            float logH = h - 50;

            Rect logRect = new Rect(rightX, logY, rightW, logH);
            GUI.color = new Color(0.04f, 0.04f, 0.06f, 0.95f);
            GUI.DrawTexture(logRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _experimentLogScroll = GUI.BeginScrollView(logRect, _experimentLogScroll,
                new Rect(0, 0, rightW - 20, _experimentLog.Count * 18));

            for (int i = 0; i < _experimentLog.Count; i++)
            {
                string line = _experimentLog[i];
                GUI.color = line.Contains("[CRITICAL]") ? new Color(1f, 0.8f, 0.2f)
                    : line.Contains("[Success]") ? new Color(0.5f, 1f, 0.5f)
                    : line.Contains("[Failure]") ? new Color(1f, 0.4f, 0.4f)
                    : new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(4, i * 18, rightW - 24, 18), line, TinyLabel());
            }
            GUI.color = Color.white;

            GUI.EndScrollView();
        }

        // ─── Phase 3: Complete ───────────────────────────────────────────

        private void DrawCompletePhase(float x, float y, float w, float h)
        {
            float centerX = x + w * 0.5f - 160;

            GUI.Label(new Rect(x, y, w, 24), "Crafting Complete!", BoldLabelCentered());
            y += 30;

            if (_craftResult.ItemName != null)
            {
                // Item name
                GUI.color = new Color(1f, 0.85f, 0.2f);
                GUI.Label(new Rect(centerX, y, 320, 22), _craftResult.ItemName, BoldLabel());
                GUI.color = Color.white;
                y += 26;

                // Assembly tier
                GUI.color = GetTierColor(_craftResult.AssemblyTier);
                GUI.Label(new Rect(centerX, y, 320, 20),
                    $"Quality: {_craftResult.AssemblyTier}", SmallLabel());
                GUI.color = Color.white;
                y += 22;

                // Crafted by
                if (!string.IsNullOrEmpty(_craftResult.CraftedBy))
                {
                    GUI.color = new Color(0.6f, 0.6f, 0.8f);
                    GUI.Label(new Rect(centerX, y, 320, 18),
                        $"Crafted by: {_craftResult.CraftedBy}", SmallLabel());
                    GUI.color = Color.white;
                    y += 20;
                }

                // Condition
                GUI.Label(new Rect(centerX, y, 320, 18),
                    $"Condition: {_craftResult.Condition:P0}", SmallLabel());
                y += 24;

                // Stats
                if (_craftResult.Stats != null && _craftResult.Stats.Count > 0)
                {
                    GUI.Label(new Rect(centerX, y, 320, 20), "Item Stats:", BoldLabel());
                    y += 22;

                    foreach (var kvp in _craftResult.Stats)
                    {
                        GUI.color = new Color(0.5f, 1f, 0.5f);
                        GUI.Label(new Rect(centerX + 10, y, 310, 18),
                            $"{kvp.Key}: {kvp.Value:F1}", SmallLabel());
                        GUI.color = Color.white;
                        y += 18;
                    }
                }
            }

            y += 20;

            if (GUI.Button(new Rect(centerX, y, 160, 30), "Craft Another"))
            {
                _phase = CraftPhase.RecipeSelect;
                _selectedRecipe = -1;
            }

            if (GUI.Button(new Rect(centerX + 170, y, 120, 30), "Close"))
            {
                Hide();
            }
        }

        // ─── Network Requests ────────────────────────────────────────────

        private void RequestRecipeList()
        {
            // TODO: Send recipe list request to server when proto message exists.
            // For now, if recipes are empty, populate with placeholder data
            // that demonstrates the UI. Server will replace these via SetRecipes().
            if (_recipes.Count == 0)
            {
                PopulatePlaceholderRecipes();
            }
        }

        private void BeginAssembly(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= _recipes.Count) return;
            var recipe = _recipes[recipeIndex];

            _selectedResourceSlots = new int[recipe.ResourceSlots?.Length ?? 0];
            for (int i = 0; i < _selectedResourceSlots.Length; i++)
                _selectedResourceSlots[i] = -1;

            _activeSlotPicking = -1;
            _assemblyTier = "";
            _assemblyWaiting = false;
            _phase = CraftPhase.Assembly;

            // TODO: Request inventory resources from server (filtered by recipe requirements).
            // For now populate placeholders if empty.
            if (_inventoryResources.Count == 0)
            {
                PopulatePlaceholderResources();
            }
        }

        private void SendAssembleRequest()
        {
            if (_selectedRecipe < 0) return;
            var recipe = _recipes[_selectedRecipe];

            _assemblyWaiting = true;

            // Build resource selections for the assembly request
            var resourceSelections = new List<(uint slotIndex, ulong spawnId)>();
            if (_selectedResourceSlots != null)
            {
                for (int i = 0; i < _selectedResourceSlots.Length; i++)
                {
                    int resIdx = _selectedResourceSlots[i];
                    if (resIdx >= 0 && resIdx < _inventoryResources.Count)
                    {
                        var res = _inventoryResources[resIdx];
                        resourceSelections.Add((res.SlotIndex, res.SpawnId));
                    }
                }
            }

            // TODO: Send CraftAssembleRequest when proto message is added.
            // For now, send the existing simple CraftRequest as a fallback.
            var data = PacketBuilder.CraftRequest(recipe.RecipeId, (uint)recipe.Station);
            NetworkManager.Instance.Send(data);

            Debug.Log($"[CraftingUI] Sent assemble request for recipe {recipe.RecipeId} " +
                      $"with {resourceSelections.Count} resources");
        }

        private void SendExperimentRequest()
        {
            if (_pointAllocation == null) return;

            int totalPoints = GetRoundAllocation();
            if (totalPoints <= 0) return;

            _experimentWaiting = true;
            _experimentRound++;
            _experimentPointsRemaining -= totalPoints;

            // TODO: Send CraftExperimentRequest when proto message is added.
            // The request should include: recipe_id, round number, and per-category point allocation.
            // For now, log and simulate locally.
            Debug.Log($"[CraftingUI] Experiment round {_experimentRound}: " +
                      $"{totalPoints} points allocated, {_experimentPointsRemaining} remaining");

            // Reset allocation for next round
            var allocCopy = (int[])_pointAllocation.Clone();
            _pointAllocation = new int[_experimentCategories.Count];

            // TODO: Remove local simulation once server handles experimentation.
            // Simulate experiment result locally for UI testing.
            SimulateExperimentResult(allocCopy, totalPoints);
        }

        private void SendFinalizeRequest()
        {
            _experimentWaiting = true;

            // TODO: Send CraftFinalizeRequest when proto message is added.
            // For now, simulate finalization locally.
            Debug.Log("[CraftingUI] Sent finalize request");

            // TODO: Remove local simulation once server handles finalization.
            SimulateFinalizeResult();
        }

        // ─── Local simulation (remove when server wiring is complete) ────

        private void SimulateExperimentResult(int[] allocation, int totalPoints)
        {
            _experimentWaiting = false;

            for (int i = 0; i < allocation.Length; i++)
            {
                if (allocation[i] <= 0) continue;

                // More points = higher variance (risk/reward)
                float baseGain = allocation[i] * 5f;
                float variance = totalPoints > 1 ? (totalPoints - 1) * 8f : 0;
                float roll = Random.Range(-variance, variance + baseGain);
                bool success = roll > 0;
                bool critical = roll > baseGain * 1.5f;

                float gain = success ? (critical ? baseGain * 2f : Mathf.Max(1f, roll)) : roll * 0.5f;
                var cat = _experimentCategories[i];
                cat.CurrentValue = Mathf.Clamp(cat.CurrentValue + gain, 0, cat.MaxValue);
                _experimentCategories[i] = cat;

                string msg = success
                    ? (critical
                        ? $"{cat.Name} +{gain:F1} (critical success!)"
                        : $"{cat.Name} +{gain:F1}")
                    : $"{cat.Name} {gain:F1} (setback)";

                string prefix = critical ? "[CRITICAL] " : success ? "[Success] " : "[Failure] ";
                _experimentLog.Add($"R{_experimentRound}: {prefix}{msg}");
            }

            // Auto-scroll log to bottom
            _experimentLogScroll.y = float.MaxValue;
        }

        private void SimulateFinalizeResult()
        {
            _experimentWaiting = false;
            var stats = new Dictionary<string, float>();
            foreach (var cat in _experimentCategories)
            {
                stats[cat.Name] = cat.CurrentValue;
            }

            _craftResult = new CraftResultData
            {
                ItemName = _selectedRecipe >= 0 ? _recipes[_selectedRecipe].Name : "Unknown Item",
                CraftedBy = "You",
                AssemblyTier = _assemblyTier,
                Stats = stats,
                Condition = 1.0f
            };

            _phase = CraftPhase.Complete;
        }

        // ─── Placeholder data (used until server sends real data) ────────

        private void PopulatePlaceholderRecipes()
        {
            _recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    RecipeId = 1, Name = "Durasteel Blade", Station = 2,
                    Profession = "Weaponsmith", SkillRequired = 10,
                    Description = "A heavy melee blade forged from durasteel alloys.",
                    ResourceSlots = new[]
                    {
                        new ResourceSlot { SlotName = "Primary Metal", RequiredClass = 110,
                            RequiredClassName = "Ferrous Metal",
                            AttributeWeights = new[] { "TN", "DN", "ML" } },
                        new ResourceSlot { SlotName = "Edge Metal", RequiredClass = 120,
                            RequiredClassName = "Non-Ferrous Metal",
                            AttributeWeights = new[] { "CN", "PR" } },
                        new ResourceSlot { SlotName = "Binding Agent", RequiredClass = 310,
                            RequiredClassName = "Flora",
                            AttributeWeights = new[] { "FL", "DC" } },
                    },
                    OutputStats = new[] { "Damage", "Speed", "Durability" }
                },
                new RecipeData
                {
                    RecipeId = 2, Name = "Composite Armor", Station = 2,
                    Profession = "Armorsmith", SkillRequired = 15,
                    Description = "Layered composite plating for torso protection.",
                    ResourceSlots = new[]
                    {
                        new ResourceSlot { SlotName = "Outer Shell", RequiredClass = 110,
                            RequiredClassName = "Ferrous Metal",
                            AttributeWeights = new[] { "TN", "TH", "DN" } },
                        new ResourceSlot { SlotName = "Inner Padding", RequiredClass = 310,
                            RequiredClassName = "Flora",
                            AttributeWeights = new[] { "FL", "TH" } },
                    },
                    OutputStats = new[] { "Armor", "Weight", "Durability" }
                },
                new RecipeData
                {
                    RecipeId = 3, Name = "Stim Pack", Station = 3,
                    Profession = "Medic", SkillRequired = 5,
                    Description = "An injectable stimulant that restores vitality.",
                    ResourceSlots = new[]
                    {
                        new ResourceSlot { SlotName = "Active Compound", RequiredClass = 410,
                            RequiredClassName = "Chemical",
                            AttributeWeights = new[] { "RE", "PR" } },
                        new ResourceSlot { SlotName = "Bio-Substrate", RequiredClass = 320,
                            RequiredClassName = "Fauna",
                            AttributeWeights = new[] { "PR", "DC" } },
                    },
                    OutputStats = new[] { "Potency", "Duration", "Side Effects" }
                },
                new RecipeData
                {
                    RecipeId = 4, Name = "Frontier Rations", Station = 4,
                    Profession = "Chef", SkillRequired = 1,
                    Description = "Hearty field rations that restore stamina over time.",
                    ResourceSlots = new[]
                    {
                        new ResourceSlot { SlotName = "Protein", RequiredClass = 320,
                            RequiredClassName = "Fauna",
                            AttributeWeights = new[] { "PR", "FL" } },
                        new ResourceSlot { SlotName = "Seasoning", RequiredClass = 310,
                            RequiredClassName = "Flora",
                            AttributeWeights = new[] { "RE", "HR" } },
                    },
                    OutputStats = new[] { "Healing", "Duration", "Flavor" }
                },
                new RecipeData
                {
                    RecipeId = 5, Name = "Plasma Rifle Core", Station = 1,
                    Profession = "Weaponsmith", SkillRequired = 30,
                    Description = "The energy core of a plasma rifle. Requires exceptional materials.",
                    ResourceSlots = new[]
                    {
                        new ResourceSlot { SlotName = "Conductor", RequiredClass = 120,
                            RequiredClassName = "Non-Ferrous Metal",
                            AttributeWeights = new[] { "CN", "TH" } },
                        new ResourceSlot { SlotName = "Resonator Crystal", RequiredClass = 210,
                            RequiredClassName = "Crystal",
                            AttributeWeights = new[] { "RS", "HR", "PR" } },
                        new ResourceSlot { SlotName = "Plasma Cell", RequiredClass = 420,
                            RequiredClassName = "Plasma",
                            AttributeWeights = new[] { "RE", "CN" } },
                        new ResourceSlot { SlotName = "Housing", RequiredClass = 110,
                            RequiredClassName = "Ferrous Metal",
                            AttributeWeights = new[] { "TN", "TH", "DC" } },
                    },
                    OutputStats = new[] { "Damage", "Range", "Heat Dissipation", "Durability" }
                },
            };
        }

        private void PopulatePlaceholderResources()
        {
            _inventoryResources = new List<ResourceEntry>
            {
                new ResourceEntry { SlotIndex = 0, ItemId = 100, Name = "Ferrous Ore Sample",
                    SpawnId = 1001, ResourceClass = 110, Quantity = 8,
                    Attributes = new uint[] { 450, 600, 780, 520, 200, 700, 650, 300, 550, 400, 250 } },
                new ResourceEntry { SlotIndex = 1, ItemId = 101, Name = "Copper Nodule",
                    SpawnId = 1002, ResourceClass = 120, Quantity = 5,
                    Attributes = new uint[] { 850, 350, 400, 700, 300, 400, 800, 200, 600, 500, 300 } },
                new ResourceEntry { SlotIndex = 2, ItemId = 102, Name = "Wild Vine",
                    SpawnId = 1003, ResourceClass = 310, Quantity = 12,
                    Attributes = new uint[] { 100, 200, 300, 200, 400, 150, 500, 350, 700, 900, 450 } },
                new ResourceEntry { SlotIndex = 3, ItemId = 103, Name = "Creature Hide",
                    SpawnId = 1004, ResourceClass = 320, Quantity = 6,
                    Attributes = new uint[] { 150, 500, 600, 300, 250, 400, 700, 150, 800, 600, 200 } },
                new ResourceEntry { SlotIndex = 4, ItemId = 104, Name = "Reactive Compound",
                    SpawnId = 1005, ResourceClass = 410, Quantity = 3,
                    Attributes = new uint[] { 300, 200, 150, 100, 950, 200, 850, 400, 300, 200, 500 } },
                new ResourceEntry { SlotIndex = 5, ItemId = 105, Name = "Quartz Crystal",
                    SpawnId = 1006, ResourceClass = 210, Quantity = 4,
                    Attributes = new uint[] { 200, 700, 500, 100, 300, 600, 900, 800, 400, 150, 750 } },
                new ResourceEntry { SlotIndex = 6, ItemId = 106, Name = "Plasma Residue",
                    SpawnId = 1007, ResourceClass = 420, Quantity = 2,
                    Attributes = new uint[] { 700, 300, 200, 150, 800, 100, 600, 500, 200, 100, 600 } },
            };
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private bool AllSlotsFilled()
        {
            if (_selectedResourceSlots == null) return false;
            foreach (int s in _selectedResourceSlots)
                if (s < 0) return false;
            return _selectedResourceSlots.Length > 0;
        }

        private int GetRoundAllocation()
        {
            if (_pointAllocation == null) return 0;
            int total = 0;
            foreach (int p in _pointAllocation) total += p;
            return total;
        }

        private string GetStationName(int station)
        {
            return station switch
            {
                1 => "Workbench",
                2 => "Forge",
                3 => "Lab",
                4 => "Kitchen",
                _ => "Field"
            };
        }

        private Color GetTierColor(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return Color.white;
            return tier.ToLower() switch
            {
                "amazing" => new Color(1f, 0.85f, 0.1f),
                "great" => new Color(0.3f, 0.9f, 0.3f),
                "good" => new Color(0.4f, 0.7f, 1f),
                "decent" => Color.white,
                "barely" => new Color(0.6f, 0.4f, 0.4f),
                _ => Color.white
            };
        }

        // ─── GUI Styles ─────────────────────────────────────────────────

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

        private GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label)
                { fontSize = 11, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label)
                { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private GUIStyle TinyLabel()
        {
            return new GUIStyle(GUI.skin.label)
                { fontSize = 9, normal = { textColor = Color.white }, wordWrap = false };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label)
                { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }

        private GUIStyle BoldLabelCentered()
        {
            return new GUIStyle(GUI.skin.label)
                { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                  normal = { textColor = Color.white } };
        }
    }
}

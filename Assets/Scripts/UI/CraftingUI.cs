using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Crafting window with recipe list, material requirements, progress bar, and station filter.
    /// Uses OnGUI for rapid prototyping — will be replaced with proper UI later.
    /// </summary>
    public class CraftingUI : MonoBehaviour
    {
        private bool _visible;
        private Vector2 _windowPos = new Vector2(250, 120);
        private bool _dragging;
        private Vector2 _dragOffset;

        private Vector2 _recipeScroll;
        private int _selectedRecipe = -1;
        private int _selectedStation;
        private bool _crafting;
        private float _craftProgress;
        private float _craftDuration = 3f;

        private static readonly string[] Stations = { "All", "Workbench", "Forge", "Alchemy", "Campfire" };

        private struct Material
        {
            public string Name;
            public int Required;
            public int Owned;
        }

        private struct Recipe
        {
            public string Name;
            public string Station;
            public Material[] Materials;
            public string ResultDescription;
        }

        private List<Recipe> _recipes;

        private void Awake()
        {
            _recipes = new List<Recipe>
            {
                new Recipe
                {
                    Name = "Iron Sword",
                    Station = "Forge",
                    Materials = new[] {
                        new Material { Name = "Iron Ingot", Required = 3, Owned = 5 },
                        new Material { Name = "Leather Wrap", Required = 1, Owned = 2 }
                    },
                    ResultDescription = "A sturdy iron blade. 15-20 damage."
                },
                new Recipe
                {
                    Name = "Health Potion",
                    Station = "Alchemy",
                    Materials = new[] {
                        new Material { Name = "Red Herb", Required = 2, Owned = 4 },
                        new Material { Name = "Water Flask", Required = 1, Owned = 1 }
                    },
                    ResultDescription = "Restores 50 HP over 5 seconds."
                },
                new Recipe
                {
                    Name = "Campfire",
                    Station = "Workbench",
                    Materials = new[] {
                        new Material { Name = "Wood", Required = 5, Owned = 12 },
                        new Material { Name = "Flint", Required = 1, Owned = 0 }
                    },
                    ResultDescription = "A campfire for cooking and warmth."
                },
                new Recipe
                {
                    Name = "Cooked Meat",
                    Station = "Campfire",
                    Materials = new[] {
                        new Material { Name = "Raw Meat", Required = 1, Owned = 3 }
                    },
                    ResultDescription = "Restores 30 hunger. Tastes okay."
                },
                new Recipe
                {
                    Name = "Iron Shield",
                    Station = "Forge",
                    Materials = new[] {
                        new Material { Name = "Iron Ingot", Required = 4, Owned = 5 },
                        new Material { Name = "Wood", Required = 2, Owned = 12 }
                    },
                    ResultDescription = "Blocks 15% incoming damage."
                },
                new Recipe
                {
                    Name = "Mana Potion",
                    Station = "Alchemy",
                    Materials = new[] {
                        new Material { Name = "Blue Herb", Required = 2, Owned = 1 },
                        new Material { Name = "Water Flask", Required = 1, Owned = 1 }
                    },
                    ResultDescription = "Restores 40 MP over 5 seconds."
                }
            };
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                _visible = !_visible;
            }

            if (_crafting)
            {
                _craftProgress += Time.deltaTime / _craftDuration;
                if (_craftProgress >= 1f)
                {
                    _crafting = false;
                    _craftProgress = 0f;
                    Debug.Log($"[CraftingUI] Crafted: {_recipes[_selectedRecipe].Name}");
                }
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            float windowW = 480, windowH = 360;
            Rect windowRect = new Rect(_windowPos.x, _windowPos.y, windowW, windowH);

            // Background
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            Rect titleBar = new Rect(_windowPos.x, _windowPos.y, windowW - 24, 24);
            DrawTitleBar(titleBar, "Crafting");
            HandleDrag(titleBar);

            if (GUI.Button(new Rect(_windowPos.x + windowW - 24, _windowPos.y, 24, 24), "X"))
            {
                _visible = false;
                return;
            }

            float cx = _windowPos.x + 8;
            float cy = _windowPos.y + 30;

            // Station filter dropdown
            GUI.Label(new Rect(cx, cy, 50, 20), "Station:", SmallLabel());
            for (int i = 0; i < Stations.Length; i++)
            {
                bool selected = i == _selectedStation;
                GUI.color = selected ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
                if (GUI.Button(new Rect(cx + 54 + i * 72, cy, 68, 20), Stations[i]))
                {
                    _selectedStation = i;
                    _selectedRecipe = -1;
                }
            }
            GUI.color = Color.white;
            cy += 26;

            // Recipe list (left)
            float listW = 160, listH = windowH - 70;
            Rect listArea = new Rect(cx, cy, listW, listH);
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            GUI.DrawTexture(listArea, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _recipeScroll = GUI.BeginScrollView(listArea, _recipeScroll, new Rect(0, 0, listW - 20, _recipes.Count * 26));
            int visIdx = 0;
            for (int i = 0; i < _recipes.Count; i++)
            {
                if (_selectedStation > 0 && _recipes[i].Station != Stations[_selectedStation])
                    continue;

                Rect btn = new Rect(2, visIdx * 26, listW - 24, 24);
                bool isSel = i == _selectedRecipe;
                GUI.color = isSel ? new Color(0.2f, 0.4f, 0.7f, 0.8f) : new Color(0.15f, 0.15f, 0.15f, 0.8f);
                GUI.DrawTexture(btn, Texture2D.whiteTexture);
                GUI.color = CanCraft(i) ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                if (GUI.Button(btn, _recipes[i].Name, SmallLabel()))
                    _selectedRecipe = i;

                visIdx++;
            }
            GUI.EndScrollView();
            GUI.color = Color.white;

            // Detail panel (right)
            float detailX = cx + listW + 8;
            float detailW = windowW - listW - 24;
            float detailY = cy;

            if (_selectedRecipe >= 0 && _selectedRecipe < _recipes.Count)
            {
                Recipe r = _recipes[_selectedRecipe];

                // Recipe name
                GUI.Label(new Rect(detailX, detailY, detailW, 22), r.Name, BoldLabel());
                detailY += 24;

                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(detailX, detailY, detailW, 18), $"Station: {r.Station}", SmallLabel());
                detailY += 20;

                GUI.color = Color.white;
                GUI.Label(new Rect(detailX, detailY, detailW, 36), r.ResultDescription, SmallLabel());
                detailY += 40;

                // Materials
                GUI.Label(new Rect(detailX, detailY, detailW, 20), "Materials:", BoldLabel());
                detailY += 22;

                foreach (var mat in r.Materials)
                {
                    bool has = mat.Owned >= mat.Required;
                    GUI.color = has ? Color.green : Color.red;
                    GUI.Label(new Rect(detailX + 8, detailY, detailW - 8, 18),
                        $"{mat.Name}: {mat.Owned}/{mat.Required}", SmallLabel());
                    detailY += 18;
                }
                GUI.color = Color.white;

                detailY += 12;

                // Craft button
                bool canCraft = CanCraft(_selectedRecipe) && !_crafting;
                GUI.enabled = canCraft;
                if (GUI.Button(new Rect(detailX, detailY, 100, 28), "Craft"))
                {
                    _crafting = true;
                    _craftProgress = 0f;
                    Debug.Log($"[CraftingUI] Crafting {r.Name}...");
                }
                GUI.enabled = true;

                // Progress bar
                if (_crafting)
                {
                    detailY += 34;
                    Rect barBg = new Rect(detailX, detailY, detailW - 8, 16);
                    GUI.color = new Color(0.1f, 0.1f, 0.1f);
                    GUI.DrawTexture(barBg, Texture2D.whiteTexture);
                    GUI.color = new Color(0.2f, 0.7f, 0.3f);
                    GUI.DrawTexture(new Rect(detailX, detailY, (detailW - 8) * _craftProgress, 16), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(barBg, $"{(_craftProgress * 100):F0}%", SmallLabelCentered());
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(new Rect(detailX, detailY + 40, detailW, 20), "Select a recipe", SmallLabelCentered());
                GUI.color = Color.white;
            }
        }

        private bool CanCraft(int idx)
        {
            if (idx < 0 || idx >= _recipes.Count) return false;
            foreach (var m in _recipes[idx].Materials)
                if (m.Owned < m.Required) return false;
            return true;
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

        private GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white }, wordWrap = true };
        }

        private GUIStyle SmallLabelCentered()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        }
    }
}

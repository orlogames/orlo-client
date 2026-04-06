using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Orlo.Proto.Admin;

namespace Orlo.UI
{
    public class CreatureBrowserUI : MonoBehaviour
    {
        private bool _visible = false;
        private Vector2 _scrollPos;
        private string _selectedCategory = "All";
        private List<CreatureInfo> _creatures = new List<CreatureInfo>();
        private string _searchText = "";

        private static readonly string[] Categories = { "All", "wildlife", "hostile", "boss", "ambient" };

        public void SetCreatureList(AdminCreatureList list)
        {
            _creatures.Clear();
            foreach (var c in list.Creatures)
                _creatures.Add(c);
            _visible = true;
        }

        public void Toggle() => _visible = !_visible;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && _visible)
                _visible = false;
        }

        private void OnGUI()
        {
            if (!_visible || _creatures.Count == 0) return;

            float w = 450, h = 500;
            float x = (Screen.width - w) / 2;
            float y = (Screen.height - h) / 2;

            // Background
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Title bar
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(x, y, w, 28), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(x, y, w - 30, 28), "Creature Browser", titleStyle);

            // Close button
            if (GUI.Button(new Rect(x + w - 28, y + 2, 24, 24), "X"))
                _visible = false;

            float cy = y + 32;

            // Category tabs
            float tabW = w / Categories.Length;
            for (int i = 0; i < Categories.Length; i++)
            {
                bool selected = _selectedCategory == Categories[i];
                GUI.color = selected ? new Color(0.3f, 0.6f, 1f, 0.8f) : new Color(0.2f, 0.2f, 0.25f, 0.8f);
                GUI.DrawTexture(new Rect(x + i * tabW, cy, tabW - 1, 22), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var tabStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
                if (GUI.Button(new Rect(x + i * tabW, cy, tabW - 1, 22),
                    Categories[i] == "All" ? "All" : char.ToUpper(Categories[i][0]) + Categories[i].Substring(1), tabStyle))
                    _selectedCategory = Categories[i];
            }
            cy += 26;

            // Search
            _searchText = GUI.TextField(new Rect(x + 4, cy, w - 8, 20), _searchText,
                new GUIStyle(GUI.skin.textField) { fontSize = 11 });
            cy += 24;

            // Filtered list
            var filtered = _creatures.Where(c =>
                (_selectedCategory == "All" || c.Category == _selectedCategory) &&
                (string.IsNullOrEmpty(_searchText) ||
                 c.DisplayName.ToLower().Contains(_searchText.ToLower()) ||
                 c.CreatureType.ToLower().Contains(_searchText.ToLower()))
            ).ToList();

            // Scroll area
            float listH = h - (cy - y) - 8;
            float itemH = 36;
            float totalH = filtered.Count * itemH;

            _scrollPos = GUI.BeginScrollView(new Rect(x + 2, cy, w - 4, listH), _scrollPos,
                new Rect(0, 0, w - 24, Mathf.Max(totalH, listH)));

            for (int i = 0; i < filtered.Count; i++)
            {
                var c = filtered[i];
                float iy = i * itemH;

                // Row background (alternating)
                GUI.color = i % 2 == 0 ? new Color(0.1f, 0.1f, 0.13f, 0.7f) : new Color(0.08f, 0.08f, 0.1f, 0.7f);
                GUI.DrawTexture(new Rect(0, iy, w - 24, itemH - 2), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Category color indicator
                Color catColor = c.Category switch
                {
                    "wildlife" => Color.green,
                    "hostile" => new Color(1f, 0.4f, 0.3f),
                    "boss" => new Color(1f, 0.8f, 0.2f),
                    "ambient" => new Color(0.5f, 0.8f, 1f),
                    _ => Color.white
                };
                GUI.color = catColor;
                GUI.DrawTexture(new Rect(2, iy + 2, 4, itemH - 6), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // Name
                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(12, iy + 2, 250, 18), c.DisplayName, nameStyle);

                // Level range
                var levelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.gray } };
                string levelText = c.MinLevel == 0 ? "" : c.MinLevel == c.MaxLevel ? $"Lv.{c.MinLevel}" : $"Lv.{c.MinLevel}-{c.MaxLevel}";
                GUI.Label(new Rect(12, iy + 18, 100, 14), levelText, levelStyle);

                // Type ID
                var typeStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
                GUI.Label(new Rect(120, iy + 18, 200, 14), c.CreatureType, typeStyle);

                // Spawn button
                if (GUI.Button(new Rect(w - 100, iy + 6, 70, 22), "Spawn"))
                {
                    SpawnCreature(c.CreatureType);
                }
            }

            GUI.EndScrollView();
        }

        private void SpawnCreature(string creatureType)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            var pos = player.transform.position;
            var data = Network.PacketBuilder.AdminSpawnCreature(creatureType, pos.x, pos.y, pos.z + 5f);
            Network.NetworkManager.Instance?.Send(data);

            var chatUI = FindFirstObjectByType<ChatUI>();
            chatUI?.AddSystemMessage($"Spawning {creatureType}...");
        }
    }
}

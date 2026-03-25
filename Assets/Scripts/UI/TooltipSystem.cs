using UnityEngine;
using System.Collections.Generic;

namespace Orlo.UI
{
    /// <summary>
    /// Tooltip system — shows contextual tooltips on hover.
    /// Supports item tooltips, skill tooltips, and general text tooltips.
    /// </summary>
    public class TooltipSystem : MonoBehaviour
    {
        private static TooltipSystem instance;
        public static TooltipSystem Instance => instance;

        private bool visible = false;
        private string title = "";
        private string body = "";
        private string footer = "";
        private Color titleColor = Color.white;
        private Rect tooltipRect;
        private float showDelay = 0.3f;
        private float hoverTimer = 0f;
        private bool hovering = false;

        // Item tooltip data
        private bool isItemTooltip = false;
        private string itemRarity = "";
        private List<string> itemStats = new List<string>();

        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle footerStyle;
        private GUIStyle rarityStyle;
        private bool stylesInitialized = false;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        /// <summary>
        /// Show a simple text tooltip at cursor
        /// </summary>
        public void ShowText(string tipTitle, string tipBody, string tipFooter = "")
        {
            title = tipTitle;
            body = tipBody;
            footer = tipFooter;
            titleColor = Color.white;
            isItemTooltip = false;
            hovering = true;
            hoverTimer = 0f;
        }

        /// <summary>
        /// Show an item tooltip with rarity and stat lines
        /// </summary>
        public void ShowItem(string itemName, string rarity, List<string> stats,
                              string description, string requirements = "")
        {
            title = itemName;
            itemRarity = rarity;
            itemStats = stats ?? new List<string>();
            body = description;
            footer = requirements;
            isItemTooltip = true;
            hovering = true;
            hoverTimer = 0f;

            titleColor = rarity switch
            {
                "Common" => new Color(0.8f, 0.8f, 0.8f),
                "Uncommon" => new Color(0.2f, 0.9f, 0.2f),
                "Rare" => new Color(0.3f, 0.5f, 1f),
                "Epic" => new Color(0.7f, 0.3f, 0.9f),
                "Legendary" => new Color(1f, 0.6f, 0f),
                _ => Color.white
            };
        }

        /// <summary>
        /// Show a skill tooltip
        /// </summary>
        public void ShowSkill(string skillName, int rank, int maxRank,
                               string description, string nextRankEffect, string prereqs)
        {
            title = $"{skillName} (Rank {rank}/{maxRank})";
            titleColor = new Color(0.4f, 0.8f, 1f);
            body = description;
            if (!string.IsNullOrEmpty(nextRankEffect) && rank < maxRank)
                body += $"\n\nNext Rank: {nextRankEffect}";
            footer = prereqs;
            isItemTooltip = false;
            itemStats.Clear();
            hovering = true;
            hoverTimer = 0f;
        }

        public void Hide()
        {
            hovering = false;
            visible = false;
            hoverTimer = 0f;
        }

        private void Update()
        {
            if (hovering)
            {
                hoverTimer += Time.deltaTime;
                if (hoverTimer >= showDelay)
                    visible = true;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(1, 1, new Color(0.08f, 0.08f, 0.12f, 0.95f));
            boxStyle.padding = new RectOffset(10, 10, 8, 8);
            boxStyle.border = new RectOffset(1, 1, 1, 1);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;
            titleStyle.wordWrap = true;

            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.fontSize = 12;
            bodyStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            bodyStyle.wordWrap = true;

            footerStyle = new GUIStyle(GUI.skin.label);
            footerStyle.fontSize = 11;
            footerStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            footerStyle.fontStyle = FontStyle.Italic;
            footerStyle.wordWrap = true;

            rarityStyle = new GUIStyle(GUI.skin.label);
            rarityStyle.fontSize = 11;
            rarityStyle.fontStyle = FontStyle.Italic;
        }

        private void OnGUI()
        {
            if (!visible) return;
            InitStyles();

            float width = 260f;
            float x = Input.mousePosition.x + 16f;
            float y = Screen.height - Input.mousePosition.y + 16f;

            // Calculate height
            float height = 16f; // padding
            titleStyle.normal.textColor = titleColor;
            height += titleStyle.CalcHeight(new GUIContent(title), width - 20f);

            if (isItemTooltip && !string.IsNullOrEmpty(itemRarity))
            {
                height += 18f;
            }

            if (isItemTooltip && itemStats.Count > 0)
            {
                height += 6f; // separator
                foreach (var stat in itemStats)
                    height += bodyStyle.CalcHeight(new GUIContent(stat), width - 20f);
                height += 6f;
            }

            if (!string.IsNullOrEmpty(body))
            {
                height += 6f;
                height += bodyStyle.CalcHeight(new GUIContent(body), width - 20f);
            }

            if (!string.IsNullOrEmpty(footer))
            {
                height += 8f;
                height += footerStyle.CalcHeight(new GUIContent(footer), width - 20f);
            }

            height += 8f;

            // Keep on screen
            if (x + width > Screen.width) x = Screen.width - width - 4f;
            if (y + height > Screen.height) y = Screen.height - height - 4f;

            tooltipRect = new Rect(x, y, width, height);
            GUI.Box(tooltipRect, "", boxStyle);

            float cy = y + 8f;

            // Title
            GUI.Label(new Rect(x + 10, cy, width - 20, 30), title, titleStyle);
            cy += titleStyle.CalcHeight(new GUIContent(title), width - 20f);

            // Rarity
            if (isItemTooltip && !string.IsNullOrEmpty(itemRarity))
            {
                rarityStyle.normal.textColor = titleColor;
                GUI.Label(new Rect(x + 10, cy, width - 20, 18), itemRarity, rarityStyle);
                cy += 18f;
            }

            // Stat lines
            if (isItemTooltip && itemStats.Count > 0)
            {
                cy += 3f;
                DrawSeparator(x + 10, cy, width - 20);
                cy += 3f;
                foreach (var stat in itemStats)
                {
                    float h = bodyStyle.CalcHeight(new GUIContent(stat), width - 20f);
                    var oldColor = bodyStyle.normal.textColor;
                    if (stat.StartsWith("+")) bodyStyle.normal.textColor = new Color(0.3f, 0.9f, 0.3f);
                    else if (stat.StartsWith("-")) bodyStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                    GUI.Label(new Rect(x + 10, cy, width - 20, h), stat, bodyStyle);
                    bodyStyle.normal.textColor = oldColor;
                    cy += h;
                }
            }

            // Body
            if (!string.IsNullOrEmpty(body))
            {
                cy += 6f;
                float h = bodyStyle.CalcHeight(new GUIContent(body), width - 20f);
                GUI.Label(new Rect(x + 10, cy, width - 20, h), body, bodyStyle);
                cy += h;
            }

            // Footer
            if (!string.IsNullOrEmpty(footer))
            {
                cy += 4f;
                DrawSeparator(x + 10, cy, width - 20);
                cy += 4f;
                float h = footerStyle.CalcHeight(new GUIContent(footer), width - 20f);
                GUI.Label(new Rect(x + 10, cy, width - 20, h), footer, footerStyle);
            }
        }

        private void DrawSeparator(float x, float y, float width)
        {
            var old = GUI.color;
            GUI.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, width, 1), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w * h; i++) tex.SetPixel(i % w, i / w, col);
            tex.Apply();
            return tex;
        }
    }
}

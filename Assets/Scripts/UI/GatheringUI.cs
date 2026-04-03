using System.Collections.Generic;
using UnityEngine;
using Orlo.World;

namespace Orlo.UI
{
    /// <summary>
    /// Gathering progress bar and quality breakdown display.
    /// Shows a center-screen progress bar while harvesting, then a
    /// quality breakdown panel on completion.
    /// </summary>
    public class GatheringUI : MonoBehaviour
    {
        public static GatheringUI Instance { get; private set; }

        // ─── State ──────────────────────────────────────────────────────────

        private bool _isGathering;
        private float _progress;      // 0..1
        private float _totalTime;
        private string _resourceName;
        private int _resourceClass;

        // Completion result
        private bool _showResult;
        private float _resultTimer;
        private const float ResultDuration = 6f;
        private string _resultName;
        private string _resultTier;
        private int _resultClass;
        private uint[] _resultAttributes; // 11 values
        private uint _resultQuantity;
        private uint _nodeRemaining;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─── Public API ─────────────────────────────────────────────────────

        /// <summary>Show gathering progress bar.</summary>
        public void ShowGathering(string resourceName, int resourceClass, float progress, float totalTime)
        {
            _isGathering = true;
            _resourceName = resourceName;
            _resourceClass = resourceClass;
            _progress = Mathf.Clamp01(progress);
            _totalTime = totalTime;
        }

        /// <summary>Update progress while gathering.</summary>
        public void UpdateProgress(float progress, float totalTime)
        {
            _progress = Mathf.Clamp01(progress);
            _totalTime = totalTime;
        }

        /// <summary>Hide gathering progress bar.</summary>
        public void HideGathering()
        {
            _isGathering = false;
        }

        /// <summary>Show the quality breakdown after gathering completes.</summary>
        public void ShowResult(string name, string qualityTier, int resourceClass,
            uint[] attributes, uint quantity, uint nodeRemaining)
        {
            _isGathering = false;
            _showResult = true;
            _resultTimer = ResultDuration;
            _resultName = name;
            _resultTier = qualityTier;
            _resultClass = resourceClass;
            _resultAttributes = attributes;
            _resultQuantity = quantity;
            _nodeRemaining = nodeRemaining;
        }

        /// <summary>Dismiss the result panel early.</summary>
        public void DismissResult()
        {
            _showResult = false;
        }

        // ─── Update ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (_showResult)
            {
                _resultTimer -= Time.deltaTime;
                if (_resultTimer <= 0f)
                    _showResult = false;

                // Dismiss on any click
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape))
                    _showResult = false;
            }
        }

        // ─── OnGUI ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_isGathering)
                DrawGatheringBar();

            if (_showResult)
                DrawResultPanel();
        }

        // ─── Gathering progress bar (center screen) ─────────────────────────

        private void DrawGatheringBar()
        {
            const float barW = 300f;
            const float barH = 20f;
            float x = (Screen.width - barW) / 2f;
            float y = Screen.height * 0.65f;

            Color typeColor = ResourceNode.GetColorForClass(_resourceClass);

            // Background
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x - 2, y - 22, barW + 4, barH + 26), Texture2D.whiteTexture);

            // Resource name
            GUI.color = typeColor;
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = typeColor }
            };
            GUI.Label(new Rect(x, y - 20, barW, 18), $"Gathering {_resourceName}", nameStyle);

            // Bar background
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, barW, barH), Texture2D.whiteTexture);

            // Bar fill
            Color barColor = Color.Lerp(new Color(0.3f, 0.6f, 1f), new Color(0.2f, 1f, 0.4f), _progress);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x, y, barW * _progress, barH), Texture2D.whiteTexture);

            // Progress text
            GUI.color = Color.white;
            var progStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            float remaining = _totalTime * (1f - _progress);
            GUI.Label(new Rect(x, y, barW, barH), $"{_progress * 100:F0}%  ({remaining:F1}s)", progStyle);
        }

        // ─── Result panel (center-right) ────────────────────────────────────

        private void DrawResultPanel()
        {
            const float panelW = 260f;
            const float lineH = 16f;
            float panelH = lineH * 16 + 40f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            float alpha = Mathf.Min(1f, _resultTimer / 0.5f); // fade in/out
            Color typeColor = ResourceNode.GetColorForClass(_resultClass);

            // Background
            GUI.color = new Color(0.03f, 0.03f, 0.08f, 0.94f * alpha);
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), Texture2D.whiteTexture);

            // Top color accent
            GUI.color = new Color(typeColor.r, typeColor.g, typeColor.b, alpha);
            GUI.DrawTexture(new Rect(x, y, panelW, 3f), Texture2D.whiteTexture);

            float cy = y + 8;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 1f, 0.4f, alpha) }
            };
            GUI.Label(new Rect(x, cy, panelW, lineH + 4), "GATHERED", titleStyle);
            cy += lineH + 8;

            // Resource name
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(typeColor.r, typeColor.g, typeColor.b, alpha) }
            };
            GUI.Label(new Rect(x, cy, panelW, lineH), _resultName ?? "Resource", nameStyle);
            cy += lineH + 2;

            // Quantity + quality
            var infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, alpha) }
            };
            GUI.Label(new Rect(x, cy, panelW, lineH),
                $"x{_resultQuantity}  |  {_resultTier}  |  {ResourceNode.GetClassLabel(_resultClass)}",
                infoStyle);
            cy += lineH;

            // Node remaining
            var remStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, alpha) }
            };
            string remText = _nodeRemaining > 0 ? $"Node remaining: {_nodeRemaining}" : "Node depleted";
            GUI.Label(new Rect(x, cy, panelW, lineH), remText, remStyle);
            cy += lineH + 8;

            // Attribute breakdown
            if (_resultAttributes != null && _resultAttributes.Length == 11)
            {
                // Section header
                var secStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f, alpha) }
                };
                GUI.Label(new Rect(x, cy, panelW, lineH), "QUALITY ATTRIBUTES", secStyle);
                cy += lineH + 2;

                for (int a = 0; a < 11; a++)
                {
                    DrawAttributeBar(x + 12, cy, panelW - 24, 12f,
                        ResourceNode.AttributeShort[a],
                        _resultAttributes[a], alpha);
                    cy += lineH;
                }
            }

            GUI.color = Color.white;
        }

        private void DrawAttributeBar(float x, float y, float w, float h,
            string shortName, uint value, float alpha)
        {
            float labelW = 28f;
            float valueW = 36f;
            float barX = x + labelW + 4;
            float barW = w - labelW - valueW - 8;
            float fill = value / 1000f;

            // Label
            GUI.color = new Color(0.6f, 0.6f, 0.6f, alpha);
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f, alpha) }
            };
            GUI.Label(new Rect(x, y, labelW, h), shortName, labelStyle);

            // Bar bg
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.7f * alpha);
            GUI.DrawTexture(new Rect(barX, y + 1, barW, h - 2), Texture2D.whiteTexture);

            // Bar fill
            Color barColor;
            if (fill >= 0.8f) barColor = new Color(0.2f, 0.9f, 0.3f, alpha);
            else if (fill >= 0.5f) barColor = new Color(0.3f, 0.7f, 1f, alpha);
            else if (fill >= 0.25f) barColor = new Color(0.9f, 0.8f, 0.2f, alpha);
            else barColor = new Color(0.8f, 0.3f, 0.2f, alpha);

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barX, y + 1, barW * fill, h - 2), Texture2D.whiteTexture);

            // Value
            GUI.color = new Color(barColor.r, barColor.g, barColor.b, alpha);
            var valStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(barColor.r, barColor.g, barColor.b, alpha) }
            };
            GUI.Label(new Rect(barX + barW + 2, y, valueW, h), $"{value}", valStyle);
        }
    }
}

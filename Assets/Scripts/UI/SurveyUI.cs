using System.Collections.Generic;
using UnityEngine;
using Orlo.World;

namespace Orlo.UI
{
    /// <summary>
    /// Survey mode UI — compass-style directional indicator for nearby resource spawns.
    /// Activated when the player uses the TMD scanner. Shows direction, distance, type,
    /// and quality preview for detected resources.
    /// </summary>
    public class SurveyUI : MonoBehaviour
    {
        public static SurveyUI Instance { get; private set; }

        // ─── Data ───────────────────────────────────────────────────────────

        public struct SurveyEntry
        {
            public ulong SpawnId;
            public string TypeId;
            public string Name;
            public int ResourceClass;
            public string QualityTier;
            public Vector3 Position;
            public float Radius;
            public uint[] Attributes; // 11 values, 0-1000
        }

        private readonly List<SurveyEntry> _entries = new();
        private bool _surveyActive;
        private float _surveyTimer; // auto-dismiss after timeout
        private const float SurveyTimeout = 30f;

        // ─── Hover state ────────────────────────────────────────────────────
        private int _hoveredIndex = -1;
        private bool _showDetail;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─── Public API ─────────────────────────────────────────────────────

        /// <summary>Activate survey mode and send a scan request.</summary>
        public void StartSurvey(Vector3 position, float range)
        {
            _surveyActive = true;
            _surveyTimer = SurveyTimeout;
            _entries.Clear();
            _hoveredIndex = -1;
            _showDetail = false;

            var net = Network.NetworkManager.Instance;
            if (net != null && net.IsConnected)
            {
                net.Send(Network.PacketBuilder.SurveyRequest(position, range));
            }
        }

        /// <summary>Called by PacketHandler when SurveyResult arrives.</summary>
        public void OnSurveyResult(List<SurveyEntry> entries)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            _surveyActive = true;
            _surveyTimer = SurveyTimeout;
        }

        /// <summary>Dismiss survey results.</summary>
        public void Dismiss()
        {
            _surveyActive = false;
            _entries.Clear();
            _hoveredIndex = -1;
            _showDetail = false;
        }

        public bool IsActive => _surveyActive;

        // ─── Update ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_surveyActive) return;

            _surveyTimer -= Time.deltaTime;
            if (_surveyTimer <= 0f)
            {
                Dismiss();
                return;
            }

            // Toggle survey with V key
            if (Input.GetKeyDown(KeyCode.V))
            {
                Dismiss();
            }
        }

        // ─── OnGUI ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_surveyActive || _entries.Count == 0) return;

            DrawCompass();
            DrawEntryList();

            if (_showDetail && _hoveredIndex >= 0 && _hoveredIndex < _entries.Count)
            {
                DrawQualityDetail(_entries[_hoveredIndex]);
            }
        }

        // ─── Compass ────────────────────────────────────────────────────────

        private void DrawCompass()
        {
            var cam = Camera.main;
            if (cam == null) return;

            float cx = Screen.width / 2f;
            float cy = 80f;
            float compassR = 60f;

            // Background circle
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.7f);
            GUI.DrawTexture(new Rect(cx - compassR, cy - compassR, compassR * 2, compassR * 2),
                Texture2D.whiteTexture);

            // Draw each resource as a blip
            Vector3 playerPos = cam.transform.position;
            Vector3 playerFwd = cam.transform.forward;
            playerFwd.y = 0;
            playerFwd.Normalize();

            foreach (var entry in _entries)
            {
                Vector3 toTarget = entry.Position - playerPos;
                toTarget.y = 0;
                float dist = toTarget.magnitude;
                if (dist < 0.1f) continue;

                Vector3 dir = toTarget.normalized;
                float angle = Vector3.SignedAngle(playerFwd, dir, Vector3.up);
                float rad = angle * Mathf.Deg2Rad;

                // Blip position on compass (closer = more towards center)
                float normalizedDist = Mathf.Clamp01(dist / 200f);
                float blipR = compassR * 0.2f + compassR * 0.7f * normalizedDist;
                float bx = cx + Mathf.Sin(rad) * blipR;
                float by = cy - Mathf.Cos(rad) * blipR;

                Color blipColor = ResourceNode.GetColorForClass(entry.ResourceClass);
                GUI.color = blipColor;
                float blipSize = 8f;
                GUI.DrawTexture(new Rect(bx - blipSize / 2, by - blipSize / 2, blipSize, blipSize),
                    Texture2D.whiteTexture);
            }

            // North marker
            GUI.color = Color.red;
            float northAngle = Vector3.SignedAngle(playerFwd, Vector3.forward, Vector3.up);
            float northRad = northAngle * Mathf.Deg2Rad;
            float nx = cx + Mathf.Sin(northRad) * (compassR - 5f);
            float ny = cy - Mathf.Cos(northRad) * (compassR - 5f);
            var northStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red }
            };
            GUI.Label(new Rect(nx - 8, ny - 8, 16, 16), "N", northStyle);

            // Label
            GUI.color = Color.white;
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(new Rect(cx - 40, cy + compassR + 2, 80, 16),
                $"TMD SCAN ({_entries.Count})", labelStyle);
        }

        // ─── Entry list (right side) ────────────────────────────────────────

        private void DrawEntryList()
        {
            var cam = Camera.main;
            if (cam == null) return;

            const float panelW = 240f;
            const float entryH = 44f;
            const float padX = 10f;
            const float padY = 60f;
            float x = Screen.width - panelW - padX;
            float y = padY;

            // Panel header
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, panelW, 24f), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.8f, 1f);
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(x, y, panelW, 24f), "SURVEY RESULTS", headerStyle);
            y += 26f;

            Vector3 playerPos = cam.transform.position;

            for (int i = 0; i < _entries.Count && i < 8; i++)
            {
                var entry = _entries[i];
                float dist = Vector3.Distance(playerPos, entry.Position);
                Color typeColor = ResourceNode.GetColorForClass(entry.ResourceClass);
                string classLabel = ResourceNode.GetClassLabel(entry.ResourceClass);

                // Entry background
                Rect entryRect = new Rect(x, y, panelW, entryH);
                bool hovered = entryRect.Contains(Event.current.mousePosition);

                GUI.color = hovered
                    ? new Color(0.1f, 0.15f, 0.25f, 0.9f)
                    : new Color(0.05f, 0.05f, 0.1f, 0.8f);
                GUI.DrawTexture(entryRect, Texture2D.whiteTexture);

                // Color bar on left
                GUI.color = typeColor;
                GUI.DrawTexture(new Rect(x, y, 4f, entryH), Texture2D.whiteTexture);

                // Name
                GUI.color = Color.white;
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, fontStyle = FontStyle.Bold,
                    normal = { textColor = typeColor }
                };
                GUI.Label(new Rect(x + 8, y + 2, panelW - 16, 16), entry.Name ?? entry.TypeId, nameStyle);

                // Class + quality
                var infoStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                };
                GUI.Label(new Rect(x + 8, y + 16, panelW / 2, 14), classLabel, infoStyle);
                GUI.Label(new Rect(x + panelW / 2, y + 16, panelW / 2 - 8, 14),
                    $"Quality: {entry.QualityTier}", infoStyle);

                // Distance
                var distStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10, alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.5f, 0.8f, 1f) }
                };
                GUI.Label(new Rect(x + panelW - 60, y + 2, 52, 16), $"{dist:F0}m", distStyle);

                if (hovered)
                {
                    _hoveredIndex = i;
                    _showDetail = true;
                }

                y += entryH + 2f;
            }

            // Timer
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            var timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.4f, 0.4f, 0.4f) }
            };
            GUI.Label(new Rect(x, y + 2, panelW, 14), $"Expires in {_surveyTimer:F0}s", timerStyle);
            GUI.color = Color.white;
        }

        // ─── Quality detail panel ───────────────────────────────────────────

        private void DrawQualityDetail(SurveyEntry entry)
        {
            const float panelW = 220f;
            const float lineH = 16f;
            float panelH = lineH * 14 + 30f; // header + 11 attrs + tier + class
            float x = Screen.width - panelW - 260f;
            float y = 60f;

            // Background
            GUI.color = new Color(0.03f, 0.03f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), Texture2D.whiteTexture);

            // Border
            GUI.color = ResourceNode.GetColorForClass(entry.ResourceClass);
            GUI.DrawTexture(new Rect(x, y, panelW, 2f), Texture2D.whiteTexture);

            // Header
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ResourceNode.GetColorForClass(entry.ResourceClass) }
            };
            GUI.Label(new Rect(x, y + 4, panelW, lineH), entry.Name ?? entry.TypeId, headerStyle);
            y += lineH + 8;

            // Tier + class
            var infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(new Rect(x, y, panelW, lineH),
                $"{ResourceNode.GetClassLabel(entry.ResourceClass)} | {entry.QualityTier}", infoStyle);
            y += lineH + 6;

            // Attribute bars
            if (entry.Attributes != null && entry.Attributes.Length == 11)
            {
                for (int a = 0; a < 11; a++)
                {
                    DrawAttributeBar(x + 8, y, panelW - 16, 12f,
                        ResourceNode.AttributeShort[a],
                        ResourceNode.AttributeNames[a],
                        entry.Attributes[a]);
                    y += lineH;
                }
            }

            GUI.color = Color.white;
        }

        private void DrawAttributeBar(float x, float y, float w, float h,
            string shortName, string fullName, uint value)
        {
            float labelW = 28f;
            float valueW = 36f;
            float barX = x + labelW + 4;
            float barW = w - labelW - valueW - 8;
            float fill = value / 1000f;

            // Short label
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(new Rect(x, y, labelW, h), shortName, labelStyle);

            // Bar background
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(barX, y + 1, barW, h - 2), Texture2D.whiteTexture);

            // Bar fill — color based on quality
            Color barColor;
            if (fill >= 0.8f) barColor = new Color(0.2f, 0.9f, 0.3f);      // Excellent
            else if (fill >= 0.5f) barColor = new Color(0.3f, 0.7f, 1f);    // Good
            else if (fill >= 0.25f) barColor = new Color(0.9f, 0.8f, 0.2f); // Medium
            else barColor = new Color(0.8f, 0.3f, 0.2f);                     // Low

            GUI.color = barColor;
            GUI.DrawTexture(new Rect(barX, y + 1, barW * fill, h - 2), Texture2D.whiteTexture);

            // Value text
            GUI.color = Color.white;
            var valStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, alignment = TextAnchor.MiddleRight,
                normal = { textColor = barColor }
            };
            GUI.Label(new Rect(barX + barW + 2, y, valueW, h), $"{value}", valStyle);
        }
    }
}

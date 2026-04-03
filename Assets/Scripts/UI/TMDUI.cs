using UnityEngine;
using Orlo.Network;
using Orlo.World;

namespace Orlo.UI
{
    /// <summary>
    /// TMD (Terrain Manipulation Device) UI — toggle with T key.
    /// Shows operation selector (1-5), radius/intensity sliders, charge bar,
    /// and a wire-circle preview on terrain at the cursor position.
    /// Uses IMGUI (OnGUI) to match existing UI style.
    /// </summary>
    public class TMDUI : MonoBehaviour
    {
        public static TMDUI Instance { get; private set; }

        // ─── TMD State ──────────────────────────────────────────────────────
        private bool _active;
        private int _selectedOp; // 0=Dig, 1=Fill, 2=Smooth, 3=Scan, 4=Reinforce
        private float _radius = 4f;
        private float _intensity = 0.5f;
        private float _charges = 100f;
        private float _maxCharges = 100f;
        private int _tier; // 0=Basic, 1=Advanced, 2=Industrial

        private readonly string[] _opNames = { "Dig", "Fill", "Smooth", "Scan", "Reinforce" };
        private readonly string[] _opKeys = { "1", "2", "3", "4", "5" };
        private readonly Color[] _opColors =
        {
            new Color(0.8f, 0.3f, 0.2f),  // Dig — red
            new Color(0.3f, 0.7f, 0.3f),  // Fill — green
            new Color(0.4f, 0.6f, 0.9f),  // Smooth — blue
            new Color(0.9f, 0.8f, 0.2f),  // Scan — yellow
            new Color(0.6f, 0.4f, 0.8f),  // Reinforce — purple
        };
        private readonly string[] _tierNames = { "Basic", "Advanced", "Industrial" };
        private readonly float[] _tierMaxRadius = { 2f, 4f, 8f };

        // ─── Terrain hit info ───────────────────────────────────────────────
        private bool _hasHit;
        private Vector3 _hitPoint;
        private Vector3 _hitNormal;

        // ─── Feedback ───────────────────────────────────────────────────────
        private string _statusMsg = "";
        private float _statusTimer;

        // ─── Preview rendering ──────────────────────────────────────────────
        private LineRenderer _previewCircle;
        private const int CircleSegments = 48;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Create preview circle line renderer
            var go = new GameObject("TMDPreviewCircle");
            go.transform.SetParent(transform);
            _previewCircle = go.AddComponent<LineRenderer>();
            _previewCircle.positionCount = CircleSegments + 1;
            _previewCircle.loop = false;
            _previewCircle.startWidth = 0.08f;
            _previewCircle.endWidth = 0.08f;
            _previewCircle.useWorldSpace = true;
            _previewCircle.material = new Material(Shader.Find("Sprites/Default"));
            _previewCircle.startColor = Color.cyan;
            _previewCircle.endColor = Color.cyan;
            _previewCircle.enabled = false;
        }

        private void Update()
        {
            // Toggle TMD mode with T
            if (Input.GetKeyDown(KeyCode.T))
            {
                _active = !_active;
                _previewCircle.enabled = _active;
                if (_active)
                    ShowStatus("TMD Active");
                else
                    ShowStatus("TMD Deactivated");
            }

            if (!_active)
            {
                _previewCircle.enabled = false;
                return;
            }

            // Operation selection: keys 1-5
            for (int i = 0; i < 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _selectedOp = i;
                    ShowStatus($"{_opNames[i]} selected");
                }
            }

            // Radius adjust with scroll wheel (hold Shift)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    float maxR = _tier < _tierMaxRadius.Length ? _tierMaxRadius[_tier] : 8f;
                    _radius = Mathf.Clamp(_radius + scroll * 2f, 0.5f, maxR);
                }
            }

            // Intensity adjust with scroll wheel (hold Ctrl)
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    _intensity = Mathf.Clamp01(_intensity + scroll * 0.2f);
                }
            }

            // Raycast terrain under cursor
            _hasHit = false;
            var cam = Camera.main;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 200f))
                {
                    _hasHit = true;
                    _hitPoint = hit.point;
                    _hitNormal = hit.normal;
                }
            }

            // Update preview circle
            UpdatePreviewCircle();

            // Execute operation on left click
            if (Input.GetMouseButtonDown(0) && _hasHit && _charges > 0)
            {
                ExecuteOperation();
            }

            // Status timer
            if (_statusTimer > 0) _statusTimer -= Time.deltaTime;
        }

        private void ExecuteOperation()
        {
            var data = PacketBuilder.TMDOperation(_selectedOp, _hitPoint, _radius, _intensity);
            NetworkManager.Instance.Send(data);
            ShowStatus($"{_opNames[_selectedOp]} at ({_hitPoint.x:F0}, {_hitPoint.z:F0})");
        }

        private void UpdatePreviewCircle()
        {
            if (!_hasHit || !_active)
            {
                _previewCircle.enabled = false;
                return;
            }

            _previewCircle.enabled = true;

            // Set color based on selected operation
            var col = _opColors[_selectedOp];
            col.a = 0.8f;
            _previewCircle.startColor = col;
            _previewCircle.endColor = col;

            // Build circle on terrain surface
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (float)i / CircleSegments * Mathf.PI * 2f;
                float x = _hitPoint.x + Mathf.Cos(angle) * _radius;
                float z = _hitPoint.z + Mathf.Sin(angle) * _radius;
                float y = _hitPoint.y + 0.15f; // Slight offset above terrain

                // Raycast down to conform to terrain surface
                if (Physics.Raycast(new Vector3(x, _hitPoint.y + 20f, z), Vector3.down, out var hit, 50f))
                    y = hit.point.y + 0.15f;

                _previewCircle.SetPosition(i, new Vector3(x, y, z));
            }
        }

        // ─── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Called when server sends TMDStatus update.
        /// </summary>
        public void UpdateStatus(int tier, float charges, float maxCharges)
        {
            _tier = tier;
            _charges = charges;
            _maxCharges = maxCharges;
        }

        /// <summary>
        /// Called when server sends TMDResult.
        /// </summary>
        public void OnOperationResult(bool success, int operation, float chargesRemaining, string error)
        {
            _charges = chargesRemaining;
            if (success)
            {
                ShowStatus($"{_opNames[Mathf.Clamp(operation, 0, 4)]} complete — {chargesRemaining:F0} charges left");
            }
            else
            {
                ShowStatus($"TMD failed: {error}");
            }
        }

        /// <summary>
        /// Called when server sends scan results.
        /// </summary>
        public void OnScanResults(int resultCount)
        {
            ShowStatus($"Scan: {resultCount} resource deposit(s) found");
        }

        public void ShowStatus(string msg)
        {
            _statusMsg = msg;
            _statusTimer = 3f;
        }

        public bool IsActive => _active;

        // ─── IMGUI ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_active) return;

            float panelWidth = 220;
            float panelHeight = 260;
            float panelX = 10;
            float panelY = Screen.height / 2f - panelHeight / 2f;

            // Panel background
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), "");

            float y = panelY + 5;

            // Title + tier
            string tierName = _tier < _tierNames.Length ? _tierNames[_tier] : "Unknown";
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(panelX, y, panelWidth, 22), $"TMD [{tierName}]", titleStyle);
            y += 24;

            // Charge bar
            float chargeRatio = _maxCharges > 0 ? _charges / _maxCharges : 0;
            GUI.Label(new Rect(panelX + 5, y, 60, 18), "Charge:");
            DrawBar(new Rect(panelX + 65, y + 2, panelWidth - 75, 14), chargeRatio,
                new Color(0.2f, 0.6f, 0.9f), new Color(0.1f, 0.1f, 0.15f));
            GUI.Label(new Rect(panelX + 65, y + 2, panelWidth - 75, 14),
                $"{_charges:F0}/{_maxCharges:F0}",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 });
            y += 22;

            // Divider
            y += 4;

            // Operation buttons
            GUI.Label(new Rect(panelX + 5, y, panelWidth, 16), "Operation:");
            y += 18;
            for (int i = 0; i < 5; i++)
            {
                var btnRect = new Rect(panelX + 5, y, panelWidth - 10, 22);
                bool selected = _selectedOp == i;

                if (selected)
                {
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = _opColors[i];
                    if (GUI.Button(btnRect, $"[{_opKeys[i]}] {_opNames[i]}"))
                        _selectedOp = i;
                    GUI.backgroundColor = oldBg;
                }
                else
                {
                    if (GUI.Button(btnRect, $"[{_opKeys[i]}] {_opNames[i]}"))
                        _selectedOp = i;
                }
                y += 24;
            }

            y += 4;

            // Radius slider
            GUI.Label(new Rect(panelX + 5, y, 60, 18), $"Radius:");
            GUI.Label(new Rect(panelX + panelWidth - 40, y, 35, 18), $"{_radius:F1}m");
            y += 16;
            float maxR = _tier < _tierMaxRadius.Length ? _tierMaxRadius[_tier] : 8f;
            _radius = GUI.HorizontalSlider(new Rect(panelX + 10, y, panelWidth - 20, 16), _radius, 0.5f, maxR);
            y += 20;

            // Intensity slider
            GUI.Label(new Rect(panelX + 5, y, 60, 18), $"Power:");
            GUI.Label(new Rect(panelX + panelWidth - 40, y, 35, 18), $"{_intensity * 100:F0}%");
            y += 16;
            _intensity = GUI.HorizontalSlider(new Rect(panelX + 10, y, panelWidth - 20, 16), _intensity, 0.05f, 1f);
            y += 20;

            // Hints
            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUI.Label(new Rect(panelX + 5, y, panelWidth - 10, 14), "Shift+Scroll = radius  Ctrl+Scroll = power", hintStyle);

            // Status message (center screen)
            if (_statusTimer > 0 && !string.IsNullOrEmpty(_statusMsg))
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = _opColors[_selectedOp] }
                };
                float alpha = Mathf.Clamp01(_statusTimer);
                var c = statusStyle.normal.textColor;
                c.a = alpha;
                statusStyle.normal.textColor = c;
                GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f + 60, 400, 30), _statusMsg, statusStyle);
            }

            // Crosshair when in TMD mode
            if (_hasHit)
            {
                float cx = Screen.width / 2f;
                float cy = Screen.height / 2f;
                var crossColor = _opColors[_selectedOp];
                var tex = Texture2D.whiteTexture;
                var oldCol = GUI.color;
                GUI.color = crossColor;
                GUI.DrawTexture(new Rect(cx - 1, cy - 12, 2, 24), tex);
                GUI.DrawTexture(new Rect(cx - 12, cy - 1, 24, 2), tex);
                GUI.color = oldCol;
            }
        }

        private void DrawBar(Rect rect, float ratio, Color fillColor, Color bgColor)
        {
            var tex = Texture2D.whiteTexture;
            var oldCol = GUI.color;

            // Background
            GUI.color = bgColor;
            GUI.DrawTexture(rect, tex);

            // Fill
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), tex);

            GUI.color = oldCol;
        }
    }
}

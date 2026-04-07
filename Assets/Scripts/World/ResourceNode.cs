using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Visual representation of a resource node in the world.
    /// Pulsing glow based on resource type, concentration-driven size,
    /// click-to-gather with progress bar, depletion fade.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        // ─── Identity ───────────────────────────────────────────────────────
        public ulong SpawnId { get; private set; }
        public string TypeId { get; private set; }
        public string DisplayName { get; private set; }
        public int ResourceClass { get; private set; }
        public string QualityTier { get; private set; }

        // ─── Quality attributes (11 stats, 0-1000 scale) ────────────────────
        public uint[] Attributes { get; private set; } = new uint[11];
        public static readonly string[] AttributeNames =
        {
            "Conductivity", "Thermal Res", "Tensile Str", "Malleability",
            "Reactivity", "Density", "Purity", "Resonance",
            "Decay Res", "Flexibility", "Harmonic Res"
        };
        public static readonly string[] AttributeShort =
        {
            "CN", "TH", "TN", "ML", "RE", "DN", "PR", "RS", "DC", "FL", "HR"
        };

        // ─── State ──────────────────────────────────────────────────────────
        private float _radius = 3f;
        private float _concentration = 1f; // 0..1 remaining
        private bool _isGathering;
        private float _gatherProgress; // 0..1
        private float _gatherTotalTime = 4f;
        private float _pulsePhase;
        private bool _depleted;

        // ─── Visual components ──────────────────────────────────────────────
        private GameObject _meshObj;
        private Material _mat;
        private Color _baseColor;
        private Vector3 _baseScale;

        // ─── Interaction ────────────────────────────────────────────────────
        private bool _mouseOver;
        private bool _showTooltip;
        private float _holdTime;
        private const float HoldThreshold = 0.3f;

        // ─── Factory ────────────────────────────────────────────────────────
        public static ResourceNode Create(ulong spawnId, string typeId, string name,
            int resourceClass, uint[] attrs, Vector3 position, float radius, string qualityTier)
        {
            var go = new GameObject($"ResourceNode_{spawnId}_{typeId}");
            go.transform.position = position;
            var node = go.AddComponent<ResourceNode>();
            node.SpawnId = spawnId;
            node.TypeId = typeId;
            node.DisplayName = name;
            node.ResourceClass = resourceClass;
            node.QualityTier = qualityTier ?? "Medium";
            node._radius = Mathf.Max(1f, radius);
            if (attrs != null && attrs.Length == 11)
                System.Array.Copy(attrs, node.Attributes, 11);
            node.BuildVisual();
            return node;
        }

        private void BuildVisual()
        {
            _baseColor = GetColorForClass(ResourceClass);

            _meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _meshObj.transform.SetParent(transform, false);
            _meshObj.transform.localPosition = Vector3.up * 0.5f;

            float scale = Mathf.Lerp(0.6f, 1.8f, _radius / 10f);
            _baseScale = Vector3.one * scale;
            _meshObj.transform.localScale = _baseScale;

            // Remove default collider — we use a trigger on the parent
            var col = _meshObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Set up material
            var renderer = _meshObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                _mat = Orlo.Rendering.OrloShaders.CreateEmissive(_baseColor, _baseColor, 0.4f);
                _mat.SetFloat("_Metallic", 0.3f);
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.7f);
                renderer.material = _mat;
            }

            // Trigger collider for interaction
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = Mathf.Max(1.5f, scale);
            sphere.isTrigger = true;

            // Add a small particle-like secondary glow indicator
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * 0.5f;
            glow.transform.localScale = _baseScale * 1.3f;
            var glowCol = glow.GetComponent<Collider>();
            if (glowCol != null) Destroy(glowCol);
            var glowRenderer = glow.GetComponent<Renderer>();
            if (glowRenderer != null)
            {
                var glowMat = Orlo.Rendering.OrloShaders.CreateTransparent(Color.white);
                // URP transparency already configured by CreateTransparent
                glowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                glowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glowMat.SetInt("_ZWrite", 0);
                glowMat.DisableKeyword("_ALPHATEST_ON");
                glowMat.EnableKeyword("_ALPHABLEND_ON");
                glowMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                glowMat.renderQueue = 3000;
                glowMat.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.15f);
                glowRenderer.material = glowMat;
            }
        }

        // ─── Update ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (_depleted) return;

            // Pulse glow
            _pulsePhase += Time.deltaTime * 2f;
            float pulse = 0.3f + Mathf.Sin(_pulsePhase) * 0.15f;
            if (_mat != null)
            {
                float concentrationMul = Mathf.Lerp(0.2f, 1f, _concentration);
                _mat.SetColor("_EmissionColor", _baseColor * pulse * concentrationMul);
            }

            // Scale based on concentration
            if (_meshObj != null)
            {
                float scaleMul = Mathf.Lerp(0.3f, 1f, _concentration);
                _meshObj.transform.localScale = _baseScale * scaleMul;
            }

            // Hold-to-gather
            if (_mouseOver && Input.GetMouseButton(0))
            {
                _holdTime += Time.deltaTime;
                if (_holdTime >= HoldThreshold && !_isGathering)
                {
                    StartGathering();
                }
            }
            else
            {
                _holdTime = 0f;
            }
        }

        // ─── Gathering ──────────────────────────────────────────────────────

        private void StartGathering()
        {
            if (_isGathering || _depleted) return;
            _isGathering = true;
            _gatherProgress = 0f;

            // Send gather request to server
            var net = Network.NetworkManager.Instance;
            if (net != null && net.IsConnected)
            {
                net.Send(Network.PacketBuilder.GatherStart(SpawnId));
            }
        }

        /// <summary>Called by PacketHandler when server sends GatherProgress.</summary>
        public void OnGatherProgress(float progress, float totalTime)
        {
            _isGathering = true;
            _gatherProgress = Mathf.Clamp01(progress);
            _gatherTotalTime = totalTime;
        }

        /// <summary>Called by PacketHandler when gathering completes.</summary>
        public void OnGatherComplete(uint remaining)
        {
            _isGathering = false;
            _gatherProgress = 0f;

            if (remaining == 0)
            {
                SetDepleted();
            }
            else
            {
                // Rough concentration estimate
                _concentration = Mathf.Clamp01(remaining / 100f);
            }
        }

        /// <summary>Cancel gathering (player moved away, etc).</summary>
        public void CancelGathering()
        {
            if (!_isGathering) return;
            _isGathering = false;
            _gatherProgress = 0f;

            var net = Network.NetworkManager.Instance;
            if (net != null && net.IsConnected)
            {
                net.Send(Network.PacketBuilder.GatherCancel(SpawnId));
            }
        }

        private void SetDepleted()
        {
            _depleted = true;
            _isGathering = false;
            _gatherProgress = 0f;
            if (_mat != null)
            {
                _mat.color = Color.gray * 0.3f;
                _mat.SetColor("_EmissionColor", Color.black);
            }
            if (_meshObj != null)
                _meshObj.transform.localScale = _baseScale * 0.15f;

            // Auto-destroy after fade
            Destroy(gameObject, 5f);
        }

        /// <summary>Update remaining concentration from server.</summary>
        public void SetConcentration(float normalized)
        {
            _concentration = Mathf.Clamp01(normalized);
        }

        // ─── Interaction detection ──────────────────────────────────────────

        private void OnMouseEnter() { _mouseOver = true; _showTooltip = true; }
        private void OnMouseExit()
        {
            _mouseOver = false;
            _showTooltip = false;
            _holdTime = 0f;
            if (_isGathering) CancelGathering();
        }

        // ─── OnGUI — tooltip + gather bar ───────────────────────────────────

        private void OnGUI()
        {
            if (_depleted) return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * 2.5f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) return;

            // Flip Y for GUI coords
            float sx = screenPos.x;
            float sy = Screen.height - screenPos.y;

            // Gathering progress bar
            if (_isGathering)
            {
                DrawGatherBar(sx, sy);
            }

            // Tooltip on hover
            if (_showTooltip && !_isGathering)
            {
                DrawTooltip(sx, sy);
            }

            // Name label always visible within range
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist < 30f)
            {
                DrawNameLabel(sx, sy, dist);
            }
        }

        private void DrawGatherBar(float sx, float sy)
        {
            const float barW = 120f;
            const float barH = 12f;
            float x = sx - barW / 2;
            float y = sy - 30f;

            // Background
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x - 1, y - 1, barW + 2, barH + 2), Texture2D.whiteTexture);

            // Fill
            Color barColor = Color.Lerp(new Color(0.3f, 0.7f, 1f), new Color(0.2f, 1f, 0.4f), _gatherProgress);
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x, y, barW * _gatherProgress, barH), Texture2D.whiteTexture);

            // Label
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(x, y, barW, barH), $"Gathering {_gatherProgress * 100:F0}%", style);
        }

        private void DrawTooltip(float sx, float sy)
        {
            const float tooltipW = 180f;
            const float lineH = 16f;
            float tooltipH = lineH * 4;
            float x = sx - tooltipW / 2;
            float y = sy - tooltipH - 10f;

            // Background box
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, tooltipW, tooltipH), Texture2D.whiteTexture);

            GUI.color = Color.white;
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _baseColor }
            };
            var infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            GUI.Label(new Rect(x, y, tooltipW, lineH), DisplayName ?? TypeId, nameStyle);
            GUI.Label(new Rect(x, y + lineH, tooltipW, lineH), $"Quality: {QualityTier}", infoStyle);
            GUI.Label(new Rect(x, y + lineH * 2, tooltipW, lineH),
                $"Class: {GetClassLabel(ResourceClass)}", infoStyle);
            GUI.Label(new Rect(x, y + lineH * 3, tooltipW, lineH),
                "Click & hold to gather", infoStyle);
        }

        private void DrawNameLabel(float sx, float sy, float dist)
        {
            float alpha = Mathf.InverseLerp(30f, 15f, dist);
            GUI.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(sx - 80, sy - 16, 160, 20), DisplayName ?? TypeId, style);
            GUI.color = Color.white;
        }

        // ─── Helpers ────────────────────────────────────────────────────────

        public static Color GetColorForClass(int resourceClass)
        {
            // Top-level class (hundreds digit)
            int top = (resourceClass / 100) * 100;
            switch (top)
            {
                case 100: return new Color(0.7f, 0.7f, 0.75f);  // Metal — silver
                case 200: return new Color(0.6f, 0.55f, 0.45f);  // Mineral — earthy
                case 300: return new Color(0.3f, 0.85f, 0.3f);   // Organic — green
                case 400: return new Color(0.3f, 0.6f, 1.0f);    // Energy — blue
                default:  return new Color(0.8f, 0.8f, 0.8f);    // Matter — white
            }
        }

        public static string GetClassLabel(int resourceClass)
        {
            switch (resourceClass)
            {
                case 0:   return "Matter";
                case 100: return "Metal";
                case 110: return "Ferrous";
                case 120: return "Non-Ferrous";
                case 130: return "Exotic Metal";
                case 200: return "Mineral";
                case 210: return "Crystal";
                case 220: return "Stone";
                case 230: return "Ore";
                case 300: return "Organic";
                case 310: return "Flora";
                case 320: return "Fauna";
                case 330: return "Exo-Organic";
                case 400: return "Energy";
                case 410: return "Chemical";
                case 420: return "Plasma";
                case 430: return "Convergence";
                default:  return $"Class-{resourceClass}";
            }
        }
    }
}

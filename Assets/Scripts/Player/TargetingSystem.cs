using System.Collections.Generic;
using UnityEngine;
using Orlo.Network;
using Orlo.World;
using Orlo.UI;

namespace Orlo.Player
{
    /// <summary>
    /// Handles click-to-target selection, target frame display, loot proximity pickup,
    /// and the F key interaction prompt. Provides the selected target entity ID to
    /// CombatBarUI for directed attacks.
    /// </summary>
    public class TargetingSystem : MonoBehaviour
    {
        public static TargetingSystem Instance { get; private set; }

        /// <summary>Currently selected target entity ID (0 = no target).</summary>
        public ulong TargetEntityId { get; private set; }

        /// <summary>Currently highlighted loot entity within pickup range (0 = none).</summary>
        public ulong NearbyLootEntityId { get; private set; }

        [Header("Targeting")]
        [SerializeField] private float maxTargetDistance = 50f;
        [SerializeField] private float lootPickupRange = 3f;

        private string _targetName = "";
        private float _targetHealth;
        private float _targetMaxHealth;
        private bool _hasTargetHealth;

        // Loot prompt
        private bool _showLootPrompt;
        private string _lootPromptName = "Loot";

        // Target frame flash
        private float _targetFlashTimer;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Entity type of current target (parsed from name). 0=unknown, 1=player, 2=creature, 3=NPC.</summary>
        private uint _targetEntityType;

        /// <summary>Whether the current target is an NPC (entity type 3) within interaction range.</summary>
        private bool _showNpcPrompt;
        private float _npcInteractRange = 5f;

        private void Update()
        {
            HandleTargetClick();
            HandleLootProximity();
            HandleLootPickup();
            HandleNpcProximity();
            HandleNpcInteraction();
            UpdateTargetHealth();

            // Tab key to clear target
            if (Input.GetKeyDown(KeyCode.Tab))
                ClearTarget();

            if (_targetFlashTimer > 0)
                _targetFlashTimer -= Time.deltaTime;
        }

        /// <summary>Left click on an entity to select it as target.</summary>
        private void HandleTargetClick()
        {
            // Only target on left click when NOT holding RMB (mouselook)
            if (!Input.GetMouseButtonDown(0) || Input.GetMouseButton(1))
                return;

            // Don't steal clicks from UI
            if (GUIUtility.hotControl != 0)
                return;

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxTargetDistance))
            {
                // Walk up the hierarchy to find the entity root
                var go = hit.collider.gameObject;
                var root = FindEntityRoot(go);
                if (root != null)
                {
                    ulong entityId = ParseEntityId(root.name);
                    if (entityId != 0)
                    {
                        SelectTarget(entityId, root);
                        return;
                    }
                }
            }

            // Clicked on nothing - clear target
            ClearTarget();
        }

        /// <summary>Walk up the hierarchy to find a GameObject named Entity_*.</summary>
        private GameObject FindEntityRoot(GameObject go)
        {
            var current = go.transform;
            while (current != null)
            {
                if (current.name.StartsWith("Entity_") || current.name.StartsWith("Loot_"))
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        /// <summary>Parse entity ID from name like "Entity_12345_2" or "Loot_12345".</summary>
        private ulong ParseEntityId(string name)
        {
            string[] parts = name.Split('_');
            if (parts.Length >= 2 && ulong.TryParse(parts[1], out ulong id))
                return id;
            return 0;
        }

        private void SelectTarget(ulong entityId, GameObject go)
        {
            TargetEntityId = entityId;
            _targetFlashTimer = 0.3f;

            // Get name from EntityManager
            var em = EntityManager.Instance;
            _targetName = em?.GetEntityName(entityId) ?? go.name;

            // Parse entity type from name (Entity_{id}_{type})
            _targetEntityType = ParseEntityType(go.name);

            // Get health if available
            _hasTargetHealth = em != null && em.TryGetEntityHealth(entityId, out _targetHealth, out _targetMaxHealth);

            Debug.Log($"[Target] Selected: {_targetName} (id={entityId}, type={_targetEntityType})");
        }

        /// <summary>Parse entity type from name like "Entity_12345_3".</summary>
        private uint ParseEntityType(string name)
        {
            string[] parts = name.Split('_');
            if (parts.Length >= 3 && uint.TryParse(parts[2], out uint type))
                return type;
            return 0;
        }

        public void ClearTarget()
        {
            if (TargetEntityId != 0)
            {
                Debug.Log($"[Target] Cleared target {TargetEntityId}");
                TargetEntityId = 0;
                _targetName = "";
                _hasTargetHealth = false;
                _targetEntityType = 0;
                _showNpcPrompt = false;
            }
        }

        /// <summary>Clear target if the given entity is our current target (e.g., on death).</summary>
        public void ClearTargetIfMatch(ulong entityId)
        {
            if (TargetEntityId == entityId)
                ClearTarget();
        }

        private void UpdateTargetHealth()
        {
            if (TargetEntityId == 0) return;
            var em = EntityManager.Instance;
            if (em != null)
                _hasTargetHealth = em.TryGetEntityHealth(TargetEntityId, out _targetHealth, out _targetMaxHealth);
        }

        /// <summary>Check for nearby loot entities and show F-to-pickup prompt.</summary>
        private void HandleLootProximity()
        {
            _showLootPrompt = false;
            NearbyLootEntityId = 0;

            var em = EntityManager.Instance;
            if (em == null) return;

            // Find player position
            var player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            Vector3 playerPos = player.transform.position;

            float closestDist = lootPickupRange;
            ulong closestLoot = 0;
            string closestName = "Loot";

            foreach (ulong lootId in em.GetLootEntityIds())
            {
                var lootGo = em.GetEntity(lootId);
                if (lootGo == null) continue;

                float dist = Vector3.Distance(playerPos, lootGo.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestLoot = lootId;
                    closestName = em.GetEntityName(lootId) ?? "Loot";
                }
            }

            if (closestLoot != 0)
            {
                _showLootPrompt = true;
                NearbyLootEntityId = closestLoot;
                _lootPromptName = closestName;
            }
        }

        /// <summary>F key picks up nearby loot.</summary>
        private void HandleLootPickup()
        {
            if (!_showLootPrompt || NearbyLootEntityId == 0) return;

            if (Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log($"[Loot] Sending pickup request for loot entity {NearbyLootEntityId}");
                var data = PacketBuilder.LootPickup(NearbyLootEntityId);
                NetworkManager.Instance.Send(data);

                // Despawn the loot entity immediately (optimistic)
                EntityManager.Instance?.DespawnLootEntity(NearbyLootEntityId);
                _showLootPrompt = false;
                NearbyLootEntityId = 0;
            }
        }

        // ─── NPC Interaction ────────────────────────────────────────────────

        /// <summary>Check if current target is an NPC within interaction range.</summary>
        private void HandleNpcProximity()
        {
            _showNpcPrompt = false;

            if (TargetEntityId == 0 || _targetEntityType != 3)
                return;

            // Check distance to NPC
            var player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            var npcGo = EntityManager.Instance?.GetEntity(TargetEntityId);
            if (npcGo == null) return;

            float dist = Vector3.Distance(player.transform.position, npcGo.transform.position);
            _showNpcPrompt = dist <= _npcInteractRange;
        }

        /// <summary>F key interacts with targeted NPC (when no loot is nearby).</summary>
        private void HandleNpcInteraction()
        {
            // Loot pickup takes priority over NPC interaction
            if (_showLootPrompt && NearbyLootEntityId != 0)
                return;

            if (!_showNpcPrompt || TargetEntityId == 0)
                return;

            if (Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log($"[NPC] Sending interact request for NPC entity {TargetEntityId}");
                var data = PacketBuilder.NPCInteract(TargetEntityId);
                NetworkManager.Instance.Send(data);
            }
        }

        // ─── OnGUI: Target Frame + Loot Prompt ─────────────────────────────

        private void OnGUI()
        {
            DrawTargetFrame();
            DrawLootPrompt();
            DrawNpcPrompt();
        }

        /// <summary>Draw the target frame in the top-center area showing name + health.</summary>
        private void DrawTargetFrame()
        {
            if (TargetEntityId == 0) return;

            float s = UIScaler.Scale;
            float frameW = 220f * s;
            float frameH = 50f * s;
            float frameX = (Screen.width - frameW) / 2f;
            float frameY = 60f * s;

            // Background
            Color bgColor = _targetFlashTimer > 0
                ? new Color(0.3f, 0.15f, 0f, 0.85f)
                : new Color(0.05f, 0.05f, 0.1f, 0.85f);
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(frameX, frameY, frameW, frameH), Texture2D.whiteTexture);

            // Border
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            GUI.DrawTexture(new Rect(frameX, frameY, frameW, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frameX, frameY + frameH - 1, frameW, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frameX, frameY, 1, frameH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frameX + frameW - 1, frameY, 1, frameH), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Target name
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(13),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(frameX, frameY + 2 * s, frameW, 20 * s), _targetName, nameStyle);

            // Health bar (if available)
            if (_hasTargetHealth && _targetMaxHealth > 0)
            {
                float barX = frameX + 10 * s;
                float barY = frameY + 24 * s;
                float barW = frameW - 20 * s;
                float barH = 14 * s;
                float fill = Mathf.Clamp01(_targetHealth / _targetMaxHealth);

                // Bar background
                GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

                // Health fill - color shifts from green to red
                Color healthColor = fill > 0.5f
                    ? Color.Lerp(Color.yellow, new Color(0.2f, 0.8f, 0.2f), (fill - 0.5f) * 2f)
                    : Color.Lerp(Color.red, Color.yellow, fill * 2f);
                GUI.color = healthColor;
                GUI.DrawTexture(new Rect(barX, barY, barW * fill, barH), Texture2D.whiteTexture);

                // Health text
                GUI.color = Color.white;
                var hpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = UIScaler.ScaledFontSize(10),
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(barX, barY, barW, barH),
                    $"{_targetHealth:F0} / {_targetMaxHealth:F0}", hpStyle);
            }
            else
            {
                // No health data - show "No data" or entity type
                var infoStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = UIScaler.ScaledFontSize(10),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                };
                GUI.Label(new Rect(frameX, frameY + 24 * s, frameW, 20 * s), "[Tab to deselect]", infoStyle);
            }

            GUI.color = Color.white;
        }

        /// <summary>Draw "Press F to loot" prompt when near loot.</summary>
        private void DrawLootPrompt()
        {
            if (!_showLootPrompt) return;

            float s = UIScaler.Scale;
            float promptW = 200f * s;
            float promptH = 35f * s;
            float promptX = (Screen.width - promptW) / 2f;
            float promptY = Screen.height * 0.55f;

            // Pulsing background
            float pulse = 0.7f + Mathf.Sin(Time.time * 3f) * 0.3f;
            GUI.color = new Color(0.1f, 0.1f, 0.05f, 0.85f * pulse);
            GUI.DrawTexture(new Rect(promptX, promptY, promptW, promptH), Texture2D.whiteTexture);

            // Gold border
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.8f * pulse);
            GUI.DrawTexture(new Rect(promptX, promptY, promptW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX, promptY + promptH - 2, promptW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX, promptY, 2, promptH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX + promptW - 2, promptY, 2, promptH), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.9f, 0.3f);
            var promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(14),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(promptX, promptY, promptW, promptH),
                $"[F] Loot {_lootPromptName}", promptStyle);

            GUI.color = Color.white;
        }

        /// <summary>Draw "Press F to talk" prompt when targeting a nearby NPC.</summary>
        private void DrawNpcPrompt()
        {
            // Don't show NPC prompt if loot prompt is already showing (loot takes priority)
            if (_showLootPrompt || !_showNpcPrompt) return;

            float s = UIScaler.Scale;
            float promptW = 220f * s;
            float promptH = 35f * s;
            float promptX = (Screen.width - promptW) / 2f;
            float promptY = Screen.height * 0.55f;

            // Pulsing background
            float pulse = 0.7f + Mathf.Sin(Time.time * 2f) * 0.3f;
            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.85f * pulse);
            GUI.DrawTexture(new Rect(promptX, promptY, promptW, promptH), Texture2D.whiteTexture);

            // Cyan border (NPC color)
            GUI.color = new Color(0.3f, 0.85f, 1f, 0.8f * pulse);
            GUI.DrawTexture(new Rect(promptX, promptY, promptW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX, promptY + promptH - 2, promptW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX, promptY, 2, promptH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX + promptW - 2, promptY, 2, promptH), Texture2D.whiteTexture);

            GUI.color = new Color(0.4f, 0.9f, 1f);
            var promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIScaler.ScaledFontSize(14),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(promptX, promptY, promptW, promptH),
                $"[F] Talk to {_targetName}", promptStyle);

            GUI.color = Color.white;
        }
    }
}

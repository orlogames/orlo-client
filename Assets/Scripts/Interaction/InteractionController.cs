using UnityEngine;
using Orlo.Network;
using Orlo.Player;
using Orlo.UI;
using Orlo.World;

namespace Orlo.Interaction
{
    /// <summary>
    /// Manages player interactions with NPCs. Detects nearby NPCInteractable objects
    /// using sphere overlap, shows interaction prompts, and sends interaction packets
    /// to the server on key press. Replaces the NPC interaction logic in TargetingSystem
    /// with a component-driven approach.
    /// </summary>
    public class InteractionController : MonoBehaviour
    {
        public static InteractionController Instance { get; private set; }

        [Header("Interaction")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        /// <summary>The closest NPCInteractable within interaction range, if any.</summary>
        public NPCInteractable CurrentTarget { get; private set; }

        /// <summary>Whether the player is currently in an interaction (UI open).</summary>
        public bool IsInteracting { get; private set; }

        private NPCInteractable[] _nearbyNPCs = new NPCInteractable[16];
        private float _scanTimer;
        private const float ScanInterval = 0.2f;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Don't process interactions during chat input
            if (ChatUI.Instance != null && ChatUI.Instance.IsInputActive)
                return;

            // Scan for nearby NPCs periodically (not every frame)
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0)
            {
                _scanTimer = ScanInterval;
                ScanForNPCs();
            }

            // Handle interaction input
            if (Input.GetKeyDown(interactKey) && !IsInteracting)
            {
                TryInteract();
            }

            // Escape to cancel interaction
            if (IsInteracting && Input.GetKeyDown(KeyCode.Escape))
            {
                EndInteraction();
            }
        }

        /// <summary>
        /// Scan for NPCInteractable components within detection range using Physics.OverlapSphere.
        /// Updates CurrentTarget to the closest NPC within interaction range.
        /// </summary>
        private void ScanForNPCs()
        {
            CurrentTarget = null;

            var colliders = Physics.OverlapSphere(transform.position, detectionRange);
            float closestDist = interactionRange;
            NPCInteractable closest = null;

            foreach (var col in colliders)
            {
                // Walk up hierarchy to find NPCInteractable
                var npc = col.GetComponentInParent<NPCInteractable>();
                if (npc == null) continue;

                float dist = Vector3.Distance(transform.position, npc.transform.position);

                // Update in-range status on each NPC
                npc.InRange = dist <= interactionRange;
                npc.IsTargeted = false;

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = npc;
                }
            }

            if (closest != null)
            {
                closest.IsTargeted = true;
                CurrentTarget = closest;
            }
        }

        /// <summary>
        /// Attempt to interact with the current target NPC. Sends NPCInteract packet to server.
        /// </summary>
        private void TryInteract()
        {
            if (CurrentTarget == null) return;

            // Check if any blocking UI is open (shop, crafting, quest dialog)
            if (ShopUI.Instance != null && IsUIVisible(ShopUI.Instance))
                return;
            if (CraftingUI.Instance != null && IsUIVisible(CraftingUI.Instance))
                return;
            if (QuestDialogUI.Instance != null && IsUIVisible(QuestDialogUI.Instance))
                return;

            Debug.Log($"[Interaction] Interacting with {CurrentTarget.NPCName} " +
                      $"(entity={CurrentTarget.EntityId}, type={CurrentTarget.Type})");

            IsInteracting = true;

            // Send interaction packet to server
            var data = PacketBuilder.NPCInteract(CurrentTarget.EntityId);
            NetworkManager.Instance.Send(data);
        }

        /// <summary>
        /// End the current interaction. Called when UI is closed or player moves away.
        /// </summary>
        public void EndInteraction()
        {
            IsInteracting = false;
        }

        /// <summary>
        /// Check if a MonoBehaviour-based UI is currently visible by checking for a _visible field.
        /// Uses reflection-free approach: checks if the component is enabled and active.
        /// </summary>
        private bool IsUIVisible(MonoBehaviour ui)
        {
            // The UI scripts use OnGUI which runs when the component is enabled.
            // We check the common pattern where they have a Show/Hide that controls _visible.
            // Since we can't access _visible, we check if the GO is active.
            return ui != null && ui.isActiveAndEnabled;
        }

        private void OnGUI()
        {
            if (IsInteracting) return;

            // Draw prompt for the current target
            if (CurrentTarget != null && CurrentTarget.InRange)
            {
                CurrentTarget.DrawWorldPrompt(Camera.main);
            }
        }
    }
}

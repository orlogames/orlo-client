using UnityEngine;
using Orlo.UI.TMD;

namespace Orlo.Interaction
{
    /// <summary>
    /// Marks a GameObject as an interactable NPC. Stores metadata about the NPC
    /// and renders the interaction prompt when the player is in range.
    /// Attach to NPC entity GameObjects (entity type 3).
    /// </summary>
    public class NPCInteractable : MonoBehaviour
    {
        public enum InteractionType
        {
            Dialogue,
            Shop,
            Craft,
            Quest
        }

        /// <summary>Server entity ID for this NPC.</summary>
        public ulong EntityId { get; private set; }

        /// <summary>Display name shown in the interaction prompt.</summary>
        public string NPCName { get; private set; } = "NPC";

        /// <summary>What kind of interaction this NPC provides.</summary>
        public InteractionType Type { get; private set; } = InteractionType.Dialogue;

        /// <summary>Whether the player is currently close enough to interact.</summary>
        public bool InRange { get; set; }

        /// <summary>Whether this NPC is the current interaction target.</summary>
        public bool IsTargeted { get; set; }

        private RacePalette P => TMDTheme.Instance?.Palette ?? RacePalette.Solari;

        /// <summary>
        /// Initialize this interactable with server data.
        /// </summary>
        public void Setup(ulong entityId, string npcName, InteractionType type = InteractionType.Dialogue)
        {
            EntityId = entityId;
            NPCName = npcName;
            Type = type;
        }

        /// <summary>
        /// Get the prompt text for this NPC based on its interaction type.
        /// </summary>
        public string GetPromptText()
        {
            string verb = Type switch
            {
                InteractionType.Shop => "Trade with",
                InteractionType.Craft => "Use",
                InteractionType.Quest => "Talk to",
                InteractionType.Dialogue => "Talk to",
                _ => "Interact with"
            };
            return $"[F] {verb} {NPCName}";
        }

        /// <summary>
        /// Get the prompt icon/color hint based on interaction type.
        /// </summary>
        public Color GetPromptColor()
        {
            return Type switch
            {
                InteractionType.Shop => new Color(1f, 0.85f, 0.2f),     // Gold for vendors
                InteractionType.Craft => new Color(0.9f, 0.5f, 0.1f),   // Orange for crafting
                InteractionType.Quest => new Color(1f, 1f, 0.3f),        // Yellow for quests
                InteractionType.Dialogue => new Color(0.4f, 0.9f, 1f),  // Cyan for dialogue
                _ => Color.white
            };
        }

        /// <summary>
        /// Draw the floating name tag above this NPC using TMD styling.
        /// Called by InteractionController when the NPC is in detection range.
        /// </summary>
        public void DrawWorldPrompt(Camera cam)
        {
            if (cam == null) return;

            // Position the prompt above the NPC's head
            Vector3 worldPos = transform.position + Vector3.up * 2.2f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // Behind camera check
            if (screenPos.z < 0) return;

            // Convert to GUI coordinates (Y is flipped)
            float guiY = Screen.height - screenPos.y;

            float s = Orlo.UI.UIScaler.Scale;
            float promptW = 240f * s;
            float promptH = 28f * s;
            float promptX = screenPos.x - promptW / 2f;
            float promptY = guiY - promptH;

            // Pulsing effect
            float pulse = 0.7f + Mathf.Sin(Time.time * 2.5f) * 0.3f;
            Color promptColor = GetPromptColor();

            // Background panel
            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.8f * pulse);
            GUI.DrawTexture(new Rect(promptX, promptY, promptW, promptH), Texture2D.whiteTexture);

            // Border in NPC type color
            GUI.color = new Color(promptColor.r, promptColor.g, promptColor.b, 0.7f * pulse);
            GUI.DrawTexture(new Rect(promptX, promptY, promptW, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX, promptY + promptH - 1, promptW, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX, promptY, 1, promptH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(promptX + promptW - 1, promptY, 1, promptH), Texture2D.whiteTexture);

            // Prompt text
            GUI.color = promptColor;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Orlo.UI.UIScaler.ScaledFontSize(12),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(promptX, promptY, promptW, promptH), GetPromptText(), style);

            GUI.color = Color.white;
        }
    }
}

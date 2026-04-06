using UnityEngine;
using System.Collections.Generic;
using Orlo.UI;

namespace Orlo.World
{
    /// <summary>
    /// Manages 3D equipment visuals on a character. Tries to load GLB models via
    /// AssetLoader first, falls back to procedurally-generated equipment meshes.
    /// Parents loaded models to the correct bone on the character skeleton.
    /// Add this component to the player GameObject.
    /// </summary>
    public class EquipmentVisualManager : MonoBehaviour
    {
        /// <summary>Currently attached equipment visuals, keyed by proto slot ID.</summary>
        private readonly Dictionary<int, GameObject> _attachedVisuals = new();

        // Bone name mapping for equipment slots (proto EquipmentSlot enum value -> bone name)
        // Uses bone names from RuntimeRigBuilder's humanoid rig hierarchy.
        private static readonly Dictionary<int, string> SlotToBone = new()
        {
            { 1,  "Head" },         // Head
            { 2,  "Chest" },        // Chest
            { 3,  "Hips" },         // Legs
            { 4,  "RightFoot" },    // Feet (primary foot bone)
            { 5,  "LeftHand" },     // Gloves (approximate)
            { 6,  "LeftLowerArm" }, // LeftBracer
            { 7,  "RightLowerArm" },// RightBracer
            { 8,  "LeftUpperArm" }, // LeftBicep
            { 9,  "RightUpperArm" },// RightBicep
            { 10, "Chest" },        // Shoulders
            { 11, "Hips" },         // Belt
            { 12, "Chest" },        // Backpack (attach to chest, offset backward)
            { 13, "LeftLowerArm" }, // LeftWrist
            { 14, "RightLowerArm" },// RightWrist
            { 15, "LeftHand" },     // LeftHand weapon
            { 16, "RightHand" },    // RightHand weapon
            { 17, "RightHand" }     // TwoHands weapon
        };

        // Slots that spawn mirrored visuals on paired bones (e.g., feet on both sides)
        private static readonly Dictionary<int, string> SlotToMirrorBone = new()
        {
            { 4, "LeftFoot" },      // Feet — mirror visual on left foot
        };

        /// <summary>Mirrored visuals for symmetric slots like feet.</summary>
        private readonly Dictionary<int, GameObject> _mirrorVisuals = new();

        /// <summary>
        /// Refresh all equipment visuals based on current EquipmentUI state.
        /// Call after login/zone change when full equipment state arrives.
        /// </summary>
        public void RefreshAllVisuals()
        {
            // Clear existing visuals
            foreach (var kv in _attachedVisuals)
            {
                if (kv.Value != null)
                    Destroy(kv.Value);
            }
            _attachedVisuals.Clear();

            foreach (var kv in _mirrorVisuals)
            {
                if (kv.Value != null)
                    Destroy(kv.Value);
            }
            _mirrorVisuals.Clear();

            // Re-attach from EquipmentUI state
            var equipUI = EquipmentUI.Instance;
            if (equipUI == null) return;

            var equipped = equipUI.GetEquippedItems();
            if (equipped == null) return;

            foreach (var kv in equipped)
            {
                if (kv.Value.Occupied)
                    AttachVisualForSlot(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Attach or update the visual for a single equipment slot.
        /// Called from PacketHandler when EquipmentChanged is received.
        /// </summary>
        public void OnSlotEquipped(int protoSlotId, InventoryUI.ItemSlot item)
        {
            // Remove old visual if any
            RemoveVisualForSlot(protoSlotId);

            if (item.Occupied)
                AttachVisualForSlot(protoSlotId, item);
        }

        /// <summary>
        /// Remove the visual for a single equipment slot.
        /// Called from PacketHandler when equipment is unequipped.
        /// </summary>
        public void OnSlotUnequipped(int protoSlotId)
        {
            RemoveVisualForSlot(protoSlotId);
        }

        private void AttachVisualForSlot(int protoSlotId, InventoryUI.ItemSlot item)
        {
            // Find the target bone
            Transform bone = FindBoneForSlot(protoSlotId);
            if (bone == null)
            {
                Debug.LogWarning($"[EquipVisual] No bone found for slot {protoSlotId}");
                return;
            }

            // Try GLB model first, fall back to procedural
            GameObject visual = TryLoadGlbVisual(item);
            if (visual == null)
                visual = CreateProceduralVisual(protoSlotId, item);
            if (visual == null) return;

            // Attach to bone
            ProceduralEquipment.AttachToBone(visual, bone);

            // Apply slot-specific transforms
            ApplySlotOffset(visual, protoSlotId);

            _attachedVisuals[protoSlotId] = visual;
            Debug.Log($"[EquipVisual] Attached {item.Name} to slot {protoSlotId} ({bone.name})");

            // Handle mirrored slots (e.g., feet — spawn on both sides)
            if (SlotToMirrorBone.TryGetValue(protoSlotId, out string mirrorBoneName))
            {
                Transform mirrorBone = FindBoneRecursive(transform, mirrorBoneName);
                if (mirrorBone != null)
                {
                    GameObject mirrorVisual = TryLoadGlbVisual(item);
                    if (mirrorVisual == null)
                        mirrorVisual = CreateProceduralVisual(protoSlotId, item);
                    if (mirrorVisual != null)
                    {
                        ProceduralEquipment.AttachToBone(mirrorVisual, mirrorBone);
                        ApplySlotOffset(mirrorVisual, protoSlotId);
                        _mirrorVisuals[protoSlotId] = mirrorVisual;
                    }
                }
            }
        }

        private void RemoveVisualForSlot(int protoSlotId)
        {
            if (_attachedVisuals.TryGetValue(protoSlotId, out var existing))
            {
                if (existing != null)
                    Destroy(existing);
                _attachedVisuals.Remove(protoSlotId);
            }

            if (_mirrorVisuals.TryGetValue(protoSlotId, out var mirror))
            {
                if (mirror != null)
                    Destroy(mirror);
                _mirrorVisuals.Remove(protoSlotId);
            }
        }

        /// <summary>
        /// Try to load a GLB model for the item via AssetLoader.
        /// Returns an instantiated GameObject or null if no GLB exists.
        /// </summary>
        private GameObject TryLoadGlbVisual(InventoryUI.ItemSlot item)
        {
            if (AssetLoader.Instance == null) return null;

            // Try loading by item name converted to asset ID (lowercase, underscored)
            string assetId = item.Name.ToLowerInvariant().Replace(' ', '_');
            var model = AssetLoader.Instance.TryLoadModel(assetId);
            if (model != null)
            {
                Debug.Log($"[EquipVisual] Loaded GLB model for '{item.Name}' (assetId={assetId})");
                return model;
            }

            // Also try with item ID if available
            if (item.ItemId > 0)
            {
                string idAsset = $"item_{item.ItemId}";
                model = AssetLoader.Instance.TryLoadModel(idAsset);
                if (model != null)
                {
                    Debug.Log($"[EquipVisual] Loaded GLB model for item ID {item.ItemId}");
                    return model;
                }
            }

            return null;
        }

        private Transform FindBoneForSlot(int protoSlotId)
        {
            if (!SlotToBone.TryGetValue(protoSlotId, out string boneName))
                return null;

            // Search recursively in the character hierarchy for the bone
            return FindBoneRecursive(transform, boneName);
        }

        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == boneName)
                    return child;

                var found = FindBoneRecursive(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Create a procedural equipment visual as fallback when no GLB model exists.
        /// </summary>
        private GameObject CreateProceduralVisual(int protoSlotId, InventoryUI.ItemSlot item)
        {
            // Determine style from rarity
            EquipmentStyle style = item.Rarity switch
            {
                >= 3 => EquipmentStyle.Ornate,
                2 => EquipmentStyle.Elegant,
                1 => EquipmentStyle.Rugged,
                _ => EquipmentStyle.Basic
            };

            // Weapon slots (LeftHand=15, RightHand=16, TwoHands=17)
            if (protoSlotId == 15 || protoSlotId == 16 || protoSlotId == 17)
            {
                // Check category to determine weapon type
                // Category 1 = melee, 2 = ranged, 3 = staff/magic
                if (item.Category == 3)
                    return ProceduralEquipment.CreateStaff(1.5f);
                else
                    return ProceduralEquipment.CreateSword(0.8f, style);
            }

            // Head slot
            if (protoSlotId == 1)
                return ProceduralEquipment.CreateHelmet(style);

            // Shield on left hand
            if (protoSlotId == 15 && item.Category == 4) // Category 4 = shield
                return ProceduralEquipment.CreateShield(0.5f, style);

            // For armor slots, we don't have procedural armor meshes yet.
            // Return null — these will be handled when armor mesh generation is added.
            return null;
        }

        /// <summary>Apply slot-specific position/rotation offsets for correct placement.</summary>
        private void ApplySlotOffset(GameObject visual, int protoSlotId)
        {
            switch (protoSlotId)
            {
                case 16: // RightHand — sword/weapon
                case 17: // TwoHands
                    visual.transform.localRotation = Quaternion.Euler(0, 0, -90f);
                    visual.transform.localPosition = new Vector3(0, -0.05f, 0);
                    break;
                case 15: // LeftHand — off-hand/shield
                    visual.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                    visual.transform.localPosition = new Vector3(0, 0.05f, 0);
                    break;
                case 1: // Head — helmet sits on top
                    visual.transform.localPosition = new Vector3(0, 0.05f, 0);
                    break;
                case 12: // Backpack — offset behind chest
                    visual.transform.localPosition = new Vector3(0, 0, -0.2f);
                    break;
                case 4: // Feet — slight downward offset to sit on foot bone
                    visual.transform.localPosition = new Vector3(0, -0.02f, 0);
                    visual.transform.localScale = Vector3.one * 0.8f;
                    break;
            }
        }

        private void OnDestroy()
        {
            foreach (var kv in _attachedVisuals)
            {
                if (kv.Value != null)
                    Destroy(kv.Value);
            }
            _attachedVisuals.Clear();

            foreach (var kv in _mirrorVisuals)
            {
                if (kv.Value != null)
                    Destroy(kv.Value);
            }
            _mirrorVisuals.Clear();
        }
    }
}

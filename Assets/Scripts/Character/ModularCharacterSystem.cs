using System;
using System.Collections.Generic;
using UnityEngine;
using Orlo.UI.CharacterCreation;

namespace Orlo.Character
{
    /// <summary>
    /// Modular character system — industry-standard approach used by Star Citizen,
    /// Arc Raiders, and every MMO character creator.
    ///
    /// Architecture:
    /// - One shared skeleton (bone hierarchy) on a root GameObject
    /// - Multiple SkinnedMeshRenderers (body modules) share the same bones
    /// - Equipment = swap the mesh on the appropriate body slot
    /// - Blendshapes drive face/body customization per module
    /// - All modules must be rigged to the SAME armature
    ///
    /// Body Slots:
    ///   Head, Torso, Arms, Legs, Feet, Hands, Hair
    ///
    /// Equipment overrides body slots — equipping chest armor replaces
    /// the Torso SkinnedMeshRenderer mesh with the armor mesh.
    /// </summary>
    public class ModularCharacterSystem : MonoBehaviour
    {
        // ─── Body Slot Definition ──────────────────────────────────────────
        public enum BodySlot
        {
            Head, Torso, Arms, Legs, Feet, Hands, Hair,
            // Equipment overlay slots (render on top of body)
            Helmet, ChestArmor, LegArmor, Gloves, Boots,
            Shoulders, Belt, Backpack, Cape,
            WeaponRight, WeaponLeft
        }

        // ─── Slot State ────────────────────────────────────────────────────
        private class SlotState
        {
            public BodySlot Slot;
            public GameObject SlotObject;        // Child GO with SkinnedMeshRenderer
            public SkinnedMeshRenderer Renderer;
            public Mesh BaseMesh;                // Default body mesh (for unequip)
            public Mesh CurrentMesh;             // Currently active mesh
            public Material[] BaseMaterials;
            public bool IsEquipmentOverride;     // True if currently showing equipment
        }

        // ─── State ─────────────────────────────────────────────────────────
        private Transform _rootBone;
        private Transform[] _boneArray;
        private Dictionary<string, Transform> _boneMap = new();
        private Dictionary<BodySlot, SlotState> _slots = new();
        private Animator _animator;
        private AppearanceData _appearance;
        private bool _initialized;

        // Standard humanoid bone names (Unity Mecanim compatible)
        private static readonly string[] HumanoidBoneNames =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Chest",
            "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToeBase",
            "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToeBase"
        };

        // Alternative bone names (Mixamo, UniRig, etc)
        private static readonly Dictionary<string, string[]> BoneAliases = new()
        {
            { "Hips", new[] { "Hips", "mixamorig:Hips", "Armature|Hips", "pelvis" } },
            { "Spine", new[] { "Spine", "mixamorig:Spine", "spine_01" } },
            { "Chest", new[] { "Chest", "Spine2", "mixamorig:Spine2", "spine_03" } },
            { "Neck", new[] { "Neck", "mixamorig:Neck", "neck_01" } },
            { "Head", new[] { "Head", "mixamorig:Head", "head" } },
            { "LeftUpperArm", new[] { "LeftUpperArm", "LeftArm", "mixamorig:LeftArm", "upperarm_l" } },
            { "LeftLowerArm", new[] { "LeftLowerArm", "LeftForeArm", "mixamorig:LeftForeArm", "lowerarm_l" } },
            { "LeftHand", new[] { "LeftHand", "mixamorig:LeftHand", "hand_l" } },
            { "RightUpperArm", new[] { "RightUpperArm", "RightArm", "mixamorig:RightArm", "upperarm_r" } },
            { "RightLowerArm", new[] { "RightLowerArm", "RightForeArm", "mixamorig:RightForeArm", "lowerarm_r" } },
            { "RightHand", new[] { "RightHand", "mixamorig:RightHand", "hand_r" } },
            { "LeftUpperLeg", new[] { "LeftUpperLeg", "LeftUpLeg", "mixamorig:LeftUpLeg", "thigh_l" } },
            { "LeftLowerLeg", new[] { "LeftLowerLeg", "LeftLeg", "mixamorig:LeftLeg", "calf_l" } },
            { "LeftFoot", new[] { "LeftFoot", "mixamorig:LeftFoot", "foot_l" } },
            { "RightUpperLeg", new[] { "RightUpperLeg", "RightUpLeg", "mixamorig:RightUpLeg", "thigh_r" } },
            { "RightLowerLeg", new[] { "RightLowerLeg", "RightLeg", "mixamorig:RightLeg", "calf_r" } },
            { "RightFoot", new[] { "RightFoot", "mixamorig:RightFoot", "foot_r" } },
        };

        // ─── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Initialize from a rigged GLB that has a skeleton already embedded.
        /// The GLB should have been auto-rigged by UniRig or Mixamo.
        /// </summary>
        public void InitializeFromRiggedModel(GameObject modelRoot)
        {
            if (modelRoot == null) return;

            // Discover the skeleton from the loaded model
            DiscoverSkeleton(modelRoot.transform);

            // Find all existing SkinnedMeshRenderers and register them as body slots
            var renderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in renderers)
            {
                // Determine which body slot this renderer represents
                BodySlot slot = ClassifyRenderer(smr);
                RegisterSlot(slot, smr);
            }

            // If no existing SMRs found (static mesh), check for MeshRenderers
            if (renderers.Length == 0)
            {
                var meshRenderers = modelRoot.GetComponentsInChildren<MeshRenderer>();
                foreach (var mr in meshRenderers)
                {
                    Debug.LogWarning($"[ModularChar] Found static MeshRenderer '{mr.name}' — model needs rigging via UniRig");
                }
            }

            _initialized = true;
            Debug.Log($"[ModularChar] Initialized with {_boneMap.Count} bones, {_slots.Count} body slots");
        }

        /// <summary>
        /// Swap the mesh on a body slot. Used for equipment changes.
        /// The new mesh must be rigged to the same skeleton.
        /// </summary>
        public void SetSlotMesh(BodySlot slot, Mesh mesh, Material[] materials = null)
        {
            if (!_initialized) return;

            if (!_slots.TryGetValue(slot, out var state))
            {
                // Create new slot
                state = CreateSlot(slot);
            }

            state.CurrentMesh = mesh;
            state.IsEquipmentOverride = true;
            state.Renderer.sharedMesh = mesh;

            if (materials != null)
                state.Renderer.materials = materials;

            // Rebind bones to our shared skeleton
            RebindBones(state.Renderer);
        }

        /// <summary>
        /// Load a rigged GLB model and set it on a slot.
        /// Uses AssetLoader to find the GLB in pak/StreamingAssets/CDN.
        /// </summary>
        public void SetSlotFromAsset(BodySlot slot, string assetId)
        {
            if (!_initialized) return;

            var loader = World.AssetLoader.Instance;
            if (loader == null) return;

            var go = loader.TryLoadModel(assetId);
            if (go == null)
            {
                Debug.LogWarning($"[ModularChar] Asset '{assetId}' not found for slot {slot}");
                return;
            }

            // Extract mesh and materials from loaded model
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                SetSlotMesh(slot, smr.sharedMesh, smr.sharedMaterials);
            }
            else
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                {
                    SetSlotMesh(slot, mf.sharedMesh,
                        go.GetComponentInChildren<MeshRenderer>()?.sharedMaterials);
                }
            }

            // Clean up temporary loaded GO
            Destroy(go);
        }

        /// <summary>
        /// Reset a slot to its default body mesh (unequip).
        /// </summary>
        public void ResetSlot(BodySlot slot)
        {
            if (!_slots.TryGetValue(slot, out var state)) return;
            if (state.BaseMesh == null) return;

            state.CurrentMesh = state.BaseMesh;
            state.IsEquipmentOverride = false;
            state.Renderer.sharedMesh = state.BaseMesh;
            state.Renderer.materials = state.BaseMaterials;
        }

        /// <summary>
        /// Apply appearance data — drives blendshapes and material colors.
        /// Call whenever a customization slider changes.
        /// </summary>
        public void ApplyAppearance(AppearanceData appearance)
        {
            if (!_initialized || appearance == null) return;
            _appearance = appearance;

            foreach (var kvp in _slots)
            {
                var state = kvp.Value;
                if (state.Renderer == null) continue;

                // Apply blendshapes if the mesh has them
                ApplyBlendshapes(state, appearance);

                // Apply material colors
                ApplyMaterials(state, appearance);
            }
        }

        /// <summary>
        /// Get the Animator component for Mecanim animation control.
        /// </summary>
        public Animator GetAnimator()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            return _animator;
        }

        /// <summary>
        /// Get a bone transform by canonical name.
        /// </summary>
        public UnityEngine.Transform GetBone(string canonicalName)
        {
            _boneMap.TryGetValue(canonicalName, out var bone);
            return bone;
        }

        // ─── Skeleton Discovery ────────────────────────────────────────────

        private void DiscoverSkeleton(UnityEngine.Transform root)
        {
            _boneMap.Clear();
            var allTransforms = root.GetComponentsInChildren<UnityEngine.Transform>(true);

            // Try to find each bone by checking aliases
            foreach (var kvp in BoneAliases)
            {
                string canonical = kvp.Key;
                foreach (string alias in kvp.Value)
                {
                    foreach (var t in allTransforms)
                    {
                        // Match by exact name or name contains (for prefixed bones)
                        if (t.name == alias || t.name.EndsWith(alias))
                        {
                            _boneMap[canonical] = t;
                            break;
                        }
                    }
                    if (_boneMap.ContainsKey(canonical)) break;
                }
            }

            // Find root bone (usually Hips or Armature)
            if (_boneMap.TryGetValue("Hips", out var hips))
                _rootBone = hips;
            else
            {
                // Fallback: find first child named "Armature" or first bone-like object
                foreach (var t in allTransforms)
                {
                    if (t.name.Contains("Armature") || t.name.Contains("Root"))
                    {
                        _rootBone = t;
                        break;
                    }
                }
                if (_rootBone == null && allTransforms.Length > 1)
                    _rootBone = allTransforms[1]; // Skip the root itself
            }

            // Build ordered bone array from discovered bones
            var boneList = new List<UnityEngine.Transform>();
            foreach (var t in allTransforms)
            {
                if (t != root) boneList.Add(t);
            }
            _boneArray = boneList.ToArray();

            Debug.Log($"[ModularChar] Discovered {_boneMap.Count} named bones, " +
                      $"root='{_rootBone?.name}', total transforms={_boneArray?.Length}");
        }

        // ─── Slot Management ───────────────────────────────────────────────

        private void RegisterSlot(BodySlot slot, SkinnedMeshRenderer smr)
        {
            var state = new SlotState
            {
                Slot = slot,
                SlotObject = smr.gameObject,
                Renderer = smr,
                BaseMesh = smr.sharedMesh,
                CurrentMesh = smr.sharedMesh,
                BaseMaterials = smr.sharedMaterials,
                IsEquipmentOverride = false
            };
            _slots[slot] = state;
        }

        private SlotState CreateSlot(BodySlot slot)
        {
            var go = new GameObject($"Slot_{slot}");
            go.transform.SetParent(transform, false);

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.rootBone = _rootBone;
            smr.bones = _boneArray;
            smr.quality = SkinQuality.Auto;

            var state = new SlotState
            {
                Slot = slot,
                SlotObject = go,
                Renderer = smr,
                IsEquipmentOverride = false
            };
            _slots[slot] = state;
            return state;
        }

        private BodySlot ClassifyRenderer(SkinnedMeshRenderer smr)
        {
            string name = smr.gameObject.name.ToLower();

            if (name.Contains("head") || name.Contains("face")) return BodySlot.Head;
            if (name.Contains("hair")) return BodySlot.Hair;
            if (name.Contains("torso") || name.Contains("chest") || name.Contains("body") || name.Contains("upper"))
                return BodySlot.Torso;
            if (name.Contains("arm")) return BodySlot.Arms;
            if (name.Contains("leg") || name.Contains("lower") || name.Contains("pants"))
                return BodySlot.Legs;
            if (name.Contains("foot") || name.Contains("boot") || name.Contains("shoe"))
                return BodySlot.Feet;
            if (name.Contains("hand") || name.Contains("glove")) return BodySlot.Hands;

            // Default: if it's the only/main renderer, treat as full body torso
            return BodySlot.Torso;
        }

        // ─── Bone Rebinding ────────────────────────────────────────────────

        /// <summary>
        /// Rebind a SkinnedMeshRenderer's bones to our shared skeleton.
        /// This is the core of modular character systems — allows mesh swapping
        /// while keeping all meshes animated by the same skeleton.
        /// </summary>
        private void RebindBones(SkinnedMeshRenderer smr)
        {
            if (_boneArray == null || _boneArray.Length == 0) return;

            // Strategy 1: Match by bone name
            var meshBones = smr.bones;
            if (meshBones != null && meshBones.Length > 0)
            {
                var newBones = new UnityEngine.Transform[meshBones.Length];
                for (int i = 0; i < meshBones.Length; i++)
                {
                    if (meshBones[i] == null) continue;
                    string boneName = meshBones[i].name;

                    // Try exact match in our skeleton
                    if (_boneMap.TryGetValue(boneName, out var match))
                    {
                        newBones[i] = match;
                    }
                    else
                    {
                        // Try fuzzy match via aliases
                        foreach (var kvp in BoneAliases)
                        {
                            foreach (var alias in kvp.Value)
                            {
                                if (boneName == alias || boneName.EndsWith(alias))
                                {
                                    if (_boneMap.TryGetValue(kvp.Key, out var aliasMatch))
                                    {
                                        newBones[i] = aliasMatch;
                                        break;
                                    }
                                }
                            }
                            if (newBones[i] != null) break;
                        }
                    }

                    // Last resort: find by name in all transforms
                    if (newBones[i] == null)
                    {
                        foreach (var t in _boneArray)
                        {
                            if (t.name == boneName)
                            {
                                newBones[i] = t;
                                break;
                            }
                        }
                    }
                }

                smr.bones = newBones;
            }

            smr.rootBone = _rootBone;
        }

        // ─── Blendshape Application ────────────────────────────────────────

        private void ApplyBlendshapes(SlotState state, AppearanceData a)
        {
            var mesh = state.Renderer.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0) return;

            // Map our AppearanceData slider names to blendshape names
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shapeName = mesh.GetBlendShapeName(i).ToLower();
                float value = GetBlendshapeValue(shapeName, a);
                if (value >= 0f)
                    state.Renderer.SetBlendShapeWeight(i, value * 100f); // Unity uses 0-100
            }
        }

        private float GetBlendshapeValue(string shapeName, AppearanceData a)
        {
            // Map common blendshape names to our slider values
            if (shapeName.Contains("cheek") && shapeName.Contains("width")) return a.CheekboneWidth;
            if (shapeName.Contains("cheek") && shapeName.Contains("height")) return a.CheekboneHeight;
            if (shapeName.Contains("jaw") && shapeName.Contains("width")) return a.JawWidth;
            if (shapeName.Contains("jaw") && shapeName.Contains("angle")) return a.JawAngle;
            if (shapeName.Contains("chin") && shapeName.Contains("height")) return a.ChinHeight;
            if (shapeName.Contains("chin") && shapeName.Contains("width")) return a.ChinWidth;
            if (shapeName.Contains("chin") && shapeName.Contains("depth")) return a.ChinDepth;
            if (shapeName.Contains("nose") && shapeName.Contains("bridge")) return a.NoseBridgeWidth;
            if (shapeName.Contains("nose") && shapeName.Contains("tip")) return a.NoseTipWidth;
            if (shapeName.Contains("lip") && shapeName.Contains("upper")) return a.LipFullnessUpper;
            if (shapeName.Contains("lip") && shapeName.Contains("lower")) return a.LipFullnessLower;
            if (shapeName.Contains("lip") && shapeName.Contains("width")) return a.LipWidth;
            if (shapeName.Contains("eye") && shapeName.Contains("spacing")) return a.EyeSpacing;
            if (shapeName.Contains("eye") && shapeName.Contains("depth")) return a.EyeDepth;
            if (shapeName.Contains("eye") && shapeName.Contains("width")) return a.EyeWidth;
            if (shapeName.Contains("brow")) return a.BrowHeight;
            if (shapeName.Contains("forehead")) return a.ForeheadHeight;
            if (shapeName.Contains("shoulder")) return a.ShoulderWidth;
            if (shapeName.Contains("chest")) return a.ChestDepth;
            if (shapeName.Contains("hip")) return a.HipWidth;
            if (shapeName.Contains("waist")) return a.WaistWidth;
            if (shapeName.Contains("muscle")) return a.MuscleDefinition;
            if (shapeName.Contains("fat") || shapeName.Contains("weight")) return a.BodyFat;
            return -1f; // No match
        }

        // ─── Material Application ──────────────────────────────────────────

        private void ApplyMaterials(SlotState state, AppearanceData a)
        {
            foreach (var mat in state.Renderer.materials)
            {
                if (mat == null) continue;

                // Determine material type from name/slot
                switch (state.Slot)
                {
                    case BodySlot.Head:
                    case BodySlot.Torso:
                    case BodySlot.Arms:
                    case BodySlot.Legs:
                    case BodySlot.Hands:
                    case BodySlot.Feet:
                        // Skin material
                        if (mat.HasProperty("_Color"))
                            mat.color = a.SkinColor;
                        if (mat.HasProperty("_Glossiness"))
                            mat.SetFloat("_Glossiness", 1f - a.Roughness);
                        break;

                    case BodySlot.Hair:
                        if (mat.HasProperty("_Color"))
                            mat.color = a.HairColor;
                        break;
                }
            }
        }

        // ─── Equipment Integration ─────────────────────────────────────────

        /// <summary>
        /// Map an equipment slot ID (from inventory system) to a body slot.
        /// </summary>
        public static BodySlot? EquipSlotToBodySlot(int equipSlot)
        {
            return equipSlot switch
            {
                0 => BodySlot.Helmet,       // Head
                1 => BodySlot.ChestArmor,   // Chest
                2 => BodySlot.LegArmor,     // Legs
                3 => BodySlot.Boots,        // Feet
                4 => BodySlot.Gloves,       // Hands
                9 => BodySlot.Shoulders,    // Shoulders
                10 => BodySlot.Belt,        // Belt
                11 => BodySlot.Backpack,    // Backpack
                14 => BodySlot.WeaponLeft,  // LeftHand
                15 => BodySlot.WeaponRight, // RightHand
                _ => null
            };
        }

        /// <summary>
        /// Equip an item by asset ID. Determines the body slot from equipment slot.
        /// </summary>
        public void EquipItem(int equipSlot, string assetId)
        {
            var bodySlot = EquipSlotToBodySlot(equipSlot);
            if (bodySlot == null) return;

            if (string.IsNullOrEmpty(assetId))
            {
                ResetSlot(bodySlot.Value);
            }
            else
            {
                SetSlotFromAsset(bodySlot.Value, assetId);
            }
        }
    }
}

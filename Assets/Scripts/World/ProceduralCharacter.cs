using System.Collections.Generic;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Specification for building a procedural character.
    /// </summary>
    [System.Serializable]
    public class CharacterSpec
    {
        public float Height = 1.8f;
        public float BodyWidth = 0.4f;
        public float LimbThickness = 0.12f;
        public Color SkinColor = new Color(0.85f, 0.7f, 0.55f);
        public Color ShirtColor = new Color(0.3f, 0.3f, 0.6f);
        public Color PantsColor = new Color(0.25f, 0.2f, 0.15f);
        public string Archetype = "humanoid"; // humanoid, stocky, slender
    }

    /// <summary>
    /// Equipment slots for character gear (SWG-style 15 wearable slots).
    /// Values match proto EquipmentSlot enum (minus the NONE=0 sentinel).
    /// </summary>
    public enum EquipmentSlot
    {
        Head,           // Hat / helmet
        Chest,          // Undershirt / vest
        Legs,           // Underlegs / pants
        Feet,           // Shoes / boots
        Gloves,         // Gloves
        LeftBracer,     // Left forearm bracer
        RightBracer,    // Right forearm bracer
        LeftBicep,      // Left upper-arm armor
        RightBicep,     // Right upper-arm armor
        Shoulders,      // Shoulder pads / pauldrons
        Belt,           // Belt / utility belt
        Backpack,       // Backpack / cloak
        LeftWrist,      // Left wrist accessory
        RightWrist,     // Right wrist accessory
        // Weapon stance slots (mutually exclusive)
        LeftHand,       // One-handed weapon in left hand
        RightHand,      // One-handed weapon in right hand
        TwoHands        // Two-handed weapon (occupies both)
        // Unarmed = all three weapon slots empty
    }

    /// <summary>
    /// Builds complete humanoid characters entirely from code.
    /// Creates a Transform skeleton, SkinnedMeshRenderer with programmatic mesh,
    /// and bone weights computed per vertex.
    /// </summary>
    public class ProceduralCharacter : MonoBehaviour
    {
        private CharacterSpec _spec;
        private readonly Dictionary<string, Transform> _bones = new();
        private readonly Dictionary<EquipmentSlot, GameObject> _equipment = new();
        private SkinnedMeshRenderer _skinnedRenderer;

        // Bone names for external access
        public static readonly string BoneRoot = "Root";
        public static readonly string BoneHips = "Hips";
        public static readonly string BoneSpine = "Spine";
        public static readonly string BoneChest = "Chest";
        public static readonly string BoneNeck = "Neck";
        public static readonly string BoneHead = "Head";
        public static readonly string BoneLeftUpperArm = "LeftUpperArm";
        public static readonly string BoneLeftLowerArm = "LeftLowerArm";
        public static readonly string BoneLeftHand = "LeftHand";
        public static readonly string BoneRightUpperArm = "RightUpperArm";
        public static readonly string BoneRightLowerArm = "RightLowerArm";
        public static readonly string BoneRightHand = "RightHand";
        public static readonly string BoneLeftUpperLeg = "LeftUpperLeg";
        public static readonly string BoneLeftLowerLeg = "LeftLowerLeg";
        public static readonly string BoneLeftFoot = "LeftFoot";
        public static readonly string BoneRightUpperLeg = "RightUpperLeg";
        public static readonly string BoneRightLowerLeg = "RightLowerLeg";
        public static readonly string BoneRightFoot = "RightFoot";

        /// <summary>
        /// Build the complete character from a spec.
        /// </summary>
        public void Build(CharacterSpec spec)
        {
            _spec = spec ?? new CharacterSpec();

            // Apply archetype modifiers
            float widthMult = 1f;
            float limbMult = 1f;
            switch (_spec.Archetype)
            {
                case "stocky":
                    widthMult = 1.3f;
                    limbMult = 1.15f;
                    break;
                case "slender":
                    widthMult = 0.8f;
                    limbMult = 0.85f;
                    break;
            }
            _spec.BodyWidth *= widthMult;
            _spec.LimbThickness *= limbMult;

            BuildSkeleton();
            BuildSkinnedMesh();
        }

        private void BuildSkeleton()
        {
            float h = _spec.Height;
            float hipHeight = h * 0.5f;
            float spineHeight = h * 0.15f;
            float chestHeight = h * 0.15f;
            float neckHeight = h * 0.05f;
            float headHeight = h * 0.12f;
            float upperLegLen = h * 0.25f;
            float lowerLegLen = h * 0.23f;
            float upperArmLen = h * 0.16f;
            float lowerArmLen = h * 0.15f;
            float shoulderWidth = _spec.BodyWidth * 1.1f;

            var root = CreateBone(BoneRoot, transform, Vector3.zero);
            var hips = CreateBone(BoneHips, root, new Vector3(0, hipHeight, 0));
            var spine = CreateBone(BoneSpine, hips, new Vector3(0, spineHeight * 0.5f, 0));
            var chest = CreateBone(BoneChest, spine, new Vector3(0, chestHeight, 0));
            var neck = CreateBone(BoneNeck, chest, new Vector3(0, neckHeight + chestHeight * 0.3f, 0));
            CreateBone(BoneHead, neck, new Vector3(0, headHeight * 0.5f, 0));

            // Left arm
            var lUpperArm = CreateBone(BoneLeftUpperArm, chest, new Vector3(-shoulderWidth, chestHeight * 0.2f, 0));
            var lLowerArm = CreateBone(BoneLeftLowerArm, lUpperArm, new Vector3(0, -upperArmLen, 0));
            CreateBone(BoneLeftHand, lLowerArm, new Vector3(0, -lowerArmLen, 0));

            // Right arm
            var rUpperArm = CreateBone(BoneRightUpperArm, chest, new Vector3(shoulderWidth, chestHeight * 0.2f, 0));
            var rLowerArm = CreateBone(BoneRightLowerArm, rUpperArm, new Vector3(0, -upperArmLen, 0));
            CreateBone(BoneRightHand, rLowerArm, new Vector3(0, -lowerArmLen, 0));

            // Left leg
            var lUpperLeg = CreateBone(BoneLeftUpperLeg, hips, new Vector3(-_spec.BodyWidth * 0.35f, 0, 0));
            var lLowerLeg = CreateBone(BoneLeftLowerLeg, lUpperLeg, new Vector3(0, -upperLegLen, 0));
            CreateBone(BoneLeftFoot, lLowerLeg, new Vector3(0, -lowerLegLen, 0));

            // Right leg
            var rUpperLeg = CreateBone(BoneRightUpperLeg, hips, new Vector3(_spec.BodyWidth * 0.35f, 0, 0));
            var rLowerLeg = CreateBone(BoneRightLowerLeg, rUpperLeg, new Vector3(0, -upperLegLen, 0));
            CreateBone(BoneRightFoot, rLowerLeg, new Vector3(0, -lowerLegLen, 0));
        }

        private Transform CreateBone(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            _bones[name] = go.transform;
            return go.transform;
        }

        private void BuildSkinnedMesh()
        {
            float h = _spec.Height;
            float bw = _spec.BodyWidth;
            float lt = _spec.LimbThickness;

            // Collect mesh parts with bone assignments
            var parts = new List<(Mesh mesh, Matrix4x4 transform, string boneName, Color color)>();

            // Torso — main body cylinder from hips to chest
            float torsoHeight = h * 0.3f;
            var torso = ProceduralMeshBuilder.BuildCylinder(bw * 0.45f, bw * 0.5f, torsoHeight, 8);
            parts.Add((torso, Matrix4x4.identity, BoneSpine, _spec.ShirtColor));

            // Head sphere
            float headR = h * 0.07f;
            var head = ProceduralMeshBuilder.BuildSphere(headR, 8, 10);
            parts.Add((head, Matrix4x4.Translate(GetBoneWorldOffset(BoneHead)), BoneHead, _spec.SkinColor));

            // Neck cylinder
            var neck = ProceduralMeshBuilder.BuildCylinder(lt * 0.5f, lt * 0.5f, h * 0.04f, 6);
            parts.Add((neck, Matrix4x4.Translate(GetBoneWorldOffset(BoneNeck)), BoneNeck, _spec.SkinColor));

            // Arms
            float armLen = h * 0.16f;
            AddLimbParts(parts, BoneLeftUpperArm, BoneLeftLowerArm, lt * 0.4f, armLen, _spec.ShirtColor, _spec.SkinColor);
            AddLimbParts(parts, BoneRightUpperArm, BoneRightLowerArm, lt * 0.4f, armLen, _spec.ShirtColor, _spec.SkinColor);

            // Hands
            float handR = lt * 0.35f;
            var leftHand = ProceduralMeshBuilder.BuildSphere(handR, 5, 6);
            parts.Add((leftHand, Matrix4x4.Translate(GetBoneWorldOffset(BoneLeftHand)), BoneLeftHand, _spec.SkinColor));
            var rightHand = ProceduralMeshBuilder.BuildSphere(handR, 5, 6);
            parts.Add((rightHand, Matrix4x4.Translate(GetBoneWorldOffset(BoneRightHand)), BoneRightHand, _spec.SkinColor));

            // Legs
            float legLen = h * 0.25f;
            AddLimbParts(parts, BoneLeftUpperLeg, BoneLeftLowerLeg, lt * 0.45f, legLen, _spec.PantsColor, _spec.PantsColor);
            AddLimbParts(parts, BoneRightUpperLeg, BoneRightLowerLeg, lt * 0.45f, legLen, _spec.PantsColor, _spec.PantsColor);

            // Feet
            var leftFoot = ProceduralMeshBuilder.BuildBox(new Vector3(lt * 0.8f, lt * 0.3f, lt * 1.2f));
            parts.Add((leftFoot, Matrix4x4.Translate(GetBoneWorldOffset(BoneLeftFoot)), BoneLeftFoot, new Color(0.2f, 0.15f, 0.1f)));
            var rightFoot = ProceduralMeshBuilder.BuildBox(new Vector3(lt * 0.8f, lt * 0.3f, lt * 1.2f));
            parts.Add((rightFoot, Matrix4x4.Translate(GetBoneWorldOffset(BoneRightFoot)), BoneRightFoot, new Color(0.2f, 0.15f, 0.1f)));

            // Build the combined skinned mesh
            BuildCombinedSkinnedMesh(parts);
        }

        private void AddLimbParts(List<(Mesh, Matrix4x4, string, Color)> parts,
            string upperBone, string lowerBone, float radius, float length,
            Color upperColor, Color lowerColor)
        {
            var upper = ProceduralMeshBuilder.BuildCylinder(radius * 0.85f, radius, length, 6);
            parts.Add((upper, Matrix4x4.Translate(GetBoneWorldOffset(upperBone)), upperBone, upperColor));

            var lower = ProceduralMeshBuilder.BuildCylinder(radius * 0.7f, radius * 0.85f, length * 0.9f, 6);
            parts.Add((lower, Matrix4x4.Translate(GetBoneWorldOffset(lowerBone)), lowerBone, lowerColor));
        }

        private Vector3 GetBoneWorldOffset(string boneName)
        {
            if (!_bones.TryGetValue(boneName, out var bone)) return Vector3.zero;
            // Offset relative to root
            return bone.position - transform.position;
        }

        private void BuildCombinedSkinnedMesh(List<(Mesh mesh, Matrix4x4 transform, string boneName, Color color)> parts)
        {
            // Build ordered bone array
            string[] boneNames = {
                BoneRoot, BoneHips, BoneSpine, BoneChest, BoneNeck, BoneHead,
                BoneLeftUpperArm, BoneLeftLowerArm, BoneLeftHand,
                BoneRightUpperArm, BoneRightLowerArm, BoneRightHand,
                BoneLeftUpperLeg, BoneLeftLowerLeg, BoneLeftFoot,
                BoneRightUpperLeg, BoneRightLowerLeg, BoneRightFoot
            };

            var boneTransforms = new Transform[boneNames.Length];
            var bindposes = new Matrix4x4[boneNames.Length];
            var boneIndexMap = new Dictionary<string, int>();

            for (int i = 0; i < boneNames.Length; i++)
            {
                boneTransforms[i] = _bones[boneNames[i]];
                bindposes[i] = boneTransforms[i].worldToLocalMatrix * transform.localToWorldMatrix;
                boneIndexMap[boneNames[i]] = i;
            }

            // Merge all mesh parts
            var allVertices = new List<Vector3>();
            var allNormals = new List<Vector3>();
            var allUVs = new List<Vector2>();
            var allTriangles = new List<int>();
            var allBoneWeights = new List<BoneWeight>();
            var allColors = new List<Color>();

            int vertexOffset = 0;
            foreach (var (mesh, mat, boneName, color) in parts)
            {
                var srcVerts = mesh.vertices;
                var srcNormals = mesh.normals;
                var srcUVs = mesh.uv;
                var srcTris = mesh.triangles;

                int boneIndex = boneIndexMap.ContainsKey(boneName) ? boneIndexMap[boneName] : 0;

                // Find nearest bone for each vertex (simple single-bone weighting)
                for (int i = 0; i < srcVerts.Length; i++)
                {
                    Vector3 worldVert = mat.MultiplyPoint3x4(srcVerts[i]);
                    allVertices.Add(worldVert);
                    allNormals.Add(srcNormals != null && i < srcNormals.Length
                        ? mat.MultiplyVector(srcNormals[i]).normalized
                        : Vector3.up);
                    allUVs.Add(srcUVs != null && i < srcUVs.Length ? srcUVs[i] : Vector2.zero);
                    allColors.Add(color);

                    // Find nearest bone for this vertex
                    int nearestBone = FindNearestBone(worldVert + transform.position, boneTransforms);

                    allBoneWeights.Add(new BoneWeight
                    {
                        boneIndex0 = nearestBone,
                        weight0 = 0.7f,
                        boneIndex1 = boneIndex,
                        weight1 = 0.3f,
                        boneIndex2 = 0,
                        weight2 = 0f,
                        boneIndex3 = 0,
                        weight3 = 0f
                    });
                }

                for (int i = 0; i < srcTris.Length; i++)
                    allTriangles.Add(srcTris[i] + vertexOffset);

                vertexOffset += srcVerts.Length;
            }

            // Create the mesh
            var combinedMesh = new Mesh { name = "CharacterMesh" };
            if (allVertices.Count > 65535)
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            combinedMesh.SetVertices(allVertices);
            combinedMesh.SetNormals(allNormals);
            combinedMesh.SetUVs(0, allUVs);
            combinedMesh.SetColors(allColors);
            combinedMesh.SetTriangles(allTriangles, 0);
            combinedMesh.boneWeights = allBoneWeights.ToArray();
            combinedMesh.bindposes = bindposes;
            combinedMesh.RecalculateBounds();

            // Create SkinnedMeshRenderer
            _skinnedRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            _skinnedRenderer.sharedMesh = combinedMesh;
            _skinnedRenderer.bones = boneTransforms;
            _skinnedRenderer.rootBone = _bones[BoneRoot];

            // Material with vertex colors
            var mat2 = new Material(Shader.Find("Standard"));
            // Enable vertex color support (particles/standard unlit would be better but Standard works)
            _skinnedRenderer.material = mat2;

            // Set bounds large enough for animation
            _skinnedRenderer.localBounds = new Bounds(
                new Vector3(0, _spec.Height * 0.5f, 0),
                new Vector3(_spec.BodyWidth * 3f, _spec.Height * 1.2f, _spec.BodyWidth * 3f));
        }

        private int FindNearestBone(Vector3 worldPos, Transform[] bones)
        {
            int nearest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < bones.Length; i++)
            {
                float dist = Vector3.Distance(worldPos, bones[i].position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get a bone transform by name.
        /// </summary>
        public Transform GetBone(string boneName)
        {
            _bones.TryGetValue(boneName, out var bone);
            return bone;
        }

        /// <summary>
        /// Equip a GameObject to a slot by attaching it to the appropriate bone.
        /// </summary>
        public void Equip(EquipmentSlot slot, GameObject equipment)
        {
            // Remove existing equipment in slot
            Unequip(slot);

            Transform bone = GetBoneForSlot(slot);
            if (bone == null) return;

            equipment.transform.SetParent(bone);
            equipment.transform.localPosition = Vector3.zero;
            equipment.transform.localRotation = Quaternion.identity;

            _equipment[slot] = equipment;
        }

        /// <summary>
        /// Remove equipment from a slot.
        /// </summary>
        public void Unequip(EquipmentSlot slot)
        {
            if (_equipment.TryGetValue(slot, out var existing))
            {
                if (existing != null) Object.Destroy(existing);
                _equipment.Remove(slot);
            }
        }

        /// <summary>
        /// Get the character spec.
        /// </summary>
        public CharacterSpec GetSpec() => _spec;

        private Transform GetBoneForSlot(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head:         return GetBone(BoneHead);
                case EquipmentSlot.Chest:        return GetBone(BoneChest);
                case EquipmentSlot.Legs:         return GetBone(BoneHips);
                case EquipmentSlot.Feet:         return GetBone(BoneLeftFoot);  // TODO: dual-foot attach
                case EquipmentSlot.Gloves:       return GetBone(BoneLeftHand);  // TODO: dual-hand attach
                case EquipmentSlot.LeftBracer:   return GetBone(BoneLeftLowerArm);
                case EquipmentSlot.RightBracer:  return GetBone(BoneRightLowerArm);
                case EquipmentSlot.LeftBicep:    return GetBone(BoneLeftUpperArm);
                case EquipmentSlot.RightBicep:   return GetBone(BoneRightUpperArm);
                case EquipmentSlot.Shoulders:    return GetBone(BoneChest);     // offset up at attach time
                case EquipmentSlot.Belt:         return GetBone(BoneHips);      // offset at attach time
                case EquipmentSlot.Backpack:     return GetBone(BoneSpine);
                case EquipmentSlot.LeftWrist:    return GetBone(BoneLeftLowerArm);  // offset at attach time
                case EquipmentSlot.RightWrist:   return GetBone(BoneRightLowerArm); // offset at attach time
                case EquipmentSlot.LeftHand:     return GetBone(BoneLeftHand);
                case EquipmentSlot.RightHand:    return GetBone(BoneRightHand);
                case EquipmentSlot.TwoHands:     return GetBone(BoneRightHand);  // Primary grip hand
                default: return null;
            }
        }
    }
}

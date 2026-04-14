using System.Collections.Generic;
using UnityEngine;

namespace Orlo.Animation
{
    /// <summary>
    /// Attaches a runtime humanoid skeleton to a static mesh loaded from GLB.
    /// Converts MeshFilter+MeshRenderer to SkinnedMeshRenderer with bone weights
    /// computed by proximity. Matches ProceduralCharacter's bone naming convention.
    /// </summary>
    public static class RuntimeRigBuilder
    {
        private static readonly string[] BoneNames =
        {
            "Root", "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot"
        };

        /// <summary>
        /// Build a humanoid skeleton on the model root and convert all child
        /// meshes to skinned meshes with bone weights.
        /// </summary>
        public static Transform[] BuildHumanoidRig(GameObject modelRoot, float modelHeight)
        {
            if (modelHeight <= 0.01f) modelHeight = 1.8f;
            float h = modelHeight;
            float bw = h * 0.22f; // body width proportional to height

            // Create bone hierarchy
            var bones = new Dictionary<string, Transform>();
            var root = CreateBone("Root", modelRoot.transform, Vector3.zero);
            bones["Root"] = root;

            var hips = CreateBone("Hips", root, new Vector3(0, h * 0.5f, 0));
            bones["Hips"] = hips;

            var spine = CreateBone("Spine", hips, new Vector3(0, h * 0.075f, 0));
            bones["Spine"] = spine;

            var chest = CreateBone("Chest", spine, new Vector3(0, h * 0.15f, 0));
            bones["Chest"] = chest;

            var neck = CreateBone("Neck", chest, new Vector3(0, h * 0.095f, 0));
            bones["Neck"] = neck;

            var head = CreateBone("Head", neck, new Vector3(0, h * 0.06f, 0));
            bones["Head"] = head;

            // Arms
            float shoulderW = bw * 1.1f;
            float upperArmLen = h * 0.16f;
            float lowerArmLen = h * 0.15f;

            bones["LeftUpperArm"] = CreateBone("LeftUpperArm", chest, new Vector3(-shoulderW, h * 0.03f, 0));
            bones["LeftLowerArm"] = CreateBone("LeftLowerArm", bones["LeftUpperArm"], new Vector3(0, -upperArmLen, 0));
            bones["LeftHand"] = CreateBone("LeftHand", bones["LeftLowerArm"], new Vector3(0, -lowerArmLen, 0));

            bones["RightUpperArm"] = CreateBone("RightUpperArm", chest, new Vector3(shoulderW, h * 0.03f, 0));
            bones["RightLowerArm"] = CreateBone("RightLowerArm", bones["RightUpperArm"], new Vector3(0, -upperArmLen, 0));
            bones["RightHand"] = CreateBone("RightHand", bones["RightLowerArm"], new Vector3(0, -lowerArmLen, 0));

            // Legs
            float legOffset = bw * 0.35f;
            float upperLegLen = h * 0.25f;
            float lowerLegLen = h * 0.23f;

            bones["LeftUpperLeg"] = CreateBone("LeftUpperLeg", hips, new Vector3(-legOffset, 0, 0));
            bones["LeftLowerLeg"] = CreateBone("LeftLowerLeg", bones["LeftUpperLeg"], new Vector3(0, -upperLegLen, 0));
            bones["LeftFoot"] = CreateBone("LeftFoot", bones["LeftLowerLeg"], new Vector3(0, -lowerLegLen, 0));

            bones["RightUpperLeg"] = CreateBone("RightUpperLeg", hips, new Vector3(legOffset, 0, 0));
            bones["RightLowerLeg"] = CreateBone("RightLowerLeg", bones["RightUpperLeg"], new Vector3(0, -upperLegLen, 0));
            bones["RightFoot"] = CreateBone("RightFoot", bones["RightLowerLeg"], new Vector3(0, -lowerLegLen, 0));

            // Build ordered bone array
            var boneArray = new Transform[BoneNames.Length];
            for (int i = 0; i < BoneNames.Length; i++)
                boneArray[i] = bones[BoneNames[i]];

            // Compute bind poses
            var bindPoses = new Matrix4x4[boneArray.Length];
            for (int i = 0; i < boneArray.Length; i++)
                bindPoses[i] = boneArray[i].worldToLocalMatrix * modelRoot.transform.localToWorldMatrix;

            // Convert all child meshes to skinned
            var filters = modelRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                ConvertToSkinned(mf.gameObject, mf.sharedMesh, mr.sharedMaterials,
                    boneArray, bindPoses, root);

                Object.Destroy(mr);
                Object.Destroy(mf);
            }

            return boneArray;
        }

        private static void ConvertToSkinned(GameObject go, Mesh originalMesh,
            Material[] materials, Transform[] bones, Matrix4x4[] bindPoses, Transform rootBone)
        {
            if (originalMesh == null) return;

            // Clone mesh to add bone data (Instantiate preserves blendshapes)
            var mesh = Object.Instantiate(originalMesh);
            mesh.name = originalMesh.name + "_skinned";

            // Safety net: if Instantiate didn't copy blendshapes, copy them explicitly
            if (originalMesh.blendShapeCount > 0 && mesh.blendShapeCount == 0)
            {
                Debug.Log($"[RuntimeRigBuilder] Explicitly copying {originalMesh.blendShapeCount} blendshapes");
                for (int bs = 0; bs < originalMesh.blendShapeCount; bs++)
                {
                    string shapeName = originalMesh.GetBlendShapeName(bs);
                    int frameCount = originalMesh.GetBlendShapeFrameCount(bs);
                    for (int f = 0; f < frameCount; f++)
                    {
                        float weight = originalMesh.GetBlendShapeFrameWeight(bs, f);
                        var dv = new Vector3[originalMesh.vertexCount];
                        var dn = new Vector3[originalMesh.vertexCount];
                        var dt = new Vector3[originalMesh.vertexCount];
                        originalMesh.GetBlendShapeFrameVertices(bs, f, dv, dn, dt);
                        mesh.AddBlendShapeFrame(shapeName, weight, dv, dn, dt);
                    }
                }
            }

            if (mesh.blendShapeCount > 0)
                Debug.Log($"[RuntimeRigBuilder] Rigging mesh '{mesh.name}' with {mesh.blendShapeCount} blendshapes");

            // Compute bone weights per vertex
            var vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            var worldMatrix = go.transform.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = worldMatrix.MultiplyPoint3x4(vertices[i]);
                weights[i] = ComputeBoneWeight(worldPos, bones);
            }

            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = rootBone;
            smr.materials = materials;

            // Set bounds generous enough to avoid culling
            float height = rootBone.parent != null ? 2f : 2f;
            smr.localBounds = new Bounds(
                new Vector3(0, height * 0.5f, 0),
                new Vector3(2f, height * 1.2f, 2f));
        }

        /// <summary>
        /// Region-based bone weighting. Assigns vertices to bones based on
        /// body region (height + lateral offset) rather than pure proximity.
        /// This prevents the "split in half" bug where torso vertices get
        /// assigned to leg bones due to proximity in model space.
        /// </summary>
        private static BoneWeight ComputeBoneWeight(Vector3 worldPos, Transform[] bones)
        {
            // Get model bounds center for relative positioning
            Vector3 hipsPos = bones[1].position;  // Hips
            float modelHeight = (bones[5].position.y - bones[14].position.y); // Head to foot
            if (modelHeight < 0.1f) modelHeight = 1.8f;

            // Normalize vertex position relative to hips
            float relY = (worldPos.y - hipsPos.y) / modelHeight; // -0.5 (feet) to +0.5 (head)
            float relX = (worldPos.x - hipsPos.x) / modelHeight; // lateral offset

            int primary, secondary;
            float primaryWeight;

            // === Body region classification ===
            if (relY > 0.35f)
            {
                // HEAD region
                primary = 5; // Head
                secondary = 4; // Neck
                primaryWeight = 0.85f;
            }
            else if (relY > 0.28f)
            {
                // NECK region
                primary = 4; // Neck
                secondary = 3; // Chest
                float t = (relY - 0.28f) / 0.07f;
                primaryWeight = 0.5f + t * 0.3f;
            }
            else if (relY > 0.12f)
            {
                // CHEST + SHOULDERS region
                if (Mathf.Abs(relX) > 0.08f)
                {
                    // Shoulder/arm area
                    bool left = relX < 0;
                    primary = left ? 6 : 9; // UpperArm
                    secondary = 3; // Chest
                    primaryWeight = Mathf.Clamp01(Mathf.Abs(relX) / 0.15f) * 0.7f + 0.15f;
                }
                else
                {
                    primary = 3; // Chest
                    secondary = 2; // Spine
                    float t = (relY - 0.12f) / 0.16f;
                    primaryWeight = 0.6f + t * 0.2f;
                }
            }
            else if (relY > -0.02f)
            {
                // SPINE / WAIST region
                if (Mathf.Abs(relX) > 0.12f)
                {
                    // Arm region (lower)
                    bool left = relX < 0;
                    if (relY > 0.06f)
                    {
                        primary = left ? 7 : 10; // LowerArm
                        secondary = left ? 6 : 9; // UpperArm
                    }
                    else
                    {
                        primary = left ? 8 : 11; // Hand
                        secondary = left ? 7 : 10; // LowerArm
                    }
                    primaryWeight = 0.75f;
                }
                else
                {
                    primary = 2; // Spine
                    secondary = 1; // Hips
                    float t = (relY + 0.02f) / 0.14f;
                    primaryWeight = 0.5f + t * 0.3f;
                }
            }
            else if (relY > -0.18f)
            {
                // HIPS / UPPER LEG region
                primary = 1; // Hips
                if (Mathf.Abs(relX) > 0.03f)
                {
                    bool left = relX < 0;
                    secondary = left ? 12 : 15; // UpperLeg
                    float t = Mathf.Clamp01((-relY) / 0.18f);
                    primaryWeight = 1f - t * 0.6f; // Blend toward leg as we go down
                }
                else
                {
                    secondary = 2; // Spine
                    primaryWeight = 0.7f;
                }
            }
            else if (relY > -0.35f)
            {
                // UPPER LEG region
                bool left = relX < 0;
                primary = left ? 12 : 15; // UpperLeg
                secondary = left ? 13 : 16; // LowerLeg
                float t = (relY + 0.18f) / (-0.17f); // 0 at top, 1 at bottom
                primaryWeight = 0.8f - Mathf.Abs(t) * 0.3f;
            }
            else if (relY > -0.48f)
            {
                // LOWER LEG region
                bool left = relX < 0;
                primary = left ? 13 : 16; // LowerLeg
                secondary = left ? 14 : 17; // Foot
                float t = (relY + 0.35f) / (-0.13f);
                primaryWeight = 0.75f - Mathf.Abs(t) * 0.25f;
            }
            else
            {
                // FOOT region
                bool left = relX < 0;
                primary = left ? 14 : 17; // Foot
                secondary = left ? 13 : 16; // LowerLeg
                primaryWeight = 0.9f;
            }

            // Clamp indices
            primary = Mathf.Clamp(primary, 0, bones.Length - 1);
            secondary = Mathf.Clamp(secondary, 0, bones.Length - 1);
            primaryWeight = Mathf.Clamp01(primaryWeight);

            return new BoneWeight
            {
                boneIndex0 = primary, weight0 = primaryWeight,
                boneIndex1 = secondary, weight1 = 1f - primaryWeight,
                boneIndex2 = 0, weight2 = 0,
                boneIndex3 = 0, weight3 = 0
            };
        }

        private static Transform CreateBone(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}

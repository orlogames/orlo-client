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

            // Clone mesh to add bone data
            var mesh = Object.Instantiate(originalMesh);
            mesh.name = originalMesh.name + "_skinned";

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

        private static BoneWeight ComputeBoneWeight(Vector3 worldPos, Transform[] bones)
        {
            // Find two nearest bones
            int best0 = 0, best1 = 1;
            float dist0 = float.MaxValue, dist1 = float.MaxValue;

            for (int i = 0; i < bones.Length; i++)
            {
                float d = Vector3.Distance(worldPos, bones[i].position);
                if (d < dist0)
                {
                    best1 = best0; dist1 = dist0;
                    best0 = i; dist0 = d;
                }
                else if (d < dist1)
                {
                    best1 = i; dist1 = d;
                }
            }

            // Weight by inverse distance
            float totalDist = dist0 + dist1;
            float w0 = totalDist > 0.001f ? 1f - (dist0 / totalDist) : 0.5f;
            float w1 = 1f - w0;

            return new BoneWeight
            {
                boneIndex0 = best0, weight0 = w0,
                boneIndex1 = best1, weight1 = w1,
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

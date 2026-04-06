using System.Collections.Generic;
using UnityEngine;
using Orlo.UI.CharacterCreation;

namespace Orlo.Animation
{
    /// <summary>
    /// Runtime vertex deformation system for character customization.
    /// Instead of pre-baked blendshapes, this deforms vertices mathematically
    /// based on body region classification and slider values from AppearanceData.
    ///
    /// Works with ANY base mesh — no special authoring needed.
    /// Each vertex is classified into a body region, then deformed based on
    /// which sliders affect that region.
    /// </summary>
    public class VertexDeformer : MonoBehaviour
    {
        private SkinnedMeshRenderer[] _renderers;
        private Mesh[] _originalMeshes;     // Undeformed copies
        private Mesh[] _deformedMeshes;     // Working copies
        private VertexRegion[][] _regionMaps; // Per-vertex region classification
        private bool _initialized;
        private AppearanceData _currentAppearance;

        // Body region classification
        private enum VertexRegion
        {
            Head, Forehead, Eyes, Nose, Mouth, Chin, Jaw, Cheeks, Ears,
            Neck, Chest, Shoulders, UpperArms, LowerArms, Hands,
            Spine, Waist, Hips,
            UpperLegs, LowerLegs, Feet
        }

        /// <summary>
        /// Initialize the deformer after the character mesh is loaded.
        /// Call once after RuntimeRigBuilder sets up the skinned meshes.
        /// </summary>
        public void Initialize()
        {
            _renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (_renderers.Length == 0) return;

            _originalMeshes = new Mesh[_renderers.Length];
            _deformedMeshes = new Mesh[_renderers.Length];
            _regionMaps = new VertexRegion[_renderers.Length][];

            for (int r = 0; r < _renderers.Length; r++)
            {
                var smr = _renderers[r];
                // Clone the original mesh for reference
                _originalMeshes[r] = Object.Instantiate(smr.sharedMesh);
                _originalMeshes[r].name = smr.sharedMesh.name + "_original";
                // Create working copy
                _deformedMeshes[r] = Object.Instantiate(smr.sharedMesh);
                _deformedMeshes[r].name = smr.sharedMesh.name + "_deformed";
                smr.sharedMesh = _deformedMeshes[r];

                // Classify vertices into body regions
                _regionMaps[r] = ClassifyVertices(_originalMeshes[r], smr);
            }

            _initialized = true;
        }

        /// <summary>
        /// Apply appearance data to deform the character mesh.
        /// Call whenever a slider changes or on initial load.
        /// </summary>
        public void ApplyAppearance(AppearanceData appearance)
        {
            if (!_initialized || appearance == null) return;
            _currentAppearance = appearance;

            for (int r = 0; r < _renderers.Length; r++)
            {
                var original = _originalMeshes[r].vertices;
                var deformed = new Vector3[original.Length];
                var regions = _regionMaps[r];

                for (int i = 0; i < original.Length; i++)
                {
                    deformed[i] = DeformVertex(original[i], regions[i], appearance);
                }

                _deformedMeshes[r].vertices = deformed;
                _deformedMeshes[r].RecalculateNormals();
                _deformedMeshes[r].RecalculateBounds();
            }

            // Apply skin color to materials
            ApplyMaterialColors(appearance);
        }

        private VertexRegion[] ClassifyVertices(Mesh mesh, SkinnedMeshRenderer smr)
        {
            var vertices = mesh.vertices;
            var regions = new VertexRegion[vertices.Length];
            var worldMatrix = smr.transform.localToWorldMatrix;

            // Get bounds for normalization
            Bounds bounds = mesh.bounds;
            float meshHeight = bounds.size.y;
            if (meshHeight < 0.1f) meshHeight = 1.8f;
            float meshCenter = bounds.center.y;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                // Normalize Y to 0 (feet) to 1 (head)
                float normY = (v.y - bounds.min.y) / meshHeight;
                // Normalize X for lateral offset (-1 to 1)
                float normX = bounds.size.x > 0.01f ? (v.x - bounds.center.x) / (bounds.size.x * 0.5f) : 0f;
                // Normalize Z for depth (-1 to 1)
                float normZ = bounds.size.z > 0.01f ? (v.z - bounds.center.z) / (bounds.size.z * 0.5f) : 0f;

                regions[i] = ClassifyVertex(normY, normX, normZ);
            }

            return regions;
        }

        private static VertexRegion ClassifyVertex(float normY, float normX, float normZ)
        {
            float absX = Mathf.Abs(normX);

            // Head region (top 15%)
            if (normY > 0.85f)
            {
                if (normY > 0.95f) return VertexRegion.Forehead;
                if (normZ > 0.2f) // Front of face
                {
                    if (normY > 0.92f) return VertexRegion.Forehead;
                    if (normY > 0.88f)
                    {
                        if (absX > 0.3f) return VertexRegion.Ears;
                        return VertexRegion.Eyes;
                    }
                    if (normY > 0.86f)
                    {
                        if (absX < 0.15f) return VertexRegion.Nose;
                        return VertexRegion.Cheeks;
                    }
                    if (absX < 0.2f) return VertexRegion.Mouth;
                    return VertexRegion.Jaw;
                }
                if (absX > 0.35f) return VertexRegion.Ears;
                return VertexRegion.Head;
            }

            // Chin / jaw (85-80%)
            if (normY > 0.80f)
            {
                if (normZ > 0.1f && absX < 0.25f) return VertexRegion.Chin;
                return VertexRegion.Jaw;
            }

            // Neck (80-75%)
            if (normY > 0.75f) return VertexRegion.Neck;

            // Shoulders + upper chest (75-65%)
            if (normY > 0.65f)
            {
                if (absX > 0.6f) return VertexRegion.Shoulders;
                return VertexRegion.Chest;
            }

            // Arms vs torso (65-50%)
            if (normY > 0.50f)
            {
                if (absX > 0.7f) return VertexRegion.UpperArms;
                if (absX > 0.5f) return VertexRegion.Shoulders;
                return VertexRegion.Chest;
            }

            // Waist / lower arms (50-45%)
            if (normY > 0.45f)
            {
                if (absX > 0.6f) return VertexRegion.LowerArms;
                return VertexRegion.Waist;
            }

            // Hands / hips (45-38%)
            if (normY > 0.38f)
            {
                if (absX > 0.7f) return VertexRegion.Hands;
                return VertexRegion.Hips;
            }

            // Upper legs (38-18%)
            if (normY > 0.18f) return VertexRegion.UpperLegs;

            // Lower legs (18-5%)
            if (normY > 0.05f) return VertexRegion.LowerLegs;

            // Feet
            return VertexRegion.Feet;
        }

        private Vector3 DeformVertex(Vector3 v, VertexRegion region, AppearanceData a)
        {
            // All sliders are 0-1, centered at 0.5. Convert to -1 to +1 range for deformation.
            Vector3 offset = Vector3.zero;

            switch (region)
            {
                case VertexRegion.Forehead:
                    offset.z += (a.ForeheadSlope - 0.5f) * 0.04f;
                    offset.y += (a.ForeheadHeight - 0.5f) * 0.03f;
                    offset.x *= 1f + (a.TempleWidth - 0.5f) * 0.15f;
                    break;

                case VertexRegion.Eyes:
                    offset.x += Mathf.Sign(v.x) * (a.EyeSpacing - 0.5f) * 0.02f;
                    offset.z += (a.EyeDepth - 0.5f) * 0.025f;
                    offset.y += (a.EyeHeight - 0.5f) * 0.015f;
                    // Scale eye area width
                    float eyeWidthScale = 1f + (a.EyeWidth - 0.5f) * 0.2f;
                    offset.x += (v.x > 0 ? 1 : -1) * (eyeWidthScale - 1f) * Mathf.Abs(v.x) * 0.3f;
                    break;

                case VertexRegion.Nose:
                    offset.x *= 1f + (a.NoseBridgeWidth - 0.5f) * 0.3f;
                    offset.y += (a.NoseBridgeHeight - 0.5f) * 0.02f;
                    offset.z += (a.NoseTipHeight - 0.5f) * 0.03f;
                    // Tip width
                    offset.x += Mathf.Sign(v.x) * (a.NoseTipWidth - 0.5f) * 0.015f;
                    // Nostril flare
                    offset.x += Mathf.Sign(v.x) * (a.NoseNostrilFlare - 0.5f) * 0.01f;
                    break;

                case VertexRegion.Mouth:
                    offset.y += (a.LipFullnessUpper - 0.5f) * 0.01f;
                    offset.y -= (a.LipFullnessLower - 0.5f) * 0.01f;
                    offset.x *= 1f + (a.LipWidth - 0.5f) * 0.2f;
                    break;

                case VertexRegion.Chin:
                    offset.y += (a.ChinHeight - 0.5f) * 0.025f;
                    offset.x *= 1f + (a.ChinWidth - 0.5f) * 0.25f;
                    offset.z += (a.ChinDepth - 0.5f) * 0.03f;
                    break;

                case VertexRegion.Jaw:
                    offset.x *= 1f + (a.JawWidth - 0.5f) * 0.2f;
                    offset.y += (a.JawAngle - 0.5f) * 0.015f;
                    // Roundness: pull corners in/out
                    float jawRound = (a.JawRoundness - 0.5f) * 0.02f;
                    offset.x += Mathf.Sign(v.x) * jawRound;
                    offset.z += jawRound * 0.5f;
                    break;

                case VertexRegion.Cheeks:
                    offset.y += (a.CheekboneHeight - 0.5f) * 0.015f;
                    offset.x *= 1f + (a.CheekboneWidth - 0.5f) * 0.2f;
                    break;

                case VertexRegion.Ears:
                    float earScale = 1f + (a.EarSize - 0.5f) * 0.4f;
                    offset.x *= earScale;
                    offset.y *= earScale;
                    break;

                case VertexRegion.Head:
                    offset.y += (a.CrownHeight - 0.5f) * 0.02f;
                    offset.x *= 1f + (a.TempleWidth - 0.5f) * 0.1f;
                    break;

                case VertexRegion.Neck:
                    // Neck scales slightly with build
                    float neckScale = 1f + (a.Build - 0.5f) * 0.15f;
                    offset.x *= neckScale;
                    offset.z *= neckScale;
                    break;

                case VertexRegion.Shoulders:
                    offset.x *= 1f + (a.ShoulderWidth - 0.5f) * 0.3f;
                    offset.x += Mathf.Sign(v.x) * (a.ShoulderWidth - 0.5f) * 0.05f;
                    break;

                case VertexRegion.Chest:
                    offset.z += (a.ChestDepth - 0.5f) * 0.04f;
                    offset.x *= 1f + (a.ShoulderWidth - 0.5f) * 0.15f;
                    // Muscle definition pushes chest forward slightly
                    offset.z += a.MuscleDefinition * 0.02f;
                    // Body fat expands chest
                    float chestFat = a.BodyFat * 0.03f;
                    offset.x += Mathf.Sign(v.x) * chestFat;
                    offset.z += chestFat;
                    break;

                case VertexRegion.UpperArms:
                    float armThick = 1f + (a.ArmThickness - 0.5f) * 0.4f;
                    offset.x *= armThick;
                    offset.z *= armThick;
                    // Arm length
                    offset.y += (a.ArmLength - 0.5f) * 0.03f * (v.y > 0 ? -1 : 1);
                    break;

                case VertexRegion.LowerArms:
                    float lowerArmThick = 1f + (a.ArmThickness - 0.5f) * 0.3f;
                    offset.x *= lowerArmThick;
                    offset.z *= lowerArmThick;
                    offset.y += (a.ArmLength - 0.5f) * 0.04f * (v.y > 0 ? -1 : 1);
                    break;

                case VertexRegion.Hands:
                    offset.y += (a.ArmLength - 0.5f) * 0.03f * (v.y > 0 ? -1 : 1);
                    break;

                case VertexRegion.Waist:
                    offset.x *= 1f + (a.WaistWidth - 0.5f) * 0.3f;
                    // Body fat
                    offset.x += Mathf.Sign(v.x) * a.BodyFat * 0.025f;
                    offset.z += a.BodyFat * 0.02f;
                    // Torso length
                    offset.y += (a.TorsoLength - 0.5f) * 0.02f;
                    break;

                case VertexRegion.Hips:
                    offset.x *= 1f + (a.HipWidth - 0.5f) * 0.3f;
                    offset.x += Mathf.Sign(v.x) * a.BodyFat * 0.02f;
                    break;

                case VertexRegion.UpperLegs:
                    float legThick = 1f + (a.LegThickness - 0.5f) * 0.35f;
                    offset.x *= legThick;
                    offset.z *= legThick;
                    // Leg length stretches Y
                    offset.y += (a.LegLength - 0.5f) * 0.04f * (v.y > 0 ? 1 : -1);
                    break;

                case VertexRegion.LowerLegs:
                    float lowerLegThick = 1f + (a.LegThickness - 0.5f) * 0.25f;
                    offset.x *= lowerLegThick;
                    offset.z *= lowerLegThick;
                    offset.y += (a.LegLength - 0.5f) * 0.05f * (v.y > 0 ? 1 : -1);
                    break;

                case VertexRegion.Feet:
                    offset.y += (a.LegLength - 0.5f) * 0.03f * (v.y > 0 ? 1 : -1);
                    break;
            }

            // Global height scaling
            float heightScale = 1f + (a.Height - 0.5f) * 0.3f; // ±15%
            Vector3 result = v + offset;
            result.y *= heightScale;

            return result;
        }

        private void ApplyMaterialColors(AppearanceData a)
        {
            if (_renderers == null) return;

            foreach (var smr in _renderers)
            {
                foreach (var mat in smr.materials)
                {
                    // Apply skin color to base color
                    if (mat.HasProperty("_Color"))
                        mat.color = a.SkinColor;
                    // Roughness
                    if (mat.HasProperty("_Glossiness"))
                        mat.SetFloat("_Glossiness", 1f - a.Roughness);
                }
            }
        }

        /// <summary>
        /// Reset mesh to original undeformed state.
        /// </summary>
        public void ResetToOriginal()
        {
            if (!_initialized) return;
            for (int r = 0; r < _renderers.Length; r++)
            {
                _deformedMeshes[r].vertices = _originalMeshes[r].vertices;
                _deformedMeshes[r].RecalculateNormals();
                _deformedMeshes[r].RecalculateBounds();
            }
        }

        private void OnDestroy()
        {
            if (_originalMeshes != null)
                foreach (var m in _originalMeshes)
                    if (m != null) Destroy(m);
            if (_deformedMeshes != null)
                foreach (var m in _deformedMeshes)
                    if (m != null) Destroy(m);
        }
    }
}

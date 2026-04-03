using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using GLTFast;
using Orlo.UI.CharacterCreation;

namespace Orlo.World
{
    /// <summary>
    /// Loads a real 3D character model from a GLB file and drives material colors
    /// from AppearanceData. Replaces ProceduralCharacter for player characters.
    ///
    /// Phase 0: Single model, color-driven materials only (no blendshapes yet).
    /// </summary>
    public class ModelCharacter : MonoBehaviour
    {
        private GameObject _modelRoot;
        private Renderer[] _renderers;
        private Material[] _instancedMaterials;
        private bool _loaded;

        /// <summary>Whether the model has finished loading.</summary>
        public bool IsLoaded => _loaded;

        /// <summary>
        /// Load a GLB model from StreamingAssets and apply initial appearance.
        /// </summary>
        public async Task LoadModel(string glbFileName, AppearanceData initialAppearance = null)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Characters", glbFileName);

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ModelCharacter] GLB not found at {path}, using fallback");
                CreateFallbackPrimitive();
                return;
            }

            var gltf = new GltfImport();
            bool success = await gltf.Load(path);

            if (!success)
            {
                Debug.LogError($"[ModelCharacter] Failed to load GLB: {path}");
                CreateFallbackPrimitive();
                return;
            }

            // Instantiate under this transform
            _modelRoot = new GameObject("CharacterModel");
            _modelRoot.transform.SetParent(transform, false);

            await gltf.InstantiateMainSceneAsync(_modelRoot.transform);

            // Collect all renderers and create instanced materials
            _renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
            CacheInstancedMaterials();

            Debug.Log($"[ModelCharacter] Loaded {glbFileName}: {_renderers.Length} renderers, " +
                      $"{_instancedMaterials.Length} materials");

            _loaded = true;

            if (initialAppearance != null)
                UpdateAppearance(initialAppearance);
        }

        /// <summary>
        /// Update material colors from AppearanceData. Instant — no mesh rebuild needed.
        /// </summary>
        public void UpdateAppearance(AppearanceData data)
        {
            if (_instancedMaterials == null || _instancedMaterials.Length == 0) return;

            // Convert AppearanceData color fields to Unity colors
            Color skinColor = data.SkinColor != default ? data.SkinColor : new Color(0.76f, 0.59f, 0.42f);
            Color hairColor = data.HairPrimaryColor != default ? data.HairPrimaryColor : new Color(0.2f, 0.15f, 0.1f);
            Color eyeColor = data.LeftEyeColor != default ? data.LeftEyeColor : new Color(0.3f, 0.5f, 0.3f);

            // Apply skin tint to all materials as a base (Phase 0 — single material model)
            // Future phases will identify material slots by name/index
            foreach (var mat in _instancedMaterials)
            {
                if (mat == null) continue;
                // Apply skin tint as a multiply over existing texture
                mat.color = skinColor;
            }

            // If we have multiple materials, try to assign by index convention:
            // Index 0 = body/skin, Index 1 = hair, Index 2 = eyes, etc.
            if (_instancedMaterials.Length >= 2)
                _instancedMaterials[1].color = hairColor;
            if (_instancedMaterials.Length >= 3)
                _instancedMaterials[2].color = eyeColor;
        }

        /// <summary>
        /// Set all renderers and children to a specific layer (for preview camera isolation).
        /// </summary>
        public void SetLayer(int layer)
        {
            if (_modelRoot == null) return;
            SetLayerRecursive(_modelRoot, layer);
        }

        /// <summary>
        /// Get the approximate height of the loaded model for camera framing.
        /// </summary>
        public float GetModelHeight()
        {
            if (_renderers == null || _renderers.Length == 0) return 1.8f;

            float maxY = 0;
            foreach (var r in _renderers)
            {
                float top = r.bounds.max.y - transform.position.y;
                if (top > maxY) maxY = top;
            }
            return maxY > 0.1f ? maxY : 1.8f;
        }

        /// <summary>
        /// Get the center point of the model for camera targeting.
        /// </summary>
        public Vector3 GetModelCenter()
        {
            if (_renderers == null || _renderers.Length == 0)
                return transform.position + Vector3.up * 0.9f;

            Bounds combinedBounds = _renderers[0].bounds;
            for (int i = 1; i < _renderers.Length; i++)
                combinedBounds.Encapsulate(_renderers[i].bounds);

            return combinedBounds.center;
        }

        private void CacheInstancedMaterials()
        {
            // Create instanced copies so we don't modify shared materials
            var mats = new System.Collections.Generic.List<Material>();
            foreach (var r in _renderers)
            {
                var sharedMats = r.sharedMaterials;
                var instanceMats = new Material[sharedMats.Length];
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    instanceMats[i] = new Material(sharedMats[i]);
                    mats.Add(instanceMats[i]);
                }
                r.materials = instanceMats;
            }
            _instancedMaterials = mats.ToArray();
        }

        private void CreateFallbackPrimitive()
        {
            // Fallback: create a capsule if GLB fails to load
            _modelRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _modelRoot.transform.SetParent(transform, false);
            _modelRoot.transform.localPosition = Vector3.up;

            _renderers = _modelRoot.GetComponentsInChildren<Renderer>();
            CacheInstancedMaterials();

            _loaded = true;
            Debug.LogWarning("[ModelCharacter] Using fallback capsule — GLB not available");
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private void OnDestroy()
        {
            // Cleanup instanced materials
            if (_instancedMaterials != null)
            {
                foreach (var mat in _instancedMaterials)
                {
                    if (mat != null) Destroy(mat);
                }
            }
        }
    }
}

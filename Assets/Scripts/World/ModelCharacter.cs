using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Orlo.UI.CharacterCreation;

namespace Orlo.World
{
    /// <summary>
    /// Loads a real 3D character model from a GLB file (glTF binary) and drives
    /// material colors from AppearanceData. Replaces ProceduralCharacter for player characters.
    ///
    /// Uses a built-in minimal GLB parser — no external packages required.
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

        /// <summary>The root GameObject of the loaded model hierarchy.</summary>
        public GameObject GetModelRoot() => _modelRoot;

        /// <summary>
        /// Load a GLB model from pak archive, StreamingAssets, or CDN cache. Apply initial appearance.
        /// </summary>
        public void LoadModel(string glbFileName, AppearanceData initialAppearance = null)
        {
            // Derive assetId from filename (strip .glb extension)
            string assetId = glbFileName;
            if (assetId.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                assetId = assetId.Substring(0, assetId.Length - 4);

            byte[] glbData = null;

            // Try pak archive via AssetLoader first
            var loader = AssetLoader.Instance;
            if (loader != null)
            {
                // Check if pak has this asset — use ReadEntry directly for raw bytes
                var pakReader = loader.GetPakReader();
                if (pakReader != null && pakReader.Contains(assetId))
                {
                    glbData = pakReader.ReadEntry(assetId);
                }
            }

            // Fall back to loose file in StreamingAssets/Characters/
            if (glbData == null)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "Characters", glbFileName);
                if (File.Exists(path))
                    glbData = File.ReadAllBytes(path);
            }

            if (glbData == null)
            {
                Debug.LogWarning($"[ModelCharacter] GLB not found: {glbFileName}, using fallback");
                CreateFallbackPrimitive();
                return;
            }

            try
            {
                var meshes = ParseGlb(glbData);

                if (meshes.Count == 0)
                {
                    Debug.LogWarning("[ModelCharacter] No meshes found in GLB, using fallback");
                    CreateFallbackPrimitive();
                    return;
                }

                _modelRoot = new GameObject("CharacterModel");
                _modelRoot.transform.SetParent(transform, false);

                // Auto-detect model orientation from mesh bounds:
                // If the model is taller in Z than Y, it's Z-up and needs -90° X.
                // If it's taller in Y, it's already Y-up (no correction needed).
                Bounds combinedBounds = default;
                bool boundsInit = false;
                foreach (var m in meshes)
                {
                    if (!boundsInit) { combinedBounds = m.mesh.bounds; boundsInit = true; }
                    else combinedBounds.Encapsulate(m.mesh.bounds);
                }
                bool isZUp = boundsInit && combinedBounds.size.z > combinedBounds.size.y * 1.2f;
                if (isZUp)
                    _modelRoot.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                // else: Y-up model, no rotation needed

                // Create a combined mesh from all primitives
                foreach (var meshData in meshes)
                {
                    var meshGO = new GameObject($"Mesh_{meshData.name}");
                    meshGO.transform.SetParent(_modelRoot.transform, false);

                    var meshFilter = meshGO.AddComponent<MeshFilter>();
                    meshFilter.mesh = meshData.mesh;

                    var meshRenderer = meshGO.AddComponent<MeshRenderer>();
                    // Use URP Lit shader for full PBR support
                    var mat = new Material(Orlo.Rendering.OrloShaders.Lit);
                    AssetLoader.ApplyPbrMaterial(mat, meshData.material);
                    meshRenderer.material = mat;
                }

                _renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
                CacheInstancedMaterials();

                Debug.Log($"[ModelCharacter] Loaded {glbFileName}: {meshes.Count} mesh(es), " +
                          $"{_renderers.Length} renderers");

                _loaded = true;

                if (initialAppearance != null)
                    UpdateAppearance(initialAppearance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModelCharacter] Failed to parse GLB: {ex.Message}");
                CreateFallbackPrimitive();
            }
        }

        /// <summary>
        /// Update material colors from AppearanceData. Instant — no mesh rebuild needed.
        /// </summary>
        public void UpdateAppearance(AppearanceData data)
        {
            if (_instancedMaterials == null || _instancedMaterials.Length == 0) return;

            Color skinColor = data.SkinColor != default ? data.SkinColor : new Color(0.76f, 0.59f, 0.42f);
            Color hairColor = data.HairColor != default ? data.HairColor : new Color(0.2f, 0.15f, 0.1f);
            Color eyeColor = data.LeftEyeColor != default ? data.LeftEyeColor : new Color(0.3f, 0.5f, 0.3f);

            // Apply skin tint to all materials (Phase 0 — single material model)
            foreach (var mat in _instancedMaterials)
            {
                if (mat == null) continue;
                mat.color = skinColor;
            }

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

        // ─── GLB Parser ───────────────────────────────────────────────────

        private struct ParsedMesh
        {
            public string name;
            public Mesh mesh;
            public AssetLoader.MaterialData material;
        }

        /// <summary>
        /// Minimal GLB (glTF Binary) parser. Extracts mesh geometry and embedded textures.
        /// Supports: positions, normals, UVs, indices, embedded PNG/JPEG textures.
        /// </summary>
        private List<ParsedMesh> ParseGlb(byte[] data)
        {
            var meshes = new List<ParsedMesh>();

            // GLB header: magic(4) + version(4) + length(4)
            if (data.Length < 12) return meshes;
            uint magic = BitConverter.ToUInt32(data, 0);
            if (magic != 0x46546C67) // 'glTF'
            {
                Debug.LogError("[GLB] Invalid magic number");
                return meshes;
            }

            // Chunk 0: JSON
            uint jsonChunkLength = BitConverter.ToUInt32(data, 12);
            // uint jsonChunkType = BitConverter.ToUInt32(data, 16); // 0x4E4F534A = JSON
            string jsonStr = Encoding.UTF8.GetString(data, 20, (int)jsonChunkLength);

            // Chunk 1: Binary buffer
            int binOffset = 20 + (int)jsonChunkLength;
            byte[] binData = null;
            if (binOffset + 8 < data.Length)
            {
                uint binChunkLength = BitConverter.ToUInt32(data, binOffset);
                // uint binChunkType = BitConverter.ToUInt32(data, binOffset + 4); // 0x004E4942 = BIN
                binData = new byte[binChunkLength];
                Array.Copy(data, binOffset + 8, binData, 0, (int)binChunkLength);
            }

            if (binData == null)
            {
                Debug.LogError("[GLB] No binary chunk found");
                return meshes;
            }

            // Parse JSON with Unity's JsonUtility-compatible manual parsing
            var gltf = SimpleJson.Parse(jsonStr);
            if (gltf == null) return meshes;

            // Extract buffer views and accessors
            var bufferViews = gltf.GetArray("bufferViews");
            var accessors = gltf.GetArray("accessors");
            var gltfMeshes = gltf.GetArray("meshes");
            var materials = gltf.GetArray("materials");
            var textures = gltf.GetArray("textures");
            var images = gltf.GetArray("images");

            if (gltfMeshes == null || accessors == null || bufferViews == null) return meshes;

            // Parse embedded textures
            var parsedTextures = new Dictionary<int, Texture2D>();
            if (images != null)
            {
                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];
                    int bvIdx = image.GetInt("bufferView", -1);
                    if (bvIdx >= 0 && bvIdx < bufferViews.Count)
                    {
                        var bv = bufferViews[bvIdx];
                        int offset = bv.GetInt("byteOffset", 0);
                        int length = bv.GetInt("byteLength", 0);

                        var tex = new Texture2D(2, 2);
                        var texData = new byte[length];
                        Array.Copy(binData, offset, texData, 0, length);
                        if (tex.LoadImage(texData))
                        {
                            parsedTextures[i] = tex;
                        }
                    }
                }
            }

            // Parse each mesh
            foreach (var gltfMesh in gltfMeshes)
            {
                string meshName = gltfMesh.GetString("name", "mesh");
                var primitives = gltfMesh.GetArray("primitives");
                if (primitives == null) continue;

                foreach (var prim in primitives)
                {
                    var attributes = prim.GetObject("attributes");
                    if (attributes == null) continue;

                    int posAccessor = attributes.GetInt("POSITION", -1);
                    int normAccessor = attributes.GetInt("NORMAL", -1);
                    int uvAccessor = attributes.GetInt("TEXCOORD_0", -1);
                    int indexAccessor = prim.GetInt("indices", -1);
                    int materialIdx = prim.GetInt("material", -1);

                    if (posAccessor < 0) continue;

                    // Read positions
                    Vector3[] positions = ReadVec3Accessor(accessors[posAccessor], bufferViews, binData);
                    Vector3[] normals = normAccessor >= 0
                        ? ReadVec3Accessor(accessors[normAccessor], bufferViews, binData) : null;
                    Vector2[] uvs = uvAccessor >= 0
                        ? ReadVec2Accessor(accessors[uvAccessor], bufferViews, binData) : null;
                    int[] indices = indexAccessor >= 0
                        ? ReadIndexAccessor(accessors[indexAccessor], bufferViews, binData) : null;

                    if (positions == null || positions.Length == 0) continue;

                    var mesh = new Mesh();
                    mesh.name = meshName;

                    // glTF uses right-handed coords; Unity uses left-handed
                    // Flip Z axis for conversion
                    for (int i = 0; i < positions.Length; i++)
                        positions[i].z = -positions[i].z;

                    mesh.vertices = positions;

                    if (normals != null)
                    {
                        for (int i = 0; i < normals.Length; i++)
                            normals[i].z = -normals[i].z;
                        mesh.normals = normals;
                    }

                    if (uvs != null) mesh.uv = uvs;

                    if (indices != null)
                    {
                        // Reverse winding order for left-handed conversion
                        for (int i = 0; i < indices.Length - 2; i += 3)
                        {
                            int tmp = indices[i];
                            indices[i] = indices[i + 2];
                            indices[i + 2] = tmp;
                        }
                        mesh.triangles = indices;
                    }

                    if (normals == null) mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    // Extract full PBR material properties
                    var matData = new AssetLoader.MaterialData
                    {
                        baseColor = Color.white,
                        metallic = 0f,
                        roughness = 1f,
                        emissiveColor = Color.black,
                        isTransparent = false
                    };

                    if (materialIdx >= 0 && materials != null && materialIdx < materials.Count)
                    {
                        var mat = materials[materialIdx];
                        var pbr = mat.GetObject("pbrMetallicRoughness");
                        if (pbr != null)
                        {
                            var colorArr = pbr.GetFloatArray("baseColorFactor");
                            if (colorArr != null && colorArr.Length >= 3)
                            {
                                matData.baseColor = new Color(
                                    colorArr[0], colorArr[1], colorArr[2],
                                    colorArr.Length >= 4 ? colorArr[3] : 1f);
                            }

                            matData.albedoTex = ResolveTexture(pbr.GetObject("baseColorTexture"), textures, parsedTextures);
                            matData.metallic = pbr.GetFloat("metallicFactor", 1f);
                            matData.roughness = pbr.GetFloat("roughnessFactor", 1f);
                            matData.metallicRoughnessTex = ResolveTexture(pbr.GetObject("metallicRoughnessTexture"), textures, parsedTextures);
                        }

                        matData.normalTex = ResolveTexture(mat.GetObject("normalTexture"), textures, parsedTextures);
                        matData.emissiveTex = ResolveTexture(mat.GetObject("emissiveTexture"), textures, parsedTextures);
                        var emissiveArr = mat.GetFloatArray("emissiveFactor");
                        if (emissiveArr != null && emissiveArr.Length >= 3)
                            matData.emissiveColor = new Color(emissiveArr[0], emissiveArr[1], emissiveArr[2]);

                        string alphaMode = mat.GetString("alphaMode", "OPAQUE");
                        matData.isTransparent = alphaMode == "BLEND";
                    }

                    meshes.Add(new ParsedMesh
                    {
                        name = meshName,
                        mesh = mesh,
                        material = matData
                    });
                }
            }

            return meshes;
        }

        private static Texture2D ResolveTexture(
            SimpleJson.JsonNode texInfo,
            List<SimpleJson.JsonNode> textures,
            Dictionary<int, Texture2D> parsedTextures)
        {
            if (texInfo == null || textures == null) return null;
            int texIdx = texInfo.GetInt("index", -1);
            if (texIdx < 0 || texIdx >= textures.Count) return null;
            int imgIdx = textures[texIdx].GetInt("source", -1);
            return parsedTextures.TryGetValue(imgIdx, out var tex) ? tex : null;
        }

        private Vector3[] ReadVec3Accessor(SimpleJson.JsonNode accessor, List<SimpleJson.JsonNode> bufferViews, byte[] bin)
        {
            int count = accessor.GetInt("count", 0);
            int bvIdx = accessor.GetInt("bufferView", 0);
            int byteOffset = accessor.GetInt("byteOffset", 0);

            var bv = bufferViews[bvIdx];
            int bvOffset = bv.GetInt("byteOffset", 0);
            int stride = bv.GetInt("byteStride", 12); // 3 floats = 12 bytes

            var result = new Vector3[count];
            int start = bvOffset + byteOffset;

            for (int i = 0; i < count; i++)
            {
                int off = start + i * stride;
                result[i] = new Vector3(
                    BitConverter.ToSingle(bin, off),
                    BitConverter.ToSingle(bin, off + 4),
                    BitConverter.ToSingle(bin, off + 8));
            }
            return result;
        }

        private Vector2[] ReadVec2Accessor(SimpleJson.JsonNode accessor, List<SimpleJson.JsonNode> bufferViews, byte[] bin)
        {
            int count = accessor.GetInt("count", 0);
            int bvIdx = accessor.GetInt("bufferView", 0);
            int byteOffset = accessor.GetInt("byteOffset", 0);

            var bv = bufferViews[bvIdx];
            int bvOffset = bv.GetInt("byteOffset", 0);
            int stride = bv.GetInt("byteStride", 8);

            var result = new Vector2[count];
            int start = bvOffset + byteOffset;

            for (int i = 0; i < count; i++)
            {
                int off = start + i * stride;
                result[i] = new Vector2(
                    BitConverter.ToSingle(bin, off),
                    BitConverter.ToSingle(bin, off + 4));
            }
            return result;
        }

        private int[] ReadIndexAccessor(SimpleJson.JsonNode accessor, List<SimpleJson.JsonNode> bufferViews, byte[] bin)
        {
            int count = accessor.GetInt("count", 0);
            int bvIdx = accessor.GetInt("bufferView", 0);
            int byteOffset = accessor.GetInt("byteOffset", 0);
            int componentType = accessor.GetInt("componentType", 5123); // 5123=UNSIGNED_SHORT

            var bv = bufferViews[bvIdx];
            int bvOffset = bv.GetInt("byteOffset", 0);

            var result = new int[count];
            int start = bvOffset + byteOffset;

            for (int i = 0; i < count; i++)
            {
                switch (componentType)
                {
                    case 5121: // UNSIGNED_BYTE
                        result[i] = bin[start + i];
                        break;
                    case 5123: // UNSIGNED_SHORT
                        result[i] = BitConverter.ToUInt16(bin, start + i * 2);
                        break;
                    case 5125: // UNSIGNED_INT
                        result[i] = (int)BitConverter.ToUInt32(bin, start + i * 4);
                        break;
                }
            }
            return result;
        }

        private void CacheInstancedMaterials()
        {
            var mats = new List<Material>();
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
            _modelRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _modelRoot.transform.SetParent(transform, false);
            _modelRoot.transform.localPosition = Vector3.up;

            _renderers = _modelRoot.GetComponentsInChildren<Renderer>();
            CacheInstancedMaterials();

            _loaded = true;
            Debug.LogWarning("[ModelCharacter] Using fallback capsule");
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private void OnDestroy()
        {
            if (_instancedMaterials != null)
                foreach (var mat in _instancedMaterials)
                    if (mat != null) Destroy(mat);
        }
    }

    /// <summary>
    /// Minimal JSON parser for glTF. Not a general-purpose parser —
    /// only supports the subset needed for glTF mesh extraction.
    /// </summary>
    internal static class SimpleJson
    {
        internal class JsonNode
        {
            private readonly Dictionary<string, object> _dict;
            private readonly List<object> _list;
            private readonly object _value;

            public JsonNode(Dictionary<string, object> dict) { _dict = dict; }
            public JsonNode(List<object> list) { _list = list; }
            public JsonNode(object value) { _value = value; }

            public string GetString(string key, string def = "")
            {
                if (_dict != null && _dict.TryGetValue(key, out var v) && v is string s) return s;
                return def;
            }

            public int GetInt(string key, int def = 0)
            {
                if (_dict == null || !_dict.TryGetValue(key, out var v)) return def;
                if (v is double d) return (int)d;
                if (v is long l) return (int)l;
                if (v is int i) return i;
                return def;
            }

            public float GetFloat(string key, float def = 0f)
            {
                if (_dict == null || !_dict.TryGetValue(key, out var v)) return def;
                if (v is double d) return (float)d;
                if (v is long l) return l;
                if (v is int i) return i;
                return def;
            }

            public float[] GetFloatArray(string key)
            {
                if (_dict == null || !_dict.TryGetValue(key, out var v)) return null;
                if (v is List<object> list)
                {
                    var result = new float[list.Count];
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] is double d) result[i] = (float)d;
                        else if (list[i] is long l) result[i] = l;
                    }
                    return result;
                }
                return null;
            }

            public JsonNode GetObject(string key)
            {
                if (_dict != null && _dict.TryGetValue(key, out var v) && v is Dictionary<string, object> d)
                    return new JsonNode(d);
                return null;
            }

            public List<JsonNode> GetArray(string key)
            {
                if (_dict == null || !_dict.TryGetValue(key, out var v)) return null;
                if (v is List<object> list)
                {
                    var result = new List<JsonNode>();
                    foreach (var item in list)
                    {
                        if (item is Dictionary<string, object> d)
                            result.Add(new JsonNode(d));
                        else
                            result.Add(new JsonNode(item));
                    }
                    return result;
                }
                return null;
            }
        }

        internal static JsonNode Parse(string json)
        {
            int idx = 0;
            var result = ParseValue(json, ref idx);
            if (result is Dictionary<string, object> d) return new JsonNode(d);
            return null;
        }

        private static object ParseValue(string json, ref int idx)
        {
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length) return null;

            char c = json[idx];
            if (c == '{') return ParseObject(json, ref idx);
            if (c == '[') return ParseArray(json, ref idx);
            if (c == '"') return ParseString(json, ref idx);
            if (c == 't' || c == 'f') return ParseBool(json, ref idx);
            if (c == 'n') { idx += 4; return null; }
            return ParseNumber(json, ref idx);
        }

        private static Dictionary<string, object> ParseObject(string json, ref int idx)
        {
            var dict = new Dictionary<string, object>();
            idx++; // skip '{'
            SkipWhitespace(json, ref idx);

            while (idx < json.Length && json[idx] != '}')
            {
                SkipWhitespace(json, ref idx);
                if (json[idx] == '}') break;
                if (json[idx] == ',') { idx++; continue; }

                string key = ParseString(json, ref idx);
                SkipWhitespace(json, ref idx);
                if (idx < json.Length && json[idx] == ':') idx++;
                SkipWhitespace(json, ref idx);

                dict[key] = ParseValue(json, ref idx);
                SkipWhitespace(json, ref idx);
            }

            if (idx < json.Length) idx++; // skip '}'
            return dict;
        }

        private static List<object> ParseArray(string json, ref int idx)
        {
            var list = new List<object>();
            idx++; // skip '['
            SkipWhitespace(json, ref idx);

            while (idx < json.Length && json[idx] != ']')
            {
                if (json[idx] == ',') { idx++; SkipWhitespace(json, ref idx); continue; }
                list.Add(ParseValue(json, ref idx));
                SkipWhitespace(json, ref idx);
            }

            if (idx < json.Length) idx++; // skip ']'
            return list;
        }

        private static string ParseString(string json, ref int idx)
        {
            idx++; // skip opening quote
            var sb = new StringBuilder();
            while (idx < json.Length && json[idx] != '"')
            {
                if (json[idx] == '\\')
                {
                    idx++;
                    if (idx < json.Length)
                    {
                        switch (json[idx])
                        {
                            case '"': case '\\': case '/': sb.Append(json[idx]); break;
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (idx + 4 < json.Length)
                                {
                                    sb.Append((char)Convert.ToInt32(json.Substring(idx + 1, 4), 16));
                                    idx += 4;
                                }
                                break;
                        }
                    }
                }
                else
                {
                    sb.Append(json[idx]);
                }
                idx++;
            }
            if (idx < json.Length) idx++; // skip closing quote
            return sb.ToString();
        }

        private static double ParseNumber(string json, ref int idx)
        {
            int start = idx;
            while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' ||
                   json[idx] == '-' || json[idx] == '+' || json[idx] == 'e' || json[idx] == 'E'))
                idx++;
            return double.Parse(json.Substring(start, idx - start),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool ParseBool(string json, ref int idx)
        {
            if (json[idx] == 't') { idx += 4; return true; }
            idx += 5; return false;
        }

        private static void SkipWhitespace(string json, ref int idx)
        {
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
        }
    }
}

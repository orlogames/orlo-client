using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Orlo.World
{
    /// <summary>
    /// Singleton that loads 3D models from GLB files in StreamingAssets/models/.
    /// Reuses the same GLB parsing approach as ModelCharacter but generalized
    /// for any asset type (props, structures, interactables, etc.).
    /// Caches loaded meshes by assetId for reuse across multiple instances.
    /// </summary>
    public class AssetLoader : MonoBehaviour
    {
        public static AssetLoader Instance { get; private set; }

        // ─── CDN Download ──────────────────────────────────────────────
        private const string CDN_BASE_URL = "https://cdn.orlo.games/assets/models/";

        private readonly HashSet<string> _downloading = new();
        private readonly HashSet<string> _downloadFailed = new();
        private readonly Queue<(string assetId, Action<GameObject> callback)> _downloadQueue = new();
        private int _activeDownloads = 0;
        private const int MAX_CONCURRENT_DOWNLOADS = 4;

        /// <summary>Number of downloads pending or in-flight.</summary>
        public int PendingDownloads => _downloadQueue.Count + _activeDownloads;

        /// <summary>Cached mesh data per assetId — avoids re-parsing GLB on every spawn.</summary>
        private readonly Dictionary<string, CachedModel> _cache = new();

        /// <summary>Set of assetIds known to have no GLB file — avoids repeated File.Exists checks.</summary>
        private readonly HashSet<string> _missingAssets = new();

        /// <summary>Pak archive reader for bundled assets.</summary>
        private PakReader _pakReader;

        /// <summary>Expose pak reader for other systems (e.g., ModelCharacter) that need raw byte access.</summary>
        public PakReader GetPakReader() => _pakReader;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Open pak file if present in StreamingAssets
            string pakPath = Path.Combine(Application.streamingAssetsPath, "assets.pak");
            if (File.Exists(pakPath))
            {
                try
                {
                    _pakReader = new PakReader(pakPath);
                    Debug.Log($"[AssetLoader] Opened assets.pak with {_pakReader.EntryCount} entries");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AssetLoader] Failed to open assets.pak: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            _pakReader?.Dispose();
            _pakReader = null;
        }

        /// <summary>
        /// Try to load a GLB model for the given assetId.
        /// Returns a new GameObject with MeshFilter + MeshRenderer + MeshCollider, or null if no GLB exists.
        /// </summary>
        public GameObject TryLoadModel(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;

            // Fast path: already know this asset has no GLB
            if (_missingAssets.Contains(assetId)) return null;

            // Check cache
            if (_cache.TryGetValue(assetId, out var cached))
            {
                return InstantiateFromCache(assetId, cached);
            }

            // Try pak file first, then loose files on disk
            byte[] glbData = null;
            string source = null;

            // 1. Pak archive (bundled with build)
            if (_pakReader != null && _pakReader.Contains(assetId))
            {
                glbData = _pakReader.ReadEntry(assetId);
                source = "pak";
            }

            // 2. Loose file in StreamingAssets/models/
            if (glbData == null)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "models", $"{assetId}.glb");
                if (File.Exists(path))
                {
                    glbData = File.ReadAllBytes(path);
                    source = "StreamingAssets";
                }
            }

            // 3. Loose file in persistentDataPath/models/ (CDN download cache)
            if (glbData == null)
            {
                string downloadedPath = Path.Combine(Application.persistentDataPath, "models", $"{assetId}.glb");
                if (File.Exists(downloadedPath))
                {
                    glbData = File.ReadAllBytes(downloadedPath);
                    source = "cache";
                }
            }

            if (glbData == null)
            {
                _missingAssets.Add(assetId);
                return null;
            }

            try
            {
                var meshEntries = ParseGlb(glbData);

                if (meshEntries.Count == 0)
                {
                    Debug.LogWarning($"[AssetLoader] No meshes found in {assetId}.glb");
                    _missingAssets.Add(assetId);
                    return null;
                }

                cached = new CachedModel { entries = meshEntries };
                _cache[assetId] = cached;

                Debug.Log($"[AssetLoader] Loaded {assetId}.glb from {source}: {meshEntries.Count} mesh(es)");
                return InstantiateFromCache(assetId, cached);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetLoader] Failed to parse {assetId}.glb: {ex.Message}");
                _missingAssets.Add(assetId);
                return null;
            }
        }

        /// <summary>
        /// Returns true if a previous CDN download for this assetId failed (404 or error).
        /// </summary>
        public bool IsDownloadFailed(string assetId) => _downloadFailed.Contains(assetId);

        /// <summary>
        /// Queue a CDN download for a missing GLB model.
        /// When the download completes, onComplete is called with the new GameObject (or null on failure).
        /// </summary>
        public void QueueDownload(string assetId, Action<GameObject> onComplete)
        {
            if (string.IsNullOrEmpty(assetId)) { onComplete?.Invoke(null); return; }
            if (_downloading.Contains(assetId) || _downloadFailed.Contains(assetId)) return;

            _downloading.Add(assetId);
            _downloadQueue.Enqueue((assetId, onComplete));
            ProcessDownloadQueue();
        }

        private void ProcessDownloadQueue()
        {
            while (_activeDownloads < MAX_CONCURRENT_DOWNLOADS && _downloadQueue.Count > 0)
            {
                var (assetId, callback) = _downloadQueue.Dequeue();
                _activeDownloads++;
                StartCoroutine(DownloadModel(assetId, callback));
            }
        }

        private IEnumerator DownloadModel(string assetId, Action<GameObject> onComplete)
        {
            string url = $"{CDN_BASE_URL}{assetId}.glb";
            Debug.Log($"[AssetLoader] Downloading {assetId} from CDN...");

            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[AssetLoader] CDN download failed for {assetId}: {request.error} (HTTP {request.responseCode})");
                    _downloadFailed.Add(assetId);
                    _downloading.Remove(assetId);
                    _activeDownloads--;
                    ProcessDownloadQueue();
                    onComplete?.Invoke(null);
                    yield break;
                }

                byte[] glbData = request.downloadHandler.data;

                // Save to persistentDataPath for future sessions
                try
                {
                    string dir = Path.Combine(Application.persistentDataPath, "models");
                    Directory.CreateDirectory(dir);
                    string filePath = Path.Combine(dir, $"{assetId}.glb");
                    File.WriteAllBytes(filePath, glbData);
                    Debug.Log($"[AssetLoader] Saved {assetId}.glb to {filePath} ({glbData.Length} bytes)");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AssetLoader] Failed to save {assetId}.glb to disk: {ex.Message}");
                }

                // Parse and cache
                GameObject result = null;
                try
                {
                    var meshEntries = ParseGlb(glbData);
                    if (meshEntries.Count > 0)
                    {
                        var cached = new CachedModel { entries = meshEntries };
                        _cache[assetId] = cached;
                        _missingAssets.Remove(assetId);
                        result = InstantiateFromCache(assetId, cached);
                        Debug.Log($"[AssetLoader] CDN model {assetId}: {meshEntries.Count} mesh(es) loaded");
                    }
                    else
                    {
                        Debug.LogWarning($"[AssetLoader] CDN model {assetId}.glb contained no meshes");
                        _downloadFailed.Add(assetId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AssetLoader] Failed to parse CDN model {assetId}.glb: {ex.Message}");
                    _downloadFailed.Add(assetId);
                }

                _downloading.Remove(assetId);
                _activeDownloads--;
                ProcessDownloadQueue();
                onComplete?.Invoke(result);
            }
        }

        /// <summary>
        /// Clear the cache and missing-asset set. Useful if assets are hot-reloaded.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            _missingAssets.Clear();
            _downloadFailed.Clear();
        }

        // ─── Instantiation ──────────────────────────────────────────────

        private GameObject InstantiateFromCache(string assetId, CachedModel cached)
        {
            var root = new GameObject($"Model_{assetId}");

            // GLB models from Meshy are authored with Z-up; apply rotation correction
            root.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            Bounds combinedBounds = default;
            bool boundsInitialized = false;

            // Use Standard shader for GLB models (supports PBR textures from Meshy)
            // Standard shader is always included via GraphicsSettings.asset
            var shader = Shader.Find("Standard")
                ?? Resources.Load<Shader>("Shaders/EntityFallback")
                ?? Shader.Find("Legacy Shaders/Diffuse");

            foreach (var entry in cached.entries)
            {
                var child = new GameObject($"Mesh_{entry.name}");
                child.transform.SetParent(root.transform, false);

                var mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = entry.mesh;

                var mr = child.AddComponent<MeshRenderer>();
                var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
                ApplyPbrMaterial(mat, entry.material);
                mr.material = mat;

                // Accumulate bounds for the collider
                if (!boundsInitialized)
                {
                    combinedBounds = entry.mesh.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(entry.mesh.bounds);
                }
            }

            // Add a box collider based on combined bounds
            if (boundsInitialized)
            {
                var col = root.AddComponent<BoxCollider>();
                col.center = combinedBounds.center;
                col.size = combinedBounds.size;
            }

            return root;
        }

        /// <summary>
        /// Apply all PBR material properties from parsed glTF data to a Unity Standard shader material.
        /// Public static so ModelCharacter can reuse this.
        /// </summary>
        public static void ApplyPbrMaterial(Material mat, MaterialData data)
        {
            // Base color
            mat.color = data.baseColor;

            // Albedo texture
            if (data.albedoTex != null)
                mat.mainTexture = data.albedoTex;

            // Metallic + smoothness (Standard shader uses smoothness = 1 - roughness)
            mat.SetFloat("_Metallic", data.metallic);
            mat.SetFloat("_GlossMapScale", 1f);
            mat.SetFloat("_Glossiness", 1f - data.roughness);

            // Metallic-roughness texture (green channel = roughness, blue channel = metallic in glTF)
            // Unity Standard shader expects metallic in R and smoothness in A of _MetallicGlossMap
            if (data.metallicRoughnessTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", data.metallicRoughnessTex);
                // When a metallic map is present, the shader reads metallic from the texture
                // Set metallic to 1 so the texture controls the actual value
                mat.SetFloat("_Metallic", 1f);
            }

            // Normal map
            if (data.normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", data.normalTex);
                mat.SetFloat("_BumpScale", 1f);
            }

            // Emission
            bool hasEmission = data.emissiveTex != null ||
                (data.emissiveColor.r > 0.001f || data.emissiveColor.g > 0.001f || data.emissiveColor.b > 0.001f);
            if (hasEmission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", data.emissiveColor);
                if (data.emissiveTex != null)
                    mat.SetTexture("_EmissionMap", data.emissiveTex);
            }

            // Transparency
            if (data.isTransparent)
            {
                mat.SetFloat("_Mode", 3f); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000; // Transparent queue
            }
        }

        // ─── GLB Parser (same approach as ModelCharacter) ───────────────

        private struct CachedModel
        {
            public List<MeshEntry> entries;
        }

        /// <summary>Holds all PBR material properties extracted from a glTF material.</summary>
        public struct MaterialData
        {
            public Color baseColor;
            public Texture2D albedoTex;
            public Texture2D normalTex;
            public Texture2D metallicRoughnessTex;
            public Texture2D emissiveTex;
            public Color emissiveColor;
            public float metallic;
            public float roughness;
            public bool isTransparent;
        }

        private struct MeshEntry
        {
            public string name;
            public Mesh mesh;
            public MaterialData material;
        }

        private List<MeshEntry> ParseGlb(byte[] data)
        {
            var meshes = new List<MeshEntry>();

            if (data.Length < 12) return meshes;
            uint magic = BitConverter.ToUInt32(data, 0);
            if (magic != 0x46546C67) // 'glTF'
            {
                Debug.LogError("[AssetLoader] Invalid GLB magic number");
                return meshes;
            }

            // Chunk 0: JSON
            uint jsonChunkLength = BitConverter.ToUInt32(data, 12);
            string jsonStr = Encoding.UTF8.GetString(data, 20, (int)jsonChunkLength);

            // Chunk 1: Binary buffer
            int binOffset = 20 + (int)jsonChunkLength;
            byte[] binData = null;
            if (binOffset + 8 < data.Length)
            {
                uint binChunkLength = BitConverter.ToUInt32(data, binOffset);
                binData = new byte[binChunkLength];
                Array.Copy(data, binOffset + 8, binData, 0, (int)binChunkLength);
            }

            if (binData == null) return meshes;

            var gltf = SimpleJson.Parse(jsonStr);
            if (gltf == null) return meshes;

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
                            parsedTextures[i] = tex;
                    }
                }
            }

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

                    // glTF right-handed to Unity left-handed: flip Z
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

                    var matData = new MaterialData
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
                            // Base color factor
                            var colorArr = pbr.GetFloatArray("baseColorFactor");
                            if (colorArr != null && colorArr.Length >= 3)
                            {
                                matData.baseColor = new Color(
                                    colorArr[0], colorArr[1], colorArr[2],
                                    colorArr.Length >= 4 ? colorArr[3] : 1f);
                            }

                            // Base color texture (albedo)
                            matData.albedoTex = ResolveTexture(pbr.GetObject("baseColorTexture"), textures, parsedTextures);

                            // Metallic / roughness factors
                            matData.metallic = pbr.GetFloat("metallicFactor", 1f);
                            matData.roughness = pbr.GetFloat("roughnessFactor", 1f);

                            // Metallic-roughness combined texture
                            matData.metallicRoughnessTex = ResolveTexture(pbr.GetObject("metallicRoughnessTexture"), textures, parsedTextures);
                        }

                        // Normal map
                        matData.normalTex = ResolveTexture(mat.GetObject("normalTexture"), textures, parsedTextures);

                        // Emissive texture + factor
                        matData.emissiveTex = ResolveTexture(mat.GetObject("emissiveTexture"), textures, parsedTextures);
                        var emissiveArr = mat.GetFloatArray("emissiveFactor");
                        if (emissiveArr != null && emissiveArr.Length >= 3)
                            matData.emissiveColor = new Color(emissiveArr[0], emissiveArr[1], emissiveArr[2]);

                        // Alpha mode
                        string alphaMode = mat.GetString("alphaMode", "OPAQUE");
                        matData.isTransparent = alphaMode == "BLEND";
                    }

                    meshes.Add(new MeshEntry
                    {
                        name = meshName,
                        mesh = mesh,
                        material = matData
                    });
                }
            }

            return meshes;
        }

        /// <summary>
        /// Resolve a glTF textureInfo object (e.g. baseColorTexture, normalTexture) to a parsed Texture2D.
        /// Returns null if the texture is not found or not embedded.
        /// </summary>
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
            int stride = bv.GetInt("byteStride", 12);

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
            int componentType = accessor.GetInt("componentType", 5123);
            var bv = bufferViews[bvIdx];
            int bvOffset = bv.GetInt("byteOffset", 0);

            var result = new int[count];
            int start = bvOffset + byteOffset;
            for (int i = 0; i < count; i++)
            {
                switch (componentType)
                {
                    case 5121: result[i] = bin[start + i]; break;
                    case 5123: result[i] = BitConverter.ToUInt16(bin, start + i * 2); break;
                    case 5125: result[i] = (int)BitConverter.ToUInt32(bin, start + i * 4); break;
                }
            }
            return result;
        }
    }
}

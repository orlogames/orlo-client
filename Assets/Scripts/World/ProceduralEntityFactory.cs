using System.Collections.Generic;
using UnityEngine;
using Orlo.VFX;

namespace Orlo.World
{
    /// <summary>
    /// Factory for building entities from procedural geometry.
    /// Replaces EntityManager's prefab-based spawning with code-generated meshes.
    /// Uses object pooling to reduce GC pressure.
    /// </summary>
    public class ProceduralEntityFactory : MonoBehaviour
    {
        public static ProceduralEntityFactory Instance { get; private set; }

        // Entity type constants matching server definitions
        public const uint TYPE_HUMANOID_NPC = 1;
        public const uint TYPE_ANIMAL = 2;
        public const uint TYPE_PROP = 3;
        public const uint TYPE_PLAYER = 4;
        public const uint TYPE_VEHICLE = 5;
        public const uint TYPE_INTERACTABLE = 6;

        // Object pool
        private readonly Dictionary<string, Queue<GameObject>> _pool = new();
        [SerializeField] private int maxPoolSizePerType = 20;

        [Header("Microparticle Assembly")]
        [Tooltip("Enable particle assembly effect when spawning entities.")]
        [SerializeField] private bool enableAssemblyEffect = true;
        [Tooltip("Compute shader for microparticle simulation.")]
        [SerializeField] private ComputeShader microparticleCompute;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Wire into EntityManager if it exists
            WireEntityManager();
        }

        private void WireEntityManager()
        {
            var em = EntityManager.Instance;
            if (em != null)
            {
                em.EntityFactory = BuildEntity;
                Debug.Log("[EntityFactory] Wired into EntityManager");
            }
        }

        /// <summary>Get a shader that won't be null in builds.</summary>
        private static Shader _cachedFallbackShader;
        private static Shader GetFallbackShader()
        {
            if (_cachedFallbackShader != null) return _cachedFallbackShader;
            _cachedFallbackShader = Resources.Load<Shader>("Shaders/EntityFallback");
            if (_cachedFallbackShader == null) _cachedFallbackShader = Shader.Find("Orlo/EntityFallback");
            if (_cachedFallbackShader == null) _cachedFallbackShader = Shader.Find("Standard");
            if (_cachedFallbackShader == null) _cachedFallbackShader = Shader.Find("Legacy Shaders/Diffuse");
            return _cachedFallbackShader;
        }

        private static Material MakeMat(Color color)
        {
            var shader = GetFallbackShader();
            if (shader == null)
            {
                Debug.LogError("[EntityFactory] All shader lookups failed!");
                return new Material(Shader.Find("Hidden/InternalErrorShader")) { color = color };
            }
            return new Material(shader) { color = color };
        }

        // Known settlement asset IDs that have dedicated procedural builders
        private static readonly HashSet<string> _settlementAssets = new()
        {
            "nexus_crystal_fountain",
            "frontier_cabin",
            "cooking_station",
            "ground_lantern",
            "wall_lantern",
            "stone_pathway_segment",
            "settlement_bench",
            "vendor_display_table",
            "settlement_pine",
        };

        /// <summary>
        /// Main dispatch: build an entity by type.
        /// </summary>
        public GameObject BuildEntity(uint entityType, string assetId, Vector3 position, Quaternion rotation)
        {
            string poolKey = $"{entityType}_{assetId}";

            // Check pool first
            GameObject pooled = GetFromPool(poolKey);
            if (pooled != null)
            {
                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.SetActive(true);
                return pooled;
            }

            // Try loading a real GLB model before falling back to procedural
            var loader = AssetLoader.Instance;
            if (loader != null)
            {
                var modelGo = loader.TryLoadModel(assetId);
                if (modelGo != null)
                {
                    modelGo.name = $"Model_{assetId}";
                    modelGo.transform.SetPositionAndRotation(position, rotation);
                    var modelTag = modelGo.AddComponent<PoolTag>();
                    modelTag.PoolKey = poolKey;
                    AttachPointLightIfNeeded(modelGo, assetId);
                    return modelGo;
                }

                // GLB not on disk — try CDN download with procedural placeholder
                if (!loader.IsDownloadFailed(assetId))
                {
                    var placeholder = BuildProceduralFallback(entityType, assetId, position, rotation);
                    var phTag = placeholder.AddComponent<PoolTag>();
                    phTag.PoolKey = poolKey;

                    loader.QueueDownload(assetId, (downloadedModel) =>
                    {
                        if (downloadedModel != null && placeholder != null)
                        {
                            // Swap downloaded model into placeholder's transform
                            downloadedModel.transform.SetParent(placeholder.transform.parent, false);
                            downloadedModel.transform.position = placeholder.transform.position;
                            downloadedModel.transform.rotation = placeholder.transform.rotation;
                            downloadedModel.transform.localScale = placeholder.transform.localScale;
                            downloadedModel.name = placeholder.name;

                            // Transfer PoolTag
                            var newTag = downloadedModel.AddComponent<PoolTag>();
                            newTag.PoolKey = poolKey;

                            Object.Destroy(placeholder);
                        }
                    });

                    return placeholder;
                }
            }

            // Build new — check for settlement assets first
            GameObject go;
            if (_settlementAssets.Contains(assetId))
            {
                go = BuildSettlementAsset(assetId, position, rotation);
            }
            else
            {
                switch (entityType)
                {
                    case TYPE_HUMANOID_NPC:
                        go = BuildHumanoidNPC(assetId, position, rotation);
                        break;
                    case TYPE_ANIMAL:
                        go = BuildAnimal(assetId, position, rotation);
                        break;
                    case TYPE_PROP:
                        go = BuildProp(assetId, position, rotation);
                        break;
                    case TYPE_PLAYER:
                        go = BuildPlayer(assetId, position, rotation);
                        break;
                    case TYPE_VEHICLE:
                        go = BuildProp(assetId, position, rotation); // Placeholder
                        break;
                    case TYPE_INTERACTABLE:
                        go = BuildProp(assetId, position, rotation); // Placeholder
                        break;
                    default:
                        go = BuildFallback(position, rotation);
                        break;
                }
            }

            // Tag with pool key for recycling
            var tag = go.AddComponent<PoolTag>();
            tag.PoolKey = poolKey;

            // Add atmospheric point lights based on asset type
            AttachPointLightIfNeeded(go, assetId);

            return go;
        }

        /// <summary>
        /// Return an entity to the pool instead of destroying it.
        /// </summary>
        public void ReturnToPool(GameObject go)
        {
            var tag = go.GetComponent<PoolTag>();
            if (tag == null)
            {
                Object.Destroy(go);
                return;
            }

            string key = tag.PoolKey;
            if (!_pool.TryGetValue(key, out var queue))
            {
                queue = new Queue<GameObject>();
                _pool[key] = queue;
            }

            if (queue.Count >= maxPoolSizePerType)
            {
                Object.Destroy(go);
                return;
            }

            go.SetActive(false);
            queue.Enqueue(go);
        }

        private GameObject GetFromPool(string key)
        {
            if (!_pool.TryGetValue(key, out var queue)) return null;

            while (queue.Count > 0)
            {
                var go = queue.Dequeue();
                if (go != null) return go;
            }

            return null;
        }

        /// <summary>
        /// Attach atmospheric point lights to lantern and building entities
        /// for warm golden hour fill lighting.
        /// </summary>
        private static void AttachPointLightIfNeeded(GameObject go, string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return;
            string id = assetId.ToLower();

            if (id.Contains("lantern"))
            {
                var lightGo = new GameObject("LanternLight");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = Vector3.up * 1.5f;
                var pl = lightGo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1.0f, 0.7f, 0.3f);
                pl.intensity = 1.2f;
                pl.range = 8f;
                pl.shadows = LightShadows.None;
            }
            else if (id.Contains("building") || id.Contains("cabin"))
            {
                var lightGo = new GameObject("BuildingLight");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = Vector3.up * 2.0f;
                var pl = lightGo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1.0f, 0.75f, 0.4f);
                pl.intensity = 0.8f;
                pl.range = 5f;
                pl.shadows = LightShadows.None;
            }
        }

        /// <summary>
        /// Build a humanoid NPC with ProceduralCharacter.
        /// </summary>
        public GameObject BuildHumanoidNPC(string assetId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject($"NPC_{assetId}");
            go.transform.SetPositionAndRotation(position, rotation);

            var spec = ResolveCharacterSpec(assetId);
            var character = go.AddComponent<ProceduralCharacter>();
            character.Build(spec);

            // Add CharacterController for NPC movement
            var cc = go.AddComponent<CharacterController>();
            cc.height = spec.Height;
            cc.radius = spec.BodyWidth * 0.5f;
            cc.center = new Vector3(0, spec.Height * 0.5f, 0);

            // Equip based on assetId
            EquipNPC(character, assetId);

            return go;
        }

        /// <summary>
        /// Build an animal entity from primitives.
        /// </summary>
        public GameObject BuildAnimal(string assetId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject($"Animal_{assetId}");
            go.transform.SetPositionAndRotation(position, rotation);

            var standard = GetFallbackShader();
            Color bodyColor;
            float bodyLength, bodyRadius, legLength, legRadius;

            // Parse animal type from assetId
            string type = assetId.ToLower();
            if (type.Contains("wolf") || type.Contains("dog"))
            {
                bodyColor = new Color(0.4f, 0.35f, 0.3f);
                bodyLength = 1.2f; bodyRadius = 0.25f;
                legLength = 0.5f; legRadius = 0.06f;
            }
            else if (type.Contains("deer") || type.Contains("elk"))
            {
                bodyColor = new Color(0.6f, 0.45f, 0.3f);
                bodyLength = 1.5f; bodyRadius = 0.3f;
                legLength = 0.7f; legRadius = 0.05f;
            }
            else if (type.Contains("bird"))
            {
                bodyColor = new Color(0.3f, 0.3f, 0.35f);
                bodyLength = 0.3f; bodyRadius = 0.1f;
                legLength = 0.15f; legRadius = 0.015f;
            }
            else // generic quadruped
            {
                bodyColor = new Color(0.5f, 0.4f, 0.35f);
                bodyLength = 1.0f; bodyRadius = 0.2f;
                legLength = 0.4f; legRadius = 0.05f;
            }

            var bodyMat = new Material(standard) { color = bodyColor };

            // Body — horizontal cylinder
            var bodyMesh = ProceduralMeshBuilder.BuildCylinder(bodyRadius * 0.85f, bodyRadius, bodyLength, 8);
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0, legLength + bodyRadius, 0);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            body.AddComponent<MeshFilter>().mesh = bodyMesh;
            body.AddComponent<MeshRenderer>().material = bodyMat;

            // Head — sphere
            var headMesh = ProceduralMeshBuilder.BuildSphere(bodyRadius * 0.7f, 6, 8);
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform);
            head.transform.localPosition = new Vector3(0, legLength + bodyRadius * 1.2f, bodyLength * 0.55f);
            head.AddComponent<MeshFilter>().mesh = headMesh;
            head.AddComponent<MeshRenderer>().material = bodyMat;

            // Snout
            var snoutMesh = ProceduralMeshBuilder.BuildCone(bodyRadius * 0.2f, bodyRadius * 0.4f, 5);
            var snout = new GameObject("Snout");
            snout.transform.SetParent(head.transform);
            snout.transform.localPosition = new Vector3(0, -bodyRadius * 0.1f, bodyRadius * 0.5f);
            snout.transform.localRotation = Quaternion.Euler(90, 0, 0);
            snout.AddComponent<MeshFilter>().mesh = snoutMesh;
            snout.AddComponent<MeshRenderer>().material = bodyMat;

            // Four legs
            var legMesh = ProceduralMeshBuilder.BuildCylinder(legRadius * 0.8f, legRadius, legLength, 5);
            float legXOffset = bodyRadius * 0.5f;
            float legZFront = bodyLength * 0.35f;
            float legZBack = -bodyLength * 0.25f;

            Vector3[] legPositions = {
                new Vector3(-legXOffset, legLength * 0.5f, legZFront),
                new Vector3(legXOffset, legLength * 0.5f, legZFront),
                new Vector3(-legXOffset, legLength * 0.5f, legZBack),
                new Vector3(legXOffset, legLength * 0.5f, legZBack),
            };

            for (int i = 0; i < 4; i++)
            {
                var leg = new GameObject($"Leg{i}");
                leg.transform.SetParent(go.transform);
                leg.transform.localPosition = legPositions[i];
                leg.AddComponent<MeshFilter>().mesh = legMesh;
                leg.AddComponent<MeshRenderer>().material = bodyMat;
            }

            // Tail
            var tailMesh = ProceduralMeshBuilder.BuildCone(legRadius * 0.5f, bodyRadius * 0.2f, 4);
            var tail = new GameObject("Tail");
            tail.transform.SetParent(go.transform);
            tail.transform.localPosition = new Vector3(0, legLength + bodyRadius, -bodyLength * 0.45f);
            tail.transform.localRotation = Quaternion.Euler(60, 0, 0);
            tail.AddComponent<MeshFilter>().mesh = tailMesh;
            tail.AddComponent<MeshRenderer>().material = bodyMat;

            // Add CharacterController
            var cc = go.AddComponent<CharacterController>();
            cc.height = bodyRadius * 2f;
            cc.radius = bodyRadius;
            cc.center = new Vector3(0, legLength + bodyRadius, 0);

            return go;
        }

        /// <summary>
        /// Build a prop/static object from primitives.
        /// </summary>
        public GameObject BuildProp(string assetId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject($"Prop_{assetId}");
            go.transform.SetPositionAndRotation(position, rotation);

            var standard = GetFallbackShader();
            string type = assetId.ToLower();

            if (type.Contains("crate") || type.Contains("box"))
            {
                var mesh = ProceduralMeshBuilder.BuildBox(new Vector3(0.8f, 0.8f, 0.8f));
                var child = new GameObject("Mesh");
                child.transform.SetParent(go.transform);
                child.transform.localPosition = new Vector3(0, 0.4f, 0);
                child.AddComponent<MeshFilter>().mesh = mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.material = new Material(standard) { color = new Color(0.5f, 0.35f, 0.15f) };
                child.AddComponent<BoxCollider>();
            }
            else if (type.Contains("barrel"))
            {
                var mesh = ProceduralMeshBuilder.BuildCylinder(0.3f, 0.35f, 1f, 10);
                var child = new GameObject("Mesh");
                child.transform.SetParent(go.transform);
                child.transform.localPosition = Vector3.zero;
                child.AddComponent<MeshFilter>().mesh = mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.material = new Material(standard) { color = new Color(0.45f, 0.3f, 0.15f) };
            }
            else if (type.Contains("lamp") || type.Contains("light"))
            {
                // Post
                var postMesh = ProceduralMeshBuilder.BuildCylinder(0.03f, 0.05f, 2.5f, 6);
                var post = new GameObject("Post");
                post.transform.SetParent(go.transform);
                post.transform.localPosition = Vector3.zero;
                post.AddComponent<MeshFilter>().mesh = postMesh;
                post.AddComponent<MeshRenderer>().material = new Material(standard) { color = new Color(0.2f, 0.2f, 0.2f) };

                // Lamp globe
                var globeMesh = ProceduralMeshBuilder.BuildSphere(0.12f, 6, 8);
                var globe = new GameObject("Globe");
                globe.transform.SetParent(go.transform);
                globe.transform.localPosition = new Vector3(0, 2.6f, 0);
                globe.AddComponent<MeshFilter>().mesh = globeMesh;
                var globeMat = new Material(standard);
                globeMat.color = new Color(1f, 0.9f, 0.6f);
                globeMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.5f) * 2f);
                globeMat.EnableKeyword("_EMISSION");
                globe.AddComponent<MeshRenderer>().material = globeMat;

                // Point light
                var light = globe.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.9f, 0.7f);
                light.intensity = 1.5f;
                light.range = 10f;
            }
            else if (type.Contains("rock") || type.Contains("boulder"))
            {
                var mesh = ProceduralMeshBuilder.BuildSphere(0.6f, 5, 6);
                var verts = mesh.vertices;
                var rng = new System.Random(assetId.GetHashCode());
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] += new Vector3(
                        (float)(rng.NextDouble() * 2 - 1) * 0.15f,
                        (float)(rng.NextDouble() * 2 - 1) * 0.1f,
                        (float)(rng.NextDouble() * 2 - 1) * 0.15f);
                }
                mesh.vertices = verts;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var child = new GameObject("Mesh");
                child.transform.SetParent(go.transform);
                child.transform.localPosition = new Vector3(0, 0.3f, 0);
                child.AddComponent<MeshFilter>().mesh = mesh;
                child.AddComponent<MeshRenderer>().material =
                    new Material(standard) { color = new Color(0.5f, 0.48f, 0.45f) };
                child.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            else
            {
                // Generic prop — capsule
                var mesh = ProceduralMeshBuilder.BuildCylinder(0.2f, 0.2f, 1f, 8);
                var child = new GameObject("Mesh");
                child.transform.SetParent(go.transform);
                child.transform.localPosition = Vector3.zero;
                child.AddComponent<MeshFilter>().mesh = mesh;
                child.AddComponent<MeshRenderer>().material =
                    new Material(standard) { color = new Color(0.6f, 0.6f, 0.6f) };
            }

            return go;
        }

        /// <summary>
        /// Build a player character entity with microparticle assembly effect.
        /// </summary>
        public GameObject BuildPlayer(string assetId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject($"Player_{assetId}");
            go.transform.SetPositionAndRotation(position, rotation);

            // Naked default — skin-colored torso, dark underwear only.
            // Equipment visuals override these colors when gear is equipped.
            var skinColor = new Color(0.85f, 0.7f, 0.55f);
            var spec = new CharacterSpec
            {
                Height = 1.8f,
                BodyWidth = 0.4f,
                SkinColor = skinColor,
                ShirtColor = skinColor,                         // bare torso (matches skin)
                PantsColor = new Color(0.15f, 0.12f, 0.1f),    // dark underwear
                Archetype = "humanoid"
            };

            var character = go.AddComponent<ProceduralCharacter>();
            character.Build(spec);

            var cc = go.AddComponent<CharacterController>();
            cc.height = spec.Height;
            cc.radius = spec.BodyWidth * 0.5f;
            cc.center = new Vector3(0, spec.Height * 0.5f, 0);

            // Attach microparticle assembly effect
            if (enableAssemblyEffect)
            {
                AttachMicroparticleEffect(go);
            }

            return go;
        }

        /// <summary>
        /// Build an Aether being — permanently particle-based entity that never fully solidifies.
        /// </summary>
        public GameObject BuildAetherBeing(string assetId, Vector3 position, Quaternion rotation)
        {
            var go = BuildHumanoidNPC(assetId, position, rotation);

            // Attach microparticle system in Aether mode
            var mps = go.AddComponent<MicroparticleSystem>();
            // Set Aether-specific properties via serialized field reflection
            // (isAetherBeing = true causes permanent shimmer loop)
            var fields = typeof(MicroparticleSystem).GetField("isAetherBeing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fields != null) fields.SetValue(mps, true);

            mps.Initialize(microparticleCompute);
            mps.Assemble();

            return go;
        }

        /// <summary>
        /// Attach a MicroparticleSystem to a GameObject and trigger assembly.
        /// </summary>
        private void AttachMicroparticleEffect(GameObject go)
        {
            var mps = go.AddComponent<MicroparticleSystem>();
            mps.Initialize(microparticleCompute);
            mps.Assemble();
        }

        // ─── CDN Fallback Helper ──────────────────────────────────────

        /// <summary>
        /// Build a procedural placeholder for any entity type, used while CDN download is in progress.
        /// Delegates to the same type-specific builders used for the normal procedural path.
        /// </summary>
        private GameObject BuildProceduralFallback(uint entityType, string assetId, Vector3 position, Quaternion rotation)
        {
            if (_settlementAssets.Contains(assetId))
                return BuildSettlementAsset(assetId, position, rotation);

            switch (entityType)
            {
                case TYPE_HUMANOID_NPC: return BuildHumanoidNPC(assetId, position, rotation);
                case TYPE_ANIMAL:       return BuildAnimal(assetId, position, rotation);
                case TYPE_PROP:         return BuildProp(assetId, position, rotation);
                case TYPE_PLAYER:       return BuildPlayer(assetId, position, rotation);
                case TYPE_VEHICLE:      return BuildProp(assetId, position, rotation);
                case TYPE_INTERACTABLE: return BuildProp(assetId, position, rotation);
                default:                return BuildFallback(position, rotation);
            }
        }

        // ─── Settlement Asset Builders ─────────────────────────────────

        /// <summary>
        /// Dispatch to the appropriate settlement-specific procedural builder.
        /// </summary>
        private GameObject BuildSettlementAsset(string assetId, Vector3 position, Quaternion rotation)
        {
            switch (assetId)
            {
                case "nexus_crystal_fountain": return BuildCrystalFountain(position, rotation);
                case "frontier_cabin":        return BuildFrontierCabin(position, rotation);
                case "cooking_station":       return BuildCookingStation(position, rotation);
                case "ground_lantern":        return BuildGroundLantern(position, rotation);
                case "wall_lantern":          return BuildWallLantern(position, rotation);
                case "stone_pathway_segment": return BuildStonePathway(position, rotation);
                case "settlement_bench":      return BuildSettlementBench(position, rotation);
                case "vendor_display_table":  return BuildVendorDisplayTable(position, rotation);
                case "settlement_pine":       return BuildSettlementPine(assetId, position, rotation);
                default:                      return BuildFallback(position, rotation);
            }
        }

        private GameObject BuildCrystalFountain(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_nexus_crystal_fountain");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();

            // Circular stone platform
            var platformMesh = ProceduralMeshBuilder.BuildCylinder(1.8f, 2.0f, 0.3f, 12);
            var platform = new GameObject("Platform");
            platform.transform.SetParent(go.transform);
            platform.transform.localPosition = Vector3.zero;
            platform.AddComponent<MeshFilter>().mesh = platformMesh;
            platform.AddComponent<MeshRenderer>().material =
                new Material(standard) { color = new Color(0.45f, 0.42f, 0.4f) };
            platform.AddComponent<MeshCollider>().sharedMesh = platformMesh;

            // Central hexagonal crystal prism
            var crystalMesh = ProceduralMeshBuilder.BuildCylinder(0.2f, 0.15f, 3.0f, 6);
            var crystal = new GameObject("CrystalMain");
            crystal.transform.SetParent(go.transform);
            crystal.transform.localPosition = new Vector3(0, 0.3f, 0);
            crystal.AddComponent<MeshFilter>().mesh = crystalMesh;
            var crystalMat = new Material(standard);
            crystalMat.color = new Color(0.5f, 0.2f, 0.8f);
            crystalMat.SetColor("_EmissionColor", new Color(0.6f, 0.15f, 0.9f) * 1.5f);
            crystalMat.EnableKeyword("_EMISSION");
            crystal.AddComponent<MeshRenderer>().material = crystalMat;

            // Smaller crystal fragments around the base
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                float radius = 0.8f + (i % 2) * 0.3f;
                float height = 0.6f + (i % 3) * 0.3f;

                var fragMesh = ProceduralMeshBuilder.BuildCylinder(0.08f, 0.05f, height, 6);
                var frag = new GameObject($"CrystalFrag_{i}");
                frag.transform.SetParent(go.transform);
                frag.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.3f,
                    Mathf.Sin(angle) * radius);
                frag.transform.localRotation = Quaternion.Euler(
                    (i * 7f) % 15f - 7f, 0, (i * 11f) % 15f - 7f);
                frag.AddComponent<MeshFilter>().mesh = fragMesh;
                var fragMat = new Material(standard);
                fragMat.color = new Color(0.6f, 0.25f, 0.85f);
                fragMat.SetColor("_EmissionColor", new Color(0.5f, 0.1f, 0.7f) * 1.0f);
                fragMat.EnableKeyword("_EMISSION");
                frag.AddComponent<MeshRenderer>().material = fragMat;
            }

            // Purple point light
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform);
            lightGo.transform.localPosition = new Vector3(0, 2.5f, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.2f, 0.9f);
            light.intensity = 2.0f;
            light.range = 8f;

            return go;
        }

        private GameObject BuildFrontierCabin(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_frontier_cabin");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();
            var woodMat = new Material(standard) { color = new Color(0.45f, 0.3f, 0.15f) };

            // Rectangular base box (3x2x2.5)
            var baseMesh = ProceduralMeshBuilder.BuildBox(new Vector3(3f, 2f, 2.5f));
            var baseObj = new GameObject("Base");
            baseObj.transform.SetParent(go.transform);
            baseObj.transform.localPosition = new Vector3(0, 1f, 0);
            baseObj.AddComponent<MeshFilter>().mesh = baseMesh;
            baseObj.AddComponent<MeshRenderer>().material = woodMat;
            baseObj.AddComponent<BoxCollider>();

            // A-frame roof — triangular prism using two angled quads
            var roofMat = new Material(standard) { color = new Color(0.35f, 0.22f, 0.1f) };
            float roofWidth = 1.8f;
            float roofAngle = 40f;

            var roofLeft = new GameObject("RoofLeft");
            roofLeft.transform.SetParent(go.transform);
            roofLeft.transform.localPosition = new Vector3(-roofWidth * 0.4f, 2.4f, 0);
            roofLeft.transform.localRotation = Quaternion.Euler(0, 0, roofAngle);
            var roofMeshL = ProceduralMeshBuilder.BuildQuad(roofWidth, 3.2f);
            roofLeft.AddComponent<MeshFilter>().mesh = roofMeshL;
            roofLeft.AddComponent<MeshRenderer>().material = roofMat;

            var roofRight = new GameObject("RoofRight");
            roofRight.transform.SetParent(go.transform);
            roofRight.transform.localPosition = new Vector3(roofWidth * 0.4f, 2.4f, 0);
            roofRight.transform.localRotation = Quaternion.Euler(0, 0, -roofAngle);
            var roofMeshR = ProceduralMeshBuilder.BuildQuad(roofWidth, 3.2f);
            roofRight.AddComponent<MeshFilter>().mesh = roofMeshR;
            roofRight.AddComponent<MeshRenderer>().material = roofMat;

            // Two purple-glowing windows
            var windowMat = new Material(standard);
            windowMat.color = new Color(0.5f, 0.3f, 0.8f);
            windowMat.SetColor("_EmissionColor", new Color(0.4f, 0.15f, 0.7f) * 1.2f);
            windowMat.EnableKeyword("_EMISSION");

            var win1 = new GameObject("Window1");
            win1.transform.SetParent(go.transform);
            win1.transform.localPosition = new Vector3(-0.7f, 1.3f, 1.26f);
            win1.AddComponent<MeshFilter>().mesh = ProceduralMeshBuilder.BuildQuad(0.4f, 0.4f);
            win1.AddComponent<MeshRenderer>().material = windowMat;

            var win2 = new GameObject("Window2");
            win2.transform.SetParent(go.transform);
            win2.transform.localPosition = new Vector3(0.7f, 1.3f, 1.26f);
            win2.AddComponent<MeshFilter>().mesh = ProceduralMeshBuilder.BuildQuad(0.4f, 0.4f);
            win2.AddComponent<MeshRenderer>().material = windowMat;

            // Door opening (dark recessed quad)
            var doorMat = new Material(standard) { color = new Color(0.1f, 0.08f, 0.05f) };
            var door = new GameObject("Door");
            door.transform.SetParent(go.transform);
            door.transform.localPosition = new Vector3(0, 0.8f, 1.26f);
            door.AddComponent<MeshFilter>().mesh = ProceduralMeshBuilder.BuildQuad(0.7f, 1.5f);
            door.AddComponent<MeshRenderer>().material = doorMat;

            return go;
        }

        private GameObject BuildCookingStation(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_cooking_station");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();

            // Dark grey cube base
            var baseMesh = ProceduralMeshBuilder.BuildBox(new Vector3(0.6f, 0.6f, 0.6f));
            var baseObj = new GameObject("Base");
            baseObj.transform.SetParent(go.transform);
            baseObj.transform.localPosition = new Vector3(0, 0.3f, 0);
            baseObj.AddComponent<MeshFilter>().mesh = baseMesh;
            baseObj.AddComponent<MeshRenderer>().material =
                new Material(standard) { color = new Color(0.25f, 0.25f, 0.28f) };
            baseObj.AddComponent<BoxCollider>();

            // Copper hemisphere on top (the pot) — use top half of a sphere
            var potMesh = ProceduralMeshBuilder.BuildSphere(0.25f, 8, 10);
            var pot = new GameObject("Pot");
            pot.transform.SetParent(go.transform);
            pot.transform.localPosition = new Vector3(0, 0.7f, 0);
            pot.AddComponent<MeshFilter>().mesh = potMesh;
            pot.AddComponent<MeshRenderer>().material =
                new Material(standard) { color = new Color(0.72f, 0.45f, 0.2f) };

            // Blue emissive strip on front (heating element)
            var stripMat = new Material(standard);
            stripMat.color = new Color(0.2f, 0.5f, 0.9f);
            stripMat.SetColor("_EmissionColor", new Color(0.1f, 0.4f, 1f) * 1.5f);
            stripMat.EnableKeyword("_EMISSION");
            var strip = new GameObject("HeatStrip");
            strip.transform.SetParent(go.transform);
            strip.transform.localPosition = new Vector3(0, 0.15f, 0.31f);
            strip.AddComponent<MeshFilter>().mesh = ProceduralMeshBuilder.BuildQuad(0.4f, 0.08f);
            strip.AddComponent<MeshRenderer>().material = stripMat;

            return go;
        }

        private GameObject BuildGroundLantern(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_ground_lantern");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();

            // Small glowing box
            var boxMesh = ProceduralMeshBuilder.BuildBox(new Vector3(0.2f, 0.25f, 0.2f));
            var box = new GameObject("Lantern");
            box.transform.SetParent(go.transform);
            box.transform.localPosition = new Vector3(0, 0.125f, 0);
            box.AddComponent<MeshFilter>().mesh = boxMesh;
            var mat = new Material(standard);
            mat.color = new Color(1f, 0.85f, 0.5f);
            mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.4f) * 1.5f);
            mat.EnableKeyword("_EMISSION");
            box.AddComponent<MeshRenderer>().material = mat;
            box.AddComponent<BoxCollider>();

            // Warm point light
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform);
            lightGo.transform.localPosition = new Vector3(0, 0.3f, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.intensity = 0.8f;
            light.range = 3f;

            return go;
        }

        private GameObject BuildWallLantern(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_wall_lantern");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();

            // Iron bracket (thin cylinder)
            var bracketMesh = ProceduralMeshBuilder.BuildCylinder(0.02f, 0.02f, 0.3f, 5);
            var bracket = new GameObject("Bracket");
            bracket.transform.SetParent(go.transform);
            bracket.transform.localPosition = new Vector3(0, 0, 0);
            bracket.transform.localRotation = Quaternion.Euler(0, 0, 90);
            bracket.AddComponent<MeshFilter>().mesh = bracketMesh;
            bracket.AddComponent<MeshRenderer>().material =
                new Material(standard) { color = new Color(0.2f, 0.2f, 0.22f) };

            // Glowing sphere
            var globeMesh = ProceduralMeshBuilder.BuildSphere(0.1f, 6, 8);
            var globe = new GameObject("Globe");
            globe.transform.SetParent(go.transform);
            globe.transform.localPosition = new Vector3(0.3f, 0, 0);
            globe.AddComponent<MeshFilter>().mesh = globeMesh;
            var globeMat = new Material(standard);
            globeMat.color = new Color(1f, 0.9f, 0.6f);
            globeMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.5f) * 2f);
            globeMat.EnableKeyword("_EMISSION");
            globe.AddComponent<MeshRenderer>().material = globeMat;

            // Warm point light
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(go.transform);
            lightGo.transform.localPosition = new Vector3(0.3f, 0, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.intensity = 0.8f;
            light.range = 3f;

            return go;
        }

        private GameObject BuildStonePathway(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_stone_pathway_segment");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();

            // Flat dark grey quad at ground level
            var quadMesh = ProceduralMeshBuilder.BuildQuad(2f, 2f);
            var quad = new GameObject("Surface");
            quad.transform.SetParent(go.transform);
            quad.transform.localPosition = new Vector3(0, 0.02f, 0);
            quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            quad.AddComponent<MeshFilter>().mesh = quadMesh;
            quad.AddComponent<MeshRenderer>().material =
                new Material(standard) { color = new Color(0.35f, 0.33f, 0.3f) };
            quad.AddComponent<BoxCollider>().size = new Vector3(2f, 0.04f, 2f);

            return go;
        }

        private GameObject BuildSettlementBench(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_settlement_bench");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();
            var woodMat = new Material(standard) { color = new Color(0.45f, 0.3f, 0.15f) };

            // Seat plank
            var seatMesh = ProceduralMeshBuilder.BuildBox(new Vector3(1.2f, 0.08f, 0.4f));
            var seat = new GameObject("Seat");
            seat.transform.SetParent(go.transform);
            seat.transform.localPosition = new Vector3(0, 0.4f, 0);
            seat.AddComponent<MeshFilter>().mesh = seatMesh;
            seat.AddComponent<MeshRenderer>().material = woodMat;
            seat.AddComponent<BoxCollider>();

            // Two legs
            var legMesh = ProceduralMeshBuilder.BuildBox(new Vector3(0.08f, 0.4f, 0.35f));
            var leg1 = new GameObject("Leg1");
            leg1.transform.SetParent(go.transform);
            leg1.transform.localPosition = new Vector3(-0.45f, 0.2f, 0);
            leg1.AddComponent<MeshFilter>().mesh = legMesh;
            leg1.AddComponent<MeshRenderer>().material = woodMat;

            var leg2 = new GameObject("Leg2");
            leg2.transform.SetParent(go.transform);
            leg2.transform.localPosition = new Vector3(0.45f, 0.2f, 0);
            leg2.AddComponent<MeshFilter>().mesh = legMesh;
            leg2.AddComponent<MeshRenderer>().material = woodMat;

            return go;
        }

        private GameObject BuildVendorDisplayTable(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_vendor_display_table");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();
            var woodMat = new Material(standard) { color = new Color(0.5f, 0.35f, 0.18f) };

            // Table surface
            var topMesh = ProceduralMeshBuilder.BuildBox(new Vector3(1.4f, 0.06f, 0.7f));
            var top = new GameObject("Surface");
            top.transform.SetParent(go.transform);
            top.transform.localPosition = new Vector3(0, 0.75f, 0);
            top.AddComponent<MeshFilter>().mesh = topMesh;
            top.AddComponent<MeshRenderer>().material = woodMat;
            top.AddComponent<BoxCollider>();

            // Two X-frame legs (simplified as angled boxes)
            var legMesh = ProceduralMeshBuilder.BuildBox(new Vector3(0.06f, 0.7f, 0.5f));
            var leg1 = new GameObject("Leg1");
            leg1.transform.SetParent(go.transform);
            leg1.transform.localPosition = new Vector3(-0.5f, 0.37f, 0);
            leg1.transform.localRotation = Quaternion.Euler(0, 0, 8f);
            leg1.AddComponent<MeshFilter>().mesh = legMesh;
            leg1.AddComponent<MeshRenderer>().material = woodMat;

            var leg2 = new GameObject("Leg2");
            leg2.transform.SetParent(go.transform);
            leg2.transform.localPosition = new Vector3(0.5f, 0.37f, 0);
            leg2.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            leg2.AddComponent<MeshFilter>().mesh = legMesh;
            leg2.AddComponent<MeshRenderer>().material = woodMat;

            // Small colored boxes on top representing wares
            Color[] wareColors = {
                new Color(0.8f, 0.2f, 0.2f),
                new Color(0.2f, 0.6f, 0.3f),
                new Color(0.3f, 0.3f, 0.8f),
                new Color(0.9f, 0.7f, 0.1f),
            };
            var wareMesh = ProceduralMeshBuilder.BuildBox(new Vector3(0.15f, 0.12f, 0.12f));
            for (int i = 0; i < wareColors.Length; i++)
            {
                var ware = new GameObject($"Ware_{i}");
                ware.transform.SetParent(go.transform);
                ware.transform.localPosition = new Vector3(
                    -0.4f + i * 0.28f, 0.84f, (i % 2 == 0) ? 0.1f : -0.1f);
                ware.AddComponent<MeshFilter>().mesh = wareMesh;
                ware.AddComponent<MeshRenderer>().material =
                    new Material(standard) { color = wareColors[i] };
            }

            return go;
        }

        private GameObject BuildSettlementPine(string assetId, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Prop_settlement_pine");
            go.transform.SetPositionAndRotation(position, rotation);
            var standard = GetFallbackShader();

            // Use assetId hash for slight variation
            int hash = assetId.GetHashCode();
            float heightVar = 1f + (hash % 20) * 0.02f; // 1.0 to 1.38
            float rotVar = (hash % 360);

            go.transform.localRotation *= Quaternion.Euler(0, rotVar, 0);

            // Brown cylinder trunk
            float trunkHeight = 1.5f * heightVar;
            var trunkMesh = ProceduralMeshBuilder.BuildCylinder(0.08f, 0.12f, trunkHeight, 6);
            var trunk = new GameObject("Trunk");
            trunk.transform.SetParent(go.transform);
            trunk.transform.localPosition = Vector3.zero;
            trunk.AddComponent<MeshFilter>().mesh = trunkMesh;
            trunk.AddComponent<MeshRenderer>().material =
                new Material(standard) { color = new Color(0.4f, 0.25f, 0.1f) };

            // 3 stacked green cones of decreasing size
            var greenMat = new Material(standard) { color = new Color(0.15f, 0.4f, 0.12f) };
            float[] coneRadii = { 0.7f, 0.5f, 0.3f };
            float[] coneHeights = { 1.0f, 0.85f, 0.7f };
            float yOffset = trunkHeight * 0.6f;

            for (int i = 0; i < 3; i++)
            {
                float r = coneRadii[i] * heightVar;
                float h = coneHeights[i] * heightVar;
                var coneMesh = ProceduralMeshBuilder.BuildCone(r, h, 8);
                var cone = new GameObject($"Canopy_{i}");
                cone.transform.SetParent(go.transform);
                cone.transform.localPosition = new Vector3(0, yOffset, 0);
                cone.AddComponent<MeshFilter>().mesh = coneMesh;
                cone.AddComponent<MeshRenderer>().material = greenMat;
                yOffset += h * 0.6f;
            }

            // Simple capsule collider area
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, trunkHeight * 0.5f + 0.5f, 0);
            col.radius = 0.5f;
            col.height = trunkHeight + 1.5f;

            return go;
        }

        private GameObject BuildFallback(Vector3 position, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Entity_Fallback";
            go.transform.SetPositionAndRotation(position, rotation);
            go.GetComponent<MeshRenderer>().material = MakeMat(new Color(0.4f, 0.4f, 0.45f));
            return go;
        }

        private CharacterSpec ResolveCharacterSpec(string assetId)
        {
            string id = assetId.ToLower();

            if (id.Contains("guard"))
            {
                return new CharacterSpec
                {
                    Height = 1.9f,
                    BodyWidth = 0.45f,
                    SkinColor = new Color(0.75f, 0.6f, 0.45f),
                    ShirtColor = new Color(0.5f, 0.5f, 0.55f),
                    PantsColor = new Color(0.3f, 0.3f, 0.35f),
                    Archetype = "stocky"
                };
            }

            if (id.Contains("bandit"))
            {
                return new CharacterSpec
                {
                    Height = 1.75f,
                    BodyWidth = 0.38f,
                    SkinColor = new Color(0.65f, 0.5f, 0.35f),
                    ShirtColor = new Color(0.15f, 0.12f, 0.1f),
                    PantsColor = new Color(0.2f, 0.18f, 0.15f),
                    Archetype = "slender"
                };
            }

            if (id.Contains("merchant") || id.Contains("villager"))
            {
                return new CharacterSpec
                {
                    Height = 1.7f,
                    BodyWidth = 0.4f,
                    SkinColor = new Color(0.85f, 0.72f, 0.55f),
                    ShirtColor = new Color(0.6f, 0.5f, 0.3f),
                    PantsColor = new Color(0.35f, 0.3f, 0.2f),
                    Archetype = "humanoid"
                };
            }

            // Default
            return new CharacterSpec();
        }

        private void EquipNPC(ProceduralCharacter character, string assetId)
        {
            string id = assetId.ToLower();

            if (id.Contains("guard"))
            {
                var sword = ProceduralEquipment.CreateSword(0.7f, EquipmentStyle.Basic);
                character.Equip(EquipmentSlot.RightHand, sword);

                var shield = ProceduralEquipment.CreateShield(0.4f, EquipmentStyle.Rugged);
                character.Equip(EquipmentSlot.LeftBracer, shield); // Shield held in left hand

                var helmet = ProceduralEquipment.CreateHelmet(EquipmentStyle.Rugged);
                character.Equip(EquipmentSlot.Head, helmet);
            }
            else if (id.Contains("bandit"))
            {
                var sword = ProceduralEquipment.CreateSword(0.65f, EquipmentStyle.Rugged);
                character.Equip(EquipmentSlot.RightHand, sword);
            }
            else if (id.Contains("mage") || id.Contains("wizard"))
            {
                var staff = ProceduralEquipment.CreateStaff(1.4f);
                character.Equip(EquipmentSlot.RightHand, staff);

                var hat = ProceduralEquipment.CreateHelmet(EquipmentStyle.Elegant);
                character.Equip(EquipmentSlot.Head, hat);
            }
        }

        /// <summary>
        /// Helper MonoBehaviour tag for pool key tracking.
        /// </summary>
        public class PoolTag : MonoBehaviour
        {
            public string PoolKey;
        }
    }
}

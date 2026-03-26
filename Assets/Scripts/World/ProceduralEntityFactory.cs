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

            // Build new
            GameObject go;
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

            // Tag with pool key for recycling
            var tag = go.AddComponent<PoolTag>();
            tag.PoolKey = poolKey;

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

            var standard = Shader.Find("Standard");
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

            var standard = Shader.Find("Standard");
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

            var spec = new CharacterSpec
            {
                Height = 1.8f,
                BodyWidth = 0.4f,
                SkinColor = new Color(0.85f, 0.7f, 0.55f),
                ShirtColor = new Color(0.2f, 0.35f, 0.6f),
                PantsColor = new Color(0.2f, 0.2f, 0.25f),
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

        private GameObject BuildFallback(Vector3 position, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Entity_Fallback";
            go.transform.SetPositionAndRotation(position, rotation);
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
                character.Equip(EquipmentSlot.MainHand, sword);

                var shield = ProceduralEquipment.CreateShield(0.4f, EquipmentStyle.Rugged);
                character.Equip(EquipmentSlot.OffHand, shield);

                var helmet = ProceduralEquipment.CreateHelmet(EquipmentStyle.Rugged);
                character.Equip(EquipmentSlot.Head, helmet);
            }
            else if (id.Contains("bandit"))
            {
                var sword = ProceduralEquipment.CreateSword(0.65f, EquipmentStyle.Rugged);
                character.Equip(EquipmentSlot.MainHand, sword);
            }
            else if (id.Contains("mage") || id.Contains("wizard"))
            {
                var staff = ProceduralEquipment.CreateStaff(1.4f);
                character.Equip(EquipmentSlot.MainHand, staff);

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

using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Style variants for equipment generation.
    /// </summary>
    public enum EquipmentStyle
    {
        Basic,
        Ornate,
        Rugged,
        Elegant
    }

    /// <summary>
    /// Builds equipment meshes entirely from procedural primitives.
    /// All methods return a ready-to-use GameObject with MeshRenderer.
    /// </summary>
    public static class ProceduralEquipment
    {
        private static Material _metalMaterial;
        private static Material _woodMaterial;
        private static Material _leatherMaterial;
        private static Material _crystalMaterial;

        private static void EnsureMaterials()
        {
            if (_metalMaterial != null) return;

            _metalMaterial = Orlo.Rendering.OrloShaders.CreateLit(new Color(0.7f, 0.7f, 0.75f), 0.8f, 0.6f);

            _woodMaterial = Orlo.Rendering.OrloShaders.CreateLit(new Color(0.4f, 0.28f, 0.12f), 0f, 0.2f);

            _leatherMaterial = Orlo.Rendering.OrloShaders.CreateLit(new Color(0.35f, 0.2f, 0.1f), 0f, 0.15f);

            _crystalMaterial = Orlo.Rendering.OrloShaders.CreateTransparent(new Color(0.3f, 0.6f, 0.9f, 0.8f));
            _crystalMaterial.SetFloat("_Metallic", 0.3f);
            if (_crystalMaterial.HasProperty("_Smoothness"))
                _crystalMaterial.SetFloat("_Smoothness", 0.9f);
            _crystalMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _crystalMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _crystalMaterial.SetInt("_ZWrite", 0);
            _crystalMaterial.EnableKeyword("_ALPHABLEND_ON");
        }

        /// <summary>
        /// Create a sword with blade, grip, guard, and pommel.
        /// </summary>
        /// <param name="length">Total blade length</param>
        /// <param name="style">Visual style variant</param>
        public static GameObject CreateSword(float length = 0.8f, EquipmentStyle style = EquipmentStyle.Basic)
        {
            EnsureMaterials();

            var root = new GameObject("Sword");

            float bladeWidth = length * 0.04f;
            float bladeLength = length;
            float gripLength = length * 0.25f;
            float guardWidth = length * 0.15f;
            float pommelRadius = length * 0.03f;

            // Blade
            Mesh bladeMesh;
            switch (style)
            {
                case EquipmentStyle.Ornate:
                    // Wider, tapered blade
                    bladeMesh = ProceduralMeshBuilder.BuildCylinder(0f, bladeWidth * 1.5f, bladeLength, 4);
                    break;
                case EquipmentStyle.Rugged:
                    // Thick, rough blade
                    bladeMesh = ProceduralMeshBuilder.BuildBox(new Vector3(bladeWidth * 2.5f, bladeLength, bladeWidth * 1.5f));
                    break;
                default:
                    // Standard flat blade
                    bladeMesh = ProceduralMeshBuilder.BuildBox(new Vector3(bladeWidth * 2f, bladeLength, bladeWidth * 0.3f));
                    break;
            }

            var bladeGo = CreateMeshChild(root, "Blade", bladeMesh, _metalMaterial,
                new Vector3(0, gripLength + bladeLength * 0.5f, 0));

            // Guard (crossguard)
            var guardMesh = ProceduralMeshBuilder.BuildBox(new Vector3(guardWidth, gripLength * 0.15f, bladeWidth * 2f));
            CreateMeshChild(root, "Guard", guardMesh, _metalMaterial,
                new Vector3(0, gripLength, 0));

            // Add guard ornaments for ornate style
            if (style == EquipmentStyle.Ornate)
            {
                var ornL = ProceduralMeshBuilder.BuildSphere(pommelRadius * 0.8f, 4, 5);
                CreateMeshChild(root, "GuardOrnL", ornL, _metalMaterial,
                    new Vector3(-guardWidth * 0.5f, gripLength, 0));
                var ornR = ProceduralMeshBuilder.BuildSphere(pommelRadius * 0.8f, 4, 5);
                CreateMeshChild(root, "GuardOrnR", ornR, _metalMaterial,
                    new Vector3(guardWidth * 0.5f, gripLength, 0));
            }

            // Grip
            var gripMesh = ProceduralMeshBuilder.BuildCylinder(bladeWidth * 0.7f, bladeWidth * 0.8f, gripLength, 6);
            CreateMeshChild(root, "Grip", gripMesh, _woodMaterial, Vector3.zero);

            // Grip wrap for non-basic styles
            if (style != EquipmentStyle.Basic)
            {
                var wrapMesh = ProceduralMeshBuilder.BuildCylinder(bladeWidth * 0.9f, bladeWidth * 0.9f, gripLength * 0.6f, 6);
                CreateMeshChild(root, "GripWrap", wrapMesh, _leatherMaterial,
                    new Vector3(0, gripLength * 0.2f, 0));
            }

            // Pommel
            var pommelMesh = ProceduralMeshBuilder.BuildSphere(pommelRadius, 5, 6);
            CreateMeshChild(root, "Pommel", pommelMesh, _metalMaterial,
                new Vector3(0, -pommelRadius, 0));

            return root;
        }

        /// <summary>
        /// Create a shield.
        /// </summary>
        /// <param name="size">Overall shield size multiplier</param>
        /// <param name="style">Visual style variant</param>
        public static GameObject CreateShield(float size = 0.5f, EquipmentStyle style = EquipmentStyle.Basic)
        {
            EnsureMaterials();

            var root = new GameObject("Shield");

            Mesh shieldMesh;
            switch (style)
            {
                case EquipmentStyle.Ornate:
                    // Round shield — flattened sphere
                    shieldMesh = ProceduralMeshBuilder.BuildSphere(size, 8, 10);
                    // Flatten Z
                    var verts = shieldMesh.vertices;
                    for (int i = 0; i < verts.Length; i++)
                        verts[i] = new Vector3(verts[i].x, verts[i].y, verts[i].z * 0.15f);
                    shieldMesh.vertices = verts;
                    shieldMesh.RecalculateNormals();
                    shieldMesh.RecalculateBounds();
                    break;

                case EquipmentStyle.Rugged:
                    // Thick rectangular shield
                    shieldMesh = ProceduralMeshBuilder.BuildBox(new Vector3(size * 1.2f, size * 1.6f, size * 0.12f));
                    break;

                default:
                    // Kite/heater shield — flattened cylinder
                    shieldMesh = ProceduralMeshBuilder.BuildCylinder(size * 0.3f, size * 0.6f, size * 1.4f, 6);
                    var v2 = shieldMesh.vertices;
                    for (int i = 0; i < v2.Length; i++)
                        v2[i] = new Vector3(v2[i].x, v2[i].y, v2[i].z * 0.15f);
                    shieldMesh.vertices = v2;
                    shieldMesh.RecalculateNormals();
                    shieldMesh.RecalculateBounds();
                    break;
            }

            CreateMeshChild(root, "ShieldBody", shieldMesh, _metalMaterial, Vector3.zero);

            // Shield boss (center bump)
            var bossMesh = ProceduralMeshBuilder.BuildSphere(size * 0.12f, 5, 6);
            CreateMeshChild(root, "Boss", bossMesh, _metalMaterial,
                new Vector3(0, 0, size * 0.08f));

            // Rim for ornate
            if (style == EquipmentStyle.Ornate)
            {
                var rimMesh = ProceduralMeshBuilder.BuildTorus(size * 0.85f, size * 0.03f, 16, 6);
                var rimVerts = rimMesh.vertices;
                for (int i = 0; i < rimVerts.Length; i++)
                    rimVerts[i] = new Vector3(rimVerts[i].x, rimVerts[i].y, rimVerts[i].z * 0.2f);
                rimMesh.vertices = rimVerts;
                rimMesh.RecalculateNormals();
                rimMesh.RecalculateBounds();
                CreateMeshChild(root, "Rim", rimMesh, _metalMaterial, Vector3.zero);
            }

            // Handle on back
            var handleMesh = ProceduralMeshBuilder.BuildCylinder(size * 0.02f, size * 0.02f, size * 0.3f, 4);
            CreateMeshChild(root, "Handle", handleMesh, _leatherMaterial,
                new Vector3(0, 0, -size * 0.06f));

            return root;
        }

        /// <summary>
        /// Create a helmet.
        /// </summary>
        /// <param name="style">Visual style variant</param>
        public static GameObject CreateHelmet(EquipmentStyle style = EquipmentStyle.Basic)
        {
            EnsureMaterials();

            var root = new GameObject("Helmet");

            float headRadius = 0.12f;

            // Base dome
            var domeMesh = ProceduralMeshBuilder.BuildSphere(headRadius * 1.1f, 8, 10);
            // Cut bottom half by adjusting vertices (push below-equator verts to equator)
            var domeVerts = domeMesh.vertices;
            for (int i = 0; i < domeVerts.Length; i++)
            {
                if (domeVerts[i].y < -headRadius * 0.15f)
                    domeVerts[i] = new Vector3(domeVerts[i].x, -headRadius * 0.15f, domeVerts[i].z);
            }
            domeMesh.vertices = domeVerts;
            domeMesh.RecalculateNormals();
            domeMesh.RecalculateBounds();
            CreateMeshChild(root, "Dome", domeMesh, _metalMaterial, Vector3.zero);

            switch (style)
            {
                case EquipmentStyle.Ornate:
                    // Visor
                    var visorMesh = ProceduralMeshBuilder.BuildBox(
                        new Vector3(headRadius * 1.6f, headRadius * 0.15f, headRadius * 0.3f));
                    CreateMeshChild(root, "Visor", visorMesh, _metalMaterial,
                        new Vector3(0, -headRadius * 0.05f, headRadius * 0.9f));

                    // Horns
                    var hornL = ProceduralMeshBuilder.BuildCone(headRadius * 0.06f, headRadius * 0.6f, 5);
                    var hornLGo = CreateMeshChild(root, "HornL", hornL, _metalMaterial,
                        new Vector3(-headRadius * 0.7f, headRadius * 0.5f, 0));
                    hornLGo.transform.localRotation = Quaternion.Euler(0, 0, 30f);

                    var hornR = ProceduralMeshBuilder.BuildCone(headRadius * 0.06f, headRadius * 0.6f, 5);
                    var hornRGo = CreateMeshChild(root, "HornR", hornR, _metalMaterial,
                        new Vector3(headRadius * 0.7f, headRadius * 0.5f, 0));
                    hornRGo.transform.localRotation = Quaternion.Euler(0, 0, -30f);
                    break;

                case EquipmentStyle.Rugged:
                    // Nose guard
                    var noseMesh = ProceduralMeshBuilder.BuildBox(
                        new Vector3(headRadius * 0.1f, headRadius * 0.8f, headRadius * 0.05f));
                    CreateMeshChild(root, "NoseGuard", noseMesh, _metalMaterial,
                        new Vector3(0, -headRadius * 0.1f, headRadius * 1.05f));

                    // Cheek plates
                    var cheekL = ProceduralMeshBuilder.BuildBox(
                        new Vector3(headRadius * 0.05f, headRadius * 0.4f, headRadius * 0.3f));
                    CreateMeshChild(root, "CheekL", cheekL, _metalMaterial,
                        new Vector3(-headRadius * 0.9f, -headRadius * 0.15f, headRadius * 0.3f));
                    var cheekR = ProceduralMeshBuilder.BuildBox(
                        new Vector3(headRadius * 0.05f, headRadius * 0.4f, headRadius * 0.3f));
                    CreateMeshChild(root, "CheekR", cheekR, _metalMaterial,
                        new Vector3(headRadius * 0.9f, -headRadius * 0.15f, headRadius * 0.3f));
                    break;

                case EquipmentStyle.Elegant:
                    // Crown-like crest
                    var crestMesh = ProceduralMeshBuilder.BuildBox(
                        new Vector3(headRadius * 0.05f, headRadius * 0.5f, headRadius * 1.5f));
                    CreateMeshChild(root, "Crest", crestMesh, _metalMaterial,
                        new Vector3(0, headRadius * 1.0f, 0));

                    // Gem on front
                    var gemMesh = ProceduralMeshBuilder.BuildSphere(headRadius * 0.06f, 4, 5);
                    CreateMeshChild(root, "Gem", gemMesh, _crystalMaterial,
                        new Vector3(0, headRadius * 0.4f, headRadius * 1.0f));
                    break;
            }

            return root;
        }

        /// <summary>
        /// Create a staff with crystal topper.
        /// </summary>
        /// <param name="length">Total staff length</param>
        public static GameObject CreateStaff(float length = 1.5f)
        {
            EnsureMaterials();

            var root = new GameObject("Staff");

            float baseRadius = length * 0.02f;
            float tipRadius = length * 0.01f;

            // Main shaft — tapered cylinder
            var shaftMesh = ProceduralMeshBuilder.BuildCylinder(tipRadius, baseRadius, length, 6);
            CreateMeshChild(root, "Shaft", shaftMesh, _woodMaterial, Vector3.zero);

            // Grip wrap in the middle
            var wrapMesh = ProceduralMeshBuilder.BuildCylinder(
                baseRadius * 1.3f, baseRadius * 1.3f, length * 0.15f, 6);
            CreateMeshChild(root, "GripWrap", wrapMesh, _leatherMaterial,
                new Vector3(0, length * 0.3f, 0));

            // Crystal icosahedron at the top
            var crystalMesh = BuildIcosahedron(length * 0.05f);
            CreateMeshChild(root, "Crystal", crystalMesh, _crystalMaterial,
                new Vector3(0, length + length * 0.04f, 0));

            // Prongs holding the crystal
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                float px = Mathf.Cos(angle) * baseRadius * 0.5f;
                float pz = Mathf.Sin(angle) * baseRadius * 0.5f;

                var prongMesh = ProceduralMeshBuilder.BuildCylinder(
                    tipRadius * 0.3f, tipRadius * 0.5f, length * 0.06f, 4);
                var prongGo = CreateMeshChild(root, $"Prong{i}", prongMesh, _metalMaterial,
                    new Vector3(px, length * 0.97f, pz));
                prongGo.transform.localRotation = Quaternion.Euler(
                    -Mathf.Sin(angle) * 15f, 0, Mathf.Cos(angle) * 15f);
            }

            // Bottom cap
            var capMesh = ProceduralMeshBuilder.BuildSphere(baseRadius * 1.2f, 4, 5);
            CreateMeshChild(root, "BottomCap", capMesh, _metalMaterial, Vector3.zero);

            return root;
        }

        /// <summary>
        /// Attach an equipment GameObject to a bone transform.
        /// </summary>
        public static void AttachToBone(GameObject equipment, Transform bone)
        {
            if (equipment == null || bone == null) return;
            equipment.transform.SetParent(bone);
            equipment.transform.localPosition = Vector3.zero;
            equipment.transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Build an icosahedron mesh (20-face polyhedron) for crystal/gem geometry.
        /// </summary>
        private static Mesh BuildIcosahedron(float radius)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f; // Golden ratio
            float s = radius / Mathf.Sqrt(1f + t * t);

            var vertices = new Vector3[]
            {
                new Vector3(-1,  t,  0) * s,
                new Vector3( 1,  t,  0) * s,
                new Vector3(-1, -t,  0) * s,
                new Vector3( 1, -t,  0) * s,
                new Vector3( 0, -1,  t) * s,
                new Vector3( 0,  1,  t) * s,
                new Vector3( 0, -1, -t) * s,
                new Vector3( 0,  1, -t) * s,
                new Vector3( t,  0, -1) * s,
                new Vector3( t,  0,  1) * s,
                new Vector3(-t,  0, -1) * s,
                new Vector3(-t,  0,  1) * s,
            };

            var triangles = new int[]
            {
                0,11,5,   0,5,1,    0,1,7,   0,7,10,  0,10,11,
                1,5,9,    5,11,4,   11,10,2,  10,7,6,  7,1,8,
                3,9,4,    3,4,2,    3,2,6,    3,6,8,   3,8,9,
                4,9,5,    2,4,11,   6,2,10,   8,6,7,   9,8,1,
            };

            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                normals[i] = vertices[i].normalized;

            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var n = normals[i];
                uvs[i] = new Vector2(
                    0.5f + Mathf.Atan2(n.z, n.x) / (2f * Mathf.PI),
                    0.5f + Mathf.Asin(n.y) / Mathf.PI);
            }

            var mesh = new Mesh { name = "Icosahedron" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateMeshChild(GameObject parent, string name,
            Mesh mesh, Material material, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;

            var mf = child.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            var mr = child.AddComponent<MeshRenderer>();
            mr.material = material;

            return child;
        }
    }
}

using UnityEngine;
using Orlo.Rendering;

namespace Orlo.UI.Lobby
{
    /// <summary>
    /// Circular rotating platform for the character model in the lobby.
    /// Procedural disc mesh with a glowing emissive edge ring and floating motes.
    /// </summary>
    public class CharacterPlatform : MonoBehaviour
    {
        public static CharacterPlatform Instance { get; private set; }

        private const int SEGMENTS = 64;
        private const float RADIUS = 1.5f;
        private const float EDGE_WIDTH = 0.1f;
        private const float INNER_RADIUS = RADIUS - EDGE_WIDTH;
        private const float ROTATION_SPEED = 3f; // degrees per second
        private const int CONCENTRIC_RINGS = 6;

        private Material _discMaterial;
        private Material _edgeMaterial;
        private MeshRenderer _renderer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            gameObject.layer = 10; // CharacterPreview
            transform.position = Vector3.zero;

            BuildMesh();
            CreateMotes();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, ROTATION_SPEED * Time.deltaTime, Space.World);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// Change the edge ring emission color for race-specific theming.
        /// </summary>
        public void SetGlowColor(Color c)
        {
            if (_edgeMaterial != null)
            {
                _edgeMaterial.EnableKeyword("_EMISSION");
                _edgeMaterial.SetColor("_EmissionColor", c * 2f);
            }
        }

        private void BuildMesh()
        {
            var mesh = new Mesh { name = "PlatformDisc" };

            // Inner disc: center vertex + CONCENTRIC_RINGS rings up to INNER_RADIUS
            // Outer ring: one ring at INNER_RADIUS and one at RADIUS
            int innerVertCount = 1 + CONCENTRIC_RINGS * SEGMENTS;
            int outerVertCount = SEGMENTS * 2;
            int totalVerts = innerVertCount + outerVertCount;

            var vertices = new Vector3[totalVerts];
            var normals = new Vector3[totalVerts];
            var colors = new Color[totalVerts];

            // All normals point up
            for (int i = 0; i < totalVerts; i++)
                normals[i] = Vector3.up;

            // --- Inner disc vertices ---
            // Center
            vertices[0] = Vector3.zero;
            colors[0] = new Color(0.08f, 0.08f, 0.12f);

            for (int ring = 0; ring < CONCENTRIC_RINGS; ring++)
            {
                float t = (float)(ring + 1) / CONCENTRIC_RINGS;
                float r = t * INNER_RADIUS;
                // Alternating darker/lighter for concentric ring pattern
                float brightness = (ring % 2 == 0) ? 0.06f : 0.10f;
                Color ringColor = new Color(brightness, brightness, brightness + 0.04f);

                for (int s = 0; s < SEGMENTS; s++)
                {
                    float angle = (float)s / SEGMENTS * Mathf.PI * 2f;
                    int idx = 1 + ring * SEGMENTS + s;
                    vertices[idx] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                    colors[idx] = ringColor;
                }
            }

            // --- Outer ring vertices (two rings: inner edge at INNER_RADIUS, outer edge at RADIUS) ---
            int outerStart = innerVertCount;
            Color edgeColor = new Color(0.4f, 0.6f, 1f);
            for (int s = 0; s < SEGMENTS; s++)
            {
                float angle = (float)s / SEGMENTS * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Inner edge of the ring (matches last inner ring)
                vertices[outerStart + s * 2] = new Vector3(cos * INNER_RADIUS, 0f, sin * INNER_RADIUS);
                colors[outerStart + s * 2] = edgeColor * 0.5f;

                // Outer edge
                vertices[outerStart + s * 2 + 1] = new Vector3(cos * RADIUS, 0f, sin * RADIUS);
                colors[outerStart + s * 2 + 1] = edgeColor;
            }

            // --- Triangles for inner disc (submesh 0) ---
            // Center fan for ring 0
            int[] innerTris = new int[CONCENTRIC_RINGS * SEGMENTS * 6];
            int ti = 0;

            // Center to first ring
            for (int s = 0; s < SEGMENTS; s++)
            {
                int cur = 1 + s;
                int next = 1 + (s + 1) % SEGMENTS;
                innerTris[ti++] = 0;
                innerTris[ti++] = next;
                innerTris[ti++] = cur;
            }

            // Ring-to-ring quads
            for (int ring = 1; ring < CONCENTRIC_RINGS; ring++)
            {
                for (int s = 0; s < SEGMENTS; s++)
                {
                    int curInner = 1 + (ring - 1) * SEGMENTS + s;
                    int nextInner = 1 + (ring - 1) * SEGMENTS + (s + 1) % SEGMENTS;
                    int curOuter = 1 + ring * SEGMENTS + s;
                    int nextOuter = 1 + ring * SEGMENTS + (s + 1) % SEGMENTS;

                    innerTris[ti++] = curInner;
                    innerTris[ti++] = nextOuter;
                    innerTris[ti++] = curOuter;

                    innerTris[ti++] = curInner;
                    innerTris[ti++] = nextInner;
                    innerTris[ti++] = nextOuter;
                }
            }

            // --- Triangles for outer ring (submesh 1) ---
            int[] outerTris = new int[SEGMENTS * 6];
            for (int s = 0; s < SEGMENTS; s++)
            {
                int i0 = outerStart + s * 2;
                int i1 = outerStart + s * 2 + 1;
                int i2 = outerStart + ((s + 1) % SEGMENTS) * 2;
                int i3 = outerStart + ((s + 1) % SEGMENTS) * 2 + 1;

                outerTris[s * 6 + 0] = i0;
                outerTris[s * 6 + 1] = i3;
                outerTris[s * 6 + 2] = i1;

                outerTris[s * 6 + 3] = i0;
                outerTris[s * 6 + 4] = i2;
                outerTris[s * 6 + 5] = i3;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(innerTris, 0);
            mesh.SetTriangles(outerTris, 1);
            mesh.RecalculateBounds();

            // Materials
            _discMaterial = OrloShaders.CreateLit(new Color(0.08f, 0.08f, 0.12f), 0.7f, 0.5f);
            _edgeMaterial = OrloShaders.CreateEmissive(
                new Color(0.1f, 0.15f, 0.25f),
                new Color(0.4f, 0.6f, 1f),
                2f
            );

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.materials = new Material[] { _discMaterial, _edgeMaterial };
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        private void CreateMotes()
        {
            var motesGo = new GameObject("PlatformMotes");
            motesGo.transform.SetParent(transform, false);
            motesGo.layer = 10;

            var ps = motesGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 20;
            main.startLifetime = 4f;
            main.startSpeed = 0.15f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.5f, 0.7f, 1f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = RADIUS * 0.9f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.4f, 0.6f, 1f), 0f),
                    new GradientColorKey(new Color(0.5f, 0.8f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.5f, 0.9f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.6f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = gradient;

            var renderer = motesGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(OrloShaders.ParticlesUnlit);
            renderer.material.SetColor("_BaseColor", new Color(0.5f, 0.7f, 1f, 0.6f));

            // Generate a small soft-circle texture so particles aren't square
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x - 15.5f) / 15.5f;
                    float dy = (y - 15.5f) / 15.5f;
                    float d = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
            }
            tex.Apply();
            renderer.material.mainTexture = tex;
        }
    }
}

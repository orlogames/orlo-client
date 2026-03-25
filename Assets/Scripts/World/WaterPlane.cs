using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Simple animated water plane that follows the player.
    /// Uses vertex displacement for wave animation.
    /// </summary>
    public class WaterPlane : MonoBehaviour
    {
        private float waterLevel = -2f;
        private Mesh waterMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Vector3[] baseVertices;
        private Transform playerTransform;

        private float windDirection = 0f;
        private float windSpeed = 2f;

        private const int GridSize = 64;
        private const float PlaneSize = 200f;

        private void Start()
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

            GenerateWaterMesh();

            // Create a simple water material
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.1f, 0.3f, 0.5f, 0.7f);
            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Glossiness", 0.9f);
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            meshRenderer.material = mat;
        }

        public void SetPlayerTransform(Transform t) => playerTransform = t;
        public void SetWaterLevel(float level) => waterLevel = level;

        public void OnWindUpdate(float dir, float speed)
        {
            windDirection = dir;
            windSpeed = speed;
        }

        private void Update()
        {
            if (playerTransform != null)
            {
                // Follow player XZ, stay at water level
                var pos = playerTransform.position;
                transform.position = new Vector3(pos.x, waterLevel, pos.z);
            }

            AnimateWaves();
        }

        private void AnimateWaves()
        {
            if (waterMesh == null || baseVertices == null) return;

            var vertices = new Vector3[baseVertices.Length];
            float time = Time.time;

            float windX = Mathf.Cos(windDirection) * windSpeed * 0.1f;
            float windZ = Mathf.Sin(windDirection) * windSpeed * 0.1f;

            for (int i = 0; i < baseVertices.Length; i++)
            {
                var v = baseVertices[i];

                // Multi-octave wave displacement
                float wave1 = Mathf.Sin((v.x + time * 2f + windX * time) * 0.15f) * 0.4f;
                float wave2 = Mathf.Sin((v.z + time * 1.5f + windZ * time) * 0.2f) * 0.3f;
                float wave3 = Mathf.Sin((v.x * 0.3f + v.z * 0.5f + time * 3f) * 0.1f) * 0.15f;

                // Wind increases wave amplitude
                float amplitude = 1f + windSpeed * 0.1f;
                v.y = (wave1 + wave2 + wave3) * amplitude;

                vertices[i] = v;
            }

            waterMesh.vertices = vertices;
            waterMesh.RecalculateNormals();
        }

        private void GenerateWaterMesh()
        {
            waterMesh = new Mesh();
            waterMesh.name = "WaterMesh";

            int vertCount = (GridSize + 1) * (GridSize + 1);
            var vertices = new Vector3[vertCount];
            var uv = new Vector2[vertCount];
            var triangles = new int[GridSize * GridSize * 6];

            float cellSize = PlaneSize / GridSize;
            float halfSize = PlaneSize * 0.5f;

            int vi = 0;
            for (int z = 0; z <= GridSize; z++)
            {
                for (int x = 0; x <= GridSize; x++)
                {
                    vertices[vi] = new Vector3(
                        x * cellSize - halfSize,
                        0f,
                        z * cellSize - halfSize);
                    uv[vi] = new Vector2((float)x / GridSize, (float)z / GridSize);
                    vi++;
                }
            }

            int ti = 0;
            for (int z = 0; z < GridSize; z++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    int bl = z * (GridSize + 1) + x;
                    int br = bl + 1;
                    int tl = bl + (GridSize + 1);
                    int tr = tl + 1;

                    triangles[ti++] = bl;
                    triangles[ti++] = tl;
                    triangles[ti++] = tr;
                    triangles[ti++] = bl;
                    triangles[ti++] = tr;
                    triangles[ti++] = br;
                }
            }

            waterMesh.vertices = vertices;
            waterMesh.uv = uv;
            waterMesh.triangles = triangles;
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();

            baseVertices = (Vector3[])vertices.Clone();
            meshFilter.mesh = waterMesh;
        }
    }
}

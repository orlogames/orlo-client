using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orlo.World
{
    /// <summary>
    /// Static utility class for building Unity Mesh objects entirely from code.
    /// All meshes are built with vertices, triangles, normals, and UVs set correctly.
    /// </summary>
    public static class ProceduralMeshBuilder
    {
        /// <summary>
        /// Build a mesh for a terrain chunk from a flat heightmap array.
        /// resolution x resolution vertices spanning worldSize x worldSize units.
        /// </summary>
        public static Mesh BuildTerrainChunk(float[] heightmap, int resolution, float worldSize, float heightScale)
        {
            int vertCount = resolution * resolution;
            var vertices  = new Vector3[vertCount];
            var normals   = new Vector3[vertCount];
            var uvs       = new Vector2[vertCount];

            float step = worldSize / (resolution - 1);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int idx    = z * resolution + x;
                    float h    = heightmap[idx] * heightScale;
                    vertices[idx] = new Vector3(x * step, h, z * step);
                    uvs[idx]      = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));
                }
            }

            // Finite-difference normals
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float hL = x > 0            ? heightmap[z * resolution + (x - 1)] * heightScale : heightmap[z * resolution + x] * heightScale;
                    float hR = x < resolution-1 ? heightmap[z * resolution + (x + 1)] * heightScale : heightmap[z * resolution + x] * heightScale;
                    float hD = z > 0            ? heightmap[(z - 1) * resolution + x] * heightScale : heightmap[z * resolution + x] * heightScale;
                    float hU = z < resolution-1 ? heightmap[(z + 1) * resolution + x] * heightScale : heightmap[z * resolution + x] * heightScale;

                    normals[z * resolution + x] = new Vector3(hL - hR, 2f * step, hD - hU).normalized;
                }
            }

            int quadCount  = (resolution - 1) * (resolution - 1);
            var triangles  = new int[quadCount * 6];
            int tri        = 0;

            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int bl = z * resolution + x;
                    int br = bl + 1;
                    int tl = bl + resolution;
                    int tr = tl + 1;

                    triangles[tri++] = bl;
                    triangles[tri++] = tl;
                    triangles[tri++] = br;
                    triangles[tri++] = br;
                    triangles[tri++] = tl;
                    triangles[tri++] = tr;
                }
            }

            var mesh = new Mesh { name = "TerrainChunk" };
            if (vertCount > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices    = vertices;
            mesh.normals     = normals;
            mesh.uv          = uvs;
            mesh.triangles   = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Build a cylinder mesh (used for trunks and limb primitives).
        /// radiusTop == 0 produces a cone.
        /// </summary>
        public static Mesh BuildCylinder(float radiusTop, float radiusBottom, float height, int segments)
        {
            segments = Mathf.Max(3, segments);

            // Rings: bottom cap centre, bottom ring, top ring, top cap centre
            int vertCount = 2 + (segments + 1) * 2;
            var vertices  = new List<Vector3>(vertCount);
            var normals   = new List<Vector3>(vertCount);
            var uvs       = new List<Vector2>(vertCount);

            // Bottom cap centre
            vertices.Add(new Vector3(0, 0, 0));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Bottom ring
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                vertices.Add(new Vector3(c * radiusBottom, 0,       s * radiusBottom));
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(c * 0.5f + 0.5f, s * 0.5f + 0.5f));
            }

            // Top ring
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                vertices.Add(new Vector3(c * radiusTop, height, s * radiusTop));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(c * 0.5f + 0.5f, s * 0.5f + 0.5f));
            }

            // Top cap centre
            vertices.Add(new Vector3(0, height, 0));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Smooth normals for side faces — recalculate after assembly
            // Side vertices are separate so we can have hard caps and smooth sides;
            // for simplicity we share and recalculate below.

            var triangles = new List<int>();

            int bottomCentre = 0;
            int bottomRingStart = 1;
            int topRingStart = 1 + (segments + 1);
            int topCentre = vertices.Count - 1;

            // Bottom cap fan
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(bottomCentre);
                triangles.Add(bottomRingStart + i + 1);
                triangles.Add(bottomRingStart + i);
            }

            // Side quads
            for (int i = 0; i < segments; i++)
            {
                int bl = bottomRingStart + i;
                int br = bottomRingStart + i + 1;
                int tl = topRingStart + i;
                int tr = topRingStart + i + 1;

                triangles.Add(bl);
                triangles.Add(tl);
                triangles.Add(br);
                triangles.Add(br);
                triangles.Add(tl);
                triangles.Add(tr);
            }

            // Top cap fan
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(topCentre);
                triangles.Add(topRingStart + i);
                triangles.Add(topRingStart + i + 1);
            }

            var mesh = new Mesh { name = "Cylinder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Build a UV sphere mesh.
        /// </summary>
        public static Mesh BuildSphere(float radius, int latSegments, int lonSegments)
        {
            latSegments = Mathf.Max(2, latSegments);
            lonSegments = Mathf.Max(3, lonSegments);

            var vertices  = new List<Vector3>();
            var normals   = new List<Vector3>();
            var uvs       = new List<Vector2>();
            var triangles = new List<int>();

            // Generate rings from top to bottom
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float theta    = lat * Mathf.PI / latSegments;           // 0..PI
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float phi    = lon * Mathf.PI * 2f / lonSegments;    // 0..2PI
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    var normal = new Vector3(cosPhi * sinTheta, cosTheta, sinPhi * sinTheta);
                    vertices.Add(normal * radius);
                    normals.Add(normal);
                    uvs.Add(new Vector2(1f - (float)lon / lonSegments, 1f - (float)lat / latSegments));
                }
            }

            int stride = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int current = lat * stride + lon;
                    int next    = current + stride;

                    triangles.Add(current);
                    triangles.Add(next + 1);
                    triangles.Add(current + 1);

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            var mesh = new Mesh { name = "Sphere" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Build a flat quad for use as grass blade geometry.
        /// Pivot is at bottom centre.
        /// </summary>
        public static Mesh BuildQuad(float width, float height)
        {
            float hw = width * 0.5f;

            var vertices = new Vector3[]
            {
                new Vector3(-hw, 0,      0),
                new Vector3( hw, 0,      0),
                new Vector3(-hw, height, 0),
                new Vector3( hw, height, 0),
            };

            var normals = new Vector3[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            };

            var uvs = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1),
            };

            var triangles = new int[] { 0, 2, 1,  1, 2, 3 };

            var mesh = new Mesh { name = "Quad" };
            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Build a cone mesh. Pivot is at base centre.
        /// </summary>
        public static Mesh BuildCone(float radius, float height, int segments)
        {
            return BuildCylinder(0f, radius, height, segments);
        }

        /// <summary>
        /// Build an axis-aligned box with per-face UVs.
        /// Pivot is at the geometric centre.
        /// </summary>
        public static Mesh BuildBox(Vector3 size)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            // 6 faces × 4 vertices = 24
            var vertices = new Vector3[]
            {
                // +Z (front)
                new Vector3(-hx, -hy,  hz), new Vector3( hx, -hy,  hz),
                new Vector3( hx,  hy,  hz), new Vector3(-hx,  hy,  hz),
                // -Z (back)
                new Vector3( hx, -hy, -hz), new Vector3(-hx, -hy, -hz),
                new Vector3(-hx,  hy, -hz), new Vector3( hx,  hy, -hz),
                // -X (left)
                new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy,  hz),
                new Vector3(-hx,  hy,  hz), new Vector3(-hx,  hy, -hz),
                // +X (right)
                new Vector3( hx, -hy,  hz), new Vector3( hx, -hy, -hz),
                new Vector3( hx,  hy, -hz), new Vector3( hx,  hy,  hz),
                // +Y (top)
                new Vector3(-hx,  hy,  hz), new Vector3( hx,  hy,  hz),
                new Vector3( hx,  hy, -hz), new Vector3(-hx,  hy, -hz),
                // -Y (bottom)
                new Vector3(-hx, -hy, -hz), new Vector3( hx, -hy, -hz),
                new Vector3( hx, -hy,  hz), new Vector3(-hx, -hy,  hz),
            };

            var normals = new Vector3[]
            {
                Vector3.forward,  Vector3.forward,  Vector3.forward,  Vector3.forward,
                Vector3.back,     Vector3.back,     Vector3.back,     Vector3.back,
                Vector3.left,     Vector3.left,     Vector3.left,     Vector3.left,
                Vector3.right,    Vector3.right,    Vector3.right,    Vector3.right,
                Vector3.up,       Vector3.up,       Vector3.up,       Vector3.up,
                Vector3.down,     Vector3.down,     Vector3.down,     Vector3.down,
            };

            var uv = new Vector2[24];
            Vector2[] faceUV = { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            for (int f = 0; f < 6; f++)
                for (int v = 0; v < 4; v++)
                    uv[f * 4 + v] = faceUV[v];

            var triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int b = f * 6;
                int o = f * 4;
                triangles[b + 0] = o + 0; triangles[b + 1] = o + 2; triangles[b + 2] = o + 1;
                triangles[b + 3] = o + 0; triangles[b + 4] = o + 3; triangles[b + 5] = o + 2;
            }

            var mesh = new Mesh { name = "Box" };
            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Build a torus mesh.
        /// outerRadius: distance from centre to tube centre.
        /// innerRadius: tube radius.
        /// </summary>
        public static Mesh BuildTorus(float outerRadius, float innerRadius, int segments, int sides)
        {
            segments = Mathf.Max(3, segments);
            sides    = Mathf.Max(3, sides);

            var vertices  = new List<Vector3>();
            var normals   = new List<Vector3>();
            var uvs       = new List<Vector2>();
            var triangles = new List<int>();

            for (int seg = 0; seg <= segments; seg++)
            {
                float u     = (float)seg / segments;
                float phi   = u * Mathf.PI * 2f;
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                for (int side = 0; side <= sides; side++)
                {
                    float v      = (float)side / sides;
                    float theta  = v * Mathf.PI * 2f;
                    float cosTheta = Mathf.Cos(theta);
                    float sinTheta = Mathf.Sin(theta);

                    // Tube point
                    float x = (outerRadius + innerRadius * cosTheta) * cosPhi;
                    float y = innerRadius * sinTheta;
                    float z = (outerRadius + innerRadius * cosTheta) * sinPhi;

                    vertices.Add(new Vector3(x, y, z));

                    // Normal: direction from tube centre outward
                    var centre = new Vector3(outerRadius * cosPhi, 0, outerRadius * sinPhi);
                    normals.Add((new Vector3(x, y, z) - centre).normalized);
                    uvs.Add(new Vector2(u, v));
                }
            }

            int stride = sides + 1;
            for (int seg = 0; seg < segments; seg++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int current = seg * stride + side;
                    int next    = current + stride;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(next + 1);

                    triangles.Add(current);
                    triangles.Add(next + 1);
                    triangles.Add(current + 1);
                }
            }

            var mesh = new Mesh { name = "Torus" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Merge multiple meshes into one, transforming each by the supplied Matrix4x4.
        /// The resulting mesh has combined normals and UVs.
        /// </summary>
        public static Mesh MergeMeshes(IEnumerable<(Mesh mesh, Matrix4x4 transform)> meshes)
        {
            var allVertices  = new List<Vector3>();
            var allNormals   = new List<Vector3>();
            var allUVs       = new List<Vector2>();
            var allTriangles = new List<int>();
            int vertexOffset = 0;

            foreach (var (src, mat) in meshes)
            {
                if (src == null) continue;

                var srcVerts   = src.vertices;
                var srcNormals = src.normals;
                var srcUVs     = src.uv;
                var srcTris    = src.triangles;

                bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
                bool hasUVs     = srcUVs     != null && srcUVs.Length     == srcVerts.Length;

                for (int i = 0; i < srcVerts.Length; i++)
                {
                    allVertices.Add(mat.MultiplyPoint3x4(srcVerts[i]));
                    allNormals.Add(hasNormals
                        ? mat.MultiplyVector(srcNormals[i]).normalized
                        : Vector3.up);
                    allUVs.Add(hasUVs ? srcUVs[i] : Vector2.zero);
                }

                for (int i = 0; i < srcTris.Length; i++)
                    allTriangles.Add(srcTris[i] + vertexOffset);

                vertexOffset += srcVerts.Length;
            }

            var mesh = new Mesh { name = "MergedMesh" };
            if (allVertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(allVertices);
            mesh.SetNormals(allNormals);
            mesh.SetUVs(0, allUVs);
            mesh.SetTriangles(allTriangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Recompute smooth (averaged) normals for the mesh in-place.
        /// Vertices that share the same position have their normals averaged.
        /// </summary>
        public static void ComputeSmoothNormals(Mesh mesh)
        {
            if (mesh == null) return;

            var vertices  = mesh.vertices;
            var triangles = mesh.triangles;
            var normals   = new Vector3[vertices.Length];

            // Accumulate face normals at each vertex
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                var faceNormal = Vector3.Cross(
                    vertices[i1] - vertices[i0],
                    vertices[i2] - vertices[i0]);

                normals[i0] += faceNormal;
                normals[i1] += faceNormal;
                normals[i2] += faceNormal;
            }

            // Average normals for coincident vertices
            var positionToNormal = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!positionToNormal.ContainsKey(vertices[i]))
                    positionToNormal[vertices[i]] = Vector3.zero;
                positionToNormal[vertices[i]] += normals[i];
            }

            for (int i = 0; i < vertices.Length; i++)
                normals[i] = positionToNormal[vertices[i]].normalized;

            mesh.normals = normals;
        }
    }
}

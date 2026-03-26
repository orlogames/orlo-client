using System.Collections.Generic;
using UnityEngine;

namespace Orlo.VFX
{
    /// <summary>
    /// Samples evenly-distributed points on a mesh surface.
    /// Used to generate target positions for the microparticle assembly effect.
    /// Works with any Unity Mesh — procedural or imported.
    /// </summary>
    public static class MicroparticleMeshSampler
    {
        /// <summary>
        /// Sample N points distributed across the mesh surface proportional to triangle area.
        /// Returns world-space positions if a transform is provided, otherwise local-space.
        /// </summary>
        public static Vector3[] SampleSurface(Mesh mesh, int sampleCount, Transform transform = null)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var colors = mesh.colors;

            if (triangles.Length < 3)
            {
                Debug.LogWarning("[MicroparticleSampler] Mesh has no triangles");
                return new Vector3[0];
            }

            // Calculate area of each triangle for weighted sampling
            int triCount = triangles.Length / 3;
            var areas = new float[triCount];
            float totalArea = 0f;

            for (int i = 0; i < triCount; i++)
            {
                Vector3 v0 = vertices[triangles[i * 3]];
                Vector3 v1 = vertices[triangles[i * 3 + 1]];
                Vector3 v2 = vertices[triangles[i * 3 + 2]];

                float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                areas[i] = area;
                totalArea += area;
            }

            if (totalArea < 0.0001f)
            {
                Debug.LogWarning("[MicroparticleSampler] Mesh has zero surface area");
                return new Vector3[0];
            }

            // Build cumulative distribution for weighted random triangle selection
            var cdf = new float[triCount];
            cdf[0] = areas[0] / totalArea;
            for (int i = 1; i < triCount; i++)
            {
                cdf[i] = cdf[i - 1] + areas[i] / totalArea;
            }

            // Sample points
            var samples = new Vector3[sampleCount];

            for (int s = 0; s < sampleCount; s++)
            {
                // Select triangle weighted by area
                float r = Random.value;
                int triIdx = System.Array.BinarySearch(cdf, r);
                if (triIdx < 0) triIdx = ~triIdx;
                triIdx = Mathf.Clamp(triIdx, 0, triCount - 1);

                // Random point within triangle using barycentric coordinates
                Vector3 v0 = vertices[triangles[triIdx * 3]];
                Vector3 v1 = vertices[triangles[triIdx * 3 + 1]];
                Vector3 v2 = vertices[triangles[triIdx * 3 + 2]];

                float u = Random.value;
                float v = Random.value;
                if (u + v > 1f) { u = 1f - u; v = 1f - v; }

                Vector3 point = v0 + u * (v1 - v0) + v * (v2 - v0);

                // Transform to world space if transform provided
                if (transform != null)
                {
                    point = transform.TransformPoint(point);
                }

                samples[s] = point;
            }

            return samples;
        }

        /// <summary>
        /// Sample points from a SkinnedMeshRenderer's current baked pose.
        /// </summary>
        public static Vector3[] SampleSkinnedMesh(SkinnedMeshRenderer smr, int sampleCount)
        {
            var bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);
            var samples = SampleSurface(bakedMesh, sampleCount, smr.transform);
            Object.Destroy(bakedMesh);
            return samples;
        }

        /// <summary>
        /// Sample points from all MeshFilter and SkinnedMeshRenderer children of a GameObject.
        /// Distributes samples proportional to each mesh's surface area.
        /// </summary>
        public static Vector3[] SampleGameObject(GameObject go, int totalSampleCount)
        {
            var allSamples = new List<Vector3>();

            // Collect all meshes with their surface areas
            var meshes = new List<(Mesh mesh, Transform transform, float area)>();
            float totalArea = 0f;

            // MeshFilters (props, equipment, animal parts)
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                float area = CalculateSurfaceArea(mf.sharedMesh);
                meshes.Add((mf.sharedMesh, mf.transform, area));
                totalArea += area;
            }

            // SkinnedMeshRenderers (characters)
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked);
                float area = CalculateSurfaceArea(baked);
                meshes.Add((baked, smr.transform, area));
                totalArea += area;
            }

            if (totalArea < 0.0001f || meshes.Count == 0)
            {
                Debug.LogWarning($"[MicroparticleSampler] No meshes found on {go.name}");
                return new Vector3[0];
            }

            // Distribute samples proportional to surface area
            foreach (var (mesh, transform, area) in meshes)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(totalSampleCount * (area / totalArea)));
                var samples = SampleSurface(mesh, count, transform);
                allSamples.AddRange(samples);
            }

            return allSamples.ToArray();
        }

        /// <summary>
        /// Calculate total surface area of a mesh.
        /// </summary>
        public static float CalculateSurfaceArea(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            float area = 0f;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                area += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            }

            return area;
        }
    }
}

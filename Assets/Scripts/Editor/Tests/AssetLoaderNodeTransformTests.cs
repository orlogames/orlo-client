using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Orlo.World;
using UnityEngine;

namespace Orlo.Tests
{
    /// <summary>
    /// EditMode guard for the glTF node-transform bake in AssetLoader.ParseGlb.
    ///
    /// Regression target: ParseGlb historically read only the flat meshes[] array
    /// and discarded every node TRS. The Threshold NPC/creature stubs are authored
    /// Z-up and carry a +90 deg X node rotation that stands them up; dropping it laid
    /// every NPC flat on its back. This test builds a fixture with exactly that shape
    /// — a mesh tall in local Z under a [0.7071,0,0,0.7071] node rotation — runs it
    /// through the REAL ParseGlb, and asserts the resulting mesh stands upright
    /// (bounds.size.y dominates). Delete the bake in ParseGlb and this goes red.
    /// </summary>
    public class AssetLoaderNodeTransformTests
    {
        [Test]
        public void ParseGlb_BakesNodeRotation_StandsZUpMeshUpright()
        {
            byte[] glb = BuildTallZUpMeshWithXRotationNode();

            Bounds bounds = ParseAndCombineBounds(glb);

            // Local mesh is tall in Z (1.8) and thin in Y/X (0.3). The +90 deg X node
            // rotation maps that tall extent onto the world Y axis. If the node
            // transform is honoured, the mesh stands up (size.y ~= 1.8 >> size.z ~= 0.3).
            // If the transform is discarded (the bug), it lies flat (size.z >> size.y).
            Assert.Greater(bounds.size.y, bounds.size.z,
                "NPC mesh must stand upright — node rotation was discarded, it is lying flat.");
            Assert.Greater(bounds.size.y, 1.0f,
                "Upright height (~1.8m) collapsed — node transform not applied to vertices.");
        }

        [Test]
        public void ParseGlb_IdentityNode_IsNoOp_PreservesLocalExtents()
        {
            // Every live CDN master carries identity nodes; honouring them must be a
            // byte-for-byte no-op. Same tall-Z mesh, but an identity node: it must stay
            // tall in Z (size.z dominant), proving the bake never fires on identity.
            byte[] glb = BuildTallZUpMeshWithIdentityNode();

            Bounds bounds = ParseAndCombineBounds(glb);

            Assert.Greater(bounds.size.z, bounds.size.y,
                "Identity node must not rotate the mesh — the bake fired on an identity transform.");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static Bounds ParseAndCombineBounds(byte[] glb)
        {
            var go = new GameObject("assetloader_test");
            try
            {
                var loader = go.AddComponent<AssetLoader>();
                var parse = typeof(AssetLoader).GetMethod(
                    "ParseGlb", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(parse, "ParseGlb not found — signature changed?");

                var entries = (IList)parse.Invoke(loader, new object[] { glb });
                Assert.IsNotNull(entries);
                Assert.Greater(entries.Count, 0, "ParseGlb returned no meshes for the fixture.");

                Bounds combined = default;
                bool first = true;
                foreach (var entry in entries)
                {
                    var meshField = entry.GetType().GetField("mesh");
                    var mesh = (Mesh)meshField.GetValue(entry);
                    if (first) { combined = mesh.bounds; first = false; }
                    else combined.Encapsulate(mesh.bounds);
                }
                return combined;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static byte[] BuildTallZUpMeshWithXRotationNode()
        {
            // +90 deg rotation about X, quaternion [x,y,z,w].
            return BuildGlb("[0.70710678,0.0,0.0,0.70710678]");
        }

        private static byte[] BuildTallZUpMeshWithIdentityNode()
        {
            return BuildGlb("[0.0,0.0,0.0,1.0]");
        }

        /// <summary>
        /// Hand-assemble a minimal single-mesh GLB: a box 0.3×0.3×1.8 (tall in local Z)
        /// drawn by one node carrying the given rotation quaternion.
        /// </summary>
        private static byte[] BuildGlb(string rotationJson)
        {
            // Vertices: X,Y ∈ ±0.15 (thin), Z ∈ 0..1.8 (tall). Bounds derive from these.
            float[] verts =
            {
                -0.15f, -0.15f, 0.0f,
                 0.15f, -0.15f, 0.0f,
                 0.15f,  0.15f, 0.0f,
                -0.15f,  0.15f, 0.0f,
                -0.15f, -0.15f, 1.8f,
                 0.15f, -0.15f, 1.8f,
                 0.15f,  0.15f, 1.8f,
                -0.15f,  0.15f, 1.8f,
            };
            ushort[] indices =
            {
                0,1,2, 0,2,3,   // bottom
                4,6,5, 4,7,6,   // top
                0,4,5, 0,5,1,
                1,5,6, 1,6,2,
                2,6,7, 2,7,3,
                3,7,4, 3,4,0,
            };

            // Binary buffer: positions (96 bytes) then indices (72 bytes) = 168.
            using var binMs = new MemoryStream();
            using (var bw = new BinaryWriter(binMs))
            {
                foreach (float f in verts) bw.Write(f);       // 8 * 3 * 4 = 96
                foreach (ushort u in indices) bw.Write(u);    // 36 * 2 = 72
            }
            byte[] bin = binMs.ToArray();                     // 168, already 4-aligned

            string json =
                "{\"asset\":{\"version\":\"2.0\"}," +
                "\"scene\":0," +
                "\"scenes\":[{\"nodes\":[0]}]," +
                "\"nodes\":[{\"mesh\":0,\"rotation\":" + rotationJson + "}]," +
                "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
                "\"accessors\":[" +
                    "{\"bufferView\":0,\"componentType\":5126,\"count\":8,\"type\":\"VEC3\"}," +
                    "{\"bufferView\":1,\"componentType\":5123,\"count\":36,\"type\":\"SCALAR\"}]," +
                "\"bufferViews\":[" +
                    "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":96,\"target\":34962}," +
                    "{\"buffer\":0,\"byteOffset\":96,\"byteLength\":72,\"target\":34963}]," +
                "\"buffers\":[{\"byteLength\":168}]}";

            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            int jsonPad = (4 - (jsonBytes.Length % 4)) % 4;   // pad JSON chunk to 4 bytes with spaces

            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                int jsonChunkLen = jsonBytes.Length + jsonPad;
                w.Write(0x46546C67u);                         // magic 'glTF'
                w.Write(2u);                                  // version
                w.Write(0u);                                  // total length (patched below)

                w.Write((uint)jsonChunkLen);                  // chunk 0 length
                w.Write(0x4E4F534Au);                         // 'JSON'
                w.Write(jsonBytes);
                for (int i = 0; i < jsonPad; i++) w.Write((byte)0x20);

                w.Write((uint)bin.Length);                    // chunk 1 length
                w.Write(0x004E4942u);                         // 'BIN\0'
                w.Write(bin);
            }
            byte[] all = ms.ToArray();
            // Patch total length at offset 8.
            byte[] len = System.BitConverter.GetBytes((uint)all.Length);
            System.Array.Copy(len, 0, all, 8, 4);
            return all;
        }
    }
}

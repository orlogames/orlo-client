using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Orlo.World;

namespace Orlo.Tests
{
    /// <summary>
    /// EditMode guard for the glTF node-transform bug: <see cref="AssetLoader"/>.ParseGlb
    /// used to iterate the flat `meshes` array and ignore `nodes`/`scenes`, silently
    /// dropping every node TRS. That rendered Z-up authored assets (every NPC/creature)
    /// flat on their backs and node-scaled masters at the wrong size.
    ///
    /// The fixtures below are hand-built GLBs of a bar that is TALL ALONG glTF +Z. When a
    /// +90° X node rotation is honoured, that axis maps to Unity +Y and the bar stands up:
    /// bounds.size.y &gt; bounds.size.z. With the old (node-ignoring) parser the bar stays
    /// long in Z and the assertion fails — which is exactly the regression we are guarding.
    /// </summary>
    public class AssetLoaderNodeTransformTests
    {
        // Bar half-extents in glTF space: thin in X/Y, tall in Z.
        private const float HalfXY = 0.1f;
        private const float HalfZ = 1.0f;

        [Test]
        public void ParseGlb_HonoursNodeRotation_StandsZUpAssetUpright()
        {
            // +90° about X: quaternion (sin45, 0, 0, cos45).
            float s = Mathf.Sqrt(0.5f);
            byte[] glb = BuildBarGlb(new float[] { s, 0f, 0f, s });

            Bounds b = ParseAndCombineBounds(glb);

            Assert.Greater(b.size.y, b.size.z,
                "Z-up bar should stand upright once the node rotation is applied " +
                "(size.y > size.z). If this fails, ParseGlb is discarding node transforms again.");
            Assert.AreEqual(2f * HalfZ, b.size.y, 1e-3f, "Tall axis (~2m) must land on Y.");
            Assert.AreEqual(2f * HalfXY, b.size.z, 1e-3f, "Thin axis (~0.2m) must land on Z.");
        }

        [Test]
        public void ParseGlb_IdentityNode_IsUnchanged()
        {
            // No rotation → the bar keeps its authored orientation (tall in Z). This pins
            // the identity fast-path as a genuine no-op so the fix can't regress clean assets.
            byte[] glb = BuildBarGlb(null);

            Bounds b = ParseAndCombineBounds(glb);

            Assert.AreEqual(2f * HalfZ, b.size.z, 1e-3f, "Identity node must leave the tall axis on Z.");
            Assert.AreEqual(2f * HalfXY, b.size.y, 1e-3f, "Identity node must leave the thin axis on Y.");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static Bounds ParseAndCombineBounds(byte[] glb)
        {
            var go = new GameObject("AssetLoaderTestHost");
            try
            {
                // Awake is not invoked for a plain MonoBehaviour in EditMode, so the
                // singleton/pak-manifest side effects never run — ParseGlb is pure over
                // its byte[] input.
                var loader = go.AddComponent<AssetLoader>();

                var parse = typeof(AssetLoader).GetMethod(
                    "ParseGlb", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(parse, "ParseGlb(byte[]) not found via reflection.");

                var entries = (IEnumerable)parse.Invoke(loader, new object[] { glb });
                Assert.NotNull(entries, "ParseGlb returned null.");

                Bounds combined = default;
                bool init = false;
                FieldInfo meshField = null;
                foreach (var entry in entries)
                {
                    if (meshField == null)
                        meshField = entry.GetType().GetField("mesh", BindingFlags.Instance | BindingFlags.Public);
                    var mesh = (Mesh)meshField.GetValue(entry);
                    Assert.NotNull(mesh, "MeshEntry.mesh was null.");
                    if (!init) { combined = mesh.bounds; init = true; }
                    else combined.Encapsulate(mesh.bounds);
                }
                Assert.IsTrue(init, "ParseGlb produced no meshes for the fixture GLB.");
                return combined;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Build a minimal single-node GLB of an axis-aligned box (thin X/Y, tall Z).
        /// If <paramref name="rotation"/> is non-null it is set on node[0] as a glTF
        /// quaternion [x,y,z,w].
        /// </summary>
        private static byte[] BuildBarGlb(float[] rotation)
        {
            // 8 box corners: X,Y in [-HalfXY, HalfXY], Z in [-HalfZ, HalfZ].
            Vector3[] v =
            {
                new Vector3(-HalfXY, -HalfXY, -HalfZ), new Vector3( HalfXY, -HalfXY, -HalfZ),
                new Vector3( HalfXY,  HalfXY, -HalfZ), new Vector3(-HalfXY,  HalfXY, -HalfZ),
                new Vector3(-HalfXY, -HalfXY,  HalfZ), new Vector3( HalfXY, -HalfXY,  HalfZ),
                new Vector3( HalfXY,  HalfXY,  HalfZ), new Vector3(-HalfXY,  HalfXY,  HalfZ),
            };
            ushort[] idx =
            {
                0,1,2, 0,2,3, // -Z
                4,6,5, 4,7,6, // +Z
                0,4,5, 0,5,1, // -Y
                3,2,6, 3,6,7, // +Y
                1,5,6, 1,6,2, // +X
                0,3,7, 0,7,4, // -X
            };

            // BIN layout: positions (8*12=96 B) then indices (36*2=72 B) = 168 B (4-aligned).
            int posBytes = v.Length * 12;
            int idxBytes = idx.Length * 2;
            byte[] bin = new byte[posBytes + idxBytes];
            int o = 0;
            foreach (var p in v)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(p.x), 0, bin, o, 4); o += 4;
                Buffer.BlockCopy(BitConverter.GetBytes(p.y), 0, bin, o, 4); o += 4;
                Buffer.BlockCopy(BitConverter.GetBytes(p.z), 0, bin, o, 4); o += 4;
            }
            foreach (var i in idx)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(i), 0, bin, o, 2); o += 2;
            }

            string rot = rotation == null
                ? ""
                : $",\"rotation\":[{F(rotation[0])},{F(rotation[1])},{F(rotation[2])},{F(rotation[3])}]";

            string json =
                "{\"asset\":{\"version\":\"2.0\"}," +
                "\"scene\":0,\"scenes\":[{\"nodes\":[0]}]," +
                "\"nodes\":[{\"mesh\":0" + rot + "}]," +
                "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
                "\"buffers\":[{\"byteLength\":" + bin.Length + "}]," +
                "\"bufferViews\":[" +
                    "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + posBytes + ",\"target\":34962}," +
                    "{\"buffer\":0,\"byteOffset\":" + posBytes + ",\"byteLength\":" + idxBytes + ",\"target\":34963}]," +
                "\"accessors\":[" +
                    "{\"bufferView\":0,\"componentType\":5126,\"count\":8,\"type\":\"VEC3\"," +
                        "\"min\":[" + F(-HalfXY) + "," + F(-HalfXY) + "," + F(-HalfZ) + "]," +
                        "\"max\":[" + F(HalfXY) + "," + F(HalfXY) + "," + F(HalfZ) + "]}," +
                    "{\"bufferView\":1,\"componentType\":5123,\"count\":" + idx.Length + ",\"type\":\"SCALAR\"}]}";

            return AssembleGlb(json, bin);
        }

        private static string F(float f) => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static byte[] AssembleGlb(string json, byte[] bin)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            int jsonPad = (4 - (jsonBytes.Length % 4)) % 4;   // pad JSON chunk with spaces
            int binPad = (4 - (bin.Length % 4)) % 4;          // pad BIN chunk with zeros

            int jsonChunkLen = jsonBytes.Length + jsonPad;
            int binChunkLen = bin.Length + binPad;
            int total = 12 + 8 + jsonChunkLen + 8 + binChunkLen;

            byte[] glb = new byte[total];
            int p = 0;
            void U32(uint x) { Buffer.BlockCopy(BitConverter.GetBytes(x), 0, glb, p, 4); p += 4; }

            U32(0x46546C67);          // 'glTF'
            U32(2);                   // version
            U32((uint)total);         // total length

            U32((uint)jsonChunkLen);
            U32(0x4E4F534A);          // 'JSON'
            Buffer.BlockCopy(jsonBytes, 0, glb, p, jsonBytes.Length); p += jsonBytes.Length;
            for (int i = 0; i < jsonPad; i++) glb[p++] = 0x20; // space

            U32((uint)binChunkLen);
            U32(0x004E4942);          // 'BIN\0'
            Buffer.BlockCopy(bin, 0, glb, p, bin.Length); p += bin.Length;
            for (int i = 0; i < binPad; i++) glb[p++] = 0x00;

            return glb;
        }
    }
}

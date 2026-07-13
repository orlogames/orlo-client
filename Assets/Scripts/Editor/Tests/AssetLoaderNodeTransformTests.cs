using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Orlo.World;

namespace Orlo.Tests
{
    /// <summary>
    /// EditMode guard for the glTF node-transform bake in AssetLoader.ParseGlb.
    ///
    /// The loader used to iterate the flat `meshes` array and never read `nodes`,
    /// silently dropping every node transform. NPC masters authored Z-up carry a
    /// +90°X node rotation ([0.7071,0,0,0.7071]) to reach Y-up; with the transform
    /// dropped they rendered flat on their backs (bounds tall in Z, not Y).
    ///
    /// These tests run the REAL ParseGlb on hand-built GLBs:
    ///  - a z-dominant mesh under a +90°X node must come out upright (size.y > size.z);
    ///  - the same mesh under an identity node must be untouched (a no-op — the
    ///    property every live CDN master relies on, all being identity-node).
    /// </summary>
    public class AssetLoaderNodeTransformTests
    {
        // Local geometry is a thin triangle that is TALL along Z (a lying-down,
        // Z-up shape): bbox = (0.2, 0.3, 1.0), so raw size.z dominates size.y.
        private static readonly Vector3[] LyingDownZUp =
        {
            new Vector3(0f,   0f,   0f),
            new Vector3(0.2f, 0.3f, 0f),
            new Vector3(0f,   0f,   1.0f),
        };

        // +90° about X in glTF quaternion order [x,y,z,w] — the real NPC rotation.
        private const float S = 0.70710678f;
        private static readonly float[] RotXPlus90 = { S, 0f, 0f, S };

        [Test]
        public void NodeRotation_IsBakedIntoMesh_RendersUpright()
        {
            var bounds = ParseSingleMeshBounds(BuildGlb(LyingDownZUp, RotXPlus90));

            Assert.Greater(bounds.size.y, bounds.size.z,
                "the +90°X node rotation must be baked in so the mesh stands upright");
            Assert.Greater(bounds.size.y, 0.9f,
                "the ~1.0m Z extent should have rotated onto Y");
        }

        [Test]
        public void IdentityNode_IsNoOp_GeometryUntouched()
        {
            var bounds = ParseSingleMeshBounds(BuildGlb(LyingDownZUp, rotation: null));

            // Identity node → no bake. The lying-down shape stays lying down: this is
            // the provable no-op that keeps identity-node masters bit-for-bit as before.
            Assert.Greater(bounds.size.z, bounds.size.y,
                "an identity node must not transform the geometry");
            Assert.AreEqual(1.0f, bounds.size.z, 1e-4f);
            Assert.AreEqual(0.3f, bounds.size.y, 1e-4f);
        }

        // ── Harness: run the real private ParseGlb and return the single mesh's bounds ──

        private static Bounds ParseSingleMeshBounds(byte[] glb)
        {
            // Uninitialized instance skips Awake/OnEnable — ParseGlb and the accessor
            // readers only touch their arguments, never instance state.
            var loader = (AssetLoader)FormatterServices.GetUninitializedObject(typeof(AssetLoader));
            var parse = typeof(AssetLoader).GetMethod("ParseGlb",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(parse, "ParseGlb not found — did the signature change?");

            var entries = (IEnumerable)parse.Invoke(loader, new object[] { glb });
            Assert.IsNotNull(entries);

            var enumerator = entries.GetEnumerator();
            Assert.IsTrue(enumerator.MoveNext(), "ParseGlb returned no mesh entries");
            var entry = enumerator.Current;
            var mesh = (Mesh)entry.GetType()
                .GetField("mesh", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(entry);
            Assert.IsNotNull(mesh);
            return mesh.bounds;
        }

        // ── Minimal single-triangle GLB builder (POSITION + ushort indices) ──

        private static byte[] BuildGlb(Vector3[] verts, float[] rotation)
        {
            // BIN: positions (vec3 float) then indices (ushort), padded to 4 bytes.
            var bin = new List<byte>();
            foreach (var v in verts)
            {
                bin.AddRange(BitConverter.GetBytes(v.x));
                bin.AddRange(BitConverter.GetBytes(v.y));
                bin.AddRange(BitConverter.GetBytes(v.z));
            }
            int posLen = bin.Count;                 // 36
            for (ushort i = 0; i < verts.Length; i++)
                bin.AddRange(BitConverter.GetBytes(i));
            int idxLen = bin.Count - posLen;        // 6
            while (bin.Count % 4 != 0) bin.Add(0);  // pad BIN chunk

            string nodeJson = rotation != null
                ? "{\"mesh\":0,\"rotation\":[" + F(rotation[0]) + "," + F(rotation[1]) + "," +
                  F(rotation[2]) + "," + F(rotation[3]) + "]}"
                : "{\"mesh\":0}";

            string json =
                "{" +
                "\"asset\":{\"version\":\"2.0\"}," +
                "\"scene\":0," +
                "\"scenes\":[{\"nodes\":[0]}]," +
                "\"nodes\":[" + nodeJson + "]," +
                "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
                "\"buffers\":[{\"byteLength\":" + bin.Count + "}]," +
                "\"bufferViews\":[" +
                    "{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + posLen + "}," +
                    "{\"buffer\":0,\"byteOffset\":" + posLen + ",\"byteLength\":" + idxLen + "}" +
                "]," +
                "\"accessors\":[" +
                    "{\"bufferView\":0,\"componentType\":5126,\"count\":" + verts.Length + ",\"type\":\"VEC3\"}," +
                    "{\"bufferView\":1,\"componentType\":5123,\"count\":" + verts.Length + ",\"type\":\"SCALAR\"}" +
                "]}";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            int jsonPad = (4 - jsonBytes.Length % 4) % 4;

            var glb = new List<byte>();
            // Header: magic 'glTF', version 2, total length (filled after assembly).
            glb.AddRange(BitConverter.GetBytes(0x46546C67u));
            glb.AddRange(BitConverter.GetBytes(2u));
            glb.AddRange(BitConverter.GetBytes(0u)); // placeholder total length

            // Chunk 0: JSON (padded with spaces).
            glb.AddRange(BitConverter.GetBytes((uint)(jsonBytes.Length + jsonPad)));
            glb.AddRange(BitConverter.GetBytes(0x4E4F534Au)); // 'JSON'
            glb.AddRange(jsonBytes);
            for (int i = 0; i < jsonPad; i++) glb.Add(0x20);

            // Chunk 1: BIN.
            glb.AddRange(BitConverter.GetBytes((uint)bin.Count));
            glb.AddRange(BitConverter.GetBytes(0x004E4942u)); // 'BIN\0'
            glb.AddRange(bin);

            byte[] result = glb.ToArray();
            byte[] total = BitConverter.GetBytes((uint)result.Length);
            Array.Copy(total, 0, result, 8, 4);
            return result;
        }

        private static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}

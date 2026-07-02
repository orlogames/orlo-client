using System.IO;
using NUnit.Framework;
using Orlo.World;

namespace Orlo.Tests
{
    /// <summary>
    /// EditMode tests for the runtime loader's contract semantics
    /// (client_loader_contract.md v1.1 §3) — the three refusal paths the
    /// contract names, plus cache atomicity and the retry state machine.
    /// </summary>
    public class RuntimeManifestTests
    {
        private const string GOOD_SHA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static string ManifestJson(int schemaVersion = 1, string cdnBase = "https://cdn.orlo.games",
                                           string r2Key = "blobs/sha256/" + GOOD_SHA)
        {
            return "{\"schema_version\":" + schemaVersion + ",\"cdn_base\":\"" + cdnBase + "\"," +
                   "\"assets\":[{\"asset_id\":\"vegetation/pine_detailed_large\",\"r2_key\":\"" + r2Key +
                   "\",\"sha256\":\"" + GOOD_SHA + "\",\"size\":174182,\"content_type\":\"model/gltf-binary\"}]}";
        }

        // ── Refusal 1: schema_version != 1 refuses the whole manifest ──

        [Test]
        public void SchemaVersionMismatch_RefusesManifest()
        {
            Assert.Throws<ManifestRefusedException>(() => RuntimeManifest.Parse(ManifestJson(schemaVersion: 2)));
        }

        [Test]
        public void GarbageJson_RefusesManifest()
        {
            Assert.Throws<ManifestRefusedException>(() => RuntimeManifest.Parse("not json at all"));
        }

        [Test]
        public void NonHttpsCdnBase_RefusesManifest()
        {
            Assert.Throws<ManifestRefusedException>(
                () => RuntimeManifest.Parse(ManifestJson(cdnBase: "http://cdn.orlo.games")));
        }

        // ── Refusal 2: malformed r2_key refuses the ENTRY, not the manifest ──

        [TestCase("blobs/sha256/short")]                                      // not 64 hex
        [TestCase("blobs/sha256/../../../etc/passwd")]                        // traversal shape
        [TestCase("blobs/sha256/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // uppercase
        [TestCase("staging/creatures/thornback_guardian.glb")]                // wrong keyspace entirely
        public void MalformedR2Key_RefusesEntry_KeepsManifest(string badKey)
        {
            var manifest = RuntimeManifest.Parse(ManifestJson(r2Key: badKey));
            Assert.AreEqual(0, manifest.EntryCount, "bad entry must not be resolvable");
            Assert.AreEqual(1, manifest.RefusedEntries.Count, "refusal must be recorded");
            Assert.Throws<AssetNotInManifestException>(() => manifest.Resolve("vegetation/pine_detailed_large"));
        }

        // ── Refusal 3: assetId miss is a hard error, and URL composition is cdn_base + r2_key only ──

        [Test]
        public void AssetIdMiss_ThrowsHardError()
        {
            var manifest = RuntimeManifest.Parse(ManifestJson());
            Assert.Throws<AssetNotInManifestException>(() => manifest.Resolve("creature/does_not_exist"));
        }

        [Test]
        public void Resolve_ComposesUrlFromCdnBasePlusR2KeyOnly()
        {
            var manifest = RuntimeManifest.Parse(ManifestJson());
            var resolved = manifest.Resolve("vegetation/pine_detailed_large");
            Assert.AreEqual("https://cdn.orlo.games/blobs/sha256/" + GOOD_SHA, resolved.Url);
            Assert.AreEqual(GOOD_SHA, resolved.Sha256);
            Assert.AreEqual(174182, resolved.Size);
        }

        // ── Cache: verified-bytes-only commit, atomic, immutable ──

        [Test]
        public void Cache_CommitsOnlyVerifiedBytes()
        {
            string root = Path.Combine(Path.GetTempPath(), "orlo_test_cache_" + Path.GetRandomFileName());
            try
            {
                var cache = new RuntimeBlobCache(root);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("glb-bytes");
                string realSha = RuntimeBlobCache.Sha256Hex(bytes);

                // Wrong hash: nothing lands on disk, not even a .part.
                Assert.IsNull(cache.VerifyAndCommit(bytes, GOOD_SHA));
                Assert.IsFalse(cache.Contains(GOOD_SHA));
                Assert.IsFalse(File.Exists(cache.PathFor(GOOD_SHA) + ".part"));

                // Right hash: committed at the content-addressed path, no .part remains.
                string path = cache.VerifyAndCommit(bytes, realSha);
                Assert.IsNotNull(path);
                Assert.IsTrue(cache.Contains(realSha));
                Assert.IsFalse(File.Exists(path + ".part"));
                Assert.AreEqual("glb-bytes", File.ReadAllText(path));

                // Idempotent re-commit of the same hex.
                Assert.AreEqual(path, cache.VerifyAndCommit(bytes, realSha));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        // ── Retry policy: 3 network tries; sha mismatch retries ONCE then fails hard ──

        [Test]
        public void RetryPolicy_NetworkFailsHardOnThirdFailure()
        {
            var policy = new FetchRetryPolicy();
            Assert.AreEqual(FetchRetryPolicy.Verdict.Retry, policy.OnNetworkFailure());
            Assert.AreEqual(FetchRetryPolicy.Verdict.Retry, policy.OnNetworkFailure());
            Assert.AreEqual(FetchRetryPolicy.Verdict.Fail, policy.OnNetworkFailure());
        }

        [Test]
        public void RetryPolicy_HashMismatchRetriesOnceThenFails()
        {
            var policy = new FetchRetryPolicy();
            Assert.AreEqual(FetchRetryPolicy.Verdict.RetryBypassCache, policy.OnHashMismatch());
            Assert.AreEqual(FetchRetryPolicy.Verdict.Fail, policy.OnHashMismatch());
        }

        [Test]
        public void RetryPolicy_BackoffIsExponential250msTo2s()
        {
            var policy = new FetchRetryPolicy();
            Assert.AreEqual(0.25f, policy.BackoffFor(1));
            Assert.AreEqual(1.0f, policy.BackoffFor(2));
            Assert.AreEqual(2.0f, policy.BackoffFor(3));
        }
    }
}

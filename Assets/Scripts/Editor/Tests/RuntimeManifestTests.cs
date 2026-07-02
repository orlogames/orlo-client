using System.IO;
using NUnit.Framework;
using Orlo.World;

namespace Orlo.Tests
{
    /// <summary>
    /// EditMode tests for the runtime loader's contract semantics
    /// (client_loader_contract.md v1.1 §3) — the three refusal paths the
    /// contract names, the sha256↔r2_key binding guard, cache atomicity,
    /// and the retry state machine.
    /// </summary>
    public class RuntimeManifestTests
    {
        private const string GOOD_SHA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static string ManifestJson(int schemaVersion = 1, string cdnBase = "https://cdn.orlo.games",
                                           string r2Key = "blobs/sha256/" + GOOD_SHA, string sha256 = GOOD_SHA)
        {
            return "{\"schema_version\":" + schemaVersion + ",\"cdn_base\":\"" + cdnBase + "\"," +
                   "\"assets\":[{\"asset_id\":\"vegetation/pine_detailed_large\",\"r2_key\":\"" + r2Key +
                   "\",\"sha256\":\"" + sha256 + "\",\"size\":174182,\"content_type\":\"model/gltf-binary\"}]}";
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

        // ── sha256 ↔ r2_key binding: content-addressed keys must agree, and a
        //    crafted sha256 must never become a cache path (review MAJOR-1) ──

        [TestCase("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")] // valid hex, wrong digest
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // uppercase
        [TestCase("../../../evil+aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]  // traversal-shaped
        public void Sha256NotMatchingR2Key_RefusesEntry(string badSha)
        {
            var manifest = RuntimeManifest.Parse(ManifestJson(sha256: badSha));
            Assert.AreEqual(0, manifest.EntryCount);
            Assert.AreEqual(1, manifest.RefusedEntries.Count);
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

        // ── Cache: verified-bytes-only commit, atomic, immutable, size-guarded hits ──

        [Test]
        public void Cache_CommitsOnlyVerifiedBytes()
        {
            string root = Path.Combine(Path.GetTempPath(), "orlo_test_cache_" + Path.GetRandomFileName());
            try
            {
                var cache = new RuntimeBlobCache(root);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("glb-bytes");
                string realSha = RuntimeBlobCache.Sha256Hex(bytes);

                // Wrong hash: nothing lands at the content-addressed path.
                Assert.IsNull(cache.VerifyAndCommit(bytes, GOOD_SHA));
                Assert.IsFalse(cache.Contains(GOOD_SHA));

                // Right hash: committed, no .part siblings remain.
                string path = cache.VerifyAndCommit(bytes, realSha);
                Assert.IsNotNull(path);
                Assert.IsTrue(cache.Contains(realSha));
                Assert.AreEqual(0, Directory.GetFiles(root, "*.part").Length);
                Assert.AreEqual("glb-bytes", File.ReadAllText(path));

                // Idempotent re-commit of the same hex.
                Assert.AreEqual(path, cache.VerifyAndCommit(bytes, realSha));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Cache_FileVariant_VerifiesAndCleansUpMismatch()
        {
            string root = Path.Combine(Path.GetTempPath(), "orlo_test_cache_" + Path.GetRandomFileName());
            try
            {
                var cache = new RuntimeBlobCache(root);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("streamed-glb-bytes");
                string realSha = RuntimeBlobCache.Sha256Hex(bytes);

                // Mismatch: .part is deleted, nothing committed.
                string badPart = cache.NewPartPath(GOOD_SHA);
                File.WriteAllBytes(badPart, bytes);
                Assert.IsNull(cache.VerifyAndCommitFile(badPart, GOOD_SHA));
                Assert.IsFalse(File.Exists(badPart));
                Assert.IsFalse(cache.Contains(GOOD_SHA));

                // Match: committed atomically at the content-addressed path.
                string goodPart = cache.NewPartPath(realSha);
                File.WriteAllBytes(goodPart, bytes);
                string path = cache.VerifyAndCommitFile(goodPart, realSha);
                Assert.IsNotNull(path);
                Assert.IsFalse(File.Exists(goodPart));
                Assert.IsTrue(cache.Contains(realSha, bytes.Length));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Cache_StaleOccupantAtFinalPath_IsReplacedNotReturned()
        {
            // Randy review MAJOR-2: a wrong-content file at the content-addressed
            // path (survived eviction) must never short-circuit a commit as success.
            string root = Path.Combine(Path.GetTempPath(), "orlo_test_cache_" + Path.GetRandomFileName());
            try
            {
                var cache = new RuntimeBlobCache(root);
                byte[] good = System.Text.Encoding.UTF8.GetBytes("verified-bytes");
                string sha = RuntimeBlobCache.Sha256Hex(good);

                // Plant a stale occupant directly at the final path.
                Directory.CreateDirectory(root);
                File.WriteAllText(cache.PathFor(sha), "stale-wrong-bytes");

                string part = cache.NewPartPath(sha);
                File.WriteAllBytes(part, good);
                string result = cache.VerifyAndCommitFile(part, sha);

                Assert.IsNotNull(result);
                Assert.AreEqual("verified-bytes", File.ReadAllText(result), "stale occupant must be replaced by verified bytes");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void Cache_WrongSizeHit_EvictsForRefetch()
        {
            string root = Path.Combine(Path.GetTempPath(), "orlo_test_cache_" + Path.GetRandomFileName());
            try
            {
                var cache = new RuntimeBlobCache(root);
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("glb-bytes");
                string realSha = RuntimeBlobCache.Sha256Hex(bytes);
                cache.VerifyAndCommit(bytes, realSha);

                Assert.IsTrue(cache.Contains(realSha, bytes.Length), "correct size = hit");
                Assert.IsFalse(cache.Contains(realSha, bytes.Length + 999), "wrong size = evict + miss");
                Assert.IsFalse(File.Exists(cache.PathFor(realSha)), "truncated/oversized entry must be gone");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        // ── Retry policy: 3 retries after the initial attempt (all backoffs
        //    reachable); sha mismatch retries ONCE then fails hard ──

        [Test]
        public void RetryPolicy_ThreeRetriesThenFail()
        {
            var policy = new FetchRetryPolicy();
            Assert.AreEqual(FetchRetryPolicy.Verdict.Retry, policy.OnNetworkFailure()); // → backoff 0.25s
            Assert.AreEqual(FetchRetryPolicy.Verdict.Retry, policy.OnNetworkFailure()); // → backoff 1.0s
            Assert.AreEqual(FetchRetryPolicy.Verdict.Retry, policy.OnNetworkFailure()); // → backoff 2.0s
            Assert.AreEqual(FetchRetryPolicy.Verdict.Fail, policy.OnNetworkFailure());  // 4th failure = done
        }

        [Test]
        public void RetryPolicy_HashMismatchRetriesOnceThenFails()
        {
            var policy = new FetchRetryPolicy();
            Assert.AreEqual(FetchRetryPolicy.Verdict.RetryBypassCache, policy.OnHashMismatch());
            Assert.AreEqual(FetchRetryPolicy.Verdict.Fail, policy.OnHashMismatch());
        }

        [Test]
        public void RetryPolicy_BackoffIsExponential250msTo2s_AllReachable()
        {
            var policy = new FetchRetryPolicy();
            Assert.AreEqual(0.25f, policy.BackoffFor(1));
            Assert.AreEqual(1.0f, policy.BackoffFor(2));
            Assert.AreEqual(2.0f, policy.BackoffFor(3)); // reachable: 3rd retry exists now
        }
    }
}

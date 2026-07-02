using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Content-addressed on-disk blob cache, per client_loader_contract.md v1.1 §4:
    /// {cache_root}/blobs/sha256/{hex} mirrors the R2 keyspace, so the file path
    /// IS the cache entry. Entries are immutable (same hex → same bytes forever);
    /// no LRU in v1 — launcher "Repair" clears the directory wholesale.
    ///
    /// Writes are atomic: bytes stream to a .part sibling, are sha256-verified,
    /// then renamed into place. A crash mid-write never leaves a corrupt entry
    /// at a valid cache path.
    /// </summary>
    public sealed class RuntimeBlobCache
    {
        private readonly string _root;

        public RuntimeBlobCache(string cacheRoot = null)
        {
            _root = cacheRoot ?? Path.Combine(Application.persistentDataPath, "blobs", "sha256");
        }

        public string PathFor(string sha256Hex) => Path.Combine(_root, sha256Hex);

        /// <summary>Cache hit = the file exists. Contents are trusted because the
        /// only writer verifies before rename (and the keyspace is immutable).</summary>
        public bool Contains(string sha256Hex) => File.Exists(PathFor(sha256Hex));

        /// <summary>
        /// Verify bytes against the expected sha256 and, on match, atomically
        /// commit them to the cache. Returns the final cache path on success,
        /// null on hash mismatch (caller decides retry semantics — contract §3).
        /// </summary>
        public string VerifyAndCommit(byte[] bytes, string expectedSha256)
        {
            string actual = Sha256Hex(bytes);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[RuntimeBlobCache] sha256 mismatch: expected {expectedSha256}, got {actual} — discarding");
                return null;
            }

            string finalPath = PathFor(expectedSha256);
            if (File.Exists(finalPath))
                return finalPath; // already committed by an earlier fetch — immutable, done

            Directory.CreateDirectory(_root);
            string partPath = finalPath + ".part";
            try
            {
                File.WriteAllBytes(partPath, bytes);
                // Move is atomic on the same volume; the verified bytes appear at
                // the content-addressed path all-or-nothing.
                File.Move(partPath, finalPath);
            }
            catch (IOException)
            {
                // Lost a race with a concurrent committer of the same hex — fine,
                // contents are identical by construction. Clean our .part and move on.
                if (File.Exists(finalPath))
                {
                    TryDelete(partPath);
                    return finalPath;
                }
                throw;
            }
            return finalPath;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Pure retry-policy state machine for one asset fetch, per contract §3:
    ///   network failure  → up to 3 tries, exponential backoff 250ms → 2s
    ///   sha256 mismatch  → discard, refetch ONCE bypassing local cache, then fail hard
    /// Kept free of Unity/network types so EditMode tests can walk every path.
    /// </summary>
    public sealed class FetchRetryPolicy
    {
        public const int MAX_NETWORK_TRIES = 3;
        private static readonly float[] BackoffSeconds = { 0.25f, 1.0f, 2.0f };

        private int _networkFailures;
        private bool _hashRetryUsed;

        public enum Verdict { Retry, RetryBypassCache, Fail }

        /// <summary>Network-level failure (5xx / timeout / DNS).</summary>
        public Verdict OnNetworkFailure()
        {
            _networkFailures++;
            return _networkFailures < MAX_NETWORK_TRIES ? Verdict.Retry : Verdict.Fail;
        }

        /// <summary>Fetched bytes failed sha256 verification.</summary>
        public Verdict OnHashMismatch()
        {
            if (_hashRetryUsed)
                return Verdict.Fail;
            _hashRetryUsed = true;
            return Verdict.RetryBypassCache;
        }

        /// <summary>Backoff before the given (1-based) network retry.</summary>
        public float BackoffFor(int failureCount) =>
            BackoffSeconds[Mathf.Clamp(failureCount - 1, 0, BackoffSeconds.Length - 1)];

        public int NetworkFailures => _networkFailures;
        public bool HashRetryUsed => _hashRetryUsed;
    }
}

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
    /// Writes are atomic: bytes land in a writer-unique temp sibling, are
    /// sha256-verified, then renamed into place. A crash mid-write never leaves
    /// a corrupt entry at a valid cache path, and concurrent same-hex writers
    /// never share a temp file (review finding: Codex MAJOR-2, 2026-07-02).
    /// </summary>
    public sealed class RuntimeBlobCache
    {
        private readonly string _root;

        public RuntimeBlobCache(string cacheRoot = null)
        {
            _root = cacheRoot ?? Path.Combine(Application.persistentDataPath, "blobs", "sha256");
        }

        public string CacheRoot => _root;

        public string PathFor(string sha256Hex) => Path.Combine(_root, sha256Hex);

        /// <summary>
        /// Cache hit = file exists AND has the expected size. The size guard is a
        /// cheap truncation check (torn manual copy, disk trouble) without paying
        /// a full re-hash per hit; the only writer verifies before rename, so
        /// contents at the right length are trusted (Codex MINOR, 2026-07-02).
        /// A wrong-size file is deleted so the caller falls through to refetch.
        /// </summary>
        public bool Contains(string sha256Hex, long expectedSize = -1)
        {
            string path = PathFor(sha256Hex);
            if (!File.Exists(path))
                return false;
            if (expectedSize >= 0 && new FileInfo(path).Length != expectedSize)
            {
                Debug.LogWarning($"[RuntimeBlobCache] cached {sha256Hex} has wrong size — evicting for refetch");
                TryDelete(path);
                return false;
            }
            return true;
        }

        /// <summary>Writer-unique temp path for streaming a download before commit.</summary>
        public string NewPartPath(string sha256Hex)
        {
            Directory.CreateDirectory(_root);
            return PathFor(sha256Hex) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".part";
        }

        /// <summary>
        /// Verify a fully-written temp file against the expected sha256 and, on
        /// match, atomically rename it into the cache. Returns the final cache
        /// path on success, null on hash mismatch (temp file deleted either way
        /// on failure; caller decides retry semantics — contract §3).
        /// </summary>
        public string VerifyAndCommitFile(string partPath, string expectedSha256)
        {
            string actual = Sha256HexOfFile(partPath);
            if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[RuntimeBlobCache] sha256 mismatch: expected {expectedSha256}, got {actual} — discarding");
                TryDelete(partPath);
                return null;
            }
            return CommitVerified(partPath, expectedSha256);
        }

        /// <summary>
        /// Byte-array variant (small payloads / tests). Same semantics as
        /// VerifyAndCommitFile.
        /// </summary>
        public string VerifyAndCommit(byte[] bytes, string expectedSha256)
        {
            string actual = Sha256Hex(bytes);
            if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[RuntimeBlobCache] sha256 mismatch: expected {expectedSha256}, got {actual} — discarding");
                return null;
            }
            string finalPath = PathFor(expectedSha256);
            if (File.Exists(finalPath))
                return finalPath;
            string partPath = NewPartPath(expectedSha256);
            File.WriteAllBytes(partPath, bytes);
            return CommitVerified(partPath, expectedSha256);
        }

        /// <summary>Rename a verified temp file to its content-addressed path.
        /// Race-safe: losing to a concurrent committer of the same hex is success.
        /// An EXISTING file at finalPath is only trusted after a hash re-check —
        /// a locked stale file that survived Contains()' eviction must never be
        /// returned as verified bytes (Randy review, PR #17 MAJOR-2).</summary>
        private string CommitVerified(string partPath, string sha256Hex)
        {
            string finalPath = PathFor(sha256Hex);
            if (File.Exists(finalPath))
            {
                if (Sha256HexOfFile(finalPath) == sha256Hex)
                {
                    TryDelete(partPath);
                    return finalPath; // genuine earlier/concurrent commit — identical bytes
                }
                // Stale or corrupt occupant (e.g. eviction failed on a locked file):
                // replace it with the verified .part. If it's still locked, throw —
                // a failed fetch beats unverified bytes.
                File.Delete(finalPath);
            }
            try
            {
                File.Move(partPath, finalPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                TryDelete(partPath);
                if (File.Exists(finalPath) && Sha256HexOfFile(finalPath) == sha256Hex)
                    return finalPath; // lost the race to a verified committer — fine
                throw;
            }
            return finalPath;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes));
        }

        public static string Sha256HexOfFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return ToHex(sha.ComputeHash(stream));
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort cleanup */ }
            catch (UnauthorizedAccessException) { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Pure retry-policy state machine for one asset fetch, per contract §3:
    ///   network failure  → 3 retries after the initial attempt (4 tries total),
    ///                      exponential backoff 250ms → 1s → 2s
    ///   sha256 mismatch  → discard, refetch ONCE bypassing caches, then fail hard
    /// Kept free of Unity/network types so EditMode tests can walk every path.
    /// (Backoff table previously had an unreachable 2s entry — review MINOR-1.)
    /// </summary>
    public sealed class FetchRetryPolicy
    {
        public const int MAX_NETWORK_RETRIES = 3; // retries after the initial attempt
        private static readonly float[] BackoffSeconds = { 0.25f, 1.0f, 2.0f };

        private int _networkFailures;
        private bool _hashRetryUsed;

        public enum Verdict { Retry, RetryBypassCache, Fail }

        /// <summary>Network-level failure (5xx / timeout / DNS).</summary>
        public Verdict OnNetworkFailure()
        {
            _networkFailures++;
            return _networkFailures <= MAX_NETWORK_RETRIES ? Verdict.Retry : Verdict.Fail;
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

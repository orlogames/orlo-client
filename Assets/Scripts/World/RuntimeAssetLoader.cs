using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Orlo.World
{
    /// <summary>
    /// Thin CDN loader implementing docs/design/client_loader_contract.md v1.1
    /// (orlo-assets): runtime_manifest is the ONLY resolver; downloads stream
    /// straight to disk (.part), are sha256-verified, and commit into the
    /// content-addressed RuntimeBlobCache.
    ///
    /// Contract MUST-NOTs enforced here:
    ///   - no URL composition beyond cdn_base + r2_key   (RuntimeManifest.Resolve)
    ///   - no fallback to assets/models/ or staging/ on a manifest miss
    ///   - no unverified bytes handed to callers
    ///   - no redirects off cdn.orlo.games (redirectLimit = 0)
    ///   - no negative caching (a miss today may resolve after a manifest bump)
    ///
    /// Concurrent fetches of the same blob are deduped in-flight: callbacks
    /// pile onto the first download instead of re-downloading (review MAJOR-3).
    ///
    /// Platform note: manifest load uses direct file IO on streamingAssetsPath,
    /// which is correct for the desktop builds this client ships (release.yml is
    /// Windows-only). Android/WebGL would need a UnityWebRequest load here.
    /// </summary>
    public class RuntimeAssetLoader : MonoBehaviour
    {
        public static RuntimeAssetLoader Instance { get; private set; }

        private const string MANIFEST_STREAMING_PATH = "runtime_manifest.json";

        private RuntimeManifest _manifest;
        private RuntimeBlobCache _cache;

        /// <summary>sha256 → callbacks waiting on an in-flight download of that blob.</summary>
        private readonly Dictionary<string, List<Action<string>>> _inFlight = new();

        public bool ManifestLoaded => _manifest != null;
        public int ManifestEntryCount => _manifest?.EntryCount ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cache = new RuntimeBlobCache();
            LoadManifestFromStreamingAssets();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Load the build-pinned manifest from StreamingAssets. A refused
        /// manifest (bad schema/JSON) leaves the loader inert — every Fetch
        /// call then fails loudly rather than guessing at URLs.
        /// </summary>
        private void LoadManifestFromStreamingAssets()
        {
            string path = Path.Combine(Application.streamingAssetsPath, MANIFEST_STREAMING_PATH);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[RuntimeAssetLoader] no {MANIFEST_STREAMING_PATH} in StreamingAssets — loader inert until one ships in the build");
                return;
            }
            try
            {
                _manifest = RuntimeManifest.Parse(File.ReadAllText(path));
                Debug.Log($"[RuntimeAssetLoader] manifest loaded: {_manifest.EntryCount} assets, cdn={_manifest.CdnBase}, {_manifest.RefusedEntries.Count} refused entries");
            }
            catch (ManifestRefusedException e)
            {
                Debug.LogError($"[RuntimeAssetLoader] manifest REFUSED: {e.Message}");
                _manifest = null;
            }
        }

        /// <summary>Testing/tooling hook: inject a manifest + cache directly.</summary>
        public void InitializeForTest(RuntimeManifest manifest, RuntimeBlobCache cache)
        {
            _manifest = manifest;
            _cache = cache;
        }

        /// <summary>
        /// Fetch an asset's verified bytes-on-disk path. onComplete receives the
        /// content-addressed cache path, or null on hard failure — it is ALWAYS
        /// invoked exactly once (contract §3: every failure surfaces). Misses
        /// throw AssetNotInManifestException synchronously — no URL probing.
        /// </summary>
        public void Fetch(string assetId, Action<string> onComplete)
        {
            if (_manifest == null)
                throw new ManifestRefusedException("no valid runtime manifest loaded — cannot resolve assets");

            var resolved = _manifest.Resolve(assetId); // throws on miss (contract §3)

            if (_cache.Contains(resolved.Sha256, resolved.Size))
            {
                onComplete?.Invoke(_cache.PathFor(resolved.Sha256));
                return;
            }

            // In-flight dedup: same blob already downloading → queue the callback.
            if (_inFlight.TryGetValue(resolved.Sha256, out var waiters))
            {
                waiters.Add(onComplete);
                return;
            }
            _inFlight[resolved.Sha256] = new List<Action<string>> { onComplete };
            StartCoroutine(FetchCoroutine(resolved));
        }

        private void CompleteAll(string sha256, string resultPath)
        {
            if (!_inFlight.TryGetValue(sha256, out var waiters))
                return;
            _inFlight.Remove(sha256);
            foreach (var cb in waiters)
            {
                try { cb?.Invoke(resultPath); }
                catch (Exception e) { Debug.LogException(e); } // one bad callback must not starve the rest
            }
        }

        private IEnumerator FetchCoroutine(RuntimeManifest.ResolvedAsset resolved)
        {
            var policy = new FetchRetryPolicy();
            bool bypassEdgeCache = false;

            while (true)
            {
                // Stream straight to a writer-unique .part — no full in-memory
                // buffering of multi-MB GLBs (contract §2 "stream to disk cache").
                string partPath;
                try
                {
                    partPath = _cache.NewPartPath(resolved.Sha256);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RuntimeAssetLoader] cache unavailable for {resolved.AssetId}: {e.Message}");
                    CompleteAll(resolved.Sha256, null);
                    yield break;
                }

                using (var request = new UnityWebRequest(resolved.Url, UnityWebRequest.kHttpVerbGET))
                {
                    request.downloadHandler = new DownloadHandlerFile(partPath) { removeFileOnAbort = true };
                    // Contract MUST-NOT: never follow redirects off cdn.orlo.games.
                    // Simplest safe form: no redirects at all — the CDN serves blobs directly.
                    request.redirectLimit = 0;
                    if (bypassEdgeCache)
                    {
                        // Hash-mismatch retry must not be served the same corrupt
                        // object from the CDN edge (review MINOR-3). Same URL —
                        // MUST-NOT intact — just a no-cache request.
                        request.SetRequestHeader("Cache-Control", "no-cache");
                        request.SetRequestHeader("Pragma", "no-cache");
                    }
                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        // removeFileOnAbort only covers aborts — a ProtocolError
                        // (404/5xx) leaves the .part behind, and retries mint new
                        // GUID'd ones. Reap it here (Randy review, PR #17 MINOR-3).
                        try { if (File.Exists(partPath)) File.Delete(partPath); }
                        catch (IOException) { /* best-effort */ }

                        var verdict = policy.OnNetworkFailure();
                        Debug.LogWarning($"[RuntimeAssetLoader] fetch failed ({request.error}, HTTP {request.responseCode}) for {resolved.AssetId} — {verdict}");
                        if (verdict == FetchRetryPolicy.Verdict.Fail)
                        {
                            CompleteAll(resolved.Sha256, null);
                            yield break;
                        }
                        // Realtime wait: a paused game (timeScale=0) on a loading
                        // screen must not freeze retry backoff (review MINOR-2).
                        yield return new WaitForSecondsRealtime(policy.BackoffFor(policy.NetworkFailures));
                        continue;
                    }
                }

                // Verify + commit outside the request scope; the handler has
                // flushed and closed the .part file. Any IO exception surfaces
                // as a failed fetch, never a stranded callback (review MAJOR-2).
                string cachePath;
                try
                {
                    cachePath = _cache.VerifyAndCommitFile(partPath, resolved.Sha256);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RuntimeAssetLoader] cache commit failed for {resolved.AssetId}: {e.Message}");
                    CompleteAll(resolved.Sha256, null);
                    yield break;
                }

                if (cachePath != null)
                {
                    CompleteAll(resolved.Sha256, cachePath);
                    yield break;
                }

                // sha256 mismatch: discard, refetch once bypassing edge caches, then fail hard.
                var hashVerdict = policy.OnHashMismatch();
                Debug.LogWarning($"[RuntimeAssetLoader] sha256 mismatch for {resolved.AssetId} — {hashVerdict}");
                if (hashVerdict == FetchRetryPolicy.Verdict.Fail)
                {
                    CompleteAll(resolved.Sha256, null);
                    yield break;
                }
                bypassEdgeCache = true;
            }
        }
    }
}

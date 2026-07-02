using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Orlo.World
{
    /// <summary>
    /// Thin CDN loader implementing docs/design/client_loader_contract.md v1.1
    /// (orlo-assets): runtime_manifest is the ONLY resolver; fetched bytes are
    /// sha256-verified into a content-addressed cache (RuntimeBlobCache).
    ///
    /// Deliberately separate from the legacy AssetLoader pak/StreamingAssets
    /// chain: this loader serves manifest-resolved world assets. Contract
    /// MUST-NOTs enforced here:
    ///   - no URL composition beyond cdn_base + r2_key   (RuntimeManifest.Resolve)
    ///   - no fallback to assets/models/ or staging/ on a manifest miss
    ///   - no unverified bytes handed to callers
    ///   - no redirects off cdn.orlo.games
    ///   - no negative caching (a miss today may resolve after a manifest bump)
    /// </summary>
    public class RuntimeAssetLoader : MonoBehaviour
    {
        public static RuntimeAssetLoader Instance { get; private set; }

        private const string MANIFEST_STREAMING_PATH = "runtime_manifest.json";

        private RuntimeManifest _manifest;
        private RuntimeBlobCache _cache;

        public bool ManifestLoaded => _manifest != null;
        public int ManifestEntryCount => _manifest?.EntryCount ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cache = new RuntimeBlobCache();
            LoadManifestFromStreamingAssets();
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
        /// content-addressed cache path, or null on hard failure. Misses throw
        /// AssetNotInManifestException synchronously — no URL probing.
        /// </summary>
        public void Fetch(string assetId, Action<string> onComplete)
        {
            if (_manifest == null)
                throw new ManifestRefusedException("no valid runtime manifest loaded — cannot resolve assets");

            var resolved = _manifest.Resolve(assetId); // throws on miss (contract §3)

            if (_cache.Contains(resolved.Sha256))
            {
                onComplete?.Invoke(_cache.PathFor(resolved.Sha256));
                return;
            }
            StartCoroutine(FetchCoroutine(resolved, onComplete));
        }

        private IEnumerator FetchCoroutine(RuntimeManifest.ResolvedAsset resolved, Action<string> onComplete)
        {
            var policy = new FetchRetryPolicy();

            while (true)
            {
                using (var request = UnityWebRequest.Get(resolved.Url))
                {
                    // Contract MUST-NOT: never follow redirects off cdn.orlo.games.
                    // Simplest safe form: no redirects at all — the CDN serves blobs directly.
                    request.redirectLimit = 0;
                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        var verdict = policy.OnNetworkFailure();
                        Debug.LogWarning($"[RuntimeAssetLoader] fetch failed ({request.error}, HTTP {request.responseCode}) for {resolved.AssetId} — {verdict}");
                        if (verdict == FetchRetryPolicy.Verdict.Fail)
                        {
                            onComplete?.Invoke(null);
                            yield break;
                        }
                        yield return new WaitForSeconds(policy.BackoffFor(policy.NetworkFailures));
                        continue;
                    }

                    byte[] bytes = request.downloadHandler.data;
                    string cachePath = _cache.VerifyAndCommit(bytes, resolved.Sha256);
                    if (cachePath != null)
                    {
                        onComplete?.Invoke(cachePath);
                        yield break;
                    }

                    // sha256 mismatch: discard, refetch once from origin, then fail hard.
                    var hashVerdict = policy.OnHashMismatch();
                    Debug.LogWarning($"[RuntimeAssetLoader] sha256 mismatch for {resolved.AssetId} — {hashVerdict}");
                    if (hashVerdict == FetchRetryPolicy.Verdict.Fail)
                    {
                        onComplete?.Invoke(null);
                        yield break;
                    }
                    // RetryBypassCache: loop re-GETs from origin. (The disk cache was
                    // never written — VerifyAndCommit only commits verified bytes.)
                }
            }
        }
    }
}

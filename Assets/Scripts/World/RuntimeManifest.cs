using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Parsed, validated runtime asset manifest — the client's SINGLE resolver
    /// for CDN-streamed assets, per docs/design/client_loader_contract.md v1.1
    /// (orlo-assets). Derived server-side from blob_manifest + specs; the client
    /// treats assetIds as opaque and never sees repo paths.
    ///
    /// Refusal semantics (contract §3, non-negotiable):
    ///   - schema_version != 1        → whole manifest refused (ManifestRefusedException)
    ///   - malformed r2_key           → that entry refused at parse (dropped + logged)
    ///   - assetId not in manifest    → Resolve throws AssetNotInManifestException;
    ///                                  callers must NOT probe URLs on a miss.
    /// </summary>
    public sealed class RuntimeManifest
    {
        public const int SUPPORTED_SCHEMA_VERSION = 1;

        // blobs/sha256/<64 lowercase hex> — client-side analogue of the server's
        // path-containment guard. Anything else in r2_key is refused (contract §3).
        private static readonly Regex R2KeyPattern =
            new Regex(@"^blobs/sha256/[0-9a-f]{64}$", RegexOptions.Compiled);

        public string CdnBase { get; }
        public int EntryCount => _entries.Count;
        /// <summary>r2_keys refused at parse for failing the format guard (for diagnostics/tests).</summary>
        public IReadOnlyList<string> RefusedEntries => _refused;

        private readonly Dictionary<string, RuntimeAssetEntry> _entries;
        private readonly List<string> _refused;

        private RuntimeManifest(string cdnBase, Dictionary<string, RuntimeAssetEntry> entries, List<string> refused)
        {
            CdnBase = cdnBase;
            _entries = entries;
            _refused = refused;
        }

        /// <summary>
        /// Parse + validate manifest JSON. Throws ManifestRefusedException on
        /// wrong schema_version, missing cdn_base, or unparseable JSON — the
        /// contract forbids limping along on a manifest we don't understand.
        /// </summary>
        public static RuntimeManifest Parse(string json)
        {
            SerializedManifest raw;
            try
            {
                raw = JsonUtility.FromJson<SerializedManifest>(json);
            }
            catch (Exception e)
            {
                throw new ManifestRefusedException($"runtime_manifest is not valid JSON: {e.Message}");
            }
            if (raw == null)
                throw new ManifestRefusedException("runtime_manifest parsed to nothing (empty or malformed JSON)");

            if (raw.schema_version != SUPPORTED_SCHEMA_VERSION)
                throw new ManifestRefusedException(
                    $"runtime_manifest schema_version={raw.schema_version}, client supports {SUPPORTED_SCHEMA_VERSION} — upgrade required");

            if (string.IsNullOrEmpty(raw.cdn_base) || !raw.cdn_base.StartsWith("https://"))
                throw new ManifestRefusedException(
                    $"runtime_manifest cdn_base missing or not https: '{raw.cdn_base}'");

            var entries = new Dictionary<string, RuntimeAssetEntry>(StringComparer.Ordinal);
            var refused = new List<string>();
            string cdnBase = raw.cdn_base.TrimEnd('/');

            if (raw.assets != null)
            {
                foreach (var entry in raw.assets)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.asset_id))
                        continue;
                    // Per-entry refusal: a bad key never becomes a fetchable URL,
                    // but one bad entry must not take down the whole manifest.
                    if (entry.r2_key == null || !R2KeyPattern.IsMatch(entry.r2_key)
                        || string.IsNullOrEmpty(entry.sha256) || entry.sha256.Length != 64)
                    {
                        refused.Add(entry.asset_id);
                        Debug.LogWarning($"[RuntimeManifest] refused entry '{entry.asset_id}': bad r2_key/sha256 ('{entry.r2_key}')");
                        continue;
                    }
                    entries[entry.asset_id] = entry;
                }
            }

            return new RuntimeManifest(cdnBase, entries, refused);
        }

        /// <summary>
        /// Resolve an assetId to its fetch info. Throws AssetNotInManifestException
        /// on a miss — hard error, surface to caller, do NOT probe URLs (contract §3).
        /// </summary>
        public ResolvedAsset Resolve(string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || !_entries.TryGetValue(assetId, out var entry))
                throw new AssetNotInManifestException(assetId ?? "<null>");

            return new ResolvedAsset
            {
                AssetId = entry.asset_id,
                // Contract MUST-NOT: URLs are composed from cdn_base + r2_key and nothing else.
                Url = $"{CdnBase}/{entry.r2_key}",
                Sha256 = entry.sha256,
                Size = entry.size,
                ContentType = entry.content_type,
            };
        }

        public bool Contains(string assetId) =>
            !string.IsNullOrEmpty(assetId) && _entries.ContainsKey(assetId);

        // ── Serialized shape (JsonUtility-native: array of entries, NOT a JSON dict;
        //    mirrors AssetLoader.ContentManifest's List<> pattern) ──
        [Serializable]
        private class SerializedManifest
        {
            public int schema_version;
            public string cdn_base;
            public List<RuntimeAssetEntry> assets;
        }

        [Serializable]
        public class RuntimeAssetEntry
        {
            public string asset_id;
            public string r2_key;
            public string sha256;
            public long size;
            public string content_type;
        }

        public struct ResolvedAsset
        {
            public string AssetId;
            public string Url;
            public string Sha256;
            public long Size;
            public string ContentType;
        }
    }

    /// <summary>Whole manifest refused (schema/JSON/cdn_base) — contract §3.</summary>
    public sealed class ManifestRefusedException : Exception
    {
        public ManifestRefusedException(string message) : base(message) { }
    }

    /// <summary>assetId miss — hard error; callers must not fall back to URL probing.</summary>
    public sealed class AssetNotInManifestException : Exception
    {
        public string AssetId { get; }
        public AssetNotInManifestException(string assetId)
            : base($"assetId '{assetId}' not in runtime manifest — no fallback, no URL probing (contract §3)")
        {
            AssetId = assetId;
        }
    }
}

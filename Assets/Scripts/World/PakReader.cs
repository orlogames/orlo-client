using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Reads assets from a .pak archive (OPAK format).
    /// Format: 16-byte header + JSON index + concatenated file data.
    /// Thread-safe via lock on file reads.
    /// </summary>
    public class PakReader : IDisposable
    {
        private const uint MAGIC = 0x4B41504F; // "OPAK" little-endian
        private const ushort FORMAT_VERSION = 1;

        private readonly FileStream _stream;
        private readonly object _lock = new();
        private readonly Dictionary<string, PakEntry> _index = new();
        private readonly long _dataOffset;
        private bool _disposed;

        public int EntryCount => _index.Count;

        private struct PakEntry
        {
            public long Offset;
            public int Size;
            public string Sha256;
        }

        public PakReader(string pakPath)
        {
            _stream = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);

            // Header: magic(4) + version(2) + flags(2) + fileCount(4) + indexLen(4) = 16 bytes
            uint magic = reader.ReadUInt32();
            if (magic != MAGIC)
                throw new InvalidDataException($"Invalid pak magic: 0x{magic:X8}");

            ushort version = reader.ReadUInt16();
            if (version > FORMAT_VERSION)
                throw new InvalidDataException($"Unsupported pak version: {version}");

            reader.ReadUInt16(); // flags (reserved)
            uint fileCount = reader.ReadUInt32();
            uint indexLen = reader.ReadUInt32();

            // Read JSON index
            byte[] indexBytes = reader.ReadBytes((int)indexLen);
            string indexJson = Encoding.UTF8.GetString(indexBytes);

            _dataOffset = 16 + indexLen;

            // Parse JSON index (minimal parser — array of objects)
            ParseIndex(indexJson);

            Debug.Log($"[PakReader] Opened {pakPath}: {_index.Count} assets, data starts at offset {_dataOffset}");
        }

        public bool Contains(string assetName)
        {
            string key = NormalizeKey(assetName);
            return _index.ContainsKey(key);
        }

        public byte[] ReadEntry(string assetName)
        {
            string key = NormalizeKey(assetName);
            if (!_index.TryGetValue(key, out var entry))
                return null;

            lock (_lock)
            {
                _stream.Seek(_dataOffset + entry.Offset, SeekOrigin.Begin);
                byte[] data = new byte[entry.Size];
                int read = 0;
                while (read < entry.Size)
                {
                    int n = _stream.Read(data, read, entry.Size - read);
                    if (n == 0) throw new EndOfStreamException("Unexpected end of pak file");
                    read += n;
                }
                return data;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stream?.Dispose();
        }

        private static string NormalizeKey(string name)
        {
            // Accept "human_guard", "human_guard.glb", or "character/human_guard.glb"
            string key = name.Replace('\\', '/');
            // Strip directory prefix if present
            int slash = key.LastIndexOf('/');
            if (slash >= 0) key = key.Substring(slash + 1);
            // Strip extension
            if (key.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(0, key.Length - 4);
            return key.ToLowerInvariant();
        }

        private void ParseIndex(string json)
        {
            // Minimal JSON array parser — avoids dependency on JsonUtility (which can't parse arrays)
            // Expected format: [{"path":"name.glb","offset":0,"size":123,"sha256":"..."},...]
            int i = 0;
            while (i < json.Length)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0) break;
                int objEnd = json.IndexOf('}', objStart);
                if (objEnd < 0) break;

                string obj = json.Substring(objStart + 1, objEnd - objStart - 1);
                i = objEnd + 1;

                string path = ExtractStringField(obj, "path");
                long offset = ExtractLongField(obj, "offset");
                int size = (int)ExtractLongField(obj, "size");
                string sha256 = ExtractStringField(obj, "sha256");

                if (string.IsNullOrEmpty(path)) continue;

                string key = NormalizeKey(path);
                _index[key] = new PakEntry { Offset = offset, Size = size, Sha256 = sha256 };
            }
        }

        private static string ExtractStringField(string obj, string field)
        {
            string pattern = $"\"{field}\"";
            int idx = obj.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colonIdx = obj.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return null;
            int quoteStart = obj.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return null;
            int quoteEnd = obj.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return null;
            return obj.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private static long ExtractLongField(string obj, string field)
        {
            string pattern = $"\"{field}\"";
            int idx = obj.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return 0;
            int colonIdx = obj.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return 0;
            int start = colonIdx + 1;
            while (start < obj.Length && (obj[start] == ' ' || obj[start] == '\t')) start++;
            int end = start;
            while (end < obj.Length && (char.IsDigit(obj[end]) || obj[end] == '-')) end++;
            if (end <= start) return 0;
            return long.Parse(obj.Substring(start, end - start));
        }
    }
}

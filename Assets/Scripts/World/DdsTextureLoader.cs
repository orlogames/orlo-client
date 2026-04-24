using System;
using System.IO; // permitted by spec; reserved for future File-based overloads
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Pure-C# DDS → Texture2D loader. Decodes the three FourCC formats Stepo's
    /// export pipeline writes — DXT1 (BC1, no alpha), DXT3 (BC2), DXT5 (BC3) —
    /// into RGBA32 and uploads to a Unity Texture2D.
    ///
    /// Why this exists: PR #9's sidecar texture loader handles PNG/JPG via
    /// Texture2D.LoadImage, but the armorer ships its three textures as DDS,
    /// and Unity's runtime APIs don't decode DDS containers (only raw GPU formats).
    /// We parse the 128-byte header ourselves and decompress block-by-block.
    ///
    /// Limitations:
    ///   - DX10 extended header is not supported (logs warning + returns null).
    ///   - Mipmaps in the file are ignored; the top-level mip is decoded and
    ///     Unity rebuilds the mip chain via Apply(updateMipmaps: true).
    ///   - No BC4/BC5/BC6/BC7 yet (TODO once a real asset needs them).
    /// </summary>
    public static class DdsTextureLoader
    {
        // FourCC tags packed little-endian as uint for cheap comparison.
        private const uint FOURCC_DXT1 = 0x31545844; // "DXT1"
        private const uint FOURCC_DXT3 = 0x33545844; // "DXT3"
        private const uint FOURCC_DXT5 = 0x35545844; // "DXT5"
        private const uint FOURCC_DX10 = 0x30315844; // "DX10"
        private const uint MAGIC_DDS  = 0x20534444;  // "DDS "

        /// <summary>
        /// Decode a DDS byte blob into a Texture2D. Returns null on any unsupported
        /// format (DX10 header, BC4/5/6/7, uncompressed, etc.) and logs a warning
        /// tagged with <paramref name="debugLabel"/> for traceability.
        /// </summary>
        public static Texture2D Load(byte[] bytes, string debugLabel = null)
        {
            string label = debugLabel ?? "<unknown>";

            if (bytes == null || bytes.Length < 128)
            {
                Debug.LogWarning($"[DdsTextureLoader] {label}: file too small ({(bytes == null ? 0 : bytes.Length)} bytes)");
                return null;
            }

            uint magic = BitConverter.ToUInt32(bytes, 0);
            if (magic != MAGIC_DDS)
            {
                Debug.LogWarning($"[DdsTextureLoader] {label}: bad magic 0x{magic:X8} (expected 'DDS ')");
                return null;
            }

            // DDS_HEADER layout (offsets relative to start of file, after 4-byte magic):
            //   4   dwSize           (always 124)
            //   8   dwFlags
            //  12   dwHeight
            //  16   dwWidth
            //  20   dwPitchOrLinearSize
            //  24   dwDepth
            //  28   dwMipMapCount
            //  32   dwReserved1[11]   (44 bytes)
            //  76   ddspf.dwSize
            //  80   ddspf.dwFlags
            //  84   ddspf.dwFourCC
            //  88   ddspf.dwRGBBitCount
            //  ... rest of pixel format + caps fields up to byte 128
            int height = (int)BitConverter.ToUInt32(bytes, 12);
            int width  = (int)BitConverter.ToUInt32(bytes, 16);
            int mipCount = (int)BitConverter.ToUInt32(bytes, 28);
            uint fourCC = BitConverter.ToUInt32(bytes, 84);

            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            {
                Debug.LogWarning($"[DdsTextureLoader] {label}: implausible dimensions {width}x{height}");
                return null;
            }

            if (fourCC == FOURCC_DX10)
            {
                Debug.LogWarning($"[DdsTextureLoader] {label}: DX10 extended header not yet supported (TODO)");
                return null;
            }

            int blockSize;
            switch (fourCC)
            {
                case FOURCC_DXT1: blockSize = 8;  break;
                case FOURCC_DXT3: blockSize = 16; break;
                case FOURCC_DXT5: blockSize = 16; break;
                default:
                    // Decode FourCC bytes back to ASCII for the warning.
                    char c0 = (char)(fourCC & 0xFF);
                    char c1 = (char)((fourCC >> 8) & 0xFF);
                    char c2 = (char)((fourCC >> 16) & 0xFF);
                    char c3 = (char)((fourCC >> 24) & 0xFF);
                    Debug.LogWarning($"[DdsTextureLoader] {label}: unsupported FourCC '{c0}{c1}{c2}{c3}' (0x{fourCC:X8})");
                    return null;
            }

            // Pixel data starts immediately after the 128-byte header.
            int dataOffset = 128;
            int blocksWide = Mathf.Max(1, (width  + 3) / 4);
            int blocksHigh = Mathf.Max(1, (height + 3) / 4);
            int expectedTopLevelBytes = blocksWide * blocksHigh * blockSize;

            if (dataOffset + expectedTopLevelBytes > bytes.Length)
            {
                Debug.LogWarning($"[DdsTextureLoader] {label}: truncated — need {expectedTopLevelBytes} bytes, " +
                                 $"have {bytes.Length - dataOffset}");
                return null;
            }

            // Decode top-level mip into RGBA32. Unity will rebuild the mip chain on Apply().
            byte[] rgba = new byte[width * height * 4];

            try
            {
                switch (fourCC)
                {
                    case FOURCC_DXT1:
                        DecodeBC1(bytes, dataOffset, width, height, blocksWide, blocksHigh, rgba);
                        break;
                    case FOURCC_DXT3:
                        DecodeBC2(bytes, dataOffset, width, height, blocksWide, blocksHigh, rgba);
                        break;
                    case FOURCC_DXT5:
                        DecodeBC3(bytes, dataOffset, width, height, blocksWide, blocksHigh, rgba);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DdsTextureLoader] {label}: decode failed — {ex.Message}");
                return null;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true);
            tex.LoadRawTextureData(rgba);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return tex;
        }

        // ─── BC1 (DXT1) ────────────────────────────────────────────────────
        // 8-byte block: 2 RGB565 endpoints (4 bytes) + 16 2-bit indices (4 bytes).
        // If c0 > c1, palette is { c0, c1, lerp(2/3,1/3), lerp(1/3,2/3) }.
        // Else            palette is { c0, c1, midpoint, transparent black }.
        private static void DecodeBC1(byte[] src, int srcOff, int width, int height,
            int blocksWide, int blocksHigh, byte[] dst)
        {
            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    int b = srcOff + (by * blocksWide + bx) * 8;
                    DecodeBC1ColorBlock(src, b, bx * 4, by * 4, width, height, dst, alphaOverride: -1);
                }
            }
        }

        // ─── BC2 (DXT3) ────────────────────────────────────────────────────
        // 16-byte block: 8 bytes explicit 4-bit alpha + 8 bytes BC1-style color
        // (color always uses the 4-color palette, no transparent-black variant).
        private static void DecodeBC2(byte[] src, int srcOff, int width, int height,
            int blocksWide, int blocksHigh, byte[] dst)
        {
            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    int b = srcOff + (by * blocksWide + bx) * 16;
                    int colorOff = b + 8;
                    int pixelX0 = bx * 4;
                    int pixelY0 = by * 4;

                    // Decode color first (BC2 forces the 4-color palette interpretation).
                    DecodeBC1ColorBlock(src, colorOff, pixelX0, pixelY0, width, height, dst,
                        alphaOverride: -1, forceFourColor: true);

                    // Overwrite alpha with the explicit 4-bit values from bytes 0..7.
                    for (int py = 0; py < 4; py++)
                    {
                        ushort row = (ushort)(src[b + py * 2] | (src[b + py * 2 + 1] << 8));
                        for (int px = 0; px < 4; px++)
                        {
                            int x = pixelX0 + px;
                            int y = pixelY0 + py;
                            if (x >= width || y >= height) continue;
                            int nibble = (row >> (px * 4)) & 0xF;
                            byte a = (byte)((nibble << 4) | nibble); // 4-bit → 8-bit replicate
                            dst[(y * width + x) * 4 + 3] = a;
                        }
                    }
                }
            }
        }

        // ─── BC3 (DXT5) ────────────────────────────────────────────────────
        // 16-byte block: 8 bytes BC4-style alpha (2 endpoints + 16 3-bit indices)
        // + 8 bytes BC1-style color (always 4-color palette).
        private static void DecodeBC3(byte[] src, int srcOff, int width, int height,
            int blocksWide, int blocksHigh, byte[] dst)
        {
            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    int b = srcOff + (by * blocksWide + bx) * 16;
                    int colorOff = b + 8;
                    int pixelX0 = bx * 4;
                    int pixelY0 = by * 4;

                    DecodeBC1ColorBlock(src, colorOff, pixelX0, pixelY0, width, height, dst,
                        alphaOverride: -1, forceFourColor: true);

                    // BC4 alpha decode.
                    byte a0 = src[b + 0];
                    byte a1 = src[b + 1];
                    byte[] palette = new byte[8];
                    palette[0] = a0;
                    palette[1] = a1;
                    if (a0 > a1)
                    {
                        // 8-step palette
                        for (int i = 1; i <= 6; i++)
                            palette[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
                    }
                    else
                    {
                        // 6-step palette + 0 + 255
                        for (int i = 1; i <= 4; i++)
                            palette[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                        palette[6] = 0;
                        palette[7] = 255;
                    }

                    // 16 3-bit indices packed into 6 bytes (bytes 2..7 of the block).
                    // Read as two 24-bit little-endian groups: bytes 2..4 = top half (8 indices),
                    // bytes 5..7 = bottom half (8 indices).
                    uint bits0 = (uint)(src[b + 2] | (src[b + 3] << 8) | (src[b + 4] << 16));
                    uint bits1 = (uint)(src[b + 5] | (src[b + 6] << 8) | (src[b + 7] << 16));

                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int linear = py * 4 + px;
                            uint bits = linear < 8 ? bits0 : bits1;
                            int shift = (linear < 8 ? linear : linear - 8) * 3;
                            int idx = (int)((bits >> shift) & 0x7);

                            int x = pixelX0 + px;
                            int y = pixelY0 + py;
                            if (x >= width || y >= height) continue;
                            dst[(y * width + x) * 4 + 3] = palette[idx];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shared BC1-style 4×4 color block decoder used by BC1, BC2, and BC3.
        /// Writes RGB into dst at (pixelX0, pixelY0); alpha is left untouched unless
        /// the BC1 transparent-black case fires (only when forceFourColor=false).
        /// </summary>
        private static void DecodeBC1ColorBlock(byte[] src, int blockOff,
            int pixelX0, int pixelY0, int width, int height, byte[] dst,
            int alphaOverride, bool forceFourColor = false)
        {
            ushort c0 = (ushort)(src[blockOff + 0] | (src[blockOff + 1] << 8));
            ushort c1 = (ushort)(src[blockOff + 2] | (src[blockOff + 3] << 8));

            // RGB565 → RGB888 (with bit replication so 0x1F maps to 0xFF, not 0xF8).
            byte r0, g0, b0, r1, g1, b1;
            Unpack565(c0, out r0, out g0, out b0);
            Unpack565(c1, out r1, out g1, out b1);

            // Build 4-entry palette (R, G, B, transparency-flag).
            byte[] palR = new byte[4];
            byte[] palG = new byte[4];
            byte[] palB = new byte[4];
            bool[] palTransparent = new bool[4];

            palR[0] = r0; palG[0] = g0; palB[0] = b0;
            palR[1] = r1; palG[1] = g1; palB[1] = b1;

            if (c0 > c1 || forceFourColor)
            {
                palR[2] = (byte)((2 * r0 + r1) / 3);
                palG[2] = (byte)((2 * g0 + g1) / 3);
                palB[2] = (byte)((2 * b0 + b1) / 3);
                palR[3] = (byte)((r0 + 2 * r1) / 3);
                palG[3] = (byte)((g0 + 2 * g1) / 3);
                palB[3] = (byte)((b0 + 2 * b1) / 3);
            }
            else
            {
                palR[2] = (byte)((r0 + r1) / 2);
                palG[2] = (byte)((g0 + g1) / 2);
                palB[2] = (byte)((b0 + b1) / 2);
                palR[3] = 0; palG[3] = 0; palB[3] = 0;
                palTransparent[3] = true; // BC1 transparent-black entry
            }

            // 16 2-bit indices in bytes 4..7.
            uint indices = (uint)(src[blockOff + 4]
                                 | (src[blockOff + 5] << 8)
                                 | (src[blockOff + 6] << 16)
                                 | (src[blockOff + 7] << 24));

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int x = pixelX0 + px;
                    int y = pixelY0 + py;
                    if (x >= width || y >= height) continue;

                    int linear = py * 4 + px;
                    int idx = (int)((indices >> (linear * 2)) & 0x3);
                    int dstOff = (y * width + x) * 4;

                    dst[dstOff + 0] = palR[idx];
                    dst[dstOff + 1] = palG[idx];
                    dst[dstOff + 2] = palB[idx];

                    // Alpha policy:
                    //   alphaOverride >= 0 — caller-supplied constant (unused today).
                    //   BC1 transparent-black slot — alpha = 0.
                    //   Otherwise: BC1 opaque pixel → 0xFF; BC2/BC3 callers will
                    //   overwrite alpha after this routine returns.
                    if (alphaOverride >= 0)
                    {
                        dst[dstOff + 3] = (byte)alphaOverride;
                    }
                    else if (palTransparent[idx])
                    {
                        dst[dstOff + 3] = 0;
                    }
                    else if (!forceFourColor)
                    {
                        // BC1 opaque pixel → solid alpha. BC2/BC3 callers will overwrite.
                        dst[dstOff + 3] = 255;
                    }
                }
            }
        }

        private static void Unpack565(ushort c, out byte r, out byte g, out byte b)
        {
            int rr = (c >> 11) & 0x1F;
            int gg = (c >> 5)  & 0x3F;
            int bb = c         & 0x1F;
            // Bit-replication for full 0..255 range.
            r = (byte)((rr << 3) | (rr >> 2));
            g = (byte)((gg << 2) | (gg >> 4));
            b = (byte)((bb << 3) | (bb >> 2));
        }
    }
}

using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Generates procedural tileable terrain textures and normal maps at runtime.
    /// Called once at startup by TerrainManager — textures persist in memory.
    /// Each texture is 256x256 with matching normal map for PBR surface detail.
    /// </summary>
    public static class TerrainTextures
    {
        private const int Size = 512;

        public static Texture2D GrassTex { get; private set; }
        public static Texture2D RockTex { get; private set; }
        public static Texture2D DirtTex { get; private set; }
        public static Texture2D SandTex { get; private set; }

        public static Texture2D GrassNorm { get; private set; }
        public static Texture2D RockNorm { get; private set; }
        public static Texture2D DirtNorm { get; private set; }
        public static Texture2D SandNorm { get; private set; }

        private static bool _initialized;

        /// <summary>
        /// Generate all terrain textures. Safe to call multiple times (no-op after first).
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            GenerateGrass();
            GenerateRock();
            GenerateDirt();
            GenerateSand();

            sw.Stop();
            Debug.Log($"[TerrainTextures] Generated 8 textures ({Size}x{Size}) in {sw.ElapsedMilliseconds}ms");
        }

        // ─────────────────────────────────────────────────────────────────
        // Grass: warm green with blade-like pattern and yellow/brown hints
        // ─────────────────────────────────────────────────────────────────
        private static void GenerateGrass()
        {
            var pixels = new Color[Size * Size];
            var heights = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (float)x / Size;
                    float v = (float)y / Size;

                    // Multi-octave noise for grass blade clumps
                    float n1 = PerlinTileable(u, v, 6f);
                    float n2 = PerlinTileable(u, v, 14f);
                    float n3 = PerlinTileable(u, v, 32f);
                    float blade = n1 * 0.4f + n2 * 0.35f + n3 * 0.25f;

                    // Directional streaks (grass blades lean one way)
                    float streak = PerlinTileable(u * 0.8f + v * 0.6f, v * 0.8f - u * 0.3f, 20f);
                    blade = blade * 0.7f + streak * 0.3f;

                    // Yellow-brown variation patches
                    float patch = PerlinTileable(u, v, 3f);

                    // Base warm green
                    float r = 0.28f + blade * 0.12f + patch * 0.06f;
                    float g = 0.46f + blade * 0.16f - patch * 0.04f;
                    float b = 0.14f + blade * 0.06f;

                    // Occasional yellow-brown dry patches
                    if (patch > 0.65f)
                    {
                        float dryAmount = (patch - 0.65f) * 2.5f;
                        r = Mathf.Lerp(r, 0.48f, dryAmount * 0.4f);
                        g = Mathf.Lerp(g, 0.42f, dryAmount * 0.3f);
                        b = Mathf.Lerp(b, 0.16f, dryAmount * 0.2f);
                    }

                    pixels[i] = new Color(r, g, b);
                    heights[i] = blade;
                }
            }

            GrassTex = CreateTexture("GrassTex", pixels);
            GrassNorm = GenerateNormalMap("GrassNorm", heights, 0.6f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Rock: warm grey-brown with multi-octave fractal cracks
        // ─────────────────────────────────────────────────────────────────
        private static void GenerateRock()
        {
            var pixels = new Color[Size * Size];
            var heights = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (float)x / Size;
                    float v = (float)y / Size;

                    // Multi-octave fractal for rocky surface
                    float n1 = PerlinTileable(u, v, 4f);
                    float n2 = PerlinTileable(u, v, 9f);
                    float n3 = PerlinTileable(u, v, 22f);
                    float n4 = PerlinTileable(u, v, 48f);
                    float rock = n1 * 0.35f + n2 * 0.3f + n3 * 0.2f + n4 * 0.15f;

                    // Crack-like dark lines (Worley-ish via threshold)
                    float crack = PerlinTileable(u, v, 12f);
                    float crackMask = crack < 0.3f ? (0.3f - crack) * 2f : 0f;

                    // Base warm grey-brown
                    float r = 0.46f + rock * 0.18f - crackMask * 0.15f;
                    float g = 0.42f + rock * 0.14f - crackMask * 0.12f;
                    float b = 0.36f + rock * 0.10f - crackMask * 0.10f;

                    // Lighter highlights on peaks
                    if (rock > 0.6f)
                    {
                        float highlight = (rock - 0.6f) * 1.5f;
                        r += highlight * 0.08f;
                        g += highlight * 0.07f;
                        b += highlight * 0.05f;
                    }

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = rock - crackMask * 0.5f;
                }
            }

            RockTex = CreateTexture("RockTex", pixels);
            RockNorm = GenerateNormalMap("RockNorm", heights, 1.2f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Dirt: brown with scattered small stones and subtle cracking
        // ─────────────────────────────────────────────────────────────────
        private static void GenerateDirt()
        {
            var pixels = new Color[Size * Size];
            var heights = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (float)x / Size;
                    float v = (float)y / Size;

                    // Broad undulation
                    float n1 = PerlinTileable(u, v, 5f);
                    // Medium detail
                    float n2 = PerlinTileable(u, v, 15f);
                    // Fine grain
                    float n3 = PerlinTileable(u, v, 35f);
                    float dirt = n1 * 0.4f + n2 * 0.35f + n3 * 0.25f;

                    // Small pebble/stone highlights
                    float pebble = PerlinTileable(u, v, 50f);
                    float pebbleMask = pebble > 0.72f ? (pebble - 0.72f) * 4f : 0f;

                    // Crack pattern (dried mud)
                    float crackX = PerlinTileable(u, v, 8f);
                    float crackLine = Mathf.Abs(crackX - 0.5f) < 0.04f ? 1f : 0f;
                    crackLine *= PerlinTileable(u, v, 3f) > 0.4f ? 1f : 0f; // only in patches

                    // Base warm brown
                    float r = 0.42f + dirt * 0.14f + pebbleMask * 0.12f - crackLine * 0.08f;
                    float g = 0.30f + dirt * 0.10f + pebbleMask * 0.10f - crackLine * 0.06f;
                    float b = 0.16f + dirt * 0.06f + pebbleMask * 0.06f - crackLine * 0.04f;

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = dirt + pebbleMask * 0.3f - crackLine * 0.2f;
                }
            }

            DirtTex = CreateTexture("DirtTex", pixels);
            DirtNorm = GenerateNormalMap("DirtNorm", heights, 0.8f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sand: warm tan with fine grain noise and wind ripple pattern
        // ─────────────────────────────────────────────────────────────────
        private static void GenerateSand()
        {
            var pixels = new Color[Size * Size];
            var heights = new float[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (float)x / Size;
                    float v = (float)y / Size;

                    // Wind ripple pattern (directional, like dune ridges)
                    float ripple = PerlinTileable(u * 0.3f + v * 0.95f, v * 0.3f - u * 0.05f, 12f);
                    float ripple2 = PerlinTileable(u * 0.35f + v * 0.9f, v * 0.35f - u * 0.1f, 24f);
                    float rippleVal = ripple * 0.6f + ripple2 * 0.4f;

                    // Fine grain noise (individual sand grains at close range)
                    float grain = PerlinTileable(u, v, 64f);
                    float grain2 = PerlinTileable(u, v, 40f);
                    float fineGrain = grain * 0.5f + grain2 * 0.5f;

                    // Broad color variation (wet/dry patches)
                    float broad = PerlinTileable(u, v, 3f);

                    // Base warm tan
                    float r = 0.74f + rippleVal * 0.06f + fineGrain * 0.04f - broad * 0.03f;
                    float g = 0.68f + rippleVal * 0.05f + fineGrain * 0.03f - broad * 0.04f;
                    float b = 0.48f + rippleVal * 0.04f + fineGrain * 0.02f - broad * 0.02f;

                    // Subtle darker wet patches
                    if (broad < 0.3f)
                    {
                        float wet = (0.3f - broad) * 1.5f;
                        r -= wet * 0.06f;
                        g -= wet * 0.05f;
                        b -= wet * 0.03f;
                    }

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = rippleVal * 0.7f + fineGrain * 0.3f;
                }
            }

            SandTex = CreateTexture("SandTex", pixels);
            SandNorm = GenerateNormalMap("SandNorm", heights, 0.4f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Utilities
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tileable Perlin noise using wrapping coordinates.
        /// Returns 0-1 range.
        /// </summary>
        private static float PerlinTileable(float u, float v, float frequency)
        {
            // Use Mathf.PerlinNoise with offset to make it tileable
            // We sample on a torus in 4D projected to 2D Perlin
            float angle_u = u * Mathf.PI * 2f;
            float angle_v = v * Mathf.PI * 2f;

            float nx = Mathf.Cos(angle_u) * frequency * 0.1591549f; // 1/(2*PI)
            float ny = Mathf.Sin(angle_u) * frequency * 0.1591549f;
            float nz = Mathf.Cos(angle_v) * frequency * 0.1591549f;
            float nw = Mathf.Sin(angle_v) * frequency * 0.1591549f;

            // Sample 2D Perlin at two different offsets and blend
            float s1 = Mathf.PerlinNoise(nx + 100f, nz + 100f);
            float s2 = Mathf.PerlinNoise(ny + 200f, nw + 200f);
            return Mathf.Clamp01(s1 * 0.5f + s2 * 0.5f);
        }

        /// <summary>
        /// Create a Texture2D from pixel array, set to wrap (tileable).
        /// </summary>
        private static Texture2D CreateTexture(string name, Color[] pixels)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.SetPixels(pixels);
            tex.Apply(true); // generate mipmaps
            return tex;
        }

        /// <summary>
        /// Generate a normal map from heightmap values using Sobel filter.
        /// </summary>
        private static Texture2D GenerateNormalMap(string name, float[] heights, float strength)
        {
            var pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Sobel filter with wrapping for tileability
                    float tl = heights[Wrap(y - 1) * Size + Wrap(x - 1)];
                    float t  = heights[Wrap(y - 1) * Size + x];
                    float tr = heights[Wrap(y - 1) * Size + Wrap(x + 1)];
                    float l  = heights[y * Size + Wrap(x - 1)];
                    float r  = heights[y * Size + Wrap(x + 1)];
                    float bl = heights[Wrap(y + 1) * Size + Wrap(x - 1)];
                    float b  = heights[Wrap(y + 1) * Size + x];
                    float br = heights[Wrap(y + 1) * Size + Wrap(x + 1)];

                    // Sobel X and Y
                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (bl + 2f * b + br) - (tl + 2f * t + tr);

                    // Normal in tangent space
                    Vector3 normal = new Vector3(-dx * strength, -dy * strength, 1f).normalized;

                    // Encode to 0-1 range for texture storage (Unity DXT5nm expects this)
                    pixels[y * Size + x] = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        normal.x * 0.5f + 0.5f  // DXT5nm uses alpha for X
                    );
                }
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private static int Wrap(int coord)
        {
            return ((coord % Size) + Size) % Size;
        }
    }
}

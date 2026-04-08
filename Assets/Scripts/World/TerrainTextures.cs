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
        // Grass: vibrant lush meadow green with dark patches and yellow highlights
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

                    // Multi-octave noise for grass blade clumps — wide contrast
                    float n1 = PerlinTileable(u, v, 6f);
                    float n2 = PerlinTileable(u, v, 14f);
                    float n3 = PerlinTileable(u, v, 32f);
                    float n4 = PerlinTileable(u, v, 56f); // fine blade detail
                    float blade = n1 * 0.3f + n2 * 0.3f + n3 * 0.25f + n4 * 0.15f;

                    // Remap to full 0-1 range for max contrast
                    blade = Mathf.Clamp01((blade - 0.3f) * 2.5f);

                    // Directional streaks (grass blades lean one way)
                    float streak = PerlinTileable(u * 0.8f + v * 0.6f, v * 0.8f - u * 0.3f, 20f);
                    blade = blade * 0.65f + streak * 0.35f;

                    // Yellow-green highlight variation
                    float patch = PerlinTileable(u, v, 3f);

                    // Vibrant green base (0.3, 0.55, 0.15) with strong variation
                    float r = 0.18f + blade * 0.24f + patch * 0.08f;
                    float g = 0.38f + blade * 0.34f - patch * 0.06f;
                    float b = 0.06f + blade * 0.18f;

                    // Darker green shadow patches (deep forest floor)
                    float shadow = PerlinTileable(u, v, 8f);
                    if (shadow < 0.35f)
                    {
                        float darkAmount = (0.35f - shadow) * 2.8f;
                        r -= darkAmount * 0.10f;
                        g -= darkAmount * 0.12f;
                        b -= darkAmount * 0.04f;
                    }

                    // Yellow-green highlights (sunlit grass tips)
                    if (blade > 0.7f)
                    {
                        float highlight = (blade - 0.7f) * 3.3f;
                        r += highlight * 0.12f;
                        g += highlight * 0.08f;
                        b -= highlight * 0.02f;
                    }

                    // Occasional yellow-brown dry patches
                    if (patch > 0.7f)
                    {
                        float dryAmount = (patch - 0.7f) * 3.3f;
                        r = Mathf.Lerp(r, 0.52f, dryAmount * 0.5f);
                        g = Mathf.Lerp(g, 0.48f, dryAmount * 0.4f);
                        b = Mathf.Lerp(b, 0.15f, dryAmount * 0.3f);
                    }

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = blade + n4 * 0.4f; // fine detail for normal map
                }
            }

            GrassTex = CreateTexture("GrassTex", pixels);
            GrassNorm = GenerateNormalMap("GrassNorm", heights, 1.8f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Rock: grey stone with dark cracks/veins and bright highlights
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

                    // Multi-octave fractal for rocky surface — high contrast
                    float n1 = PerlinTileable(u, v, 4f);
                    float n2 = PerlinTileable(u, v, 9f);
                    float n3 = PerlinTileable(u, v, 22f);
                    float n4 = PerlinTileable(u, v, 48f);
                    float n5 = PerlinTileable(u, v, 80f); // fine grain detail
                    float rock = n1 * 0.25f + n2 * 0.25f + n3 * 0.2f + n4 * 0.18f + n5 * 0.12f;

                    // Remap for full range
                    rock = Mathf.Clamp01((rock - 0.25f) * 2.0f);

                    // Crack network — two overlapping crack directions
                    float crack1 = PerlinTileable(u, v, 12f);
                    float crack2 = PerlinTileable(u * 0.7f + v * 0.3f, v * 0.7f - u * 0.3f, 16f);
                    float crackMask = 0f;
                    if (crack1 < 0.28f) crackMask += (0.28f - crack1) * 3.5f;
                    if (crack2 < 0.22f) crackMask += (0.22f - crack2) * 3.0f;
                    crackMask = Mathf.Clamp01(crackMask);

                    // Base grey stone (0.45, 0.43, 0.40) with strong variation
                    float r = 0.32f + rock * 0.28f - crackMask * 0.22f;
                    float g = 0.30f + rock * 0.26f - crackMask * 0.20f;
                    float b = 0.27f + rock * 0.24f - crackMask * 0.18f;

                    // Bright highlights on peaks (lichen-like pale spots)
                    if (rock > 0.65f)
                    {
                        float highlight = (rock - 0.65f) * 2.8f;
                        r += highlight * 0.18f;
                        g += highlight * 0.16f;
                        b += highlight * 0.12f;
                    }

                    // Slight warm tint variation (mineral streaks)
                    float mineral = PerlinTileable(u, v, 6f);
                    if (mineral > 0.6f)
                    {
                        float tint = (mineral - 0.6f) * 2.5f;
                        r += tint * 0.06f;
                        g -= tint * 0.02f;
                        b -= tint * 0.04f;
                    }

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = rock - crackMask * 0.7f + n5 * 0.3f;
                }
            }

            RockTex = CreateTexture("RockTex", pixels);
            RockNorm = GenerateNormalMap("RockNorm", heights, 2.5f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Dirt: warm brown earth with dark spots and pebble noise
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
                    // Medium clumps
                    float n2 = PerlinTileable(u, v, 15f);
                    // Fine grain
                    float n3 = PerlinTileable(u, v, 35f);
                    // Very fine (individual dirt granules)
                    float n4 = PerlinTileable(u, v, 60f);
                    float dirt = n1 * 0.3f + n2 * 0.3f + n3 * 0.25f + n4 * 0.15f;

                    // Remap for contrast
                    dirt = Mathf.Clamp01((dirt - 0.25f) * 2.2f);

                    // Small pebble/stone highlights — more frequent and brighter
                    float pebble = PerlinTileable(u, v, 50f);
                    float pebble2 = PerlinTileable(u + 0.5f, v + 0.5f, 70f);
                    float pebbleMask = 0f;
                    if (pebble > 0.65f) pebbleMask += (pebble - 0.65f) * 5f;
                    if (pebble2 > 0.7f) pebbleMask += (pebble2 - 0.7f) * 4f;
                    pebbleMask = Mathf.Clamp01(pebbleMask);

                    // Crack pattern (dried mud) — wider and more visible
                    float crackX = PerlinTileable(u, v, 8f);
                    float crackY = PerlinTileable(u, v, 11f);
                    float crackLine = 0f;
                    if (Mathf.Abs(crackX - 0.5f) < 0.05f) crackLine += 1f;
                    if (Mathf.Abs(crackY - 0.5f) < 0.04f) crackLine += 0.7f;
                    crackLine = Mathf.Clamp01(crackLine);
                    crackLine *= PerlinTileable(u, v, 3f) > 0.35f ? 1f : 0f;

                    // Dark moisture spots
                    float moisture = PerlinTileable(u, v, 7f);
                    float moistureMask = moisture < 0.3f ? (0.3f - moisture) * 3f : 0f;

                    // Base warm brown (0.45, 0.32, 0.18) with strong variation
                    float r = 0.30f + dirt * 0.30f + pebbleMask * 0.18f - crackLine * 0.14f - moistureMask * 0.10f;
                    float g = 0.20f + dirt * 0.24f + pebbleMask * 0.14f - crackLine * 0.10f - moistureMask * 0.08f;
                    float b = 0.08f + dirt * 0.16f + pebbleMask * 0.10f - crackLine * 0.06f - moistureMask * 0.05f;

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = dirt + pebbleMask * 0.5f - crackLine * 0.4f + n4 * 0.2f;
                }
            }

            DirtTex = CreateTexture("DirtTex", pixels);
            DirtNorm = GenerateNormalMap("DirtNorm", heights, 2.0f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sand: light warm tan with visible ripple patterns
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

                    // Wind ripple pattern — stronger directional ridges
                    float ripple = PerlinTileable(u * 0.3f + v * 0.95f, v * 0.3f - u * 0.05f, 12f);
                    float ripple2 = PerlinTileable(u * 0.35f + v * 0.9f, v * 0.35f - u * 0.1f, 24f);
                    float rippleVal = ripple * 0.55f + ripple2 * 0.45f;

                    // Sharpen ripples into ridges (push toward 0 and 1)
                    rippleVal = Mathf.Clamp01((rippleVal - 0.3f) * 2.0f);

                    // Fine grain noise (individual sand grains)
                    float grain = PerlinTileable(u, v, 64f);
                    float grain2 = PerlinTileable(u, v, 40f);
                    float fineGrain = grain * 0.5f + grain2 * 0.5f;

                    // Broad color variation (wet/dry patches)
                    float broad = PerlinTileable(u, v, 3f);

                    // Base light warm tan (0.75, 0.65, 0.45) with visible ripple contrast
                    float r = 0.60f + rippleVal * 0.18f + fineGrain * 0.06f;
                    float g = 0.50f + rippleVal * 0.16f + fineGrain * 0.05f;
                    float b = 0.30f + rippleVal * 0.14f + fineGrain * 0.04f;

                    // Darker troughs between ripples
                    if (rippleVal < 0.25f)
                    {
                        float trough = (0.25f - rippleVal) * 4f;
                        r -= trough * 0.10f;
                        g -= trough * 0.08f;
                        b -= trough * 0.06f;
                    }

                    // Wet/dark patches (more visible)
                    if (broad < 0.3f)
                    {
                        float wet = (0.3f - broad) * 2.5f;
                        r -= wet * 0.12f;
                        g -= wet * 0.10f;
                        b -= wet * 0.06f;
                    }

                    // Scattered bright shell/mineral flecks
                    float fleck = PerlinTileable(u, v, 90f);
                    if (fleck > 0.82f)
                    {
                        float bright = (fleck - 0.82f) * 5.5f;
                        r += bright * 0.10f;
                        g += bright * 0.10f;
                        b += bright * 0.08f;
                    }

                    pixels[i] = new Color(
                        Mathf.Clamp01(r),
                        Mathf.Clamp01(g),
                        Mathf.Clamp01(b)
                    );
                    heights[i] = rippleVal * 0.8f + fineGrain * 0.2f;
                }
            }

            SandTex = CreateTexture("SandTex", pixels);
            SandNorm = GenerateNormalMap("SandNorm", heights, 1.4f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Utilities
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tileable Perlin noise using wrapping coordinates.
        /// Returns 0-1 range with full contrast (not compressed toward 0.5).
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
            float raw = s1 * 0.5f + s2 * 0.5f;

            // Averaging two samples compresses range toward 0.5 — expand it back
            // Remap from ~[0.25, 0.75] to [0, 1] for full contrast
            return Mathf.Clamp01((raw - 0.25f) * 2.0f);
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

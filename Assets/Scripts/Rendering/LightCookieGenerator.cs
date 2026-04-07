using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Generates procedural light cookie textures for dappled sunlight through tree canopy.
    /// Applied to the main directional light to create natural shadow patterns on the ground.
    /// This is a KEY visual element for the golden-hour forest settlement look.
    /// </summary>
    public static class LightCookieGenerator
    {
        /// <summary>
        /// Create a canopy dapple cookie texture — simulates sunlight filtering through tree leaves.
        /// White = full light, black = shadow from leaves.
        /// </summary>
        public static Texture2D CreateCanopyCookie(int size = 512)
        {
            var tex = new Texture2D(size, size, TextureFormat.R8, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            float[] pixels = new float[size * size];

            // Multi-octave noise for natural-looking canopy gaps
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size;
                    float fy = y / (float)size;

                    // Large branches (slow variation)
                    float n1 = Mathf.PerlinNoise(fx * 4f + 100f, fy * 4f + 100f);
                    // Medium leaf clusters
                    float n2 = Mathf.PerlinNoise(fx * 12f + 200f, fy * 12f + 200f);
                    // Fine leaf detail
                    float n3 = Mathf.PerlinNoise(fx * 30f + 300f, fy * 30f + 300f);

                    // Combine octaves: branches dominate, leaves add detail
                    float combined = n1 * 0.5f + n2 * 0.3f + n3 * 0.2f;

                    // Threshold to create distinct light/shadow patches
                    // Center around 0.5 with soft edges for natural look
                    float cookie = Mathf.SmoothStep(0.35f, 0.65f, combined);

                    // Add some circular clearings (gaps in canopy)
                    float cx = fx - 0.3f;
                    float cy = fy - 0.7f;
                    float clearing1 = 1f - Mathf.Clamp01((cx * cx + cy * cy) * 20f);

                    cx = fx - 0.7f;
                    cy = fy - 0.4f;
                    float clearing2 = 1f - Mathf.Clamp01((cx * cx + cy * cy) * 15f);

                    cookie = Mathf.Max(cookie, clearing1 * 0.8f);
                    cookie = Mathf.Max(cookie, clearing2 * 0.6f);

                    pixels[y * size + x] = cookie;
                }
            }

            // Write pixels
            Color[] colors = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = pixels[i];
                colors[i] = new Color(v, v, v, v);
            }
            tex.SetPixels(colors);
            tex.Apply(true);

            return tex;
        }

        /// <summary>
        /// Apply the canopy cookie to the main directional light.
        /// Creates dappled sunlight effect on the ground — essential for forest settlement look.
        /// </summary>
        public static void ApplyToDirectionalLight(Light sunLight)
        {
            if (sunLight == null || sunLight.type != LightType.Directional) return;

            var cookie = CreateCanopyCookie(512);
            sunLight.cookie = cookie;
            sunLight.cookieSize = 80f; // Size in world units — controls how large the dapple pattern is

            Debug.Log("[LightCookie] Applied canopy cookie to directional light (512x512, 80m pattern size)");
        }
    }
}

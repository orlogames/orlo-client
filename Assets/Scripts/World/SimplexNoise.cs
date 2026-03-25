using System;
using UnityEngine;

namespace Orlo.World
{
    /// <summary>
    /// Full seeded simplex noise implementation. No external packages required.
    /// Noise2D and Noise3D return values in the range [-1, 1].
    /// Based on the algorithm by Stefan Gustavson (2005).
    /// </summary>
    public class SimplexNoise
    {
        // ---------------------------------------------------------------------------
        //  Permutation table (256 entries, doubled to 512 to avoid index wrapping)
        // ---------------------------------------------------------------------------
        private readonly int[] _perm = new int[512];
        private readonly int[] _permMod12 = new int[512];

        // Gradient vectors for 3D simplex noise (12 edges of a cube)
        private static readonly int[][] Grad3 = {
            new[]{1,1,0}, new[]{-1,1,0}, new[]{1,-1,0}, new[]{-1,-1,0},
            new[]{1,0,1}, new[]{-1,0,1}, new[]{1,0,-1}, new[]{-1,0,-1},
            new[]{0,1,1}, new[]{0,-1,1}, new[]{0,1,-1}, new[]{0,-1,-1}
        };

        // ---------------------------------------------------------------------------
        //  Construction
        // ---------------------------------------------------------------------------
        public SimplexNoise(int seed)
        {
            // Build a seeded permutation table
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            // Fisher-Yates shuffle with seeded LCG
            uint s = (uint)seed;
            for (int i = 255; i > 0; i--)
            {
                s = s * 1664525u + 1013904223u;       // LCG
                int j = (int)(s >> 24) % (i + 1);    // 0..i
                int tmp = p[i]; p[i] = p[j]; p[j] = tmp;
            }

            for (int i = 0; i < 512; i++)
            {
                _perm[i]       = p[i & 255];
                _permMod12[i]  = _perm[i] % 12;
            }
        }

        // ---------------------------------------------------------------------------
        //  Public noise functions
        // ---------------------------------------------------------------------------

        /// <summary>Returns simplex noise in [-1, 1] for 2D input.</summary>
        public float Noise2D(float x, float y)
        {
            const float F2 = 0.366025403f;  // (sqrt(3)-1)/2
            const float G2 = 0.211324865f;  // (3-sqrt(3))/6

            // Skew input to determine which simplex cell we are in
            float s  = (x + y) * F2;
            int   i  = FastFloor(x + s);
            int   j  = FastFloor(y + s);

            float t  = (i + j) * G2;
            float X0 = i - t;
            float Y0 = j - t;
            float x0 = x - X0;
            float y0 = y - Y0;

            // Which simplex triangle?
            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; }
            else          { i1 = 0; j1 = 1; }

            float x1 = x0 - i1 + G2;
            float y1 = y0 - j1 + G2;
            float x2 = x0 - 1f + 2f * G2;
            float y2 = y0 - 1f + 2f * G2;

            // Hash corner coords
            int ii = i & 255;
            int jj = j & 255;
            int gi0 = _permMod12[ii       + _perm[jj      ]];
            int gi1 = _permMod12[ii + i1  + _perm[jj + j1 ]];
            int gi2 = _permMod12[ii + 1   + _perm[jj + 1  ]];

            // Contributions from three corners
            float n0 = Corner2D(gi0, x0, y0);
            float n1 = Corner2D(gi1, x1, y1);
            float n2 = Corner2D(gi2, x2, y2);

            // Scale to [-1, 1]
            return 70f * (n0 + n1 + n2);
        }

        /// <summary>Returns simplex noise in [-1, 1] for 3D input.</summary>
        public float Noise3D(float x, float y, float z)
        {
            const float F3 = 1f / 3f;
            const float G3 = 1f / 6f;

            // Skew to determine simplex cell
            float s  = (x + y + z) * F3;
            int   i  = FastFloor(x + s);
            int   j  = FastFloor(y + s);
            int   k  = FastFloor(z + s);

            float t  = (i + j + k) * G3;
            float X0 = i - t;
            float Y0 = j - t;
            float Z0 = k - t;
            float x0 = x - X0;
            float y0 = y - Y0;
            float z0 = z - Z0;

            // Determine simplex traversal order
            int i1, j1, k1, i2, j2, k2;
            if (x0 >= y0)
            {
                if      (y0 >= z0) { i1=1;j1=0;k1=0; i2=1;j2=1;k2=0; }
                else if (x0 >= z0) { i1=1;j1=0;k1=0; i2=1;j2=0;k2=1; }
                else               { i1=0;j1=0;k1=1; i2=1;j2=0;k2=1; }
            }
            else
            {
                if      (y0 < z0)  { i1=0;j1=0;k1=1; i2=0;j2=1;k2=1; }
                else if (x0 < z0)  { i1=0;j1=1;k1=0; i2=0;j2=1;k2=1; }
                else               { i1=0;j1=1;k1=0; i2=1;j2=1;k2=0; }
            }

            float x1 = x0 - i1 + G3;
            float y1 = y0 - j1 + G3;
            float z1 = z0 - k1 + G3;
            float x2 = x0 - i2 + 2f * G3;
            float y2 = y0 - j2 + 2f * G3;
            float z2 = z0 - k2 + 2f * G3;
            float x3 = x0 - 1f + 3f * G3;
            float y3 = y0 - 1f + 3f * G3;
            float z3 = z0 - 1f + 3f * G3;

            int ii = i & 255;
            int jj = j & 255;
            int kk = k & 255;
            int gi0 = _permMod12[ii      + _perm[jj      + _perm[kk     ]]];
            int gi1 = _permMod12[ii + i1 + _perm[jj + j1 + _perm[kk + k1]]];
            int gi2 = _permMod12[ii + i2 + _perm[jj + j2 + _perm[kk + k2]]];
            int gi3 = _permMod12[ii + 1  + _perm[jj + 1  + _perm[kk + 1 ]]];

            float n0 = Corner3D(gi0, x0, y0, z0);
            float n1 = Corner3D(gi1, x1, y1, z1);
            float n2 = Corner3D(gi2, x2, y2, z2);
            float n3 = Corner3D(gi3, x3, y3, z3);

            return 32f * (n0 + n1 + n2 + n3);
        }

        /// <summary>
        /// Fractal Brownian Motion — stacks octaves of simplex noise.
        /// Returns a value roughly in [-1, 1].
        /// </summary>
        public float FBM(float x, float y, int octaves, float lacunarity = 2f, float persistence = 0.5f)
        {
            float value     = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue  = 0f;

            for (int i = 0; i < octaves; i++)
            {
                value     += Noise2D(x * frequency, y * frequency) * amplitude;
                maxValue  += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return value / maxValue;
        }

        /// <summary>
        /// Ridged multifractal noise — good for mountain ridges and veins.
        /// Returns a value in [0, 1].
        /// </summary>
        public float Ridged(float x, float y, int octaves)
        {
            float value     = 0f;
            float amplitude = 0.5f;
            float frequency = 1f;
            float weight    = 1f;

            for (int i = 0; i < octaves; i++)
            {
                float n  = 1f - Mathf.Abs(Noise2D(x * frequency, y * frequency));
                n       *= n;
                n       *= weight;
                weight   = Mathf.Clamp01(n * 2f);

                value    += n * amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }

            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// Domain-warped noise — samples the noise field at a position offset by
        /// another noise lookup, producing swirling, turbulent patterns.
        /// Returns a value roughly in [-1, 1].
        /// </summary>
        public float Warp(float x, float y, int octaves, float strength)
        {
            // First warp pass: offset the position
            float qx = FBM(x,             y,             octaves);
            float qy = FBM(x + 5.2f,      y + 1.3f,     octaves);

            // Second warp pass
            float rx = FBM(x + strength * qx + 1.7f,  y + strength * qy + 9.2f,  octaves);
            float ry = FBM(x + strength * qx + 8.3f,  y + strength * qy + 2.8f,  octaves);

            return FBM(x + strength * rx, y + strength * ry, octaves);
        }

        // ---------------------------------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------------------------------

        private static int FastFloor(float x) => x > 0 ? (int)x : (int)x - 1;

        private static float Dot2D(int[] g, float x, float y) => g[0] * x + g[1] * y;
        private static float Dot3D(int[] g, float x, float y, float z) => g[0] * x + g[1] * y + g[2] * z;

        private float Corner2D(int gi, float x, float y)
        {
            float t = 0.5f - x * x - y * y;
            if (t < 0) return 0f;
            t *= t;
            return t * t * Dot2D(Grad3[gi], x, y);
        }

        private float Corner3D(int gi, float x, float y, float z)
        {
            float t = 0.6f - x * x - y * y - z * z;
            if (t < 0) return 0f;
            t *= t;
            return t * t * Dot3D(Grad3[gi], x, y, z);
        }
    }
}

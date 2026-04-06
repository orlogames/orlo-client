using UnityEngine;
using System.Collections.Generic;

namespace Orlo.Rendering
{
    /// <summary>
    /// Renders a procedural star dome with named game-universe stars, constellations,
    /// and background fill stars. Stars are deterministic from seed and fade with
    /// the day/night cycle.
    ///
    /// === NAMED STAR CATALOG (Orlo Universe) ===
    ///
    /// Each named star corresponds to a visitable star system in the game universe.
    /// Future: players can look at a star and see system info / set nav waypoint.
    ///
    /// Name             | Spectral | Description
    /// -----------------+----------+------------------------------------------------------------
    /// Veridian         | G2 (yellow) | Current system — NOT rendered (player is here)
    /// Ashenmere        | K3 (orange) | Volcanic system, rich in rare earth minerals
    /// Cryo Reach       | B1 (blue-white) | Ice giant system on the galactic rim, Korrath homeworld
    /// Solari Prime     | G0 (yellow) | Solari homeworld, brightest star in the southern sky
    /// Voidreach        | M5 (deep red) | Dying red dwarf near the Convergence boundary
    /// Thyren Gate      | B8 (blue) | Binary system, Thyren ancestral territory
    /// Kaelen Drift     | A2 (white) | Nomad fleet staging point, massive asteroid belt
    /// Duskhollow       | K7 (orange-red) | Dense nebula system, smuggler haven
    /// Pyreth           | O9 (blue-white) | Supergiant star, uninhabitable but rich in exotic matter
    /// Greymarch        | F5 (yellow-white) | Frontier colony, contested by all factions
    /// Cinderfall       | M2 (red) | Precursor ruin system, heavily excavated
    /// Starweave Nexus  | A0 (white) | Ancient jump gate hub, multiple warp lanes converge
    /// Obsidian Reach   | K1 (orange) | Pirate-controlled, rich in convergence ore
    /// Hollowsong       | F8 (yellow-white) | Vael homeworld, dense tropical planet
    /// Tempest Run      | B3 (blue) | Permanent ion storm system, hazardous navigation
    /// Iron Cradle      | G5 (yellow) | Major industrial hub, Vanguard stronghold
    /// Shardlight       | A5 (white) | Crystal planet system, rare crafting materials
    /// Siltvein         | K5 (orange) | Swamp world, biological research outpost
    /// Pale Circuit     | M0 (red-orange) | Automated Precursor facility still operational
    /// Ember Throne     | G8 (yellow-orange) | Crucible faction capital system
    /// </summary>
    public class StarFieldRenderer : MonoBehaviour
    {
        // ----- Configuration -----
        private const float DomeRadius = 5000f;
        private const int NamedStarCount = 19;        // Hardcoded catalog (excluding Veridian)
        private const int FillStarCount = 2000;        // Procedural background stars
        private const int TotalStars = NamedStarCount + FillStarCount;
        private const int ConstellationCount = 10;
        private const int ConstellationStarsPer = 6;   // Average stars per constellation
        private const float TwinkleSpeed = 1.5f;

        // ----- Runtime state -----
        private Mesh starMesh;
        private Material starMaterial;
        private float nightFactor;
        private float gameTime;  // Accumulated for twinkle

        // Per-star data (parallel arrays for perf)
        private Vector3[] starDirections;    // Unit direction on dome
        private float[] starBrightness;      // Base brightness 0-1
        private float[] starTwinklePhase;    // Random phase offset
        private Color[] starColors;          // Spectral color
        private string[] starNames;          // null for unnamed stars
        private bool[] isConstellationStar;  // Brighter rendering

        // Shader property IDs
        private static readonly int PropNightFactor = Shader.PropertyToID("_NightFactor");
        private static readonly int PropTime = Shader.PropertyToID("_Time2");

        // ===== Named Star Definitions =====

        private struct NamedStar
        {
            public string Name;
            public float Azimuth;   // Degrees, 0=North, 90=East
            public float Altitude;  // Degrees above horizon
            public float Brightness;
            public Color Color;

            public NamedStar(string name, float az, float alt, float bright, Color col)
            {
                Name = name; Azimuth = az; Altitude = alt; Brightness = bright; Color = col;
            }
        }

        // Spectral class color helpers
        private static readonly Color SpO = new Color(0.62f, 0.72f, 1.0f);    // O-type blue-white
        private static readonly Color SpB = new Color(0.68f, 0.78f, 1.0f);    // B-type blue
        private static readonly Color SpA = new Color(0.85f, 0.88f, 1.0f);    // A-type white
        private static readonly Color SpF = new Color(1.0f, 0.97f, 0.88f);    // F-type yellow-white
        private static readonly Color SpG = new Color(1.0f, 0.92f, 0.70f);    // G-type yellow
        private static readonly Color SpK = new Color(1.0f, 0.73f, 0.42f);    // K-type orange
        private static readonly Color SpM = new Color(1.0f, 0.50f, 0.30f);    // M-type red

        private static readonly NamedStar[] Catalog = new NamedStar[]
        {
            // Name, Azimuth, Altitude, Brightness, Color
            new NamedStar("Ashenmere",       315f, 35f,  0.95f, SpK),
            new NamedStar("Cryo Reach",       20f, 72f,  0.90f, SpB),
            new NamedStar("Solari Prime",    180f, 28f,  1.00f, SpG),
            new NamedStar("Voidreach",        90f, 12f,  0.45f, SpM),
            new NamedStar("Thyren Gate",     270f, 40f,  0.85f, SpB),
            new NamedStar("Kaelen Drift",     45f, 55f,  0.75f, SpA),
            new NamedStar("Duskhollow",      225f, 22f,  0.60f, SpK),
            new NamedStar("Pyreth",          350f, 60f,  0.80f, SpO),
            new NamedStar("Greymarch",       135f, 48f,  0.70f, SpF),
            new NamedStar("Cinderfall",      110f, 30f,  0.50f, SpM),
            new NamedStar("Starweave Nexus",  70f, 65f,  0.88f, SpA),
            new NamedStar("Obsidian Reach",  200f, 18f,  0.55f, SpK),
            new NamedStar("Hollowsong",      160f, 52f,  0.72f, SpF),
            new NamedStar("Tempest Run",     290f, 58f,  0.78f, SpB),
            new NamedStar("Iron Cradle",     240f, 32f,  0.82f, SpG),
            new NamedStar("Shardlight",        5f, 45f,  0.76f, SpA),
            new NamedStar("Siltvein",        145f, 15f,  0.48f, SpK),
            new NamedStar("Pale Circuit",    330f, 25f,  0.52f, SpM),
            new NamedStar("Ember Throne",    260f, 38f,  0.84f, SpG),
        };

        // ===== Constellation Definitions =====
        // Each constellation is a set of star indices (into our combined array) forming line patterns.
        // We define them as offsets around a center point. Stars within constellations are brighter.

        private struct Constellation
        {
            public string Name;
            public float CenterAz;
            public float CenterAlt;
            public Vector2[] Offsets; // (azimuth offset, altitude offset) for each star
        }

        private static readonly Constellation[] Constellations = new Constellation[]
        {
            new Constellation { Name = "The Forge",     CenterAz = 30f,  CenterAlt = 50f, Offsets = new[] { v(-3,4), v(0,6), v(3,4), v(2,0), v(-2,0), v(0,-3) } },
            new Constellation { Name = "The Wanderer",  CenterAz = 100f, CenterAlt = 55f, Offsets = new[] { v(-4,2), v(-2,5), v(1,6), v(4,3), v(3,-1) } },
            new Constellation { Name = "The Root",      CenterAz = 170f, CenterAlt = 40f, Offsets = new[] { v(0,5), v(-2,2), v(2,2), v(-4,-1), v(4,-1), v(0,-3) } },
            new Constellation { Name = "The Void Eye",  CenterAz = 220f, CenterAlt = 60f, Offsets = new[] { v(-3,3), v(0,5), v(3,3), v(3,-2), v(0,-4), v(-3,-2) } },
            new Constellation { Name = "The Spear",     CenterAz = 280f, CenterAlt = 45f, Offsets = new[] { v(0,-5), v(0,-2), v(0,1), v(0,4), v(-2,6), v(2,6) } },
            new Constellation { Name = "The Crown",     CenterAz = 340f, CenterAlt = 68f, Offsets = new[] { v(-5,0), v(-3,3), v(0,5), v(3,3), v(5,0) } },
            new Constellation { Name = "The Serpent",   CenterAz = 60f,  CenterAlt = 25f, Offsets = new[] { v(-6,1), v(-3,3), v(0,2), v(3,0), v(5,-2), v(7,0) } },
            new Constellation { Name = "The Anvil",     CenterAz = 150f, CenterAlt = 70f, Offsets = new[] { v(-4,0), v(-2,3), v(2,3), v(4,0), v(-4,-2), v(4,-2) } },
            new Constellation { Name = "The Gate",      CenterAz = 310f, CenterAlt = 30f, Offsets = new[] { v(-3,4), v(3,4), v(-3,-2), v(3,-2), v(0,6) } },
            new Constellation { Name = "The Drift",     CenterAz = 200f, CenterAlt = 20f, Offsets = new[] { v(-5,2), v(-2,0), v(1,2), v(4,1), v(6,3) } },
        };

        private static Vector2 v(float x, float y) => new Vector2(x, y);

        // ===== Lifecycle =====

        public void Initialize()
        {
            GenerateStarData();
            BuildMesh();
            CreateMaterial();
        }

        private void LateUpdate()
        {
            if (starMaterial == null) return;

            // Follow camera
            var cam = Camera.main;
            if (cam != null)
                transform.position = cam.transform.position;

            gameTime += Time.deltaTime;
            starMaterial.SetFloat(PropNightFactor, nightFactor);
            starMaterial.SetFloat(PropTime, gameTime);
        }

        /// <summary>
        /// Called by SkyboxController each frame with current night visibility factor (0=day, 1=night).
        /// </summary>
        public void SetNightFactor(float factor)
        {
            nightFactor = factor;
        }

        // ===== Star Generation =====

        private void GenerateStarData()
        {
            int totalCount = NamedStarCount + FillStarCount;
            // Count constellation stars
            int constellationStarTotal = 0;
            for (int i = 0; i < Constellations.Length; i++)
                constellationStarTotal += Constellations[i].Offsets.Length;
            totalCount += constellationStarTotal;

            starDirections = new Vector3[totalCount];
            starBrightness = new float[totalCount];
            starTwinklePhase = new float[totalCount];
            starColors = new Color[totalCount];
            starNames = new string[totalCount];
            isConstellationStar = new bool[totalCount];

            int idx = 0;
            var rng = new System.Random(42); // Deterministic seed

            // 1. Named stars from catalog
            for (int i = 0; i < Catalog.Length; i++)
            {
                ref NamedStar s = ref Catalog[i]; // Note: NamedStar is a struct, use local copy
                var ns = Catalog[i];
                starDirections[idx] = AzAltToDirection(ns.Azimuth, ns.Altitude);
                starBrightness[idx] = ns.Brightness;
                starTwinklePhase[idx] = (float)rng.NextDouble();
                starColors[idx] = ns.Color;
                starNames[idx] = ns.Name;
                isConstellationStar[idx] = false;
                idx++;
            }

            // 2. Constellation stars
            for (int c = 0; c < Constellations.Length; c++)
            {
                var con = Constellations[c];
                for (int s = 0; s < con.Offsets.Length; s++)
                {
                    float az = con.CenterAz + con.Offsets[s].x;
                    float alt = con.CenterAlt + con.Offsets[s].y;
                    alt = Mathf.Clamp(alt, 5f, 85f);
                    starDirections[idx] = AzAltToDirection(az, alt);
                    starBrightness[idx] = 0.65f + (float)rng.NextDouble() * 0.25f; // Brighter than fill
                    starTwinklePhase[idx] = (float)rng.NextDouble();
                    // Constellation stars are blue-white to white
                    float t = (float)rng.NextDouble();
                    starColors[idx] = Color.Lerp(SpA, SpB, t);
                    starNames[idx] = null;
                    isConstellationStar[idx] = true;
                    idx++;
                }
            }

            // 3. Fill stars — procedural from noise
            for (int i = 0; i < FillStarCount; i++)
            {
                // Uniform distribution on hemisphere (above horizon)
                float u = (float)rng.NextDouble();
                float v2 = (float)rng.NextDouble();
                float azimuth = u * 360f;
                // Bias toward higher altitudes (cos distribution for uniform sphere coverage)
                float altitude = Mathf.Asin((float)v2) * Mathf.Rad2Deg;
                altitude = Mathf.Max(3f, altitude); // Keep above horizon haze

                starDirections[idx] = AzAltToDirection(azimuth, altitude);

                // Brightness: power law distribution (many dim, few bright)
                float b = (float)rng.NextDouble();
                starBrightness[idx] = Mathf.Pow(b, 3f) * 0.6f + 0.05f;
                starTwinklePhase[idx] = (float)rng.NextDouble();

                // Color: mostly white, some colored
                float colorRoll = (float)rng.NextDouble();
                if (colorRoll < 0.60f)
                    starColors[idx] = Color.Lerp(SpA, SpF, (float)rng.NextDouble()); // White-ish
                else if (colorRoll < 0.75f)
                    starColors[idx] = Color.Lerp(SpG, SpK, (float)rng.NextDouble()); // Yellow-orange
                else if (colorRoll < 0.88f)
                    starColors[idx] = Color.Lerp(SpB, SpO, (float)rng.NextDouble()); // Blue
                else
                    starColors[idx] = Color.Lerp(SpK, SpM, (float)rng.NextDouble()); // Red

                starNames[idx] = null;
                isConstellationStar[idx] = false;
                idx++;
            }
        }

        private Vector3 AzAltToDirection(float azimuthDeg, float altitudeDeg)
        {
            float az = azimuthDeg * Mathf.Deg2Rad;
            float alt = altitudeDeg * Mathf.Deg2Rad;
            // Unity: Y=up, Z=forward(north), X=right(east)
            return new Vector3(
                Mathf.Sin(az) * Mathf.Cos(alt),
                Mathf.Sin(alt),
                Mathf.Cos(az) * Mathf.Cos(alt)
            ).normalized;
        }

        // ===== Mesh Building =====

        private void BuildMesh()
        {
            int totalCount = starDirections.Length;
            // Each star = 1 quad (4 verts, 6 indices)
            var vertices = new Vector3[totalCount * 4];
            var colors = new Color[totalCount * 4];
            var uvs = new Vector2[totalCount * 4];
            var indices = new int[totalCount * 6];

            for (int i = 0; i < totalCount; i++)
            {
                Vector3 dir = starDirections[i];
                Vector3 pos = dir * DomeRadius;

                // Quad size: named/constellation stars are larger
                float size;
                if (starNames[i] != null)
                    size = 8f + starBrightness[i] * 12f; // Named: 8-20 units
                else if (isConstellationStar[i])
                    size = 5f + starBrightness[i] * 8f;  // Constellation: 5-13
                else
                    size = 2f + starBrightness[i] * 6f;  // Fill: 2-8

                // Billboard axes (tangent to dome)
                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(dir, up)) > 0.99f)
                    up = Vector3.forward;
                Vector3 right = Vector3.Cross(up, dir).normalized * size;
                Vector3 upDir = Vector3.Cross(dir, right).normalized * size;

                int vi = i * 4;
                // Quad corners — inverted winding for inside-out rendering
                vertices[vi + 0] = pos - right - upDir;
                vertices[vi + 1] = pos + right - upDir;
                vertices[vi + 2] = pos + right + upDir;
                vertices[vi + 3] = pos - right + upDir;

                Color c = starColors[i];
                colors[vi + 0] = c;
                colors[vi + 1] = c;
                colors[vi + 2] = c;
                colors[vi + 3] = c;

                // UV: x=twinkle phase, y=brightness
                Vector2 uv = new Vector2(starTwinklePhase[i], starBrightness[i]);
                uvs[vi + 0] = uv;
                uvs[vi + 1] = uv;
                uvs[vi + 2] = uv;
                uvs[vi + 3] = uv;

                int ii = i * 6;
                // Front-facing from inside the sphere (CW winding when viewed from inside)
                indices[ii + 0] = vi + 0;
                indices[ii + 1] = vi + 2;
                indices[ii + 2] = vi + 1;
                indices[ii + 3] = vi + 0;
                indices[ii + 4] = vi + 3;
                indices[ii + 5] = vi + 2;
            }

            starMesh = new Mesh();
            starMesh.name = "StarDome";
            // Use 32-bit indices if needed
            if (vertices.Length > 65535)
                starMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            starMesh.vertices = vertices;
            starMesh.colors = colors;
            starMesh.uv = uvs;
            starMesh.triangles = indices;
            starMesh.bounds = new Bounds(Vector3.zero, Vector3.one * DomeRadius * 2f);

            var mf = gameObject.AddComponent<MeshFilter>();
            mf.mesh = starMesh;

            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private void CreateMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/OrloStars");
            if (shader == null) shader = Shader.Find("Orlo/Stars");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
                Debug.LogWarning("[Orlo] Star shader not found in Resources/Shaders/OrloStars, using fallback");
            }

            starMaterial = new Material(shader);
            starMaterial.renderQueue = 2900; // Before transparent, after skybox

            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = starMaterial;
        }

        // ===== Public API =====

        /// <summary>
        /// Get the named star closest to a world-space direction (for "look at star" interaction).
        /// Returns null if no named star within angleTolerance degrees.
        /// </summary>
        public string GetNamedStarAt(Vector3 worldDirection, float angleTolerance = 2f)
        {
            float bestDot = Mathf.Cos(angleTolerance * Mathf.Deg2Rad);
            string bestName = null;
            Vector3 dir = worldDirection.normalized;

            for (int i = 0; i < starDirections.Length; i++)
            {
                if (starNames[i] == null) continue;
                float dot = Vector3.Dot(dir, starDirections[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestName = starNames[i];
                }
            }
            return bestName;
        }
    }
}

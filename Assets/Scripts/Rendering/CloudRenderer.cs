using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Renders a dynamic cloud layer using layered Perlin noise on a single quad.
    /// Clouds scroll with wind, vary density by weather state, cast shadows,
    /// and glow when backlit by the sun. Follows the player at a configurable altitude.
    /// </summary>
    public class CloudRenderer : MonoBehaviour
    {
        [Header("Cloud Layer")]
        [SerializeField] private float altitude = 800f;
        [SerializeField] private float planeSize = 4000f;

        [Header("Noise")]
        [SerializeField] private int noiseResolution = 512;
        [SerializeField] private float noiseScale1 = 0.0025f;
        [SerializeField] private float noiseScale2 = 0.006f;
        [SerializeField] private float noiseScale3 = 0.015f;
        [SerializeField] private float cloudThreshold = 0.42f;
        [SerializeField] private float edgeSoftness = 0.18f;

        [Header("Animation")]
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.3f);
        [SerializeField] private float windSpeed = 8f;

        [Header("Appearance")]
        [SerializeField] private float cloudBrightness = 1.1f;
        [SerializeField] private float backlitIntensity = 0.6f;
        [SerializeField] private float cloudAlpha = 0.85f;
        [SerializeField] private float shadowStrength = 0.3f;

        // Weather-driven density (0 = clear sky, 1 = full overcast)
        private float cloudDensity = 0.35f;
        private float targetDensity = 0.35f;

        // Sun state
        private Vector3 sunDirection = new Vector3(-0.5f, -0.6f, 0.5f).normalized;
        private Color sunColor = new Color(1f, 0.92f, 0.75f);

        // References
        private Transform playerTransform;
        private Mesh quadMesh;
        private Material cloudMaterial;
        private Texture2D noiseTexture;
        private Vector2 uvOffset;
        private Light sunLight;

        // Shadow projector
        private GameObject shadowProjector;
        private Material shadowMaterial;

        private void Awake()
        {
            GenerateNoiseTexture();
            CreateCloudMaterial();
            CreateQuadMesh();
            CreateShadowPlane();
        }

        private void GenerateNoiseTexture()
        {
            noiseTexture = new Texture2D(noiseResolution, noiseResolution, TextureFormat.R8, false);
            noiseTexture.wrapMode = TextureWrapMode.Repeat;
            noiseTexture.filterMode = FilterMode.Bilinear;

            var pixels = new Color[noiseResolution * noiseResolution];

            // Seed offsets for each octave so they look different
            float ox1 = 0f, oy1 = 0f;
            float ox2 = 137.5f, oy2 = 289.3f;
            float ox3 = 541.7f, oy3 = 823.1f;

            for (int y = 0; y < noiseResolution; y++)
            {
                for (int x = 0; x < noiseResolution; x++)
                {
                    float nx = (float)x / noiseResolution;
                    float ny = (float)y / noiseResolution;

                    // 3 octaves of Perlin noise at different frequencies
                    float n1 = Mathf.PerlinNoise(nx / noiseScale1 + ox1, ny / noiseScale1 + oy1);
                    float n2 = Mathf.PerlinNoise(nx / noiseScale2 + ox2, ny / noiseScale2 + oy2);
                    float n3 = Mathf.PerlinNoise(nx / noiseScale3 + ox3, ny / noiseScale3 + oy3);

                    // Weighted blend: large shapes dominant, fine detail layered on
                    float noise = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f;

                    pixels[y * noiseResolution + x] = new Color(noise, noise, noise, 1f);
                }
            }

            noiseTexture.SetPixels(pixels);
            noiseTexture.Apply();

            Debug.Log($"[CloudRenderer] Generated {noiseResolution}x{noiseResolution} cloud noise texture");
        }

        private void CreateCloudMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/OrloCloud");
            if (shader == null) shader = Shader.Find("Orlo/Clouds");
            if (shader == null)
            {
                Debug.LogWarning("[CloudRenderer] Cloud shader not found — clouds disabled");
                enabled = false;
                return;
            }

            cloudMaterial = new Material(shader);
            cloudMaterial.SetTexture("_NoiseTex", noiseTexture);
            cloudMaterial.SetColor("_CloudColor", Color.white);
            cloudMaterial.SetFloat("_Threshold", cloudThreshold);
            cloudMaterial.SetFloat("_Softness", edgeSoftness);
            cloudMaterial.SetFloat("_Density", cloudDensity);
            cloudMaterial.SetFloat("_Brightness", cloudBrightness);
            cloudMaterial.SetFloat("_BacklitPower", backlitIntensity);
            cloudMaterial.SetFloat("_CloudAlpha", cloudAlpha);
        }

        private void CreateQuadMesh()
        {
            quadMesh = new Mesh();
            quadMesh.name = "CloudQuad";

            float halfSize = planeSize * 0.5f;
            quadMesh.vertices = new Vector3[]
            {
                new Vector3(-halfSize, 0, -halfSize),
                new Vector3( halfSize, 0, -halfSize),
                new Vector3( halfSize, 0,  halfSize),
                new Vector3(-halfSize, 0,  halfSize)
            };
            quadMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
        }

        private void CreateShadowPlane()
        {
            var shader = Resources.Load<Shader>("Shaders/OrloCloudShadow");
            if (shader == null) shader = Shader.Find("Orlo/CloudShadow");
            if (shader == null)
            {
                Debug.LogWarning("[CloudRenderer] Shadow shader not found — cloud shadows disabled");
                return;
            }

            shadowMaterial = new Material(shader);
            shadowMaterial.SetTexture("_NoiseTex", noiseTexture);

            // Create a plane just above the terrain surface to receive cloud shadows
            shadowProjector = new GameObject("CloudShadowPlane");
            shadowProjector.transform.SetParent(transform);

            var mf = shadowProjector.AddComponent<MeshFilter>();
            mf.mesh = quadMesh;

            var mr = shadowProjector.AddComponent<MeshRenderer>();
            mr.material = shadowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void Update()
        {
            // Smooth density transitions
            cloudDensity = Mathf.Lerp(cloudDensity, targetDensity, Time.deltaTime * 0.5f);

            // Scroll UVs with wind
            Vector2 windNorm = windDirection.normalized;
            uvOffset += windNorm * (windSpeed * 0.00001f * Time.deltaTime);

            // Wrap to prevent precision loss over long sessions
            uvOffset.x %= 1f;
            uvOffset.y %= 1f;

            // Track player position
            if (playerTransform != null)
            {
                Vector3 pos = playerTransform.position;
                transform.position = new Vector3(pos.x, pos.y + altitude, pos.z);
            }

            // Find sun light if not cached
            if (sunLight == null)
            {
                sunLight = FindSunLight();
            }

            if (sunLight != null)
            {
                sunDirection = sunLight.transform.forward;
                sunColor = sunLight.color;
            }

            UpdateCloudMaterial();
            UpdateShadowPlane();
            RenderCloud();
        }

        private Light FindSunLight()
        {
            // Find the directional light (the sun)
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                    return light;
            }
            return null;
        }

        private void UpdateCloudMaterial()
        {
            if (cloudMaterial == null) return;

            cloudMaterial.SetVector("_UVOffset", new Vector4(uvOffset.x, uvOffset.y, 0, 0));
            cloudMaterial.SetVector("_SunDir", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0));
            cloudMaterial.SetColor("_SunColor", sunColor);
            cloudMaterial.SetFloat("_Density", cloudDensity);
            cloudMaterial.SetFloat("_Threshold", cloudThreshold);
            cloudMaterial.SetFloat("_Softness", edgeSoftness);
            cloudMaterial.SetFloat("_Brightness", cloudBrightness);
            cloudMaterial.SetFloat("_BacklitPower", backlitIntensity);
            cloudMaterial.SetFloat("_CloudAlpha", cloudAlpha);
            cloudMaterial.SetColor("_CloudColor", Color.white);
        }

        private void UpdateShadowPlane()
        {
            if (shadowProjector == null || shadowMaterial == null) return;

            // Position shadow plane just above the terrain (at player Y + small offset)
            float groundY = playerTransform != null ? playerTransform.position.y + 0.5f : 0.5f;
            float playerX = playerTransform != null ? playerTransform.position.x : 0f;
            float playerZ = playerTransform != null ? playerTransform.position.z : 0f;

            // Offset shadow plane in the sun's light direction (projected from cloud altitude)
            // This makes the shadow match where the sun would cast through the clouds
            Vector3 sunFlat = new Vector3(sunDirection.x, 0f, sunDirection.z).normalized;
            float projectionOffset = altitude * Mathf.Tan(Mathf.Acos(Mathf.Abs(sunDirection.y)));
            projectionOffset = Mathf.Min(projectionOffset, planeSize * 0.3f); // Clamp to reasonable range

            shadowProjector.transform.position = new Vector3(
                playerX + sunFlat.x * projectionOffset,
                groundY,
                playerZ + sunFlat.z * projectionOffset
            );

            shadowMaterial.SetVector("_UVOffset", new Vector4(uvOffset.x, uvOffset.y, 0, 0));
            shadowMaterial.SetFloat("_Density", cloudDensity);
            shadowMaterial.SetFloat("_Threshold", cloudThreshold);
            shadowMaterial.SetFloat("_Softness", edgeSoftness);
            shadowMaterial.SetFloat("_ShadowStrength", shadowStrength * cloudDensity);
        }

        private void RenderCloud()
        {
            if (cloudMaterial == null || quadMesh == null) return;

            // Draw the cloud quad using Graphics.DrawMesh (no GameObject needed)
            Graphics.DrawMesh(quadMesh, transform.localToWorldMatrix, cloudMaterial, 0);
        }

        // --- Public API ---

        /// <summary>
        /// Set the sun direction (forward vector of the directional light).
        /// Called by SkyboxController or GameBootstrap.
        /// </summary>
        public void SetSunDirection(Vector3 dir)
        {
            sunDirection = dir.normalized;
        }

        /// <summary>
        /// Set the sun color for cloud coloring and backlit glow.
        /// </summary>
        public void SetSunColor(Color color)
        {
            sunColor = color;
        }

        /// <summary>
        /// Set cloud density from weather state.
        /// 0 = clear sky, 0.3 = scattered, 0.6 = partly cloudy, 1.0 = overcast.
        /// </summary>
        public void SetCloudDensity(float density)
        {
            targetDensity = Mathf.Clamp01(density);
        }

        /// <summary>
        /// Set wind parameters for cloud scrolling.
        /// </summary>
        public void SetWind(float direction, float speed)
        {
            windDirection = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction));
            windSpeed = speed;
        }

        /// <summary>
        /// Set the player transform for the cloud layer to follow.
        /// </summary>
        public void SetPlayerTransform(Transform t)
        {
            playerTransform = t;
        }

        /// <summary>
        /// Current cloud density (0-1). Used by GodRaysEffect to modulate ray intensity.
        /// </summary>
        public float CloudDensity => cloudDensity;

        /// <summary>
        /// Current cloud coverage estimate (0-1). Used by god rays to decide if rays are visible.
        /// Partly cloudy (0.2-0.7) = best god ray conditions.
        /// </summary>
        public float GodRayFactor
        {
            get
            {
                // Bell curve peaking at ~0.4 density (partly cloudy = best god rays)
                // Clear sky: no clouds to cast rays through
                // Overcast: too thick, sun fully blocked
                float x = cloudDensity;
                return Mathf.Max(0f, 4f * x * (1f - x)); // Peaks at 1.0 when density = 0.5
            }
        }

        private void OnDestroy()
        {
            if (cloudMaterial != null) Destroy(cloudMaterial);
            if (shadowMaterial != null) Destroy(shadowMaterial);
            if (noiseTexture != null) Destroy(noiseTexture);
            if (quadMesh != null) Destroy(quadMesh);
        }
    }
}

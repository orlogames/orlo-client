using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Lightweight post-processing for Built-in Render Pipeline.
    /// Adds bloom (bright area glow) and warm color grading via OnRenderImage.
    /// No external packages required — uses pre-compiled shaders loaded from Resources.
    /// Attach to the main camera (auto-attached by GameBootstrap).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostProcessSetup : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private float bloomThreshold = 0.7f;
        [SerializeField] private float bloomIntensity = 0.45f;
        [SerializeField] private int bloomIterations = 4;

        [Header("Color Grading")]
        [SerializeField] private float warmth = 0.12f;          // Shift toward warm tones
        [SerializeField] private float contrast = 1.08f;
        [SerializeField] private float saturation = 1.1f;
        [SerializeField] private float vignetteIntensity = 0.25f;

        private Material _bloomMat;
        private Material _compositeMat;
        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.allowHDR = true;

            // Load pre-compiled shaders from Resources
            var bloomShader = Resources.Load<Shader>("Shaders/OrloBloom");
            if (bloomShader == null) bloomShader = Shader.Find("Orlo/Bloom");

            var compositeShader = Resources.Load<Shader>("Shaders/OrloComposite");
            if (compositeShader == null) compositeShader = Shader.Find("Orlo/Composite");

            if (bloomShader == null || compositeShader == null)
            {
                Debug.LogWarning("[PostProcess] Failed to load shaders — post-processing disabled");
                enabled = false;
                return;
            }

            _bloomMat = new Material(bloomShader);
            _compositeMat = new Material(compositeShader);
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_bloomMat == null || _compositeMat == null)
            {
                Graphics.Blit(src, dst);
                return;
            }

            // --- Bloom pass ---
            int w = src.width / 2;
            int h = src.height / 2;

            _bloomMat.SetFloat("_Threshold", bloomThreshold);

            // Threshold + first downsample
            var rt0 = RenderTexture.GetTemporary(w, h, 0, src.format);
            Graphics.Blit(src, rt0, _bloomMat, 0);

            // Progressive blur downsamples
            var last = rt0;
            var mips = new RenderTexture[bloomIterations];
            mips[0] = rt0;

            for (int i = 1; i < bloomIterations; i++)
            {
                w = Mathf.Max(1, w / 2);
                h = Mathf.Max(1, h / 2);
                var rt = RenderTexture.GetTemporary(w, h, 0, src.format);
                Graphics.Blit(last, rt, _bloomMat, 1);
                mips[i] = rt;
                last = rt;
            }

            // Progressive upsample (blur back up)
            for (int i = bloomIterations - 2; i >= 0; i--)
            {
                var rt = RenderTexture.GetTemporary(mips[i].width, mips[i].height, 0, src.format);
                Graphics.Blit(last, rt, _bloomMat, 1);
                RenderTexture.ReleaseTemporary(last);
                last = rt;
            }

            // --- Composite pass ---
            _compositeMat.SetTexture("_BloomTex", last);
            _compositeMat.SetFloat("_BloomIntensity", bloomIntensity);
            _compositeMat.SetFloat("_Warmth", warmth);
            _compositeMat.SetFloat("_Contrast", contrast);
            _compositeMat.SetFloat("_Saturation", saturation);
            _compositeMat.SetFloat("_VignetteIntensity", vignetteIntensity);

            Graphics.Blit(src, dst, _compositeMat, 0);

            // Cleanup
            RenderTexture.ReleaseTemporary(last);
            for (int i = 0; i < bloomIterations; i++)
            {
                if (mips[i] != null && mips[i] != last)
                    RenderTexture.ReleaseTemporary(mips[i]);
            }
        }

        private void OnDestroy()
        {
            if (_bloomMat != null) Destroy(_bloomMat);
            if (_compositeMat != null) Destroy(_compositeMat);
        }
    }
}

using UnityEngine;

namespace Orlo.Rendering
{
    /// <summary>
    /// Lightweight post-processing for Built-in Render Pipeline.
    /// Adds bloom (bright area glow) and warm color grading via OnRenderImage.
    /// No external packages required — uses a runtime-compiled shader.
    /// Attach to the main camera (auto-attached by GameBootstrap).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostProcessSetup : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private float bloomThreshold = 0.8f;
        [SerializeField] private float bloomIntensity = 0.35f;
        [SerializeField] private int bloomIterations = 4;

        [Header("Color Grading")]
        [SerializeField] private float warmth = 0.08f;          // Shift toward warm tones
        [SerializeField] private float contrast = 1.05f;
        [SerializeField] private float saturation = 1.1f;
        [SerializeField] private float vignetteIntensity = 0.25f;

        private Material _bloomMat;
        private Material _compositeMat;
        private Camera _cam;

        private static readonly string BloomShaderSource = @"
Shader ""Hidden/Orlo/Bloom""
{
    Properties { _MainTex (""Base"", 2D) = ""white"" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        // Pass 0: Threshold + Downsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragThreshold
            #include ""UnityCG.cginc""

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Threshold;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            half4 fragThreshold(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                float brightness = dot(c.rgb, float3(0.2126, 0.7152, 0.0722));
                float contribution = max(0, brightness - _Threshold);
                return half4(c.rgb * contribution, 1);
            }
            ENDCG
        }

        // Pass 1: Gaussian blur (horizontal + vertical in one pass via box filter)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            #include ""UnityCG.cginc""

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            half4 fragBlur(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                half4 c = tex2D(_MainTex, i.uv) * 0.25;
                c += tex2D(_MainTex, i.uv + float2(texel.x, 0)) * 0.125;
                c += tex2D(_MainTex, i.uv - float2(texel.x, 0)) * 0.125;
                c += tex2D(_MainTex, i.uv + float2(0, texel.y)) * 0.125;
                c += tex2D(_MainTex, i.uv - float2(0, texel.y)) * 0.125;
                c += tex2D(_MainTex, i.uv + texel) * 0.0625;
                c += tex2D(_MainTex, i.uv - texel) * 0.0625;
                c += tex2D(_MainTex, i.uv + float2(texel.x, -texel.y)) * 0.0625;
                c += tex2D(_MainTex, i.uv + float2(-texel.x, texel.y)) * 0.0625;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}";

        private static readonly string CompositeShaderSource = @"
Shader ""Hidden/Orlo/Composite""
{
    Properties { _MainTex (""Base"", 2D) = ""white"" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _MainTex;
            sampler2D _BloomTex;
            float _BloomIntensity;
            float _Warmth;
            float _Contrast;
            float _Saturation;
            float _VignetteIntensity;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            half4 frag(v2f i) : SV_Target
            {
                half4 scene = tex2D(_MainTex, i.uv);
                half4 bloom = tex2D(_BloomTex, i.uv);

                // Additive bloom
                half3 c = scene.rgb + bloom.rgb * _BloomIntensity;

                // Warm color shift (add to red, slightly to green, reduce blue)
                c.r += _Warmth;
                c.g += _Warmth * 0.4;
                c.b -= _Warmth * 0.3;

                // Contrast (around 0.5 midpoint)
                c = (c - 0.5) * _Contrast + 0.5;

                // Saturation
                float luma = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(float3(luma, luma, luma), c, _Saturation);

                // Vignette
                float2 d = i.uv - 0.5;
                float vignette = 1.0 - dot(d, d) * _VignetteIntensity * 2.0;
                c *= vignette;

                return half4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}";

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.allowHDR = true;

            // Create materials from inline shader source
            _bloomMat = CreateMaterial(BloomShaderSource);
            _compositeMat = CreateMaterial(CompositeShaderSource);

            if (_bloomMat == null || _compositeMat == null)
            {
                Debug.LogWarning("[PostProcess] Failed to compile shaders — post-processing disabled");
                enabled = false;
            }
        }

        private Material CreateMaterial(string shaderSource)
        {
            var shader = ShaderUtil_CreateShaderFromSource(shaderSource);
            if (shader == null || !shader.isSupported)
                return null;
            return new Material(shader);
        }

        /// <summary>
        /// Runtime shader compilation via Shader.Find fallback or Material constructor.
        /// Unity does not expose a public API for runtime shader compilation from source,
        /// so we use a workaround: create a temporary Material with the source string.
        /// </summary>
        private Shader ShaderUtil_CreateShaderFromSource(string source)
        {
            // Unity's Material(string) constructor compiles the shader source at runtime
            try
            {
                var mat = new Material(source);
                return mat.shader;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PostProcess] Shader compilation failed: {e.Message}");
                return null;
            }
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

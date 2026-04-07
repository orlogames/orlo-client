#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Strips unused shader variants during build to drastically reduce compile time.
/// URP generates 295K+ variants by default. We only target Windows DX11/DX12 forward+
/// rendering, so we can strip mobile, deferred, and unused feature variants.
///
/// Target: reduce from ~295K to ~20-30K variants.
/// </summary>
public class ShaderVariantStripper : IPreprocessShaders
{
    // Lower = runs earlier. We want to run before other strippers.
    public int callbackOrder => 0;

    // Keywords we NEVER use — strip any variant containing these
    private static readonly HashSet<string> STRIP_KEYWORDS = new()
    {
        // Mobile / GLES — we only target Windows DX11/DX12
        "SHADER_API_MOBILE",
        "_SURFACE_TYPE_TRANSPARENT_GLES2",

        // Deferred rendering — we use Forward+
        "LIGHTMAP_ON",
        "LIGHTMAP_SHADOW_MIXING",
        "SHADOWS_SHADOWMASK",
        "DIRLIGHTMAP_COMBINED",
        "_DEFERRED_FIRST_LIGHT",
        "_DEFERRED_MAIN_LIGHT",
        "_GBUFFER_NORMALS_OCT",
        "_RENDER_PASS_ENABLED",

        // Light layers — not using
        "_LIGHT_LAYERS",

        // Debug / development variants
        "DEBUG_DISPLAY",
        "LOD_FADE_CROSSFADE",

        // Screen space shadows (not using custom SS shadows)
        "_MAIN_LIGHT_SHADOWS_SCREEN",

        // Reflection probe blending (not using probe blending)
        "_REFLECTION_PROBE_BLENDING",
        "_REFLECTION_PROBE_BOX_PROJECTION",

        // Clustered rendering (Forward+ handles this differently)
        "_CLUSTERED_RENDERING",

        // Unused additional light features
        "_ADDITIONAL_LIGHT_SHADOWS",

        // Point light cookies (not using)
        "_LIGHT_COOKIES",

        // Mixed lighting modes we don't use
        "LIGHTPROBE_SH",
        "_MIXED_LIGHTING_SUBTRACTIVE",

        // Editor-only
        "EDITOR_VISUALIZATION",
        "SCENESELECTIONPASS",
        "SCENEPICKINGPASS",
    };

    // Shader names that are entirely unused — strip ALL variants
    private static readonly HashSet<string> STRIP_SHADERS = new()
    {
        // Legacy shaders we replaced
        "Orlo/Bloom",      // Replaced by URP Volume Bloom
        "Orlo/Composite",  // Replaced by URP Volume Color Grading
        "Orlo/GodRays",    // Stubbed, pending ScriptableRendererFeature

        // URP shaders we don't use
        "Universal Render Pipeline/2D/Sprite-Lit-Default",
        "Universal Render Pipeline/2D/Sprite-Unlit-Default",
        "Universal Render Pipeline/2D/Sprite-Custom-Default",
        "Universal Render Pipeline/Nature/SpeedTree7",
        "Universal Render Pipeline/Nature/SpeedTree7 Billboard",
        "Universal Render Pipeline/Nature/SpeedTree8_PBRLit",
        "Universal Render Pipeline/Terrain/Lit",
        "Universal Render Pipeline/Complex Lit",
        "Universal Render Pipeline/Baked Lit",
    };

    // Pass types we don't need
    private static readonly HashSet<PassType> STRIP_PASSES = new()
    {
        PassType.Meta,            // Light baking metadata — not baking
        PassType.MotionVectors,   // Not using motion blur currently
        PassType.ScriptableRenderPipelineDefaultUnlit, // Don't need default unlit pass
    };

    private int _totalStripped = 0;
    private int _totalKept = 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        string shaderName = shader.name;

        // Strip entire unused shaders
        if (STRIP_SHADERS.Contains(shaderName))
        {
            _totalStripped += data.Count;
            data.Clear();
            return;
        }

        // Strip unused pass types
        if (STRIP_PASSES.Contains(snippet.passType))
        {
            _totalStripped += data.Count;
            data.Clear();
            return;
        }

        // Strip individual variants with unused keywords
        for (int i = data.Count - 1; i >= 0; i--)
        {
            var keywords = data[i].shaderKeywordSet;
            bool strip = false;

            foreach (var keyword in keywords.GetShaderKeywords())
            {
                string name = keyword.name;
                if (STRIP_KEYWORDS.Contains(name))
                {
                    strip = true;
                    break;
                }
            }

            if (strip)
            {
                data.RemoveAt(i);
                _totalStripped++;
            }
            else
            {
                _totalKept++;
            }
        }
    }
}
#endif

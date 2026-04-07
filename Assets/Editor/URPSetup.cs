#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

/// <summary>
/// Automatically creates and assigns URP pipeline assets when the project opens.
/// This runs once via [InitializeOnLoad] — idempotent if assets already exist.
/// </summary>
[InitializeOnLoad]
public static class URPSetup
{
    private const string SETTINGS_PATH = "Assets/Settings";
    private const string URP_ASSET_PATH = SETTINGS_PATH + "/URPAsset.asset";
    private const string URP_RENDERER_PATH = SETTINGS_PATH + "/URPRenderer.asset";

    static URPSetup()
    {
        // Delay to avoid editor initialization conflicts
        EditorApplication.delayCall += SetupURP;
    }

    private static void SetupURP()
    {
        // Check if URP is already assigned
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            Debug.Log("[URPSetup] URP already configured, skipping setup.");
            return;
        }

        // Create Settings directory if needed
        if (!AssetDatabase.IsValidFolder(SETTINGS_PATH))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        // Create Universal Renderer Data if it doesn't exist
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(URP_RENDERER_PATH);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, URP_RENDERER_PATH);
            Debug.Log("[URPSetup] Created URP Renderer at " + URP_RENDERER_PATH);
        }

        // Create Universal Render Pipeline Asset if it doesn't exist
        UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(URP_ASSET_PATH);
        if (urpAsset == null)
        {
            // Create URP asset with our renderer
            urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urpAsset, URP_ASSET_PATH);
            Debug.Log("[URPSetup] Created URP Asset at " + URP_ASSET_PATH);
        }

        // Configure URP settings for our game
        ConfigureURPAsset(urpAsset);

        // Add SSAO renderer feature
        try { AddSSAOFeature(rendererData); } catch (System.Exception e)
        { Debug.LogWarning("[URPSetup] Could not add SSAO: " + e.Message); }

        // Assign to Graphics Settings
        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;

        // Save all changes
        EditorUtility.SetDirty(urpAsset);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();

        Debug.Log("[URPSetup] URP pipeline configured successfully!");
        Debug.Log("[URPSetup] Render Pipeline: " + GraphicsSettings.currentRenderPipeline?.name);
    }

    private static void ConfigureURPAsset(UniversalRenderPipelineAsset asset)
    {
        // URP 17 made many properties read-only in code.
        // Use SerializedObject to set them via the editor API.
        var so = new SerializedObject(asset);

        SetBool(so, "m_SupportsHDR", true);
        SetBool(so, "m_MainLightShadowsSupported", true);
        SetInt(so, "m_MainLightShadowmapResolution", 2048);
        SetFloat(so, "m_ShadowDistance", 150f);
        SetInt(so, "m_ShadowCascadeCount", 4);
        SetInt(so, "m_AdditionalLightsRenderingMode", 1); // 1 = PerPixel
        SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
        SetInt(so, "m_MSAA", 4);
        SetFloat(so, "m_RenderScale", 1f);
        SetBool(so, "m_SupportsCameraDepthTexture", true);
        SetBool(so, "m_SupportsCameraOpaqueTexture", true);

        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[URPSetup] Configured URP via SerializedObject: HDR, Shadows, " +
                  "AdditionalLights, MSAA 4x, DepthTex, OpaqueTex");
    }

    private static void SetBool(SerializedObject so, string prop, bool val)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = val;
    }

    private static void SetInt(SerializedObject so, string prop, int val)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.intValue = val;
    }

    private static void SetFloat(SerializedObject so, string prop, float val)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.floatValue = val;
    }

    private static void AddSSAOFeature(UniversalRendererData rendererData)
    {
        // Check if SSAO already exists
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature != null && feature.GetType().Name.Contains("Ambient"))
            {
                Debug.Log("[URPSetup] SSAO already configured.");
                return;
            }
        }

        // Use reflection to create SSAO — class name varies across URP versions
        var ssaoType = System.Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
        if (ssaoType == null)
        {
            Debug.LogWarning("[URPSetup] SSAO class not found in URP — add manually via Renderer asset inspector");
            return;
        }

        var ssaoFeature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
        ssaoFeature.name = "SSAO";

        rendererData.rendererFeatures.Add(ssaoFeature);
        rendererData.SetDirty();
        AssetDatabase.AddObjectToAsset(ssaoFeature, rendererData);
        EditorUtility.SetDirty(rendererData);

        Debug.Log("[URPSetup] Added SSAO Renderer Feature");
    }
}
#endif

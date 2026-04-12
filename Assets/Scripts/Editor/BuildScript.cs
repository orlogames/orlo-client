using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Linq;

public class BuildScript
{
    private static string GetBuildPath()
    {
        // Check command line args for -buildPath
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildPath")
                return args[i + 1];
        }
        return "build/OrloClient";
    }

    private static BuildTarget GetBuildTarget()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildTarget")
            {
                switch (args[i + 1])
                {
                    case "StandaloneLinux64": return BuildTarget.StandaloneLinux64;
                    case "StandaloneWindows64": return BuildTarget.StandaloneWindows64;
                    case "StandaloneOSX": return BuildTarget.StandaloneOSX;
                }
            }
        }
        return BuildTarget.StandaloneWindows64;
    }

    /// <summary>
    /// Ensure URP pipeline asset exists and is assigned to GraphicsSettings.
    /// Without this, batch-mode builds use Built-in pipeline and all URP shaders are null (pink).
    /// </summary>
    private static void EnsureURPPipeline()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            Debug.Log($"[BuildScript] URP already assigned: {GraphicsSettings.currentRenderPipeline.name}");
            return;
        }

        Debug.Log("[BuildScript] No render pipeline assigned — creating URP assets...");

        const string settingsPath = "Assets/Settings";
        const string rendererPath = settingsPath + "/URPRenderer.asset";
        const string assetPath = settingsPath + "/URPAsset.asset";

        if (!AssetDatabase.IsValidFolder(settingsPath))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // Create renderer
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }

        // Create pipeline asset
        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (urpAsset == null)
        {
            urpAsset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(urpAsset, assetPath);
        }

        // Configure via SerializedObject (URP 17 properties are read-only)
        var so = new SerializedObject(urpAsset);
        SetProp(so, "m_SupportsHDR", true);
        SetProp(so, "m_MainLightShadowsSupported", true);
        SetProp(so, "m_MainLightShadowmapResolution", 2048);
        SetProp(so, "m_ShadowDistance", 150f);
        SetProp(so, "m_ShadowCascadeCount", 4);
        SetProp(so, "m_AdditionalLightsRenderingMode", 1);
        SetProp(so, "m_AdditionalLightsPerObjectLimit", 8);
        SetProp(so, "m_MSAA", 4);
        SetProp(so, "m_RenderScale", 1f);
        SetProp(so, "m_SupportsCameraDepthTexture", true);
        SetProp(so, "m_SupportsCameraOpaqueTexture", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Assign to graphics settings — must mark dirty and save explicitly
        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;

        // Force-save the pipeline asset
        EditorUtility.SetDirty(urpAsset);
        EditorUtility.SetDirty(renderer);

        // Explicitly write GraphicsSettings and QualitySettings to disk
        // AssetDatabase.SaveAssets() only saves asset files, not ProjectSettings
        var gfxSettings = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        gfxSettings.Update();
        var srpProp = gfxSettings.FindProperty("m_CustomRenderPipeline");
        if (srpProp != null)
        {
            srpProp.objectReferenceValue = urpAsset;
            gfxSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildScript] URP pipeline created and assigned: {urpAsset.name}");
        Debug.Log($"[BuildScript] GraphicsSettings.currentRenderPipeline = {GraphicsSettings.currentRenderPipeline?.name ?? "null"}");
    }

    private static void SetProp(SerializedObject so, string name, bool val)
    { var p = so.FindProperty(name); if (p != null) p.boolValue = val; }
    private static void SetProp(SerializedObject so, string name, int val)
    { var p = so.FindProperty(name); if (p != null) p.intValue = val; }
    private static void SetProp(SerializedObject so, string name, float val)
    { var p = so.FindProperty(name); if (p != null) p.floatValue = val; }

    [MenuItem("Build/Build Game")]
    public static void Build()
    {
        var buildPath = GetBuildPath();
        var target = GetBuildTarget();

        // Get all enabled scenes
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogWarning("[BuildScript] No scenes in build settings, using all scenes in Assets");
            scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
        }

        if (scenes.Length == 0)
        {
            Debug.LogWarning("[BuildScript] No scene files found, creating Boot scene");
            CreateBootScene.Create();
            scenes = new[] { "Assets/Scenes/Boot.unity" };
        }

        // Ensure correct extension for target platform
        if (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows)
        {
            if (!buildPath.EndsWith(".exe"))
                buildPath += ".exe";
        }

        // Ensure URP pipeline is assigned before building
        EnsureURPPipeline();

        Debug.Log($"[BuildScript] Building {target} to {buildPath} with {scenes.Length} scenes");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = target,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] Build succeeded: {report.summary.totalSize / 1024 / 1024}MB");
        }
        else
        {
            Debug.LogError($"[BuildScript] Build failed: {report.summary.result}");
            EditorApplication.Exit(1);
        }
    }
}

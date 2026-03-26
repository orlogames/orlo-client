using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
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

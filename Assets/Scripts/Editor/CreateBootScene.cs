using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CreateBootScene
{
    [MenuItem("Build/Create Boot Scene")]
    public static void Create()
    {
        // Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Create the bootstrap GameObject
        var bootstrapGO = new GameObject("GameBootstrap");
        bootstrapGO.AddComponent<Orlo.GameBootstrap>();

        // Core systems (asset loading, entity management)
        var systemsGO = new GameObject("CoreSystems");
        systemsGO.AddComponent<Orlo.World.AssetLoader>();
        systemsGO.AddComponent<Orlo.World.EntityManager>();
        systemsGO.AddComponent<Orlo.World.ProceduralEntityFactory>();

        // Save scene
        string scenePath = "Assets/Scenes/Boot.unity";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
            System.IO.Path.Combine(Application.dataPath, "../", scenePath)));
        EditorSceneManager.SaveScene(scene, scenePath);

        // Add to build settings
        var buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log($"[CreateBootScene] Created and saved {scenePath}");
    }
}

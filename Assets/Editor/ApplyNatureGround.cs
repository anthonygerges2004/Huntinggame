using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyNatureGround
{
    [InitializeOnLoadMethod]
    static void AutoCorrectBrokenGround()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += TryAutoCorrect;
        };
    }

    static void TryAutoCorrect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "GetStarted_Scene")
            return;

        if (GameObject.Find(ApplyForestEnvironment.RootName) != null)
            return;

        // Forest importer handles the realistic Nature pack look.
    }

    [MenuItem("Tools/Hunting Game/Apply Nature Ground")]
    public static void ApplyFromMenu()
    {
        ApplyForestEnvironment.ApplyFromMenu();
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GetStarted_Scene.unity");
        GameObject root = NatureGroundBuilder.Build();
        if (root == null)
            return;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }
}

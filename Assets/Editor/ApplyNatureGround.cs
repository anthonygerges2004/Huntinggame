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

        int applied = EditorPrefs.GetInt("HuntingGame.NatureGroundVersion", 0);
        if (applied == NatureGroundBuilder.LayoutVersion &&
            GameObject.Find(NatureGroundBuilder.RootName) != null)
            return;

        GameObject root = NatureGroundBuilder.Build();
        if (root == null)
            return;

        EditorPrefs.SetInt("HuntingGame.NatureGroundVersion", NatureGroundBuilder.LayoutVersion);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("Updated hunting ground to cover the island and keep trees on land.");
    }

    [MenuItem("Tools/Hunting Game/Apply Nature Ground")]
    public static void ApplyFromMenu()
    {
        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
        {
            EditorUtility.DisplayDialog(
                "Nature ground",
                "Open GetStarted_Scene first, then run this again.",
                "OK");
            return;
        }

        GameObject root = NatureGroundBuilder.Build();
        if (root == null)
            return;

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        EditorPrefs.SetInt("HuntingGame.NatureGroundVersion", NatureGroundBuilder.LayoutVersion);
        Debug.Log("Nature ground corrected: stray tiles removed, meadows now cover the island.");
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

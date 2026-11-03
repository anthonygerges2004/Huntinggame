using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RefactorGameMap
{
    [MenuItem("Tools/Hunting Game/Refactor Game Map (Pro Visual Pass)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Map Refactor", "Press Stop first, then run this again.", "OK");
            return;
        }

        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
            EditorSceneManager.OpenScene("Assets/Scenes/GetStarted_Scene.unity");

        ApplyRealisticTerrain.FixAllTerrains();
        GameCulling.ApplyForActiveScene();
        HuntWorldDressing.Dress(rebuildFoliage: true);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Map Refactor",
            "Professional visual pass complete.\n\nApplied terrain fixes, upgraded world dressing with broader asset usage, improved atmospheric lighting, and rebuilt foliage for the imported terrain pack.",
            "OK");
    }
}

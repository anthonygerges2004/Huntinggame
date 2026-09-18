using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyAnimalModels
{
    [MenuItem("Tools/Hunting Game/Apply Animal Models")]
    public static void ApplyFromMenu()
    {
        int count = ApplyToOpenScene();
        EditorUtility.DisplayDialog(
            "Animal models",
            $"Applied wildlife models to {count} animals.\n\n" +
            "Bird = chicken, deer = deer, large = horse, legendary = tiger.",
            "OK");
    }

    public static void ApplyFromCommandLine()
    {
        string scenePath = "Assets/Scenes/GetStarted_Scene.unity";
        EditorSceneManager.OpenScene(scenePath);
        ApplyToOpenScene();
        EditorSceneManager.SaveOpenScenes();
    }

    static int ApplyToOpenScene()
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            Undo.RecordObject(animals[i].transform, "Apply animal models");
            AnimalVisuals.Attach(animals[i]);
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Applied animal models to {animals.Length} animals.");
        return animals.Length;
    }
}

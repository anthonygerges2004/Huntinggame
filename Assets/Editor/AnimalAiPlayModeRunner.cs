using System.IO;
using HuntingGame.Animals;
using UnityEditor;
using UnityEngine;

public static class AnimalAiPlayModeRunner
{
    static double started;

    [MenuItem("Hunting Game/Run Animal AI PlayMode Tests")]
    public static void Run()
    {
        string request = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.RequestName);
        string result = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.ResultName);
        Directory.CreateDirectory(Path.GetDirectoryName(request));
        if (File.Exists(result))
            File.Delete(result);
        File.WriteAllText(request, "run");
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        if (!EditorApplication.isPlaying)
            EditorApplication.EnterPlaymode();
    }

    static void Watch()
    {
        string result = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.ResultName);
        if (File.Exists(result))
        {
            string text = File.ReadAllText(result);
            EditorApplication.update -= Watch;
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            if (text.StartsWith("passed"))
                Debug.Log("Animal AI PlayMode tests passed. " + text);
            else
                Debug.LogError("Animal AI PlayMode tests failed.\n" + text);
            return;
        }

        if (EditorApplication.timeSinceStartup - started > 90d)
        {
            EditorApplication.update -= Watch;
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            Debug.LogError("Animal AI PlayMode tests timed out.");
        }
    }
}

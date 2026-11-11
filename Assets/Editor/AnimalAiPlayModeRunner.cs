using System.IO;
using HuntingGame.Animals;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AnimalAiPlayModeRunner
{
    const string RunningKey = "HuntingGame.AiTests.Running";
    const string StartedKey = "HuntingGame.AiTests.Started";

    static AnimalAiPlayModeRunner()
    {
        if (SessionState.GetBool(RunningKey, false))
            EditorApplication.update += Watch;
    }

    [MenuItem("Hunting Game/Run Animal AI PlayMode Tests")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before starting the animal AI tests.");
            return;
        }
        string request = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.RequestName);
        string result = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.ResultName);
        Directory.CreateDirectory(Path.GetDirectoryName(request));
        if (File.Exists(result))
            File.Delete(result);
        File.WriteAllText(request, "run");
        SessionState.SetBool(RunningKey, true);
        SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
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
            SessionState.SetBool(RunningKey, false);
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            if (text.StartsWith("passed"))
                Debug.Log("Animal AI PlayMode tests passed. " + text);
            else
                Debug.LogError("Animal AI PlayMode tests failed.\n" + text);
            return;
        }

        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f) > 90d)
        {
            EditorApplication.update -= Watch;
            SessionState.SetBool(RunningKey, false);
            string request = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.RequestName);
            if (File.Exists(request))
                File.Delete(request);
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            Debug.LogError("Animal AI PlayMode tests timed out.");
        }
    }
}

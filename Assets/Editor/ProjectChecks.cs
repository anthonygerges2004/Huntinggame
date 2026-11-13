using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ProjectChecks
{
    const string Request = "Temp/project-checks-request.txt";
    static double nextCheck;
    static ProjectChecks() { EditorApplication.update += CheckRequest; }
    static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1d;
        if (!File.Exists(Request) || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        bool editorOnly = File.ReadAllText(Request).Trim() == "editor";
        File.Delete(Request);
        Run();
        if (!editorOnly) AnimalAiPlayModeRunner.Run();
    }

    [MenuItem("Hunting Game/Run All Editor Checks")]
    public static void Run()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before running editor checks.");
            return;
        }
        int passed = 0, failed = 0;
        var report = new StringBuilder();
        foreach (var type in typeof(ProjectChecks).Assembly.GetTypes().Where(t => t.Name.EndsWith("Tests")))
        {
            var methods = type.GetMethods();
            foreach (var test in methods.Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TestAttribute") && m.GetParameters().Length == 0))
            {
                object instance = Activator.CreateInstance(type);
                try
                {
                    foreach (var m in methods.Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "SetUpAttribute"))) m.Invoke(instance, null);
                    test.Invoke(instance, null);
                    passed++;
                    report.AppendLine("PASS " + type.Name + "." + test.Name);
                }
                catch (Exception e) { failed++; report.AppendLine("FAIL " + type.Name + "." + test.Name + ": " + (e.InnerException ?? e)); }
                finally
                {
                    foreach (var m in methods.Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TearDownAttribute")))
                        try { m.Invoke(instance, null); } catch (Exception e) { failed++; report.AppendLine("FAIL teardown " + e); }
                }
            }
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/project-checks-results.txt", "Passed: " + passed + ", failed: " + failed + "\n" + report);
        Debug.Log("Project checks: " + passed + " passed, " + failed + " failed. Details: Temp/project-checks-results.txt");
    }
}

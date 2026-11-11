using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bottom-right Scene view shortcuts sourced directly from Build Settings.</summary>
[InitializeOnLoad]
public static class QuickSceneView
{
    const string Menu = "Hunting Game/Quick Scenes";
    const string Preference = "HuntingGame.QuickSceneView.Visible";
    static bool expanded = true;
    static bool opening;
    static Vector2 scroll;
    static EditorBuildSettingsScene[] scenes;

    static QuickSceneView()
    {
        Refresh();
        SceneView.duringSceneGui += Draw;
        EditorBuildSettings.sceneListChanged += Refresh;
        EditorApplication.projectChanged += Refresh;
        EditorApplication.playModeStateChanged += _ => SceneView.RepaintAll();
    }

    static void Refresh()
    {
        scenes = EditorBuildSettings.scenes;
        SceneView.RepaintAll();
    }

    [MenuItem(Menu)]
    static void Toggle()
    {
        EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true));
        SceneView.RepaintAll();
    }

    [MenuItem(Menu, true)]
    static bool ValidateMenu()
    {
        UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Preference, true));
        return true;
    }

    static void Draw(SceneView view)
    {
        if (!EditorPrefs.GetBool(Preference, true)) return;
        if (scenes == null) scenes = EditorBuildSettings.scenes;
        float width = Mathf.Min(260f, view.position.width - 24f);
        float available = view.position.height - 90f;
        if (width < 130f || available < 30f) return;
        float height = expanded ? Mathf.Min(available, 76f + Mathf.Min(scenes.Length, 7) * 26f) : 28f;
        Rect panel = new Rect(view.position.width - width - 12f, view.position.height - height - 34f, width, height);
        Event current = Event.current;
        bool overPanel = panel.Contains(current.mousePosition);
        if (overPanel) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        Handles.BeginGUI();
        GUILayout.BeginArea(panel, GUIContent.none, EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(expanded, "Quick Scenes", true, EditorStyles.foldoutHeader);
        if (expanded)
        {
            if (scenes.Length == 0)
                GUILayout.Label("Add scenes in Build Settings / Profiles.", EditorStyles.wordWrappedMiniLabel);
            else
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                string active = SceneManager.GetActiveScene().path;
                foreach (var scene in scenes)
                {
                    string path = scene.path;
                    bool exists = !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                    bool selected = exists && path == active;
                    string name = string.IsNullOrEmpty(path) ? "Missing scene" : Path.GetFileNameWithoutExtension(path);
                    string label = (selected ? "\u25cf " : "") + name + (!scene.enabled ? " (excluded)" : "");
                    string tooltip = path + (scene.enabled ? "" : "\nExcluded from builds; still available for editing.");
                    using (new EditorGUI.DisabledScope(!exists || selected || opening || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(23f))) QueueOpen(path);
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.Label(EditorApplication.isPlayingOrWillChangePlaymode ? "Exit Play mode to switch scenes." : "Scenes from Build Settings", EditorStyles.miniLabel);
        }
        GUILayout.EndArea();
        Handles.EndGUI();
        // Do not let clicks/scrolls over the panel select or orbit objects behind it.
        if (overPanel && (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.ScrollWheel))
            current.Use();
    }

    static void QueueOpen(string path)
    {
        opening = true;
        // Open outside the current GUI pass, since changing scenes invalidates editor state.
        EditorApplication.delayCall += () =>
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { opening = false; SceneView.RepaintAll(); }
        };
    }
}
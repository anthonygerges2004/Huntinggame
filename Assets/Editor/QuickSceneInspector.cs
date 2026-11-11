using HuntingGame.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(QuickScene))]
public class QuickSceneInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Open replaces the current scene after a save prompt. Additive keeps the current scenes open. Scene switching is disabled in Play mode.", MessageType.Info);
        var shortcut = (QuickScene)target;
        if (shortcut.scenes == null) return;
        foreach (SceneAsset asset in shortcut.scenes)
        {
            if (asset == null) continue;
            string path = AssetDatabase.GetAssetPath(asset);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(asset.name, EditorStyles.linkLabel)) EditorGUIUtility.PingObject(asset);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || string.IsNullOrEmpty(path)))
                {
                    if (GUILayout.Button("Open", GUILayout.Width(65f)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    }
                    bool loaded = SceneManager.GetSceneByPath(path).isLoaded;
                    using (new EditorGUI.DisabledScope(loaded))
                        if (GUILayout.Button("Additive", GUILayout.Width(70f)))
                            EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
            }
        }
    }

    [MenuItem("Hunting Game/Quick Scene Assets")]
    static void SelectShortcuts()
    {
        var asset = AssetDatabase.LoadAssetAtPath<QuickScene>("Assets/Editor/QuickScenes.asset");
        if (asset == null)
        {
            Debug.LogWarning("Create a shortcut asset with Assets > Create > Hunting Game > Quick Scene Shortcuts.");
            return;
        }
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
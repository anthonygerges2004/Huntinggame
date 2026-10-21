using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BakeOcclusionForFoliage
{
    const int OccluderStatic = 16;
    const int OccludeeStatic = 32;

    [MenuItem("Tools/Hunting Game/Mark Foliage Static And Bake Occlusion")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Occlusion", "Press Stop first, then run this again.", "OK");
            return;
        }

        int marked = 0;
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null)
                continue;

            GameObject go = trees[i].gameObject;
            LODGroup lod = go.GetComponent<LODGroup>();
            if (lod != null)
                go = lod.gameObject;

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags |= (StaticEditorFlags)OccluderStatic;
            flags |= (StaticEditorFlags)OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            marked++;
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null)
                continue;
            GameObjectUtility.SetStaticEditorFlags(
                terrains[i].gameObject,
                GameObjectUtility.GetStaticEditorFlags(terrains[i].gameObject) |
                (StaticEditorFlags)OccluderStatic |
                (StaticEditorFlags)OccludeeStatic);
        }

        GameCulling.ApplyToAllTerrains();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (!StaticOcclusionCulling.isRunning)
            StaticOcclusionCulling.GenerateInBackground();

        EditorUtility.DisplayDialog(
            "Occlusion",
            "Marked " + marked + " trees and terrain tiles as static.\n" +
            "Occlusion bake is running in the background (see progress bar).\n" +
            "Save the scene when the bake finishes.",
            "OK");
    }
}

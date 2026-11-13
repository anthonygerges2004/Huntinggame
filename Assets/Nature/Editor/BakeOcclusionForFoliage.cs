using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BakeOcclusionForFoliage
{
    [MenuItem("Tools/Hunting Game/Mark Foliage Static And Bake Occlusion")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Occlusion", "Press Stop first, then run this again.", "OK");
            return;
        }

        if (StaticOcclusionCulling.isRunning)
        {
            Debug.LogWarning("An occlusion bake is already running.");
            return;
        }

        PrepareLoadedScenes();
        if (!StaticOcclusionCulling.Compute())
            throw new System.InvalidOperationException("Occlusion bake failed. See the Editor log.");

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("Occlusion bake completed and scenes saved. Data size: " + StaticOcclusionCulling.umbraDataSize + " bytes.");
    }

    // Entry point for a noninteractive bake of the actual build scene.
    public static void BakeMainScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GetStarted_Scene.unity", OpenSceneMode.Single);
        Run();
    }

    static void PrepareLoadedScenes()
    {
        int marked = 0;
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null)
                continue;

            // Harvestable trees move, deform and disappear. Their baked silhouettes must
            // never hide objects after chopping. Apply this to renderer children too.
            foreach (Transform child in trees[i].GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                flags &= ~(StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.allowOcclusionWhenDynamic = true;
                    EditorUtility.SetDirty(renderer);
                }
            }
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
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
        }

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            camera.useOcclusionCulling = true;
            EditorUtility.SetDirty(camera);
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Prepared " + marked + " harvestable trees as dynamic occludees and " + terrains.Length + " terrain occluders.");
    }
}

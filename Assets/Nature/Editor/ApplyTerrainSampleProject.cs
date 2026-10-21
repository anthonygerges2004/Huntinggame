using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs Unity Terrain Sample / HDRP sample heightmaps as URP hunt terrains and re-applies lake, river, camp, and culling.
/// Does not switch the project render pipeline to HDRP.
/// </summary>
public static class ApplyTerrainSampleProject
{
    const string SampleRootName = "_TerrainSample";
    const string RuntimeFolder = "Assets/Nature/TerrainSample/Runtime";
    const string PrefsKey = "HuntingGame.TerrainSampleVersion";
    const int LayoutVersion = 1;

    static readonly string[] SearchHints =
    {
        "Terrain Sample",
        "TerrainSample",
        "HDRP Terrain",
        "com.unity.terrain-tools",
        "TerrainDemoScene"
    };

    [MenuItem("Tools/Hunting Game/Apply Terrain Sample Project (Keep Hunt Features)")]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Terrain Sample", "Press Stop first, then run this again.", "OK");
            return;
        }

        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
            EditorSceneManager.OpenScene("Assets/Scenes/GetStarted_Scene.unity");

        List<TerrainData> sources = FindSampleTerrainData();
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Terrain Sample",
                "No Terrain Sample heightmaps were found.\n\n" +
                "1. Finish importing \"Unity HDRP Terrain | Terrain Sample Project\" into Assets.\n" +
                "2. Run this menu again.\n\n" +
                "Your game stays on URP. Only the terrain heightmaps and layers are used.",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Terrain Sample",
                "This will:\n" +
                "• Hide the old _RealisticTerrain tiles\n" +
                "• Create new URP terrains from the Terrain Sample (" + sources.Count + " tile(s))\n" +
                "• Re-carve your lake, river, and second lake at the same places\n" +
                "• Put trees, camp, and player back on the ground\n\n" +
                "Your project stays URP (not HDRP).\n\nContinue?",
                "Apply", "Cancel"))
            return;

        try
        {
            ApplyInternal(sources);
            EditorPrefs.SetInt(PrefsKey, LayoutVersion);
            EditorUtility.DisplayDialog(
                "Terrain Sample",
                "Terrain sample is in place with hunt features re-applied.\n\nPress Play to test fishing, swimming, and the river.",
                "OK");
        }
        catch (System.SystemException ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Terrain Sample", "Failed: " + ex.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static void ApplyInternal(List<TerrainData> sources)
    {
        HideOldTerrains();
        EnsureAssetFolder(RuntimeFolder);

        GameObject root = new GameObject(SampleRootName);
        Undo.RegisterCreatedObjectUndo(root, "Terrain Sample");
        List<Terrain> tiles = new List<Terrain>();

        float x = 0f;
        float z = 0f;
        for (int i = 0; i < sources.Count; i++)
        {
            EditorUtility.DisplayProgressBar("Terrain Sample", "Creating terrain " + (i + 1) + " / " + sources.Count, i / (float)sources.Count);
            TerrainData runtimeData = CloneTerrainData(sources[i], "HuntTerrain_" + (i + 1) + ".asset");
            GameObject go = Terrain.CreateTerrainGameObject(runtimeData);
            go.name = "SampleTerrain" + (i + 1);
            go.transform.SetParent(root.transform, true);
            go.transform.position = new Vector3(x, 0f, z);
            Terrain terrain = go.GetComponent<Terrain>();
            GameCulling.ApplyToTerrain(terrain);
            tiles.Add(terrain);
            x += runtimeData.size.x;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            Terrain left = i > 0 ? tiles[i - 1] : null;
            Terrain right = i < tiles.Count - 1 ? tiles[i + 1] : null;
            tiles[i].SetNeighbors(left, null, right, null);
        }

        Terrain.SetConnectivityDirty();
        MakeFishingLake.BackupAllActiveTerrains();

        EditorUtility.DisplayProgressBar("Terrain Sample", "Re-applying lake and river...", 0.75f);
        if (GameObject.Find("Water") != null)
            MakeFishingLake.RecarveExistingPrimaryLake();
        MakeRiver.RecarveExistingRiverSystem();

        EditorUtility.DisplayProgressBar("Terrain Sample", "Snapping objects to ground...", 0.9f);
        ApplyRealisticTerrain.FixAllTerrains();
        GameCulling.ApplyForActiveScene();
        PutObjectsOnGround.Run();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    static List<TerrainData> FindSampleTerrainData()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainData");
        List<TerrainData> list = new List<TerrainData>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;
            if (path.Contains("Realistic Terrain Collection"))
                continue;
            if (path.Contains("/Nature/TerrainSample/Runtime/"))
                continue;

            bool hint = false;
            for (int h = 0; h < SearchHints.Length; h++)
            {
                if (path.IndexOf(SearchHints[h], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hint = true;
                    break;
                }
            }

            if (!hint)
                continue;

            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data != null)
                list.Add(data);
        }

        if (list.Count == 0)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Realistic Terrain Collection") || path.Contains("/Nature/TerrainSample/Runtime/"))
                    continue;
                TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (data != null && data.size.x >= 500f)
                    list.Add(data);
            }
        }

        list.Sort((a, b) =>
        {
            string pa = AssetDatabase.GetAssetPath(a);
            string pb = AssetDatabase.GetAssetPath(b);
            int c = pa.CompareTo(pb);
            return c != 0 ? c : a.name.CompareTo(b.name);
        });
        return list;
    }

    static TerrainData CloneTerrainData(TerrainData source, string fileName)
    {
        string path = RuntimeFolder + "/" + fileName;
        TerrainData existing = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        TerrainData copy = Object.Instantiate(source);
        copy.name = Path.GetFileNameWithoutExtension(fileName);
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        return copy;
    }

    static void HideOldTerrains()
    {
        GameObject old = GameObject.Find(ApplyRealisticTerrain.RootName);
        if (old == null)
            return;
        Undo.RecordObject(old, "Terrain Sample");
        old.SetActive(false);
    }

    static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

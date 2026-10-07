using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MatchTerrainResolution
{
    const string LowResTerrain1 = "Assets/Realistic Terrain Collection/Terrains/Terrain1/Low Resolution_1.asset";

    [InitializeOnLoadMethod]
    static void AutoFix()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && NeedsFix())
                Run();
        };
    }

    static bool NeedsFix()
    {
        GameObject terrainObject = GameObject.Find("Terrain1");
        Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        if (terrain == null || terrain.terrainData == null)
            return false;

        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t != terrain && t.terrainData != null &&
                t.terrainData.heightmapResolution != terrain.terrainData.heightmapResolution)
                return true;
        }

        return false;
    }

    [MenuItem("Tools/Hunting Game/Match Terrain Resolution")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Terrain", "Press Stop first, then run this again.", "OK");
            return;
        }

        GameObject terrainObject = GameObject.Find("Terrain1");
        Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        if (terrain == null)
        {
            EditorUtility.DisplayDialog("Terrain", "Terrain1 was not found in the scene.", "OK");
            return;
        }

        TerrainData lowRes = AssetDatabase.LoadAssetAtPath<TerrainData>(LowResTerrain1);
        if (lowRes == null)
        {
            EditorUtility.DisplayDialog("Terrain", "Missing file:\n" + LowResTerrain1, "OK");
            return;
        }

        Terrain other = null;
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t != terrain && t.terrainData != null)
            {
                other = t;
                break;
            }
        }

        if (other != null && other.terrainData.heightmapResolution != lowRes.heightmapResolution)
        {
            EditorUtility.DisplayDialog(
                "Terrain",
                "The low resolution file does not match the other terrains (" +
                lowRes.heightmapResolution + " vs " + other.terrainData.heightmapResolution + "). Nothing was changed.",
                "OK");
            return;
        }

        if (terrain.terrainData != lowRes)
        {
            Undo.RecordObject(terrain, "Match Terrain Resolution");
            terrain.terrainData = lowRes;

            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                Undo.RecordObject(collider, "Match Terrain Resolution");
                collider.terrainData = lowRes;
            }

            ApplyRealisticTerrain.FixAllTerrains();
            Terrain.SetConnectivityDirty();
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }

        EditorUtility.DisplayDialog(
            "Terrain",
            "Terrain1 now uses the same resolution as the other terrains (" + lowRes.heightmapResolution +
            ").\n\nNext, everything will be put back on the ground.",
            "OK");

        PutObjectsOnGround.Run();
    }
}

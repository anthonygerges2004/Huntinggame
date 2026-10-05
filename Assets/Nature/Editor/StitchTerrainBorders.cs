using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StitchTerrainBorders
{
    const float BlendWidth = 400f;

    [MenuItem("Tools/Hunting Game/Stitch Terrain Borders")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Terrain", "Press Stop first, then run this again.", "OK");
            return;
        }

        List<Terrain> terrains = new List<Terrain>();
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t.terrainData != null)
                terrains.Add(t);
        }

        List<Terrain[]> pairs = new List<Terrain[]>();
        foreach (Terrain left in terrains)
        {
            foreach (Terrain right in terrains)
            {
                if (left != right && IsRightNeighbor(left, right))
                    pairs.Add(new[] { left, right });
            }
        }

        if (pairs.Count == 0)
        {
            EditorUtility.DisplayDialog("Terrain", "No terrains were found side by side.", "OK");
            return;
        }

        int clamped = 0;
        foreach (Terrain[] pair in pairs)
            clamped += Stitch(pair[0], pair[1]);

        foreach (Terrain t in terrains)
        {
            t.Flush();
            EditorUtility.SetDirty(t.terrainData);
        }

        Terrain.SetConnectivityDirty();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(terrains[0].gameObject.scene);

        string message = "Smoothed " + pairs.Count + " terrain borders.";
        if (clamped > 0)
            message += "\n\nSome points hit a terrain's height limit, so a small step may remain in a few places.";
        EditorUtility.DisplayDialog("Terrain", message, "OK");
    }

    static bool IsRightNeighbor(Terrain left, Terrain right)
    {
        Vector3 pl = left.transform.position;
        Vector3 pr = right.transform.position;
        Vector3 sl = left.terrainData.size;
        Vector3 sr = right.terrainData.size;
        return Mathf.Abs(pl.x + sl.x - pr.x) < 0.5f &&
               Mathf.Abs(pl.z - pr.z) < 0.5f &&
               Mathf.Abs(sl.z - sr.z) < 0.5f;
    }

    static int Stitch(Terrain left, Terrain right)
    {
        float borderX = right.transform.position.x;

        // Both sides must aim for targets taken from the untouched heights.
        float[] leftTargets = EdgeTargets(left, left, right, borderX);
        float[] rightTargets = EdgeTargets(right, left, right, borderX);

        return BlendBand(left, true, leftTargets) + BlendBand(right, false, rightTargets);
    }

    static float[] EdgeTargets(Terrain rowsOf, Terrain left, Terrain right, float borderX)
    {
        TerrainData data = rowsOf.terrainData;
        int res = data.heightmapResolution;
        float z0 = rowsOf.transform.position.z;
        float[] targets = new float[res];

        for (int j = 0; j < res; j++)
        {
            float worldZ = z0 + data.size.z * j / (res - 1);
            targets[j] = (WorldHeight(left, borderX, worldZ) + WorldHeight(right, borderX, worldZ)) * 0.5f;
        }

        return targets;
    }

    static float WorldHeight(Terrain terrain, float worldX, float worldZ)
    {
        TerrainData data = terrain.terrainData;
        Vector3 p = terrain.transform.position;
        float nx = Mathf.Clamp01((worldX - p.x) / data.size.x);
        float nz = Mathf.Clamp01((worldZ - p.z) / data.size.z);
        return p.y + data.GetInterpolatedHeight(nx, nz);
    }

    static int BlendBand(Terrain terrain, bool borderOnRight, float[] targets)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float cell = data.size.x / (res - 1);
        int cols = Mathf.Clamp(Mathf.CeilToInt(BlendWidth / cell) + 1, 2, res);
        int xBase = borderOnRight ? res - cols : 0;
        float baseY = terrain.transform.position.y;
        float heightScale = data.size.y;
        int clamped = 0;

        float[,] heights = data.GetHeights(xBase, 0, cols, res);

        for (int j = 0; j < res; j++)
        {
            for (int c = 0; c < cols; c++)
            {
                int i = xBase + c;
                float distance = borderOnRight ? (res - 1 - i) * cell : i * cell;
                float w = 1f - Mathf.Clamp01(distance / BlendWidth);
                if (w <= 0f)
                    continue;
                w = w * w * (3f - 2f * w);

                float current = baseY + heights[j, c] * heightScale;
                float blended = Mathf.Lerp(current, targets[j], w);
                float normalized = (blended - baseY) / heightScale;
                if (normalized < 0f || normalized > 1f)
                    clamped++;
                heights[j, c] = Mathf.Clamp01(normalized);
            }
        }

        data.SetHeights(xBase, 0, heights);
        return clamped;
    }
}

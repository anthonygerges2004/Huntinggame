using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombineTerrains
{
    const string CombinedName = "Terrain_Combined";
    const string AssetFolder = "Assets/Realistic Terrain Collection/Terrains/Combined";
    const string AssetPath = AssetFolder + "/Combined_Terrain.asset";
    const int HeightResolution = 2049;
    const int AlphaResolution = 2048;
    const float BlendWidth = 400f;

    [MenuItem("Tools/Hunting Game/Combine Terrains Into One")]
    static void Combine()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Terrain", "Press Stop first, then run this again.", "OK");
            return;
        }

        List<Terrain> tiles = FindTiles();
        if (tiles.Count < 2)
        {
            EditorUtility.DisplayDialog("Terrain", "Need at least 2 terrains to combine. Found " + tiles.Count + ".", "OK");
            return;
        }

        string problem = CheckInARow(tiles);
        if (problem != null &&
            !EditorUtility.DisplayDialog(
                "Terrain",
                problem + "\n\nClick OK to snap terrains into one row, then combine.",
                "Snap And Combine", "Cancel"))
            return;

        if (problem == null && !EditorUtility.DisplayDialog(
                "Terrain",
                "Combine " + tiles.Count + " terrains into one?\n\nThe old terrains are only hidden, so " +
                "Tools > Hunting Game > Split Terrain Back undoes this.",
                "Combine", "Cancel"))
            return;

        try
        {
            AlignTilesInRow(tiles);
            string problemAfterAlign = CheckInARow(tiles, afterAlign: true);
            if (problemAfterAlign != null)
            {
                EditorUtility.DisplayDialog("Terrain", problemAfterAlign + "\n\nNothing was changed.", "OK");
                return;
            }

            TerrainData data = BuildData(tiles, out Vector3 origin);
            GameObject combined = Terrain.CreateTerrainGameObject(data);
            combined.name = CombinedName;
            CopySettings(tiles[0], combined, origin);
            Undo.RegisterCreatedObjectUndo(combined, "Combine Terrains");

            foreach (Terrain tile in tiles)
            {
                Undo.RecordObject(tile.gameObject, "Combine Terrains");
                tile.gameObject.SetActive(false);
            }

            Terrain.SetConnectivityDirty();
            ApplyRealisticTerrain.FixAllTerrains();
            SaveScene();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog(
            "Terrain",
            "Done. The " + tiles.Count + " terrains are now one terrain called " + CombinedName +
            ".\n\nNext, everything will be put back on the ground.",
            "OK");

        PutObjectsOnGround.Run();
        SaveScene();
    }

    [MenuItem("Tools/Hunting Game/Split Terrain Back")]
    static void SplitBack()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Terrain", "Press Stop first, then run this again.", "OK");
            return;
        }

        GameObject combined = GameObject.Find(CombinedName);
        GameObject root = GameObject.Find(ApplyRealisticTerrain.RootName);
        if (combined == null || root == null)
        {
            EditorUtility.DisplayDialog("Terrain", "There is no combined terrain in the scene.", "OK");
            return;
        }

        foreach (Terrain tile in root.GetComponentsInChildren<Terrain>(true))
        {
            if (tile.gameObject == combined)
                continue;
            Undo.RecordObject(tile.gameObject, "Split Terrain Back");
            tile.gameObject.SetActive(true);
        }

        Undo.DestroyObjectImmediate(combined);
        Terrain.SetConnectivityDirty();
        SaveScene();
        PutObjectsOnGround.Run();
        SaveScene();
    }

    [MenuItem("Tools/Hunting Game/Align Terrains In Row")]
    public static void AlignFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Terrain", "Press Stop first, then run this again.", "OK");
            return;
        }

        List<Terrain> tiles = FindTiles();
        if (tiles.Count < 2)
        {
            EditorUtility.DisplayDialog("Terrain", "Need at least 2 enabled terrains under " + ApplyRealisticTerrain.RootName + ".", "OK");
            return;
        }

        AlignTilesInRow(tiles);
        SaveScene();
        EditorUtility.DisplayDialog("Terrain", "Moved " + tiles.Count + " terrains so they touch edge to edge.", "OK");
    }

    static List<Terrain> FindTiles()
    {
        List<Terrain> tiles = new List<Terrain>();
        GameObject root = GameObject.Find(ApplyRealisticTerrain.RootName);
        if (root != null)
        {
            foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
            {
                if (t.enabled && t.terrainData != null && t.name != CombinedName && t.gameObject.activeInHierarchy)
                    tiles.Add(t);
            }
        }

        if (tiles.Count == 0)
        {
            foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
            {
                if (t.enabled && t.terrainData != null && t.name != CombinedName)
                    tiles.Add(t);
            }
        }

        tiles.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return tiles;
    }

    static void AlignTilesInRow(List<Terrain> tiles)
    {
        if (tiles.Count == 0)
            return;

        Terrain first = tiles[0];
        float y = first.transform.position.y;
        float z = first.transform.position.z;
        float x = first.transform.position.x;

        Undo.RecordObject(first.transform, "Align Terrains");
        first.transform.position = new Vector3(x, y, z);

        for (int i = 1; i < tiles.Count; i++)
        {
            Terrain prev = tiles[i - 1];
            Terrain tile = tiles[i];
            x = prev.transform.position.x + prev.terrainData.size.x;
            Undo.RecordObject(tile.transform, "Align Terrains");
            tile.transform.position = new Vector3(x, y, z);
        }
    }

    static string CheckInARow(List<Terrain> tiles, bool afterAlign = false)
    {
        if (GameObject.Find(CombinedName) != null)
            return "A combined terrain already exists.";

        for (int i = 1; i < tiles.Count; i++)
        {
            Terrain left = tiles[i - 1];
            Terrain right = tiles[i];
            Vector3 pl = left.transform.position;
            Vector3 pr = right.transform.position;
            float gap = pr.x - (pl.x + left.terrainData.size.x);
            bool touching = Mathf.Abs(gap) < 1f;
            bool sameRow = Mathf.Abs(pl.z - pr.z) < 1f;
            if (!touching || !sameRow)
            {
                string detail = left.name + " and " + right.name + " are not side by side";
                if (!touching)
                    detail += " (gap along X is about " + Mathf.RoundToInt(gap) + " m)";
                if (!sameRow)
                    detail += " (different Z position)";
                return detail + ".";
            }

            if (!afterAlign && Mathf.Abs(left.terrainData.size.z - right.terrainData.size.z) > 1f)
            {
                return left.name + " depth is " + Mathf.RoundToInt(left.terrainData.size.z) + " m but " +
                       right.name + " depth is " + Mathf.RoundToInt(right.terrainData.size.z) +
                       " m.\n\nThe combine tool can still merge them (Snap And Combine).";
            }
        }

        return null;
    }

    class Tile
    {
        public Terrain terrain;
        public float x0;
        public float z0;
        public float width;
        public float depth;
        public float baseY;
        public float heightScale;
        public float[,] heights;
        public int res;
    }

    static TerrainData BuildData(List<Terrain> terrains, out Vector3 origin)
    {
        List<Tile> tiles = new List<Tile>();
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (Terrain t in terrains)
        {
            TerrainData d = t.terrainData;
            Tile tile = new Tile
            {
                terrain = t,
                x0 = t.transform.position.x,
                z0 = t.transform.position.z,
                width = d.size.x,
                depth = d.size.z,
                baseY = t.transform.position.y,
                heightScale = d.size.y,
                res = d.heightmapResolution
            };
            tile.heights = d.GetHeights(0, 0, tile.res, tile.res);
            tiles.Add(tile);
            minY = Mathf.Min(minY, tile.baseY);
            maxY = Mathf.Max(maxY, tile.baseY + tile.heightScale);
        }

        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            minZ = Mathf.Min(minZ, tiles[i].z0);
            maxZ = Mathf.Max(maxZ, tiles[i].z0 + tiles[i].depth);
        }

        origin = new Vector3(tiles[0].x0, minY, minZ);
        float totalX = tiles[tiles.Count - 1].x0 + tiles[tiles.Count - 1].width - origin.x;
        float sizeZ = Mathf.Max(1f, maxZ - minZ);
        float sizeY = Mathf.Max(1f, maxY - minY);

        List<TerrainLayer> layers = new List<TerrainLayer>();
        foreach (Terrain t in terrains)
        {
            foreach (TerrainLayer layer in t.terrainData.terrainLayers)
            {
                if (layer != null && !layers.Contains(layer))
                    layers.Add(layer);
            }
        }

        TerrainData data = new TerrainData();
        data.heightmapResolution = HeightResolution;
        data.alphamapResolution = AlphaResolution;
        data.baseMapResolution = 1024;
        data.size = new Vector3(totalX, sizeY, sizeZ);
        data.terrainLayers = layers.ToArray();

        EditorUtility.DisplayProgressBar("Combine Terrains", "Copying mountains...", 0.1f);
        float[,] heights = new float[HeightResolution, HeightResolution];
        for (int j = 0; j < HeightResolution; j++)
        {
            float worldZ = origin.z + sizeZ * j / (HeightResolution - 1);
            for (int i = 0; i < HeightResolution; i++)
            {
                float worldX = origin.x + totalX * i / (HeightResolution - 1);
                float h = BlendedHeight(tiles, worldX, worldZ);
                heights[j, i] = Mathf.Clamp01((h - minY) / sizeY);
            }
        }
        data.SetHeights(0, 0, heights);

        if (layers.Count > 0)
        {
            for (int k = 0; k < tiles.Count; k++)
            {
                EditorUtility.DisplayProgressBar("Combine Terrains", "Copying ground paint " + (k + 1) + "/" + tiles.Count + "...",
                    0.3f + 0.6f * k / tiles.Count);
                CopyAlphamap(data, tiles[k], layers, origin.x, totalX);
            }
        }

        EditorUtility.DisplayProgressBar("Combine Terrains", "Saving...", 0.95f);
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Realistic Terrain Collection/Terrains", "Combined");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(AssetPath) != null)
            AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(data, AssetPath);
        AssetDatabase.SaveAssets();

        return data;
    }

    static int TileIndex(List<Tile> tiles, float worldX)
    {
        for (int k = 0; k < tiles.Count - 1; k++)
        {
            if (worldX < tiles[k].x0 + tiles[k].width)
                return k;
        }
        return tiles.Count - 1;
    }

    // Near a border both tiles fade to the same edge height, so there is no cliff where they meet.
    static float BlendedHeight(List<Tile> tiles, float worldX, float worldZ)
    {
        int k = TileIndex(tiles, worldX);
        Tile tile = tiles[k];
        float u = (worldX - tile.x0) / tile.width;
        float h = WorldHeight(tile, u, worldZ);

        if (k < tiles.Count - 1)
        {
            float distance = tile.x0 + tile.width - worldX;
            float edge = (WorldHeight(tile, 1f, worldZ) + WorldHeight(tiles[k + 1], 0f, worldZ)) * 0.5f;
            h = Mathf.Lerp(h, edge, Weight(distance));
        }

        if (k > 0)
        {
            float distance = worldX - tile.x0;
            float edge = (WorldHeight(tiles[k - 1], 1f, worldZ) + WorldHeight(tile, 0f, worldZ)) * 0.5f;
            h = Mathf.Lerp(h, edge, Weight(distance));
        }

        return h;
    }

    static float Weight(float distance)
    {
        float w = 1f - Mathf.Clamp01(distance / BlendWidth);
        return w * w * (3f - 2f * w);
    }

    static float WorldHeight(Tile tile, float u, float worldZ)
    {
        float localV = tile.depth > 0.01f ? (worldZ - tile.z0) / tile.depth : 0f;
        float fx = Mathf.Clamp01(u) * (tile.res - 1);
        float fz = Mathf.Clamp01(localV) * (tile.res - 1);
        int x0 = Mathf.Min((int)fx, tile.res - 2);
        int z0 = Mathf.Min((int)fz, tile.res - 2);
        float tx = fx - x0;
        float tz = fz - z0;
        float a = Mathf.Lerp(tile.heights[z0, x0], tile.heights[z0, x0 + 1], tx);
        float b = Mathf.Lerp(tile.heights[z0 + 1, x0], tile.heights[z0 + 1, x0 + 1], tx);
        return tile.baseY + Mathf.Lerp(a, b, tz) * tile.heightScale;
    }

    static void CopyAlphamap(TerrainData data, Tile tile, List<TerrainLayer> layers, float originX, float totalX)
    {
        TerrainData src = tile.terrain.terrainData;
        int srcRes = src.alphamapResolution;
        TerrainLayer[] srcLayers = src.terrainLayers;
        int[] map = new int[srcLayers.Length];
        for (int l = 0; l < srcLayers.Length; l++)
            map[l] = srcLayers[l] != null ? layers.IndexOf(srcLayers[l]) : -1;

        int colStart = Mathf.Clamp(Mathf.CeilToInt((tile.x0 - originX) / totalX * AlphaResolution - 0.5f), 0, AlphaResolution);
        int colEnd = Mathf.Clamp(Mathf.CeilToInt((tile.x0 + tile.width - originX) / totalX * AlphaResolution - 0.5f), 0, AlphaResolution);
        int cols = colEnd - colStart;
        if (cols <= 0)
            return;

        float[,,] block = new float[AlphaResolution, cols, layers.Count];

        if (srcLayers.Length == 0 || src.alphamapLayers == 0)
        {
            for (int j = 0; j < AlphaResolution; j++)
                for (int c = 0; c < cols; c++)
                    block[j, c, 0] = 1f;
            data.SetAlphamaps(colStart, 0, block);
            return;
        }

        float[,,] alpha = src.GetAlphamaps(0, 0, srcRes, srcRes);
        int srcLayerCount = alpha.GetLength(2);

        for (int j = 0; j < AlphaResolution; j++)
        {
            float sz = (j + 0.5f) / AlphaResolution * srcRes - 0.5f;
            int z0 = Mathf.Clamp((int)Mathf.Floor(sz), 0, srcRes - 1);
            int z1 = Mathf.Min(z0 + 1, srcRes - 1);
            float tz = Mathf.Clamp01(sz - z0);

            for (int c = 0; c < cols; c++)
            {
                float worldX = originX + (colStart + c + 0.5f) / AlphaResolution * totalX;
                float sx = (worldX - tile.x0) / tile.width * srcRes - 0.5f;
                int x0 = Mathf.Clamp((int)Mathf.Floor(sx), 0, srcRes - 1);
                int x1 = Mathf.Min(x0 + 1, srcRes - 1);
                float tx = Mathf.Clamp01(sx - x0);

                float sum = 0f;
                for (int l = 0; l < srcLayerCount && l < map.Length; l++)
                {
                    if (map[l] < 0)
                        continue;
                    float a = Mathf.Lerp(alpha[z0, x0, l], alpha[z0, x1, l], tx);
                    float b = Mathf.Lerp(alpha[z1, x0, l], alpha[z1, x1, l], tx);
                    float w = Mathf.Lerp(a, b, tz);
                    block[j, c, map[l]] += w;
                    sum += w;
                }

                if (sum > 0.0001f)
                {
                    for (int l = 0; l < layers.Count; l++)
                        block[j, c, l] /= sum;
                }
                else
                {
                    block[j, c, 0] = 1f;
                }
            }
        }

        data.SetAlphamaps(colStart, 0, block);
    }

    static void CopySettings(Terrain from, GameObject combined, Vector3 origin)
    {
        GameObject root = GameObject.Find(ApplyRealisticTerrain.RootName);
        if (root != null)
            combined.transform.SetParent(root.transform, true);
        combined.transform.position = origin;
        combined.layer = from.gameObject.layer;
        combined.tag = from.gameObject.tag;
        combined.isStatic = from.gameObject.isStatic;

        Terrain terrain = combined.GetComponent<Terrain>();
        terrain.materialTemplate = from.materialTemplate;
        terrain.heightmapPixelError = from.heightmapPixelError;
        terrain.basemapDistance = from.basemapDistance;
        terrain.shadowCastingMode = from.shadowCastingMode;
        terrain.drawInstanced = false;
        GameCulling.ApplyToTerrain(terrain);
        terrain.groupingID = from.groupingID;
    }

    static void SaveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}

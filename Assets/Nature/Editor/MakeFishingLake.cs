using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MakeFishingLake
{
    const string DoneKey = "HuntingGame.FishingLakeMade";
    const string Folder = "Assets/Nature/Lake";
    const string SandTexturePath = Folder + "/SandTexture.png";
    const string SandLayerPath = Folder + "/Sand.terrainlayer";
    const string WaterMatPath = Folder + "/WaterMat.mat";

    const float WaterRadius = 150f;
    const float BowlRadius = 136f;
    const float BeachRadius = 154f;
    const float OuterRadius = 175f;
    const float SandRadius = 164f;
    const float MaxDepth = 5f;
    static readonly Color WaterColor = new Color(0.16f, 0.43f, 0.6f, 0.6f);

    [InitializeOnLoadMethod]
    static void AutoMake()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || scene.name != "GetStarted_Scene")
                    return;

                GameObject water = GameObject.Find("Water");
                if (water == null)
                {
                    if (!EditorPrefs.GetBool(DoneKey, false))
                        Make();
                }
                else if (water.transform.localScale.x < WaterRadius * 2f - 1f)
                {
                    MakeBigger();
                }
            };
        };
    }

    [MenuItem("Tools/Hunting Game/Make Lake Bigger")]
    static void MakeBigger()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Lake", "Press Stop first, then run this again.", "OK");
            return;
        }

        GameObject water = GameObject.Find("Water");
        if (water == null)
        {
            EditorUtility.DisplayDialog("Lake", "There is no Water object. Use Make Fishing Lake first.", "OK");
            return;
        }

        if (ActiveTerrains().Count == 0)
        {
            EditorUtility.DisplayDialog("Lake", "No terrain in the scene.", "OK");
            return;
        }

        Vector3 oldCenter = water.transform.position;
        float oldRadius = water.transform.localScale.x * 0.5f;
        float oldOuter = oldRadius + 20f;
        Vector3 camp = CampPosition();

        Vector3 center;
        if (!RemoveOldLake(oldCenter, oldOuter) || !FindLakeSpot(camp, oldCenter, out center))
            center = KeepAway(oldCenter, oldRadius, camp);

        float waterY = Shape(center, out string backup, out int cleared);
        Undo.RecordObject(water.transform, "Make Lake Bigger");
        PlaceWater(water, center, waterY);
        if (water.GetComponent<FishingSpot>() == null)
            water.AddComponent<FishingSpot>();

        FinishScene(center);
        EditorUtility.DisplayDialog(
            "Lake",
            "The lake is now " + Mathf.RoundToInt(WaterRadius * 2f) + " m wide and up to " + MaxDepth + " m deep.\n" +
            "It is " + Mathf.RoundToInt(Vector3.Distance(Flat(camp), Flat(center))) + " m " + Direction(camp, center) +
            " of your camp, and " + Mathf.RoundToInt(Vector3.Distance(Flat(oldCenter), Flat(center))) +
            " m from where the old lake was.\n\n" +
            (cleared > 0 ? cleared + " trees/bushes that stood in the new water were hidden.\n\n" : "") +
            "Terrain backup: " + backup,
            "OK");
    }

    static float Shape(Vector3 center, out string backup, out int cleared)
    {
        Undo.SetCurrentGroupName("Fishing Lake");
        float waterY = WaterLevel(center);
        backup = "";

        foreach (Terrain terrain in ActiveTerrains())
        {
            if (!Touches(terrain, center, OuterRadius))
                continue;

            TerrainData data = terrain.terrainData;
            string copy = BackupTerrain(data);
            backup += (backup.Length > 0 ? "\n" : "") + copy;
            Undo.RegisterCompleteObjectUndo(data, "Fishing Lake");

            DigBowl(terrain, center, waterY);
            PaintSand(terrain, center);
            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        cleared = ClearFoliage(center);
        MoveAnimalsOut(center);
        return waterY;
    }

    static List<Terrain> ActiveTerrains()
    {
        List<Terrain> terrains = new List<Terrain>();
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t.enabled && t.terrainData != null)
                terrains.Add(t);
        }
        return terrains;
    }

    static bool Touches(Terrain terrain, Vector3 center, float radius)
    {
        Vector3 o = terrain.transform.position;
        Vector3 s = terrain.terrainData.size;
        return center.x + radius > o.x && center.x - radius < o.x + s.x &&
               center.z + radius > o.z && center.z - radius < o.z + s.z;
    }

    // Puts the old lake's ground and paint back from the first backup made before any lake existed.
    static bool RemoveOldLake(Vector3 center, float radius)
    {
        bool restoredAny = false;
        foreach (Terrain terrain in ActiveTerrains())
        {
            if (!Touches(terrain, center, radius))
                continue;
            if (!RestoreArea(terrain, center, radius))
                return false;
            restoredAny = true;
        }

        if (!restoredAny)
            return false;

        GameObject foliage = GameObject.Find("_HuntFoliage");
        if (foliage != null)
        {
            foreach (Transform child in foliage.transform)
            {
                if (child.gameObject.activeSelf)
                    continue;
                if (Vector3.Distance(Flat(child.position), Flat(center)) > radius + 1f)
                    continue;
                Undo.RecordObject(child.gameObject, "Fishing Lake");
                child.gameObject.SetActive(true);
            }
        }

        return true;
    }

    static bool RestoreArea(Terrain terrain, Vector3 center, float radius)
    {
        TerrainData data = terrain.terrainData;
        string assetPath = AssetDatabase.GetAssetPath(data);
        string backup = OldestBackup(Path.GetFileName(assetPath));
        if (backup == null)
            return false;

        EnsureFolder();
        string tempPath = Folder + "/_Restore.asset";
        File.Copy(backup, tempPath, true);
        AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceUpdate);

        try
        {
            TerrainData original = AssetDatabase.LoadAssetAtPath<TerrainData>(tempPath);
            if (original == null || original.heightmapResolution != data.heightmapResolution)
                return false;

            Undo.RegisterCompleteObjectUndo(data, "Fishing Lake");
            Vector3 origin = terrain.transform.position;

            int res = data.heightmapResolution;
            RectInt h = CellRect(center, radius, origin, data.size, res - 1, res);
            if (h.width > 0 && h.height > 0)
                data.SetHeights(h.x, h.y, original.GetHeights(h.x, h.y, h.width, h.height));

            if (original.alphamapResolution == data.alphamapResolution && original.alphamapLayers > 0)
            {
                int ares = data.alphamapResolution;
                RectInt a = CellRect(center, radius, origin, data.size, ares, ares);
                if (a.width > 0 && a.height > 0)
                {
                    float[,,] from = original.GetAlphamaps(a.x, a.y, a.width, a.height);
                    float[,,] to = data.GetAlphamaps(a.x, a.y, a.width, a.height);
                    List<TerrainLayer> layers = new List<TerrainLayer>(data.terrainLayers);
                    TerrainLayer[] fromLayers = original.terrainLayers;

                    for (int j = 0; j < a.height; j++)
                    {
                        for (int i = 0; i < a.width; i++)
                        {
                            for (int l = 0; l < to.GetLength(2); l++)
                                to[j, i, l] = 0f;
                            for (int l = 0; l < fromLayers.Length && l < from.GetLength(2); l++)
                            {
                                int index = layers.IndexOf(fromLayers[l]);
                                if (index >= 0)
                                    to[j, i, index] += from[j, i, l];
                            }
                        }
                    }
                    data.SetAlphamaps(a.x, a.y, to);
                }
            }

            terrain.Flush();
            EditorUtility.SetDirty(data);
            return true;
        }
        finally
        {
            AssetDatabase.DeleteAsset(tempPath);
        }
    }

    static RectInt CellRect(Vector3 center, float radius, Vector3 origin, Vector3 size, float cells, int count)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - origin.x) / size.x * cells), 0, count - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + radius - origin.x) / size.x * cells), 0, count - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - radius - origin.z) / size.z * cells), 0, count - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + radius - origin.z) / size.z * cells), 0, count - 1);
        return new RectInt(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
    }

    static string OldestBackup(string fileName)
    {
        string[] dirs = Directory.GetDirectories(Path.GetTempPath(), "lake_backup_*");
        System.Array.Sort(dirs, System.StringComparer.Ordinal);
        foreach (string dir in dirs)
        {
            string file = Path.Combine(dir, fileName);
            if (File.Exists(file))
                return file;
        }
        return null;
    }

    // Used when there is no backup: grows the lake in place, pushed away from the camp and the world edge.
    static Vector3 KeepAway(Vector3 center, float oldRadius, Vector3 camp)
    {
        Vector3 wanted = KeepInsideWorld(center);
        Vector3 fromCamp = Flat(wanted - camp);
        float needed = OuterRadius + 30f;
        if (fromCamp.magnitude < needed)
            wanted = camp + (fromCamp.sqrMagnitude > 0.01f ? fromCamp.normalized : Vector3.forward) * needed;

        float maxShift = Mathf.Max(0f, BeachRadius - oldRadius - 20f);
        return center + Vector3.ClampMagnitude(Flat(wanted - center), maxShift);
    }

    static Vector3 KeepInsideWorld(Vector3 center)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Terrain t in ActiveTerrains())
        {
            Vector3 o = t.transform.position;
            Vector3 s = t.terrainData.size;
            minX = Mathf.Min(minX, o.x);
            maxX = Mathf.Max(maxX, o.x + s.x);
            minZ = Mathf.Min(minZ, o.z);
            maxZ = Mathf.Max(maxZ, o.z + s.z);
        }

        float margin = OuterRadius + 10f;
        Vector3 wanted = center;
        if (maxX - minX > margin * 2f)
            wanted.x = Mathf.Clamp(center.x, minX + margin, maxX - margin);
        if (maxZ - minZ > margin * 2f)
            wanted.z = Mathf.Clamp(center.z, minZ + margin, maxZ - margin);
        return wanted;
    }

    static float GroundHeight(Vector3 p)
    {
        Terrain t = WorldGround.TerrainAt(p);
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
    }

    static void FinishScene(Vector3 center)
    {
        EditorPrefs.SetBool(DoneKey, true);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
            view.LookAt(center + Vector3.up * 10f, Quaternion.Euler(40f, 0f, 0f), 160f);
    }

    [MenuItem("Tools/Hunting Game/Make Fishing Lake")]
    static void Make()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Lake", "Press Stop first, then run this again.", "OK");
            return;
        }

        if (GameObject.Find("Water") != null)
        {
            EditorUtility.DisplayDialog("Lake", "There is already a Water object in the scene.", "OK");
            return;
        }

        Vector3 camp = CampPosition();
        if (!FindLakeSpot(camp, camp, out Vector3 center))
        {
            EditorUtility.DisplayDialog("Lake", "No terrain was found in the scene.", "OK");
            return;
        }

        float waterY = Shape(center, out string backup, out int cleared);
        CreateWater(center, waterY);
        FinishScene(center);

        float distance = Vector3.Distance(Flat(camp), Flat(center));
        EditorUtility.DisplayDialog(
            "Lake",
            "A fishing lake was made " + Mathf.RoundToInt(distance) + " m " + Direction(camp, center) +
            " of your camp, with a sand beach. It shows on the minimap when you get close.\n\n" +
            (cleared > 0 ? cleared + " trees/bushes that stood in the lake were hidden.\n\n" : "") +
            "In Play mode: walk to the shore, look at the water and press G.\n\n" +
            "Terrain backup: " + backup,
            "OK");
    }

    static Vector3 CampPosition()
    {
        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        if (tent != null)
            return tent.transform.position;
        GameObject player = GameObject.Find("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    // Searches every terrain; each 250 m from "near" counts like 1 m of bumpiness. The camp always stays dry.
    static bool FindLakeSpot(Vector3 camp, Vector3 near, out Vector3 best)
    {
        best = Vector3.zero;
        Terrain bestTerrain = null;
        float bestScore = float.MaxValue;
        const float step = 40f;

        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (!terrain.enabled || terrain.terrainData == null)
                continue;

            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            for (float x = o.x + step * 0.5f; x < o.x + s.x; x += step)
            {
                for (float z = o.z + step * 0.5f; z < o.z + s.z; z += step)
                {
                    Vector3 candidate = new Vector3(x, 0f, z);
                    if (!FarFromEdges(terrain, candidate))
                        continue;

                    if (Vector3.Distance(Flat(candidate), Flat(camp)) < OuterRadius + 40f)
                        continue;

                    float score = HeightRange(candidate) + Vector3.Distance(Flat(candidate), Flat(near)) / 250f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        bestTerrain = terrain;
                    }
                }
            }
        }

        if (bestTerrain != null)
            best.y = bestTerrain.SampleHeight(best) + bestTerrain.transform.position.y;
        return bestTerrain != null;
    }

    static string Direction(Vector3 from, Vector3 to)
    {
        Vector3 d = Flat(to - from);
        string ns = d.z > Mathf.Abs(d.x) * 0.4f ? "north" : d.z < -Mathf.Abs(d.x) * 0.4f ? "south" : "";
        string ew = d.x > Mathf.Abs(d.z) * 0.4f ? "east" : d.x < -Mathf.Abs(d.z) * 0.4f ? "west" : "";
        return ns.Length > 0 && ew.Length > 0 ? ns + "-" + ew : ns + ew;
    }

    // Keeps the lake out of the border band the terrain combine tool smooths.
    static bool FarFromEdges(Terrain terrain, Vector3 p)
    {
        Vector3 o = terrain.transform.position;
        Vector3 s = terrain.terrainData.size;
        return p.x > o.x + 450f && p.x < o.x + s.x - 450f &&
               p.z > o.z + OuterRadius + 40f && p.z < o.z + s.z - OuterRadius - 40f;
    }

    static float HeightRange(Vector3 center)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        float r = OuterRadius;
        foreach (Vector3 p in RingPoints(center, new[] { 0f, r * 0.25f, r * 0.5f, r * 0.75f, r }))
        {
            float h = GroundHeight(p);
            min = Mathf.Min(min, h);
            max = Mathf.Max(max, h);
        }
        return max - min;
    }

    static IEnumerable<Vector3> RingPoints(Vector3 center, float[] radii)
    {
        foreach (float r in radii)
        {
            int count = r <= 0f ? 1 : 24;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                yield return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
            }
        }
    }

    // Low parts of the rim are raised into a bank by DigBowl, so a low percentile is enough.
    static float WaterLevel(Vector3 center)
    {
        List<float> rim = new List<float>();
        foreach (Vector3 p in RingPoints(center, new[] { BeachRadius + 3f, BeachRadius + 8f, OuterRadius }))
            rim.Add(GroundHeight(p));
        rim.Sort();
        return rim[Mathf.FloorToInt(rim.Count * 0.3f)] - 0.4f;
    }

    static void DigBowl(Terrain terrain, Vector3 center, float waterY)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        int res = data.heightmapResolution;
        float cellX = data.size.x / (res - 1);
        float cellZ = data.size.z / (res - 1);

        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - OuterRadius - origin.x) / cellX), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + OuterRadius - origin.x) / cellX), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - OuterRadius - origin.z) / cellZ), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + OuterRadius - origin.z) / cellZ), 0, res - 1);
        int w = x1 - x0 + 1;
        int h = z1 - z0 + 1;

        float[,] heights = data.GetHeights(x0, z0, w, h);
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                float worldX = origin.x + (x0 + i) * cellX;
                float worldZ = origin.z + (z0 + j) * cellZ;
                float d = new Vector2(worldX - center.x, worldZ - center.z).magnitude;
                if (d > OuterRadius)
                    continue;

                float original = origin.y + heights[j, i] * data.size.y;
                float shaped;
                if (d <= BowlRadius)
                {
                    shaped = Mathf.Min(original, Profile(d, waterY));
                }
                else if (d <= BeachRadius)
                {
                    shaped = Profile(d, waterY);
                }
                else
                {
                    float s = Smooth((d - BeachRadius) / (OuterRadius - BeachRadius));
                    shaped = Mathf.Lerp(waterY + 0.3f, original, s);
                }

                heights[j, i] = Mathf.Clamp01((shaped - origin.y) / data.size.y);
            }
        }

        data.SetHeights(x0, z0, heights);
    }

    static float Profile(float d, float waterY)
    {
        if (d <= BowlRadius)
        {
            float t = d / BowlRadius;
            return waterY - 0.6f - (MaxDepth - 0.6f) * (1f - t * t);
        }

        float s = Smooth((d - BowlRadius) / (BeachRadius - BowlRadius));
        return Mathf.Lerp(waterY - 0.6f, waterY + 0.3f, s);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static void PaintSand(Terrain terrain, Vector3 center)
    {
        TerrainLayer sand = SandLayer();
        TerrainData data = terrain.terrainData;
        int res = data.alphamapResolution;

        List<TerrainLayer> layers = new List<TerrainLayer>(data.terrainLayers);
        int sandIndex = layers.IndexOf(sand);
        if (sandIndex < 0)
        {
            float[,,] before = data.alphamapLayers > 0 ? data.GetAlphamaps(0, 0, res, res) : null;
            int oldCount = before != null ? before.GetLength(2) : 0;

            layers.Add(sand);
            data.terrainLayers = layers.ToArray();
            sandIndex = layers.Count - 1;

            float[,,] after = new float[res, res, layers.Count];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    if (oldCount == 0)
                    {
                        after[z, x, sandIndex] = 1f;
                        continue;
                    }
                    for (int l = 0; l < oldCount; l++)
                        after[z, x, l] = before[z, x, l];
                }
            }
            data.SetAlphamaps(0, 0, after);
        }

        Vector3 origin = terrain.transform.position;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - SandRadius - origin.x) / data.size.x * res), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + SandRadius - origin.x) / data.size.x * res), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - SandRadius - origin.z) / data.size.z * res), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + SandRadius - origin.z) / data.size.z * res), 0, res - 1);
        int w = x1 - x0 + 1;
        int h = z1 - z0 + 1;

        float[,,] alpha = data.GetAlphamaps(x0, z0, w, h);
        int count = alpha.GetLength(2);
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                float worldX = origin.x + (x0 + i + 0.5f) / res * data.size.x;
                float worldZ = origin.z + (z0 + j + 0.5f) / res * data.size.z;
                float d = new Vector2(worldX - center.x, worldZ - center.z).magnitude;
                float sandAmount = 1f - Smooth((d - BeachRadius) / (SandRadius - BeachRadius));
                if (sandAmount <= 0f)
                    continue;

                for (int l = 0; l < count; l++)
                    alpha[j, i, l] *= 1f - sandAmount;
                alpha[j, i, sandIndex] += sandAmount;
            }
        }

        data.SetAlphamaps(x0, z0, alpha);
    }

    static TerrainLayer SandLayer()
    {
        EnsureFolder();
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SandLayerPath);
        if (layer != null)
            return layer;

        if (!File.Exists(SandTexturePath))
        {
            Texture2D tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
            System.Random random = new System.Random(7);
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.08f +
                              (float)random.NextDouble() * 0.07f;
                    tex.SetPixel(x, y, new Color(0.72f + n, 0.64f + n, 0.47f + n * 0.8f));
                }
            }
            File.WriteAllBytes(SandTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SandTexturePath);
        }

        layer = new TerrainLayer
        {
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SandTexturePath),
            tileSize = new Vector2(4f, 4f)
        };
        AssetDatabase.CreateAsset(layer, SandLayerPath);
        return layer;
    }

    static Material WaterMaterial()
    {
        EnsureFolder();
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
        if (mat != null)
        {
            mat.SetColor("_BaseColor", WaterColor);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", WaterColor);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, WaterMatPath);
        return mat;
    }

    static void CreateWater(Vector3 center, float waterY)
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.name = "Water";
        Object.DestroyImmediate(water.GetComponent<Collider>());
        PlaceWater(water, center, waterY);

        MeshRenderer renderer = water.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        water.AddComponent<FishingSpot>();
        Undo.RegisterCreatedObjectUndo(water, "Make Fishing Lake");
    }

    static void PlaceWater(GameObject water, Vector3 center, float waterY)
    {
        water.transform.position = new Vector3(center.x, waterY - 0.01f, center.z);
        water.transform.localScale = new Vector3(WaterRadius * 2f, 0.01f, WaterRadius * 2f);
        water.GetComponent<MeshRenderer>().sharedMaterial = WaterMaterial();
    }

    static int ClearFoliage(Vector3 center)
    {
        HashSet<GameObject> targets = new HashSet<GameObject>();
        GameObject foliage = GameObject.Find("_HuntFoliage");
        if (foliage != null)
        {
            foreach (Transform child in foliage.transform)
                targets.Add(child.gameObject);
        }
        foreach (HarvestableTree tree in Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude))
            targets.Add(tree.gameObject);

        int cleared = 0;
        foreach (GameObject go in targets)
        {
            if (!go.activeSelf || go.GetComponent<Animal>() != null)
                continue;
            if (Vector3.Distance(Flat(go.transform.position), Flat(center)) > OuterRadius + 1f)
                continue;

            Undo.RecordObject(go, "Make Fishing Lake");
            go.SetActive(false);
            cleared++;
        }
        return cleared;
    }

    static void MoveAnimalsOut(Vector3 center)
    {
        foreach (Animal animal in Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude))
        {
            Vector3 offset = Flat(animal.transform.position - center);
            if (offset.magnitude > OuterRadius + 4f)
                continue;

            Vector3 dir = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.forward;
            Undo.RecordObject(animal.transform, "Make Fishing Lake");
            animal.transform.position = WorldGround.Snap(center + dir * (OuterRadius + 6f));
        }
    }

    static string BackupTerrain(TerrainData data)
    {
        string path = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(path))
            return "(none)";

        string dir = Path.Combine(Path.GetTempPath(), "lake_backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Path.GetFileName(path));
        File.Copy(path, target, true);
        return target;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Nature", "Lake");
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}

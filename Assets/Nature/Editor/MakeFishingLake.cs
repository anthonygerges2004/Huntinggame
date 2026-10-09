using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LakeSize
{
    public readonly float Water;
    public readonly float Bowl;
    public readonly float Beach;
    public readonly float Outer;
    public readonly float Sand;
    public readonly float Depth;

    public LakeSize(float water, float bowl, float beach, float outer, float sand, float depth)
    {
        Water = water;
        Bowl = bowl;
        Beach = beach;
        Outer = outer;
        Sand = sand;
        Depth = depth;
    }
}

public static class MakeFishingLake
{
    const string DoneKey = "HuntingGame.FishingLakeMade";
    internal const string Folder = "Assets/Nature/Lake";
    const string SandTexturePath = Folder + "/SandTexture.png";
    const string SandLayerPath = Folder + "/Sand.terrainlayer";
    const string WaterMatPath = Folder + "/WaterMat.mat";

    internal static readonly LakeSize Big = new LakeSize(150f, 136f, 154f, 175f, 164f, 5f);
    internal static readonly LakeSize Medium = new LakeSize(90f, 80f, 93f, 110f, 101f, 4f);
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
                    return;
                }

                if (water.transform.localScale.x < Big.Water * 2f - 1f)
                    MakeBigger();

                MakeRiver.AutoRun();
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
        if (!RemoveOldLake(oldCenter, oldOuter) || !FindLakeSpot(camp, oldCenter, Big, out center))
            center = KeepAway(oldCenter, oldRadius, camp, Big);

        float waterY = Shape(center, Big, out string backup, out int cleared);
        Undo.RecordObject(water.transform, "Make Lake Bigger");
        PlaceWater(water, center, waterY, Big);
        EnsureLakeComponents(water);

        FinishScene(center, Big);
        EditorUtility.DisplayDialog(
            "Lake",
            "The lake is now " + Mathf.RoundToInt(Big.Water * 2f) + " m wide and up to " + Big.Depth + " m deep.\n" +
            "It is " + Mathf.RoundToInt(Vector3.Distance(Flat(camp), Flat(center))) + " m " + Direction(camp, center) +
            " of your camp, and " + Mathf.RoundToInt(Vector3.Distance(Flat(oldCenter), Flat(center))) +
            " m from where the old lake was.\n\n" +
            (cleared > 0 ? cleared + " trees/bushes that stood in the new water were hidden.\n\n" : "") +
            "Terrain backup: " + backup,
            "OK");
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
        if (!FindLakeSpot(camp, camp, Big, out Vector3 center))
        {
            EditorUtility.DisplayDialog("Lake", "No terrain was found in the scene.", "OK");
            return;
        }

        float waterY = Shape(center, Big, out string backup, out int cleared);
        CreateWater("Water", center, waterY, Big);
        FinishScene(center, Big);

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

    internal static float Shape(Vector3 center, LakeSize size, out string backup, out int cleared)
    {
        Undo.SetCurrentGroupName("Fishing Lake");
        float waterY = WaterLevel(center, size);
        backup = "";

        foreach (Terrain terrain in ActiveTerrains())
        {
            if (!Touches(terrain, center, size.Outer))
                continue;

            TerrainData data = terrain.terrainData;
            string copy = BackupTerrain(data);
            backup += (backup.Length > 0 ? "\n" : "") + copy;
            Undo.RegisterCompleteObjectUndo(data, "Fishing Lake");

            DigBowl(terrain, center, waterY, size);
            Rect area = new Rect(center.x - size.Sand, center.z - size.Sand, size.Sand * 2f, size.Sand * 2f);
            PaintSand(terrain, area, p => LakeSand(Vector3.Distance(Flat(p), Flat(center)), size));
            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        cleared = HideFoliage(p => Vector3.Distance(Flat(p), Flat(center)) <= size.Outer + 1f);
        MoveAnimalsOut(center, size);
        return waterY;
    }

    internal static List<Terrain> ActiveTerrains()
    {
        List<Terrain> terrains = new List<Terrain>();
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t.enabled && t.terrainData != null)
                terrains.Add(t);
        }
        return terrains;
    }

    internal static bool Touches(Terrain terrain, Vector3 center, float radius)
    {
        return Touches(terrain, new Rect(center.x - radius, center.z - radius, radius * 2f, radius * 2f));
    }

    internal static bool Touches(Terrain terrain, Rect area)
    {
        Vector3 o = terrain.transform.position;
        Vector3 s = terrain.terrainData.size;
        return area.xMax > o.x && area.xMin < o.x + s.x &&
               area.yMax > o.z && area.yMin < o.z + s.z;
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
        Rect area = new Rect(center.x - radius, center.z - radius, radius * 2f, radius * 2f);
        return RestoreMasked(terrain, area, p => true, null, null);
    }

    // Inside the area, every point where mask is true gets its height and paint from the first backup.
    // reshape(point, originalHeight) and sand(point) can then rebuild things that should stay, like another lake.
    internal static bool RestoreMasked(Terrain terrain, Rect area, System.Func<Vector3, bool> mask,
        System.Func<Vector3, float, float> reshape, System.Func<Vector3, float> sand)
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
            float cellX = data.size.x / (res - 1);
            float cellZ = data.size.z / (res - 1);
            RectInt h = CellRect(area, origin, data.size, res - 1, res);
            if (h.width > 0 && h.height > 0)
            {
                float[,] from = original.GetHeights(h.x, h.y, h.width, h.height);
                float[,] to = data.GetHeights(h.x, h.y, h.width, h.height);
                for (int j = 0; j < h.height; j++)
                {
                    for (int i = 0; i < h.width; i++)
                    {
                        Vector3 world = new Vector3(origin.x + (h.x + i) * cellX, 0f, origin.z + (h.y + j) * cellZ);
                        if (!mask(world))
                            continue;

                        float height = origin.y + from[j, i] * original.size.y;
                        if (reshape != null)
                            height = reshape(world, height);
                        to[j, i] = Mathf.Clamp01((height - origin.y) / data.size.y);
                    }
                }
                data.SetHeights(h.x, h.y, to);
            }

            if (original.alphamapResolution == data.alphamapResolution && original.alphamapLayers > 0)
            {
                int ares = data.alphamapResolution;
                RectInt a = CellRect(area, origin, data.size, ares, ares);
                if (a.width > 0 && a.height > 0)
                {
                    float[,,] from = original.GetAlphamaps(a.x, a.y, a.width, a.height);
                    float[,,] to = data.GetAlphamaps(a.x, a.y, a.width, a.height);
                    List<TerrainLayer> layers = new List<TerrainLayer>(data.terrainLayers);
                    TerrainLayer[] fromLayers = original.terrainLayers;
                    int sandIndex = layers.IndexOf(AssetDatabase.LoadAssetAtPath<TerrainLayer>(SandLayerPath));

                    for (int j = 0; j < a.height; j++)
                    {
                        for (int i = 0; i < a.width; i++)
                        {
                            Vector3 world = new Vector3(
                                origin.x + (a.x + i + 0.5f) / ares * data.size.x, 0f,
                                origin.z + (a.y + j + 0.5f) / ares * data.size.z);
                            if (!mask(world))
                                continue;

                            for (int l = 0; l < to.GetLength(2); l++)
                                to[j, i, l] = 0f;
                            for (int l = 0; l < fromLayers.Length && l < from.GetLength(2); l++)
                            {
                                int index = layers.IndexOf(fromLayers[l]);
                                if (index >= 0)
                                    to[j, i, index] += from[j, i, l];
                            }

                            float amount = sand != null && sandIndex >= 0 ? Mathf.Clamp01(sand(world)) : 0f;
                            if (amount <= 0f)
                                continue;
                            for (int l = 0; l < to.GetLength(2); l++)
                                to[j, i, l] *= 1f - amount;
                            to[j, i, sandIndex] += amount;
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

    internal static RectInt CellRect(Rect area, Vector3 origin, Vector3 size, float cells, int count)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt((area.xMin - origin.x) / size.x * cells), 0, count - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((area.xMax - origin.x) / size.x * cells), 0, count - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((area.yMin - origin.z) / size.z * cells), 0, count - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((area.yMax - origin.z) / size.z * cells), 0, count - 1);
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
    static Vector3 KeepAway(Vector3 center, float oldRadius, Vector3 camp, LakeSize size)
    {
        Vector3 wanted = KeepInsideWorld(center, size);
        Vector3 fromCamp = Flat(wanted - camp);
        float needed = size.Outer + 30f;
        if (fromCamp.magnitude < needed)
            wanted = camp + (fromCamp.sqrMagnitude > 0.01f ? fromCamp.normalized : Vector3.forward) * needed;

        float maxShift = Mathf.Max(0f, size.Beach - oldRadius - 20f);
        return center + Vector3.ClampMagnitude(Flat(wanted - center), maxShift);
    }

    static Vector3 KeepInsideWorld(Vector3 center, LakeSize size)
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

        float margin = size.Outer + 10f;
        Vector3 wanted = center;
        if (maxX - minX > margin * 2f)
            wanted.x = Mathf.Clamp(center.x, minX + margin, maxX - margin);
        if (maxZ - minZ > margin * 2f)
            wanted.z = Mathf.Clamp(center.z, minZ + margin, maxZ - margin);
        return wanted;
    }

    internal static float GroundHeight(Vector3 p)
    {
        Terrain t = WorldGround.TerrainAt(p);
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
    }

    internal static void FinishScene(Vector3 center, LakeSize size)
    {
        FinishScene(center, size.Water * 1.1f);
    }

    internal static void FinishScene(Vector3 center, float viewSize)
    {
        EditorPrefs.SetBool(DoneKey, true);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
            view.LookAt(center + Vector3.up * 10f, Quaternion.Euler(40f, 0f, 0f), viewSize);
    }

    internal static Vector3 CampPosition()
    {
        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        if (tent != null)
            return tent.transform.position;
        GameObject player = GameObject.Find("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    // Searches every terrain; each 250 m from "near" counts like 1 m of bumpiness. The camp always stays dry.
    static bool FindLakeSpot(Vector3 camp, Vector3 near, LakeSize size, out Vector3 best)
    {
        best = Vector3.zero;
        Terrain bestTerrain = null;
        float bestScore = float.MaxValue;
        const float step = 40f;

        foreach (Terrain terrain in ActiveTerrains())
        {
            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;
            for (float x = o.x + step * 0.5f; x < o.x + s.x; x += step)
            {
                for (float z = o.z + step * 0.5f; z < o.z + s.z; z += step)
                {
                    Vector3 candidate = new Vector3(x, 0f, z);
                    if (!FarFromEdges(terrain, candidate, size))
                        continue;

                    if (Vector3.Distance(Flat(candidate), Flat(camp)) < size.Outer + 40f)
                        continue;

                    float score = HeightRange(candidate, size) + Vector3.Distance(Flat(candidate), Flat(near)) / 250f;
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
            best.y = GroundHeight(best);
        return bestTerrain != null;
    }

    internal static string Direction(Vector3 from, Vector3 to)
    {
        Vector3 d = Flat(to - from);
        string ns = d.z > Mathf.Abs(d.x) * 0.4f ? "north" : d.z < -Mathf.Abs(d.x) * 0.4f ? "south" : "";
        string ew = d.x > Mathf.Abs(d.z) * 0.4f ? "east" : d.x < -Mathf.Abs(d.z) * 0.4f ? "west" : "";
        return ns.Length > 0 && ew.Length > 0 ? ns + "-" + ew : ns + ew;
    }

    // Keeps the lake out of the border band the terrain combine tool smooths.
    internal static bool FarFromEdges(Terrain terrain, Vector3 p, LakeSize size)
    {
        Vector3 o = terrain.transform.position;
        Vector3 s = terrain.terrainData.size;
        float marginX = Mathf.Max(450f, size.Outer + 40f);
        return p.x > o.x + marginX && p.x < o.x + s.x - marginX &&
               p.z > o.z + size.Outer + 40f && p.z < o.z + s.z - size.Outer - 40f;
    }

    internal static float HeightRange(Vector3 center, LakeSize size)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        float r = size.Outer;
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
    internal static float WaterLevel(Vector3 center, LakeSize size)
    {
        List<float> rim = new List<float>();
        foreach (Vector3 p in RingPoints(center, new[] { size.Beach + 3f, size.Beach + 8f, size.Outer }))
            rim.Add(GroundHeight(p));
        rim.Sort();
        return rim[Mathf.FloorToInt(rim.Count * 0.3f)] - 0.4f;
    }

    static void DigBowl(Terrain terrain, Vector3 center, float waterY, LakeSize size)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        int res = data.heightmapResolution;
        Rect area = new Rect(center.x - size.Outer, center.z - size.Outer, size.Outer * 2f, size.Outer * 2f);
        RectInt cells = CellRect(area, origin, data.size, res - 1, res);
        float cellX = data.size.x / (res - 1);
        float cellZ = data.size.z / (res - 1);

        float[,] heights = data.GetHeights(cells.x, cells.y, cells.width, cells.height);
        for (int j = 0; j < cells.height; j++)
        {
            for (int i = 0; i < cells.width; i++)
            {
                float worldX = origin.x + (cells.x + i) * cellX;
                float worldZ = origin.z + (cells.y + j) * cellZ;
                float d = new Vector2(worldX - center.x, worldZ - center.z).magnitude;
                if (d > size.Outer)
                    continue;

                float original = origin.y + heights[j, i] * data.size.y;
                float shaped = LakeHeight(d, original, waterY, size);
                heights[j, i] = Mathf.Clamp01((shaped - origin.y) / data.size.y);
            }
        }

        data.SetHeights(cells.x, cells.y, heights);
    }

    // Ground height at distance d from a lake's centre, given the ground there before the lake was dug.
    internal static float LakeHeight(float d, float original, float waterY, LakeSize size)
    {
        if (d > size.Outer)
            return original;
        if (d <= size.Bowl)
            return Mathf.Min(original, Profile(d, waterY, size));
        if (d <= size.Beach)
            return Profile(d, waterY, size);

        float s = Smooth((d - size.Beach) / (size.Outer - size.Beach));
        return Mathf.Lerp(waterY + 0.3f, original, s);
    }

    internal static float LakeSand(float d, LakeSize size)
    {
        return 1f - Smooth((d - size.Beach) / (size.Sand - size.Beach));
    }

    static float Profile(float d, float waterY, LakeSize size)
    {
        if (d <= size.Bowl)
        {
            float t = d / size.Bowl;
            return waterY - 0.6f - (size.Depth - 0.6f) * (1f - t * t);
        }

        float s = Smooth((d - size.Bowl) / (size.Beach - size.Bowl));
        return Mathf.Lerp(waterY - 0.6f, waterY + 0.3f, s);
    }

    internal static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // amount(worldPoint) gives 0..1 sand for each paint texel inside the area.
    internal static void PaintSand(Terrain terrain, Rect area, System.Func<Vector3, float> amount)
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
        RectInt cells = CellRect(area, origin, data.size, res, res);
        float[,,] alpha = data.GetAlphamaps(cells.x, cells.y, cells.width, cells.height);
        int count = alpha.GetLength(2);
        for (int j = 0; j < cells.height; j++)
        {
            for (int i = 0; i < cells.width; i++)
            {
                float worldX = origin.x + (cells.x + i + 0.5f) / res * data.size.x;
                float worldZ = origin.z + (cells.y + j + 0.5f) / res * data.size.z;
                float sandAmount = Mathf.Clamp01(amount(new Vector3(worldX, 0f, worldZ)));
                if (sandAmount <= 0f)
                    continue;

                for (int l = 0; l < count; l++)
                    alpha[j, i, l] *= 1f - sandAmount;
                alpha[j, i, sandIndex] += sandAmount;
            }
        }

        data.SetAlphamaps(cells.x, cells.y, alpha);
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

    internal static Material WaterMaterial()
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

    internal static GameObject CreateWater(string name, Vector3 center, float waterY, LakeSize size)
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.name = name;
        Object.DestroyImmediate(water.GetComponent<Collider>());
        PlaceWater(water, center, waterY, size);

        MeshRenderer renderer = water.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        EnsureLakeComponents(water);
        Undo.RegisterCreatedObjectUndo(water, "Make Fishing Lake");
        return water;
    }

    internal static void EnsureLakeComponents(GameObject water)
    {
        if (water.GetComponent<LakeWater>() == null)
            Undo.AddComponent<LakeWater>(water);
        if (water.GetComponent<FishingSpot>() == null)
            Undo.AddComponent<FishingSpot>(water);
    }

    static void PlaceWater(GameObject water, Vector3 center, float waterY, LakeSize size)
    {
        water.transform.position = new Vector3(center.x, waterY - 0.01f, center.z);
        water.transform.localScale = new Vector3(size.Water * 2f, 0.01f, size.Water * 2f);
        water.GetComponent<MeshRenderer>().sharedMaterial = WaterMaterial();
    }

    internal static int HideFoliage(System.Func<Vector3, bool> inside)
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
            if (!inside(go.transform.position))
                continue;

            Undo.RecordObject(go, "Make Fishing Lake");
            go.SetActive(false);
            cleared++;
        }
        return cleared;
    }

    static void MoveAnimalsOut(Vector3 center, LakeSize size)
    {
        foreach (Animal animal in Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude))
        {
            Vector3 offset = Flat(animal.transform.position - center);
            if (offset.magnitude > size.Outer + 4f)
                continue;

            Vector3 dir = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.forward;
            Undo.RecordObject(animal.transform, "Make Fishing Lake");
            animal.transform.position = WorldGround.Snap(center + dir * (size.Outer + 6f));
        }
    }

    internal static string BackupTerrain(TerrainData data)
    {
        string path = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(path))
            return "(none)";

        string dir = Path.Combine(Path.GetTempPath(), "lake_backup_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Path.GetFileName(path));
        if (!File.Exists(target))
            File.Copy(path, target, true);
        return target;
    }

    internal static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Nature", "Lake");
    }

    internal static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}

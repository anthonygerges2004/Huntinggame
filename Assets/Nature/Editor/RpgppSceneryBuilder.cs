using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the hunting scene's scenery from RPG Poly Pack Lite (Assets/RPGPP_LT):
///  - repaints the terrain in the pack's stylised colours (grass, forest floor, cliffs, dirt, sand)
///  - levels the ground under the castles so they sit on it
///  - builds a village on the flattest ground near camp, with a dirt path from the camp,
///    and a market stall at the trader's campfire
///  - plants a forest, rocks, bushes, logs, grass and flowers around the camp, plus clouds
///  - hands the pack's prefabs to OpenWorld for the wilderness beyond the built area
///
/// Nothing is lost: the old foliage is switched off (tagged RpgppHidden), each terrain is
/// copied to Assets/RPGPP_Scenery/TerrainBackup first, and "Restore Old Scenery" puts it all back.
/// </summary>
public static partial class RpgppSceneryBuilder
{
    const string ScenePath = "Assets/Scenes/GetStarted_Scene.unity";
    const string PackFolder = "Assets/RPGPP_LT";
    const string PrefabFolder = PackFolder + "/Prefabs";
    const string GeneratedFolder = "Assets/RPGPP_Scenery";
    const string LayerFolder = GeneratedFolder + "/TerrainLayers";
    const string BackupFolder = GeneratedFolder + "/TerrainBackup";
    const string OldFoliageName = "_HuntFoliage";
    const string Log = "[RPGPP Scenery] ";
    const int Seed = 20261103;

    const string FcpFolder = "Assets/URP GanzSe Free Camping Props";

    // ---- World layout (metres, measured from the campfire) ----
    const float CampFlatRadius = 13f;      // the spawn camp is levelled flat out to here
    const float CampFalloff = 14f;         // ...and blends back into the hillside over this
    const float CampClearRadius = 22f;     // no forest / rocks / pack grass inside the camp
    const float ClearingRadius = 50f;      // trees thin out inside this radius
    const float ForestRadius = 650f;       // edge of the hand-built scenery
    const float GroundCoverRadius = 220f;  // grass / flowers only near the player area
    const float ShoreBuffer = 6f;
    const float VillageSearchMin = 70f;    // close to spawn, but clear of the camp
    const float VillageSearchMax = 160f;
    const float VillageDistanceCost = 0.06f; // metres of slope we accept per metre closer to spawn
    const float PathHalfWidth = 2.2f;

    // ---- Counts ----
    const int TreeCount = 1500;
    const int BushCount = 480;
    const int BigRockCount = 150;
    const int SmallRockCount = 260;
    const int TinyRockCount = 200;
    const int LogCount = 80;
    const int GrassCount = 3400;
    const int FlowerCount = 1000;
    const int PlantCount = 300;
    const int CloudCount = 28;

    // In the pack's demo scene the paths run along each building's local +Z side,
    // so doors face +Z.
    const float BuildingFrontYaw = 0f;

    static readonly string[] BroadleafTrees = { "rpgpp_lt_tree_01", "rpgpp_lt_tree_02" };
    static readonly string[] PineTrees = { "rpgpp_lt_tree_pine_01" };
    static readonly string[] Bushes = { "rpgpp_lt_bush_01", "rpgpp_lt_bush_02" };
    static readonly string[] BigRocks = { "rpgpp_lt_rock_01", "rpgpp_lt_rock_02", "rpgpp_lt_rock_03" };
    static readonly string[] SmallRocks = { "rpgpp_lt_rock_small_01", "rpgpp_lt_rock_small_02", "rpgpp_lt_stones_01" };
    static readonly string[] TinyRocks = { "rpgpp_lt_rocks_tiny_01" };
    static readonly string[] Logs = { "rpgpp_lt_log_wood_01" };
    static readonly string[] Grass = { "rpgpp_lt_grass_small_01a", "rpgpp_lt_grass_small_01b" };
    static readonly string[] Flowers = { "rpgpp_lt_flower_01", "rpgpp_lt_flower_02", "rpgpp_lt_flower_03" };
    static readonly string[] Plants = { "rpgpp_lt_plant_01", "rpgpp_lt_plant_02" };
    static readonly string[] Clouds = { "rpgpp_lt_cloud_01", "rpgpp_lt_cloud_02" };

    // Big pieces get exact mesh colliders; small props get a box; ground cover gets none.
    static readonly HashSet<string> MeshColliderPieces = new HashSet<string>
    {
        "rpgpp_lt_building_01", "rpgpp_lt_building_02", "rpgpp_lt_building_03",
        "rpgpp_lt_building_04", "rpgpp_lt_building_05", "rpgpp_lt_shed_wood_01",
        "rpgpp_lt_shed_wood_02", "rpgpp_lt_well_01", "rpgpp_lt_wagon_01",
        "rpgpp_lt_awning_standing_01a", "rpgpp_lt_awning_standing_01b"
    };

    enum ColliderKind { None, Box, Mesh, Trunk }
    enum GroundKind { Grass, ForestFloor, Rock, Dirt, Sand }

    struct Native
    {
        public Bounds bounds; // prefab-local bounds at scale 1
    }

    struct Pad
    {
        public Vector3 center;
        public Quaternion rot;
        public float hx, hz;
    }

    // Something placed on the ground that must be re-seated once all levelling is done.
    struct Seat
    {
        public Transform t;
        public float offset;    // pivot height above the ground (props)
        public bool building;   // buildings keep their pad height and get their footprint re-flattened
        public Pad interior;
        public float padY;
    }

    struct PropSpot
    {
        public string name;
        public float ex, ez;  // -1..1 across the parent footprint (x right, z front)
        public float pad;     // extra metres outside the footprint edge
        public float yaw;
        public PropSpot(string name, float ex, float ez, float pad, float yaw)
        {
            this.name = name; this.ex = ex; this.ez = ez; this.pad = pad; this.yaw = yaw;
        }
    }

    static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Native> nativeCache = new Dictionary<string, Native>();
    static Dictionary<string, string> prefabPaths;

    static System.Random rng;
    static Vector3 camp;
    static Vector3 village;
    static bool hasVillage;
    static float packScale = 1f;
    static Terrain[] terrains;
    static LakeWater[] lakes;
    static RiverWater[] rivers;
    static readonly List<Bounds> keepOut = new List<Bounds>();
    static readonly List<Vector3> keepOutCircles = new List<Vector3>(); // x, z, radius
    static readonly List<Pad> dirtPads = new List<Pad>();
    static readonly List<Seat> seats = new List<Seat>();
    static readonly List<string> report = new List<string>();
    static SpacingGrid solidGrid;
    static SpacingGrid coverGrid;
    static int placed;
    static int missing;

    // ------------------------------------------------------------------ menus

    [MenuItem("Tools/Hunting Game/Rebuild Scenery (RPG Poly Pack)")]
    public static void RebuildFromMenu()
    {
        if (!Prepare("Rebuild Scenery"))
            return;

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            EditorUtility.DisplayDialog("Rebuild Scenery",
                "RPG Poly Pack Lite was not found at " + PackFolder + ". Import it first.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Rebuild Scenery",
                "This rebuilds the world around the camp with RPG Poly Pack Lite:\n\n" +
                "• stylised terrain colours, castles levelled onto the ground\n" +
                "• a flat spawn camp built from GanzSe Free Camping Props\n" +
                "• a village with market stalls close to spawn, joined by a dirt path\n" +
                "• forest, rocks, bushes, grass, flowers and clouds\n\n" +
                "The terrain is backed up first and the old foliage is only switched off. " +
                "Tools > Hunting Game > Restore Old Scenery undoes all of it.\n\nThe scene will be saved.",
                "Rebuild", "Cancel"))
            return;

        RunBuildAndSave();
        FrameAt(hasVillage ? village : camp);
        EditorUtility.DisplayDialog("Rebuild Scenery", string.Join("\n", report) + "\n\nPress Play to walk around.", "OK");
    }

    [MenuItem("Tools/Hunting Game/Restore Old Scenery")]
    public static void RestoreFromMenu()
    {
        if (!Prepare("Restore Old Scenery"))
            return;
        string result = Restore();
        EditorUtility.DisplayDialog("Restore Old Scenery", result, "OK");
    }

    /// <summary>
    /// Batch-mode entry point (Unity must be closed):
    /// Unity.exe -batchmode -projectPath . -executeMethod RpgppSceneryBuilder.BatchRebuild -shots &lt;folder&gt; -quit
    /// Rebuilds, saves, and renders screenshots of the camp, village, path and castles.
    /// </summary>
    public static void BatchRebuild()
    {
        EditorSceneManager.OpenScene(ScenePath);
        RunBuildAndSave();
        string shots = ArgValue("-shots");
        if (!string.IsNullOrEmpty(shots))
            CaptureViews(shots);
        foreach (string line in report)
            Debug.Log(Log + line);
    }

    /// <summary>Batch-mode screenshots only, without rebuilding.</summary>
    public static void BatchCapture()
    {
        EditorSceneManager.OpenScene(ScenePath);
        camp = FindCamp();
        RpgppWildSet set = RpgppWildSet.Find();
        if (set != null)
            camp = set.center;
        string shots = ArgValue("-shots");
        CaptureViews(string.IsNullOrEmpty(shots) ? "Temp/RpgppShots" : shots);
    }

    static string ArgValue(string key)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == key)
                return args[i + 1];
        }

        return null;
    }

    static bool Prepare(string title)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(title, "Press Stop first, then run this again.", "OK");
            return false;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            EditorSceneManager.OpenScene(ScenePath);
        }

        return true;
    }

    static void RunBuildAndSave()
    {
        try
        {
            Build();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ build

    static void Build()
    {
        rng = new System.Random(Seed);
        prefabCache.Clear();
        nativeCache.Clear();
        prefabPaths = null;
        keepOut.Clear();
        keepOutCircles.Clear();
        dirtPads.Clear();
        seats.Clear();
        report.Clear();
        solidGrid = new SpacingGrid(8f);
        coverGrid = new SpacingGrid(4f);
        placed = 0;
        missing = 0;
        hasVillage = false;

        Progress("Converting materials to URP", 0.02f);
        ConvertPackMaterials();

        terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        lakes = Object.FindObjectsByType<LakeWater>(FindObjectsInactive.Exclude);
        rivers = Object.FindObjectsByType<RiverWater>(FindObjectsInactive.Exclude);
        camp = FindCamp();
        packScale = ComputePackScale();

        Progress("Backing up terrain", 0.04f);
        BackupAndResetTerrains();

        Progress("Switching off old foliage", 0.06f);
        GameObject old = GameObject.Find(RpgppScenery.RootName);
        if (old != null)
            Object.DestroyImmediate(old);
        HideOldScenery();

        GameObject root = new GameObject(RpgppScenery.RootName);
        CollectKeepOuts();
        ResetWorldState();

        // Choose every site first, then the roads, so buildings can keep clear of the roads.
        keepOutCircles.Add(new Vector3(camp.x, camp.z, CampClearRadius));
        hasVillage = FindVillageSite(out village, out _);
        if (hasVillage)
            reserved.Add(new Vector3(village.x, village.z, 60f));
        hasCastle = FindCastleSite();
        if (hasCastle)
        {
            reserved.Add(new Vector3(castleCenter.x, castleCenter.z, 60f));
            reserved.Add(new Vector3(townCenter.x, townCenter.z, 55f));
        }
        else
        {
            report.Add("No site found for the castle.");
        }

        hasLake = FindLakeSite();
        if (hasLake)
            reserved.Add(new Vector3(lakeVillage.x, lakeVillage.z, 50f));
        else
            hasFarm = FindFarmSite();
        if (hasFarm)
            reserved.Add(new Vector3(farm.x, farm.z, 55f));
        PlanRoads();

        // Structures first, so the forest grows around them.
        Progress("Building the spawn camp", 0.1f);
        BuildSpawnCamp(Group(root, "Camp"));

        Progress("Building the castle", 0.12f);
        BuildStronghold(Group(root, "Fortress"));
        Progress("Building the villages", 0.15f);
        BuildCastleTown(Group(root, "CastleTown"));
        BuildMainVillage(Group(root, "Village"));
        BuildLakeHamlet(Group(root, "LakeHamlet"));
        BuildFarmstead(Group(root, "Farmstead"));

        Progress("Grading roads", 0.18f);
        GradeRoads();

        // Every pad is levelled now; make each footprint exactly flat and sit everything on it.
        Reseat();

        Progress("Painting terrain", 0.2f);
        PaintTerrains();

        // Forest, rocks and bushes around every settlement; grass and flowers near each one.
        scatterZones.Add(new Vector4(camp.x, 0f, camp.z, ForestRadius));
        coverZones.Add(new Vector4(camp.x, 0f, camp.z, GroundCoverRadius));
        if (hasCastle)
        {
            scatterZones.Add(new Vector4(castleCenter.x, 0f, castleCenter.z, 360f));
            coverZones.Add(new Vector4(townCenter.x, 0f, townCenter.z, 130f));
        }

        if (hasLake)
        {
            scatterZones.Add(new Vector4(lakeVillage.x, 0f, lakeVillage.z, 340f));
            coverZones.Add(new Vector4(lakeVillage.x, 0f, lakeVillage.z, 130f));
        }

        if (hasFarm)
        {
            scatterZones.Add(new Vector4(farm.x, 0f, farm.z, 300f));
            coverZones.Add(new Vector4(farm.x, 0f, farm.z, 120f));
        }

        if (hasVillage)
            coverZones.Add(new Vector4(village.x, 0f, village.z, 130f));

        activeZones = scatterZones;
        Progress("Planting the forest", 0.35f);
        ScatterTrees(Group(root, "Forest"));
        Progress("Placing rocks", 0.5f);
        ScatterRocks(Group(root, "Rocks"));
        Progress("Growing bushes", 0.58f);
        ScatterBushes(Group(root, "Bushes"));
        Progress("Dropping logs", 0.62f);
        ScatterLogs(Group(root, "Logs"));
        Progress("Growing grass and flowers", 0.66f);
        activeZones = coverZones;
        ScatterGroundCover(Group(root, "GroundCover"));
        activeZones = scatterZones;
        Progress("Adding clouds", 0.9f);
        ScatterClouds(Group(root, "Clouds"));

        Progress("Bringing the villages to life", 0.95f);
        BuildLife(Group(root, "People"));

        FillWildSet(root);

        foreach (Terrain t in terrains)
        {
            t.Flush();
            EditorUtility.SetDirty(t.terrainData);
        }

        report.Insert(0, "Placed " + placed + " objects from RPG Poly Pack Lite (pack scale " +
                         packScale.ToString("0.00") + ").");
        if (missing > 0)
            report.Add(missing + " prefab(s) were missing; see the Console.");
        Debug.Log(Log + string.Join(" | ", report));
    }

    static Transform Group(GameObject root, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        return go.transform;
    }

    static void Progress(string message, float t)
    {
        if (!Application.isBatchMode)
            EditorUtility.DisplayProgressBar("Rebuild Scenery (RPG Poly Pack)", message, t);
    }

    static Vector3 FindCamp()
    {
        GameObject fire = GameObject.Find("Campfire");
        if (fire == null)
            fire = GameObject.Find("Player");
        Vector3 p = fire != null ? fire.transform.position : new Vector3(1111f, 0f, 1111f);
        return WorldGround.Snap(p);
    }

    // The pack is modelled to a consistent scale; use a barrel (~0.95 m tall) as the reference.
    static float ComputePackScale()
    {
        if (!TryNative("rpgpp_lt_barrel_01", out Native barrel) || barrel.bounds.size.y < 0.01f)
            return 1f;
        return Mathf.Clamp(0.95f / barrel.bounds.size.y, 0.01f, 100f);
    }

    static void FillWildSet(GameObject root)
    {
        RpgppWildSet set = root.AddComponent<RpgppWildSet>();
        set.center = camp;
        set.coverageRadius = ForestRadius;
        set.zones = scatterZones.ToArray();
        set.trees = Prefabs(BroadleafTrees, PineTrees);
        set.treeHeight = new Vector2(6.5f, 11f);
        set.bushes = Prefabs(Bushes);
        set.bushHeight = new Vector2(1.1f, 1.9f);
        set.rocks = Prefabs(BigRocks, SmallRocks);
        set.rockHeight = new Vector2(0.8f, 2.6f);
        set.grass = Prefabs(Grass, Flowers);
        set.grassScale = new Vector2(packScale, packScale * 1.6f);
    }

    static GameObject[] Prefabs(params string[][] groups)
    {
        List<GameObject> list = new List<GameObject>();
        foreach (string[] group in groups)
        {
            foreach (string name in group)
            {
                GameObject prefab = Prefab(name);
                if (prefab != null)
                    list.Add(prefab);
            }
        }

        return list.ToArray();
    }

    // ------------------------------------------------------------------ restore

    static string Restore()
    {
        GameObject root = GameObject.Find(RpgppScenery.RootName);
        if (root != null)
            Object.DestroyImmediate(root);

        int shown = 0;
        foreach (RpgppHidden hidden in Object.FindObjectsByType<RpgppHidden>(FindObjectsInactive.Include))
        {
            if (hidden == null || !hidden.gameObject.scene.IsValid())
                continue;
            hidden.gameObject.SetActive(true);
            Object.DestroyImmediate(hidden);
            shown++;
        }

        foreach (RpgppMoved moved in Object.FindObjectsByType<RpgppMoved>(FindObjectsInactive.Include))
        {
            if (moved == null || !moved.gameObject.scene.IsValid())
                continue;
            moved.transform.SetPositionAndRotation(moved.position, moved.rotation);
            Object.DestroyImmediate(moved);
        }

        int restored = 0;
        foreach (RpgppTerrainBackup backup in Object.FindObjectsByType<RpgppTerrainBackup>(FindObjectsInactive.Include))
        {
            Terrain terrain = backup.GetComponent<Terrain>();
            TerrainData data = terrain != null ? terrain.terrainData : null;
            TerrainData saved = backup.backupData;
            if (data != null && saved != null && saved.heightmapResolution == data.heightmapResolution)
            {
                int res = saved.heightmapResolution;
                data.SetHeights(0, 0, saved.GetHeights(0, 0, res, res));

                TerrainLayer[] layers = backup.originalLayers != null &&
                                        backup.originalLayers.Length == saved.alphamapLayers
                    ? backup.originalLayers
                    : saved.terrainLayers;
                data.terrainLayers = layers;
                data.alphamapResolution = saved.alphamapResolution;
                data.SetAlphamaps(0, 0, saved.GetAlphamaps(0, 0, saved.alphamapWidth, saved.alphamapHeight));
                terrain.Flush();
                EditorUtility.SetDirty(data);
                restored++;
            }
            else if (data != null && backup.originalLayers != null &&
                     backup.originalLayers.Length == data.terrainLayers.Length)
            {
                data.terrainLayers = backup.originalLayers;
                EditorUtility.SetDirty(data);
                restored++;
            }

            Object.DestroyImmediate(backup);
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        return "Removed the RPG Poly Pack scenery, switched " + shown + " old object group(s) back on " +
               "and restored " + restored + " terrain(s) (heights, paint and textures).";
    }

    // ------------------------------------------------------------------ materials

    static void ConvertPackMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError(Log + "URP Lit shader not found.");
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PackFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null)
                continue;
            if (path.Contains("_sky_"))
                continue; // sky dome isn't used; the day/night cycle owns the sky

            if (!mat.shader.name.StartsWith("Universal Render Pipeline"))
            {
                Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.1f;
                mat.shader = lit;
                mat.SetTexture("_BaseMap", tex);
                mat.SetFloat("_Smoothness", gloss);
                mat.SetFloat("_Metallic", 0f);
            }

            // The pack's colours all come from its palette texture, so the tint must stay white.
            // (An older rock-tint pass had turned it dark grey, which made everything look bland.)
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
        }
    }

    // ------------------------------------------------------------------ old scenery

    static void HideOldScenery()
    {
        GameObject foliage = GameObject.Find(OldFoliageName);
        if (foliage != null)
            Hide(foliage);

        // The old castles are replaced by the new stronghold.
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.activeSelf || go.name == RpgppScenery.RootName)
                continue;
            bool castle = go.name.IndexOf("castle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          go.GetComponentInChildren<CastleBuilder>(true) != null;
            if (castle)
                Hide(go);
        }

        // Loose chop-trees left outside the old foliage root.
        foreach (HarvestableTree tree in Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude))
        {
            if (tree == null || tree.GetComponent<Animal>() != null)
                continue;
            if (tree.transform.root.name == RpgppScenery.RootName)
                continue;
            Hide(tree.gameObject);
        }
    }

    static void Hide(GameObject go)
    {
        if (go.GetComponent<RpgppHidden>() == null)
            go.AddComponent<RpgppHidden>();
        go.SetActive(false);
    }

    static void CollectKeepOuts()
    {
        HashSet<string> ignore = new HashSet<string>
        {
            RpgppScenery.RootName, OldFoliageName, ApplyRealisticTerrain.RootName, "River", "Water", "Water2",
            "Player", "Directional Light", "EventSystem", "MiniMapCamerra", "GameManager", "Canvas",
            "ThirdPersonPoint", "permalight"
        };

        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.activeInHierarchy || ignore.Contains(go.name))
                continue;
            if (go.GetComponentInChildren<Terrain>() != null || go.GetComponentInChildren<Animal>() != null)
                continue;
            if (!TryRenderBounds(go, out Bounds b) || b.size.x > 400f || b.size.z > 400f)
                continue;

            b.Expand(new Vector3(12f, 0f, 12f));
            keepOut.Add(b);
        }
    }

    static bool TryRenderBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
                continue;
            if (!any)
            {
                b = r.bounds;
                any = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        return any;
    }

    // ------------------------------------------------------------------ terrain: backup, levelling

    // Copies each TerrainData once before it is ever levelled or repainted. On later rebuilds the
    // heights are reset from that copy first, so levelling never piles up.
    static void BackupAndResetTerrains()
    {
        EnsureFolder(BackupFolder);
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            if (data == null)
                continue;

            RpgppTerrainBackup backup = terrain.GetComponent<RpgppTerrainBackup>();
            if (backup == null)
            {
                backup = terrain.gameObject.AddComponent<RpgppTerrainBackup>();
                backup.originalLayers = data.terrainLayers;
            }

            if (backup.backupData == null)
            {
                string source = AssetDatabase.GetAssetPath(data);
                string target = BackupFolder + "/" + terrain.name + "_" + Path.GetFileName(source);
                if (!File.Exists(target))
                    AssetDatabase.CopyAsset(source, target);
                backup.backupData = AssetDatabase.LoadAssetAtPath<TerrainData>(target);
            }
            else if (backup.backupData.heightmapResolution == data.heightmapResolution)
            {
                int res = data.heightmapResolution;
                data.SetHeights(0, 0, backup.backupData.GetHeights(0, 0, res, res));
            }

            EditorUtility.SetDirty(backup);
        }
    }

    // Blends the ground toward targetY inside a rotated rectangle, fading out over `falloff` metres.
    // Round version: flat out to `radius`, then blends back over `falloff`.
    static void LevelDisc(Vector3 center, float radius, float targetY, float falloff)
    {
        Level(center, Quaternion.identity, radius, radius, targetY, falloff, round: true);
    }

    static void Level(Vector3 center, Quaternion rot, float hx, float hz, float targetY, float falloff, bool round = false)
    {
        Quaternion inv = Quaternion.Inverse(rot);
        float reach = Mathf.Sqrt(hx * hx + hz * hz) + falloff;

        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            if (data == null)
                continue;

            Vector3 o = terrain.transform.position;
            Vector3 s = data.size;
            int res = data.heightmapResolution;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - reach - o.x) / s.x * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + reach - o.x) / s.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - reach - o.z) / s.z * (res - 1)), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + reach - o.z) / s.z * (res - 1)), 0, res - 1);
            if (x1 <= x0 || z1 <= z0)
                continue;

            int w = x1 - x0 + 1;
            int h = z1 - z0 + 1;
            float[,] heights = data.GetHeights(x0, z0, w, h);
            bool changed = false;
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float wx = o.x + (x0 + i) / (float)(res - 1) * s.x;
                    float wz = o.z + (z0 + j) / (float)(res - 1) * s.z;
                    Vector3 local = inv * new Vector3(wx - center.x, 0f, wz - center.z);
                    float dist;
                    if (round)
                    {
                        dist = Mathf.Max(0f, local.magnitude - hx);
                    }
                    else
                    {
                        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - hx);
                        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - hz);
                        dist = Mathf.Sqrt(dx * dx + dz * dz);
                    }
                    float k = dist <= 0f ? 1f : falloff <= 0f ? 0f : 1f - Mathf.SmoothStep(0f, 1f, dist / falloff);
                    if (k <= 0f)
                        continue;

                    float current = heights[j, i] * s.y + o.y;
                    heights[j, i] = Mathf.Clamp01((Mathf.Lerp(current, targetY, k) - o.y) / s.y);
                    changed = true;
                }
            }

            if (changed)
                data.SetHeights(x0, z0, heights);
        }
    }

    // ------------------------------------------------------------------ terrain: paint

    static void PaintTerrains()
    {
        TerrainLayer[] layers =
        {
            StyledLayer(GroundKind.Grass), StyledLayer(GroundKind.ForestFloor), StyledLayer(GroundKind.Rock),
            StyledLayer(GroundKind.Dirt), StyledLayer(GroundKind.Sand)
        };

        float grass = 0f, forest = 0f, rock = 0f, dirt = 0f, sand = 0f;
        long texels = 0;

        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            RpgppTerrainBackup backup = terrain.GetComponent<RpgppTerrainBackup>();
            if (data == null)
                continue;

            // Sand (lake shores, river banks) comes from the untouched backup paint.
            TerrainData source = backup != null && backup.backupData != null ? backup.backupData : data;
            int w = source.alphamapWidth;
            int h = source.alphamapHeight;
            float[,,] before = source.GetAlphamaps(0, 0, w, h);
            TerrainLayer[] beforeLayers = source.terrainLayers;
            bool[] isSand = new bool[before.GetLength(2)];
            for (int l = 0; l < isSand.Length && l < beforeLayers.Length; l++)
                isSand[l] = Classify(beforeLayers[l]) == GroundKind.Sand;

            float[,,] map = new float[h, w, layers.Length];
            Vector3 o = terrain.transform.position;
            Vector3 size = data.size;
            for (int y = 0; y < h; y++)
            {
                float nz = h <= 1 ? 0f : y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float nx = w <= 1 ? 0f : x / (float)(w - 1);
                    Vector3 world = new Vector3(o.x + nx * size.x, 0f, o.z + nz * size.z);

                    float wSand = 0f;
                    for (int l = 0; l < isSand.Length; l++)
                    {
                        if (isSand[l])
                            wSand += before[y, x, l];
                    }

                    float up = data.GetInterpolatedNormal(nx, nz).y;
                    float wRock = Smooth(Mathf.InverseLerp(0.74f, 0.60f, up)) * (1f - wSand);
                    float wDirt = Mathf.Min(DirtMask(world), 1f - wSand - wRock);
                    float rest = Mathf.Max(0f, 1f - wSand - wRock - wDirt);
                    float wForest = rest * Smooth(Mathf.InverseLerp(0.47f, 0.6f, ForestNoise(world)));
                    float wGrass = rest - wForest;

                    map[y, x, 0] = wGrass;
                    map[y, x, 1] = wForest;
                    map[y, x, 2] = wRock;
                    map[y, x, 3] = wDirt;
                    map[y, x, 4] = wSand;
                    grass += wGrass; forest += wForest; rock += wRock; dirt += wDirt; sand += wSand;
                    texels++;
                }
            }

            data.terrainLayers = layers;
            data.alphamapResolution = source.alphamapResolution;
            data.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(data);
        }

        if (texels > 0)
        {
            report.Add("Terrain repainted: " + Pct(grass, texels) + " grass, " + Pct(forest, texels) + " forest floor, " +
                       Pct(rock, texels) + " cliff, " + Pct(dirt, texels) + " dirt, " + Pct(sand, texels) + " sand.");
        }
    }

    static string Pct(float v, long n)
    {
        return Mathf.RoundToInt(v * 100f / n) + "%";
    }

    static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // Dirt around the fire, the village square, under buildings and castles, and along the path.
    static float DirtMask(Vector3 p)
    {
        float m = 1f - Mathf.Clamp01((Flat(p - camp).magnitude - 4.5f) / 3f);
        foreach (Vector4 sq in squares)
        {
            float d = Flat(p - new Vector3(sq.x, 0f, sq.z)).magnitude;
            m = Mathf.Max(m, 1f - Mathf.Clamp01((d - sq.w) / 4f));
        }

        float road = RoadDistance(p);
        m = Mathf.Max(m, 1f - Mathf.Clamp01((road - PathHalfWidth) / 1.3f));

        foreach (Pad pad in dirtPads)
        {
            float reach = pad.hx + pad.hz + 2f;
            if (Flat(p - pad.center).sqrMagnitude > reach * reach)
                continue;
            Vector3 local = Quaternion.Inverse(pad.rot) * (p - pad.center);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - pad.hx);
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - pad.hz);
            m = Mathf.Max(m, 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / 2f));
        }

        return m;
    }

    static GroundKind Classify(TerrainLayer layer)
    {
        if (layer == null)
            return GroundKind.Grass;

        string s = (layer.name + " " + (layer.diffuseTexture != null ? layer.diffuseTexture.name : "")).ToLowerInvariant();
        if (s.Contains("rock") || s.Contains("cliff") || s.Contains("stone") || s.Contains("mountain"))
            return GroundKind.Rock;
        if (s.Contains("sand") || s.Contains("beach"))
            return GroundKind.Sand;
        if (s.Contains("forest") || s.Contains("moss") || s.Contains("leaves") || s.Contains("needle"))
            return GroundKind.ForestFloor;
        if (s.Contains("dirt") || s.Contains("soil") || s.Contains("mud") || s.Contains("path") ||
            s.Contains("gravel") || s.Contains("ground"))
            return GroundKind.Dirt;
        return GroundKind.Grass;
    }

    static Color GroundColor(GroundKind kind)
    {
        switch (kind)
        {
            case GroundKind.ForestFloor: return new Color(0.30f, 0.47f, 0.20f);
            case GroundKind.Dirt: return new Color(0.58f, 0.44f, 0.29f);
            case GroundKind.Rock: return new Color(0.50f, 0.48f, 0.45f);
            case GroundKind.Sand: return new Color(0.86f, 0.78f, 0.55f);
            default: return new Color(0.45f, 0.66f, 0.27f);
        }
    }

    static TerrainLayer StyledLayer(GroundKind kind)
    {
        EnsureFolder(LayerFolder);
        string name = "rpgpp_" + kind.ToString().ToLowerInvariant();
        string layerPath = LayerFolder + "/" + name + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer != null)
            return layer;

        layer = new TerrainLayer
        {
            diffuseTexture = GroundTexture(name, GroundColor(kind), (int)kind),
            tileSize = new Vector2(8f, 8f),
            smoothness = 0f,
            metallic = 0f
        };
        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    // Small tileable texture: the flat colour with soft, low-contrast blotches.
    static Texture2D GroundTexture(string name, Color baseColor, int seed)
    {
        string path = LayerFolder + "/" + name + ".png";
        if (!File.Exists(path))
        {
            const int n = 128;
            System.Random noise = new System.Random(Seed + seed * 97);
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            float p1 = (float)noise.NextDouble() * 6.28f;
            float p2 = (float)noise.NextDouble() * 6.28f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 6.2831853f;
                    float v = y / (float)n * 6.2831853f;
                    float blotch = Mathf.Sin(u * 3f + p1) * Mathf.Sin(v * 2f + p2) * 0.5f +
                                   Mathf.Sin((u + v) * 5f + p2) * 0.3f +
                                   Mathf.Sin((u - v) * 7f + p1) * 0.2f;
                    float grain = ((float)noise.NextDouble() - 0.5f) * 0.035f;
                    float k = 1f + blotch * 0.05f + grain;
                    tex.SetPixel(x, y, new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f));
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // ------------------------------------------------------------------ village

    // Finds the flattest open ground within reach of the camp: the camp itself sits on a slope.
    static bool FindVillageSite(out Vector3 site, out float range)
    {
        site = camp;
        range = float.MaxValue;
        float bestScore = float.MaxValue;

        for (float r = VillageSearchMin; r <= VillageSearchMax; r += 15f)
        {
            int steps = Mathf.Max(12, Mathf.RoundToInt(2f * Mathf.PI * r / 20f));
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Vector3 c = camp + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!OnTerrain(c, out Vector3 ground, out _) || SiteBlocked(ground, 34f))
                    continue;

                float lo = ground.y, hi = ground.y;
                bool wet = NearWater(ground);
                for (int k = 0; k < 16 && !wet; k++)
                {
                    float ka = k * Mathf.PI / 8f;
                    float kr = k % 2 == 0 ? 26f : 13f;
                    Vector3 p = ground + new Vector3(Mathf.Cos(ka) * kr, 0f, Mathf.Sin(ka) * kr);
                    if (!OnTerrain(p, out Vector3 g, out _) || NearWater(g))
                    {
                        wet = true;
                        break;
                    }

                    lo = Mathf.Min(lo, g.y);
                    hi = Mathf.Max(hi, g.y);
                }

                if (wet)
                    continue;

                float score = (hi - lo) + r * VillageDistanceCost;
                if (score < bestScore)
                {
                    bestScore = score;
                    site = ground;
                    range = hi - lo;
                }
            }
        }

        return bestScore < float.MaxValue;
    }

    static PropSpot[] StallProps(bool second)
    {
        return second
            ? new[]
            {
                new PropSpot("rpgpp_lt_table_01", 0f, 0f, 0f, 0f),
                new PropSpot("rpgpp_lt_basket_02", -1f, 0.4f, 0.4f, 0f),
                new PropSpot("rpgpp_lt_sack_02_set", -1f, -0.6f, 0.6f, 20f),
                new PropSpot("rpgpp_lt_vase_03", 1f, 0.5f, 0.3f, 0f),
                new PropSpot("rpgpp_lt_bowl_metal_01", 0.6f, 1f, 0.4f, 0f),
                new PropSpot("rpgpp_lt_package_01", 1f, -0.7f, 0.5f, 25f),
            }
            : new[]
            {
                new PropSpot("rpgpp_lt_table_01", 0f, 0f, 0f, 0f),
                new PropSpot("rpgpp_lt_crate_01", -1f, 0.2f, 0.4f, 12f),
                new PropSpot("rpgpp_lt_crate_02", -1f, -0.6f, 0.4f, -20f),
                new PropSpot("rpgpp_lt_barrel_01", 1f, 0.1f, 0.4f, 0f),
                new PropSpot("rpgpp_lt_vase_01", 0.8f, 1f, 0.3f, 0f),
                new PropSpot("rpgpp_lt_vase_02", 0.4f, 1f, 0.5f, 0f),
                new PropSpot("rpgpp_lt_basket_01", -0.5f, 1f, 0.4f, 30f),
            };
    }

    static PropSpot[] ShedProps()
    {
        return new[]
        {
            new PropSpot("rpgpp_lt_log_wood_02a", 1f, 0f, 0.8f, 90f),
            new PropSpot("rpgpp_lt_log_wood_02b", 1f, -0.7f, 0.8f, 90f),
            new PropSpot("rpgpp_lt_bucket_01", -1f, 0.8f, 0.4f, 0f),
            new PropSpot("rpgpp_lt_rake_01", -1f, 0.2f, 0.3f, 15f),
            new PropSpot("rpgpp_lt_broom_01", -1f, -0.2f, 0.3f, -10f),
            new PropSpot("rpgpp_lt_ladder_01", 0.3f, -1f, 0.3f, 180f),
        };
    }

    // Each house gets the same kit of yard props plus a different flower bed by the door.
    static PropSpot[] HouseProps(int variant)
    {
        string flower = Flowers[variant % Flowers.Length];
        string other = Flowers[(variant + 1) % Flowers.Length];
        return new[]
        {
            new PropSpot("rpgpp_lt_barrel_01", 1f, 0.7f, 0.6f, 0f),
            new PropSpot("rpgpp_lt_barrel_02", 1f, 0.3f, 0.7f, 40f),
            new PropSpot("rpgpp_lt_crate_01", -1f, 0.7f, 0.6f, 8f),
            new PropSpot("rpgpp_lt_box_wood_01", -1f, 0.35f, 0.6f, -15f),
            new PropSpot("rpgpp_lt_bench_wood_02", 0.65f, 1f, 0.9f, 180f),
            new PropSpot("rpgpp_lt_hanger_clothes_01", -1f, -0.5f, 1.5f, 90f),
            new PropSpot("rpgpp_lt_bird_house_01", 1f, -0.7f, 1.8f, 0f),
            new PropSpot(flower, -0.75f, 1f, 0.5f, 0f),
            new PropSpot(other, -0.45f, 1f, 0.6f, 40f),
            new PropSpot(flower, -0.15f, 1f, 0.5f, 80f),
            new PropSpot("rpgpp_lt_plant_01", 1f, 1f, 0.5f, 0f),
        };
    }

    // ------------------------------------------------------------------ spawn camp

    // A flat camp at spawn, built from GanzSe Free Camping Props: the trader's fire in the middle,
    // benches round it, tents at the back, flag bunting, and bushes and grass round the edge.
    static void BuildSpawnCamp(Transform parent)
    {
        campPieces.Clear();

        // The camp opens toward the village (where the path leaves), or downhill if there is none.
        Vector3 open = hasVillage ? Flat(village - camp).normalized : -UphillAt(camp);
        float baseAngle = Mathf.Atan2(open.z, open.x) * Mathf.Rad2Deg;

        LevelDisc(camp, CampFlatRadius, camp.y, CampFalloff);
        dirtPads.Add(new Pad { center = camp, rot = Quaternion.identity, hx = 2.5f, hz = 2.5f });

        GameObject fireLogic = GameObject.Find("Campfire");
        if (fireLogic != null)
        {
            RpgppMoved.Remember(fireLogic);
            fireLogic.transform.position = camp;
        }

        PlaceCampPiece(parent, "FCP_Campfire_Type1_Color1", 0, camp, Quaternion.Euler(0f, baseAngle, 0f), 0.9f);

        for (int i = 0; i < 4; i++)
        {
            Vector3 p = CampPoint(baseAngle + 45f + i * 90f, 3.6f);
            PlaceCampPiece(parent, "FCP_Bench_Type1_Color1", i, p, Face(p, camp), 0.9f);
        }

        float[] tentAngles = { 180f, 118f, 242f };
        for (int i = 0; i < tentAngles.Length; i++)
        {
            Vector3 p = CampPoint(baseAngle + tentAngles[i], 8.8f);
            PlaceCampPiece(parent, "FCP_Tent_Type1_Color1", i, p, Face(p, camp), 2.4f);
        }

        // Bunting between the tents, and a pair framing the entrance.
        float[] flagAngles = { 149f, 211f, 32f, -32f };
        for (int i = 0; i < flagAngles.Length; i++)
        {
            Vector3 p = CampPoint(baseAngle + flagAngles[i], i < 2 ? 9.5f : 8f);
            PlaceCampPiece(parent, "FCP_FlagBunting_Type1_Color1", i, p, Face(p, camp), 2.6f, spanAcross: true);
        }

        // Bushes round the edge (not across the entrance) and grass tufts inside.
        GameObject bush = FcpAsset("FCP__Bush_Type1_Color1");
        GameObject grass = FcpAsset("FCP_GrassCluster_Type1_Color1");
        for (int i = 0; i < 16 && bush != null; i++)
        {
            float a = baseAngle + 40f + i * (280f / 15f) + Range(-6f, 6f);
            Vector3 p = CampPoint(a, Range(12.5f, 15.5f));
            SpawnFcp(parent, bush, p, Quaternion.Euler(0f, Range(0f, 360f), 0f), Range(0.9f, 1.35f));
        }

        int tufts = 0;
        for (int attempt = 0; attempt < 400 && tufts < 45 && grass != null; attempt++)
        {
            float a = Range(0f, 360f);
            float r = Range(4.8f, 16f);
            Vector3 p = CampPoint(a, r);
            if (!coverGrid.Free(p, 1.6f) || NearCampPiece(p, 1.8f))
                continue;
            SpawnFcp(parent, grass, p, Quaternion.Euler(0f, Range(0f, 360f), 0f), Range(0.8f, 1.4f));
            coverGrid.Add(p);
            tufts++;
        }

        // Spawn the player at the camp entrance, looking at the fire.
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            RpgppMoved.Remember(player);
            Vector3 p = CampPoint(baseAngle, 5.5f);
            WorldGround.TryGetGround(p, out Vector3 g, out _);
            player.transform.position = g;
            player.transform.rotation = Face(g, camp);
        }

        SnapNearbyWildlife(camp, CampFlatRadius + CampFalloff + 10f);
        report.Add("Spawn camp levelled flat (" + Mathf.RoundToInt(CampFlatRadius * 2f) +
                   " m across) and built from GanzSe Free Camping Props.");
    }

    static readonly List<Vector3> campPieces = new List<Vector3>();

    static Vector3 CampPoint(float angleDeg, float r)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return camp + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    static bool NearCampPiece(Vector3 p, float r)
    {
        foreach (Vector3 c in campPieces)
        {
            if (Flat(c - p).sqrMagnitude < r * r)
                return true;
        }

        return false;
    }

    static GameObject FcpAsset(string name)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(FcpFolder + "/Prefabs/" + name + ".prefab");
        if (asset == null)
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(FcpFolder + "/Models/Original 1.0/" + name + ".fbx");
        if (asset == null)
        {
            Debug.LogWarning(Log + "Missing camping prop " + name);
            missing++;
        }

        return asset;
    }

    // Uses the scene's existing camp object for index 0 (other scripts look those up by name),
    // and copies of it for the rest; falls back to the pack model at a sensible height.
    // spanAcross: the piece is long and thin (bunting); turn it so its long side runs around the
    // camp rather than pointing at the fire.
    static void PlaceCampPiece(Transform parent, string name, int index, Vector3 at, Quaternion rot,
                               float fallbackHeight, bool spanAcross = false)
    {
        GameObject go = FindSceneObject(index == 0 ? name : name + "_" + index);
        RpgppMoved.Remember(go);
        if (go == null)
        {
            GameObject template = FindSceneObject(name) ?? FindSceneObject(name + "_1");
            if (template != null)
            {
                go = Object.Instantiate(template, parent);
            }
            else
            {
                GameObject model = FcpAsset(name);
                if (model == null)
                    return;
                go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                if (TryRenderBounds(go, out Bounds nb) && nb.size.y > 0.01f)
                    go.transform.localScale *= fallbackHeight / nb.size.y;
                AddBoxFromRenderers(go);
            }

            go.name = name + "_Camp" + index;
            placed++;
        }

        if (spanAcross)
        {
            go.transform.rotation = Quaternion.identity;
            if (TryRenderBounds(go, out Bounds lb) && lb.size.z > lb.size.x)
                rot *= Quaternion.Euler(0f, 90f, 0f);
        }

        go.transform.rotation = rot;
        go.transform.position = at;
        SitOnGround(go, 0.02f);
        campPieces.Add(at);
    }

    static GameObject SpawnFcp(Transform parent, GameObject asset, Vector3 at, Quaternion rot, float scale)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.rotation = rot;
        go.transform.localScale = asset.transform.localScale * scale;
        go.transform.position = at;
        SitOnGround(go, 0.03f);
        MarkStatic(go);
        placed++;
        return go;
    }

    static GameObject FindSceneObject(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    // Puts the bottom of an object's renderers on the ground and remembers it for Reseat().
    static void SitOnGround(GameObject go, float sink)
    {
        if (!WorldGround.TryGetGround(go.transform.position, out Vector3 g, out _))
            return;
        float offset = 0f;
        if (TryRenderBounds(go, out Bounds b))
            offset = go.transform.position.y - b.min.y;
        Vector3 p = go.transform.position;
        p.y = g.y + offset - sink;
        go.transform.position = p;
        seats.Add(new Seat { t = go.transform, offset = offset - sink });
    }

    static void AddBoxFromRenderers(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null || !TryRenderBounds(go, out Bounds world))
            return;
        BoxCollider box = go.AddComponent<BoxCollider>();
        Vector3 s = go.transform.lossyScale;
        box.center = go.transform.InverseTransformPoint(world.center);
        box.size = new Vector3(world.size.x / Mathf.Max(0.0001f, s.x), world.size.y / Mathf.Max(0.0001f, s.y),
                               world.size.z / Mathf.Max(0.0001f, s.z)) * 0.9f;
    }

    static void SnapNearbyWildlife(Vector3 center, float radius)
    {
        foreach (Animal animal in Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude))
        {
            if (animal == null || animal.kind == AnimalKind.Bird)
                continue;
            if (Flat(animal.transform.position - center).magnitude > radius)
                continue;
            animal.transform.position = WorldGround.Snap(animal.transform.position);
        }
    }

    // After all levelling: re-flatten each building's own footprint at its pad height (a later
    // pad's soft edge may have raised it), then sit every prop back on the final ground.
    static void Reseat()
    {
        foreach (Seat seat in seats)
        {
            if (seat.building && seat.t != null)
                Level(seat.interior.center, seat.interior.rot, seat.interior.hx, seat.interior.hz, seat.padY, 0f);
        }

        foreach (Seat seat in seats)
        {
            if (seat.building || seat.t == null)
                continue;
            if (!WorldGround.TryGetGround(seat.t.position, out Vector3 g, out _))
                continue;
            Vector3 p = seat.t.position;
            p.y = g.y + seat.offset;
            seat.t.position = p;
        }
    }

    // Places a structure facing `faceTarget`, somewhere on a ring around `origin`, levels a pad
    // under it and dresses it with props. Returns false if no spot was found.
    static GameObject PlaceStructure(Transform parent, string name, Vector3 origin, Vector3 faceTarget, float angleDeg,
                                     float rMin, float rMax, float maxSwing, float maxDrop, PropSpot[] props, bool fence,
                                     out Vector3 front)
    {
        front = origin;
        if (!TryNative(name, out Native native))
            return null;

        float scale = packScale;
        Vector3 size = native.bounds.size * scale;
        float hx = size.x * 0.5f;
        float hz = size.z * 0.5f;
        float reach = Mathf.Sqrt(hx * hx + hz * hz) + 2.5f;
        int swings = Mathf.Max(1, Mathf.RoundToInt(maxSwing / 15f) * 2 + 1);

        for (int step = 0; step < swings; step++)
        {
            // 0, +15, -15, +30, -30 ... degrees around the preferred direction
            float offset = ((step + 1) / 2) * 15f * (step % 2 == 0 ? 1f : -1f);
            float a = (angleDeg + offset) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            for (float r = rMin; r <= Mathf.Max(rMin, rMax); r += 2.5f)
            {
                Vector3 center = origin + dir * r;
                bool atOrigin = r < 0.01f;
                if (!OnTerrain(center, out Vector3 g, out _) || (!atOrigin && Blocked(g, reach)) || NearWater(g) ||
                    (!atOrigin && OnPath(g, reach)))
                    continue;

                Quaternion rot = atOrigin
                    ? Quaternion.Euler(0f, angleDeg + 90f, 0f)
                    : Quaternion.Euler(0f, Mathf.Atan2(faceTarget.x - g.x, faceTarget.z - g.z) * Mathf.Rad2Deg + BuildingFrontYaw, 0f);
                if (!FootprintOk(g, rot, hx + 0.5f, hz + 0.5f, maxDrop, out float lo, out float hi))
                    continue;

                float padY = (lo + hi) * 0.5f;
                Pad interior = new Pad { center = g, rot = rot, hx = hx + 0.8f, hz = hz + 0.8f };
                Level(g, rot, interior.hx, interior.hz, padY, 6f);
                dirtPads.Add(new Pad { center = g, rot = rot, hx = hx + 1.2f, hz = hz + 1.2f });

                Vector3 pivot = new Vector3(g.x, 0f, g.z) -
                                rot * new Vector3(native.bounds.center.x, 0f, native.bounds.center.z) * scale;
                pivot.y = padY - native.bounds.min.y * scale - 0.05f;
                GameObject building = Spawn(name, parent, pivot, rot, scale);
                if (building == null)
                    return null;
                AddCollider(building, native, MeshColliderPieces.Contains(name) ? ColliderKind.Mesh : ColliderKind.Box);
                MarkStatic(building);
                keepOutCircles.Add(new Vector3(g.x, g.z, reach));
                solidGrid.Add(g);
                seats.Add(new Seat { t = building.transform, building = true, interior = interior, padY = padY });

                foreach (PropSpot spot in props)
                {
                    Vector3 local = new Vector3(spot.ex * (hx + spot.pad), 0f, spot.ez * (hz + spot.pad));
                    SpawnAt(parent, spot.name, g + rot * local, rot * Quaternion.Euler(0f, spot.yaw, 0f));
                }

                if (fence)
                    BuildFence(parent, g, rot, hx + 3f, -(hz + 3f));
                front = WorldGround.Snap(g + rot * new Vector3(0f, 0f, hz + 1.6f));
                return building;
            }
        }

        Debug.LogWarning(Log + "No spot found for " + name + "; skipped it.");
        return null;
    }

    static Quaternion Face(Vector3 from, Vector3 to)
    {
        Vector3 d = Flat(to - from);
        return d.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(d, Vector3.up);
    }

    static string Compass(Vector3 d)
    {
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        float bearing = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
        return names[Mathf.RoundToInt(bearing / 45f) % 8];
    }

    // A straight fence run behind a building, from local x = -halfWidth to +halfWidth at local z.
    static void BuildFence(Transform parent, Vector3 center, Quaternion rot, float halfWidth, float z)
    {
        string[] pieces = { "rpgpp_lt_fence_wood_01a", "rpgpp_lt_fence_wood_01b" };
        if (!TryNative(pieces[0], out Native native))
            return;

        float length = Mathf.Max(0.5f, native.bounds.size.x * packScale);
        int count = Mathf.Max(1, Mathf.CeilToInt(halfWidth * 2f / length));
        float start = -count * length * 0.5f + length * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Vector3 world = center + rot * new Vector3(start + i * length, 0f, z);
            if (NearWater(world))
                continue;
            SpawnAt(parent, pieces[i % pieces.Length], world, rot);
        }
    }

    static bool FootprintOk(Vector3 center, Quaternion rot, float hx, float hz, float maxDrop, out float lo, out float hi)
    {
        lo = float.MaxValue;
        hi = float.MinValue;
        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iz = -1; iz <= 1; iz++)
            {
                Vector3 p = center + rot * new Vector3(ix * hx, 0f, iz * hz);
                if (!OnTerrain(p, out Vector3 ground, out _) || NearWater(p))
                    return false;
                lo = Mathf.Min(lo, ground.y);
                hi = Mathf.Max(hi, ground.y);
            }
        }

        return hi - lo <= maxDrop;
    }

    // Spawns a pack prop at pack scale, sitting on the ground.
    static GameObject SpawnAt(Transform parent, string name, Vector3 world, Quaternion rot)
    {
        if (!TryNative(name, out Native native) || !OnTerrain(world, out Vector3 ground, out _))
            return null;

        Vector3 pivot = ground;
        pivot.y -= native.bounds.min.y * packScale + 0.02f;
        GameObject go = Spawn(name, parent, pivot, rot, packScale);
        if (go == null)
            return null;
        seats.Add(new Seat { t = go.transform, offset = pivot.y - ground.y });

        Vector3 size = native.bounds.size * packScale;
        ColliderKind kind = MeshColliderPieces.Contains(name) ? ColliderKind.Mesh
            : size.y > 0.6f && Mathf.Max(size.x, size.z) > 0.4f ? ColliderKind.Box
            : ColliderKind.None;
        AddCollider(go, native, kind);
        MarkStatic(go);
        solidGrid.Add(ground);
        return go;
    }

    // ------------------------------------------------------------------ nature

    static void ScatterTrees(Transform parent)
    {
        int count = 0;
        for (int attempt = 0; attempt < TreeCount * 14 && count < TreeCount; attempt++)
        {
            if (!Candidate(ForestRadius, CampClearRadius + 4f, 0.78f, 3f, out Vector3 p, out Vector3 normal))
                continue;

            float forest = ForestNoise(p);
            float chance = Mathf.Max(0.05f, Smooth(Mathf.InverseLerp(0.40f, 0.62f, forest)));
            chance *= Mathf.InverseLerp(CampClearRadius + 4f, ClearingRadius, Flat(p - camp).magnitude);
            if (Rand() > chance || !solidGrid.Free(p, 4.2f))
                continue;

            float altitude = p.y - camp.y;
            bool pine = Rand() < Mathf.Clamp01(0.3f + altitude / 150f + (1f - normal.y) * 2f);
            string name = Pick(pine ? PineTrees : BroadleafTrees);
            float height = pine ? Range(8f, 12.5f) : Range(6.5f, 9.5f);

            GameObject tree = SpawnScaled(parent, name, p, Quaternion.Euler(0f, Range(0f, 360f), 0f), height, 0.2f);
            if (tree == null)
                continue;

            HarvestableTree harvest = tree.AddComponent<HarvestableTree>();
            harvest.woodAmount = pine ? 4 : 3;
            AddCollider(tree, nativeCache[name], ColliderKind.Trunk);
            solidGrid.Add(p);
            count++;
        }

        report.Add("Forest: " + count + " trees.");
    }

    static void ScatterRocks(Transform parent)
    {
        int count = 0;
        for (int attempt = 0; attempt < BigRockCount * 20 && count < BigRockCount; attempt++)
        {
            if (!Candidate(ForestRadius, CampClearRadius + 6f, 0.5f, 3f, out Vector3 p, out Vector3 normal))
                continue;
            if (Rand() > Mathf.Clamp01(0.25f + (1f - normal.y) * 2.2f) || !solidGrid.Free(p, 6f))
                continue;

            string name = Pick(BigRocks);
            float height = Range(1.8f, 4.2f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.6f)) *
                             Quaternion.Euler(0f, Range(0f, 360f), 0f);
            GameObject rock = SpawnScaled(parent, name, p, rot, height, height * 0.18f);
            if (rock == null)
                continue;
            AddCollider(rock, nativeCache[name], ColliderKind.Box);
            MarkStatic(rock);
            solidGrid.Add(p);
            count++;
        }

        ScatterSmall(parent, SmallRocks, SmallRockCount, 420f, 0.6f, 2.5f, 0.4f, 1.0f, 0.15f, 0.01f, true, p => 1f);
        ScatterSmall(parent, TinyRocks, TinyRockCount, 300f, 0.7f, 2f, 0.2f, 0.4f, 0.1f, 0.02f, false, p => 1f);
    }

    static void ScatterBushes(Transform parent)
    {
        ScatterSmall(parent, Bushes, BushCount, ForestRadius * 0.8f, 0.72f, 3f, 1.1f, 1.9f, 0.1f, 0.012f, true,
            p => 0.15f + 0.85f * Mathf.Clamp01(1f - Mathf.Abs(ForestNoise(p) - 0.45f) / 0.15f));
    }

    static void ScatterLogs(Transform parent)
    {
        int count = 0;
        for (int attempt = 0; attempt < LogCount * 20 && count < LogCount; attempt++)
        {
            if (!Candidate(500f, ClearingRadius, 0.86f, 4f, out Vector3 p, out Vector3 normal))
                continue;
            if (ForestNoise(p) < 0.5f || !solidGrid.Free(p, 4f))
                continue;

            string name = Pick(Logs);
            if (!TryNative(name, out Native native))
                return;
            float scale = packScale * Range(0.9f, 1.3f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Range(0f, 360f), 0f);
            Vector3 pivot = p - Vector3.up * (native.bounds.min.y * scale + 0.05f);
            GameObject log = Spawn(name, parent, pivot, rot, scale);
            if (log == null)
                continue;
            AddCollider(log, native, ColliderKind.Box);
            MarkStatic(log);
            solidGrid.Add(p);
            count++;
        }
    }

    static void ScatterGroundCover(Transform parent)
    {
        // Grass everywhere near the player area, a bit thinner under dense forest.
        ScatterSmall(parent, Grass, GrassCount, GroundCoverRadius, 0.75f, 1.3f, 0f, 0f, 0f, 0.025f, false,
            p => 0.55f + 0.45f * (1f - ForestNoise(p)), packRelative: true, scaleMin: 1f, scaleMax: 1.6f);

        // Flowers in meadow patches.
        ScatterSmall(parent, Flowers, FlowerCount, GroundCoverRadius + 30f, 0.8f, 1.2f, 0f, 0f, 0f, 0.025f, false,
            p =>
            {
                float patch = Mathf.PerlinNoise(p.x * 0.035f + 41.2f, p.z * 0.035f + 17.9f);
                float meadow = ForestNoise(p) < 0.48f ? 1f : 0.25f;
                return meadow * Mathf.InverseLerp(0.45f, 0.7f, patch);
            }, packRelative: true, scaleMin: 0.9f, scaleMax: 1.4f);

        // Leafy plants under and around the trees.
        ScatterSmall(parent, Plants, PlantCount, 280f, 0.75f, 2f, 0f, 0f, 0f, 0.015f, false,
            p => Mathf.InverseLerp(0.38f, 0.6f, ForestNoise(p)), packRelative: true, scaleMin: 0.9f, scaleMax: 1.5f);
    }

    static void ScatterSmall(Transform parent, string[] names, int target, float radius, float minNormalY,
                             float spacing, float heightMin, float heightMax, float sinkFraction, float cullSize,
                             bool castShadows, System.Func<Vector3, float> density, bool packRelative = false,
                             float scaleMin = 1f, float scaleMax = 1f)
    {
        int count = 0;
        for (int attempt = 0; attempt < target * 10 && count < target; attempt++)
        {
            if (!Candidate(radius, CampClearRadius * 0.5f, minNormalY, 1.5f, out Vector3 p, out Vector3 normal))
                continue;
            if (Rand() > density(p) || !coverGrid.Free(p, spacing))
                continue;

            string name = Pick(names);
            if (!TryNative(name, out Native native))
                return;

            Quaternion rot = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.5f)) *
                             Quaternion.Euler(0f, Range(0f, 360f), 0f);
            GameObject go;
            if (packRelative)
            {
                float scale = packScale * Range(scaleMin, scaleMax);
                Vector3 pivot = p - Vector3.up * (native.bounds.min.y * scale + 0.03f);
                go = Spawn(name, parent, pivot, rot, scale);
            }
            else
            {
                float height = Range(heightMin, heightMax);
                go = SpawnScaled(parent, name, p, rot, height, height * sinkFraction);
            }

            if (go == null)
                continue;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (!castShadows)
            {
                foreach (Renderer r in renderers)
                    r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Fade out once tiny on screen, so thousands of grass tufts stay cheap.
            LODGroup lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(cullSize, renderers) });
            lod.RecalculateBounds();

            MarkStatic(go);
            coverGrid.Add(p);
            count++;
        }
    }

    static void ScatterClouds(Transform parent)
    {
        for (int i = 0; i < CloudCount; i++)
        {
            string name = Pick(Clouds);
            if (!TryNative(name, out Native native))
                return;

            float angle = Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Rand()) * 1400f;
            Vector3 p = camp + new Vector3(Mathf.Cos(angle) * r, Range(170f, 280f), Mathf.Sin(angle) * r);
            float width = Mathf.Max(0.01f, Mathf.Max(native.bounds.size.x, native.bounds.size.z));
            float scale = Range(45f, 90f) / width;

            GameObject cloud = Spawn(name, parent, p, Quaternion.Euler(0f, Range(0f, 360f), 0f), scale);
            if (cloud == null)
                continue;
            foreach (Renderer rend in cloud.GetComponentsInChildren<Renderer>())
            {
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }
    }

    // ------------------------------------------------------------------ placement helpers

    // Random ground point in one of the active zones that passes the slope, water and keep-out
    // checks. maxRadius is given for the camp zone; other zones scale by the same fraction, and
    // minRadius (the camp clearing) only applies around the camp.
    static bool Candidate(float maxRadius, float minRadius, float minNormalY, float clearance,
                          out Vector3 point, out Vector3 normal)
    {
        List<Vector4> zones = activeZones;
        Vector3 center = camp;
        float rMax = maxRadius;
        float rMin = minRadius;
        if (zones != null && zones.Count > 0)
        {
            float fraction = maxRadius / Mathf.Max(1f, zones[0].w);
            float total = 0f;
            foreach (Vector4 z in zones)
                total += z.w * z.w;
            float pick = Rand() * total;
            for (int i = 0; i < zones.Count; i++)
            {
                pick -= zones[i].w * zones[i].w;
                if (pick <= 0f || i == zones.Count - 1)
                {
                    center = new Vector3(zones[i].x, 0f, zones[i].z);
                    rMax = zones[i].w * fraction;
                    rMin = i == 0 ? minRadius : 0f;
                    break;
                }
            }
        }

        float angle = Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Mathf.Lerp(rMin * rMin, rMax * rMax, Rand()));
        Vector3 p = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

        if (!OnTerrain(p, out point, out normal))
            return false;
        if (normal.y < minNormalY || NearWater(point) || Blocked(point, clearance) || OnPath(point, clearance))
            return false;
        return true;
    }

    static bool OnPath(Vector3 p, float clearance)
    {
        return RoadDistance(p) < PathHalfWidth + clearance;
    }

    static bool OnTerrain(Vector3 p, out Vector3 point, out Vector3 normal)
    {
        if (!WorldGround.TryGetGround(p, out point, out normal))
            return false;
        // TryGetGround clamps to the terrain edge; reject points that were clamped.
        return Mathf.Abs(point.x - p.x) < 0.01f && Mathf.Abs(point.z - p.z) < 0.01f;
    }

    // Same test as WaterBodies.SurfaceAt, but against lists cached once per build
    // (outside Play mode WaterBodies searches the whole scene on every call).
    static bool NearWater(Vector3 p)
    {
        float ground = WaterBodies.GroundY(p);
        if (lakes != null)
        {
            foreach (LakeWater lake in lakes)
            {
                if (lake != null && lake.Inside(p, -ShoreBuffer) && ground <= lake.SurfaceY + 1.5f)
                    return true;
            }
        }

        if (rivers != null)
        {
            foreach (RiverWater river in rivers)
            {
                if (river != null && river.Sample(p, -ShoreBuffer, out float surfaceY, out _) &&
                    ground <= surfaceY + 1.5f)
                    return true;
            }
        }

        return false;
    }

    static bool Blocked(Vector3 p, float radius)
    {
        foreach (Bounds b in keepOut)
        {
            if (p.x > b.min.x - radius && p.x < b.max.x + radius &&
                p.z > b.min.z - radius && p.z < b.max.z + radius)
                return true;
        }

        foreach (Vector3 c in keepOutCircles)
        {
            float dx = p.x - c.x;
            float dz = p.z - c.y;
            float rr = c.z + radius;
            if (dx * dx + dz * dz < rr * rr)
                return true;
        }

        return false;
    }

    static Vector3 UphillAt(Vector3 p)
    {
        WorldGround.TryGetGround(p, out _, out Vector3 normal);
        Vector3 uphill = new Vector3(-normal.x, 0f, -normal.z);
        if (uphill.sqrMagnitude < 0.0001f)
            uphill = Vector3.forward;
        return uphill.normalized;
    }

    static float ForestNoise(Vector3 p)
    {
        float a = Mathf.PerlinNoise(p.x * 0.0065f + 13.7f, p.z * 0.0065f + 71.3f);
        float b = Mathf.PerlinNoise(p.x * 0.021f + 5.1f, p.z * 0.021f + 2.9f);
        return a * 0.75f + b * 0.25f;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    // ------------------------------------------------------------------ spawning

    // Spawns a prefab scaled so it is `height` metres tall, with its base `sink` metres below ground.
    static GameObject SpawnScaled(Transform parent, string name, Vector3 ground, Quaternion rot, float height, float sink)
    {
        if (!TryNative(name, out Native native) || native.bounds.size.y < 0.001f)
            return null;

        float scale = height / native.bounds.size.y;
        Vector3 pivot = ground - rot * (Vector3.up * (native.bounds.min.y * scale)) - Vector3.up * sink;
        return Spawn(name, parent, pivot, rot, scale);
    }

    static GameObject Spawn(string name, Transform parent, Vector3 position, Quaternion rotation, float scale)
    {
        GameObject prefab = Prefab(name);
        if (prefab == null)
            return null;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = prefab.transform.localScale * scale;
        placed++;
        return go;
    }

    static void AddCollider(GameObject go, Native native, ColliderKind kind)
    {
        Bounds b = native.bounds; // prefab-local, so it matches the instance's local space
        switch (kind)
        {
            case ColliderKind.Box:
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size * 0.9f;
                break;
            }
            case ColliderKind.Mesh:
            {
                foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;
                    MeshCollider mc = filter.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = filter.sharedMesh;
                }
                break;
            }
            case ColliderKind.Trunk:
            {
                float scale = Mathf.Max(0.0001f, go.transform.lossyScale.y);
                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = 0.35f / scale;
                capsule.height = b.size.y * 0.6f;
                capsule.center = new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z);
                break;
            }
        }
    }

    static void MarkStatic(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    static GameObject Prefab(string name)
    {
        if (prefabCache.TryGetValue(name, out GameObject cached))
            return cached;

        if (prefabPaths == null)
        {
            prefabPaths = new Dictionary<string, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                prefabPaths[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }

        GameObject prefab = null;
        if (prefabPaths.TryGetValue(name, out string found))
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(found);
        if (prefab == null)
        {
            Debug.LogWarning(Log + "Missing prefab " + name + " under " + PrefabFolder);
            missing++;
        }

        prefabCache[name] = prefab;
        return prefab;
    }

    // Bounds of the prefab's meshes in its own root space, at scale 1.
    static bool TryNative(string name, out Native native)
    {
        if (nativeCache.TryGetValue(name, out native))
            return true;

        GameObject prefab = Prefab(name);
        if (prefab == null)
            return false;

        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        bool any = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;

            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            Bounds mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        if (!any)
            return false;

        native = new Native { bounds = bounds };
        nativeCache[name] = native;
        return true;
    }

    static float Rand()
    {
        return (float)rng.NextDouble();
    }

    static float Range(float min, float max)
    {
        return min + (max - min) * Rand();
    }

    static string Pick(string[] names)
    {
        return names[rng.Next(names.Length)];
    }

    // ------------------------------------------------------------------ views

    static void FrameAt(Vector3 pivot)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
            return;
        view.orthographic = false;
        view.LookAt(pivot + Vector3.up * 8f, Quaternion.Euler(22f, 145f, 0f), 70f);
        view.Repaint();
    }

    // Renders a handful of views to PNG so the result can be checked without opening the editor.
    static void CaptureViews(string folder)
    {
        Directory.CreateDirectory(folder);
        ShaderUtil.allowAsyncCompilation = false;

        List<(string name, Vector3 eye, Vector3 look)> views = new List<(string, Vector3, Vector3)>();
        Vector3 across = Vector3.Cross(Vector3.up, UphillAt(camp)).normalized;
        views.Add(("camp", Eye(camp + across * 18f, 4f), camp + Vector3.up * 1.5f));
        views.Add(("camp_overhead", Eye(camp - UphillAt(camp) * 40f, 35f), camp));
        // Every settlement from eye level at its entrance, and from above.
        foreach (Settlement s in Object.FindObjectsByType<Settlement>(FindObjectsInactive.Exclude))
        {
            string key = s.title.Replace(" ", "_").ToLowerInvariant();
            Vector3 toCamp = Flat(camp - s.center).sqrMagnitude > 1f ? Flat(camp - s.center).normalized : Vector3.forward;
            views.Add((key, Eye(s.center + toCamp * (s.ringRadius + 14f), 2.2f), s.center + Vector3.up * 2f));
            views.Add((key + "_overhead", Eye(s.center + toCamp * 30f, 55f), s.center));
            if (s.doors.Length > 0)
            {
                Vector3 d = s.doors[0];
                Vector3 side = Vector3.Cross(Vector3.up, Flat(d - s.center).normalized);
                views.Add((key + "_people", Eye(d + side * 6f, 1.7f), d + Vector3.up * 1.2f));
            }
        }

        // The castle from the approach road, from the side, and from above.
        GameObject stronghold = GameObject.Find(RpgppScenery.RootName + "/Fortress/Stronghold");
        if (stronghold != null && TryRenderBounds(stronghold, out Bounds cb))
        {
            Vector3 c = new Vector3(cb.center.x, cb.min.y, cb.center.z);
            Vector3 fwd = stronghold.transform.forward;
            Vector3 right = stronghold.transform.right;
            float reach = cb.extents.magnitude;
            views.Add(("castle_front", Eye(c + fwd * (reach + 30f), 4f), c + Vector3.up * 8f));
            views.Add(("castle_gate", Eye(c + fwd * (cb.extents.z + 8f), 1.7f), c + fwd * cb.extents.z + Vector3.up * 3f));
            views.Add(("castle_side", Eye(c + (right - fwd * 0.4f).normalized * (reach + 35f), 10f), c + Vector3.up * 6f));
            views.Add(("castle_overhead", c + fwd * (reach + 15f) + Vector3.up * (reach + 50f), c));
            views.Add(("castle_courtyard", Eye(c + fwd * (cb.extents.z - 6f), 1.7f), c - fwd * 8f + Vector3.up * 4f));
        }

        views.Add(("aerial", camp + new Vector3(-350f, 380f, -350f), camp));

        GameObject camGo = new GameObject("_RpgppCaptureCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 4000f;
        RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        Texture2D tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);

        try
        {
            foreach (var view in views)
            {
                camGo.transform.position = view.eye;
                camGo.transform.rotation = Quaternion.LookRotation(view.look - view.eye, Vector3.up);
                cam.Render();
                cam.Render(); // second pass after any shaders finished compiling
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(folder, view.name + ".png"), tex.EncodeToPNG());
                Debug.Log(Log + "Captured " + view.name + " from " + view.eye);
            }
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }
    }

    static Vector3 Eye(Vector3 at, float height)
    {
        return WorldGround.Snap(at) + Vector3.up * height;
    }

    // Keeps objects of one layer a minimum distance apart.
    class SpacingGrid
    {
        readonly float cell;
        readonly Dictionary<long, List<Vector2>> cells = new Dictionary<long, List<Vector2>>();

        public SpacingGrid(float cell)
        {
            this.cell = cell;
        }

        long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        public bool Free(Vector3 p, float minDistance)
        {
            int cx = Mathf.FloorToInt(p.x / cell);
            int cz = Mathf.FloorToInt(p.z / cell);
            int reach = Mathf.CeilToInt(minDistance / cell);
            float sq = minDistance * minDistance;
            for (int x = cx - reach; x <= cx + reach; x++)
            {
                for (int z = cz - reach; z <= cz + reach; z++)
                {
                    if (!cells.TryGetValue(Key(x, z), out List<Vector2> list))
                        continue;
                    foreach (Vector2 q in list)
                    {
                        float dx = q.x - p.x;
                        float dz = q.y - p.z;
                        if (dx * dx + dz * dz < sq)
                            return false;
                    }
                }
            }

            return true;
        }

        public void Add(Vector3 p)
        {
            long key = Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));
            if (!cells.TryGetValue(key, out List<Vector2> list))
            {
                list = new List<Vector2>();
                cells[key] = list;
            }

            list.Add(new Vector2(p.x, p.z));
        }
    }
}

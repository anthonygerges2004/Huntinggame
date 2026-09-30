using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ApplyRealisticTerrain
{
    public const string RootName = "_RealisticTerrain";
    const string FoliageName = "_HuntFoliage";
    const string PrefsKey = "HuntingGame.RealisticTerrainVersion";
    public const int LayoutVersion = 10;

    static readonly string[] TerrainAssets =
    {
        "Assets/Realistic Terrain Collection/Terrains/Terrain1/Medium Resolution_1.asset",
        "Assets/Realistic Terrain Collection/Terrains/Terrain2/Low Resolution_2.asset",
        "Assets/Realistic Terrain Collection/Terrains/Terrain3/Low Resolution_3.asset",
        "Assets/Realistic Terrain Collection/Terrains/Terrain4/Low Resolution_4.asset",
        "Assets/Realistic Terrain Collection/Terrains/Terrain5/Low Resolution_5.asset"
    };

    static readonly string[] LowPolyTreePrefabs =
    {
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Tree_02.prefab",
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Tree_10.prefab",
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Birch_Tree_05.prefab",
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Birch_Tree_06.prefab"
    };

    static readonly string[] GrassPrefabs =
    {
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_11.prefab",
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_15.prefab",
        "Assets/URP GanzSe Free Camping Props/Prefabs/FCP_GrassCluster_Type1_Color1.prefab"
    };

    static readonly string[] BushPrefabs =
    {
        "Assets/URP GanzSe Free Camping Props/Prefabs/FCP__Bush_Type1_Color1.prefab"
    };

    static readonly string[] RockPrefabs =
    {
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Rock_Moss_Grown_09.prefab",
        "Assets/URP GanzSe Free Camping Props/Prefabs/FCP_RockCluster_Type1_Color1.prefab"
    };

    static bool building;

    [InitializeOnLoadMethod]
    static void AutoApply()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += TryAutoApply;
        };
    }

    static void TryAutoApply()
    {
        if (building || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!PackIsImported())
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "GetStarted_Scene")
            return;

        bool hasRoot = GameObject.Find(RootName) != null;
        bool hasFoliage = GameObject.Find(FoliageName) != null;
        int applied = EditorPrefs.GetInt(PrefsKey, 0);

        if (Object.FindAnyObjectByType<Terrain>() != null)
        {
            FixAllTerrains();
            EnsureGameObjectForest();
        }

        if (hasRoot && hasFoliage && applied >= LayoutVersion)
            return;

        Build(false);
    }

    [MenuItem("Tools/Hunting Game/Fix Pink Trees")]
    public static void FixPinkTreesFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Trees",
                "Press Stop first, then run this again.",
                "OK");
            return;
        }

        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
        {
            EditorUtility.DisplayDialog("Trees", "Open GetStarted_Scene first.", "OK");
            return;
        }

        FixAllTerrains();
        EnsureGameObjectForest();
        ReplacePlaceholderTrees();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        FrameCamp();
        EditorUtility.DisplayDialog(
            "Trees",
            "Trees refreshed.\nIf Conifers [BOTD] is imported, those realistic pines are used.",
            "OK");
    }

    [MenuItem("Tools/Hunting Game/Apply Realistic Terrain")]
    public static void ApplyFromMenu()
    {
        if (!PackIsImported())
        {
            EditorUtility.DisplayDialog(
                "Terrain",
                "Assets/Realistic Terrain Collection is missing. Import that pack first.",
                "OK");
            return;
        }

        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
            EditorSceneManager.OpenScene("Assets/Scenes/GetStarted_Scene.unity");

        Build(true);
        EditorUtility.DisplayDialog(
            "Terrain",
            "Trees and grass are placed on the mountains, like in the video.\n\nThe pink billboard trees are gone. Press Play.",
            "OK");
    }

    public static bool PackIsImported()
    {
        return System.IO.File.Exists(TerrainAssets[0]) ||
               System.IO.File.Exists(
                   "Assets/Realistic Terrain Collection/Terrains/Terrain1/Low Resolution_1.asset");
    }

    public static void Build(bool save)
    {
        if (building)
            return;

        Scene hunting = SceneManager.GetActiveScene();
        if (!hunting.IsValid() || hunting.name != "GetStarted_Scene")
            return;

        building = true;
        try
        {
            DestroyIfFound(RootName);
            DestroyIfFound(FoliageName);
            HideNamed("Ground_1");
            HideNamed("_NatureGround");
            HideNamed("_NatureForest");
            HideNamed("_GeneratedIslandLayout");

            GameObject root = new GameObject(RootName);
            List<Terrain> tiles = new List<Terrain>();

            for (int i = 0; i < TerrainAssets.Length; i++)
            {
                string path = TerrainAssets[i];
                if (!System.IO.File.Exists(path))
                    path = path.Replace("Medium Resolution", "Low Resolution");

                TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (data == null)
                {
                    Debug.LogError("Missing terrain asset: " + path);
                    continue;
                }

                GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
                terrainObject.name = "Terrain" + (i + 1);
                terrainObject.transform.SetParent(root.transform, true);
                terrainObject.transform.position = new Vector3(i * 3000f, 0f, 0f);
                MakeUrpReady(terrainObject.GetComponent<Terrain>());
                tiles.Add(terrainObject.GetComponent<Terrain>());
            }

            if (tiles.Count == 0)
            {
                Debug.LogError("No realistic terrain was created.");
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                Terrain left = i > 0 ? tiles[i - 1] : null;
                Terrain right = i < tiles.Count - 1 ? tiles[i + 1] : null;
                tiles[i].SetNeighbors(left, null, right, null);
            }

            Terrain first = tiles[0];
            Vector3 spawn = FindCampSpot(first);
            Move("Player", spawn + Vector3.up * 1.2f);
            Move("Campfire", spawn);
            Move("FCP_Campfire_Type1_Color1", spawn);
            Move("FCP_Tent_Type1_Color1", spawn + new Vector3(6f, 0f, -4f));
            Move("FCP_Bench_Type1_Color1", spawn + new Vector3(-5f, 0f, 3f));
            Move("FCP_Bench_Type1_Color1_1", spawn + new Vector3(4f, 0f, 5f));
            Move("FCP_FlagBunting_Type1_Color1", spawn + new Vector3(-4f, 0f, -5f));
            Move("FCP_FlagBunting_Type1_Color1_1", spawn + new Vector3(5f, 0f, 2f));

            PlaceAnimalsAround(first, spawn);
            PlaceExistingTreesAround(spawn);
            ScatterFoliage(spawn);
            ApplyAtmosphere();
            StretchViewDistance();
            FixAllTerrains();
            ReplacePlaceholderTrees();
            FrameAt(spawn);

            EditorPrefs.SetInt(PrefsKey, LayoutVersion);
            EditorSceneManager.MarkSceneDirty(hunting);
            if (save)
                EditorSceneManager.SaveScene(hunting);

            Debug.Log("Realistic Terrain Collection Lite is in the hunting scene (5 terrains + trees/grass).");
        }
        finally
        {
            building = false;
        }
    }

    static Vector3 FindCampSpot(Terrain terrain)
    {
        Vector3 best = SpawnOn(terrain, 1111f, 1111f);
        if (WorldGround.IsMeadow(best))
            return best;

        for (int i = 0; i < 90; i++)
        {
            float x = 180f + (i * 41f) % 2100f;
            float z = 180f + (i * 67f) % 2100f;
            Vector3 candidate = SpawnOn(terrain, x, z);
            if (WorldGround.IsMeadow(candidate))
                return candidate;
        }

        return best;
    }

    static Vector3 SpawnOn(Terrain terrain, float x, float z)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 world = new Vector3(origin.x + x, 0f, origin.z + z);
        world.y = terrain.SampleHeight(world) + origin.y;
        return world;
    }

    static void PlaceAnimalsAround(Terrain terrain, Vector3 camp)
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            float angle = i * 0.9f;
            float radius = 28f + (i % 7) * 9f;
            Vector3 pos = camp + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            animals[i].transform.position = pos;
        }
    }

    static void PlaceExistingTreesAround(Vector3 camp)
    {
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        int n = 0;
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i].GetComponent<Animal>() != null)
                continue;
            if (trees[i].transform.root.name == FoliageName)
                continue;

            float angle = n * 137.508f * Mathf.Deg2Rad;
            float radius = 20f + (n % 6) * 8f;
            Vector3 pos = camp + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            trees[i].transform.position = WorldGround.Snap(pos);
            n++;
        }
    }

    static void ScatterFoliage(Vector3 camp)
    {
        GameObject root = new GameObject(FoliageName);
        string[] trees = ResolveTreePrefabs();
        if (UsesConiferPack(trees))
        {
            GameObject marker = new GameObject("ConiferForest");
            marker.transform.SetParent(root.transform, false);
            ScatterPrefabs(root.transform, camp, trees, 120, 28f, 560f, 0.9f, 1.2f, true);
        }
        else
            ScatterPrefabs(root.transform, camp, trees, 520, 16f, 420f, 0.85f, 1.55f, true);
        ScatterPrefabs(root.transform, camp, GrassPrefabs, 240, 4f, 220f, 0.75f, 1.45f, false);
        ScatterPrefabs(root.transform, camp, BushPrefabs, 40, 10f, 160f, 0.9f, 1.35f, false);
        ScatterPrefabs(root.transform, camp, RockPrefabs, 24, 18f, 240f, 0.7f, 1.4f, false);
    }

    static void ScatterPrefabs(
        Transform parent,
        Vector3 camp,
        string[] paths,
        int count,
        float minRadius,
        float maxRadius,
        float minScale,
        float maxScale,
        bool harvestable)
    {
        List<GameObject> prefabs = new List<GameObject>();
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab != null)
                prefabs.Add(prefab);
        }

        if (prefabs.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle = i * 137.508f * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(minRadius, maxRadius, (i % 11) / 10f);
            Vector3 pos = camp + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            pos = WorldGround.Snap(pos);

            GameObject prefab = prefabs[i % prefabs.Count];
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, true);
            instance.transform.position = pos;
            instance.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
            instance.transform.localScale *= Mathf.Lerp(minScale, maxScale, (i % 5) / 4f);

            if (harvestable && instance.GetComponent<HarvestableTree>() == null)
                instance.AddComponent<HarvestableTree>();

            MeshRenderer renderer = instance.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
                renderer.forceMeshLod = 0;
        }
    }

    static void MakeUrpReady(Terrain terrain)
    {
        if (terrain == null)
            return;

        terrain.heightmapPixelError = 5f;
        terrain.basemapDistance = 2000f;
        terrain.treeDistance = 0f;
        terrain.detailObjectDistance = 180f;
        terrain.drawInstanced = false;
        terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
        terrain.materialTemplate = GetUrpTerrainMaterial();
        EnsureLayers(terrain);
        FixTerrainTrees(terrain);
    }

    static void FixAllTerrains()
    {
        Material mat = GetUrpTerrainMaterial();
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null)
                continue;
            terrains[i].drawInstanced = false;
            terrains[i].materialTemplate = mat;
            EnsureLayers(terrains[i]);
            FixTerrainTrees(terrains[i]);
            EditorUtility.SetDirty(terrains[i]);
        }

        SceneView.RepaintAll();
    }

    static Material GetUrpTerrainMaterial()
    {
        const string path = "Assets/Materials/Hunt_URP_TerrainLit.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");

        if (mat == null)
        {
            if (shader == null)
            {
                Debug.LogError("URP Terrain Lit shader was not found.");
                return null;
            }

            mat = new Material(shader);
            mat.name = "Hunt_URP_TerrainLit";
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null && mat.shader != shader)
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    static void EnsureLayers(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        if (data == null)
            return;

        TerrainLayer[] layers = data.terrainLayers;
        bool broken = layers == null || layers.Length == 0;
        if (!broken)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null || layers[i].diffuseTexture == null)
                {
                    broken = true;
                    break;
                }
            }
        }

        if (!broken)
            return;

        TerrainLayer grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/_TerrainAutoUpgrade/layer_grass_strawgrass_straw_normal18c7f28bcf2878ea.terrainlayer");
        TerrainLayer floor = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/_TerrainAutoUpgrade/layer_forest_floorforest_floor_normal18c7f28bcf2878ea.terrainlayer");
        TerrainLayer rock = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/_TerrainAutoUpgrade/layer_solid_rocksolid_rock_normal312cdf1c675cb011.terrainlayer");
        TerrainLayer rock2 = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/_TerrainAutoUpgrade/layer_solid_rock_2solid_rock_2_normal312cdf1c675cb011.terrainlayer");

        if (grass == null || floor == null || rock == null || rock2 == null)
            return;

        Undo.RecordObject(data, "Assign URP terrain layers");
        data.terrainLayers = new[] { grass, floor, rock, rock2 };
        if (data.alphamapLayers == 0)
            PaintBySlope(data);
        EditorUtility.SetDirty(data);
    }

    static void PaintBySlope(TerrainData data)
    {
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        float[,,] map = new float[h, w, 4];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = w <= 1 ? 0f : x / (float)(w - 1);
                float nz = h <= 1 ? 0f : y / (float)(h - 1);
                float steep = 1f - data.GetInterpolatedNormal(nx, nz).y;
                float height = data.size.y <= 0.01f ? 0f : data.GetInterpolatedHeight(nx, nz) / data.size.y;
                float rockAmt = Mathf.Clamp01(steep * 2.4f + Mathf.InverseLerp(0.32f, 0.72f, height));
                float grassAmt = Mathf.Clamp01((1f - rockAmt) * (1.15f - height));
                float forestAmt = Mathf.Clamp01(1f - rockAmt - grassAmt);
                float sum = grassAmt + forestAmt + rockAmt;
                if (sum < 0.001f)
                    sum = 1f;

                map[y, x, 0] = grassAmt / sum;
                map[y, x, 1] = forestAmt / sum;
                map[y, x, 2] = rockAmt * 0.65f / sum;
                map[y, x, 3] = rockAmt * 0.35f / sum;
            }
        }

        data.SetAlphamaps(0, 0, map);
    }

    static void FixTerrainTrees(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        if (data == null)
            return;

        Undo.RecordObject(data, "Remove magenta terrain billboards");
        data.SetTreeInstances(new TreeInstance[0], true);
        data.treePrototypes = new TreePrototype[0];
        if (data.detailPrototypes != null && data.detailPrototypes.Length > 0)
        {
            int layers = data.detailPrototypes.Length;
            for (int i = 0; i < layers; i++)
            {
                int w = data.detailWidth;
                int h = data.detailHeight;
                if (w > 0 && h > 0)
                    data.SetDetailLayer(0, 0, i, new int[h, w]);
            }

            data.detailPrototypes = new DetailPrototype[0];
        }

        terrain.treeDistance = 0f;
        terrain.detailObjectDistance = 0f;
        terrain.Flush();
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);
    }

    static void EnsureGameObjectForest()
    {
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
            return;

        string[] trees = ResolveTreePrefabs();
        bool conifers = UsesConiferPack(trees);
        GameObject existing = GameObject.Find(FoliageName);
        if (existing != null && conifers && ForestHasConifers(existing))
        {
            ForceFullMeshLod();
            return;
        }

        if (existing != null && !conifers)
        {
            ForceFullMeshLod();
            return;
        }

        if (!conifers)
            ReplacePlaceholderTrees();
        ForceFullMeshLod();

        if (existing != null)
            Object.DestroyImmediate(existing);

        ScatterFoliage(FindCampWorld(terrain));
        ForceFullMeshLod();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static bool ForestHasConifers(GameObject root)
    {
        for (int i = 0; i < root.transform.childCount; i++)
        {
            string name = root.transform.GetChild(i).name;
            if (name.IndexOf("Conifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static bool UsesConiferPack(string[] paths)
    {
        if (paths == null)
            return false;
        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i] != null &&
                paths[i].IndexOf("Conifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static string[] ResolveTreePrefabs()
    {
        string[] conifers = FindConiferPrefabPaths();
        if (conifers.Length > 0)
            return conifers;
        return LowPolyTreePrefabs;
    }

    static string[] FindConiferPrefabPaths()
    {
        var matches = new List<string>();
        if (!System.IO.Directory.Exists("Assets"))
            return new string[0];

        string[] folders = System.IO.Directory.GetDirectories("Assets", "*", System.IO.SearchOption.AllDirectories);
        for (int i = 0; i < folders.Length; i++)
        {
            string folder = folders[i].Replace('\\', '/');
            if (folder.IndexOf("Conifer", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf("Billboard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (file.IndexOf("Mobile", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (file.IndexOf("Atlas", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (!matches.Contains(path))
                    matches.Add(path);
            }
        }

        matches.Sort((a, b) => PrefabHeight(b).CompareTo(PrefabHeight(a)));
        var tall = new List<string>();
        for (int i = 0; i < matches.Count && tall.Count < 4; i++)
        {
            if (PrefabHeight(matches[i]) < 4f)
                continue;
            tall.Add(matches[i]);
        }

        return tall.ToArray();
    }

    static float PrefabHeight(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return 0f;

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        float height = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                height = Mathf.Max(height, renderers[i].bounds.size.y);
        }

        return height;
    }

    static void ForceFullMeshLod()
    {
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].forceMeshLod = 0;
        }
    }

    static void ReplacePlaceholderTrees()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LowPolyTreePrefabs[0]);
        if (prefab == null)
            return;

        MeshFilter goodFilter = prefab.GetComponentInChildren<MeshFilter>();
        MeshRenderer goodRenderer = prefab.GetComponentInChildren<MeshRenderer>();
        if (goodFilter == null || goodRenderer == null)
            return;

        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        for (int i = 0; i < trees.Length; i++)
        {
            HarvestableTree tree = trees[i];
            if (tree == null || tree.GetComponent<Animal>() != null)
                continue;

            MeshFilter filter = tree.GetComponent<MeshFilter>();
            MeshRenderer renderer = tree.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
                continue;
            if (filter.sharedMesh == goodFilter.sharedMesh)
                continue;

            Undo.RecordObject(filter, "Swap tree mesh");
            Undo.RecordObject(renderer, "Swap tree material");
            filter.sharedMesh = goodFilter.sharedMesh;
            renderer.sharedMaterials = goodRenderer.sharedMaterials;
        }
    }

    static Vector3 FindCampWorld(Terrain terrain)
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
            return player.transform.position;
        if (terrain != null)
            return SpawnOn(terrain, 1111f, 1111f);
        return new Vector3(1111f, 0f, 1111f);
    }

    static void ApplyAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0016f;
        RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.68f);

        Material sky = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Realistic Terrain Collection/Other/Skybox_Textures/DawnDusk/Skybox.mat");
        if (sky != null)
            RenderSettings.skybox = sky;
    }

    static void StretchViewDistance()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return;
        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.farClipPlane = 8000f;
            cam.nearClipPlane = 0.1f;
        }
    }

    static void FrameCamp()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
            FrameAt(player.transform.position);
    }

    static void FrameAt(Vector3 pivot)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        sceneView.orthographic = false;
        sceneView.LookAt(pivot + Vector3.up * 6f, Quaternion.Euler(18f, 145f, 0f), 55f);
        sceneView.Repaint();
    }

    static void Move(string name, Vector3 worldPosition)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            return;
        go.transform.position = WorldGround.Snap(worldPosition);
    }

    static void HideNamed(string name)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject go = objects[i];
            if (go == null || go.name != name || !go.scene.IsValid())
                continue;
            go.SetActive(false);
        }
    }

    static void DestroyIfFound(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
            Object.DestroyImmediate(go);
    }
}

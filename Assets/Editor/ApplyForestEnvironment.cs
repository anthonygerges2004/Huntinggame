using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyForestEnvironment
{
    public const string RootName = "_NatureForest";
    public const int LayoutVersion = 8;
    const string DemoPath = "Assets/Pure Poly/Free Low Poly Nature Pack/Scenes/Demo_Terrain.unity";

    static readonly string[] KeepNames =
    {
        "Terrain", "Trees", "Water", "Mountains", "Plants", "Rocks",
        "Floor_Tiles", "Props", "Mushrooms"
    };

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
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "GetStarted_Scene")
            return;

        int applied = EditorPrefs.GetInt("HuntingGame.NatureGroundVersion", 0);
        if (applied == LayoutVersion && GameObject.Find(RootName) != null)
            return;

        if (GameObject.Find(RootName) == null)
        {
            Build();
            return;
        }

        ImproveExistingForest();
    }

    [MenuItem("Tools/Hunting Game/Apply Forest Environment")]
    public static void ApplyFromMenu()
    {
        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
        {
            EditorUtility.DisplayDialog("Forest", "Open GetStarted_Scene first.", "OK");
            return;
        }

        if (GameObject.Find(RootName) != null)
        {
            ImproveExistingForest();
            return;
        }

        Build();
    }

    [MenuItem("Tools/Hunting Game/Place Gameplay On Forest")]
    public static void PlaceFromMenu()
    {
        if (Object.FindAnyObjectByType<Terrain>() == null)
        {
            EditorUtility.DisplayDialog("Forest", "No terrain in this scene yet.", "OK");
            return;
        }

        ImproveExistingForest();
    }

    static void ImproveExistingForest()
    {
        if (ForestTreesNeedRestore())
        {
            Build();
            return;
        }

        PlaceGameplayOnTerrain();
        EditorPrefs.SetInt("HuntingGame.NatureGroundVersion", LayoutVersion);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Opened camp and hunting clearings. Animals were placed on the forest.");
    }

    static bool ForestTreesNeedRestore()
    {
        GameObject trees = GameObject.Find("Trees");
        return trees == null || trees.transform.childCount < 20;
    }

    public static void Build()
    {
        Scene hunting = SceneManager.GetActiveScene();
        if (!hunting.IsValid() || hunting.name != "GetStarted_Scene")
            return;

        ClearOldEnvironment();

        if (!System.IO.File.Exists(DemoPath))
        {
            Debug.LogError("Forest demo scene was not found: " + DemoPath);
            return;
        }

        Scene demo = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Additive);
        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, hunting);

        List<GameObject> demoRoots = new List<GameObject>(demo.GetRootGameObjects());
        foreach (GameObject go in demoRoots)
        {
            if (go == null)
                continue;
            if (!ShouldKeep(go.name))
                continue;

            go.transform.SetParent(root.transform, true);
            SceneManager.MoveGameObjectToScene(go, hunting);
        }

        EditorSceneManager.CloseScene(demo, false);

        AlignForestToIsland(root);
        HideOldGround();
        PlaceGameplayOnTerrain();

        EditorPrefs.SetInt("HuntingGame.NatureGroundVersion", LayoutVersion);
        EditorSceneManager.MarkSceneDirty(hunting);
        EditorSceneManager.SaveScene(hunting);
        Selection.activeGameObject = root;
        Debug.Log("Forest environment copied from the Nature pack demo. Animals were placed on the terrain.");
    }

    static bool ShouldKeep(string name)
    {
        for (int i = 0; i < KeepNames.Length; i++)
        {
            if (name == KeepNames[i])
                return true;
        }

        return false;
    }

    static void ClearOldEnvironment()
    {
        DestroyIfFound(RootName);
        DestroyIfFound(NatureGroundBuilder.RootName);
        DestroyLooseNaturePrefabs();

        UnparentGameplay();
        HideGeneratedVisuals();
    }

    static void DestroyIfFound(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
            Object.DestroyImmediate(go);
    }

    static void HideOldGround()
    {
        GameObject ground = GameObject.Find("Ground_1");
        if (ground == null)
            return;

        MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;

        Collider collider = ground.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
    }

    static void AlignForestToIsland(GameObject root)
    {
        Terrain terrain = root.GetComponentInChildren<Terrain>();
        if (terrain == null || terrain.terrainData == null)
            return;

        Vector3 size = terrain.terrainData.size;
        Vector3 terrainCenter = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        Vector3 target = new(0.5f, 0f, 6f);
        Vector3 delta = target - terrainCenter;
        delta.y = 0f;
        root.transform.position += delta;
    }

    static void PlaceGameplayOnTerrain()
    {
        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
            return;

        UnparentGameplay();
        HideGeneratedVisuals();

        List<Vector3> meadows = FindMeadowPoints(terrain);
        if (meadows.Count == 0)
            meadows.Add(WorldGround.Snap(new Vector3(0f, 0f, 6f)));

        Vector3 camp = PickClosestMeadow(meadows, new Vector3(18f, 0f, -12f));
        MoveNamed("Campfire", camp);
        MoveNamed("FCP_Campfire_Type1_Color1", camp);
        MoveNamed("FCP_Tent_Type1_Color1", camp + new Vector3(3.2f, 0f, -2.2f));
        MoveNamed("FCP_Bench_Type1_Color1", camp + new Vector3(-2.6f, 0f, 1.6f));
        MoveNamed("FCP_Bench_Type1_Color1_1", camp + new Vector3(1.6f, 0f, 2.6f));
        MoveNamed("FCP_FlagBunting_Type1_Color1", camp + new Vector3(-2.2f, 0f, -2.2f));
        MoveNamed("FCP_FlagBunting_Type1_Color1_1", camp + new Vector3(2.2f, 0f, 1.2f));

        Vector3 player = WorldGround.Snap(camp + new Vector3(-4f, 0f, 5f));
        player.y += 1f;
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            Undo.RecordObject(playerObject.transform, "Place player at camp");
            playerObject.transform.position = player;
        }

        PlaceAnimals(meadows, camp);
        OpenClearing(camp, 13f);
        Vector3 huntClearing = WorldGround.Snap(camp + new Vector3(-16f, 0f, 18f));
        OpenClearing(huntClearing, 9f);
        SnapGeneratedTreesOnly();
        HideMisnamedLegendaryTree();
    }

    static void PlaceAnimals(List<Vector3> meadows, Vector3 camp)
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        var used = new List<Vector3> { camp };

        for (int i = 0; i < animals.Length; i++)
        {
            Animal animal = animals[i];
            Vector3 point;

            if (animal.kind == AnimalKind.Legendary || animal.name.IndexOf("Legendary", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                point = FarthestFrom(meadows, camp);
            }
            else if (animal.kind == AnimalKind.Bird || animal.name.IndexOf("Bird", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                point = TakeMeadow(meadows, camp, 8f, 16f, used);
            }
            else if (animal.kind == AnimalKind.Large ||
                     animal.name.IndexOf("Big", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     animal.name.IndexOf("Large", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                point = TakeMeadow(meadows, camp, 18f, 80f, used);
            }
            else
            {
                point = TakeMeadow(meadows, camp, 14f, 80f, used);
            }

            used.Add(point);
            Undo.RecordObject(animal.transform, "Place animal in meadow");
            animal.transform.position = WorldGround.Snap(point);
        }
    }

    static List<Vector3> FindMeadowPoints(Terrain terrain)
    {
        var points = new List<Vector3>();
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        for (int x = 5; x <= 19; x++)
        {
            for (int z = 5; z <= 19; z++)
            {
                float wx = origin.x + size.x * (x / 24f);
                float wz = origin.z + size.z * (z / 24f);
                Vector3 point = WorldGround.Snap(new Vector3(wx, 0f, wz));
                if (WorldGround.IsMeadow(point))
                    points.Add(point);
            }
        }

        return points;
    }

    static Vector3 PickClosestMeadow(List<Vector3> meadows, Vector3 preferred)
    {
        Vector3 best = meadows[0];
        float bestDistance = float.MaxValue;
        for (int i = 0; i < meadows.Count; i++)
        {
            float distance = Vector3.Distance(Flat(meadows[i]), Flat(preferred));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = meadows[i];
            }
        }

        return WorldGround.Snap(best);
    }

    static Vector3 TakeMeadow(List<Vector3> meadows, Vector3 camp, float minFromCamp, float maxFromCamp, List<Vector3> used)
    {
        Vector3 best = FarthestFrom(meadows, camp);
        float bestScore = float.MinValue;

        for (int i = 0; i < meadows.Count; i++)
        {
            float fromCamp = Vector3.Distance(Flat(meadows[i]), Flat(camp));
            if (fromCamp < minFromCamp || fromCamp > maxFromCamp)
                continue;
            if (TooClose(meadows[i], used, 7f))
                continue;

            float score = fromCamp;
            if (score > bestScore)
            {
                bestScore = score;
                best = meadows[i];
            }
        }

        if (bestScore == float.MinValue)
        {
            for (int i = 0; i < meadows.Count; i++)
            {
                if (TooClose(meadows[i], used, 5f))
                    continue;
                float fromCamp = Vector3.Distance(Flat(meadows[i]), Flat(camp));
                if (fromCamp > bestScore)
                {
                    bestScore = fromCamp;
                    best = meadows[i];
                }
            }
        }

        return WorldGround.Snap(best);
    }

    static bool TooClose(Vector3 point, List<Vector3> used, float minDistance)
    {
        for (int i = 0; i < used.Count; i++)
        {
            if (Vector3.Distance(Flat(point), Flat(used[i])) < minDistance)
                return true;
        }

        return false;
    }

    static Vector3 FarthestFrom(List<Vector3> meadows, Vector3 from)
    {
        Vector3 best = meadows[0];
        float bestDistance = -1f;
        for (int i = 0; i < meadows.Count; i++)
        {
            float distance = Vector3.Distance(Flat(meadows[i]), Flat(from));
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = meadows[i];
            }
        }

        return best;
    }

    static void UnparentGameplay()
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Include);
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i] == null || animals[i].transform.parent == null)
                continue;
            Undo.SetTransformParent(animals[i].transform, null, "Unparent animal");
        }

        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Include);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null || trees[i].transform.parent == null)
                continue;
            if (trees[i].GetComponent<Animal>() != null)
                continue;
            if (!IsGeneratedTree(trees[i].gameObject))
                continue;
            Undo.SetTransformParent(trees[i].transform, null, "Unparent tree");
        }
    }

    static bool IsGeneratedTree(GameObject go)
    {
        return go.name.IndexOf("Tree_Generated", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void DestroyLooseNaturePrefabs()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;
            if (!roots[i].name.StartsWith("PP_"))
                continue;
            Object.DestroyImmediate(roots[i]);
        }
    }

    static void OpenClearing(Vector3 center, float radius)
    {
        HideGroupChildrenNear("Trees", center, radius);
        HideGroupChildrenNear("Plants", center, radius);
        HideGroupChildrenNear("Rocks", center, radius);
        HideGroupChildrenNear("Mushrooms", center, radius);
        HideGroupChildrenNear("Props", center, radius);
    }

    static void HideGroupChildrenNear(string groupName, Vector3 center, float radius)
    {
        GameObject group = GameObject.Find(groupName);
        if (group == null)
            return;

        List<Transform> children = new List<Transform>();
        for (int i = 0; i < group.transform.childCount; i++)
            children.Add(group.transform.GetChild(i));

        for (int i = 0; i < children.Count; i++)
        {
            if (Vector3.Distance(Flat(children[i].position), Flat(center)) >= radius)
                continue;
            Undo.RecordObject(children[i].gameObject, "Open forest clearing");
            children[i].gameObject.SetActive(false);
        }
    }

    static void SnapGeneratedTreesOnly()
    {
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null)
                continue;
            if (!IsGeneratedTree(trees[i].gameObject))
                continue;

            Undo.RecordObject(trees[i].transform, "Snap generated tree to terrain");
            trees[i].transform.position = WorldGround.Snap(trees[i].transform.position);
        }
    }

    static void HideGeneratedVisuals()
    {
        GameObject generated = GameObject.Find("_GeneratedIslandLayout");
        if (generated != null)
            generated.SetActive(false);
    }

    static void HideMisnamedLegendaryTree()
    {
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null || trees[i].GetComponent<Animal>() != null)
                continue;
            if (trees[i].name.IndexOf("Legendary", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Undo.RecordObject(trees[i].gameObject, "Hide misnamed legendary tree");
            trees[i].gameObject.SetActive(false);
        }
    }

    static void MoveNamed(string name, Vector3 worldPosition)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
            return;

        Undo.RecordObject(go.transform, "Place on forest terrain");
        go.transform.position = WorldGround.Snap(worldPosition);
    }

    static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}

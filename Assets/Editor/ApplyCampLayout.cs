using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyCampLayout
{
    private const string FillRootName = "_GeneratedIslandLayout";
    private static readonly Vector3 CampCenter = new(18f, 0f, -18f);

    [InitializeOnLoadMethod]
    private static void BuildAutomaticallyOnce()
    {
        EditorApplication.delayCall += TryBuildAutomatically;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryBuildAutomatically;
        };
    }

    private static void TryBuildAutomatically()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
            return;
        if (GameObject.Find(FillRootName) != null)
            return;

        BuildLayout();
    }

    [MenuItem("Tools/Hunting Game/Build Final Island Layout")]
    public static void BuildFinalLayout()
    {
        if (!EditorUtility.DisplayDialog(
                "Build final island layout",
                "This will arrange the camp, spread the original objects, and rebuild generated trees, rocks, and wildlife.",
                "Build Layout",
                "Cancel"))
            return;

        BuildLayout();
    }

    private static void BuildLayout()
    {
        GameObject generatedRoot = GameObject.Find(FillRootName);
        if (generatedRoot != null)
            Object.DestroyImmediate(generatedRoot);

        generatedRoot = new GameObject(FillRootName);
        Undo.RegisterCreatedObjectUndo(generatedRoot, "Build island layout");

        ArrangeCamp();
        ArrangeOriginalSceneObjects();

        GameObject treeTemplate = GameObject.Find("Tree_1") ?? GameObject.Find("Tree");
        GameObject deerTemplate = GameObject.Find("Deer");
        GameObject birdTemplate = GameObject.Find("Bird");
        GameObject bigTemplate = GameObject.Find("BigAnimal");
        GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/URP GanzSe Free Camping Props/Prefabs/FCP_RockCluster_Type1_Color1.prefab");

        if (treeTemplate == null || deerTemplate == null || birdTemplate == null || rockPrefab == null)
        {
            Object.DestroyImmediate(generatedRoot);
            Debug.LogError("Layout generation stopped: a required tree, animal, or rock asset is missing.");
            return;
        }

        CreateTrees(treeTemplate, generatedRoot.transform);
        CreateRocks(rockPrefab, generatedRoot.transform);
        CreateWildlife(deerTemplate, birdTemplate, bigTemplate, generatedRoot.transform);

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = generatedRoot;
        Debug.Log("Final island layout built and saved. Camp is at the bottom-right.");
    }

    private static void ArrangeCamp()
    {
        SetPosition("Campfire", CampCenter + new Vector3(0f, 2f, 0f));
        SetPosition("FCP_Campfire_Type1_Color1", CampCenter);
        SetPosition("FCP_Tent_Type1_Color1", CampCenter + new Vector3(3f, 0f, -2f));
        SetPosition("FCP_Bench_Type1_Color1", CampCenter + new Vector3(-2.5f, 0f, 1.5f));
        SetPosition("FCP_Bench_Type1_Color1_1", CampCenter + new Vector3(1.5f, 0f, 2.5f));
        SetPosition("FCP_FlagBunting_Type1_Color1", CampCenter + new Vector3(-2f, 0f, -2f));
        SetPosition("FCP_FlagBunting_Type1_Color1_1", CampCenter + new Vector3(2f, 0f, 1f));
    }

    private static void ArrangeOriginalSceneObjects()
    {
        SetPosition("Player", new Vector3(0f, 1f, 20f));

        Vector3[] treePositions =
        {
            new(-21f, 0f, 18f), new(-12f, 0f, 20f), new(-3f, 0f, 17f),
            new(8f, 0f, 20f), new(17f, 0f, 13f), new(-20f, 0f, 7f)
        };
        string[] trees = { "Tree", "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5" };
        for (int i = 0; i < trees.Length; i++)
            SetPosition(trees[i], treePositions[i]);

        SetPosition("Deer", new Vector3(-15f, 1f, 14f));
        SetPosition("Deer_1", new Vector3(7f, 1f, 15f));
        SetPosition("Bird", new Vector3(-7f, 1f, 18f));
        SetPosition("BigAnimal", new Vector3(13f, 1f, 7f));
        SetPosition("Legendary", new Vector3(-19f, 1f, -10f));
    }

    private static void CreateTrees(GameObject template, Transform parent)
    {
        Vector3[] positions =
        {
            new(-16f, 0f, 15f), new(-8f, 0f, 13f), new(2f, 0f, 15f),
            new(13f, 0f, 17f), new(-22f, 0f, 1f), new(-14f, 0f, 5f),
            new(-5f, 0f, 7f), new(5f, 0f, 8f), new(15f, 0f, 4f),
            new(-18f, 0f, -5f), new(-9f, 0f, -3f), new(0f, 0f, 1f),
            new(9f, 0f, -2f), new(-21f, 0f, -15f), new(-12f, 0f, -12f),
            new(-3f, 0f, -16f), new(6f, 0f, -12f), new(12f, 0f, -9f),
            new(14f, 0f, -17f), new(22f, 0f, -10f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            if (InsideCampClearing(positions[i]))
                continue;
            GameObject tree = Object.Instantiate(template, positions[i], RandomYRotation(i), parent);
            tree.name = $"Tree_Generated_{i + 1:00}";
            float scale = 0.8f + (i % 5) * 0.1f;
            tree.transform.localScale = template.transform.localScale * scale;
            Undo.RegisterCreatedObjectUndo(tree, "Create island tree");
        }
    }

    private static void CreateRocks(GameObject prefab, Transform parent)
    {
        Vector3[] positions =
        {
            new(-18f, 0f, 11f), new(-10f, 0f, 17f), new(0f, 0f, 11f),
            new(10f, 0f, 13f), new(20f, 0f, 7f), new(-16f, 0f, 0f),
            new(-4f, 0f, 3f), new(7f, 0f, 4f), new(-20f, 0f, -9f),
            new(-8f, 0f, -9f), new(3f, 0f, -7f), new(13f, 0f, -6f),
            new(-14f, 0f, -19f), new(1f, 0f, -19f), new(20f, 0f, 16f),
            new(-2f, 0f, 20f), new(-22f, 0f, 14f), new(17f, 0f, -4f),
            new(-19f, 0f, -20f), new(8f, 0f, -20f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            if (InsideCampClearing(positions[i]))
                continue;
            GameObject rock = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            rock.name = $"Rock_Generated_{i + 1:00}";
            rock.transform.SetPositionAndRotation(positions[i], RandomYRotation(i + 30));
            rock.transform.localScale *= 0.75f + (i % 4) * 0.15f;
            Undo.RegisterCreatedObjectUndo(rock, "Create island rock");
        }
    }

    private static void CreateWildlife(
        GameObject deerTemplate,
        GameObject birdTemplate,
        GameObject bigTemplate,
        Transform parent)
    {
        Vector3[] deerPositions =
        {
            new(-13f, 1f, 10f), new(-2f, 1f, 13f), new(11f, 1f, 10f),
            new(-17f, 1f, 0f), new(2f, 1f, 5f), new(15f, 1f, 1f),
            new(-10f, 1f, -8f), new(5f, 1f, -12f)
        };
        Vector3[] birdPositions =
        {
            new(-18f, 1f, 16f), new(-6f, 1f, 16f), new(5f, 1f, 18f),
            new(-12f, 1f, 3f), new(7f, 1f, 2f), new(-3f, 1f, -8f)
        };
        Vector3[] bigPositions =
        {
            new(-18f, 1f, -15f), new(10f, 1f, -7f)
        };

        CloneGroup(deerTemplate, "Deer_Generated", deerPositions, parent);
        CloneGroup(birdTemplate, "Bird_Generated", birdPositions, parent);
        if (bigTemplate != null)
            CloneGroup(bigTemplate, "LargeAnimal_Generated", bigPositions, parent);
    }

    private static void CloneGroup(
        GameObject template,
        string prefix,
        Vector3[] positions,
        Transform parent)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject copy = Object.Instantiate(template, positions[i], RandomYRotation(i + 60), parent);
            copy.name = $"{prefix}_{i + 1:00}";
            Undo.RegisterCreatedObjectUndo(copy, "Create wildlife");
        }
    }

    private static bool InsideCampClearing(Vector3 position)
    {
        Vector2 offset = new(position.x - CampCenter.x, position.z - CampCenter.z);
        return offset.sqrMagnitude < 42f;
    }

    private static Quaternion RandomYRotation(int seed)
    {
        return Quaternion.Euler(0f, (seed * 137.5f) % 360f, 0f);
    }

    private static void SetPosition(string name, Vector3 position)
    {
        GameObject target = GameObject.Find(name);
        if (target == null)
            return;
        Undo.RecordObject(target.transform, "Arrange island layout");
        target.transform.position = position;
    }
}

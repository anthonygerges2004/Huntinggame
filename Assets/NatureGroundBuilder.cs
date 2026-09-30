using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class NatureGroundBuilder
{
    public const string RootName = "_NatureGround";
    public const int LayoutVersion = 4;
    static readonly Vector3 CampCenter = new(18f, 0f, -18f);
    const string Pack = "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/";
    static Bounds island;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BuildWhenPlaying()
    {
        if (!Application.isPlaying)
            return;
        if (SceneManager.GetActiveScene().name != "GetStarted_Scene")
            return;
        if (GameObject.Find("_NatureForest") != null)
            return;
        if (GameObject.Find("_RealisticTerrain") != null)
            return;
        if (Object.FindAnyObjectByType<Terrain>() != null)
            return;
        if (GameObject.Find(RootName) != null)
            return;

        Build();
    }

    public static GameObject Build()
    {
        island = ReadIslandBounds();
        RemoveOldNaturePieces();
        HideBaseGroundMesh();

        GameObject root = new GameObject(RootName);
        Transform parent = root.transform;

        GameObject meadowA = Load(Pack + "PP_Meadow_07.prefab");
        GameObject meadowB = Load(Pack + "PP_Meadow_08.prefab");
        GameObject path = Load(Pack + "PP_Meadow_Path_05.prefab");
        GameObject floor = Load(Pack + "PP_Floor_Tile_05.prefab");
        GameObject grassA = Load(Pack + "PP_Grass_11.prefab");
        GameObject grassB = Load(Pack + "PP_Grass_15.prefab");
        GameObject flowerA = Load(Pack + "PP_Sunflower_04.prefab");
        GameObject flowerB = Load(Pack + "PP_Daffodil_03.prefab");
        GameObject lake = Load(Pack + "PP_Lake_Ground_04.prefab");
        GameObject birch = Load(Pack + "PP_Birch_Tree_05.prefab");
        GameObject bush = Load("Assets/URP GanzSe Free Camping Props/Prefabs/FCP__Bush_Type1_Color1.prefab");

        if (meadowA == null || meadowB == null)
        {
            Debug.LogError("Nature ground stopped: meadow prefabs were not found.");
            Object.DestroyImmediate(root);
            return null;
        }

        CoverIslandWithMeadows(meadowA, meadowB, parent);
        PlaceLakeInCorner(lake, parent);
        PlacePathToCamp(path, parent);
        PlaceCampFloor(floor, parent);
        PlaceForestRing(birch, parent);
        PlaceBushesNearCamp(bush, parent);
        ScatterGrassAndFlowers(grassA, grassB, flowerA, flowerB, parent);

        return root;
    }

    static Bounds ReadIslandBounds()
    {
        GameObject ground = GameObject.Find("Ground_1");
        if (ground != null)
        {
            Collider collider = ground.GetComponent<Collider>();
            if (collider != null)
                return collider.bounds;

            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
                return renderer.bounds;
        }

        return new Bounds(new Vector3(0.5f, 0f, 6f), new Vector3(119f, 1f, 127f));
    }

    static void HideBaseGroundMesh()
    {
        GameObject ground = GameObject.Find("Ground_1");
        if (ground == null)
            return;

        MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    static void RemoveOldNaturePieces()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name.StartsWith("PP_"))
                Object.DestroyImmediate(roots[i]);
        }
    }

    static void CoverIslandWithMeadows(GameObject meadowA, GameObject meadowB, Transform parent)
    {
        float nativeSize = MeasureFootprint(meadowA, 1f);
        if (nativeSize < 4f)
            nativeSize = 16f;

        const int tilesX = 4;
        const int tilesZ = 4;
        float needed = Mathf.Max(island.size.x / tilesX, island.size.z / tilesZ) * 1.35f;
        float scale = Mathf.Clamp(needed / nativeSize, 1.2f, 10f);

        int index = 0;
        for (int x = 0; x < tilesX; x++)
        {
            for (int z = 0; z < tilesZ; z++)
            {
                float worldX = Mathf.Lerp(island.min.x, island.max.x, (x + 0.5f) / tilesX);
                float worldZ = Mathf.Lerp(island.min.z, island.max.z, (z + 0.5f) / tilesZ);
                GameObject prefab = index % 2 == 0 ? meadowA : meadowB;
                GameObject tile = Spawn(
                    prefab,
                    new Vector3(worldX, 0.02f, worldZ),
                    YRotation(index * 90f),
                    parent,
                    scale);
                if (tile != null)
                    tile.name = $"Meadow_{index:00}";
                index++;
            }
        }
    }

    static void PlaceLakeInCorner(GameObject lake, Transform parent)
    {
        if (lake == null)
            return;

        float nativeSize = MeasureFootprint(lake, 1f);
        if (nativeSize < 4f)
            nativeSize = 16f;
        float scale = Mathf.Clamp(22f / nativeSize, 0.8f, 4f);

        Vector3 position = new(
            island.min.x + 18f,
            0.03f,
            island.max.z - 18f);
        if (InsideCamp(position))
            position.x = island.center.x - 20f;

        GameObject water = Spawn(lake, position, YRotation(20f), parent, scale);
        if (water != null)
            water.name = "Lake";
    }

    static void PlacePathToCamp(GameObject path, Transform parent)
    {
        if (path == null)
            return;

        float nativeSize = MeasureFootprint(path, 1f);
        if (nativeSize < 2f)
            nativeSize = 8f;
        float scale = Mathf.Clamp(8f / nativeSize, 0.6f, 2.5f);

        Vector3 start = ClampToIsland(new Vector3(0f, 0.04f, island.max.z - 22f), 10f);
        Vector3 end = ClampToIsland(CampCenter + new Vector3(-5f, 0.04f, 6f), 8f);
        const int steps = 5;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            Vector3 position = Vector3.Lerp(start, end, t);
            Vector3 direction = end - start;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(direction.normalized)
                : Quaternion.identity;
            GameObject tile = Spawn(path, position, rotation, parent, scale);
            if (tile != null)
                tile.name = $"Path_{i + 1}";
        }
    }

    static void PlaceCampFloor(GameObject floor, Transform parent)
    {
        if (floor == null)
            return;

        float nativeSize = MeasureFootprint(floor, 1f);
        if (nativeSize < 1f)
            nativeSize = 4f;
        float scale = Mathf.Clamp(5.5f / nativeSize, 0.6f, 3.5f);

        Vector3[] tiles =
        {
            CampCenter + new Vector3(-2f, 0.05f, -2f),
            CampCenter + new Vector3(2f, 0.05f, -2f),
            CampCenter + new Vector3(-2f, 0.05f, 2f),
            CampCenter + new Vector3(2f, 0.05f, 2f)
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            GameObject tile = Spawn(floor, tiles[i], YRotation(i * 90f), parent, scale);
            if (tile != null)
                tile.name = $"CampFloor_{i + 1}";
        }
    }

    static void PlaceForestRing(GameObject birch, Transform parent)
    {
        if (birch == null)
            return;

        float radiusX = island.extents.x * 0.72f;
        float radiusZ = island.extents.z * 0.72f;
        const int count = 10;
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 position = new(
                island.center.x + Mathf.Cos(angle) * radiusX,
                0.02f,
                island.center.z + Mathf.Sin(angle) * radiusZ);
            position = ClampToIsland(position, 10f);
            if (InsideCamp(position))
                continue;

            float scale = 0.85f + (i % 3) * 0.08f;
            Spawn(birch, position, YRotation(i * 37f), parent, scale);
        }
    }

    static void PlaceBushesNearCamp(GameObject bush, Transform parent)
    {
        if (bush == null)
            return;

        Vector3[] spots =
        {
            CampCenter + new Vector3(-6f, 0f, 4f),
            CampCenter + new Vector3(6f, 0f, 5f),
            CampCenter + new Vector3(-5f, 0f, -6f)
        };

        for (int i = 0; i < spots.Length; i++)
            Spawn(bush, ClampToIsland(spots[i], 6f), YRotation(i * 40f), parent, 1f);
    }

    static void ScatterGrassAndFlowers(
        GameObject grassA,
        GameObject grassB,
        GameObject flowerA,
        GameObject flowerB,
        Transform parent)
    {
        Vector3[] grassSpots =
        {
            PointOnIsland(-0.4f, 0.35f), PointOnIsland(-0.15f, 0.2f), PointOnIsland(0.2f, 0.3f),
            PointOnIsland(-0.55f, 0.05f), PointOnIsland(0.15f, 0.0f), PointOnIsland(-0.25f, -0.25f),
            PointOnIsland(0.3f, -0.2f), PointOnIsland(-0.5f, -0.35f), PointOnIsland(0.05f, 0.45f),
            PointOnIsland(0.45f, 0.15f), PointOnIsland(-0.1f, -0.45f), PointOnIsland(0.35f, 0.4f)
        };
        Scatter(grassA, grassSpots, parent, 0.9f, 1.25f, true);
        Scatter(grassB, new[]
        {
            PointOnIsland(-0.3f, 0.4f), PointOnIsland(0.05f, 0.25f), PointOnIsland(-0.6f, 0.15f),
            PointOnIsland(0.25f, -0.05f), PointOnIsland(-0.2f, -0.4f), PointOnIsland(0.4f, -0.3f)
        }, parent, 0.85f, 1.15f, true);

        Scatter(flowerA, new[]
        {
            PointOnIsland(-0.25f, 0.22f), PointOnIsland(0.12f, 0.12f),
            PointOnIsland(-0.35f, -0.12f), PointOnIsland(0.22f, -0.18f)
        }, parent, 0.8f, 1.05f, true);
        Scatter(flowerB, new[]
        {
            PointOnIsland(-0.12f, 0.28f), PointOnIsland(0.18f, 0.02f), PointOnIsland(-0.4f, -0.22f)
        }, parent, 0.7f, 1f, true);
    }

    static Vector3 PointOnIsland(float nx, float nz)
    {
        float x = island.center.x + nx * island.extents.x * 0.7f;
        float z = island.center.z + nz * island.extents.z * 0.7f;
        return ClampToIsland(new Vector3(x, 0.03f, z), 10f);
    }

    static Vector3 ClampToIsland(Vector3 position, float margin)
    {
        position.x = Mathf.Clamp(position.x, island.min.x + margin, island.max.x - margin);
        position.z = Mathf.Clamp(position.z, island.min.z + margin, island.max.z - margin);
        position.y = 0.03f;
        return position;
    }

    static void Scatter(
        GameObject prefab,
        Vector3[] spots,
        Transform parent,
        float minScale,
        float maxScale,
        bool skipCamp)
    {
        if (prefab == null)
            return;

        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 position = spots[i];
            if (skipCamp && InsideCamp(position))
                continue;

            float scale = Mathf.Lerp(minScale, maxScale, (i % 5) / 4f);
            Spawn(prefab, position, YRotation(i * 47f), parent, scale);
        }
    }

    static bool InsideCamp(Vector3 position)
    {
        Vector2 offset = new(position.x - CampCenter.x, position.z - CampCenter.z);
        return offset.sqrMagnitude < 42f;
    }

    static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        float scale)
    {
        GameObject instance;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        else
            instance = Object.Instantiate(prefab, parent);
#else
        instance = Object.Instantiate(prefab, parent);
#endif
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one * scale;
        DisableColliders(instance);
        return instance;
    }

    static void DisableColliders(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    static float MeasureFootprint(GameObject prefab, float scale)
    {
        GameObject temp = Object.Instantiate(prefab);
        temp.transform.position = new Vector3(0f, -2000f, 0f);
        temp.transform.localScale = Vector3.one * scale;
        Renderer renderer = temp.GetComponentInChildren<Renderer>();
        float size = 16f;
        if (renderer != null)
            size = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
        Object.DestroyImmediate(temp);
        return Mathf.Max(size, 1f);
    }

    static Quaternion YRotation(float degrees)
    {
        return Quaternion.Euler(0f, degrees, 0f);
    }

    static GameObject Load(string path)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }
}

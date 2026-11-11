using System.Collections.Generic;
using HuntingGame.Wind;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One continuous land mass. Terrain streams in around the player, wildlife and
/// trees stay in the same world, and nothing loads a new scene.
/// </summary>
[DefaultExecutionOrder(-200)]
public class OpenWorld : MonoBehaviour
{
    const float ShowDistance = 145f;
    const float HideDistance = 200f;
    const float CampClearing = 75f;
    const float DenClearance = 28f;
    const float RespawnSeconds = 90f;

    const int MaxDeer = 7;
    const int MaxBirds = 6;
    const int MaxLarge = 2;
    const int MaxGame = 9; // pigs, boar, foxes, rabbits and wolves were too rare to ever run into

    static readonly FlyerKind[] WoodBirds =
    {
        FlyerKind.Sparrow, FlyerKind.Thrush, FlyerKind.Thrush, FlyerKind.Robin,
        FlyerKind.Crow, FlyerKind.Jay, FlyerKind.Woodpecker, FlyerKind.Pigeon,
        FlyerKind.Pheasant, FlyerKind.Hawk, FlyerKind.Owl
    };

    static readonly QuarryKind[] GameRoll =
    {
        QuarryKind.Pig, QuarryKind.Pig, QuarryKind.Pig,
        QuarryKind.Boar,
        QuarryKind.Rabbit, QuarryKind.Rabbit,
        QuarryKind.Fox,
        QuarryKind.Wolf
    };

    class Tile
    {
        public Terrain terrain;
        public Vector3 origin;
        public Vector3 size;
    }

    class Den
    {
        public Vector3 home;
        public AnimalKind kind;
        public FlyerKind flyer;
        public QuarryKind quarry;
        public Animal alive;
        public bool occupied;
        public bool releasing;
        public float respawnAt;
    }

    class Prop
    {
        public GameObject root;
        public bool tree;
    }

    readonly List<Den> dens = new List<Den>();
    readonly List<Prop> props = new List<Prop>();
    Transform wildRoot;
    Transform propRoot;
    TerrainStream stream;
    Vector3 camp;
    float nextRefresh;
    bool worldReady;
    bool propsBuilt;
    Coroutine hermitSpawnRoutine;

    void Awake()
    {
        if (GetComponent<WindSystem>() == null)
            gameObject.AddComponent<WindSystem>();
        if (GetComponent<GapRiverCrossings>() == null)
            gameObject.AddComponent<GapRiverCrossings>();
        JoinTerrains();
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (GetComponent<HuntVisibility>() == null)
            gameObject.AddComponent<HuntVisibility>();

        if (hermitSpawnRoutine != null)
            StopCoroutine(hermitSpawnRoutine);
        hermitSpawnRoutine = StartCoroutine(SpawnHermitAfterCampReady());
    }

    void OnDisable()
    {
        if (hermitSpawnRoutine != null)
        {
            StopCoroutine(hermitSpawnRoutine);
            hermitSpawnRoutine = null;
        }
    }

    System.Collections.IEnumerator SpawnHermitAfterCampReady()
    {
        yield return null;
        yield return null;
        camp = FindCamp();
        MountainHermit.EnsureSpawn(camp);
        RideableHorse.EnsureOnLivestock(force: true);
    }

    void Start()
    {
        camp = FindCamp();
        MountainHermit.EnsureSpawn(camp);
        RideableHorse.EnsureOnLivestock(force: true);
        BuildDens();
        worldReady = true;

        MinimapFollow map = Object.FindAnyObjectByType<MinimapFollow>();
        if (map != null && map.height < 140f)
            map.height = 170f;
    }

    void Update()
    {
        if (stream != null)
            stream.Tick();

        if (!worldReady || Time.time < nextRefresh)
            return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        nextRefresh = Time.time + 0.45f;
        RefreshWildlife();
        RefreshWilds();
    }

    void LateUpdate()
    {
        KeepPlayerOnLand();
    }

    static void HoldDeadPoses()
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            Animal animal = animals[i];
            if (animal == null || !animal.Dead)
                continue;

            Animator animator = animal.GetComponentInChildren<Animator>();
            if (animator != null && animator.enabled)
                animator.enabled = false;
        }
    }

    static void ApplyPropLod(GameObject root, float distance)
    {
        bool near = distance < 90f;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        ShadowCastingMode mode = near ? ShadowCastingMode.On : ShadowCastingMode.Off;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].shadowCastingMode != mode)
                renderers[i].shadowCastingMode = mode;
        }
    }

    void JoinTerrains()
    {
        Terrain[] found = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        List<Terrain> ranked = new List<Terrain>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].terrainData != null && found[i].terrainData.size.x > 2f)
                ranked.Add(found[i]);
        }

        ranked.Sort((a, b) => Rank(b).CompareTo(Rank(a)));

        List<Tile> kept = new List<Tile>();
        List<Terrain> dropped = new List<Terrain>();
        for (int i = 0; i < ranked.Count; i++)
        {
            Tile candidate = MakeTile(ranked[i]);
            if (CoveredBy(candidate, kept) > 0.82f)
            {
                dropped.Add(ranked[i]);
                continue;
            }

            kept.Add(candidate);
        }

        for (int i = 0; i < dropped.Count; i++)
        {
            if (dropped[i] != null && dropped[i].gameObject.activeSelf)
                dropped[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < kept.Count; i++)
        {
            if (!kept[i].terrain.gameObject.activeSelf)
                kept[i].terrain.gameObject.SetActive(true);
        }

        CloseGaps(kept);
        LinkNeighbors(kept);
        GameCulling.ApplyForActiveScene();

        if (kept.Count == 0)
            return;

        Bounds world = new Bounds(kept[0].origin, Vector3.zero);
        for (int i = 0; i < kept.Count; i++)
        {
            Vector3 center = kept[i].origin + kept[i].size * 0.5f;
            world.Encapsulate(center);
            world.Encapsulate(kept[i].origin);
            world.Encapsulate(kept[i].origin + kept[i].size);
        }

        stream = GetComponent<TerrainStream>();
        if (stream == null)
            stream = gameObject.AddComponent<TerrainStream>();

        List<Terrain> sources = new List<Terrain>();
        for (int i = 0; i < kept.Count; i++)
            sources.Add(kept[i].terrain);
        stream.Setup(sources);

        if (GetComponent<WorldWeather>() == null)
            gameObject.AddComponent<WorldWeather>();

        Debug.Log(
            "Open world ready. The land is " +
            Mathf.RoundToInt(world.size.x) + " m by " +
            Mathf.RoundToInt(world.size.z) +
            " m, in " + stream.ChunkCount +
            " streamed chunks. Walk in any direction.");
    }

    static int Rank(Terrain terrain)
    {
        int score = Mathf.RoundToInt(terrain.terrainData.size.x * terrain.terrainData.size.z);
        if (terrain.gameObject.activeInHierarchy)
            score += 10000000;
        if (terrain.name == "Terrain_Combined")
            score += 100000000;
        return score;
    }

    static Tile MakeTile(Terrain terrain)
    {
        return new Tile
        {
            terrain = terrain,
            origin = terrain.transform.position,
            size = terrain.terrainData.size
        };
    }

    static float CoveredBy(Tile candidate, List<Tile> kept)
    {
        float area = Mathf.Max(1f, candidate.size.x * candidate.size.z);
        float covered = 0f;
        for (int i = 0; i < kept.Count; i++)
            covered += OverlapArea(candidate, kept[i]);
        return Mathf.Clamp01(covered / area);
    }

    static float OverlapArea(Tile a, Tile b)
    {
        float overlapX = Mathf.Min(a.origin.x + a.size.x, b.origin.x + b.size.x) - Mathf.Max(a.origin.x, b.origin.x);
        float overlapZ = Mathf.Min(a.origin.z + a.size.z, b.origin.z + b.size.z) - Mathf.Max(a.origin.z, b.origin.z);
        if (overlapX <= 0f || overlapZ <= 0f)
            return 0f;
        return overlapX * overlapZ;
    }

    void CloseGaps(List<Tile> tiles)
    {
        for (int guard = 0; guard < 12; guard++)
        {
            bool moved = false;
            tiles.Sort((a, b) => a.origin.x.CompareTo(b.origin.x));
            for (int i = 1; i < tiles.Count; i++)
            {
                if (!SharesAxis(tiles[i - 1].origin.z, tiles[i - 1].size.z, tiles[i].origin.z, tiles[i].size.z))
                    continue;

                float gap = tiles[i].origin.x - (tiles[i - 1].origin.x + tiles[i - 1].size.x);
                if (gap > 0.4f)
                {
                    Shift(tiles[i], new Vector3(-(gap + 0.25f), 0f, 0f));
                    moved = true;
                }
            }

            tiles.Sort((a, b) => a.origin.z.CompareTo(b.origin.z));
            for (int i = 1; i < tiles.Count; i++)
            {
                if (!SharesAxis(tiles[i - 1].origin.x, tiles[i - 1].size.x, tiles[i].origin.x, tiles[i].size.x))
                    continue;

                float gap = tiles[i].origin.z - (tiles[i - 1].origin.z + tiles[i - 1].size.z);
                if (gap > 0.4f)
                {
                    Shift(tiles[i], new Vector3(0f, 0f, -(gap + 0.25f)));
                    moved = true;
                }
            }

            if (!moved)
                break;
        }
    }

    static bool SharesAxis(float originA, float sizeA, float originB, float sizeB)
    {
        float overlap = Mathf.Min(originA + sizeA, originB + sizeB) - Mathf.Max(originA, originB);
        float smaller = Mathf.Max(1f, Mathf.Min(sizeA, sizeB));
        return overlap > smaller * 0.35f;
    }

    void Shift(Tile tile, Vector3 delta)
    {
        Vector3 oldOrigin = tile.origin;
        MoveObjectsOnTile(oldOrigin, tile.size, delta, tile.terrain.transform);
        tile.terrain.transform.position += delta;
        tile.origin += delta;
    }

    static void MoveObjectsOnTile(Vector3 origin, Vector3 size, Vector3 delta, Transform terrainTransform)
    {
        HashSet<Transform> skip = new HashSet<Transform>();
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null)
                continue;

            Transform hierarchy = terrains[i].transform;
            while (hierarchy != null)
            {
                skip.Add(hierarchy);
                hierarchy = hierarchy.parent;
            }

            Transform[] under = terrains[i].GetComponentsInChildren<Transform>(true);
            for (int c = 0; c < under.Length; c++)
                skip.Add(under[c]);
        }

        skip.Add(terrainTransform);
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < all.Length; i++)
        {
            Transform item = all[i];
            if (item == null || skip.Contains(item))
                continue;
            if (!ContainsXZ(origin, size, item.position))
                continue;
            if (item.parent != null && ContainsXZ(origin, size, item.parent.position))
                continue;

            item.position += delta;
        }
    }

    static bool ContainsXZ(Vector3 origin, Vector3 size, Vector3 point)
    {
        return point.x >= origin.x && point.x <= origin.x + size.x &&
               point.z >= origin.z && point.z <= origin.z + size.z;
    }

    static void LinkNeighbors(List<Tile> tiles)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            Terrain left = null;
            Terrain right = null;
            Terrain top = null;
            Terrain bottom = null;
            for (int j = 0; j < tiles.Count; j++)
            {
                if (i == j)
                    continue;

                float rightGap = tiles[j].origin.x - (tiles[i].origin.x + tiles[i].size.x);
                float leftGap = tiles[i].origin.x - (tiles[j].origin.x + tiles[j].size.x);
                float topGap = tiles[j].origin.z - (tiles[i].origin.z + tiles[i].size.z);
                float bottomGap = tiles[i].origin.z - (tiles[j].origin.z + tiles[j].size.z);

                if (Mathf.Abs(rightGap) < 3f &&
                    SharesAxis(tiles[i].origin.z, tiles[i].size.z, tiles[j].origin.z, tiles[j].size.z))
                    right = tiles[j].terrain;
                if (Mathf.Abs(leftGap) < 3f &&
                    SharesAxis(tiles[i].origin.z, tiles[i].size.z, tiles[j].origin.z, tiles[j].size.z))
                    left = tiles[j].terrain;
                if (Mathf.Abs(topGap) < 3f &&
                    SharesAxis(tiles[i].origin.x, tiles[i].size.x, tiles[j].origin.x, tiles[j].size.x))
                    top = tiles[j].terrain;
                if (Mathf.Abs(bottomGap) < 3f &&
                    SharesAxis(tiles[i].origin.x, tiles[i].size.x, tiles[j].origin.x, tiles[j].size.x))
                    bottom = tiles[j].terrain;
            }

            tiles[i].terrain.allowAutoConnect = true;
            tiles[i].terrain.SetNeighbors(left, top, right, bottom);
        }

        Terrain.SetConnectivityDirty();
    }

    void BuildDens()
    {
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
            return;

        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null || terrains[i].terrainData == null)
                continue;
            Vector3 origin = terrains[i].transform.position;
            Vector3 size = terrains[i].terrainData.size;
            minX = Mathf.Min(minX, origin.x);
            minZ = Mathf.Min(minZ, origin.z);
            maxX = Mathf.Max(maxX, origin.x + size.x);
            maxZ = Mathf.Max(maxZ, origin.z + size.z);
        }

        float width = maxX - minX;
        float depth = maxZ - minZ;
        float spacing = Mathf.Max(width, depth) > 6000f ? 150f : 100f;
        while (width > 1f && depth > 1f && (width / spacing) * (depth / spacing) > 900f && spacing < 220f)
            spacing += 20f;

        List<Vector3> taken = new List<Vector3>();
        Animal[] existing = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                taken.Add(existing[i].transform.position);
        }

        int ix = 0;
        for (float x = minX + 40f; x < maxX - 40f; x += spacing, ix++)
        {
            int iz = 0;
            for (float z = minZ + 40f; z < maxZ - 40f; z += spacing, iz++)
            {
                Vector3 home = WorldGround.Snap(new Vector3(x, 0f, z));
                if (!WorldGround.IsMeadow(home))
                    continue;
                if (FlatDistance(home, camp) < CampClearing)
                    continue;
                if (NearAny(home, taken, DenClearance))
                    continue;

                int hash = Mathf.Abs((ix * 73856093) ^ (iz * 19349663));
                Den den = new Den();
                int roll = hash % 20;
                if (roll == 0)
                {
                    den.kind = AnimalKind.Large;
                }
                else if (roll <= 4)
                {
                    den.kind = AnimalKind.Game;
                    den.quarry = GameRoll[hash % GameRoll.Length];
                }
                else if (roll <= 10)
                {
                    den.kind = AnimalKind.Bird;
                    den.flyer = PickFlyer(hash, home);
                }
                else
                {
                    den.kind = AnimalKind.Deer;
                }

                den.home = home;
                dens.Add(den);
                taken.Add(home);
            }
        }
    }

    void RefreshWildlife()
    {
        Transform player = PlayerTransform();
        if (player == null)
            return;

        int deer = 0;
        int birds = 0;
        int large = 0;
        int game = 0;
        for (int i = 0; i < dens.Count; i++)
        {
            Den den = dens[i];
            if (den.occupied && den.alive == null)
            {
                den.occupied = false;
                if (!den.releasing)
                    den.respawnAt = Time.time + RespawnSeconds;
                den.releasing = false;
            }

            if (den.alive == null)
                continue;

            float anchor = den.alive.Dead
                ? FlatDistance(den.alive.transform.position, player.position)
                : FlatDistance(den.home, player.position);
            float limit = den.alive.Dead ? HideDistance + 40f : HideDistance;
            if (anchor > limit)
                Release(den);
            else
                Count(den.kind, ref deer, ref birds, ref large, ref game);
        }

        HoldDeadPoses();

        for (int n = 0; n < MaxDeer + MaxBirds + MaxLarge + MaxGame; n++)
        {
            int best = PickDen(player.position, deer, birds, large, game);
            if (best < 0)
                break;

            Spawn(dens[best]);
            Count(dens[best].kind, ref deer, ref birds, ref large, ref game);
        }
    }

    int PickDen(Vector3 player, int deer, int birds, int large, int game)
    {
        float total = 0f;
        int nearest = -1;
        float nearestDistance = ShowDistance;
        for (int i = 0; i < dens.Count; i++)
        {
            if (!DenOpen(dens[i], deer, birds, large, game))
                continue;

            float distance = FlatDistance(dens[i].home, player);
            if (distance < 8f || distance >= ShowDistance)
                continue;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }

            total += DenWeight(distance);
        }

        if (total <= 0.0001f)
            return nearest;

        float roll = Random.Range(0f, total);
        for (int i = 0; i < dens.Count; i++)
        {
            if (!DenOpen(dens[i], deer, birds, large, game))
                continue;

            float distance = FlatDistance(dens[i].home, player);
            if (distance < 8f || distance >= ShowDistance)
                continue;

            roll -= DenWeight(distance);
            if (roll <= 0f)
                return i;
        }

        return nearest;
    }

    static bool DenOpen(Den den, int deer, int birds, int large, int game)
    {
        if (den.occupied || den.alive != null || Time.time < den.respawnAt)
            return false;
        if (Overhunt.AreaIsQuiet(den.home))
            return false;
        if (den.kind == AnimalKind.Deer && deer >= MaxDeer)
            return false;
        if (den.kind == AnimalKind.Bird && birds >= MaxBirds)
            return false;
        if (den.kind == AnimalKind.Large && large >= MaxLarge)
            return false;
        if (den.kind == AnimalKind.Game && game >= MaxGame)
            return false;
        return true;
    }

    // More tickets around a stalk, almost none in the hunter's lap or on the far edge.
    static float DenWeight(float distance)
    {
        if (distance < 36f)
            return 0f;

        float off = Mathf.Abs(distance - 78f) / 70f;
        float score = Mathf.Clamp01(1f - off);
        return 0.2f + score * score * 4f;
    }

    static void Count(AnimalKind kind, ref int deer, ref int birds, ref int large, ref int game)
    {
        if (kind == AnimalKind.Bird)
            birds++;
        else if (kind == AnimalKind.Large)
            large++;
        else if (kind == AnimalKind.Game)
            game++;
        else
            deer++;
    }

    static FlyerKind PickFlyer(int hash, Vector3 home)
    {
        if (WaterBodies.SurfaceAt(home, 22f, out _, out _) && hash % 3 != 2)
            return (hash & 1) == 0 ? FlyerKind.Duck : FlyerKind.Goose;
        return WoodBirds[hash % WoodBirds.Length];
    }

    void Spawn(Den den)
    {
        if (wildRoot == null)
        {
            GameObject root = new GameObject("_OpenWildlife");
            wildRoot = root.transform;
        }

        Vector3 ground = WorldGround.Snap(den.home);
        bool bird = den.kind == AnimalKind.Bird;
        FlyerProfile flyerProfile = FlyerProfile.Of(den.flyer);
        float height = bird ? flyerProfile.spawnHeight : 0f;

        string bodyName = bird
            ? flyerProfile.objectName
            : den.kind == AnimalKind.Game
                ? QuarryProfile.Of(den.quarry).objectName
                : den.kind == AnimalKind.Large ? "WildLarge" : "WildDeer";
        GameObject body = new GameObject(bodyName);
        body.transform.SetParent(wildRoot, true);
        body.transform.position = ground + Vector3.up * height;
        body.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (bird)
        {
            SphereCollider hit = body.AddComponent<SphereCollider>();
            hit.radius = flyerProfile.collider;
            hit.center = new Vector3(0f, 0.12f, 0f);
        }
        else
        {
            body.AddComponent<CapsuleCollider>();
        }

        Animal animal = body.AddComponent<Animal>();
        animal.kind = den.kind;
        animal.flyer = den.flyer;
        animal.quarry = den.quarry;
        animal.nocturnal = den.kind == AnimalKind.Large;
        if (den.kind == AnimalKind.Large)
        {
            animal.meatAmount = 5;
            animal.nightDamage = 14f;
            animal.moveSpeed = 2.4f;
        }
        else if (den.kind == AnimalKind.Bird)
        {
            animal.meatAmount = flyerProfile.meat;
            animal.moveSpeed = flyerProfile.cruiseSpeed;
            animal.fleeSpeed = flyerProfile.fleeSpeed;
            animal.nocturnal = flyerProfile.nocturnal;
        }
        else if (den.kind != AnimalKind.Game)
        {
            animal.meatAmount = 2;
        }

        den.alive = animal;
        den.occupied = true;
        den.releasing = false;
    }

    void Release(Den den)
    {
        den.releasing = true;
        den.occupied = false;
        if (den.alive != null)
            Destroy(den.alive.gameObject);
        den.alive = null;
    }

    void RefreshWilds()
    {
        Transform player = PlayerTransform();
        if (player == null)
            return;

        // With the RPG Poly Pack scenery, the hand-built area already has trees,
        // so roaming props only start near its edge.
        RpgppWildSet set = WildSet();
        bool roaming = set != null
            ? !set.Covers(player.position, 150f)
            : FlatDistance(player.position, camp) > 190f;
        if (!roaming)
        {
            if (propRoot != null)
                propRoot.gameObject.SetActive(false);
            return;
        }

        if (!propsBuilt)
            BuildProps();
        if (propRoot == null)
            return;

        propRoot.gameObject.SetActive(true);
        for (int i = 0; i < props.Count; i++)
        {
            if (props[i].root == null)
                continue;
            float distance = FlatDistance(props[i].root.transform.position, player.position);
            float far = props[i].tree ? 165f : 120f;
            if (!(distance < far && props[i].root.activeSelf))
                PlaceProp(props[i], player.position);
            if (props[i].root.activeSelf)
                ApplyPropLod(props[i].root, FlatDistance(props[i].root.transform.position, player.position));
        }
    }

    void BuildProps()
    {
        propsBuilt = true;

        RpgppWildSet set = WildSet();
        if (set != null)
        {
            BuildRpgppProps(set);
            return;
        }

        GameObject treePrefab = LoadPrefab("Assets/RealisticTree/RealisticTree_LOD.prefab");
        GameObject[] grass = new[]
        {
            LoadPrefab("Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_11.prefab"),
            LoadPrefab("Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_15.prefab"),
            LoadPrefab("Assets/URP GanzSe Free Camping Props/Prefabs/FCP_GrassCluster_Type1_Color1.prefab")
        };
        GameObject bush = LoadPrefab("Assets/URP GanzSe Free Camping Props/Prefabs/FCP__Bush_Type1_Color1.prefab");
        GameObject rock = LoadPrefab("Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Rock_Moss_Grown_09.prefab");

        if (treePrefab == null && bush == null && rock == null && grass[0] == null && grass[1] == null && grass[2] == null)
            return;

        GameObject root = new GameObject("_OpenWild");
        propRoot = root.transform;
        AddProps(treePrefab, 24, true, 0.95f, 1.35f);
        AddProps(grass[0], 12, false, 0.8f, 1.4f);
        AddProps(grass[1], 12, false, 0.8f, 1.4f);
        AddProps(grass[2], 10, false, 0.8f, 1.5f);
        AddProps(bush, 10, false, 0.9f, 1.3f);
        AddProps(rock, 8, false, 0.7f, 1.3f);
    }

    void AddProps(GameObject prefab, int count, bool tree, float minScale, float maxScale)
    {
        if (prefab == null)
            return;

        for (int i = 0; i < count; i++)
        {
            GameObject instance = Object.Instantiate(prefab, propRoot);
            instance.name = tree ? "WildTree" : prefab.name;
            float scale = Random.Range(minScale, maxScale);
            instance.transform.localScale *= scale;
            if (tree && instance.GetComponent<HarvestableTree>() == null)
                instance.AddComponent<HarvestableTree>();
            props.Add(new Prop { root = instance, tree = tree });
        }
    }

    // Same wilderness mix, but from RPG Poly Pack Lite, sized to match the built scenery.
    void BuildRpgppProps(RpgppWildSet set)
    {
        GameObject root = new GameObject("_OpenWild");
        propRoot = root.transform;
        AddFitted(set.trees, 24, true, set.treeHeight, Vector2.zero);
        AddFitted(set.bushes, 12, false, set.bushHeight, Vector2.zero);
        AddFitted(set.rocks, 10, false, set.rockHeight, Vector2.zero);
        AddFitted(set.grass, 30, false, Vector2.zero, set.grassScale);
    }

    void AddFitted(GameObject[] prefabs, int count, bool tree, Vector2 height, Vector2 scale)
    {
        if (prefabs == null || prefabs.Length == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[i % prefabs.Length];
            if (prefab == null)
                continue;

            GameObject instance = Object.Instantiate(prefab, propRoot);
            instance.name = tree ? "WildTree" : prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            if (height.y > 0f)
            {
                Bounds b = RenderBounds(instance);
                if (b.size.y > 0.001f)
                    instance.transform.localScale *= Random.Range(height.x, height.y) / b.size.y;
            }
            else if (scale.y > 0f)
            {
                instance.transform.localScale *= Random.Range(scale.x, scale.y);
            }

            if (tree)
            {
                if (instance.GetComponent<HarvestableTree>() == null)
                    instance.AddComponent<HarvestableTree>();
                AddTrunk(instance);
            }

            props.Add(new Prop { root = instance, tree = tree });
        }
    }

    RpgppWildSet wildSet;
    bool wildSetChecked;

    RpgppWildSet WildSet()
    {
        if (!wildSetChecked)
        {
            wildSet = RpgppWildSet.Find();
            wildSetChecked = true;
        }

        return wildSet;
    }

    static Bounds RenderBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // A trunk-sized capsule so the player can't walk through the tree.
    static void AddTrunk(GameObject tree)
    {
        Bounds world = RenderBounds(tree);
        float s = Mathf.Max(0.0001f, tree.transform.lossyScale.y);
        Vector3 localCenter = (world.center - tree.transform.position) / s;
        float localHeight = world.size.y / s;
        CapsuleCollider capsule = tree.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = 0.35f / s;
        capsule.height = localHeight * 0.6f;
        capsule.center = new Vector3(localCenter.x, localCenter.y - localHeight * 0.2f, localCenter.z);
    }

    void PlaceProp(Prop prop, Vector3 around)
    {
        float min = prop.tree ? 28f : 12f;
        float max = prop.tree ? 140f : 90f;
        bool placed = false;
        Vector3 spot = around;
        RpgppWildSet set = WildSet();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(min, max);
            Vector3 candidate = around + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            candidate = WorldGround.Snap(candidate);
            if (WorldGround.InsideWater(candidate))
                continue;
            // Don't double up inside the hand-built scenery.
            if (set != null && set.Covers(candidate, 20f))
                continue;
            if (prop.tree && !WorldGround.IsMeadow(candidate) && attempt < 6)
                continue;
            spot = candidate;
            placed = true;
            break;
        }

        if (!placed)
        {
            prop.root.SetActive(false);
            return;
        }

        prop.root.transform.position = spot;
        prop.root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        prop.root.SetActive(true);
    }

    PlayerMove cachedPlayer;

    void KeepPlayerOnLand()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;
        if (RideableHorse.IsRiding)
            return;

        if (cachedPlayer == null)
            cachedPlayer = Object.FindAnyObjectByType<PlayerMove>();
        if (cachedPlayer == null)
            return;

        Vector3 position = cachedPlayer.transform.position;
        if (WaterBodies.InsideWater(position) || WaterBodies.TryGetSwim(position, out _, out _))
            return;

        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
            return;

        // The strips between tiles are rivers. Pulling the player back onto land
        // every frame made the banks and the water stutter.
        if (!InsideAny(terrains, position))
        {
            if (position.y < -40f || !InsideMap(terrains, position, 60f))
                PlacePlayer(cachedPlayer, NearestInside(terrains, position));
            return;
        }

        Vector3 ground = WorldGround.Snap(position);
        if (position.y < ground.y - 25f)
            PlacePlayer(cachedPlayer, ground + Vector3.up * 1.2f);
    }

    static bool InsideMap(Terrain[] terrains, Vector3 position, float margin)
    {
        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            minX = Mathf.Min(minX, origin.x);
            minZ = Mathf.Min(minZ, origin.z);
            maxX = Mathf.Max(maxX, origin.x + size.x);
            maxZ = Mathf.Max(maxZ, origin.z + size.z);
        }

        if (minX > maxX)
            return false;

        return position.x >= minX - margin && position.x <= maxX + margin &&
               position.z >= minZ - margin && position.z <= maxZ + margin;
    }

    static bool InsideAny(Terrain[] terrains, Vector3 position)
    {
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
                return true;
        }

        return false;
    }

    static Vector3 NearestInside(Terrain[] terrains, Vector3 position)
    {
        Vector3 best = position;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float inset = 2f;
            float x = Mathf.Clamp(position.x, origin.x + inset, origin.x + Mathf.Max(inset, size.x - inset));
            float z = Mathf.Clamp(position.z, origin.z + inset, origin.z + Mathf.Max(inset, size.z - inset));
            float dx = position.x - x;
            float dz = position.z - z;
            float distance = dx * dx + dz * dz;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = new Vector3(x, 0f, z);
            best.y = terrain.SampleHeight(best) + origin.y + 1.2f;
        }

        return best;
    }

    static void PlacePlayer(PlayerMove move, Vector3 position)
    {
        CharacterController controller = move.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;
        move.transform.position = position;
        if (controller != null)
            controller.enabled = true;
    }

    static Vector3 FindCamp()
    {
        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        if (tent != null)
            return WorldGround.Snap(tent.transform.position);

        GameObject fire = GameObject.Find("Campfire");
        if (fire == null)
            fire = GameObject.Find("FCP_Campfire_Type1_Color1");
        if (fire != null)
            return WorldGround.Snap(fire.transform.position);

        Transform player = PlayerTransform();
        if (player != null)
            return WorldGround.Snap(player.position);

        return WorldGround.Snap(new Vector3(48f, 0f, 57f));
    }

    static Transform PlayerTransform()
    {
        if (PlayerHealth.Instance != null)
            return PlayerHealth.Instance.transform;

        PlayerMove move = Object.FindAnyObjectByType<PlayerMove>();
        return move != null ? move.transform : null;
    }

    static bool NearAny(Vector3 point, List<Vector3> spots, float radius)
    {
        float limit = radius * radius;
        for (int i = 0; i < spots.Count; i++)
        {
            Vector3 delta = spots[i] - point;
            delta.y = 0f;
            if (delta.sqrMagnitude < limit)
                return true;
        }

        return false;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static GameObject LoadPrefab(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }
}

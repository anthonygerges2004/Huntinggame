using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fills the open world with life when play starts.
///
/// Vegetation: dense forests, ragged woodland edges, copses and lone meadow trees, with
/// bushes and rocks between them, laid out by large-scale noise so the land reads as real
/// countryside instead of bare hills. Everything is added as terrain tree instances (GPU
/// instanced, billboarded at range), avoiding water, paths, steep slopes, the camp and any
/// building, wall or prop. Works on copies of the terrain data; the assets on disk never change.
///
/// Ambience around the player: drifting pollen and dust in daylight, butterflies over the
/// grass, fireflies at night, and bird flocks crossing the sky.
/// </summary>
public class WorldLife : MonoBehaviour
{
    public const bool Enabled = true;

    const float Cell = 6.5f;              // sample spacing in metres
    const int MaxNewPerTerrain = 26000;
    const float CampClear = 60f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || Object.FindAnyObjectByType<WorldLife>() != null)
            return;
        new GameObject("_WorldLife").AddComponent<WorldLife>();
    }

    struct Kind
    {
        public int proto;
        public float minScale, maxScale;
    }

    IEnumerator Start()
    {
        // Let OpenWorld join and place the terrain tiles first.
        yield return null;
        yield return null;

        Terrain[] terrains = Terrain.activeTerrains;
        Vector3 camp = FindCamp();
        RpgppWildSet set = RpgppWildSet.Find();
        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;
            Populate(terrain, set, camp);
            yield return null;
        }

        BuildAmbience();
    }

    static Vector3 FindCamp()
    {
        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        return tent != null ? tent.transform.position : new Vector3(float.MaxValue, 0f, float.MaxValue);
    }

    // ------------------------------------------------------------------ vegetation

    void Populate(Terrain terrain, RpgppWildSet set, Vector3 camp)
    {
        TerrainData data = Object.Instantiate(terrain.terrainData);
        data.name = terrain.terrainData.name + " (alive)";

        var prototypes = new List<TreePrototype>(data.treePrototypes);
        var trees = new List<Kind>();
        var bushes = new List<Kind>();
        var rocks = new List<Kind>();

        // The terrain's own tree types first (they already look right here).
        for (int i = 0; i < prototypes.Count; i++)
        {
            GameObject p = prototypes[i].prefab;
            if (p == null) continue;
            float h = PrefabHeight(p);
            var k = new Kind { proto = i, minScale = 0.8f, maxScale = 1.35f };
            if (h > 0f && h < 2.5f) bushes.Add(k); else trees.Add(k);
        }

        // Plus the wilderness set (RPG Poly Pack), sized to real heights.
        if (set != null)
        {
            AddKinds(set.trees, set.treeHeight, prototypes, trees);
            AddKinds(set.bushes, set.bushHeight, prototypes, bushes);
            AddKinds(set.rocks, set.rockHeight, prototypes, rocks);
        }

        if (trees.Count == 0 && bushes.Count == 0)
        {
            Object.Destroy(data);
            return;
        }

        data.treePrototypes = prototypes.ToArray();
        data.RefreshPrototypes();

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        bool[] pathLayer = PathLayers(data);
        float[,,] alpha = pathLayer != null ? data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight) : null;

        // Existing trees, so new ones keep their distance.
        var occupied = new HashSet<long>();
        TreeInstance[] old = data.treeInstances;
        for (int i = 0; i < old.Length; i++)
            occupied.Add(Key(origin + Vector3.Scale(old[i].position, size), 4f));

        var added = new List<TreeInstance>(MaxNewPerTerrain);
        var overlap = new Collider[16];
        float seed = (origin.x * 0.013f + origin.z * 0.007f) % 100f;
        System.Random rng = new System.Random((int)(origin.x * 31 + origin.z * 17));

        for (float x = Cell * 0.5f; x < size.x; x += Cell)
        {
            for (float z = Cell * 0.5f; z < size.z; z += Cell)
            {
                if (added.Count >= MaxNewPerTerrain)
                    break;

                float jx = x + ((float)rng.NextDouble() - 0.5f) * Cell * 0.9f;
                float jz = z + ((float)rng.NextDouble() - 0.5f) * Cell * 0.9f;
                float nx = Mathf.Clamp01(jx / size.x), nz = Mathf.Clamp01(jz / size.z);
                Vector3 world = new Vector3(origin.x + jx, 0f, origin.z + jz);

                // Large forests, medium copses, fine break-up.
                float forest = Mathf.PerlinNoise(world.x * 0.0028f + seed, world.z * 0.0028f + seed);
                float copse = Mathf.PerlinNoise(world.x * 0.011f + 40f, world.z * 0.011f + 40f);
                float fine = Mathf.PerlinNoise(world.x * 0.05f + 90f, world.z * 0.05f + 90f);
                float density = forest * 0.75f + copse * 0.35f + fine * 0.12f - 0.18f;

                float treeChance, bushChance, rockChance;
                if (density > 0.52f) { treeChance = 0.62f; bushChance = 0.22f; rockChance = 0.03f; }      // forest
                else if (density > 0.4f) { treeChance = 0.22f; bushChance = 0.2f; rockChance = 0.03f; }   // woodland edge
                else if (density > 0.3f) { treeChance = 0.05f; bushChance = 0.08f; rockChance = 0.02f; }  // scrub
                else { treeChance = 0.012f; bushChance = 0.025f; rockChance = 0.012f; }                   // open meadow

                double roll = rng.NextDouble();
                List<Kind> pool;
                if (roll < treeChance) pool = trees;
                else if (roll < treeChance + bushChance) pool = bushes.Count > 0 ? bushes : trees;
                else if (roll < treeChance + bushChance + rockChance) pool = rocks;
                else continue;
                if (pool.Count == 0)
                    continue;

                float steep = data.GetSteepness(nx, nz);
                if (steep > (pool == rocks ? 45f : 32f))
                    continue;

                world.y = terrain.SampleHeight(world) + origin.y;
                if (Vector2.Distance(new Vector2(world.x, world.z), new Vector2(camp.x, camp.z)) < CampClear)
                    continue;
                if (WaterBodies.InsideWater(world))
                    continue;
                if (alpha != null && OnPath(alpha, pathLayer, nx, nz, data))
                    continue;

                long key = Key(world, 4f);
                if (occupied.Contains(key))
                    continue;

                // Keep clear of buildings, walls, fences and props.
                int hits = Physics.OverlapSphereNonAlloc(world + Vector3.up * 2.5f, pool == trees ? 5f : 3f,
                    overlap, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int h = 0; h < hits; h++)
                {
                    if (!(overlap[h] is TerrainCollider))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                    continue;

                occupied.Add(key);
                Kind k = pool[rng.Next(pool.Count)];
                float s = Mathf.Lerp(k.minScale, k.maxScale, (float)rng.NextDouble());
                float tint = 0.85f + (float)rng.NextDouble() * 0.15f;
                added.Add(new TreeInstance
                {
                    prototypeIndex = k.proto,
                    position = new Vector3(nx, 0f, nz),
                    heightScale = s,
                    widthScale = s * (0.9f + (float)rng.NextDouble() * 0.2f),
                    rotation = (float)(rng.NextDouble() * Mathf.PI * 2.0),
                    color = new Color(tint, tint, tint, 1f),
                    lightmapColor = Color.white
                });
            }
        }

        var all = new TreeInstance[old.Length + added.Count];
        old.CopyTo(all, 0);
        added.CopyTo(all, old.Length);
        data.SetTreeInstances(all, true);

        terrain.terrainData = data;
        TerrainCollider col = terrain.GetComponent<TerrainCollider>();
        if (col != null)
            col.terrainData = data;
        terrain.Flush();
    }

    static long Key(Vector3 p, float cell)
    {
        long x = Mathf.FloorToInt(p.x / cell);
        long z = Mathf.FloorToInt(p.z / cell);
        return (x << 32) ^ (z & 0xffffffffL);
    }

    static void AddKinds(GameObject[] prefabs, Vector2 height, List<TreePrototype> protos, List<Kind> into)
    {
        if (prefabs == null)
            return;
        foreach (GameObject p in prefabs)
        {
            if (p == null || !ValidTreePrefab(p))
                continue;
            float h = PrefabHeight(p);
            if (h <= 0.01f)
                continue;
            float lo = height.x > 0f ? height.x / h : 0.85f;
            float hi = height.y > 0f ? height.y / h : 1.25f;
            int index = protos.FindIndex(t => t.prefab == p);
            if (index < 0)
            {
                protos.Add(new TreePrototype { prefab = p, bendFactor = 0.3f });
                index = protos.Count - 1;
            }
            into.Add(new Kind { proto = index, minScale = lo, maxScale = hi });
        }
    }

    // Terrain trees need a mesh renderer or an LOD group on the prefab root.
    static bool ValidTreePrefab(GameObject p)
    {
        if (p.GetComponent<LODGroup>() != null)
            return true;
        return p.GetComponent<MeshFilter>() != null && p.GetComponent<MeshRenderer>() != null;
    }

    static float PrefabHeight(GameObject p)
    {
        Renderer[] rs = p.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0)
            return 0f;
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (Renderer r in rs)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;
            Bounds b = mf.sharedMesh.bounds;
            float scale = r.transform.lossyScale.y;
            lo = Mathf.Min(lo, r.transform.position.y + (b.center.y - b.extents.y) * scale);
            hi = Mathf.Max(hi, r.transform.position.y + (b.center.y + b.extents.y) * scale);
        }
        return hi > lo ? hi - lo : 0f;
    }

    static bool[] PathLayers(TerrainData data)
    {
        TerrainLayer[] layers = data.terrainLayers;
        if (layers == null || layers.Length == 0)
            return null;
        bool any = false;
        bool[] path = new bool[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            string n = (layers[i].name + " " + (layers[i].diffuseTexture != null ? layers[i].diffuseTexture.name : "")).ToLowerInvariant();
            path[i] = n.Contains("path") || n.Contains("road") || n.Contains("dirt") || n.Contains("gravel")
                || n.Contains("sand") || n.Contains("mud") || n.Contains("soil") || n.Contains("cobble");
            any |= path[i];
        }
        return any ? path : null;
    }

    static bool OnPath(float[,,] alpha, bool[] path, float nx, float nz, TerrainData data)
    {
        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
        int az = Mathf.Clamp(Mathf.RoundToInt(nz * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
        float sum = 0f;
        for (int l = 0; l < path.Length && l < alpha.GetLength(2); l++)
            if (path[l])
                sum += alpha[az, ax, l];
        return sum > 0.35f;
    }

    // ------------------------------------------------------------------ ambience

    ParticleSystem pollen, butterflies, fireflies, flocks;

    void BuildAmbience()
    {
        pollen = MakeSystem("Pollen", Dot(32, 0.9f, 1f), new Color(1f, 0.97f, 0.85f, 0.55f), 260, 9f, new Vector2(0.03f, 0.07f), 18f, 0.25f, false);
        butterflies = MakeSystem("Butterflies", Butterfly(), Color.white, 24, 7f, new Vector2(0.12f, 0.2f), 16f, 1.4f, false);
        var main = butterflies.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.25f), new Color(0.95f, 0.45f, 0.75f));
        fireflies = MakeSystem("Fireflies", Dot(32, 0.5f, 1f), new Color(0.85f, 1f, 0.45f, 1f), 120, 6f, new Vector2(0.06f, 0.11f), 22f, 0.5f, true);
        flocks = MakeFlocks();
    }

    static Texture2D Dot(int size, float hardness, float alpha)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, Mathf.Lerp(2.2f, 0.6f, hardness)) * alpha;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    static Texture2D Butterfly()
    {
        const int s = 32;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                float wing = Mathf.Abs(u) > 0.08f
                    ? Mathf.Clamp01(1f - Mathf.Pow((Mathf.Abs(u) - 0.5f) / 0.45f, 2f) - Mathf.Pow((v - 0.15f * Mathf.Sign(v)) / 0.75f, 2f)) : 0f;
                float body = Mathf.Abs(u) < 0.08f && Mathf.Abs(v) < 0.6f ? 1f : 0f;
                float a = Mathf.Max(wing > 0.05f ? 1f : 0f, body);
                Color c = body > 0f ? new Color(0.15f, 0.12f, 0.1f, 1f) : new Color(1f, 1f, 1f, a);
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    static Material ParticleMaterial(Texture2D tex, bool glow)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(sh);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", glow ? 2f : 0f); // additive for fireflies
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", glow ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    }

    ParticleSystem MakeSystem(string name, Texture2D tex, Color color, int max, float life,
        Vector2 size, float radius, float noise, bool glow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = max / life;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 2f, 3f, radius * 2f);
        shape.position = new Vector3(0f, 1.6f, 0f);

        var n = ps.noise;
        n.enabled = true;
        n.strength = noise;
        n.frequency = 0.35f;
        n.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMaterial(tex, glow);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    ParticleSystem MakeFlocks()
    {
        // Distant birds: dark wing-shaped specks gliding across the sky in loose groups.
        const int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                float curve = 0.35f * Mathf.Abs(u) - 0.1f;            // a shallow "V" of wings
                float a = Mathf.Abs(v - curve) < 0.09f && Mathf.Abs(u) < 0.95f ? 1f : 0f;
                tex.SetPixel(x, y, new Color(0.08f, 0.08f, 0.09f, a));
            }
        tex.Apply();

        ParticleSystem ps = MakeSystem("BirdFlocks", tex, new Color(0.1f, 0.1f, 0.12f, 0.9f), 40, 28f,
            new Vector2(0.9f, 1.5f), 260f, 2.5f, false);
        var main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 14f);
        var shape = ps.shape;
        shape.position = new Vector3(0f, 70f, 0f);
        shape.scale = new Vector3(520f, 30f, 520f);
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 12, 1, 9f) });
        emission.burstCount = 1;
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(6f, 10f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.z = new ParticleSystem.MinMaxCurve(2f, 5f);
        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.separateAxes = true;
        sizeOver.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        sizeOver.y = new ParticleSystem.MinMaxCurve(1f, Flap());
        return ps;
    }

    static AnimationCurve Flap()
    {
        var c = new AnimationCurve();
        for (int i = 0; i <= 120; i++)
        {
            float t = i / 120f;
            c.AddKey(t, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 70f)));
        }
        return c;
    }

    void LateUpdate()
    {
        if (pollen == null)
            return;

        Vector3 p = PlayerView.Position();
        pollen.transform.position = p;
        butterflies.transform.position = p;
        fireflies.transform.position = p;
        if (flocks != null)
            flocks.transform.position = new Vector3(p.x, 0f, p.z) + new Vector3(-120f, p.y, -60f);

        DayNightCycle day = DayNightCycle.Instance;
        bool night = day != null && day.IsNight;
        float rainy = WindSystemStrength();
        SetRate(pollen, night ? 0f : 260f / 9f * (1f - rainy));
        SetRate(butterflies, night ? 0f : 24f / 7f * (1f - rainy));
        SetRate(fireflies, night ? 120f / 6f : 0f);
        if (flocks != null)
        {
            var em = flocks.emission;
            em.enabled = !night;
        }
    }

    static float WindSystemStrength()
    {
        // Heavier wind/rain thins out the insects; reuse weather fog as a cheap proxy.
        return Mathf.Clamp01((RenderSettings.fogDensity - 0.002f) / 0.006f);
    }

    static void SetRate(ParticleSystem ps, float rate)
    {
        var em = ps.emission;
        em.rateOverTime = rate;
    }
}

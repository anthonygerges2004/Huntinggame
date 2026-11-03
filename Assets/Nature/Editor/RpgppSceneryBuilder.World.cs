using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// The joined-up world: a castle on its own levelled plateau with a town at its gate, the main
// village near spawn, a fishing hamlet on the lake, roads between them, and people and
// livestock living in them.
public static partial class RpgppSceneryBuilder
{
    const string KitPath = "Assets/CastlePack/Castles_and_Forts.fbx";
    const string CastleTexturePath = GeneratedFolder + "/Castle/castle_stylized.png";
    const string CastleMaterialPath = GeneratedFolder + "/Castle/castle_stylized.mat";
    const string VillagerFolder = GeneratedFolder + "/Villagers";
    const string ChickenPrefab = "Assets/ithappy/Animals_FREE/Prefabs/Chicken_001.prefab";
    const string HorsePrefab = "Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab";

    const float CastleScale = 2.6f;      // kit is on a 2 m grid; 2.6x gives 5.2 m walls, 10.4 m segments
    const float CastleHalf = 9f;         // tower centres at +-9 kit metres
    const float CastleSearchMin = 130f;
    const float CastleSearchMax = 320f;
    const float TownGap = 28f;           // town square this far in front of the gate
    const float RoadGradeRadius = 2.2f;
    const float LakeMaxDistance = 450f;  // further than this, a farmstead near spawn replaces the lake hamlet

    static readonly List<Vector3> reserved = new List<Vector3>();       // x, z, radius - site search only
    static readonly List<Vector4> squares = new List<Vector4>();        // x, z, radius - dirt + clearings
    static readonly List<Vector3[]> roads = new List<Vector3[]>();
    static readonly List<Vector4> scatterZones = new List<Vector4>();   // x, z, radius
    static readonly List<Vector4> coverZones = new List<Vector4>();
    static readonly List<Settlement> settlements = new List<Settlement>();
    static readonly List<(Vector3 home, float radius)> chickenHomes = new List<(Vector3, float)>();
    static readonly List<(Vector3 home, float radius)> horseHomes = new List<(Vector3, float)>();
    static readonly List<(Vector3 pos, Vector3 face)> guardPosts = new List<(Vector3, Vector3)>();
    static readonly List<Vector3[]> patrols = new List<Vector3[]>();
    static readonly List<(Vector3 pos, Vector3 face)> fisherSpots = new List<(Vector3, Vector3)>();
    static List<Vector4> activeZones;
    static RoadGrid roadGrid;

    static bool hasCastle;
    static Vector3 castleCenter;
    static float castleYaw;
    static Vector3 townCenter;
    static bool hasFarm;
    static Vector3 farm;
    static bool hasLake;
    static Vector3 lakeVillage;
    static Vector3 lakeShore;
    static LakeWater homeLake;

    static void ResetWorldState()
    {
        reserved.Clear();
        squares.Clear();
        roads.Clear();
        scatterZones.Clear();
        coverZones.Clear();
        settlements.Clear();
        chickenHomes.Clear();
        horseHomes.Clear();
        guardPosts.Clear();
        patrols.Clear();
        fisherSpots.Clear();
        kitPieces = null;
        kitBoundsCache.Clear();
        hasCastle = false;
        hasLake = false;
        hasFarm = false;
        homeLake = null;
        roadGrid = new RoadGrid(20f, 14f);
    }

    static bool SiteBlocked(Vector3 p, float radius)
    {
        if (Blocked(p, radius))
            return true;
        foreach (Vector3 c in reserved)
        {
            float dx = p.x - c.x;
            float dz = p.z - c.y;
            float rr = c.z + radius;
            if (dx * dx + dz * dz < rr * rr)
                return true;
        }

        return false;
    }

    // ------------------------------------------------------------------ sites

    // A broad, fairly flat spot that stands above its surroundings, with room for a town at the gate.
    static bool FindCastleSite()
    {
        Vector3 toward = hasVillage ? village : camp;
        float half = (CastleHalf + 1f) * CastleScale;
        float bestScore = float.MaxValue;

        for (float r = CastleSearchMin; r <= CastleSearchMax; r += 20f)
        {
            int steps = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * r / 30f));
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Vector3 c = camp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!OnTerrain(c, out Vector3 g, out _) || SiteBlocked(g, half + 40f))
                    continue;
                if (!AreaRange(g, half + 6f, 5, out float lo, out float hi))
                    continue;

                Vector3 front = Flat(toward - g).normalized;
                Vector3 town = g + front * (half + TownGap);
                if (!OnTerrain(town, out Vector3 tg, out _) || SiteBlocked(tg, 40f) || !AreaRange(tg, 22f, 4, out float tlo, out float thi))
                    continue;

                float ring = 0f;
                int ringCount = 0;
                for (int k = 0; k < 8; k++)
                {
                    float ka = k * Mathf.PI / 4f;
                    if (WorldGround.TryGetGround(g + new Vector3(Mathf.Cos(ka), 0f, Mathf.Sin(ka)) * 95f, out Vector3 rp, out _))
                    {
                        ring += rp.y;
                        ringCount++;
                    }
                }

                float prominence = ringCount > 0 ? g.y - ring / ringCount : 0f;
                float score = (hi - lo) + (thi - tlo) * 0.5f - Mathf.Clamp(prominence, -10f, 25f) * 0.15f + r * 0.03f;
                if (score < bestScore)
                {
                    bestScore = score;
                    castleCenter = g;
                    castleYaw = Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg;
                    townCenter = tg;
                }
            }
        }

        return bestScore < float.MaxValue;
    }

    // Height range over a square grid of samples; false if any sample is off the terrain or wet.
    static bool AreaRange(Vector3 center, float half, int n, out float lo, out float hi)
    {
        lo = float.MaxValue;
        hi = float.MinValue;
        for (int ix = 0; ix < n; ix++)
        {
            for (int iz = 0; iz < n; iz++)
            {
                Vector3 p = center + new Vector3(Mathf.Lerp(-half, half, ix / (n - 1f)), 0f, Mathf.Lerp(-half, half, iz / (n - 1f)));
                if (!OnTerrain(p, out Vector3 g, out _) || NearWater(g))
                    return false;
                lo = Mathf.Min(lo, g.y);
                hi = Mathf.Max(hi, g.y);
            }
        }

        return true;
    }

    // A dry, gentle spot on the shore of the lake nearest the camp.
    static bool FindLakeSite()
    {
        if (lakes == null)
            return false;

        float nearest = float.MaxValue;
        foreach (LakeWater lake in lakes)
        {
            if (lake == null)
                continue;
            float d = Flat(lake.Center - camp).magnitude;
            if (d < nearest)
            {
                nearest = d;
                homeLake = lake;
            }
        }

        if (homeLake == null || nearest > LakeMaxDistance)
            return false;

        float bestScore = float.MaxValue;
        for (int i = 0; i < 45; i++)
        {
            float a = i * Mathf.PI * 2f / 45f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (float r = homeLake.Radius + 22f; r <= homeLake.Radius + 56f; r += 6f)
            {
                Vector3 c = homeLake.Center + dir * r;
                if (!OnTerrain(c, out Vector3 g, out _) || NearWater(g) || SiteBlocked(g, 45f))
                    continue;

                float lo = g.y, hi = g.y;
                int dry = 0;
                for (int k = 0; k < 16; k++)
                {
                    float ka = k * Mathf.PI / 8f;
                    Vector3 p = g + new Vector3(Mathf.Cos(ka), 0f, Mathf.Sin(ka)) * (k % 2 == 0 ? 22f : 12f);
                    if (!OnTerrain(p, out Vector3 pg, out _) || NearWater(pg))
                        continue;
                    dry++;
                    lo = Mathf.Min(lo, pg.y);
                    hi = Mathf.Max(hi, pg.y);
                }

                if (dry < 11)
                    continue;

                float score = (hi - lo) + Flat(g - camp).magnitude * 0.004f + (r - homeLake.Radius) * 0.04f;
                if (score < bestScore)
                {
                    bestScore = score;
                    lakeVillage = g;
                }
            }
        }

        if (bestScore == float.MaxValue)
            return false;

        // Walk from the hamlet toward the water to find the shoreline for the jetty.
        Vector3 toLake = Flat(homeLake.Center - lakeVillage).normalized;
        lakeShore = lakeVillage;
        for (float d = 0f; d < 80f; d += 1f)
        {
            Vector3 p = lakeVillage + toLake * d;
            if (WaterBodies.GroundY(p) <= homeLake.SurfaceY + 0.1f && homeLake.Inside(p, 0f))
            {
                lakeShore = p;
                break;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ roads

    static void PlanRoads()
    {
        if (hasVillage)
            AddRoad(camp, village);
        if (hasCastle)
        {
            AddRoad(hasVillage ? village : camp, townCenter);
            Vector3 gate = castleCenter + Quaternion.Euler(0f, castleYaw, 0f) * Vector3.forward * ((CastleHalf + 1.5f) * CastleScale);
            AddRoad(townCenter, gate);
        }

        if (hasLake)
        {
            AddRoad(hasVillage ? village : camp, lakeVillage);
        }

        if (hasFarm)
            AddRoad(hasVillage ? village : camp, farm);

        int length = 0;
        foreach (Vector3[] road in roads)
        {
            for (int i = 1; i < road.Length; i++)
                length += Mathf.RoundToInt(Flat(road[i] - road[i - 1]).magnitude);
        }

        report.Add("Roads: " + roads.Count + " roads, " + length + " m in all.");
    }

    // A gently winding road from a to b that steers around water. The ends run straight in.
    static void AddRoad(Vector3 a, Vector3 b)
    {
        float len = Flat(b - a).magnitude;
        int n = Mathf.Max(2, Mathf.CeilToInt(len / 10f));
        Vector3 along = Flat(b - a).normalized;
        Vector3 perp = Vector3.Cross(Vector3.up, along);
        float amplitude = Mathf.Min(14f, len * 0.07f);
        float p1 = Range(0f, 6.28f);
        float p2 = Range(0f, 6.28f);

        Vector3[] points = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float envelope = Mathf.Sin(t * Mathf.PI);
            envelope *= envelope;
            float wave = Mathf.Sin(t * 2.7f * Mathf.PI + p1) * 0.7f + Mathf.Sin(t * 5.3f * Mathf.PI + p2) * 0.3f;
            Vector3 p = Vector3.Lerp(a, b, t) + perp * (envelope * amplitude * wave);

            for (int k = 0; k < 30 && i > 0 && i < n && NearWater(p); k++)
                p += AwayFromWater(p) * 4f;

            WorldGround.TryGetGround(p, out Vector3 g, out _);
            points[i] = g;
        }

        roads.Add(points);
        for (int i = 1; i < points.Length; i++)
            roadGrid.Add(points[i - 1], points[i]);
    }

    static Vector3 AwayFromWater(Vector3 p)
    {
        Vector3 best = Vector3.zero;
        float bestDist = float.MaxValue;
        if (lakes != null)
        {
            foreach (LakeWater lake in lakes)
            {
                if (lake == null)
                    continue;
                float d = Flat(p - lake.Center).magnitude - lake.Radius;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = Flat(p - lake.Center).normalized;
                }
            }
        }

        if (best == Vector3.zero)
            best = Vector3.forward;
        return best;
    }

    static float RoadDistance(Vector3 p)
    {
        return roadGrid != null ? roadGrid.Distance(p) : float.MaxValue;
    }

    // Smooths the ground across each road so it reads as a worn track, not paint on a slope.
    static void GradeRoads()
    {
        foreach (Vector3[] road in roads)
        {
            for (int i = 1; i < road.Length; i++)
            {
                Vector3 a = road[i - 1];
                Vector3 b = road[i];
                float len = Flat(b - a).magnitude;
                for (float d = 0f; d < len; d += 3f)
                {
                    Vector3 p = Vector3.Lerp(a, b, d / Mathf.Max(0.01f, len));
                    if (NearSettledGround(p))
                        continue;
                    if (!WorldGround.TryGetGround(p, out Vector3 g, out _))
                        continue;
                    LevelDisc(g, RoadGradeRadius, g.y, 2.5f);
                }
            }
        }
    }

    static bool NearSettledGround(Vector3 p)
    {
        if (Flat(p - camp).magnitude < CampFlatRadius + CampFalloff)
            return true;
        foreach (Vector4 s in squares)
        {
            if (Flat(p - new Vector3(s.x, 0f, s.z)).magnitude < s.w + 10f)
                return true;
        }

        return hasCastle && Flat(p - castleCenter).magnitude < (CastleHalf + 1f) * CastleScale * 1.45f + 6f;
    }

    // ------------------------------------------------------------------ settlements

    class Plot
    {
        public string name;
        public float angle, rMin, rMax, swing = 25f, drop = 5f;
        public PropSpot[] props = new PropSpot[0];
        public bool fence;
        public bool door = true;

        public Plot(string name, float angle, float rMin, float rMax, PropSpot[] props, bool fence = false, bool door = true)
        {
            this.name = name; this.angle = angle; this.rMin = rMin; this.rMax = rMax;
            this.props = props; this.fence = fence; this.door = door;
        }
    }

    // A village: a levelled round square with a well, benches and flowers, buildings around it
    // facing in, and a Settlement describing where people walk. Angle 0 = `entranceDeg`.
    static Settlement BuildSettlement(Transform parent, string title, Vector3 center, float entranceDeg, Plot[] plots,
                                      int benches = 4)
    {
        GameObject root = parent.gameObject;
        const float squareRadius = 14f;
        LevelDisc(center, squareRadius, center.y, 12f);
        squares.Add(new Vector4(center.x, 0f, center.z, squareRadius - 1f));

        float wellHalf = 1.3f;
        if (TryNative("rpgpp_lt_well_01", out Native well))
            wellHalf = Mathf.Max(well.bounds.size.x, well.bounds.size.z) * packScale * 0.5f;
        PlaceStructure(parent, "rpgpp_lt_well_01", center, center, entranceDeg, 0f, 0f, 0f, 3f, new[]
        {
            new PropSpot("rpgpp_lt_bucket_01", 1f, 0.4f, 0.4f, 0f),
        }, false, out _);

        float ring = Mathf.Max(7f, wellHalf + 4.8f);
        List<Vector3> spots = new List<Vector3>();
        for (int i = 0; i < benches; i++)
        {
            float a = (entranceDeg + 90f + i * (360f / Mathf.Max(1, benches))) * Mathf.Deg2Rad;
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wellHalf + 2.4f);
            SpawnAt(parent, i % 2 == 0 ? "rpgpp_lt_bench_wood_01" : "rpgpp_lt_bench_wood_03", p, Face(p, center));
            float between = a + Mathf.PI / Mathf.Max(1, benches);
            spots.Add(WorldGround.Snap(center + new Vector3(Mathf.Cos(between), 0f, Mathf.Sin(between)) * (ring - 1.4f)));
        }

        int flowers = Mathf.RoundToInt(2f * Mathf.PI * (wellHalf + 0.7f) / 0.9f);
        for (int i = 0; i < flowers; i++)
        {
            float a = (entranceDeg + i * 360f / flowers) * Mathf.Deg2Rad;
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wellHalf + 0.7f);
            SpawnAt(parent, Flowers[i % Flowers.Length], p, Quaternion.Euler(0f, Range(0f, 360f), 0f));
        }

        List<Vector3> doors = new List<Vector3>();
        int built = 1;
        foreach (Plot plot in plots)
        {
            GameObject b = PlaceStructure(parent, plot.name, center, center, entranceDeg + plot.angle, plot.rMin, plot.rMax,
                                          plot.swing, plot.drop, plot.props, plot.fence, out Vector3 front);
            if (b == null)
                continue;
            built++;
            if (plot.door)
                doors.Add(front);
            else
                spots.Add(front);
        }

        Settlement s = root.AddComponent<Settlement>();
        s.title = title;
        s.center = center;
        s.ringRadius = ring;
        s.doors = doors.ToArray();
        s.spots = spots.ToArray();
        settlements.Add(s);

        keepOutCircles.Add(new Vector3(center.x, center.z, squareRadius + 2f));
        report.Add(title + ": " + built + " buildings and structures, " +
                   Mathf.RoundToInt(Flat(center - camp).magnitude) + " m " + Compass(center - camp) + " of spawn.");
        return s;
    }

    // Drops doors whose walk to the square is blocked, so villagers never walk through walls.
    static void PruneBlockedDoors(Settlement s)
    {
        Physics.SyncTransforms();
        List<Vector3> ok = new List<Vector3>();
        foreach (Vector3 d in s.doors)
        {
            Vector3 ringPoint = s.RingPoint(s.AngleOf(d));
            Vector3 a = d + Vector3.up * 0.9f;
            Vector3 b = ringPoint + Vector3.up * 0.9f;
            if (!Physics.Linecast(a, b, ~0, QueryTriggerInteraction.Ignore))
                ok.Add(d);
        }

        s.doors = ok.ToArray();
    }

    static void BuildMainVillage(Transform parent)
    {
        if (!hasVillage)
            return;

        float entrance = Mathf.Atan2(camp.z - village.z, camp.x - village.x) * Mathf.Rad2Deg;
        Plot[] plots =
        {
            new Plot("rpgpp_lt_awning_standing_01a", 42f, 10f, 13f, StallProps(false), door: false) { swing = 15f, drop = 3f },
            new Plot("rpgpp_lt_awning_standing_01b", -42f, 10f, 13f, StallProps(true), door: false) { swing = 15f, drop = 3f },
            new Plot("rpgpp_lt_building_01", 75f, 18f, 27f, HouseProps(0), true),
            new Plot("rpgpp_lt_building_02", 128f, 18f, 27f, HouseProps(1), true),
            new Plot("rpgpp_lt_building_03", 180f, 18f, 27f, HouseProps(2), true),
            new Plot("rpgpp_lt_building_04", 232f, 18f, 27f, HouseProps(3), true),
            new Plot("rpgpp_lt_building_05", 285f, 18f, 27f, HouseProps(4), true),
            new Plot("rpgpp_lt_shed_wood_01", 100f, 33f, 45f, ShedProps()),
            new Plot("rpgpp_lt_building_03", 154f, 33f, 45f, HouseProps(5), true),
            new Plot("rpgpp_lt_building_01", 206f, 33f, 45f, HouseProps(6), true),
            new Plot("rpgpp_lt_building_05", 258f, 33f, 45f, HouseProps(7), true),
            new Plot("rpgpp_lt_shed_wood_02", 310f, 33f, 45f, ShedProps()),
            new Plot("rpgpp_lt_wagon_01", 18f, 14f, 17f, WagonProps(), door: false) { swing = 15f, drop = 3f },
            new Plot("rpgpp_lt_trough_01", -70f, 12f, 15f, new PropSpot[0], door: false) { swing = 15f, drop = 2f },
        };

        Settlement s = BuildSettlement(parent, "Main village", village, entrance, plots);
        Banners(parent, village, entrance, 15f);
        AddLivestock(s, 6, 0);
        horseHomes.Add((WorldGround.Snap(village + Dir(entrance + 18f) * 19f), 3f));
        horseHomes.Add((WorldGround.Snap(village + Dir(entrance + 26f) * 19f), 3f));
    }

    static void BuildCastleTown(Transform parent)
    {
        if (!hasCastle)
            return;

        townCenter = WorldGround.Snap(townCenter);
        float entrance = Mathf.Atan2(castleCenter.z - townCenter.z, castleCenter.x - townCenter.x) * Mathf.Rad2Deg;
        Plot[] plots =
        {
            new Plot("rpgpp_lt_awning_standing_01b", 35f, 10f, 13f, StallProps(true), door: false) { swing = 15f, drop = 3f },
            new Plot("rpgpp_lt_awning_standing_01a", -35f, 10f, 13f, StallProps(false), door: false) { swing = 15f, drop = 3f },
            new Plot("rpgpp_lt_building_02", 70f, 18f, 27f, HouseProps(8), true),
            new Plot("rpgpp_lt_building_04", 118f, 18f, 27f, HouseProps(9), true),
            new Plot("rpgpp_lt_building_01", 166f, 18f, 27f, HouseProps(10), true),
            new Plot("rpgpp_lt_building_05", 214f, 18f, 27f, HouseProps(11), true),
            new Plot("rpgpp_lt_building_03", 262f, 18f, 27f, HouseProps(12), true),
            new Plot("rpgpp_lt_shed_wood_01", 140f, 33f, 44f, ShedProps()),
            new Plot("rpgpp_lt_building_02", 190f, 33f, 44f, HouseProps(13), true),
            new Plot("rpgpp_lt_shed_wood_02", 238f, 33f, 44f, ShedProps()),
            new Plot("rpgpp_lt_wagon_01", 300f, 13f, 17f, WagonProps(), door: false) { swing = 15f, drop = 3f },
        };

        Settlement s = BuildSettlement(parent, "Castle town", townCenter, entrance, plots);
        Banners(parent, townCenter, entrance, 15f);
        AddLivestock(s, 5, 0);
    }

    static void BuildLakeHamlet(Transform parent)
    {
        if (!hasLake)
            return;

        float toLake = Mathf.Atan2(lakeShore.z - lakeVillage.z, lakeShore.x - lakeVillage.x) * Mathf.Rad2Deg;
        Plot[] plots =
        {
            new Plot("rpgpp_lt_building_01", 75f, 17f, 25f, HouseProps(14), true),
            new Plot("rpgpp_lt_building_04", 135f, 17f, 25f, HouseProps(15), true),
            new Plot("rpgpp_lt_building_02", 225f, 17f, 25f, HouseProps(16), true),
            new Plot("rpgpp_lt_building_05", 285f, 17f, 25f, HouseProps(17), true),
            new Plot("rpgpp_lt_shed_wood_01", 180f, 22f, 32f, ShedProps()),
            new Plot("rpgpp_lt_awning_standing_01a", 40f, 10f, 13f, StallProps(false), door: false) { swing = 15f, drop = 3f },
        };

        Settlement s = BuildSettlement(parent, "Lakeside hamlet", lakeVillage, toLake, plots, benches: 3);
        AddLivestock(s, 4, 0);
        BuildJetty(parent, toLake);

        // Fish drying racks, barrels and nets by the shore.
        for (int i = -1; i <= 1; i += 2)
        {
            Vector3 p = lakeVillage + Dir(toLake + i * 28f) * 11f;
            SpawnAt(parent, i < 0 ? "rpgpp_lt_hanger_wood_01" : "rpgpp_lt_hanger_wood_02", p, Face(p, lakeShore));
            SpawnAt(parent, "rpgpp_lt_barrel_01", p + Dir(toLake + i * 90f) * 2.2f, Quaternion.identity);
            SpawnAt(parent, "rpgpp_lt_crate_02", p + Dir(toLake + i * 90f) * 3.4f, Quaternion.Euler(0f, 20f * i, 0f));
        }
    }

    // A plank jetty from the shore out over the water, with two people fishing off the end.
    static void BuildJetty(Transform parent, float toLakeDeg)
    {
        string tile = "rpgpp_lt_wood_path_01a";
        if (homeLake == null || !TryNative(tile, out Native native))
            return;

        Vector3 dir = Dir(toLakeDeg);
        bool alongZ = native.bounds.size.z >= native.bounds.size.x;
        float tileLength = Mathf.Max(native.bounds.size.x, native.bounds.size.z) * packScale;
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (alongZ ? 0f : 90f);
        float deck = homeLake.SurfaceY + 0.35f;
        int count = Mathf.Clamp(Mathf.CeilToInt(14f / Mathf.Max(0.5f, tileLength)), 3, 12);

        Vector3 start = lakeShore - dir * tileLength * 1.5f;
        Vector3 end = start;
        for (int i = 0; i < count; i++)
        {
            Vector3 c = start + dir * (tileLength * (i + 0.5f));
            float ground = WaterBodies.GroundY(c);
            float y = Mathf.Max(deck, ground + 0.05f);
            Quaternion plankRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pivot = new Vector3(c.x, 0f, c.z) -
                            plankRot * new Vector3(native.bounds.center.x, 0f, native.bounds.center.z) * packScale;
            pivot.y = y - native.bounds.min.y * packScale;
            GameObject plank = Spawn(i % 2 == 0 ? tile : "rpgpp_lt_wood_path_01b", parent, pivot, plankRot, packScale);
            if (plank != null)
            {
                AddCollider(plank, native, ColliderKind.Box);
                MarkStatic(plank);
            }

            end = new Vector3(c.x, y + native.bounds.size.y * packScale, c.z);
        }

        Vector3 side = Vector3.Cross(Vector3.up, dir);
        fisherSpots.Add((end - dir * 0.6f + side * 0.6f, dir));
        fisherSpots.Add((end - dir * 1.8f - side * 0.6f, dir));
    }

    static PropSpot[] WagonProps()
    {
        return new[]
        {
            new PropSpot("rpgpp_lt_sack_01", 1f, 0.5f, 0.5f, 20f),
            new PropSpot("rpgpp_lt_sack_02", 1f, -0.2f, 0.5f, -35f),
            new PropSpot("rpgpp_lt_crate_03", -1f, 0.3f, 0.5f, 10f),
            new PropSpot("rpgpp_lt_barrel_02", -1f, -0.6f, 0.5f, 0f),
        };
    }

    static void Banners(Transform parent, Vector3 center, float entranceDeg, float r)
    {
        for (int i = -1; i <= 1; i += 2)
        {
            Vector3 p = center + Dir(entranceDeg + i * 12f) * r;
            SpawnAt(parent, i < 0 ? "rpgpp_lt_banner_01a" : "rpgpp_lt_banner_01b", p, Face(p, center + Dir(entranceDeg) * 40f));
        }
    }

    static void AddLivestock(Settlement s, int chickens, int horses)
    {
        for (int i = 0; i < chickens && s.doors.Length > 0; i++)
        {
            Vector3 door = s.doors[i % s.doors.Length];
            chickenHomes.Add((door + (door - s.center).normalized * 1.5f, 4f));
        }
    }

    static Vector3 Dir(float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    // ------------------------------------------------------------------ farmstead

    const float FarmSearchMin = 100f;
    const float FarmSearchMax = 240f;

    // Gentle ground near spawn, clear of the village and the castle, for a small farm.
    static bool FindFarmSite()
    {
        float bestScore = float.MaxValue;
        for (float r = FarmSearchMin; r <= FarmSearchMax; r += 12f)
        {
            int steps = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * r / 18f));
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Vector3 c = camp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!OnTerrain(c, out Vector3 g, out _) || SiteBlocked(g, 48f) || !AreaRange(g, 26f, 5, out float lo, out float hi))
                    continue;

                float score = (hi - lo) + r * 0.03f;
                if (score < bestScore)
                {
                    bestScore = score;
                    farm = g;
                }
            }
        }

        return bestScore < float.MaxValue;
    }

    static void BuildFarmstead(Transform parent)
    {
        if (!hasFarm)
            return;

        farm = WorldGround.Snap(farm);
        float entrance = Mathf.Atan2((hasVillage ? village : camp).z - farm.z, (hasVillage ? village : camp).x - farm.x) * Mathf.Rad2Deg;

        // The paddock and the field go down first so the buildings fit around them.
        BuildPaddock(parent, farm + Dir(entrance + 180f) * 27f, entrance);
        BuildField(parent, farm + Dir(entrance + 95f) * 27f, entrance + 95f);

        Plot[] plots =
        {
            new Plot("rpgpp_lt_building_04", 40f, 17f, 24f, HouseProps(18), true),
            new Plot("rpgpp_lt_building_02", 140f, 17f, 24f, HouseProps(19), true),
            new Plot("rpgpp_lt_shed_wood_01", 230f, 17f, 26f, ShedProps()),
            new Plot("rpgpp_lt_shed_wood_02", 300f, 17f, 26f, ShedProps()),
            new Plot("rpgpp_lt_wagon_01", -30f, 11f, 15f, WagonProps(), door: false) { swing = 15f, drop = 3f },
            new Plot("rpgpp_lt_trough_01", 200f, 11f, 15f, new PropSpot[0], door: false) { swing = 15f, drop = 2f },
        };

        Settlement s = BuildSettlement(parent, "Farmstead", farm, entrance, plots, benches: 2);
        AddLivestock(s, 8, 0);
    }

    // A fenced square with a gate gap facing the farmyard, and horses grazing inside.
    static void BuildPaddock(Transform parent, Vector3 center, float entranceDeg)
    {
        if (!OnTerrain(center, out Vector3 g, out _) || NearWater(g) || Blocked(g, 10f))
            return;

        const float half = 7f;
        Quaternion rot = Quaternion.Euler(0f, 90f - entranceDeg, 0f);
        Level(g, rot, half, half, g.y, 6f);
        string[] pieces = { "rpgpp_lt_fence_wood_02a", "rpgpp_lt_fence_wood_02b" };
        if (!TryNative(pieces[0], out Native native))
            return;

        float length = Mathf.Max(0.5f, native.bounds.size.x * packScale);
        int perSide = Mathf.Max(2, Mathf.RoundToInt(half * 2f / length));
        for (int side = 0; side < 4; side++)
        {
            Quaternion sideRot = rot * Quaternion.Euler(0f, side * 90f, 0f);
            for (int i = 0; i < perSide; i++)
            {
                // Leave a gate gap in the middle of the side facing the farmyard.
                if (side == 0 && Mathf.Abs(i - (perSide - 1) * 0.5f) < 0.75f)
                    continue;
                float x = -half + length * (i + 0.5f) * (half * 2f / (length * perSide));
                Vector3 p = g + sideRot * new Vector3(x, 0f, half);
                SpawnAt(parent, pieces[i % pieces.Length], p, sideRot);
            }
        }

        keepOutCircles.Add(new Vector3(g.x, g.z, half + 2f));
        horseHomes.Add((g + rot * new Vector3(-2.5f, 0f, -1f), half - 2f));
        horseHomes.Add((g + rot * new Vector3(2.5f, 0f, 1.5f), half - 2f));
        horseHomes.Add((g + rot * new Vector3(0f, 0f, -3f), half - 2f));
    }

    // Rows of leafy crops on tilled dirt.
    static void BuildField(Transform parent, Vector3 center, float facingDeg)
    {
        if (!OnTerrain(center, out Vector3 g, out _) || NearWater(g) || Blocked(g, 9f))
            return;

        const float hx = 6f, hz = 8f;
        Quaternion rot = Quaternion.Euler(0f, 90f - facingDeg, 0f);
        Level(g, rot, hx, hz, g.y, 5f);
        dirtPads.Add(new Pad { center = g, rot = rot, hx = hx, hz = hz });
        for (float x = -hx + 1f; x <= hx - 1f; x += 1.6f)
        {
            for (float z = -hz + 1f; z <= hz - 1f; z += 1.1f)
            {
                Vector3 p = g + rot * new Vector3(x, 0f, z);
                GameObject crop = SpawnAt(parent, (Mathf.RoundToInt(x * 10f) % 2 == 0) ? "rpgpp_lt_plant_01" : "rpgpp_lt_plant_02", p,
                                          Quaternion.Euler(0f, Range(0f, 360f), 0f));
                if (crop != null)
                {
                    foreach (Collider c in crop.GetComponentsInChildren<Collider>())
                        Object.DestroyImmediate(c);
                }
            }
        }

        keepOutCircles.Add(new Vector3(g.x, g.z, Mathf.Max(hx, hz) + 2f));
    }

    // ------------------------------------------------------------------ castle

    static Dictionary<string, Transform> kitPieces;
    static readonly Dictionary<string, Bounds> kitBoundsCache = new Dictionary<string, Bounds>();
    static float kitNorm = 1f;
    static Material castleMaterial;

    static bool LoadKit()
    {
        GameObject kit = AssetDatabase.LoadAssetAtPath<GameObject>(KitPath);
        if (kit == null)
        {
            report.Add("Castle kit not found at " + KitPath + "; no castle built.");
            return false;
        }

        kitPieces = new Dictionary<string, Transform>();
        foreach (Transform t in kit.GetComponentsInChildren<Transform>(true))
            kitPieces[t.name] = t;

        Bounds tower = KitBounds("Tower_Mid");
        kitNorm = tower.size.y > 0.001f ? 2f / tower.size.y : 1f;
        castleMaterial = CastleMaterial();
        return true;
    }

    static Material CastleMaterial()
    {
        EnsureFolder(GeneratedFolder + "/Castle");
        // Start from a copy of the kit's own material so every property and keyword matches a
        // material that is known to render correctly.
        if (AssetDatabase.LoadAssetAtPath<Material>(CastleMaterialPath) != null)
            AssetDatabase.DeleteAsset(CastleMaterialPath);
        Material walls = AssetDatabase.LoadAssetAtPath<Material>("Assets/CastlePack/Textures/Walls.mat");
        Material mat;
        if (walls != null)
        {
            mat = new Material(walls);
        }
        else
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
                return null;
            mat = new Material(lit);
        }

        mat.name = "castle_stylized";
        AssetDatabase.CreateAsset(mat, CastleMaterialPath);

        AssetDatabase.ImportAsset(CastleTexturePath);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CastleTexturePath);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.08f);
        mat.SetFloat("_Metallic", 0f);
        mat.enableInstancing = false;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static GameObject CopyPiece(string name, Transform parent)
    {
        if (kitPieces == null || !kitPieces.TryGetValue(name, out Transform src))
        {
            Debug.LogWarning(Log + "Castle piece missing: " + name);
            return null;
        }

        GameObject copy = Object.Instantiate(src.gameObject);
        copy.name = name;
        Vector3 scale = copy.transform.localScale;
        Quaternion rot = copy.transform.localRotation;
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = rot;
        copy.transform.localScale = scale;
        return copy;
    }

    // Bounds of a kit piece at yaw 0, scale 1, pivot at the origin.
    static Bounds KitBounds(string name)
    {
        if (kitBoundsCache.TryGetValue(name, out Bounds cached))
            return cached;

        GameObject holder = new GameObject("_kitMeasure");
        GameObject copy = CopyPiece(name, holder.transform);
        Bounds b = new Bounds();
        if (copy != null)
            TryRenderBounds(copy, out b);
        Object.DestroyImmediate(holder);
        kitBoundsCache[name] = b;
        return b;
    }

    // Places a kit piece at castle-local kit metres. Walls pick the yaw that runs them `along`
    // with their walkway side facing `inward`.
    static GameObject Piece(Transform castle, string name, float x, float y, float z, float yawDeg)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(castle, false);
        holder.transform.localPosition = new Vector3(x, y, z) / kitNorm;
        holder.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        GameObject copy = CopyPiece(name, holder.transform);
        if (copy == null)
        {
            Object.DestroyImmediate(holder);
            return null;
        }

        foreach (MeshRenderer r in copy.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (castleMaterial != null && (mats[i] == null || mats[i].name.StartsWith("Walls")))
                    mats[i] = castleMaterial;
            }

            r.sharedMaterials = mats;
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null && r.GetComponent<Collider>() == null)
                r.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
        }

        // Not static-batched: batching the castle together with other packs showed the wrong
        // texture on it in Play mode.
        placed++;
        return holder;
    }

    static float WallYaw(string name, Vector3 along, Vector3 inward)
    {
        Bounds b = KitBounds(name);
        bool lengthZ = b.size.z >= b.size.x;
        Vector3 lengthAxis = lengthZ ? Vector3.forward : Vector3.right;
        float offset = lengthZ ? b.center.x : b.center.z;
        Vector3 walkAxis = (lengthZ ? Vector3.right : Vector3.forward) * (Mathf.Abs(offset) < 0.01f ? 0f : Mathf.Sign(offset));

        float bestYaw = 0f;
        float best = float.MinValue;
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 90f * k, 0f);
            float score = Mathf.Abs(Vector3.Dot(q * lengthAxis, along)) * 10f + Vector3.Dot(q * walkAxis, inward);
            if (score > best)
            {
                best = score;
                bestYaw = 90f * k;
            }
        }

        return bestYaw;
    }

    static void Tower(Transform castle, float x, float z, string[] levels, string cap)
    {
        float y = 0f;
        foreach (string level in levels)
        {
            Piece(castle, level, x, y, z, 0f);
            y += 2f;
        }

        if (!string.IsNullOrEmpty(cap))
            Piece(castle, cap, x, y, z, 0f);
    }

    static void WallRun(Transform castle, string name, Vector3 along, Vector3 inward, Vector3 sideOrigin, float[] offsets)
    {
        float yaw = WallYaw(name, along, inward);
        foreach (float o in offsets)
        {
            Vector3 p = sideOrigin + along * o;
            Piece(castle, name, p.x, 0f, p.z, yaw);
        }
    }

    static Transform BuildStronghold(Transform parent)
    {
        if (!hasCastle || !LoadKit())
            return null;

        float half = (CastleHalf + 1f) * CastleScale;
        Quaternion rot = Quaternion.Euler(0f, castleYaw, 0f);
        AreaRange(castleCenter, half, 5, out float lo, out float hi);
        float padY = (lo + hi) * 0.5f;
        Level(castleCenter, rot, half + 4f, half + 4f, padY, 22f);
        dirtPads.Add(new Pad { center = castleCenter, rot = rot, hx = half - 2f, hz = half - 2f });
        keepOutCircles.Add(new Vector3(castleCenter.x, castleCenter.z, half * 1.45f));
        castleCenter.y = padY;

        GameObject root = new GameObject("Stronghold");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(new Vector3(castleCenter.x, padY - 0.1f, castleCenter.z), rot);
        root.transform.localScale = Vector3.one * CastleScale * kitNorm;
        Transform c = root.transform;
        float C = CastleHalf;

        // Corner towers with cone roofs.
        string[] corner = { "Tower_Doorway", "Tower_Mid", "Tower_Mid" };
        Tower(c, -C, -C, corner, "Roof_Cone");
        Tower(c, C, -C, corner, "Roof_Cone");
        Tower(c, -C, C, corner, "Roof_Cone");
        Tower(c, C, C, corner, "Roof_Cone");

        // Gatehouse: a gate between two battlemented towers on the front (+Z) wall.
        string[] gateTower = { "Tower_Mid", "Tower_Mid" };
        Tower(c, -3f, C, gateTower, "Tower_top_1");
        Tower(c, 3f, C, gateTower, "Tower_top_1");
        Piece(c, "Gate_2x4", 0f, 0f, C, WallYaw("Gate_2x4", Vector3.right, Vector3.back));

        // Curtain walls with walkways facing in.
        WallRun(c, "Wall_2x4_walkway", Vector3.right, Vector3.back, new Vector3(0f, 0f, C), new[] { -6f, 6f });
        WallRun(c, "Wall_2x4_walkway", Vector3.right, Vector3.forward, new Vector3(0f, 0f, -C), new[] { -6f, -2f, 2f, 6f });
        WallRun(c, "Wall_2x4_walkway", Vector3.forward, Vector3.right, new Vector3(-C, 0f, 0f), new[] { -6f, -2f, 2f, 6f });
        WallRun(c, "Wall_2x4_walkway", Vector3.forward, Vector3.left, new Vector3(C, 0f, 0f), new[] { -6f, -2f, 2f, 6f });

        // The keep: a three-storey hall against the back wall, with a tall tower beside it.
        float keepYaw = WallYaw("Building_Shape_Beveled_rectangle", Vector3.forward, Vector3.zero);
        for (int level = 0; level < 3; level++)
        {
            Piece(c, "Building_Shape_Beveled_rectangle", -1f, level * 2f, -5.6f, keepYaw);
            Piece(c, "Building_Shape_Beveled_rectangle", 1f, level * 2f, -5.6f, keepYaw);
        }

        float keepRoofYaw = WallYaw("Roof_rectangle", Vector3.forward, Vector3.zero);
        Piece(c, "Roof_rectangle", -1f, 6f, -5.6f, keepRoofYaw);
        Piece(c, "Roof_rectangle", 1f, 6f, -5.6f, keepRoofYaw);
        Tower(c, 3.6f, -5.6f, new[] { "Tower_Doorway", "Tower_Mid", "Tower_Mid", "Tower_Mid" }, "Tower_top_1");

        // Houses along the side walls.
        Piece(c, "Building_Shape_Beveled_cube", -6.4f, 0f, 1.8f, 0f);
        Piece(c, "Roof_Cube", -6.4f, 2f, 1.8f, 0f);
        Piece(c, "Building_Shape_Beveled_cube", -6.4f, 0f, -1.4f, 0f);
        Piece(c, "Building_Shape_Beveled_cube", -6.4f, 2f, -1.4f, 0f);
        Piece(c, "Roof_Cube", -6.4f, 4f, -1.4f, 0f);
        float sideYaw = WallYaw("Building_Shape_Beveled_rectangle", Vector3.forward, Vector3.zero);
        Piece(c, "Building_Shape_Beveled_rectangle", 6.4f, 0f, 1.2f, sideYaw);
        Piece(c, "Roof_rectangle", 6.4f, 2f, 1.2f, WallYaw("Roof_rectangle", Vector3.forward, Vector3.zero));
        Piece(c, "Building_Shape_Beveled_cube", 6.4f, 0f, -3f, 0f);
        Piece(c, "Roof_Cube", 6.4f, 2f, -3f, 0f);

        // The courtyard: well, market stalls, a wagon, barrels and banners.
        Vector3 yard = CastleWorld(c, 0f, 2.4f);
        Settlement s = BuildCourtyard(parent, c, yard);

        // Guards at the gate, a patrol along the inside of the front wall.
        Vector3 gateOut = CastleWorld(c, 0f, C + 1.6f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        guardPosts.Add((gateOut + right * 3.2f, fwd));
        guardPosts.Add((gateOut - right * 3.2f, fwd));
        patrols.Add(new[] { CastleWorld(c, -5.5f, 6.6f), CastleWorld(c, 5.5f, 6.6f) });
        patrols.Add(new[] { CastleWorld(c, -2.5f, -2.4f), CastleWorld(c, 2.5f, -2.4f) });

        Vector3 bannerL = CastleWorld(c, -2.2f, C + 3.2f);
        Vector3 bannerR = CastleWorld(c, 2.2f, C + 3.2f);
        SpawnAt(parent, "rpgpp_lt_banner_01a", bannerL, Quaternion.LookRotation(fwd));
        SpawnAt(parent, "rpgpp_lt_banner_01b", bannerR, Quaternion.LookRotation(fwd));

        report.Add("Castle built " + Mathf.RoundToInt(Flat(castleCenter - camp).magnitude) + " m " +
                   Compass(castleCenter - camp) + " of spawn on a levelled plateau (ground was " +
                   (hi - lo).ToString("0.0") + " m uneven before levelling).");
        return c;
    }

    static Vector3 CastleWorld(Transform castle, float x, float z)
    {
        Vector3 p = castle.TransformPoint(new Vector3(x, 0f, z) / kitNorm);
        return WorldGround.Snap(p);
    }

    static Settlement BuildCourtyard(Transform parent, Transform castle, Vector3 yard)
    {
        GameObject go = new GameObject("Courtyard");
        go.transform.SetParent(parent, false);
        Transform t = go.transform;
        Quaternion rot = castle.rotation;

        SpawnAt(t, "rpgpp_lt_well_01", yard, rot);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        SpawnAt(t, "rpgpp_lt_awning_standing_01a", yard + right * 8.5f + fwd * 6f, Face(yard + right * 8.5f + fwd * 6f, yard));
        SpawnAt(t, "rpgpp_lt_awning_standing_01b", yard - right * 8.5f + fwd * 6f, Face(yard - right * 8.5f + fwd * 6f, yard));
        SpawnAt(t, "rpgpp_lt_wagon_01", yard - right * 7.3f - fwd * 6f, Quaternion.LookRotation(fwd));
        SpawnAt(t, "rpgpp_lt_trough_01", yard + right * 9f - fwd * 7f, Quaternion.LookRotation(right));
        string[] clutter = { "rpgpp_lt_barrel_01", "rpgpp_lt_barrel_02", "rpgpp_lt_crate_01", "rpgpp_lt_crate_02", "rpgpp_lt_sack_02_set" };
        for (int i = 0; i < clutter.Length; i++)
            SpawnAt(t, clutter[i], yard + right * (10.5f + (i % 2) * 1.2f) + fwd * (1.5f - i * 1.4f), Quaternion.Euler(0f, i * 37f, 0f));
        SpawnAt(t, "rpgpp_lt_banner_01a", yard - fwd * 7.5f - right * 3f, Quaternion.LookRotation(fwd));
        SpawnAt(t, "rpgpp_lt_banner_01b", yard - fwd * 7.5f + right * 3f, Quaternion.LookRotation(fwd));

        horseHomes.Add((WorldGround.Snap(yard - right * 7.5f - fwd * 2.5f), 2.5f));
        horseHomes.Add((WorldGround.Snap(yard + right * 6f - fwd * 8f), 2.5f));
        chickenHomes.Add((WorldGround.Snap(yard - right * 6f + fwd * 2f), 3f));
        chickenHomes.Add((WorldGround.Snap(yard - right * 6f + fwd * 3f), 3f));

        Settlement s = go.AddComponent<Settlement>();
        s.title = "Castle courtyard";
        s.center = yard;
        s.ringRadius = 6.5f;
        s.doors = new[]
        {
            CastleWorld(castle, 0f, -3.1f),        // keep
            CastleWorld(castle, -4.9f, 1.8f),      // west house
            CastleWorld(castle, -4.9f, -1.4f),
            CastleWorld(castle, 4.9f, 1.2f),       // east hall
            CastleWorld(castle, 4.9f, -3f),
            CastleWorld(castle, 0f, CastleHalf - 1.8f), // inside the gate
        };
        s.spots = new[]
        {
            WorldGround.Snap(yard + right * 6f + fwd * 4.5f),
            WorldGround.Snap(yard - right * 6f + fwd * 4.5f),
            WorldGround.Snap(yard + right * 3.5f),
            WorldGround.Snap(yard - right * 3.5f),
        };
        settlements.Add(s);
        squares.Add(new Vector4(yard.x, 0f, yard.z, 2f));
        return s;
    }

    // ------------------------------------------------------------------ life

    static Villager.Palette villagerPalette;

    static Material ColorMaterial(string name, Color color)
    {
        EnsureFolder(VillagerFolder);
        string path = VillagerFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.15f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Villager.Palette Palette()
    {
        return new Villager.Palette
        {
            skin = new[]
            {
                ColorMaterial("skin_1", new Color(0.96f, 0.80f, 0.66f)), ColorMaterial("skin_2", new Color(0.87f, 0.67f, 0.50f)),
                ColorMaterial("skin_3", new Color(0.66f, 0.46f, 0.32f)), ColorMaterial("skin_4", new Color(0.45f, 0.30f, 0.20f)),
            },
            cloth = new[]
            {
                ColorMaterial("cloth_guard_red", new Color(0.72f, 0.16f, 0.14f)),
                ColorMaterial("cloth_blue", new Color(0.22f, 0.42f, 0.74f)), ColorMaterial("cloth_green", new Color(0.30f, 0.56f, 0.28f)),
                ColorMaterial("cloth_yellow", new Color(0.90f, 0.72f, 0.24f)), ColorMaterial("cloth_purple", new Color(0.50f, 0.32f, 0.62f)),
                ColorMaterial("cloth_teal", new Color(0.20f, 0.60f, 0.58f)), ColorMaterial("cloth_orange", new Color(0.88f, 0.48f, 0.20f)),
                ColorMaterial("cloth_cream", new Color(0.90f, 0.85f, 0.72f)), ColorMaterial("cloth_rose", new Color(0.82f, 0.42f, 0.52f)),
            },
            trousers = new[]
            {
                ColorMaterial("pants_brown", new Color(0.40f, 0.28f, 0.18f)), ColorMaterial("pants_grey", new Color(0.36f, 0.37f, 0.40f)),
                ColorMaterial("pants_navy", new Color(0.18f, 0.22f, 0.36f)), ColorMaterial("pants_olive", new Color(0.36f, 0.38f, 0.22f)),
            },
            hair = new[]
            {
                ColorMaterial("hair_black", new Color(0.10f, 0.08f, 0.07f)), ColorMaterial("hair_brown", new Color(0.36f, 0.22f, 0.12f)),
                ColorMaterial("hair_blond", new Color(0.86f, 0.70f, 0.38f)), ColorMaterial("hair_red", new Color(0.62f, 0.26f, 0.12f)),
                ColorMaterial("hair_grey", new Color(0.70f, 0.70f, 0.68f)),
            },
            dark = ColorMaterial("dark", new Color(0.12f, 0.10f, 0.09f)),
            straw = ColorMaterial("straw", new Color(0.88f, 0.76f, 0.44f)),
            metal = ColorMaterial("metal", new Color(0.62f, 0.64f, 0.68f)),
            wood = ColorMaterial("wood", new Color(0.46f, 0.30f, 0.16f)),
        };
    }

    static void BuildLife(Transform parent)
    {
        villagerPalette = Palette();
        int people = 0;
        int creatures = 0;

        foreach (Settlement s in settlements)
        {
            PruneBlockedDoors(s);
            int count = s.title == "Main village" ? 14 : s.title == "Castle town" ? 11 : s.title == "Castle courtyard" ? 6 : 7;
            for (int i = 0; i < count; i++)
            {
                Vector3 start = i % 2 == 0 && s.doors.Length > 0 ? s.doors[i % s.doors.Length]
                    : s.spots.Length > 0 ? s.spots[i % s.spots.Length] : s.RingPoint(i);
                start += new Vector3(Range(-0.6f, 0.6f), 0f, Range(-0.6f, 0.6f));
                Villager v = Villager.Create(parent, WorldGround.Snap(start), Quaternion.Euler(0f, Range(0f, 360f), 0f),
                                             Villager.Role.Wanderer, villagerPalette, rng, s.title + " villager " + (i + 1));
                v.settlement = s;
                people++;
            }
        }

        foreach (var post in guardPosts)
        {
            Villager.Create(parent, WorldGround.Snap(post.pos), Quaternion.LookRotation(post.face), Villager.Role.Guard,
                            villagerPalette, rng, "Gate guard");
            people++;
        }

        foreach (Vector3[] route in patrols)
        {
            Villager v = Villager.Create(parent, route[0], Quaternion.identity, Villager.Role.Patrol, villagerPalette, rng, "Patrol guard");
            v.path = route;
            people++;
        }

        foreach (var spot in fisherSpots)
        {
            Villager v = Villager.Create(parent, spot.pos, Quaternion.LookRotation(spot.face), Villager.Role.Fisher,
                                         villagerPalette, rng, "Fisher");
            v.snapToGround = false;
            people++;
        }

        // Travellers walking the roads, two per road, starting at different points.
        for (int r = 0; r < roads.Count; r++)
        {
            Vector3[] road = roads[r];
            if (road.Length < 7)
                continue;
            Vector3[] walk = new Vector3[road.Length - 4];
            System.Array.Copy(road, 2, walk, 0, walk.Length);
            for (int k = 0; k < 2; k++)
            {
                int start = (walk.Length - 1) * (k + 1) / 3;
                Villager v = Villager.Create(parent, walk[start], Quaternion.identity, Villager.Role.Traveler, villagerPalette, rng, "Traveller");
                v.path = walk;
                people++;
            }
        }

        creatures += SpawnCreatures(parent, ChickenPrefab, chickenHomes, 0.45f, 0.45f, 0.5f, "Chicken");
        creatures += SpawnCreatures(parent, HorsePrefab, horseHomes, 1.65f, 0.7f, 0.85f, "Horse");
        report.Add("Life: " + people + " people (villagers, guards, a patrol, fishers, travellers) and " + creatures + " animals.");
    }

    static int SpawnCreatures(Transform parent, string prefabPath, List<(Vector3 home, float radius)> homes, float height,
                              float speed, float restChance, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null || homes.Count == 0)
            return 0;

        int n = 0;
        foreach (var home in homes)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(WorldGround.Snap(home.home), Quaternion.Euler(0f, Range(0f, 360f), 0f));
            if (TryRenderBounds(go, out Bounds b) && b.size.y > 0.01f)
                go.transform.localScale *= height / b.size.y;
            AmbientCreature.DisableImportedControls(go);
            AmbientCreature creature = go.AddComponent<AmbientCreature>();
            creature.home = home.home;
            creature.radius = home.radius;
            creature.speed = speed;
            creature.restChance = restChance;
            n++;
        }

        return n;
    }

    // ------------------------------------------------------------------ road lookup

    // Buckets road segments into grid cells (padded) so distance checks stay fast.
    class RoadGrid
    {
        readonly float cell;
        readonly float pad;
        readonly List<(Vector2 a, Vector2 b)> segments = new List<(Vector2, Vector2)>();
        readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();

        public RoadGrid(float cell, float pad)
        {
            this.cell = cell;
            this.pad = pad;
        }

        static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        public void Add(Vector3 a, Vector3 b)
        {
            Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z);
            int index = segments.Count;
            segments.Add((a2, b2));
            int x0 = Mathf.FloorToInt((Mathf.Min(a2.x, b2.x) - pad) / cell);
            int x1 = Mathf.FloorToInt((Mathf.Max(a2.x, b2.x) + pad) / cell);
            int z0 = Mathf.FloorToInt((Mathf.Min(a2.y, b2.y) - pad) / cell);
            int z1 = Mathf.FloorToInt((Mathf.Max(a2.y, b2.y) + pad) / cell);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    long k = Key(x, z);
                    if (!cells.TryGetValue(k, out List<int> list))
                    {
                        list = new List<int>();
                        cells[k] = list;
                    }

                    list.Add(index);
                }
            }
        }

        // Exact within `pad` metres of a road; beyond that it may just report "far".
        public float Distance(Vector3 p)
        {
            if (!cells.TryGetValue(Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell)), out List<int> list))
                return float.MaxValue;

            Vector2 q = new Vector2(p.x, p.z);
            float best = float.MaxValue;
            foreach (int i in list)
            {
                Vector2 a = segments[i].a, ab = segments[i].b - a;
                float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }

            return best;
        }
    }
}

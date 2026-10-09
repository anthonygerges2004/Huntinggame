using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MakeRiver
{
    const string DoneKey = "HuntingGame.RiverMade";
    const string RouteKey = "HuntingGame.RiverRouted";
    const string MeshPath = MakeFishingLake.Folder + "/RiverMesh.asset";
    const string DialogTitle = "River";
    const string UndoName = "Make River";

    // River cross-section (metres from the centre line).
    const float HalfWidth = 20f;
    const float BedRadius = 15f;
    const float BankRadius = 22f;
    const float SandRadius = 27f;
    const float OuterRadius = 36f;
    const float Depth = 4f;

    // Second lake placement (distance between the two lake edges).
    const float MinLength = 1200f;
    const float MaxLength = 2600f;
    const float IdealLength = 1800f;
    const float SearchStep = 60f;
    const float CampClearance = 150f;
    const int RoutesToTry = 6;

    // Route finding.
    const float RouteCell = 20f;
    const float RouteMargin = 800f;
    const float UphillCost = 25f;
    const float AboveSourceCost = 0.3f;
    const float BelowEndCost = 0.15f;
    const float LakeCost = 0.2f;

    // Path shape.
    const float PointSpacing = 6f;
    const float EndRamp = 120f;

    internal static void AutoRun()
    {
        bool hasRiver = GameObject.Find("River") != null;
        if (!hasRiver && !EditorPrefs.GetBool(DoneKey, false))
            Make();
        else if (hasRiver && !EditorPrefs.GetBool(RouteKey, false))
            MoveFurther();
    }

    [MenuItem("Tools/Hunting Game/Make River And Second Lake")]
    internal static void Make()
    {
        if (!CanRun(out GameObject lakeA))
            return;

        if (GameObject.Find("River") != null || GameObject.Find("Water2") != null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "The river and second lake already exist.", "OK");
            return;
        }

        Build(lakeA);
    }

    [MenuItem("Tools/Hunting Game/Move Second Lake Further")]
    static void MoveFurther()
    {
        if (!CanRun(out GameObject lakeA))
            return;

        if (!RemoveRiverAndSecondLake(lakeA))
        {
            EditorPrefs.SetBool(RouteKey, true);
            EditorUtility.DisplayDialog(DialogTitle,
                "The old river could not be removed because the terrain backup is missing. Nothing was changed.", "OK");
            return;
        }

        Build(lakeA);
    }

    static void Build(GameObject lakeA)
    {
        MakeFishingLake.EnsureLakeComponents(lakeA);
        Vector3 a = lakeA.transform.position;
        float radiusA = lakeA.transform.localScale.x * 0.5f;
        float levelA = a.y + lakeA.transform.localScale.y;
        Vector3 camp = MakeFishingLake.CampPosition();
        LakeSize sizeB = MakeFishingLake.Medium;

        try
        {
            EditorUtility.DisplayProgressBar(DialogTitle, "Looking for a place for the second lake...", 0.1f);
            if (!FindSecondLake(a, radiusA, levelA, camp, sizeB, out Vector3 b, out bool behindMountain))
            {
                EditorPrefs.SetBool(RouteKey, true);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(DialogTitle, "No place for a second lake was found.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar(DialogTitle, "Digging the second lake...", 0.35f);
            float levelB = MakeFishingLake.Shape(b, sizeB, out _, out int clearedB);
            MakeFishingLake.CreateWater("Water2", b, levelB, sizeB);

            EditorUtility.DisplayProgressBar(DialogTitle, "Finding a way around the mountains...", 0.5f);
            bool aIsStart = levelA >= levelB;
            Vector3[] path = aIsStart
                ? Route(a, radiusA, levelA, b, sizeB.Water, levelB, camp, out _)
                : Route(b, sizeB.Water, levelB, a, radiusA, levelA, camp, out _);
            if (path == null)
            {
                EditorPrefs.SetBool(RouteKey, true);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(DialogTitle, "No way for the river was found. Press Ctrl+Z to undo the second lake.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar(DialogTitle, "Digging the river...", 0.65f);
            List<Vector2> lakes = new List<Vector2> { new Vector2(a.x, a.z), new Vector2(b.x, b.z) };
            List<float> lakeBeaches = new List<float> { radiusA + 4f, sizeB.Beach };
            CarveRiver(path, lakes, lakeBeaches);

            EditorUtility.DisplayProgressBar(DialogTitle, "Adding the water...", 0.85f);
            CreateRiverObject(path);
            int clearedRiver = MakeFishingLake.HideFoliage(p => DistanceToRiver(path, p) <= SandRadius + 2f);
            MoveAnimalsOut(path);

            EditorPrefs.SetBool(DoneKey, true);
            EditorPrefs.SetBool(RouteKey, true);
            MakeFishingLake.FinishScene((a + b) * 0.5f, Vector3.Distance(a, b) * 0.7f);

            EditorUtility.ClearProgressBar();
            ShowResult(path, camp, b, sizeB, aIsStart, behindMountain, clearedB + clearedRiver);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static bool CanRun(out GameObject lakeA)
    {
        lakeA = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(DialogTitle, "Press Stop first, then run this again.", "OK");
            return false;
        }

        lakeA = GameObject.Find("Water");
        if (lakeA == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "Make the first lake before the river.", "OK");
            return false;
        }

        return true;
    }

    static void ShowResult(Vector3[] path, Vector3 camp, Vector3 lakeB, LakeSize sizeB, bool aIsStart,
        bool behindMountain, int hiddenFoliage)
    {
        int lakeWidth = Mathf.RoundToInt(sizeB.Water * 2f);
        int campDistance = Mathf.RoundToInt(Vector3.Distance(MakeFishingLake.Flat(camp), MakeFishingLake.Flat(lakeB)));
        int riverLength = Mathf.RoundToInt(PathLength(path));
        int riverWidth = Mathf.RoundToInt(HalfWidth * 2f);

        string message =
            "Done!\n\n" +
            "Second lake: " + lakeWidth + " m wide, " + campDistance + " m " +
            MakeFishingLake.Direction(camp, lakeB) + " of your camp" +
            (behindMountain ? ", behind a mountain" : "") + ".\n" +
            "River: " + riverLength + " m long, " + riverWidth + " m wide. It winds around the high ground.\n\n" +
            "The river begins at the " + (aIsStart ? "big lake" : "second lake") +
            " and ends at the " + (aIsStart ? "second lake" : "big lake") + ".\n\n" +
            (hiddenFoliage > 0 ? hiddenFoliage + " trees/bushes in the water were hidden.\n\n" : "") +
            "You can swim in the river (it pushes you downstream) and fish in both lakes.";

        EditorUtility.DisplayDialog(DialogTitle, message, "OK");
    }

    [MenuItem("Tools/Hunting Game/Make River Wider")]
    static void MakeWider()
    {
        if (!CanRun(out GameObject lakeA))
            return;

        GameObject old = GameObject.Find("River");
        GameObject lakeB = GameObject.Find("Water2");
        if (old == null || lakeB == null)
        {
            EditorUtility.DisplayDialog(DialogTitle, "River or Water2 is missing from the scene.", "OK");
            return;
        }

        Vector3[] path = old.GetComponent<RiverWater>().points;
        List<Vector2> lakes = new List<Vector2>();
        List<float> beaches = new List<float>();
        foreach (GameObject lake in new[] { lakeA, lakeB })
        {
            lakes.Add(new Vector2(lake.transform.position.x, lake.transform.position.z));
            beaches.Add(lake.transform.localScale.x * 0.5f + 4f);
        }

        CarveRiver(path, lakes, beaches);
        Undo.DestroyObjectImmediate(old);
        CreateRiverObject(path);
        MakeFishingLake.HideFoliage(p => DistanceToRiver(path, p) <= SandRadius + 2f);
        MoveAnimalsOut(path);
        MakeFishingLake.FinishScene(path[path.Length / 2], PathLength(path) * 0.7f);
        EditorUtility.DisplayDialog(DialogTitle, "The river is now " + Mathf.RoundToInt(HalfWidth * 2f) + " m wide.", "OK");
    }

    // ---------------------------------------------------------------- Removing the old river

    // Puts the ground under the old river and second lake back as it was, and rebuilds the big lake's bank there.
    static bool RemoveRiverAndSecondLake(GameObject lakeA)
    {
        GameObject river = GameObject.Find("River");
        GameObject lakeB = GameObject.Find("Water2");
        RiverWater riverWater = river != null ? river.GetComponent<RiverWater>() : null;
        Vector3[] oldPath = riverWater != null ? riverWater.points : null;
        if (oldPath != null && oldPath.Length < 2)
            oldPath = null;

        Vector3 a = lakeA.transform.position;
        float levelA = a.y + lakeA.transform.localScale.y;
        LakeSize sizeA = MakeFishingLake.Big;
        Vector3 b = lakeB != null ? lakeB.transform.position : Vector3.zero;
        float reachB = lakeB != null ? lakeB.transform.localScale.x * 0.5f + (MakeFishingLake.Medium.Outer - MakeFishingLake.Medium.Water) + 4f : 0f;
        float reachRiver = OuterRadius + 4f;

        System.Func<Vector3, bool> mask = p =>
            (lakeB != null && Vector3.Distance(MakeFishingLake.Flat(p), MakeFishingLake.Flat(b)) <= reachB) ||
            (oldPath != null && DistanceToRiver(oldPath, p) <= reachRiver);

        Rect area = new Rect(0f, 0f, 0f, 0f);
        bool hasArea = false;
        if (oldPath != null)
        {
            area = PathArea(oldPath, reachRiver);
            hasArea = true;
        }
        if (lakeB != null)
        {
            Rect lakeArea = new Rect(b.x - reachB, b.z - reachB, reachB * 2f, reachB * 2f);
            area = hasArea ? Rect.MinMaxRect(Mathf.Min(area.xMin, lakeArea.xMin), Mathf.Min(area.yMin, lakeArea.yMin),
                                             Mathf.Max(area.xMax, lakeArea.xMax), Mathf.Max(area.yMax, lakeArea.yMax))
                           : lakeArea;
            hasArea = true;
        }

        if (hasArea)
        {
            foreach (Terrain terrain in MakeFishingLake.ActiveTerrains())
            {
                if (!MakeFishingLake.Touches(terrain, area))
                    continue;

                bool ok = MakeFishingLake.RestoreMasked(terrain, area, mask,
                    (p, original) => MakeFishingLake.LakeHeight(
                        Vector3.Distance(MakeFishingLake.Flat(p), MakeFishingLake.Flat(a)), original, levelA, sizeA),
                    p => MakeFishingLake.LakeSand(
                        Vector3.Distance(MakeFishingLake.Flat(p), MakeFishingLake.Flat(a)), sizeA));
                if (!ok)
                    return false;
            }
            AssetDatabase.SaveAssets();
        }

        GameObject foliage = GameObject.Find("_HuntFoliage");
        if (foliage != null && hasArea)
        {
            foreach (Transform child in foliage.transform)
            {
                Vector3 p = child.position;
                if (child.gameObject.activeSelf || !mask(p))
                    continue;
                if (Vector3.Distance(MakeFishingLake.Flat(p), MakeFishingLake.Flat(a)) <= sizeA.Outer + 1f)
                    continue;
                Undo.RecordObject(child.gameObject, UndoName);
                child.gameObject.SetActive(true);
            }
        }

        if (river != null)
            Undo.DestroyObjectImmediate(river);
        if (lakeB != null)
            Undo.DestroyObjectImmediate(lakeB);
        return true;
    }

    // ---------------------------------------------------------------- Lake placement

    struct Candidate
    {
        public Vector3 center;
        public float score;
        public bool mountain;
    }

    static bool FindSecondLake(Vector3 a, float radiusA, float levelA, Vector3 camp, LakeSize size,
        out Vector3 best, out bool behindMountain)
    {
        best = Vector3.zero;
        behindMountain = false;

        Vector3 flatA = MakeFishingLake.Flat(a);
        Vector3 flatCamp = MakeFishingLake.Flat(camp);
        float minCenter = radiusA + size.Water + MinLength;
        float maxCenter = radiusA + size.Water + MaxLength;
        float idealCenter = radiusA + size.Water + IdealLength;
        List<Candidate> candidates = new List<Candidate>();

        foreach (Terrain terrain in MakeFishingLake.ActiveTerrains())
        {
            Vector3 o = terrain.transform.position;
            Vector3 s = terrain.terrainData.size;

            for (float x = o.x + SearchStep * 0.5f; x < o.x + s.x; x += SearchStep)
            {
                for (float z = o.z + SearchStep * 0.5f; z < o.z + s.z; z += SearchStep)
                {
                    Vector3 b = new Vector3(x, 0f, z);

                    float centers = Vector3.Distance(flatA, b);
                    if (centers < minCenter || centers > maxCenter)
                        continue;
                    if (!MakeFishingLake.FarFromEdges(terrain, b, size))
                        continue;
                    if (Vector3.Distance(flatCamp, b) < size.Outer + CampClearance)
                        continue;

                    float levelB = MakeFishingLake.WaterLevel(b, size);
                    bool mountain = HighestBetween(flatA, b) - Mathf.Max(levelA, levelB) > 25f;
                    float score = MakeFishingLake.HeightRange(b, size)
                                  + Mathf.Abs(centers - idealCenter) / 400f
                                  + (levelB > levelA - 3f ? 15f : 0f)
                                  - (mountain ? 4f : 0f);

                    candidates.Add(new Candidate { center = b, score = score, mountain = mountain });
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        candidates.Sort((x, y) => x.score.CompareTo(y.score));
        float bestTotal = float.MaxValue;
        bool found = false;

        for (int i = 0; i < Mathf.Min(RoutesToTry, candidates.Count); i++)
        {
            Candidate c = candidates[i];
            float levelB = MakeFishingLake.WaterLevel(c.center, size);
            bool aIsStart = levelA >= levelB;
            Vector3[] path = aIsStart
                ? Route(a, radiusA, levelA, c.center, size.Water, levelB, camp, out float penalty)
                : Route(c.center, size.Water, levelB, a, radiusA, levelA, camp, out penalty);
            if (path == null)
                continue;

            float total = c.score + penalty / 50f;
            if (total < bestTotal)
            {
                bestTotal = total;
                best = c.center;
                behindMountain = c.mountain;
                found = true;
            }
        }

        if (found)
            best.y = MakeFishingLake.GroundHeight(best);
        return found;
    }

    static float HighestBetween(Vector3 a, Vector3 b)
    {
        float highest = float.MinValue;
        for (int i = 1; i < 40; i++)
            highest = Mathf.Max(highest, MakeFishingLake.GroundHeight(Vector3.Lerp(a, b, i / 40f)));
        return highest;
    }

    // ---------------------------------------------------------------- Route

    // Finds a cheap way from the source lake to the end lake: climbing and high ground cost extra,
    // so the river goes around mountains. Returns points from the source lake's edge to the end lake's edge,
    // with y = water surface, which only ever goes down.
    static Vector3[] Route(Vector3 source, float sourceRadius, float sourceLevel,
        Vector3 end, float endRadius, float endLevel, Vector3 camp, out float penalty)
    {
        penalty = 0f;
        Rect world = WorldRect();
        Rect box = Rect.MinMaxRect(
            Mathf.Max(world.xMin + 40f, Mathf.Min(source.x, end.x) - RouteMargin),
            Mathf.Max(world.yMin + 40f, Mathf.Min(source.z, end.z) - RouteMargin),
            Mathf.Min(world.xMax - 40f, Mathf.Max(source.x, end.x) + RouteMargin),
            Mathf.Min(world.yMax - 40f, Mathf.Max(source.z, end.z) + RouteMargin));

        int w = Mathf.CeilToInt(box.width / RouteCell) + 1;
        int h = Mathf.CeilToInt(box.height / RouteCell) + 1;
        int count = w * h;
        float[] ground = new float[count];
        byte[] kind = new byte[count];
        Vector3 flatCamp = MakeFishingLake.Flat(camp);
        Vector3 flatSource = MakeFishingLake.Flat(source);
        Vector3 flatEnd = MakeFishingLake.Flat(end);

        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                int n = j * w + i;
                Vector3 p = new Vector3(box.xMin + i * RouteCell, 0f, box.yMin + j * RouteCell);
                ground[n] = MakeFishingLake.GroundHeight(p);
                if (Vector3.Distance(p, flatSource) < sourceRadius - 5f || Vector3.Distance(p, flatEnd) < endRadius - 5f)
                    kind[n] = 2;
                else if (Vector3.Distance(p, flatCamp) < OuterRadius + 120f)
                    kind[n] = 1;
            }
        }

        int start = CellIndex(box, w, h, source);
        int goal = CellIndex(box, w, h, end);
        float[] cost = new float[count];
        int[] parent = new int[count];
        for (int n = 0; n < count; n++)
        {
            cost[n] = float.MaxValue;
            parent[n] = -1;
        }

        MinHeap open = new MinHeap();
        cost[start] = 0f;
        open.Push(0f, start);
        int goalX = goal % w, goalY = goal / w;

        while (open.Count > 0)
        {
            open.Pop(out float f, out int current);
            if (current == goal)
                break;

            int cx = current % w, cy = current / w;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int next = ny * w + nx;
                    if (kind[next] == 1)
                        continue;

                    float length = (dx != 0 && dy != 0 ? 1.4142f : 1f) * RouteCell;
                    float step;
                    if (kind[next] == 2)
                    {
                        step = length * LakeCost;
                    }
                    else
                    {
                        float rise = ground[next] - ground[current];
                        step = length
                               + UphillCost * Mathf.Max(0f, rise)
                               + length * AboveSourceCost * Mathf.Max(0f, ground[next] - sourceLevel)
                               + length * BelowEndCost * Mathf.Max(0f, endLevel - 1f - ground[next]);
                    }

                    float total = cost[current] + step;
                    if (total >= cost[next])
                        continue;

                    cost[next] = total;
                    parent[next] = current;
                    float guess = Mathf.Sqrt((nx - goalX) * (nx - goalX) + (ny - goalY) * (ny - goalY)) * RouteCell * 0.5f;
                    open.Push(total + guess, next);
                }
            }
        }

        if (parent[goal] < 0)
            return null;

        List<Vector3> cells = new List<Vector3>();
        for (int n = goal; n >= 0; n = parent[n])
            cells.Add(new Vector3(box.xMin + (n % w) * RouteCell, 0f, box.yMin + (n / w) * RouteCell));
        cells.Reverse();

        List<Vector3> smooth = Smooth(cells, 3, 3);
        List<Vector3> trimmed = Trim(smooth, flatSource, sourceRadius - 0.4f, flatEnd, endRadius - 0.4f);
        if (trimmed == null || trimmed.Count < 2)
            return null;

        Vector3[] path = Resample(trimmed, PointSpacing);
        SetSurface(path, sourceLevel, endLevel);
        penalty = cost[goal] - PathLength(path);
        return path;
    }

    static Rect WorldRect()
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Terrain t in MakeFishingLake.ActiveTerrains())
        {
            Vector3 o = t.transform.position;
            Vector3 s = t.terrainData.size;
            minX = Mathf.Min(minX, o.x);
            maxX = Mathf.Max(maxX, o.x + s.x);
            minZ = Mathf.Min(minZ, o.z);
            maxZ = Mathf.Max(maxZ, o.z + s.z);
        }
        return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
    }

    static int CellIndex(Rect box, int w, int h, Vector3 p)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt((p.x - box.xMin) / RouteCell), 0, w - 1);
        int j = Mathf.Clamp(Mathf.RoundToInt((p.z - box.yMin) / RouteCell), 0, h - 1);
        return j * w + i;
    }

    // Moving average that keeps both ends in place.
    static List<Vector3> Smooth(List<Vector3> points, int radius, int passes)
    {
        List<Vector3> result = new List<Vector3>(points);
        for (int pass = 0; pass < passes; pass++)
        {
            List<Vector3> next = new List<Vector3>(result);
            for (int i = 1; i < result.Count - 1; i++)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                for (int k = Mathf.Max(0, i - radius); k <= Mathf.Min(result.Count - 1, i + radius); k++)
                {
                    sum += result[k];
                    n++;
                }
                next[i] = sum / n;
            }
            result = next;
        }
        return result;
    }

    // Keeps only the part between leaving the source lake and reaching the end lake, ending exactly on their edges.
    static List<Vector3> Trim(List<Vector3> points, Vector3 source, float sourceRadius, Vector3 end, float endRadius)
    {
        int leave = -1;
        for (int i = 0; i < points.Count; i++)
        {
            if (Vector3.Distance(points[i], source) < sourceRadius)
                leave = i;
        }

        int arrive = -1;
        for (int i = Mathf.Max(0, leave + 1); i < points.Count; i++)
        {
            if (Vector3.Distance(points[i], end) < endRadius)
            {
                arrive = i;
                break;
            }
        }

        if (leave < 0 || arrive < 0 || arrive <= leave + 1)
            return null;

        List<Vector3> result = new List<Vector3>();
        result.Add(EdgePoint(points[leave], points[leave + 1], source, sourceRadius));
        for (int i = leave + 1; i < arrive; i++)
            result.Add(points[i]);
        result.Add(EdgePoint(points[arrive], points[arrive - 1], end, endRadius));
        return result;
    }

    // Point on the segment from inside (within the circle) to outside that lies on the circle.
    static Vector3 EdgePoint(Vector3 inside, Vector3 outside, Vector3 center, float radius)
    {
        float lo = 0f, hi = 1f;
        for (int k = 0; k < 20; k++)
        {
            float mid = (lo + hi) * 0.5f;
            if (Vector3.Distance(Vector3.Lerp(inside, outside, mid), center) < radius)
                lo = mid;
            else
                hi = mid;
        }
        return Vector3.Lerp(inside, outside, lo);
    }

    static Vector3[] Resample(List<Vector3> points, float spacing)
    {
        List<Vector3> result = new List<Vector3> { points[0] };
        float carried = 0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float length = Vector3.Distance(a, b);
            float d = spacing - carried;
            while (d < length)
            {
                result.Add(Vector3.Lerp(a, b, d / length));
                d += spacing;
            }
            carried = length - (d - spacing);
        }

        Vector3 last = points[points.Count - 1];
        if (Vector3.Distance(result[result.Count - 1], last) > spacing * 0.3f)
            result.Add(last);
        else
            result[result.Count - 1] = last;
        return result.ToArray();
    }

    // Water follows the ground a little below it, never goes up, and meets both lake levels exactly.
    static void SetSurface(Vector3[] path, float sourceLevel, float endLevel)
    {
        int n = path.Length;
        float[] s = new float[n];
        for (int i = 0; i < n; i++)
            s[i] = Mathf.Clamp(MakeFishingLake.GroundHeight(path[i]) - 0.8f, endLevel, sourceLevel);

        s[0] = sourceLevel;
        for (int i = 1; i < n; i++)
            s[i] = Mathf.Min(s[i], s[i - 1]);

        for (int pass = 0; pass < 2; pass++)
        {
            float[] next = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = Mathf.Max(0, i - 7); k <= Mathf.Min(n - 1, i + 7); k++)
                {
                    sum += s[k];
                    count++;
                }
                next[i] = sum / count;
            }
            s = next;
        }

        float total = PathLength(path);
        float along = 0f;
        s[0] = sourceLevel;
        for (int i = 1; i < n; i++)
        {
            along += Vector3.Distance(MakeFishingLake.Flat(path[i]), MakeFishingLake.Flat(path[i - 1]));
            float ramp = MakeFishingLake.Smooth(1f - (total - along) / EndRamp);
            s[i] = Mathf.Min(Mathf.Lerp(s[i], endLevel, ramp), s[i - 1]);
        }
        s[n - 1] = endLevel;

        for (int i = 0; i < n; i++)
            path[i].y = s[i];
    }

    class MinHeap
    {
        readonly List<float> keys = new List<float>();
        readonly List<int> values = new List<int>();

        public int Count => keys.Count;

        public void Push(float key, int value)
        {
            keys.Add(key);
            values.Add(value);
            int i = keys.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (keys[parent] <= keys[i])
                    break;
                Swap(i, parent);
                i = parent;
            }
        }

        public void Pop(out float key, out int value)
        {
            key = keys[0];
            value = values[0];
            int last = keys.Count - 1;
            keys[0] = keys[last];
            values[0] = values[last];
            keys.RemoveAt(last);
            values.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, smallest = i;
                if (l < keys.Count && keys[l] < keys[smallest])
                    smallest = l;
                if (r < keys.Count && keys[r] < keys[smallest])
                    smallest = r;
                if (smallest == i)
                    break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        void Swap(int a, int b)
        {
            (keys[a], keys[b]) = (keys[b], keys[a]);
            (values[a], values[b]) = (values[b], values[a]);
        }
    }

    // ---------------------------------------------------------------- Path helpers

    static float PathLength(Vector3[] path)
    {
        float length = 0f;
        for (int i = 0; i < path.Length - 1; i++)
            length += Vector3.Distance(MakeFishingLake.Flat(path[i]), MakeFishingLake.Flat(path[i + 1]));
        return length;
    }

    const float BucketSize = 50f;
    static Vector3[] indexedPath;
    static Dictionary<long, List<int>> buckets;

    static long BucketKey(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }

    // Groups segments into 50 m squares, so a lookup only checks the segments near a point.
    static void BuildIndex(Vector3[] path)
    {
        indexedPath = path;
        buckets = new Dictionary<long, List<int>>();
        for (int i = 0; i < path.Length - 1; i++)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(path[i].x, path[i + 1].x) / BucketSize);
            int x1 = Mathf.FloorToInt(Mathf.Max(path[i].x, path[i + 1].x) / BucketSize);
            int z0 = Mathf.FloorToInt(Mathf.Min(path[i].z, path[i + 1].z) / BucketSize);
            int z1 = Mathf.FloorToInt(Mathf.Max(path[i].z, path[i + 1].z) / BucketSize);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    long key = BucketKey(x, z);
                    if (!buckets.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>();
                        buckets[key] = list;
                    }
                    list.Add(i);
                }
            }
        }
    }

    // Distance from p to the river's middle line (only exact up to 50 m away; farther returns a huge number),
    // plus the water surface height and flow direction there.
    static float Closest(Vector3[] path, Vector3 p, out float surface, out Vector3 flow)
    {
        if (indexedPath != path || buckets == null)
            BuildIndex(path);

        float best = float.MaxValue;
        surface = path[0].y;
        flow = Vector3.forward;
        Vector2 p2 = new Vector2(p.x, p.z);
        int bx = Mathf.FloorToInt(p.x / BucketSize);
        int bz = Mathf.FloorToInt(p.z / BucketSize);

        for (int x = bx - 1; x <= bx + 1; x++)
        {
            for (int z = bz - 1; z <= bz + 1; z++)
            {
                if (!buckets.TryGetValue(BucketKey(x, z), out List<int> list))
                    continue;

                foreach (int i in list)
                {
                    Vector2 a = new Vector2(path[i].x, path[i].z);
                    Vector2 b = new Vector2(path[i + 1].x, path[i + 1].z);
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / ab.sqrMagnitude) : 0f;
                    float d = (a + ab * t - p2).sqrMagnitude;

                    if (d < best)
                    {
                        best = d;
                        surface = Mathf.Lerp(path[i].y, path[i + 1].y, t);
                        flow = new Vector3(ab.x, 0f, ab.y);
                    }
                }
            }
        }

        return best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
    }

    static float DistanceToRiver(Vector3[] path, Vector3 p)
    {
        return Closest(path, p, out _, out _);
    }

    static Rect PathArea(Vector3[] path, float margin)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Vector3 p in path)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        return Rect.MinMaxRect(minX - margin, minZ - margin, maxX + margin, maxZ + margin);
    }

    // ---------------------------------------------------------------- Terrain

    static void CarveRiver(Vector3[] path, List<Vector2> lakes, List<float> lakeBeaches)
    {
        Rect area = PathArea(path, OuterRadius + 2f);
        Rect sandArea = PathArea(path, SandRadius + 2f);

        foreach (Terrain terrain in MakeFishingLake.ActiveTerrains())
        {
            if (!MakeFishingLake.Touches(terrain, area))
                continue;

            TerrainData data = terrain.terrainData;
            MakeFishingLake.BackupTerrain(data);
            Undo.RegisterCompleteObjectUndo(data, UndoName);

            Vector3 origin = terrain.transform.position;
            int res = data.heightmapResolution;
            RectInt cells = MakeFishingLake.CellRect(area, origin, data.size, res - 1, res);
            float cellX = data.size.x / (res - 1);
            float cellZ = data.size.z / (res - 1);
            float[,] heights = data.GetHeights(cells.x, cells.y, cells.width, cells.height);

            for (int j = 0; j < cells.height; j++)
            {
                for (int i = 0; i < cells.width; i++)
                {
                    Vector3 world = new Vector3(origin.x + (cells.x + i) * cellX, 0f, origin.z + (cells.y + j) * cellZ);
                    float d = Closest(path, world, out float surface, out _);
                    if (d > OuterRadius)
                        continue;

                    float current = origin.y + heights[j, i] * data.size.y;
                    float shaped = d <= BankRadius
                        ? CrossSection(d, surface)
                        : Mathf.Lerp(surface + 0.4f, current, MakeFishingLake.Smooth((d - BankRadius) / (OuterRadius - BankRadius)));

                    if (InsideLake(world, lakes, lakeBeaches))
                        shaped = Mathf.Min(current, shaped);

                    heights[j, i] = Mathf.Clamp01((shaped - origin.y) / data.size.y);
                }
            }

            data.SetHeights(cells.x, cells.y, heights);

            MakeFishingLake.PaintSand(terrain, sandArea, p =>
                1f - MakeFishingLake.Smooth((DistanceToRiver(path, p) - BankRadius) / (SandRadius - BankRadius)));

            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
    }

    static float CrossSection(float d, float surface)
    {
        if (d <= BedRadius)
        {
            float t = d / BedRadius;
            return surface - 0.5f - (Depth - 0.5f) * (1f - t * t);
        }

        float s = MakeFishingLake.Smooth((d - BedRadius) / (BankRadius - BedRadius));
        return Mathf.Lerp(surface - 0.5f, surface + 0.4f, s);
    }

    static bool InsideLake(Vector3 p, List<Vector2> lakes, List<float> beaches)
    {
        Vector2 p2 = new Vector2(p.x, p.z);
        for (int i = 0; i < lakes.Count; i++)
        {
            if (Vector2.Distance(p2, lakes[i]) <= beaches[i])
                return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- Water object

    static void CreateRiverObject(Vector3[] path)
    {
        Mesh mesh = BuildRiverMesh(path);

        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
            AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);

        GameObject river = new GameObject("River");
        river.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer = river.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = MakeFishingLake.WaterMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        RiverWater water = river.AddComponent<RiverWater>();
        water.points = path;
        water.halfWidth = HalfWidth;

        Undo.RegisterCreatedObjectUndo(river, UndoName);
    }

    static Mesh BuildRiverMesh(Vector3[] path)
    {
        Vector3[] vertices = new Vector3[path.Length * 2];
        Vector2[] uvs = new Vector2[path.Length * 2];
        int[] triangles = new int[(path.Length - 1) * 6];
        float along = 0f;

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 prev = path[Mathf.Max(0, i - 1)];
            Vector3 next = path[Mathf.Min(path.Length - 1, i + 1)];
            Vector3 tangent = MakeFishingLake.Flat(next - prev).normalized;
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x) * HalfWidth;

            if (i > 0)
                along += Vector3.Distance(MakeFishingLake.Flat(path[i]), MakeFishingLake.Flat(path[i - 1]));

            float v = along / (HalfWidth * 2f);
            vertices[i * 2] = path[i] + side;
            vertices[i * 2 + 1] = path[i] - side;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        for (int i = 0; i < path.Length - 1; i++)
        {
            int vi = i * 2;
            int ti = i * 6;
            triangles[ti] = vi;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh { name = "RiverMesh" };
        if (vertices.Length > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- Animals

    static void MoveAnimalsOut(Vector3[] path)
    {
        const float pushRadius = OuterRadius + 4f;

        foreach (Animal animal in Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude))
        {
            Vector3 p = animal.transform.position;
            float d = Closest(path, p, out _, out Vector3 flow);
            if (d > SandRadius + 4f)
                continue;

            Vector3 side = new Vector3(-flow.z, 0f, flow.x).normalized;
            Vector3 optionA = p + side * (pushRadius - d);
            Vector3 optionB = p - side * (pushRadius - d);
            Vector3 target = DistanceToRiver(path, optionA) > DistanceToRiver(path, optionB) ? optionA : optionB;

            Undo.RecordObject(animal.transform, UndoName);
            animal.transform.position = WorldGround.Snap(target);
        }
    }
}

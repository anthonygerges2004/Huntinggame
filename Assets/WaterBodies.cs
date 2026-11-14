using System.Collections.Generic;
using UnityEngine;

public static class WaterBodies
{
    public const float SwimDepth = 1.4f;

    public static readonly List<LakeWater> Lakes = new List<LakeWater>();
    public static readonly List<RiverWater> Rivers = new List<RiverWater>();

    // Outside Play mode the lists are empty, so editor tools search the scene instead.
    static IEnumerable<LakeWater> AllLakes()
    {
        if (Application.isPlaying)
            return Lakes;
        return Object.FindObjectsByType<LakeWater>(FindObjectsInactive.Exclude);
    }

    static IEnumerable<RiverWater> AllRivers()
    {
        if (Application.isPlaying)
            return Rivers;
        return Object.FindObjectsByType<RiverWater>(FindObjectsInactive.Exclude);
    }

    public static float GroundY(Vector3 position)
    {
        Terrain terrain = WorldGround.TerrainAt(position);
        if (terrain == null)
            return float.MinValue;
        return terrain.SampleHeight(position) + terrain.transform.position.y;
    }

    public static bool SurfaceAt(Vector3 position, float margin, out float surfaceY, out Vector3 current)
    {
        current = Vector3.zero;
        foreach (LakeWater lake in AllLakes())
        {
            if (lake != null && lake.Inside(position, margin))
            {
                surfaceY = lake.SurfaceY;
                return true;
            }
        }

        foreach (RiverWater river in AllRivers())
        {
            if (river != null && river.Sample(position, margin, out surfaceY, out current))
                return true;
        }

        surfaceY = 0f;
        return false;
    }

    public static bool TryGetSwim(Vector3 position, out float surfaceY, out Vector3 current)
    {
        if (!SurfaceAt(position, 0.5f, out surfaceY, out current))
            return false;
        if (position.y > surfaceY + 0.2f)
            return false;
        return GroundY(position) < surfaceY - SwimDepth;
    }

    public static bool InsideWater(Vector3 position)
    {
        if (!SurfaceAt(position, 0f, out float surfaceY, out _))
            return false;
        return GroundY(position) <= surfaceY + 0.45f;
    }

    public static bool Underwater(Vector3 position)
    {
        if (!SurfaceAt(position, 0f, out float surfaceY, out _))
            return false;
        return position.y < surfaceY && GroundY(position) < position.y;
    }

    /// <summary>Finds the nearest lake/river surface crossed by a bullet, including water without colliders.</summary>
    public static bool SegmentSurface(Vector3 start, Vector3 end, out Vector3 hit, out float fraction)
    {
        hit = default;
        fraction = float.PositiveInfinity;
        foreach (LakeWater lake in AllLakes())
        {
            if (lake == null || !SegmentPlane(start, end, new Vector3(0f, lake.SurfaceY, 0f), Vector3.up, out float t)) continue;
            Vector3 p = Vector3.Lerp(start, end, t);
            if (t < fraction && lake.Inside(p, 0f)) { hit = p; fraction = t; }
        }
        foreach (RiverWater river in AllRivers())
        {
            if (river == null || river.points == null) continue;
            for (int i = 0; i < river.points.Length - 1; i++)
            {
                Vector3 a = river.points[i], b = river.points[i + 1];
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float lengthSq = flat.sqrMagnitude;
                Vector3 normal = lengthSq > 0.0001f ? Vector3.up - flat * ((b.y - a.y) / lengthSq) : Vector3.up;
                // The sloped strip and horizontal end caps match RiverWater.Sample's clamped projection.
                for (int plane = 0; plane < 3; plane++)
                {
                    Vector3 anchor = plane == 2 ? b : a;
                    if (!SegmentPlane(start, end, anchor, plane == 0 ? normal : Vector3.up, out float t) || t >= fraction) continue;
                    Vector3 p = Vector3.Lerp(start, end, t);
                    if (river.Sample(p, 0f, out float surface, out _) && Mathf.Abs(p.y - surface) < 0.025f)
                    { hit = p; fraction = t; }
                }
            }
        }
        return fraction <= 1f;
    }

    public static bool SegmentPlane(Vector3 start, Vector3 end, Vector3 anchor, Vector3 normal, out float fraction)
    {
        float denominator = Vector3.Dot(normal, end - start);
        fraction = 0f;
        if (Mathf.Abs(denominator) < 0.000001f) return false;
        fraction = Vector3.Dot(normal, anchor - start) / denominator;
        return fraction > 0.00001f && fraction <= 1f;
    }
}

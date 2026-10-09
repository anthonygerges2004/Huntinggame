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
}

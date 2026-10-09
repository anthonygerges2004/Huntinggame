using UnityEngine;

public static class WorldGround
{
    public static Terrain Terrain
    {
        get
        {
            return TerrainAt(Vector3.zero);
        }
    }

    public static Terrain TerrainAt(Vector3 worldPosition)
    {
        Terrain[] terrains = UnityEngine.Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0)
        {
            if (UnityEngine.Terrain.activeTerrain != null)
                return UnityEngine.Terrain.activeTerrain;
            return Object.FindAnyObjectByType<Terrain>();
        }

        Terrain containing = null;
        Terrain nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (worldPosition.x >= origin.x &&
                worldPosition.x <= origin.x + size.x &&
                worldPosition.z >= origin.z &&
                worldPosition.z <= origin.z + size.z)
            {
                containing = terrain;
                break;
            }

            float clampedX = Mathf.Clamp(worldPosition.x, origin.x, origin.x + size.x);
            float clampedZ = Mathf.Clamp(worldPosition.z, origin.z, origin.z + size.z);
            float dx = worldPosition.x - clampedX;
            float dz = worldPosition.z - clampedZ;
            float dist = dx * dx + dz * dz;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = terrain;
            }
        }

        return containing != null ? containing : nearest;
    }

    public static Vector3 Snap(Vector3 worldPosition)
    {
        Terrain terrain = TerrainAt(worldPosition);
        if (terrain == null || terrain.terrainData == null)
            return worldPosition;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float x = Mathf.Clamp(worldPosition.x, origin.x + 8f, origin.x + size.x - 8f);
        float z = Mathf.Clamp(worldPosition.z, origin.z + 8f, origin.z + size.z - 8f);
        Vector3 point = new(x, 0f, z);
        point.y = terrain.SampleHeight(point) + origin.y;
        return point;
    }

    public static bool IsMeadow(Vector3 worldPosition)
    {
        Terrain terrain = TerrainAt(worldPosition);
        if (terrain == null || terrain.terrainData == null)
            return true;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float nx = Mathf.InverseLerp(origin.x, origin.x + size.x, worldPosition.x);
        float nz = Mathf.InverseLerp(origin.z, origin.z + size.z, worldPosition.z);
        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
        if (normal.y < 0.82f)
            return false;

        float height = terrain.SampleHeight(worldPosition);
        if (size.y > 0.01f && height / size.y > 0.55f)
            return false;

        return !InsideWater(worldPosition);
    }

    public static bool InsideWater(Vector3 worldPosition)
    {
        return WaterBodies.InsideWater(worldPosition);
    }
}

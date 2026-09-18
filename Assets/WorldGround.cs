using UnityEngine;

public static class WorldGround
{
    public static Terrain Terrain
    {
        get
        {
            if (Terrain.activeTerrain != null)
                return Terrain.activeTerrain;
            return Object.FindAnyObjectByType<Terrain>();
        }
    }

    public static Vector3 Snap(Vector3 worldPosition)
    {
        Terrain terrain = Terrain;
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
        Terrain terrain = Terrain;
        if (terrain == null || terrain.terrainData == null)
            return true;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float nx = Mathf.InverseLerp(origin.x, origin.x + size.x, worldPosition.x);
        float nz = Mathf.InverseLerp(origin.z, origin.z + size.z, worldPosition.z);
        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
        if (normal.y < 0.88f)
            return false;

        float height = terrain.SampleHeight(worldPosition);
        if (size.y > 0.01f && height / size.y > 0.42f)
            return false;

        return !InsideWater(worldPosition);
    }

    public static bool InsideWater(Vector3 worldPosition)
    {
        GameObject water = GameObject.Find("Water");
        Terrain terrain = Terrain;
        if (water == null || terrain == null)
            return false;

        float groundY = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        return groundY <= water.transform.position.y + 0.45f;
    }
}

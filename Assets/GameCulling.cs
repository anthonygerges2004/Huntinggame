using UnityEngine;

/// <summary>Shared culling distances for camera and terrain tiles.</summary>
public static class GameCulling
{
    public const float MainCameraFarClip = 2000f;
    public const float MinimapCameraFarClip = 500f;

    public const float TerrainTreeDistance = 2000f;
    public const float TerrainTreeBillboard = 1200f;
    public const float TerrainDetailDistance = 80f;
    public const float TerrainDetailDensity = 0.5f;
    public const float TerrainPixelError = 10f;
    public const float TerrainBasemapDistance = 1000f;

    public static void ApplyToCamera(Camera camera, bool minimap)
    {
        if (camera == null)
            return;

        camera.farClipPlane = minimap ? MinimapCameraFarClip : MainCameraFarClip;
        camera.useOcclusionCulling = true;
    }

    public static void ApplyToTerrain(Terrain terrain)
    {
        if (terrain == null)
            return;

        terrain.treeDistance = TerrainTreeDistance;
        terrain.treeBillboardDistance = TerrainTreeBillboard;
        terrain.detailObjectDistance = TerrainDetailDistance;
        terrain.detailObjectDensity = TerrainDetailDensity;
        terrain.heightmapPixelError = TerrainPixelError;
        terrain.basemapDistance = TerrainBasemapDistance;
        terrain.enableHeightmapLODFrustumCulling = true;
    }

    public static void ApplyToAllTerrains()
    {
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        for (int i = 0; i < terrains.Length; i++)
            ApplyToTerrain(terrains[i]);
    }

    public static void ApplyForActiveScene()
    {
        ApplyToAllTerrains();

        GameObject player = GameObject.Find("Player");
        if (player != null)
            ApplyToCamera(player.GetComponentInChildren<Camera>(), minimap: false);

        GameObject minimap = GameObject.Find("MiniMapCamerra");
        if (minimap != null)
            ApplyToCamera(minimap.GetComponent<Camera>(), minimap: true);
    }
}

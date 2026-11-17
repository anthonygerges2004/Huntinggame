using UnityEngine;

/// <summary>Shared culling distances for camera and terrain tiles.</summary>
public static class GameCulling
{
    public const float MainCameraFarClip = 2000f;
    public const float MinimapCameraFarClip = 500f;

    // Billboards closer than ~500 m read as blurry flat cards, which made the whole
    // forest look low-resolution. Keep real 3D trees out to a sensible range.
    public const float TerrainTreeDistance = 1500f;
    public const float TerrainTreeBillboard = 220f;
    public const float TerrainDetailDistance = 110f;
    public const float TerrainDetailDensity = 0.8f;
    // Lower = the drawn ground follows the true height more closely at a distance, so animals on
    // far hillsides no longer appear to float above a simplified slope.
    public const float TerrainPixelError = 2f;
    public const float TerrainBasemapDistance = 1200f;

    public static void ApplyToCamera(Camera camera, bool minimap)
    {
        if (camera == null)
            return;

        camera.farClipPlane = minimap ? MinimapCameraFarClip : GraphicsQuality.FarClip;
        camera.useOcclusionCulling = true;
    }

    public static void ApplyToTerrain(Terrain terrain)
    {
        if (terrain == null)
            return;

        // Distances follow the Graphics setting in the pause menu.
        terrain.treeDistance = GraphicsQuality.TreeDistance;
        terrain.treeBillboardDistance = GraphicsQuality.TreeBillboard;
        terrain.detailObjectDistance = GraphicsQuality.DetailDistance;
        terrain.detailObjectDensity = GraphicsQuality.DetailDensity;
        terrain.heightmapPixelError = GraphicsQuality.PixelError;
        terrain.basemapDistance = TerrainBasemapDistance;
        // The forests are dense: only the nearest trees get full 3D meshes, the rest are
        // billboards, with a soft cross-fade so the switch is not visible.
        terrain.treeMaximumFullLODCount = GraphicsQuality.TreeMaximumFullLod;
        terrain.treeCrossFadeLength = 30f;
        // Heightmap frustum culling cuts holes in the ground mesh.
        // Trees and grass use the distances above, and the camera frustum
        // culls everything else. Distances stay fixed so nothing pops while walking.
        terrain.enableHeightmapLODFrustumCulling = false;
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

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class GameHUD
{
    Canvas mapCanvas;
    Texture2D mapTexture;
    RectTransform mapMarker;
    TMP_Text mapPosition;
    Transform mapPlayer;
    Bounds mapBounds;
    bool mapOpen;
    const float MapSize = 680f;
    const float MapWidth = 1180f, MapHeight = 600f;

    void BuildMap()
    {
        GameObject go = new GameObject("WorldMap", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        mapCanvas = go.GetComponent<Canvas>();
        mapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mapCanvas.sortingOrder = 90;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.matchWidthOrHeight = 1f;
        Stretch(Img(go.transform, "Backdrop", null, new Color(0.025f, 0.035f, 0.03f, 0.98f)).rectTransform);
        TMP_Text title = Text(go.transform, "HUNTING GROUNDS", 26f, Gold, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(.5f, 1f), new Vector2(0, -26), new Vector2(700, 40), new Vector2(.5f, 1));
        TMP_Text hint = Text(go.transform, "Scroll: zoom | Drag: pan | Click: waypoint | Right-click: clear | F: find me | Home: overview | M / ESC: close", 16f, TextDim, TextAlignmentOptions.Center, false);
        Place(hint.rectTransform, new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(1500, 30), new Vector2(.5f, 0));
        RectTransform frame = Panel(go.transform, "MapFrame", new Vector2(.5f, .5f), Vector2.zero, new Vector2(MapWidth + 8, MapHeight + 8), Gold, 0);
        GameObject image = new GameObject("TerrainMap", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        image.transform.SetParent(frame, false);
        Place((RectTransform)image.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(MapWidth, MapHeight), new Vector2(.5f, .5f));
        mapImage = image.GetComponent<RawImage>();
        mapImage.raycastTarget = false;
        mapViewport = (RectTransform)image.transform;
        image.AddComponent<RectMask2D>();
        TMP_Text marker = Text(image.transform, "▲", 24f, Gold, TextAlignmentOptions.Center, true);
        mapMarker = marker.rectTransform;
        Place(mapMarker, new Vector2(.5f, .5f), Vector2.zero, new Vector2(32, 32), new Vector2(.5f, .5f));
        TMP_Text north = Text(go.transform, "N", 20f, Gold, TextAlignmentOptions.Center, true);
        Place(north.rectTransform, new Vector2(.5f, .5f), new Vector2(0, MapHeight / 2 + 20), new Vector2(40, 28), new Vector2(.5f, .5f));
        mapPosition = Text(go.transform, "", 16f, TextMain, TextAlignmentOptions.Center, false);
        Place(mapPosition.rectTransform, new Vector2(.5f, 0), new Vector2(0, 65), new Vector2(1000, 30), new Vector2(.5f, 0));
        BuildMapNavigation(go.transform, image.transform);
        go.SetActive(false);
    }

    void GenerateMap()
    {
        Terrain[] terrains = Terrain.activeTerrains;
        bool found = false;
        foreach (Terrain t in terrains)
        {
            if (t.terrainData == null) continue;
            Bounds b = new Bounds(t.transform.position + t.terrainData.size / 2f, t.terrainData.size);
            if (!found) { mapBounds = b; found = true; }
            else mapBounds.Encapsulate(b);
        }
        if (!found) mapBounds = new Bounds(mapPlayer.position, new Vector3(1000, 200, 1000));
        float span = Mathf.Max(1f, mapBounds.size.x, mapBounds.size.z * MapWidth / MapHeight) * 1.04f;
        mapBounds = new Bounds(mapBounds.center, new Vector3(span, Mathf.Max(1, mapBounds.size.y), span * MapHeight / MapWidth));
        const int resolution = 1024;
        Color[] pixels = new Color[resolution * resolution];
        Color low = new Color(.30f, .42f, .24f), high = new Color(.88f, .83f, .64f);
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float wx = mapBounds.min.x + span * x / (resolution - 1f);
            float wz = mapBounds.min.z + mapBounds.size.z * y / (resolution - 1f);
            Color color = new Color(.07f, .10f, .09f);
            foreach (Terrain t in terrains)
            {
                TerrainData data = t.terrainData;
                if (data == null) continue;
                Vector3 p = t.transform.position;
                float u = (wx - p.x) / data.size.x, v = (wz - p.z) / data.size.z;
                if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                float height = data.GetInterpolatedHeight(u, v) + p.y;
                float elevation = Mathf.InverseLerp(mapBounds.min.y, mapBounds.max.y, height);
                Vector3 normal = data.GetInterpolatedNormal(u, v);
                float shade = .65f + .35f * Mathf.Clamp01(Vector3.Dot(normal, new Vector3(-.5f, 1, .5f).normalized));
                color = Color.Lerp(low, high, elevation) * shade;
                if (Mathf.Repeat(height, 25f) < 1.2f) color *= .78f;
                break;
            }
            if (x % 64 == 0 || y % 64 == 0) color = Color.Lerp(color, Gold, .16f);
            pixels[y * resolution + x] = color;
        }
        if (mapTexture != null) Destroy(mapTexture);
        mapTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        mapTexture.name = "WorldMapTerrain";
        mapTexture.wrapMode = TextureWrapMode.Clamp;
        mapTexture.SetPixels(pixels);
        mapTexture.Apply(false, true);
        mapCanvas.GetComponentInChildren<RawImage>(true).texture = mapTexture;
    }

    // Returns true when the map consumed this frame's menu input.
    bool UpdateMap()
    {
        UpdateWaypointHud();
        Keyboard keyboard = Keyboard.current;
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        if (mapOpen)
        {
            if (dead || !GamePause.Active || (keyboard != null && (keyboard.mKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)))
            {
                mapOpen = false;
                mapCanvas.gameObject.SetActive(false);
                GamePause.Set(false);
                if (!dead) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            if (mapOpen) HandleMapNavigation();
            return true;
        }
        if (dead || Inventory.IsOpen || keyboard == null || !keyboard.mKey.wasPressedThisFrame) return false;
        PlayerMove player = FindAnyObjectByType<PlayerMove>();
        if (player == null) return false;
        mapPlayer = player.transform;
        if (mapTexture == null) GenerateMap();
        RefreshMapLocations();
        mapZoom = Mathf.Clamp(mapBounds.size.x / 2500f, 1f, 16f);
        mapCenter = MapUV(mapPlayer.position);
        GamePause.Set(true);
        if (!GamePause.Active) return false;
        mapOpen = true;
        mapCanvas.gameObject.SetActive(true);
        DrawNavigationMap();
        return true;
    }
}

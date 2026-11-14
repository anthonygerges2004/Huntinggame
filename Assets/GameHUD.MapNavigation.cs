using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class GameHUD
{
    RawImage mapImage;
    RectTransform mapViewport, waypointMarker;
    TMP_Text waypointHud;
    Vector2 mapCenter, mapPress, mapLastMouse;
    float mapZoom = 1;
    bool mapDragging;
    Vector2? mapWaypoint;
    readonly List<(Vector2 position, RectTransform rect)> mapLocations = new List<(Vector2, RectTransform)>();
    Vector2 MapUV(Vector3 p) => new Vector2((p.x - mapBounds.min.x) / mapBounds.size.x, (p.z - mapBounds.min.z) / mapBounds.size.z);
    Rect MapView => new Rect(mapCenter - Vector2.one / (2 * mapZoom), Vector2.one / mapZoom);

    void BuildMapNavigation(Transform root, Transform image)
    {
        TMP_Text legend = Text(root, "▲ YOU    ◆ WAYPOINT    ● CAMP / TRADER    ● CASTLE MARKET    ● FISHING    ● HERMIT", 16, TextDim, TextAlignmentOptions.Center, false);
        Place(legend.rectTransform, new Vector2(.5f, 1), new Vector2(0, -75), new Vector2(1400, 30), new Vector2(.5f, 1));
        waypointMarker = Text(image, "◆", 24, WaterColor, TextAlignmentOptions.Center, true).rectTransform;
        Place(waypointMarker, new Vector2(.5f, .5f), Vector2.zero, new Vector2(32, 32), new Vector2(.5f, .5f));
        waypointHud = Text(canvas.transform, "", 17, WaterColor, TextAlignmentOptions.MidlineLeft, true);
        Place(waypointHud.rectTransform, new Vector2(0, 1), new Vector2(24, -95), new Vector2(380, 32), new Vector2(0, 1));
    }

    void UpdateWaypointHud()
    {
        if (mapWaypoint.HasValue && mapPlayer != null)
        {
            Vector2 delta = mapWaypoint.Value - new Vector2(mapPlayer.position.x, mapPlayer.position.z);
            float bearing = Mathf.Repeat(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 360);
            waypointHud.text = $"◆ WAYPOINT  {delta.magnitude:0} m / {bearing:0}°";
        }
        else waypointHud.text = "";
    }

    void HandleMapNavigation()
    {
        Keyboard key = Keyboard.current;
        if (key != null && key.homeKey.wasPressedThisFrame) { mapZoom = 1; mapCenter = Vector2.one * .5f; }
        if (key != null && key.fKey.wasPressedThisFrame) mapCenter = MapUV(mapPlayer.position);
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 screen = mouse.position.ReadValue();
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(mapViewport, screen);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mapViewport, screen, null, out Vector2 local);
            Vector2 uv = new Vector2(local.x / MapWidth + .5f, local.y / MapHeight + .5f);
            if (inside)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0)
                {
                    Vector2 before = MapView.position + uv / mapZoom;
                    mapZoom = Mathf.Clamp(mapZoom * (scroll > 0 ? 1.25f : .8f), 1, 32);
                    mapCenter = before - (uv - Vector2.one * .5f) / mapZoom;
                }
                if (mouse.rightButton.wasPressedThisFrame) mapWaypoint = null;
                if (mouse.leftButton.wasPressedThisFrame) { mapPress = screen; mapLastMouse = local; mapDragging = true; }
            }
            if (mapDragging && mouse.leftButton.isPressed)
            {
                Vector2 delta = local - mapLastMouse;
                mapCenter -= new Vector2(delta.x / MapWidth, delta.y / MapHeight) / mapZoom;
                mapLastMouse = local;
            }
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (mapDragging && inside && Vector2.Distance(screen, mapPress) < 6)
                {
                    Vector2 point = MapView.position + uv / mapZoom;
                    Vector3 world = new Vector3(mapBounds.min.x + point.x * mapBounds.size.x, 0, mapBounds.min.z + point.y * mapBounds.size.z);
                    // Empty space outside the terrain is not a navigable destination.
                    foreach (Terrain terrain in Terrain.activeTerrains)
                    {
                        if (terrain.terrainData == null) continue;
                        Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
                        if (world.x >= origin.x && world.x <= origin.x + size.x && world.z >= origin.z && world.z <= origin.z + size.z)
                        { mapWaypoint = new Vector2(world.x, world.z); break; }
                    }
                }
                mapDragging = false;
            }
        }
        DrawNavigationMap();
    }

    void PlaceMapLocation(RectTransform marker, Vector2 world)
    {
        Vector2 uv = (MapUV(new Vector3(world.x, 0, world.y)) - MapView.position) * mapZoom;
        marker.gameObject.SetActive(uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1);
        marker.anchoredPosition = new Vector2((uv.x - .5f) * MapWidth, (uv.y - .5f) * MapHeight);
    }

    void DrawNavigationMap()
    {
        float margin = .5f / mapZoom;
        mapCenter = new Vector2(Mathf.Clamp(mapCenter.x, margin, 1 - margin), Mathf.Clamp(mapCenter.y, margin, 1 - margin));
        mapImage.uvRect = MapView;
        Vector3 p = mapPlayer.position;
        PlaceMapLocation(mapMarker, new Vector2(p.x, p.z));
        mapMarker.localRotation = Quaternion.Euler(0, 0, -mapPlayer.eulerAngles.y);
        foreach (var location in mapLocations) PlaceMapLocation(location.rect, location.position);
        waypointMarker.gameObject.SetActive(mapWaypoint.HasValue);
        if (mapWaypoint.HasValue) PlaceMapLocation(waypointMarker, mapWaypoint.Value);
        mapMarker.SetAsLastSibling();
        float distance = mapWaypoint.HasValue ? Vector2.Distance(mapWaypoint.Value, new Vector2(p.x, p.z)) : 0;
        mapPosition.text = $"YOU  X {p.x:0} / Z {p.z:0} m    |    View width: {mapBounds.size.x / mapZoom:0} m    |    Zoom {mapZoom:0.0}x" + (mapWaypoint.HasValue ? $"    |    Waypoint: {distance:0} m" : "    |    Click a destination to navigate");
    }

    void AddMapLocation(Vector3 position, string label, Color color)
    {
        TMP_Text text = Text(mapViewport, "● " + label, 16, color, TextAlignmentOptions.MidlineLeft, true);
        Place(text.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(180, 28), new Vector2(0, .5f));
        mapLocations.Add((new Vector2(position.x, position.z), text.rectTransform));
    }

    void RefreshMapLocations()
    {
        mapDragging = false;
        foreach (var location in mapLocations) Destroy(location.rect.gameObject);
        mapLocations.Clear();
        if (FieldChallenges.Instance != null)
            foreach (var post in FieldChallenges.Instance.Posts)
                AddMapLocation(post.position, post.title + (post.complete ? " [DONE]" : " [K]"), post.complete ? GoodColor : Gold);
        if (CastleMarket.TryGetCenter(out Vector3 castle)) AddMapLocation(castle, "Castle market", Gold);
        foreach (Trader trader in FindObjectsByType<Trader>()) AddMapLocation(trader.transform.position, "Camp / trader", new Color(.95f, .65f, .35f));
        foreach (FishingSpot spot in FindObjectsByType<FishingSpot>()) AddMapLocation(spot.transform.position, "Fishing", WaterColor);
        if (MountainHermit.Instance != null) AddMapLocation(MountainHermit.Instance.transform.position, "Hermit", TextMain);
    }
}

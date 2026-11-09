using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared look for runtime-built screens (inventory, hotbar): generated rounded sprites,
/// the HUD palette, and small helpers for images and text. Matches GameHUD's style.
/// </summary>
public static class UIKit
{
    public static readonly Color Panel = new Color(0.045f, 0.05f, 0.06f, 0.92f);
    public static readonly Color Tile = new Color(1f, 1f, 1f, 0.05f);
    public static readonly Color TileHover = new Color(1f, 1f, 1f, 0.11f);
    public static readonly Color Track = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color TextMain = new Color(0.96f, 0.95f, 0.91f);
    public static readonly Color TextDim = new Color(0.72f, 0.72f, 0.68f);
    public static readonly Color TextFaint = new Color(1f, 1f, 1f, 0.35f);
    public static readonly Color Gold = new Color(0.96f, 0.77f, 0.40f);
    public static readonly Color Warn = new Color(1f, 0.42f, 0.36f);
    public static readonly Color Good = new Color(0.52f, 0.86f, 0.46f);

    static Sprite round, outline, disc;

    public static Sprite Round => round != null ? round : (round = MakeRounded(64, 22f, 0f));
    public static Sprite Outline => outline != null ? outline : (outline = MakeRounded(64, 22f, 3f));
    public static Sprite Disc => disc != null ? disc : (disc = MakeDisc(64));

    public static TMP_FontAsset Font
    {
        get
        {
            if (GameHUD.Instance != null && GameHUD.Instance.HudFont != null)
                return GameHUD.Instance.HudFont;
            return TMP_Settings.defaultFontAsset;
        }
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color color, float radius = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && (sprite == round || sprite == outline))
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 22f / Mathf.Max(0.5f, radius);
        }

        return img;
    }

    public static RawImage Raw(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    public static TMP_Text Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align,
                                bool bold, float spacing = 0f)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = align;
        tmp.characterSpacing = spacing;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    public static RectTransform Group(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>Anchor and pivot at the same corner, then offset and size.</summary>
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Place(rect, anchor, pos, size, anchor);
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static Canvas MakeCanvas(string name, int order, bool interactive)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (interactive)
            root.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ---------------------------------------------------------------- sprites

    static Texture2D NewTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }

    // Rounded square for 9-slicing; stroke > 0 makes an outline of that width.
    static Sprite MakeRounded(int size, float radius, float stroke)
    {
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float dx = Mathf.Max(radius - fx, 0f, fx - (size - radius));
                float dy = Mathf.Max(radius - fy, 0f, fy - (size - radius));
                float inside = radius - Mathf.Sqrt(dx * dx + dy * dy);
                // Distance to the square's edge for the straight parts.
                float edge = Mathf.Min(Mathf.Min(fx, size - fx), Mathf.Min(fy, size - fy));
                float d = dx > 0f && dy > 0f ? inside : Mathf.Min(inside, edge);
                float a = Mathf.Clamp01(d + 0.5f);
                if (stroke > 0f)
                    a *= Mathf.Clamp01(stroke - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        float b = radius + 1f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(b, b, b, b));
    }

    static Sprite MakeDisc(int size)
    {
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(c - 1f - d + 0.5f) * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

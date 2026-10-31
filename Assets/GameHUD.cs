using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameHUD : MonoBehaviour
{
    public TMP_Text hudText;

    bool layoutReady;
    TMP_Text healthValue;
    TMP_Text timeValue;
    TMP_Text meatValue;
    TMP_Text cookedValue;
    TMP_Text woodValue;
    TMP_Text pickaxeValue;
    TMP_Text fireValue;

    void Start()
    {
        SetupLayout();
        Canvas canvas = hudText != null ? hudText.canvas : Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
            MakeCrosshair(canvas);
    }

    void Update()
    {
        if (hudText == null)
            return;

        if (!layoutReady)
            SetupLayout();

        if (!layoutReady)
            return;

        int health = PlayerHealth.Instance != null
            ? Mathf.CeilToInt(PlayerHealth.Instance.health)
            : 0;
        string time = DayNightCycle.Instance != null
            ? DayNightCycle.Instance.TimeLabel()
            : "-";
        int meat = Inventory.Instance != null ? Inventory.Instance.meat : 0;
        int cooked = Inventory.Instance != null ? Inventory.Instance.cookedMeat : 0;
        int wood = Inventory.Instance != null ? Inventory.Instance.wood : 0;
        bool pickaxe = Inventory.Instance != null && Inventory.Instance.hasPickaxe;
        bool fire = Campfire.PlayerIsSafe();
        int spoil = Inventory.Instance != null
            ? Mathf.CeilToInt(Inventory.Instance.SpoilSecondsLeft())
            : 0;

        healthValue.text = health.ToString();
        healthValue.color = Color.white;

        timeValue.text = time;
        timeValue.color = Color.white;

        meatValue.text = meat > 0 ? meat + "  (" + spoil + "s)" : meat.ToString();
        cookedValue.text = cooked.ToString();
        woodValue.text = wood.ToString();
        pickaxeValue.text = pickaxe ? "Yes" : "No";
        fireValue.text = fire ? "Safe" : "None";
        fireValue.color = fire
            ? new Color(0.55f, 1f, 0.55f)
            : new Color(1f, 0.72f, 0.35f);
    }

    void SetupLayout()
    {
        if (hudText == null)
            return;

        Canvas canvas = hudText.canvas;
        if (canvas == null)
            return;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform existing = canvas.transform.Find("HudBoard");
        if (existing != null)
            Destroy(existing.gameObject);

        hudText.enabled = false;
        hudText.raycastTarget = false;

        GameObject board = new GameObject("HudBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        board.transform.SetParent(canvas.transform, false);

        RectTransform boardRect = board.GetComponent<RectTransform>();
        boardRect.anchorMin = new Vector2(0f, 1f);
        boardRect.anchorMax = new Vector2(0f, 1f);
        boardRect.pivot = new Vector2(0f, 1f);
        boardRect.anchoredPosition = new Vector2(12f, -12f);
        boardRect.sizeDelta = new Vector2(960f, 62f);

        Image boardBg = board.GetComponent<Image>();
        boardBg.color = new Color(0.03f, 0.04f, 0.05f, 0.52f);
        boardBg.raycastTarget = false;

        TMP_FontAsset font = hudText.font;
        float boxW = 130f;
        float boxH = 46f;
        float gap = 6f;
        float startX = 7f;

        healthValue = MakeBox(board.transform, font, "HP", startX, -8f, boxW, boxH, new Color(0.29f, 0.73f, 0.44f));
        timeValue = MakeBox(board.transform, font, "Time", startX + (boxW + gap) * 1f, -8f, boxW, boxH, new Color(0.37f, 0.52f, 0.86f));
        meatValue = MakeBox(board.transform, font, "Meat", startX + (boxW + gap) * 2f, -8f, boxW, boxH, new Color(0.74f, 0.38f, 0.38f));
        cookedValue = MakeBox(board.transform, font, "Cooked", startX + (boxW + gap) * 3f, -8f, boxW, boxH, new Color(0.83f, 0.59f, 0.33f));
        woodValue = MakeBox(board.transform, font, "Wood", startX + (boxW + gap) * 4f, -8f, boxW, boxH, new Color(0.66f, 0.5f, 0.29f));
        pickaxeValue = MakeBox(board.transform, font, "Pickaxe", startX + (boxW + gap) * 5f, -8f, boxW, boxH, new Color(0.5f, 0.61f, 0.75f));
        fireValue = MakeBox(board.transform, font, "Fire", startX + (boxW + gap) * 6f, -8f, boxW, boxH, new Color(0.95f, 0.51f, 0.26f));

        MakeHint(board.transform, font, 0f, -(boxH + gap) * 3f, (boxW * 3f) + (gap * 2f), 52f);
        MakeCrosshair(canvas);

        layoutReady = healthValue != null;
    }

    TMP_Text MakeBox(Transform parent, TMP_FontAsset font, string label, float x, float y, float width, float height, Color accent)
    {
        GameObject box = new GameObject(label + "Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(parent, false);

        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 1f);
        boxRect.anchorMax = new Vector2(0f, 1f);
        boxRect.pivot = new Vector2(0f, 1f);
        boxRect.anchoredPosition = new Vector2(x, y);
        boxRect.sizeDelta = new Vector2(width, height);

        Image background = box.GetComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.1f, 0.82f);
        background.raycastTarget = false;

        GameObject bar = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(box.transform, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 5f);
        Image barImage = bar.GetComponent<Image>();
        barImage.color = accent;
        barImage.raycastTarget = false;

        MakeLabel(box.transform, font, label, 8f, -8f, width - 16f, 16f, 11f, Color.white);
        return MakeLabel(box.transform, font, "0", 8f, -22f, width - 16f, 22f, 18f, Color.white);
    }

    void MakeHint(Transform parent, TMP_FontAsset font, float x, float y, float width, float height)
    {
        GameObject box = new GameObject("ControlsBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(parent, false);

        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 1f);
        boxRect.anchorMax = new Vector2(0f, 1f);
        boxRect.pivot = new Vector2(0f, 1f);
        boxRect.anchoredPosition = new Vector2(x, y);
        boxRect.sizeDelta = new Vector2(width, height);

        Image background = box.GetComponent<Image>();
        background.color = new Color(0.05f, 0.05f, 0.05f, 0.72f);
        background.raycastTarget = false;

        MakeLabel(
            box.transform,
            font,
            "F light/feed fire    C cook    Q eat    E chop    1 sell    3 pickaxe",
            12f,
            -14f,
            width - 24f,
            28f,
            16f,
            new Color(0.85f, 0.85f, 0.85f));
    }

    TMP_Text MakeLabel(Transform parent, TMP_FontAsset font, string text, float x, float y, float width, float height, float size, Color color)
    {
        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.faceColor = color;
        tmp.enableVertexGradient = false;
        tmp.outlineWidth = 0f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        Material mat = new Material(tmp.fontSharedMaterial);
        mat.SetColor(ShaderUtilities.ID_FaceColor, color);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        tmp.fontSharedMaterial = mat;

        return tmp;
    }

    void MakeBottomHint(Canvas canvas, TMP_FontAsset font)
    {
        hintRoot = new GameObject("ControlsHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        hintRoot.transform.SetParent(canvas.transform, false);

        RectTransform rect = hintRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 14f);
        rect.sizeDelta = new Vector2(920f, 34f);

        Image bg = hintRoot.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.05f, 0.62f);
        bg.raycastTarget = false;

        MakeLabel(
            hintRoot.transform,
            font,
            "F fire/feed  C cook meat  Q eat cooked  E chop  1 sell  3 pickaxe  R fish  G inventory",
            14f,
            -8f,
            892f,
            22f,
            13f,
            Color.white);
    }

    void MakeCrosshair(Canvas canvas)
    {
        Transform existing = canvas.transform.Find("AimPoint");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject root = new GameObject("AimPoint", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(48f, 48f);

        MakeAimBar(root.transform, new Vector2(28f, 4f));
        MakeAimBar(root.transform, new Vector2(4f, 28f));
        MakeAimBar(root.transform, new Vector2(6f, 6f));
    }

    void MakeAimBar(Transform parent, Vector2 size)
    {
        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(parent, false);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        Image image = bar.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }
}

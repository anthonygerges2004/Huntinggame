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
        healthValue.color = health > 50
            ? new Color(0.55f, 1f, 0.55f)
            : health > 25
                ? new Color(1f, 0.89f, 0.48f)
                : new Color(1f, 0.45f, 0.45f);

        timeValue.text = time;
        timeValue.color = time == "Night"
            ? new Color(0.62f, 0.72f, 1f)
            : Color.white;

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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform existing = canvas.transform.Find("HudBoard");
        if (existing != null)
            Destroy(existing.gameObject);

        hudText.enabled = false;
        hudText.raycastTarget = false;

        GameObject board = new GameObject("HudBoard", typeof(RectTransform));
        board.transform.SetParent(canvas.transform, false);

        RectTransform boardRect = board.GetComponent<RectTransform>();
        boardRect.anchorMin = new Vector2(0f, 1f);
        boardRect.anchorMax = new Vector2(0f, 1f);
        boardRect.pivot = new Vector2(0f, 1f);
        boardRect.anchoredPosition = new Vector2(18f, -18f);
        boardRect.sizeDelta = new Vector2(540f, 320f);

        TMP_FontAsset font = hudText.font;
        float boxW = 168f;
        float boxH = 70f;
        float gap = 10f;

        healthValue = MakeBox(board.transform, font, "HP", 0f, 0f, boxW, boxH, new Color(0.18f, 0.42f, 0.22f));
        timeValue = MakeBox(board.transform, font, "Time", boxW + gap, 0f, boxW, boxH, new Color(0.18f, 0.28f, 0.45f));

        meatValue = MakeBox(board.transform, font, "Meat", 0f, -(boxH + gap), boxW, boxH, new Color(0.42f, 0.2f, 0.18f));
        cookedValue = MakeBox(board.transform, font, "Cooked", boxW + gap, -(boxH + gap), boxW, boxH, new Color(0.45f, 0.28f, 0.12f));
        woodValue = MakeBox(board.transform, font, "Wood", (boxW + gap) * 2f, -(boxH + gap), boxW, boxH, new Color(0.36f, 0.28f, 0.12f));

        pickaxeValue = MakeBox(board.transform, font, "Pickaxe", 0f, -(boxH + gap) * 2f, boxW, boxH, new Color(0.22f, 0.26f, 0.32f));
        fireValue = MakeBox(board.transform, font, "Fire", boxW + gap, -(boxH + gap) * 2f, boxW, boxH, new Color(0.42f, 0.24f, 0.1f));

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
        background.color = new Color(0.05f, 0.05f, 0.05f, 0.72f);
        background.raycastTarget = false;

        GameObject bar = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(box.transform, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 6f);
        Image barImage = bar.GetComponent<Image>();
        barImage.color = accent;
        barImage.raycastTarget = false;

        MakeLabel(box.transform, font, label, 10f, -12f, width - 20f, 20f, 15f, new Color(0.78f, 0.78f, 0.78f));
        return MakeLabel(box.transform, font, "0", 10f, -32f, width - 20f, 32f, 26f, Color.white);
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
            "F fire    C cook    Q eat    E chop    1 sell    3 pickaxe",
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
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
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

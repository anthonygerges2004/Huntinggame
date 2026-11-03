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
    TMP_Text challengeCount;
    TMP_Text challengeBird;
    TMP_Text challengeShot;
    TMP_Text challengeDeer;
    TMP_Text challengeCage;
    GameObject hintRoot;
    RectTransform crosshairLeft;
    RectTransform crosshairRight;
    RectTransform crosshairTop;
    RectTransform crosshairBottom;
    RectTransform crosshairCenter;
    float currentAimGap = 9f;

    void Start()
    {
        EnsureHudSource();
        SetupLayout();
    }

    void Update()
    {
        if (!layoutReady)
        {
            EnsureHudSource();
            SetupLayout();
        }

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

        bool bird = HuntChallenges.Instance != null && HuntChallenges.Instance.ShadeBirdDone;
        bool shot = HuntChallenges.Instance != null && HuntChallenges.Instance.LongShotDone;
        bool deer = HuntChallenges.Instance != null && HuntChallenges.Instance.QuietDeerDone;
        bool cage = BirdChallenge.Won;
        int done = (bird ? 1 : 0) + (shot ? 1 : 0) + (deer ? 1 : 0) + (cage ? 1 : 0);

        if (challengeCount != null)
            challengeCount.text = done + " / 4";
        SetChallenge(challengeBird, bird, "Kill a bird under a tree");
        SetChallenge(challengeShot, shot, "Hit an animal from 20 m");
        SetChallenge(challengeDeer, deer, "Kill a deer while crouching");
        SetChallenge(challengeCage, cage, "Win the bird challenge");

        UpdateCrosshair();
    }

    void SetChallenge(TMP_Text label, bool done, string task)
    {
        if (label == null)
            return;

        label.text = (done ? "Done" : "Open") + "    " + task;
        label.color = done
            ? new Color(0.55f, 1f, 0.55f)
            : new Color(0.86f, 0.86f, 0.86f);
    }

    void EnsureHudSource()
    {
        if (hudText != null && hudText.canvas != null && hudText.font != null)
            return;

        if (hudText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].canvas != null)
                {
                    hudText = texts[i];
                    break;
                }
            }
        }

        Canvas canvas = hudText != null ? hudText.canvas : Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject root = new GameObject("GameHUDCanvas");
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
        }

        if (hudText == null)
        {
            GameObject seed = new GameObject("HudSeed", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            seed.transform.SetParent(canvas.transform, false);
            TextMeshProUGUI tmp = seed.GetComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.enabled = false;
            tmp.raycastTarget = false;
            hudText = tmp;
        }
        else if (hudText.font == null)
        {
            hudText.font = TMP_Settings.defaultFontAsset;
        }
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

        Transform oldChallenges = canvas.transform.Find("ChallengePanel");
        if (oldChallenges != null)
            Destroy(oldChallenges.gameObject);

        Transform oldHint = canvas.transform.Find("ControlsHint");
        if (oldHint != null)
            Destroy(oldHint.gameObject);

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

        TMP_FontAsset font = hudText.font != null ? hudText.font : TMP_Settings.defaultFontAsset;
        if (font == null)
            return;
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

        MakeChallengePanel(canvas.transform, font);
        MakeBottomHint(canvas, font);
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

    void MakeChallengePanel(Transform parent, TMP_FontAsset font)
    {
        GameObject panel = new GameObject("ChallengePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-18f, -18f);
        panelRect.sizeDelta = new Vector2(340f, 176f);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.05f, 0.05f, 0.05f, 0.78f);
        background.raycastTarget = false;

        MakeLabel(panel.transform, font, "Challenges", 14f, -10f, 180f, 24f, 18f, Color.white);
        challengeCount = MakeLabel(panel.transform, font, "0 / 4", 190f, -10f, 136f, 24f, 18f, new Color(0.85f, 0.85f, 0.85f));
        challengeCount.alignment = TextAlignmentOptions.Right;

        challengeBird = MakeLabel(panel.transform, font, "Open    Kill a bird under a tree", 14f, -42f, 312f, 26f, 16f, new Color(0.86f, 0.86f, 0.86f));
        challengeShot = MakeLabel(panel.transform, font, "Open    Hit an animal from 20 m", 14f, -72f, 312f, 26f, 16f, new Color(0.86f, 0.86f, 0.86f));
        challengeDeer = MakeLabel(panel.transform, font, "Open    Kill a deer while crouching", 14f, -102f, 312f, 26f, 16f, new Color(0.86f, 0.86f, 0.86f));
        challengeCage = MakeLabel(panel.transform, font, "Open    Win the bird challenge", 14f, -132f, 312f, 26f, 16f, new Color(0.86f, 0.86f, 0.86f));
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
        rect.sizeDelta = new Vector2(1080f, 34f);
        Image bg = hintRoot.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.05f, 0.62f);
        bg.raycastTarget = false;

        MakeLabel(
            hintRoot.transform,
            font,
            "F fire  C cook  Q eat  E chop  2 shotgun  Right click aim  1 sell  3 pickaxe  B build  V view  T challenge  G inventory",
            14f,
            -8f,
            1052f,
            22f,
            13f,
            Color.white);
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
        return tmp;
    }

    void UpdateCrosshair()
    {
        if (crosshairLeft == null || crosshairRight == null || crosshairTop == null || crosshairBottom == null)
            return;

        bool aiming = PlayerShotgun.IsAiming;
        float targetGap = aiming ? 3.5f : 9f;
        currentAimGap = Mathf.MoveTowards(currentAimGap, targetGap, Time.deltaTime * 35f);

        float barLen = aiming ? 10f : 12f;
        float offset = currentAimGap + barLen * 0.5f;

        crosshairLeft.anchoredPosition = new Vector2(-offset, 0f);
        crosshairRight.anchoredPosition = new Vector2(offset, 0f);
        crosshairTop.anchoredPosition = new Vector2(0f, offset);
        crosshairBottom.anchoredPosition = new Vector2(0f, -offset);
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
        rootRect.sizeDelta = new Vector2(64f, 64f);

        // Center aiming pip
        crosshairCenter = MakeReticleTick(root.transform, "CenterDot", new Vector2(2.5f, 2.5f));
        crosshairCenter.anchoredPosition = Vector2.zero;

        // 4 Tactical reticle lines (Left, Right, Top, Bottom)
        crosshairLeft = MakeReticleTick(root.transform, "LeftTick", new Vector2(12f, 2f));
        crosshairRight = MakeReticleTick(root.transform, "RightTick", new Vector2(12f, 2f));
        crosshairTop = MakeReticleTick(root.transform, "TopTick", new Vector2(2f, 12f));
        crosshairBottom = MakeReticleTick(root.transform, "BottomTick", new Vector2(2f, 12f));

        UpdateCrosshair();
    }

    RectTransform MakeReticleTick(Transform parent, string name, Vector2 size)
    {
        GameObject tickGroup = new GameObject(name, typeof(RectTransform));
        tickGroup.transform.SetParent(parent, false);
        RectTransform groupRect = tickGroup.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0.5f);
        groupRect.anchorMax = new Vector2(0.5f, 0.5f);
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.sizeDelta = size;

        // Shadow outline for high contrast against any background
        GameObject shadow = new GameObject("Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        shadow.transform.SetParent(tickGroup.transform, false);
        RectTransform shadowRect = shadow.GetComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.anchoredPosition = Vector2.zero;
        shadowRect.sizeDelta = size + new Vector2(2f, 2f);
        Image shadowImg = shadow.GetComponent<Image>();
        shadowImg.color = new Color(0f, 0f, 0f, 0.72f);
        shadowImg.raycastTarget = false;

        // Razor-sharp foreground bar
        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(tickGroup.transform, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = size;
        Image barImg = bar.GetComponent<Image>();
        barImg.color = new Color(1f, 1f, 1f, 0.95f);
        barImg.raycastTarget = false;

        return groupRect;
    }
}
using System.Collections.Generic;
using HuntingGame.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Survival HUD built at runtime on its own canvas:
//   top centre    compass, time of day, weather, countdown to night
//   top right     challenges card
//   bottom left   health / food / water / warmth / stamina meters, condition tags above
//   bottom right  fire status and resource tiles
//   under that   a small "H Controls" tag; H opens the key list
//   centre        crosshair that tightens while aiming
//   full screen   red vignette on damage and at low health
// Runs after gameplay scripts so the shop panel and world tags they ask for this frame are drawn this frame.
[DefaultExecutionOrder(500)]
public partial class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }
    public TMP_FontAsset HudFont => font;

    [Tooltip("Optional: any TMP text whose font the HUD should use. It is hidden at runtime.")]
    public TMP_Text hudText;

    // ---------------------------------------------------------------- palette

    static readonly Color PanelColor = new Color(0.045f, 0.05f, 0.06f, 0.80f);
    static readonly Color TileColor = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color TextMain = new Color(0.96f, 0.95f, 0.91f);
    static readonly Color TextDim = new Color(0.80f, 0.80f, 0.76f);
    static readonly Color Gold = new Color(0.96f, 0.77f, 0.40f);
    static readonly Color HealthColor = new Color(0.88f, 0.28f, 0.26f);
    static readonly Color FoodColor = new Color(0.92f, 0.64f, 0.26f);
    static readonly Color WarmColor = new Color(1f, 0.50f, 0.22f);
    static readonly Color ColdColor = new Color(0.45f, 0.68f, 1f);
    static readonly Color GoodColor = new Color(0.52f, 0.86f, 0.46f);
    static readonly Color WarnColor = new Color(1f, 0.42f, 0.36f);
    static readonly Color WaterColor = new Color(0.36f, 0.72f, 0.95f);
    static readonly Color StaminaColor = new Color(0.86f, 0.86f, 0.80f);

    const float RefWidth = 1600f;
    const float RefHeight = 900f;
    const float Margin = 20f;

    // ---------------------------------------------------------------- state

    sealed class Meter
    {
        public RectTransform fill;
        public RectTransform trail;
        public Image fillImage;
        public TMP_Text value;
        public Color color;
        public float shown = 1f;
        public float trailValue = 1f;
        public float trailHold;
    }

    sealed class CompassMark
    {
        public float angle;
        public RectTransform rect;
        public Graphic[] graphics;
        public float[] baseAlpha;
    }

    sealed class ChallengeRow
    {
        public Image dot;
        public TMP_Text label;
        public string task;
        public int state = -1;
    }

    TMP_FontAsset font;
    // Not kept across a script reload: after one, the helper objects behind the HUD are gone,
    // so the HUD must know it is no longer built.
    [System.NonSerialized] bool built;
    Canvas canvas;
    CanvasGroup rootGroup;
    Camera cam;

    Meter healthMeter, foodMeter, waterMeter, warmthMeter, staminaMeter;
    readonly List<Image> statusPills = new List<Image>();
    readonly List<TMP_Text> statusTexts = new List<TMP_Text>();
    readonly List<Image> statusDots = new List<Image>();
    readonly List<(string text, Color color)> statusList = new List<(string, Color)>();
    string shownStatus = null;
    float vitalsHeight;
    float lastHealth = -1f;
    float damageFlash;
    Image vignette;

    CanvasGroup compassGroup;
    readonly List<CompassMark> compassMarks = new List<CompassMark>();
    TMP_Text headingText;
    TMP_Text infoText;
    const float CompassWidth = 480f;
    const float PixelsPerDegree = 3.4f;
    const float StripHeight = 46f;
    RectTransform compassStrip;

    TMP_Text meatCount, meatTimer, cookedCount, spoiledCount, woodCount;
    RectTransform meatSpoilFill;
    GameObject meatSpoilTrack;
    Image firePillBg, fireDot;
    TMP_Text fireLabel;
    TMP_Text coinLabel;
    int shownCoins = -1;
    float coinFlash;

    TMP_Text challengeCount;
    RectTransform challengeProgress;
    CanvasGroup challengeGroup;
    readonly ChallengeRow[] challenges = new ChallengeRow[5];

    CanvasGroup controlsGroup;
    CanvasGroup controlsHintGroup;
    bool controlsVisible;
    float challengesHeight;
    int challengesDone;

    RectTransform crosshairRoot;
    RectTransform crossLeft, crossRight, crossTop, crossBottom;
    float crossGap = 11f;

    static Sprite roundSprite, discSprite, ringSprite, vignetteSprite;

    // ---------------------------------------------------------------- lifecycle

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        // A script reload during play keeps the canvases but loses everything that drives them.
        // Swap in a fresh HUD (OnDestroy clears the old canvases) instead of running half-built.
        if (built || canvas == null)
            return;
        GameHUD fresh = gameObject.AddComponent<GameHUD>();
        fresh.hudText = hudText;
        Destroy(this);
    }

    void Start()
    {
        TryBuild();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (GamePause.Active)
            GamePause.Set(false);
        if (canvas != null)
            Destroy(canvas.gameObject);
        if (mapCanvas != null) Destroy(mapCanvas.gameObject);
        if (mapTexture != null) Destroy(mapTexture);
        if (pauseCanvas != null)
            Destroy(pauseCanvas.gameObject);
    }

    void Update()
    {
        if (!built)
        {
            TryBuild();
            if (!built)
                return;
        }

        float dt = Time.unscaledDeltaTime;

        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        bool hidden = dead || Inventory.IsOpen;
        rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, hidden ? 0f : 1f, dt * 6f);

        UpdateFieldChallenges();
        HandleControlsToggle();
        if (!UpdateMap()) UpdatePause(dt);

        UpdateVitals(dt);
        UpdateCompass();
        UpdateResources();
        UpdateChallenges();
        UpdateControls(dt);
        UpdateCrosshair(dt);
        UpdateHunt(dt);
        UpdateCareer(dt);
    }

    void TryBuild()
    {
        font = hudText != null && hudText.font != null ? hudText.font : TMP_Settings.defaultFontAsset;
        if (font == null)
            return;

        if (hudText != null)
        {
            hudText.enabled = false;
            hudText.raycastTarget = false;
        }

        MakeSprites();
        BuildCanvas();
        BuildVignette();
        BuildCompass();
        BuildVitals();
        BuildResources();
        BuildChallenges();
        BuildControls();
        BuildCrosshair();
        BuildHunt();
        BuildPause();
        BuildCareer();
        BuildShop();
        BuildMap();
        BuildFieldChallenges();
        if (GetComponent<HuntExtras>() == null)
            gameObject.AddComponent<HuntExtras>();
        GameAudio.Ensure(gameObject);
        CinematicLook.Ensure(gameObject);

        built = true;
    }

    // ---------------------------------------------------------------- vitals

    void BuildVitals()
    {
        const float width = 320f;
        const float rowH = 36f;
        const float pad = 16f;
        vitalsHeight = pad * 2f + rowH * 5f - 8f;
        RectTransform panel = Panel(canvas.transform, "Vitals", new Vector2(0f, 0f), new Vector2(Margin, Margin),
                                    new Vector2(width, vitalsHeight), PanelColor, 10f);

        healthMeter = MakeMeter(panel, "HEALTH", HealthColor, pad, width - pad * 2f);
        foodMeter = MakeMeter(panel, "FOOD", FoodColor, pad + rowH, width - pad * 2f);
        waterMeter = MakeMeter(panel, "WATER", WaterColor, pad + rowH * 2f, width - pad * 2f);
        warmthMeter = MakeMeter(panel, "WARMTH", WarmColor, pad + rowH * 3f, width - pad * 2f);
        staminaMeter = MakeMeter(panel, "STAMINA", StaminaColor, pad + rowH * 4f, width - pad * 2f);

        // Condition tags (hungry, wet, freezing...) stack above the panel.
        for (int i = 0; i < 6; i++)
        {
            Image pill = Img(canvas.transform, "Status" + i, roundSprite, PanelColor, 13f);
            Place(pill.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(100f, 26f), new Vector2(0f, 0f));
            Image dot = Img(pill.rectTransform, "Dot", discSprite, Color.white);
            Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(7f, 7f), new Vector2(0.5f, 0.5f));
            TMP_Text text = Text(pill.rectTransform, "", 12f, TextMain, TextAlignmentOptions.MidlineLeft, true);
            text.characterSpacing = 4f;
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(200f, 20f), new Vector2(0f, 0.5f));
            pill.gameObject.SetActive(false);
            statusPills.Add(pill);
            statusDots.Add(dot);
            statusTexts.Add(text);
        }
    }

    void UpdateStatus(SurvivalStats stats)
    {
        statusList.Clear();
        if (stats != null)
        {
            if (stats.Poisoned) statusList.Add(("FOOD POISONING", WarnColor));
            if (stats.Starving) statusList.Add(("STARVING", WarnColor));
            else if (stats.Hungry) statusList.Add(("HUNGRY", FoodColor));
            if (stats.Dehydrated) statusList.Add(("DEHYDRATED", WarnColor));
            else if (stats.Thirsty) statusList.Add(("THIRSTY", WaterColor));
            if (stats.Freezing) statusList.Add(("FREEZING", ColdColor));
            else if (stats.Cold) statusList.Add(("COLD", ColdColor));
            if (stats.Wet) statusList.Add(("WET", WaterColor));
            if (stats.Recovering) statusList.Add((stats.ByFire ? "RESTING" : "RECOVERING", GoodColor));
            if (Inventory.TooHeavyToSprint) statusList.Add(("HEAVY LOAD", WarnColor));
        }

        string key = "";
        for (int i = 0; i < statusList.Count; i++)
            key += statusList[i].text + "|";
        if (key == shownStatus)
            return;
        shownStatus = key;

        float x = Margin;
        for (int i = 0; i < statusPills.Count; i++)
        {
            bool on = i < statusList.Count;
            statusPills[i].gameObject.SetActive(on);
            if (!on)
                continue;

            statusTexts[i].text = statusList[i].text;
            statusDots[i].color = statusList[i].color;
            float w = 24f + statusTexts[i].preferredWidth + 12f;
            statusPills[i].rectTransform.sizeDelta = new Vector2(w, 26f);
            statusPills[i].rectTransform.anchoredPosition = new Vector2(x, Margin + vitalsHeight + 8f + CareerStripHeight + 8f);
            x += w + 6f;
        }
    }

    Meter MakeMeter(RectTransform panel, string label, Color color, float top, float width)
    {
        Meter m = new Meter { color = color };

        Image dot = Img(panel, "Dot", discSprite, color);
        Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(16f + 4f, -top - 7f), new Vector2(8f, 8f), new Vector2(0.5f, 0.5f));

        TMP_Text name = Text(panel, label, 13f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        name.characterSpacing = 6f;
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(16f + 14f, -top), new Vector2(160f, 15f), new Vector2(0f, 1f));

        m.value = Text(panel, "100", 17f, TextMain, TextAlignmentOptions.MidlineRight, true);
        Place(m.value.rectTransform, new Vector2(0f, 1f), new Vector2(16f + width, -top + 1f), new Vector2(80f, 16f), new Vector2(1f, 1f));

        RectTransform track = Img(panel, "Track", roundSprite, TrackColor, 2.5f).rectTransform;
        Place(track, new Vector2(0f, 1f), new Vector2(16f, -top - 19f), new Vector2(width, 7f), new Vector2(0f, 1f));

        Image trail = Img(track, "Trail", roundSprite, new Color(1f, 0.92f, 0.85f, 0.55f), 2.5f);
        m.trail = trail.rectTransform;
        Stretch(m.trail);

        m.fillImage = Img(track, "Fill", roundSprite, color, 2.5f);
        m.fill = m.fillImage.rectTransform;
        Stretch(m.fill);
        return m;
    }

    void UpdateVitals(float dt)
    {
        float maxHealth = PlayerHealth.Instance != null ? Mathf.Max(1f, PlayerHealth.Instance.maxHealth) : 100f;
        float health = PlayerHealth.Instance != null ? PlayerHealth.Instance.health : 0f;
        float health01 = Mathf.Clamp01(health / maxHealth);

        // Only real hits flash the screen; slow losses (starving, cold) show on the bar.
        if (lastHealth >= 0f && health < lastHealth - 2.5f)
            damageFlash = Mathf.Min(1f, damageFlash + Mathf.Clamp((lastHealth - health) / 15f, 0.35f, 1f));
        lastHealth = health;

        SetMeter(healthMeter, health01, Mathf.CeilToInt(health).ToString(), dt, HealthColor);

        SurvivalStats stats = SurvivalStats.Current;
        if (stats != null)
        {
            float food = Mathf.Clamp01(stats.Hunger01);
            float water = Mathf.Clamp01(stats.Water01);
            float warmth = Mathf.Clamp01(stats.Warmth01);
            SetMeter(foodMeter, food, Mathf.RoundToInt(food * 100f).ToString(), dt, FoodColor);
            SetMeter(waterMeter, water, Mathf.RoundToInt(water * 100f).ToString(), dt, WaterColor);
            SetMeter(warmthMeter, warmth, Mathf.RoundToInt(warmth * 100f).ToString(), dt,
                     Color.Lerp(ColdColor, WarmColor, Mathf.InverseLerp(0.2f, 0.7f, warmth)));
        }
        else
        {
            SetMeter(foodMeter, 0f, "—", dt, FoodColor);
            SetMeter(waterMeter, 0f, "—", dt, WaterColor);
            SetMeter(warmthMeter, 0f, "—", dt, WarmColor);
        }

        float stamina = PlayerMove.Stamina01;
        SetMeter(staminaMeter, stamina, Mathf.RoundToInt(stamina * 100f).ToString(), dt, StaminaColor);
        UpdateStatus(stats);

        // Red edges: a flash when hit, and a slow pulse when health is low.
        damageFlash = Mathf.MoveTowards(damageFlash, 0f, dt * 1.6f);
        float low = Mathf.InverseLerp(0.35f, 0.05f, health01);
        float pulse = low * (0.28f + 0.14f * Mathf.Sin(Time.unscaledTime * 5f));
        float alpha = Mathf.Clamp01(Mathf.Max(damageFlash * 0.55f, pulse));
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            alpha = 0f;
        vignette.color = new Color(0.55f, 0.02f, 0.02f, alpha);
    }

    void SetMeter(Meter m, float value01, string text, float dt, Color color)
    {
        m.shown = Mathf.Lerp(m.shown, value01, 1f - Mathf.Exp(-dt * 10f));

        // The pale trail shows how much was just lost, then catches up.
        if (value01 >= m.trailValue)
        {
            m.trailValue = value01;
            m.trailHold = 0f;
        }
        else
        {
            m.trailHold += dt;
            if (m.trailHold > 0.45f)
                m.trailValue = Mathf.MoveTowards(m.trailValue, value01, dt * 0.6f);
        }

        m.fill.anchorMax = new Vector2(Mathf.Clamp01(m.shown), 1f);
        m.trail.anchorMax = new Vector2(Mathf.Clamp01(m.trailValue), 1f);

        Color c = color;
        if (value01 < 0.25f)
            c = Color.Lerp(color, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
        m.fillImage.color = c;

        if (m.value.text != text)
            m.value.text = text;
        m.value.color = value01 < 0.25f ? WarnColor : TextMain;
    }

    // ---------------------------------------------------------------- compass

    void BuildCompass()
    {
        GameObject groupGo = new GameObject("Compass", typeof(RectTransform), typeof(CanvasGroup));
        groupGo.transform.SetParent(canvas.transform, false);
        RectTransform group = groupGo.GetComponent<RectTransform>();
        Place(group, new Vector2(0.5f, 1f), new Vector2(0f, -Margin + 6f), new Vector2(CompassWidth, 92f), new Vector2(0.5f, 1f));
        compassGroup = groupGo.GetComponent<CanvasGroup>();

        RectTransform bar = Panel(group, "Strip", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CompassWidth, StripHeight), PanelColor, 8f);
        compassStrip = bar;
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = Vector2.zero;
        bar.gameObject.AddComponent<RectMask2D>();

        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        for (int a = 0; a < 360; a += 15)
        {
            bool cardinal = a % 90 == 0;
            bool inter = !cardinal && a % 45 == 0;

            GameObject go = new GameObject("Mark" + a, typeof(RectTransform));
            go.transform.SetParent(bar, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(40f, 30f), new Vector2(0.5f, 0.5f));

            List<Graphic> graphics = new List<Graphic>();
            if (cardinal || inter)
            {
                TMP_Text t = Text(rect, names[a / 45], cardinal ? 17f : 13f, a == 0 ? Gold : (cardinal ? TextMain : TextDim),
                                  TextAlignmentOptions.Center, true);
                Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(40f, 20f), new Vector2(0.5f, 0.5f));
                graphics.Add(t);
            }
            else
            {
                Image tick = Img(rect, "Tick", null, new Color(1f, 1f, 1f, 0.35f));
                Place(tick.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 9f), new Vector2(0.5f, 0.5f));
                graphics.Add(tick);
            }

            float[] baseAlpha = new float[graphics.Count];
            for (int i = 0; i < graphics.Count; i++)
                baseAlpha[i] = graphics[i].color.a;
            compassMarks.Add(new CompassMark { angle = a, rect = rect, graphics = graphics.ToArray(), baseAlpha = baseAlpha });
        }

        // Centre marker: a small gold notch hanging under the strip.
        Image notch = Img(group, "Notch", roundSprite, Gold, 1.5f);
        Place(notch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -StripHeight + 4f), new Vector2(3f, 10f), new Vector2(0.5f, 1f));

        headingText = Text(group, "000", 12f, Gold, TextAlignmentOptions.Center, true);
        headingText.characterSpacing = 4f;
        Place(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -StripHeight - 8f), new Vector2(80f, 14f), new Vector2(0.5f, 1f));

        infoText = Text(group, "", 15f, TextMain, TextAlignmentOptions.Center, false);
        infoText.richText = true;
        Place(infoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -StripHeight - 24f), new Vector2(CompassWidth + 200f, 20f), new Vector2(0.5f, 1f));
    }

    void UpdateCompass()
    {
        // The bird challenge draws its own status line at the top of the screen.
        compassGroup.alpha = BirdChallenge.Active ? 0f : 1f;

        if (cam == null || !cam.isActiveAndEnabled)
            cam = Camera.main;
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;

        float half = CompassWidth * 0.5f;
        for (int i = 0; i < compassMarks.Count; i++)
        {
            CompassMark m = compassMarks[i];
            float x = Mathf.DeltaAngle(yaw, m.angle) * PixelsPerDegree;
            bool visible = Mathf.Abs(x) < half + 20f;
            if (m.rect.gameObject.activeSelf != visible)
                m.rect.gameObject.SetActive(visible);
            if (!visible)
                continue;

            m.rect.anchoredPosition = new Vector2(x, 6f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.6f, half, Mathf.Abs(x)));
            for (int g = 0; g < m.graphics.Length; g++)
            {
                Color c = m.graphics[g].color;
                c.a = m.baseAlpha[g] * fade;
                m.graphics[g].color = c;
            }
        }

        string heading = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f)).ToString("000");
        if (headingText.text != heading)
            headingText.text = heading;

        infoText.text = InfoLine();
    }

    string InfoLine()
    {
        DayNightCycle day = DayNightCycle.Instance;
        string phase = day != null ? day.TimeLabel().ToUpperInvariant() : "--";
        bool night = day != null && day.IsNight;
        string phaseHex = night ? "#9FB8FF" : "#F5C466";

        string line = (day != null ? "<color=#8C8C86>DAY </color><b>" + day.Day + "</b><color=#8C8C86>   ·   </color>" : "")
                      + "<color=" + phaseHex + "><b>" + phase + "</b></color>";
        if (WorldWeather.Instance != null)
            line += "<color=#8C8C86>   ·   </color>" + WorldWeather.Instance.Label;

        if (day != null)
        {
            float t = day.TimeOfDay01;
            float len = Mathf.Max(1f, day.dayLengthSeconds);
            string next;
            float seconds;
            if (night)
            {
                next = "Dawn in ";
                seconds = (0.7f - t) * len;
            }
            else
            {
                next = "Night in ";
                seconds = (t < 0.5f ? 0.5f - t : 1.5f - t) * len;
            }

            line += "<color=#8C8C86>   ·   " + next + "</color>" + FormatSeconds(seconds);

            SurvivalStats stats = SurvivalStats.Current;
            if (night && !Campfire.PlayerIsSafe() && stats != null && stats.Cold)
                line += "<color=#8C8C86>   ·   </color><color=#FF6A5C><b>FIND A FIRE</b></color>";
        }

        return line;
    }

    static string FormatSeconds(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return s >= 60 ? (s / 60) + ":" + (s % 60).ToString("00") : s + "s";
    }

    // ---------------------------------------------------------------- resources

    void BuildResources()
    {
        const float tileW = 88f;
        const float tileH = 70f;
        const float gap = 6f;
        const float pad = 8f;
        float width = pad * 2f + tileW * 4f + gap * 3f;
        float height = pad * 2f + tileH;

        RectTransform panel = Panel(canvas.transform, "Resources", new Vector2(1f, 0f), new Vector2(-Margin, Margin),
                                    new Vector2(width, height), PanelColor, 10f);

        meatCount = MakeTile(panel, "RAW MEAT", new Color(0.86f, 0.36f, 0.36f), pad, pad, tileW, tileH, out RectTransform meatTile);
        cookedCount = MakeTile(panel, "COOKED", new Color(0.90f, 0.62f, 0.32f), pad + (tileW + gap), pad, tileW, tileH, out _);
        spoiledCount = MakeTile(panel, "SPOILED", new Color(0.56f, 0.62f, 0.30f), pad + (tileW + gap) * 2f, pad, tileW, tileH, out _);
        woodCount = MakeTile(panel, "WOOD", new Color(0.72f, 0.54f, 0.34f), pad + (tileW + gap) * 3f, pad, tileW, tileH, out _);

        // Spoil timer on the raw meat tile.
        meatTimer = Text(meatTile, "", 12f, TextDim, TextAlignmentOptions.BottomRight, true);
        Place(meatTimer.rectTransform, new Vector2(1f, 0f), new Vector2(-8f, 12f), new Vector2(60f, 16f), new Vector2(1f, 0f));

        Image track = Img(meatTile, "SpoilTrack", roundSprite, TrackColor, 4f);
        Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(8f, 6f), new Vector2(tileW - 16f, 3f), new Vector2(0f, 0f));
        meatSpoilTrack = track.gameObject;
        Image fill = Img(track.rectTransform, "Fill", roundSprite, new Color(0.86f, 0.36f, 0.36f), 4f);
        meatSpoilFill = fill.rectTransform;
        Stretch(meatSpoilFill);

        // Fire status pill above the tiles.
        RectTransform pill = Panel(canvas.transform, "FireStatus", new Vector2(1f, 0f), new Vector2(-Margin, Margin + height + 8f),
                                   new Vector2(200f, 28f), PanelColor, 14f);
        firePillBg = pill.GetComponent<Image>();
        fireDot = Img(pill, "Dot", discSprite, WarmColor);
        Place(fireDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(8f, 8f), new Vector2(0.5f, 0.5f));
        fireLabel = Text(pill, "NO FIRE NEARBY", 12.5f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        fireLabel.characterSpacing = 5f;
        Place(fireLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(160f, 20f), new Vector2(0f, 0.5f));

        // Coin purse, beside the fire pill: earned by selling pelts at the tanner.
        RectTransform purse = Panel(canvas.transform, "Coins", new Vector2(1f, 0f), new Vector2(-Margin - 200f - 8f, Margin + height + 8f),
                                    new Vector2(150f, 28f), PanelColor, 14f);
        Image coin = Img(purse, "Coin", discSprite, Gold);
        Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(12f, 12f), new Vector2(0f, 0.5f));
        Image coinRing = Img(coin.rectTransform, "Ring", ringSprite, new Color(0.55f, 0.38f, 0.10f, 0.9f));
        Stretch(coinRing.rectTransform);
        coinRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        coinLabel = Text(purse, "0", 13f, TextMain, TextAlignmentOptions.MidlineLeft, true);
        coinLabel.characterSpacing = 4f;
        coinLabel.richText = true;
        Place(coinLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(110f, 20f), new Vector2(0f, 0.5f));
    }

    TMP_Text MakeTile(RectTransform panel, string label, Color accent, float x, float y, float w, float h, out RectTransform tile)
    {
        tile = Img(panel, label + "Tile", roundSprite, TileColor, 6f).rectTransform;
        Place(tile, new Vector2(0f, 0f), new Vector2(x, y), new Vector2(w, h), new Vector2(0f, 0f));

        Image dot = Img(tile, "Dot", discSprite, accent);
        Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -14f), new Vector2(7f, 7f), new Vector2(0.5f, 0.5f));

        TMP_Text name = Text(tile, label, 11.5f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        name.characterSpacing = 4f;
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -7f), new Vector2(w - 24f, 14f), new Vector2(0f, 1f));

        TMP_Text count = Text(tile, "0", 28f, TextMain, TextAlignmentOptions.BottomLeft, true);
        Place(count.rectTransform, new Vector2(0f, 0f), new Vector2(9f, 8f), new Vector2(w - 18f, 36f), new Vector2(0f, 0f));
        return count;
    }

    void UpdateResources()
    {
        Inventory inv = Inventory.Instance;
        int meat = inv != null ? inv.meat : 0;
        int cooked = inv != null ? inv.cookedMeat : 0;
        int spoiled = inv != null ? inv.spoiledMeat : 0;
        int wood = inv != null ? inv.wood : 0;

        SetCount(meatCount, meat);
        SetCount(cookedCount, cooked);
        SetCount(spoiledCount, spoiled);
        SetCount(woodCount, wood);

        bool spoiling = inv != null && meat > 0;
        if (meatSpoilTrack.activeSelf != spoiling)
            meatSpoilTrack.SetActive(spoiling);
        if (spoiling)
        {
            float left = inv.SpoilSecondsLeft();
            float frac = Mathf.Clamp01(left / Mathf.Max(1f, inv.meatSpoilSeconds));
            meatSpoilFill.anchorMax = new Vector2(frac, 1f);
            meatTimer.text = FormatSeconds(left);
            meatTimer.color = frac < 0.25f ? WarnColor : TextDim;
        }
        else if (meatTimer.text.Length > 0)
        {
            meatTimer.text = "";
        }

        int purse = Inventory.Coins;
        if (purse != shownCoins)
        {
            if (shownCoins >= 0 && purse > shownCoins)
                coinFlash = 1f;
            shownCoins = purse;
            coinLabel.text = purse + "  <color=#8C8C86>COINS</color>";
        }

        coinFlash = Mathf.MoveTowards(coinFlash, 0f, Time.unscaledDeltaTime * 1.2f);
        coinLabel.color = Color.Lerp(TextMain, Gold, coinFlash);

        bool fire = Campfire.PlayerIsSafe();
        string fireText = fire ? "WARM · BY THE FIRE" : "NO FIRE NEARBY";
        if (fireLabel.text != fireText)
            fireLabel.text = fireText;
        fireLabel.color = fire ? TextMain : TextDim;
        fireDot.color = fire
            ? Color.Lerp(WarmColor, Gold, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f))
            : new Color(1f, 1f, 1f, 0.25f);
        firePillBg.color = fire ? new Color(0.22f, 0.11f, 0.04f, 0.85f) : PanelColor;
    }

    static void SetCount(TMP_Text label, int value)
    {
        string s = value.ToString();
        if (label.text != s)
            label.text = s;
        label.color = value > 0 ? TextMain : new Color(TextMain.r, TextMain.g, TextMain.b, 0.35f);
    }

    // ---------------------------------------------------------------- challenges

    void BuildChallenges()
    {
        const float width = 320f;
        const float rowH = 24f;
        const float pad = 16f;
        float height = pad + 18f + 12f + rowH * 5f + pad - 6f + ContractsBlockHeight;
        challengesHeight = height;

        RectTransform panel = Panel(canvas.transform, "Challenges", new Vector2(1f, 1f), new Vector2(-Margin, -Margin),
                                    new Vector2(width, height), PanelColor, 10f);
        challengeGroup = panel.gameObject.AddComponent<CanvasGroup>();

        TMP_Text title = Text(panel, "CHALLENGES", 13f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        title.characterSpacing = 6f;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad), new Vector2(180f, 16f), new Vector2(0f, 1f));

        challengeCount = Text(panel, "0 / 5", 14f, Gold, TextAlignmentOptions.MidlineRight, true);
        Place(challengeCount.rectTransform, new Vector2(1f, 1f), new Vector2(-pad, -pad), new Vector2(80f, 16f), new Vector2(1f, 1f));

        RectTransform track = Img(panel, "Progress", roundSprite, TrackColor, 4f).rectTransform;
        Place(track, new Vector2(0f, 1f), new Vector2(pad, -pad - 22f), new Vector2(width - pad * 2f, 3f), new Vector2(0f, 1f));
        challengeProgress = Img(track, "Fill", roundSprite, Gold, 4f).rectTransform;
        Stretch(challengeProgress);
        challengeProgress.anchorMax = new Vector2(0f, 1f);

        string[] tasks =
        {
            "Kill a bird under a tree",
            "Hit an animal from 20 m",
            "Kill a deer while crouching",
            "Win the bird challenge",
            "Win the sniper challenge",
        };

        for (int i = 0; i < tasks.Length; i++)
        {
            float y = -pad - 36f - rowH * i;
            ChallengeRow row = new ChallengeRow { task = tasks[i] };
            row.dot = Img(panel, "Check", ringSprite, TextDim);
            Place(row.dot.rectTransform, new Vector2(0f, 1f), new Vector2(pad + 6f, y - 8f), new Vector2(12f, 12f), new Vector2(0.5f, 0.5f));
            row.label = Text(panel, tasks[i], 14.5f, TextMain, TextAlignmentOptions.MidlineLeft, false);
            row.label.richText = true;
            Place(row.label.rectTransform, new Vector2(0f, 1f), new Vector2(pad + 20f, y), new Vector2(width - pad * 2f - 20f, 16f), new Vector2(0f, 1f));
            challenges[i] = row;
        }

        BuildContractRows(panel, width, pad, -pad - 36f - rowH * tasks.Length - 10f);
    }

    void UpdateChallenges()
    {
        challengeGroup.alpha = BirdChallenge.Active ? 0.35f : 1f;
        UpdateContractRows();

        HuntChallenges hc = HuntChallenges.Instance;
        bool[] done =
        {
            hc != null && hc.ShadeBirdDone,
            hc != null && hc.LongShotDone,
            hc != null && hc.QuietDeerDone,
            BirdChallenge.Won,
            hc != null && hc.SniperVitalDone,
        };

        int count = 0;
        for (int i = 0; i < done.Length; i++)
        {
            if (done[i])
                count++;

            ChallengeRow row = challenges[i];
            int state = done[i] ? 1 : 0;
            if (row.state == state)
                continue;

            row.state = state;
            row.dot.sprite = done[i] ? discSprite : ringSprite;
            row.dot.color = done[i] ? GoodColor : new Color(1f, 1f, 1f, 0.45f);
            row.label.text = done[i] ? "<s>" + row.task + "</s>" : row.task;
            row.label.color = done[i] ? TextDim : TextMain;
        }

        challengesDone = count;
        string countText = count + " / 5";
        if (challengeCount.text != countText)
            challengeCount.text = countText;
        challengeCount.color = count == 5 ? GoodColor : Gold;
        float target = count / 5f;
        float current = challengeProgress.anchorMax.x;
        challengeProgress.anchorMax = new Vector2(Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 1.5f), 1f);
    }

    // ---------------------------------------------------------------- controls

    void BuildControls()
    {
        string[,] keys =
        {
            { "F", "Light fire" }, { "C", "Cook" }, { "Q", "Eat" }, { "X", "Bait" }, { "E", "Start/stop chop; hold: wood" },
            { "M", "World map" }, { "B", "Build" }, { "G", "Inventory" }, { "1-5", "Hotbar" }, { "RMB", "Aim" }, { "LMB", "Fire" },
            { "Z", "Drink" }, { "R", "Reload" }, { "Shift", "Steady aim" }, { "V", "Camera" },
            { "T", "Bird cage" }, { "Y", "Sniper post" }, { "Trader", "1 2 3" }, { "Esc", "Pause" },
            { "Tab", "Instinct" }, { "P", "Photo" }, { "J", "Journal" },
        };

        const float width = 300f;
        const float pad = 16f;
        const float rowH = 26f;
        float top = -Margin - challengesHeight - 8f;

        // Small tag shown while the list is closed.
        RectTransform chip = Panel(canvas.transform, "ControlsHint", new Vector2(1f, 1f), new Vector2(-Margin, top),
                                   new Vector2(124f, 30f), PanelColor, 15f);
        controlsHintGroup = chip.gameObject.AddComponent<CanvasGroup>();
        KeyCap(chip, "H", new Vector2(8f, -5f));
        TMP_Text chipText = Text(chip, "CONTROLS", 12f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        chipText.characterSpacing = 4f;
        Place(chipText.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(80f, 20f), new Vector2(0f, 0.5f));

        // The full list, two columns, opened with H.
        int rows = (keys.GetLength(0) + 1) / 2;
        float height = pad * 2f + 18f + 10f + rows * rowH - 6f;
        RectTransform card = Panel(canvas.transform, "Controls", new Vector2(1f, 1f), new Vector2(-Margin, top),
                                   new Vector2(width, height), PanelColor, 10f);
        controlsGroup = card.gameObject.AddComponent<CanvasGroup>();
        controlsGroup.alpha = 0f;

        TMP_Text title = Text(card, "CONTROLS", 13f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        title.characterSpacing = 6f;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad), new Vector2(150f, 16f), new Vector2(0f, 1f));
        TMP_Text close = Text(card, "H to close", 12f, TextDim, TextAlignmentOptions.MidlineRight, false);
        Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(-pad, -pad), new Vector2(120f, 16f), new Vector2(1f, 1f));

        float colW = (width - pad * 2f) * 0.5f;
        for (int i = 0; i < keys.GetLength(0); i++)
        {
            int col = i / rows;
            int row = i % rows;
            float x = pad + col * colW;
            float y = -pad - 28f - row * rowH;
            float capW = KeyCap(card, keys[i, 0], new Vector2(x, y));
            float textX = Mathf.Max(46f, capW + 8f);
            TMP_Text action = Text(card, keys[i, 1], 14f, TextMain, TextAlignmentOptions.MidlineLeft, false);
            Place(action.rectTransform, new Vector2(0f, 1f), new Vector2(x + textX, y), new Vector2(colW - textX, 20f), new Vector2(0f, 1f));
        }
    }

    float KeyCap(Transform parent, string key, Vector2 topLeft)
    {
        float w = key.Length > 1 ? Mathf.Max(38f, key.Length * 8f + 8f) : 22f;
        Image cap = Img(parent, "Key_" + key, roundSprite, new Color(0.93f, 0.91f, 0.86f, 0.95f), 4f);
        Place(cap.rectTransform, new Vector2(0f, 1f), topLeft, new Vector2(w, 20f), new Vector2(0f, 1f));
        TMP_Text label = Text(cap.rectTransform, key, 11.5f, new Color(0.08f, 0.08f, 0.09f), TextAlignmentOptions.Center, true);
        Stretch(label.rectTransform);
        return w;
    }

    void HandleControlsToggle()
    {
        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.H);
#endif
        if (pressed && !Inventory.IsOpen)
            controlsVisible = !controlsVisible;
    }

    void UpdateControls(float dt)
    {
        bool busy = BirdChallenge.Active || WoodBuilder.IsBuilding;
        float card = controlsVisible && !busy ? 1f : 0f;
        float chip = !controlsVisible && !busy ? 1f : 0f;
        controlsGroup.alpha = Mathf.MoveTowards(controlsGroup.alpha, card, dt * 6f);
        controlsHintGroup.alpha = Mathf.MoveTowards(controlsHintGroup.alpha, chip, dt * 6f);
    }

    // ---------------------------------------------------------------- crosshair

    void BuildCrosshair()
    {
        GameObject root = new GameObject("Crosshair", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        crosshairRoot = root.GetComponent<RectTransform>();
        Place(crosshairRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f), new Vector2(0.5f, 0.5f));

        RectTransform dot = Tick(crosshairRoot, "Dot", new Vector2(3f, 3f));
        dot.anchoredPosition = Vector2.zero;
        crossLeft = Tick(crosshairRoot, "Left", new Vector2(14f, 2f));
        crossRight = Tick(crosshairRoot, "Right", new Vector2(14f, 2f));
        crossTop = Tick(crosshairRoot, "Top", new Vector2(2f, 14f));
        crossBottom = Tick(crosshairRoot, "Bottom", new Vector2(2f, 14f));
    }

    RectTransform Tick(Transform parent, string name, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));

        Image shadow = Img(rect, "Shadow", null, new Color(0f, 0f, 0f, 0.6f));
        Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(2f, 2f), new Vector2(0.5f, 0.5f));
        Image bar = Img(rect, "Bar", null, new Color(1f, 1f, 1f, 0.92f));
        Place(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
        return rect;
    }

    void UpdateCrosshair(float dt)
    {
        bool show = !Inventory.IsOpen && !WoodBuilder.IsBuilding && !PlayerShotgun.ScopeIsUp;
        if (crosshairRoot.gameObject.activeSelf != show)
            crosshairRoot.gameObject.SetActive(show);

        bool aiming = PlayerShotgun.IsAiming;
        crossGap = Mathf.MoveTowards(crossGap, aiming ? 4f : 11f, dt * 50f);
        float offset = crossGap + 7f;
        crossLeft.anchoredPosition = new Vector2(-offset, 0f);
        crossRight.anchoredPosition = new Vector2(offset, 0f);
        crossTop.anchoredPosition = new Vector2(0f, offset);
        crossBottom.anchoredPosition = new Vector2(0f, -offset);
    }

    // ---------------------------------------------------------------- canvas & vignette

    void BuildCanvas()
    {
        GameObject root = new GameObject("HuntHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    void BuildVignette()
    {
        vignette = Img(canvas.transform, "DamageVignette", vignetteSprite, new Color(0.55f, 0.02f, 0.02f, 0f));
        vignette.type = Image.Type.Simple;
        Stretch(vignette.rectTransform);
    }

    // ---------------------------------------------------------------- ui helpers

    RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, float radius)
    {
        Image img = Img(parent, name, roundSprite, color, radius);
        Place(img.rectTransform, anchor, pos, size, anchor);
        return img.rectTransform;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color, float radius = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        StyleImage(img, sprite, color, radius);
        return img;
    }

    static void StyleImage(Image img, Sprite sprite, Color color, float radius)
    {
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite == roundSprite && sprite != null)
        {
            // The rounded sprite has a 24 px corner; this scales it to the wanted radius in canvas units.
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 24f / Mathf.Max(0.5f, radius);
        }
    }

    TMP_Text Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool bold)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = false;
        return tmp;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------- generated sprites

    static void MakeSprites()
    {
        if (roundSprite == null)
            roundSprite = MakeRounded(64, 24f);
        if (discSprite == null)
            discSprite = MakeCircle(64, 31f, 0f);
        if (ringSprite == null)
            ringSprite = MakeCircle(64, 29f, 7f);
        if (vignetteSprite == null)
            vignetteSprite = MakeVignette(128);
    }

    static Texture2D NewTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }

    // White rounded square for 9-slicing; corners are anti-aliased.
    static Sprite MakeRounded(int size, float radius)
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
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        float b = radius + 1f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(b, b, b, b));
    }

    // Filled disc (thickness 0) or ring.
    static Sprite MakeCircle(int size, float radius, float thickness)
    {
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float outer = Mathf.Clamp01(radius - d + 0.5f);
                float inner = thickness > 0f ? Mathf.Clamp01(d - (radius - thickness) + 0.5f) : 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(outer * inner * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Transparent centre fading to opaque edges.
    static Sprite MakeVignette(int size)
    {
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v) / 1.41421f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}

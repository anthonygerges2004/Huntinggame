using System.Collections.Generic;
using HuntingGame.Animals;
using HuntingGame.Senses;
using HuntingGame.Wind;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hunting layer of the HUD:
//   bottom right   weapon panel: gun picture, rounds, reload, the fuzzy gun tip
//   centre         hit marker, range finder, breath bar while scoped, detection arrows
//   upper centre   kill banner (species, shot placement, distance, meat)
//   compass        markers for camp, trader, hermit, sniper post and bird cage
//   right of it    wind gauge: where your scent is drifting
//   lower centre   message feed (replaces the plain centred notice text)
public partial class GameHUD
{
    // ---------------------------------------------------------------- state

    const int MaxPips = 6;
    const int MaxArrows = 6;
    const int ToastRows = 3;
    const float DetectRange = 170f;
    const float ToastLife = 3.4f;

    CanvasGroup weaponGroup;
    RawImage weaponIcon;
    TMP_Text weaponName, weaponAmmo, weaponState, weaponTip;
    readonly List<Image> ammoPips = new List<Image>();
    GameObject reloadTrack;
    RectTransform reloadFill;
    HuntWeapon shownWeapon = HuntWeapon.None;

    TMP_Text rangeText;
    CanvasGroup breathGroup;
    RectTransform breathFill;
    Image breathFillImage;
    TMP_Text breathLabel;

    RectTransform hitRoot;
    readonly Image[] hitTicks = new Image[4];
    float hitShownAt = -100f;

    CanvasGroup bannerGroup;
    TMP_Text bannerTitle, bannerDetail;
    Image bannerRule;
    float bannerAge = 100f;

    RectTransform detectRoot;
    readonly List<Image> detectArrows = new List<Image>();
    readonly List<Watched> watched = new List<Watched>();
    readonly List<(float level, float bearing)> detectHits = new List<(float, float)>();
    float nextAnimalScan;

    readonly List<Poi> pois = new List<Poi>();

    RectTransform windArrow;
    Image windArrowImage;
    TMP_Text windText;

    readonly ToastRow[] toasts = new ToastRow[ToastRows];
    static Sprite chevronSprite;

    struct Watched
    {
        public Animal animal;
        public AnimalBrain brain;
        public AnimalSenses senses;
    }

    sealed class Poi
    {
        public string label;
        public Color color;
        public System.Func<Vector3?> where;
        public RectTransform rect;
        public Image icon;
        public TMP_Text text;
    }

    sealed class ToastRow
    {
        public Image bg;
        public TMP_Text text;
        public string message;
        public float age = 100f;
    }

    // ---------------------------------------------------------------- build

    void BuildHunt()
    {
        if (chevronSprite == null)
            chevronSprite = MakeChevron(64);

        BuildWeaponPanel();
        BuildCentre();
        BuildBanner();
        BuildDetection();
        BuildPois();
        BuildWind();
        BuildToasts();
    }

    void BuildWeaponPanel()
    {
        // Same width as the resource tiles, stacked above them and the fire pill.
        const float width = 386f;
        const float height = 98f;
        const float bottom = Margin + 86f + 8f + 28f + 8f;

        RectTransform panel = Panel(canvas.transform, "Weapon", new Vector2(1f, 0f), new Vector2(-Margin, bottom),
                                    new Vector2(width, height), PanelColor, 10f);
        weaponGroup = panel.gameObject.AddComponent<CanvasGroup>();
        weaponGroup.alpha = 0f;

        Image iconBg = Img(panel, "IconBg", roundSprite, TileColor, 6f);
        Place(iconBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(78f, 78f), new Vector2(0f, 0.5f));
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        iconGo.transform.SetParent(iconBg.rectTransform, false);
        weaponIcon = iconGo.GetComponent<RawImage>();
        weaponIcon.raycastTarget = false;
        weaponIcon.color = new Color(1f, 1f, 1f, 0f);
        Place(weaponIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f), new Vector2(0.5f, 0.5f));

        const float left = 100f;
        weaponName = Text(panel, "", 13f, Gold, TextAlignmentOptions.MidlineLeft, true);
        weaponName.characterSpacing = 6f;
        Place(weaponName.rectTransform, new Vector2(0f, 1f), new Vector2(left, -12f), new Vector2(190f, 16f), new Vector2(0f, 1f));

        weaponAmmo = Text(panel, "", 28f, TextMain, TextAlignmentOptions.TopRight, true);
        Place(weaponAmmo.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(110f, 32f), new Vector2(1f, 1f));

        for (int i = 0; i < MaxPips; i++)
        {
            Image pip = Img(panel, "Pip" + i, roundSprite, Gold, 2f);
            Place(pip.rectTransform, new Vector2(0f, 1f), new Vector2(left + i * 15f, -36f), new Vector2(10f, 16f), new Vector2(0f, 1f));
            ammoPips.Add(pip);
        }

        weaponState = Text(panel, "", 12f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        weaponState.characterSpacing = 4f;
        Place(weaponState.rectTransform, new Vector2(0f, 1f), new Vector2(left, -58f), new Vector2(200f, 16f), new Vector2(0f, 1f));

        RectTransform track = Img(panel, "ReloadTrack", roundSprite, TrackColor, 2f).rectTransform;
        Place(track, new Vector2(0f, 1f), new Vector2(left + 96f, -63f), new Vector2(width - left - 110f, 5f), new Vector2(0f, 1f));
        reloadTrack = track.gameObject;
        reloadFill = Img(track, "Fill", roundSprite, Gold, 2f).rectTransform;
        Stretch(reloadFill);
        reloadTrack.SetActive(false);

        weaponTip = Text(panel, "", 11.5f, TextDim, TextAlignmentOptions.MidlineLeft, false);
        weaponTip.overflowMode = TextOverflowModes.Ellipsis;
        Place(weaponTip.rectTransform, new Vector2(0f, 0f), new Vector2(left, 10f), new Vector2(width - left - 12f, 16f), new Vector2(0f, 0f));
    }

    void BuildCentre()
    {
        rangeText = Text(canvas.transform, "", 13f, TextMain, TextAlignmentOptions.Center, true);
        rangeText.characterSpacing = 3f;
        Place(rangeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(260f, 18f), new Vector2(0.5f, 0.5f));
        AddShadow(rangeText);

        GameObject breathGo = new GameObject("Breath", typeof(RectTransform), typeof(CanvasGroup));
        breathGo.transform.SetParent(canvas.transform, false);
        RectTransform breath = breathGo.GetComponent<RectTransform>();
        Place(breath, new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(180f, 30f), new Vector2(0.5f, 0.5f));
        breathGroup = breathGo.GetComponent<CanvasGroup>();
        breathGroup.alpha = 0f;

        breathLabel = Text(breath, "HOLD SHIFT · STEADY", 10.5f, TextMain, TextAlignmentOptions.Center, true);
        breathLabel.characterSpacing = 4f;
        Place(breathLabel.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(220f, 14f), new Vector2(0.5f, 1f));
        AddShadow(breathLabel);
        RectTransform track = Img(breath, "Track", roundSprite, new Color(0f, 0f, 0f, 0.45f), 2f).rectTransform;
        Place(track, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(130f, 5f), new Vector2(0.5f, 0f));
        breathFillImage = Img(track, "Fill", roundSprite, Color.white, 2f);
        breathFill = breathFillImage.rectTransform;
        Stretch(breathFill);

        // Hit marker: four short slashes on the diagonals, like a classic shooter.
        GameObject hitGo = new GameObject("HitMarker", typeof(RectTransform));
        hitGo.transform.SetParent(canvas.transform, false);
        hitRoot = hitGo.GetComponent<RectTransform>();
        Place(hitRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), new Vector2(0.5f, 0.5f));
        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + 90f * i;
            Image tick = Img(hitRoot, "Tick" + i, null, Color.white);
            RectTransform rt = tick.rectTransform;
            Place(rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 12f), new Vector2(0.5f, 0.5f));
            float rad = angle * Mathf.Deg2Rad;
            rt.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 13f;
            rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            hitTicks[i] = tick;
        }

        hitRoot.gameObject.SetActive(false);
    }

    void BuildBanner()
    {
        GameObject go = new GameObject("KillBanner", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);
        RectTransform root = go.GetComponent<RectTransform>();
        Place(root, new Vector2(0.5f, 0.5f), new Vector2(0f, 168f), new Vector2(640f, 70f), new Vector2(0.5f, 0.5f));
        bannerGroup = go.GetComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;

        bannerTitle = Text(root, "", 32f, TextMain, TextAlignmentOptions.Center, true);
        bannerTitle.characterSpacing = 10f;
        Place(bannerTitle.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(640f, 38f), new Vector2(0.5f, 1f));
        AddShadow(bannerTitle);

        bannerRule = Img(root, "Rule", roundSprite, Gold, 1f);
        Place(bannerRule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(180f, 2f), new Vector2(0.5f, 1f));

        bannerDetail = Text(root, "", 14f, TextDim, TextAlignmentOptions.Center, true);
        bannerDetail.characterSpacing = 5f;
        Place(bannerDetail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(640f, 18f), new Vector2(0.5f, 1f));
        AddShadow(bannerDetail);
    }

    void BuildDetection()
    {
        GameObject go = new GameObject("Detection", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        detectRoot = go.GetComponent<RectTransform>();
        Place(detectRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));

        for (int i = 0; i < MaxArrows; i++)
        {
            Image arrow = Img(detectRoot, "Arrow" + i, chevronSprite, Color.white);
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 13f), new Vector2(0.5f, 0.5f));
            arrow.gameObject.SetActive(false);
            detectArrows.Add(arrow);
        }
    }

    void BuildPois()
    {
        AddPoi("CAMP", Gold, () => Campfire.Instance != null ? Campfire.Instance.FireWorldPosition() : (Vector3?)null);
        AddPoi("TRADER", new Color(0.55f, 0.85f, 0.55f), () => Trader.Instance != null ? Trader.Instance.transform.position : (Vector3?)null);
        AddPoi("SHOPS", new Color(0.95f, 0.78f, 0.35f), () => CastleMarket.TryGetCenter(out Vector3 shops) ? shops : (Vector3?)null);
        AddPoi("HERMIT", new Color(0.80f, 0.70f, 1f), () => MountainHermit.Instance != null ? MountainHermit.Instance.transform.position : (Vector3?)null);
        AddPoi("SNIPER POST", new Color(1f, 0.55f, 0.45f), () => DeerChallenge.TryGetPost(out Vector3 p) ? p : (Vector3?)null);
        AddPoi("BIRD CAGE", new Color(0.55f, 0.80f, 1f), () => BirdChallenge.TryGetCage(out Vector3 p) ? p : (Vector3?)null);
    }

    void AddPoi(string label, Color color, System.Func<Vector3?> where)
    {
        GameObject go = new GameObject("Poi_" + label, typeof(RectTransform));
        go.transform.SetParent(compassStrip, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, -13f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));

        Image icon = Img(rect, "Diamond", roundSprite, color, 1.5f);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f), new Vector2(0.5f, 0.5f));
        icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        TMP_Text text = Text(rect, label, 9.5f, color, TextAlignmentOptions.MidlineLeft, true);
        text.characterSpacing = 3f;
        Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(140f, 12f), new Vector2(0f, 0.5f));

        go.SetActive(false);
        pois.Add(new Poi { label = label, color = color, where = where, rect = rect, icon = icon, text = text });
    }

    void BuildWind()
    {
        // A small round gauge to the right of the compass strip.
        RectTransform group = compassStrip.parent as RectTransform;
        Image ring = Img(group, "WindGauge", discSprite, PanelColor);
        Place(ring.rectTransform, new Vector2(0.5f, 1f), new Vector2(CompassWidth * 0.5f + 32f, -StripHeight * 0.5f),
              new Vector2(42f, 42f), new Vector2(0.5f, 0.5f));
        Image edge = Img(ring.rectTransform, "Edge", ringSprite, new Color(1f, 1f, 1f, 0.18f));
        Stretch(edge.rectTransform);
        edge.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        windArrowImage = Img(ring.rectTransform, "Arrow", chevronSprite, WaterColor);
        windArrow = windArrowImage.rectTransform;
        Place(windArrow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 22f), new Vector2(0.5f, 0.5f));

        windText = Text(group, "WIND", 9.5f, TextDim, TextAlignmentOptions.Center, true);
        windText.characterSpacing = 3f;
        Place(windText.rectTransform, new Vector2(0.5f, 1f), new Vector2(CompassWidth * 0.5f + 32f, -StripHeight - 2f),
              new Vector2(90f, 12f), new Vector2(0.5f, 1f));
    }

    void BuildToasts()
    {
        for (int i = 0; i < ToastRows; i++)
        {
            ToastRow row = new ToastRow();
            row.bg = Img(canvas.transform, "Toast" + i, roundSprite, PanelColor, 14f);
            Place(row.bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 134f + i * 34f), new Vector2(200f, 28f), new Vector2(0.5f, 0f));
            row.text = Text(row.bg.rectTransform, "", 14.5f, TextMain, TextAlignmentOptions.Center, false);
            Stretch(row.text.rectTransform);
            row.bg.gameObject.SetActive(false);
            toasts[i] = row;
        }
    }

    // ---------------------------------------------------------------- public

    /// <summary>Shows a short message in the feed above the hotbar. Returns false before the HUD is built.</summary>
    public bool Toast(string message)
    {
        if (!built || string.IsNullOrEmpty(message))
            return false;

        // Same message again just stays up longer.
        if (toasts[0].message == message && toasts[0].age < ToastLife)
        {
            toasts[0].age = 0f;
            return true;
        }

        for (int i = ToastRows - 1; i > 0; i--)
        {
            toasts[i].message = toasts[i - 1].message;
            toasts[i].age = toasts[i - 1].age;
        }

        toasts[0].message = message;
        toasts[0].age = 0f;
        for (int i = 0; i < ToastRows; i++)
            ApplyToastText(toasts[i]);
        return true;
    }

    void ApplyToastText(ToastRow row)
    {
        bool on = !string.IsNullOrEmpty(row.message) && row.age < ToastLife;
        row.bg.gameObject.SetActive(on);
        if (!on)
            return;
        row.text.text = row.message;
        row.bg.rectTransform.sizeDelta = new Vector2(Mathf.Min(760f, row.text.preferredWidth + 40f), 28f);
    }

    // ---------------------------------------------------------------- update

    void UpdateHunt(float dt)
    {
        UpdateWeaponPanel(dt);
        UpdateRangeAndBreath(dt);
        UpdateHitMarker();
        UpdateBanner(dt);
        UpdateDetection();
        UpdatePois();
        UpdateWind();
        UpdateToasts(dt);
    }

    void UpdateWeaponPanel(float dt)
    {
        PlayerShotgun gun = PlayerShotgun.Instance;
        HuntWeapon weapon = gun != null ? gun.Equipped : HuntWeapon.None;
        bool show = weapon != HuntWeapon.None && !BirdChallenge.Active;
        weaponGroup.alpha = Mathf.MoveTowards(weaponGroup.alpha, show ? 1f : 0f, dt * 6f);
        if (weapon == HuntWeapon.None || gun == null)
        {
            shownWeapon = HuntWeapon.None;
            return;
        }

        // Checked every frame: the drawn picture shows first, then the photo replaces it once it renders.
        Texture2D icon = WeaponIcons.Get(weapon);
        if (weaponIcon.texture != icon)
        {
            weaponIcon.texture = icon;
            weaponIcon.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (weapon != shownWeapon)
        {
            shownWeapon = weapon;
            weaponName.text = PlayerShotgun.ActiveLabel.ToUpperInvariant();
        }

        gun.TryRounds(weapon, out int rounds, out int mag, out _);
        string ammo = rounds + "<size=60%><color=#8C8C86> / " + mag + "</color></size>";
        weaponAmmo.richText = true;
        if (weaponAmmo.text != ammo)
            weaponAmmo.text = ammo;
        weaponAmmo.color = rounds == 0 ? WarnColor : TextMain;

        for (int i = 0; i < ammoPips.Count; i++)
        {
            bool used = i < mag;
            if (ammoPips[i].gameObject.activeSelf != used)
                ammoPips[i].gameObject.SetActive(used);
            if (used)
                ammoPips[i].color = i < rounds ? Gold : new Color(1f, 1f, 1f, 0.14f);
        }

        bool reloading = PlayerShotgun.IsReloading;
        if (reloadTrack.activeSelf != reloading)
            reloadTrack.SetActive(reloading);

        string state;
        Color stateColor = TextDim;
        if (reloading)
        {
            state = "RELOADING";
            stateColor = TextMain;
            reloadFill.anchorMax = new Vector2(PlayerShotgun.Reload01, 1f);
        }
        else if (rounds == 0)
        {
            state = "EMPTY · PRESS R";
            stateColor = WarnColor;
        }
        else if (rounds < mag)
            state = "R  RELOAD";
        else
            state = PlayerShotgun.ScopeIsUp ? "SHIFT  HOLD BREATH" : "RMB  AIM";

        if (weaponState.text != state)
            weaponState.text = state;
        weaponState.color = stateColor;

        string tip = GunAdvisor.Line ?? "";
        if (weaponTip.text != tip)
            weaponTip.text = tip;
    }

    void UpdateRangeAndBreath(float dt)
    {
        bool aiming = PlayerShotgun.IsAiming && !Inventory.IsOpen;
        string range = "";
        Color rangeColor = TextMain;
        if (aiming && TryAimTarget(out float distance, out Animal animal))
        {
            PlayerShotgun gun = PlayerShotgun.Instance;
            float gunRange = 0f;
            if (gun != null)
                gun.TryRounds(gun.Equipped, out _, out _, out gunRange);

            range = Mathf.RoundToInt(distance) + " m";
            if (animal != null && !animal.Dead)
                range = HuntFeedback.KindTitle(animal) + "   " + range;
            if (gunRange > 1f && distance > gunRange)
            {
                range += "   OUT OF RANGE";
                rangeColor = WarnColor;
            }
            else if (animal != null && !animal.Dead)
                rangeColor = Gold;
        }

        if (rangeText.text != range)
            rangeText.text = range;
        rangeText.color = rangeColor;

        bool scoped = PlayerShotgun.ScopeIsUp && !Inventory.IsOpen;
        breathGroup.alpha = Mathf.MoveTowards(breathGroup.alpha, scoped ? 1f : 0f, dt * 6f);
        if (!scoped && breathGroup.alpha <= 0f)
            return;

        breathFill.anchorMax = new Vector2(Mathf.Clamp01(ScopeSteady.Breath01), 1f);
        if (ScopeSteady.Gasping)
        {
            breathLabel.text = "OUT OF BREATH";
            breathLabel.color = WarnColor;
            breathFillImage.color = WarnColor;
        }
        else if (ScopeSteady.Holding)
        {
            breathLabel.text = "STEADY";
            breathLabel.color = Gold;
            breathFillImage.color = Gold;
        }
        else
        {
            breathLabel.text = "HOLD SHIFT · STEADY";
            breathLabel.color = TextMain;
            breathFillImage.color = new Color(1f, 1f, 1f, 0.85f);
        }
    }

    readonly RaycastHit[] aimHits = new RaycastHit[16];

    bool TryAimTarget(out float distance, out Animal animal)
    {
        distance = 0f;
        animal = null;
        Ray ray = PlayerView.AimRay();
        int count = Physics.RaycastNonAlloc(ray, aimHits, 900f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider bestCollider = null;
        for (int i = 0; i < count; i++)
        {
            Collider c = aimHits[i].collider;
            if (c == null || aimHits[i].distance >= best)
                continue;
            if (c.GetComponentInParent<PlayerMove>() != null)
                continue;
            best = aimHits[i].distance;
            bestCollider = c;
        }

        if (bestCollider == null)
            return false;

        distance = Vector3.Distance(PlayerView.Position(), ray.origin + ray.direction * best);
        animal = bestCollider.GetComponentInParent<Animal>();
        return true;
    }

    void UpdateHitMarker()
    {
        if (HuntFeedback.LastHitTime > hitShownAt)
        {
            hitShownAt = HuntFeedback.LastHitTime;
            Color c = HuntFeedback.LastHitKilled ? new Color(1f, 0.32f, 0.26f) : Color.white;
            if (HuntFeedback.LastHitVital && !HuntFeedback.LastHitKilled)
                c = Gold;
            for (int i = 0; i < hitTicks.Length; i++)
                hitTicks[i].color = c;
        }

        float age = Time.unscaledTime - hitShownAt;
        bool on = age < 0.42f;
        if (hitRoot.gameObject.activeSelf != on)
            hitRoot.gameObject.SetActive(on);
        if (!on)
            return;

        // Pops out, then settles and fades.
        float pop = age < 0.06f ? Mathf.Lerp(1.5f, 1f, age / 0.06f) : 1f;
        hitRoot.localScale = Vector3.one * pop * (HuntFeedback.LastHitKilled ? 1.25f : 1f);
        float alpha = 1f - Mathf.InverseLerp(0.2f, 0.42f, age);
        for (int i = 0; i < hitTicks.Length; i++)
        {
            Color c = hitTicks[i].color;
            c.a = alpha;
            hitTicks[i].color = c;
        }
    }

    void UpdateBanner(float dt)
    {
        const float hold = 2.8f;
        if (bannerAge > 0.9f && HuntFeedback.TryTakeBanner(out HuntFeedback.Banner banner))
        {
            bannerAge = 0f;
            bannerTitle.text = banner.title;
            bannerTitle.color = banner.trophy ? Gold : TextMain;
            bannerRule.color = banner.trophy ? Gold : new Color(1f, 1f, 1f, 0.35f);
            bannerDetail.text = banner.detail;
        }

        bannerAge += dt;
        float alpha = bannerAge < 0.15f
            ? bannerAge / 0.15f
            : 1f - Mathf.InverseLerp(hold, hold + 0.6f, bannerAge);
        bannerGroup.alpha = Mathf.Clamp01(alpha);
        float slide = Mathf.SmoothStep(14f, 0f, Mathf.Clamp01(bannerAge / 0.25f));
        bannerTitle.rectTransform.anchoredPosition = new Vector2(0f, -slide);
        bannerRule.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(40f, 220f, Mathf.Clamp01(bannerAge / 0.4f)), 2f);
    }

    void UpdateDetection()
    {
        if (Time.unscaledTime >= nextAnimalScan)
        {
            nextAnimalScan = Time.unscaledTime + 0.5f;
            RescanAnimals();
        }

        detectHits.Clear();
        Vector3 me = PlayerView.Position();
        float rangeSq = DetectRange * DetectRange;
        for (int i = 0; i < watched.Count; i++)
        {
            Watched w = watched[i];
            if (w.animal == null || w.animal.Dead || !w.animal.IsPresent())
                continue;

            Vector3 delta = w.animal.transform.position - me;
            delta.y = 0f;
            if (delta.sqrMagnitude > rangeSq)
                continue;

            float level = w.senses != null ? Mathf.Clamp01(w.senses.Threat) : 0f;
            if (w.brain != null)
            {
                if (w.brain.State == AnimalAiState.Alert)
                    level = Mathf.Max(level, 0.6f);
                else if (w.brain.State == AnimalAiState.Flee)
                    level = 1f;
            }
            else if (w.animal.Fleeing)
                level = 1f;

            if (level < 0.3f)
                continue;

            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            detectHits.Add((level, bearing));
        }

        detectHits.Sort((a, b) => b.level.CompareTo(a.level));

        bool show = !Inventory.IsOpen && !BirdChallenge.Active;
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        for (int i = 0; i < detectArrows.Count; i++)
        {
            Image arrow = detectArrows[i];
            bool on = show && i < detectHits.Count;
            if (arrow.gameObject.activeSelf != on)
                arrow.gameObject.SetActive(on);
            if (!on)
                continue;

            float rel = Mathf.DeltaAngle(yaw, detectHits[i].bearing);
            float rad = rel * Mathf.Deg2Rad;
            float level = detectHits[i].level;
            float radius = 58f + (1f - level) * 10f;
            arrow.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -rel);
            Color c = Color.Lerp(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.26f, 0.2f), Mathf.InverseLerp(0.55f, 1f, level));
            float pulse = level >= 0.99f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;
            c.a = Mathf.Lerp(0.45f, 0.95f, level) * pulse;
            arrow.color = c;
        }
    }

    void RescanAnimals()
    {
        watched.Clear();
        Animal[] animals = Object.FindObjectsByType<Animal>();
        Vector3 me = PlayerView.Position();
        float scanSq = (DetectRange + 60f) * (DetectRange + 60f);
        for (int i = 0; i < animals.Length; i++)
        {
            Animal a = animals[i];
            if (a == null || a.Dead)
                continue;
            if ((a.transform.position - me).sqrMagnitude > scanSq)
                continue;
            watched.Add(new Watched
            {
                animal = a,
                brain = a.GetComponent<AnimalBrain>(),
                senses = a.GetComponent<AnimalSenses>(),
            });
        }
    }

    void UpdatePois()
    {
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        Vector3 me = PlayerView.Position();
        float half = CompassWidth * 0.5f - 12f;

        for (int i = 0; i < pois.Count; i++)
        {
            Poi poi = pois[i];
            Vector3? where = poi.where();
            bool on = where.HasValue;
            if (poi.rect.gameObject.activeSelf != on)
                poi.rect.gameObject.SetActive(on);
            if (!on)
                continue;

            Vector3 delta = where.Value - me;
            delta.y = 0f;
            float dist = delta.magnitude;
            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float x = Mathf.DeltaAngle(yaw, bearing) * PixelsPerDegree;
            bool pinned = Mathf.Abs(x) > half;
            x = Mathf.Clamp(x, -half, half);
            poi.rect.anchoredPosition = new Vector2(x, -13f);

            // Pinned to the edge = it is behind you; only the diamond shows, dimmed.
            bool near = Mathf.Abs(x) < 70f && !pinned;
            string label = near ? poi.label + "  " + FormatDistance(dist) : (pinned ? "" : poi.label);
            if (poi.text.text != label)
                poi.text.text = label;

            // Keep the label inside the strip on the right-hand side.
            bool flip = x > half - 90f;
            poi.text.alignment = flip ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            poi.text.rectTransform.pivot = new Vector2(flip ? 1f : 0f, 0.5f);
            poi.text.rectTransform.anchoredPosition = new Vector2(flip ? -8f : 8f, 0f);

            Color c = poi.color;
            c.a = pinned ? 0.45f : 1f;
            poi.icon.color = c;
            poi.text.color = c;
        }
    }

    static string FormatDistance(float meters)
    {
        return meters >= 1000f ? (meters / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(meters) + " m";
    }

    void UpdateWind()
    {
        WindSystem wind = WindSystem.Current;
        if (wind == null)
        {
            windText.text = "";
            windArrowImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        Vector3 dir = wind.Direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        float bearing = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        float rel = Mathf.DeltaAngle(yaw, bearing);
        windArrow.localRotation = Quaternion.Euler(0f, 0f, -rel);

        // Wind blowing the way you face carries your scent onto whatever you are stalking.
        bool scentAhead = Mathf.Abs(rel) < 55f && wind.Strength > 0.15f;
        Color c = scentAhead ? WarnColor : WaterColor;
        c.a = 0.55f + 0.45f * Mathf.Clamp01(wind.Strength);
        windArrowImage.color = c;

        string label = scentAhead
            ? "<color=#FF6A5C>SCENT AHEAD</color>"
            : "WIND " + Mathf.RoundToInt(Mathf.Clamp01(wind.Strength) * 100f) + "%";
        windText.richText = true;
        if (windText.text != label)
            windText.text = label;
    }

    void UpdateToasts(float dt)
    {
        for (int i = 0; i < toasts.Length; i++)
        {
            ToastRow row = toasts[i];
            if (string.IsNullOrEmpty(row.message))
                continue;

            row.age += dt;
            bool on = row.age < ToastLife;
            if (row.bg.gameObject.activeSelf != on)
                row.bg.gameObject.SetActive(on);
            if (!on)
                continue;

            float alpha = row.age < 0.12f
                ? row.age / 0.12f
                : 1f - Mathf.InverseLerp(ToastLife - 0.5f, ToastLife, row.age);
            float fade = Mathf.Lerp(1f, 0.7f, i / (float)ToastRows);
            Color bg = PanelColor;
            bg.a *= alpha * fade;
            row.bg.color = bg;
            Color t = TextMain;
            t.a = alpha * fade;
            row.text.color = t;
        }
    }

    // ---------------------------------------------------------------- helpers

    static void AddShadow(TMP_Text text)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    // Upward-pointing chevron, anti-aliased, for detection arrows and the wind gauge.
    static Sprite MakeChevron(int size)
    {
        Texture2D tex = NewTexture(size);
        Color32[] px = new Color32[size * size];
        Vector2 tip = new Vector2(0.5f, 0.92f);
        Vector2 leftFoot = new Vector2(0.08f, 0.1f);
        Vector2 rightFoot = new Vector2(0.92f, 0.1f);
        Vector2 notch = new Vector2(0.5f, 0.38f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                bool inside = InTriangle(p, tip, leftFoot, notch) || InTriangle(p, tip, notch, rightFoot);
                float edge = Mathf.Min(
                    SegmentDistance(p, tip, leftFoot), SegmentDistance(p, leftFoot, notch),
                    SegmentDistance(p, notch, rightFoot), SegmentDistance(p, rightFoot, tip)) * size;
                float a = inside ? Mathf.Clamp01(edge + 0.5f) : Mathf.Clamp01(0.5f - edge);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.000001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}

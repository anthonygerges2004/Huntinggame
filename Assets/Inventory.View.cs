using HuntingGame.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The backpack screen and the hotbar, built at runtime in the HUD's style.
//
//   BACKPACK window (G)
//     header    title, slots used, mini vitals
//     left      hotbar row (keys 1-5) and 4 rows of storage
//     right     details for the hovered / selected item: picture, category, description,
//               stats, and actions (equip, eat, place bait, drop)
//     footer    control hints
//
//   HOTBAR (bag closed)  five slots at the bottom centre, selected one raised in gold.
//
// Mouse: drag to move or stack, drag outside the window to drop, right-click to use,
// hover a slot and press 1-5 to put it on that hotbar key.
public partial class Inventory
{
    // ---------------------------------------------------------------- layout

    const float WinW = 1100f;
    const float WinH = 708f;
    const float GridSlot = 84f;
    const float GridGap = 8f;
    const float BarSlot = 66f;
    const float BarGap = 8f;

    sealed class SlotWidget
    {
        public RectTransform root;
        public Image bg;
        public Image border;
        public RawImage icon;
        public TMP_Text key;
        public TMP_Text count;
        public Image strip;
        public GameObject spoilTrack;
        public RectTransform spoilFill;
        public string shownCount = null;
        public Texture shownIcon;
    }

    sealed class StatRow
    {
        public GameObject root;
        public TMP_Text label;
        public Image[] segments;
        public TMP_Text value;
    }

    [System.NonSerialized] bool viewBuilt; // false again after a script reload, so the view rebuilds
    Canvas bagCanvas;
    CanvasGroup bagGroup;
    CanvasGroup barGroup;
    RectTransform windowRect;
    RectTransform canvasRect;
    readonly SlotWidget[] gridSlots = new SlotWidget[SlotCount];
    readonly SlotWidget[] barSlots = new SlotWidget[GridColumns];
    TMP_Text barLabel;
    float barLabelAge = 100f;
    int barLabelFor = -99;

    TMP_Text slotsUsedText;
    readonly Image[] vitalFills = new Image[4];
    readonly TMP_Text[] vitalValues = new TMP_Text[4];

    RectTransform detailRoot;
    TMP_Text detailEmpty;
    RawImage detailIcon;
    TMP_Text detailName, detailCategory, detailCount, detailExtra, detailDesc;
    Image detailCategoryPill;
    readonly StatRow[] statRows = new StatRow[3];
    readonly Button[] actionButtons = new Button[3];
    readonly TMP_Text[] actionLabels = new TMP_Text[3];
    readonly System.Action[] actionCalls = new System.Action[3];

    RawImage ghostIcon;
    TMP_Text ghostCount;
    RectTransform ghostRect;

    int hoverIndex = -1;
    int detailIndex;

    // ---------------------------------------------------------------- per frame

    void UpdateView()
    {
        if (!viewBuilt)
        {
            if (UIKit.Font == null)
                return;
            BuildView();
        }

        float dt = Time.unscaledDeltaTime;
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        bool bagShown = inventoryOpen && !dead && !GamePause.Active;
        bool barShown = !inventoryOpen && !dead && !GamePause.Active;

        bagGroup.alpha = Mathf.MoveTowards(bagGroup.alpha, bagShown ? 1f : 0f, dt * 9f);
        bagGroup.blocksRaycasts = bagShown;
        bagGroup.interactable = bagShown;
        barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, barShown ? 1f : 0f, dt * 9f);
        if (!bagShown)
            hoverIndex = -1;

        if (barGroup.alpha > 0f)
            RefreshHotbar(dt);
        if (bagGroup.alpha > 0f)
            RefreshBag();
    }

    void RefreshHotbar(float dt)
    {
        for (int i = 0; i < barSlots.Length; i++)
        {
            bool selected = i == selectedHotbar;
            RefreshSlot(barSlots[i], slots[i], selected, false, false);
            // The selected slot lifts a little.
            float lift = selected ? 6f : 0f;
            Vector2 pos = barSlots[i].root.anchoredPosition;
            pos.y = Mathf.MoveTowards(pos.y, 8f + lift, dt * 60f);
            barSlots[i].root.anchoredPosition = pos;
        }

        if (barLabelFor != selectedHotbar)
        {
            barLabelFor = selectedHotbar;
            barLabelAge = 0f;
            bool has = selectedHotbar >= 0 && selectedHotbar < slots.Length && slots[selectedHotbar].count > 0;
            barLabel.text = has ? SlotTitle(slots[selectedHotbar]) : "EMPTY HANDS";
        }

        barLabelAge += dt;
        Color c = UIKit.TextMain;
        c.a = 1f - Mathf.InverseLerp(1.8f, 2.4f, barLabelAge);
        barLabel.color = c;
    }

    void RefreshBag()
    {
        int used = 0;
        int items = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count > 0)
            {
                used++;
                items += slots[i].count;
            }

            RefreshSlot(gridSlots[i], slots[i], i == selectedHotbar, i == ShownDetail(), i == hoverIndex);
        }

        float kg = CarryKg;
        slotsUsedText.text = used + " / " + SlotCount + " SLOTS   ·   <color=#F5C466>" + coins + " COINS</color>"
                             + (kg > 0.5f ? "   ·   " + (kg > HeavyLoadKg ? "<color=#FF6A5C>" : "") + Mathf.RoundToInt(kg) + " KG HAULED"
                                + (kg > HeavyLoadKg ? "</color>" : "") : "");
        RefreshVitals();
        RefreshDetails();
        RefreshEquipment();
    }

    // ---------------------------------------------------------------- equipment row

    readonly (Image dot, TMP_Text name, TMP_Text status)[] equipment = new (Image, TMP_Text, TMP_Text)[4];

    void BuildEquipment(RectTransform win)
    {
        TMP_Text label = UIKit.Text(win, "EQUIPMENT", 11f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, true, 6f);
        UIKit.Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 62f), new Vector2(120f, 16f));

        const float left = 152f;
        float tileW = (WinW - left - 36f - 3f * 10f) / 4f;
        for (int i = 0; i < equipment.Length; i++)
        {
            Image tile = UIKit.Img(win, "Gear" + i, UIKit.Round, new Color(1f, 1f, 1f, 0.04f), 8f);
            UIKit.Place(tile.rectTransform, new Vector2(0f, 0f), new Vector2(left + i * (tileW + 10f), 50f), new Vector2(tileW, 40f));
            Image dot = UIKit.Img(tile.rectTransform, "Dot", UIKit.Disc, UIKit.TextFaint);
            UIKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(8f, 8f), new Vector2(0f, 0.5f));
            TMP_Text name = UIKit.Text(tile.rectTransform, "", 12f, UIKit.TextMain, TextAlignmentOptions.MidlineLeft, true, 3f);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -4f), new Vector2(tileW - 34f, 16f));
            TMP_Text status = UIKit.Text(tile.rectTransform, "", 11f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, false);
            status.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 4f), new Vector2(tileW - 34f, 16f));
            equipment[i] = (dot, name, status);
        }
    }

    void RefreshEquipment()
    {
        // Fire starter: a lighter (owned or rented) or the starter matches.
        if (ownsLighter)
            SetGear(0, "LIGHTER", "Owned  ·  lights fires (F)", UIKit.Good);
        else if (rentedLighter && lighterSeconds > 0f)
            SetGear(0, "LIGHTER", "Rented  ·  return in " + FormatTime(lighterSeconds), UIKit.Gold);
        else if (rentedLighter)
            SetGear(0, "LIGHTER", "<color=#FF6A5C>Overdue! Return it (6)</color>", UIKit.Warn);
        else if (matches > 0)
            SetGear(0, "MATCHES", matches + (matches == 1 ? " left" : " left") + "  ·  one per fire", UIKit.Gold);
        else
            SetGear(0, "LIGHTER", "None  ·  buy or rent at gun shop", UIKit.TextFaint);

        SurvivalStats s = SurvivalStats.Current;
        bool coat = s != null && s.HasHuntingCoat;
        bool oil = s != null && s.HasOilskin;
        SetGear(1, "HUNTING COAT", coat ? "Worn  ·  the cold bites less" : "Not owned  ·  clothes stall", coat ? UIKit.Good : UIKit.TextFaint);
        SetGear(2, "OILSKIN", oil ? "Worn  ·  rain soaks you slower" : "Not owned  ·  clothes stall", oil ? UIKit.Good : UIKit.TextFaint);
        SetGear(3, "PICKAXE", hasPickaxe ? "Carried" : "Not owned  ·  trader", hasPickaxe ? UIKit.Good : UIKit.TextFaint);
    }

    void SetGear(int i, string name, string status, Color dot)
    {
        if (equipment[i].name.text != name)
            equipment[i].name.text = name;
        if (equipment[i].status.text != status)
            equipment[i].status.text = status;
        equipment[i].dot.color = dot;
        equipment[i].name.color = dot == UIKit.TextFaint ? UIKit.TextDim : UIKit.TextMain;
    }

    int ShownDetail()
    {
        if (hoverIndex >= 0 && slots[hoverIndex].count > 0)
            return hoverIndex;
        return detailIndex;
    }

    void RefreshSlot(SlotWidget w, Slot slot, bool equipped, bool focused, bool hovered)
    {
        bool has = slot.count > 0;
        w.bg.color = hovered ? UIKit.TileHover : (has ? new Color(1f, 1f, 1f, 0.065f) : new Color(1f, 1f, 1f, 0.03f));

        Texture icon = has ? IconFor(slot) : null;
        if (w.shownIcon != icon)
        {
            w.shownIcon = icon;
            w.icon.texture = icon;
            w.icon.enabled = icon != null;
        }

        string count = "";
        if (has)
        {
            if (slot.kind == ItemKind.Axe)
                count = AxeSwing.HasAxe ? "" : "<size=70%><color=#FF6A5C>OUT</color></size>";
            else if (slot.kind == ItemKind.Carcass && slot.goods != null)
                count = "<size=75%>" + Mathf.RoundToInt(slot.goods.weightKg) + "kg</size>";
            else if (slot.kind == ItemKind.Pelt && slot.goods != null)
                count = "<size=75%><color=#F5C466>" + slot.goods.PeltValue(Time.time) + "c</color></size>";
            else if (IsWeapon(slot.kind))
            {
                PlayerShotgun gun = PlayerShotgun.Instance;
                if (gun != null && gun.TryRounds(ToWeapon(slot.kind), out int rounds, out int mag, out _))
                    count = rounds + "<size=70%><color=#9A9A92>/" + mag + "</color></size>";
            }
            else
                count = slot.count.ToString();
        }

        if (w.shownCount != count)
        {
            w.shownCount = count;
            w.count.text = count;
        }

        // A thrown axe stays on its slot, faded, until it is picked back up.
        w.icon.color = has && slot.kind == ItemKind.Axe && !AxeSwing.HasAxe ? new Color(1f, 1f, 1f, 0.35f) : Color.white;

        w.strip.enabled = has;
        if (has)
            w.strip.color = CategoryColor(slot.kind);

        bool carcass = has && slot.kind == ItemKind.Carcass && slot.goods != null;
        bool spoiling = has && (slot.kind == ItemKind.RawMeat || carcass);
        if (w.spoilTrack.activeSelf != spoiling)
            w.spoilTrack.SetActive(spoiling);
        if (spoiling)
        {
            float frac = carcass
                ? Mathf.Clamp01(slot.goods.SecondsUntilSpoiled(Time.time) / HuntGoods.SpoilSeconds)
                : Mathf.Clamp01(SpoilSecondsLeft() / Mathf.Max(1f, meatSpoilSeconds));
            w.spoilFill.anchorMax = new Vector2(frac, 1f);
            w.spoilFill.GetComponent<Image>().color = frac < 0.25f ? UIKit.Warn : new Color(0.95f, 0.55f, 0.45f);
        }

        if (equipped)
        {
            w.border.enabled = true;
            w.border.color = UIKit.Gold;
        }
        else if (focused && has)
        {
            w.border.enabled = true;
            w.border.color = new Color(1f, 1f, 1f, 0.55f);
        }
        else if (hovered)
        {
            w.border.enabled = true;
            w.border.color = new Color(1f, 1f, 1f, 0.22f);
        }
        else
            w.border.enabled = false;

        if (w.key != null)
            w.key.color = equipped ? UIKit.Gold : UIKit.TextFaint;
    }

    void RefreshVitals()
    {
        float health = 1f;
        if (PlayerHealth.Instance != null)
            health = Mathf.Clamp01(PlayerHealth.Instance.health / Mathf.Max(1f, PlayerHealth.Instance.maxHealth));
        SurvivalStats s = SurvivalStats.Current;
        float[] v =
        {
            health,
            s != null ? Mathf.Clamp01(s.Hunger01) : 1f,
            s != null ? Mathf.Clamp01(s.Water01) : 1f,
            s != null ? Mathf.Clamp01(s.Warmth01) : 1f,
        };

        for (int i = 0; i < 4; i++)
        {
            vitalFills[i].rectTransform.anchorMax = new Vector2(v[i], 1f);
            string text = Mathf.RoundToInt(v[i] * 100f).ToString();
            if (vitalValues[i].text != text)
                vitalValues[i].text = text;
            vitalValues[i].color = v[i] < 0.25f ? UIKit.Warn : UIKit.TextMain;
        }
    }

    void RefreshDetails()
    {
        int index = ShownDetail();
        bool has = index >= 0 && index < slots.Length && slots[index].count > 0;
        detailEmpty.gameObject.SetActive(!has);
        detailRoot.gameObject.SetActive(has);
        if (!has)
            return;

        Slot slot = slots[index];
        ItemKind kind = slot.kind;
        Texture icon = IconFor(slot);
        if (detailIcon.texture != icon)
            detailIcon.texture = icon;
        detailIcon.enabled = icon != null;

        detailName.text = SlotTitle(slot);
        string category = CategoryName(kind);
        detailCategory.text = category;
        Color cat = CategoryColor(kind);
        detailCategory.color = cat;
        Color pill = cat;
        pill.a = 0.16f;
        detailCategoryPill.color = pill;
        detailCategoryPill.rectTransform.sizeDelta = new Vector2(detailCategory.preferredWidth + 22f, 22f);
        detailDesc.text = slot.goods != null ? GoodsDescription(kind) : ItemDescription(kind);

        string countLine;
        string extra = "";
        if (IsGoods(kind) && slot.goods != null)
        {
            DescribeGoods(kind, slot.goods, out countLine, out extra);
        }
        else if (kind == ItemKind.Axe)
        {
            countLine = AxeSwing.HasAxe ? "Sharp and ready" : "<color=#FF6A5C>Thrown. Walk over it to pick it up.</color>";
            int key = index < GridColumns ? index + 1 : 0;
            extra = index == selectedHotbar
                ? "<color=#F5C466>IN HAND</color>"
                : (key > 0 ? "On hotbar key " + key : "In storage. Move it to the hotbar to use it.");
            HideStat(0);
            HideStat(1);
            HideStat(2);
        }
        else if (IsWeapon(kind))
        {
            HuntWeapon weapon = ToWeapon(kind);
            int rounds = 0, mag = 0;
            float range = 0f;
            if (PlayerShotgun.Instance != null)
                PlayerShotgun.Instance.TryRounds(weapon, out rounds, out mag, out range);
            int spare = PlayerShotgun.Instance != null ? PlayerShotgun.Instance.CartridgeCount(weapon) : 0;
            countLine = rounds + " / " + mag + " loaded   ·   "
                        + (spare > 0 ? spare + " spare" : "<color=#FF6A5C>no spares</color>");
            int key = index < GridColumns ? index + 1 : 0;
            extra = index == selectedHotbar
                ? "<color=#F5C466>EQUIPPED</color>"
                : (key > 0 ? "On hotbar key " + key : "In storage. Move it to the hotbar to use it.");
            SetStat(0, "RANGE", Mathf.Clamp01(range / 500f), Mathf.RoundToInt(range) + " m");
            SetStat(1, "ACCURACY", WeaponAccuracy(weapon), WeaponAccuracyLabel(weapon));
            SetStat(2, "CAPACITY", Mathf.Clamp01(mag / 6f), mag + (weapon == HuntWeapon.Shotgun ? " shells" : " rounds"));
        }
        else
        {
            int total = TotalOf(kind);
            countLine = total + " carried" + (total != slot.count ? "   ·   " + slot.count + " in this stack" : "");
            if (kind == ItemKind.RawMeat)
            {
                float left = SpoilSecondsLeft();
                float frac = Mathf.Clamp01(left / Mathf.Max(1f, meatSpoilSeconds));
                extra = frac < 0.25f ? "<color=#FF6A5C>Spoiling soon</color>" : "Fresh";
                SetStat(0, "FRESHNESS", frac, FormatTime(left));
            }
            else if (kind == ItemKind.CookedMeat)
            {
                extra = "Keeps forever";
                SetStat(0, "STACK", slot.count / (float)maxStackSize, slot.count + " / " + maxStackSize);
            }
            else
            {
                SetStat(0, "STACK", slot.count / (float)maxStackSize, slot.count + " / " + maxStackSize);
            }

            HideStat(1);
            HideStat(2);
        }

        detailCount.text = countLine;
        detailExtra.text = extra;
        SetupActions(index, kind);
    }

    void SetStat(int row, string label, float value01, string value)
    {
        StatRow r = statRows[row];
        r.root.SetActive(true);
        r.label.text = label;
        r.value.text = value;
        int lit = Mathf.Clamp(Mathf.RoundToInt(value01 * r.segments.Length), value01 > 0.001f ? 1 : 0, r.segments.Length);
        for (int i = 0; i < r.segments.Length; i++)
            r.segments[i].color = i < lit ? UIKit.Gold : UIKit.Track;
    }

    void HideStat(int row)
    {
        statRows[row].root.SetActive(false);
    }

    void SetupActions(int index, ItemKind kind)
    {
        for (int i = 0; i < actionCalls.Length; i++)
            actionCalls[i] = null;

        if (kind == ItemKind.Carcass)
        {
            SetAction(0, "SKIN IT HERE", () => SkinSlotByHand(index), true);
            SetAction(1, "DISCARD", () => DiscardSlot(index), false);
            SetAction(2, "", null, false);
            return;
        }

        if (kind == ItemKind.Pelt)
        {
            SetAction(0, "", null, false);
            SetAction(1, "DISCARD", () => DiscardSlot(index), false);
            SetAction(2, "", null, false);
            return;
        }

        if (IsGear(kind))
        {
            bool equipped = index == selectedHotbar;
            SetAction(0, equipped ? "EQUIPPED" : "EQUIP", equipped ? (System.Action)null : () => EquipFromSlot(index), true);
            SetAction(1, index < GridColumns ? "UNEQUIP" : "", equipped ? () => SelectHotbar(-1, true) : (System.Action)null, false);
            SetAction(2, "", null, false);
            return;
        }

        switch (kind)
        {
            case ItemKind.CookedMeat:
                SetAction(0, "EAT", () => UseSlot(index), true);
                break;
            case ItemKind.RawMeat:
                SetAction(0, "EAT RAW", () => UseSlot(index), true);
                break;
            case ItemKind.SpoiledMeat:
                SetAction(0, "PLACE BAIT", () => UseSlot(index), true);
                break;
            default:
                SetAction(0, "", null, false);
                break;
        }

        SetAction(1, "DROP 1", () => DropFromSlot(index, 1), false);
        SetAction(2, "DROP ALL", () => DropFromSlot(index, int.MaxValue), false);
    }

    void SetAction(int i, string label, System.Action call, bool primary)
    {
        bool show = !string.IsNullOrEmpty(label);
        if (actionButtons[i].gameObject.activeSelf != show)
            actionButtons[i].gameObject.SetActive(show);
        if (!show)
            return;

        actionLabels[i].text = label;
        actionCalls[i] = call;
        actionButtons[i].interactable = call != null;
        actionLabels[i].color = call == null ? UIKit.TextFaint : (primary ? UIKit.Gold : UIKit.TextMain);
    }

    // ---------------------------------------------------------------- slot events

    public void SlotHover(int index, bool on)
    {
        if (on)
            hoverIndex = index;
        else if (hoverIndex == index)
            hoverIndex = -1;
    }

    public void SlotClicked(int index)
    {
        if (index >= 0 && index < slots.Length && slots[index].count > 0)
            detailIndex = index;
    }

    public void SlotUse(int index)
    {
        if (index >= 0 && index < slots.Length && slots[index].count > 0)
        {
            detailIndex = index;
            if (IsGoods(slots[index].kind))
                return;
            if (IsGear(slots[index].kind))
                EquipFromSlot(index);
            else
                UseSlot(index);
        }
    }

    public void SlotBeginDrag(int index, Vector2 screen)
    {
        if (!inventoryOpen || dragging || index < 0 || index >= slots.Length || slots[index].count <= 0)
            return;

        dragging = true;
        draggedSlot = slots[index];
        dragFromIndex = index;
        slots[index] = default;
        RecalculatePublicCounts();

        ghostIcon.texture = IconFor(draggedSlot);
        ghostCount.text = IsSingle(draggedSlot.kind) ? "" : draggedSlot.count.ToString();
        ghostRect.gameObject.SetActive(true);
        SlotDragMove(screen);
    }

    public void SlotDragMove(Vector2 screen)
    {
        if (!dragging)
            return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            ghostRect.anchoredPosition = local;
    }

    public void SlotDrop(int index)
    {
        if (!dragging)
            return;
        PlaceDraggedIntoSlot(index);
        detailIndex = index;
        ghostRect.gameObject.SetActive(false);
    }

    public void SlotEndDrag(Vector2 screen)
    {
        ghostRect.gameObject.SetActive(false);
        if (!dragging)
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(windowRect, screen, null))
            DropDraggedStack();
        else
            ReturnDraggedToSource();
    }

    // ---------------------------------------------------------------- actions

    void EquipFromSlot(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index].count <= 0)
            return;

        if (index < GridColumns)
        {
            SelectHotbar(index, true);
            detailIndex = index;
            return;
        }

        // From storage: swap into the selected hotbar key, or the first empty one.
        int target = -1;
        for (int i = 0; i < GridColumns; i++)
        {
            if (slots[i].count <= 0)
            {
                target = i;
                break;
            }
        }

        if (target < 0)
            target = selectedHotbar >= 0 ? selectedHotbar : 0;

        Slot moved = slots[index];
        slots[index] = slots[target];
        slots[target] = moved;
        RecalculatePublicCounts();
        SelectHotbar(target, true);
        detailIndex = target;
    }

    void UseSlot(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index].count <= 0)
            return;

        switch (slots[index].kind)
        {
            case ItemKind.CookedMeat:
                if (SurvivalStats.Current != null && SurvivalStats.Current.Hunger01 > 0.97f)
                    ShowNotice("You're full.");
                else if (EatCookedMeat())
                    ShowNotice("Ate cooked meat.");
                break;
            case ItemKind.RawMeat:
                EatRawNow();
                break;
            case ItemKind.SpoiledMeat:
                PlaceOneBait();
                break;
            case ItemKind.Wood:
                ShowNotice("Wood feeds a fire (F) or builds shelter (B).");
                break;
        }
    }

    void EatRawNow()
    {
        if (SurvivalStats.Current != null && SurvivalStats.Current.Hunger01 > 0.97f)
        {
            ShowNotice("You're full.");
            return;
        }

        if (RemoveItem(ItemKind.RawMeat, 1) != 1)
            return;
        bool sick = SurvivalStats.Current != null && SurvivalStats.Current.EatRaw();
        ShowNotice(sick ? "That raw meat made you sick. Food poisoning!" : "Ate raw meat. You got lucky this time.");
    }

    void DropFromSlot(int index, int amount)
    {
        if (index < 0 || index >= slots.Length || slots[index].count <= 0 || IsSingle(slots[index].kind))
            return;

        ItemKind kind = slots[index].kind;
        int take = Mathf.Min(amount, slots[index].count);
        slots[index].count -= take;
        if (slots[index].count <= 0)
            slots[index] = default;
        RecalculatePublicCounts();
        DropItemToWorld(kind, take);
        ShowNotice("Dropped " + take + " " + ItemShortName(kind).ToLowerInvariant());
    }

    int TotalOf(ItemKind kind)
    {
        int total = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].kind == kind && slots[i].count > 0)
                total += slots[i].count;
        }
        return total;
    }

    // ---------------------------------------------------------------- item data

    static Texture IconFor(Slot slot)
    {
        if (slot.goods != null && IsGoods(slot.kind))
            return ItemIcons.Goods(slot.goods, slot.kind == ItemKind.Pelt);
        return IconFor(slot.kind);
    }

    static string SlotTitle(Slot slot)
    {
        if (slot.goods != null && slot.kind == ItemKind.Carcass)
            return slot.goods.Title + " CARCASS";
        if (slot.goods != null && slot.kind == ItemKind.Pelt)
            return slot.goods.Title + " PELT";
        return ItemName(slot.kind);
    }

    static string GoodsDescription(ItemKind kind)
    {
        return kind == ItemKind.Carcass
            ? "A whole animal. Bring it to the Tanner in the castle market: he skins it cleanly for full meat and an intact pelt. Heavy loads slow you down, and it spoils with time."
            : "A cured hide. It keeps. Sell it to the Tanner in the castle market; its price depends on the animal, its quality and how cleanly it was taken.";
    }

    void DescribeGoods(ItemKind kind, HuntGoods g, out string countLine, out string extra)
    {
        float now = Time.time;
        int value = g.PeltValue(now);
        countLine = Mathf.RoundToInt(g.weightKg) + " kg   ·   " + g.TierLabel + "   ·   " + g.ConditionLabel;
        if (kind == ItemKind.Carcass)
        {
            float left = g.SecondsUntilSpoiled(now);
            extra = g.Spoiled(now)
                ? "<color=#FF6A5C>Spoiled. Meat is only bait now.</color>"
                : (now - g.killedAt > HuntGoods.FreshSeconds ? "<color=#FF6A5C>Turning · spoils in " + FormatTime(left) + "</color>"
                                                               : "Fresh · spoils in " + FormatTime(left));
            SetStat(0, "PELT VALUE", Mathf.Clamp01(value / 120f), "~" + value + " coins");
            SetStat(1, "FRESHNESS", g.Freshness01(now), g.FreshnessLabel(now));
            SetStat(2, "MEAT", Mathf.Clamp01(g.meat / 8f), g.meat + " (tanner)  ·  " + Mathf.CeilToInt(g.meat * HuntGoods.RoughMeatShare) + " (by hand)");
        }
        else
        {
            extra = "<color=#F5C466>The Tanner pays " + value + " coins</color>";
            SetStat(0, "PELT VALUE", Mathf.Clamp01(value / 120f), value + " coins");
            SetStat(1, "CONDITION", 1f - (int)g.condition / 3.5f - (g.rough ? 0.15f : 0f), g.ConditionLabel);
            SetStat(2, "QUALITY", Mathf.Clamp01(HuntGoods.TierMultiplier(g.tier, g.coat) / 6f), g.TierLabel);
        }
    }

    static Texture IconFor(ItemKind kind)
    {
        if (IsWeapon(kind))
            return WeaponIcons.Get(ToWeapon(kind));
        return ItemIcons.Get(kind.ToString());
    }

    static string ItemName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat: return "RAW VENISON";
            case ItemKind.CookedMeat: return "COOKED MEAT";
            case ItemKind.SpoiledMeat: return "SPOILED MEAT";
            case ItemKind.Wood: return "FIREWOOD";
            case ItemKind.Shotgun: return "COMBAT SHOTGUN";
            case ItemKind.Rifle: return "ASSAULT RIFLE";
            case ItemKind.Sniper: return "SNIPER RIFLE";
            case ItemKind.Pistol: return "PISTOL";
            case ItemKind.SMG: return "SMG";
            case ItemKind.Axe: return "HAND AXE";
            case ItemKind.Carcass: return "CARCASS";
            case ItemKind.Pelt: return "PELT";
            default: return "";
        }
    }

    static string CategoryName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat:
            case ItemKind.CookedMeat: return "FOOD";
            case ItemKind.SpoiledMeat: return "BAIT";
            case ItemKind.Wood: return "MATERIAL";
            case ItemKind.Axe: return "TOOL";
            case ItemKind.Carcass: return "CARCASS";
            case ItemKind.Pelt: return "PELT";
            default: return "WEAPON";
        }
    }

    static Color CategoryColor(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat: return new Color(0.95f, 0.45f, 0.42f);
            case ItemKind.CookedMeat: return new Color(0.96f, 0.68f, 0.36f);
            case ItemKind.SpoiledMeat: return new Color(0.66f, 0.76f, 0.36f);
            case ItemKind.Wood: return new Color(0.80f, 0.62f, 0.40f);
            case ItemKind.Axe: return new Color(0.80f, 0.82f, 0.86f);
            case ItemKind.Carcass: return new Color(0.82f, 0.38f, 0.32f);
            case ItemKind.Pelt: return new Color(0.92f, 0.74f, 0.46f);
            default: return new Color(0.58f, 0.74f, 0.95f);
        }
    }

    static string ItemDescription(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat:
                return "Fresh from the hunt. Cook it on a lit fire (C) before it spoils. Eating it raw fills you up but can give you food poisoning.";
            case ItemKind.CookedMeat:
                return "Grilled over the fire. The safe way to restore hunger, and it never spoils. Press Q to eat.";
            case ItemKind.SpoiledMeat:
                return "Not fit to eat. Place it as bait (X) to draw prey in for a while. At night, away from the fire, it can lure something big.";
            case ItemKind.Wood:
                return "Chopped from trees (E). Feeds the campfire and builds shelter (B). A fire keeps you warm and predators away.";
            case ItemKind.Axe:
                return "A trusty hand axe. Click or press E to chop trees for firewood, and right-click to throw it at prey. Walk over it to pick it back up.";
            case ItemKind.Shotgun:
                return "Six shells of wide buckshot. Devastating up close and the best choice for birds and fast targets, but it tears pelts.";
            case ItemKind.Rifle:
                return "A thirty-round assault rifle. Hold the trigger for full auto, tap it for accurate shots to medium range.";
            case ItemKind.Pistol:
                return "Your sidearm. Twelve rounds, light and quick. Fine for rabbits and close work; get closer than you think.";
            case ItemKind.SMG:
                return "A compact submachine gun. Thirty rounds of full auto for charging boar and wolves at close range.";
            case ItemKind.Sniper:
                return "A scoped long-range rifle. Hold right mouse to scope in and Shift to steady your breath. Made for clean shots past 100 m.";
            default:
                return "";
        }
    }

    static float WeaponAccuracy(HuntWeapon weapon)
    {
        switch (weapon)
        {
            case HuntWeapon.Shotgun: return 0.25f;
            case HuntWeapon.Pistol: return 0.45f;
            case HuntWeapon.SMG: return 0.35f;
            case HuntWeapon.Rifle: return 0.7f;
            case HuntWeapon.Sniper: return 1f;
            default: return 0f;
        }
    }

    static string WeaponAccuracyLabel(HuntWeapon weapon)
    {
        switch (weapon)
        {
            case HuntWeapon.Shotgun: return "Wide spread";
            case HuntWeapon.Pistol: return "Short range";
            case HuntWeapon.SMG: return "Spray";
            case HuntWeapon.Rifle: return "Precise";
            case HuntWeapon.Sniper: return "Pinpoint";
            default: return "";
        }
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // ---------------------------------------------------------------- build

    void BuildView()
    {
        // After a script reload the old canvas survives without its widgets; replace it.
        if (bagCanvas != null)
            Destroy(bagCanvas.gameObject);
        viewBuilt = true;
        bagCanvas = UIKit.MakeCanvas("InventoryUI", 70, true);
        canvasRect = bagCanvas.GetComponent<RectTransform>();
        bagCanvas.GetComponent<CanvasGroup>().blocksRaycasts = true;

        BuildHotbarView();
        BuildBagView();
    }

    void OnDestroy()
    {
        if (bagCanvas != null)
            Destroy(bagCanvas.gameObject);
    }

    void BuildHotbarView()
    {
        RectTransform root = UIKit.Group(bagCanvas.transform, "Hotbar");
        float width = GridColumns * BarSlot + (GridColumns - 1) * BarGap + 16f;
        UIKit.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(width, BarSlot + 18f), new Vector2(0.5f, 0f));
        barGroup = root.gameObject.AddComponent<CanvasGroup>();
        barGroup.blocksRaycasts = false;
        barGroup.interactable = false;

        Image plate = UIKit.Img(root, "Plate", UIKit.Round, new Color(0.045f, 0.05f, 0.06f, 0.78f), 12f);
        UIKit.Place(plate.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(width, BarSlot + 16f), new Vector2(0.5f, 0f));

        for (int i = 0; i < GridColumns; i++)
        {
            SlotWidget w = BuildSlot(root, BarSlot, i, i + 1, false);
            UIKit.Place(w.root, new Vector2(0f, 0f), new Vector2(8f + i * (BarSlot + BarGap), 8f), new Vector2(BarSlot, BarSlot));
            barSlots[i] = w;
        }

        barLabel = UIKit.Text(root, "", 13f, UIKit.TextMain, TextAlignmentOptions.Center, true, 6f);
        UIKit.Place(barLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(400f, 18f), new Vector2(0.5f, 1f));
        Shadow shadow = barLabel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
    }

    void BuildBagView()
    {
        RectTransform root = UIKit.Group(bagCanvas.transform, "Bag");
        UIKit.Stretch(root);
        bagGroup = root.gameObject.AddComponent<CanvasGroup>();
        bagGroup.alpha = 0f;
        bagGroup.blocksRaycasts = false;

        Image dim = UIKit.Img(root, "Dim", null, new Color(0.01f, 0.015f, 0.02f, 0.55f));
        UIKit.Stretch(dim.rectTransform);

        Image window = UIKit.Img(root, "Window", UIKit.Round, UIKit.Panel, 16f);
        windowRect = window.rectTransform;
        UIKit.Place(windowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WinW, WinH));
        window.raycastTarget = true; // drops inside the window but between slots return the item
        Image edge = UIKit.Img(windowRect, "Edge", UIKit.Outline, new Color(1f, 1f, 1f, 0.06f), 16f);
        UIKit.Stretch(edge.rectTransform);

        BuildHeader(windowRect);
        BuildGrid(windowRect);
        BuildDetails(windowRect);
        BuildEquipment(windowRect);
        BuildFooter(windowRect);
        BuildGhost(root);
    }

    void BuildHeader(RectTransform win)
    {
        TMP_Text title = UIKit.Text(win, "BACKPACK", 28f, UIKit.TextMain, TextAlignmentOptions.MidlineLeft, true, 12f);
        UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -26f), new Vector2(360f, 34f));

        slotsUsedText = UIKit.Text(win, "", 12f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, true, 4f);
        UIKit.Place(slotsUsedText.rectTransform, new Vector2(0f, 1f), new Vector2(38f, -62f), new Vector2(400f, 16f));

        string[] names = { "HEALTH", "FOOD", "WATER", "WARMTH" };
        Color[] colors =
        {
            new Color(0.88f, 0.28f, 0.26f), new Color(0.92f, 0.64f, 0.26f),
            new Color(0.36f, 0.72f, 0.95f), new Color(1f, 0.50f, 0.22f),
        };
        const float meterW = 112f;
        for (int i = 0; i < 4; i++)
        {
            float x = -36f - (3 - i) * (meterW + 18f);
            RectTransform m = UIKit.Group(win, "Vital" + i);
            UIKit.Place(m, new Vector2(1f, 1f), new Vector2(x, -30f), new Vector2(meterW, 34f));
            TMP_Text label = UIKit.Text(m, names[i], 10.5f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, true, 4f);
            UIKit.Place(label.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(meterW, 14f));
            vitalValues[i] = UIKit.Text(m, "100", 13f, UIKit.TextMain, TextAlignmentOptions.MidlineRight, true);
            UIKit.Place(vitalValues[i].rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(50f, 14f));
            Image track = UIKit.Img(m, "Track", UIKit.Round, UIKit.Track, 2f);
            UIKit.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -22f), new Vector2(meterW, 5f));
            vitalFills[i] = UIKit.Img(track.rectTransform, "Fill", UIKit.Round, colors[i], 2f);
            RectTransform fill = vitalFills[i].rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        Image rule = UIKit.Img(win, "HeaderRule", null, new Color(1f, 1f, 1f, 0.07f));
        UIKit.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(WinW - 72f, 1f), new Vector2(0.5f, 1f));
    }

    void BuildGrid(RectTransform win)
    {
        const float left = 36f;
        SectionLabel(win, "HOTBAR", new Vector2(left, -108f));
        SectionLabel(win, "STORAGE", new Vector2(left, -108f - 18f - GridSlot - 16f));

        for (int i = 0; i < SlotCount; i++)
        {
            int col = i % GridColumns;
            int row = i / GridColumns;
            float y = -128f - row * (GridSlot + GridGap);
            if (row > 0)
                y -= 26f; // room for the STORAGE label
            SlotWidget w = BuildSlot(win, GridSlot, i, row == 0 ? col + 1 : 0, true);
            UIKit.Place(w.root, new Vector2(0f, 1f), new Vector2(left + col * (GridSlot + GridGap), y), new Vector2(GridSlot, GridSlot));
            gridSlots[i] = w;
        }
    }

    void SectionLabel(RectTransform parent, string text, Vector2 pos)
    {
        TMP_Text t = UIKit.Text(parent, text, 11f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, true, 6f);
        UIKit.Place(t.rectTransform, new Vector2(0f, 1f), pos, new Vector2(200f, 14f));
    }

    SlotWidget BuildSlot(Transform parent, float size, int index, int hotkey, bool interactive)
    {
        SlotWidget w = new SlotWidget();
        w.bg = UIKit.Img(parent, "Slot" + index, UIKit.Round, UIKit.Tile, size * 0.14f);
        w.bg.raycastTarget = interactive;
        w.root = w.bg.rectTransform;

        w.icon = UIKit.Raw(w.root, "Icon");
        UIKit.Stretch(w.icon.rectTransform, size * 0.12f);
        w.icon.rectTransform.offsetMax = new Vector2(-size * 0.12f, -size * 0.14f);
        w.icon.enabled = false;

        w.strip = UIKit.Img(w.root, "Category", UIKit.Round, Color.white, 1.5f);
        UIKit.Place(w.strip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(size * 0.42f, 3f), new Vector2(0.5f, 0f));
        w.strip.enabled = false;

        RectTransform track = UIKit.Img(w.root, "Spoil", UIKit.Round, new Color(0f, 0f, 0f, 0.5f), 1.5f).rectTransform;
        UIKit.Place(track, new Vector2(0f, 1f), new Vector2(size * 0.1f, -5f), new Vector2(size * 0.8f, 3f));
        Image fill = UIKit.Img(track, "Fill", UIKit.Round, UIKit.Warn, 1.5f);
        w.spoilFill = fill.rectTransform;
        w.spoilFill.anchorMin = Vector2.zero;
        w.spoilFill.anchorMax = Vector2.one;
        w.spoilFill.pivot = new Vector2(0f, 0.5f);
        w.spoilFill.offsetMin = Vector2.zero;
        w.spoilFill.offsetMax = Vector2.zero;
        w.spoilTrack = track.gameObject;
        w.spoilTrack.SetActive(false);

        if (hotkey > 0)
        {
            w.key = UIKit.Text(w.root, hotkey.ToString(), size * 0.16f, UIKit.TextFaint, TextAlignmentOptions.TopLeft, true);
            UIKit.Place(w.key.rectTransform, new Vector2(0f, 1f), new Vector2(7f, -5f), new Vector2(20f, 18f));
        }

        w.count = UIKit.Text(w.root, "", Mathf.Max(13f, size * 0.19f), UIKit.TextMain, TextAlignmentOptions.BottomRight, true);
        UIKit.Place(w.count.rectTransform, new Vector2(1f, 0f), new Vector2(-7f, 6f), new Vector2(size - 10f, 22f));
        Shadow cs = w.count.gameObject.AddComponent<Shadow>();
        cs.effectColor = new Color(0f, 0f, 0f, 0.8f);
        cs.effectDistance = new Vector2(1f, -1f);

        w.border = UIKit.Img(w.root, "Border", UIKit.Outline, UIKit.Gold, size * 0.14f);
        UIKit.Stretch(w.border.rectTransform);
        w.border.enabled = false;

        if (interactive)
        {
            InventorySlotView view = w.bg.gameObject.AddComponent<InventorySlotView>();
            view.index = index;
            view.owner = this;
        }

        return w;
    }

    void BuildDetails(RectTransform win)
    {
        float left = 36f + GridColumns * GridSlot + (GridColumns - 1) * GridGap + 34f;
        float width = WinW - left - 36f;
        const float top = -108f;
        float height = WinH - 108f - 110f; // leaves room for the equipment row

        Image panel = UIKit.Img(win, "Details", UIKit.Round, new Color(1f, 1f, 1f, 0.035f), 12f);
        UIKit.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(left, top), new Vector2(width, height));
        RectTransform p = panel.rectTransform;

        detailEmpty = UIKit.Text(p, "Hover or click an item to see it here.", 14f, UIKit.TextDim, TextAlignmentOptions.Center, false);
        UIKit.Stretch(detailEmpty.rectTransform);

        detailRoot = UIKit.Group(p, "Content");
        UIKit.Stretch(detailRoot);

        Image iconBox = UIKit.Img(detailRoot, "IconBox", UIKit.Round, new Color(0f, 0f, 0f, 0.25f), 12f);
        UIKit.Place(iconBox.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(150f, 150f));
        detailIcon = UIKit.Raw(iconBox.rectTransform, "Icon");
        UIKit.Stretch(detailIcon.rectTransform, 10f);

        float tx = 24f + 150f + 22f;
        float tw = width - tx - 24f;
        detailName = UIKit.Text(detailRoot, "", 24f, UIKit.TextMain, TextAlignmentOptions.MidlineLeft, true, 4f);
        UIKit.Place(detailName.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -30f), new Vector2(tw, 30f));

        detailCategoryPill = UIKit.Img(detailRoot, "Pill", UIKit.Round, Color.white, 11f);
        UIKit.Place(detailCategoryPill.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -68f), new Vector2(80f, 22f));
        detailCategory = UIKit.Text(detailCategoryPill.rectTransform, "", 10.5f, Color.white, TextAlignmentOptions.Center, true, 5f);
        UIKit.Stretch(detailCategory.rectTransform);

        detailCount = UIKit.Text(detailRoot, "", 14f, UIKit.TextMain, TextAlignmentOptions.MidlineLeft, false);
        UIKit.Place(detailCount.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -104f), new Vector2(tw, 20f));
        detailExtra = UIKit.Text(detailRoot, "", 13f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, false);
        UIKit.Place(detailExtra.rectTransform, new Vector2(0f, 1f), new Vector2(tx, -128f), new Vector2(tw, 20f));

        detailDesc = UIKit.Text(detailRoot, "", 14f, UIKit.TextDim, TextAlignmentOptions.TopLeft, false);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;
        detailDesc.lineSpacing = 6f;
        UIKit.Place(detailDesc.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -194f), new Vector2(width - 48f, 80f));

        for (int i = 0; i < statRows.Length; i++)
        {
            StatRow r = new StatRow();
            RectTransform row = UIKit.Group(detailRoot, "Stat" + i);
            UIKit.Place(row, new Vector2(0f, 1f), new Vector2(24f, -290f - i * 34f), new Vector2(width - 48f, 20f));
            r.root = row.gameObject;
            r.label = UIKit.Text(row, "", 11f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, true, 4f);
            UIKit.Place(r.label.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 20f), new Vector2(0f, 0.5f));
            r.segments = new Image[10];
            float segW = 20f;
            for (int s = 0; s < r.segments.Length; s++)
            {
                r.segments[s] = UIKit.Img(row, "Seg" + s, UIKit.Round, UIKit.Track, 1.5f);
                UIKit.Place(r.segments[s].rectTransform, new Vector2(0f, 0.5f), new Vector2(118f + s * (segW + 4f), 0f),
                            new Vector2(segW, 6f), new Vector2(0f, 0.5f));
            }
            r.value = UIKit.Text(row, "", 13f, UIKit.TextMain, TextAlignmentOptions.MidlineRight, true);
            UIKit.Place(r.value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 20f), new Vector2(1f, 0.5f));
            row.gameObject.SetActive(false);
            statRows[i] = r;
        }

        float btnW = (width - 48f - 2f * 10f) / 3f;
        for (int i = 0; i < actionButtons.Length; i++)
        {
            int slot = i;
            Image bg = UIKit.Img(detailRoot, "Action" + i, UIKit.Round, Color.white, 8f);
            bg.raycastTarget = true;
            UIKit.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(24f + i * (btnW + 10f), 24f), new Vector2(btnW, 44f));
            Button b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            ColorBlock colors = b.colors;
            colors.normalColor = new Color(1f, 1f, 1f, i == 0 ? 0.10f : 0.06f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.18f);
            colors.pressedColor = new Color(0.96f, 0.77f, 0.40f, 0.45f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            Navigation nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            b.onClick.AddListener(() => actionCalls[slot]?.Invoke());
            actionButtons[i] = b;
            actionLabels[i] = UIKit.Text(bg.rectTransform, "", 13f, UIKit.TextMain, TextAlignmentOptions.Center, true, 5f);
            UIKit.Stretch(actionLabels[i].rectTransform);
        }

        detailRoot.gameObject.SetActive(false);
    }

    void BuildFooter(RectTransform win)
    {
        const string key = "<color=#F5C466><b>";
        const string end = "</b></color>";
        string hints = key + "DRAG" + end + "  move / stack     " + key + "RIGHT-CLICK" + end + "  use     "
                       + key + "1-5" + end + "  put on hotbar     " + key + "DRAG OUT" + end + "  drop";
        TMP_Text t = UIKit.Text(win, hints, 12f, UIKit.TextDim, TextAlignmentOptions.MidlineLeft, false, 1f);
        UIKit.Place(t.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 20f), new Vector2(WinW - 260f, 18f));

        TMP_Text close = UIKit.Text(win, key + "G" + end + " / " + key + "ESC" + end + "  close", 12f, UIKit.TextDim,
                                    TextAlignmentOptions.MidlineRight, false, 1f);
        UIKit.Place(close.rectTransform, new Vector2(1f, 0f), new Vector2(-36f, 20f), new Vector2(220f, 18f));
    }

    void BuildGhost(RectTransform root)
    {
        ghostRect = UIKit.Group(root, "DragGhost");
        UIKit.Place(ghostRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(GridSlot, GridSlot), new Vector2(0.5f, 0.5f));
        Image bg = UIKit.Img(ghostRect, "Bg", UIKit.Round, new Color(0.1f, 0.11f, 0.12f, 0.85f), 12f);
        UIKit.Stretch(bg.rectTransform);
        Image border = UIKit.Img(ghostRect, "Border", UIKit.Outline, UIKit.Gold, 12f);
        UIKit.Stretch(border.rectTransform);
        ghostIcon = UIKit.Raw(ghostRect, "Icon");
        UIKit.Stretch(ghostIcon.rectTransform, 10f);
        ghostCount = UIKit.Text(ghostRect, "", 16f, UIKit.TextMain, TextAlignmentOptions.BottomRight, true);
        UIKit.Place(ghostCount.rectTransform, new Vector2(1f, 0f), new Vector2(-7f, 6f), new Vector2(70f, 22f));
        ghostRect.gameObject.SetActive(false);
    }
}

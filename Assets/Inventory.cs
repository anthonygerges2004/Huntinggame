using HuntingGame.Animals;
using HuntingGame.Ballistics;
using HuntingGame.Crafting;
using HuntingGame.Survival;
using HuntingGame.Tracking;
using HuntingGame.Wounds;
using UnityEngine;
using UnityEngine.InputSystem;

public class Inventory : MonoBehaviour
{
    enum ItemKind
    {
        None,
        RawMeat,
        CookedMeat,
        SpoiledMeat,
        Wood,
        Shotgun,
        Rifle,
        Sniper
    }

    struct Slot
    {
        public ItemKind kind;
        public int count;
    }

    public static Inventory Instance;
    public static bool IsOpen => Instance != null && Instance.inventoryOpen;

    public int meat;
    public int cookedMeat;
    public int spoiledMeat;
    public int wood;
    public bool hasPickaxe = true;
    [Tooltip("Real minutes raw meat stays fresh. Cooked meat keeps.")]
    public float rawMeatFreshMinutes = 6f;
    public float meatSpoilSeconds => Mathf.Max(0.1f, rawMeatFreshMinutes) * 60f;
    float rawConfirmUntil;
    public int maxStackSize = 64;

    const int GridColumns = 5;
    const int GridRows = 5;
    const int SlotCount = GridColumns * GridRows;

    public int selectedHotbar;
    Slot[] slots = new Slot[SlotCount];
    float spoilTimer;
    string notice;
    float noticeUntil;
    bool inventoryOpen;
    bool dragging;
    Slot draggedSlot;
    int dragFromIndex = -1;
    Rect inventoryRect;
    float slotSizeUi;
    float gapUi;
    float padUi;
    float headerUi;
    float footerUi;

    GUIStyle inventoryTextStyle;
    GUIStyle inventoryCountStyle;
    GUIStyle inventoryTitleStyle;

    void Awake()
    {
        Instance = this;
        spoilTimer = meatSpoilSeconds;
        Overhunt.ResetProgress();

        if (GetComponent<HuntWorldDressing>() == null)
            gameObject.AddComponent<HuntWorldDressing>();
        if (GetComponent<HuntChallenges>() == null)
            gameObject.AddComponent<HuntChallenges>();
        if (GetComponent<BirdChallenge>() == null)
            gameObject.AddComponent<BirdChallenge>();
        if (GetComponent<DeerChallenge>() == null)
            gameObject.AddComponent<DeerChallenge>();
        if (GetComponent<AmbientFlyers>() == null)
            gameObject.AddComponent<AmbientFlyers>();
        if (GetComponent<FoliageDistanceCull>() == null)
            gameObject.AddComponent<FoliageDistanceCull>();
        if (GetComponent<OpenWorld>() == null)
            gameObject.AddComponent<OpenWorld>();
        if (GetComponent<AnimalAiScheduler>() == null)
            gameObject.AddComponent<AnimalAiScheduler>();
        if (GetComponent<TrackPool>() == null)
            gameObject.AddComponent<TrackPool>();
        if (GetComponent<WoundPool>() == null)
            gameObject.AddComponent<WoundPool>();
        if (GetComponent<CraftBook>() == null)
            gameObject.AddComponent<CraftBook>();
        if (GetComponent<BallisticsGun>() == null)
            gameObject.AddComponent<BallisticsGun>();
        if (GetComponent<GunAdvisor>() == null)
            gameObject.AddComponent<GunAdvisor>();

        slots[0] = new Slot { kind = ItemKind.Shotgun, count = 1 };
        slots[1] = new Slot { kind = ItemKind.Rifle, count = 1 };
        slots[2] = new Slot { kind = ItemKind.Sniper, count = 1 };
        selectedHotbar = 0;

        int startMeat = meat;
        int startCooked = cookedMeat;
        int startWood = wood;
        meat = 0;
        cookedMeat = 0;
        wood = 0;
        if (startMeat > 0)
            AddItem(ItemKind.RawMeat, startMeat);
        if (startCooked > 0)
            AddItem(ItemKind.CookedMeat, startCooked);
        if (startWood > 0)
            AddItem(ItemKind.Wood, startWood);
        RecalculatePublicCounts();
    }

    void OnDisable()
    {
        if (dragging)
            ReturnDraggedToSource();
        inventoryOpen = false;
    }

    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
        {
            if (dragging)
                ReturnDraggedToSource();
            inventoryOpen = false;
            return;
        }

        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (inventoryOpen && dragging)
                ReturnDraggedToSource();
            inventoryOpen = !inventoryOpen;
        }

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (!inventoryOpen)
                EatSomething();
        }

        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame
            && !inventoryOpen && !BirdChallenge.Active)
            PlaceOneBait();

        TryHotbarKeys();

        if (meat <= 0)
        {
            spoilTimer = meatSpoilSeconds;
            return;
        }

        spoilTimer -= Time.deltaTime;
        if (spoilTimer <= 0f)
        {
            int spoiled = meat;
            RemoveItem(ItemKind.RawMeat, spoiled);
            AddItem(ItemKind.SpoiledMeat, spoiled);
            spoilTimer = meatSpoilSeconds;
            ShowNotice("Meat spoiled. Press X to place it as bait.");
        }
    }

    public void AddMeat(int amount)
    {
        int added = AddItem(ItemKind.RawMeat, amount);
        if (added > 0)
            spoilTimer = meatSpoilSeconds;
    }

    public void AddWood(int amount)
    {
        AddItem(ItemKind.Wood, amount);
    }

    public void AddCookedMeat(int amount)
    {
        AddItem(ItemKind.CookedMeat, amount);
    }

    public void AddSpoiledMeat(int amount)
    {
        AddItem(ItemKind.SpoiledMeat, amount);
    }

    public bool RemoveSpoiledMeat(int amount)
    {
        return RemoveItem(ItemKind.SpoiledMeat, amount) == amount;
    }

    public bool UseWood(int amount)
    {
        if (amount <= 0)
            return true;
        if (wood < amount)
            return false;
        return RemoveItem(ItemKind.Wood, amount) == amount;
    }

    public bool CookOneMeat()
    {
        if (meat <= 0 || GetSpaceFor(ItemKind.CookedMeat) <= 0)
            return false;

        if (RemoveItem(ItemKind.RawMeat, 1) != 1)
            return false;

        AddItem(ItemKind.CookedMeat, 1);
        spoilTimer = meat > 0 ? meatSpoilSeconds : spoilTimer;
        return true;
    }

    public bool EatCookedMeat()
    {
        if (cookedMeat <= 0)
            return false;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return false;

        if (RemoveItem(ItemKind.CookedMeat, 1) != 1)
            return false;

        if (SurvivalStats.Current != null)
            SurvivalStats.Current.EatCooked();
        else if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(10f);
        return true;
    }

    // Q: eat cooked meat; with none, a second Q within a few seconds eats raw meat (risky).
    void EatSomething()
    {
        if (SurvivalStats.Current != null && SurvivalStats.Current.Hunger01 > 0.97f)
        {
            ShowNotice("You're full.");
            return;
        }

        if (EatCookedMeat())
        {
            ShowNotice("Ate cooked meat.");
            rawConfirmUntil = 0f;
            return;
        }

        if (meat <= 0)
        {
            ShowNotice(spoiledMeat > 0 ? "Spoiled meat isn't edible. Use it as bait (X)." : "Nothing to eat. Hunt, then cook meat on a fire.");
            return;
        }

        if (Time.time > rawConfirmUntil)
        {
            rawConfirmUntil = Time.time + 3f;
            ShowNotice("No cooked meat. Press Q again to eat it raw (risky).");
            return;
        }

        rawConfirmUntil = 0f;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;
        if (RemoveItem(ItemKind.RawMeat, 1) != 1)
            return;

        bool sick = SurvivalStats.Current != null && SurvivalStats.Current.EatRaw();
        ShowNotice(sick ? "That raw meat made you sick. Food poisoning!" : "Ate raw meat. You got lucky this time.");
    }

    public bool RemoveOneSellableMeat()
    {
        if (RemoveItem(ItemKind.CookedMeat, 1) == 1)
            return true;

        return RemoveItem(ItemKind.RawMeat, 1) == 1;
    }

    int AddItem(ItemKind kind, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0 || kind == ItemKind.None)
            return 0;

        int cap = IsWeapon(kind) ? 1 : maxStackSize;
        if (IsWeapon(kind))
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].kind == kind && slots[i].count > 0)
                    return 0;
            }
        }

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].kind != kind || slots[i].count >= cap)
                continue;

            int add = Mathf.Min(cap - slots[i].count, remaining);
            slots[i].count += add;
            remaining -= add;
        }

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].count > 0)
                continue;

            int add = Mathf.Min(cap, remaining);
            slots[i].kind = kind;
            slots[i].count = add;
            remaining -= add;
        }

        RecalculatePublicCounts();
        return amount - remaining;
    }

    int RemoveItem(ItemKind kind, int amount)
    {
        int need = Mathf.Max(0, amount);
        if (need <= 0 || kind == ItemKind.None)
            return 0;

        for (int i = 0; i < slots.Length && need > 0; i++)
        {
            if (slots[i].kind != kind || slots[i].count <= 0)
                continue;

            int take = Mathf.Min(slots[i].count, need);
            slots[i].count -= take;
            need -= take;
            if (slots[i].count <= 0)
            {
                slots[i].kind = ItemKind.None;
                slots[i].count = 0;
            }
        }

        RecalculatePublicCounts();
        return amount - need;
    }

    int GetSpaceFor(ItemKind kind)
    {
        int total = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0)
                total += maxStackSize;
            else if (slots[i].kind == kind)
                total += maxStackSize - slots[i].count;
        }
        return total;
    }

    void RecalculatePublicCounts()
    {
        meat = 0;
        cookedMeat = 0;
        spoiledMeat = 0;
        wood = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0)
                continue;

            switch (slots[i].kind)
            {
                case ItemKind.RawMeat:
                    meat += slots[i].count;
                    break;
                case ItemKind.CookedMeat:
                    cookedMeat += slots[i].count;
                    break;
                case ItemKind.SpoiledMeat:
                    spoiledMeat += slots[i].count;
                    break;
                case ItemKind.Wood:
                    wood += slots[i].count;
                    break;
            }
        }
    }

    public void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Time.time + 2f;
    }

    void OnGUI()
    {
        if (inventoryOpen)
        {
            DrawInventoryWindow();
            HandleInventoryDrag(Event.current);
            DrawDraggedSlot(Event.current.mousePosition);
        }
        else
            DrawHotbar();

        DrawGunTip();

        if (Time.time <= noticeUntil && !string.IsNullOrEmpty(notice))
            ScreenText.Centered(notice, Screen.height * 0.7f);
    }

    void DrawGunTip()
    {
        if (string.IsNullOrEmpty(GunAdvisor.Line))
            return;

        float y;
        if (inventoryOpen)
            y = inventoryRect.yMax + 22f;
        else
        {
            float size = Mathf.Clamp(Screen.height * 0.09f, 64f, 110f);
            y = Screen.height - size - 46f;
        }

        ScreenText.Centered(GunAdvisor.Line, y, 20);
    }

    public int HotbarKeyFor(HuntWeapon weapon)
    {
        if (weapon == HuntWeapon.None)
            return 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count > 0 && ToWeapon(slots[i].kind) == weapon)
                return i < GridColumns ? i + 1 : 0;
        }

        return 0;
    }

    void DrawInventoryWindow()
    {
        EnsureInventoryStyles();

        gapUi = Mathf.Max(12f, Screen.width * 0.006f);
        padUi = Mathf.Max(24f, Screen.width * 0.01f);
        headerUi = Mathf.Max(56f, Screen.height * 0.05f);
        footerUi = Mathf.Max(24f, Screen.height * 0.02f);

        float maxWidth = Screen.width * 0.92f;
        float maxHeight = Screen.height * 0.92f;
        float slotByWidth = (maxWidth - padUi * 2f - gapUi * (GridColumns - 1)) / GridColumns;
        float slotByHeight = (maxHeight - headerUi - footerUi - gapUi * (GridRows - 1)) / GridRows;
        slotSizeUi = Mathf.Max(140f, Mathf.Min(slotByWidth, slotByHeight));

        float gridWidth = GridColumns * slotSizeUi + (GridColumns - 1) * gapUi;
        float gridHeight = GridRows * slotSizeUi + (GridRows - 1) * gapUi;
        float width = gridWidth + padUi * 2f;
        float height = headerUi + gridHeight + footerUi;

        Color prevContent = GUI.contentColor;
        GUI.contentColor = Color.white;

        inventoryRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(inventoryRect, "Inventory", inventoryTitleStyle);

        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridColumns; col++)
            {
                int index = row * GridColumns + col;
                float x = inventoryRect.x + padUi + col * (slotSizeUi + gapUi);
                float y = inventoryRect.y + headerUi + row * (slotSizeUi + gapUi);
                int hotkey = row == 0 ? col + 1 : 0;
                DrawSlot(new Rect(x, y, slotSizeUi, slotSizeUi), slots[index], hotkey, index == selectedHotbar);
            }
        }

        GUI.contentColor = prevContent;
    }

    void DrawHotbar()
    {
        EnsureInventoryStyles();
        float size = Mathf.Clamp(Screen.height * 0.09f, 64f, 110f);
        float gap = 8f;
        float total = GridColumns * size + (GridColumns - 1) * gap;
        float x0 = (Screen.width - total) * 0.5f;
        float y = Screen.height - size - 18f;
        for (int i = 0; i < GridColumns; i++)
            DrawSlot(new Rect(x0 + i * (size + gap), y, size, size), slots[i], i + 1, i == selectedHotbar);
    }

    void DrawSlot(Rect rect, Slot slot, int hotkey, bool selected)
    {
        Color prev = GUI.color;
        if (selected)
        {
            GUI.color = new Color(0.95f, 0.78f, 0.2f, 1f);
            GUI.Box(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f), GUIContent.none);
        }

        GUI.color = slot.count > 0 ? ItemColor(slot.kind) : new Color(0.24f, 0.24f, 0.24f, 1f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = prev;

        if (hotkey > 0)
            GUI.Label(new Rect(rect.x + 8f, rect.y + rect.height - 28f, 36f, 24f), hotkey.ToString(), inventoryTextStyle);

        if (slot.count <= 0)
            return;

        GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f), ItemShortName(slot.kind), inventoryTextStyle);
        if (!IsWeapon(slot.kind))
            GUI.Label(new Rect(rect.x + 8f, rect.y + rect.height - 30f, rect.width - 16f, 24f), slot.count.ToString(), inventoryCountStyle);
    }

    void DrawDraggedSlot(Vector2 mouse)
    {
        if (!dragging || draggedSlot.count <= 0)
            return;

        float preview = Mathf.Clamp(slotSizeUi * 0.85f, 92f, 180f);
        Rect rect = new Rect(mouse.x - preview * 0.5f, mouse.y - preview * 0.5f, preview, preview);
        Slot previewSlot = draggedSlot;
        DrawSlot(rect, previewSlot, 0, false);
    }

    void HandleInventoryDrag(Event e)
    {
        if (e == null)
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            int index = SlotIndexAtPoint(e.mousePosition);
            if (index >= 0 && slots[index].count > 0)
            {
                dragging = true;
                draggedSlot = slots[index];
                dragFromIndex = index;
                slots[index] = default;
                RecalculatePublicCounts();
                e.Use();
            }
            return;
        }

        if (!dragging)
            return;

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            int target = SlotIndexAtPoint(e.mousePosition);
            if (target >= 0)
                PlaceDraggedIntoSlot(target);
            else if (!inventoryRect.Contains(e.mousePosition))
                DropDraggedStack();
            else
                ReturnDraggedToSource();

            e.Use();
        }
    }

    int SlotIndexAtPoint(Vector2 point)
    {
        if (!inventoryRect.Contains(point))
            return -1;

        float localX = point.x - (inventoryRect.x + padUi);
        float localY = point.y - (inventoryRect.y + headerUi);
        if (localX < 0f || localY < 0f)
            return -1;

        int col = Mathf.FloorToInt(localX / (slotSizeUi + gapUi));
        int row = Mathf.FloorToInt(localY / (slotSizeUi + gapUi));
        if (col < 0 || col >= GridColumns || row < 0 || row >= GridRows)
            return -1;

        float offsetX = localX - col * (slotSizeUi + gapUi);
        float offsetY = localY - row * (slotSizeUi + gapUi);
        if (offsetX > slotSizeUi || offsetY > slotSizeUi)
            return -1;

        return row * GridColumns + col;
    }

    void PlaceDraggedIntoSlot(int target)
    {
        if (!dragging || target < 0 || target >= slots.Length)
            return;

        if (target == dragFromIndex)
        {
            slots[dragFromIndex] = draggedSlot;
            ClearDrag();
            RecalculatePublicCounts();
            return;
        }

        Slot targetSlot = slots[target];
        if (targetSlot.count <= 0)
        {
            slots[target] = draggedSlot;
            ClearDrag();
            RecalculatePublicCounts();
            return;
        }

        int stackCap = IsWeapon(targetSlot.kind) ? 1 : maxStackSize;
        if (targetSlot.kind == draggedSlot.kind && targetSlot.count < stackCap)
        {
            int move = Mathf.Min(stackCap - targetSlot.count, draggedSlot.count);
            targetSlot.count += move;
            draggedSlot.count -= move;
            slots[target] = targetSlot;

            if (draggedSlot.count > 0)
                slots[dragFromIndex] = draggedSlot;

            ClearDrag();
            RecalculatePublicCounts();
            return;
        }

        slots[dragFromIndex] = targetSlot;
        slots[target] = draggedSlot;
        ClearDrag();
        RecalculatePublicCounts();
    }

    void DropDraggedStack()
    {
        if (IsWeapon(draggedSlot.kind))
        {
            ReturnDraggedToSource();
            ShowNotice("That stays in the bag.");
            return;
        }

        DropItemToWorld(draggedSlot.kind, draggedSlot.count);
        ShowNotice("Dropped " + draggedSlot.count + " " + ItemShortName(draggedSlot.kind));
        ClearDrag();
        RecalculatePublicCounts();
    }

    void ReturnDraggedToSource()
    {
        slots[dragFromIndex] = draggedSlot;
        ClearDrag();
        RecalculatePublicCounts();
    }

    void ClearDrag()
    {
        dragging = false;
        draggedSlot = default;
        dragFromIndex = -1;
        ApplySelectedWeapon(false);
    }

    string ItemShortName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat:
                return "Raw";
            case ItemKind.CookedMeat:
                return "Cooked";
            case ItemKind.SpoiledMeat:
                return "Spoiled";
            case ItemKind.Wood:
                return "Wood";
            case ItemKind.Shotgun:
                return "Shotgun";
            case ItemKind.Rifle:
                return "Rifle";
            case ItemKind.Sniper:
                return "Sniper";
            default:
                return "";
        }
    }

    Color ItemColor(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat:
                return new Color(0.53f, 0.18f, 0.16f, 1f);
            case ItemKind.CookedMeat:
                return new Color(0.55f, 0.38f, 0.16f, 1f);
            case ItemKind.SpoiledMeat:
                return new Color(0.32f, 0.34f, 0.14f, 1f);
            case ItemKind.Wood:
                return new Color(0.34f, 0.25f, 0.14f, 1f);
            case ItemKind.Shotgun:
                return new Color(0.28f, 0.30f, 0.32f, 1f);
            case ItemKind.Rifle:
                return new Color(0.36f, 0.24f, 0.14f, 1f);
            case ItemKind.Sniper:
                return new Color(0.12f, 0.16f, 0.20f, 1f);
            default:
                return new Color(0.24f, 0.24f, 0.24f, 1f);
        }
    }

    public void SelectHotbar(int index, bool announce)
    {
        if (index < -1 || index >= GridColumns)
            return;

        selectedHotbar = index;
        ApplySelectedWeapon(announce);
    }

    public void ApplySelectedWeapon(bool announce)
    {
        HuntWeapon weapon = HuntWeapon.None;
        string line = "Empty hands.";
        if (selectedHotbar >= 0 && selectedHotbar < slots.Length && slots[selectedHotbar].count > 0)
        {
            ItemKind kind = slots[selectedHotbar].kind;
            weapon = ToWeapon(kind);
            line = weapon == HuntWeapon.None ? ItemShortName(kind) : WeaponNotice(weapon);
        }

        if (PlayerShotgun.Instance != null)
            PlayerShotgun.Instance.Equip(weapon);

        if (announce)
            ShowNotice(selectedHotbar < 0 ? line : (selectedHotbar + 1) + "  " + line);
    }

    void TryHotbarKeys()
    {
        if (Keyboard.current == null || dragging || BirdChallenge.Active || WoodBuilder.IsBuilding || NearTrader())
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SelectHotbar(0, true);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SelectHotbar(1, true);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SelectHotbar(2, true);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            SelectHotbar(3, true);
        else if (Keyboard.current.digit5Key.wasPressedThisFrame)
            SelectHotbar(4, true);
    }

    static bool NearTrader()
    {
        return Trader.Instance != null
            && Vector3.Distance(Trader.Instance.transform.position, PlayerView.Position()) <= Trader.Instance.tradeRange;
    }

    static bool IsWeapon(ItemKind kind)
    {
        return kind == ItemKind.Shotgun || kind == ItemKind.Rifle || kind == ItemKind.Sniper;
    }

    static HuntWeapon ToWeapon(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.Shotgun:
                return HuntWeapon.Shotgun;
            case ItemKind.Rifle:
                return HuntWeapon.Rifle;
            case ItemKind.Sniper:
                return HuntWeapon.Sniper;
            default:
                return HuntWeapon.None;
        }
    }

    static string WeaponNotice(HuntWeapon weapon)
    {
        switch (weapon)
        {
            case HuntWeapon.Shotgun:
                return "Double barrel. Two shots, then it breaks open.";
            case HuntWeapon.Rifle:
                return "Hunting rifle. Five rounds.";
            case HuntWeapon.Sniper:
                return "Sniper. Hold right mouse for the scope.";
            default:
                return "Empty hands.";
        }
    }

    void EnsureInventoryStyles()
    {
        if (inventoryTextStyle != null)
            return;

        inventoryTextStyle = new GUIStyle(GUI.skin.label);
        inventoryTextStyle.fontSize = 20;
        inventoryTextStyle.normal.textColor = Color.white;
        inventoryTextStyle.hover.textColor = Color.white;
        inventoryTextStyle.active.textColor = Color.white;
        inventoryTextStyle.focused.textColor = Color.white;
        inventoryTextStyle.onNormal.textColor = Color.white;
        inventoryTextStyle.onHover.textColor = Color.white;
        inventoryTextStyle.onActive.textColor = Color.white;
        inventoryTextStyle.onFocused.textColor = Color.white;
        inventoryTextStyle.alignment = TextAnchor.UpperLeft;

        inventoryCountStyle = new GUIStyle(inventoryTextStyle);
        inventoryCountStyle.fontSize = 24;
        inventoryCountStyle.fontStyle = FontStyle.Bold;
        inventoryCountStyle.alignment = TextAnchor.UpperRight;

        inventoryTitleStyle = new GUIStyle(GUI.skin.box);
        inventoryTitleStyle.fontSize = 26;
        inventoryTitleStyle.alignment = TextAnchor.UpperCenter;
        inventoryTitleStyle.normal.textColor = Color.white;
        inventoryTitleStyle.hover.textColor = Color.white;
        inventoryTitleStyle.active.textColor = Color.white;
        inventoryTitleStyle.focused.textColor = Color.white;
        inventoryTitleStyle.onNormal.textColor = Color.white;
        inventoryTitleStyle.onHover.textColor = Color.white;
        inventoryTitleStyle.onActive.textColor = Color.white;
        inventoryTitleStyle.onFocused.textColor = Color.white;
    }

    void DropItemToWorld(ItemKind kind, int amount)
    {
        if (amount <= 0 || kind == ItemKind.None)
            return;

        Transform body = PlayerView.Body();
        Vector3 origin = body != null
            ? body.position + body.forward * 1.5f + Vector3.up * 0.4f
            : PlayerView.AimOrigin() + PlayerView.AimDirection() * 1.5f;
        origin = WorldGround.Sit(origin);

        if (kind == ItemKind.RawMeat)
            MeatPickup.Spawn(origin, amount);
        else if (kind == ItemKind.SpoiledMeat)
            MeatBait.Place(origin, amount);
        else
            InventoryWorldDrop.Spawn(origin, kind.ToString(), amount);
    }

    public float SpoilSecondsLeft()
    {
        return meat > 0 ? spoilTimer : 0f;
    }

    void PlaceOneBait()
    {
        if (spoiledMeat <= 0)
        {
            ShowNotice("No spoiled meat. Let raw meat spoil, then press X.");
            return;
        }

        if (!RemoveSpoiledMeat(1))
            return;

        Transform body = PlayerView.Body();
        Vector3 origin = body != null
            ? body.position + body.forward * 2.2f + Vector3.up * 0.4f
            : PlayerView.AimOrigin() + PlayerView.AimDirection() * 2.2f;
        origin = WorldGround.Sit(origin);
        MeatBait.Place(origin, 1);

        bool nightAway = DayNightCycle.Instance != null
            && DayNightCycle.Instance.IsNight
            && !Campfire.Covers(origin);
        ShowNotice(nightAway
            ? "Bait placed away from the fire. A large animal may come."
            : "Bait placed. Nearby prey will come for a short time.");
    }
}

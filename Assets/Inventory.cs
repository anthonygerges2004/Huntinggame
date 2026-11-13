using HuntingGame.Animals;
using HuntingGame.Ballistics;
using HuntingGame.Crafting;
using HuntingGame.Survival;
using HuntingGame.Tracking;
using HuntingGame.Wounds;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Inventory : MonoBehaviour
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
        Sniper,
        Axe,
        Carcass,
        Pelt,
        Pistol,
        SMG
    }

    struct Slot
    {
        public ItemKind kind;
        public int count;
        public HuntGoods goods; // carcasses and pelts: the one animal this slot holds
    }

    public static Inventory Instance;
    // True while the bag or the pause menu is open; gameplay input everywhere checks this.
    public static bool IsOpen => GamePause.Active || (Instance != null && Instance.inventoryOpen);

    /// <summary>The hand axe is out only while its hotbar slot is selected.</summary>
    public static bool AxeInHand
    {
        get
        {
            if (Instance == null)
                return true;
            int i = Instance.selectedHotbar;
            return i >= 0 && i < Instance.slots.Length && Instance.slots[i].count > 0 && Instance.slots[i].kind == ItemKind.Axe;
        }
    }

    public int meat;
    public int cookedMeat;
    public int spoiledMeat;
    public int wood;
    public bool hasPickaxe = true;
    public bool ownsLighter;
    public bool rentedLighter;
    public float lighterSeconds;
    bool toldLighterExpired;

    [Tooltip("Starter matches: each lights one fire, so the first nights are survivable before a lighter is bought.")]
    public int matches = 3;
    public bool LighterLit => ownsLighter || (rentedLighter && lighterSeconds > 0f);
    public bool LighterWorks => LighterLit || matches > 0;

    /// <summary>Uses a match if there is no working lighter. Returns a line to show, or null.</summary>
    public string StrikeFire()
    {
        if (LighterLit || matches <= 0)
            return null;
        matches--;
        return matches > 0
            ? "Struck a match. " + matches + (matches == 1 ? " match" : " matches") + " left."
            : "That was your last match. Buy or rent a lighter at the castle gun shop.";
    }
    public bool LighterDueBack => rentedLighter && !ownsLighter;

    public const float LighterRentSeconds = 180f;
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

    /// <summary>The bag itself, not counting the pause menu.</summary>
    public bool BagOpen => inventoryOpen;

    public void CloseBag()
    {
        if (!inventoryOpen)
            return;
        if (dragging)
            ReturnDraggedToSource();
        inventoryOpen = false;
    }
    bool dragging;
    Slot draggedSlot;
    int dragFromIndex = -1;

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
        if (GetComponent<AmbientGame>() == null)
            gameObject.AddComponent<AmbientGame>();
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

        // You start with a pistol and the axe; every other gun is bought at the castle gun shop.
        slots[0] = new Slot { kind = ItemKind.Pistol, count = 1 };
        slots[1] = new Slot { kind = ItemKind.Axe, count = 1 };
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
        // Gun pictures are photographed once from the real models. Rendering has to happen here, not in OnGUI.
        WeaponIcons.Warm();
        UpdateView();
        if (!GamePause.Active)
            TickLighter();

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
        {
            if (dragging)
                ReturnDraggedToSource();
            inventoryOpen = false;
            return;
        }

        if (GamePause.Active)
            return;

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
        TickGoods();
        if (!inventoryOpen)
            AnimalCarcass.Tick(this);

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

    // Return the accepted count so world pickups keep anything that does not fit.
    public int CollectItem(string itemId, int amount)
    {
        if (amount <= 0 || !System.Enum.TryParse(itemId, out ItemKind kind) ||
            !System.Enum.IsDefined(typeof(ItemKind), kind) || kind == ItemKind.None)
            return 0;
        int added = AddItem(kind, amount);
        if (kind == ItemKind.RawMeat && added > 0)
            spoilTimer = meatSpoilSeconds;
        return added;
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
        return amount > 0 && spoiledMeat >= amount && RemoveItem(ItemKind.SpoiledMeat, amount) == amount;
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

    public bool TrySellMeatForWood(int price)
    {
        if (price <= 0)
            return false;
        Slot[] before = (Slot[])slots.Clone();
        bool removed = RemoveOneSellableMeat();
        if (!removed && spoiledMeat >= 2)
            removed = RemoveSpoiledMeat(2);
        if (removed && AddItem(ItemKind.Wood, price) == price)
            return true;
        slots = before;
        RecalculatePublicCounts();
        return false;
    }

    int AddItem(ItemKind kind, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0 || kind == ItemKind.None)
            return 0;

        int cap = IsGear(kind) ? 1 : maxStackSize;
        if (IsGear(kind))
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
        if (GameHUD.Instance != null && GameHUD.Instance.Toast(text))
            return;

        notice = text;
        noticeUntil = Time.time + 2f;
    }

    void OnGUI()
    {
        if (GamePause.Active || (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead))
            return;

        // The backpack and hotbar are drawn by Inventory.View.cs on their own canvas.
        // With the HUD up, the gun tip lives in its weapon panel and notices in its message feed.
        if (GameHUD.Instance == null)
            DrawGunTip();

        if (Time.time <= noticeUntil && !string.IsNullOrEmpty(notice))
            ScreenText.Centered(notice, Screen.height * 0.7f);
    }

    void DrawGunTip()
    {
        if (string.IsNullOrEmpty(GunAdvisor.Line))
            return;

        float y = inventoryOpen ? Screen.height * 0.92f : Screen.height - 130f;

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

        int stackCap = IsSingle(targetSlot.kind) ? 1 : maxStackSize;
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
        if (IsGoods(draggedSlot.kind))
        {
            ReturnDraggedToSource();
            ShowNotice("Sell it to the tanner, or use Discard in its details.");
            return;
        }

        if (IsGear(draggedSlot.kind))
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
            case ItemKind.Pistol:
                return "Pistol";
            case ItemKind.SMG:
                return "SMG";
            case ItemKind.Axe:
                return "Axe";
            case ItemKind.Carcass:
                return "Carcass";
            case ItemKind.Pelt:
                return "Pelt";
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
            case ItemKind.Pistol:
                return new Color(0.22f, 0.22f, 0.24f, 1f);
            case ItemKind.SMG:
                return new Color(0.18f, 0.20f, 0.18f, 1f);
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
            if (kind == ItemKind.Axe)
                line = AxeSwing.HasAxe ? "Hand axe. Click or E to chop, right-click to throw." : "Your axe is out there. Walk over it to pick it up.";
            else
                line = weapon == HuntWeapon.None ? ItemShortName(kind) : WeaponNotice(weapon);
        }

        if (PlayerShotgun.Instance != null)
            PlayerShotgun.Instance.Equip(weapon);

        if (announce)
            ShowNotice(selectedHotbar < 0 ? line : (selectedHotbar + 1) + "  " + line);
    }

    void TryHotbarKeys()
    {
        if (Keyboard.current == null || dragging || BirdChallenge.Active || WoodBuilder.IsBuilding || NearTrader() || CastleShop.BlockingHotbar())
            return;

        // With the bag open, 1-5 over a slot puts that item on the key instead of selecting it.
        if (inventoryOpen && hoverIndex >= 0)
        {
            int key = -1;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) key = 0;
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) key = 1;
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) key = 2;
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) key = 3;
            else if (Keyboard.current.digit5Key.wasPressedThisFrame) key = 4;
            if (key >= 0)
            {
                if (key != hoverIndex)
                {
                    Slot moved = slots[hoverIndex];
                    slots[hoverIndex] = slots[key];
                    slots[key] = moved;
                    RecalculatePublicCounts();
                    ApplySelectedWeapon(false);
                }
                detailIndex = key;
                return;
            }
        }

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

    public bool BuyLighter()
    {
        if (ownsLighter)
            return false;

        ownsLighter = true;
        rentedLighter = false;
        lighterSeconds = 0f;
        toldLighterExpired = false;
        return true;
    }

    public bool BeginLighterRental()
    {
        if (ownsLighter || rentedLighter)
            return false;

        rentedLighter = true;
        lighterSeconds = LighterRentSeconds;
        toldLighterExpired = false;
        return true;
    }

    public bool ReturnLighter()
    {
        if (!rentedLighter || ownsLighter)
            return false;

        rentedLighter = false;
        lighterSeconds = 0f;
        toldLighterExpired = false;
        return true;
    }

    void TickLighter()
    {
        if (ownsLighter || !rentedLighter || lighterSeconds <= 0f)
            return;

        lighterSeconds -= Time.deltaTime;
        if (lighterSeconds > 0f)
            return;

        lighterSeconds = 0f;
        if (toldLighterExpired)
            return;

        toldLighterExpired = true;
        ShowNotice("Lighter overdue! The gun shop owner will attack if you approach. Press 6 at his shop to return it.");
    }

    public bool OwnsWeapon(HuntWeapon weapon)
    {
        if (weapon == HuntWeapon.None)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count > 0 && ToWeapon(slots[i].kind) == weapon)
                return true;
        }

        return false;
    }

    public bool TryAddWeapon(HuntWeapon weapon)
    {
        if (weapon == HuntWeapon.None || OwnsWeapon(weapon))
            return false;

        ItemKind kind = ItemKind.None;
        if (weapon == HuntWeapon.Shotgun)
            kind = ItemKind.Shotgun;
        else if (weapon == HuntWeapon.Rifle)
            kind = ItemKind.Rifle;
        else if (weapon == HuntWeapon.Sniper)
            kind = ItemKind.Sniper;
        else if (weapon == HuntWeapon.Pistol)
            kind = ItemKind.Pistol;
        else if (weapon == HuntWeapon.SMG)
            kind = ItemKind.SMG;
        if (kind == ItemKind.None)
            return false;

        return AddItem(kind, 1) == 1;
    }

    public int GiveWood(int amount)
    {
        return AddItem(ItemKind.Wood, amount);
    }

    public int GiveRawMeat(int amount)
    {
        int added = AddItem(ItemKind.RawMeat, amount);
        if (added > 0)
            spoilTimer = meatSpoilSeconds;
        return added;
    }

    public int GiveCooked(int amount)
    {
        return AddItem(ItemKind.CookedMeat, amount);
    }

    public int GiveBait(int amount)
    {
        return AddItem(ItemKind.SpoiledMeat, amount);
    }

    public bool SpendRaw(int amount)
    {
        return amount > 0 && meat >= amount && RemoveItem(ItemKind.RawMeat, amount) == amount;
    }

    public bool SpendCooked(int amount)
    {
        return amount > 0 && cookedMeat >= amount && RemoveItem(ItemKind.CookedMeat, amount) == amount;
    }

    public int SpaceForWood()
    {
        return GetSpaceFor(ItemKind.Wood);
    }

    public int SpaceForRaw()
    {
        return GetSpaceFor(ItemKind.RawMeat);
    }

    public int SpaceForCooked()
    {
        return GetSpaceFor(ItemKind.CookedMeat);
    }

    public int SpaceForBait()
    {
        return GetSpaceFor(ItemKind.SpoiledMeat);
    }

    static bool NearTrader()
    {
        return Trader.Instance != null
            && Vector3.Distance(Trader.Instance.transform.position, PlayerView.Position()) <= Trader.Instance.tradeRange;
    }

    /// <summary>One-of-a-kind items that never stack or drop: the guns and the axe.</summary>
    static bool IsGear(ItemKind kind)
    {
        return IsWeapon(kind) || kind == ItemKind.Axe;
    }

    static bool IsWeapon(ItemKind kind)
    {
        return kind == ItemKind.Shotgun || kind == ItemKind.Rifle || kind == ItemKind.Sniper || kind == ItemKind.Pistol || kind == ItemKind.SMG;
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
            case ItemKind.Pistol:
                return HuntWeapon.Pistol;
            case ItemKind.SMG:
                return HuntWeapon.SMG;
            default:
                return HuntWeapon.None;
        }
    }

    static string WeaponNotice(HuntWeapon weapon)
    {
        switch (weapon)
        {
            case HuntWeapon.Pistol:
                return "Pistol. Twelve rounds, quick to draw.";
            case HuntWeapon.Shotgun:
                return "Combat shotgun. Six shells of buckshot.";
            case HuntWeapon.SMG:
                return "SMG. Hold the trigger for full auto.";
            case HuntWeapon.Rifle:
                return "Assault rifle. Thirty rounds, full auto.";
            case HuntWeapon.Sniper:
                return "Sniper rifle. Hold right mouse for the scope.";
            default:
                return "Empty hands.";
        }
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

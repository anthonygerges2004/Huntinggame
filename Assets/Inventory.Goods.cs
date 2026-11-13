using UnityEngine;

// Hunting goods in the bag: carcasses (heavy, spoil over time) and pelts (light, keep),
// plus the coin purse they turn into.
//
//   world      tap E on a downed animal = TakeCarcass, hold E = FieldSkin (rough cut)
//   tanner     SkinAllCarcasses (clean cut, full meat), SellAllPelts (coins)
//   bag        a carcass can also be skinned by hand from its details panel
public partial class Inventory
{
    public const float MaxCarryKg = 350f;
    public const float HeavyLoadKg = 120f;

    [Tooltip("Coins from selling pelts. Spent at the castle market stalls.")]
    public int coins = 20;

    /// <summary>Total weight of carcasses in the bag.</summary>
    public float CarryKg
    {
        get
        {
            float kg = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].count > 0 && slots[i].kind == ItemKind.Carcass && slots[i].goods != null)
                    kg += slots[i].goods.weightKg;
            }
            return kg;
        }
    }

    /// <summary>Movement speed scale from hauling carcasses: down to half speed at the carry limit.</summary>
    public static float CarrySpeedMultiplier =>
        Instance == null ? 1f : 1f - 0.5f * Mathf.Clamp01(Instance.CarryKg / MaxCarryKg);

    public static bool TooHeavyToSprint => Instance != null && Instance.CarryKg > HeavyLoadKg;

    public static int Coins => Instance != null ? Instance.coins : 0;

    // ---------------------------------------------------------------- coins

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;
        coins += amount;
        HuntProgress.CoinsEarned += amount;
    }

    public bool SpendCoins(int amount)
    {
        if (amount <= 0)
            return true;
        if (coins < amount)
            return false;
        coins -= amount;
        return true;
    }

    /// <summary>Gives coins back after a purchase that could not complete. Not counted as earnings.</summary>
    public void RefundCoins(int amount)
    {
        if (amount > 0)
            coins += amount;
    }

    // ---------------------------------------------------------------- slots for goods

    static bool IsGoods(ItemKind kind) => kind == ItemKind.Carcass || kind == ItemKind.Pelt;

    // Edit-mode tests and tools cannot use Destroy; play mode should not use DestroyImmediate.
    static void RemoveObject(GameObject go)
    {
        if (go == null)
            return;
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    /// <summary>Never stacks: gear, carcasses and pelts each take a slot of their own.</summary>
    static bool IsSingle(ItemKind kind) => IsGear(kind) || IsGoods(kind);

    int FreeSlotForGoods()
    {
        // Storage first, so the hotbar stays for weapons and food.
        for (int i = GridColumns; i < slots.Length; i++)
            if (slots[i].count <= 0)
                return i;
        for (int i = 0; i < GridColumns; i++)
            if (slots[i].count <= 0)
                return i;
        return -1;
    }

    bool PutGoods(ItemKind kind, HuntGoods goods)
    {
        int index = FreeSlotForGoods();
        if (index < 0)
            return false;
        slots[index] = new Slot { kind = kind, count = 1, goods = goods };
        RecalculatePublicCounts();
        return true;
    }

    public int CarcassCount => CountOf(ItemKind.Carcass);
    public int PeltCount => CountOf(ItemKind.Pelt);

    int CountOf(ItemKind kind)
    {
        int n = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].count > 0 && slots[i].kind == kind)
                n++;
        return n;
    }

    public int TotalPeltValue()
    {
        int total = 0;
        float now = Time.time;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].count > 0 && slots[i].kind == ItemKind.Pelt && slots[i].goods != null)
                total += slots[i].goods.PeltValue(now);
        return total;
    }

    /// <summary>Pelts in the bag, best first, for the tanner's appraisal list.</summary>
    public System.Collections.Generic.List<HuntGoods> PeltsByValue()
    {
        var list = new System.Collections.Generic.List<HuntGoods>();
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].count > 0 && slots[i].kind == ItemKind.Pelt && slots[i].goods != null)
                list.Add(slots[i].goods);
        float now = Time.time;
        list.Sort((a, b) => b.PeltValue(now).CompareTo(a.PeltValue(now)));
        return list;
    }

    // ---------------------------------------------------------------- in the field

    public void TakeCarcass(AnimalCarcass carcass)
    {
        if (carcass == null || carcass.goods == null)
            return;
        if (CarcassBackCarry.IsHandling || RideableHorse.IsRiding)
            return;

        HuntGoods g = carcass.goods;
        if (CarryKg + g.weightKg > MaxCarryKg)
        {
            ShowNotice("Too heavy to haul with what you carry. Hold E to skin it here instead.");
            return;
        }

        if (!PutGoods(ItemKind.Carcass, g))
        {
            ShowNotice("No room in the bag. Hold E to skin it here instead.");
            return;
        }

        RivalHunter.NotifyLoot(carcass);
        CarcassBackCarry.Attach(this, carcass, Mathf.Max(0, CarcassCount - 1));
        ApplySelectedWeapon(false);
        RemoveObject(carcass.gameObject);
        string load = CarryKg > HeavyLoadKg ? " Heavy load: no sprinting." : "";
        ShowNotice("Carrying the " + g.Title.ToLowerInvariant() + " on your back (" + Mathf.RoundToInt(g.weightKg)
                   + " kg). U drops it. The tanner in the castle market will skin it." + load);
    }

    public bool ContainsCarcass(HuntGoods goods)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].count > 0 && slots[i].kind == ItemKind.Carcass && ReferenceEquals(slots[i].goods, goods))
                return true;
        return false;
    }

    public bool ReleaseCarcass(HuntGoods goods)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].count > 0 && slots[i].kind == ItemKind.Carcass && ReferenceEquals(slots[i].goods, goods))
            {
                slots[i] = default;
                RecalculatePublicCounts();
                return true;
            }
        return false;
    }

    public void FieldSkin(AnimalCarcass carcass)
    {
        if (carcass == null || carcass.goods == null)
            return;

        if (FreeSlotForGoods() < 0)
        {
            ShowNotice("No room in the bag for the pelt.");
            return;
        }

        HuntGoods g = carcass.goods;
        Vector3 at = carcass.transform.position;
        SkinInto(g, true, at);
        RivalHunter.NotifyLoot(carcass);
        RemoveObject(carcass.gameObject);
    }

    /// <summary>Skins a carcass that is already in the bag, by hand.</summary>
    public void SkinSlotByHand(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index].kind != ItemKind.Carcass || slots[index].goods == null)
            return;

        HuntGoods g = slots[index].goods;
        slots[index] = default;
        RecalculatePublicCounts();
        SkinInto(g, true, PlayerView.Position());
    }

    /// <summary>Pelt into the bag, meat into the bag (overflow is left on the ground). Returns the pelt's value.</summary>
    int SkinInto(HuntGoods g, bool byHand, Vector3 dropPoint)
    {
        int meat = g.Skin(Time.time, byHand, out bool spoiled);
        PutGoods(ItemKind.Pelt, g);

        int kept = spoiled ? GiveBait(meat) : GiveRawMeat(meat);
        int left = meat - kept;
        if (left > 0)
        {
            if (spoiled)
                MeatBait.Place(WorldGround.Sit(dropPoint), left);
            else
                MeatPickup.Spawn(dropPoint, left);
        }

        int value = g.PeltValue(Time.time);
        if (byHand)
            ShowNotice("Skinned it yourself: " + g.Title.ToLowerInvariant() + " pelt (" + g.ConditionLabel.ToLowerInvariant()
                       + ", ~" + value + " coins) and " + meat + (spoiled ? " spoiled meat." : " meat."));
        return value;
    }

    // ---------------------------------------------------------------- at the tanner

    /// <summary>The tanner skins every carcass properly. Returns how many he skinned.</summary>
    public int SkinAllCarcasses(out int meatTotal)
    {
        meatTotal = 0;
        int skinned = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0 || slots[i].kind != ItemKind.Carcass || slots[i].goods == null)
                continue;

            HuntGoods g = slots[i].goods;
            // The pelt goes straight back into the carcass's own slot.
            int meat = g.Skin(Time.time, false, out bool spoiled);
            slots[i] = new Slot { kind = ItemKind.Pelt, count = 1, goods = g };
            RecalculatePublicCounts();
            int kept = spoiled ? GiveBait(meat) : GiveRawMeat(meat);
            if (meat - kept > 0)
            {
                if (spoiled)
                    MeatBait.Place(WorldGround.Sit(PlayerView.Position()), meat - kept);
                else
                    MeatPickup.Spawn(PlayerView.Position(), meat - kept);
            }

            meatTotal += meat;
            skinned++;
        }

        return skinned;
    }

    /// <summary>Sells every pelt in the bag. Returns the coins earned.</summary>
    public int SellAllPelts(out int sold)
    {
        sold = 0;
        int earned = 0;
        float now = Time.time;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0 || slots[i].kind != ItemKind.Pelt || slots[i].goods == null)
                continue;

            earned += slots[i].goods.PeltValue(now);
            slots[i] = default;
            sold++;
        }

        RecalculatePublicCounts();
        AddCoins(earned);
        HuntProgress.PeltsSold += sold;
        if (selectedHotbar >= 0)
            ApplySelectedWeapon(false);
        return earned;
    }

    public void DiscardSlot(int index)
    {
        if (index < 0 || index >= slots.Length || !IsGoods(slots[index].kind) || slots[index].goods == null)
            return;

        string what = slots[index].goods.Title.ToLowerInvariant() + (slots[index].kind == ItemKind.Carcass ? " carcass" : " pelt");
        slots[index] = default;
        RecalculatePublicCounts();
        ShowNotice("Left the " + what + " behind.");
    }

    void TickGoods()
    {
        float now = Time.time;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0 || slots[i].kind != ItemKind.Carcass || slots[i].goods == null)
                continue;
            HuntGoods g = slots[i].goods;
            if (!g.spoilAnnounced && g.Spoiled(now))
            {
                g.spoilAnnounced = true;
                ShowNotice("The " + g.Title.ToLowerInvariant() + " carcass has spoiled. The pelt is worth less and the meat is only bait.");
            }
        }
    }
}

using System.Collections.Generic;
using HuntingGame.Survival;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Five stalls in the castle courtyard: guns, clothes, wood, meat, and a butcher who cooks.
/// </summary>
public class CastleMarket : MonoBehaviour
{
    public static bool TryGetCenter(out Vector3 position)
    {
        position = center;
        return placed;
    }

    static Vector3 center;
    static bool placed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetMarket() { placed = false; center = Vector3.zero; }

    public static void Place()
    {
        if (placed)
            return;

        Transform castle = FindCastle();
        if (castle == null)
            return;

        if (castle.Find("CastleShops") != null)
        {
            MarkPlaced(castle.Find("CastleShops"));
            return;
        }

        float step;
        float floorY;
        bool measured = TryMeasure(castle, out step, out floorY);
        if (!measured)
            return;

        GameObject root = new GameObject("CastleShops");
        root.transform.SetParent(castle, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        float gap = Mathf.Clamp(14f * step / 4f, 2.6f, 3.6f);
        float range = 2.8f;
        float z = Mathf.Clamp(-3f * step, -10f, -2f);
        float y = floorY;

        Add(root.transform, CastleShop.Kind.Guns, -2f * gap, y, z, range);
        Add(root.transform, CastleShop.Kind.Clothes, -gap, y, z, range);
        Add(root.transform, CastleShop.Kind.Wood, 0f, y, z, range);
        Add(root.transform, CastleShop.Kind.Meat, gap, y, z, range);
        Add(root.transform, CastleShop.Kind.Butchery, 2f * gap, y, z, range);

        MarkPlaced(root.transform);
    }

    static void MarkPlaced(Transform shops)
    {
        placed = true;
        center = shops.position;
    }

    static void Add(Transform root, CastleShop.Kind kind, float x, float y, float z, float range)
    {
        GameObject stall = new GameObject(kind.ToString());
        stall.transform.SetParent(root, false);
        stall.transform.localPosition = new Vector3(x, y, z);
        stall.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Vector3 parentScale = root.lossyScale;
        stall.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
        stall.transform.position = WorldGround.Snap(stall.transform.position);
        CastleShop shop = stall.AddComponent<CastleShop>();
        shop.Setup(kind, range);
    }

    static Transform FindCastle()
    {
        CastleBuilder[] builders = Object.FindObjectsByType<CastleBuilder>(FindObjectsInactive.Exclude);
        for (int i = 0; i < builders.Length; i++)
        {
            if (builders[i] != null && builders[i].transform.childCount > 0)
                return builders[i].transform;
        }

        string[] names = { "Stronghold", "newCastle", "Castle", "Castleie" };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject go = GameObject.Find(names[i]);
            if (go != null && go.transform.childCount > 0)
                return go.transform;
        }

        return null;
    }

    static bool TryMeasure(Transform castle, out float step, out float floorY)
    {
        step = 0f;
        floorY = 0f;
        float maxX = 0f;
        bool foundPiece = false;
        for (int i = 0; i < castle.childCount; i++)
        {
            Transform child = castle.GetChild(i);
            if (child == null)
                continue;
            string name = child.name;
            if (!name.StartsWith("Tower") && !name.StartsWith("Wall") && !name.StartsWith("Gate"))
                continue;
            foundPiece = true;
            maxX = Mathf.Max(maxX, Mathf.Abs(child.localPosition.x));
        }

        if (!foundPiece || maxX < 1f)
            return false;

        step = maxX / 11f;
        return step > 0.05f && step < 20f;
    }
}

public class CastleShop : MonoBehaviour
{
    public enum Kind
    {
        Guns,
        Clothes,
        Wood,
        Meat,
        Butchery
    }

    static readonly List<CastleShop> Shops = new List<CastleShop>();

    public Kind ShopKind { get; private set; }
    float range = 2.4f;
    bool numberKeys;
    float discount;
    float bargainUntil;
    float nextBargain;
    int lastPaid;
    Villager lighterKeeper;
    float nextKeeperStrike;
    float punchUntil;

    void LateUpdate()
    {
        if (lighterKeeper == null || ShopKind != Kind.Guns) return;
        Inventory inv = Inventory.Instance;
        PlayerHealth health = PlayerHealth.Instance;
        bool overdue = inv != null && inv.LighterDueBack && inv.lighterSeconds <= 0f;
        if (!overdue || health == null || health.isDead || Time.timeScale <= 0f) return;
        Vector3 toward = health.transform.position - lighterKeeper.transform.position;
        toward.y = 0f;
        if (toward.sqrMagnitude > 9f || toward.sqrMagnitude < 0.001f) return;
        lighterKeeper.transform.rotation = Quaternion.LookRotation(toward);
        if (Time.time >= nextKeeperStrike)
        {
            // Only strike a player in reach, with an unobstructed line across the counter.
            Vector3 from = lighterKeeper.transform.position + Vector3.up * 1.4f;
            Vector3 to = health.transform.position + Vector3.up * 1.2f;
            bool clear = !Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform == health.transform || hit.transform.IsChildOf(health.transform);
            if (clear)
            {
                nextKeeperStrike = Time.time + 1.5f;
                punchUntil = Time.time + 0.4f;
                health.TakeDamage(5f, "Overdue lighter rental");
                Notice("Return my lighter! Press 6 at the gun shop, or buy it with 5.");
            }
        }
        if (Time.time < punchUntil && lighterKeeper.armR != null)
            lighterKeeper.armR.localRotation = Quaternion.Euler(-85f, 0f, 0f);
    }

    int Price(int cost) => Mathf.Max(1, Mathf.CeilToInt(cost * (1f - (Time.time < bargainUntil ? discount : 0f))));

    void Bargain()
    {
        if (Time.time < nextBargain) { Notice("That is my final offer for now."); return; }
        nextBargain = Time.time + 60f;
        discount = VillageReputation.Trust < 0 ? 0f : VillageReputation.Trust >= 30 ? 0.25f : 0.15f;
        bargainUntil = Time.time + 60f;
        Notice(discount > 0f ? "Agreed: " + Mathf.RoundToInt(discount * 100f) + "% off purchases for 60 seconds. Prices shown include the deal; rounded up." : "Earn the village's trust before asking for a discount.");
    }

    public static bool BlockingHotbar()
    {
        if (FurniturePlacement.Active)
            return true;
        if (Inventory.IsOpen)
            return false;

        CastleShop shop = Closest();
        return shop != null && shop.numberKeys;
    }

    public static bool AtButchery()
    {
        CastleShop shop = Closest();
        return shop != null && shop.ShopKind == Kind.Butchery;
    }

    public void Setup(Kind kind, float useRange, bool buildStall = true)
    {
        ShopKind = kind;
        range = useRange;
        numberKeys = kind != Kind.Butchery;
        if (buildStall)
            BuildStall();
    }

    void OnEnable()
    {
        if (!Shops.Contains(this))
            Shops.Add(this);
    }

    void OnDisable()
    {
        Shops.Remove(this);
    }

    void Update()
    {
        if (!Ready() || Keyboard.current == null)
            return;

        if (Keyboard.current.nKey.wasPressedThisFrame && ShopKind != Kind.Wood && ShopKind != Kind.Butchery) { Bargain(); return; }
        if (ShopKind == Kind.Guns)
            TickGuns();
        else if (ShopKind == Kind.Clothes)
            TickClothes();
        else if (ShopKind == Kind.Wood)
            TickWood();
        else if (ShopKind == Kind.Meat)
            TickMeat();
    }

    void OnGUI()
    {
        if (!Ready())
        {
            if (ShopKind == Kind.Guns && Camera.main != null && !Inventory.IsOpen)
            {
                float distance = Vector3.Distance(PlayerView.Position(), transform.position);
                Vector3 label = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.8f);
                if (distance < 65f && label.z > 0f)
                    GUI.Box(new Rect(label.x - 110f, Screen.height - label.y - 22f, 220f, 44f),
                        "GUN SHOP  " + Mathf.RoundToInt(distance) + " m\nWeapons / Ammo / Lighters");
            }
            return;
        }

        float y = Screen.height * 0.56f;
        float line = 22f * ScreenText.Scale;
        ScreenText.Centered(Title(), y, 26);
        if (ShopKind != Kind.Wood && ShopKind != Kind.Butchery)
            ScreenText.Centered("N: bargain | " + (Time.time < bargainUntil && discount > 0f ? "Deal active" : "Standard prices"), y - 24f * ScreenText.Scale, 18);
        string[] lines = OfferLines();
        if (ShopKind != Kind.Wood && ShopKind != Kind.Butchery)
            for (int n = 0; n < lines.Length; n++)
                lines[n] = System.Text.RegularExpressions.Regex.Replace(lines[n], @"\b(\d+) wood", m => Price(int.Parse(m.Groups[1].Value)) + " wood");
        for (int i = 0; i < lines.Length; i++)
            ScreenText.Centered(lines[i], y + line * (i + 1), 18);
    }

    bool Ready()
    {
        if (FurniturePlacement.Active)
            return false;
        if (Inventory.IsOpen)
            return false;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return false;
        return Closest() == this;
    }

    static CastleShop Closest()
    {
        Vector3 player = PlayerView.Position();
        CastleShop best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Shops.Count; i++)
        {
            CastleShop shop = Shops[i];
            if (shop == null)
                continue;
            Vector3 offset = player - shop.transform.position;
            if (Mathf.Abs(offset.y) > 2.5f) continue;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > shop.range || distance >= bestDistance)
                continue;
            best = shop;
            bestDistance = distance;
        }

        return best;
    }

    void TickGuns()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            BuyCartridges(HuntWeapon.Shotgun, 8, 2, "shotgun");
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            BuyCartridges(HuntWeapon.Rifle, 6, 2, "rifle");
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            BuyCartridges(HuntWeapon.Sniper, 4, 3, "sniper");
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            BuyHuntingGear();
        else if (Keyboard.current.digit5Key.wasPressedThisFrame)
            BuyLighter();
        else if (Keyboard.current.digit6Key.wasPressedThisFrame)
            RentOrReturnLighter();
    }

    void TickClothes()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            BuyCoat();
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            BuyOilskin();
    }

    void TickWood()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            TradeMeatForWood(false, 3);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            TradeMeatForWood(true, 5);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            FurniturePlacement.Begin();
    }

    void TickMeat()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            BuyMeat(false, 3);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            BuyMeat(true, 5);
    }

    void BuyCartridges(HuntWeapon weapon, int count, int cost, string label)
    {
        if (PlayerShotgun.Instance == null)
        {
            Notice("The guns aren't ready yet.");
            return;
        }

        if (PlayerShotgun.Instance.CartridgeCount(weapon) + count > 240)
        {
            Notice("You can't carry more " + label + " cartridges.");
            return;
        }

        if (!PayWood(cost))
            return;

        PlayerShotgun.Instance.AddCartridges(weapon, count);
        Notice("Bought " + count + " " + label + " cartridges. "
            + PlayerShotgun.Instance.CartridgeCount(weapon) + " carried.");
    }

    void BuyHuntingGear()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;

        if (!inv.OwnsWeapon(HuntWeapon.Shotgun))
            BuyGun(HuntWeapon.Shotgun, 8, "shotgun");
        else if (!inv.OwnsWeapon(HuntWeapon.Rifle))
            BuyGun(HuntWeapon.Rifle, 10, "rifle");
        else if (!inv.OwnsWeapon(HuntWeapon.Sniper))
            BuyGun(HuntWeapon.Sniper, 14, "sniper");
        else if (!inv.hasPickaxe)
            BuyPickaxe();
        else
            BuyBait();
    }

    void BuyGun(HuntWeapon weapon, int cost, string label)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (inv.OwnsWeapon(weapon))
        {
            Notice("You already have that " + label + ".");
            return;
        }

        if (!PayWood(cost))
            return;

        if (!inv.TryAddWeapon(weapon))
        {
            inv.GiveWood(lastPaid);
            Notice("Make room in the bag.");
            return;
        }

        Notice("Bought a " + label + ".");
    }

    void BuyPickaxe()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (inv.hasPickaxe)
        {
            Notice("You already have a pickaxe.");
            return;
        }

        if (!PayWood(5))
            return;

        inv.hasPickaxe = true;
        Notice("Bought a pickaxe.");
    }

    void BuyLighter()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (inv.ownsLighter)
        {
            Notice("You already own a lighter.");
            return;
        }

        if (!PayWood(6))
            return;

        inv.BuyLighter();
        Notice("Bought a lighter. Press F to lay wood and light it.");
    }

    void RentOrReturnLighter()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (inv.ownsLighter)
        {
            Notice("The lighter is yours. Nothing to return.");
            return;
        }

        if (inv.LighterDueBack)
        {
            inv.ReturnLighter();
            Notice("Lighter returned.");
            return;
        }

        if (!PayWood(2))
            return;

        if (!inv.BeginLighterRental())
        {
            inv.GiveWood(lastPaid);
            Notice("Return the rented lighter before taking another.");
            return;
        }

        Notice("Rented a lighter for 3 minutes. Bring it back here.");
    }

    void BuyBait()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (inv.SpaceForBait() < 3)
        {
            Notice("Make room for the bait.");
            return;
        }

        if (!PayWood(2))
            return;

        int bait = inv.GiveBait(3);
        if (bait <= 0)
        {
            inv.GiveWood(lastPaid);
            Notice("Make room for the bait.");
            return;
        }

        Notice("Bought bait. Press X to place it.");
    }

    void BuyCoat()
    {
        SurvivalStats body = SurvivalStats.Current;
        if (body == null)
            return;
        if (body.HasHuntingCoat)
        {
            Notice("You're already wearing the hunting coat.");
            return;
        }

        if (!PayWood(6))
            return;

        body.GiveHuntingCoat();
        Notice("Hunting coat on. The cold won't bite as hard.");
    }

    void BuyOilskin()
    {
        SurvivalStats body = SurvivalStats.Current;
        if (body == null)
            return;
        if (body.HasOilskin)
        {
            Notice("You're already wearing the oilskin.");
            return;
        }

        if (!PayWood(5))
            return;

        body.GiveOilskin();
        Notice("Oilskin on. Rain soaks you more slowly.");
    }

    void TradeMeatForWood(bool cooked, int woodAmount)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (inv.SpaceForWood() < woodAmount)
        {
            Notice("Make room for the wood.");
            return;
        }

        bool paid = cooked ? inv.cookedMeat >= 1 && inv.SpendCooked(1) : inv.meat >= 1 && inv.SpendRaw(1);
        if (!paid)
        {
            Notice(cooked ? "Bring cooked meat to trade." : "Bring raw meat to trade.");
            return;
        }

        int got = inv.GiveWood(woodAmount);
        if (got < woodAmount)
        {
            inv.UseWood(got);
            if (cooked)
                inv.GiveCooked(1);
            else
                inv.GiveRawMeat(1);
            Notice("Make room for the wood.");
            return;
        }

        Notice("Traded meat for " + woodAmount + " wood.");
    }

    void BuyMeat(bool cooked, int cost)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;

        int room = cooked ? inv.SpaceForCooked() : inv.SpaceForRaw();
        if (room < 1)
        {
            Notice("Make room in the bag.");
            return;
        }

        if (!PayWood(cost))
            return;

        int got = cooked ? inv.GiveCooked(1) : inv.GiveRawMeat(1);
        if (got < 1)
        {
            inv.GiveWood(lastPaid);
            Notice("Make room in the bag.");
            return;
        }

        Notice(cooked ? "Bought cooked meat." : "Bought raw meat. Cook it before it spoils.");
    }

    bool PayWood(int cost)
    {
        cost = Price(cost);
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return false;
        if (inv.wood < cost)
        {
            Notice("Need " + cost + " wood.");
            return false;
        }

        if (!inv.UseWood(cost)) return false;
        lastPaid = cost;
        return true;
    }

    static void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
    }

    string Title()
    {
        switch (ShopKind)
        {
            case Kind.Guns: return "GUN SHOP";
            case Kind.Clothes: return "CLOTHES";
            case Kind.Wood: return "TIMBER & FURNITURE";
            case Kind.Meat: return "MEAT MARKET";
            default: return "BUTCHERY";
        }
    }

    string[] OfferLines()
    {
        if (ShopKind == Kind.Guns)
        {
            return new[]
            {
                "1  8 shotgun shells   2 wood",
                "2  6 rifle rounds   2 wood",
                "3  4 sniper rounds   3 wood",
                "4  " + GearLine(),
                LighterBuyLine(),
                LighterRentLine()
            };
        }

        if (ShopKind == Kind.Clothes)
        {
            return new[]
            {
                "1  Hunting coat   6 wood",
                "2  Oilskin   5 wood"
            };
        }

        if (ShopKind == Kind.Wood)
        {
            return new[]
            {
                "1  Trade 1 raw meat for 3 wood",
                "2  Trade 1 cooked meat for 5 wood",
                "3  Buy and place a wooden table   6 wood"
            };
        }

        if (ShopKind == Kind.Meat)
        {
            return new[]
            {
                "1  Raw meat   3 wood",
                "2  Cooked meat   5 wood"
            };
        }

        return new[] { "Press C to cook the raw meat you are carrying" };
    }

    string GearLine()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return "Hunting supplies";
        if (!inv.OwnsWeapon(HuntWeapon.Shotgun))
            return "Shotgun   8 wood";
        if (!inv.OwnsWeapon(HuntWeapon.Rifle))
            return "Rifle   10 wood";
        if (!inv.OwnsWeapon(HuntWeapon.Sniper))
            return "Sniper   14 wood";
        if (!inv.hasPickaxe)
            return "Pickaxe   5 wood";
        return "Bait x3   2 wood";
    }

    static string LighterBuyLine()
    {
        Inventory inv = Inventory.Instance;
        if (inv != null && inv.ownsLighter)
            return "You own a lighter";
        return "5  Buy a lighter   6 wood";
    }

    static string LighterRentLine()
    {
        Inventory inv = Inventory.Instance;
        if (inv != null && inv.ownsLighter)
            return "Yours to keep";
        if (inv == null)
            return "6  Rent a lighter   2 wood, 3 min (owner attacks if overdue)";
        if (!inv.LighterDueBack)
            return "6  Rent a lighter   2 wood, 3 min (owner attacks if overdue)";

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, inv.lighterSeconds));
        if (seconds <= 0)
            return "6  Return the rented lighter";
        return "6  Return lighter   " + (seconds / 60) + ":" + (seconds % 60).ToString("00") + " left";
    }

    void BuildStall()
    {
        Color roof = RoofColor();
        Color counter = CounterColor();
        Material wood = Mat(new Color(0.36f, 0.22f, 0.12f), 0.25f);
        Material roofMat = Mat(roof, 0.15f);
        Material counterMat = Mat(counter, 0.3f);
        Material cloth = Mat(roof, 0.1f);

        Box(transform, "Shop sign board", wood, new Vector3(0f, 2.35f, 0f), new Vector3(2.6f, 0.65f, 0.1f), false);
        for (int face = 0; face < 2; face++)
        {
            GameObject sign = new GameObject("Readable shop sign");
            sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0f, 2.35f, face == 0 ? 0.065f : -0.065f);
            sign.transform.localRotation = Quaternion.Euler(0f, face == 0 ? 180f : 0f, 0f);
            TextMesh text = sign.AddComponent<TextMesh>();
            text.text = Title(); text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.fontSize = 64;
            text.characterSize = 0.055f; text.color = new Color(1f, 0.87f, 0.58f);
        }
        Box(transform, "Counter front", wood, new Vector3(0f, 0.43f, 0.25f), new Vector3(1.7f, 0.8f, 0.1f), true);
        Box(transform, "Counter leg left", wood, new Vector3(-0.7f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.5f), true);
        Box(transform, "Counter leg right", wood, new Vector3(0.7f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.5f), true);
        if (ShopKind == Kind.Guns)
        {
            Material steel = Mat(new Color(0.25f, 0.28f, 0.3f), 0.6f);
            Box(transform, "Weapon rack", wood, new Vector3(0f, 1.15f, -1.2f), new Vector3(1.65f, 0.12f, 0.12f), false);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.55f + i * 0.55f;
                Box(transform, "Rifle stock", wood, new Vector3(x, 0.85f, -1.16f), new Vector3(0.12f, 0.45f, 0.12f), false);
                Box(transform, "Rifle barrel", steel, new Vector3(x, 1.35f, -1.16f), new Vector3(0.045f, 0.65f, 0.045f), false);
                Box(transform, "Ammo box", roofMat, new Vector3(x, 1.0f, 0.1f), new Vector3(0.25f, 0.18f, 0.22f), false);
            }
        }
        Box(transform, "Counter", counterMat, new Vector3(0f, 0.85f, 0f), new Vector3(1.7f, 0.16f, 0.7f), true);
        Box(transform, "PostL", wood, new Vector3(-0.75f, 1.15f, -0.28f), new Vector3(0.08f, 1.5f, 0.08f), false);
        Box(transform, "PostR", wood, new Vector3(0.75f, 1.15f, -0.28f), new Vector3(0.08f, 1.5f, 0.08f), false);
        Box(transform, "Roof", roofMat, new Vector3(0f, 1.95f, 0.05f), new Vector3(1.9f, 0.06f, 1.15f), false);
        Box(transform, "Flag", cloth, new Vector3(0f, 2.25f, -0.28f), new Vector3(0.55f, 0.4f, 0.04f), false);

        if (ShopKind == Kind.Guns)
        {
            var palette = new Villager.Palette
            {
                skin = new[] { Mat(new Color(0.65f, 0.43f, 0.3f), 0.1f) },
                cloth = new[] { cloth }, trousers = new[] { wood }, hair = new[] { wood },
                dark = wood, straw = roofMat, metal = counterMat, wood = wood
            };
            lighterKeeper = Villager.Create(transform, transform.TransformPoint(new Vector3(0f, 0f, -0.8f)),
                transform.rotation, Villager.Role.Shopkeeper, palette, new System.Random(47), "Gun shop owner");
            lighterKeeper.snapToGround = false;
        }

        if (ShopKind == Kind.Guns)
            Box(transform, "Rifle", Mat(new Color(0.18f, 0.18f, 0.2f), 0.6f), new Vector3(0.1f, 0.98f, 0.05f), new Vector3(0.9f, 0.05f, 0.08f), false);
        else if (ShopKind == Kind.Clothes)
            Box(transform, "Hanging", cloth, new Vector3(0f, 1.35f, -0.2f), new Vector3(0.45f, 0.7f, 0.06f), false);
        else if (ShopKind == Kind.Wood)
        {
            Box(transform, "Planks", wood, new Vector3(0f, 1.05f, 0.05f), new Vector3(0.9f, 0.18f, 0.4f), false);
            Box(transform, "Planks2", wood, new Vector3(0.05f, 1.2f, 0.02f), new Vector3(0.7f, 0.12f, 0.35f), false);
        }
        else if (ShopKind == Kind.Meat)
            Box(transform, "Slab", Mat(new Color(0.55f, 0.16f, 0.14f), 0.2f), new Vector3(0f, 1.0f, 0.05f), new Vector3(0.55f, 0.1f, 0.35f), false);
        else
            BuildHearth();
    }

    void BuildHearth()
    {
        Material stone = Mat(new Color(0.35f, 0.34f, 0.32f), 0.15f);
        Box(transform, "Hearth", stone, new Vector3(0f, 0.45f, 0.35f), new Vector3(0.8f, 0.5f, 0.55f), true);
        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(flame.GetComponent<Collider>());
        flame.name = "Flame";
        flame.transform.SetParent(transform, false);
        flame.transform.localPosition = new Vector3(0f, 0.85f, 0.35f);
        flame.transform.localScale = new Vector3(0.22f, 0.32f, 0.22f);
        flame.GetComponent<Renderer>().sharedMaterial = Mat(new Color(1f, 0.45f, 0.12f), 0.1f);

        GameObject lightObj = new GameObject("HearthLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.1f, 0.35f);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.25f);
        light.range = 5f;
        light.intensity = 2.2f;
    }

    Color RoofColor()
    {
        switch (ShopKind)
        {
            case Kind.Guns: return new Color(0.22f, 0.24f, 0.28f);
            case Kind.Clothes: return new Color(0.25f, 0.38f, 0.62f);
            case Kind.Wood: return new Color(0.45f, 0.28f, 0.14f);
            case Kind.Meat: return new Color(0.55f, 0.18f, 0.16f);
            default: return new Color(0.42f, 0.28f, 0.18f);
        }
    }

    Color CounterColor()
    {
        switch (ShopKind)
        {
            case Kind.Guns: return new Color(0.28f, 0.27f, 0.25f);
            case Kind.Clothes: return new Color(0.55f, 0.32f, 0.38f);
            case Kind.Wood: return new Color(0.40f, 0.26f, 0.14f);
            case Kind.Meat: return new Color(0.72f, 0.7f, 0.64f);
            default: return new Color(0.32f, 0.31f, 0.29f);
        }
    }

    static void Box(Transform parent, string name, Material material, Vector3 pos, Vector3 scale, bool solid)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!solid)
            Object.Destroy(box.GetComponent<Collider>());
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = pos;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Material Mat(Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}

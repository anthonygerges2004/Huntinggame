using System.Collections.Generic;
using HuntingGame.Survival;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Four stalls in the castle courtyard, each with a keeper: guns, clothes, a tanner who skins
/// carcasses and buys pelts (the main source of coins), and a butcher who cooks.
/// Food and timber are sold at the Castle town market just outside (VillageMarket), so they
/// are not repeated here.
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

        // Narrower than the old five-stall row, so every stall stands clear of the walls.
        Add(root.transform, CastleShop.Kind.Guns, -1.5f * gap, y, z, range);
        Add(root.transform, CastleShop.Kind.Clothes, -0.5f * gap, y, z, range);
        Add(root.transform, CastleShop.Kind.Tanner, 0.5f * gap, y, z, range);
        Add(root.transform, CastleShop.Kind.Butchery, 1.5f * gap, y, z, range);

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
        Butchery,
        Tanner
    }

    /// <summary>Goods are bought with coins at the old wood prices times this.</summary>
    public const int CoinRate = 3;

    /// <summary>Stalls that sell goods for coins and take bargaining. Wood barters; the tanner buys; the butcher cooks.</summary>
    bool Sells => ShopKind == Kind.Guns || ShopKind == Kind.Clothes || ShopKind == Kind.Meat;

    int CoinPrice(int cost) => Price(cost) * CoinRate;

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
                Notice("Return my lighter! Press 6 at the gun shop, or buy it.");
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
        PushHud();
        if (!Ready() || Keyboard.current == null)
            return;

        if (Keyboard.current.nKey.wasPressedThisFrame && Sells) { Bargain(); return; }
        if (ShopKind == Kind.Guns)
            TickGuns();
        else if (ShopKind == Kind.Clothes)
            TickClothes();
        else if (ShopKind == Kind.Wood)
            TickWood();
        else if (ShopKind == Kind.Meat)
            TickMeat();
        else if (ShopKind == Kind.Tanner)
            TickTanner();
    }

    /// <summary>Sends the stall menu (or the gun-shop sign) to the HUD, which draws it in the game's style.</summary>
    void PushHud()
    {
        GameHUD hud = GameHUD.Instance;
        if (hud == null)
            return;

        if (!Ready())
        {
            if (ShopKind == Kind.Guns && !Inventory.IsOpen)
            {
                float distance = Vector3.Distance(PlayerView.Position(), transform.position);
                if (distance < 65f)
                    hud.ShowWorldTag(transform.position + Vector3.up * 2.8f, "GUN SHOP  " + Mathf.RoundToInt(distance) + " m",
                                     "Weapons  ·  Ammo  ·  Lighters");
            }
            return;
        }

        string status = "";
        if (Sells)
            status = Time.time < bargainUntil && discount > 0f
                ? "<color=#8FDB75>DEAL ACTIVE  ·  " + Mathf.RoundToInt(discount * 100f) + "% OFF</color>"
                : "N  BARGAIN FOR A DISCOUNT";
        string[] lines = OfferLines();
        if (Sells)
            for (int n = 0; n < lines.Length; n++)
                lines[n] = System.Text.RegularExpressions.Regex.Replace(lines[n], @"\b(\d+) wood", m => CoinPrice(int.Parse(m.Groups[1].Value)) + " coins");
        hud.ShowShop(Title(), status, lines);
    }

    void OnGUI()
    {
        // With the HUD up, PushHud draws all of this in the game's style.
        if (GameHUD.Instance != null)
            return;

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
        if (Sells)
            ScreenText.Centered("N: bargain | " + (Time.time < bargainUntil && discount > 0f ? "Deal active" : "Standard prices"), y - 24f * ScreenText.Scale, 18);
        string[] lines = OfferLines();
        if (Sells)
            for (int n = 0; n < lines.Length; n++)
                lines[n] = System.Text.RegularExpressions.Regex.Replace(lines[n], @"\b(\d+) wood", m => CoinPrice(int.Parse(m.Groups[1].Value)) + " coins");
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
            GunOrAmmo(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            GunOrAmmo(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            GunOrAmmo(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            GunOrAmmo(3);
        else if (Keyboard.current.digit7Key.wasPressedThisFrame)
            GunOrAmmo(4);
        else if (Keyboard.current.digit8Key.wasPressedThisFrame)
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
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SellMeat(false, RawMeatPrice);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            SellMeat(true, CookedMeatPrice);
    }

    const int RawMeatPrice = 4;
    const int CookedMeatPrice = 7;

    void SellMeat(bool cooked, int coins)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;
        if (!(cooked ? inv.SpendCooked(1) : inv.SpendRaw(1)))
        {
            Notice(cooked ? "No cooked meat to sell." : "No raw meat to sell.");
            return;
        }

        inv.AddCoins(coins);
        Notice("Sold " + (cooked ? "cooked" : "raw") + " meat for " + coins + " coins.");
    }

    // ---------------------------------------------------------------- tanner

    void TickTanner()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            int skinned = inv.SkinAllCarcasses(out int meat);
            Notice(skinned == 0
                ? "No carcasses to skin. Bring me whole animals: tap E on a kill to take it."
                : "Skinned " + skinned + (skinned == 1 ? " carcass" : " carcasses") + ". The pelts and " + meat
                  + " meat are in your bag. The butcher next door will cook the meat.");
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            int earned = inv.SellAllPelts(out int sold);
            Notice(sold == 0
                ? "No pelts to sell."
                : "Sold " + sold + (sold == 1 ? " pelt" : " pelts") + " for " + earned + " coins.");
        }
    }

    string[] TannerLines()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return new[] { "Bring me carcasses and pelts." };

        var lines = new List<string>();
        int carcasses = inv.CarcassCount;
        int pelts = inv.PeltCount;
        lines.Add(carcasses > 0
            ? "1  Skin " + carcasses + (carcasses == 1 ? " carcass" : " carcasses") + " cleanly   free"
            : "Bring whole carcasses and I skin them cleanly, for free.");
        lines.Add(pelts > 0
            ? "2  Sell " + pelts + (pelts == 1 ? " pelt" : " pelts") + "   +" + inv.TotalPeltValue() + " coins"
            : "No pelts to sell yet.");

        float now = Time.time;
        List<HuntGoods> best = inv.PeltsByValue();
        for (int i = 0; i < best.Count && i < 4; i++)
        {
            HuntGoods g = best[i];
            string title = g.Title.Substring(0, 1) + g.Title.Substring(1).ToLowerInvariant();
            lines.Add(title + " pelt · " + g.ConditionLabel.ToLowerInvariant() + "   worth " + g.PeltValue(now));
        }

        if (best.Count > 4)
            lines.Add("…and " + (best.Count - 4) + " more");
        return lines.ToArray();
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

        if (!Pay(cost))
            return;

        PlayerShotgun.Instance.AddCartridges(weapon, count);
        Notice("Bought " + count + " " + label + " cartridges. "
            + PlayerShotgun.Instance.CartridgeCount(weapon) + " carried.");
    }

    // The gun rack: key, weapon, gun price, ammo per box, ammo price (prices in coins).
    struct GunOffer
    {
        public string key;
        public HuntWeapon weapon;
        public int gunCoins;
        public int box;
        public int boxCoins;
        public string ammo;
    }

    static readonly GunOffer[] Rack =
    {
        new GunOffer { key = "1", weapon = HuntWeapon.Shotgun, gunCoins = 45, box = 8, boxCoins = 6, ammo = "shotgun shells" },
        new GunOffer { key = "2", weapon = HuntWeapon.SMG, gunCoins = 70, box = 30, boxCoins = 8, ammo = "SMG rounds" },
        new GunOffer { key = "3", weapon = HuntWeapon.Rifle, gunCoins = 110, box = 30, boxCoins = 10, ammo = "rifle rounds" },
        new GunOffer { key = "4", weapon = HuntWeapon.Sniper, gunCoins = 160, box = 5, boxCoins = 9, ammo = "sniper rounds" },
        new GunOffer { key = "7", weapon = HuntWeapon.Pistol, gunCoins = 20, box = 12, boxCoins = 3, ammo = "pistol rounds" },
    };

    void GunOrAmmo(int index)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null || PlayerShotgun.Instance == null)
            return;
        GunOffer o = Rack[index];
        string name = WeaponVisual.DisplayName(o.weapon).ToLowerInvariant();
        if (inv.OwnsWeapon(o.weapon))
        {
            if (PlayerShotgun.Instance.CartridgeCount(o.weapon) + o.box > 240)
            {
                Notice("You can't carry more " + o.ammo + ".");
                return;
            }
            if (!PayCoins(o.boxCoins))
                return;
            PlayerShotgun.Instance.AddCartridges(o.weapon, o.box);
            Notice("Bought " + o.box + " " + o.ammo + ". " + PlayerShotgun.Instance.CartridgeCount(o.weapon) + " carried.");
            return;
        }

        if (!PayCoins(o.gunCoins))
            return;
        if (!inv.TryAddWeapon(o.weapon))
        {
            inv.RefundCoins(lastPaid);
            Notice("Make room in the bag.");
            return;
        }
        Notice("Bought the " + name + ". Find it in your bag (Tab) or press its hotbar number.");
    }

    bool PayCoins(int coins)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return false;
        if (inv.coins < coins)
        {
            Notice("Need " + coins + " coins. Sell pelts at the tanner.");
            return false;
        }
        if (!inv.SpendCoins(coins))
            return false;
        lastPaid = coins;
        return true;
    }

    static string RackLine(GunOffer o)
    {
        Inventory inv = Inventory.Instance;
        if (inv != null && inv.OwnsWeapon(o.weapon))
            return o.key + "  " + o.box + " " + o.ammo + "   " + o.boxCoins + " coins";
        return o.key + "  " + WeaponVisual.DisplayName(o.weapon) + "   " + o.gunCoins + " coins";
    }

    void BuyHuntingGear()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return;

        if (!inv.hasPickaxe)
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

        if (!Pay(cost))
            return;

        if (!inv.TryAddWeapon(weapon))
        {
            inv.RefundCoins(lastPaid);
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

        if (!Pay(5))
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

        if (!Pay(6))
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

        if (!Pay(2))
            return;

        if (!inv.BeginLighterRental())
        {
            inv.RefundCoins(lastPaid);
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

        if (!Pay(2))
            return;

        int bait = inv.GiveBait(3);
        if (bait <= 0)
        {
            inv.RefundCoins(lastPaid);
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

        if (!Pay(6))
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

        if (!Pay(5))
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

        if (!Pay(cost))
            return;

        int got = cooked ? inv.GiveCooked(1) : inv.GiveRawMeat(1);
        if (got < 1)
        {
            inv.RefundCoins(lastPaid);
            Notice("Make room in the bag.");
            return;
        }

        Notice(cooked ? "Bought cooked meat." : "Bought raw meat. Cook it before it spoils.");
    }

    bool Pay(int cost)
    {
        cost = CoinPrice(cost);
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return false;
        if (inv.coins < cost)
        {
            Notice("Need " + cost + " coins. Sell pelts at the tanner.");
            return false;
        }

        if (!inv.SpendCoins(cost)) return false;
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
            case Kind.Tanner: return "TANNER";
            default: return "BUTCHERY";
        }
    }

    string[] OfferLines()
    {
        if (ShopKind == Kind.Guns)
        {
            return new[]
            {
                RackLine(Rack[0]),
                RackLine(Rack[1]),
                RackLine(Rack[2]),
                RackLine(Rack[3]),
                RackLine(Rack[4]),
                "8  " + GearLine(),
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
                "2  Cooked meat   5 wood",
                "3  Sell 1 raw meat   +" + RawMeatPrice + " coins",
                "4  Sell 1 cooked meat   +" + CookedMeatPrice + " coins"
            };
        }

        if (ShopKind == Kind.Tanner)
            return TannerLines();

        return new[] { "Press C to cook the raw meat you are carrying" };
    }

    string GearLine()
    {
        Inventory inv = Inventory.Instance;
        if (inv == null)
            return "Hunting supplies";
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
        // One readable face on each side of the board; depth-tested so the back never bleeds through.
        for (int face = 0; face < 2; face++)
            SignText.Make(transform, Title(), new Vector3(0f, 2.35f, face == 0 ? 0.056f : -0.056f),
                          Quaternion.Euler(0f, face == 0 ? 180f : 0f, 0f), new Vector2(2.4f, 0.5f), new Color(1f, 0.87f, 0.58f));
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

        // Every stall is staffed: the keeper stands behind the counter facing customers.
        var palette = new Villager.Palette
        {
            skin = new[] { Mat(KeeperSkin(), 0.1f) },
            cloth = new[] { ShopKind == Kind.Guns ? cloth : Mat(KeeperCloth(), 0.1f) },
            trousers = new[] { wood }, hair = new[] { wood },
            dark = wood, straw = roofMat, metal = counterMat, wood = wood
        };
        Villager keeper = Villager.Create(transform, transform.TransformPoint(new Vector3(0f, 0f, -0.8f)),
            transform.rotation, Villager.Role.Shopkeeper, palette, new System.Random(47 + (int)ShopKind * 13), KeeperName());
        keeper.snapToGround = false;
        if (ShopKind == Kind.Guns)
            lighterKeeper = keeper;

        if (ShopKind == Kind.Tanner)
            BuildHides(wood);

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
        else if (ShopKind == Kind.Butchery)
            BuildHearth();
    }

    string KeeperName()
    {
        switch (ShopKind)
        {
            case Kind.Guns: return "Gun shop owner";
            case Kind.Clothes: return "Tailor";
            case Kind.Wood: return "Carpenter";
            case Kind.Meat: return "Meat seller";
            case Kind.Tanner: return "Tanner";
            default: return "Butcher";
        }
    }

    Color KeeperSkin()
    {
        switch (ShopKind)
        {
            case Kind.Clothes: return new Color(0.86f, 0.68f, 0.55f);
            case Kind.Tanner: return new Color(0.72f, 0.52f, 0.38f);
            case Kind.Butchery: return new Color(0.55f, 0.38f, 0.27f);
            default: return new Color(0.65f, 0.43f, 0.3f);
        }
    }

    Color KeeperCloth()
    {
        switch (ShopKind)
        {
            case Kind.Clothes: return new Color(0.30f, 0.42f, 0.66f);
            case Kind.Tanner: return new Color(0.42f, 0.30f, 0.20f);
            case Kind.Butchery: return new Color(0.88f, 0.86f, 0.80f); // apron white
            default: return new Color(0.45f, 0.32f, 0.20f);
        }
    }

    // Hides stretched on a rack behind the counter, and one being scraped on it.
    void BuildHides(Material wood)
    {
        Box(transform, "Hide rack", wood, new Vector3(0f, 1.62f, -1.05f), new Vector3(1.7f, 0.08f, 0.08f), false);
        Color[] hides = { new Color(0.55f, 0.36f, 0.20f), new Color(0.78f, 0.44f, 0.18f), new Color(0.48f, 0.47f, 0.45f) };
        for (int i = 0; i < hides.Length; i++)
        {
            float x = -0.55f + i * 0.55f;
            Box(transform, "Hanging hide", Mat(hides[i], 0.05f), new Vector3(x, 1.22f, -1.05f), new Vector3(0.42f, 0.7f, 0.03f), false);
        }

        Box(transform, "Hide on counter", Mat(new Color(0.62f, 0.42f, 0.24f), 0.05f), new Vector3(0f, 0.95f, 0.02f),
            new Vector3(0.8f, 0.03f, 0.5f), false);
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
            case Kind.Tanner: return new Color(0.50f, 0.36f, 0.20f);
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
            case Kind.Tanner: return new Color(0.46f, 0.34f, 0.22f);
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

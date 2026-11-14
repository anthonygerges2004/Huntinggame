using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HuntProgressTests
{
    const BindingFlags Private = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    Inventory inventory;
    CastleShop shop;
    PlayerShotgun gun;

    [SetUp]
    public void SetUp()
    {
        ResetProgress();
        root = new GameObject("RankTests");
        root.SetActive(false);
        inventory = root.AddComponent<Inventory>();
        Inventory.Instance = inventory;
        inventory.coins = 1000;
        shop = root.AddComponent<CastleShop>();
        shop.Setup(CastleShop.Kind.Guns, 3f, false);
        gun = root.AddComponent<PlayerShotgun>();
        typeof(PlayerShotgun).GetMethod("Awake", Private).Invoke(gun, null);
    }

    [TearDown]
    public void TearDown()
    {
        Inventory.Instance = null;
        Object.DestroyImmediate(root);
        ResetProgress();
    }

    static void ResetProgress()
    {
        typeof(HuntProgress).GetMethod("ResetForPlayMode", Private).Invoke(null, null);
        while (HuntFeedback.TryTakeBanner(out _)) { }
    }

    [TestCase(149, 0)]
    [TestCase(150, 1)]
    [TestCase(399, 1)]
    [TestCase(400, 2)]
    [TestCase(799, 2)]
    [TestCase(800, 3)]
    [TestCase(1399, 3)]
    [TestCase(1400, 4)]
    [TestCase(2199, 4)]
    [TestCase(2200, 5)]
    public void XpThresholds(int xp, int expected)
    {
        HuntProgress.AddXp(xp);
        Assert.AreEqual(expected, HuntProgress.RankIndex);
        Assert.That(HuntProgress.RankProgress01, Is.InRange(0f, 1f));
    }

    [Test]
    public void NonPositiveXpIgnoredAndOverflowSaturates()
    {
        HuntProgress.AddXp(-10);
        HuntProgress.AddXp(0);
        Assert.AreEqual(0, HuntProgress.Xp);
        HuntProgress.AddXp(int.MaxValue);
        HuntProgress.AddXp(100);
        Assert.AreEqual(int.MaxValue, HuntProgress.Xp);
        Assert.IsTrue(HuntProgress.IsMaxRank);
        Assert.AreEqual(1f, HuntProgress.RankProgress01);
    }

    [TestCase(HuntWeapon.Pistol, 0)]
    [TestCase(HuntWeapon.Shotgun, 1)]
    [TestCase(HuntWeapon.SMG, 2)]
    [TestCase(HuntWeapon.Rifle, 3)]
    [TestCase(HuntWeapon.Sniper, 4)]
    public void WeaponAcquisitionRequiresItsRank(HuntWeapon weapon, int rank)
    {
        if (rank > 0)
        {
            HuntProgress.AddXp(HuntProgress.Ranks[rank].xp - 1);
            Assert.IsFalse(inventory.TryAddWeapon(weapon));
            Assert.IsFalse(inventory.OwnsWeapon(weapon));
            HuntProgress.AddXp(1);
        }
        Assert.IsTrue(HuntProgress.WeaponUnlocked(weapon));
        if (rank == 0) Assert.IsTrue(inventory.TryAddWeapon(weapon));
        Assert.IsTrue(inventory.OwnsWeapon(weapon));
        Assert.IsFalse(inventory.TryAddWeapon(weapon), "Duplicate guns must not be added.");
    }

    [Test]
    public void InvalidWeaponsAreNeverUnlocked()
    {
        HuntProgress.AddXp(2200);
        Assert.IsFalse(HuntProgress.WeaponUnlocked(HuntWeapon.None));
        Assert.IsFalse(HuntProgress.WeaponUnlocked((HuntWeapon)999));
        Assert.IsFalse(HuntProgress.WeaponUnlocked((HuntWeapon)(-1)));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void DailyContractsNeverRequireLockedWeapons(int rank)
    {
        HuntProgress.SetRankForTesting(rank);
        for (int day = 1; day <= 100; day++)
        {
            typeof(HuntProgress).GetMethod("RollContracts", Private).Invoke(null, new object[] { day });
            Assert.AreEqual(3, HuntProgress.Contracts.Count);
            foreach (var contract in HuntProgress.Contracts)
                Assert.IsTrue(contract.requiredWeapon == HuntWeapon.None || HuntProgress.WeaponUnlocked(contract.requiredWeapon), contract.text);
        }
    }

    [Test]
    public void RankTestingSupportsDowngradesAndClamps()
    {
        HuntProgress.SetRankForTesting(99);
        Assert.AreEqual(5, HuntProgress.RankIndex);
        HuntProgress.SetRankForTesting(1);
        Assert.AreEqual(150, HuntProgress.Xp);
        Assert.IsTrue(HuntProgress.WeaponUnlocked(HuntWeapon.Shotgun));
        Assert.IsFalse(HuntProgress.WeaponUnlocked(HuntWeapon.Rifle));
        HuntProgress.SetRankForTesting(-5);
        Assert.AreEqual(0, HuntProgress.RankIndex);
        Assert.AreEqual(0, HuntProgress.Xp);
    }

    [Test]
    public void RankingUpAwardsAllEarnedGunsWithoutCharging()
    {
        HuntProgress.AddXp(1400);
        Assert.AreEqual(1000, inventory.coins);
        foreach (HuntWeapon weapon in new[] { HuntWeapon.Pistol, HuntWeapon.Shotgun, HuntWeapon.SMG, HuntWeapon.Rifle, HuntWeapon.Sniper })
            Assert.IsTrue(inventory.OwnsWeapon(weapon), weapon.ToString());
        inventory.GrantRankWeapons();
        Assert.IsFalse(inventory.TryAddWeapon(HuntWeapon.Sniper));
    }

    [Test]
    public void FullBagDefersFreeGunUntilSpaceIsAvailable()
    {
        inventory.GiveWood(64 * 25);
        HuntProgress.AddXp(150);
        Assert.IsFalse(inventory.OwnsWeapon(HuntWeapon.Shotgun));
        Assert.AreEqual(1000, inventory.coins);
        var kind = typeof(Inventory).GetNestedType("ItemKind", BindingFlags.NonPublic);
        typeof(Inventory).GetMethod("RemoveItem", Private).Invoke(inventory,
            new object[] { System.Enum.Parse(kind, "Wood"), 128 });
        inventory.GrantRankWeapons();
        Assert.IsTrue(inventory.OwnsWeapon(HuntWeapon.Shotgun));
        Assert.AreEqual(1000, inventory.coins);
    }

    [Test]
    public void SkinsChargeOncePerGunAndReequipForFree()
    {
        HuntProgress.AddXp(400);
        Assert.IsTrue(inventory.BuyOrEquipSkin(HuntWeapon.Shotgun, 1, 30));
        Assert.AreEqual(970, inventory.coins);
        Assert.AreEqual(1, inventory.SelectedSkin(HuntWeapon.Shotgun));
        Assert.IsFalse(inventory.OwnsSkin(HuntWeapon.SMG, 1));
        Assert.IsTrue(inventory.BuyOrEquipSkin(HuntWeapon.Shotgun, 0, 0));
        Assert.IsTrue(inventory.BuyOrEquipSkin(HuntWeapon.Shotgun, 1, 30));
        Assert.AreEqual(970, inventory.coins);
        Assert.IsTrue(inventory.BuyOrEquipSkin(HuntWeapon.SMG, 1, 30));
        Assert.AreEqual(940, inventory.coins);
    }

    [Test]
    public void SkinVisualInitializesOnInactiveGunAndRestoresOriginalColor()
    {
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(root.transform, false);
        model.SetActive(false);
        Renderer renderer = model.GetComponent<Renderer>();
        var block = new MaterialPropertyBlock();
        int color = Shader.PropertyToID("_BaseColor");
        block.SetColor(color, Color.red);
        renderer.SetPropertyBlock(block);
        GunSkinVisual visual = null;
        Assert.DoesNotThrow(() => visual = model.AddComponent<GunSkinVisual>());
        Assert.DoesNotThrow(() => visual.Apply(1));
        renderer.GetPropertyBlock(block);
        Assert.AreEqual(GunSkins.Tint(1), block.GetColor(color));
        visual.Apply(0);
        renderer.GetPropertyBlock(block);
        Assert.AreEqual(Color.red, block.GetColor(color));
    }

    [Test]
    public void SkinsRejectLockedGunsInvalidIndicesAndInsufficientCoins()
    {
        Assert.IsFalse(inventory.BuyOrEquipSkin(HuntWeapon.Sniper, 1, 30));
        Assert.IsFalse(inventory.BuyOrEquipSkin(HuntWeapon.None, 1, 30));
        HuntProgress.AddXp(150);
        Assert.IsFalse(inventory.BuyOrEquipSkin(HuntWeapon.Shotgun, 99, 30));
        inventory.coins = 29;
        Assert.IsFalse(inventory.BuyOrEquipSkin(HuntWeapon.Shotgun, 1, 30));
        Assert.AreEqual(29, inventory.coins);
        Assert.IsFalse(inventory.OwnsSkin(HuntWeapon.Shotgun, 1));
        Assert.AreEqual(0, inventory.SelectedSkin(HuntWeapon.Shotgun));
    }

    [Test]
    public void ShopNeverSellsLockedGuns()
    {
        typeof(CastleShop).GetMethod("GunOrAmmo", Private).Invoke(shop, new object[] { 3 });
        Assert.AreEqual(1000, inventory.coins);
        Assert.IsFalse(inventory.OwnsWeapon(HuntWeapon.Sniper));
    }

    [Test]
    public void ShopDiscountAppliesToSkinAndOwnedSkinIsFree()
    {
        HuntProgress.AddXp(150);
        typeof(CastleShop).GetField("skinWeapon", Private).SetValue(shop, HuntWeapon.Shotgun);
        typeof(CastleShop).GetField("discount", Private).SetValue(shop, 0.25f);
        typeof(CastleShop).GetField("bargainUntil", Private).SetValue(shop, Time.time + 60f);
        var buy = typeof(CastleShop).GetMethod("BuySkin", Private);
        buy.Invoke(shop, new object[] { 1 });
        Assert.AreEqual(977, inventory.coins);
        buy.Invoke(shop, new object[] { 1 });
        Assert.AreEqual(977, inventory.coins);
    }
    [TestCase(0)]
    [TestCase(4)]
    public void ControllerRejectsLockedOrUnownedGun(int rank)
    {
        Inventory.Instance = null;
        HuntProgress.SetRankForTesting(rank);
        Inventory.Instance = inventory;
        var rigType = typeof(PlayerShotgun).GetNestedType("Rig", BindingFlags.NonPublic);
        var rigs = System.Array.CreateInstance(rigType, 6);
        var rig = System.Activator.CreateInstance(rigType, true);
        rigType.GetField("id").SetValue(rig, HuntWeapon.Sniper);
        rigs.SetValue(rig, (int)HuntWeapon.Sniper);
        typeof(PlayerShotgun).GetField("built", Private).SetValue(gun, rigs);
        Assert.DoesNotThrow(() => gun.Equip(HuntWeapon.Sniper));
        Assert.AreEqual(HuntWeapon.None, gun.Equipped);
    }
}

using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The pelt economy: appraisal, shot condition, freshness, and the carcass → tanner → coins loop.
/// </summary>
public class PeltEconomyTests
{
    GameObject root;
    Inventory inventory;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("PeltEconomyTest");
        root.SetActive(false);
        inventory = root.AddComponent<Inventory>();
        inventory.coins = 0;
        Inventory.Instance = inventory; // the static speed/weight helpers read the live inventory
    }

    [TearDown]
    public void TearDown()
    {
        if (Inventory.Instance == inventory)
            Inventory.Instance = null;
        Object.DestroyImmediate(root);
        foreach (MeatPickup leftover in Object.FindObjectsByType<MeatPickup>(FindObjectsInactive.Include))
            Object.DestroyImmediate(leftover.gameObject);
    }

    static HuntGoods Deer(AnimalTrophy.Tier tier = AnimalTrophy.Tier.Common, PeltCondition condition = PeltCondition.Good,
                          float kg = 72f, int meat = 3)
    {
        return new HuntGoods
        {
            kind = AnimalKind.Deer,
            species = "DEER",
            tier = tier,
            condition = condition,
            weightKg = kg,
            meat = meat,
            killedAt = Time.time,
        };
    }

    AnimalCarcass CarcassOf(HuntGoods goods)
    {
        GameObject body = new GameObject("TestCarcass");
        AnimalCarcass c = body.AddComponent<AnimalCarcass>();
        c.goods = goods;
        return c;
    }

    // ---------------------------------------------------------------- appraisal

    [Test]
    public void CommonDeer_GoodFreshPelt_IsWorthTheBasePrice()
    {
        Assert.AreEqual(18, Deer().PeltValue(Time.time));
    }

    [Test]
    public void TrophyPristine_IsWorthMoreThanCommonGood()
    {
        int trophy = Deer(AnimalTrophy.Tier.Trophy, PeltCondition.Pristine).PeltValue(Time.time);
        Assert.AreEqual(54, trophy); // 18 x 2.5 x 1.2
        Assert.Greater(trophy, Deer().PeltValue(Time.time));
    }

    [Test]
    public void RareCoat_IsTheMostValuable()
    {
        HuntGoods albino = Deer(AnimalTrophy.Tier.Rare);
        albino.coat = AnimalTrophy.Coat.Albino;
        Assert.AreEqual(108, albino.PeltValue(Time.time)); // 18 x 6
        Assert.Greater(albino.PeltValue(Time.time), Deer(AnimalTrophy.Tier.Trophy, PeltCondition.Pristine).PeltValue(Time.time));
    }

    [Test]
    public void SpeciesValues_RankSensibly()
    {
        Assert.Less(HuntGoods.BaseValue(AnimalKind.Game, QuarryKind.Rabbit), HuntGoods.BaseValue(AnimalKind.Game, QuarryKind.Fox));
        Assert.Less(HuntGoods.BaseValue(AnimalKind.Deer, QuarryKind.None), HuntGoods.BaseValue(AnimalKind.Game, QuarryKind.Wolf));
        Assert.Less(HuntGoods.BaseValue(AnimalKind.Large, QuarryKind.None), HuntGoods.BaseValue(AnimalKind.Legendary, QuarryKind.None));
    }

    // ---------------------------------------------------------------- condition from the shot

    [Test]
    public void Condition_FollowsTheKillingShot()
    {
        Assert.AreEqual(PeltCondition.Pristine, HuntGoods.ConditionFromShot("Head", HuntWeapon.Sniper, 150f, 1));
        Assert.AreEqual(PeltCondition.Good, HuntGoods.ConditionFromShot("Chest", HuntWeapon.Rifle, 80f, 1));
        Assert.AreEqual(PeltCondition.Fair, HuntGoods.ConditionFromShot("Body", HuntWeapon.Rifle, 80f, 1));
        Assert.AreEqual(PeltCondition.Torn, HuntGoods.ConditionFromShot("Chest", HuntWeapon.Shotgun, 12f, 1));
        Assert.AreEqual(PeltCondition.Fair, HuntGoods.ConditionFromShot("Chest", HuntWeapon.Shotgun, 40f, 1));
    }

    [Test]
    public void ManyHits_CostTheHideAGrade()
    {
        Assert.AreEqual(PeltCondition.Good, HuntGoods.ConditionFromShot("Head", HuntWeapon.Rifle, 50f, 3));
        Assert.AreEqual(PeltCondition.Torn, HuntGoods.ConditionFromShot("Chest", HuntWeapon.Shotgun, 10f, 5));
    }

    // ---------------------------------------------------------------- freshness

    [Test]
    public void Freshness_HoldsThenSlidesThenFloors()
    {
        Assert.AreEqual(1f, HuntGoods.FreshnessAt(0f), 0.001f);
        Assert.AreEqual(1f, HuntGoods.FreshnessAt(HuntGoods.FreshSeconds), 0.001f);
        float mid = (HuntGoods.FreshSeconds + HuntGoods.SpoilSeconds) * 0.5f;
        Assert.AreEqual(0.7f, HuntGoods.FreshnessAt(mid), 0.001f);
        Assert.AreEqual(0.4f, HuntGoods.FreshnessAt(HuntGoods.SpoilSeconds + 999f), 0.001f);
    }

    [Test]
    public void SkinnedPelt_KeepsItsValue()
    {
        HuntGoods g = Deer();
        g.Skin(Time.time, false, out _);
        int now = g.PeltValue(Time.time);
        int later = g.PeltValue(Time.time + HuntGoods.SpoilSeconds * 3f);
        Assert.AreEqual(now, later);
    }

    // ---------------------------------------------------------------- the full loop

    [Test]
    public void CarcassToTannerToCoins()
    {
        inventory.TakeCarcass(CarcassOf(Deer(AnimalTrophy.Tier.Trophy, PeltCondition.Pristine, 90f, 4)));
        Assert.AreEqual(1, inventory.CarcassCount);
        Assert.AreEqual(90f, inventory.CarryKg, 0.01f);

        int skinned = inventory.SkinAllCarcasses(out int meat);
        Assert.AreEqual(1, skinned);
        Assert.AreEqual(4, meat, "The tanner keeps all the meat");
        Assert.AreEqual(4, inventory.meat);
        Assert.AreEqual(0, inventory.CarcassCount);
        Assert.AreEqual(1, inventory.PeltCount);
        Assert.AreEqual(0f, inventory.CarryKg, 0.01f);

        int earned = inventory.SellAllPelts(out int sold);
        Assert.AreEqual(1, sold);
        Assert.AreEqual(54, earned);
        Assert.AreEqual(54, inventory.coins);
        Assert.AreEqual(0, inventory.PeltCount);
    }

    [Test]
    public void SkinningByHand_GivesLessMeatAndACheaperPelt()
    {
        inventory.FieldSkin(CarcassOf(Deer(meat: 5)));
        Assert.AreEqual(1, inventory.PeltCount);
        Assert.AreEqual(3, inventory.meat, "60% of 5, rounded up");
        Assert.AreEqual(12, inventory.TotalPeltValue(), "18 x 0.65, rounded");
    }

    [Test]
    public void TooHeavy_CarcassStaysInTheWorld()
    {
        AnimalCarcass huge = CarcassOf(Deer(kg: Inventory.MaxCarryKg + 10f));
        inventory.TakeCarcass(huge);
        Assert.AreEqual(0, inventory.CarcassCount);
        Assert.IsTrue(huge != null, "A carcass that could not be taken is left where it lay");
        Object.DestroyImmediate(huge.gameObject);
    }

    [Test]
    public void HeavyLoad_SlowsAndStopsSprinting()
    {
        Assert.AreEqual(1f, Inventory.CarrySpeedMultiplier, 0.001f);
        inventory.TakeCarcass(CarcassOf(Deer(kg: 140f)));
        Assert.Less(Inventory.CarrySpeedMultiplier, 1f);
        Assert.IsTrue(Inventory.TooHeavyToSprint);
    }
}

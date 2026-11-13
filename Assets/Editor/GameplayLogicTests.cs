using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameplayLogicTests
{
    GameObject root;
    Inventory inventory;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("InventoryLogicTest");
        root.SetActive(false);
        inventory = root.AddComponent<Inventory>();
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(root); }

    [Test]
    public void WoodPickup_CollectsOnlyWhatFitsAndCannotDuplicateItems()
    {
        inventory.CollectItem("Wood", 1599);
        InventoryWorldDrop log = root.AddComponent<InventoryWorldDrop>();
        log.itemId = "Wood";
        log.amount = 3;
        Assert.AreEqual(1, log.CollectAvailableWood(inventory));
        Assert.AreEqual(1600, inventory.wood);
        Assert.AreEqual(2, log.amount);
        Assert.AreEqual(0, log.CollectAvailableWood(inventory));
        Assert.AreEqual(2, log.amount);
        Assert.IsTrue(inventory.UseWood(2));
        Assert.AreEqual(2, log.CollectAvailableWood(inventory));
        Assert.AreEqual(0, log.amount);
        Assert.AreEqual(0, log.CollectAvailableWood(inventory));
        Assert.AreEqual(1600, inventory.wood);
    }

    [Test]
    public void WoodOnGround_DoesNotSpinOrCollectWithoutInteraction()
    {
        Inventory previousInventory = Inventory.Instance;
        try
        {
            Inventory.Instance = inventory;
            InventoryWorldDrop log = root.AddComponent<InventoryWorldDrop>();
            log.itemId = "Wood";
            log.amount = 1;
            Vector3 position = root.transform.position;
            Quaternion rotation = root.transform.rotation;
            typeof(InventoryWorldDrop).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(log, null);
            Assert.AreEqual(1, log.amount);
            Assert.AreEqual(0, inventory.wood);
            Assert.AreEqual(position, root.transform.position);
            Assert.AreEqual(rotation, root.transform.rotation);
        }
        finally { Inventory.Instance = previousInventory; }
    }

    [Test]
    public void UnpausingAfterDeath_KeepsTheWorldStopped()
    {
        PlayerHealth previousHealth = PlayerHealth.Instance;
        bool paused = GamePause.Active;
        float clock = Time.timeScale;
        bool audio = AudioListener.pause;
        try
        {
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            PlayerHealth.Instance = health;
            GamePause.Set(true);
            typeof(PlayerHealth).GetProperty("isDead").SetValue(health, true);
            GamePause.Set(false);
            Assert.IsFalse(GamePause.Active);
            Assert.AreEqual(0f, Time.timeScale);
        }
        finally
        {
            PlayerHealth.Instance = previousHealth;
            GamePause.Set(paused);
            Time.timeScale = clock;
            AudioListener.pause = audio;
        }
    }

    [Test]
    public void FullBag_BirdPickupKeepsUncollectedMeat()
    {
        Inventory previousInventory = Inventory.Instance;
        PlayerHealth previousHealth = PlayerHealth.Instance;
        try
        {
            Inventory.Instance = inventory;
            PlayerHealth.Instance = null;
            inventory.CollectItem("Wood", 1600);
            FallenBird bird = root.AddComponent<FallenBird>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FallenBird).GetField("landed", flags).SetValue(bird, true);
            typeof(FallenBird).GetField("meat", flags).SetValue(bird, 2);
            typeof(FallenBird).GetField("player", flags).SetValue(bird, root.transform);
            typeof(FallenBird).GetMethod("Update", flags).Invoke(bird, null);
            Assert.AreEqual(2, typeof(FallenBird).GetField("meat", flags).GetValue(bird));
            Assert.AreEqual(0, inventory.meat);
            inventory.UseWood(64);
            inventory.CollectItem("RawMeat", 63);
            typeof(FallenBird).GetMethod("Update", flags).Invoke(bird, null);
            Assert.AreEqual(1, typeof(FallenBird).GetField("meat", flags).GetValue(bird));
            Assert.AreEqual(64, inventory.meat);
        }
        finally { Inventory.Instance = previousInventory; PlayerHealth.Instance = previousHealth; }
    }

    [Test]
    public void FailedFoodSpend_DoesNotConsumePartialPayment()
    {
        inventory.CollectItem("RawMeat", 1);
        inventory.CollectItem("CookedMeat", 1);
        inventory.CollectItem("SpoiledMeat", 1);
        Assert.IsFalse(inventory.SpendRaw(2));
        Assert.IsFalse(inventory.SpendCooked(2));
        Assert.IsFalse(inventory.RemoveSpoiledMeat(2));
        Assert.AreEqual(1, inventory.meat);
        Assert.AreEqual(1, inventory.cookedMeat);
        Assert.AreEqual(1, inventory.spoiledMeat);
        Assert.IsFalse(inventory.SpendRaw(0));
        Assert.IsFalse(inventory.SpendCooked(-1));
        Assert.IsTrue(inventory.SpendRaw(1));
        Assert.IsTrue(inventory.SpendCooked(1));
        Assert.IsTrue(inventory.RemoveSpoiledMeat(1));
    }

    [Test]
    public void RivalRelationship_ClampsTrustAndAllowsReconciliation()
    {
        Assert.AreEqual(100, RivalHunter.AdjustedTrust(90, 35));
        Assert.AreEqual(-100, RivalHunter.AdjustedTrust(-90, -20));
        Assert.AreEqual(30, RivalHunter.AdjustedTrust(-5, 35));
        Assert.AreEqual(-25, RivalHunter.AdjustedTrust(-10, -15));
    }

    [Test]
    public void RivalKill_DoesNotAwardPlayerKillsOrExperience()
    {
        Animal animal = root.AddComponent<Animal>();
        animal.kind = AnimalKind.Deer;
        int kills = HuntFeedback.Kills;
        int xp = HuntProgress.Xp;
        animal.RivalKill(Vector3.forward);
        Assert.IsTrue(animal.Dead);
        Assert.AreEqual(kills, HuntFeedback.Kills);
        Assert.AreEqual(xp, HuntProgress.Xp);
        Assert.IsNotNull(root.GetComponent<AnimalCarcass>());
        animal.RivalKill(Vector3.forward);
        Assert.AreEqual(1, root.GetComponents<AnimalCarcass>().Length);
    }

    [Test]
    public void AxeBladeSweep_MissesWhenOnlyCrosshairWouldHit()
    {
        Vector3 origin = new Vector3(10000f, 10000f, 10000f);
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            trunk.transform.position = origin + Vector3.forward;
            Physics.SyncTransforms();
            Assert.IsFalse(AxeSwing.TryBladeSurface(origin + Vector3.right * 2f,
                origin + Vector3.right * 2f + Vector3.forward * 2f, null, null, out _));
            Assert.IsTrue(AxeSwing.TryBladeSurface(origin, origin + Vector3.forward * 2f, null, null, out RaycastHit hit));
            Assert.AreEqual(trunk.GetComponent<Collider>(), hit.collider);
            Assert.That(hit.point.z, Is.EqualTo(trunk.transform.position.z - 0.5f).Within(0.02f));
        }
        finally { Object.DestroyImmediate(trunk); }
    }

    [Test]
    public void AxeBladeSweep_WallBlocksTreeAndOwnerIsIgnored()
    {
        Vector3 origin = new Vector3(10000f, 10000f, 10000f);
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = origin + Vector3.forward;
            trunk.transform.position = origin + Vector3.forward * 3f;
            Physics.SyncTransforms();
            Assert.IsTrue(AxeSwing.TryBladeSurface(origin, origin + Vector3.forward * 4f, null, null, out RaycastHit hit));
            Assert.AreEqual(wall.GetComponent<Collider>(), hit.collider);
            Assert.IsTrue(AxeSwing.TryBladeSurface(origin, origin + Vector3.forward * 4f, wall.transform, null, out hit));
            Assert.AreEqual(trunk.GetComponent<Collider>(), hit.collider);
            Assert.IsFalse(AxeSwing.TryBladeSurface(origin, origin, null, null, out _));
        }
        finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(trunk); }
    }

    [Test]
    public void FullInventory_RejectsPickupWithoutClaimingItWasCollected()
    {
        Assert.AreEqual(1600, inventory.CollectItem("Wood", 1600));
        Assert.AreEqual(0, inventory.CollectItem("RawMeat", 8));
        Assert.AreEqual(1600, inventory.wood);
        Assert.AreEqual(0, inventory.meat);
    }

    [Test]
    public void PartialPickup_ReportsOnlyWhatFits()
    {
        inventory.CollectItem("Wood", 1598);
        Assert.AreEqual(2, inventory.CollectItem("Wood", 9));
        Assert.AreEqual(1600, inventory.wood);
    }

    [Test]
    public void FailedTrade_KeepsMeatWhenPaymentDoesNotFit()
    {
        inventory.CollectItem("RawMeat", 1600);
        Assert.IsFalse(inventory.TrySellMeatForWood(1));
        Assert.AreEqual(1600, inventory.meat);
        Assert.AreEqual(0, inventory.wood);
    }

    [Test]
    public void CameraCollision_DoesNotForceCameraPastACloseWall()
    {
        var views = root.AddComponent<CameraViewSwitcher>();
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Vector3 pivot = new Vector3(10000f, 10000f, 10000f);
            wall.transform.position = pivot + Vector3.back * 0.55f;
            wall.transform.localScale = new Vector3(2f, 2f, 0.05f);
            Physics.SyncTransforms();
            Vector3 result = (Vector3)typeof(CameraViewSwitcher).GetMethod("ResolveCollision", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(views, new object[] { pivot, pivot + Vector3.back * 4f });
            Assert.Less(Vector3.Distance(pivot, result), 0.5f);
        }
        finally { Object.DestroyImmediate(wall); }
    }

    [Test]
    public void SpoiledPickup_StaysSpoiled()
    {
        Assert.AreEqual(4, inventory.CollectItem("SpoiledMeat", 4));
        Assert.AreEqual(4, inventory.spoiledMeat);
        Assert.AreEqual(0, inventory.meat);
        Assert.AreEqual(0, inventory.CollectItem("Unknown", 1));
    }

    [Test]
    public void PelletCollision_IgnoresPlayerWithoutSkippingNearbyWall()
    {
        var gun = root.AddComponent<HuntingGame.Ballistics.BallisticsGun>();
        GameObject owner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            owner.transform.position = origin + Vector3.forward * 0.3f;
            owner.transform.localScale = Vector3.one * 0.1f;
            wall.transform.position = origin + Vector3.forward * 0.48f;
            wall.transform.localScale = new Vector3(2f, 2f, 0.05f);
            Physics.SyncTransforms();
            object[] args = { origin, Vector3.forward, 2f, owner.transform, default(RaycastHit) };
            bool found = (bool)typeof(HuntingGame.Ballistics.BallisticsGun).GetMethod("Cast", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gun, args);
            Assert.IsTrue(found);
            Assert.AreEqual(wall.GetComponent<Collider>(), ((RaycastHit)args[4]).collider);
        }
        finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(wall); }
    }

    [Test]
    public void LegWound_StopsAtImpactAndNeverRecoversFromABodyHit()
    {
        Animal animal = root.AddComponent<Animal>();
        var placeType = typeof(Animal).GetNestedType("ShotPlace", BindingFlags.NonPublic);
        var wound = typeof(Animal).GetMethod("BeginWound", BindingFlags.Instance | BindingFlags.NonPublic);
        var tick = typeof(Animal).GetMethod("TickWound", BindingFlags.Instance | BindingFlags.NonPublic);
        wound.Invoke(animal, new object[] { System.Enum.Parse(placeType, "Legs"), Vector3.zero });
        Assert.AreEqual(0f, animal.WoundMoveScale);
        tick.Invoke(animal, new object[] { 1f });
        float legSpeed = animal.WoundMoveScale;
        Assert.LessOrEqual(legSpeed * animal.fleeSpeed, 0.301f);
        wound.Invoke(animal, new object[] { System.Enum.Parse(placeType, "Body"), Vector3.zero });
        tick.Invoke(animal, new object[] { 1f });
        Assert.AreEqual(legSpeed, animal.WoundMoveScale);
    }
}

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

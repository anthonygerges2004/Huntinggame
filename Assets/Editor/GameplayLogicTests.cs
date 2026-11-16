using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameplayLogicTests
{
    [Test]
    public void Grounding_RepairsElevatedIdleRootAndModelOffsetLargerThanOldLimit()
    {
        TerrainData data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
        GameObject terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.position = new Vector3(200000f, 4f, 200000f);
        GameObject walker = new GameObject("ElevatedIdleDeer");
        walker.SetActive(false);
        try
        {
            Animal animal = walker.AddComponent<Animal>();
            animal.kind = AnimalKind.Deer;
            walker.transform.position = new Vector3(200016f, 29f, 200016f);
            Transform model = new GameObject(AnimalVisuals.ModelChildName).transform;
            model.SetParent(walker.transform, false);
            model.localPosition = Vector3.up * 7f;
            Transform hoof = new GameObject("toe.L").transform;
            hoof.SetParent(model, false);
            AnimalGrounding grounding = walker.AddComponent<AnimalGrounding>();
            InvokeLifecycle(grounding, "Start");
            InvokeLifecycle(grounding, "LateUpdate");
            Assert.AreEqual(4f, walker.transform.position.y, 0.01f);
            Assert.AreEqual(4.03f, hoof.position.y, 0.04f,
                "A model offset above 2.5 metres must not remain permanently airborne.");
            animal.kind = AnimalKind.Bird;
            walker.transform.position += Vector3.up * 20f;
            AnimalGrounding.GroundLivingRoot(animal);
            Assert.AreEqual(24f, walker.transform.position.y, 0.01f, "Bird flight must be preserved.");
        }
        finally
        {
            Object.DestroyImmediate(walker);
            Object.DestroyImmediate(terrain);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void Grounding_InterpolatedModelFollowsSlopeInsteadOfWalkingAboveIt()
    {
        TerrainData data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
        float[,] heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) heights[z, x] = x / 32f;
        data.SetHeights(0, 0, heights);
        GameObject terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.position = new Vector3(100000f, 0f, 100000f);
        GameObject walker = new GameObject("SlopeWalker");
        try
        {
            walker.transform.position = new Vector3(100020f, 12.5f, 100016f);
            Transform model = new GameObject(AnimalVisuals.ModelChildName).transform;
            model.SetParent(walker.transform, false);
            Transform hoof = new GameObject("toe.L").transform;
            hoof.SetParent(model, false);
            AnimalGrounding grounding = walker.AddComponent<AnimalGrounding>();
            InvokeLifecycle(grounding, "Start");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(AnimalGrounding).GetField("smoothed", flags).SetValue(grounding, true);
            typeof(AnimalGrounding).GetField("smoothRoot", flags).SetValue(grounding, new Vector3(100008f, 12.5f, 100016f));
            InvokeLifecycle(grounding, "LateUpdate");
            Assert.IsTrue(WorldGround.TryGetGround(hoof.position, out Vector3 ground, out _));
            Assert.AreEqual(ground.y + 0.03f, hoof.position.y, 0.04f,
                "The displayed hoof must follow terrain at its smoothed XZ, even beyond the close-detail range.");
        }
        finally
        {
            Object.DestroyImmediate(walker);
            Object.DestroyImmediate(terrain);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void BirdFlight_OnGroundAnimalCannotRaiseItsPosition()
    {
        GameObject walker = new GameObject("GroundAnimalWithFlightComponent");
        walker.SetActive(false);
        try
        {
            walker.AddComponent<Animal>().kind = AnimalKind.Deer;
            BirdFlight flight = walker.AddComponent<BirdFlight>();
            Vector3 before = walker.transform.position;
            InvokeLifecycle(flight, "Start");
            Assert.IsFalse(flight.enabled);
            Assert.AreEqual(before, walker.transform.position);
        }
        finally { Object.DestroyImmediate(walker); }
    }

    static void InvokeLifecycle(MonoBehaviour component, string method)
    {
        // SendMessage skips inactive objects; these fixtures deliberately avoid
        // activating Animal.Start and its unrelated scene/gameplay initialization.
        component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }

    [Test]
    public void Grounding_ImportedPlacementOffsetCannotSeparateModelFromAnimal()
    {
        TerrainData data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
        GameObject terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.position = new Vector3(300000f, 4f, 300000f);
        GameObject walker = new GameObject("OffsetDeer");
        walker.SetActive(false);
        try
        {
            walker.AddComponent<Animal>().kind = AnimalKind.Deer;
            walker.transform.SetPositionAndRotation(new Vector3(300016f, 4f, 300016f), Quaternion.Euler(0f, 90f, 0f));
            Transform container = new GameObject("ImportedVisual").transform;
            container.SetParent(walker.transform, false);
            container.localPosition = new Vector3(20f, 7f, -13f);
            Transform rig = new GameObject("Rig").transform;
            rig.SetParent(container, false);
            rig.gameObject.AddComponent<Animator>();
            Transform hoof = new GameObject("toe.L").transform;
            hoof.SetParent(rig, false);
            AnimalGrounding grounding = walker.AddComponent<AnimalGrounding>();
            InvokeLifecycle(grounding, "Start");
            InvokeLifecycle(grounding, "LateUpdate");
            Assert.AreEqual(walker.transform.position.x, hoof.position.x, 0.04f);
            Assert.AreEqual(walker.transform.position.z, hoof.position.z, 0.04f);
            Assert.AreEqual(4.03f, hoof.position.y, 0.04f);
            walker.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            InvokeLifecycle(grounding, "LateUpdate");
            Assert.AreEqual(walker.transform.position.x, hoof.position.x, 0.04f);
            Assert.AreEqual(walker.transform.position.z, hoof.position.z, 0.04f);
        }
        finally { Object.DestroyImmediate(walker); Object.DestroyImmediate(terrain); Object.DestroyImmediate(data); }
    }

    [Test]
    public void Grounding_RecoversImmediatelyFromLargeAnimationHeightChangesInBothDirections()
    {
        TerrainData data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
        GameObject terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.position = new Vector3(400000f, 4f, 400000f);
        GameObject walker = new GameObject("AnimatedDeer");
        walker.SetActive(false);
        try
        {
            walker.AddComponent<Animal>().kind = AnimalKind.Deer;
            walker.transform.position = new Vector3(400016f, 4f, 400016f);
            Transform model = new GameObject(AnimalVisuals.ModelChildName).transform;
            model.SetParent(walker.transform, false);
            Transform hoof = new GameObject("toe.L").transform;
            hoof.SetParent(model, false);
            AnimalGrounding grounding = walker.AddComponent<AnimalGrounding>();
            InvokeLifecycle(grounding, "Start");
            foreach (float height in new[] { 8f, -8f, 0f })
            {
                hoof.localPosition = Vector3.up * height;
                InvokeLifecycle(grounding, "LateUpdate");
                Assert.AreEqual(4.03f, hoof.position.y, 0.04f, "Failed to correct pose height " + height);
            }
        }
        finally { Object.DestroyImmediate(walker); Object.DestroyImmediate(terrain); Object.DestroyImmediate(data); }
    }

    [Test]
    public void Grounding_FallenBodySettlesOnSlopesWithoutKeepingStandingHeightOffset()
    {
        TerrainData data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 8f, 32f) };
        float[,] heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++) heights[z, x] = x / 32f;
        data.SetHeights(0, 0, heights);
        GameObject terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.position = new Vector3(500000f, 4f, 500000f);
        GameObject walker = new GameObject("FallenDeer");
        walker.SetActive(false);
        try
        {
            Animal animal = walker.AddComponent<Animal>();
            animal.kind = AnimalKind.Deer;
            walker.transform.position = new Vector3(500016f, 8.26f, 500016f);
            Transform model = new GameObject(AnimalVisuals.ModelChildName).transform;
            model.SetParent(walker.transform, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(model, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.6f, 1f, 2f);
            AnimalGrounding grounding = walker.AddComponent<AnimalGrounding>();
            InvokeLifecycle(grounding, "Start");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Animal).GetField("dead", flags).SetValue(animal, true);
            Vector3 rootPosition = walker.transform.position;
            foreach (float roll in new[] { 46f, -78f, 86f })
            {
                walker.transform.rotation = Quaternion.Euler(8f, 35f, roll);
                typeof(AnimalGrounding).GetField("shift", flags).SetValue(grounding, 2.5f);
                InvokeLifecycle(grounding, "LateUpdate");
                float gap = float.PositiveInfinity;
                foreach (Vector3 vertex in body.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    Vector3 world = body.transform.TransformPoint(vertex);
                    Assert.IsTrue(WorldGround.TryGetGround(world, out Vector3 ground, out _));
                    gap = Mathf.Min(gap, world.y - ground.y);
                }
                Assert.AreEqual(0.03f, gap, 0.04f, "Carcass must touch the terrain after roll " + roll);
                Assert.AreEqual(rootPosition, walker.transform.position, "Keep the death path and loot origin intact.");
                Vector3 settled = model.position;
                InvokeLifecycle(grounding, "LateUpdate");
                Assert.AreEqual(settled, model.position, "A settled carcass must not drift.");
            }
        }
        finally { Object.DestroyImmediate(walker); Object.DestroyImmediate(terrain); Object.DestroyImmediate(data); }
    }

    [Test]
    public void Grounding_DistanceSuspendedAnimalStillFinishesItsDeathFall()
    {
        GameObject body = new GameObject("DistantShotDeer");
        body.SetActive(false);
        try
        {
            Animal animal = body.AddComponent<Animal>();
            animal.SetSuspendSelfTick(true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Animal).GetField("dead", flags).SetValue(animal, true);
            typeof(Animal).GetField("dropping", flags).SetValue(animal, true);
            typeof(Animal).GetField("dropTime", flags).SetValue(animal, 1f);
            typeof(Animal).GetField("fallSeconds", flags).SetValue(animal, 1f);
            typeof(Animal).GetField("standRotation", flags).SetValue(animal, Quaternion.identity);
            InvokeLifecycle(animal, "Update");
            Assert.IsFalse((bool)typeof(Animal).GetField("dropping", flags).GetValue(animal),
                "Suspended AI must not freeze the death animation mid-fall.");
        }
        finally { Object.DestroyImmediate(body); }
    }

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
    public void Cooking_LastRawMeatUsesTheSlotItFrees()
    {
        inventory.CollectItem("Wood", 24 * 64);
        inventory.CollectItem("RawMeat", 1);
        Assert.IsTrue(inventory.CookOneMeat());
        Assert.AreEqual(0, inventory.meat);
        Assert.AreEqual(1, inventory.cookedMeat);
    }

    [Test]
    public void Cooking_FullBagPreservesRawMeatWhenNoSlotIsFreed()
    {
        inventory.CollectItem("Wood", 24 * 64);
        inventory.CollectItem("RawMeat", 2);
        Assert.IsFalse(inventory.CookOneMeat());
        Assert.AreEqual(2, inventory.meat);
        Assert.AreEqual(0, inventory.cookedMeat);
    }

    [Test]
    public void MeatBait_DoesNotAttractHerbivores()
    {
        Assert.IsFalse(MeatBait.EatsMeatBait(AnimalKind.Deer));
        Assert.IsFalse(MeatBait.EatsMeatBait(AnimalKind.Game, QuarryKind.Rabbit));
        Assert.IsTrue(MeatBait.EatsMeatBait(AnimalKind.Game, QuarryKind.Wolf));
        Assert.IsTrue(MeatBait.EatsMeatBait(AnimalKind.Game, QuarryKind.Boar));
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

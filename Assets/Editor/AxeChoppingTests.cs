using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Contact tests use isolated colliders and a blade marker; no keyboard, camera,
// scene player, or coroutines are required to exercise cancellation and blocking.
public class AxeChoppingTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly Vector3 origin = new Vector3(19700f, 18300f, 17900f);
    GameObject root;
    GameObject lockedTreeObject;
    GameObject blocker;
    AxeSwing axe;
    HarvestableTree lockedTree;
    AxeSwing previousInstance;
    bool previousHasAxe;

    [SetUp]
    public void SetUp()
    {
        previousInstance = AxeSwing.Instance;
        previousHasAxe = AxeSwing.HasAxe;
        root = new GameObject("ChoppingSessionTest");
        root.SetActive(false);
        root.transform.position = origin;
        axe = root.AddComponent<AxeSwing>();
        var blade = new GameObject("Blade contact");
        blade.transform.SetParent(root.transform, false);
        blade.transform.localPosition = Vector3.forward * 2f;
        axe.bladeContact = blade.transform;
        lockedTreeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lockedTreeObject.name = "Locked tree";
        lockedTreeObject.transform.position = origin + Vector3.forward;
        lockedTree = lockedTreeObject.AddComponent<HarvestableTree>();
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        // The inactive test axe must never take ownership of the live player's pose.
        AxeSwing.Instance = previousInstance;
        AxeSwing.HasAxe = previousHasAxe;
        if (root != null) Object.DestroyImmediate(root);
        if (lockedTreeObject != null) Object.DestroyImmediate(lockedTreeObject);
        if (blocker != null) Object.DestroyImmediate(blocker);
    }

    void SeedStrike()
    {
        Set(axe, "swinging", true);
        Set(axe, "strikeActive", true);
        Set(axe, "sessionSwing", true);
        Set(axe, "repeatChop", true);
        Set(axe, "choppingTarget", lockedTree);
        Set(axe, "swingTarget", lockedTree);
        Set(axe, "previousBlade", origin);
    }

    static void Set(object target, string name, object value)
    {
        typeof(AxeSwing).GetField(name, Private).SetValue(target, value);
    }

    static int CutCount(HarvestableTree tree)
    {
        return ((ICollection)typeof(HarvestableTree).GetField("cuts", Private).GetValue(tree)).Count;
    }

    bool CanContinue()
    {
        return (bool)typeof(AxeSwing).GetMethod("CanContinueChopping", Private).Invoke(axe, null);
    }

    void WithReadyPlayer(System.Action<Camera> check)
    {
        var restore = new List<System.Action>();
        void Property(System.Type type, string name, object value)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object previous = property.GetValue(null);
            restore.Add(() => property.SetValue(null, previous));
            property.SetValue(null, value);
        }
        void Field(System.Type type, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object previous = field.GetValue(null);
            restore.Add(() => field.SetValue(null, previous));
            field.SetValue(null, value);
        }
        try
        {
            Inventory inventory = root.AddComponent<Inventory>();
            inventory.CollectItem("Axe", 1);
            inventory.selectedHotbar = 0;
            CameraViewSwitcher views = root.AddComponent<CameraViewSwitcher>();
            var cameraObject = new GameObject("Isolated chopping camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            typeof(CameraViewSwitcher).GetField("playerCamera", Private).SetValue(views, camera);
            typeof(CameraViewSwitcher).GetField("isFirstPerson", Private).SetValue(views, true);
            Property(typeof(CameraViewSwitcher), "Instance", views);
            Field(typeof(Inventory), "Instance", inventory);
            Field(typeof(PlayerHealth), "Instance", null);
            Field(typeof(AxeSwing), "Instance", axe);
            Field(typeof(AxeSwing), "HasAxe", true);
            Field(typeof(PlayerMove), "IsMoving", false);
            Field(typeof(PlayerMove), "IsSprinting", false);
            Field(typeof(WoodBuilder), "IsBuilding", false);
            Property(typeof(GamePause), "Active", false);
            Property(typeof(CampfireBuild), "Busy", false);
            Property(typeof(BirdChallenge), "Active", false);
            Property(typeof(PlayerShotgun), "Instance", null);
            Property(typeof(RideableHorse), "Mounted", null);
            Property(typeof(MountainHermit), "Instance", null);
            Property(typeof(AnimalCarcass), "Focus", null);
            Property(typeof(InventoryWorldDrop), "WoodFocus", null);
            Field(typeof(InventoryWorldDrop), "focusFrame", -1);
            Physics.SyncTransforms();
            Assert.AreSame(lockedTree, HarvestableTree.AimedTree(), "The isolated camera must point at the test tree.");
            Assert.IsTrue(Inventory.AxeInHand, "The isolated player must have an axe equipped.");
            check(camera);
        }
        finally
        {
            axe.StopChopping();
            for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
        }
    }

    [Test]
    public void EChopping_StartsOnAnAimedTreeAndRejectsContinuationAfterLookingAway()
    {
        WithReadyPlayer(camera =>
        {
            Assert.IsTrue(axe.StartChopping(lockedTree));
            Assert.IsTrue(axe.IsChopping);
            Assert.IsFalse(axe.IsSwinging, "Starting the session stores intent; Update owns the swing coroutine.");
            Assert.IsTrue(CanContinue());
            camera.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Assert.IsNull(HarvestableTree.AimedTree());
            Assert.IsFalse(CanContinue(), "A session must not follow a tree outside the player's aim.");
        });
    }

    [Test]
    public void EChopping_RejectsContinuationWhenThePlayerMoves()
    {
        WithReadyPlayer(camera =>
        {
            Assert.IsTrue(axe.StartChopping(lockedTree));
            Assert.IsTrue(CanContinue());
            PlayerMove.IsMoving = true;
            Assert.IsFalse(CanContinue());
            PlayerMove.IsMoving = false;
            Assert.IsTrue(CanContinue());
            root.transform.position += Vector3.right * 0.31f;
            Physics.SyncTransforms();
            Assert.AreSame(lockedTree, HarvestableTree.AimedTree(), "The tree is still in aim; displacement alone must invalidate the action.");
            Assert.IsFalse(CanContinue());
        });
    }

    [Test]
    public void EChopping_RejectsNoTargetWithoutStartingAnAction()
    {
        Assert.IsFalse(axe.StartChopping(null));
        Assert.IsFalse(axe.IsChopping);
        Assert.IsFalse(axe.IsSwinging);
    }

    [Test]
    public void StoppingBeforeContact_PreventsThePendingBladeFromCutting()
    {
        SeedStrike();
        Assert.IsTrue(AxeSwing.TryBladeSurface(origin, axe.bladeContact.position, null, root.transform, out _),
            "The pending blade path must actually intersect the tree for this cancellation check.");
        axe.StopChopping();
        axe.TryTreeContact();
        Assert.IsFalse(axe.IsChopping);
        Assert.AreEqual(0, CutCount(lockedTree));
        Assert.IsFalse((bool)typeof(AxeSwing).GetField("treeContact", Private).GetValue(axe));
    }

    [Test]
    public void ATreeStartingToFall_EndsTheSessionBeforeAnotherCut()
    {
        SeedStrike();
        typeof(HarvestableTree).GetField("chopped", Private).SetValue(lockedTree, true);
        Assert.IsFalse(lockedTree.CanBeChopped);
        axe.TryTreeContact();
        Assert.IsFalse(axe.IsChopping);
        Assert.AreEqual(0, CutCount(lockedTree));
    }

    [Test]
    public void AnotherTreeInTheBladePath_BlocksTheLockedTreeWithoutBeingCut()
    {
        SeedStrike();
        lockedTreeObject.transform.position = origin + Vector3.forward * 3f;
        blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.transform.position = origin + Vector3.forward;
        HarvestableTree otherTree = blocker.AddComponent<HarvestableTree>();
        Physics.SyncTransforms();
        Assert.IsTrue(AxeSwing.TryBladeSurface(origin, axe.bladeContact.position, null, root.transform, out RaycastHit hit));
        Assert.AreEqual(blocker.GetComponent<Collider>(), hit.collider);
        axe.TryTreeContact();
        Assert.IsFalse(axe.IsChopping);
        Assert.AreEqual(0, CutCount(lockedTree));
        Assert.AreEqual(0, CutCount(otherTree));
    }

    [Test]
    public void AWallInTheBladePath_StopsRepeatedChopping()
    {
        SeedStrike();
        lockedTreeObject.transform.position = origin + Vector3.forward * 3f;
        blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.transform.position = origin + Vector3.forward;
        Physics.SyncTransforms();
        Assert.IsTrue(AxeSwing.TryBladeSurface(origin, axe.bladeContact.position, null, root.transform, out RaycastHit hit));
        Assert.AreEqual(blocker.GetComponent<Collider>(), hit.collider);
        axe.TryTreeContact();
        Assert.IsFalse(axe.IsChopping);
        Assert.AreEqual(0, CutCount(lockedTree));
    }
}

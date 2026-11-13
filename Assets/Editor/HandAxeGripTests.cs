using NUnit.Framework;
using UnityEngine;

// These checks use finger anatomy, not assumed local axes from either avatar.
public class HandAxeGripTests
{
    GameObject root;

    sealed class HandRig
    {
        public Transform hand, index, middle, little;
    }

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("HandAxeGripTest");
        root.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    HandRig MakeHand(string name, Vector3 position, Quaternion anatomy, Quaternion boneBasis)
    {
        Transform hand = new GameObject(name).transform;
        hand.SetParent(root.transform, false);
        hand.SetPositionAndRotation(position, anatomy * boneBasis);
        var rig = new HandRig { hand = hand };
        rig.index = Finger(hand, "Index proximal", Quaternion.Inverse(boneBasis) * new Vector3(0.025f, 0f, 0.09f));
        rig.middle = Finger(hand, "Middle proximal", Quaternion.Inverse(boneBasis) * new Vector3(0f, 0f, 0.095f));
        rig.little = Finger(hand, "Little proximal", Quaternion.Inverse(boneBasis) * new Vector3(-0.025f, 0f, 0.085f));
        return rig;
    }

    static Transform Finger(Transform hand, string name, Vector3 position)
    {
        Transform finger = new GameObject(name).transform;
        finger.SetParent(hand, false);
        finger.localPosition = position;
        return finger;
    }

    HandAxeGrip MakeAxe(string name, Transform parent = null)
    {
        GameObject axe = new GameObject(name);
        axe.transform.SetParent(parent != null ? parent : root.transform, false);
        return axe.AddComponent<HandAxeGrip>();
    }

    static void Place(HandAxeGrip axe, HandRig rig)
    {
        axe.PlaceOnHand(rig.hand, rig.index, rig.middle, rig.little);
    }

    static Vector3 PalmCenter(HandRig rig)
    {
        Assert.IsTrue(HandAxeGrip.TryPalmFrame(rig.hand, rig.index, rig.middle, rig.little, out Vector3 center, out _));
        return center;
    }

    [Test]
    public void DifferentAvatarBoneAxes_ProduceTheSameWorldGrip()
    {
        Vector3 position = new Vector3(3f, 2f, -4f);
        Quaternion anatomy = Quaternion.Euler(28f, -42f, 17f);
        HandRig source = MakeHand("Source hand", position, anatomy, Quaternion.identity);
        HandRig visible = MakeHand("Visible hand", position, anatomy, Quaternion.Euler(90f, -90f, 0f));
        HandAxeGrip a = MakeAxe("Source axe");
        HandAxeGrip b = MakeAxe("Visible axe");
        Place(a, source);
        Place(b, visible);
        Assert.That(Vector3.Distance(a.transform.position, b.transform.position), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(a.transform.rotation, b.transform.rotation), Is.LessThan(0.02f));
        Assert.That(Vector3.Distance(a.transform.TransformPoint(a.GripLocalPoint), PalmCenter(source)), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(b.transform.TransformPoint(b.GripLocalPoint), PalmCenter(visible)), Is.LessThan(0.0001f));
    }

    [Test]
    public void HandleAndBlade_FollowPalmAnatomyRatherThanWorldUp()
    {
        Quaternion anatomy = Quaternion.Euler(72f, 35f, -28f);
        HandRig rig = MakeHand("Tilted hand", Vector3.one, anatomy, Quaternion.Euler(-90f, 0f, 90f));
        HandAxeGrip axe = MakeAxe("Axe");
        Place(axe, rig);
        Vector3 acrossPalm = (rig.index.position - rig.little.position).normalized;
        Vector3 fingerDirection = Vector3.ProjectOnPlane(rig.middle.position - rig.hand.position, acrossPalm).normalized;
        Assert.That(Vector3.Dot(axe.transform.up, acrossPalm), Is.GreaterThan(0.9999f));
        Assert.That(Vector3.Dot(axe.transform.forward, fingerDirection), Is.GreaterThan(0.9999f));
        Assert.That(Vector3.Distance(axe.transform.TransformPoint(axe.GripLocalPoint), PalmCenter(rig)), Is.LessThan(0.0001f));
    }

    [Test]
    public void OverheadSwing_KeepsTheHandleInThePalmWithoutFlipping()
    {
        HandRig rig = MakeHand("Swinging hand", new Vector3(-2f, 3f, 1f), Quaternion.identity, Quaternion.identity);
        HandAxeGrip axe = MakeAxe("Axe");
        Quaternion previous = Quaternion.identity;
        for (int degrees = 0; degrees <= 360; degrees += 15)
        {
            rig.hand.rotation = Quaternion.Euler(degrees, 0f, 0f);
            Place(axe, rig);
            Assert.That(Vector3.Distance(axe.transform.TransformPoint(axe.GripLocalPoint), PalmCenter(rig)), Is.LessThan(0.0001f));
            if (degrees > 0)
                Assert.That(Quaternion.Angle(previous, axe.transform.rotation), Is.LessThan(15.1f), "Grip flipped at " + degrees + " degrees.");
            previous = axe.transform.rotation;
        }
    }

    [Test]
    public void ScaledAndRotatedParent_DoesNotPullTheGripOutOfThePalm()
    {
        HandRig rig = MakeHand("Hand", new Vector3(4f, -1f, 2f), Quaternion.Euler(31f, 56f, 22f), Quaternion.Euler(0f, 90f, 0f));
        Transform parent = new GameObject("Scaled weapon parent").transform;
        parent.SetParent(root.transform, false);
        parent.SetPositionAndRotation(new Vector3(-3f, 2f, 5f), Quaternion.Euler(43f, -61f, 19f));
        parent.localScale = new Vector3(0.55f, 1.8f, 1.25f);
        rig.hand.SetParent(parent, true);
        HandAxeGrip axe = MakeAxe("Axe", parent);
        Place(axe, rig);
        Assert.That(Vector3.Distance(axe.transform.TransformPoint(axe.GripLocalPoint), PalmCenter(rig)), Is.LessThan(0.0001f));
        parent.localScale = new Vector3(1.4f, 0.7f, 2.1f);
        rig.hand.rotation = Quaternion.Euler(118f, 26f, -37f);
        Place(axe, rig);
        Assert.That(Vector3.Distance(axe.transform.TransformPoint(axe.GripLocalPoint), PalmCenter(rig)), Is.LessThan(0.0001f));
    }

    [Test]
    public void MissingOrCollinearFingerAnatomy_RejectsAnUnstableFrame()
    {
        HandRig rig = MakeHand("Hand", Vector3.zero, Quaternion.identity, Quaternion.identity);
        Assert.IsFalse(HandAxeGrip.TryPalmFrame(null, rig.index, rig.middle, rig.little, out _, out _));
        Assert.IsFalse(HandAxeGrip.TryPalmFrame(rig.hand, null, rig.middle, rig.little, out _, out _));
        rig.index.position = rig.little.position;
        Assert.IsFalse(HandAxeGrip.TryPalmFrame(rig.hand, rig.index, rig.middle, rig.little, out _, out _));
    }
}

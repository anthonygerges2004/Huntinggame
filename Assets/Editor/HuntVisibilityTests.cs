using NUnit.Framework;
using UnityEngine;

// The cull is the one from the visibility talk: outside the view goes,
// and a solid box only hides what it completely covers. A gap stays drawn.
public class HuntVisibilityTests
{
    GameObject cameraObject;
    Camera view;

    [SetUp]
    public void SetUp()
    {
        cameraObject = new GameObject("CullTestCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        view = cameraObject.AddComponent<Camera>();
        view.enabled = true;
        view.fieldOfView = 60f;
        view.nearClipPlane = 0.3f;
        view.farClipPlane = 2000f;
        view.aspect = 16f / 9f;
        view.pixelRect = new Rect(0f, 0f, 1600f, 900f);
        cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
    }

    [TearDown]
    public void TearDown()
    {
        if (cameraObject != null)
            Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void BehindTheCamera_IsOutside_AheadIsKept()
    {
        Plane[] planes = new Plane[6];
        GeometryUtility.CalculateFrustumPlanes(view, planes);

        Assert.IsFalse(HuntVisibility.Outside(planes, new Bounds(new Vector3(0f, 0f, 20f), Vector3.one * 2f)));
        Assert.IsTrue(HuntVisibility.Outside(planes, new Bounds(new Vector3(0f, 0f, -20f), Vector3.one * 2f)));
        Assert.IsTrue(HuntVisibility.Outside(planes, new Bounds(new Vector3(500f, 0f, 20f), Vector3.one * 2f)));
    }

    [Test]
    public void OneWall_HidesWhatItFullyCovers_AndNotTheGap()
    {
        Bounds wall = new Bounds(new Vector3(0f, 0f, 10f), new Vector3(10f, 8f, 1f));
        Bounds behind = new Bounds(new Vector3(0f, 0f, 16f), Vector3.one);
        Bounds beside = new Bounds(new Vector3(12f, 0f, 16f), Vector3.one);

        Assert.IsTrue(HuntVisibility.Occluded(view, behind, wall));
        Assert.IsFalse(HuntVisibility.Occluded(view, beside, wall));

        Bounds left = new Bounds(new Vector3(-6f, 0f, 10f), new Vector3(4f, 8f, 1f));
        Bounds right = new Bounds(new Vector3(6f, 0f, 10f), new Vector3(4f, 8f, 1f));
        Bounds inTheGap = new Bounds(new Vector3(0f, 0f, 16f), Vector3.one);
        Assert.IsFalse(HuntVisibility.Occluded(view, inTheGap, left));
        Assert.IsFalse(HuntVisibility.Occluded(view, inTheGap, right));
    }

    [Test]
    public void CloserObject_IsNotHiddenByAWallBehindIt()
    {
        Bounds wall = new Bounds(new Vector3(0f, 0f, 16f), new Vector3(10f, 8f, 1f));
        Bounds inFront = new Bounds(new Vector3(0f, 0f, 8f), Vector3.one);
        Assert.IsFalse(HuntVisibility.Occluded(view, inFront, wall));
    }

    [Test]
    public void SolidOccluders_AreWallsAndRocks_NotTreesOrPebbles()
    {
        GameObject wall = new GameObject("CastleWall");
        GameObject tree = new GameObject("WildTree");
        wall.hideFlags = HideFlags.HideAndDontSave;
        tree.hideFlags = HideFlags.HideAndDontSave;

        Assert.IsTrue(HuntVisibility.Solid(wall.transform, new Bounds(Vector3.zero, new Vector3(8f, 4f, 0.6f))));
        Assert.IsFalse(HuntVisibility.Solid(tree.transform, new Bounds(Vector3.zero, new Vector3(6f, 12f, 6f))));
        Assert.IsFalse(HuntVisibility.SolidSize(new Bounds(Vector3.zero, new Vector3(0.3f, 0.2f, 0.3f))));
        Assert.IsFalse(HuntVisibility.SolidSize(new Bounds(Vector3.zero, new Vector3(3000f, 80f, 3000f))));

        Object.DestroyImmediate(wall);
        Object.DestroyImmediate(tree);
    }
}

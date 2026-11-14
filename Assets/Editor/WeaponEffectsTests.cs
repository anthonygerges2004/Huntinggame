using NUnit.Framework;
using UnityEngine;

public class WeaponEffectsTests
{
    [TestCase(0f, 0f)]
    [TestCase(0.16f, 0f)]
    [TestCase(0.45f, 1f)]
    [TestCase(0.8f, 0f)]
    [TestCase(1f, 0f)]
    public void MagazineLeavesAndReturnsToItsSeat(float time, float expected)
    {
        Assert.AreEqual(expected, GunReloadVisual.Removal01(time), 0.001f);
    }

    [Test]
    public void ReloadPoseStartsAndEndsNeutral()
    {
        Assert.AreEqual(0f, PlayerShotgun.ReloadPose01(0f));
        Assert.AreEqual(0f, PlayerShotgun.ReloadPose01(1f));
        Assert.AreEqual(1f, PlayerShotgun.ReloadPose01(0.5f));
    }

    [Test]
    public void WaterPlaneFindsFastCrossingAndRejectsParallelOrMissedSegments()
    {
        Assert.IsTrue(WaterBodies.SegmentPlane(Vector3.up * 5f, Vector3.down * 5f, Vector3.zero, Vector3.up, out float t));
        Assert.AreEqual(0.5f, t, 0.0001f);
        Assert.IsFalse(WaterBodies.SegmentPlane(Vector3.up, Vector3.up + Vector3.right, Vector3.zero, Vector3.up, out _));
        Assert.IsFalse(WaterBodies.SegmentPlane(Vector3.up * 5f, Vector3.up * 2f, Vector3.zero, Vector3.up, out _));
    }

    [Test]
    public void LakeWithoutColliderStillReceivesBulletHit()
    {
        GameObject lake = new GameObject("Bullet lake test");
        try
        {
            lake.transform.position = new Vector3(100000f, 0f, 100000f);
            lake.transform.localScale = new Vector3(20f, 1f, 20f);
            lake.AddComponent<LakeWater>();
            Vector3 center = lake.transform.position;
            Assert.IsTrue(WaterBodies.SegmentSurface(center + Vector3.up * 5f, center - Vector3.up * 5f, out Vector3 hit, out _));
            Assert.AreEqual(1f, hit.y, 0.001f);
            Assert.IsFalse(WaterBodies.SegmentSurface(center + Vector3.right * 20f + Vector3.up * 5f,
                center + Vector3.right * 20f - Vector3.up * 5f, out _, out _));
        }
        finally { Object.DestroyImmediate(lake); }
    }

    [Test]
    public void SlopedRiverHitMatchesItsSampledHeight()
    {
        GameObject root = new GameObject("Bullet river test");
        try
        {
            RiverWater river = root.AddComponent<RiverWater>();
            river.points = new[] { new Vector3(200000f, 2f, 200000f), new Vector3(200010f, 4f, 200000f) };
            river.halfWidth = 3f;
            Vector3 p = new Vector3(200005f, 3f, 200000f);
            Assert.IsTrue(WaterBodies.SegmentSurface(p + Vector3.up * 5f, p - Vector3.up * 5f, out Vector3 hit, out _));
            Assert.AreEqual(3f, hit.y, 0.001f);
        }
        finally { Object.DestroyImmediate(root); }
    }
}

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShotBloodTests
{
    GameObject root;
    GameObject ground;
    GameObject animal;
    ShotBlood effect;
    readonly Vector3 origin = new Vector3(10000f, 10000f, 10000f);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("BloodCollisionTest");
        effect = root.AddComponent<ShotBlood>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        if (ground != null) Object.DestroyImmediate(ground);
        if (animal != null) Object.DestroyImmediate(animal);
    }

    bool Sweep(Vector3 start, Vector3 step, out RaycastHit hit)
    {
        object[] args = { start, step, default(RaycastHit) };
        bool found = (bool)typeof(ShotBlood).GetMethod("TrySurface", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, args);
        hit = (RaycastHit)args[2];
        return found;
    }

    [Test]
    public void EmptySpace_DoesNotInventAGroundSplash()
    {
        Assert.IsFalse(Sweep(origin, Vector3.down * 5f, out _));
        Assert.IsFalse(Sweep(origin, Vector3.zero, out _));
    }

    [Test]
    public void FastDrop_HitsActualSlopedSurface()
    {
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = origin + Vector3.down * 2f;
        ground.transform.localScale = new Vector3(8f, 0.1f, 8f);
        ground.transform.rotation = Quaternion.Euler(0f, 0f, 25f);
        Physics.SyncTransforms();
        Assert.IsTrue(Sweep(origin, Vector3.down * 8f, out RaycastHit hit));
        Assert.AreEqual(ground.GetComponent<Collider>(), hit.collider);
        Assert.Greater(Vector3.Dot(hit.normal, ground.transform.up), 0.99f);
    }

    [Test]
    public void AnimalCollider_DoesNotHideTheGroundBehindIt()
    {
        animal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        animal.transform.position = origin + Vector3.down;
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = origin + Vector3.down * 3f;
        typeof(ShotBlood).GetField("animal", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, animal.transform);
        Physics.SyncTransforms();
        Assert.IsTrue(Sweep(origin, Vector3.down * 5f, out RaycastHit hit));
        Assert.AreEqual(ground.GetComponent<Collider>(), hit.collider);
    }

    [Test]
    public void SplashMesh_HasValidLightingNormalsAndTriangles()
    {
        var savedRandom = Random.state;
        Mesh mesh = null;
        try
        {
            mesh = (Mesh)typeof(ShotBlood).GetMethod("MakeSplashMesh", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Assert.AreEqual(29, mesh.vertexCount);
            foreach (Vector3 normal in mesh.normals)
                Assert.Greater(normal.sqrMagnitude, 0.99f);
            foreach (int index in mesh.triangles)
                Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        }
        finally
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            Random.state = savedRandom;
        }
    }
}

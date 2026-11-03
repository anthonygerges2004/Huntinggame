using System;
using System.Collections.Generic;
using System.Reflection;
using HuntingGame.Tracking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

// Edit-mode checks for step timing, the fixed pool, fade, rain, and spawn allocations.
public class TrackTests
{
    GameObject poolObject;
    GameObject animalObject;
    GameObject weatherObject;
    TrackSettings settings;
    TrackPool pool;
    AnimalTracks tracks;
    TrackPool previousPool;
    WorldWeather previousWeather;
    WorldWeather.Kind previousKind;
    bool madeWeather;

    [SetUp]
    public void SetUp()
    {
        previousPool = TrackPool.Current;
        previousWeather = WorldWeather.Instance;
        previousKind = previousWeather != null ? previousWeather.Current : WorldWeather.Kind.Clear;
        if (previousWeather != null)
            previousWeather.SetKind(WorldWeather.Kind.Clear);
        SetCurrent(null);
    }

    [TearDown]
    public void TearDown()
    {
        if (animalObject != null)
            UnityEngine.Object.DestroyImmediate(animalObject);
        if (poolObject != null)
            UnityEngine.Object.DestroyImmediate(poolObject);
        if (settings != null)
            UnityEngine.Object.DestroyImmediate(settings);

        if (madeWeather)
        {
            WorldWeather.Instance = previousWeather;
            if (weatherObject != null)
                UnityEngine.Object.DestroyImmediate(weatherObject);
        }
        else if (previousWeather != null)
        {
            previousWeather.SetKind(previousKind);
        }

        SetCurrent(previousPool);
        poolObject = null;
        animalObject = null;
        weatherObject = null;
        settings = null;
        pool = null;
        tracks = null;
        madeWeather = false;
    }

    [Test]
    public void Footprints_SpawnOnStepInterval_NotEveryMove()
    {
        MakePool(8);
        MakeDeer();
        float interval = pool.StepInterval;
        Assert.Greater(interval, 0.15f);

        const float start = 50f;
        Vector3 home = animalObject.transform.position;
        tracks.ArmAt(start);

        tracks.SampleAt(home, 0f, start);
        Assert.AreEqual(0, pool.ActiveCount, "Standing still should not leave a footprint.");

        Vector3 step = home + new Vector3(3f, 0f, 0f);
        tracks.SampleAt(step, 90f, start);
        Assert.AreEqual(1, pool.ActiveCount, "A step at the interval should leave one footprint.");

        for (int i = 0; i < 8; i++)
        {
            step += new Vector3(3f, 0f, 0f);
            tracks.SampleAt(step, 90f, start + interval * 0.5f);
        }

        Assert.AreEqual(1, pool.ActiveCount, "Moves before the step interval should not spawn another footprint.");

        step += new Vector3(3f, 0f, 0f);
        tracks.SampleAt(step, 90f, start + interval);
        Assert.AreEqual(2, pool.ActiveCount, "The next footprint should spawn once the step interval has elapsed.");
    }

    [Test]
    public void Pool_NeverExceedsMaxActive()
    {
        const int max = 16;
        MakePool(max);
        Assert.AreEqual(max, pool.MaxActive);
        Assert.AreEqual(max, poolObject.transform.childCount);

        for (int i = 0; i < max * 3; i++)
        {
            pool.Place(TrackKind.Footprint, new Vector3(i, 0f, 0f), 10f, 0.3f, 12f, 5f);
            Assert.LessOrEqual(pool.ActiveCount, max);
        }

        Assert.AreEqual(max, pool.ActiveCount, "A full pool should stay full by reusing slots.");
        Assert.AreEqual(max, poolObject.transform.childCount, "Spawning should not create marks beyond the pool.");
    }

    [Test]
    public void OldTracks_FadeAndReturnToThePool()
    {
        MakePool(8);
        const float start = 40f;
        const float life = 10f;
        pool.Place(TrackKind.Footprint, Vector3.zero, 0f, 0.3f, life, start);
        pool.Tick(start);

        float freshAlpha = PrintAlpha();
        float freshScale = PrintScale();
        int children = poolObject.transform.childCount;
        List<EntityId> ids = ChildIds();
        Assert.Greater(freshAlpha, 0.9f);
        Assert.AreEqual(1, pool.ActiveCount);

        pool.Tick(start + 7f);
        Assert.AreEqual(1, pool.ActiveCount, "A track should still be out during its fade.");
        Assert.Less(PrintAlpha(), freshAlpha * 0.7f, "An old track should turn transparent.");
        Assert.Less(PrintScale(), freshScale, "An old track should shrink.");

        pool.Tick(start + life);
        Assert.AreEqual(0, pool.ActiveCount, "An expired track should leave the active set.");
        Assert.AreEqual(children, poolObject.transform.childCount);
        Assert.IsFalse(AnyActiveRoot(), "An expired track should be inactive and waiting in the pool.");

        pool.Place(TrackKind.Footprint, new Vector3(4f, 0f, 0f), 0f, 0.3f, life, start + life);
        Assert.AreEqual(1, pool.ActiveCount);
        Assert.AreEqual(children, poolObject.transform.childCount, "The next track should reuse a pooled object.");
        Assert.IsTrue(ids.Contains(ActiveRootId()));
    }

    [Test]
    public void Rain_SpeedsUpFading()
    {
        const float start = 100f;
        const float life = 40f;

        MakePool(8);
        pool.Place(TrackKind.Footprint, Vector3.zero, 0f, 0.3f, life, start);
        pool.Tick(start + 10f);
        float clearAlpha = PrintAlpha();
        pool.Tick(start + 20f);
        Assert.AreEqual(1, pool.ActiveCount, "Ten seconds of clear weather should not expire a 40 second track.");
        Assert.Greater(clearAlpha, 0.85f);

        MakePool(8);
        UseRain();
        pool.Place(TrackKind.Footprint, Vector3.zero, 0f, 0.3f, life, start);
        pool.Tick(start + 10f);
        float rainAlpha = PrintAlpha();
        Assert.Less(rainAlpha, clearAlpha, "The same ten seconds in rain should fade the track further.");
        Assert.AreEqual(1, pool.ActiveCount);

        pool.Tick(start + 20f);
        Assert.AreEqual(0, pool.ActiveCount, "Rain should finish the fade and return the track to the pool.");
        Assert.AreEqual(8, poolObject.transform.childCount);
        Assert.IsFalse(AnyActiveRoot());
    }

    [Test]
    public void SpawningTracks_DoesNotAllocate()
    {
        MakePool(8);
        int count = pool.MaxActive;
        for (int i = 0; i < count; i++)
            pool.Place(TrackKind.Footprint, SpawnAt(i), 15f, 0.28f, 16f, 80f);
        Assert.AreEqual(count, pool.ActiveCount, "The pool should be full before the allocation measurement.");

        bool counterWorks = AllocationCounterWorks();
        Recorder recorder = Recorder.Get("GC.Alloc");
        bool recorderWorks = recorder != null && recorder.isValid;
        if (recorderWorks)
        {
            recorder.FilterToCurrentThread();
            recorder.enabled = false;
            recorder.enabled = true;
        }

        long before = counterWorks ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        for (int i = 0; i < count; i++)
            pool.Place(TrackKind.Footprint, SpawnAt(i + count), 15f, 0.28f, 16f, 80f);
        long managed = counterWorks ? GC.GetAllocatedBytesForCurrentThread() - before : 0L;

        int samples = 0;
        long recorderBytes = 0L;
        if (recorderWorks)
        {
            samples = recorder.sampleBlockCount;
            recorderBytes = recorder.elapsedNanoseconds;
            recorder.enabled = false;
        }

        Assert.IsTrue(counterWorks || recorderWorks, "This editor build cannot measure GC allocations.");
        if (counterWorks)
            Assert.AreEqual(0L, managed, "Spawning tracks allocated " + managed + " managed bytes.");
        if (recorderWorks)
            Assert.AreEqual(0, samples, "Spawning tracks hit GC.Alloc " + samples + " times (" + recorderBytes + " bytes).");
    }

    [MenuItem("Hunting Game/Run Track Tests")]
    public static void RunFromMenu()
    {
        int failed = RunAll();
        if (failed == 0)
            Debug.Log("Track tests passed: step interval, pool cap, fade, rain, and no spawn allocations.");
        else
            Debug.LogError("Track tests failed: " + failed + ".");
    }

    public static int RunAll()
    {
        int failed = 0;
        failed += Run(t => t.Footprints_SpawnOnStepInterval_NotEveryMove());
        failed += Run(t => t.Pool_NeverExceedsMaxActive());
        failed += Run(t => t.OldTracks_FadeAndReturnToThePool());
        failed += Run(t => t.Rain_SpeedsUpFading());
        failed += Run(t => t.SpawningTracks_DoesNotAllocate());
        return failed;
    }

    static int Run(Action<TrackTests> body)
    {
        TrackTests tests = new TrackTests();
        try
        {
            tests.SetUp();
            body(tests);
            return 0;
        }
        catch (Exception error)
        {
            Debug.LogError(error);
            return 1;
        }
        finally
        {
            tests.TearDown();
        }
    }

    void MakePool(int maxActive)
    {
        if (poolObject != null)
            UnityEngine.Object.DestroyImmediate(poolObject);
        if (settings != null)
            UnityEngine.Object.DestroyImmediate(settings);
        SetCurrent(null);

        settings = ScriptableObject.CreateInstance<TrackSettings>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        settings.maxActive = maxActive;
        settings.poolSize = maxActive;
        settings.stepInterval = 0.55f;
        settings.spacing = 2.2f;
        settings.footprintLifetime = 16f;

        poolObject = new GameObject("TrackTestPool");
        poolObject.hideFlags = HideFlags.HideAndDontSave;
        poolObject.SetActive(false);
        pool = poolObject.AddComponent<TrackPool>();
        FieldInfo field = typeof(TrackPool).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(pool, settings);
        SetCurrent(null);
        Wake(pool);
        if (pool.MaxActive != maxActive)
            throw new InvalidOperationException("Track pool did not build. Max=" + pool.MaxActive);
    }

    static void Wake(MonoBehaviour behaviour)
    {
        MethodInfo awake = behaviour.GetType().GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        awake.Invoke(behaviour, null);
    }

    void MakeDeer()
    {
        animalObject = new GameObject("TrackTestDeer");
        animalObject.hideFlags = HideFlags.HideAndDontSave;
        animalObject.transform.position = new Vector3(25000f, 40f, 25000f);
        Animal animal = animalObject.AddComponent<Animal>();
        animal.kind = AnimalKind.Deer;
        tracks = animalObject.AddComponent<AnimalTracks>();
        Wake(tracks);
    }

    void UseRain()
    {
        if (WorldWeather.Instance == null)
        {
            weatherObject = new GameObject("TrackTestWeather");
            weatherObject.hideFlags = HideFlags.HideAndDontSave;
            weatherObject.SetActive(false);
            WorldWeather.Instance = weatherObject.AddComponent<WorldWeather>();
            madeWeather = true;
        }

        WorldWeather.Instance.SetKind(WorldWeather.Kind.Rain);
    }

    static void SetCurrent(TrackPool value)
    {
        MethodInfo setter = typeof(TrackPool).GetProperty("Current").GetSetMethod(true);
        setter.Invoke(null, new object[] { value });
    }

    static Vector3 SpawnAt(int index)
    {
        return new Vector3(index, 0f, 2f);
    }

    static bool AllocationCounterWorks()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        byte[] probe = new byte[32];
        long delta = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(probe);
        return delta > 0L;
    }

    float PrintAlpha()
    {
        Renderer renderer = ActivePrint();
        Assert.IsNotNull(renderer, "Expected an active footprint.");
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        return block.GetColor("_BaseColor").a;
    }

    float PrintScale()
    {
        Renderer renderer = ActivePrint();
        Assert.IsNotNull(renderer, "Expected an active footprint.");
        return renderer.transform.localScale.magnitude;
    }

    Renderer ActivePrint()
    {
        int count = poolObject.transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform root = poolObject.transform.GetChild(i);
            if (!root.gameObject.activeSelf)
                continue;

            Transform print = root.Find("Print");
            if (print == null || !print.gameObject.activeSelf)
                continue;

            return print.GetComponent<Renderer>();
        }

        return null;
    }

    bool AnyActiveRoot()
    {
        int count = poolObject.transform.childCount;
        for (int i = 0; i < count; i++)
        {
            if (poolObject.transform.GetChild(i).gameObject.activeSelf)
                return true;
        }

        return false;
    }

    EntityId ActiveRootId()
    {
        int count = poolObject.transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform root = poolObject.transform.GetChild(i);
            if (root.gameObject.activeSelf)
                return root.gameObject.GetEntityId();
        }

        return default;
    }

    List<EntityId> ChildIds()
    {
        List<EntityId> ids = new List<EntityId>(poolObject.transform.childCount);
        int count = poolObject.transform.childCount;
        for (int i = 0; i < count; i++)
            ids.Add(poolObject.transform.GetChild(i).gameObject.GetEntityId());
        return ids;
    }
}

using System;
using System.Reflection;
using HuntingGame.Animals;
using HuntingGame.Senses;
using HuntingGame.Wind;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Edit-mode checks for smell, sight, hearing, and the sense interval.
public class AnimalSenseTests
{
    GameObject windObject;
    GameObject player;
    GameObject animalObject;
    GameObject wall;
    Animal animal;
    AnimalSenses senses;
    AnimalAiAgent agent;
    bool madeWind;
    bool windWasEnabled;
    Vector3 savedWind;
    float savedStrength;
    bool savedMoving;
    bool savedSprinting;
    bool savedCrouching;
    float savedNoise;

    [SetUp]
    public void SetUp()
    {
        savedMoving = PlayerMove.IsMoving;
        savedSprinting = PlayerMove.IsSprinting;
        savedCrouching = PlayerMove.IsCrouching;
        savedNoise = PlayerMove.SurfaceNoise;
        PlayerMove.SetGait(false, false, false, 1f);

        madeWind = WindSystem.Current == null;
        if (madeWind)
        {
            windObject = new GameObject("SenseTestWind");
            windObject.hideFlags = HideFlags.HideAndDontSave;
            Wake(windObject.AddComponent<WindSystem>());
        }
        else
        {
            windObject = WindSystem.Current.gameObject;
            savedWind = WindSystem.Current.Direction;
            savedStrength = WindSystem.Current.Strength;
            windWasEnabled = WindSystem.Current.enabled;
        }

        WindSystem.Current.SetFixed(Vector3.forward, 1f);
        WindSystem.Current.enabled = false;

        player = new GameObject("SenseTestPlayer");
        player.hideFlags = HideFlags.HideAndDontSave;
        player.transform.position = Vector3.zero;

        animalObject = new GameObject("SenseTestAnimal");
        animalObject.hideFlags = HideFlags.HideAndDontSave;
        animal = animalObject.AddComponent<Animal>();
        senses = animalObject.AddComponent<AnimalSenses>();
        Wake(senses);
        senses.SetWatch(player.transform);
    }

    static void Wake(MonoBehaviour behaviour)
    {
        MethodInfo awake = behaviour.GetType().GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (awake != null)
            awake.Invoke(behaviour, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (wall != null)
            UnityEngine.Object.DestroyImmediate(wall);
        if (animalObject != null)
            UnityEngine.Object.DestroyImmediate(animalObject);
        if (player != null)
            UnityEngine.Object.DestroyImmediate(player);

        if (madeWind)
        {
            if (windObject != null)
                UnityEngine.Object.DestroyImmediate(windObject);
        }
        else if (WindSystem.Current != null)
        {
            WindSystem.Current.SetFixed(savedWind, savedStrength);
            WindSystem.Current.enabled = windWasEnabled;
        }

        PlayerMove.SetGait(savedMoving, savedSprinting, savedCrouching, savedNoise);
    }

    [Test]
    public void DownwindAt20Meters_SmellsPlayer()
    {
        PlaceAnimal(new Vector3(0f, 0f, 20f), Vector3.forward);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsTrue(animal.Fleeing, "Downwind animal at 20m should smell the player.");
    }

    [Test]
    public void UpwindAt20Meters_DoesNotSmellPlayer()
    {
        PlaceAnimal(new Vector3(0f, 0f, -20f), Vector3.back);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsFalse(animal.Fleeing, "Upwind animal at 20m should not smell the player.");
    }

    [Test]
    public void Sight_FailsOutsideCone_AndBehindObstacle()
    {
        WindSystem.Current.SetFixed(Vector3.right, 1f);
        PlaceAnimal(new Vector3(0f, 0f, 8f), Vector3.back);

        senses.Tick(AnimalAiBand.Full);
        Assert.IsTrue(animal.Fleeing, "Animal facing the player in the open should see them.");

        animal.EndFlee();
        Vector3 toPlayer = player.transform.position - animalObject.transform.position;
        toPlayer.y = 0f;
        animalObject.transform.rotation = Quaternion.LookRotation(toPlayer.normalized) * Quaternion.Euler(0f, 60f, 0f);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsFalse(animal.Fleeing, "Sight should fail 60 degrees outside the view cone.");

        animalObject.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.hideFlags = HideFlags.HideAndDontSave;
        wall.transform.position = new Vector3(0f, 1.2f, 4f);
        wall.transform.localScale = new Vector3(3f, 3f, 0.6f);
        Physics.SyncTransforms();
        senses.Tick(AnimalAiBand.Full);
        Assert.IsFalse(animal.Fleeing, "Sight should fail when a solid block stands between the animal and the player.");
    }

    [Test]
    public void Hearing_SprintReachesFarther_CrouchReachesLess()
    {
        WindSystem.Current.SetFixed(Vector3.forward, 1f);

        PlaceAnimal(new Vector3(12f, 0f, 0f), Vector3.right);
        PlayerMove.SetGait(true, true, false, 1f);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsTrue(animal.Fleeing, "Sprinting at 12m should be heard.");

        animal.EndFlee();
        PlayerMove.SetGait(true, false, false, 1f);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsFalse(animal.Fleeing, "Walking at 12m should be inside sprint range and outside walk range.");

        PlaceAnimal(new Vector3(6f, 0f, 0f), Vector3.right);
        PlayerMove.SetGait(true, false, false, 1f);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsTrue(animal.Fleeing, "Walking at 6m should be heard.");

        animal.EndFlee();
        PlayerMove.SetGait(true, false, true, 1f);
        senses.Tick(AnimalAiBand.Full);
        Assert.IsFalse(animal.Fleeing, "Crouching at 6m should be quieter than walking.");
    }

    [Test]
    public void Senses_RunOnInterval_NotEveryTick()
    {
        agent = animalObject.AddComponent<AnimalAiAgent>();
        Wake(agent);
        senses = animalObject.GetComponent<AnimalSenses>();
        senses.SetWatch(player.transform);
        float interval = senses.Interval;
        Assert.Greater(interval, 0.05f);

        PlaceAnimal(new Vector3(0f, 0f, -20f), Vector3.back);
        const float start = 80f;
        agent.ScheduledTick(start, 0.2f);
        Assert.IsFalse(animal.Fleeing, "Upwind animal should stay calm on the first sense tick.");

        PlaceAnimal(new Vector3(0f, 0f, 20f), Vector3.forward);
        agent.ScheduledTick(start + interval * 0.5f, 0.2f);
        Assert.IsFalse(animal.Fleeing, "Senses should not run again before the interval elapses.");

        agent.ScheduledTick(start + interval, 0.2f);
        Assert.IsTrue(animal.Fleeing, "Senses should run once the staggered interval has elapsed.");
    }

    [MenuItem("Hunting Game/Run Sense Tests")]
    public static void RunFromMenu()
    {
        int failed = RunAll();
        if (failed == 0)
            Debug.Log("Sense tests passed: smell, sight, hearing, and staggered interval.");
        else
            Debug.LogError("Sense tests failed: " + failed + ".");
    }

    public static int RunAll()
    {
        int failed = 0;
        failed += Run(t => t.DownwindAt20Meters_SmellsPlayer());
        failed += Run(t => t.UpwindAt20Meters_DoesNotSmellPlayer());
        failed += Run(t => t.Sight_FailsOutsideCone_AndBehindObstacle());
        failed += Run(t => t.Hearing_SprintReachesFarther_CrouchReachesLess());
        failed += Run(t => t.Senses_RunOnInterval_NotEveryTick());
        return failed;
    }

    static int Run(Action<AnimalSenseTests> body)
    {
        AnimalSenseTests tests = new AnimalSenseTests();
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

    void PlaceAnimal(Vector3 position, Vector3 away)
    {
        animalObject.transform.position = position;
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.forward;
        animalObject.transform.rotation = Quaternion.LookRotation(away.normalized);
        animal.EndFlee();
    }
}

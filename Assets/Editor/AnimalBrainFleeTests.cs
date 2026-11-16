using System.Reflection;
using HuntingGame.Animals;
using NUnit.Framework;
using UnityEngine;

public class AnimalBrainFleeTests
{
    GameObject body;
    AnimalBrain brain;
    SpeciesData species;

    [SetUp]
    public void SetUp()
    {
        body = new GameObject("FleeRecoveryTest");
        body.AddComponent<Animal>();
        brain = body.AddComponent<AnimalBrain>();
        species = ScriptableObject.CreateInstance<SpeciesData>();
        species.kind = AnimalKind.Deer;
        species.prey = true;
        species.calmSeconds = 0.5f;
        species.fleeSeconds = 20f;
        species.hideSeconds = 0f; // These tests isolate the legacy return path.
        brain.UseSpecies(species);
        brain.Bind();
        brain.ArmIdle();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(body);
        Object.DestroyImmediate(species);
    }

    void LetThreatExpire()
    {
        typeof(AnimalBrain).GetField("lastNotice", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(brain, Time.time - 2f);
        brain.Tick(0f);
    }

    [Test]
    public void OrdinaryNotice_ReturnsAfterCalmWithoutWaitingForFullRun()
    {
        brain.Notice(Vector3.left * 8f, true);
        Assert.AreEqual(AnimalAiState.Flee, brain.State);
        LetThreatExpire();
        Assert.AreEqual(AnimalAiState.Graze, brain.State);
    }

    [Test]
    public void GradedScare_KeepsItsMinimumRunAfterThreatExpires()
    {
        brain.ReactToThreat(Vector3.left * 8f, 1f, true);
        LetThreatExpire();
        Assert.AreEqual(AnimalAiState.Flee, brain.State);
        brain.Tick(8f);
        Assert.AreEqual(AnimalAiState.Graze, brain.State);
    }

    [Test]
    public void Prey_HidesAfterFlightThenReturnsHome()
    {
        species.hideSeconds = 4f;
        brain.Notice(Vector3.left * 8f, true);
        LetThreatExpire();
        Assert.AreEqual(AnimalAiState.Hiding, brain.State);
        brain.Tick(5f);
        Assert.AreEqual(AnimalAiState.Graze, brain.State);
    }

    [Test]
    public void HiddenAnimal_ImmediatelyFleesNewThreat()
    {
        species.hideSeconds = 4f;
        brain.Notice(Vector3.left * 8f, true);
        LetThreatExpire();
        brain.Notice(Vector3.right * 8f, true);
        Assert.AreEqual(AnimalAiState.Flee, brain.State);
    }

    [Test]
    public void WolfCatalog_IsPredatorAndRabbitIsPrey()
    {
        Animal animal = body.GetComponent<Animal>();
        animal.kind = AnimalKind.Game;
        animal.quarry = QuarryKind.Wolf;
        Assert.IsTrue(SpeciesCatalog.For(animal).predator);
        Assert.IsFalse(SpeciesCatalog.For(animal).prey);
        animal.quarry = QuarryKind.Rabbit;
        Assert.IsTrue(SpeciesCatalog.For(animal).prey);
        Assert.IsFalse(SpeciesCatalog.For(animal).predator);
    }

    [Test]
    public void PredatorSight_OwnColliderCannotHideWallBetweenAnimals()
    {
        GameObject target = new GameObject("OccludedPrey");
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject ownCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            body.transform.position = new Vector3(300000f, 0f, 300000f);
            target.transform.position = body.transform.position + Vector3.forward * 10f;
            AnimalBrain prey = target.AddComponent<AnimalBrain>();
            ownCollider.transform.SetParent(body.transform, false);
            ownCollider.transform.localPosition = new Vector3(0f, 0.8f, 2f);
            wall.transform.position = body.transform.position + new Vector3(0f, 0.8f, 5f);
            wall.transform.localScale = new Vector3(4f, 4f, 1f);
            Physics.SyncTransforms();
            var method = typeof(AnimalBrain).GetMethod("CanSeeAnimal", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsFalse((bool)method.Invoke(brain, new object[] { prey }));
            wall.SetActive(false);
            Physics.SyncTransforms();
            Assert.IsTrue((bool)method.Invoke(brain, new object[] { prey }));
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(ownCollider);
        }
    }
}

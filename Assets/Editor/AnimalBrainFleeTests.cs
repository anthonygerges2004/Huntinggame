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
}

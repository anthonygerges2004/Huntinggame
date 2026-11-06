using HuntingGame.Senses;
using NUnit.Framework;

// The deer threat score is a fuzzy blend. These lock the cases the hunt is built around.
public class DeerThreatFuzzyTests
{
    const float Smell = 26f * 1.25f;
    const float Sight = 32f;
    const float Edge = 0.766f;

    [Test]
    public void CloseAndUpwind_IsHighEvenWhenQuiet()
    {
        float threat = DeerThreatFuzzy.Evaluate(new DeerThreatFuzzy.Sample
        {
            distance = 6f,
            smellRange = Smell,
            hearRange = 0f,
            sightRange = Sight,
            windAlignment = 1f,
            viewDot = -1f,
            viewEdgeDot = Edge,
            viewBlocked = true,
            noise = 0f,
            timeOfDay = -1f
        });

        Assert.Greater(threat, 0.58f);
    }

    [Test]
    public void FarDownwindAndQuiet_StaysLow()
    {
        float threat = DeerThreatFuzzy.Evaluate(new DeerThreatFuzzy.Sample
        {
            distance = 24f,
            smellRange = Smell,
            hearRange = 0f,
            sightRange = Sight,
            windAlignment = -1f,
            viewDot = -1f,
            viewEdgeDot = Edge,
            viewBlocked = true,
            noise = 0f,
            timeOfDay = 0.25f
        });

        Assert.Less(threat, 0.25f);
    }

    [Test]
    public void SprintNearby_IsHighEvenBehindTheDeer()
    {
        float threat = DeerThreatFuzzy.Evaluate(new DeerThreatFuzzy.Sample
        {
            distance = 12f,
            smellRange = Smell,
            hearRange = 16f,
            sightRange = Sight,
            windAlignment = 0f,
            viewDot = -1f,
            viewEdgeDot = Edge,
            viewBlocked = false,
            noise = 1f,
            timeOfDay = 0.25f
        });

        Assert.Greater(threat, 0.58f);
    }

    [Test]
    public void DuskLook_IsHigherThanTheSameLookAtNoon()
    {
        DeerThreatFuzzy.Sample look = new DeerThreatFuzzy.Sample
        {
            distance = 18f,
            smellRange = 1f,
            hearRange = 0f,
            sightRange = Sight,
            windAlignment = -1f,
            viewDot = 1f,
            viewEdgeDot = Edge,
            viewBlocked = false,
            noise = 0f,
            timeOfDay = 0.25f
        };

        float noon = DeerThreatFuzzy.Evaluate(look);
        look.timeOfDay = 0.48f;
        float dusk = DeerThreatFuzzy.Evaluate(look);
        Assert.Greater(dusk, noon);
    }

    [Test]
    public void OutsideTheCone_DoesNotBecomeALook()
    {
        float threat = DeerThreatFuzzy.Evaluate(new DeerThreatFuzzy.Sample
        {
            distance = 8f,
            smellRange = Smell,
            hearRange = 0f,
            sightRange = Sight,
            windAlignment = 0f,
            viewDot = 0.5f,
            viewEdgeDot = Edge,
            viewBlocked = false,
            noise = 0f,
            timeOfDay = -1f
        });

        Assert.Less(threat, 0.58f);
    }

    [Test]
    public void PartialCover_IsLessAlarmingThanAClearLook()
    {
        DeerThreatFuzzy.Sample look = new DeerThreatFuzzy.Sample
        {
            distance = 8f,
            smellRange = 1f,
            hearRange = 0f,
            sightRange = Sight,
            windAlignment = -1f,
            viewDot = 1f,
            viewEdgeDot = Edge,
            viewBlocked = false,
            noise = 0f,
            timeOfDay = -1f
        };

        float open = DeerThreatFuzzy.Evaluate(look);
        look.viewClear = 0.35f;
        float covered = DeerThreatFuzzy.Evaluate(look);
        look.viewClear = 0f;
        look.viewBlocked = true;
        float hidden = DeerThreatFuzzy.Evaluate(look);

        Assert.Greater(open, covered);
        Assert.Greater(covered, 0.05f);
        Assert.Less(hidden, 0.05f);
    }
}

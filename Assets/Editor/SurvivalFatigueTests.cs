using HuntingGame.Survival;
using NUnit.Framework;

public class SurvivalFatigueTests
{
    [Test] public void HealthyAndDryHasNoPenalty()
    {
        Assert.AreEqual(0f, SurvivalFatigueFuzzy.Evaluate(1f, 1f, 1f, 0f).fatigue, 0.00001f);
        Assert.AreEqual(1f, SurvivalFatigueFuzzy.Evaluate(0f, 0f, 0f, 1f).fatigue, 0.00001f);
    }

    [Test] public void CombinedNeedsAndColdWetClothesCompoundFatigue()
    {
        float combined = SurvivalFatigueFuzzy.Evaluate(0.1f, 0.35f, 1f, 0f).fatigue;
        Assert.IsTrue(combined > SurvivalFatigueFuzzy.Evaluate(0.1f, 1f, 1f, 0f).fatigue);
        Assert.IsTrue(combined > SurvivalFatigueFuzzy.Evaluate(1f, 0.35f, 1f, 0f).fatigue);
        Assert.IsTrue(SurvivalFatigueFuzzy.Evaluate(1f, 1f, 0.1f, 1f).fatigue > SurvivalFatigueFuzzy.Evaluate(1f, 1f, 0.1f, 0f).fatigue);
        Assert.AreEqual(0f, SurvivalFatigueFuzzy.Evaluate(1f, 1f, 1f, 1f).shiver, 0.00001f);
    }

    [Test] public void EveryInputIsMonotoneAndOutputsStayBounded()
    {
        for (int h = 0; h <= 10; h++) for (int t = 0; t <= 10; t++)
        for (int c = 0; c <= 10; c++) for (int w = 0; w <= 10; w++)
        {
            float food = h / 10f, water = t / 10f, warmth = c / 10f, wet = w / 10f;
            var value = SurvivalFatigueFuzzy.Evaluate(food, water, warmth, wet);
            Assert.IsTrue(value.fatigue >= 0f && value.fatigue <= 1f && value.shiver >= 0f && value.shiver <= 1f);
            Assert.IsTrue(SurvivalFatigueFuzzy.Evaluate(food + 0.01f, water, warmth, wet).fatigue <= value.fatigue + 0.00001f);
            Assert.IsTrue(SurvivalFatigueFuzzy.Evaluate(food, water + 0.01f, warmth, wet).fatigue <= value.fatigue + 0.00001f);
            Assert.IsTrue(SurvivalFatigueFuzzy.Evaluate(food, water, warmth + 0.01f, wet).fatigue <= value.fatigue + 0.00001f);
            Assert.IsTrue(SurvivalFatigueFuzzy.Evaluate(food, water, warmth, wet + 0.01f).fatigue >= value.fatigue - 0.00001f);
        }
    }

    [Test] public void SmoothingIsFrameRateIndependentAndPauseDoesNotAdvanceIt()
    {
        float a = 0f, b = 0f;
        for (int i = 0; i < 30; i++) a = SurvivalFatigueFuzzy.Approach(a, 1f, 1f / 30f, 2f);
        for (int i = 0; i < 144; i++) b = SurvivalFatigueFuzzy.Approach(b, 1f, 1f / 144f, 2f);
        Assert.AreEqual(a, b, 0.0001f);
        Assert.AreEqual(a, SurvivalFatigueFuzzy.Approach(a, 1f, 0f, 2f), 0f);
        Assert.IsTrue(SurvivalFatigueFuzzy.Approach(1f, 0f, 1f, 6f) > 0f);
        Assert.IsTrue(SurvivalFatigueFuzzy.Approach(1f, 0f, 120f, 6f) < 0.001f);
    }
}
using HuntingGame.Senses;
using UnityEditor;
using UnityEngine;

public static class GunChoiceFuzzyDebug
{
    [MenuItem("Hunting Game/Score Gun Examples")]
    static void ScoreExamples()
    {
        GunChoiceFuzzy.Bands bands = GunChoiceFuzzy.Bands.Standard();
        Report("calm still deer, 16 m", 16f, GunChoiceFuzzy.Target.Deer(), bands);
        Report("calm still deer, 80 m", 80f, GunChoiceFuzzy.Target.Deer(), bands);
        Report("calm still deer, 200 m, light wind", 200f, GunChoiceFuzzy.Target.Deer(), bands);
        Report("bird, 30 m", 30f, new GunChoiceFuzzy.Target { size = 0f, wind = 0.2f }, bands);
        Report("bird, 200 m", 200f, new GunChoiceFuzzy.Target { size = 0f, wind = 0.2f }, bands);
        Report("deer fleeing, 20 m", 20f, new GunChoiceFuzzy.Target { size = 0.5f, speed = 1f, alertness = 1f, wind = 0.2f }, bands);
        Report("deer fleeing, 200 m", 200f, new GunChoiceFuzzy.Target { size = 0.5f, speed = 1f, alertness = 1f, wind = 0.2f }, bands);
        Report("large, calm, 25 m", 25f, new GunChoiceFuzzy.Target { size = 1f, wind = 0.2f }, bands);
        Report("large, calm, 80 m", 80f, new GunChoiceFuzzy.Target { size = 1f, wind = 0.2f }, bands);
        Report("calm still deer, 200 m, strong wind", 200f, new GunChoiceFuzzy.Target { size = 0.5f, wind = 0.9f }, bands);
    }

    static void Report(string label, float distance, GunChoiceFuzzy.Target target, GunChoiceFuzzy.Bands bands)
    {
        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(Full(distance), target, bands);
        Debug.Log(label + "  →  " + choice.weapon
            + "   shotgun " + choice.shotgun.ToString("0.00")
            + "  rifle " + choice.rifle.ToString("0.00")
            + "  sniper " + choice.sniper.ToString("0.00"));
    }

    static GunChoiceFuzzy.Sample Full(float distance)
    {
        return new GunChoiceFuzzy.Sample
        {
            distance = distance,
            shotgunRounds = 2,
            shotgunMag = 2,
            shotgunRange = 120f,
            rifleRounds = 5,
            rifleMag = 5,
            rifleRange = 240f,
            sniperRounds = 4,
            sniperMag = 4,
            sniperRange = 500f
        };
    }
}

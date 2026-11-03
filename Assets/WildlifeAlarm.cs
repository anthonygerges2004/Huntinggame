using HuntingGame.Animals;
using UnityEngine;

public static class WildlifeAlarm
{
    public const float ShotRadius = 25f;

    public static void NotifyShot(Vector3 position)
    {
        AnimalAiScheduler.Alert(position, ShotRadius);
    }
}

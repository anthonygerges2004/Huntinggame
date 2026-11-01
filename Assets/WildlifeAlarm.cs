using UnityEngine;

public static class WildlifeAlarm
{
    public static void NotifyShot(Vector3 position)
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            if (!animals[i].IsPresent() || animals[i].Dead)
                continue;
            if (Vector3.Distance(animals[i].transform.position, position) < 25f)
                animals[i].FleeFrom(position);
        }
    }
}

using UnityEngine;

public class WildlifeAlarm : MonoBehaviour
{
    public static void NotifyShot(Vector3 position)
    {
        Animal[] animals = FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            if (!animals[i].IsPresent())
                continue;
            if (Vector3.Distance(animals[i].transform.position, position) < 25f)
                animals[i].FleeFrom(position);
        }
    }
}

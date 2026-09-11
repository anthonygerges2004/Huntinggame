using UnityEngine;

public class WildlifeAlarm : MonoBehaviour
{
    public static void NotifyShot(Vector3 position)
    {
        Animal[] animals = FindObjectsByType<Animal>(FindObjectsSortMode.None);
        for (int i = 0; i < animals.Length; i++)
        {
            if (Vector3.Distance(animals[i].transform.position, position) < 25f)
                animals[i].FleeFrom(position);
        }
    }
}

using UnityEngine;

public class Overhunt : MonoBehaviour
{
    public static int kills;
    public static int killsBeforeAreaGoesQuiet = 6;

    public static void RegisterKill()
    {
        kills++;
        if (kills < killsBeforeAreaGoesQuiet)
            return;

        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsSortMode.None);
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i].kind != AnimalKind.Legendary)
                animals[i].FleeFrom(animals[i].transform.position + Vector3.forward);
        }
    }
}

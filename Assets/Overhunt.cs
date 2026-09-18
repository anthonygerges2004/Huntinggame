using UnityEngine;

public class Overhunt : MonoBehaviour
{
    public static int kills;
    public static int killsBeforeAreaGoesQuiet = 6;
    static bool areaWasSpooked;

    public static void ResetProgress()
    {
        kills = 0;
        areaWasSpooked = false;
    }

    public static void RegisterKill()
    {
        kills++;
        if (kills < killsBeforeAreaGoesQuiet || areaWasSpooked)
            return;

        areaWasSpooked = true;
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i].kind != AnimalKind.Legendary)
                animals[i].FleeFrom(animals[i].transform.position + Vector3.forward);
        }
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// A few pigs, a boar, foxes, rabbits and a wolf just outside camp, so the new game
/// is there before the player walks into the open dens.
/// </summary>
public class AmbientGame : MonoBehaviour
{
    bool spawned;

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        yield return null;
        if (spawned)
            yield break;
        spawned = true;
        SpawnAll();
    }

    void SpawnAll()
    {
        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        Vector3 camp = WorldGround.Snap(tent != null ? tent.transform.position : transform.position);

        Spawn(QuarryKind.Pig, Ring(camp, 96f, 0.4f));
        Spawn(QuarryKind.Pig, Ring(camp, 104f, 0.7f));
        Spawn(QuarryKind.Pig, Ring(camp, 112f, 1.1f));
        Spawn(QuarryKind.Boar, Ring(camp, 128f, 2.2f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 88f, 3.1f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 94f, 3.6f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 102f, 4.2f));
        Spawn(QuarryKind.Fox, Ring(camp, 118f, 5.1f));
        Spawn(QuarryKind.Fox, Ring(camp, 136f, 5.8f));
        Spawn(QuarryKind.Wolf, Ring(camp, 150f, 0.15f));
    }

    static void Spawn(QuarryKind kind, Vector3 position)
    {
        QuarryProfile profile = QuarryProfile.Of(kind);
        GameObject body = new GameObject(profile.objectName);
        body.transform.position = WorldGround.Snap(position);
        body.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        body.AddComponent<CapsuleCollider>();
        Animal animal = body.AddComponent<Animal>();
        animal.kind = AnimalKind.Game;
        animal.quarry = kind;
    }

    static Vector3 Ring(Vector3 camp, float radius, float angle)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = angle + i * 0.35f;
            Vector3 pos = WorldGround.Snap(camp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            if (!WorldGround.InsideWater(pos))
                return pos;
        }

        return WorldGround.Snap(camp + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
    }
}

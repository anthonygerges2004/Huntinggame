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

        // Close enough to see from camp at the start (they were 90-150 m out, usually behind trees).
        Spawn(QuarryKind.Pig, Ring(camp, 42f, 0.4f));
        Spawn(QuarryKind.Pig, Ring(camp, 46f, 0.6f));
        Spawn(QuarryKind.Pig, Ring(camp, 50f, 0.85f));
        Spawn(QuarryKind.Boar, Ring(camp, 64f, 2.2f));
        Spawn(QuarryKind.Boar, Ring(camp, 72f, 4.6f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 34f, 3.1f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 37f, 3.4f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 40f, 1.6f));
        Spawn(QuarryKind.Rabbit, Ring(camp, 44f, 5.5f));
        Spawn(QuarryKind.Fox, Ring(camp, 58f, 5.1f));
        Spawn(QuarryKind.Fox, Ring(camp, 66f, 1.3f));
        Spawn(QuarryKind.Wolf, Ring(camp, 90f, 0.15f));
        Spawn(QuarryKind.Wolf, Ring(camp, 96f, 3.9f));
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

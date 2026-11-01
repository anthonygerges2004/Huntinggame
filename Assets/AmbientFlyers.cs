using System.Collections;
using UnityEngine;

public class AmbientFlyers : MonoBehaviour
{
    public int sparrowFlocks = 3;
    public int sparrowsPerFlock = 4;
    public int crowCount = 5;
    public int duckCount = 4;
    public int hawkCount = 2;

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

        for (int f = 0; f < sparrowFlocks; f++)
        {
            Vector3 center = Ring(camp, 22f + f * 14f, f * 1.7f);
            for (int i = 0; i < sparrowsPerFlock; i++)
                Spawn("WildSparrow", FlyerKind.Sparrow, TreeOrAir(center + RandomOffset(3.5f), camp, 40f), 1);
        }

        for (int i = 0; i < crowCount; i++)
            Spawn("WildCrow", FlyerKind.Crow, TreeOrAir(Ring(camp, 30f + i * 8f, i * 2.1f + 0.4f), camp, 55f), 1);

        int ducks = WaterBodies.Lakes.Count > 0 || WaterBodies.Rivers.Count > 0 ? duckCount : 0;
        for (int i = 0; i < ducks; i++)
            Spawn("WildDuck", FlyerKind.Duck, WaterPoint(camp, i), 2);

        for (int i = 0; i < hawkCount; i++)
            Spawn("WildHawk", FlyerKind.Hawk, Ring(camp, 48f + i * 18f, i * 2.6f + 1.1f), 3);
    }

    static void Spawn(string name, FlyerKind kind, Vector3 position, int meat)
    {
        Vector3 ground = WorldGround.Snap(position);
        float height = kind == FlyerKind.Hawk ? 20f : kind == FlyerKind.Duck ? 4f : kind == FlyerKind.Crow ? 12f : 8f;
        GameObject bird = new GameObject(name);
        bird.transform.position = ground + Vector3.up * height;

        SphereCollider hitBox = bird.AddComponent<SphereCollider>();
        hitBox.radius = BirdVisual.ColliderRadius(kind);
        hitBox.center = new Vector3(0f, 0.12f, 0f);

        Animal animal = bird.AddComponent<Animal>();
        animal.kind = AnimalKind.Bird;
        animal.flyer = kind;
        animal.meatAmount = meat;
        animal.moveSpeed = 6f;
        animal.fleeSpeed = 13f;
        animal.nocturnal = false;
    }

    static Vector3 Ring(Vector3 camp, float radius, float angle)
    {
        Vector3 pos = camp + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        return WorldGround.Snap(pos);
    }

    static Vector3 RandomOffset(float radius)
    {
        Vector2 c = Random.insideUnitCircle * radius;
        return new Vector3(c.x, 0f, c.y);
    }

    static Vector3 TreeOrAir(Vector3 fallback, Vector3 camp, float range)
    {
        if (Random.value < 0.7f &&
            BirdFlight.TryTreeSeat(camp, range, out Vector3 seat, out _))
            return seat;
        return fallback;
    }

    static Vector3 WaterPoint(Vector3 camp, int index)
    {
        if (WaterBodies.Lakes.Count > 0)
        {
            LakeWater lake = WaterBodies.Lakes[index % WaterBodies.Lakes.Count];
            if (lake != null)
            {
                float ang = index * 1.9f;
                float r = Mathf.Max(3f, lake.Radius * 0.45f);
                return lake.Center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
            }
        }

        foreach (RiverWater river in WaterBodies.Rivers)
        {
            if (river != null)
                return river.transform.position;
        }

        return Ring(camp, 40f, index * 1.3f);
    }
}

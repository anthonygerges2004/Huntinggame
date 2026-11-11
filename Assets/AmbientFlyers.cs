using System.Collections;
using UnityEngine;

public class AmbientFlyers : MonoBehaviour
{
    public int sparrowFlocks = 3;
    public int sparrowsPerFlock = 4;
    public int crowCount = 5;
    public int duckCount = 4;
    public int hawkCount = 2;
    public int thrushFlocks = 2;
    public int thrushesPerFlock = 3;
    public int robinCount = 3;
    public int jayCount = 2;
    public int woodpeckerCount = 2;
    public int pigeonCount = 4;
    public int pheasantCount = 2;
    public int gooseCount = 2;
    public int owlCount = 1;

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
                Spawn(FlyerKind.Sparrow, TreeOrAir(center + RandomOffset(3.5f), camp, 40f));
        }

        for (int i = 0; i < crowCount; i++)
            Spawn(FlyerKind.Crow, TreeOrAir(Ring(camp, 30f + i * 8f, i * 2.1f + 0.4f), camp, 55f));

        int ducks = WaterBodies.Lakes.Count > 0 || WaterBodies.Rivers.Count > 0 ? duckCount : 0;
        for (int i = 0; i < ducks; i++)
            Spawn(FlyerKind.Duck, WaterPoint(camp, i));

        for (int i = 0; i < hawkCount; i++)
            Spawn(FlyerKind.Hawk, Ring(camp, 48f + i * 18f, i * 2.6f + 1.1f));

        for (int f = 0; f < thrushFlocks; f++)
        {
            Vector3 center = Ring(camp, 36f + f * 16f, f * 2.4f + 0.8f);
            for (int i = 0; i < thrushesPerFlock; i++)
                Spawn(FlyerKind.Thrush, TreeOrAir(center + RandomOffset(4f), camp, 48f));
        }

        for (int i = 0; i < robinCount; i++)
            Spawn(FlyerKind.Robin, TreeOrAir(Ring(camp, 28f + i * 10f, i * 2.2f + 1.4f), camp, 40f));

        for (int i = 0; i < jayCount; i++)
            Spawn(FlyerKind.Jay, TreeOrAir(Ring(camp, 40f + i * 14f, i * 1.8f + 2.6f), camp, 55f));

        for (int i = 0; i < woodpeckerCount; i++)
            Spawn(FlyerKind.Woodpecker, TreeOrAir(Ring(camp, 34f + i * 12f, i * 2.8f + 0.2f), camp, 36f));

        Vector3 pigeons = Ring(camp, 52f, 4.4f);
        for (int i = 0; i < pigeonCount; i++)
            Spawn(FlyerKind.Pigeon, pigeons + RandomOffset(6f));

        for (int i = 0; i < pheasantCount; i++)
            Spawn(FlyerKind.Pheasant, Ring(camp, 70f + i * 12f, i * 1.3f + 5.2f));

        int geese = WaterBodies.Lakes.Count > 0 || WaterBodies.Rivers.Count > 0 ? gooseCount : 0;
        for (int i = 0; i < geese; i++)
            Spawn(FlyerKind.Goose, WaterPoint(camp, i + 3));

        for (int i = 0; i < owlCount; i++)
            Spawn(FlyerKind.Owl, TreeOrAir(Ring(camp, 64f, 1.9f), camp, 70f));
    }

    static void Spawn(FlyerKind kind, Vector3 position)
    {
        FlyerProfile profile = FlyerProfile.Of(kind);
        Vector3 ground = WorldGround.Snap(position);
        GameObject bird = new GameObject(profile.objectName);
        bird.transform.position = ground + Vector3.up * profile.spawnHeight;

        SphereCollider hitBox = bird.AddComponent<SphereCollider>();
        hitBox.radius = profile.collider;
        hitBox.center = new Vector3(0f, 0.12f, 0f);

        Animal animal = bird.AddComponent<Animal>();
        animal.kind = AnimalKind.Bird;
        animal.flyer = kind;
        animal.meatAmount = profile.meat;
        animal.moveSpeed = profile.cruiseSpeed;
        animal.fleeSpeed = profile.fleeSpeed;
        animal.nocturnal = profile.nocturnal;
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

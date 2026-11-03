using System.Collections.Generic;
using UnityEngine;

public class MeatBait : MonoBehaviour
{
    public int amount = 1;
    public float lifeSeconds = 22f;
    public float preyRadius = 18f;
    public float largeRadius = 50f;
    public float pickupRange = 1.35f;

    static readonly List<MeatBait> Active = new List<MeatBait>();
    static Material baitMat;
    static Transform player;

    float age;
    bool drewLarge;
    Vector3 basePos;

    public static MeatBait Place(Vector3 position, int amount)
    {
        if (amount <= 0)
            return null;

        GameObject root = new GameObject("MeatBait");
        root.transform.position = WorldGround.Sit(position);

        MeatBait bait = root.AddComponent<MeatBait>();
        bait.amount = amount;
        bait.BuildVisual();
        return bait;
    }

    public static bool TryGetTarget(Vector3 from, AnimalKind kind, out Vector3 target)
    {
        target = default;
        bool large = kind == AnimalKind.Large || kind == AnimalKind.Legendary;
        if (large)
        {
            if (DayNightCycle.Instance == null || !DayNightCycle.Instance.IsNight)
                return false;
        }
        else if (kind != AnimalKind.Bird && kind != AnimalKind.Deer)
        {
            return false;
        }

        MeatBait best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < Active.Count; i++)
        {
            MeatBait bait = Active[i];
            if (bait == null)
                continue;
            if (large && Campfire.Covers(bait.transform.position))
                continue;

            float limit = large ? bait.largeRadius : bait.preyRadius;
            Vector3 offset = bait.transform.position - from;
            offset.y = 0f;
            float dist = offset.magnitude;
            if (dist <= limit && dist < bestDist)
            {
                bestDist = dist;
                best = bait;
            }
        }

        if (best == null)
            return false;

        target = best.transform.position;
        return true;
    }

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    void Start()
    {
        basePos = transform.position;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 2f) * 0.04f);

        if (age >= lifeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        TryPickup();
        TryDrawLarge();
    }

    void TryPickup()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (player == null)
        {
            PlayerMove move = FindAnyObjectByType<PlayerMove>();
            if (move == null)
                return;
            player = move.transform;
        }

        Vector3 toPlayer = player.position - basePos;
        float height = Mathf.Abs(toPlayer.y);
        toPlayer.y = 0f;
        if (toPlayer.magnitude > pickupRange || height > 2.5f)
            return;

        if (Inventory.Instance == null)
            return;

        Inventory.Instance.AddSpoiledMeat(amount);
        Inventory.Instance.ShowNotice("Picked up spoiled meat.");
        Destroy(gameObject);
    }

    void TryDrawLarge()
    {
        if (drewLarge || age < 2f)
            return;

        if (DayNightCycle.Instance == null || !DayNightCycle.Instance.IsNight)
            return;

        if (Campfire.Covers(transform.position))
            return;

        Animal[] animals = FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            Animal animal = animals[i];
            if (animal == null || animal.Dead || !animal.IsPresent())
                continue;
            if (animal.kind != AnimalKind.Large && animal.kind != AnimalKind.Legendary)
                continue;

            Vector3 offset = animal.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= largeRadius)
            {
                drewLarge = true;
                return;
            }
        }

        Vector3 spawn = RingPoint(transform.position, 14f);
        GameObject visitor = new GameObject("LargeBaitVisitor");
        visitor.transform.position = spawn;
        Animal spawned = visitor.AddComponent<Animal>();
        spawned.kind = AnimalKind.Large;
        spawned.nocturnal = true;
        spawned.meatAmount = 5;
        spawned.nightDamage = 14f;
        drewLarge = true;

        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("A large animal is coming to the bait.");
    }

    static Vector3 RingPoint(Vector3 center, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            point = WorldGround.Snap(point);
            if (!WorldGround.InsideWater(point))
                return point;
        }

        return WorldGround.Snap(center + Vector3.forward * radius);
    }

    void BuildVisual()
    {
        if (baitMat == null)
        {
            baitMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            baitMat.SetColor("_BaseColor", new Color(0.28f, 0.32f, 0.12f));
        }

        int pieces = Mathf.Clamp(amount, 1, 3);
        for (int i = 0; i < pieces; i++)
        {
            float angle = i * Mathf.PI * 2f / pieces;
            Vector3 offset = pieces == 1
                ? Vector3.zero
                : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.18f;

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(piece.GetComponent<Collider>());
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = offset + Vector3.up * 0.08f;
            piece.transform.localScale = new Vector3(0.36f, 0.16f, 0.26f);
            piece.GetComponent<Renderer>().sharedMaterial = baitMat;
        }
    }
}

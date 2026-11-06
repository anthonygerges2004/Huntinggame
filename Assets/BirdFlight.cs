using UnityEngine;

[DefaultExecutionOrder(20)]
public class BirdFlight : MonoBehaviour
{
    Animal animal;
    Transform wingLeft;
    Transform wingRight;
    Vector3 home;
    Vector3 target;
    float age;
    float perchUntil;
    float nextDecision;
    bool perched;
    bool landing;
    Vector3 perchOutward = Vector3.forward;
    Vector3 perchRest;
    float cruiseMin;
    float cruiseMax;
    float cruiseSpeed;
    float fleeSpeed;
    float wander;

    void Start()
    {
        animal = GetComponent<Animal>();
        CacheWings();
        ApplyStats();

        home = WorldGround.Snap(transform.position);
        float startY = home.y + Random.Range(cruiseMin, cruiseMax);
        if (animal != null && animal.flyer == FlyerKind.Duck &&
            WaterBodies.SurfaceAt(home, 12f, out float surface, out _))
            startY = surface + Random.Range(cruiseMin, cruiseMax);
        if (transform.position.y < startY - 1f)
            transform.position = new Vector3(transform.position.x, startY, transform.position.z);

        nextDecision = Time.time + Random.Range(2f, 6f);
        if (animal != null && animal.flyer != FlyerKind.Duck && Random.value < 0.55f && TryPerch(true))
            SnapToPerch();
        else
            PickTarget();
    }

    void CacheWings()
    {
        Transform left = transform.Find("WingLeft");
        Transform right = transform.Find("WingRight");
        wingLeft = left != null ? left : FindNamed("WingLeft");
        wingRight = right != null ? right : FindNamed("WingRight");
    }

    Transform FindNamed(string name)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i];
        }
        return null;
    }

    void ApplyStats()
    {
        FlyerKind kind = animal != null ? animal.flyer : FlyerKind.Sparrow;
        switch (kind)
        {
            case FlyerKind.Crow:
                cruiseMin = 8f;
                cruiseMax = 16f;
                cruiseSpeed = 7.5f;
                fleeSpeed = 14f;
                wander = 48f;
                break;
            case FlyerKind.Duck:
                cruiseMin = 2.2f;
                cruiseMax = 6.2f;
                cruiseSpeed = 6f;
                fleeSpeed = 12f;
                wander = 26f;
                break;
            case FlyerKind.Hawk:
                cruiseMin = 16f;
                cruiseMax = 28f;
                cruiseSpeed = 9f;
                fleeSpeed = 16f;
                wander = 78f;
                break;
            default:
                cruiseMin = 5f;
                cruiseMax = 11f;
                cruiseSpeed = 6.5f;
                fleeSpeed = 13f;
                wander = 34f;
                break;
        }
    }

    void Update()
    {
        if (animal == null || animal.Dead)
            return;

        age += Time.deltaTime;
        bool night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;

        if (animal.Fleeing)
        {
            perched = false;
            landing = false;
            Vector3 away = FleeDestination();
            FlyToward(away, fleeSpeed, 2.4f);
            Flap(28f, 55f);
            animal.TickFlee(Time.deltaTime);
            return;
        }

        if (MeatBait.TryGetTarget(transform.position, AnimalKind.Bird, out Vector3 baitPoint))
        {
            perched = false;
            landing = false;
            Vector3 hover = baitPoint;
            hover.y = WorldGround.Snap(baitPoint).y + Mathf.Clamp(cruiseMin * 0.35f, 2f, 4.5f);
            FlyToward(hover, cruiseSpeed, 0.45f);
            Flap(18f, 40f);
            return;
        }

        if (night && !perched && !landing)
            TryPerch(true);

        if (perched)
        {
            SitPose();
            if (!night && Time.time >= perchUntil)
            {
                perched = false;
                landing = false;
                PickTarget();
            }
            return;
        }

        if (landing)
        {
            FlyToward(target, cruiseSpeed + 1.5f, 0.4f);
            Flap(20f, 45f);
            if (Vector3.Distance(transform.position, target) < 1.1f)
                SnapToPerch();
            return;
        }

        FlyToward(target, cruiseSpeed, 1.1f);
        Flap(animal.flyer == FlyerKind.Hawk ? 10f : 22f, animal.flyer == FlyerKind.Hawk ? 28f : 50f);

        if (Vector3.Distance(transform.position, target) < 1.4f || Time.time >= nextDecision)
        {
            nextDecision = Time.time + Random.Range(4f, 9f);
            if (!night && Random.value < PerchChance())
                TryPerch(false);
            else
                PickTarget();
        }
    }

    float PerchChance()
    {
        switch (animal.flyer)
        {
            case FlyerKind.Hawk:
                return 0.22f;
            case FlyerKind.Duck:
                return 0.12f;
            case FlyerKind.Crow:
                return 0.62f;
            default:
                return 0.7f;
        }
    }

    void FlyToward(Vector3 dest, float speed, float bob)
    {
        Vector3 pos = transform.position;
        Vector3 flatTo = new Vector3(dest.x - pos.x, 0f, dest.z - pos.z);
        if (flatTo.sqrMagnitude > 0.04f)
        {
            Vector3 step = flatTo.normalized * (speed * Time.deltaTime);
            if (step.sqrMagnitude > flatTo.sqrMagnitude)
                step = flatTo;
            pos.x += step.x;
            pos.z += step.z;

            Quaternion look = Quaternion.LookRotation(flatTo.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 4.5f * Time.deltaTime);
        }

        float wantY = dest.y + Mathf.Sin(age * 2.4f) * bob * 0.12f;
        pos.y = Mathf.MoveTowards(pos.y, wantY, speed * 0.55f * Time.deltaTime);

        float floor = WorldGround.Snap(pos).y + 1.2f;
        if (animal != null && animal.flyer == FlyerKind.Duck &&
            WaterBodies.SurfaceAt(pos, 8f, out float surface, out _))
            floor = surface + 1.4f;
        if (pos.y < floor)
            pos.y = floor;
        transform.position = pos;
    }

    Vector3 FleeDestination()
    {
        Vector3 fromPlayer = transform.position - PlayerView.Position();
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude < 0.25f)
            fromPlayer = RandomHorizontalDir() * 12f;
        else
            fromPlayer = fromPlayer.normalized * Random.Range(14f, 26f);

        Vector3 dest = transform.position + fromPlayer;
        dest = WorldGround.Snap(dest);
        dest.y = transform.position.y + Random.Range(1.5f, 4f);
        dest.y = Mathf.Clamp(dest.y, WorldGround.Snap(dest).y + cruiseMin, WorldGround.Snap(dest).y + cruiseMax);
        return dest;
    }

    static Vector3 RandomHorizontalDir()
    {
        if (Random.value < 0.4f)
        {
            int card = Random.Range(0, 4);
            if (card == 0)
                return Vector3.forward;
            if (card == 1)
                return Vector3.back;
            if (card == 2)
                return Vector3.right;
            return Vector3.left;
        }

        Vector2 v = Random.insideUnitCircle;
        if (v.sqrMagnitude < 0.01f)
            v = Vector2.right;
        return new Vector3(v.x, 0f, v.y).normalized;
    }

    void PickTarget()
    {
        if (animal.flyer == FlyerKind.Duck && TryWaterTarget(out Vector3 water))
        {
            target = water;
            return;
        }

        Vector3 flatDir = RandomHorizontalDir();
        float dist = Random.Range(wander * 0.4f, wander);
        Vector3 pos = home + flatDir * dist;
        pos = WorldGround.Snap(pos);
        pos.y = home.y + Random.Range(cruiseMin, cruiseMax);
        target = pos;
    }

    bool TryWaterTarget(out Vector3 point)
    {
        point = Vector3.zero;
        if (WaterBodies.SurfaceAt(home, 8f, out float surface, out _))
        {
            Vector3 flatDir = RandomHorizontalDir();
            float dist = Random.Range(wander * 0.35f, wander);
            point = new Vector3(home.x + flatDir.x * dist, surface + Random.Range(cruiseMin, cruiseMax),
                home.z + flatDir.z * dist);
            if (WaterBodies.SurfaceAt(point, 4f, out surface, out _))
            {
                point.y = surface + Random.Range(cruiseMin, cruiseMax);
                return true;
            }
        }

        foreach (LakeWater lake in WaterBodies.Lakes)
        {
            if (lake == null)
                continue;
            Vector2 circle = Random.insideUnitCircle * Mathf.Max(4f, lake.Radius * 0.65f);
            point = lake.Center + new Vector3(circle.x, 0f, circle.y);
            point.y = lake.SurfaceY + Random.Range(cruiseMin, cruiseMax);
            home = WorldGround.Snap(new Vector3(point.x, 0f, point.z));
            return true;
        }

        return false;
    }

    bool TryPerch(bool force)
    {
        if (animal != null && animal.flyer == FlyerKind.Duck)
            return TryWaterPerch();

        float range = force ? 90f : 42f;
        if (!TryTreeSeat(transform.position, range, out Vector3 seat, out Vector3 outward))
        {
            if (force)
            {
                PickTarget();
                target.y = WorldGround.Snap(target).y + cruiseMin;
            }
            return false;
        }

        target = seat;
        perchOutward = outward;
        landing = true;
        perched = Vector3.Distance(transform.position, target) < 1.15f;
        perchUntil = Time.time + Random.Range(10f, 22f);
        if (perched)
            SnapToPerch();
        return true;
    }

    public static bool TryTreeSeat(Vector3 near, float maxRange, out Vector3 seat, out Vector3 outward)
    {
        seat = near;
        outward = Vector3.forward;
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        HarvestableTree pick = null;
        int found = 0;
        for (int i = 0; i < trees.Length; i++)
        {
            HarvestableTree tree = trees[i];
            if (tree == null)
                continue;
            float d = Flat(tree.transform.position - near).magnitude;
            if (d > maxRange)
                continue;
            found++;
            if (pick == null || Random.Range(0, found) == 0)
                pick = tree;
        }

        if (pick == null)
            return false;

        return SeatOnTree(pick, out seat, out outward);
    }

    public static bool SeatOnTree(HarvestableTree tree, out Vector3 seat, out Vector3 outward)
    {
        seat = tree.transform.position;
        outward = Vector3.forward;

        bool hasBounds = false;
        Bounds bounds = new Bounds();
        Renderer[] renderers = tree.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled)
                continue;
            if (hasBounds)
                bounds.Encapsulate(renderers[i].bounds);
            else
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
        }

        Vector3 trunk = tree.transform.position;
        float height = hasBounds ? bounds.max.y - trunk.y : 6f;
        if (height < 2.4f)
            return false;

        float radius = hasBounds
            ? Mathf.Max(0.9f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.78f)
            : 1.6f;
        float ang = Random.Range(0f, Mathf.PI * 2f);
        outward = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
        float up = Random.Range(0.42f, 0.66f);
        seat = new Vector3(trunk.x, trunk.y + height * up, trunk.z) + outward * radius;
        return true;
    }

    void SnapToPerch()
    {
        transform.position = target;
        perchRest = target;
        perched = true;
        landing = false;
        perchUntil = Time.time + Random.Range(10f, 22f);
        Vector3 look = perchOutward.sqrMagnitude > 0.01f ? perchOutward : Vector3.forward;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        SitPose();
    }

    void SitPose()
    {
        Vector3 look = perchOutward.sqrMagnitude > 0.01f ? perchOutward : transform.forward;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f)
            look = Vector3.forward;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(look.normalized, Vector3.up),
            8f * Time.deltaTime);
        transform.position = perchRest + Vector3.up * (Mathf.Sin(age * 2.2f) * 0.02f);

        if (wingLeft == null || wingRight == null)
            return;
        float fold = -48f + Mathf.Sin(age * 1.4f) * 3f;
        wingLeft.localRotation = Quaternion.Euler(0f, 0f, fold);
        wingRight.localRotation = Quaternion.Euler(0f, 0f, -fold);
    }

    bool TryWaterPerch()
    {
        if (!WaterBodies.SurfaceAt(transform.position, 6f, out float surface, out _))
            return false;
        Vector3 sit = transform.position;
        sit.y = surface + 0.35f;
        target = sit;
        landing = true;
        perched = Vector3.Distance(transform.position, sit) < 1.2f;
        perchUntil = Time.time + Random.Range(6f, 14f);
        if (perched)
        {
            landing = false;
            transform.position = sit;
        }
        return true;
    }

    void Flap(float rate, float amount)
    {
        if (wingLeft == null || wingRight == null)
            return;
        float angle = perched ? Mathf.Sin(age * rate) * 4f - 8f : Mathf.Sin(age * rate) * amount;
        wingLeft.localRotation = Quaternion.Euler(0f, 0f, angle);
        wingRight.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}

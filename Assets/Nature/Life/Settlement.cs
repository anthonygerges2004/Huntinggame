using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The walkable layout of a village or courtyard: a ring around the square, the doors of the
/// buildings that open onto it, and spots inside (stalls, the well, benches).
/// Villagers route door -> ring -> around the ring -> destination, so they never cut
/// through buildings or the well. Filled in by the RPG Poly Pack scenery builder.
/// </summary>
public class Settlement : MonoBehaviour
{
    public string title;
    public Vector3 center;
    public float ringRadius = 7f;
    public Vector3[] doors = new Vector3[0];
    public Vector3[] spots = new Vector3[0];
    [System.NonSerialized] public int assignedWorkers;

    const float ArcStepDegrees = 25f;

    void Start()
    {
        VillageMarket.Install(this);
    }

    public Vector3 RingPoint(float angleRad)
    {
        Vector3 p = center + new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * ringRadius;
        return WorldGround.Snap(p);
    }

    public float AngleOf(Vector3 p)
    {
        return Mathf.Atan2(p.z - center.z, p.x - center.x);
    }

    public Vector3 RandomPoint()
    {
        bool door = doors.Length > 0 && (spots.Length == 0 || Random.value < 0.5f);
        if (door)
            return doors[Random.Range(0, doors.Length)];
        if (spots.Length > 0)
            return spots[Random.Range(0, spots.Length)];
        return RingPoint(Random.Range(0f, Mathf.PI * 2f));
    }

    /// <summary>Fills <paramref name="route"/> with points from <paramref name="from"/> to a random destination.</summary>
    public void PickRoute(Vector3 from, List<Vector3> route)
    {
        route.Clear();
        Vector3 target = RandomPoint();
        RouteTo(from, target, route);
    }

    public void RouteTo(Vector3 from, Vector3 target, List<Vector3> route)
    {
        route.Clear();
        if (Flat(target - from).magnitude < 1.5f)
            return;

        float a0 = AngleOf(from);
        float a1 = AngleOf(target);
        float fromDist = Flat(from - center).magnitude;
        float toDist = Flat(target - center).magnitude;

        // Step onto the ring if we're outside it (at a door) or well inside it (at a spot).
        if (Mathf.Abs(fromDist - ringRadius) > 1f)
            route.Add(RingPoint(a0));

        // Walk around the ring the short way.
        float delta = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg);
        int steps = Mathf.FloorToInt(Mathf.Abs(delta) / ArcStepDegrees);
        for (int i = 1; i <= steps; i++)
            route.Add(RingPoint((a0 * Mathf.Rad2Deg + Mathf.Sign(delta) * ArcStepDegrees * i) * Mathf.Deg2Rad));

        if (Mathf.Abs(toDist - ringRadius) > 1f)
            route.Add(RingPoint(a1));
        route.Add(target);
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i < 24; i++)
            Gizmos.DrawLine(RingPoint(i * Mathf.PI / 12f), RingPoint((i + 1) * Mathf.PI / 12f));
        Gizmos.color = Color.cyan;
        foreach (Vector3 d in doors)
            Gizmos.DrawSphere(d, 0.4f);
        Gizmos.color = Color.green;
        foreach (Vector3 s in spots)
            Gizmos.DrawSphere(s, 0.3f);
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;

public static class Overhunt
{
    public static int kills;
    public static int killsBeforeAreaGoesQuiet = 6;
    public const float AreaRadius = 80f;

    class Area
    {
        public Vector3 center;
        public int kills;
        public bool quiet;
    }

    static readonly List<Area> areas = new List<Area>();

    public static void ResetProgress()
    {
        kills = 0;
        areas.Clear();
    }

    public static bool AreaIsQuiet(Vector3 position)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            if (!areas[i].quiet)
                continue;
            if (FlatDistance(areas[i].center, position) <= AreaRadius)
                return true;
        }

        return false;
    }

    public static void RegisterKill(Vector3 position)
    {
        kills++;
        Area area = FindArea(position);
        if (area == null)
        {
            area = new Area { center = position };
            areas.Add(area);
        }

        area.kills++;
        if (area.kills < killsBeforeAreaGoesQuiet || area.quiet)
            return;

        area.quiet = true;
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        float fleeRadius = AreaRadius * 1.15f;
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i] == null || animals[i].kind == AnimalKind.Legendary)
                continue;
            if (FlatDistance(animals[i].transform.position, position) > fleeRadius)
                continue;
            animals[i].FleeFrom(position);
        }
    }

    static Area FindArea(Vector3 position)
    {
        Area best = null;
        float bestDistance = AreaRadius;
        for (int i = 0; i < areas.Count; i++)
        {
            float distance = FlatDistance(areas[i].center, position);
            if (distance > bestDistance)
                continue;
            bestDistance = distance;
            best = areas[i];
        }

        return best;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}

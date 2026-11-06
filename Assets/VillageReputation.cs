using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where you drop spoiled bait near settlements changes how villagers feel about you.
/// </summary>
public static class VillageReputation
{
    public static int Trust { get; private set; }
    public static int LivestockDefended { get; private set; }

    static float nextTick;
    static float nextSettlementScan;
    static Settlement lastSettlement;
    static bool warnedThisVisit;
    static readonly HashSet<int> greetedSettlements = new HashSet<int>();

    static Settlement[] cachedSettlements = new Settlement[0];

    public static void OnBaitPlaced(Vector3 position)
    {
        Settlement s = NearestSettlement(position, 45f);
        if (s == null)
            return;

        float dist = FlatDistance(position, s.center);
        if (dist > 9f && dist < 38f)
        {
            Trust = Mathf.Clamp(Trust + 6, -100, 100);
            LivestockDefended++;
            Notice("Bait outside the village may draw predators away from the pens.");
            return;
        }

        if (dist <= 9f)
        {
            Trust = Mathf.Clamp(Trust - 10, -100, 100);
            Notice("That bait is too close to homes. People are uneasy.");
        }
    }

    public static void OnLargePredatorDrawn(Vector3 baitPosition)
    {
        RefreshSettlementsIfNeeded();
        Villager[] people = Object.FindObjectsByType<Villager>();
        for (int i = 0; i < people.Length; i++)
        {
            Villager v = people[i];
            if (v == null || v.role != Villager.Role.Traveler)
                continue;

            Vector3 offset = v.transform.position - baitPosition;
            offset.y = 0f;
            if (offset.magnitude > 22f)
                continue;

            Trust = Mathf.Clamp(Trust + 8, -100, 100);
            Notice("Night bait kept a predator busy while a traveller passed.");
            return;
        }
    }

    public static void Tick(Vector3 playerPosition)
    {
        if (Time.time < nextTick)
            return;
        nextTick = Time.time + 2.5f;

        RefreshSettlementsIfNeeded();
        Settlement s = NearestSettlement(playerPosition, 32f);
        if (s == null)
        {
            lastSettlement = null;
            warnedThisVisit = false;
            return;
        }

        if (lastSettlement == s)
            return;

        lastSettlement = s;
        warnedThisVisit = false;
        int id = SettlementKey(s);
        if (greetedSettlements.Contains(id))
            return;

        greetedSettlements.Add(id);
        if (Trust >= 25)
            Notice("Villagers nod — you have helped protect the livestock.");
        else if (Trust <= -20)
            Notice("Guards watch you. Leave bait farther from the square.");
    }

    static void RefreshSettlementsIfNeeded()
    {
        if (Time.time < nextSettlementScan && cachedSettlements.Length > 0)
            return;
        nextSettlementScan = Time.time + 20f;
        cachedSettlements = Object.FindObjectsByType<Settlement>();
    }

    static Settlement NearestSettlement(Vector3 position, float maxRange)
    {
        RefreshSettlementsIfNeeded();
        Settlement best = null;
        float bestDist = maxRange;
        for (int i = 0; i < cachedSettlements.Length; i++)
        {
            Settlement s = cachedSettlements[i];
            if (s == null)
                continue;
            float d = FlatDistance(position, s.center);
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }

        return best;
    }

    static int SettlementKey(Settlement s)
    {
        Vector3 c = s.center;
        return Mathf.RoundToInt(c.x) * 73856093 ^ Mathf.RoundToInt(c.z) * 19349663;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
    }
}

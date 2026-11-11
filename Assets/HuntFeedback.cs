using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One place where shots, hits and kills are reported. The HUD reads it for hit markers
/// and kill banners, and the pause menu reads it for the hunt log.
/// </summary>
public static class HuntFeedback
{
    public struct Banner
    {
        public string title;
        public string detail;
        public bool trophy;
    }

    // Hit marker state, polled by the HUD.
    public static float LastHitTime { get; private set; } = -100f;
    public static bool LastHitKilled { get; private set; }
    public static bool LastHitVital { get; private set; }

    // Hunt log.
    public static int ShotsFired { get; private set; }
    public static int ShotsLanded { get; private set; }
    public static int Kills { get; private set; }
    public static float LongestKill { get; private set; }
    public static int MeatTaken { get; private set; }
    public static float HuntStartTime { get; private set; } = -1f;

    static readonly Dictionary<AnimalKind, int> killsByKind = new Dictionary<AnimalKind, int>();
    static readonly Queue<Banner> banners = new Queue<Banner>();
    static bool shotPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlayMode()
    {
        LastHitTime = -100f;
        LastHitKilled = false;
        LastHitVital = false;
        ShotsFired = 0;
        ShotsLanded = 0;
        Kills = 0;
        LongestKill = 0f;
        MeatTaken = 0;
        HuntStartTime = -1f;
        killsByKind.Clear();
        banners.Clear();
        shotPending = false;
    }

    public static float Accuracy01 => ShotsFired > 0 ? Mathf.Clamp01((float)ShotsLanded / ShotsFired) : 0f;

    public static int KillsOf(AnimalKind kind)
    {
        return killsByKind.TryGetValue(kind, out int n) ? n : 0;
    }

    public static void ShotFired()
    {
        if (HuntStartTime < 0f)
            HuntStartTime = Time.time;
        ShotsFired++;
        shotPending = true;
    }

    /// <summary>A wound that did not kill. Place is "Head", "Chest", "Body" or "Legs".</summary>
    public static void Hit(string place)
    {
        CountLanded();
        LastHitTime = Time.unscaledTime;
        LastHitKilled = false;
        LastHitVital = place == "Head" || place == "Chest";
    }

    public static void Kill(Animal animal, string place, float distance, int meat)
    {
        CountLanded();
        LastHitTime = Time.unscaledTime;
        LastHitKilled = true;
        LastHitVital = place == "Head" || place == "Chest";

        AnimalKind kind = animal != null ? animal.kind : AnimalKind.Deer;
        Kills++;
        killsByKind[kind] = KillsOf(kind) + 1;
        MeatTaken += Mathf.Max(0, meat);
        bool record = distance > LongestKill + 0.5f && Kills > 1;
        if (distance > LongestKill)
            LongestKill = distance;

        int xp = HuntProgress.OnKill(animal, place, distance);
        HuntExtras.NotifyKill(animal, distance);
        AnimalTrophy trophy = animal != null ? animal.GetComponent<AnimalTrophy>() : null;

        string detail = "";
        if (!string.IsNullOrEmpty(place))
            detail = place.ToUpperInvariant() + " SHOT";
        if (trophy != null)
            detail += (detail.Length > 0 ? "   ·   " : "") + Mathf.RoundToInt(trophy.weightKg) + " KG";
        if (distance > 0.5f)
            detail += (detail.Length > 0 ? "   ·   " : "") + Mathf.RoundToInt(distance) + " m";
        if (meat > 0)
            detail += (detail.Length > 0 ? "   ·   " : "") + "+" + meat + " MEAT";
        if (record)
            detail += "   ·   LONGEST SHOT";
        if (xp > 0)
            detail += "   ·   +" + xp + " XP";

        banners.Enqueue(new Banner
        {
            title = KindTitle(animal) + " DOWN",
            detail = detail,
            trophy = kind == AnimalKind.Legendary || record || (trophy != null && trophy.tier >= AnimalTrophy.Tier.Trophy),
        });
        while (banners.Count > 4)
            banners.Dequeue();
    }

    /// <summary>Queues a banner for anything worth a moment on screen: rank ups, contracts, rare sightings.</summary>
    public static void PushBanner(string title, string detail, bool gold)
    {
        banners.Enqueue(new Banner { title = title, detail = detail, trophy = gold });
        while (banners.Count > 4)
            banners.Dequeue();
    }

    public static bool TryTakeBanner(out Banner banner)
    {
        if (banners.Count > 0)
        {
            banner = banners.Dequeue();
            return true;
        }

        banner = default;
        return false;
    }

    public static string KindTitle(Animal animal)
    {
        if (animal == null)
            return "ANIMAL";

        AnimalTrophy trophy = animal.GetComponent<AnimalTrophy>();
        return (trophy != null ? trophy.Prefix() : "") + SpeciesTitle(animal);
    }

    public static string SpeciesTitle(Animal animal)
    {
        if (animal == null)
            return "ANIMAL";

        switch (animal.kind)
        {
            case AnimalKind.Bird:
                return FlyerProfile.Of(animal.flyer).title;
            case AnimalKind.Game:
                return QuarryProfile.Of(animal.quarry).title;
            case AnimalKind.Large: return "WILD HORSE";
            case AnimalKind.Legendary: return "LEGENDARY TIGER";
            default: return "DEER";
        }
    }

    static void CountLanded()
    {
        // A shotgun blast can wound several animals; it still counts as one shot that landed.
        if (!shotPending)
            return;
        shotPending = false;
        ShotsLanded++;
    }
}

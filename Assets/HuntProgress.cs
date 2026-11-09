using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The hunter's career: XP from kills, ranks that unlock perks, daily contracts, and the
/// per-species records shown in the journal. Every kill reaches it through HuntFeedback.Kill.
/// </summary>
public static class HuntProgress
{
    // ---------------------------------------------------------------- ranks & perks

    public struct Rank
    {
        public string title;
        public int xp;
        public string perk;
        public string perkText;
    }

    public static readonly Rank[] Ranks =
    {
        new Rank { title = "GREENHORN", xp = 0, perk = "", perkText = "Every hunter starts somewhere." },
        new Rank { title = "TRACKER", xp = 150, perk = "STEADY HANDS", perkText = "Hold your breath 40% longer on the scope." },
        new Rank { title = "STALKER", xp = 400, perk = "LIGHT STEP", perkText = "Your footsteps are 25% quieter to animals." },
        new Rank { title = "BUTCHER", xp = 800, perk = "FIELD DRESSING", perkText = "Every deer or larger gives 1 extra meat." },
        new Rank { title = "MARKSMAN", xp = 1400, perk = "ICE VEINS", perkText = "No buck fever, and 25% less scope sway." },
        new Rank { title = "APEX HUNTER", xp = 2200, perk = "PREDATOR SENSE", perkText = "Hunter's Instinct reaches 60% farther and recharges faster." },
    };

    public static int Xp { get; private set; }
    public static int RankIndex { get; private set; }
    public static Rank Current => Ranks[RankIndex];
    public static bool IsMaxRank => RankIndex >= Ranks.Length - 1;

    public static float RankProgress01
    {
        get
        {
            if (IsMaxRank)
                return 1f;
            int from = Ranks[RankIndex].xp;
            int to = Ranks[RankIndex + 1].xp;
            return Mathf.Clamp01((Xp - from) / (float)Mathf.Max(1, to - from));
        }
    }

    public static bool Has(int rank) => RankIndex >= rank;

    // Perk effects other systems read.
    public static float BreathMultiplier => Has(1) ? 1.4f : 1f;
    public static float FootstepNoise => Has(2) ? 0.75f : 1f;
    public static int BonusMeat => Has(3) ? 1 : 0;
    public static bool NoBuckFever => Has(4);
    public static float SwayMultiplier => Has(4) ? 0.75f : 1f;
    public static float InstinctRange => Has(5) ? 160f : 100f;
    public static float InstinctCooldown => Has(5) ? 24f : 40f;

    // ---------------------------------------------------------------- journal

    public sealed class SpeciesRecord
    {
        public int kills;
        public float heaviest;
        public float longest;
        public int trophies;
        public int rare;
    }

    static readonly Dictionary<AnimalKind, SpeciesRecord> records = new Dictionary<AnimalKind, SpeciesRecord>();

    public static SpeciesRecord RecordFor(AnimalKind kind)
    {
        if (!records.TryGetValue(kind, out SpeciesRecord r))
        {
            r = new SpeciesRecord();
            records[kind] = r;
        }
        return r;
    }

    // ---------------------------------------------------------------- contracts

    public sealed class Contract
    {
        public string text;
        public int need;
        public int have;
        public int reward;
        public System.Func<KillInfo, bool> counts;
        public bool Done => have >= need;
    }

    public struct KillInfo
    {
        public Animal animal;
        public AnimalKind kind;
        public string place;
        public float distance;
        public HuntWeapon weapon;
        public bool night;
        public bool crouching;
        public AnimalTrophy.Tier tier;
    }

    public static readonly List<Contract> Contracts = new List<Contract>();
    static int contractDay = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlayMode()
    {
        Xp = 0;
        RankIndex = 0;
        records.Clear();
        Contracts.Clear();
        contractDay = -1;
    }

    /// <summary>New contracts each in-game day. Called by the HUD every frame; cheap when nothing changes.</summary>
    public static void TickContracts()
    {
        int day = DayNightCycle.Instance != null ? DayNightCycle.Instance.Day : 1;
        if (day == contractDay)
            return;

        bool first = contractDay < 0;
        contractDay = day;
        RollContracts(day);
        if (!first && Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Day " + day + ". New hunting contracts are posted.");
    }

    static void RollContracts(int day)
    {
        Contracts.Clear();
        System.Random rng = new System.Random(day * 7919 + 17);
        List<Contract> pool = new List<Contract>
        {
            new Contract { text = "Take a deer with the rifle", need = 1, reward = 60,
                counts = k => k.kind == AnimalKind.Deer && k.weapon == HuntWeapon.Rifle },
            new Contract { text = "Head-shot a deer", need = 1, reward = 80,
                counts = k => k.kind == AnimalKind.Deer && k.place == "Head" },
            new Contract { text = "Make a kill from 120 m or more", need = 1, reward = 90,
                counts = k => k.distance >= 120f },
            new Contract { text = "Shoot 2 birds with the shotgun", need = 2, reward = 50,
                counts = k => k.kind == AnimalKind.Bird && k.weapon == HuntWeapon.Shotgun },
            new Contract { text = "Hunt something at night", need = 1, reward = 70,
                counts = k => k.night },
            new Contract { text = "Bring down a Prime animal or better", need = 1, reward = 75,
                counts = k => k.tier >= AnimalTrophy.Tier.Prime },
            new Contract { text = "Take 2 deer while crouched", need = 2, reward = 70,
                counts = k => k.kind == AnimalKind.Deer && k.crouching },
            new Contract { text = "Drop a deer with one chest shot", need = 1, reward = 60,
                counts = k => k.kind == AnimalKind.Deer && k.place == "Chest" },
            new Contract { text = "Kill 3 animals today", need = 3, reward = 60,
                counts = k => true },
            new Contract { text = "Bag a wild horse", need = 1, reward = 110,
                counts = k => k.kind == AnimalKind.Large },
        };

        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int pick = rng.Next(pool.Count);
            Contracts.Add(pool[pick]);
            pool.RemoveAt(pick);
        }
    }

    // ---------------------------------------------------------------- kills

    /// <summary>Called once per kill. Returns the XP it earned.</summary>
    public static int OnKill(Animal animal, string place, float distance)
    {
        TickContracts();

        AnimalTrophy trophy = animal != null ? animal.GetComponent<AnimalTrophy>() : null;
        KillInfo info = new KillInfo
        {
            animal = animal,
            kind = animal != null ? animal.kind : AnimalKind.Deer,
            place = place ?? "",
            distance = distance,
            weapon = PlayerShotgun.Instance != null ? PlayerShotgun.Instance.Equipped : HuntWeapon.None,
            night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight,
            crouching = PlayerMove.IsCrouching,
            tier = trophy != null ? trophy.tier : AnimalTrophy.Tier.Common,
        };

        // Records for the journal.
        SpeciesRecord r = RecordFor(info.kind);
        r.kills++;
        if (distance > r.longest)
            r.longest = distance;
        if (trophy != null && trophy.weightKg > r.heaviest)
            r.heaviest = trophy.weightKg;
        if (info.tier >= AnimalTrophy.Tier.Trophy)
            r.trophies++;
        if (trophy != null && trophy.IsRare)
            r.rare++;

        // XP: species, quality, clean shot, range.
        float xp;
        switch (info.kind)
        {
            case AnimalKind.Bird: xp = 10f; break;
            case AnimalKind.Large: xp = 50f; break;
            case AnimalKind.Legendary: xp = 200f; break;
            default: xp = 25f; break;
        }

        xp *= AnimalTrophy.XpMultiplier(info.tier);
        if (info.place == "Head" || info.place == "Chest")
            xp += 10f;
        xp += Mathf.Floor(distance / 10f);
        int earned = Mathf.RoundToInt(xp);

        foreach (Contract c in Contracts)
        {
            if (c.Done || c.counts == null || !c.counts(info))
                continue;
            c.have++;
            if (c.Done)
            {
                earned += c.reward;
                HuntFeedback.PushBanner("CONTRACT COMPLETE", c.text.ToUpperInvariant() + "   ·   +" + c.reward + " XP", true);
            }
        }

        AddXp(earned);
        return earned;
    }

    public static void AddXp(int amount)
    {
        if (amount <= 0)
            return;

        Xp += amount;
        while (!IsMaxRank && Xp >= Ranks[RankIndex + 1].xp)
        {
            RankIndex++;
            Rank rank = Ranks[RankIndex];
            HuntFeedback.PushBanner("RANK UP  ·  " + rank.title, rank.perk + "   ·   " + rank.perkText.ToUpperInvariant(), true);
        }
    }
}

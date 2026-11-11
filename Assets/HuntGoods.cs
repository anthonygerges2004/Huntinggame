using UnityEngine;

/// <summary>How cleanly a pelt came off the animal. Set by the killing shot, then by the skinning.</summary>
public enum PeltCondition
{
    Pristine,
    Good,
    Fair,
    Torn,
}

/// <summary>
/// A carcass or a pelt carried in the bag. One object per animal, so every hide keeps its own
/// species, quality, condition and age. The tanner turns a carcass into a pelt plus meat,
/// and buys pelts for coins; the price is worked out here.
/// </summary>
public sealed class HuntGoods
{
    // Carcass freshness: full value for a while, then it slides until the meat turns.
    public const float FreshSeconds = 300f;
    public const float SpoilSeconds = 600f;
    public const float RoughSkinMultiplier = 0.65f;
    public const float RoughMeatShare = 0.6f;

    public AnimalKind kind;
    public QuarryKind quarry;
    public AnimalTrophy.Tier tier;
    public AnimalTrophy.Coat coat;
    public string species = "DEER";
    public float weightKg = 70f;
    public int meat = 2;
    public PeltCondition condition = PeltCondition.Good;
    public Color fur = new Color(0.55f, 0.38f, 0.22f);
    public bool stripes;

    /// <summary>Time.time when the animal died. Only matters while it is still a carcass.</summary>
    public float killedAt;

    /// <summary>Set once skinned: the freshness the hide had when it came off, and whether you did it yourself.</summary>
    public bool skinned;
    public bool spoilAnnounced;
    public bool rough;
    public float freshnessAtSkinning = 1f;

    // ---------------------------------------------------------------- naming

    public string Prefix
    {
        get
        {
            if (coat == AnimalTrophy.Coat.Albino) return "ALBINO ";
            if (coat == AnimalTrophy.Coat.Melanistic) return "MELANISTIC ";
            if (tier == AnimalTrophy.Tier.Trophy) return "TROPHY ";
            if (tier == AnimalTrophy.Tier.Prime) return "PRIME ";
            return "";
        }
    }

    public string Title => Prefix + species;

    public string TierLabel
    {
        get
        {
            if (coat == AnimalTrophy.Coat.Albino) return "Rare · albino";
            if (coat == AnimalTrophy.Coat.Melanistic) return "Rare · melanistic";
            switch (tier)
            {
                case AnimalTrophy.Tier.Trophy: return "Trophy";
                case AnimalTrophy.Tier.Prime: return "Prime";
                default: return "Common";
            }
        }
    }

    public string ConditionLabel
    {
        get
        {
            string label = condition.ToString();
            return rough ? label + " · rough cut" : label;
        }
    }

    // ---------------------------------------------------------------- freshness

    /// <summary>1 while fresh, sliding to 0.4 at the spoil time. A skinned pelt keeps the value it had.</summary>
    public float Freshness01(float now)
    {
        if (skinned)
            return freshnessAtSkinning;
        return FreshnessAt(now - killedAt);
    }

    public static float FreshnessAt(float age)
    {
        if (age <= FreshSeconds)
            return 1f;
        if (age >= SpoilSeconds)
            return 0.4f;
        return Mathf.Lerp(1f, 0.4f, (age - FreshSeconds) / (SpoilSeconds - FreshSeconds));
    }

    public bool Spoiled(float now) => !skinned && now - killedAt >= SpoilSeconds;

    public float SecondsUntilSpoiled(float now) => Mathf.Max(0f, SpoilSeconds - (now - killedAt));

    public string FreshnessLabel(float now)
    {
        if (skinned)
            return freshnessAtSkinning >= 0.999f ? "Cured · keeps" : "Cured late · keeps";
        float age = now - killedAt;
        if (age < FreshSeconds) return "Fresh";
        if (age < SpoilSeconds) return "Turning";
        return "Spoiled";
    }

    // ---------------------------------------------------------------- appraisal

    public static int BaseValue(AnimalKind kind, QuarryKind quarry)
    {
        switch (kind)
        {
            case AnimalKind.Large: return 34;
            case AnimalKind.Legendary: return 160;
            case AnimalKind.Game:
                switch (quarry)
                {
                    case QuarryKind.Rabbit: return 6;
                    case QuarryKind.Fox: return 16;
                    case QuarryKind.Pig: return 9;
                    case QuarryKind.Boar: return 20;
                    case QuarryKind.Wolf: return 26;
                    default: return 10;
                }
            default: return 18; // deer
        }
    }

    public static float TierMultiplier(AnimalTrophy.Tier tier, AnimalTrophy.Coat coat)
    {
        if (coat != AnimalTrophy.Coat.Normal)
            return 6f;
        switch (tier)
        {
            case AnimalTrophy.Tier.Prime: return 1.5f;
            case AnimalTrophy.Tier.Trophy: return 2.5f;
            case AnimalTrophy.Tier.Rare: return 6f;
            default: return 1f;
        }
    }

    public static float ConditionMultiplier(PeltCondition condition)
    {
        switch (condition)
        {
            case PeltCondition.Pristine: return 1.2f;
            case PeltCondition.Good: return 1f;
            case PeltCondition.Fair: return 0.75f;
            default: return 0.5f;
        }
    }

    /// <summary>Coins the tanner pays for this hide. A carcass is priced as if he skinned it now.</summary>
    public int PeltValue(float now)
    {
        float value = BaseValue(kind, quarry) * TierMultiplier(tier, coat) * ConditionMultiplier(condition) * Freshness01(now);
        if (rough)
            value *= RoughSkinMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

    /// <summary>
    /// Condition from the killing shot. A clean head shot keeps the hide whole, buckshot up close
    /// riddles it, and an animal hit again and again loses a grade.
    /// </summary>
    public static PeltCondition ConditionFromShot(string place, HuntWeapon weapon, float distance, int hits)
    {
        PeltCondition c;
        if (weapon == HuntWeapon.Shotgun)
            c = distance > 28f ? PeltCondition.Fair : PeltCondition.Torn;
        else if (place == "Head")
            c = PeltCondition.Pristine;
        else if (place == "Chest" || place == "Legs")
            c = PeltCondition.Good;
        else
            c = PeltCondition.Fair;

        if (hits >= 3 && c < PeltCondition.Torn)
            c++;
        return c;
    }

    // ---------------------------------------------------------------- skinning

    /// <summary>Turns this carcass into a pelt. Returns the meat it yields; spoiled carcasses yield bait instead.</summary>
    public int Skin(float now, bool byHand, out bool meatSpoiled)
    {
        meatSpoiled = Spoiled(now);
        freshnessAtSkinning = Freshness01(now);
        skinned = true;
        rough = byHand;
        int yield = byHand ? Mathf.Max(1, Mathf.CeilToInt(meat * RoughMeatShare)) : meat;
        return yield;
    }

    public static HuntGoods FromAnimal(Animal animal, string place, HuntWeapon weapon, float distance, int hits)
    {
        HuntGoods g = new HuntGoods();
        if (animal == null)
            return g;

        g.kind = animal.kind;
        g.quarry = animal.quarry;
        g.meat = Mathf.Max(1, animal.meatAmount);
        g.killedAt = Time.time;
        g.species = HuntFeedback.SpeciesTitle(animal);
        g.condition = ConditionFromShot(place, weapon, distance, hits);

        AnimalTrophy trophy = animal.GetComponent<AnimalTrophy>();
        if (trophy != null)
        {
            g.tier = trophy.tier;
            g.coat = trophy.coat;
            g.weightKg = trophy.weightKg;
        }
        else
            g.weightKg = animal.kind == AnimalKind.Game ? QuarryProfile.Of(animal.quarry).weightKg : 70f;

        g.fur = FurColor(animal.kind, animal.quarry);
        g.stripes = animal.kind == AnimalKind.Legendary;
        if (g.coat == AnimalTrophy.Coat.Albino)
        {
            g.fur = new Color(0.95f, 0.93f, 0.88f);
            g.stripes = false;
        }
        else if (g.coat == AnimalTrophy.Coat.Melanistic)
        {
            g.fur = new Color(0.12f, 0.11f, 0.10f);
            g.stripes = false;
        }

        return g;
    }

    public static Color FurColor(AnimalKind kind, QuarryKind quarry)
    {
        switch (kind)
        {
            case AnimalKind.Large: return new Color(0.48f, 0.28f, 0.16f);
            case AnimalKind.Legendary: return new Color(0.92f, 0.52f, 0.18f);
            case AnimalKind.Game: return QuarryProfile.Of(quarry).body;
            default: return new Color(0.62f, 0.42f, 0.24f);
        }
    }
}

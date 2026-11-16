using HuntingGame.Senses;
using UnityEngine;

namespace HuntingGame.Animals
{
    // Bounded assistance: changes future encounter placement, never teleports live animals.
    public static class WildlifeDirector
    {
        static float lastSuccess;
        static int streak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { lastSuccess = 0f; streak = 0; }

        public static void SuccessfulHunt()
        {
            streak = Time.time - lastSuccess < 180f ? Mathf.Min(streak + 1, 5) : 1;
            lastSuccess = Time.time;
        }

        public static float Caution => 1f +
            Mathf.Clamp01(1f - (Time.time - lastSuccess) / 180f) * streak * 0.04f;

        public static float PreferredDistance
        {
            get
            {
                float health = PlayerHealth.Instance != null
                    ? PlayerHealth.Instance.health / Mathf.Max(1f, PlayerHealth.Instance.maxHealth) : 1f;
                float assistance = Mathf.Max(1f - Mathf.Clamp01(health),
                    Mathf.Clamp01((Time.time - lastSuccess - 180f) / 240f));
                return Mathf.Lerp(78f, 52f, assistance);
            }
        }
    }

    public static class SpeciesCatalog
    {
        static SpeciesData deer;
        static SpeciesData bird;
        static SpeciesData large;
        static SpeciesData legendary;

        static readonly System.Collections.Generic.Dictionary<QuarryKind, SpeciesData> game =
            new System.Collections.Generic.Dictionary<QuarryKind, SpeciesData>();

        public static SpeciesData For(Animal animal)
        {
            if (animal == null)
                return For(AnimalKind.Deer);
            if (animal.kind == AnimalKind.Game)
                return ForGame(animal.quarry == QuarryKind.None ? QuarryKind.Pig : animal.quarry);
            return For(animal.kind);
        }

        public static SpeciesData For(AnimalKind kind)
        {
            if (kind == AnimalKind.Bird)
                return bird != null ? bird : (bird = Make(kind, true, 6.5f, 13f, 1.4f, 5f, 0.7f, 5.5f, 6f, 60f, 150f, 1.35f));
            if (kind == AnimalKind.Large)
                return large != null ? large : (large = Make(kind, false, 2.4f, 7.5f, 2.5f, 6f, 0.8f, 6f, 8f, 40f, 110f, 0f));
            if (kind == AnimalKind.Legendary)
                return legendary != null ? legendary : (legendary = Make(kind, false, 2.6f, 9f, 2f, 6f, 0.6f, 7f, 10f, 50f, 130f, 0f));
            if (kind == AnimalKind.Game)
                return ForGame(QuarryKind.Pig);
            SpeciesData data = deer != null ? deer : (deer = Make(kind, true, 2f, 8f, 2.2f, 7f, 1.15f, 8f, 6f, 210f, 260f, 1.2f));
            data.fullLodRadius = 210f;
            data.reducedLodRadius = 260f;
            return data;
        }

        static SpeciesData ForGame(QuarryKind quarry)
        {
            if (game.TryGetValue(quarry, out SpeciesData cached))
                return cached;

            QuarryProfile profile = QuarryProfile.Of(quarry);
            SpeciesData data = Make(
                AnimalKind.Game,
                true,
                profile.grazeSpeed,
                profile.fleeSpeed,
                profile.idleSeconds,
                profile.grazeSeconds,
                profile.alertSeconds,
                profile.fleeSeconds,
                profile.grazeRadius,
                90f,
                150f,
                profile.downwind);
            data.senses = SenseCatalog.ForQuarry(quarry);
            data.predator = quarry == QuarryKind.Wolf;
            data.prey = !data.predator;
            game[quarry] = data;
            return data;
        }

        static SpeciesData Make(
            AnimalKind kind,
            bool prey,
            float grazeSpeed,
            float fleeSpeed,
            float idleSeconds,
            float grazeSeconds,
            float alertSeconds,
            float fleeSeconds,
            float grazeRadius,
            float fullLod,
            float reducedLod,
            float downwindWeight)
        {
            SpeciesData data = ScriptableObject.CreateInstance<SpeciesData>();
            data.kind = kind;
            data.prey = prey;
            data.senses = SenseCatalog.For(kind);
            data.grazeSpeed = grazeSpeed;
            data.fleeSpeed = fleeSpeed;
            data.idleSeconds = idleSeconds;
            data.grazeSeconds = grazeSeconds;
            data.alertSeconds = alertSeconds;
            data.fleeSeconds = fleeSeconds;
            data.grazeRadius = grazeRadius;
            data.fullLodRadius = fullLod;
            data.reducedLodRadius = reducedLod;
            data.downwindWeight = downwindWeight;
            return data;
        }
    }
}

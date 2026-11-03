using HuntingGame.Senses;
using UnityEngine;

namespace HuntingGame.Animals
{
    public static class SpeciesCatalog
    {
        static SpeciesData deer;
        static SpeciesData bird;
        static SpeciesData large;
        static SpeciesData legendary;

        public static SpeciesData For(AnimalKind kind)
        {
            if (kind == AnimalKind.Bird)
                return bird != null ? bird : (bird = Make(kind, true, 6.5f, 13f, 1.4f, 5f, 0.7f, 5.5f, 6f, 60f, 150f, 1.35f));
            if (kind == AnimalKind.Large)
                return large != null ? large : (large = Make(kind, false, 2.4f, 7.5f, 2.5f, 6f, 0.8f, 6f, 8f, 40f, 110f, 0f));
            if (kind == AnimalKind.Legendary)
                return legendary != null ? legendary : (legendary = Make(kind, false, 2.6f, 9f, 2f, 6f, 0.6f, 7f, 10f, 50f, 130f, 0f));
            return deer != null ? deer : (deer = Make(kind, true, 2f, 8f, 2.2f, 7f, 1.15f, 8f, 6f, 45f, 120f, 1.2f));
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

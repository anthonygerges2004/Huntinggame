using UnityEngine;

namespace HuntingGame.Senses
{
    public static class SenseCatalog
    {
        static AnimalSenseSettings deer;
        static AnimalSenseSettings bird;
        static AnimalSenseSettings large;
        static AnimalSenseSettings legendary;

        static readonly System.Collections.Generic.Dictionary<QuarryKind, AnimalSenseSettings> game =
            new System.Collections.Generic.Dictionary<QuarryKind, AnimalSenseSettings>();

        public static AnimalSenseSettings For(Animal animal)
        {
            if (animal != null && animal.kind == AnimalKind.Game)
                return ForGame(animal.quarry == QuarryKind.None ? QuarryKind.Pig : animal.quarry);
            return For(animal != null ? animal.kind : AnimalKind.Deer);
        }

        public static AnimalSenseSettings For(AnimalKind kind)
        {
            if (kind == AnimalKind.Bird)
                return bird != null ? bird : (bird = Make(40f, 110f, 10f, 5f, 2.2f, 8f, 60f));
            if (kind == AnimalKind.Large)
                return large != null ? large : (large = Make(26f, 75f, 18f, 9f, 4f, 36f, 70f));
            if (kind == AnimalKind.Legendary)
                return legendary != null ? legendary : (legendary = Make(40f, 90f, 22f, 12f, 5f, 48f, 80f));
            return deer != null ? deer : (deer = Make(32f, 80f, 16f, 8f, 3.5f, 26f, 70f));
        }

        public static AnimalSenseSettings ForQuarry(QuarryKind quarry)
        {
            return ForGame(quarry);
        }

        static AnimalSenseSettings ForGame(QuarryKind quarry)
        {
            if (game.TryGetValue(quarry, out AnimalSenseSettings cached))
                return cached;

            QuarryProfile profile = QuarryProfile.Of(quarry);
            AnimalSenseSettings data = Make(
                profile.sight,
                profile.sightDegrees,
                profile.sprintHear,
                profile.walkHear,
                profile.crouchHear,
                profile.smell,
                profile.smellDegrees);
            game[quarry] = data;
            return data;
        }

        static AnimalSenseSettings Make(
            float sight,
            float sightDegrees,
            float sprint,
            float walk,
            float crouch,
            float smell,
            float smellDegrees)
        {
            AnimalSenseSettings settings = ScriptableObject.CreateInstance<AnimalSenseSettings>();
            settings.sightRange = sight;
            settings.sightDegrees = sightDegrees;
            settings.sprintHearRange = sprint;
            settings.walkHearRange = walk;
            settings.crouchHearRange = crouch;
            settings.smellRange = smell;
            settings.smellDegrees = smellDegrees;
            settings.tickInterval = 0.3f;
            settings.reducedRangeScale = 0.6f;
            return settings;
        }
    }
}

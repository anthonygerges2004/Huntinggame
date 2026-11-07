using UnityEngine;

namespace HuntingGame.Senses
{
    /// <summary>
    /// Zero-order Sugeno pick for the gun that can actually land the shot.
    /// The shotgun's pattern is only tight up close. The rifle covers the stalk.
    /// The sniper takes over once the rifle is past a comfortable zero, and an empty gun scores nothing.
    /// </summary>
    public static class GunChoiceFuzzy
    {
        public struct Sample
        {
            public float distance;
            public int shotgunRounds;
            public int shotgunMag;
            public float shotgunRange;
            public int rifleRounds;
            public int rifleMag;
            public float rifleRange;
            public int sniperRounds;
            public int sniperMag;
            public float sniperRange;
            public HuntWeapon equipped;
        }

        public struct Choice
        {
            public HuntWeapon weapon;
            public float shotgun;
            public float rifle;
            public float sniper;
            public bool loadedButOutOfReach;
        }

        public static Choice Choose(Sample sample)
        {
            float shotgunRange = sample.shotgunRange > 1f ? sample.shotgunRange : 120f;
            float rifleRange = sample.rifleRange > 1f ? sample.rifleRange : 240f;
            float sniperRange = sample.sniperRange > 1f ? sample.sniperRange : 500f;
            float distance = Mathf.Max(0f, sample.distance);

            float shotgunAmmo = AmmoWeight(sample.shotgunRounds, sample.shotgunMag);
            float rifleAmmo = AmmoWeight(sample.rifleRounds, sample.rifleMag);
            float sniperAmmo = AmmoWeight(sample.sniperRounds, sample.sniperMag);

            float shotgun = ShotgunFit(distance, shotgunRange) * shotgunAmmo;
            float rifle = RifleFit(distance, rifleRange) * rifleAmmo;
            float sniper = SniperFit(distance, sniperRange) * sniperAmmo;

            HuntWeapon weapon = HuntWeapon.None;
            float best = 0.08f;
            Consider(shotgun, HuntWeapon.Shotgun, ref best, ref weapon);
            Consider(rifle, HuntWeapon.Rifle, ref best, ref weapon);
            Consider(sniper, HuntWeapon.Sniper, ref best, ref weapon);

            float held = HeldScore(sample.equipped, shotgun, rifle, sniper);
            if (sample.equipped != HuntWeapon.None && held >= 0.08f && held >= best - 0.1f)
                weapon = sample.equipped;

            bool loaded = shotgunAmmo > 0f || rifleAmmo > 0f || sniperAmmo > 0f;
            return new Choice
            {
                weapon = weapon,
                shotgun = shotgun,
                rifle = rifle,
                sniper = sniper,
                loadedButOutOfReach = weapon == HuntWeapon.None && loaded
            };
        }

        static void Consider(float score, HuntWeapon id, ref float best, ref HuntWeapon weapon)
        {
            if (score <= best)
                return;

            best = score;
            weapon = id;
        }

        static float HeldScore(HuntWeapon equipped, float shotgun, float rifle, float sniper)
        {
            switch (equipped)
            {
                case HuntWeapon.Shotgun: return shotgun;
                case HuntWeapon.Rifle: return rifle;
                case HuntWeapon.Sniper: return sniper;
                default: return 0f;
            }
        }

        // Ten degrees of spread. Past roughly 30 m the pattern is wider than the deer.
        static float ShotgunFit(float distance, float range)
        {
            if (distance > range)
                return 0f;

            return RampDown(distance, 12f, 30f);
        }

        // Accurate enough to hunt with, including a point-blank fallback when the shotgun is empty.
        static float RifleFit(float distance, float range)
        {
            if (distance > range)
                return 0f;

            float comfort = Mathf.Min(RampUp(distance, 18f, 40f), RampDown(distance, range * 0.62f, range));
            float pointBlank = 0.48f * RampDown(distance, 0f, 45f);
            return Mathf.Max(comfort, pointBlank);
        }

        // The scope is the wrong tool up close. It earns the shot once the rifle's zero is getting long.
        static float SniperFit(float distance, float range)
        {
            if (distance > range)
                return 0f;

            float scoped = Mathf.Min(RampUp(distance, 110f, 190f), RampDown(distance, range * 0.82f, range));
            float fallback = 0.14f * RampDown(distance, 0f, 80f);
            return Mathf.Max(scoped, fallback);
        }

        static float AmmoWeight(int rounds, int mag)
        {
            if (rounds <= 0)
                return 0f;

            float ratio = mag < 1 ? 1f : Mathf.Clamp01(rounds / (float)mag);
            return Mathf.Lerp(0.72f, 1f, RampUp(ratio, 0.05f, 0.55f));
        }

        static float RampUp(float x, float a, float b)
        {
            if (x <= a)
                return 0f;
            if (x >= b)
                return 1f;
            return (x - a) / Mathf.Max(0.0001f, b - a);
        }

        static float RampDown(float x, float a, float b)
        {
            if (x <= a)
                return 1f;
            if (x >= b)
                return 0f;
            return (b - x) / Mathf.Max(0.0001f, b - a);
        }
    }
}

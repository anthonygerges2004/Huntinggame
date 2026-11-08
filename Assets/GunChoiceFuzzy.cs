using UnityEngine;

namespace HuntingGame.Senses
{
    /// <summary>
    /// Zero-order Sugeno pick for the gun that can land the shot.
    /// Distance sets the base fit. The animal in the sights moves it:
    /// a small, fast, or alert target favors the shotgun, a running or windy
    /// shot takes the sniper off, and an empty gun still scores nothing.
    /// A calm still deer in a light wind keeps the original distance picks.
    ///
    /// Examples, full magazines, standard bands:
    /// calm still deer, 16 m, wind 0.2 → shotgun
    /// calm still deer, 80 m, wind 0.2 → rifle
    /// calm still deer, 200 m, wind 0.2 → sniper
    /// bird (size 0), 30 m → shotgun
    /// bird (size 0), 200 m → rifle
    /// deer fleeing (speed 1, alert 1), 20 m → shotgun
    /// deer fleeing (speed 1, alert 1), 200 m → rifle
    /// large (size 1), calm still, 25 m → shotgun
    /// large (size 1), calm still, 80 m → rifle
    /// calm still deer, 200 m, wind 0.9 → rifle
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

        /// <summary>0 bird, 0.5 deer, 1 large or legendary. Speed 0 is standing, 1 is a flat-out run.</summary>
        public struct Target
        {
            public float size;
            public float speed;
            public float alertness;
            public float wind;

            public static Target Deer()
            {
                return new Target
                {
                    size = 0.5f,
                    speed = 0f,
                    alertness = 0f,
                    wind = 0.2f
                };
            }
        }

        [System.Serializable]
        public struct Bands
        {
            public float shotgunCloseFull;
            public float shotgunCloseGone;
            public float rifleRiseStart;
            public float rifleRiseFull;
            public float riflePointBlank;
            public float riflePointBlankGone;
            public float sniperRiseStart;
            public float sniperRiseFull;
            public float sniperFallback;
            public float sniperFallbackGone;

            public float smallGone;
            public float bigStart;
            public float bigFull;
            public float stillFull;
            public float stillGone;
            public float fastStart;
            public float fastFull;
            public float calmFull;
            public float calmGone;
            public float alertStart;
            public float alertFull;
            public float windCalmFull;
            public float windCalmGone;

            public float smallGunStart;
            public float smallGunGone;
            public float fastGunStart;
            public float fastGunGone;
            public float bigGunStart;
            public float bigGunGone;
            public float smallRiflePenalty;
            public float smallRifleFloor;

            public float scoreFloor;
            public float holdMargin;

            public static Bands Standard()
            {
                return new Bands
                {
                    shotgunCloseFull = 12f,
                    shotgunCloseGone = 30f,
                    rifleRiseStart = 18f,
                    rifleRiseFull = 40f,
                    riflePointBlank = 0.48f,
                    riflePointBlankGone = 45f,
                    sniperRiseStart = 110f,
                    sniperRiseFull = 190f,
                    sniperFallback = 0.14f,
                    sniperFallbackGone = 80f,
                    smallGone = 0.45f,
                    bigStart = 0.55f,
                    bigFull = 0.8f,
                    stillFull = 0.15f,
                    stillGone = 0.4f,
                    fastStart = 0.55f,
                    fastFull = 0.8f,
                    calmFull = 0.25f,
                    calmGone = 0.5f,
                    alertStart = 0.45f,
                    alertFull = 0.7f,
                    windCalmFull = 0.35f,
                    windCalmGone = 0.6f,
                    smallGunStart = 35f,
                    smallGunGone = 55f,
                    fastGunStart = 18f,
                    fastGunGone = 40f,
                    bigGunStart = 16f,
                    bigGunGone = 40f,
                    smallRiflePenalty = 0.55f,
                    smallRifleFloor = 0.45f,
                    scoreFloor = 0.08f,
                    holdMargin = 0.1f
                };
            }
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
            return Choose(sample, Target.Deer(), Bands.Standard());
        }

        public static Choice Choose(Sample sample, Target target, Bands bands)
        {
            float shotgunRange = sample.shotgunRange > 1f ? sample.shotgunRange : 120f;
            float rifleRange = sample.rifleRange > 1f ? sample.rifleRange : 240f;
            float sniperRange = sample.sniperRange > 1f ? sample.sniperRange : 500f;
            float distance = Mathf.Max(0f, sample.distance);

            float shotgunAmmo = AmmoWeight(sample.shotgunRounds, sample.shotgunMag);
            float rifleAmmo = AmmoWeight(sample.rifleRounds, sample.rifleMag);
            float sniperAmmo = AmmoWeight(sample.sniperRounds, sample.sniperMag);

            float small = RampDown(target.size, 0f, bands.smallGone);
            float big = RampUp(target.size, bands.bigStart, bands.bigFull);
            float fast = RampUp(target.speed, bands.fastStart, bands.fastFull);
            float alert = RampUp(target.alertness, bands.alertStart, bands.alertFull);
            float still = RampDown(target.speed, bands.stillFull, bands.stillGone);
            float calm = RampDown(target.alertness, bands.calmFull, bands.calmGone);
            float calmWind = RampDown(target.wind, bands.windCalmFull, bands.windCalmGone);

            float shotgun = 0f;
            if (distance <= shotgunRange)
            {
                float close = ShotgunFit(distance, shotgunRange, bands);
                float smallReach = Mathf.Min(small, RampDown(distance, bands.smallGunStart, bands.smallGunGone));
                float rush = Mathf.Min(Mathf.Max(fast, alert), RampDown(distance, bands.fastGunStart, bands.fastGunGone));
                float bulk = Mathf.Min(big, RampDown(distance, bands.bigGunStart, bands.bigGunGone));
                shotgun = Mathf.Max(close, Mathf.Max(smallReach, Mathf.Max(rush, bulk)));
            }

            shotgun *= shotgunAmmo;

            float rifleAnimal = Mathf.Max(bands.smallRifleFloor, 1f - bands.smallRiflePenalty * small);
            float rifle = RifleFit(distance, rifleRange, bands) * rifleAmmo * rifleAnimal;

            float sniperAnimal = Mathf.Min(1f - small, Mathf.Min(still, Mathf.Min(calm, calmWind)));
            float sniper = SniperFit(distance, sniperRange, bands) * sniperAmmo * sniperAnimal;

            float floor = bands.scoreFloor > 0f ? bands.scoreFloor : 0.08f;
            HuntWeapon weapon = HuntWeapon.None;
            float best = floor;
            Consider(shotgun, HuntWeapon.Shotgun, ref best, ref weapon);
            Consider(rifle, HuntWeapon.Rifle, ref best, ref weapon);
            Consider(sniper, HuntWeapon.Sniper, ref best, ref weapon);

            float margin = bands.holdMargin > 0f ? bands.holdMargin : 0.1f;
            float held = HeldScore(sample.equipped, shotgun, rifle, sniper);
            if (sample.equipped != HuntWeapon.None && held >= floor && held >= best - margin)
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

        static float ShotgunFit(float distance, float range, Bands bands)
        {
            if (distance > range)
                return 0f;

            return RampDown(distance, bands.shotgunCloseFull, bands.shotgunCloseGone);
        }

        static float RifleFit(float distance, float range, Bands bands)
        {
            if (distance > range)
                return 0f;

            float comfort = Mathf.Min(
                RampUp(distance, bands.rifleRiseStart, bands.rifleRiseFull),
                RampDown(distance, range * 0.62f, range));
            float pointBlank = bands.riflePointBlank * RampDown(distance, 0f, bands.riflePointBlankGone);
            return Mathf.Max(comfort, pointBlank);
        }

        static float SniperFit(float distance, float range, Bands bands)
        {
            if (distance > range)
                return 0f;

            float scoped = Mathf.Min(
                RampUp(distance, bands.sniperRiseStart, bands.sniperRiseFull),
                RampDown(distance, range * 0.82f, range));
            float fallback = bands.sniperFallback * RampDown(distance, 0f, bands.sniperFallbackGone);
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

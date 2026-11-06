using UnityEngine;

namespace HuntingGame.Senses
{
    /// <summary>
    /// Zero-order Sugeno score for how threatening the hunter is to a deer.
    /// Deer notice a scent or a still shape and stare. They sprint when the hunter
    /// is very close, moves in view, or makes a sudden noise.
    /// </summary>
    public static class DeerThreatFuzzy
    {
        public struct Sample
        {
            public float distance;
            public float smellRange;
            public float hearRange;
            public float sightRange;
            /// <summary>1 = scent blowing toward the deer, -1 = scent blowing away.</summary>
            public float windAlignment;
            /// <summary>1 = looking straight at the hunter. Ignored when the view is blocked.</summary>
            public float viewDot;
            public float viewEdgeDot;
            public bool viewBlocked;
            /// <summary>0–1 of the hunter's body with a clear line. 0 keeps a full look, for older samples.</summary>
            public float viewClear;
            /// <summary>0 = still or crouched, 0.5 = walking, 1 = sprinting. Hear range must be &gt; 0.</summary>
            public float noise;
            /// <summary>0–1 day cycle. Negative means daytime, so tests are not tied to the clock.</summary>
            public float timeOfDay;
        }

        public static float Evaluate(Sample sample)
        {
            float smell = SmellScore(sample);
            float hear = HearScore(sample);
            float sight = SightScore(sample, BandAt(sample.timeOfDay));

            float threat = Mathf.Max(smell, Mathf.Max(hear, sight));
            int strong = (smell > 0.45f ? 1 : 0) + (hear > 0.45f ? 1 : 0) + (sight > 0.45f ? 1 : 0);
            if (strong >= 2)
                threat = Mathf.Min(1f, threat + 0.06f);
            return threat;
        }

        static float SmellScore(Sample sample)
        {
            if (sample.smellRange < 0.5f || sample.distance > sample.smellRange * 1.05f)
                return 0f;

            float ratio = sample.distance / sample.smellRange;
            float close = Close(ratio);
            float near = Near(ratio);
            float far = Far(ratio);
            float toward = Toward(sample.windAlignment);
            float cross = Cross(sample.windAlignment);
            float away = Away(sample.windAlignment);

            // Scent at hunting distance makes a deer look up. It runs when the
            // hunter is in its nose, not from a faint smell twenty meters off.
            return Sugeno(
                Weight(VeryClose(ratio), toward), 0.82f,
                Weight(close, toward), 0.68f,
                Weight(near, toward), 0.46f,
                Weight(far, toward), 0.30f,
                Weight(VeryClose(ratio), cross), 0.36f,
                Weight(close, cross), 0.16f,
                Weight(near, away), 0.06f,
                Weight(far, away), 0.04f);
        }

        static float HearScore(Sample sample)
        {
            if (sample.hearRange < 0.5f || sample.distance > sample.hearRange * 1.05f)
                return 0f;

            float ratio = sample.distance / sample.hearRange;
            float close = Close(ratio);
            float near = Near(ratio);
            float far = Far(ratio);
            float sprint = RampUp(sample.noise, 0.65f, 0.9f);
            float walk = Trap(sample.noise, 0.3f, 0.45f, 0.55f, 0.75f);
            float quiet = RampDown(sample.noise, 0.2f, 0.45f);

            // Sprinting nearby is loud even when the deer is looking the other way.
            return Sugeno(
                Weight(sprint, close), 0.96f,
                Weight(sprint, near), 0.9f,
                Weight(sprint, far), 0.64f,
                Weight(walk, close), 0.74f,
                Weight(walk, near), 0.72f,
                Weight(walk, far), 0.18f,
                Weight(quiet, close), 0.3f,
                Weight(quiet, near), 0.14f);
        }

        static float SightScore(Sample sample, Daylight light)
        {
            if (sample.viewBlocked || sample.sightRange < 0.5f || sample.distance > sample.sightRange * 1.05f)
                return 0f;

            float ratio = sample.distance / sample.sightRange;
            float close = Close(ratio);
            float near = Near(ratio);
            float far = Far(ratio);
            float facingLo = Mathf.Min(Mathf.Lerp(sample.viewEdgeDot, 1f, 0.55f), 0.98f);
            float facing = RampUp(sample.viewDot, facingLo, 1f);
            float edge = Edge(sample.viewDot, sample.viewEdgeDot);
            if (facing + edge <= 0.001f)
                return 0f;

            // A still hunter is a shape they stare at. Movement is what makes them bolt,
            // unless the hunter is already close enough to be unmistakable.
            float moving = RampUp(sample.noise, 0.35f, 0.55f);
            float still = 1f - moving;
            float very = VeryClose(ratio);
            float score = Sugeno(
                Weight(facing, very), 0.86f,
                Weight(Weight(facing, close), moving), 0.84f,
                Weight(Weight(facing, close), still), 0.50f,
                Weight(Weight(facing, near), moving), 0.74f,
                Weight(Weight(facing, near), still), 0.42f,
                Weight(Weight(facing, far), moving), 0.56f,
                Weight(Weight(edge, close), moving), 0.64f,
                Weight(Weight(edge, close), still), 0.30f);

            // The same look is more alarming at dusk than at noon, and weaker in full dark.
            score = Mathf.Min(1f, score + 0.12f * light.dusk * Mathf.Clamp01(facing + edge));
            score *= Mathf.Lerp(1f, 0.72f, light.night);
            // Cover is a fuzzy AND with the look: a half-hidden hunter is only half as alarming.
            // 0 means this sample did not measure cover, so the look stands as it is.
            float clear = sample.viewClear > 0f ? Mathf.Clamp01(sample.viewClear) : 1f;
            score *= clear;
            return score;
        }

        struct Daylight
        {
            public float dusk;
            public float night;
        }

        static Daylight BandAt(float time)
        {
            if (time < 0f)
                return new Daylight { dusk = 0f, night = 0f };

            time = Mathf.Repeat(time, 1f);
            return new Daylight
            {
                dusk = Mathf.Max(RampDown(time, 0.02f, 0.12f), Trap(time, 0.34f, 0.46f, 0.52f, 0.62f)),
                night = Trap(time, 0.5f, 0.58f, 0.68f, 0.78f)
            };
        }

        static float Sugeno(
            float w0, float z0,
            float w1, float z1,
            float w2, float z2,
            float w3, float z3,
            float w4, float z4,
            float w5, float z5,
            float w6, float z6,
            float w7, float z7)
        {
            float weight = w0 + w1 + w2 + w3 + w4 + w5 + w6 + w7;
            if (weight <= 0.0001f)
                return 0f;

            float sum = w0 * z0 + w1 * z1 + w2 * z2 + w3 * z3 + w4 * z4 + w5 * z5 + w6 * z6 + w7 * z7;
            return Mathf.Clamp01(sum / weight);
        }

        static float Weight(float a, float b)
        {
            return Mathf.Min(a, b);
        }

        static float VeryClose(float ratio)
        {
            return RampDown(ratio, 0.12f, 0.22f);
        }

        static float Close(float ratio)
        {
            return RampDown(ratio, 0.35f, 0.65f);
        }

        static float Near(float ratio)
        {
            return Trap(ratio, 0.25f, 0.45f, 0.7f, 1f);
        }

        static float Far(float ratio)
        {
            return RampUp(ratio, 0.72f, 1.02f);
        }

        static float Toward(float alignment)
        {
            return RampUp(alignment, 0.15f, 0.75f);
        }

        static float Cross(float alignment)
        {
            return Trap(alignment, -0.55f, -0.15f, 0.15f, 0.55f);
        }

        static float Away(float alignment)
        {
            return RampDown(alignment, -0.75f, -0.15f);
        }

        static float Edge(float dot, float edgeDot)
        {
            float inner = Mathf.Lerp(edgeDot, 1f, 0.35f);
            float outer = edgeDot - 0.22f;
            return Trap(dot, outer, edgeDot, edgeDot, inner);
        }

        static float Trap(float x, float a, float b, float c, float d)
        {
            if (x <= a || x >= d)
                return 0f;
            if (x >= b && x <= c)
                return 1f;
            if (x < b)
                return (x - a) / Mathf.Max(0.0001f, b - a);
            return (d - x) / Mathf.Max(0.0001f, d - c);
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

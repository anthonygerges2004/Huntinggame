using HuntingGame.Senses;
using HuntingGame.Tracking;
using UnityEngine;

namespace HuntingGame.Animals
{
    [RequireComponent(typeof(Animal))]
    public class AnimalAiAgent : MonoBehaviour, IAnimalAi
    {
        Animal animal;
        BirdFlight birdFlight;
        AnimalBrain brain;
        IAnimalSenses senses;
        IAnimalTracks tracks;
        Transform body;
        AnimalAiBand band = AnimalAiBand.Full;
        float lastTick;
        float nextTick;
        float nextSense;
        bool registered;

        public Transform Body => body;
        public AnimalAiBand Band => band;

        void Awake()
        {
            animal = GetComponent<Animal>();
            birdFlight = GetComponent<BirdFlight>();
            body = transform;

            brain = GetComponent<AnimalBrain>();
            if (brain == null)
                brain = gameObject.AddComponent<AnimalBrain>();

            AnimalSenses senseBody = GetComponent<AnimalSenses>();
            if (senseBody == null)
                senseBody = gameObject.AddComponent<AnimalSenses>();
            senses = senseBody;

            if (animal.kind != AnimalKind.Bird)
            {
                AnimalTracks trackBody = GetComponent<AnimalTracks>();
                if (trackBody == null)
                    trackBody = gameObject.AddComponent<AnimalTracks>();
                tracks = trackBody;
            }
        }

        void OnEnable()
        {
            if (registered)
                return;

            AnimalAiScheduler.Register(this);
            registered = true;
        }

        void OnDisable()
        {
            if (!registered)
                return;

            AnimalAiScheduler.Unregister(this);
            registered = false;
            if (animal != null)
                animal.SetSuspendSelfTick(false);
            if (birdFlight != null)
                birdFlight.enabled = true;
        }

        public void Evaluate(float distanceSqr, float fullSqr, float reducedSqr)
        {
            if (!isActiveAndEnabled)
                return;

            SpeciesData species = brain != null ? brain.Species : null;
            if (species != null && species.fullLodRadius > 1f)
            {
                fullSqr = species.fullLodRadius * species.fullLodRadius;
                float reducedRadius = species.reducedLodRadius > species.fullLodRadius
                    ? species.reducedLodRadius
                    : species.fullLodRadius;
                reducedSqr = reducedRadius * reducedRadius;
            }

            // 1.21 keeps the current band until the animal is about 10% farther, so it does not flicker on the border.
            float stayFullSqr = fullSqr * 1.21f;
            float stayReducedSqr = reducedSqr * 1.21f;
            AnimalAiBand next;

            if (band == AnimalAiBand.Full)
            {
                if (distanceSqr <= stayFullSqr)
                    next = AnimalAiBand.Full;
                else if (distanceSqr <= stayReducedSqr)
                    next = AnimalAiBand.Reduced;
                else
                    next = AnimalAiBand.Dormant;
            }
            else if (band == AnimalAiBand.Reduced)
            {
                if (distanceSqr <= fullSqr)
                    next = AnimalAiBand.Full;
                else if (distanceSqr <= stayReducedSqr)
                    next = AnimalAiBand.Reduced;
                else
                    next = AnimalAiBand.Dormant;
            }
            else if (distanceSqr <= fullSqr)
                next = AnimalAiBand.Full;
            else if (distanceSqr <= reducedSqr)
                next = AnimalAiBand.Reduced;
            else
                next = AnimalAiBand.Dormant;

            if (next == band)
                return;

            band = next;
            animal.SetSuspendSelfTick(band != AnimalAiBand.Full);
            if (birdFlight != null)
                birdFlight.enabled = band != AnimalAiBand.Dormant;

            if (band == AnimalAiBand.Reduced)
            {
                lastTick = Time.time;
                nextTick = Time.time;
            }
        }

        public void ScheduledTick(float now, float reducedInterval)
        {
            if (!isActiveAndEnabled)
                return;

            if (band == AnimalAiBand.Dormant)
            {
                animal.SimulatePresence();
                return;
            }

            if (band == AnimalAiBand.Reduced && now >= nextTick)
            {
                float delta = now - lastTick;
                if (delta < reducedInterval)
                    delta = reducedInterval;
                if (delta > 0.5f)
                    delta = 0.5f;

                lastTick = now;
                nextTick = now + reducedInterval;
                animal.Simulate(delta);
            }

            if (senses != null && now >= nextSense)
            {
                senses.Tick(band);
                nextSense = now + senses.Interval;
            }

            if (tracks != null && !animal.Dead)
                tracks.Sample(body.position, body.eulerAngles.y);
        }

        public void Hear(Vector3 position, float radiusSqr)
        {
            if (band == AnimalAiBand.Dormant || animal == null || animal.Dead || !animal.IsPresent())
                return;

            float limit = band == AnimalAiBand.Reduced ? radiusSqr * 0.36f : radiusSqr;
            Vector3 offset = body.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > limit)
                return;

            animal.FleeFrom(position);
        }
    }
}

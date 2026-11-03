using UnityEngine;

namespace HuntingGame.Animals
{
    public interface IAnimalAi
    {
        Transform Body { get; }
        AnimalAiBand Band { get; }
        void Evaluate(float distanceSqr, float fullSqr, float reducedSqr);
        void ScheduledTick(float now, float reducedInterval);
        void Hear(Vector3 position, float radiusSqr);
    }
}

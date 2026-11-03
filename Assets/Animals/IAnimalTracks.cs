using UnityEngine;

namespace HuntingGame.Tracking
{
    public interface IAnimalTracks
    {
        void Sample(Vector3 position, float yawDegrees);
        void SpillBlood(Vector3 position);
    }
}

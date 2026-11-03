using UnityEngine;

namespace HuntingGame.Ballistics
{
    public interface IBallistics
    {
        void Fire(
            Vector3 origin,
            Vector3 forward,
            Vector3 up,
            Vector3 right,
            float spread,
            float range,
            int pellets,
            Transform ignore);
    }
}

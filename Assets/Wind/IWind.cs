using UnityEngine;

namespace HuntingGame.Wind
{
    public interface IWind
    {
        Vector3 Direction { get; }
        float Strength { get; }
    }
}

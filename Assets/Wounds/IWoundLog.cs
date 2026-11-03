using UnityEngine;

namespace HuntingGame.Wounds
{
    public interface IWoundLog
    {
        void Mark(Vector3 point, bool heavy);
    }
}

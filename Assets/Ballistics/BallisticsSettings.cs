using UnityEngine;

namespace HuntingGame.Ballistics
{
    [CreateAssetMenu(fileName = "BallisticsSettings", menuName = "Hunting Game/Ballistics Settings")]
    public class BallisticsSettings : ScriptableObject
    {
        public int maxHits = 16;
    }
}

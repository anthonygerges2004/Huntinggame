using UnityEngine;

namespace HuntingGame.Ballistics
{
    [CreateAssetMenu(fileName = "BallisticsSettings", menuName = "Hunting Game/Ballistics Settings")]
    public class BallisticsSettings : ScriptableObject
    {
        public int maxHits = 16;
        public float muzzleSpeed = 360f;
        public float gravity = 9.81f;
        public float drag = 0.65f;
        public float pelletRadius = 0.02f;
    }
}

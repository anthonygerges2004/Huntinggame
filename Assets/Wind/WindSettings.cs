using UnityEngine;

namespace HuntingGame.Wind
{
    [CreateAssetMenu(fileName = "WindSettings", menuName = "Hunting Game/Wind Settings")]
    public class WindSettings : ScriptableObject
    {
        public float changeInterval = 18f;
        public float turnDegrees = 50f;
        public float degreesPerSecond = 8f;
        public float minStrength = 0.25f;
        public float maxStrength = 1f;
        public float strengthPerSecond = 0.08f;
    }
}

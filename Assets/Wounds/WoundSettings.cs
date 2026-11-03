using UnityEngine;

namespace HuntingGame.Wounds
{
    [CreateAssetMenu(fileName = "WoundSettings", menuName = "Hunting Game/Wound Settings")]
    public class WoundSettings : ScriptableObject
    {
        public float lifetime = 40f;
        public float size = 0.55f;
        public float heavySize = 0.9f;
        public int poolSize = 16;
    }
}

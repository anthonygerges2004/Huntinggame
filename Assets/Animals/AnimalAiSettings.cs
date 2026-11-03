using UnityEngine;

namespace HuntingGame.Animals
{
    [CreateAssetMenu(fileName = "AnimalAiSettings", menuName = "Hunting Game/Animal AI Settings")]
    public class AnimalAiSettings : ScriptableObject
    {
        public float fullRadius = 45f;
        public float reducedRadius = 120f;
        public float reducedTickInterval = 0.2f;
        public int evaluationsPerSlice = 8;
    }
}

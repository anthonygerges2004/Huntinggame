using HuntingGame.Senses;
using UnityEngine;

namespace HuntingGame.Animals
{
    [CreateAssetMenu(fileName = "SpeciesData", menuName = "Hunting Game/Species Data")]
    public class SpeciesData : ScriptableObject
    {
        public AnimalKind kind = AnimalKind.Deer;
        public bool prey = true;
        public AnimalSenseSettings senses;
        public float grazeSpeed = 2f;
        public float fleeSpeed = 8f;
        public float idleSeconds = 2.2f;
        public float grazeSeconds = 7f;
        public float alertSeconds = 1.15f;
        public float fleeSeconds = 8f;
        public float calmSeconds = 2.5f;
        public float grazeRadius = 6f;
        public float fullLodRadius = 45f;
        public float reducedLodRadius = 120f;
        public float downwindWeight = 1.2f;
    }
}

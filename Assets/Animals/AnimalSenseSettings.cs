using UnityEngine;

namespace HuntingGame.Senses
{
    [CreateAssetMenu(fileName = "AnimalSenseSettings", menuName = "Hunting Game/Animal Sense Settings")]
    public class AnimalSenseSettings : ScriptableObject
    {
        public float sightRange = 32f;
        public float sightDegrees = 80f;
        public float sprintHearRange = 16f;
        public float walkHearRange = 8f;
        public float crouchHearRange = 3.5f;
        public float smellRange = 26f;
        public float smellDegrees = 70f;
        public float tickInterval = 0.3f;
        public float reducedRangeScale = 0.6f;
    }
}

using UnityEngine;

namespace HuntingGame.Survival
{
    [CreateAssetMenu(fileName = "SurvivalSettings", menuName = "Hunting Game/Survival Settings")]
    public class SurvivalSettings : ScriptableObject
    {
        public float tickInterval = 0.5f;
        public float hungerFullSeconds = 150f;
        public float eatNutrition = 0.4f;
        public float starveInterval = 1f;
        public float starveDamage = 1f;
        public float coldSeconds = 20f;
        public float warmSeconds = 8f;
    }
}

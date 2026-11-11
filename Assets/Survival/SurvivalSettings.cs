using UnityEngine;

namespace HuntingGame.Survival
{
    // Tuning for the survival model. Times are real seconds/minutes of play.
    [CreateAssetMenu(fileName = "SurvivalSettings", menuName = "Hunting Game/Survival Settings")]
    public class SurvivalSettings : ScriptableObject
    {
        public float tickInterval = 0.25f;

        [Header("Fatigue response")]
        [Min(0.05f)] public float fatigueBuildSeconds = 2f;
        [Min(0.05f)] public float fatigueRecoverySeconds = 6f;

        [Header("Food")]
        [Tooltip("Minutes for a full stomach to empty while walking around.")]
        public float foodLastsMinutes = 16f;
        public float sprintHungerMultiplier = 2f;
        [Tooltip("Shivering burns calories: hunger speeds up this much when cold.")]
        public float coldHungerMultiplier = 1.4f;
        public float cookedMeatNutrition = 0.35f;
        public float cookedMeatHeal = 4f;
        public float rawMeatNutrition = 0.15f;
        [Range(0f, 1f)] public float rawMeatPoisonChance = 0.5f;

        [Header("Water")]
        [Tooltip("Minutes for full hydration to run out.")]
        public float waterLastsMinutes = 10f;
        public float sprintThirstMultiplier = 1.8f;
        public float drinkPerSip = 0.2f;
        public float sipSeconds = 0.6f;

        [Header("Body temperature")]
        [Tooltip("Comfortable level the body settles at in clear daylight.")]
        [Range(0f, 1f)] public float dayComfort = 0.85f;
        [Range(0f, 1f)] public float overcastComfort = 0.7f;
        [Range(0f, 1f)] public float rainComfort = 0.45f;
        [Range(0f, 1f)] public float nightComfort = 0f;
        [Tooltip("Minutes to go from warm to freezing outdoors at night.")]
        public float nightCoolMinutes = 3f;
        [Tooltip("Minutes for daylight to bring the body back to comfortable.")]
        public float dayRecoverMinutes = 2.5f;
        [Tooltip("Minutes in water to go from warm to freezing.")]
        public float waterCoolMinutes = 1f;
        [Tooltip("Seconds next to a fire to go from freezing to fully warm.")]
        public float fireWarmSeconds = 30f;
        [Tooltip("Wet clothes make you cool faster by this factor.")]
        public float wetCoolMultiplier = 1.8f;
        public float dryingSeconds = 120f;
        public float fireDryingMultiplier = 5f;

        [Header("Health")]
        public float starvingDamagePerMinute = 6f;
        public float dehydratedDamagePerMinute = 10f;
        public float freezingDamagePerMinute = 8f;
        [Range(0f, 1f)] public float freezingBelow = 0.12f;
        public float poisonSeconds = 45f;
        public float poisonDamagePerMinute = 14f;
        [Tooltip("Natural healing while fed, hydrated and warm.")]
        public float regenPerMinute = 3f;
        public float restingByFireRegenMultiplier = 2.5f;
        [Tooltip("Seconds after taking damage before healing starts.")]
        public float regenDelay = 12f;
        [Range(0f, 1f)] public float regenNeeds = 0.4f;

        [Header("Falls")]
        public float safeFallSpeed = 13f;
        public float fallDamagePerMetrePerSecond = 6f;
    }
}

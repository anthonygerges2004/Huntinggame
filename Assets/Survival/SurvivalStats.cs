using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Survival
{
    public class SurvivalStats : MonoBehaviour, ISurvivalStats
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.Survival");

        public static SurvivalStats Current { get; private set; }

        [SerializeField] SurvivalSettings settings;

        PlayerHealth body;
        float hunger = 1f;
        float warmth = 1f;
        float interval = 0.5f;
        float hungerPerTick;
        float eatAmount = 0.4f;
        float starveInterval = 1f;
        float starveDamage = 1f;
        float coldPerTick;
        float warmPerTick;
        float starveWait;
        float timer;

        public float Health01
        {
            get
            {
                if (body == null || body.maxHealth <= 0f)
                    return 0f;
                return body.health / body.maxHealth;
            }
        }

        public float Hunger01 => hunger;
        public float Warmth01 => warmth;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            Current = this;
            body = GetComponent<PlayerHealth>();
            if (settings == null)
                settings = ScriptableObject.CreateInstance<SurvivalSettings>();

            interval = settings.tickInterval < 0.1f ? 0.1f : settings.tickInterval;
            float full = settings.hungerFullSeconds < 1f ? 1f : settings.hungerFullSeconds;
            hungerPerTick = interval / full;
            eatAmount = settings.eatNutrition < 0.05f ? 0.05f : settings.eatNutrition;
            starveInterval = settings.starveInterval < interval ? interval : settings.starveInterval;
            starveDamage = settings.starveDamage < 0f ? 0f : settings.starveDamage;
            float cold = settings.coldSeconds < 1f ? 1f : settings.coldSeconds;
            float warm = settings.warmSeconds < 1f ? 1f : settings.warmSeconds;
            coldPerTick = interval / cold;
            warmPerTick = interval / warm;
            timer = interval;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void EatMeal()
        {
            hunger += eatAmount;
            if (hunger > 1f)
                hunger = 1f;
            starveWait = 0f;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f)
                return;

            timer = interval;
            TickMarker.Begin();
            Tick();
            TickMarker.End();
        }

        void Tick()
        {
            if (body != null && body.isDead)
                return;

            hunger -= hungerPerTick;
            if (hunger < 0f)
                hunger = 0f;

            bool night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
            bool exposed = night && !Campfire.PlayerIsSafe();
            if (exposed)
                warmth -= coldPerTick;
            else
                warmth += warmPerTick;

            if (warmth < 0f)
                warmth = 0f;
            else if (warmth > 1f)
                warmth = 1f;

            if (hunger > 0f || body == null)
            {
                starveWait = 0f;
                return;
            }

            starveWait += interval;
            if (starveWait < starveInterval)
                return;

            starveWait = 0f;
            body.TakeDamage(starveDamage);
        }
    }
}

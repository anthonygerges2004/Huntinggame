using Unity.Profiling;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HuntingGame.Survival
{
    // The player's body: food, water, body temperature, wetness and food poisoning.
    // Health is never drained by the clock - only by what those needs do to the body
    // (starving, dehydration, freezing, poisoning) and by injuries. When the body is
    // fed, watered and warm it slowly heals, faster while resting by a fire.
    public class SurvivalStats : MonoBehaviour, ISurvivalStats
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.Survival");

        public static SurvivalStats Current { get; private set; }

        [SerializeField] SurvivalSettings settings;

        PlayerHealth body;
        Camera view;
        float food = 0.85f;
        float water = 0.85f;
        float warmth = 0.85f;
        float wet;
        float poisonLeft;
        float pendingDamage;
        string pendingCause = "Injuries";
        float pendingHeal;
        float timer;
        float sipTimer;
        float nearWaterTimer;
        bool nearWater;
        bool recovering;
        bool byFire;

        // Warnings are announced once when crossing a threshold.
        bool warnedHungry, warnedStarving, warnedThirsty, warnedDehydrated, warnedCold, warnedFreezing, warnedWet;

        public SurvivalSettings Settings => settings;

        public float Health01 => body == null || body.maxHealth <= 0f ? 0f : body.health / body.maxHealth;
        public float Hunger01 => food;
        public float Water01 => water;
        public float Warmth01 => warmth;
        public float Wet01 => wet;

        public bool HasHuntingCoat { get; private set; }
        public bool HasOilskin { get; private set; }

        public void GiveHuntingCoat()
        {
            HasHuntingCoat = true;
        }

        public void GiveOilskin()
        {
            HasOilskin = true;
        }

        public bool Starving => food <= 0f;
        public bool Hungry => food < 0.25f;
        public bool Dehydrated => water <= 0f;
        public bool Thirsty => water < 0.25f;
        public bool Freezing => warmth < settings.freezingBelow;
        public bool Cold => warmth < 0.35f;
        public bool Wet => wet > 0.3f;
        public bool Poisoned => poisonLeft > 0f;
        public bool Recovering => recovering;
        public bool ByFire => byFire;
        public bool NearDrinkableWater => nearWater;

        // Shared continuous state drives gameplay and the visible body pose.
        public float Fatigue01 { get; private set; }
        public float Shiver01 { get; private set; }
        public float MoveSpeedMultiplier => 1f - 0.32f * Fatigue01;
        public float StaminaRecoveryMultiplier => 1f - 0.8f * Fatigue01;

        void UpdateFatigue(float dt)
        {
            SurvivalFatigueFuzzy.Result target = SurvivalFatigueFuzzy.Evaluate(food, water, warmth, wet);
            float fatigue = Poisoned ? Mathf.Max(0.5f, target.fatigue) : target.fatigue;
            float response = fatigue > Fatigue01 ? settings.fatigueBuildSeconds : settings.fatigueRecoverySeconds;
            Fatigue01 = SurvivalFatigueFuzzy.Approach(Fatigue01, fatigue, dt, response);
            Shiver01 = SurvivalFatigueFuzzy.Approach(Shiver01, target.shiver, dt, response);
        }
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
            warmth = settings.dayComfort;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        // ------------------------------------------------------------ eating & drinking

        public void EatMeal()
        {
            EatCooked();
        }

        public void EatCooked()
        {
            food = Mathf.Min(1f, food + settings.cookedMeatNutrition);
            if (body != null)
                body.Heal(settings.cookedMeatHeal);
            warnedHungry = warnedStarving = false;
        }

        // Returns true when the raw meat made the player sick.
        public bool EatRaw()
        {
            food = Mathf.Min(1f, food + settings.rawMeatNutrition);
            warnedHungry = warnedStarving = false;
            if (Random.value >= settings.rawMeatPoisonChance)
                return false;

            poisonLeft = settings.poisonSeconds;
            return true;
        }

        public void Warm(float amount)
        {
            warmth = Mathf.Clamp01(warmth + Mathf.Max(0f, amount));
        }

        public void Drink(float amount)
        {
            water = Mathf.Min(1f, water + amount);
            warnedThirsty = warnedDehydrated = false;
        }

        // ------------------------------------------------------------ update

        void Update()
        {
            if (body != null && body.isDead)
                return;

            if (GamePause.Active || Time.deltaTime <= 0f) return;
            UpdateFatigue(Time.deltaTime);
            UpdateDrinking();

            timer -= Time.deltaTime;
            if (timer > 0f)
                return;

            float dt = Mathf.Max(0.1f, settings.tickInterval);
            timer = dt;
            TickMarker.Begin();
            Tick(dt);
            TickMarker.End();
        }

        void Tick(float dt)
        {
            DayNightCycle day = DayNightCycle.Instance;
            bool night = day != null && day.IsNight;
            byFire = Campfire.PlayerIsSafe();
            Vector3 pos = transform.position;
            bool swimming = WaterBodies.TryGetSwim(pos, out _, out _);
            bool wading = swimming || WaterBodies.InsideWater(pos);
            bool sprinting = PlayerMove.IsSprinting;
            WorldWeather.Kind weather = WorldWeather.Instance != null ? WorldWeather.Instance.Current : WorldWeather.Kind.Clear;
            bool raining = weather == WorldWeather.Kind.Rain;

            // Wet clothes: soaked by water, slowly by rain, dried by time and fire.
            if (swimming)
                wet = 1f;
            else if (wading)
                wet = Mathf.Max(wet, 0.6f);
            else if (raining && !byFire)
                wet = Mathf.Min(1f, wet + dt / (HasOilskin ? 220f : 90f));
            else
                wet = Mathf.Max(0f, wet - dt / Mathf.Max(1f, settings.dryingSeconds) * (byFire ? settings.fireDryingMultiplier : 1f));

            // Body temperature drifts towards what the surroundings allow.
            if (byFire)
            {
                warmth += dt / Mathf.Max(1f, settings.fireWarmSeconds);
            }
            else if (swimming)
            {
                warmth -= dt / Mathf.Max(1f, settings.waterCoolMinutes * 60f);
            }
            else
            {
                float comfort = night ? settings.nightComfort
                              : raining ? settings.rainComfort
                              : weather == WorldWeather.Kind.Clear ? settings.dayComfort
                              : settings.overcastComfort;
                if (Wet)
                    comfort -= 0.2f * wet;
                if (HasHuntingCoat)
                    comfort = Mathf.Min(1f, comfort + 0.2f);

                if (warmth > comfort)
                {
                    float cool = dt / Mathf.Max(1f, settings.nightCoolMinutes * 60f);
                    if (!night)
                        cool *= 0.6f;
                    if (Wet)
                        cool *= Mathf.Lerp(1f, settings.wetCoolMultiplier, wet);
                    if (sprinting)
                        cool *= 0.5f; // moving keeps you warm
                    warmth = Mathf.Max(comfort, warmth - cool);
                }
                else
                {
                    warmth = Mathf.Min(comfort, warmth + dt / Mathf.Max(1f, settings.dayRecoverMinutes * 60f));
                }
            }

            warmth = Mathf.Clamp01(warmth);

            // Food and water burn down; faster when running, cold or sick.
            float hungerRate = dt / Mathf.Max(1f, settings.foodLastsMinutes * 60f);
            if (sprinting) hungerRate *= settings.sprintHungerMultiplier;
            if (Cold) hungerRate *= settings.coldHungerMultiplier;
            food = Mathf.Max(0f, food - hungerRate);

            float thirstRate = dt / Mathf.Max(1f, settings.waterLastsMinutes * 60f);
            if (sprinting) thirstRate *= settings.sprintThirstMultiplier;
            if (byFire) thirstRate *= 1.15f;
            if (Poisoned) thirstRate *= 1.6f;
            water = Mathf.Max(0f, water - thirstRate);

            // What the body suffers.
            float worst = 0f;
            if (Starving)
                AddDamage(settings.starvingDamagePerMinute * dt / 60f, "Starved to death", ref worst);
            if (Dehydrated)
                AddDamage(settings.dehydratedDamagePerMinute * dt / 60f, "Died of thirst", ref worst);
            if (Freezing)
            {
                float severity = 1f + (settings.freezingBelow - warmth) / Mathf.Max(0.01f, settings.freezingBelow);
                AddDamage(settings.freezingDamagePerMinute * severity * dt / 60f, "Froze to death", ref worst);
            }
            if (Poisoned)
            {
                poisonLeft -= dt;
                AddDamage(settings.poisonDamagePerMinute * dt / 60f, "Food poisoning", ref worst);
            }

            if (pendingDamage >= 1f && body != null)
            {
                float amount = Mathf.Floor(pendingDamage);
                pendingDamage -= amount;
                body.TakeDamage(amount, pendingCause);
            }

            // Natural healing.
            float sinceHurt = body != null ? Time.time - body.LastDamageTime : 0f;
            recovering = body != null && worst <= 0f && body.health < body.maxHealth &&
                         food > settings.regenNeeds && water > settings.regenNeeds && warmth > settings.regenNeeds &&
                         sinceHurt > settings.regenDelay;
            if (recovering)
            {
                float rate = settings.regenPerMinute;
                if (byFire && !PlayerMove.IsMoving)
                    rate *= settings.restingByFireRegenMultiplier;
                if (food > 0.75f && water > 0.75f)
                    rate *= 1.3f;
                pendingHeal += rate * dt / 60f;
                if (pendingHeal >= 0.5f)
                {
                    body.Heal(pendingHeal);
                    pendingHeal = 0f;
                }
            }
            else
            {
                pendingHeal = 0f;
            }

            Announce();
        }

        void AddDamage(float amount, string cause, ref float worst)
        {
            pendingDamage += amount;
            if (amount > worst)
            {
                worst = amount;
                pendingCause = cause;
            }
        }

        void Announce()
        {
            Warn(ref warnedHungry, Hungry, food > 0.35f, "You're getting hungry. Cook some meat.");
            Warn(ref warnedStarving, Starving, food > 0.05f, "You're starving! Eat something.");
            Warn(ref warnedThirsty, Thirsty, water > 0.35f, "You're thirsty. Find water to drink.");
            Warn(ref warnedDehydrated, Dehydrated, water > 0.05f, "You're dehydrated! Drink water now.");
            Warn(ref warnedCold, Cold, warmth > 0.45f, "You're getting cold.");
            Warn(ref warnedFreezing, Freezing, warmth > 0.2f, "You're freezing! Get to a fire.");
            Warn(ref warnedWet, wet > 0.8f, wet < 0.2f, "Your clothes are soaked. Dry off by a fire.");
        }

        static void Warn(ref bool warned, bool condition, bool cleared, string message)
        {
            if (!warned && condition)
            {
                warned = true;
                if (Inventory.Instance != null)
                    Inventory.Instance.ShowNotice(message);
            }
            else if (warned && cleared)
            {
                warned = false;
            }
        }

        // ------------------------------------------------------------ drinking

        void UpdateDrinking()
        {
            nearWaterTimer -= Time.deltaTime;
            if (nearWaterTimer <= 0f)
            {
                nearWaterTimer = 0.25f;
                nearWater = FindWaterNearby();
            }

            sipTimer -= Time.deltaTime;
            if (!nearWater || Inventory.IsOpen || water >= 0.999f)
                return;

            bool held;
#if ENABLE_INPUT_SYSTEM
            held = Keyboard.current != null && Keyboard.current.zKey.isPressed;
#else
            held = Input.GetKey(KeyCode.Z);
#endif
            if (!held || sipTimer > 0f)
                return;

            sipTimer = Mathf.Max(0.1f, settings.sipSeconds);
            Drink(settings.drinkPerSip);
        }

        bool FindWaterNearby()
        {
            Vector3 pos = transform.position;
            if (WaterBodies.InsideWater(pos))
                return true;

            if (view == null || !view.isActiveAndEnabled)
                view = Camera.main;
            Vector3 fwd = view != null ? view.transform.forward : transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

            for (int i = 1; i <= 3; i++)
            {
                Vector3 p = pos + fwd * (i * 0.9f);
                if (WaterBodies.SurfaceAt(p, 0.2f, out float surface, out _) && pos.y - surface < 2.6f)
                    return true;
            }

            return false;
        }

        void OnGUI()
        {
            if (!nearWater || water >= 0.98f || Inventory.IsOpen || BirdChallenge.Active || (body != null && body.isDead))
                return;

            ScreenText.Centered("Hold Z to drink", Screen.height * 0.5f + 100f * ScreenText.Scale, 20);
        }
    }
}

namespace HuntingGame.Survival
{
    /// <summary>
    /// Zero-order Sugeno inference: two complementary memberships for each input,
    /// sixteen product-AND rules, weighted-average defuzzification. Food/water/warmth
    /// are reserves (1 is healthy); wetness is exposure (1 is soaked).
    /// </summary>
    public static class SurvivalFatigueFuzzy
    {
        public struct Result { public float fatigue; public float shiver; }

        public static Result Evaluate(float food, float water, float warmth, float wetness)
        {
            float hungry = 1f - Ramp(food, 0f, 0.65f);
            float thirsty = 1f - Ramp(water, 0f, 0.7f);
            float cold = 1f - Ramp(warmth, 0.05f, 0.6f);
            float wet = Ramp(wetness, 0.2f, 1f);
            float sum = 0f, weights = 0f;
            for (int rule = 0; rule < 16; rule++)
            {
                int h = rule & 1, t = (rule >> 1) & 1, c = (rule >> 2) & 1, w = (rule >> 3) & 1;
                float weight = (h == 1 ? hungry : 1f - hungry) * (t == 1 ? thirsty : 1f - thirsty)
                    * (c == 1 ? cold : 1f - cold) * (w == 1 ? wet : 1f - wet);
                // Strong hunger AND thirst compounds fatigue; soaked AND cold is worse
                // than wet clothes alone. Every consequent is monotone in each stressor.
                float severity = Clamp(0.22f * h + 0.34f * t + 0.28f * c + 0.06f * w
                    + 0.16f * h * t + 0.18f * c * w);
                sum += weight * severity;
                weights += weight;
            }
            return new Result { fatigue = weights > 0f ? Clamp(sum / weights) : 0f,
                shiver = cold * (0.65f + 0.35f * wet) };
        }

        public static float Approach(float current, float target, float dt, float seconds)
        {
            if (dt <= 0f) return current;
            float blend = 1f - (float)System.Math.Exp(-dt / System.Math.Max(0.05f, seconds));
            return Clamp(current + (target - current) * blend);
        }

        static float Ramp(float x, float low, float high) => Clamp((x - low) / (high - low));
        static float Clamp(float x) => System.Math.Max(0f, System.Math.Min(1f, x));
    }
}
using UnityEngine;

/// <summary>
/// Aim sway and hold-breath. While aiming, the view drifts with breathing. It drifts more
/// while moving, when out of stamina and when cold, and less when crouched. Holding Shift
/// steadies the scope for a few seconds; running out of breath makes it worse for a moment.
/// The sway turns the camera itself, so the shot goes exactly where the reticle is.
/// </summary>
public static class ScopeSteady
{
    public const float BreathSeconds = 4.5f;
    const float GaspSeconds = 2.2f;
    const float RefillPerSecond = 0.45f;
    const float ScopeSway = 0.42f;
    const float IronSway = 0.16f;

    public static float Breath01 { get; private set; } = 1f;
    public static bool Holding { get; private set; }
    public static bool Gasping => gaspLeft > 0f;

    static float gaspLeft;
    static float clock;
    static float amount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlayMode()
    {
        Breath01 = 1f;
        Holding = false;
        gaspLeft = 0f;
        clock = 0f;
        amount = 0f;
    }

    /// <summary>Returns the sway to add to the camera, in degrees (x = pitch, y = yaw).</summary>
    public static Vector2 Tick(float dt, bool firstPerson, bool holdKey)
    {
        bool aiming = firstPerson && PlayerShotgun.IsAiming && !PlayerShotgun.IsReloading;
        bool scoped = aiming && PlayerShotgun.ScopeIsUp;

        Holding = scoped && holdKey && gaspLeft <= 0f && Breath01 > 0f;
        if (Holding)
        {
            Breath01 = Mathf.Max(0f, Breath01 - dt / (BreathSeconds * HuntProgress.BreathMultiplier));
            if (Breath01 <= 0f)
            {
                gaspLeft = GaspSeconds;
                Holding = false;
            }
        }
        else
        {
            gaspLeft = Mathf.Max(0f, gaspLeft - dt);
            if (gaspLeft <= 0f)
                Breath01 = Mathf.Min(1f, Breath01 + dt * RefillPerSecond);
        }

        float target = 0f;
        if (aiming)
        {
            target = (scoped ? ScopeSway : IronSway) * Mathf.Clamp01(PlayerShotgun.Mount);
            if (PlayerMove.IsMoving)
                target *= 2.4f;
            if (PlayerMove.IsCrouching)
                target *= 0.6f;
            target *= 1f + (1f - PlayerMove.Stamina01) * 1.6f;

            HuntingGame.Survival.SurvivalStats stats = HuntingGame.Survival.SurvivalStats.Current;
            if (stats != null && (stats.Cold || stats.Freezing))
                target *= stats.Freezing ? 1.8f : 1.35f;

            target *= HuntProgress.SwayMultiplier;
            // Buck fever: a racing heart shakes the aim when big game is in the sights.
            target *= 1f + HuntExtras.BuckFever * 1.3f;

            if (Holding)
                target *= 0.1f;
            else if (gaspLeft > 0f)
                target *= 2.3f;
        }

        amount = Mathf.MoveTowards(amount, target, dt * 2.5f);
        clock += dt * (Holding ? 0.35f : 1f);

        if (amount < 0.0005f)
            return Vector2.zero;

        // Slow figure-eight breathing plus a little noise, like a real rested rifle.
        float pitch = Mathf.Sin(clock * 1.3f) * 0.7f + (Mathf.PerlinNoise(clock * 0.9f, 3.1f) - 0.5f) * 1.2f;
        float yaw = Mathf.Sin(clock * 0.65f) * 0.9f + (Mathf.PerlinNoise(7.7f, clock * 0.8f) - 0.5f) * 1.2f;
        return new Vector2(pitch, yaw) * amount;
    }
}

using UnityEngine;

public enum AnimalKind
{
    Bird,
    Deer,
    Large,
    Legendary
}

public class Animal : MonoBehaviour
{
    public AnimalKind kind = AnimalKind.Deer;
    public int meatAmount = 2;
    public float moveSpeed = 2f;
    public float fleeSpeed = 8f;
    public float nightDamage = 8f;
    public float nightAttackRange = 4f;
    public float nightAttackCooldown = 1.5f;

    Vector3 startPos;
    Vector3 wanderTarget;
    bool fleeing;
    float attackTimer;
    bool dead;

    void Start()
    {
        startPos = transform.position;
        PickWanderTarget();
    }

    void Update()
    {
        if (dead)
            return;

        if (fleeing)
        {
            Vector3 away = transform.position + transform.forward * 2f;
            transform.position = Vector3.MoveTowards(transform.position, away, fleeSpeed * Time.deltaTime);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, wanderTarget, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, wanderTarget) < 0.3f)
            PickWanderTarget();

        TryNightAttack();
    }

    void PickWanderTarget()
    {
        Vector2 circle = Random.insideUnitCircle * 6f;
        wanderTarget = startPos + new Vector3(circle.x, 0f, circle.y);
        wanderTarget.y = transform.position.y;
    }

    public void FleeFrom(Vector3 danger)
    {
        fleeing = true;
        Vector3 dir = transform.position - danger;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;
        transform.forward = dir.normalized;
    }

    void TryNightAttack()
    {
        if (DayNightCycle.Instance == null || !DayNightCycle.Instance.IsNight)
            return;

        if (Campfire.PlayerIsSafe())
            return;

        Transform player = Camera.main != null ? Camera.main.transform : null;
        if (player == null)
            return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > nightAttackRange)
            return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        attackTimer = nightAttackCooldown;
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.TakeDamage(nightDamage);
    }

    public void Hit()
    {
        if (dead)
            return;

        dead = true;

        if (Inventory.Instance != null)
            Inventory.Instance.AddMeat(meatAmount);

        WildlifeAlarm.NotifyShot(transform.position);
        Overhunt.RegisterKill();

        if (kind == AnimalKind.Legendary && Trader.Instance != null)
            Trader.Instance.LegendaryTakenByPlayer();

        Destroy(gameObject);
    }
}

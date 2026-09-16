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
    Vector3 previousPosition;
    Animator animalAnimator;
    bool fleeing;
    float attackTimer;
    bool dead;

    static readonly int VerticalAnimation = Animator.StringToHash("Vert");
    static readonly int StateAnimation = Animator.StringToHash("State");

    void Start()
    {
        startPos = transform.position;
        previousPosition = transform.position;
        animalAnimator = GetComponentInChildren<Animator>();
        DisableImportedMovementControllers();
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
            UpdateAnimation(true);
            return;
        }

        FaceDirection(wanderTarget - transform.position);
        transform.position = Vector3.MoveTowards(transform.position, wanderTarget, moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, wanderTarget) < 0.3f)
            PickWanderTarget();

        TryNightAttack();
        UpdateAnimation(false);
    }

    void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            6f * Time.deltaTime);
    }

    void UpdateAnimation(bool running)
    {
        if (animalAnimator == null)
            return;

        float distanceMoved = Vector3.Distance(transform.position, previousPosition);
        bool moving = distanceMoved > 0.0001f;

        animalAnimator.SetFloat(
            VerticalAnimation,
            moving ? 1f : 0f,
            0.12f,
            Time.deltaTime);
        animalAnimator.SetFloat(
            StateAnimation,
            running ? 1f : 0f,
            0.12f,
            Time.deltaTime);

        previousPosition = transform.position;
    }

    void DisableImportedMovementControllers()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this)
                continue;

            string typeName = behaviour.GetType().FullName;
            if (typeName == "ithappy.Animals_FREE.MovePlayerInput" ||
                typeName == "ithappy.Animals_FREE.CreatureMover")
            {
                behaviour.enabled = false;
            }
        }

        CharacterController[] childControllers =
            GetComponentsInChildren<CharacterController>(true);
        foreach (CharacterController controller in childControllers)
        {
            if (controller.gameObject != gameObject)
                controller.enabled = false;
        }
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

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
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
        {
            PlayerHealth.Instance.TakeDamage(nightDamage);
            Debug.Log($"{kind} attacked player! {nightDamage} damage taken.");
        }
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

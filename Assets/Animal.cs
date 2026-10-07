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

    Vector3 homePos;
    Vector3 wanderTarget;
    Vector3 previousPosition;
    Animator animalAnimator;
    bool fleeing;
    bool returningHome;
    float fleeTimer;
    float attackTimer;
    bool dead;

    public float fleeDuration = 8f;
    public bool nocturnal;

    bool present = true;

    static readonly int VerticalAnimation = Animator.StringToHash("Vert");
    static readonly int StateAnimation = Animator.StringToHash("State");

    void Start()
    {
        ApplyKindFromName();
        AnimalVisuals.Attach(this);
        homePos = WorldGround.Snap(transform.position);
        transform.position = homePos;
        previousPosition = transform.position;
        animalAnimator = GetComponentInChildren<Animator>();
        DisableImportedMovementControllers();
        PickWanderTarget();
        SetPresent(IsPresent());
    }

    void Update()
    {
        if (dead)
            return;

        bool shouldBePresent = IsPresent();
        if (shouldBePresent != present)
            SetPresent(shouldBePresent);

        if (!present)
            return;

        if (fleeing)
        {
            fleeTimer -= Time.deltaTime;
            Vector3 away = transform.position + transform.forward * 2f;
            Vector3 next = WorldGround.Snap(
                Vector3.MoveTowards(transform.position, away, fleeSpeed * Time.deltaTime));
            if (WorldGround.InsideWater(next))
            {
                transform.Rotate(0f, 110f, 0f);
                away = transform.position + transform.forward * 2f;
                next = WorldGround.Snap(
                    Vector3.MoveTowards(transform.position, away, fleeSpeed * Time.deltaTime));
            }

            transform.position = next;
            UpdateAnimation(true);

            if (fleeTimer <= 0f)
                StartReturningHome();

            return;
        }

        if (returningHome)
        {
            Vector3 home = WorldGround.Snap(homePos);
            FaceDirection(home - transform.position);
            transform.position = WorldGround.Snap(
                Vector3.MoveTowards(transform.position, home, moveSpeed * Time.deltaTime));
            UpdateAnimation(false);

            if (Vector3.Distance(Flat(transform.position), Flat(home)) < 0.6f)
            {
                returningHome = false;
                PickWanderTarget();
            }

            TryNightAttack();
            return;
        }

        FaceDirection(wanderTarget - transform.position);
        transform.position = WorldGround.Snap(
            Vector3.MoveTowards(transform.position, wanderTarget, moveSpeed * Time.deltaTime));
        if (Vector3.Distance(Flat(transform.position), Flat(wanderTarget)) < 0.5f)
            PickWanderTarget();

        TryNightAttack();
        UpdateAnimation(false);
    }

    void ApplyKindFromName()
    {
        string objectName = gameObject.name;
        if (objectName.IndexOf("Legendary", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            kind = AnimalKind.Legendary;
            nocturnal = true;
            if (meatAmount < 10)
                meatAmount = 15;
            return;
        }

        if (objectName.IndexOf("Bird", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            kind = AnimalKind.Bird;
            return;
        }

        if (objectName.IndexOf("Big", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Large", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            kind = AnimalKind.Large;
            nocturnal = true;
            if (meatAmount <= 2)
                meatAmount = 5;
            if (nightDamage <= 8f)
                nightDamage = 14f;
            return;
        }
    }

    public bool IsPresent()
    {
        if (kind == AnimalKind.Legendary)
            nocturnal = true;

        if (!nocturnal)
            return true;

        return DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
    }

    void SetPresent(bool on)
    {
        present = on;
        if (on)
        {
            transform.position = WorldGround.Snap(homePos);
            previousPosition = transform.position;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is MeshRenderer && renderers[i].transform == transform)
                continue;
            renderers[i].enabled = on;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = on;
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
        for (int i = 0; i < 8; i++)
        {
            Vector2 circle = Random.insideUnitCircle * 6f;
            Vector3 candidate = homePos + new Vector3(circle.x, 0f, circle.y);
            candidate = WorldGround.Snap(candidate);
            if (WorldGround.IsMeadow(candidate) || i == 7)
            {
                wanderTarget = candidate;
                return;
            }
        }
    }

    static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    public void FleeFrom(Vector3 danger)
    {
        fleeing = true;
        returningHome = false;
        fleeTimer = fleeDuration;
        Vector3 dir = transform.position - danger;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;
        transform.forward = dir.normalized;
    }

    void StartReturningHome()
    {
        fleeing = false;
        returningHome = true;
        FaceDirection(homePos - transform.position);
    }

    void TryNightAttack()
    {
        if (kind == AnimalKind.Bird)
            return;

        if (DayNightCycle.Instance == null || !DayNightCycle.Instance.IsNight)
            return;

        if (Campfire.PlayerIsSafe())
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        Transform player = PlayerHealth.Instance != null
            ? PlayerHealth.Instance.transform
            : (Camera.main != null ? Camera.main.transform : null);
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
        if (dead || !present)
            return;

        dead = true;

        MeatPickup.Spawn(transform.position, meatAmount);
        HuntChallenges.ReportKill(this);

        WildlifeAlarm.NotifyShot(transform.position);
        Overhunt.RegisterKill();

        if (kind == AnimalKind.Legendary && Trader.Instance != null)
            Trader.Instance.LegendaryTakenByPlayer();

        Destroy(gameObject);
    }
}
using HuntingGame.Animals;
using HuntingGame.Tracking;
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
    public FlyerKind flyer = FlyerKind.Sparrow;
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
    bool dropping;
    float hideLeft = 100f;
    bool wounded;
    ShotPlace woundPlace;
    float woundSide = 1f;
    float stumbleLeft;
    float boltLeft;
    float bleedLeft;
    Vector3 bleedAnchor;
    bool bleedAnchorSet;
    DeerWoundPose woundPose;
    bool bonesCached;
    Transform skull;
    Transform chestBone;
    Transform rearBone;
    string lastHitPlace = "";
    float dropTime;
    Quaternion standRotation;
    Quaternion laidRotation;
    Vector3 fallFrom;
    Vector3 fallTo;

    public float fleeDuration = 8f;
    public bool nocturnal;

    bool present = true;
    float step;
    bool suspendSelfTick;
    AnimalBrain brain;

    public bool Dead => dead;
    public bool Fleeing => fleeing;

    /// <summary>True once the adrenaline bolt or the stumble is over and the deer is favoring a side.</summary>
    public bool Limping => wounded && stumbleLeft <= 0f && boltLeft <= 0f;

    /// <summary>
    /// Speed scale for a wounded deer. A leg or head hit nearly stops them, then they limp.
    /// A body or chest hit keeps a short bolt before the stride falls apart.
    /// </summary>
    public float WoundMoveScale
    {
        get
        {
            if (!wounded)
                return 1f;
            if (stumbleLeft > 0f)
                return 0.06f;
            if (boltLeft > 0f)
                return woundPlace == ShotPlace.Chest ? 1f : 0.86f;

            switch (woundPlace)
            {
                case ShotPlace.Legs: return 0.34f;
                case ShotPlace.Head: return 0.38f;
                case ShotPlace.Chest: return 0.46f;
                default: return 0.55f;
            }
        }
    }

    public Vector3 HomePosition => homePos;

    static readonly int VerticalAnimation = Animator.StringToHash("Vert");
    static readonly int StateAnimation = Animator.StringToHash("State");

    void Start()
    {
        ApplyKindFromName();
        AnimalVisuals.Attach(this);
        AnimalTrophy.Roll(this);
        homePos = WorldGround.Snap(transform.position);
        if (kind != AnimalKind.Bird)
            transform.position = homePos;
        previousPosition = transform.position;
        animalAnimator = GetComponentInChildren<Animator>();
        DisableImportedMovementControllers();
        if (kind == AnimalKind.Bird)
        {
            if (GetComponent<BirdFlight>() == null)
                gameObject.AddComponent<BirdFlight>();
        }
        else
            PickWanderTarget();
        SetPresent(IsPresent());
        if (GetComponent<AnimalAiAgent>() == null)
            gameObject.AddComponent<AnimalAiAgent>();
        brain = GetComponent<AnimalBrain>();
        if (brain != null)
            brain.Bind();
    }

    void Update()
    {
        if (suspendSelfTick)
            return;

        Simulate(Time.deltaTime);
    }

    public void SetSuspendSelfTick(bool suspend)
    {
        suspendSelfTick = suspend;
    }

    public void SimulatePresence()
    {
        if (dead)
            return;

        bool shouldBePresent = IsPresent();
        if (shouldBePresent != present)
            SetPresent(shouldBePresent);
    }

    public void Simulate(float delta)
    {
        step = delta;
        if (dead)
        {
            if (brain != null && brain.isActiveAndEnabled)
                brain.Tick(delta);
            TickDrop(delta);
            return;
        }

        TickWound(delta);
        SimulatePresence();
        if (!present)
            return;

        if (brain != null && brain.isActiveAndEnabled)
        {
            brain.Tick(delta);
            if (kind != AnimalKind.Bird &&
                brain.State != AnimalAiState.Flee &&
                brain.State != AnimalAiState.Dead)
                TryNightAttack();
            return;
        }

        if (kind == AnimalKind.Bird)
            return;

        if (fleeing)
        {
            fleeTimer -= step;
            float flee = fleeSpeed * WoundMoveScale;
            Vector3 away = transform.position + transform.forward * 2f;
            Vector3 next = WorldGround.Snap(
                Vector3.MoveTowards(transform.position, away, flee * step));
            if (WorldGround.InsideWater(next))
            {
                transform.Rotate(0f, 110f, 0f);
                away = transform.position + transform.forward * 2f;
                next = WorldGround.Snap(
                    Vector3.MoveTowards(transform.position, away, flee * step));
            }

            transform.position = next;
            UpdateAnimation(flee > fleeSpeed * 0.7f);

            if (fleeTimer <= 0f)
                StartReturningHome();

            return;
        }

        if (returningHome)
        {
            Vector3 home = WorldGround.Snap(homePos);
            FaceDirection(home - transform.position);
            transform.position = WorldGround.Snap(
                Vector3.MoveTowards(transform.position, home, moveSpeed * WoundMoveScale * step));
            UpdateAnimation(false);

            if (Vector3.Distance(Flat(transform.position), Flat(home)) < 0.6f)
            {
                returningHome = false;
                PickWanderTarget();
            }

            TryNightAttack();
            return;
        }

        if (MeatBait.TryGetTarget(transform.position, kind, out Vector3 baitPoint))
        {
            float baitDist = Vector3.Distance(Flat(transform.position), Flat(baitPoint));
            if (baitDist > 1.4f)
            {
                FaceDirection(baitPoint - transform.position);
                Vector3 next = WorldGround.Snap(
                    Vector3.MoveTowards(transform.position, baitPoint, moveSpeed * 1.35f * step));
                if (!WorldGround.InsideWater(next))
                    transform.position = next;
            }

            UpdateAnimation(false);
            TryNightAttack();
            return;
        }

        FaceDirection(wanderTarget - transform.position);
        transform.position = WorldGround.Snap(
            Vector3.MoveTowards(transform.position, wanderTarget, moveSpeed * WoundMoveScale * step));
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

        if (objectName.IndexOf("Bird", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Sparrow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Crow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Duck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Hawk", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            kind = AnimalKind.Bird;
            if (objectName.IndexOf("Crow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                flyer = FlyerKind.Crow;
            else if (objectName.IndexOf("Duck", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                flyer = FlyerKind.Duck;
                if (meatAmount < 2)
                    meatAmount = 2;
            }
            else if (objectName.IndexOf("Hawk", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                flyer = FlyerKind.Hawk;
                if (meatAmount < 3)
                    meatAmount = 3;
            }
            else
                flyer = FlyerKind.Sparrow;
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
        if (on && kind != AnimalKind.Bird)
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
            6f * step);
    }

    void UpdateAnimation(bool running)
    {
        if (animalAnimator == null)
            return;

        if (Limping)
            running = false;

        animalAnimator.speed = Limping ? 0.68f : 1f;

        float distanceMoved = Vector3.Distance(transform.position, previousPosition);
        bool moving = distanceMoved > 0.0001f;

        animalAnimator.SetFloat(
            VerticalAnimation,
            moving ? 1f : 0f,
            0.12f,
            step);
        animalAnimator.SetFloat(
            StateAnimation,
            running ? 1f : 0f,
            0.12f,
            step);

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

    public void NoticeThreat(Vector3 danger)
    {
        if (brain != null && brain.isActiveAndEnabled)
            brain.Notice(danger, false);
        else
            FleeImmediate(danger);
    }

    public void FleeFrom(Vector3 danger)
    {
        if (brain != null && brain.isActiveAndEnabled)
            brain.Notice(danger, true);
        else
            FleeImmediate(danger);
    }

    public void BeginFlee(Vector3 direction, float duration)
    {
        fleeing = true;
        returningHome = false;
        fleeTimer = duration > 0.05f ? duration : fleeDuration;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            direction = transform.forward;
        direction.Normalize();
        if (kind == AnimalKind.Bird)
            transform.forward = direction;
        else
            transform.forward = direction;
    }

    public void EndFlee()
    {
        fleeing = false;
        returningHome = false;
    }

    public void Hold()
    {
        UpdateAnimation(false);
    }

    public void Face(Vector3 worldPoint)
    {
        FaceDirection(worldPoint - transform.position);
        UpdateAnimation(false);
    }

    public void MoveOnGround(Vector3 destination, float speed, bool running)
    {
        FaceDirection(destination - transform.position);
        transform.position = WorldGround.Snap(
            Vector3.MoveTowards(transform.position, destination, speed * step));
        UpdateAnimation(running);
    }

    public bool MoveHeading(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        Vector3 next = WorldGround.Snap(transform.position + direction * (speed * step));
        if (WorldGround.InsideWater(next))
            return false;

        Quaternion facing = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * step);
        transform.position = next;
        UpdateAnimation(!Limping && speed > fleeSpeed * 0.7f);
        return true;
    }

    /// <summary>A head hit weaves. Other wounds keep the heading the caller chose.</summary>
    public Vector3 Steer(Vector3 heading)
    {
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.0001f || !wounded || woundPlace != ShotPlace.Head)
            return heading;

        float amount = stumbleLeft > 0f ? 0.65f : 0.22f;
        Vector3 side = Vector3.Cross(Vector3.up, heading.normalized);
        return (heading + side * (Mathf.Sin(Time.time * 6.5f) * amount)).normalized;
    }

    void FleeImmediate(Vector3 danger)
    {
        Vector3 dir = transform.position - danger;
        BeginFlee(dir, fleeDuration);
    }

    public void TickFlee(float dt)
    {
        fleeTimer -= dt;
        if (fleeTimer <= 0f)
            fleeing = false;
    }

    void StartReturningHome()
    {
        fleeing = false;
        returningHome = true;
        FaceDirection(homePos - transform.position);
    }

    void TryNightAttack()
    {
        // Only predators hunt people at night; birds and deer never attack.
        if (kind == AnimalKind.Bird || kind == AnimalKind.Deer)
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

        attackTimer -= step;
        if (attackTimer > 0f)
            return;

        attackTimer = nightAttackCooldown;
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.TakeDamage(nightDamage, "Killed by a predator");
            Debug.Log($"{kind} attacked player! {nightDamage} damage taken.");
        }
    }

    public void Hit()
    {
        Hit(transform.forward);
    }

    public void Hit(Vector3 shotDirection)
    {
        Vector3 point = transform.position + Vector3.up * 1.05f;
        ApplyHit(shotDirection, point, Vector3.up, true);
    }

    public void Hit(Vector3 shotDirection, Vector3 point, Vector3 normal)
    {
        ApplyHit(shotDirection, point, normal, false);
    }

    enum ShotPlace
    {
        Head,
        Chest,
        Body,
        Legs
    }

    void ApplyHit(Vector3 shotDirection, Vector3 point, Vector3 normal, bool killingBlow)
    {
        if (dead || !present)
            return;

        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.up;
        ShotBlood.Play(point, normal, transform);

        if (killingBlow || kind != AnimalKind.Deer)
        {
            FinishKill(shotDirection);
            return;
        }

        ShotPlace place = Classify(point);
        lastHitPlace = place.ToString();
        float amount = DamageFor(place);
        hideLeft -= amount;
        BeginWound(place, point);

        transform.Rotate(0f, Random.Range(-14f, 14f), 0f);
        Vector3 shooter = point - shotDirection;
        FleeFrom(shooter);

        if (hideLeft <= 0f)
        {
            bool sniperVital = SniperEquipped() && (place == ShotPlace.Head || place == ShotPlace.Chest);
            float shotDistance = Vector3.Distance(PlayerView.Position(), point);
            FinishKill(shotDirection, sniperVital, shotDistance);
            return;
        }

        HuntFeedback.Hit(lastHitPlace);
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(WoundLine(place, amount));
    }

    ShotPlace Classify(Vector3 point)
    {
        CacheBones();
        if (skull != null && Vector3.Distance(point, skull.position) <= 0.62f)
            return ShotPlace.Head;

        Vector3 local = transform.InverseTransformPoint(point);
        if (local.y < 0.72f)
            return ShotPlace.Legs;

        float chestDist = chestBone != null ? Vector3.Distance(point, chestBone.position) : 99f;
        float rearDist = rearBone != null ? Vector3.Distance(point, rearBone.position) : 99f;
        if (chestBone != null && chestDist < rearDist && chestDist <= 0.7f)
            return ShotPlace.Chest;

        if (local.y > 1.15f)
            return ShotPlace.Chest;

        return ShotPlace.Body;
    }

    void CacheBones()
    {
        if (bonesCached)
            return;

        bonesCached = true;
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            if (skull == null && boneName == "scull")
                skull = bones[i];
            else if (chestBone == null && (boneName == "spine.003" || boneName == "spine.004"))
                chestBone = bones[i];
            else if (rearBone == null && (boneName == "thigh.L" || boneName == "spine.001"))
                rearBone = bones[i];
        }
    }

    static float DamageFor(ShotPlace place)
    {
        switch (place)
        {
            case ShotPlace.Head: return 50f;
            case ShotPlace.Chest: return 34f;
            case ShotPlace.Legs: return 15f;
            default: return 20f;
        }
    }

    string WoundLine(ShotPlace place, float amount)
    {
        int left = Mathf.CeilToInt(hideLeft / Mathf.Max(1f, amount));
        if (left <= 1)
        {
            if (place == ShotPlace.Legs)
                return "Leg shot. One more drops it.";
            if (place == ShotPlace.Head)
                return "Head shot. One more there drops it.";
            if (place == ShotPlace.Chest)
                return "Chest shot. One more there drops it.";
            return "Body shot. One more there drops it.";
        }

        if (place == ShotPlace.Legs)
            return "Leg shot. It stumbles and limps.";
        if (place == ShotPlace.Head)
            return "Head shot. It staggers.";
        if (place == ShotPlace.Chest)
            return "Chest shot. It bolts, then slows.";
        return "Body shot. It runs, then limps.";
    }

    void BeginWound(ShotPlace place, Vector3 point)
    {
        bool alreadyLimping = Limping;
        Vector3 local = transform.InverseTransformPoint(point);
        woundSide = Mathf.Abs(local.x) < 0.04f
            ? (Random.value < 0.5f ? -1f : 1f)
            : (local.x >= 0f ? 1f : -1f);
        wounded = true;
        woundPlace = place;

        // A broken leg or a head hit cannot carry a clean sprint. Gut and chest shots do,
        // for a moment, the way a deer runs off before the wound shows in the stride.
        switch (place)
        {
            case ShotPlace.Legs:
                stumbleLeft = 0.45f;
                boltLeft = 0f;
                break;
            case ShotPlace.Head:
                stumbleLeft = 0.55f;
                boltLeft = 0f;
                break;
            case ShotPlace.Chest:
                stumbleLeft = 0f;
                boltLeft = alreadyLimping ? 0.55f : 1.7f;
                break;
            default:
                stumbleLeft = 0f;
                boltLeft = alreadyLimping ? 0.4f : 1.15f;
                break;
        }

        PoseWound();
    }

    void TickWound(float delta)
    {
        if (!wounded)
            return;

        stumbleLeft = Mathf.Max(0f, stumbleLeft - delta);
        boltLeft = Mathf.Max(0f, boltLeft - delta);
        PoseWound();
    }

    void LateUpdate()
    {
        if (!wounded || dead)
            return;

        if (!bleedAnchorSet)
        {
            bleedAnchor = transform.position;
            bleedAnchorSet = true;
            return;
        }

        float moved = Vector3.Distance(transform.position, bleedAnchor);
        bleedAnchor = transform.position;
        if (moved < 0.04f)
            return;

        bleedLeft -= Time.deltaTime;
        if (bleedLeft > 0f)
            return;

        bleedLeft = Limping ? 0.7f : 1.35f;
        DripBlood();
    }

    void PoseWound()
    {
        if (woundPose == null)
            woundPose = GetComponent<DeerWoundPose>() ?? gameObject.AddComponent<DeerWoundPose>();

        float missing = 1f - Mathf.Clamp01(hideLeft / 100f);
        float shown = Limping
            ? Mathf.Clamp01(0.62f + missing * 0.3f + (woundPlace == ShotPlace.Legs ? 0.2f : 0f))
            : (stumbleLeft > 0f ? 0.78f : 0.36f);
        bool tuck = woundPlace == ShotPlace.Legs || Limping;
        bool low = woundPlace == ShotPlace.Head || woundPlace == ShotPlace.Chest || Limping;
        woundPose.Show(shown, woundSide, tuck, low);
    }

    void DripBlood()
    {
        TrackPool pool = TrackPool.Current;
        if (pool == null)
            return;

        Vector3 ground = WorldGround.Snap(transform.position);
        ground.y += 0.05f;
        pool.Place(TrackKind.Blood, ground, 0f, 0.22f, 0f);
    }

    static bool SniperEquipped()
    {
        return PlayerShotgun.Instance != null && PlayerShotgun.Instance.Equipped == HuntWeapon.Sniper;
    }

    void FinishKill(Vector3 shotDirection, bool sniperVital = false, float shotDistance = 0f)
    {
        if (dead)
            return;

        dead = true;
        if (kind != AnimalKind.Bird)
            meatAmount += HuntProgress.BonusMeat;
        LeaveBlood(transform.position);
        float killDistance = shotDistance > 0.1f
            ? shotDistance
            : Vector3.Distance(PlayerView.Position(), transform.position);
        HuntFeedback.Kill(this, lastHitPlace, killDistance, meatAmount);

        HuntChallenges.ReportKill(this);
        DeerChallenge.NotifyKill(this, sniperVital, shotDistance);
        WildlifeAlarm.NotifyShot(transform.position);
        Overhunt.RegisterKill(transform.position);

        if (kind == AnimalKind.Legendary && Trader.Instance != null)
            Trader.Instance.LegendaryTakenByPlayer();

        if (kind == AnimalKind.Bird)
        {
            FallenBird drop = gameObject.GetComponent<FallenBird>();
            if (drop == null)
                drop = gameObject.AddComponent<FallenBird>();
            drop.Begin(meatAmount);
            return;
        }

        MeatPickup.Spawn(transform.position, meatAmount);
        BeginDrop(shotDirection);
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(kind == AnimalKind.Deer ? "Deer down." : "It drops.");
    }

    void BeginDrop(Vector3 shotDirection)
    {
        if (animalAnimator == null)
            animalAnimator = GetComponentInChildren<Animator>();
        if (animalAnimator != null)
        {
            animalAnimator.speed = 1f;
            animalAnimator.enabled = false;
        }

        if (woundPose != null)
            woundPose.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Vector3 flatShot = shotDirection;
        flatShot.y = 0f;
        if (flatShot.sqrMagnitude < 0.001f)
            flatShot = transform.forward;
        flatShot.Normalize();

        // Always roll onto a side, never pitch over the nose or tail. Pitching around the
        // feet pivot lifted the hindquarters a metre into the air on front and back shots.
        Vector3 along = transform.forward;
        along.y = 0f;
        if (along.sqrMagnitude < 0.001f)
            along = Vector3.forward;
        along.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, along);
        // A positive roll about forward tips the back toward the left, so fall away from the shot.
        float roll = Vector3.Dot(flatShot, right) >= 0f ? -84f : 84f;

        standRotation = transform.rotation;
        laidRotation = Quaternion.AngleAxis(roll, along) * standRotation;
        fallFrom = transform.position;
        fallTo = WorldGround.Snap(transform.position) + Vector3.up * 0.28f;
        dropTime = 0f;
        dropping = true;
    }

    void TickDrop(float delta)
    {
        if (!dropping)
            return;

        dropTime += delta;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dropTime / 0.45f));
        transform.rotation = Quaternion.Slerp(standRotation, laidRotation, t);
        transform.position = Vector3.Lerp(fallFrom, fallTo, t);
        if (t >= 1f)
            dropping = false;
    }

    void LeaveBlood(Vector3 position)
    {
        AnimalTracks tracks = GetComponent<AnimalTracks>();
        if (tracks != null)
        {
            tracks.SpillBlood(position);
            return;
        }

        TrackPool pool = TrackPool.Current;
        if (pool == null)
            return;

        Vector3 ground = WorldGround.Snap(position);
        ground.y += 0.06f;
        pool.Place(TrackKind.Blood, ground, 0f, 0f, 0f);
    }
}
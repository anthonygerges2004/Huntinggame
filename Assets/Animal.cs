using HuntingGame.Animals;
using HuntingGame.Tracking;
using UnityEngine;

public enum AnimalKind
{
    Bird,
    Deer,
    Large,
    Legendary,
    Game
}

public class Animal : MonoBehaviour
{
    public AnimalKind kind = AnimalKind.Deer;
    public FlyerKind flyer = FlyerKind.Sparrow;
    public QuarryKind quarry = QuarryKind.None;

    public bool HeavyShot =>
        kind == AnimalKind.Large || kind == AnimalKind.Legendary ||
        (kind == AnimalKind.Game && (quarry == QuarryKind.Boar || quarry == QuarryKind.Pig));
    public int meatAmount = 2;
    public float moveSpeed = 2f;
    public float fleeSpeed = 8f;
    public float nightDamage = 8f;
    public float nightAttackRange = 4f;
    public float nightAttackCooldown = 1.5f;

    Vector3 homePos;
    Vector3 wanderTarget;
    Vector3 previousPosition;
    Vector3 lastStep;
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
    int hitsTaken; // every hit marks the hide; three or more costs the pelt a grade
    float dropTime;
    Quaternion standRotation;
    Vector3 fallFrom;
    Vector3 fallPush;
    float fallPitch;
    float fallRoll;
    float fallSeconds = 0.9f;
    float fallLay = 0.24f;
    ShotPlace deathPlace;
    bool fallBled;
    Transform deathHead;
    Vector3 deathHeadHome;
    LimbFold[] deathLimbs;
    int deathLimbCount;

    struct LimbFold
    {
        public Transform bone;
        public Quaternion rest;
        public Vector3 degrees;
    }

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
    /// Every wound prevents sprinting; leg damage is the most restrictive.
    /// </summary>
    public float WoundMoveScale
    {
        get
        {
            if (!wounded)
                return 1f;
            if (stumbleLeft > 0f)
                return 0f;

            switch (woundPlace)
            {
                case ShotPlace.Legs: return Mathf.Min(0.04f, 0.3f / Mathf.Max(1f, fleeSpeed));
                case ShotPlace.Head: return Mathf.Min(0.06f, 0.45f / Mathf.Max(1f, fleeSpeed));
                case ShotPlace.Chest: return Mathf.Min(0.08f, 0.6f / Mathf.Max(1f, fleeSpeed));
                default: return Mathf.Min(0.1f, 0.8f / Mathf.Max(1f, fleeSpeed));
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

        AnimalGrounding.GroundLivingRoot(this);

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
        if (NameHas(objectName, "Legendary"))
        {
            kind = AnimalKind.Legendary;
            nocturnal = true;
            if (meatAmount < 10)
                meatAmount = 15;
            return;
        }

        if (TryBirdFromName(objectName) || TryGameFromName(objectName))
            return;

        if (NameHas(objectName, "Big") || NameHas(objectName, "Large"))
        {
            kind = AnimalKind.Large;
            nocturnal = true;
            if (meatAmount <= 2)
                meatAmount = 5;
            if (nightDamage <= 8f)
                nightDamage = 14f;
            return;
        }

        if (kind == AnimalKind.Game)
            ApplyQuarryStats();
        else if (kind == AnimalKind.Bird)
            ApplyBirdStats();
    }

    bool TryBirdFromName(string objectName)
    {
        FlyerKind found;
        if (NameHas(objectName, "Woodpecker"))
            found = FlyerKind.Woodpecker;
        else if (NameHas(objectName, "Pheasant"))
            found = FlyerKind.Pheasant;
        else if (NameHas(objectName, "Sparrow"))
            found = FlyerKind.Sparrow;
        else if (NameHas(objectName, "Thrush"))
            found = FlyerKind.Thrush;
        else if (NameHas(objectName, "Pigeon"))
            found = FlyerKind.Pigeon;
        else if (NameHas(objectName, "Robin"))
            found = FlyerKind.Robin;
        else if (NameHas(objectName, "Goose"))
            found = FlyerKind.Goose;
        else if (NameHas(objectName, "Crow"))
            found = FlyerKind.Crow;
        else if (NameHas(objectName, "Duck"))
            found = FlyerKind.Duck;
        else if (NameHas(objectName, "Hawk"))
            found = FlyerKind.Hawk;
        else if (NameHas(objectName, "Owl"))
            found = FlyerKind.Owl;
        else if (NameHas(objectName, "Jay"))
            found = FlyerKind.Jay;
        else if (NameHas(objectName, "Bird"))
            found = FlyerKind.Sparrow;
        else
            return false;

        kind = AnimalKind.Bird;
        flyer = found;
        ApplyBirdStats();
        return true;
    }

    void ApplyBirdStats()
    {
        FlyerProfile profile = FlyerProfile.Of(flyer);
        meatAmount = profile.meat;
        nocturnal = profile.nocturnal;
        moveSpeed = profile.cruiseSpeed;
        fleeSpeed = profile.fleeSpeed;
    }

    bool TryGameFromName(string objectName)
    {
        if (NameHas(objectName, "Boar"))
            quarry = QuarryKind.Boar;
        else if (NameHas(objectName, "Rabbit"))
            quarry = QuarryKind.Rabbit;
        else if (NameHas(objectName, "Wolf"))
            quarry = QuarryKind.Wolf;
        else if (NameHas(objectName, "Fox"))
            quarry = QuarryKind.Fox;
        else if (NameHas(objectName, "Pig"))
            quarry = QuarryKind.Pig;
        else
            return false;

        ApplyQuarryStats();
        return true;
    }

    void ApplyQuarryStats()
    {
        if (quarry == QuarryKind.None)
            quarry = QuarryKind.Pig;
        QuarryProfile profile = QuarryProfile.Of(quarry);
        kind = AnimalKind.Game;
        meatAmount = profile.meat;
        nightDamage = profile.nightDamage;
        nightAttackRange = profile.attackRange;
        nocturnal = profile.nocturnal;
    }

    static bool NameHas(string objectName, string token)
    {
        return objectName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
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
            lastStep = Vector3.zero;
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

        if (wounded)
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
        // Birds, deer, and small game never attack. Boar and wolves do, at night.
        if (kind == AnimalKind.Bird || kind == AnimalKind.Deer || nightDamage <= 0f)
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

        ShotPlace place = PlaceOf(point);
        lastHitPlace = place.ToString();
        hitsTaken++;
        if (killingBlow || kind != AnimalKind.Deer)
        {
            FinishKill(shotDirection, false, 0f, place);
            return;
        }

        float amount = DamageFor(place);
        hideLeft -= amount;
        BeginWound(place, point);

        transform.Rotate(-9f, Random.Range(-18f, 18f), Random.Range(-6f, 6f), Space.Self);
        Vector3 shooter = point - shotDirection;
        FleeFrom(shooter);

        if (hideLeft <= 0f)
        {
            bool sniperVital = SniperEquipped() && (place == ShotPlace.Head || place == ShotPlace.Chest);
            float shotDistance = Vector3.Distance(PlayerView.Position(), point);
            FinishKill(shotDirection, sniperVital, shotDistance, place);
            return;
        }

        HuntFeedback.Hit(lastHitPlace);
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(WoundLine(place, amount));
    }

    ShotPlace PlaceOf(Vector3 point)
    {
        CacheBones();
        if (skull != null)
            return Classify(point);

        Vector3 local = transform.InverseTransformPoint(point);
        float reach = 0.28f;
        if (kind == AnimalKind.Bird)
            reach = 0.1f;
        else if (kind == AnimalKind.Game && quarry == QuarryKind.Rabbit)
            reach = 0.1f;
        else if (kind == AnimalKind.Game && (quarry == QuarryKind.Wolf || quarry == QuarryKind.Boar))
            reach = 0.42f;
        else if (kind == AnimalKind.Large || kind == AnimalKind.Legendary)
            reach = 0.7f;

        if (local.z > reach * 0.55f && local.y > reach * 0.2f)
            return ShotPlace.Head;
        if (local.y < reach * 0.45f)
            return ShotPlace.Legs;
        if (local.z > 0f)
            return ShotPlace.Chest;
        return ShotPlace.Body;
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
            return "Chest shot. It staggers and struggles to move.";
        return "Body shot. It staggers, then limps slowly.";
    }

    void BeginWound(ShotPlace place, Vector3 point)
    {
        // A later hit must not restore mobility lost to a more severe wound.
        if (wounded && WoundSeverity(woundPlace) > WoundSeverity(place))
            place = woundPlace;
        Vector3 local = transform.InverseTransformPoint(point);
        woundSide = Mathf.Abs(local.x) < 0.04f
            ? (Random.value < 0.5f ? -1f : 1f)
            : (local.x >= 0f ? 1f : -1f);
        wounded = true;
        woundPlace = place;

        // Stop at impact, then allow only an impaired walk.
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
                stumbleLeft = 0.65f;
                boltLeft = 0f;
                break;
            default:
                stumbleLeft = 0.5f;
                boltLeft = 0f;
                break;
        }

        PoseWound();
    }

    static int WoundSeverity(ShotPlace place)
    {
        switch (place)
        {
            case ShotPlace.Legs: return 3;
            case ShotPlace.Head: return 2;
            case ShotPlace.Chest: return 1;
            default: return 0;
        }
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
        if (!dead)
        {
            lastStep = transform.position - previousPosition;
            previousPosition = transform.position;
        }

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

    bool TakesFieldDressing()
    {
        if (kind == AnimalKind.Deer || kind == AnimalKind.Large || kind == AnimalKind.Legendary)
            return true;
        return kind == AnimalKind.Game && QuarryProfile.Of(quarry).fieldDress;
    }

    string DownedLine()
    {
        if (kind == AnimalKind.Deer)
            return "Deer down.";
        if (kind == AnimalKind.Game)
            return QuarryProfile.Of(quarry).downed;
        return "It drops.";
    }

    public void RivalKill(Vector3 direction)
    {
        if (dead || !IsPresent()) return;
        FinishKill(direction, playerCredit: false);
    }

    void FinishKill(Vector3 shotDirection, bool sniperVital = false, float shotDistance = 0f, ShotPlace place = ShotPlace.Body, bool playerCredit = true)
    {
        if (dead)
            return;

        dead = true;
        if (playerCredit && TakesFieldDressing())
            meatAmount += HuntProgress.BonusMeat;
        LeaveBlood(transform.position);
        float killDistance = shotDistance > 0.1f
            ? shotDistance
            : Vector3.Distance(PlayerView.Position(), transform.position);
        if (playerCredit)
        {
            HuntFeedback.Kill(this, lastHitPlace, killDistance, meatAmount);
            HuntChallenges.ReportKill(this);
            DeerChallenge.NotifyKill(this, sniperVital, shotDistance);
        }
        WildlifeAlarm.NotifyShot(transform.position);
        Overhunt.RegisterKill(transform.position);

        if (playerCredit && kind == AnimalKind.Legendary && Trader.Instance != null)
            Trader.Instance.LegendaryTakenByPlayer();

        if (kind == AnimalKind.Bird)
        {
            FallenBird drop = gameObject.GetComponent<FallenBird>();
            if (drop == null)
                drop = gameObject.AddComponent<FallenBird>();
            drop.Begin(meatAmount, shotDirection);
            return;
        }

        // The animal stays as a carcass: take it to the tanner whole, or skin it on the spot.
        HuntWeapon weapon = playerCredit && PlayerShotgun.Instance != null ? PlayerShotgun.Instance.Equipped : HuntWeapon.None;
        AnimalCarcass.Create(this, HuntGoods.FromAnimal(this, lastHitPlace, weapon, killDistance, hitsTaken));
        BeginDrop(shotDirection, place);
        if (playerCredit && Inventory.Instance != null)
            Inventory.Instance.ShowNotice(DownedLine() + "  Walk up to it: E takes the carcass, hold E skins it.");
    }

    void BeginDrop(Vector3 shotDirection, ShotPlace place)
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

        Vector3 travel = lastStep;
        travel.y = 0f;
        float speed = 0f;
        Vector3 heading = transform.forward;
        heading.y = 0f;
        if (travel.sqrMagnitude > 0.000004f && step > 0.0001f)
        {
            speed = Mathf.Clamp(travel.magnitude / step, 0f, 8f);
            heading = travel.normalized;
        }

        if (heading.sqrMagnitude < 0.001f)
            heading = Vector3.forward;
        heading.Normalize();

        Vector3 along = transform.forward;
        along.y = 0f;
        if (along.sqrMagnitude < 0.001f)
            along = Vector3.forward;
        along.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, along);
        float side = Vector3.Dot(flatShot, right) >= 0f ? -1f : 1f;

        // The body stays a side fall. A nose-down pitch on the root lifts the hindquarters.
        deathPlace = place;
        fallLay = LayHeight();
        switch (place)
        {
            case ShotPlace.Head:
                fallSeconds = 0.62f;
                fallPitch = 12f;
                fallRoll = 46f * side;
                fallPush = flatShot * 0.22f + heading * (speed * 0.04f);
                break;
            case ShotPlace.Legs:
                fallSeconds = 0.84f;
                fallPitch = 4f;
                fallRoll = 86f * side;
                fallPush = heading * (0.28f + speed * 0.08f) + flatShot * 0.18f;
                break;
            case ShotPlace.Chest:
                fallSeconds = 1.05f;
                fallPitch = 8f;
                fallRoll = 78f * side;
                fallPush = heading * (0.45f + speed * 0.16f) + flatShot * 0.4f;
                break;
            default:
                fallSeconds = 0.95f;
                fallPitch = 6f;
                fallRoll = 82f * side;
                fallPush = heading * (0.35f + speed * 0.12f) + flatShot * 0.28f;
                break;
        }

        if (kind == AnimalKind.Large || kind == AnimalKind.Legendary)
        {
            fallSeconds += 0.28f;
            fallPush *= 1.3f;
        }
        else if (kind == AnimalKind.Game && quarry == QuarryKind.Rabbit)
        {
            fallSeconds = 0.5f;
            fallPush *= 0.4f;
            fallPitch = Mathf.Min(fallPitch, 6f);
        }

        if (fallPush.magnitude > 1.8f)
            fallPush = fallPush.normalized * 1.8f;

        standRotation = transform.rotation;
        fallFrom = transform.position;
        dropTime = 0f;
        fallBled = false;
        dropping = true;
        GatherDeathLimbs(place);
    }

    float LayHeight()
    {
        if (kind == AnimalKind.Large || kind == AnimalKind.Legendary)
            return 0.4f;
        if (kind != AnimalKind.Game)
            return 0.26f;
        if (quarry == QuarryKind.Rabbit)
            return 0.07f;
        if (quarry == QuarryKind.Fox)
            return 0.16f;
        if (quarry == QuarryKind.Wolf)
            return 0.28f;
        return 0.22f;
    }

    void GatherDeathLimbs(ShotPlace place)
    {
        if (deathLimbs == null)
            deathLimbs = new LimbFold[24];
        deathLimbCount = 0;

        float knee = place == ShotPlace.Legs ? 72f : 50f;
        float headDrop = place == ShotPlace.Head ? 58f : place == ShotPlace.Chest ? 24f : 14f;
        RememberLimb(transform.Find("LegFL"), new Vector3(-knee, 0f, 16f));
        RememberLimb(transform.Find("LegFR"), new Vector3(-knee, 0f, -16f));
        RememberLimb(transform.Find("LegHL"), new Vector3(knee * 0.72f, 0f, -10f));
        RememberLimb(transform.Find("LegHR"), new Vector3(knee * 0.72f, 0f, 10f));
        RememberLimb(transform.Find("LegFL/Knee"), new Vector3(-42f, 0f, 0f));
        RememberLimb(transform.Find("LegFR/Knee"), new Vector3(-42f, 0f, 0f));
        RememberLimb(transform.Find("LegHL/Knee"), new Vector3(34f, 0f, 0f));
        RememberLimb(transform.Find("LegHR/Knee"), new Vector3(34f, 0f, 0f));

        deathHead = transform.Find("Head");
        if (deathHead != null)
        {
            deathHeadHome = deathHead.localPosition;
            RememberLimb(deathHead, new Vector3(headDrop, 0f, 0f));
        }

        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            Transform bone = bones[i];
            float hinge = bone.localRotation.x >= 0f ? 1f : -1f;
            if (boneName == "thigh.L" || boneName == "thigh.R")
                RememberLimb(bone, new Vector3(50f * hinge, 0f, 0f));
            else if (boneName == "shin.L" || boneName == "shin.R")
                RememberLimb(bone, new Vector3(28f * hinge, 0f, 0f));
            else if (boneName.StartsWith("front_thigh"))
                RememberLimb(bone, new Vector3(36f * hinge, 0f, 0f));
            else if (boneName.StartsWith("front_shin"))
                RememberLimb(bone, new Vector3(18f * hinge, 0f, 0f));
            else if (boneName == "scull")
                RememberLimb(bone, new Vector3(headDrop * hinge, 0f, 0f));
        }
    }

    void RememberLimb(Transform bone, Vector3 degrees)
    {
        if (bone == null || deathLimbs == null || deathLimbCount >= deathLimbs.Length)
            return;

        deathLimbs[deathLimbCount].bone = bone;
        deathLimbs[deathLimbCount].rest = bone.localRotation;
        deathLimbs[deathLimbCount].degrees = degrees;
        deathLimbCount++;
    }

    void TickDrop(float delta)
    {
        if (!dropping)
            return;

        dropTime += delta;
        float t = Mathf.Clamp01(dropTime / Mathf.Max(0.2f, fallSeconds));
        float slide = 1f - Mathf.Exp(-t * 3.1f);
        float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 1f, t));
        float flinch = Mathf.Sin(Mathf.Clamp01(t / 0.12f) * Mathf.PI);
        float rollStart = deathPlace == ShotPlace.Head ? 0.04f : deathPlace == ShotPlace.Legs ? 0.08f : 0.2f;
        float rollT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rollStart, 0.92f, t));

        Vector3 pos = fallFrom + fallPush * slide;
        Vector3 ground = WorldGround.Snap(pos);
        float hop = Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * (deathPlace == ShotPlace.Head ? 0.02f : 0.07f);
        pos.y = Mathf.Lerp(fallFrom.y, ground.y + fallLay, settle) + hop * (1f - settle);

        Vector3 along = standRotation * Vector3.forward;
        along.y = 0f;
        if (along.sqrMagnitude < 0.001f)
            along = Vector3.forward;
        along.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, along);
        float pitchNow = fallPitch * rollT + 10f * flinch;
        Quaternion pitched = Quaternion.AngleAxis(pitchNow, right) * standRotation;
        transform.SetPositionAndRotation(pos, Quaternion.AngleAxis(fallRoll * rollT, along) * pitched);

        float bendStart = deathPlace == ShotPlace.Legs ? 0.02f : 0.12f;
        float bend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bendStart, 0.78f, t));
        float twitch = Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI * 2f) * (1f - t) * 0.45f;
        bend = Mathf.Clamp01(bend + twitch);
        for (int i = 0; i < deathLimbCount; i++)
        {
            LimbFold limb = deathLimbs[i];
            if (limb.bone == null)
                continue;
            limb.bone.localRotation = limb.rest * Quaternion.Euler(limb.degrees * bend);
        }

        if (deathHead != null)
        {
            float tuck = bend * (deathPlace == ShotPlace.Head ? 1.15f : 0.45f);
            deathHead.localPosition = deathHeadHome + new Vector3(0f, -0.05f, -0.04f) * tuck;
        }

        if (!fallBled && t > 0.4f)
        {
            fallBled = true;
            LeaveBlood(pos);
        }

        if (t >= 1f)
        {
            dropping = false;
            LeaveBlood(transform.position);
        }
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

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace DeerHunt
{
    /// <summary>
    /// Deer state machine. Idle grazes, Wander picks a NavMesh point, Alert looks up,
    /// Flee runs away, Wounded limps, Dead leaves the body.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class DeerController : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Wander,
            Alert,
            Flee,
            Wounded,
            Dead
        }

        [SerializeField] float detectRadius = 18f;
        [SerializeField] float loseRadius = 46f;
        [SerializeField] float hearRadius = 42f;
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float fleeSpeed = 6.4f;
        [SerializeField] float limpSpeed = 1.15f;
        [SerializeField] float idleSeconds = 3f;
        [SerializeField] float alertSeconds = 0.9f;
        [SerializeField] float calmSeconds = 4f;
        [SerializeField] float wanderRadius = 16f;
        [SerializeField] float chestCollapseDelay = 2.5f;

        [SerializeField] RuntimeAnimatorController animatorController;

        NavMeshAgent agent;
        Health health;
        Animator animator;
        State state;
        float stateTimer;
        float calmTimer;
        bool collapsing;
        Transform player;
        Vector3 home;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int IsAlertId = Animator.StringToHash("IsAlert");
        static readonly int IsHitId = Animator.StringToHash("IsHit");
        static readonly int IsDeadId = Animator.StringToHash("IsDead");
        static readonly int VertId = Animator.StringToHash("Vert");
        static readonly int StateId = Animator.StringToHash("State");

        public void UseAnimator(RuntimeAnimatorController controller)
        {
            if (controller == null)
                return;

            animatorController = controller;
            ApplyAnimatorController(controller);
        }

        void ApplyAnimatorController(RuntimeAnimatorController controller)
        {
            if (animator == null || controller == null)
                return;

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            BindAvatar();
            animator.Rebind();
            animator.Update(0f);
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            home = transform.position;
            DisableImportedMotion();
            if (GetComponentsInChildren<HitZone>(true).Length == 0)
                BuildHitZones();

            BindAvatar();
            if (animatorController == null)
                animatorController = LoadBuiltController();
            ApplyAnimatorController(animatorController);
            agent.angularSpeed = 240f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 1.1f;
            agent.autoBraking = true;
        }

        void OnEnable()
        {
            health.OnDeath += Die;
            WeaponController.ShotFired += HearShot;
        }

        void OnDisable()
        {
            health.OnDeath -= Die;
            WeaponController.ShotFired -= HearShot;
        }

        void Start()
        {
            PlayerController body = FindAnyObjectByType<PlayerController>();
            player = body != null ? body.transform : null;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            EnterIdle();
        }

        void Update()
        {
            if (state == State.Dead)
                return;

            if (!agent.isOnNavMesh)
                return;

            float distance = player == null ? 999f : Vector3.Distance(transform.position, player.position);
            if (state != State.Wounded && state != State.Flee && distance <= detectRadius)
                EnterAlert();

            switch (state)
            {
                case State.Idle:
                    TickIdle();
                    break;
                case State.Wander:
                    TickWander(distance);
                    break;
                case State.Alert:
                    TickAlert(distance);
                    break;
                case State.Flee:
                    TickFlee(distance);
                    break;
                case State.Wounded:
                    TickWounded();
                    break;
            }

            DriveAnimator();
        }

        public void TakeShot(HitZone zone, Vector3 point, Vector3 shooter, float baseDamage)
        {
            if (health.Dead || zone == null)
                return;

            player = FindAnyObjectByType<PlayerController>()?.transform;
            float amount = zone.InstantKill ? health.Max : baseDamage * zone.DamageMultiplier;
            bool willDie = zone.InstantKill || health.Current - amount <= 0f;
            float distance = Vector3.Distance(transform.position, shooter);

            if (animator != null && Has(IsHitId))
                animator.SetTrigger(IsHitId);

            HitMarkerUI.Mark(willDie, zone.Label, point, distance);
            transform.Rotate(0f, Random.Range(-18f, 18f), 0f);
            health.Damage(amount);

            if (health.Dead)
            {
                GameManager.Instance?.RegisterKill(zone, distance);
                return;
            }

            float side = HitSide(point);
            if (zone.Kind == HitZone.Zone.Chest)
                BeginCollapse(side);
            else if (zone.Kind == HitZone.Zone.Legs)
                StartReaction(StumbleThenLimp(side));
            else
                StartReaction(BoltThenLimp(side));
        }

        Coroutine reaction;

        void StartReaction(IEnumerator routine)
        {
            if (reaction != null)
                StopCoroutine(reaction);
            reaction = StartCoroutine(routine);
        }

        float HitSide(Vector3 point)
        {
            float x = transform.InverseTransformPoint(point).x;
            if (Mathf.Abs(x) < 0.04f)
                return Random.value < 0.5f ? -1f : 1f;
            return x >= 0f ? 1f : -1f;
        }

        void ShowWound(float side, float severity, bool tuckLeg, bool dropHead)
        {
            DeerWoundPose pose = GetComponent<DeerWoundPose>();
            if (pose == null)
                pose = gameObject.AddComponent<DeerWoundPose>();
            pose.Show(severity, side, tuckLeg, dropHead);
        }

        void TickIdle()
        {
            Halt();
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
                EnterWander();
        }

        void TickWander(float distance)
        {
            if (!OnMesh())
                return;

            agent.isStopped = false;
            agent.speed = walkSpeed;
            if (agent.pathPending)
                return;
            if (!agent.hasPath)
            {
                EnterIdle();
                return;
            }

            if (agent.remainingDistance <= agent.stoppingDistance + 0.35f)
                EnterIdle();
            else if (distance < loseRadius)
                calmTimer = calmSeconds;
        }

        void TickAlert(float distance)
        {
            Halt();
            LookAround();
            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f)
                return;

            // A nearby player or a shot leads to a run. Only a far hunter is ignored.
            if (distance <= loseRadius)
                EnterFlee();
            else
                EnterIdle();
        }

        void TickFlee(float distance)
        {
            if (!OnMesh())
                return;

            agent.isStopped = false;
            agent.speed = fleeSpeed;
            if (distance > loseRadius)
            {
                calmTimer -= Time.deltaTime;
                if (calmTimer <= 0f)
                    EnterIdle();
            }
            else
            {
                calmTimer = calmSeconds;
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
                    SetAwayDestination();
            }
        }

        void TickWounded()
        {
            if (!OnMesh())
                return;

            agent.isStopped = false;
            agent.speed = limpSpeed;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.4f)
                SetAwayDestination();
        }

        void EnterIdle()
        {
            state = State.Idle;
            stateTimer = idleSeconds;
            calmTimer = calmSeconds;
            Halt();
        }

        void EnterWander()
        {
            state = State.Wander;
            if (!OnMesh())
            {
                EnterIdle();
                return;
            }

            agent.isStopped = false;
            agent.speed = walkSpeed;
            Vector3 point = home + Random.insideUnitSphere * wanderRadius;
            point.y = transform.position.y;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                EnterIdle();
        }

        void EnterAlert()
        {
            if (state == State.Alert || state == State.Flee || state == State.Dead || state == State.Wounded)
                return;

            state = State.Alert;
            stateTimer = alertSeconds;
            Halt();
        }

        void EnterFlee()
        {
            if (state == State.Dead)
                return;

            state = State.Flee;
            calmTimer = calmSeconds;
            if (!OnMesh())
                return;

            agent.isStopped = false;
            agent.speed = fleeSpeed;
            SetAwayDestination();
        }

        void EnterWounded()
        {
            if (state == State.Dead)
                return;

            state = State.Wounded;
            if (!OnMesh())
                return;

            agent.isStopped = false;
            agent.speed = limpSpeed;
            SetAwayDestination();
        }

        void BeginCollapse(float side)
        {
            if (collapsing || health.Dead)
                return;

            collapsing = true;
            StartReaction(CollapseRoutine(side));
        }

        IEnumerator CollapseRoutine(float side)
        {
            // A chest hit runs off, then the stride breaks, then the deer goes down.
            ShowWound(side, 0.34f, false, true);
            EnterFlee();
            yield return new WaitForSeconds(1.1f);
            if (health.Dead)
                yield break;

            ShowWound(side, 0.9f, true, true);
            EnterWounded();
            yield return new WaitForSeconds(Random.Range(1.6f, 2.4f));
            if (health.Dead)
                yield break;

            PlayerController body = FindAnyObjectByType<PlayerController>();
            float distance = body == null ? 0f : Vector3.Distance(transform.position, body.transform.position);
            GameManager.Instance?.RegisterKill(FindZone(HitZone.Zone.Chest), distance);
            health.Kill();
        }

        IEnumerator StumbleThenLimp(float side)
        {
            ShowWound(side, 0.75f, true, false);
            if (OnMesh())
            {
                agent.speed = 0f;
                agent.isStopped = true;
            }

            yield return new WaitForSeconds(0.45f);
            if (!health.Dead)
            {
                ShowWound(side, 0.9f, true, true);
                EnterWounded();
            }
        }

        IEnumerator BoltThenLimp(float side)
        {
            ShowWound(side, 0.3f, false, true);
            EnterFlee();
            yield return new WaitForSeconds(1.15f);
            if (!health.Dead)
            {
                ShowWound(side, 0.8f, true, true);
                EnterWounded();
            }
        }

        void HearShot(Vector3 position)
        {
            if (health.Dead || state == State.Wounded)
                return;

            if (Vector3.Distance(transform.position, position) <= hearRadius)
                EnterAlert();
        }

        void Die()
        {
            if (state == State.Dead)
                return;

            state = State.Dead;
            StopAllCoroutines();
            DeerWoundPose pose = GetComponent<DeerWoundPose>();
            if (pose != null)
                pose.Clear();
            if (animator != null)
                animator.speed = 1f;
            if (OnMesh())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (agent != null)
                agent.enabled = false;
            HitZone[] zones = GetComponentsInChildren<HitZone>(true);
            for (int i = 0; i < zones.Length; i++)
            {
                Collider box = zones[i].GetComponent<Collider>();
                if (box != null)
                    box.enabled = false;
            }
            if (animator != null && Has(IsAlertId))
                animator.SetBool(IsAlertId, false);
            if (animator != null && Has(IsDeadId))
                animator.SetBool(IsDeadId, true);
            StartCoroutine(TipOver());
        }

        IEnumerator TipOver()
        {
            Quaternion from = transform.rotation;
            Quaternion to = Quaternion.AngleAxis(80f, transform.right) * from;
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.45f;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.rotation = Quaternion.Slerp(from, to, s);
                transform.position = start;
                yield return null;
            }
        }

        void SetAwayDestination()
        {
            Vector3 away = transform.position - (player != null ? player.position : transform.position + Vector3.back);
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            away.Normalize();
            Vector3 point = transform.position + away * 14f;
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        void LookAround()
        {
            Vector3 forward = player != null ? player.position - transform.position : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = transform.forward;

            float sweep = Mathf.Sin(Time.time * 2.4f) * 40f;
            Quaternion look = Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(0f, sweep, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
        }

        bool OnMesh()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
        }

        void Halt()
        {
            if (!OnMesh())
                return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        void DriveAnimator()
        {
            if (animator == null)
                return;

            float speed = agent.enabled ? agent.velocity.magnitude : 0f;
            bool alert = state == State.Alert;
            if (Has(SpeedId))
                animator.SetFloat(SpeedId, speed);
            if (Has(IsAlertId))
                animator.SetBool(IsAlertId, alert);

            // The deer model already in this project blends idle / walk / run on Vert and State.
            if (Has(VertId))
                animator.SetFloat(VertId, speed > 0.25f ? 1f : 0f);
            bool running = state != State.Wounded && speed > 3.2f;
            if (Has(StateId))
                animator.SetFloat(StateId, running ? 1f : 0f);
            animator.speed = state == State.Wounded ? 0.68f : 1f;
        }

        void BuildHitZones()
        {
            foreach (Collider existing in GetComponentsInChildren<Collider>(true))
                existing.enabled = false;

            Bounds world = new Bounds(transform.position + Vector3.up, new Vector3(0.5f, 1.2f, 1.4f));
            SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null)
                world = skin.bounds;

            Vector3 center = transform.InverseTransformPoint(world.center);
            Vector3 size = world.size;
            int layer = LayerMask.NameToLayer("Deer");

            MakeZone("Head", HitZone.Zone.Head, PrimitiveType.Sphere,
                center + new Vector3(0f, size.y * 0.28f, size.z * 0.32f),
                new Vector3(size.x * 0.38f, size.y * 0.28f, size.z * 0.28f), 1.25f, false, layer);
            MakeZone("Chest", HitZone.Zone.Chest, PrimitiveType.Sphere,
                center + new Vector3(0f, size.y * 0.08f, size.z * 0.08f),
                new Vector3(size.x * 0.55f, size.y * 0.32f, size.z * 0.34f), 0.85f, false, layer);
            MakeZone("Body", HitZone.Zone.Body, PrimitiveType.Cube,
                center + new Vector3(0f, -size.y * 0.02f, -size.z * 0.16f),
                new Vector3(size.x * 0.7f, size.y * 0.36f, size.z * 0.42f), 0.5f, false, layer);
            MakeZone("Legs", HitZone.Zone.Legs, PrimitiveType.Cube,
                center + new Vector3(0f, -size.y * 0.32f, 0f),
                new Vector3(size.x * 0.55f, size.y * 0.28f, size.z * 0.7f), 0.375f, false, layer);
        }

        void MakeZone(string name, HitZone.Zone kind, PrimitiveType shape, Vector3 localPos, Vector3 scale, float multiplier, bool instant, int layer)
        {
            GameObject box = GameObject.CreatePrimitive(shape);
            box.name = name;
            if (layer >= 0)
                box.layer = layer;
            box.transform.SetParent(transform, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = Vector3.Max(scale, new Vector3(0.15f, 0.15f, 0.15f));
            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
            HitZone zone = box.AddComponent<HitZone>();
            zone.Configure(kind, multiplier, instant);
        }

        void DisableImportedMotion()
        {
            CharacterController[] bodies = GetComponentsInChildren<CharacterController>(true);
            for (int i = 0; i < bodies.Length; i++)
                bodies[i].enabled = false;

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null || behaviours[i] == this)
                    continue;

                string typeName = behaviours[i].GetType().FullName;
                if (typeName == "ithappy.Animals_FREE.MovePlayerInput" ||
                    typeName == "ithappy.Animals_FREE.CreatureMover" ||
                    typeName == "ithappy.Animals_FREE.PlayerCamera" ||
                    typeName == "ithappy.Animals_FREE.ThirdPersonCamera")
                    behaviours[i].enabled = false;
            }
        }

        bool Has(int hash)
        {
            if (animator == null)
                return false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash)
                    return true;
            }

            return false;
        }

        static RuntimeAnimatorController LoadBuiltController()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/DeerHunt/DeerPrototype.controller");
#else
            return null;
#endif
        }

        void BindAvatar()
        {
            if (animator == null || animator.avatar != null)
                return;

#if UNITY_EDITOR
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/ithappy/Animals_FREE/Meshes/Deer_001.fbx");
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar)
                {
                    animator.avatar = avatar;
                    animator.Rebind();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    return;
                }
            }
#endif
        }

        HitZone FindZone(HitZone.Zone kind)
        {
            HitZone[] zones = GetComponentsInChildren<HitZone>(true);
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].Kind == kind)
                    return zones[i];
            }

            return zones.Length > 0 ? zones[0] : null;
        }
    }
}

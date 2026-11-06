using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Village horse the player can mount. Uses the pack walk/run animations while ridden.
/// </summary>
[DisallowMultipleComponent]
public class RideableHorse : MonoBehaviour
{
    public static RideableHorse Mounted { get; private set; }

    public float mountRange = 3.6f;
    public float walkSpeed = 3.2f;
    public float runSpeed = 7.5f;
    public float turnSpeed = 120f;
    public float gallopAnimSpeed = 1f;

    const string HorsePrefabPath = "Assets/ithappy/Animals_FREE/Prefabs/Horse_001.prefab";

    static readonly int Vert = Animator.StringToHash("Vert");
    static readonly int State = Animator.StringToHash("State");

    AmbientCreature ambient;
    Animator animator;
    CharacterController controller;
    Transform saddle;
    PlayerMove rider;
    CharacterController riderController;
    float animVert;
    float animState;
    static float nextLivestockScan;

    public static bool IsRiding => Mounted != null;

    public static bool IsHorse(GameObject go)
    {
        if (go == null)
            return false;
        string n = go.name;
        if (n.IndexOf("horse", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        Animator anim = go.GetComponentInChildren<Animator>();
        return anim != null && anim.runtimeAnimatorController != null &&
               anim.runtimeAnimatorController.name.IndexOf("Horse", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static void TagHorseIfNeeded(GameObject go)
    {
        if (go == null || !IsHorse(go))
            return;
        if (go.GetComponent<RideableHorse>() != null)
            return;
        AmbientCreature creature = go.GetComponent<AmbientCreature>();
        if (creature == null)
        {
            AmbientCreature.DisableImportedControls(go);
            creature = go.AddComponent<AmbientCreature>();
            creature.home = go.transform.position;
            creature.radius = 4f;
            creature.speed = 0.55f;
            creature.restChance = 0.65f;
        }

        go.AddComponent<RideableHorse>();
    }

    public static float RideGait01 =>
        Mounted != null ? Mounted.animVert : 0f;

    public static bool BlocksAxeSwing()
    {
        if (Mounted != null)
            return true;

        PlayerMove move = Object.FindAnyObjectByType<PlayerMove>();
        if (move == null)
            return false;

        RideableHorse near = Nearest(move.transform.position, mountOnly: true);
        return near != null;
    }

    public static void TryHandleMountKey(PlayerMove player)
    {
        if (player == null || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (Inventory.IsOpen || BirdChallenge.Active)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Mounted != null)
        {
            if (Mounted.rider == player)
                Mounted.Dismount();
            return;
        }

        EnsureOnLivestock();

        RideableHorse horse = Nearest(player.transform.position, mountOnly: true);
        if (horse == null)
            return;

        horse.Mount(player);
    }

    static RideableHorse Nearest(Vector3 position, bool mountOnly)
    {
        RideableHorse best = null;
        float bestDist = float.MaxValue;
        RideableHorse[] horses = Object.FindObjectsByType<RideableHorse>();
        for (int i = 0; i < horses.Length; i++)
        {
            RideableHorse h = horses[i];
            if (h == null || !h.isActiveAndEnabled)
                continue;
            if (mountOnly && h.rider != null)
                continue;

            float dist = HorizontalDistance(position, h.MountPoint());
            float limit = h.mountRange + h.MountSizeBonus();
            if (dist > limit || dist >= bestDist)
                continue;

            best = h;
            bestDist = dist;
        }

        return best;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void Awake()
    {
        ambient = GetComponent<AmbientCreature>();
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        EnsureSaddle();
    }

    void LateUpdate()
    {
        SyncRiderTransform();
    }

    Vector3 MountPoint()
    {
        return saddle != null ? saddle.position : transform.position + Vector3.up * 1.2f;
    }

    float MountSizeBonus()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 0f;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.35f;
    }

    void SyncRiderTransform()
    {
        if (rider == null || saddle == null)
            return;

        rider.transform.position = saddle.position + saddle.up * -0.92f + saddle.forward * 0.06f;
        rider.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    void EnsureSaddle()
    {
        Transform existing = transform.Find("Saddle");
        if (existing != null)
        {
            saddle = existing;
            return;
        }

        saddle = new GameObject("Saddle").transform;
        saddle.SetParent(transform, false);
        float y = 1.15f;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            y = bounds.max.y - transform.position.y - 0.08f;
        }

        saddle.localPosition = new Vector3(0f, y, -0.05f);
        saddle.localRotation = Quaternion.identity;
    }

    void OnDisable()
    {
        if (Mounted == this)
            Dismount();
    }

    public void Mount(PlayerMove player)
    {
        if (player == null || rider != null)
            return;

        rider = player;
        riderController = player.GetComponent<CharacterController>();
        Mounted = this;

        if (ambient != null)
            ambient.PauseWander = true;

        if (riderController != null)
            riderController.enabled = false;

        SyncRiderTransform();

        if (CameraViewSwitcher.Instance != null)
            CameraViewSwitcher.Instance.PreferThirdPerson();

        if (Inventory.Instance != null)
        {
            string msg = gallopAnimSpeed > 1.1f
                ? "Hermit's steed — E to dismount. Shift gallops fast."
                : "On the horse. E to dismount. Shift to gallop.";
            Inventory.Instance.ShowNotice(msg);
        }
    }

    public void ConfigureHermitSteed()
    {
        walkSpeed = 5f;
        runSpeed = 16.5f;
        turnSpeed = 140f;
        gallopAnimSpeed = 1.45f;
        mountRange = 3.2f;
    }

    public void Dismount()
    {
        if (rider == null)
        {
            Mounted = null;
            return;
        }

        Transform body = rider.transform;
        Vector3 off = transform.position + transform.right * 1.6f;
        if (WorldGround.TryGetGround(off, out Vector3 ground, out _))
            off = ground + Vector3.up * 1.05f;
        else
            off.y = body.position.y;

        body.position = off;
        body.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (riderController != null)
            riderController.enabled = true;

        if (controller != null)
            controller.enabled = false;

        if (ambient != null)
            ambient.PauseWander = false;

        SetHorseAnim(0f, 0f, false);
        if (animator != null)
            animator.speed = 1f;
        rider = null;
        riderController = null;
        if (Mounted == this)
            Mounted = null;
    }

    public void Drive(in Vector3 localMove, bool running)
    {
        if (rider == null)
            return;

        float planar = new Vector2(localMove.x, localMove.z).magnitude;
        bool moving = planar > 0.05f;
        float speed = running && moving ? runSpeed : walkSpeed;
        float targetVert = moving ? (running ? 1f : 0.55f) : 0f;
        float targetState = running && moving ? 1f : 0f;

        if (moving)
        {
            Vector3 dir = localMove;
            dir.y = 0f;
            dir.Normalize();
            Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);
        }

        Vector3 p = transform.position + transform.forward * (moving ? speed * Time.deltaTime : 0f);
        if (WorldGround.TryGetGround(p, out Vector3 g, out _))
            p.y = g.y;
        transform.position = p;
        SyncRiderTransform();

        animVert = Mathf.MoveTowards(animVert, targetVert, Time.deltaTime * 4.5f);
        animState = Mathf.MoveTowards(animState, targetState, Time.deltaTime * 4.5f);
        SetHorseAnim(animVert, animState, running && moving);
        PlayerMove.SetGait(moving, running && moving, false, 1.15f);
    }

    void SetHorseAnim(float vert, float state, bool galloping)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return;
        animator.speed = galloping ? gallopAnimSpeed : 1f;
        animator.SetFloat(Vert, vert);
        animator.SetFloat(State, state);
    }

    void SetHorseAnim(float vert, float state)
    {
        SetHorseAnim(vert, state, false);
    }

    public static GameObject SpawnByPath(Vector3 position, float height, Transform parent = null)
    {
        GameObject prefab = LoadHorsePrefab();
        if (prefab == null)
            return null;

        GameObject go = Object.Instantiate(prefab, parent);
        go.name = "Horse";
        go.transform.SetPositionAndRotation(WorldGround.Snap(position), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y > 0.01f)
            {
                float scale = height / bounds.size.y;
                go.transform.localScale *= scale;
            }
        }

        AmbientCreature.DisableImportedControls(go);
        AmbientCreature creature = go.GetComponent<AmbientCreature>();
        if (creature == null)
            creature = go.AddComponent<AmbientCreature>();
        creature.home = go.transform.position;
        creature.radius = 2.5f;
        creature.speed = 0.35f;
        creature.restChance = 0.85f;

        if (go.GetComponent<RideableHorse>() == null)
            go.AddComponent<RideableHorse>();

        return go;
    }

    static GameObject LoadHorsePrefab()
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HorsePrefabPath);
        if (prefab != null)
            return prefab;
#endif
        return Resources.Load<GameObject>("Animals/Horse_001");
    }

    public static void EnsureOnLivestock(bool force = false)
    {
        if (!force && Time.time < nextLivestockScan)
            return;
        nextLivestockScan = Time.time + 4f;

        AmbientCreature[] creatures = Object.FindObjectsByType<AmbientCreature>();
        for (int i = 0; i < creatures.Length; i++)
            TagHorseIfNeeded(creatures[i].gameObject);
    }
}

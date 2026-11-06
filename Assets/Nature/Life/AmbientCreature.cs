using UnityEngine;

/// <summary>
/// Village livestock (chickens, horses) from the ithappy animal pack. Wanders slowly around
/// its home with the pack's walk animation. It is not huntable: it has no Animal component.
/// The pack's own player-input and camera scripts are switched off.
/// </summary>
public class AmbientCreature : MonoBehaviour
{
    public Vector3 home;
    public float radius = 5f;
    public float speed = 0.6f;
    [Range(0f, 1f)] public float restChance = 0.6f;

    static readonly int Vert = Animator.StringToHash("Vert");
    static readonly int State = Animator.StringToHash("State");
    const float CullDistance = 140f;

    Animator animator;
    Renderer[] renderers;
    Vector3 target;
    bool moving;
    bool visible = true;
    float waitUntil;
    float nextCull;

    /// <summary>When true (e.g. player is riding), grazing movement stops.</summary>
    public bool PauseWander { get; set; }

    void Start()
    {
        DisableImportedControls(gameObject);
        RideableHorse.TagHorseIfNeeded(gameObject);
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        waitUntil = Time.time + Random.Range(0f, 4f);
    }

    public static void DisableImportedControls(GameObject go)
    {
        foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour is AmbientCreature)
                continue;
            string ns = behaviour.GetType().Namespace;
            if (ns != null && ns.StartsWith("ithappy"))
                behaviour.enabled = false;
        }

        foreach (CharacterController controller in go.GetComponentsInChildren<CharacterController>(true))
            controller.enabled = false;
        foreach (Camera cam in go.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;
    }

    void Update()
    {
        if (Time.time >= nextCull)
        {
            nextCull = Time.time + 0.6f;
            bool near = Vector3.Distance(PlayerView.Position(), transform.position) < CullDistance;
            if (near != visible)
            {
                visible = near;
                foreach (Renderer r in renderers)
                {
                    if (r != null)
                        r.enabled = near;
                }

                if (animator != null)
                    animator.enabled = near;
            }
        }

        if (!visible)
            return;

        if (PauseWander || (RideableHorse.Mounted != null && RideableHorse.Mounted.gameObject == gameObject))
        {
            SetWalk(0f);
            return;
        }

        if (!moving)
        {
            SetWalk(0f);
            if (Time.time < waitUntil)
                return;

            Vector2 offset = Random.insideUnitCircle * radius;
            target = home + new Vector3(offset.x, 0f, offset.y);
            moving = true;
        }

        Vector3 p = transform.position;
        Vector3 to = target - p;
        to.y = 0f;
        if (to.magnitude < 0.25f)
        {
            moving = false;
            waitUntil = Time.time + (Random.value < restChance ? Random.Range(3f, 9f) : Random.Range(0.5f, 1.5f));
            return;
        }

        Vector3 dir = to.normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 180f * Time.deltaTime);
        p += transform.forward * speed * Time.deltaTime;
        if (WorldGround.TryGetGround(p, out Vector3 g, out _))
            p.y = g.y;
        transform.position = p;
        SetWalk(1f);
    }

    void SetWalk(float amount)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return;
        animator.SetFloat(Vert, amount);
        animator.SetFloat(State, 0f);
    }
}

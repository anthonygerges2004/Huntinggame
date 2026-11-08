using UnityEngine;

/// <summary>
/// Hurt gait posed on top of the deer walk. The rig only has idle, walk, and run,
/// so a limp is a shorter step on the hit side plus a lower head.
/// Applied in LateUpdate, after the animator, and only while that animator is writing.
/// </summary>
public class DeerWoundPose : MonoBehaviour
{
    public float Severity;
    public float Side = 1f;
    public bool TuckLeg;
    public bool DropHead;

    Transform hindThigh;
    Transform hindShin;
    Transform frontThigh;
    Transform frontShin;
    Transform skull;
    Transform spine;
    float hindSign = 1f;
    float hindShinSign = 1f;
    float frontSign = 1f;
    float frontShinSign = 1f;
    float skullSign = -1f;
    bool bonesReady;
    float gait;
    Vector3 lastPosition;
    Animator animator;

    public void Show(float severity, float side, bool tuckLeg, bool dropHead)
    {
        float signed = side >= 0f ? 1f : -1f;
        if (!bonesReady || signed != Side)
        {
            Side = signed;
            bonesReady = false;
            CacheBones();
        }

        Severity = Mathf.Clamp01(severity);
        TuckLeg = tuckLeg;
        DropHead = dropHead;
    }

    public void Clear()
    {
        Severity = 0f;
    }

    void CacheBones()
    {
        bonesReady = true;
        bool left = Side < 0f;
        hindThigh = Find(left ? "thigh.L" : "thigh.R");
        hindShin = Find(left ? "shin.L" : "shin.R");
        frontThigh = Find(left ? "front_thigh.L" : "front_thigh.R");
        frontShin = Find(left ? "front_shin.L" : "front_shin.R");
        skull = Find("scull");
        spine = Find("spine.003");
        hindSign = HingeSign(hindThigh);
        hindShinSign = HingeSign(hindShin);
        frontSign = HingeSign(frontThigh);
        frontShinSign = HingeSign(frontShin);
        skullSign = HingeSign(skull);
        animator = GetComponentInChildren<Animator>();
        lastPosition = transform.position;
    }

    static float HingeSign(Transform bone)
    {
        if (bone == null)
            return 1f;

        return bone.localRotation.x >= 0f ? 1f : -1f;
    }

    Transform Find(string boneName)
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].name == boneName)
                return bones[i];
        }

        return null;
    }

    void LateUpdate()
    {
        if (Severity < 0.02f)
            return;

        if (!bonesReady)
            CacheBones();

        if (animator != null && !animator.enabled)
            return;

        float moved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        bool stepping = moved > 0.0025f;
        gait += stepping ? moved * 3.1f : Time.deltaTime * 0.35f;

        // The bad step is short. The other three beats barely tuck, so the stride looks uneven.
        float cycle = Mathf.Repeat(gait, 1f);
        float hitch = !stepping
            ? 0.45f
            : (cycle < 0.28f ? Mathf.Sin(cycle / 0.28f * Mathf.PI) : 0.12f);

        float leg = (TuckLeg ? 34f : 14f) * Severity * hitch;
        Bend(hindThigh, hindSign, leg);
        Bend(hindShin, hindShinSign, leg * 0.45f);
        Bend(frontThigh, frontSign, leg * (TuckLeg ? 0.55f : 0.25f));
        Bend(frontShin, frontShinSign, leg * 0.2f);

        if (skull != null)
        {
            float nod = (DropHead ? 18f : 5f) * Severity * (0.75f + 0.25f * hitch);
            skull.localRotation = skull.localRotation * Quaternion.Euler(skullSign * nod, 0f, 0f);
        }

        if (spine != null)
        {
            float lean = 8f * Side * Severity * (0.6f + 0.4f * hitch);
            spine.localRotation = spine.localRotation * Quaternion.Euler(0f, 0f, lean);
        }
    }

    static void Bend(Transform bone, float sign, float degrees)
    {
        if (bone == null || degrees <= 0.01f)
            return;

        bone.localRotation = bone.localRotation * Quaternion.Euler(sign * degrees, 0f, 0f);
    }
}

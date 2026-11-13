using UnityEngine;

// Runs after the visible humanoid has received its retargeted pose, before blade collision.
[DefaultExecutionOrder(2050)]
public class HandAxeGrip : MonoBehaviour
{
    public static HandAxeGrip Instance { get; private set; }
    public Vector3 GripLocalPoint => primaryGripLocal;
    public Vector3 BladeWorldPoint => transform.TransformPoint(bladeLocal);

    Vector3 primaryGripLocal = new Vector3(0f, -0.14f, -0.028f);
    Vector3 supportGripLocal = new Vector3(0f, 0.10f, -0.012f);
    Vector3 bladeLocal = new Vector3(0f, 0.61f, 0.145f);
    Transform playerRoot;
    Transform fallbackHand;
    Transform boundHand, index, middle, little;
    Animator visibleAnimator;
    readonly ThirdPersonAxeSupport support = new ThirdPersonAxeSupport();

    public void Setup(Transform handBone, Transform player, float grip)
    {
        Instance = this;
        playerRoot = player;
        fallbackHand = handBone;
        ConfigureModel(grip);
        UpdateAttachment();
    }

    void LateUpdate() { UpdateAttachment(); }

    void UpdateAttachment()
    {
        Transform hand = fallbackHand;
        visibleAnimator = null;
        if (playerRoot != null)
        {
            SurvivalistBody visible = playerRoot.GetComponent<SurvivalistBody>();
            if (visible != null && visible.VisibleRightHand != null)
            {
                hand = visible.VisibleRightHand;
                visibleAnimator = visible.VisibleAnimator;
            }
        }
        if (hand == null) return;
        if (boundHand != hand)
        {
            boundHand = hand;
            Animator animator = visibleAnimator != null ? visibleAnimator : hand.GetComponentInParent<Animator>();
            if (animator != null && animator.isHuman)
            {
                index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            }
            else { index = middle = little = null; }
        }
        PlaceOnHand(hand, index, middle, little);
        if (visibleAnimator != null && playerRoot != null)
        {
            Vector3 pose = PlayerBodyAnimation.CurrentChopPose;
            float weight = Mathf.Clamp01(pose.x * (1f - pose.z));
            support.Apply(visibleAnimator, transform, playerRoot, supportGripLocal, weight);
        }
    }

    public static bool TryPalmFrame(Transform hand, Transform index, Transform middle, Transform little,
        out Vector3 center, out Quaternion rotation)
    {
        center = hand != null ? hand.position : Vector3.zero;
        rotation = hand != null ? hand.rotation : Quaternion.identity;
        if (hand == null || index == null || middle == null || little == null) return false;
        Vector3 along = middle.position - hand.position;
        Vector3 across = index.position - little.position;
        if (along.sqrMagnitude < 0.000001f || across.sqrMagnitude < 0.000001f) return false;
        across.Normalize();
        along = Vector3.ProjectOnPlane(along, across);
        if (along.sqrMagnitude < 0.000001f) return false;
        along.Normalize();
        Vector3 palmNormal = Vector3.Cross(along, across).normalized;
        Vector3 knuckles = (index.position + middle.position + little.position) / 3f;
        center = Vector3.Lerp(hand.position, knuckles, 0.85f) + palmNormal * 0.015f;
        // Mesh +Y follows the handle toward the thumb, +Z follows the cutting edge.
        // Finger positions define both axes; raw bone axes differ across avatars.
        rotation = Quaternion.LookRotation(along, across);
        return true;
    }

    public void PlaceOnHand(Transform hand, Transform index, Transform middle, Transform little)
    {
        if (hand == null) return;
        TryPalmFrame(hand, index, middle, little, out Vector3 center, out Quaternion rotation);
        if (transform.parent != hand) transform.SetParent(hand, false);
        transform.rotation = rotation;
        WeaponHold.SetWorldScale(transform, 1f);
        // Use a full 3D anchor: the wooden handle curves away from the mesh's Y axis.
        transform.position = center - transform.TransformVector(primaryGripLocal);
    }

    void ConfigureModel(float fallback)
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
            if (filter.sharedMesh != null && filter.sharedMesh.name.IndexOf("FellingAxe", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return; // Calibrated anchors for the supplied metre-scale felling axe.
        Bounds bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                else bounds.Encapsulate(point);
            }
        }
        primaryGripLocal = any ? new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, 0.14f), 0f)
            : new Vector3(0, Mathf.Max(0.05f, fallback), 0);
        supportGripLocal = primaryGripLocal + Vector3.up * 0.24f;
        bladeLocal = any ? new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, 0.9f), bounds.max.z)
            : primaryGripLocal + Vector3.up * 0.5f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}

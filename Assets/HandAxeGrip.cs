using UnityEngine;

[DefaultExecutionOrder(200)]
public class HandAxeGrip : MonoBehaviour
{
    public static HandAxeGrip Instance { get; private set; }

    Transform hand;
    Transform player;
    float gripLocalY = 0.08f;

    public void Setup(Transform handBone, Transform playerRoot, float grip)
    {
        Instance = this;
        hand = handBone;
        player = playerRoot;
        gripLocalY = MeasureGrip(grip);
        if (hand != null)
            transform.SetParent(hand, false);
        transform.localScale = Vector3.one;
    }

    float MeasureGrip(float fallback)
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        bool any = false;
        float minY = 0f;
        float maxY = 0f;
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null)
                continue;

            float lo = mesh.bounds.min.y * filters[i].transform.localScale.y;
            float hi = mesh.bounds.max.y * filters[i].transform.localScale.y;
            if (hi < lo)
            {
                float swap = lo;
                lo = hi;
                hi = swap;
            }

            if (!any)
            {
                minY = lo;
                maxY = hi;
                any = true;
            }
            else
            {
                minY = Mathf.Min(minY, lo);
                maxY = Mathf.Max(maxY, hi);
            }
        }

        if (!any)
            return Mathf.Max(0.05f, fallback);

        return Mathf.Lerp(minY, maxY, 0.22f);
    }

    void LateUpdate()
    {
        if (hand == null || player == null)
            return;

        Vector3 fingers = hand.up;
        if (fingers.sqrMagnitude < 0.0001f)
            fingers = -player.up;
        fingers.Normalize();

        Vector3 palm = hand.forward;
        if (palm.sqrMagnitude < 0.0001f)
            palm = -player.right;
        palm.Normalize();

        // Carry: head up beside the hip. Throw: head follows the arm as it comes forward.
        Vector3 carried = (player.up * 0.92f + player.right * 0.18f - player.forward * 0.08f).normalized;
        float forward = Mathf.InverseLerp(-0.15f, 0.55f, Vector3.Dot(fingers, player.forward));
        float raised = Mathf.InverseLerp(0.05f, 0.7f, Vector3.Dot(fingers, player.up));
        Vector3 headDir = Vector3.Slerp(carried, fingers, Mathf.Max(forward, raised * 0.45f));
        if (headDir.sqrMagnitude < 0.0001f)
            headDir = player.up;
        headDir.Normalize();

        Vector3 blade = Vector3.ProjectOnPlane(player.forward * 0.85f + player.right * 0.2f, headDir);
        if (blade.sqrMagnitude < 0.0001f)
            blade = Vector3.ProjectOnPlane(player.right, headDir);
        blade.Normalize();

        Quaternion rot = Quaternion.LookRotation(blade, headDir);
        Vector3 hold = hand.position
            + fingers * 0.07f
            + palm * 0.028f
            + player.right * 0.025f;

        transform.SetPositionAndRotation(hold - rot * new Vector3(0f, gripLocalY, 0f), rot);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

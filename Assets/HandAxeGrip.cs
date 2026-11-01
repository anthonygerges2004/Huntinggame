using UnityEngine;

[DefaultExecutionOrder(200)]
public class HandAxeGrip : MonoBehaviour
{
    public static HandAxeGrip Instance { get; private set; }

    float gripLocalY = 0.08f;

    static readonly Vector3 HoldPos = new Vector3(0f, 0.02f, 0f);
    static readonly Quaternion HoldRot = Quaternion.identity;

    public void Setup(Transform handBone, Transform playerRoot, float grip)
    {
        Instance = this;
        gripLocalY = MeasureGrip(grip);
        StickToSlot();
    }

    void LateUpdate()
    {
        StickToSlot();
        if (transform.parent == null)
            return;

        bool swinging = PlayerBodyAnimation.IsActing();
        transform.localPosition = HoldPos + new Vector3(0f, -gripLocalY, 0f);
        transform.localRotation = swinging ? Quaternion.Euler(22f, 0f, 0f) : HoldRot;
        WeaponHold.SetWorldScale(transform, 1f);
    }

    void StickToSlot()
    {
        if (WeaponHold.Instance == null)
            return;

        WeaponHold.Instance.EnsureSlot();
        if (transform.parent != WeaponHold.Instance.RightSlot)
            WeaponHold.Instance.Attach(transform, HoldPos + new Vector3(0f, -gripLocalY, 0f), HoldRot, 1f);
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

        return Mathf.Lerp(minY, maxY, 0.36f);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

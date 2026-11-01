using UnityEngine;

[DefaultExecutionOrder(120)]
public class WeaponHold : MonoBehaviour
{
    public static WeaponHold Instance { get; private set; }

    public Transform RightSlot { get; private set; }
    public Transform RightHand { get; private set; }
    public Transform LeftHand { get; private set; }
    public Transform RightArm { get; private set; }
    public Transform LeftArm { get; private set; }
    public Transform RightForeArm { get; private set; }
    public Transform LeftForeArm { get; private set; }
    public Transform RightShoulder { get; private set; }
    public Transform LeftShoulder { get; private set; }

    Quaternion rightArmBind;
    Quaternion rightForeBind;
    Quaternion rightHandBind;
    Quaternion leftArmBind;
    Quaternion leftForeBind;
    Quaternion leftHandBind;
    bool bindsReady;

    void Awake()
    {
        Instance = this;
        BindBones();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool BindBones()
    {
        if (RightHand == null)
            RightHand = FindBone("mixamorig:RightHand") ?? FindBone("RightHand");
        if (LeftHand == null)
            LeftHand = FindBone("mixamorig:LeftHand") ?? FindBone("LeftHand");
        if (RightArm == null)
            RightArm = FindBone("mixamorig:RightArm");
        if (LeftArm == null)
            LeftArm = FindBone("mixamorig:LeftArm");
        if (RightForeArm == null)
            RightForeArm = FindBone("mixamorig:RightForeArm");
        if (LeftForeArm == null)
            LeftForeArm = FindBone("mixamorig:LeftForeArm");
        if (RightShoulder == null)
            RightShoulder = FindBone("mixamorig:RightShoulder") ?? FindBone("mixamorig:Spine2");
        if (LeftShoulder == null)
            LeftShoulder = FindBone("mixamorig:LeftShoulder");

        if (!bindsReady && RightArm != null && LeftArm != null && RightForeArm != null && LeftForeArm != null && RightHand != null && LeftHand != null)
        {
            rightArmBind = RightArm.localRotation;
            rightForeBind = RightForeArm.localRotation;
            rightHandBind = RightHand.localRotation;
            leftArmBind = LeftArm.localRotation;
            leftForeBind = LeftForeArm.localRotation;
            leftHandBind = LeftHand.localRotation;
            bindsReady = true;
        }

        return RightShoulder != null && RightHand != null;
    }

    public void HandsOnWeapon(Vector3 rightGrip, Vector3 leftGrip, Vector3 barrel)
    {
        if (!bindsReady)
            BindBones();

        Vector3 rightPole = RightArm != null
            ? RightArm.position + transform.right * 0.32f - transform.forward * 0.18f
            : transform.position + transform.right * 0.4f;
        Vector3 leftPole = LeftArm != null
            ? LeftArm.position - transform.right * 0.32f - transform.forward * 0.18f
            : transform.position - transform.right * 0.4f;

        TwoBoneIk(RightArm, rightArmBind, RightForeArm, rightForeBind, RightHand, rightGrip, rightPole);
        TwoBoneIk(LeftArm, leftArmBind, LeftForeArm, leftForeBind, LeftHand, leftGrip, leftPole);

        if (barrel.sqrMagnitude > 0.0001f)
        {
            PointAlong(RightHand, rightHandBind, barrel.normalized);
            PointAlong(LeftHand, leftHandBind, barrel.normalized);
        }
    }

    public Transform ShoulderAnchor()
    {
        BindBones();
        return RightShoulder != null ? RightShoulder : transform;
    }

    public void AttachToShoulder(Transform weapon, float worldScale)
    {
        if (weapon == null)
            return;

        Transform anchor = ShoulderAnchor();
        weapon.SetParent(anchor, true);
        SetWorldScale(weapon, worldScale);
    }

    public bool EnsureSlot()
    {
        BindBones();
        if (RightHand == null)
            return false;

        if (RightSlot == null)
        {
            Transform existing = RightHand.Find("RightWeaponSlot");
            RightSlot = existing != null ? existing : new GameObject("RightWeaponSlot").transform;
        }

        if (RightSlot.parent != transform)
            RightSlot.SetParent(transform, false);

        RightSlot.position = RightHand.position + RightHand.up * 0.05f;
        RightSlot.rotation = Quaternion.LookRotation(
            RightHand.up.sqrMagnitude > 0.0001f ? RightHand.up : transform.forward,
            transform.up);
        RightSlot.localScale = Vector3.one;
        return true;
    }

    public void Attach(Transform weapon, Vector3 localPos, Quaternion localRot, float worldScale)
    {
        if (weapon == null || !EnsureSlot())
            return;

        weapon.SetParent(RightSlot, false);
        weapon.localPosition = localPos;
        weapon.localRotation = localRot;
        SetWorldScale(weapon, worldScale);
    }

    void TwoBoneIk(Transform upper, Quaternion upperBind, Transform mid, Quaternion midBind, Transform tip, Vector3 target, Vector3 pole)
    {
        if (upper == null || mid == null || tip == null)
            return;

        float upperLen = Vector3.Distance(upper.position, mid.position);
        float lowerLen = Vector3.Distance(mid.position, tip.position);
        if (upperLen < 0.001f || lowerLen < 0.001f)
            return;

        Vector3 root = upper.position;
        Vector3 toTarget = target - root;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.05f, upperLen + lowerLen - 0.015f);
        Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;

        Vector3 planeN = Vector3.Cross(dir, pole - root);
        if (planeN.sqrMagnitude < 0.0001f)
            planeN = Vector3.Cross(dir, transform.right);
        planeN.Normalize();

        float cosA = (upperLen * upperLen + dist * dist - lowerLen * lowerLen) / (2f * upperLen * dist);
        float bend = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
        PointAlong(upper, upperBind, Quaternion.AngleAxis(-bend, planeN) * dir);

        Vector3 lower = target - mid.position;
        if (lower.sqrMagnitude > 0.0001f)
            PointAlong(mid, midBind, lower.normalized);
    }

    static void PointAlong(Transform bone, Quaternion bind, Vector3 along)
    {
        if (bone == null || bone.parent == null || along.sqrMagnitude < 0.0001f)
            return;

        Quaternion bindWorld = bone.parent.rotation * bind;
        Quaternion hang = Quaternion.FromToRotation(bindWorld * Vector3.up, along.normalized);
        bone.localRotation = Quaternion.Inverse(bone.parent.rotation) * (hang * bindWorld);
    }

    public static void SetWorldScale(Transform target, float worldSize)
    {
        if (target == null)
            return;

        target.localScale = Vector3.one;
        Vector3 lossy = target.lossyScale;
        target.localScale = new Vector3(
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            worldSize / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
    }

    Transform FindBone(string boneName)
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].name == boneName)
                return bones[i];
        }

        return null;
    }
}

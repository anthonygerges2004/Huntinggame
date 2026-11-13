using UnityEngine;

/// <summary>Places the free hand on the axe after the visible humanoid pose is retargeted.</summary>
public sealed class ThirdPersonAxeSupport
{
    Animator rig;
    Transform upper;
    Transform forearm;
    Transform hand;
    Quaternion anatomicalFrame;
    Vector3 palmOffset;
    bool ready;

    readonly Transform[] fingers = new Transform[12];
    readonly Quaternion[] fingerRest = new Quaternion[12];
    readonly Vector3[] fingerFlex = new Vector3[12];

    static readonly HumanBodyBones[] FingerIds =
    {
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal
    };

    public void Apply(Animator animator, Transform axe, Transform player, Vector3 supportGripLocal, float weight)
    {
        weight = Mathf.Clamp01(weight);
        if (animator == null || axe == null || weight <= 0f) return;
        if (rig != animator || !ready) Cache(animator);
        if (!ready || upper == null || forearm == null || hand == null) return;

        Vector3 fingerDirection = -axe.forward;
        Vector3 handleDirection = axe.up;
        Quaternion gripRotation = Quaternion.LookRotation(fingerDirection, handleDirection)
            * Quaternion.Inverse(anatomicalFrame);
        Vector3 contact = axe.TransformPoint(supportGripLocal);
        Vector3 wrist = contact - gripRotation * Vector3.Scale(palmOffset, hand.lossyScale);

        // Capture the pose supplied by the humanoid retargeter before moving any bone.
        Quaternion upperIncoming = upper.rotation;
        Quaternion foreIncoming = forearm.rotation;
        Quaternion handIncoming = hand.rotation;
        Vector3 root = upper.position;
        float upperLength = Vector3.Distance(root, forearm.position);
        float lowerLength = Vector3.Distance(forearm.position, hand.position);
        if (upperLength < 0.001f || lowerLength < 0.001f) return;

        Vector3 toWrist = wrist - root;
        Vector3 direction = toWrist.sqrMagnitude > 0.000001f ? toWrist.normalized : axe.forward;
        float shortest = Mathf.Abs(upperLength - lowerLength) + 0.015f;
        float longest = Mathf.Max(shortest, upperLength + lowerLength - 0.02f);
        float distance = Mathf.Clamp(toWrist.magnitude, shortest, longest);
        Vector3 reachableWrist = root + direction * distance;

        Vector3 outward = player != null
            ? -player.right * 0.55f + player.forward * 0.35f - player.up * 0.35f
            : -axe.right * 0.55f - Vector3.up * 0.35f;
        Vector3 bendDirection = Vector3.ProjectOnPlane(outward, direction);
        if (bendDirection.sqrMagnitude < 0.000001f)
            bendDirection = Vector3.ProjectOnPlane(player != null ? -player.right : axe.up, direction);
        if (bendDirection.sqrMagnitude < 0.000001f)
            bendDirection = Vector3.Cross(direction, Vector3.forward);
        if (bendDirection.sqrMagnitude < 0.000001f) return;
        bendDirection.Normalize();

        float cosine = Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength)
            / (2f * upperLength * distance), -1f, 1f);
        Vector3 elbow = root + direction * (upperLength * cosine)
            + bendDirection * (upperLength * Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine)));

        // Imported skeletons have different local bone axes. Rotate their actual child
        // directions, rather than assuming the forearm extends along local +Y.
        Quaternion upperSolved = Quaternion.FromToRotation(forearm.position - root, elbow - root) * upperIncoming;
        upper.rotation = Quaternion.Slerp(upperIncoming, upperSolved, weight);
        Vector3 lowerDirection = reachableWrist - forearm.position;
        if (lowerDirection.sqrMagnitude > 0.000001f)
        {
            Quaternion foreSolved = Quaternion.FromToRotation(hand.position - forearm.position, lowerDirection) * forearm.rotation;
            forearm.rotation = Quaternion.Slerp(foreIncoming, foreSolved, weight);
        }
        hand.rotation = Quaternion.Slerp(handIncoming, gripRotation, weight);
        CloseFingers(weight);
    }

    void Cache(Animator animator)
    {
        rig = animator;
        ready = false;
        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) return;
        upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        Transform index = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
        Transform pinky = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
        if (upper == null || forearm == null || hand == null || middle == null || index == null || pinky == null) return;

        Vector3 finger = hand.InverseTransformPoint(middle.position);
        Vector3 across = hand.InverseTransformDirection(index.position - pinky.position);
        if (finger.sqrMagnitude < 0.000001f) return;
        finger.Normalize();
        across = Vector3.ProjectOnPlane(across, finger);
        if (across.sqrMagnitude < 0.000001f) return;
        across.Normalize();
        // The left hand is mirrored: the palm lies on the opposite side of this
        // index-to-pinky anatomical frame compared with the right hand.
        Vector3 palmNormal = -Vector3.Cross(finger, across).normalized;
        anatomicalFrame = Quaternion.LookRotation(finger, across);
        palmOffset = (hand.InverseTransformPoint(index.position) + hand.InverseTransformPoint(pinky.position)) * 0.5f
            - finger * 0.015f + palmNormal * 0.015f;

        Vector3 palmWorld = hand.TransformDirection(palmNormal);
        for (int i = 0; i < fingers.Length; i++)
        {
            Transform bone = animator.GetBoneTransform(FingerIds[i]);
            fingers[i] = bone;
            fingerFlex[i] = Vector3.zero;
            if (bone == null) continue;
            fingerRest[i] = bone.localRotation;
            Transform next = i % 3 < 2 ? animator.GetBoneTransform(FingerIds[i + 1]) : null;
            Vector3 along = next != null ? next.position - bone.position
                : bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position;
            Vector3 curlAxis = Vector3.Cross(along, palmWorld);
            if (curlAxis.sqrMagnitude > 0.000001f)
                fingerFlex[i] = bone.InverseTransformDirection(curlAxis.normalized);
        }
        ready = true;
    }

    void CloseFingers(float weight)
    {
        for (int i = 0; i < fingers.Length; i++)
        {
            Transform bone = fingers[i];
            if (bone == null || fingerFlex[i].sqrMagnitude < 0.000001f) continue;
            float angle = i % 3 == 0 ? 28f : i % 3 == 1 ? 35f : 24f;
            Quaternion closed = fingerRest[i] * Quaternion.AngleAxis(angle, fingerFlex[i]);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, closed, weight);
        }
    }
}

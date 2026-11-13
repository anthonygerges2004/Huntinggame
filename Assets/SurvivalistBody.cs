using UnityEngine;

/// <summary>
/// Dresses the player as the Survivalist character pack model. The original rig keeps running
/// every animation, IK hand hold and script that targets its bones; it is just no longer drawn.
/// After all of that has run, its humanoid pose is copied onto the Survivalist every frame, so
/// the new body walks, sneaks, aims and holds the gun exactly like the old one did.
/// </summary>
[DefaultExecutionOrder(2000)]
public class SurvivalistBody : MonoBehaviour
{
    public const string BodyName = "SurvivalistBody";

    Animator source;
    Transform body;
    public Transform VisibleRightHand => dstHand;
    public Animator VisibleAnimator { get; private set; }
    HumanPoseHandler from;
    HumanPoseHandler to;
    HumanPose pose;
    HumanPose own;
    Animator moves;       // the Survivalist's own animator (idle / walk / run / jump mocap)
    CharacterController controller;
    bool[] upper;         // muscles taken from the aiming rig: spine, head, arms, fingers
    Vector3 lastPos;
    float speed;
    float airTime;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int FreeFallId = Animator.StringToHash("FreeFall");
    static readonly int JumpId = Animator.StringToHash("Jump");

    public static bool Apply(Transform player)
    {
        if (player == null || player.GetComponent<SurvivalistBody>() != null)
            return player != null;

        GameObject prefab = Resources.Load<GameObject>("Characters/Survivalist");
        if (prefab == null)
            return false;

        Animator src = null;
        foreach (Animator a in player.GetComponentsInChildren<Animator>(true))
        {
            if (a.avatar != null && a.avatar.isValid && a.avatar.isHuman)
            {
                src = a;
                break;
            }
        }
        if (src == null)
            return false;

        GameObject go = Instantiate(prefab, src.transform == player ? player : src.transform.parent, false);
        go.name = BodyName;
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);

        Animator dst = go.GetComponentInChildren<Animator>(true);
        if (dst == null || dst.avatar == null || !dst.avatar.isValid || !dst.avatar.isHuman)
        {
            Destroy(go);
            return false;
        }

        // The new body runs its own mocap locomotion; only the upper body follows the aiming rig.
        RuntimeAnimatorController moveSet = Resources.Load<RuntimeAnimatorController>("Characters/SurvivalistMoves");
        dst.runtimeAnimatorController = moveSet;
        dst.applyRootMotion = false;
        dst.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Transform[] dstFeet = Bones(dst);
        Transform dstHand = dst.GetBoneTransform(HumanBodyBones.RightHand);
        dst.enabled = moveSet != null;
        if (dst.GetComponent<AnimationEventSink>() == null)
            dst.gameObject.AddComponent<AnimationEventSink>();

        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            // The pack ships first-person arms in the same prefab; the game draws its own.
            bool fpsArms = r.name.IndexOf("FPS", System.StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name.IndexOf("FPS", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    fpsArms = true;
            if (fpsArms)
                r.gameObject.SetActive(false);
            if (r is SkinnedMeshRenderer sk)
                sk.updateWhenOffscreen = true;
            // The pack's outfit reads almost black in the game's light: lift it, and make sure the
            // albedo texture is bound under URP's name.
            Material[] mats = r.materials;
            foreach (Material m in mats)
            {
                if (m == null)
                    continue;
                if (m.HasProperty("_MainTex") && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null)
                    m.SetTexture("_BaseMap", m.GetTexture("_MainTex"));
                m.SetColor("_BaseColor", new Color(1.55f, 1.5f, 1.45f, 1f));
                m.SetFloat("_Smoothness", 0.25f);
                m.SetFloat("_Metallic", 0f);
            }
            r.materials = mats;
        }

        // The old body stays animated but is never drawn.
        src.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (Renderer r in src.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.transform.IsChildOf(go.transform) && !IsGun(r.transform))
                r.forceRenderingOff = true;
        }

        SurvivalistBody link = player.gameObject.AddComponent<SurvivalistBody>();
        link.source = src;
        link.body = dst.transform;
        link.from = new HumanPoseHandler(src.avatar, src.transform);
        link.to = new HumanPoseHandler(dst.avatar, dst.transform);
        link.pose = new HumanPose();
        link.srcFeet = Bones(src);
        link.dstFeet = dstFeet;
        link.srcHand = src.GetBoneTransform(HumanBodyBones.RightHand);
        link.dstHand = dstHand;
        link.VisibleAnimator = dst;
        link.moves = moveSet != null ? dst : null;
        link.controller = player.GetComponent<CharacterController>();
        link.own = new HumanPose();
        link.lastPos = player.position;
        link.upper = new bool[HumanTrait.MuscleCount];
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            string n = HumanTrait.MuscleName[i];
            link.upper[i] = !(n.Contains("Leg") || n.Contains("Foot") || n.Contains("Toes"));
        }
        return true;
    }

    Vector3 Feet()
    {
        if (controller != null)
            return transform.TransformPoint(controller.center) - transform.up * (controller.height * 0.5f * transform.lossyScale.y);
        return transform.position;
    }

    void Update()
    {
        if (moves == null || body == null)
            return;

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        delta.y = 0f;
        float target = Mathf.Min(delta.magnitude / dt, 12f);
        speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-10f * dt));

        bool grounded = controller == null || controller.isGrounded;
        airTime = grounded ? 0f : airTime + dt;
        moves.SetFloat(SpeedId, speed < 0.15f ? 0f : speed <= 5f ? speed * 0.4f : Mathf.Lerp(2f, 6f, Mathf.InverseLerp(5f, 8.5f, speed)));
        moves.SetFloat(MotionSpeedId, speed < 0.15f ? 1f : Mathf.Clamp(speed / Mathf.Max(0.1f, moves.GetFloat(SpeedId)), 0.5f, 2.5f));
        moves.SetBool(GroundedId, airTime < 0.2f);
        moves.SetBool(FreeFallId, airTime > 0.35f);
        moves.SetBool(JumpId, false);
    }

    void LateUpdate()
    {
        if (source == null || body == null || from == null)
            return;

        from.GetHumanPose(ref pose);
        if (moves != null && !PlayerMove.IsCrouching && !RideableHorse.IsRiding)
        {
            // Legs and hips from the Survivalist's own animation (so it stands on its own feet);
            // spine, head and arms from the aiming rig (so it holds and points the gun).
            body.SetPositionAndRotation(Feet(), transform.rotation);
            to.GetHumanPose(ref own);
            for (int i = 0; i < own.muscles.Length && i < pose.muscles.Length; i++)
                if (upper[i])
                    own.muscles[i] = pose.muscles[i];
            to.SetHumanPose(ref own);
            body.SetPositionAndRotation(Feet(), transform.rotation);
            StandOnGround();
            FollowHands();
            HideOldRig();
            return;
        }

        // Crouching (no crouch clip in the pack): copy the whole pose from the old rig.
        body.SetPositionAndRotation(Feet(), transform.rotation);
        to.SetHumanPose(ref pose);
        StandOnGround();
        FollowHands();

        // Leg lengths differ between the two rigs, so the copied hips left the new body sunk into
        // the ground. Stand its lowest foot exactly where the old rig's lowest foot is.

        HideOldRig();
    }

    // The pack model's pivot is not exactly at its soles: put the lowest foot on the capsule bottom.
    void StandOnGround()
    {
        float low = Lowest(dstFeet);
        if (float.IsPositiveInfinity(low))
            return;
        body.position += Vector3.up * (Feet().y + 0.04f - low);
    }

    // The held gun is placed by the old rig's hand; carry it over to the new body's hand.
    void FollowHands()
    {
        if (dstHand == null || srcHand == null)
            return;
        // The axe attaches directly to the visible palm after this retarget pass.
        Transform gun = PlayerShotgun.Instance != null ? PlayerShotgun.Instance.HandGun : null;
        if (gun == null || !gun.gameObject.activeInHierarchy)
            return;
        gun.position += dstHand.position - srcHand.position;
    }

    void HideOldRig()
    {
        // Things later added under the old rig (hats, held props) must stay hidden too.
        if (Time.frameCount % 30 == 0)
        {
            foreach (SkinnedMeshRenderer r in source.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!r.forceRenderingOff && !r.transform.IsChildOf(body) && !IsGun(r.transform))
                    r.forceRenderingOff = true;
            }
        }
    }

    Transform srcHand, dstHand;
    Transform[] srcFeet = System.Array.Empty<Transform>();
    Transform[] dstFeet = System.Array.Empty<Transform>();

    static float Lowest(Transform[] feet)
    {
        float low = float.PositiveInfinity;
        for (int i = 0; i < feet.Length; i++)
            if (feet[i] != null && feet[i].position.y < low)
                low = feet[i].position.y;
        return low;
    }

    static Transform[] Bones(Animator a)
    {
        var list = new System.Collections.Generic.List<Transform>(4);
        HumanBodyBones[] ids = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        foreach (HumanBodyBones id in ids)
        {
            Transform b = a.GetBoneTransform(id);
            if (b != null)
                list.Add(b);
        }
        return list.ToArray();
    }

    static bool IsGun(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            string n = t.name;
            if (n == "Shotgun" || n == "Rifle" || n == "Sniper" || n == "Pistol" || n == "SMG"
                || n == "HandAxe" || n == "FallingAxe" || n == "RightWeaponSlot")
                return true;
        }
        return false;
    }

    void OnDestroy()
    {
        from?.Dispose();
        to?.Dispose();
    }
}



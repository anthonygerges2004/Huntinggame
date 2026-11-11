using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerBodyAnimation : MonoBehaviour
{
    public const float ThrowCock = 0.42f;
    public const float ThrowHold = 0.07f;
    public const float ThrowSnap = 0.16f;
    public const float ThrowRecover = 0.32f;
    public const float ChopWindup = 0.42f;
    public const float ChopStrike = 0.18f;
    public const float ChopContact = 0.10f;
    public const float ChopPull = 0.18f;
    public const float ChopRecover = 0.34f;

    public static PlayerBodyAnimation Instance { get; private set; }

    CharacterController controller;
    float cycle;
    float speedBlend;
    float airBlend;
    float fatiguePoseWeight;
    float throwTime = -1f;
    float chopTime = -1f;
    float lightTime = -1f;

    Transform hips;
    Transform spine;
    Transform spine1;
    Transform spine2;
    Transform neck;
    Transform leftShoulder;
    Transform rightShoulder;
    Transform leftUpLeg;
    Transform rightUpLeg;
    Transform leftLeg;
    Transform rightLeg;
    Transform leftFoot;
    Transform rightFoot;
    Transform leftArm;
    Transform rightArm;
    Transform leftForeArm;
    Transform rightForeArm;
    Transform leftHand;
    Transform rightHand;
    Transform[] leftFingers;
    Transform[] rightFingers;
    Quaternion[] leftFingerBind;
    Quaternion[] rightFingerBind;
    Vector3[] restFingerEuler;
    Vector3[] gripFingerEuler;

    Quaternion hipsRot;
    Quaternion spineRot;
    Quaternion spine1Rot;
    Quaternion spine2Rot;
    Quaternion neckRot;
    Quaternion leftShoulderRot;
    Quaternion rightShoulderRot;
    Quaternion leftUpLegRot;
    Quaternion rightUpLegRot;
    Quaternion leftLegRot;
    Quaternion rightLegRot;
    Quaternion leftFootRot;
    Quaternion rightFootRot;
    Quaternion leftArmRot;
    Quaternion rightArmRot;
    Quaternion leftForeArmRot;
    Quaternion rightForeArmRot;
    Quaternion leftHandRot;
    Quaternion rightHandRot;
    Vector3 hipsPos;
    Animator[] animators;
    bool ready;

    void Awake()
    {
        Instance = this;
        animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].enabled = false;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        CachePose();
        ready = true;
    }

    public static void PlayThrow()
    {
        if (Instance == null)
            return;
        Instance.throwTime = 0f;
        Instance.chopTime = -1f;
    }

    public static void PlayChop()
    {
        if (Instance == null)
            return;
        Instance.chopTime = 0f;
        Instance.throwTime = -1f;
    }

    public static void PlayLightFire()
    {
        if (Instance == null)
            return;
        Instance.lightTime = 0f;
        Instance.chopTime = -1f;
        Instance.throwTime = -1f;
    }

    public static bool IsActing()
    {
        return Instance != null && (Instance.chopTime >= 0f || Instance.throwTime >= 0f || Instance.lightTime >= 0f);
    }

    void LateUpdate()
    {
        if (!ready)
            return;

        if (animators != null)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i].enabled)
                    animators[i].enabled = false;
            }
        }

        float planar = 0f;
        bool grounded = true;
        if (controller != null)
        {
            Vector3 velocity = controller.velocity;
            planar = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            grounded = controller.isGrounded;
        }

        bool crouch = PlayerMove.IsCrouching;
        float walkSpeed = 5f;
        float runSpeed = 8.5f;
        float speedParam = 0f;
        if (grounded && planar > 0.12f)
        {
            if (planar <= walkSpeed)
                speedParam = Mathf.InverseLerp(0.12f, walkSpeed, planar);
            else
                speedParam = 1f + Mathf.InverseLerp(walkSpeed, runSpeed, planar);
        }

        speedBlend = Mathf.MoveTowards(speedBlend, speedParam, Time.deltaTime * 5f);
        airBlend = Mathf.MoveTowards(airBlend, grounded ? 0f : 1f, Time.deltaTime * 6f);

        bool riding = RideableHorse.IsRiding;
        float rideGait = RideableHorse.RideGait01;

        float idleW;
        float walkW;
        float runW;
        if (speedBlend <= 1f)
        {
            idleW = 1f - speedBlend;
            walkW = speedBlend;
            runW = 0f;
        }
        else
        {
            idleW = 0f;
            walkW = 2f - speedBlend;
            runW = speedBlend - 1f;
        }

        var survival = HuntingGame.Survival.SurvivalStats.Current;
        float poseTarget = grounded && !riding && !IsActing() ? 1f : 0f;
        fatiguePoseWeight = Mathf.MoveTowards(fatiguePoseWeight, poseTarget, Time.deltaTime * 6f);
        float fatiguePose = survival != null ? survival.Fatigue01 * fatiguePoseWeight : 0f;
        float shiverPose = survival != null ? survival.Shiver01 * fatiguePoseWeight : 0f;
        // Velocity already controls cadence. Shorten the step and compensate its frequency
        // together so fatigue does not simply slow the feet while the body slides forward.
        float stepScale = Mathf.Lerp(1f, 0.86f, fatiguePose);
        float stride = (walkW * 8.4f + runW * 12.6f) / stepScale;
        if (crouch)
            stride *= 0.78f;
        if (walkW + runW > 0.01f)
            cycle += Time.deltaTime * stride;

        float l = Mathf.Sin(cycle);
        float r = Mathf.Sin(cycle + Mathf.PI);
        float passL = Mathf.Max(0f, l);
        float passR = Mathf.Max(0f, r);
        float shift = Mathf.Sin(Time.time * 1.15f);
        float breathe = Mathf.Sin(Time.time * 1.7f);
        float tiredBreath = Mathf.Sin(Time.time * 2.5f) * fatiguePose;

        float leftThigh = (10f + shift * 4f) * idleW + l * 30f * walkW + l * 48f * runW;
        float rightThigh = (8f - shift * 4f) * idleW + r * 30f * walkW + r * 48f * runW;
        float leftKnee = -18f * idleW - passL * 48f * walkW - passL * 78f * runW;
        float rightKnee = -14f * idleW - passR * 48f * walkW - passR * 78f * runW;

        float hangL = 70f * idleW + 66f * walkW + 56f * runW;
        float hangR = -(70f * idleW + 66f * walkW + 56f * runW);
        float leftArmPitch = 16f * idleW - l * 28f * walkW - l * 48f * runW;
        float rightArmPitch = 16f * idleW - r * 28f * walkW - r * 48f * runW;
        float leftElbow = 34f * idleW + (30f + Mathf.Max(0f, -l) * 16f) * walkW + (24f + Mathf.Max(0f, -l) * 22f) * runW;
        float rightElbow = 34f * idleW + (30f + Mathf.Max(0f, -r) * 16f) * walkW + (24f + Mathf.Max(0f, -r) * 22f) * runW;
        float hipYaw = shift * 3f * idleW + l * 8f * walkW + l * 12f * runW;
        float hipRoll = shift * 4f * idleW;
        float spineTwist = -shift * 2f * idleW - l * 7f * walkW - l * 11f * runW;
        float spinePitch = breathe * 2.2f * idleW + 6f * walkW + 15f * runW;
        float hipBob = breathe * 0.012f * idleW + Mathf.Abs(l) * 0.035f * walkW + Mathf.Abs(l) * 0.07f * runW;

        leftThigh *= stepScale;
        rightThigh *= stepScale;
        leftKnee *= stepScale;
        rightKnee *= stepScale;
        leftArmPitch *= Mathf.Lerp(1f, 0.8f, fatiguePose);
        rightArmPitch *= Mathf.Lerp(1f, 0.8f, fatiguePose);
        spinePitch += fatiguePose * (crouch ? 3f : 7f) + tiredBreath * 1.5f;
        hipBob += tiredBreath * 0.005f * idleW;
        // Small torso tremor, never camera shake. Weapon and action poses remain authoritative.
        spineTwist += Mathf.Sin(Time.time * 24f) * shiverPose * (PlayerShotgun.IsAiming ? 0.08f : 0.45f);

        if (crouch)
        {
            leftThigh += 18f;
            rightThigh += 18f;
            leftKnee -= 38f;
            rightKnee -= 38f;
            spinePitch += 12f;
            hipBob -= 0.16f;
            hangL -= 6f;
            hangR += 6f;
        }

        if (riding)
        {
            float bounce = Mathf.Sin(Time.time * (4f + rideGait * 8f)) * (0.012f + rideGait * 0.022f);
            idleW = 0f;
            walkW = 0f;
            runW = 0f;
            leftThigh = 62f;
            rightThigh = 62f;
            leftKnee = -78f;
            rightKnee = -78f;
            leftArmPitch = -42f;
            rightArmPitch = -42f;
            leftElbow = 52f;
            rightElbow = 52f;
            hangL = 34f;
            hangR = -34f;
            spinePitch = 10f + rideGait * 4f;
            spineTwist = 0f;
            hipYaw = 0f;
            hipRoll = 0f;
            hipBob = bounce;
        }

        if (airBlend > 0.01f && !riding)
        {
            leftThigh = Mathf.Lerp(leftThigh, 22f, airBlend);
            rightThigh = Mathf.Lerp(rightThigh, 22f, airBlend);
            leftKnee = Mathf.Lerp(leftKnee, -58f, airBlend);
            rightKnee = Mathf.Lerp(rightKnee, -58f, airBlend);
            leftArmPitch = Mathf.Lerp(leftArmPitch, -18f, airBlend);
            rightArmPitch = Mathf.Lerp(rightArmPitch, -18f, airBlend);
            hangL = Mathf.Lerp(hangL, 55f, airBlend);
            hangR = Mathf.Lerp(hangR, -55f, airBlend);
            spinePitch = Mathf.Lerp(spinePitch, -8f, airBlend);
        }

        bool holdingShotgun = !riding && PlayerShotgun.IsEquipped;
        bool holdingAxe = !riding && AxeSwing.HasAxe && !holdingShotgun && Inventory.AxeInHand;
        float shotgunMount = holdingShotgun ? PlayerShotgun.Mount : 0f;
        float shotgunRecoil = holdingShotgun ? PlayerShotgun.RecoilKick : 0f;
        float shotgunPump = holdingShotgun ? PlayerShotgun.PumpPull : 0f;
        if (holdingShotgun)
        {
            spinePitch += 10f * (1f - shotgunMount) - 22f * shotgunRecoil;
            spineTwist += 7f * (1f - shotgunMount);
            hipRoll += shotgunRecoil * 5f;
            rightArmPitch -= 18f * shotgunRecoil;
            leftArmPitch -= 8f * shotgunRecoil;
            float low = 1f - PlayerShotgun.GunPresent;
            float caught = Mathf.SmoothStep(0.38f, 0.92f, PlayerShotgun.GunPresent);
            rightArmPitch = Mathf.Lerp(rightArmPitch, 46f, low);
            hangR = Mathf.Lerp(hangR, -4f, low);
            leftArmPitch = Mathf.Lerp(34f, leftArmPitch, caught);
            leftElbow = Mathf.Lerp(16f, leftElbow, caught);
        }
        if (holdingAxe)
        {
            hangR = -48f;
            rightArmPitch = -18f - r * 3f * walkW;
            rightElbow = 78f;
            if (crouch)
            {
                hangR = -40f;
                rightArmPitch = -12f;
                rightElbow = 70f;
            }

            float drawn = PlayerShotgun.AxePresent;
            hangR = Mathf.Lerp(6f, hangR, drawn);
            rightArmPitch = Mathf.Lerp(32f, rightArmPitch, drawn);
            rightElbow = Mathf.Lerp(14f, rightElbow, drawn);
        }

        float actionPitch = 0f;
        float actionElbow = 0f;
        float actionLean = 0f;
        float actionYaw = 0f;
        float actionHang = hangR;
        float actionHangL = hangL;
        float actionPitchL = leftArmPitch;
        float actionElbowL = leftElbow;
        float actionHipYaw = 0f;
        float actionSpineYaw = 0f;
        float actionLeftThigh = 0f;
        float actionRightThigh = 0f;
        bool acting = false;

        if (chopTime >= 0f)
        {
            acting = true;
            chopTime += Time.deltaTime;
            float recover = ChopContact + ChopPull + ChopRecover;
            float strikeStart = ChopWindup;
            float recoverStart = ChopWindup + ChopStrike;
            float total = recoverStart + recover;
            if (chopTime < strikeStart)
            {
                float k = Ease(chopTime / strikeStart);
                actionHang = Mathf.Lerp(hangR, -18f, k);
                actionPitch = Mathf.Lerp(0f, -40f, k);
                actionElbow = Mathf.Lerp(12f, 22f, k);
                actionLean = Mathf.Lerp(0f, -10f, k);
                actionYaw = Mathf.Lerp(0f, 8f, k);
            }
            else if (chopTime < recoverStart)
            {
                float k = Ease((chopTime - strikeStart) / ChopStrike);
                actionHang = Mathf.Lerp(-18f, -70f, k);
                actionPitch = Mathf.Lerp(-40f, 38f, k);
                actionElbow = Mathf.Lerp(22f, 8f, k);
                actionLean = Mathf.Lerp(-10f, 10f, k);
                actionYaw = Mathf.Lerp(8f, -6f, k);
            }
            else if (chopTime < total)
            {
                float k = Ease((chopTime - recoverStart) / recover);
                actionHang = Mathf.Lerp(-70f, hangR, k);
                actionPitch = Mathf.Lerp(38f, 0f, k);
                actionElbow = Mathf.Lerp(8f, holdingAxe ? 12f : 0f, k);
                actionLean = Mathf.Lerp(10f, 0f, k);
                actionYaw = Mathf.Lerp(-6f, 0f, k);
            }
            else
            {
                chopTime = -1f;
                acting = false;
            }
        }
        else if (throwTime >= 0f)
        {
            acting = true;
            throwTime += Time.deltaTime;
            float holdStart = ThrowCock;
            float snapStart = ThrowCock + ThrowHold;
            float recoverStart = snapStart + ThrowSnap;
            float total = recoverStart + ThrowRecover;
            if (throwTime < holdStart)
            {
                float k = Ease(throwTime / holdStart);
                actionHang = Mathf.Lerp(hangR, -22f, k);
                actionPitch = Mathf.Lerp(0f, -88f, k);
                actionElbow = Mathf.Lerp(holdingAxe ? 38f : 20f, 58f, k);
                actionYaw = Mathf.Lerp(0f, 16f, k);
                actionLean = Mathf.Lerp(0f, -20f, k);
                actionHangL = Mathf.Lerp(hangL, 48f, k);
                actionPitchL = Mathf.Lerp(leftArmPitch, 32f, k);
                actionElbowL = Mathf.Lerp(leftElbow, 18f, k);
                actionHipYaw = Mathf.Lerp(0f, 14f, k);
                actionSpineYaw = Mathf.Lerp(0f, 20f, k);
                actionRightThigh = Mathf.Lerp(0f, -10f, k);
                actionLeftThigh = Mathf.Lerp(0f, 12f, k);
            }
            else if (throwTime < snapStart)
            {
                actionHang = -22f;
                actionPitch = -88f;
                actionElbow = 58f;
                actionYaw = 16f;
                actionLean = -20f;
                actionHangL = 48f;
                actionPitchL = 32f;
                actionElbowL = 18f;
                actionHipYaw = 14f;
                actionSpineYaw = 20f;
                actionRightThigh = -10f;
                actionLeftThigh = 12f;
            }
            else if (throwTime < recoverStart)
            {
                float k = Ease((throwTime - snapStart) / ThrowSnap);
                actionHang = Mathf.Lerp(-22f, -32f, k);
                actionPitch = Mathf.Lerp(-88f, 58f, k);
                actionElbow = Mathf.Lerp(58f, 10f, k);
                actionYaw = Mathf.Lerp(16f, -10f, k);
                actionLean = Mathf.Lerp(-20f, 16f, k);
                actionHangL = Mathf.Lerp(48f, 62f, k);
                actionPitchL = Mathf.Lerp(32f, -14f, k);
                actionElbowL = Mathf.Lerp(18f, 28f, k);
                actionHipYaw = Mathf.Lerp(14f, -12f, k);
                actionSpineYaw = Mathf.Lerp(20f, -18f, k);
                actionRightThigh = Mathf.Lerp(-10f, 8f, k);
                actionLeftThigh = Mathf.Lerp(12f, -6f, k);
            }
            else if (throwTime < total)
            {
                float k = Ease((throwTime - recoverStart) / ThrowRecover);
                actionHang = Mathf.Lerp(-32f, hangR, k);
                actionPitch = Mathf.Lerp(58f, 0f, k);
                actionElbow = Mathf.Lerp(10f, holdingAxe ? 38f : 0f, k);
                actionYaw = Mathf.Lerp(-10f, 0f, k);
                actionLean = Mathf.Lerp(16f, 0f, k);
                actionHangL = Mathf.Lerp(62f, hangL, k);
                actionPitchL = Mathf.Lerp(-14f, leftArmPitch, k);
                actionElbowL = Mathf.Lerp(28f, leftElbow, k);
                actionHipYaw = Mathf.Lerp(-12f, 0f, k);
                actionSpineYaw = Mathf.Lerp(-18f, 0f, k);
                actionRightThigh = Mathf.Lerp(8f, 0f, k);
                actionLeftThigh = Mathf.Lerp(-6f, 0f, k);
            }
            else
            {
                throwTime = -1f;
                acting = false;
            }
        }
        else if (lightTime >= 0f)
        {
            acting = true;
            lightTime += Time.deltaTime;
            const float place = 2.08f;
            const float flick = 0.75f;
            const float recover = 0.4f;
            float total = place + flick + recover;
            float crouchIn = Mathf.Clamp01(lightTime / 0.28f);
            float crouchOut = lightTime < place + flick ? 1f : 1f - Mathf.Clamp01((lightTime - place - flick) / recover);
            float kneel = Mathf.Min(crouchIn, crouchOut);
            actionLeftThigh = 36f * kneel;
            actionRightThigh = 36f * kneel;
            leftKnee = Mathf.Lerp(leftKnee, -72f, kneel);
            rightKnee = Mathf.Lerp(rightKnee, -72f, kneel);
            actionLean = 20f * kneel;

            if (lightTime < place)
            {
                float bob = Mathf.Sin(lightTime / 0.45f * Mathf.PI);
                actionHangL = Mathf.Lerp(hangL, 18f, kneel);
                actionPitchL = Mathf.Lerp(leftArmPitch, 58f, kneel) + bob * 10f;
                actionElbowL = Mathf.Lerp(leftElbow, 24f, kneel);
                actionHang = Mathf.Lerp(hangR, -20f, kneel);
                actionPitch = Mathf.Lerp(0f, 12f, kneel);
                actionElbow = Mathf.Lerp(rightElbow, 40f, kneel);
            }
            else if (lightTime < place + flick)
            {
                float k = Mathf.Clamp01((lightTime - place) / flick);
                float strike = k < 0.72f ? k / 0.72f : 1f - (k - 0.72f) / 0.28f;
                actionHang = -16f;
                actionPitch = Mathf.Lerp(18f, 64f, k);
                actionElbow = Mathf.Lerp(48f, 18f, strike);
                actionYaw = -8f * strike;
                actionHangL = 24f;
                actionPitchL = 40f;
                actionElbowL = 30f;
            }
            else if (lightTime < total)
            {
                float k = (lightTime - place - flick) / recover;
                actionHang = Mathf.Lerp(-16f, hangR, k);
                actionPitch = Mathf.Lerp(64f, 0f, k);
                actionElbow = Mathf.Lerp(18f, rightElbow, k);
                actionHangL = Mathf.Lerp(24f, hangL, k);
                actionPitchL = Mathf.Lerp(40f, leftArmPitch, k);
                actionElbowL = Mathf.Lerp(30f, leftElbow, k);
            }
            else
            {
                lightTime = -1f;
                acting = false;
            }
        }

        if (acting)
        {
            hangR = actionHang;
            hangL = actionHangL;
            rightArmPitch = actionPitch;
            leftArmPitch = actionPitchL;
            rightElbow = actionElbow;
            leftElbow = actionElbowL;
            spinePitch += actionLean;
            hipYaw += actionHipYaw;
            spineTwist += actionSpineYaw;
            leftThigh += actionLeftThigh;
            rightThigh += actionRightThigh;
        }

        Pose(hips, hipsRot, breathe * 1.2f * idleW, hipYaw, hipRoll);
        if (hips != null)
            hips.localPosition = hipsPos + Vector3.up * hipBob;

        Pose(spine, spineRot, spinePitch + actionLean, spineTwist + (acting ? actionYaw * 0.4f : 0f), 0f);
        Pose(spine1, spine1Rot, spinePitch * 0.45f + actionLean * 0.3f, spineTwist * 0.5f, 0f);
        Pose(spine2, spine2Rot, 6f + spinePitch * 0.2f, spineTwist * 0.25f, 0f);
        Pose(neck, neckRot, 8f, 0f, 0f);
        Pose(leftUpLeg, leftUpLegRot, leftThigh, 0f, 0f);
        Pose(rightUpLeg, rightUpLegRot, rightThigh, 0f, 0f);
        Pose(leftLeg, leftLegRot, leftKnee, 0f, 0f);
        Pose(rightLeg, rightLegRot, rightKnee, 0f, 0f);
        Pose(leftFoot, leftFootRot, -leftThigh * 0.35f - leftKnee * 0.15f, 0f, 0f);
        Pose(rightFoot, rightFootRot, -rightThigh * 0.35f - rightKnee * 0.15f, 0f, 0f);
        PoseArmsAndHands(hangL, hangR, leftArmPitch, rightArmPitch, leftElbow, rightElbow,
            acting ? actionYaw : 0f, holdingAxe || holdingShotgun || throwTime >= 0f,
            holdingAxe && !acting, holdingShotgun && !acting,
            shotgunMount, shotgunRecoil, shotgunPump);
    }

    void PoseArmsAndHands(
        float hangLDeg, float hangRDeg,
        float pitchL, float pitchR,
        float elbowL, float elbowR,
        float yawR, bool gripRight, bool holdAxeHigh, bool holdShotgun,
        float shotgunMount, float shotgunRecoil, float shotgunPump)
    {
        Vector3 down = -transform.up;
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        PointAlong(leftShoulder, leftShoulderRot, holdShotgun
            ? Vector3.Slerp(-right, fwd, 0.42f + 0.18f * shotgunMount)
            : Vector3.Slerp(-right, down, 0.32f));
        PointAlong(rightShoulder, rightShoulderRot, holdAxeHigh
            ? (right * 0.55f + down * 0.7f + fwd * 0.25f).normalized
            : holdShotgun
                ? Vector3.Slerp(right, fwd, 0.38f + 0.22f * shotgunMount)
                : Vector3.Slerp(right, down, 0.32f));

        Vector3 leftArmDir;
        Vector3 leftForeDir;
        Vector3 rightArmDir;
        Vector3 rightForeDir;
        if (holdShotgun)
        {
            Vector3 readyRightArm = (down * 0.2f + right * 0.48f + fwd * 0.62f).normalized;
            Vector3 readyRightFore = (fwd * 0.48f + right * 0.12f - up * 0.62f).normalized;
            Vector3 readyLeftArm = (fwd * 0.72f - up * 0.38f - right * 0.08f).normalized;
            Vector3 readyLeftFore = (fwd * 0.84f - up * 0.32f).normalized;

            Vector3 aimRightArm = (right * 0.58f + fwd * 0.62f + up * 0.08f).normalized;
            Vector3 aimRightFore = (fwd * 0.9f + up * 0.12f).normalized;
            Vector3 aimLeftArm = (fwd * 0.82f - right * 0.08f + up * 0.04f).normalized;
            Vector3 aimLeftFore = (fwd * 0.94f + up * 0.1f).normalized;

            rightArmDir = Vector3.Slerp(readyRightArm, aimRightArm, shotgunMount);
            rightForeDir = Vector3.Slerp(readyRightFore, aimRightFore, shotgunMount);
            leftArmDir = Vector3.Slerp(readyLeftArm, aimLeftArm, shotgunMount);
            leftForeDir = Vector3.Slerp(readyLeftFore, aimLeftFore, shotgunMount);

            rightForeDir = (rightForeDir + up * 0.28f * shotgunRecoil - fwd * 0.12f * shotgunRecoil).normalized;
            leftForeDir = (leftForeDir + up * 0.16f * shotgunRecoil + (down * 0.55f + fwd * 0.10f) * shotgunPump).normalized;
        }
        else if (holdAxeHigh)
        {
            leftArmDir = Vector3.Slerp(-right, down, Mathf.Clamp01(Mathf.Abs(hangLDeg) / 88f));
            leftArmDir = Quaternion.AngleAxis(pitchL, right) * leftArmDir;
            leftForeDir = Quaternion.AngleAxis(-elbowL, right) * leftArmDir;
            rightArmDir = (down * 0.62f + fwd * 0.52f + right * 0.22f).normalized;
            rightForeDir = (up * 0.58f + fwd * 0.68f + right * 0.1f).normalized;
        }
        else
        {
            leftArmDir = Vector3.Slerp(-right, down, Mathf.Clamp01(Mathf.Abs(hangLDeg) / 88f));
            leftArmDir = Quaternion.AngleAxis(pitchL, right) * leftArmDir;
            leftForeDir = Quaternion.AngleAxis(-elbowL, right) * leftArmDir;
            rightArmDir = Vector3.Slerp(right, down, Mathf.Clamp01(Mathf.Abs(hangRDeg) / 88f));
            rightArmDir = Quaternion.AngleAxis(pitchR, right) * rightArmDir;
            if (Mathf.Abs(yawR) > 0.01f)
                rightArmDir = Quaternion.AngleAxis(yawR, up) * rightArmDir;
            rightForeDir = Quaternion.AngleAxis(-elbowR, right) * rightArmDir;
        }

        if (chopTime >= 0f)
        {
            float lift, drive, release;
            ChopWeights(out lift, out drive, out release);
            Vector3 readyArm = (down * 0.62f + fwd * 0.52f + right * 0.22f).normalized;
            Vector3 readyFore = (up * 0.58f + fwd * 0.68f + right * 0.1f).normalized;
            Vector3 cockArm = (right * 0.55f + up * 0.65f - fwd * 0.25f).normalized;
            Vector3 cockFore = (up * 0.85f - fwd * 0.35f - right * 0.12f).normalized;
            Vector3 hitArm = (fwd * 0.90f + down * 0.28f + right * 0.12f).normalized;
            Vector3 hitFore = (fwd * 0.92f - up * 0.30f - right * 0.16f).normalized;
            rightArmDir = Vector3.Slerp(Vector3.Slerp(readyArm, cockArm, lift), hitArm, drive);
            rightForeDir = Vector3.Slerp(Vector3.Slerp(readyFore, cockFore, lift), hitFore, drive);
            rightArmDir = Vector3.Slerp(rightArmDir, readyArm, release);
            rightForeDir = Vector3.Slerp(rightForeDir, readyFore, release);
            // The free hand stays clear of the blade and balances the torso.
            leftArmDir = Vector3.Slerp(leftArmDir, (down - right * 0.35f - fwd * 0.2f).normalized, lift * (1f - release));
        }

        PointAlong(leftArm, leftArmRot, leftArmDir);
        PointAlong(rightArm, rightArmRot, rightArmDir);
        PointAlong(leftForeArm, leftForeArmRot, leftForeDir);
        PointAlong(rightForeArm, rightForeArmRot, rightForeDir);

        Vector3 leftPalm = -fwd * 0.8f + right * 0.35f;
        Vector3 rightPalm = holdAxeHigh
            ? (-right * 0.9f + up * 0.12f - fwd * 0.2f)
            : holdShotgun
                ? (up * 0.2f - right * 0.15f)
                : (-fwd * 0.8f - right * 0.35f);
        Vector3 leftFingerDir = holdShotgun
            ? Vector3.Slerp((fwd * 0.82f - up * 0.18f).normalized, fwd, shotgunMount)
            : (leftForeDir * 0.7f + down * 0.3f - fwd * 0.1f).normalized;
        Vector3 rightFingerDir = holdAxeHigh
            ? (up * 0.5f + fwd * 0.72f).normalized
            : holdShotgun
                ? Vector3.Slerp((fwd * 0.90f - up * 0.16f).normalized, fwd, shotgunMount)
                : (rightForeDir * 0.7f + down * 0.3f - fwd * 0.1f).normalized;
        if (chopTime >= 0f)
        {
            float lift, drive, release;
            ChopWeights(out lift, out drive, out release);
            Vector3 readyGrip = (up * 0.5f + fwd * 0.72f).normalized;
            rightFingerDir = Vector3.Slerp(Vector3.Slerp(readyGrip, (up - fwd * 0.4f).normalized, lift),
                (fwd * 0.9f - up * 0.35f).normalized, drive);
            rightFingerDir = Vector3.Slerp(rightFingerDir, readyGrip, release);
            rightPalm = -right + up * 0.12f - fwd * 0.2f;
        }
        AimBone(leftHand, leftHandRot, leftFingerDir, leftPalm);
        AimBone(rightHand, rightHandRot, rightFingerDir, rightPalm);

        CurlFingers(leftFingers, leftFingerBind, restFingerEuler);
        CurlFingers(rightFingers, rightFingerBind, gripRight ? gripFingerEuler : restFingerEuler);
    }

    void ChopWeights(out float lift, out float drive, out float release)
    {
        lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chopTime / ChopWindup));
        float strike = Mathf.Clamp01((chopTime - ChopWindup) / ChopStrike);
        drive = strike * strike;
        release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((chopTime - ChopWindup - ChopStrike - ChopContact) / (ChopPull + ChopRecover)));
    }

    void PointAlong(Transform bone, Quaternion bind, Vector3 along)
    {
        if (bone == null || bone.parent == null || along.sqrMagnitude < 0.0001f)
            return;

        along.Normalize();
        Quaternion bindWorld = bone.parent.rotation * bind;
        Quaternion hang = Quaternion.FromToRotation(bindWorld * Vector3.up, along);
        bone.localRotation = Quaternion.Inverse(bone.parent.rotation) * (hang * bindWorld);
    }

    void AimBone(Transform bone, Quaternion bind, Vector3 along, Vector3 palm)
    {
        if (bone == null || bone.parent == null || along.sqrMagnitude < 0.0001f)
            return;

        along.Normalize();
        Quaternion bindWorld = bone.parent.rotation * bind;
        Quaternion hang = Quaternion.FromToRotation(bindWorld * Vector3.up, along);
        Quaternion hungWorld = hang * bindWorld;

        Vector3 wantPalm = Vector3.ProjectOnPlane(palm, along);
        Vector3 havePalm = Vector3.ProjectOnPlane(hungWorld * Vector3.forward, along);
        if (wantPalm.sqrMagnitude > 0.0001f && havePalm.sqrMagnitude > 0.0001f)
            hungWorld = Quaternion.FromToRotation(havePalm.normalized, wantPalm.normalized) * hungWorld;

        bone.localRotation = Quaternion.Inverse(bone.parent.rotation) * hungWorld;
    }

    static void CurlFingers(Transform[] bones, Quaternion[] bind, Vector3[] eulers)
    {
        if (bones == null)
            return;

        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
                continue;
            Vector3 e = eulers[i];
            bones[i].localRotation = bind[i] * Quaternion.Euler(e.x, e.y, e.z);
        }
    }

    static float Ease(float t)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    void CachePose()
    {
        hips = FindBone("mixamorig:Hips");
        spine = FindBone("mixamorig:Spine");
        spine1 = FindBone("mixamorig:Spine1");
        spine2 = FindBone("mixamorig:Spine2");
        neck = FindBone("mixamorig:Neck");
        leftShoulder = FindBone("mixamorig:LeftShoulder");
        rightShoulder = FindBone("mixamorig:RightShoulder");
        leftUpLeg = FindBone("mixamorig:LeftUpLeg");
        rightUpLeg = FindBone("mixamorig:RightUpLeg");
        leftLeg = FindBone("mixamorig:LeftLeg");
        rightLeg = FindBone("mixamorig:RightLeg");
        leftFoot = FindBone("mixamorig:LeftFoot");
        rightFoot = FindBone("mixamorig:RightFoot");
        leftArm = FindBone("mixamorig:LeftArm");
        rightArm = FindBone("mixamorig:RightArm");
        leftForeArm = FindBone("mixamorig:LeftForeArm");
        rightForeArm = FindBone("mixamorig:RightForeArm");
        leftHand = FindBone("mixamorig:LeftHand");
        rightHand = FindBone("mixamorig:RightHand");

        hipsRot = Rot(hips);
        spineRot = Rot(spine);
        spine1Rot = Rot(spine1);
        spine2Rot = Rot(spine2);
        neckRot = Rot(neck);
        leftShoulderRot = Rot(leftShoulder);
        rightShoulderRot = Rot(rightShoulder);
        leftUpLegRot = Rot(leftUpLeg);
        rightUpLegRot = Rot(rightUpLeg);
        leftLegRot = Rot(leftLeg);
        rightLegRot = Rot(rightLeg);
        leftFootRot = Rot(leftFoot);
        rightFootRot = Rot(rightFoot);
        leftArmRot = Rot(leftArm);
        rightArmRot = Rot(rightArm);
        leftForeArmRot = Rot(leftForeArm);
        rightForeArmRot = Rot(rightForeArm);
        leftHandRot = Rot(leftHand);
        rightHandRot = Rot(rightHand);
        hipsPos = hips != null ? hips.localPosition : Vector3.zero;
        CacheHands();
    }

    void CacheHands()
    {
        string[] joints =
        {
            "HandIndex1", "HandIndex2", "HandIndex3",
            "HandMiddle1", "HandMiddle2", "HandMiddle3",
            "HandRing1", "HandRing2", "HandRing3",
            "HandPinky1", "HandPinky2", "HandPinky3",
            "HandThumb1", "HandThumb2", "HandThumb3"
        };

        restFingerEuler = new Vector3[]
        {
            new Vector3(24f, 0f, 0f), new Vector3(28f, 0f, 0f), new Vector3(16f, 0f, 0f),
            new Vector3(26f, 0f, 0f), new Vector3(30f, 0f, 0f), new Vector3(18f, 0f, 0f),
            new Vector3(24f, 0f, 0f), new Vector3(28f, 0f, 0f), new Vector3(16f, 0f, 0f),
            new Vector3(20f, 0f, 0f), new Vector3(24f, 0f, 0f), new Vector3(14f, 0f, 0f),
            new Vector3(10f, 16f, 0f), new Vector3(10f, 0f, 0f), new Vector3(8f, 0f, 0f)
        };
        gripFingerEuler = new Vector3[]
        {
            new Vector3(68f, 0f, 0f), new Vector3(78f, 0f, 0f), new Vector3(52f, 0f, 0f),
            new Vector3(70f, 0f, 0f), new Vector3(80f, 0f, 0f), new Vector3(54f, 0f, 0f),
            new Vector3(66f, 0f, 0f), new Vector3(76f, 0f, 0f), new Vector3(50f, 0f, 0f),
            new Vector3(58f, 0f, 0f), new Vector3(68f, 0f, 0f), new Vector3(44f, 0f, 0f),
            new Vector3(18f, 28f, 0f), new Vector3(14f, 0f, 0f), new Vector3(16f, 0f, 0f)
        };

        leftFingers = new Transform[joints.Length];
        rightFingers = new Transform[joints.Length];
        leftFingerBind = new Quaternion[joints.Length];
        rightFingerBind = new Quaternion[joints.Length];
        for (int i = 0; i < joints.Length; i++)
        {
            leftFingers[i] = FindBone("mixamorig:Left" + joints[i]);
            rightFingers[i] = FindBone("mixamorig:Right" + joints[i]);
            leftFingerBind[i] = Rot(leftFingers[i]);
            rightFingerBind[i] = Rot(rightFingers[i]);
        }
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

    static Quaternion Rot(Transform bone)
    {
        return bone != null ? bone.localRotation : Quaternion.identity;
    }

    void Pose(Transform bone, Quaternion bind, float pitch, float yaw, float roll)
    {
        if (bone == null || bone.parent == null)
            return;

        Quaternion offset = Quaternion.identity;
        if (Mathf.Abs(roll) > 0.01f)
            offset = Quaternion.AngleAxis(roll, bone.parent.InverseTransformDirection(transform.forward)) * offset;
        if (Mathf.Abs(pitch) > 0.01f)
            offset = Quaternion.AngleAxis(pitch, bone.parent.InverseTransformDirection(transform.right)) * offset;
        if (Mathf.Abs(yaw) > 0.01f)
            offset = Quaternion.AngleAxis(yaw, bone.parent.InverseTransformDirection(transform.up)) * offset;

        bone.localRotation = offset * bind;
    }
}

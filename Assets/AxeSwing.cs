using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(2100)]
public class AxeSwing : MonoBehaviour
{
    public static AxeSwing Instance;
    public static bool HasAxe = true;

    [Header("Controls")]
    public bool swingOnE = true;
    public bool swingOnLeftClick = true;
    public bool throwOnRightClick = true;

    [Header("Timing (seconds)")]
    public float windUpTime = 0.18f;
    public float strikeTime = 0.10f;
    public float recoverTime = 0.28f;

    [Header("Motion")]
    public float windUpAngle = -40f;
    public float strikeAngle = 65f;
    public Vector3 windUpOffset = new Vector3(0f, 0.12f, -0.12f);
    public Vector3 strikeOffset = new Vector3(0f, -0.12f, 0.25f);

    [Header("Throw")]
    public float throwSpeed = 22f;
    public float throwSpin = 18f;

    Vector3 restPos;
    Quaternion restRot;
    bool swinging;
    public bool IsSwinging => swinging;
    public bool IsChopping => repeatChop;
    bool repeatChop;
    bool sessionSwing;
    HarvestableTree choppingTarget;
    HarvestableTree swingTarget;
    Vector3 choppingOrigin;
    int pickupFrame = -1;
    [Tooltip("Optional marker at the axe blade. Otherwise the top of the axe mesh is used.")]
    public Transform bladeContact;
    bool treeContact;
    bool strikeActive;
    Vector3 chopPose;
    Vector3 previousBlade;
    Transform measuredAxe;
    Vector3 bladeLocal;

    void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        HasAxe = true;
    }

    void OnGUI()
    {
        if (Instance != this)
            return;

        if (!HasAxe)
            ScreenText.Centered("No axe. Walk over it to pick it up.", Screen.height * 0.5f - 90f * ScreenText.Scale, 24);
    }

    void Start()
    {
        restPos = transform.localPosition;
        restRot = transform.localRotation;
    }

    void Update()
    {
        if (Instance != this) return;
        if (GameplayBlocked())
        {
            StopChopping();
            if (swinging)
            {
                StopAllCoroutines();
                swinging = false;
                strikeActive = false;
                sessionSwing = false;
                swingTarget = null;
                PlayerBodyAnimation.EndChop();
                transform.localRotation = restRot;
                transform.localPosition = restPos;
            }
            return;
        }

        bool pressedE = swingOnE && ChopKeyPressed();
        if (repeatChop && pressedE)
        {
            StopChopping(); // A second tap stops after extracting the current stroke.
            return;
        }
        if (repeatChop && !CanContinueChopping()) StopChopping();

        if (!HasAxe || swinging || Time.frameCount == pickupFrame)
            return;

        if (ThrowPressed()) { StopChopping(); StartCoroutine(Throw()); }
        else if (pressedE && StartChopping(HarvestableTree.AimedTree())) StartCoroutine(Swing());
        else if (repeatChop) StartCoroutine(Swing());
        else if (SwingPressed()) StartCoroutine(Swing());
    }

    bool GameplayBlocked()
    {
        return Inventory.IsOpen || !Inventory.AxeInHand || CampfireBuild.Busy || WoodBuilder.IsBuilding
            || BirdChallenge.Active || PlayerShotgun.IsEquipped || RideableHorse.BlocksAxeSwing()
            || (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead);
    }

    public bool StartChopping(HarvestableTree target)
    {
        if (target == null || swinging || !target.CanBeChopped || GameplayBlocked() || !HasAxe || Inventory.Instance == null
            || InventoryWorldDrop.WoodInReach || InventoryWorldDrop.CollectingWood || AnimalCarcass.PlayerNear
            || MountainHermit.InTalkRange() || PlayerMove.IsMoving || PlayerMove.IsSprinting
            || HarvestableTree.AimedTree() != target) return false;
        choppingTarget = target;
        choppingOrigin = PlayerView.Position();
        repeatChop = true;
        return true;
    }

    public void StopChopping()
    {
        repeatChop = false;
        choppingTarget = null;
    }

    bool CanContinueChopping()
    {
        return choppingTarget != null && choppingTarget.CanBeChopped && HasAxe && !GameplayBlocked()
            && !PlayerMove.IsMoving && !PlayerMove.IsSprinting
            && Vector3.Distance(choppingOrigin, PlayerView.Position()) <= 0.3f
            && HarvestableTree.AimedTree() == choppingTarget;
    }

    static bool ChopKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    bool SwingPressed()
    {
        if (InventoryWorldDrop.CollectingWood) return false;
        if (Inventory.IsOpen || WoodBuilder.IsBuilding || BirdChallenge.Active || PlayerShotgun.IsEquipped || !Inventory.AxeInHand) return false;
        if (RideableHorse.BlocksAxeSwing() || MountainHermit.InTalkRange()) return false;
        if (AnimalCarcass.PlayerNear) return false; // E is for the carcass at your feet
#if ENABLE_INPUT_SYSTEM
        var ms = Mouse.current;
        return swingOnLeftClick && ms != null && ms.leftButton.wasPressedThisFrame;
#else
        return swingOnLeftClick && Input.GetMouseButtonDown(0);
#endif
    }

    bool ThrowPressed()
    {
        if (Inventory.IsOpen || !throwOnRightClick || WoodBuilder.IsBuilding || BirdChallenge.Active || PlayerShotgun.IsEquipped || !Inventory.AxeInHand) return false;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead) return false;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    void OnDisable()
    {
        if (Instance != this) return;
        StopChopping();
        StopAllCoroutines();
        swinging = strikeActive = sessionSwing = false;
        swingTarget = null;
        PlayerBodyAnimation.EndChop();
        transform.localPosition = restPos;
        transform.localRotation = restRot;
    }

    void LateUpdate()
    {
        if (Instance != this || !swinging) return;
        if (strikeActive) TryTreeContact();
        else previousBlade = BladePoint();
    }

    IEnumerator Swing()
    {
        swinging = true;
        sessionSwing = repeatChop;
        swingTarget = sessionSwing ? choppingTarget : null;
        treeContact = false;
        PlayerBodyAnimation.PlayChop();
        Vector3 raised = new Vector3(0.12f, 0.18f, -0.10f);
        Vector3 contact = new Vector3(-0.12f, -0.06f, 0.22f);
        Vector3 pulled = new Vector3(-0.05f, 0.01f, 0.04f);
        Quaternion cock = restRot * Quaternion.Euler(-55f, -18f, -24f);
        Quaternion bite = restRot * Quaternion.Euler(40f, 12f, 18f);
        Quaternion extracted = restRot * Quaternion.Euler(16f, 6f, 7f);
        Vector3 readyPose = Vector3.zero;
        Vector3 raisedPose = new Vector3(1f, 0f, 0f);
        Vector3 hitPose = new Vector3(1f, 1f, 0f);
        yield return ChopBlend(restRot, Vector3.zero, cock, raised, PlayerBodyAnimation.ChopWindup, false, readyPose, raisedPose);
        if (sessionSwing && (!repeatChop || swingTarget == null || !swingTarget.CanBeChopped))
        {
            yield return RecoverCancelledSwing();
            yield break;
        }
        previousBlade = BladePoint();
        strikeActive = true;
        GameAudio.AxeWhoosh();
        yield return ChopBlend(cock, raised, bite, contact, PlayerBodyAnimation.ChopStrike, true, raisedPose, hitPose);
        strikeActive = false;
        // Preserve the pose at contact: recovery cannot jump to the end of a missed swing.
        Quaternion stoppedRotation = transform.localRotation;
        Vector3 stoppedPosition = transform.localPosition - restPos;
        Vector3 stoppedPose = chopPose;
        if (!treeContact)
        {
            if (sessionSwing)
            {
                bool missed = repeatChop;
                StopChopping();
                if (missed && Inventory.Instance != null) Inventory.Instance.ShowNotice("Move closer and line up the axe, then press E again.");
            }
            else StrikeWhatYouLookAt();
        }
        yield return new WaitForSeconds(treeContact ? PlayerBodyAnimation.ChopContact : 0.025f);
        Vector3 pulledPose = new Vector3(1f, stoppedPose.y, 0.35f);
        Vector3 restPose = new Vector3(1f, stoppedPose.y, 1f);
        yield return ChopBlend(stoppedRotation, stoppedPosition, extracted, pulled, PlayerBodyAnimation.ChopPull, false, stoppedPose, pulledPose);
        yield return ChopBlend(extracted, pulled, restRot, Vector3.zero, PlayerBodyAnimation.ChopRecover, false, pulledPose, restPose);
        PlayerBodyAnimation.EndChop();
        swinging = false;
        sessionSwing = false;
        swingTarget = null;
    }

    IEnumerator RecoverCancelledSwing()
    {
        yield return ChopBlend(transform.localRotation, transform.localPosition - restPos, restRot, Vector3.zero,
            PlayerBodyAnimation.ChopRecover, false, chopPose, new Vector3(chopPose.x, chopPose.y, 1f));
        PlayerBodyAnimation.EndChop();
        swinging = strikeActive = sessionSwing = false;
        swingTarget = null;
    }

    IEnumerator ChopBlend(Quaternion from, Vector3 fromOffset, Quaternion to, Vector3 toOffset,
        float duration, bool striking, Vector3 poseFrom, Vector3 poseTo)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (sessionSwing && !repeatChop && poseTo.z == 0f) yield break;
            if (striking && treeContact) yield break;
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float k = striking ? t * t : Mathf.SmoothStep(0f, 1f, t);
            transform.localRotation = Quaternion.Slerp(from, to, k);
            transform.localPosition = restPos + Vector3.Lerp(fromOffset, toOffset, k);
            if (striking) transform.localPosition += new Vector3(0.025f, 0.045f, -0.02f) * Mathf.Sin(t * Mathf.PI);
            chopPose = Vector3.Lerp(poseFrom, poseTo, k);
            PlayerBodyAnimation.SetChopPose(chopPose);
            // One yield per frame. Collision runs after the visible hand's LateUpdate.
            yield return null;
        }
    }
    static void StrikeWhatYouLookAt()
    {
        BuildPiece piece = WoodBuilder.LookedAtPiece(2.2f);
        if (piece != null)
        {
            piece.Hit();
            return;
        }

        Ray ray = PlayerView.AimRay();
        if (Physics.Raycast(ray, out RaycastHit hit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            Animal animal = hit.collider.GetComponentInParent<Animal>();
            if (animal != null)
            {
                animal.Hit();
                return;
            }
        }

        // Trees are cut only by the blade sweep, never by a crosshair fallback.
    }

    Vector3 BladePoint()
    {
        if (bladeContact != null) return bladeContact.position;
        if (CameraViewSwitcher.Instance != null && !CameraViewSwitcher.Instance.IsFirstPerson && HandAxeGrip.Instance != null)
            return HandAxeGrip.Instance.BladeWorldPoint;
        Transform axe = HeldVisual();
        if (measuredAxe != axe)
        {
            measuredAxe = axe;
            Bounds bounds = new Bounds();
            bool any = false;
            foreach (MeshFilter filter in axe.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = axe.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }
            bladeLocal = bounds.center + Vector3.up * bounds.extents.y * 0.8f;
        }
        return axe.TransformPoint(bladeLocal);
    }

    public void TryTreeContact()
    {
        if (!swinging || !strikeActive || treeContact) return;
        if (sessionSwing && (!repeatChop || swingTarget == null || !swingTarget.CanBeChopped))
        {
            StopChopping();
            return;
        }
        Vector3 blade = BladePoint();
        Transform owner = PlayerView.Body();
        bool contact = TryBladeSurface(previousBlade, blade, owner, transform, out RaycastHit closest);
        previousBlade = blade;
        if (!contact) return;
        // The nearest solid object blocks the swing, even when it is not a tree.
        treeContact = true;
        HarvestableTree tree = closest.collider.GetComponentInParent<HarvestableTree>();
        if (sessionSwing && tree != swingTarget)
        {
            StopChopping();
            if (Inventory.Instance != null) Inventory.Instance.ShowNotice("The axe was blocked. Reposition and press E again.");
            return;
        }
        if (tree != null) tree.ChopAt(closest.point, closest.normal);
        else
        {
            Animal animal = closest.collider.GetComponentInParent<Animal>();
            BuildPiece piece = closest.collider.GetComponentInParent<BuildPiece>();
            if (animal != null) animal.Hit();
            else if (piece != null) piece.Hit();
        }
    }

    public static bool TryBladeSurface(Vector3 from, Vector3 to, Transform owner, Transform held, out RaycastHit closest)
    {
        closest = default;
        Vector3 step = to - from;
        if (step.sqrMagnitude < 0.000001f) return false;
        float nearest = float.MaxValue;
        foreach (RaycastHit hit in Physics.SphereCastAll(from, 0.055f, step.normalized,
            step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner))) continue;
            if (held != null && (hit.transform == held || hit.transform.IsChildOf(held))) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            closest = hit;
            if (hit.distance <= 0.0001f && hit.point == Vector3.zero)
            {
                // Unity reports a zero point when the sweep starts overlapping a collider.
                closest.point = hit.collider.ClosestPoint(from);
                Vector3 normal = from - closest.point;
                closest.normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : -step.normalized;
            }
        }
        return closest.collider != null;
    }

    IEnumerator Throw()
    {
        swinging = true;
        PlayerBodyAnimation.PlayThrow();
        Vector3 backOffset = windUpOffset;
        float backAngle = windUpAngle;
        yield return Blend(0f, Vector3.zero, backAngle, backOffset, PlayerBodyAnimation.ThrowCock);
        yield return new WaitForSeconds(PlayerBodyAnimation.ThrowHold);
        float throwAngle = strikeAngle * 0.45f;
        yield return Blend(backAngle, backOffset, throwAngle, strikeOffset * 0.5f, PlayerBodyAnimation.ThrowSnap);

        Transform held = HeldVisual();
        Vector3 origin = held.position;
        Quaternion rot = held.rotation;
        Vector3 scale = held.lossyScale;
        Vector3 throwDir = ThrowDirection();
        Vector3 throwRight = Vector3.Cross(Vector3.up, throwDir);
        if (throwRight.sqrMagnitude < 0.0001f)
            throwRight = transform.right;
        throwRight.Normalize();

        GameObject flying = Instantiate(held.gameObject);
        flying.name = "ThrownAxe";
        flying.transform.SetParent(null);
        flying.transform.SetPositionAndRotation(origin, rot);
        flying.transform.localScale = scale;

        AxeSwing[] swings = flying.GetComponentsInChildren<AxeSwing>(true);
        for (int i = 0; i < swings.Length; i++)
        {
            swings[i].enabled = false;
            Destroy(swings[i]);
        }

        HandAxeGrip[] grips = flying.GetComponentsInChildren<HandAxeGrip>(true);
        for (int i = 0; i < grips.Length; i++)
        {
            grips[i].enabled = false;
            Destroy(grips[i]);
        }

        Renderer[] flyingRenderers = flying.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < flyingRenderers.Length; i++)
            flyingRenderers[i].enabled = true;

        ThrownAxe thrown = flying.GetComponent<ThrownAxe>();
        if (thrown == null)
            thrown = flying.AddComponent<ThrownAxe>();

        // Release from the palm without a visible forward teleport.
        flying.transform.SetPositionAndRotation(origin, rot);
        thrown.Launch(throwDir * throwSpeed, throwRight * -throwSpin);

        SetHeldVisible(false);
        HasAxe = false;
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Axe thrown. Walk over it to pick it up.");

        transform.localRotation = restRot;
        transform.localPosition = restPos;
        yield return new WaitForSeconds(PlayerBodyAnimation.ThrowRecover);
        swinging = false;
    }

    Transform HeldVisual()
    {
        if (CameraViewSwitcher.Instance != null && !CameraViewSwitcher.Instance.IsFirstPerson
            && HandAxeGrip.Instance != null)
            return HandAxeGrip.Instance.transform;
        return transform;
    }

    Vector3 ThrowDirection()
    {
        Camera view = PlayerView.GetCamera();
        if (view == null) return PlayerView.AimDirection();
        Ray sight = view.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        Vector3 target = sight.GetPoint(80f);
        float nearest = 80f;
        Transform owner = PlayerView.Body();
        foreach (RaycastHit hit in Physics.RaycastAll(sight, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && (hit.transform == owner || hit.transform.IsChildOf(owner))) continue;
            if (hit.distance >= nearest) continue;
            nearest = hit.distance;
            target = hit.point;
        }
        Vector3 direction = target - HeldVisual().position;
        return Vector3.Dot(direction, sight.direction) > 0f ? direction.normalized : sight.direction;
    }
    public void GiveBack()
    {
        HasAxe = true;
        pickupFrame = Time.frameCount;
        transform.localRotation = restRot;
        transform.localPosition = restPos;
        SetHeldVisible(true);
        if (HandAxeGrip.Instance != null)
            SetRenderers(HandAxeGrip.Instance.gameObject, true);
    }

    static void SetRenderers(GameObject root, bool visible)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    void SetHeldVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    IEnumerator Blend(float a0, Vector3 o0, float a1, Vector3 o1, float time)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / time)
        {
            bool striking = a0 == windUpAngle && a1 == strikeAngle;
            float s = striking ? t * t : Mathf.SmoothStep(0f, 1f, t);
            transform.localRotation = restRot * Quaternion.Euler(Mathf.Lerp(a0, a1, s), 0f, 0f);
            transform.localPosition = restPos + Vector3.Lerp(o0, o1, s);
            yield return null;
        }
        transform.localRotation = restRot * Quaternion.Euler(a1, 0f, 0f);
        transform.localPosition = restPos + o1;
    }
}

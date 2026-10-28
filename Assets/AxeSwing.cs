using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    bool showChopHint;
    int pickupFrame = -1;

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
        {
            ScreenText.Centered("No axe. Walk over it to pick it up.", Screen.height * 0.5f - 90f * ScreenText.Scale, 24);
            return;
        }

        if (showChopHint)
            ScreenText.Centered("Left click or E to chop", Screen.height * 0.62f, 22);
    }

    void Start()
    {
        restPos = transform.localPosition;
        restRot = transform.localRotation;
    }

    void Update()
    {
        if (Instance != this)
            return;

        if (!HasAxe || swinging)
            showChopHint = false;
        else if (Time.frameCount % 8 == 0)
            showChopHint = LookingAtWood();

        if (!HasAxe || swinging || Time.frameCount == pickupFrame)
            return;

        if (ThrowPressed()) StartCoroutine(Throw());
        else if (SwingPressed()) StartCoroutine(Swing());
    }

    bool SwingPressed()
    {
        if (WoodBuilder.IsBuilding || BirdChallenge.Active) return false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var ms = Mouse.current;
        return (swingOnE && kb != null && kb.eKey.wasPressedThisFrame)
            || (swingOnLeftClick && ms != null && ms.leftButton.wasPressedThisFrame);
#else
        return (swingOnE && Input.GetKeyDown(KeyCode.E))
            || (swingOnLeftClick && Input.GetMouseButtonDown(0));
#endif
    }

    bool ThrowPressed()
    {
        if (!throwOnRightClick || WoodBuilder.IsBuilding || BirdChallenge.Active) return false;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead) return false;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    IEnumerator Swing()
    {
        swinging = true;
        PlayerBodyAnimation.PlayChop();
        BuildPiece piece = WoodBuilder.LookedAtPiece(4f);
        HarvestableTree tree = piece == null ? HarvestableTree.AimedTree() : null;

        yield return Blend(0f, Vector3.zero, windUpAngle, windUpOffset, PlayerBodyAnimation.ChopWindup);
        yield return Blend(windUpAngle, windUpOffset, strikeAngle, strikeOffset, PlayerBodyAnimation.ChopStrike);

        if (piece != null)
            piece.Hit();
        else if (tree != null && tree.InChopRange())
            tree.Chop();
        else
            StrikeWhatYouLookAt();

        yield return Blend(strikeAngle, strikeOffset, 0f, Vector3.zero, recoverTime);
        swinging = false;
    }

    static void StrikeWhatYouLookAt()
    {
        BuildPiece piece = WoodBuilder.LookedAtPiece(4f);
        if (piece != null)
        {
            piece.Hit();
            return;
        }

        HarvestableTree.ChopLookedAtTree();
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
            Destroy(swings[i]);

        HandAxeGrip[] grips = flying.GetComponentsInChildren<HandAxeGrip>(true);
        for (int i = 0; i < grips.Length; i++)
            Destroy(grips[i]);

        Renderer[] flyingRenderers = flying.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < flyingRenderers.Length; i++)
            flyingRenderers[i].enabled = true;

        ThrownAxe thrown = flying.GetComponent<ThrownAxe>();
        if (thrown == null)
            thrown = flying.AddComponent<ThrownAxe>();

        origin += throwDir * 0.4f + Vector3.up * 0.12f;
        flying.transform.SetPositionAndRotation(origin, rot);
        thrown.Launch(throwDir * throwSpeed + Vector3.up * 2.8f, throwRight * -throwSpin + throwDir * 5f);

        SetHeldVisible(false);
        HasAxe = false;
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Axe thrown. Walk over it to pick it up.");

        transform.localRotation = restRot;
        transform.localPosition = restPos;
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
        if (CameraViewSwitcher.Instance != null && CameraViewSwitcher.Instance.IsFirstPerson)
        {
            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform.forward;
        }

        PlayerMove move = CameraViewSwitcher.Instance != null
            ? CameraViewSwitcher.Instance.GetComponent<PlayerMove>()
            : null;
        float pitch = move != null ? move.LookPitch : 0f;
        Transform body = move != null ? move.transform : transform.root;
        return Quaternion.Euler(pitch, body.eulerAngles.y, 0f) * Vector3.forward;
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
            float s = Mathf.SmoothStep(0f, 1f, t);
            transform.localRotation = restRot * Quaternion.Euler(Mathf.Lerp(a0, a1, s), 0f, 0f);
            transform.localPosition = restPos + Vector3.Lerp(o0, o1, s);
            yield return null;
        }
        transform.localRotation = restRot * Quaternion.Euler(a1, 0f, 0f);
        transform.localPosition = restPos + o1;
    }

    static bool LookingAtWood()
    {
        if (WoodBuilder.LookedAtPiece(4f) != null)
            return true;

        return HarvestableTree.IsLookingAtTree();
    }
}

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
        if (HasAxe || Instance != this)
            return;

        ScreenText.Centered("No axe! Go pick it up where it landed.", Screen.height * 0.5f - 90f * ScreenText.Scale, 24);
    }

    void Start()
    {
        restPos = transform.localPosition;
        restRot = transform.localRotation;
    }

    void Update()
    {
        if (!HasAxe || swinging || Time.frameCount == pickupFrame)
            return;

        if (ThrowPressed()) StartCoroutine(Throw());
        else if (SwingPressed()) StartCoroutine(Swing());
    }

    bool SwingPressed()
    {
        if (WoodBuilder.IsBuilding) return false;
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
        if (!throwOnRightClick || WoodBuilder.IsBuilding) return false;
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
        yield return Blend(0f, Vector3.zero, windUpAngle, windUpOffset, windUpTime);
        yield return Blend(windUpAngle, windUpOffset, strikeAngle, strikeOffset, strikeTime);
        yield return Blend(strikeAngle, strikeOffset, 0f, Vector3.zero, recoverTime);
        swinging = false;
    }

    IEnumerator Throw()
    {
        swinging = true;
        yield return Blend(0f, Vector3.zero, windUpAngle * 1.5f, windUpOffset * 1.5f, windUpTime);

        Camera cam = Camera.main;
        if (cam != null)
        {
            GameObject flying = Instantiate(gameObject, transform.position, transform.rotation);
            flying.name = "ThrownAxe";
            flying.transform.localScale = transform.lossyScale;

            AxeSwing copyScript = flying.GetComponent<AxeSwing>();
            copyScript.enabled = false;
            Destroy(copyScript);

            ThrownAxe thrown = flying.AddComponent<ThrownAxe>();
            Vector3 velocity = cam.transform.forward * throwSpeed + Vector3.up * 1.5f;
            thrown.Launch(velocity, cam.transform.right * -throwSpin);

            SetHeldVisible(false);
            HasAxe = false;
        }

        transform.localRotation = restRot;
        transform.localPosition = restPos;
        swinging = false;
    }

    public void GiveBack()
    {
        HasAxe = true;
        pickupFrame = Time.frameCount;
        transform.localRotation = restRot;
        transform.localPosition = restPos;
        SetHeldVisible(true);
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
}

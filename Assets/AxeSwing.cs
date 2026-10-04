using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class AxeSwing : MonoBehaviour
{
    [Header("Controls")]
    public bool swingOnE = true;
    public bool swingOnLeftClick = true;

    [Header("Timing (seconds)")]
    public float windUpTime = 0.18f;
    public float strikeTime = 0.10f;
    public float recoverTime = 0.28f;

    [Header("Motion")]
    public float windUpAngle = -40f;
    public float strikeAngle = 65f;
    public Vector3 windUpOffset = new Vector3(0f, 0.12f, -0.12f);
    public Vector3 strikeOffset = new Vector3(0f, -0.12f, 0.25f);

    [Header("Hook your chop logic here")]
    public UnityEvent onStrike;

    Vector3 restPos;
    Quaternion restRot;
    bool swinging;

    void Start()
    {
        restPos = transform.localPosition;
        restRot = transform.localRotation;
    }

    void Update()
    {
        if (!swinging && SwingPressed()) StartCoroutine(Swing());
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

    IEnumerator Swing()
    {
        swinging = true;
        yield return Blend(0f, Vector3.zero, windUpAngle, windUpOffset, windUpTime);
        yield return Blend(windUpAngle, windUpOffset, strikeAngle, strikeOffset, strikeTime);
        onStrike.Invoke();                       // impact moment
        yield return Blend(strikeAngle, strikeOffset, 0f, Vector3.zero, recoverTime);
        swinging = false;
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
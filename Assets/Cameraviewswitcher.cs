using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CameraViewSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform eyePoint;
    [SerializeField] private Transform thirdPersonPoint;
    [SerializeField] private Renderer[] bodyRenderers;

    [Header("Settings")]
    [SerializeField] private float transitionSpeed = 10f;
    [SerializeField] private bool startInFirstPerson = false;

    private bool isFirstPerson;

    private void Start()
    {
        isFirstPerson = startInFirstPerson;
        ApplyBodyVisibility();
        SnapCamera();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;
            ApplyBodyVisibility();
        }
    }

    private void LateUpdate()
    {
        Transform cam = playerCamera.transform;
        Transform target = isFirstPerson ? eyePoint : thirdPersonPoint;
        Vector3 targetLocal = cam.parent.InverseTransformPoint(target.position);

        cam.localPosition = Vector3.Lerp(cam.localPosition, targetLocal, transitionSpeed * Time.deltaTime);
    }

    private void SnapCamera()
    {
        Transform cam = playerCamera.transform;
        Transform target = isFirstPerson ? eyePoint : thirdPersonPoint;
        cam.localPosition = cam.parent.InverseTransformPoint(target.position);
    }

    private void ApplyBodyVisibility()
    {
        var mode = isFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        foreach (var r in bodyRenderers)
        {
            if (r != null)
                r.shadowCastingMode = mode;
        }
    }
}
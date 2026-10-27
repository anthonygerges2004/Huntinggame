using System.Collections.Generic;
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
    [SerializeField] private float thirdPersonPitch = 14f;
    [SerializeField] private float cameraPadding = 0.35f;

    private bool isFirstPerson;
    private Renderer[] weaponRenderers;
    private Renderer[] handAxeRenderers;

    private void Start()
    {
        isFirstPerson = startInFirstPerson;
        CacheWeaponRenderers();
        CreateHandAxe();
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
        Vector3 desired = isFirstPerson ? eyePoint.position : ResolveThirdPersonPosition();
        Vector3 targetLocal = cam.parent.InverseTransformPoint(desired);

        cam.localPosition = Vector3.Lerp(cam.localPosition, targetLocal, transitionSpeed * Time.deltaTime);

        if (!isFirstPerson)
        {
            float mousePitch = cam.localEulerAngles.x;
            if (mousePitch > 180f)
                mousePitch -= 360f;
            cam.localRotation = Quaternion.Euler(mousePitch + thirdPersonPitch, 0f, 0f);
        }

        ApplyWeaponVisibility();
    }

    private void SnapCamera()
    {
        Transform cam = playerCamera.transform;
        Vector3 desired = isFirstPerson ? eyePoint.position : ResolveThirdPersonPosition();
        cam.localPosition = cam.parent.InverseTransformPoint(desired);
        if (!isFirstPerson)
            cam.localRotation = Quaternion.Euler(thirdPersonPitch, 0f, 0f);
    }

    private Vector3 ResolveThirdPersonPosition()
    {
        Vector3 desired = thirdPersonPoint.position;
        Vector3 from = eyePoint.position;
        Vector3 offset = desired - from;
        float distance = offset.magnitude;
        if (distance < 0.05f)
            return desired;

        Vector3 direction = offset / distance;
        RaycastHit[] hits = Physics.RaycastAll(from, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        float closest = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;
            if (hits[i].distance < closest)
                closest = hits[i].distance;
        }

        if (closest >= distance)
            return desired;

        float allowed = Mathf.Max(0.75f, closest - cameraPadding);
        return from + direction * allowed;
    }

    private void CacheWeaponRenderers()
    {
        var found = new List<Renderer>();
        foreach (Transform child in playerCamera.transform)
            found.AddRange(child.GetComponentsInChildren<Renderer>(true));
        weaponRenderers = found.ToArray();
    }

    private void CreateHandAxe()
    {
        Transform axe = null;
        foreach (Transform child in playerCamera.transform)
        {
            if (child.GetComponentInChildren<Renderer>(true) != null)
            {
                axe = child;
                break;
            }
        }

        Transform hand = FindRightHand();
        if (axe == null || hand == null)
            return;

        GameObject copy = Instantiate(axe.gameObject, hand, false);
        copy.name = "HandAxe";

        AxeSwing[] swings = copy.GetComponentsInChildren<AxeSwing>(true);
        for (int i = 0; i < swings.Length; i++)
        {
            swings[i].enabled = false;
            Destroy(swings[i]);
        }

        Collider[] colliders = copy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        copy.transform.localPosition = new Vector3(0.1f, 0.02f, 0.03f);
        copy.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.left);
        copy.transform.localScale = Vector3.one;
        handAxeRenderers = copy.GetComponentsInChildren<Renderer>(true);
    }

    private Transform FindRightHand()
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            if (boneName == "mixamorig:RightHand" || boneName == "RightHand")
                return bones[i];
        }

        return null;
    }

    private void ApplyBodyVisibility()
    {
        var mode = isFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        foreach (var r in bodyRenderers)
        {
            if (r != null)
                r.shadowCastingMode = mode;
        }

        ApplyWeaponVisibility();
    }

    private void ApplyWeaponVisibility()
    {
        if (weaponRenderers == null)
            return;

        SetRenderersEnabled(weaponRenderers, isFirstPerson && AxeSwing.HasAxe);
        SetRenderersEnabled(handAxeRenderers, !isFirstPerson && AxeSwing.HasAxe);
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }
}
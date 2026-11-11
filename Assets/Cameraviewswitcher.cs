using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DefaultExecutionOrder(50)]
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
    [SerializeField] private float thirdPersonPitch = 12f;
    [SerializeField] private float cameraPadding = 0.35f;

    [Header("Third Person Follow")]
    [SerializeField] private float followDistance = 4.4f;
    [SerializeField] private float lookHeight = 1.2f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 50f;
    [SerializeField] private float minDistance = 2.2f;
    [SerializeField] private float maxDistance = 7f;
    [SerializeField] private float zoomSpeed = 0.012f;
    [SerializeField] private float followSmooth = 0.08f;

    public static CameraViewSwitcher Instance { get; private set; }
    public bool IsFirstPerson => isFirstPerson;
    public Camera PlayerCamera => playerCamera;
    public Transform EyePoint => eyePoint;
    public float MinLookPitch => isFirstPerson ? -85f : minPitch;
    public float MaxLookPitch => isFirstPerson ? 85f : maxPitch;

    public void PreferThirdPerson()
    {
        if (isFirstPerson)
        {
            isFirstPerson = false;
            ApplyParenting();
            ApplyBodyVisibility();
            SnapCamera();
        }
    }

    private bool isFirstPerson;
    private Renderer[] weaponRenderers;
    private Renderer[] handAxeRenderers;
    private Transform cameraParent;
    private Vector3 followVelocity;
    private float orbitPitch;
    private float currentDistance;
    const float ShoulderDistance = 3.3f;
    const float ShoulderOffset = 0.48f;
    private PlayerMove playerMove;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        isFirstPerson = startInFirstPerson;
        playerMove = GetComponent<PlayerMove>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
            cameraParent = playerCamera.transform.parent;
        orbitPitch = thirdPersonPitch;
        // Over-the-shoulder framing: close enough that the hunter fills the shot (scroll to zoom out).
        currentDistance = Mathf.Clamp(Mathf.Min(followDistance, ShoulderDistance), minDistance, maxDistance);

        CacheWeaponRenderers();
        CreateHandAxe();
        RefreshBodyRenderers();
        ApplyParenting();
        ApplyBodyVisibility();
        SnapCamera();
    }

    private void Update()
    {
        if (Inventory.IsOpen)
            return;

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;
            ApplyParenting();
            ApplyBodyVisibility();
            SnapCamera();
        }

        if (!isFirstPerson && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                currentDistance = Mathf.Clamp(currentDistance - scroll * zoomSpeed, minDistance, maxDistance);
        }
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
            return;

        aimFov = Mathf.MoveTowards(aimFov, PlayerShotgun.IsAiming ? 1f : 0f, Time.deltaTime * 6f);

        if (isFirstPerson)
            UpdateFirstPerson();
        else
            UpdateThirdPerson();

        ApplyWeaponVisibility();
        ApplyAimZoom();
    }

    float savedFov = -1f;
    float aimFov;

    /// <summary>The unzoomed field of view the camera returns to when not aiming.</summary>
    public float BaseFov => savedFov > 1f ? savedFov : (playerCamera != null ? playerCamera.fieldOfView : 70f);

    void ApplyAimZoom()
    {
        if (playerCamera == null)
            return;

        if (savedFov < 1f)
            savedFov = playerCamera.fieldOfView;
        // The pause menu FOV slider overrides the scene's camera once the player has set it.
        if (GameSettings.Fov > 1f)
            savedFov = GameSettings.Fov;

        float zoom = PlayerShotgun.AimFovScale;
        playerCamera.fieldOfView = Mathf.Lerp(savedFov, savedFov * zoom, aimFov);
    }

    private void UpdateFirstPerson()
    {
        Transform cam = playerCamera.transform;
        playerCamera.nearClipPlane = 0.02f;

        Vector3 targetLocal = cam.parent != null
            ? cam.parent.InverseTransformPoint(eyePoint != null ? eyePoint.position : transform.position + Vector3.up * 1.6f)
            : (eyePoint != null ? eyePoint.position : transform.position + Vector3.up * 1.6f);
        // A slow lerp chases the head bob and the view vibrates. Snap to the eye.
        cam.localPosition = targetLocal;
    }

    private void UpdateThirdPerson()
    {
        Transform cam = playerCamera.transform;
        orbitPitch = Mathf.Clamp(ReadLookPitch(), minPitch, maxPitch);

        float aim = aimFov;
        float height = Mathf.Lerp(lookHeight, lookHeight + 0.12f, aim);
        float distance = Mathf.Lerp(currentDistance, Mathf.Min(currentDistance, 2.0f), aim);
        Vector3 lookPoint = transform.position + Vector3.up * height;
        Quaternion orbit = Quaternion.Euler(orbitPitch, transform.eulerAngles.y, 0f);
        // The camera sits over the right shoulder, so the crosshair has a clear line past the hunter.
        float shoulder = Mathf.Lerp(ShoulderOffset, ShoulderOffset + 0.12f, aim);
        Vector3 pivot = lookPoint + Vector3.up * 0.2f + transform.right * shoulder;
        Vector3 desired = ResolveCollision(pivot, pivot + orbit * Vector3.back * distance);

        Vector3 smoothed = Vector3.SmoothDamp(cam.position, desired, ref followVelocity, followSmooth);
        // Recheck after smoothing: interpolation must not travel through an obstacle.
        cam.position = ResolveCollision(pivot, smoothed);
        if ((cam.position - smoothed).sqrMagnitude > 0.0001f)
            followVelocity = Vector3.zero;
        cam.rotation = Quaternion.Euler(-PlayerShotgun.ViewKick, 0f, PlayerShotgun.ViewRoll * 0.35f) * orbit;

        playerCamera.nearClipPlane = 0.05f;
    }

    private void SnapCamera()
    {
        if (playerCamera == null)
            return;

        followVelocity = Vector3.zero;
        Transform cam = playerCamera.transform;
        if (isFirstPerson)
        {
            UpdateFirstPerson();
            cam.localRotation = Quaternion.Euler(ReadLookPitch(), 0f, 0f);
            return;
        }

        orbitPitch = Mathf.Clamp(ReadLookPitch(), minPitch, maxPitch);
        Vector3 lookPoint = transform.position + Vector3.up * lookHeight;
        Quaternion orbit = Quaternion.Euler(orbitPitch, transform.eulerAngles.y, 0f);
        Vector3 pivot = lookPoint + Vector3.up * 0.2f + transform.right * ShoulderOffset;
        Vector3 desired = pivot + orbit * Vector3.back * currentDistance;
        cam.position = ResolveCollision(pivot, desired);
        cam.rotation = Quaternion.Euler(orbitPitch, transform.eulerAngles.y, 0f);
    }

    private void ApplyParenting()
    {
        if (playerCamera == null)
            return;

        Transform cam = playerCamera.transform;
        if (isFirstPerson)
        {
            Transform parent = cameraParent != null ? cameraParent : transform;
            cam.SetParent(parent, true);
        }
        else
        {
            cam.SetParent(null, true);
        }
    }

    private float ReadLookPitch()
    {
        if (playerMove != null)
            return playerMove.LookPitch;
        return thirdPersonPitch;
    }

    private Vector3 ResolveCollision(Vector3 from, Vector3 desired)
    {
        Vector3 offset = desired - from;
        float distance = offset.magnitude;
        if (distance < 0.05f)
            return desired;

        Vector3 direction = offset / distance;
        RaycastHit[] hits = Physics.SphereCastAll(from, 0.18f, direction, distance, ~0, QueryTriggerInteraction.Ignore);
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

        float allowed = Mathf.Max(0f, closest - cameraPadding);
        return from + direction * allowed;
    }

    private void CacheWeaponRenderers()
    {
        var found = new List<Renderer>();
        if (playerCamera != null)
        {
            foreach (Transform child in playerCamera.transform)
            {
                if (!UnderName(child, "Axe") && child.GetComponentInChildren<AxeSwing>(true) == null)
                    continue;
                found.AddRange(child.GetComponentsInChildren<Renderer>(true));
            }
        }
        weaponRenderers = found.ToArray();
    }

    private void CreateHandAxe()
    {
        if (playerCamera == null || GetComponentInChildren<HandAxeGrip>(true) != null)
            return;

        if (GetComponent<WeaponHold>() == null)
            gameObject.AddComponent<WeaponHold>();

        Transform axe = FindCameraAxe();
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

        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = Vector3.one;
        float grip = 0.3f;
        MeshFilter[] filters = copy.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
                continue;
            Vector3 size = filters[i].sharedMesh.bounds.size;
            grip = Mathf.Max(size.x, size.y, size.z) * 0.38f;
        }

        HandAxeGrip hold = copy.GetComponent<HandAxeGrip>();
        if (hold == null)
            hold = copy.AddComponent<HandAxeGrip>();
        hold.Setup(hand, transform, grip);
        handAxeRenderers = copy.GetComponentsInChildren<Renderer>(true);
    }

    private Transform FindCameraAxe()
    {
        Transform[] cameraKids = playerCamera.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < cameraKids.Length; i++)
        {
            Transform child = cameraKids[i];
            if (child == playerCamera.transform)
                continue;
            if (child.GetComponentInChildren<Renderer>(true) == null)
                continue;
            if (child.name.IndexOf("Axe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.GetComponent<AxeSwing>() != null)
                return child;
        }

        return null;
    }

    private Transform FindRightHand()
    {
        Transform left = null;
        Transform right = null;
        Transform[] bones = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < bones.Length; i++)
        {
            string boneName = bones[i].name;
            if (boneName == "mixamorig:LeftHand" || boneName == "LeftHand")
                left = bones[i];
            if (boneName == "mixamorig:RightHand" || boneName == "RightHand")
                right = bones[i];
        }

        return right != null ? right : left;
    }

    public void RefreshBodyRenderers()
    {
        var found = new List<Renderer>();
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null)
                continue;
            if (playerCamera != null && r.transform.IsChildOf(playerCamera.transform))
                continue;
            if (UnderName(r.transform, "HandAxe") || UnderName(r.transform, "FallingAxe") || UnderName(r.transform, "Shotgun") || UnderName(r.transform, "Rifle") || UnderName(r.transform, "Sniper") || UnderName(r.transform, "Pistol") || UnderName(r.transform, "SMG"))
                continue;
            found.Add(r);
        }

        bodyRenderers = found.ToArray();
        ApplyBodyVisibility();
    }

    private void ApplyBodyVisibility()
    {
        if (bodyRenderers == null)
            return;

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

        bool axeOut = AxeSwing.HasAxe && !PlayerShotgun.IsEquipped && Inventory.AxeInHand;
        SetRenderersEnabled(weaponRenderers, isFirstPerson && axeOut);
        SetRenderersEnabled(handAxeRenderers, !isFirstPerson && axeOut);
    }

    private static bool UnderName(Transform t, string name)
    {
        while (t != null)
        {
            if (t.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }

        return false;
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}


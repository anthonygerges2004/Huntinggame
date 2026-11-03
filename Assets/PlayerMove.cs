using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;
    public float runSpeed = 8.5f;
    public float crouchSpeed = 2f;
    public float jumpHeight = 1.4f;
    public float mouseSensitivity = 0.15f;
    public float gravity = -20f;
    public float swimSpeed = 3f;
    public float swimVerticalSpeed = 2f;
    public float sprintMultiplier = 2.2f;
    public float sprintDurationSeconds = 10f;
    public float sprintRechargeSeconds = 15f;

    public static bool IsCrouching;
    public static bool IsMoving;
    public static bool IsSprinting;
    public static bool Frozen;
    public static float SurfaceNoise { get; private set; } = 1f;

    public static void SetGait(bool moving, bool sprinting, bool crouching, float surfaceNoise)
    {
        IsMoving = moving;
        IsSprinting = sprinting;
        IsCrouching = crouching;
        SurfaceNoise = surfaceNoise < 0.05f ? 0.05f : surfaceNoise;
    }

    public float LookPitch => xRotation;

    CharacterController controller;
    Transform cam;
    CameraViewSwitcher views;
    float xRotation;
    float verticalSpeed;
    float sprintRemaining;
    float surfaceTimer;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        views = GetComponent<CameraViewSwitcher>();
        if (GetComponent<PlayerBodyAnimation>() == null)
            gameObject.AddComponent<PlayerBodyAnimation>();
        if (GetComponent<PlayerHunterLook>() == null)
            gameObject.AddComponent<PlayerHunterLook>();
        if (GetComponent<WeaponHold>() == null)
            gameObject.AddComponent<WeaponHold>();
        if (GetComponent<PlayerShotgun>() == null)
            gameObject.AddComponent<PlayerShotgun>();
        cam = GetComponentInChildren<Camera>().transform;
        sprintRemaining = sprintDurationSeconds;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 grounded = WorldGround.Snap(transform.position);
        grounded.y += 1f;
        controller.enabled = false;
        transform.position = grounded;
        controller.enabled = true;
    }

    void Update()
    {
        RefreshSurface();

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
        {
            SetNoise(false, false);
            return;
        }

        if (Keyboard.current == null || Mouse.current == null)
        {
            SetNoise(false, false);
            return;
        }

        if (Inventory.IsOpen)
        {
            UpdateSprint(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsCrouching = false;
            SetNoise(false, false);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);

        bool firstPerson = views == null || views.IsFirstPerson;
        xRotation -= look.y;
        xRotation = Mathf.Clamp(xRotation, firstPerson ? -85f : -80f, firstPerson ? 85f : 70f);
        if (firstPerson && cam != null)
            cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (Frozen)
        {
            UpdateSprint(false);
            IsCrouching = false;
            SetNoise(false, false);
            return;
        }

        IsCrouching = Keyboard.current.leftCtrlKey.isPressed;
        bool running = !IsCrouching && !PlayerShotgun.IsAiming && Keyboard.current.leftShiftKey.isPressed;

        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
        if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
        if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
        if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        bool isSwimming = WaterBodies.TryGetSwim(transform.position, out float surface, out Vector3 current);
        if (isSwimming)
        {
            UpdateSprint(false);
            SetNoise(false, false);
            Swim(move, surface, current);
            return;
        }

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        if (controller.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalSpeed += gravity * Time.deltaTime;

        bool sprinting = running && move.sqrMagnitude > 0.0001f && sprintRemaining > 0f;
        UpdateSprint(sprinting);
        SetNoise(move.sqrMagnitude > 0.0001f, sprinting);

        float currentSpeed = IsCrouching ? crouchSpeed : (sprinting ? runSpeed : speed);
        if (PlayerShotgun.IsAiming)
            currentSpeed *= 0.55f;
        Vector3 motion = move * currentSpeed;
        motion.y = verticalSpeed;
        controller.Move(motion * Time.deltaTime);
    }

    void UpdateSprint(bool sprinting)
    {
        if (sprinting)
            sprintRemaining -= Time.deltaTime;
        else
            sprintRemaining += Time.deltaTime * (sprintDurationSeconds / Mathf.Max(0.01f, sprintRechargeSeconds));

        sprintRemaining = Mathf.Clamp(sprintRemaining, 0f, sprintDurationSeconds);
    }

    static void SetNoise(bool moving, bool sprinting)
    {
        IsMoving = moving;
        IsSprinting = sprinting;
    }

    void RefreshSurface()
    {
        surfaceTimer -= Time.deltaTime;
        if (surfaceTimer > 0f)
            return;

        surfaceTimer = 0.4f;
        SurfaceNoise = SampleSurface(transform.position);
    }

    static float SampleSurface(Vector3 position)
    {
        if (WaterBodies.InsideWater(position))
            return 1.65f;

        Terrain terrain = WorldGround.TerrainAt(position);
        if (terrain == null || terrain.terrainData == null)
            return 1f;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (size.x < 1f || size.z < 1f || size.y < 0.01f)
            return 1f;

        float nx = Mathf.Clamp01((position.x - origin.x) / size.x);
        float nz = Mathf.Clamp01((position.z - origin.z) / size.z);
        float steep = 1f - terrain.terrainData.GetInterpolatedNormal(nx, nz).y;
        float height = terrain.terrainData.GetInterpolatedHeight(nx, nz) / size.y;
        float rock = Mathf.Clamp01(steep * 2.4f + Mathf.InverseLerp(0.32f, 0.72f, height));
        if (rock > 0.55f)
            return 1.3f;

        float grass = Mathf.Clamp01((1f - rock) * (1.15f - height));
        if (grass > 0.55f)
            return 0.75f;

        return 1f;
    }

    void Swim(Vector3 move, float surface, Vector3 current)
    {
        IsCrouching = false;
        float floatY = surface - 0.35f;
        float y = transform.position.y;

        float up = 0f;
        if (Keyboard.current.spaceKey.isPressed && y < floatY) up += 1f;
        if (Keyboard.current.leftCtrlKey.isPressed) up -= 1f;

        verticalSpeed = up != 0f
            ? up * swimVerticalSpeed
            : Mathf.Clamp((floatY - y) * 2f, -swimVerticalSpeed, swimVerticalSpeed);

        Vector3 motion = move * swimSpeed + current;
        motion.y = verticalSpeed;
        controller.Move(motion * Time.deltaTime);
    }

    void OnGUI()
    {
        if (cam == null || !WaterBodies.Underwater(cam.position))
            return;

        GUI.color = new Color(0.1f, 0.32f, 0.45f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

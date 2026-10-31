using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;
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
    public static bool Frozen;

    CharacterController controller;
    Transform cam;
    float xRotation;
    float verticalSpeed;
    float sprintRemaining;

    void Start()
    {
        controller = GetComponent<CharacterController>();
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
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Keyboard.current == null || Mouse.current == null)
            return;

        if (Inventory.IsOpen)
        {
            UpdateSprint(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsCrouching = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);

        xRotation -= look.y;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (Frozen)
        {
            UpdateSprint(false);
            IsCrouching = false;
            return;
        }

        IsCrouching = Keyboard.current.leftCtrlKey.isPressed;

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
            Swim(move, surface, current);
            return;
        }

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        if (controller.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalSpeed += gravity * Time.deltaTime;

        bool sprinting = !IsCrouching
            && move.sqrMagnitude > 0.01f
            && Keyboard.current.leftShiftKey.isPressed
            && sprintRemaining > 0f;
        UpdateSprint(sprinting);

        float currentSpeed = IsCrouching ? crouchSpeed : speed;
        if (sprinting)
            currentSpeed *= sprintMultiplier;

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

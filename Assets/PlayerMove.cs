using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    public float speed = 5f;
    public float crouchSpeed = 2f;
    public float mouseSensitivity = 0.15f;
    public float gravity = -20f;

    public static bool IsCrouching;

    CharacterController controller;
    Transform cam;
    float xRotation;
    float verticalSpeed;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);

        xRotation -= look.y;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        IsCrouching = Keyboard.current.leftCtrlKey.isPressed;

        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
        if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
        if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
        if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        verticalSpeed += gravity * Time.deltaTime;

        float currentSpeed = IsCrouching ? crouchSpeed : speed;
        Vector3 motion = move * currentSpeed;
        motion.y = verticalSpeed;
        controller.Move(motion * Time.deltaTime);
    }
}

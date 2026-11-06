using UnityEngine;
using UnityEngine.InputSystem;

namespace DeerHunt
{
    /// <summary>
    /// First-person move and look. WASD, mouse, Shift to sprint, Control to crouch.
    /// The weapon adds recoil through AddRecoil.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float walkSpeed = 3.4f;
        [SerializeField] float sprintSpeed = 6.2f;
        [SerializeField] float crouchSpeed = 1.7f;
        [SerializeField] float lookSensitivity = 0.12f;
        [SerializeField] float gravity = -18f;
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.15f;
        [SerializeField] float eyeStand = 1.62f;
        [SerializeField] float eyeCrouch = 0.95f;

        CharacterController body;
        Camera view;
        float pitch;
        float recoil;
        float verticalSpeed;
        float height;

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public Camera View => view;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            view = GetComponentInChildren<Camera>();
            height = standHeight;
            body.height = standHeight;
            body.center = new Vector3(0f, standHeight * 0.5f, 0f);
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.Playing)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }

            Look();
            Move();
        }

        void Look()
        {
            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked || view == null)
                return;

            float zoomScale = 1f;
            WeaponController weapon = view.GetComponent<WeaponController>();
            if (weapon != null && weapon.IsZoomed)
                zoomScale = 0.22f;

            Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity * zoomScale;
            transform.Rotate(0f, delta.x, 0f);

            pitch -= delta.y;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 18f);
            view.transform.localRotation = Quaternion.Euler(pitch - recoil, 0f, 0f);
        }

        void Move()
        {
            if (Keyboard.current == null)
                return;

            IsCrouching = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
            Vector2 input = ReadMove();
            bool wantsSprint = Keyboard.current.leftShiftKey.isPressed && input.y > 0.1f;
            IsSprinting = wantsSprint && !IsCrouching;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            height = Mathf.MoveTowards(height, targetHeight, Time.deltaTime * 6f);
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            if (view != null)
            {
                float eye = IsCrouching ? eyeCrouch : eyeStand;
                Vector3 local = view.transform.localPosition;
                local.y = Mathf.MoveTowards(local.y, eye, Time.deltaTime * 6f);
                view.transform.localPosition = local;
            }

            float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;
            if (body.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            wish.y = verticalSpeed;
            body.Move(wish * Time.deltaTime);
        }

        static Vector2 ReadMove()
        {
            Vector2 input = Vector2.zero;
            Keyboard keys = Keyboard.current;
            if (keys.wKey.isPressed) input.y += 1f;
            if (keys.sKey.isPressed) input.y -= 1f;
            if (keys.dKey.isPressed) input.x += 1f;
            if (keys.aKey.isPressed) input.x -= 1f;
            if (input.sqrMagnitude > 1f)
                input.Normalize();
            return input;
        }

        public void AddRecoil(float kick)
        {
            recoil = Mathf.Clamp(recoil + kick, 0f, 8f);
        }
    }
}

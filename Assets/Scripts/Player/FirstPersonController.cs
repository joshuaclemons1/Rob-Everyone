using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Player
{
    // Walk/sprint/crouch/jump + mouse-look controller, plus bhop-style air
    // control (see HandleMove). Reads the New Input System's devices
    // directly (Keyboard.current / Mouse.current) so there's no Input
    // Actions asset to configure yet -- good enough until movement needs
    // to be swappable (e.g. jail state freezing the player).
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField, Range(0.3f, 1f)] private float crouchHeightRatio = 0.55f;

        [SerializeField] private float groundAcceleration = 60f;

        // Bhop-style air control: id Software's classic "air-accelerate"
        // formula. It caps speed gain per frame only in the current strafe
        // direction (airWishSpeed), so as the player turns while strafing
        // in the air, velocity keeps accumulating in the new direction on
        // top of what's already there -- chaining jump + air-strafe lets a
        // player who's learned the timing carry more speed than sprint
        // alone. No cap on total speed, by design (see gameplay-design.md's
        // Movement section: this is a skill-ceiling reward, not a
        // menu-toggle ability).
        [SerializeField] private float airAcceleration = 12f;
        [SerializeField] private float airWishSpeed = 3f;

        private CharacterController controller;
        private float verticalVelocity;
        private float cameraPitch;
        private Vector3 horizontalVelocity;
        private float standHeight;
        private Vector3 standCenter;
        private float standCameraY;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            standHeight = controller.height;
            standCenter = controller.center;
            if (cameraTransform != null) standCameraY = cameraTransform.localPosition.y;
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            HandleLook();
            HandleCrouch();
            HandleMove();
        }

        private void HandleLook()
        {
            if (Mouse.current == null || cameraTransform == null) return;

            Vector2 delta = Mouse.current.delta.ReadValue() * (mouseSensitivity * 0.02f);

            transform.Rotate(Vector3.up, delta.x);

            cameraPitch = Mathf.Clamp(cameraPitch - delta.y, -85f, 85f);
            cameraTransform.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
        }

        private void HandleCrouch()
        {
            if (Keyboard.current == null) return;

            IsCrouching = Keyboard.current.leftCtrlKey.isPressed;

            float targetHeight = IsCrouching ? standHeight * crouchHeightRatio : standHeight;
            float heightDelta = standHeight - targetHeight;

            controller.height = targetHeight;
            // Shift the center down by half the height lost rather than
            // recomputing it from scratch -- keeps whatever center offset
            // was already tuned in the Inspector, and guarantees a no-op
            // when standing (heightDelta == 0).
            controller.center = standCenter - new Vector3(0f, heightDelta * 0.5f, 0f);

            if (cameraTransform == null) return;
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = standCameraY - heightDelta;
            cameraTransform.localPosition = camPos;
        }

        private void HandleMove()
        {
            if (Keyboard.current == null) return;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 wishDir = (transform.right * input.x + transform.forward * input.y).normalized;
            bool grounded = controller.isGrounded;

            IsSprinting = grounded && !IsCrouching && input.y > 0f && Keyboard.current.leftShiftKey.isPressed;

            if (grounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;

                float targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
                Vector3 targetVelocity = wishDir * targetSpeed;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, groundAcceleration * Time.deltaTime);

                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                float currentSpeedInWishDir = Vector3.Dot(horizontalVelocity, wishDir);
                float addSpeedCap = airWishSpeed - currentSpeedInWishDir;
                if (addSpeedCap > 0f)
                {
                    float accelSpeed = Mathf.Min(airAcceleration * airWishSpeed * Time.deltaTime, addSpeedCap);
                    horizontalVelocity += wishDir * accelSpeed;
                }
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 move = horizontalVelocity;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }
    }
}

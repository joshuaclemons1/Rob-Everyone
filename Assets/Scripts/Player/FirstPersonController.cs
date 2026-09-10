using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Player
{
    // Walk/sprint/crouch/jump + mouse-look controller, plus bhop-style air
    // control (see HandleMove). Reads the New Input System's devices
    // directly (Keyboard.current / Mouse.current) so there's no Input
    // Actions asset to configure yet -- good enough until movement needs
    // to be swappable (e.g. jail state freezing the player).
    //
    // NetworkBehaviour + isOwned guard (Stage 4): every connected client
    // has a full copy of every player's GameObject, but only *one* of
    // them is "yours" (isOwned true) -- this component only ever reads
    // input/moves the CharacterController for that one copy. The
    // resulting position/rotation reaches everyone else via a
    // NetworkTransform component (Inspector-only, no code) riding
    // alongside this on the Player prefab, set to Client Authority so the
    // owner's own locally-simulated movement is what gets broadcast
    // (matches this controller already being fully client-predicted, no
    // server reconciliation) rather than the server re-simulating it.
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : NetworkBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float crouchSpeed = 2.5f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField, Range(0.3f, 1f)] private float crouchHeightRatio = 0.55f;
        // See DelayedLaunch's own comment -- CharacterController.isGrounded
        // lags a frame or more behind an actual launch, so IsJumpPending
        // needs to stay true a little past the real jump for the Animator
        // lie to hold through that gap.
        [SerializeField] private float groundedLiePadding = 0.15f;

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
        // Horizontal-only, matching what the animator cares about (a
        // straight-up jump shouldn't blend into a run just because
        // verticalVelocity is large) -- PlayerAnimationDriver reads this
        // each frame to blend Idle/Walk/Run.
        public float HorizontalSpeed => horizontalVelocity.magnitude;
        public bool IsGrounded => controller.isGrounded;
        // True from the moment a jump is triggered until the real launch
        // velocity actually applies (see jumpPending below) --
        // PlayerAnimationDriver reports Grounded as false to the Animator
        // for this whole window, even though the character is physically
        // still touching the ground during the anticipation squat.
        // Without that, the Jump state's own Grounded-gated exit
        // transitions (back to Idle/Walk/Run) fire immediately during the
        // squat itself, since nothing else distinguishes "still
        // squatting, haven't left yet" from "already landed" -- both are
        // genuinely Grounded=true.
        public bool IsJumpPending => jumpPending;
        // Time from launch to the peak of the arc (v=0), assuming flat
        // ground -- PlayerAnimationDriver doubles this for the full
        // up-then-down cycle length, to rescale the Jump clip's playback
        // speed so its authored poses land at the same point in the real
        // physics arc regardless of how jumpHeight/gravity are tuned,
        // instead of playing at a fixed 1x speed that only happens to
        // match one specific set of values.
        public float JumpApexTime => Mathf.Sqrt(jumpHeight * -2f * gravity) / -gravity;
        // Fired the instant a jump is triggered, not polled -- a jump is a
        // single-frame event (the space press), not a state, so a bool
        // property would need its own separate "have I already told the
        // animator about this jump" bookkeeping. PlayerAnimationDriver just
        // subscribes and fires the Jump trigger straight from this.
        public event System.Action Jumped;

        // True once Jumped has fired for the current press and until the
        // actual upward velocity has been applied -- guards against a
        // second space press mid-anticipation (see ScheduleJumpLaunch)
        // re-triggering an overlapping jump before the first one has even
        // left the ground.
        private bool jumpPending;
        // Set by ScheduleJumpLaunch during the *same* Jumped dispatch it
        // was called from -- if nothing sets it (e.g. no
        // PlayerAnimationDriver is attached), HandleMove falls back to
        // launching immediately, so jumping still works standalone.
        private bool jumpScheduled;

        // Server-set, client-visible -- true while PoliceAI has this
        // specific player in custody (Stage 4's per-player jail/catch
        // handling; see RoundManager). Only the owner's own Update loop
        // needs to check it, but it's a SyncVar (not server-only state)
        // since a caught player's own client is exactly who needs to stop
        // processing input.
        [SyncVar] public bool IsFrozen;

        // Local-only (deliberately NOT synced, unlike IsFrozen) -- set by
        // InventoryScreenUI while the Tab / steal screen is up so mouse
        // movement drives the cursor instead of the camera, and WASD
        // doesn't walk you around blind. You're still fully present in the
        // world (a rival can still tase you mid-rearrange); this just
        // parks your own input.
        public bool LookSuppressed { get; set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            standHeight = controller.height;
            standCenter = controller.center;
            if (cameraTransform != null) standCameraY = cameraTransform.localPosition.y;
        }

        // OnStartAuthority, not OnEnable -- this component is enabled on
        // every client's copy of every player (including remote ones you
        // don't control), but the cursor should only lock for the one
        // copy that's actually yours. Mirror guarantees isOwned/authority
        // is already correctly set by the time this fires.
        public override void OnStartAuthority()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        // Called by GameFlowManager right after it teleports this player
        // via NetworkTransform's CmdTeleport/ServerTeleport. Without this,
        // verticalVelocity keeps whatever it accumulated before the
        // teleport (gravity runs every frame regardless of position, and
        // controller.isGrounded can't yet know it's standing on solid
        // ground again the instant after an externally-set position) --
        // HandleMove then applies that stale, often quite negative
        // velocity on the very next frame, pulling the character down
        // through the floor right after landing at the correct spot.
        // Confirmed bug: a brand-new connection's first-ever placement
        // (falling from its default spawn transform for the one frame
        // GameFlowManager deliberately waits before teleporting it) sank
        // well below the intended height immediately after teleporting.
        public void ResetMotion()
        {
            verticalVelocity = 0f;
            horizontalVelocity = Vector3.zero;
        }

        // OnStartClient (not OnStartAuthority) -- this needs to run for
        // every copy on every client, including the non-owned ones, since
        // it's specifically *disabling* things for those. Without this,
        // every client ends up with one active Camera/AudioListener per
        // connected player instead of just their own -- Unity picks
        // whichever one renders last as what actually reaches the screen,
        // which in practice ends up being the same one on every client
        // (confirmed bug: host's own view got hijacked by the joining
        // client's camera, making it look like the host couldn't control
        // anything -- they could, they just couldn't see it).
        public override void OnStartClient()
        {
            if (isOwned) return;

            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.enabled = false;

            AudioListener listener = GetComponentInChildren<AudioListener>(true);
            if (listener != null) listener.enabled = false;
        }

        private void Update()
        {
            // Remote players' copies exist so NetworkTransform has
            // something to write synced positions into -- they must never
            // also read local input/run their own physics simulation, or
            // every client would see every player jitter between their
            // own dead-reckoned guess and everyone else's simultaneously
            // self-moved position.
            if (!isOwned) return;
            if (IsFrozen) return;

            // While the inventory / steal screen is open the mouse is the
            // cursor, so only mouse-look is parked -- you can still walk,
            // sprint, jump and crouch (deliberate: keep moving toward the
            // exit while you sort loot). IsFrozen (jail) still stops
            // everything.
            if (!LookSuppressed) HandleLook();
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

            // Skip the camera bob while the inventory screen owns the
            // camera (PlayerCameraRig has it detached) -- the body still
            // crouches, but the camera write would just be fought by the
            // rig every LateUpdate.
            if (cameraTransform == null || LookSuppressed) return;
            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = standCameraY - heightDelta;
            cameraTransform.localPosition = camPos;
        }

        // Called by PlayerAnimationDriver, synchronously from within its
        // own Jumped handler, once it knows how long the jump clip's
        // anticipation/squat portion will actually take to play at
        // whatever speed it's using -- keeps the character's feet on the
        // ground until that finishes, instead of leaving the ground on
        // the very first frame while the animation is still mid-squat.
        // Always routes through the coroutine (even for delay <= 0) so
        // the post-launch grounded-lie padding below applies uniformly
        // regardless of whether a delay was actually needed.
        public void ScheduleJumpLaunch(float delay)
        {
            jumpScheduled = true;
            StartCoroutine(DelayedLaunch(delay));
        }

        private IEnumerator DelayedLaunch(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // A car impact mid-anticipation disables this component for
            // the ragdoll stun -- the coroutine itself keeps running
            // regardless (Unity doesn't pause coroutines just because
            // their component is disabled), so without this check a
            // pending jump would silently apply a stale launch velocity
            // the moment the player regains control after the stun ends.
            // jumpPending still has to be cleared here even though the
            // launch itself is being skipped -- otherwise it stays stuck
            // true forever, and PlayerAnimationDriver reports
            // Grounded=false to the Animator permanently from then on.
            if (!enabled)
            {
                jumpPending = false;
                yield break;
            }

            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // CharacterController.isGrounded only updates on the *next*
            // Move() call -- for at least a frame after the line above,
            // sometimes more depending on exactly how this coroutine's
            // resumption lines up against HandleMove's own Update, it can
            // still report stale "grounded" data even though the launch
            // velocity has already been applied. Keep lying about
            // Grounded for a short cushion past the real launch so the
            // Jump state's exit transitions can't fire on that stale
            // read, same bug as the squat itself just shifted later.
            if (groundedLiePadding > 0f) yield return new WaitForSeconds(groundedLiePadding);

            jumpPending = false;
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

                if (!jumpPending && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    jumpPending = true;
                    jumpScheduled = false;
                    Jumped?.Invoke();

                    // No listener scheduled a delayed launch (e.g. no
                    // PlayerAnimationDriver is attached to sync it against
                    // an anticipation animation) -- jump immediately
                    // rather than silently doing nothing.
                    if (!jumpScheduled) ScheduleJumpLaunch(0f);
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

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

        [SerializeField] private float groundAcceleration = 60f;
        // Hold Space to auto-jump the instant you land (CS-style bhop /
        // autohop). Off = jump only on the initial press.
        [SerializeField] private bool holdToAutoHop = true;

        // Bhop-style air control: id Software's classic "air-accelerate"
        // formula, tuned toward Source values. `airWishSpeed` is a tight
        // per-tick cap on how much speed you can add *in the current
        // strafe direction* -- kept small on purpose so holding W in the
        // air doesn't accelerate you; you gain speed by air-strafing
        // (turning the view while holding a strafe key), which keeps
        // adding into the new direction on top of what's already there.
        // `airAcceleration` is high so each strafing tick actually reaches
        // that cap. No cap on total speed, by design (gameplay-design.md's
        // Movement section: a skill-ceiling reward, not a menu toggle).
        [SerializeField] private float airAcceleration = 100f;
        [SerializeField] private float airWishSpeed = 1.0f;
        // Hard ceiling on horizontal speed -- air-strafing can't push you
        // past this. ~2x sprint reads as "clearly bhopping" without
        // getting silly. Set very high to remove the cap.
        [SerializeField] private float maxAirSpeed = 16f;

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
        // True for the ~frame between triggering a jump and the
        // CharacterController actually registering as airborne
        // (isGrounded lags a frame). Guards against the same grounded
        // window triggering a second jump, and lets PlayerAnimationDriver
        // report Grounded=false to the Animator immediately so the Jump
        // state can start without waiting on isGrounded to catch up.
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

        private bool jumpPending;
        private float jumpPendingSince;

        // Server-set, client-visible -- a general "can't act at all" flag,
        // synced since whoever this is set on needs their own client to
        // stop processing input. Jail no longer uses this (a jailed
        // player can walk around their cell -- see SpectatingFrozen
        // below for why that still needs pausing sometimes); nothing
        // currently sets this true, but it's kept as the general-purpose
        // freeze other systems (CarryController, InventoryScreenUI,
        // PlayerRagdoll) already read, for whatever future case needs a
        // real full stop.
        [SyncVar] public bool IsFrozen;

        // Local-only (deliberately NOT synced, unlike IsFrozen) -- set by
        // InventoryScreenUI while the Tab / steal screen is up so mouse
        // movement drives the cursor instead of the camera, and WASD
        // doesn't walk you around blind. You're still fully present in the
        // world (a rival can still tase you mid-rearrange); this just
        // parks your own input.
        public bool LookSuppressed { get; set; }

        // Local-only, same shape as LookSuppressed -- set by
        // SpectatorController while actively spectating (T key). Your
        // camera view has been handed off to someone else's chase-cam
        // entirely at that point, so walking/looking with your own body
        // would just happen off-screen and out of your control; this
        // parks input the same way LookSuppressed does, just for both
        // look and movement instead of only look.
        public bool SpectatingFrozen { get; set; }

        // Local-only, same shape as SpectatingFrozen -- set by
        // ExitCarState while seated at the exit waiting to actually get
        // away. Deliberately NOT the synced IsFrozen flag: PlayerRagdoll.
        // EndRagdoll refuses to hand control back while IsFrozen is true,
        // and a player who gets hit by a car while seated here needs
        // that stun to recover completely normally (control genuinely
        // returning), not stay stuck frozen afterward -- confirmed bug
        // when this used IsFrozen instead.
        public bool ExitCarFrozen { get; set; }

        // Set by CarryController on the owner while hauling a downed rival.
        // Walk/sprint stay normal; this just kills air control and autohop
        // so you can't bhop a body around the map.
        public bool CarryingSomething { get; set; }

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

            // Plural, not just the first match -- SpectatorController adds
            // a second Camera/AudioListener pair (its own chase-cam) to
            // this same prefab, and GetComponentInChildren<T> singular
            // would only disable whichever of the two happens to come
            // first in the hierarchy, potentially leaving the other (e.g.
            // the normal FP camera) live on every other client's copy of
            // this player. Harmless either way for the spectator camera
            // specifically (it starts disabled and only ever gets enabled
            // by its own owning client), but this stays correct regardless
            // of hierarchy order.
            foreach (Camera cam in GetComponentsInChildren<Camera>(true)) cam.enabled = false;
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
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
            if (IsFrozen || SpectatingFrozen || ExitCarFrozen) return;

            // While the inventory / steal screen is open the mouse is the
            // cursor, so only mouse-look is parked -- you can still walk,
            // sprint, jump and crouch (deliberate: keep moving toward the
            // exit while you sort loot).
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

            // jumpPending only exists to bridge the frame or two before
            // isGrounded catches up to the launch -- clear it the moment
            // we're actually airborne, with a hard timeout as a backstop.
            if (jumpPending && (!grounded || Time.time - jumpPendingSince > 0.3f)) jumpPending = false;

            // Autohop is disabled while carrying a body -- a single
            // deliberate jump is fine, chaining hops isn't.
            bool autoHop = holdToAutoHop && !CarryingSomething;
            bool wantJump = grounded && !jumpPending &&
                (autoHop ? Keyboard.current.spaceKey.isPressed : Keyboard.current.spaceKey.wasPressedThisFrame);

            if (wantJump)
            {
                // Instant -- the velocity applies this frame, no waiting
                // on any animation. The Jumped event lets the animator
                // react but never gates this. Ground friction is
                // deliberately skipped this frame so a bhop keeps its
                // carried horizontal speed straight through the hop.
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpPending = true;
                jumpPendingSince = Time.time;
                Jumped?.Invoke();
            }
            else if (grounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;

                float targetSpeed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * targetSpeed, groundAcceleration * Time.deltaTime);
            }
            else if (!CarryingSomething)
            {
                float currentSpeedInWishDir = Vector3.Dot(horizontalVelocity, wishDir);
                float addSpeedCap = airWishSpeed - currentSpeedInWishDir;
                if (addSpeedCap > 0f)
                {
                    float accelSpeed = Mathf.Min(airAcceleration * airWishSpeed * Time.deltaTime, addSpeedCap);
                    horizontalVelocity += wishDir * accelSpeed;
                }

                // Hard ceiling -- air-strafing keeps redirecting your
                // velocity but can't push the magnitude past this.
                if (horizontalVelocity.sqrMagnitude > maxAirSpeed * maxAirSpeed)
                {
                    horizontalVelocity = horizontalVelocity.normalized * maxAirSpeed;
                }
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 move = horizontalVelocity;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }
    }
}

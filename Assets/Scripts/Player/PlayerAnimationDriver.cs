using Mirror;
using UnityEngine;

namespace RobEveryone.Player
{
    // Drives the skin's Animator from FirstPersonController's movement
    // state -- Speed blends Idle/Walk/Run, Grounded gates whether Jump can
    // play, and Jump fires the instant a jump is actually triggered rather
    // than being polled. Lives on the skin instance the same way
    // PlayerRagdoll's bones do (found fresh each spawn, since the selected
    // skin/Animator differ per playthrough) -- see PlayerSkinSpawner for
    // why the Animator drives that shared, always-visible skin rather than
    // some owner-only rig: a networked observer needs to see the *same*
    // walk/run/jump animation this local player sees, not a separate
    // first-person-only puppet.
    //
    // PlayerRagdoll disables this same Animator component for the
    // duration of a stun and re-enables it afterward -- without that, a
    // ragdolled bone's physics-driven Transform would get overwritten
    // every frame by whatever this driver's parameters say the idle/walk
    // pose should be, the exact bug DisableAnimator was originally written
    // to prevent back when the Animator had nothing driving it at all.
    //
    // Networking (Stage 4): the skin/Animator instance is spawned fresh
    // per-player at runtime (PlayerSkinSpawner), so there's no fixed
    // Animator reference to hand Mirror's stock NetworkAnimator component
    // in the Inspector -- this drives the sync by hand instead. The
    // continuous Speed/Grounded blend uses SyncVars with this
    // component's Sync Direction set to Client To Server in the
    // Inspector (owner writes them directly, matching how NetworkTransform
    // already treats this same client-authoritative movement); the
    // one-shot Jump trigger uses a Command+ClientRpc round trip instead,
    // since a SyncVar can only replicate *state*, not a fire-once event.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(PlayerSkinSpawner))]
    public class PlayerAnimationDriver : NetworkBehaviour
    {
        // Drag the same Jump clip used in the Animator Controller's Jump
        // state here too -- its authored length is what HandleJumped
        // rescales against the real physics air time, so the clip's own
        // "leaving the ground" pose lands at the actual moment of launch
        // instead of wherever a fixed 1x playback speed happens to put it.
        [SerializeField] private AnimationClip jumpClip;

        // How much of jumpClip's own timeline is the anticipation/squat
        // before the character actually leaves the ground, as a fraction
        // of the clip's total length (0.2 = squat is the first 20%).
        // Scrub the clip in its Import Settings preview to find this by
        // eye -- there's no way to read it back out of the imported clip
        // automatically, since Unity doesn't tag "this is the liftoff
        // frame" on its own.
        [SerializeField, Range(0f, 0.9f)] private float jumpAnticipationFraction = 0.2f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private static readonly int JumpParam = Animator.StringToHash("Jump");
        private static readonly int JumpSpeedParam = Animator.StringToHash("JumpSpeed");

        private FirstPersonController firstPersonController;
        private PlayerSkinSpawner skinSpawner;
        private Animator animator;

        // Client-authoritative (Sync Direction: Client To Server on this
        // component in the Inspector) -- the owner writes these directly
        // every frame in Update, same as NetworkTransform already does
        // for this player's position. Non-owner copies read them in
        // ApplyRemoteAnimatorState instead of touching
        // firstPersonController at all (that controller's Update doesn't
        // even run on a non-owned copy -- see its own isOwned guard).
        [SyncVar(hook = nameof(OnSyncedSpeedChanged))] private float syncedSpeed;
        [SyncVar(hook = nameof(OnSyncedGroundedChanged))] private bool syncedGrounded;

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            skinSpawner = GetComponent<PlayerSkinSpawner>();
        }

        private void Start()
        {
            // Doesn't guarantee success -- see TryResolveAnimator's own
            // comment. Every place below that needs the Animator calls it
            // again instead of trusting a one-shot field set here, so a
            // skin that arrives later (the common case for a non-owner)
            // still gets picked up instead of this driver being
            // permanently stuck unable to touch the Animator.
            TryResolveAnimator();
        }

        // The skin (and its Animator) isn't guaranteed to exist yet the
        // first time this runs -- the owner's own copy spawns its skin
        // synchronously in OnStartLocalPlayer, but a non-owner's copy only
        // gets one once its cosmetics SyncVars finish a real Client ->
        // Server -> other Clients round trip (PlayerSkinSpawner's
        // OnCosmeticsChanged), which routinely takes longer than a single
        // frame. Resolving lazily like this, from every call site that
        // needs animator instead of just once in Start, means a skin that
        // shows up late still gets wired up correctly the next time any
        // of them run, rather than this driver silently never animating a
        // remote player for the rest of its lifetime (confirmed bug: a
        // joining client's character stuck permanently in whatever pose
        // it spawned in, on every other client's screen).
        private bool TryResolveAnimator()
        {
            if (animator != null) return true;
            if (skinSpawner.SkinInstance == null) return false;

            animator = skinSpawner.SkinInstance.GetComponentInChildren<Animator>(true);
            if (animator == null) return false;

            if (isOwned) firstPersonController.Jumped += HandleJumped;
            return true;
        }

        private void OnDestroy()
        {
            if (firstPersonController != null) firstPersonController.Jumped -= HandleJumped;
        }

        private void Update()
        {
            if (!TryResolveAnimator() || !animator.enabled) return;
            if (!isOwned) return; // non-owner copies are driven by the SyncVar hooks below instead

            animator.SetFloat(SpeedParam, firstPersonController.HorizontalSpeed);

            // Lie about Grounded during the jump anticipation window --
            // see IsJumpPending's own comment for why: the character is
            // genuinely still touching the ground during the squat, but
            // the Jump state's exit transitions can't tell that apart
            // from an actual landing unless we hide it from them too.
            bool grounded = firstPersonController.IsGrounded && !firstPersonController.IsJumpPending;
            animator.SetBool(GroundedParam, grounded);

            // Client-authoritative writes -- only meaningful because this
            // component's Sync Direction is set to Client To Server in
            // the Inspector; a plain [SyncVar] would silently be ignored
            // coming from a non-server caller otherwise.
            syncedSpeed = firstPersonController.HorizontalSpeed;
            syncedGrounded = grounded;
        }

        private void OnSyncedSpeedChanged(float _, float newValue)
        {
            if (isOwned || !TryResolveAnimator()) return;
            animator.SetFloat(SpeedParam, newValue);
        }

        private void OnSyncedGroundedChanged(bool _, bool newValue)
        {
            if (isOwned || !TryResolveAnimator()) return;
            animator.SetBool(GroundedParam, newValue);
        }

        private void HandleJumped()
        {
            if (!TryResolveAnimator() || !animator.enabled) return;

            float delay = ComputeJumpTiming(out float speedMultiplier);

            animator.SetFloat(JumpSpeedParam, speedMultiplier);
            animator.SetTrigger(JumpParam);
            firstPersonController.ScheduleJumpLaunch(delay);

            // Tell the server, which relays to every *other* client
            // (includeOwner: false -- this client already triggered its
            // own copy above, an Rpc echo back to itself would double it
            // up a frame later).
            CmdNotifyJumped(speedMultiplier);
        }

        // Split out of HandleJumped so both the locally-triggering owner
        // and the RPC-driven remote copies compute the exact same delay
        // from the exact same speedMultiplier, rather than each side
        // deriving it independently and risking drift.
        private float ComputeJumpTiming(out float speedMultiplier)
        {
            speedMultiplier = 1f;
            if (jumpClip == null) return 0f;

            float realAirTime = firstPersonController.JumpApexTime * 2f;
            float airborneFraction = 1f - jumpAnticipationFraction;
            if (realAirTime <= 0f || airborneFraction <= 0f) return 0f;

            speedMultiplier = (airborneFraction * jumpClip.length) / realAirTime;
            if (speedMultiplier <= 0f)
            {
                speedMultiplier = 1f;
                return 0f;
            }

            return (jumpAnticipationFraction * jumpClip.length) / speedMultiplier;
        }

        [Command]
        private void CmdNotifyJumped(float speedMultiplier)
        {
            RpcPlayJump(speedMultiplier);
        }

        [ClientRpc(includeOwner = false)]
        private void RpcPlayJump(float speedMultiplier)
        {
            if (!TryResolveAnimator()) return;

            animator.SetFloat(JumpSpeedParam, speedMultiplier);
            animator.SetTrigger(JumpParam);
        }
    }
}

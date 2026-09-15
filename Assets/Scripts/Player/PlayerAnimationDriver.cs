using Mirror;
using UnityEngine;

namespace RobEveryone.Player
{
    // Which one-shot arm animation to fire on the Animator's Action
    // layer. The int values ARE the ActionType blend-tree thresholds the
    // PlayerAnimatorBuilder wires up -- keep them in sync with that tool.
    public enum PlayerActionAnim
    {
        PickUp = 0,      // grabbing loot or hoisting a downed rival
        Shoot = 1,       // Tranq Gun / Taser point-and-zap
        Swing = 2,       // Bat / Hammer melee (and thrown items, close enough)
        ReceiveHit = 3,  // non-ragdoll flinch -- reserved, nothing fires it yet
        // Issue #50: the arms-only jump overlay used while bhopping/moving
        // -- fired instead of the base layer's full-body Jump state so the
        // legs stay in the Walk/Run blend the whole time. See
        // PlayerAnimationDriver.HandleJumped/ShouldUseArmsOnlyJump.
        Jump = 4,
    }

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

        // How fast the upper-body Action layer fades in when a one-shot
        // fires and back out when it's done (weight units per second).
        [SerializeField] private float actionBlendSpeed = 8f;

        // Issue #50: how fast a player has to be moving at the moment they
        // jump for it to route through the arms-only PlayerActionAnim.Jump
        // overlay instead of the full-body base-layer Jump state. Small,
        // not zero -- FirstPersonController.HorizontalSpeed is rarely
        // EXACTLY 0 even standing still (tiny residual input, floating
        // point drift), so a hard > 0f check would flicker between the two
        // on what should read as a clean standing jump.
        [SerializeField] private float movingJumpSpeedThreshold = 0.1f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private static readonly int JumpParam = Animator.StringToHash("Jump");
        private static readonly int JumpSpeedParam = Animator.StringToHash("JumpSpeed");
        private static readonly int CarryingParam = Animator.StringToHash("Carrying");
        private static readonly int ActionParam = Animator.StringToHash("Action");
        private static readonly int ActionTypeParam = Animator.StringToHash("ActionType");

        private FirstPersonController firstPersonController;
        private PlayerSkinSpawner skinSpawner;
        private CarryController carryController;
        private Animator animator;
        // The "Action" layer (upper-body one-shots) -- index resolved once
        // the Animator exists; -1 until then / if the controller has no
        // such layer (an un-rebuilt controller still animates, just
        // without the arm one-shots).
        private int actionLayerIndex = -1;

        // Client-authoritative (Sync Direction: Client To Server on this
        // component in the Inspector) -- the owner writes these directly
        // every frame in Update, same as NetworkTransform already does
        // for this player's position. Non-owner copies read them in
        // ApplyRemoteAnimatorState instead of touching
        // firstPersonController at all (that controller's Update doesn't
        // even run on a non-owned copy -- see its own isOwned guard).
        [SyncVar(hook = nameof(OnSyncedSpeedChanged))] private float syncedSpeed;
        [SyncVar(hook = nameof(OnSyncedGroundedChanged))] private bool syncedGrounded;

        // The one correct source for "how fast/grounded is this player
        // right now" regardless of which copy you're asking -- owner
        // reads its own live FirstPersonController (never stale), a
        // remote copy reads the synced values instead (its own
        // FirstPersonController.Update never runs, so those fields
        // would just be frozen at their spawn defaults). PlayerFootstepAudio
        // is built on these rather than duplicating this branch itself.
        public float EffectiveSpeed => isOwned ? firstPersonController.HorizontalSpeed : syncedSpeed;
        public bool EffectiveGrounded => isOwned ? firstPersonController.IsGrounded : syncedGrounded;

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            skinSpawner = GetComponent<PlayerSkinSpawner>();
            carryController = GetComponent<CarryController>();
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

            actionLayerIndex = animator.GetLayerIndex("Action");
            if (actionLayerIndex >= 0) animator.SetLayerWeight(actionLayerIndex, 0f);

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

            // Carrying is server-authoritative and already replicated
            // (CarryController.carried is a plain SyncVar), so every copy
            // -- owner and observers -- can read it straight off without
            // this driver syncing anything itself.
            animator.SetBool(CarryingParam, carryController != null && carryController.IsCarrying);

            // Fade the upper-body Action layer in while its one-shot is
            // playing and back out once the state machine has fallen back
            // to the empty default. Runs on every copy so observers see
            // the pickup/shoot/swing an Rpc fired the same as the owner.
            UpdateActionLayerWeight();

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

            // Issue #50: a bhop/moving jump plays an arms-only overlay on
            // the Action layer instead of snapping the whole body into the
            // base layer's Jump pose -- the legs just keep running the
            // whole time. Reuses the exact upper-body-one-shot machinery
            // already built for PickUp/Shoot/Swing (PlayAction handles its
            // own local-trigger + Command/ClientRpc relay identically to
            // those) rather than inventing a parallel system, per the
            // issue's own framing. A standing-still jump, or jumping while
            // carrying (which already has its own distinct full-body
            // transition -- see PlayerAnimatorBuilder.BuildBaseLayer's
            // jumpToCarry), is unchanged: the ordinary full-body Jump
            // state below, exactly as before this issue.
            if (ShouldUseArmsOnlyJump())
            {
                PlayAction(PlayerActionAnim.Jump);
                return;
            }

            // The jump physics are instant now (FirstPersonController
            // applies the launch the same frame Space goes down) -- this
            // only fires the animation. JumpSpeed rescales the clip so it
            // roughly matches the real air time regardless of how
            // jumpHeight/gravity are tuned; the anticipation squat plays
            // while the character is already rising, which is a fine
            // trade for the input never feeling delayed. Set
            // jumpAnticipationFraction to 0 in the Inspector if you'd
            // rather skip the squat entirely.
            float speedMultiplier = ComputeJumpSpeedMultiplier();

            animator.SetFloat(JumpSpeedParam, speedMultiplier);
            animator.SetTrigger(JumpParam);

            // Tell the server, which relays to every *other* client
            // (includeOwner: false -- this client already triggered its
            // own copy above, an Rpc echo back to itself would double it
            // up a frame later).
            CmdNotifyJumped(speedMultiplier);
        }

        // See movingJumpSpeedThreshold's own comment for the speed side of
        // this. Carrying is excluded regardless of speed -- it already has
        // its own distinct full-body carry-jump transition in the base
        // layer, and layering an arms overlay on top of hands already full
        // holding a downed player would look wrong. Falls back to false if
        // the controller has no Action layer at all (an un-rebuilt
        // PlayerAnimator.controller), same defensive pattern
        // TryResolveAnimator already uses elsewhere in this file --
        // PlayAction itself also no-ops without one, but checking here
        // too keeps this method's own name ("should") honest.
        private bool ShouldUseArmsOnlyJump()
        {
            return actionLayerIndex >= 0
                && firstPersonController.HorizontalSpeed > movingJumpSpeedThreshold
                && (carryController == null || !carryController.IsCarrying);
        }

        // Split out so the locally-triggering owner and the RPC-driven
        // remote copies compute the exact same multiplier rather than
        // each deriving it independently and risking drift.
        private float ComputeJumpSpeedMultiplier()
        {
            if (jumpClip == null) return 1f;

            float realAirTime = firstPersonController.JumpApexTime * 2f;
            float airborneFraction = 1f - jumpAnticipationFraction;
            if (realAirTime <= 0f || airborneFraction <= 0f) return 1f;

            float m = (airborneFraction * jumpClip.length) / realAirTime;
            return m > 0f ? m : 1f;
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

        // ---- upper-body one-shots (pick up / shoot / swing) ------------

        // Called by the owner's own controllers (Interactor, CarryController,
        // SabotageUseController) the frame an action is committed locally.
        // Fires the clip here immediately for responsiveness, then relays
        // to every other client the same way HandleJumped does.
        public void PlayAction(PlayerActionAnim anim)
        {
            if (!isOwned) return;
            TriggerAction(anim);
            CmdPlayAction((int)anim);
        }

        [Command]
        private void CmdPlayAction(int anim) => RpcPlayAction(anim);

        [ClientRpc(includeOwner = false)]
        private void RpcPlayAction(int anim) => TriggerAction((PlayerActionAnim)anim);

        private void TriggerAction(PlayerActionAnim anim)
        {
            if (!TryResolveAnimator() || actionLayerIndex < 0) return;
            animator.SetFloat(ActionTypeParam, (int)anim);
            animator.SetTrigger(ActionParam);
        }

        private void UpdateActionLayerWeight()
        {
            if (actionLayerIndex < 0) return;

            // The Action state (and its blend tree) carry the "Action"
            // tag; the fall-back "None" state is empty and untagged.
            // We hold full weight through the first ~85% of the clip,
            // then let the weight fade carry the arm back to the base
            // pose over its tail -- so the crossback blends Action ->
            // base directly and never routes through the empty None
            // state (which writes nothing and would pop).
            AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(actionLayerIndex);
            bool inAction = cur.IsTag("Action") && (cur.normalizedTime % 1f) < 0.85f;
            if (!inAction && animator.IsInTransition(actionLayerIndex))
            {
                inAction = animator.GetNextAnimatorStateInfo(actionLayerIndex).IsTag("Action");
            }

            float target = inAction ? 1f : 0f;
            float current = animator.GetLayerWeight(actionLayerIndex);
            if (!Mathf.Approximately(current, target))
            {
                animator.SetLayerWeight(actionLayerIndex,
                    Mathf.MoveTowards(current, target, actionBlendSpeed * Time.deltaTime));
            }
        }
    }
}

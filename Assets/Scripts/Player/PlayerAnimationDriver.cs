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
    // some owner-only rig: a future networked observer needs to see the
    // *same* walk/run/jump animation this local player sees, not a
    // separate first-person-only puppet.
    //
    // PlayerRagdoll disables this same Animator component for the
    // duration of a stun and re-enables it afterward -- without that, a
    // ragdolled bone's physics-driven Transform would get overwritten
    // every frame by whatever this driver's parameters say the idle/walk
    // pose should be, the exact bug DisableAnimator was originally written
    // to prevent back when the Animator had nothing driving it at all.
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(PlayerSkinSpawner))]
    public class PlayerAnimationDriver : MonoBehaviour
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

        private void Awake()
        {
            firstPersonController = GetComponent<FirstPersonController>();
            skinSpawner = GetComponent<PlayerSkinSpawner>();
        }

        private void Start()
        {
            // Start, not Awake -- guarantees PlayerSkinSpawner's own Awake
            // (which does the actual Instantiate) has already run.
            if (skinSpawner.SkinInstance == null) return;

            animator = skinSpawner.SkinInstance.GetComponentInChildren<Animator>(true);
            if (animator == null) return;

            firstPersonController.Jumped += HandleJumped;
        }

        private void OnDestroy()
        {
            if (firstPersonController != null) firstPersonController.Jumped -= HandleJumped;
        }

        private void Update()
        {
            if (animator == null || !animator.enabled) return;

            animator.SetFloat(SpeedParam, firstPersonController.HorizontalSpeed);

            // Lie about Grounded during the jump anticipation window --
            // see IsJumpPending's own comment for why: the character is
            // genuinely still touching the ground during the squat, but
            // the Jump state's exit transitions can't tell that apart
            // from an actual landing unless we hide it from them too.
            bool grounded = firstPersonController.IsGrounded && !firstPersonController.IsJumpPending;
            animator.SetBool(GroundedParam, grounded);
        }

        private void HandleJumped()
        {
            if (animator == null || !animator.enabled) return;

            // Only the *airborne* portion of the clip (after the squat)
            // needs to match realAirTime -- scaling the squat along with
            // it was the bug in the first version of this: it compressed
            // the anticipation pose too, but the actual upward velocity
            // was still applying on the very first frame, so the
            // character left the ground before the squat had time to
            // read as a squat at all. Delaying the real launch (below)
            // until the squat's own real-time duration has elapsed is
            // what actually fixes that.
            float delay = 0f;
            if (jumpClip != null)
            {
                float realAirTime = firstPersonController.JumpApexTime * 2f;
                float airborneFraction = 1f - jumpAnticipationFraction;
                if (realAirTime > 0f && airborneFraction > 0f)
                {
                    float speedMultiplier = (airborneFraction * jumpClip.length) / realAirTime;
                    if (speedMultiplier > 0f)
                    {
                        animator.SetFloat(JumpSpeedParam, speedMultiplier);
                        delay = (jumpAnticipationFraction * jumpClip.length) / speedMultiplier;
                    }
                }
            }

            animator.SetTrigger(JumpParam);
            firstPersonController.ScheduleJumpLaunch(delay);
        }
    }
}

using UnityEngine;

namespace RobEveryone.Player
{
    // Interval-based footstep audio, timed off PlayerAnimationDriver's
    // EffectiveSpeed/EffectiveGrounded -- already consistent on every
    // client (owner's own live movement, or the synced replica for a
    // remote copy), so this needs no networking of its own: every
    // client independently derives the same "footstep now" timing off
    // the same already-correct signal, the same way HomeownerAI's state
    // color-tint or PickupItem's despawn already react locally to synced
    // state rather than needing a dedicated RPC per occurrence.
    //
    // Picks a clip pool by comparing the current speed against
    // FirstPersonController's own tuned walk/crouch/sprint speeds
    // (exposed read-only there) rather than needing a separate synced
    // "which gait" flag -- the speed value alone already tells you which
    // one a player's currently in.
    [RequireComponent(typeof(PlayerAnimationDriver))]
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip[] walkClips;
        [SerializeField] private AudioClip[] sprintClips;
        [SerializeField] private AudioClip[] crouchClips;

        [SerializeField] private float walkStepInterval = 0.5f;
        [SerializeField] private float sprintStepInterval = 0.42f;
        [SerializeField] private float crouchStepInterval = 0.7f;

        // Below this, treat the player as standing still -- no steps,
        // regardless of small residual horizontalVelocity noise.
        [SerializeField] private float minSpeedToStep = 0.5f;

        [SerializeField, Range(0f, 1f)] private float volume = 0.3f;
        [SerializeField] private float maxAudibleDistance = 20f;

        private PlayerAnimationDriver animDriver;
        private FirstPersonController fpc;
        private AudioSource audioSource;
        // An absolute timestamp, not a per-frame countdown -- deliberately
        // never reset when the player stops/goes airborne (see Update),
        // so a brief tap-stop-tap of the movement keys still respects the
        // cooldown from the last real footstep instead of re-arming to
        // "ready to fire instantly" on every single tap. Confirmed bug
        // with a countdown-reset-to-zero approach: rapid W/S or A/D
        // tapping span-fired a footstep on nearly every tap, since each
        // stop reset the countdown to 0 and the very next moving frame
        // read that as "already overdue."
        private float nextStepTime;

        private void Awake()
        {
            animDriver = GetComponent<PlayerAnimationDriver>();
            fpc = GetComponent<FirstPersonController>();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D -- nearby rivals hear footsteps positionally
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = maxAudibleDistance;
        }

        private void Update()
        {
            float speed = animDriver.EffectiveSpeed;

            if (!animDriver.EffectiveGrounded || speed < minSpeedToStep) return;
            if (Time.time < nextStepTime) return;

            (AudioClip[] pool, float interval) = ResolveGait(speed);
            nextStepTime = Time.time + interval;
            PlayRandomClip(pool);
        }

        private (AudioClip[] pool, float interval) ResolveGait(float speed)
        {
            float sprintThreshold = (fpc.WalkSpeed + fpc.SprintSpeed) * 0.5f;
            float crouchThreshold = (fpc.CrouchSpeed + fpc.WalkSpeed) * 0.5f;

            if (speed >= sprintThreshold) return (sprintClips, sprintStepInterval);
            if (speed <= crouchThreshold) return (crouchClips, crouchStepInterval);
            return (walkClips, walkStepInterval);
        }

        private void PlayRandomClip(AudioClip[] pool)
        {
            if (pool == null || pool.Length == 0) return;
            AudioClip clip = pool[Random.Range(0, pool.Length)];
            audioSource.PlayOneShot(clip, volume);
        }
    }
}

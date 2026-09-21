using System.Collections.Generic;
using Mirror;
using RobEveryone.Audio;
using RobEveryone.Core;
using RobEveryone.Inventory;
using RobEveryone.Round;
using UnityEngine;
using UnityEngine.AI;

namespace RobEveryone.AI
{
    public enum PoliceState { Patrol, Respond, Searching, Chase, Returning }

    // Patrol -> Respond -> Chase -> Catch. Doesn't listen for
    // HomeownerAI.OnAlertRaised itself -- PoliceDispatcher is the sole
    // subscriber and decides who actually responds to a given alert (see
    // RespondTo below), so a single break-in doesn't pull every officer
    // on the map off patrol at once. Once responding, uses its own
    // vision check to actually spot a player and start a real chase.
    // Catching hands off to RoundManager.NotifyPlayerCaught -> JailState.
    // EnterJail (Stage 7 Jail & Bail) -- a reversible jailed+frozen state,
    // not a permanent freeze/round-ending event for everyone.
    //
    // Networking (Stage 4): originally a single hand-placed scene object
    // (NetworkIdentity added in the Inspector -- Mirror auto-spawns
    // scene-placed identities when the server starts). Stage 7 Milestone D
    // adds PoliceDispatcher, which runtime-Instantiates + NetworkServer.
    // Spawns more of these from a real prefab as alerts come in -- see
    // OnStartServer's roundManager self-heal and RespondTo below, both
    // added specifically so a dispatched instance (no per-instance
    // Inspector wiring possible) still works identically to the original
    // hand-placed one. A dispatched instance also self-destructs once it
    // walks itself back to its own spawn point after giving up
    // (MarkDispatched/PoliceState.Returning) -- the original hand-placed
    // officer(s) never do this, they just resume patrolling forever. All
    // the actual AI logic (isServer-gated) checks every connected player
    // (PlayerInventory.AllPlayers) instead of one hardcoded target, and
    // State is a SyncVar so every client's Animator/Speed feed and Scene
    // gizmo stay correct without re-deriving the state machine themselves.
    // Milestone E (night mode): every agent.speed assignment goes through
    // EffectiveSpeed, CanSee's distance check through the same IsNight
    // multiplier, and UpdateChase's lose-interest timer too -- faster,
    // sees farther, and gives up chasing more slowly at night.
    [RequireComponent(typeof(NavMeshAgent))]
    public class PoliceAI : NetworkBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorSpeedParam = "Speed";

        [SerializeField] private List<Transform> patrolPoints = new();
        [SerializeField] private float patrolSpeed = 3.5f;
        [SerializeField] private float chaseSpeed = 6f;

        [SerializeField] private float viewDistance = 12f;
        [SerializeField] private float viewAngle = 90f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        [SerializeField] private float catchDistance = 2.2f;
        [SerializeField] private float loseInterestTime = 4f;

        // Issue #71: a dispatched officer's walk home is a real backstop
        // against a genuine NavMeshAgent quirk (see UpdateReturning's own
        // comment) -- same "don't fully trust the system, verify with a
        // timeout" approach GameFlowManager.TeleportPlayerTo already uses
        // for a similarly hard-to-fully-explain case. A single flat
        // constant doesn't work, though -- real playtest logging showed
        // a genuinely-still-walking officer (remainingDistance counting
        // down normally, ~81 units out) getting killed early by a flat
        // 20s budget that was simply too short for the distance. The
        // per-trip budget is computed from the actual distance instead
        // (ReturnToSpawn) -- these two just tune that computation.
        [SerializeField] private float returningTimeoutSafetyMultiplier = 2f;
        [SerializeField] private float returningTimeoutMinimum = 15f;

        [SerializeField] private float searchDuration = 3f;
        [SerializeField] private float searchSweepAngle = 45f;
        [SerializeField] private float searchSweepFrequency = 2f;

        // Night round (Milestone E) -- faster, sees farther, and chases
        // longer after losing line-of-sight. Proposed defaults, tune by
        // playtesting.
        [SerializeField] private float nightSpeedMultiplier = 1.4f;
        [SerializeField] private float nightViewDistanceMultiplier = 1.5f;
        [SerializeField] private float nightLoseInterestMultiplier = 2f;

        [SerializeField] private float animatorSpeedSmoothTime = 0.1f;

        private NavMeshAgent agent;
        private Vector3 lastPosition;
        private float animatorSpeedSmoothed;
        private float animatorSpeedSmoothVelocity;
        private int patrolIndex;
        private float timeSinceSeenPlayer;
        private float searchTimer;
        private float searchBaseYaw;
        // Whoever's currently being chased/responded to -- picked fresh
        // each time a chase starts (EnterChase/RespondTo), since with
        // multiple players it's no longer a fixed single target.
        private Transform chaseTarget;

        // [field: SyncVar], not [SyncVar] directly -- SyncVar only
        // applies to an actual field, and on an auto-property the
        // `field:` target attaches it to the compiler-generated backing
        // field instead of the property declaration itself.
        [field: SyncVar]
        public PoliceState State { get; private set; } = PoliceState.Patrol;

        // Played via RpcChaseStarted from EnterChase -- State itself has
        // no hook (see the [field: SyncVar] comment above), so a chase
        // starting is broadcast the same way PlayerImpactRelay's own
        // knockdown/sound is: an explicit ClientRpc rather than a synced-
        // field hook.
        [SerializeField] private AudioClip[] chaseStartClips;
        [SerializeField, Range(0f, 1f)] private float chaseStartVolume = 0.7f;

        // Set by PoliceDispatcher right after spawning a new instance --
        // a dispatched officer heads home and despawns once it gives up
        // (see UpdateSearching's timeout branch), instead of resuming
        // patrol forever like the original hand-placed officer(s), which
        // never call this and so leave isDispatched false.
        private bool isDispatched;
        private Vector3 dispatchSpawnPosition;

        [Server]
        public void MarkDispatched(Vector3 spawnPosition)
        {
            isDispatched = true;
            dispatchSpawnPosition = spawnPosition;
        }

        private bool IsNight => GameFlowManager.Instance != null && GameFlowManager.Instance.IsNightRound;
        private float EffectiveSpeed(float baseSpeed) => IsNight ? baseSpeed * nightSpeedMultiplier : baseSpeed;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            lastPosition = transform.position;
        }

        // Self-heals roundManager -- a hand-placed officer already has
        // this wired in the Inspector, but a freshly-dispatched instance
        // (PoliceDispatcher.Instantiate, Milestone D) can't have a
        // per-instance Inspector reference set for it, since it doesn't
        // exist at edit time.
        public override void OnStartServer()
        {
            if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();

            agent.speed = EffectiveSpeed(patrolSpeed);
            if (patrolPoints.Count > 0)
            {
                agent.SetDestination(patrolPoints[0].position);
            }
        }

        private void Update()
        {
            // Feed the Animator from actual observed movement (position
            // delta over time), not agent.velocity -- confirmed bug:
            // agent.velocity read correctly on the host (server and
            // client are the same process there, so the NavMeshAgent
            // really is simulating locally) but stayed permanently zero
            // on every other client, since all of this script's actual
            // pathing (SetDestination, the whole state machine below) is
            // isServer-gated -- a remote client's own NavMeshAgent copy
            // never receives a destination and never simulates anything,
            // even though the object visibly moves (NetworkTransform
            // interpolates the position independently of any local
            // NavMeshAgent state). Measuring the position change directly
            // is correct everywhere regardless of what's actually driving
            // the Transform. Still SmoothDamp'd before reaching the
            // Animator, though -- confirmed bug: the raw instantaneous
            // value is noisy on a remote client specifically, since
            // NetworkTransform interpolates position between network
            // snapshots rather than moving it smoothly every single
            // frame, so an unsmoothed per-frame delta jumps around and
            // reads as jerky blending.
            if (animator != null)
            {
                float rawSpeed = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
                animatorSpeedSmoothed = Mathf.SmoothDamp(animatorSpeedSmoothed, rawSpeed, ref animatorSpeedSmoothVelocity, animatorSpeedSmoothTime);
                animator.SetFloat(animatorSpeedParam, animatorSpeedSmoothed);
            }
            lastPosition = transform.position;

            if (!isServer) return;

            switch (State)
            {
                case PoliceState.Patrol:
                    UpdatePatrol();
                    break;
                case PoliceState.Respond:
                    UpdateRespond();
                    break;
                case PoliceState.Searching:
                    UpdateSearching();
                    break;
                case PoliceState.Chase:
                    UpdateChase();
                    break;
                case PoliceState.Returning:
                    UpdateReturning();
                    break;
            }
        }

        // Called by PoliceDispatcher -- either the single closest
        // available officer redirected to a fresh alert, or a
        // newly-spawned one sent straight at the alert it was spawned
        // for. No longer self-triggered off HomeownerAI.OnAlertRaised;
        // PoliceDispatcher is the sole subscriber to that event now.
        [Server]
        public void RespondTo(Vector3 lastKnownPosition, PlayerInventory blamed)
        {
            if (State == PoliceState.Chase) return; // already got someone, don't interrupt that

            // A framed alert (the Alarm Clock) already knows exactly who
            // to blame -- skip straight to a real chase instead of just
            // moving toward a position and hoping the next vision-cone
            // check happens to spot them.
            if (blamed != null)
            {
                EnterChase(blamed.transform);
                return;
            }

            State = PoliceState.Respond;
            agent.speed = EffectiveSpeed(chaseSpeed);
            agent.SetDestination(lastKnownPosition);
        }

        // TEMPORARY (issue #71 debugging) -- throttles UpdatePatrol's own
        // diagnostic log to once every 3s, same convention as
        // UpdateReturning used last round. Testing a direct report that
        // it's specifically a *redirected* (isDispatched=False) officer
        // that gets stuck, not a freshly-dispatched one -- UpdatePatrol
        // currently has zero logging at all to confirm or rule that out.
        private float nextPatrolLogAt;

        private void UpdatePatrol()
        {
            Transform seen = FindVisiblePlayer();
            if (seen != null)
            {
                EnterChase(seen);
                return;
            }

            if (Time.time >= nextPatrolLogAt)
            {
                nextPatrolLogAt = Time.time + 3f;
                Debug.Log($"[Issue71] {name} (netId={netId}) UpdatePatrol: isDispatched={isDispatched}, " +
                    $"patrolPoints.Count={patrolPoints.Count}, patrolIndex={patrolIndex}, pathPending={agent.pathPending}, " +
                    $"pathStatus={agent.pathStatus}, remainingDistance={agent.remainingDistance}, isOnNavMesh={agent.isOnNavMesh}, " +
                    $"hasPath={agent.hasPath}, velocity={agent.velocity}");
            }

            if (patrolPoints.Count == 0) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                agent.SetDestination(PickNextPatrolPoint().position);
            }
        }

        // Random instead of sequential -- a fixed cycle order made patrol
        // routes fully predictable, easy to memorize and route around.
        // Excludes whatever patrolIndex currently is so it never
        // "re-picks" the point it's already standing at.
        private Transform PickNextPatrolPoint()
        {
            if (patrolPoints.Count == 1) return patrolPoints[0];

            int next;
            do
            {
                next = Random.Range(0, patrolPoints.Count);
            } while (next == patrolIndex);

            patrolIndex = next;
            return patrolPoints[patrolIndex];
        }

        private void UpdateRespond()
        {
            Transform seen = FindVisiblePlayer();
            if (seen != null)
            {
                EnterChase(seen);
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                // Arrived at the last-known position with nothing found --
                // pause and look around for a bit rather than immediately
                // giving up, so the response doesn't feel instant/robotic.
                State = PoliceState.Searching;
                searchTimer = searchDuration;
                searchBaseYaw = transform.eulerAngles.y;
                agent.ResetPath();
            }
        }

        private void UpdateSearching()
        {
            Transform seen = FindVisiblePlayer();
            if (seen != null)
            {
                EnterChase(seen);
                return;
            }

            // Sweep the facing left/right around the direction it arrived
            // facing, rather than spinning continuously, so it visually
            // reads as "looking around" for the player.
            float sweep = Mathf.Sin(searchTimer * searchSweepFrequency) * searchSweepAngle;
            transform.rotation = Quaternion.Euler(0f, searchBaseYaw + sweep, 0f);

            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
            {
                GiveUpAndResumeOrReturn();
            }
        }

        // Whatever made an officer give up on a player (a search timing
        // out, a chase losing its target -- see UpdateChase) funnels
        // through here: a dispatched officer heads home instead of
        // resuming patrol, exactly once, from a single shared decision
        // point. Originally only wired into UpdateSearching's own
        // timeout, which meant UpdateChase's own "target went null"
        // branch bypassed it entirely and just set State = Patrol
        // directly -- confirmed bug: a dispatched officer whose target
        // disconnected mid-chase landed in Patrol with no patrol points
        // of its own (the base prefab's are deliberately empty, see
        // OnStartServer's own comment) and stood frozen forever,
        // permanently occupying a slot against PoliceDispatcher's cap.
        private void GiveUpAndResumeOrReturn()
        {
            if (isDispatched)
            {
                ReturnToSpawn();
                return;
            }

            agent.speed = EffectiveSpeed(patrolSpeed);
            State = PoliceState.Patrol;
            if (patrolPoints.Count > 0)
            {
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }

        // A dispatched officer doesn't just resume patrolling forever --
        // it walks itself back to wherever it was spawned and despawns
        // once it arrives (UpdateReturning below), freeing its slot
        // against PoliceDispatcher's cap for a future alert.
        private void ReturnToSpawn()
        {
            State = PoliceState.Returning;
            agent.speed = EffectiveSpeed(patrolSpeed);
            agent.SetDestination(dispatchSpawnPosition);
            returningStartedAt = Time.time;

            // Confirmed via real playtest logging: a flat timeout killed
            // an officer that was genuinely still walking home correctly
            // (remainingDistance counting down normally) just because the
            // trip was long. Scale the budget to the actual distance
            // instead of guessing one constant that's simultaneously too
            // short for a far spawn point and needlessly long for a
            // close one.
            float distance = Vector3.Distance(transform.position, dispatchSpawnPosition);
            float expectedTravelTime = distance / Mathf.Max(EffectiveSpeed(patrolSpeed), 0.1f);
            returningTimeoutThisTrip = Mathf.Max(returningTimeoutMinimum, expectedTravelTime * returningTimeoutSafetyMultiplier);
        }

        private float returningStartedAt;
        private float returningTimeoutThisTrip;

        private void UpdateReturning()
        {
            // Still worth abandoning the walk home for a fresh sighting --
            // same as every other non-Chase state.
            Transform seen = FindVisiblePlayer();
            if (seen != null)
            {
                EnterChase(seen);
                return;
            }

            if (Time.time - returningStartedAt >= returningTimeoutThisTrip)
            {
                NetworkServer.Destroy(gameObject);
                return;
            }

            if (agent.pathPending) return;

            // Defensive: a dispatch spawn point that isn't actually
            // reachable on the baked NavMesh (off-mesh, inside geometry,
            // a station point placed before the map's own NavMesh existed,
            // etc.) makes SetDestination fail silently -- no exception,
            // just an invalid/partial path with remainingDistance stuck
            // reporting Infinity, so the plain "did we arrive" check below
            // would never pass on its own. Real playtest logging (issue
            // #71) also caught this happening even with SetDestination
            // returning true, isOnNavMesh true, and pathStatus reporting
            // PathComplete -- a genuine NavMeshAgent quirk on a long trip
            // that neither this check nor the arrival check below was
            // ever going to catch on their own, since Unity itself was
            // reporting the path as healthy. returningTimeoutThisTrip
            // above is the real backstop for that case.
            bool arrived = agent.remainingDistance <= agent.stoppingDistance;
            bool pathFailed = agent.pathStatus != NavMeshPathStatus.PathComplete;
            if (arrived || pathFailed)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        private void UpdateChase()
        {
            if (chaseTarget == null)
            {
                GiveUpAndResumeOrReturn();
                return;
            }

            // Path toward the target's live position every frame, regardless
            // of whether they're still visible this exact frame -- gating
            // the destination on visibility created a feedback loop where
            // losing the cone for even one frame left Police facing the
            // wrong way with nothing to correct it.
            agent.SetDestination(chaseTarget.position);

            // Catching is pure proximity, not gated on the vision cone --
            // standing on top of someone is a catch regardless of exactly
            // which way Police is facing at that instant. It IS gated on
            // there being no wall in between, though (issue #4 fix):
            // Vector3.Distance is straight-line, so on a thin wall between
            // two rooms it can read well under catchDistance while Police
            // is actually navigating the long way around, on the other
            // side of solid geometry, with no path and nothing visible to
            // the player at all -- reproducing as "caught with no officer
            // in sight." Reuses the same obstructionMask CanSee already
            // raycasts against below.
            if (Vector3.Distance(transform.position, chaseTarget.position) <= catchDistance
                && !IsBlockedByGeometry(chaseTarget))
            {
                CatchPlayer(chaseTarget);
                return;
            }

            if (CanSee(chaseTarget))
            {
                timeSinceSeenPlayer = 0f;
                return;
            }

            timeSinceSeenPlayer += Time.deltaTime;
            float effectiveLoseInterestTime = IsNight ? loseInterestTime * nightLoseInterestMultiplier : loseInterestTime;
            if (timeSinceSeenPlayer >= effectiveLoseInterestTime)
            {
                State = PoliceState.Respond;
            }
        }

        private void EnterChase(Transform target)
        {
            bool wasAlreadyChasing = State == PoliceState.Chase;
            chaseTarget = target;
            State = PoliceState.Chase;
            agent.speed = EffectiveSpeed(chaseSpeed);
            timeSinceSeenPlayer = 0f;

            // Only the moment a chase actually starts, not every frame it
            // continues (EnterChase re-fires each time a fresh target is
            // (re)acquired while already chasing -- e.g. CanSee reacquiring
            // the same or a different player mid-chase).
            if (!wasAlreadyChasing) RpcChaseStarted();
        }

        [ClientRpc]
        private void RpcChaseStarted()
        {
            SfxPlayer.PlayRandomAt(chaseStartClips, transform.position, chaseStartVolume);
        }

        // Checks every connected player rather than one hardcoded target --
        // returns the first one currently visible. Called every frame
        // Police isn't already chasing someone specific.
        private Transform FindVisiblePlayer()
        {
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                if (player == null) continue;
                if (CanSee(player.transform)) return player.transform;
            }
            return null;
        }

        // Issue #4 fix. Plain "is anything solid in the way" check, from
        // the same eye reference point CanSee uses -- deliberately doesn't
        // reuse CanSee itself, since that also gates on viewDistance/
        // viewAngle/facing, none of which should matter for a point-blank
        // proximity catch (see UpdateChase's own comment on why catching
        // is proximity-only in the first place).
        private bool IsBlockedByGeometry(Transform target)
        {
            if (target == null || eye == null) return false;

            Vector3 toTarget = target.position - eye.position;
            float distance = toTarget.magnitude;
            if (distance <= 0.01f) return false;

            // Same "a hit on the player's own collider doesn't count as
            // blocked" carve-out CanSee's raycast makes below -- otherwise
            // the player's own body would block their own catch the
            // instant Police is close enough for it to matter.
            if (Physics.Raycast(eye.position, toTarget.normalized, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<PlayerInventory>() == null;
            }

            return false;
        }

        private bool CanSee(Transform target)
        {
            if (target == null || eye == null) return false;

            Vector3 toPlayer = target.position - eye.position;
            float distance = toPlayer.magnitude;
            float effectiveViewDistance = IsNight ? viewDistance * nightViewDistanceMultiplier : viewDistance;
            if (distance > effectiveViewDistance) return false;

            // Angle is measured on the horizontal plane only. Comparing the
            // full 3D direction would make height differences (a low eye
            // vs. a head-height target) count against the cone, which gets
            // worse the closer the target stands -- backwards from how a
            // vision cone should behave.
            Vector3 flatForward = eye.forward;
            flatForward.y = 0f;
            Vector3 flatToPlayer = toPlayer;
            flatToPlayer.y = 0f;
            float angle = Vector3.Angle(flatForward, flatToPlayer);
            if (angle > viewAngle * 0.5f) return false;

            if (Physics.Raycast(eye.position, toPlayer.normalized, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<PlayerInventory>() != null;
            }

            return true;
        }

        [Server]
        private void CatchPlayer(Transform target)
        {
            PlayerInventory caught = target.GetComponentInParent<PlayerInventory>();
            // Freezing now happens in JailState.EnterJail, triggered by
            // RoundManager.NotifyPlayerCaught below -- Jail & Bail means
            // this is no longer a permanent freeze, just the start of a
            // reversible jailed state.

            if (roundManager != null && caught != null)
            {
                roundManager.NotifyPlayerCaught(caught);
            }

            chaseTarget = null;
            State = PoliceState.Respond;
            agent.ResetPath();
        }

        // Draws the vision cone in red so it's visually distinct from a
        // Homeowner's cyan cone when both are selected in the Scene view.
        private void OnDrawGizmosSelected()
        {
            if (eye == null) return;

            Gizmos.color = Color.red;
            Vector3 leftEdge = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.up) * eye.forward;
            Vector3 rightEdge = Quaternion.AngleAxis(viewAngle * 0.5f, Vector3.up) * eye.forward;

            Gizmos.DrawRay(eye.position, eye.forward * viewDistance);
            Gizmos.DrawRay(eye.position, leftEdge * viewDistance);
            Gizmos.DrawRay(eye.position, rightEdge * viewDistance);
        }
    }
}

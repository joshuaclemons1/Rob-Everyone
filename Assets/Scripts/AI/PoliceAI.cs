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

        // Issue #77 follow-up: every officer currently in the scene,
        // server-side only -- same AllPlayers pattern PlayerInventory
        // already uses. RoundManager.NotifyPlayerCaught reads this to
        // broadcast AbandonChaseIfTargeting to whichever officer(s) are
        // actually chasing whoever just got caught (search-released or
        // jailed), not just whichever one happened to land the catch.
        public static readonly List<PoliceAI> AllOfficers = new();

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

            AllOfficers.Add(this);

            agent.speed = EffectiveSpeed(patrolSpeed);
            if (patrolPoints.Count > 0 && patrolPoints[0] != null)
            {
                agent.SetDestination(patrolPoints[0].position);
            }
        }

        public override void OnStopServer()
        {
            AllOfficers.Remove(this);
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

        private void UpdatePatrol()
        {
            Transform seen = FindVisiblePlayer();
            if (seen != null)
            {
                EnterChase(seen);
                return;
            }

            if (!HasAnyPatrolPoint()) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                Transform next = PickNextPatrolPoint();
                if (next != null) agent.SetDestination(next.position);
            }
        }

        // Issue #71: patrolPoints.Count alone isn't enough to know
        // whether an officer actually has anywhere to patrol to. The
        // base Police.prefab deliberately ships with 13 placeholder
        // slots, every one left unassigned ({fileID: 0}) -- a
        // freshly-dispatched instance (PoliceDispatcher.Instantiate) has
        // no patrol points of its own and isn't meant to try patrolling
        // at all, only to return to spawn once it gives up (see
        // GiveUpAndResumeOrReturn). A hand-placed officer's own scene-
        // level PrefabInstance overrides fill those same 13 slots with
        // real Transforms. Confirmed bug (a genuine regression from
        // issue #70's own fix): shrinking the *base* prefab's array to a
        // true empty list broke those scene-level overrides entirely --
        // Unity's Array.data[N] overrides can't attach to indices that
        // no longer exist in the base array, so every hand-placed
        // officer's patrolPoints read back as empty at runtime too.
        // Restored the base prefab's 13 placeholder slots and fixed the
        // code to check for an actually-assigned entry instead, so both
        // shapes (empty list, or a list of all-null placeholders) mean
        // "no patrol points" without needing the base array's length to
        // be zero.
        private bool HasAnyPatrolPoint()
        {
            foreach (Transform point in patrolPoints)
            {
                if (point != null) return true;
            }
            return false;
        }

        // Random instead of sequential -- a fixed cycle order made patrol
        // routes fully predictable, easy to memorize and route around.
        // Excludes whatever patrolIndex currently is so it never
        // "re-picks" the point it's already standing at. Skips null
        // entries (see HasAnyPatrolPoint's own comment) -- bounded
        // attempt count rather than an unbounded loop, purely as
        // insurance against every remaining candidate happening to be
        // null.
        private Transform PickNextPatrolPoint()
        {
            if (patrolPoints.Count == 1) return patrolPoints[0];

            int next = patrolIndex;
            for (int attempt = 0; attempt < patrolPoints.Count * 2; attempt++)
            {
                int candidate = Random.Range(0, patrolPoints.Count);
                if (candidate == patrolIndex || patrolPoints[candidate] == null) continue;

                next = candidate;
                break;
            }

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
            if (patrolPoints.Count > 0 && patrolPoints[patrolIndex] != null)
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

            // Issue #77 follow-up (real playtest): a just-caught player is
            // fully exempt from being caught again -- by this officer or
            // any other -- until PlayerInventory.IsCatchCooldownActive
            // clears. Confirmed real bug without this: a released
            // (empty-handed) catch resets this officer's own State to
            // Respond in CatchPlayer below, but nothing about the release
            // moved the player or made them any less visible, so the very
            // next frame's FindVisiblePlayer/EnterChase immediately
            // re-acquires them and re-triggers this same check -- an
            // infinite catch/release loop. Worse with two officers
            // converging on the same target at once: each one
            // independently runs that same loop, and every re-entry into
            // Chase re-fires RpcChaseStarted's "spotted you" audio (only
            // suppressed by wasAlreadyChasing when State doesn't actually
            // flip) -- the reported dueling, spamming "searching noise".
            // Skipping the catch here instead of just skipping
            // NotifyPlayerCaught downstream is what actually stops the
            // loop: State stays in Chase (no flip, no re-fired audio)
            // rather than bouncing through Respond every single frame.
            PlayerInventory targetInventory = chaseTarget.GetComponentInParent<PlayerInventory>();
            if (targetInventory != null && targetInventory.IsCatchCooldownActive)
            {
                timeSinceSeenPlayer = 0f;
                return;
            }

            // Issue #75/#76: a player who's committed to boarding the exit
            // van (ExitCarState.IsWaiting flips true the instant they
            // board, well before the vulnerable window's own
            // carWaitDuration actually elapses) is safe from a catch
            // outright -- same intent as the van's own safety window, just
            // enforced here too, since a straight proximity+geometry check
            // has zero awareness of it on its own. Confirmed real bug:
            // Police camping right next to the van could still land a
            // catch before that window ever got a chance to matter.
            // Reading it straight from the target rather than caching it
            // once in EnterChase -- boarding can happen well after a chase
            // already started. Also closes #76's "caught through the
            // van's own walls" report, whatever's actually causing a
            // seated player's model to clip through the geometry in the
            // first place -- this makes it moot regardless.
            ExitCarState exitState = chaseTarget.GetComponentInParent<ExitCarState>();
            if (exitState != null && exitState.IsWaiting)
            {
                timeSinceSeenPlayer = 0f;
                return;
            }

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

            // Issue #77 follow-up: a player mid-catch-cooldown is fully
            // invisible to every officer, not just whichever one actually
            // caught them -- AbandonChaseIfTargeting (broadcast from
            // RoundManager.NotifyPlayerCaught) is what breaks off an
            // *already*-chasing officer, but without this, any *other*
            // officer who simply walks within view during the cooldown
            // window would spot and start a brand new chase on them,
            // undoing the whole point of the window. Every caller of
            // CanSee (FindVisiblePlayer included) is always given a real
            // player's own Transform, so this is safe to check here
            // rather than duplicating it at every call site.
            PlayerInventory targetInventory = target.GetComponentInParent<PlayerInventory>();
            if (targetInventory != null && targetInventory.IsCatchCooldownActive) return false;

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

            // Issue #77 follow-up: NotifyPlayerCaught above already
            // broadcasts AbandonChaseIfTargeting to every officer whose
            // chaseTarget is this same player -- this officer included,
            // since chaseTarget is still set to `target` at the moment
            // that call runs. Deliberately not ALSO resetting
            // chaseTarget/State/agent's path here anymore: doing both
            // used to race, with this method's own trailing reset always
            // stomping straight back over whatever the broadcast had just
            // set (e.g. a dispatched officer's proper "head home"
            // Returning state getting silently overwritten back to plain
            // Respond a moment later).
        }

        // Issue #77 follow-up (real playtest): a player who just got
        // caught -- search-released or jailed -- needs every officer
        // actually chasing them to break off and head back to patrol
        // (or home, if dispatched) right away, not just whichever one
        // happened to land the catch. Without this, a second officer
        // converging on the same target kept right on chasing/standing
        // on top of a released player for the whole cooldown window
        // instead of giving them room to get away. Called from
        // RoundManager.NotifyPlayerCaught for every active officer; a
        // no-op for any officer not currently chasing this exact target.
        // Reuses GiveUpAndResumeOrReturn -- the same "resume a real
        // patrol route, or head home and despawn if dispatched" path a
        // natural lost-interest/search-timeout already uses, so a forced
        // abandon here looks identical to an organic one.
        [Server]
        public void AbandonChaseIfTargeting(Transform target)
        {
            if (chaseTarget == null || chaseTarget != target) return;

            chaseTarget = null;
            agent.ResetPath();
            GiveUpAndResumeOrReturn();
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

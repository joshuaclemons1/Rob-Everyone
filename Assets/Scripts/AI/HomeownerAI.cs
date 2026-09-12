using System;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace RobEveryone.AI
{
    public enum HomeownerState { Idle, Suspicious, Alerted }

    // Idle -> Suspicious -> Alerted state machine driven by a facing-direction
    // vision cone. Suspicion builds while the player is seen and decays while
    // they're not, so a quick peek through a doorway doesn't instantly bust
    // you. Alerted fires OnPoliceCalled once -- Stage 3c's Police AI will
    // listen for that instead of this script chasing anyone itself.
    //
    // Networking (Stage 4): server-only (isServer guard) -- the vision
    // check/suspicion math only ever runs on the server, which is what
    // decides State. Every client still needs to see the same color
    // change though (a bystander should see a Homeowner go red just as
    // reliably as the player being chased does), so State is now a
    // SyncVar with a hook driving the same bodyRenderer color logic that
    // used to live in SetState. With multiple players now able to be in
    // the same house at once, this checks every connected player instead
    // of one hardcoded target -- whichever is closest/visible first wins
    // the alert.
    // NetworkBehaviour, not MonoBehaviour -- [SyncVar]/[Server] only work
    // on a NetworkBehaviour, but this doesn't need its own NetworkIdentity:
    // it's nested inside a house prefab whose *root* has the identity, and
    // Mirror discovers NetworkBehaviour components anywhere in that same
    // hierarchy automatically (see HousePoolSpawner's own comment).
    [RequireComponent(typeof(NavMeshAgent))]
    public class HomeownerAI : NetworkBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private float viewDistance = 10f;
        [SerializeField] private float viewAngle = 60f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [SerializeField] private Animator animator;
        [SerializeField] private string animatorSpeedParam = "Speed";
        // Drives the "pointing" pose (a frozen first frame of the
        // Shoot_OneHanded clip) while Suspicious -- see the Animator
        // Controller setup for how this bool actually triggers that.
        [SerializeField] private string animatorSuspiciousParam = "Suspicious";

        [SerializeField] private float suspicionBuildRate = 1f;
        [SerializeField] private float suspicionDecayRate = 0.5f;
        [SerializeField] private float suspicionThreshold = 2f;

        // Auto-discovered in Awake if left empty -- the visible model
        // (BaseCharacter, a Quaternius rig swapped in for the old capsule
        // placeholder) can carry its own arbitrary number of renderers/
        // material slots, and hand-wiring those one at a time is easy to
        // get wrong (confirmed bug: this used to be a single hand-wired
        // Renderer still pointing at the old capsule's now-disabled
        // MeshRenderer, so color changes landed on a renderer nobody
        // could ever see). Leave this empty for "tint everything found
        // under this object"; populate it manually only to deliberately
        // exclude something (e.g. eyes) from the tint.
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Color idleColor = Color.black;
        [SerializeField] private Color suspiciousColor = Color.yellow;
        [SerializeField] private Color alertedColor = Color.red;

        // Same pattern as PoliceAI's own patrol -- cycles between these
        // points while Idle. Slower than Police's patrolSpeed (3.5) since
        // a Homeowner isn't hunting anyone, just puttering around their
        // own house.
        [SerializeField] private List<Transform> patrolPoints = new();
        [SerializeField] private float patrolSpeed = 2f;

        // Alerted behavior -- retreat to spawn (wherever this Homeowner
        // started, captured in Awake) and wait there rather than stand
        // and fight/look around. "Police clear the space" (AreaClear
        // below) is approximated as "no PoliceAI anywhere is still
        // actively responding/searching/chasing" -- there's only ever
        // one hand-placed officer right now, so this is really just
        // "that one officer's calmed down," but the check already
        // generalizes correctly once Milestone D's dispatch pooling adds
        // more. Combined with "not currently seeing the player" so this
        // doesn't calm down mid-chase just because Police hasn't reacted
        // yet on the very first frame.
        [SerializeField] private float alertedSearchDuration = 4f;

        // Suspicious behavior while not currently seeing whoever set them
        // off -- spins in place looking around instead of just standing
        // frozen facing one direction, giving up (back to Idle) after
        // this long of continuously not seeing them. Resets back to full
        // duration the instant they're seen again (Update), so this is
        // really "how long since I last actually saw them," not "how
        // long has Suspicious been active."
        [SerializeField] private float suspiciousSearchDuration = 3f;
        [SerializeField] private float suspiciousSpinSpeed = 180f;

        [SerializeField] private float animatorSpeedSmoothTime = 0.1f;

        private NavMeshAgent agent;
        private Vector3 lastPosition;
        private float animatorSpeedSmoothed;
        private float animatorSpeedSmoothVelocity;
        private int patrolIndex;
        private Vector3 spawnPosition;
        private float alertedSearchTimer;
        private float suspiciousSearchTimer;

        private float suspicion;

        [SyncVar(hook = nameof(OnStateChanged))]
        private HomeownerState state = HomeownerState.Idle;
        public HomeownerState State => state;

        // Server-only event -- PoliceAI subscribes to this on the server
        // copy only (see its own isServer guard), so this never needs to
        // fire on a client that isn't the server/host.
        public event Action OnPoliceCalled;

        // Static so any number of PoliceAI instances can respond without
        // being hand-wired to every homeowner in the Inspector. Carries the
        // player's last-known position at the moment of the call, plus who
        // to blame -- null for an organic, vision-cone-triggered alert
        // (every existing call site), a specific PlayerInventory for a
        // framed one (ForceAlert, e.g. the Alarm Clock).
        public static event Action<Vector3, PlayerInventory> OnAlertRaised;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            lastPosition = transform.position;
            spawnPosition = transform.position;

            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // Same reasoning as PoliceAI's own OnStartServer -- this is
        // nested inside a house prefab whose root NetworkIdentity is
        // already re-enabled by the time NetworkServer.SpawnObjects()
        // triggers this, so starting the patrol here (rather than Awake,
        // which runs before that) is safe and reliable.
        public override void OnStartServer()
        {
            agent.speed = patrolSpeed;
            if (patrolPoints.Count > 0) agent.SetDestination(patrolPoints[0].position);
        }

        private void Update()
        {
            // Feed the Animator from actual observed movement, not
            // agent.velocity -- same fix as PoliceAI's own (see its
            // comment): a remote client's NavMeshAgent never receives a
            // destination (all the actual pathing below is isServer-
            // gated), so its velocity would stay zero forever even
            // though the object visibly moves via NetworkTransform.
            // Runs before the isServer return so it applies to every
            // client, including the server/host itself. SmoothDamp'd
            // before reaching the Animator -- same as PoliceAI's own,
            // the raw instantaneous value is noisy on a remote client
            // (NetworkTransform interpolates position between network
            // snapshots, not smoothly every frame) and reads as jerky
            // blending otherwise.
            if (animator != null)
            {
                float rawSpeed = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
                animatorSpeedSmoothed = Mathf.SmoothDamp(animatorSpeedSmoothed, rawSpeed, ref animatorSpeedSmoothVelocity, animatorSpeedSmoothTime);
                animator.SetFloat(animatorSpeedParam, animatorSpeedSmoothed);
            }
            lastPosition = transform.position;

            if (!isServer) return;

            if (state == HomeownerState.Alerted)
            {
                UpdateAlerted();
                return;
            }

            Transform seenPlayer = FindVisiblePlayer();

            // Confirmed bug this replaces: Suspicious (yellow) kept
            // patrolling right through building suspicion, which read as
            // if nothing had actually been noticed. Stop and pay
            // attention instead -- face them directly while actually
            // seen, or spin around looking for them otherwise, giving up
            // (back to Idle) if suspiciousSearchDuration passes without
            // seeing them again.
            if (state == HomeownerState.Suspicious)
            {
                StopMoving();

                if (seenPlayer != null)
                {
                    FaceTarget(seenPlayer.position);
                    suspiciousSearchTimer = suspiciousSearchDuration;
                }
                else
                {
                    transform.Rotate(Vector3.up, suspiciousSpinSpeed * Time.deltaTime);

                    suspiciousSearchTimer -= Time.deltaTime;
                    if (suspiciousSearchTimer <= 0f)
                    {
                        suspicion = 0f;
                        SetState(HomeownerState.Idle, null);
                        ResumePatrol();
                        return;
                    }
                }
            }
            else
            {
                UpdatePatrol();
            }

            suspicion += (seenPlayer != null ? suspicionBuildRate : -suspicionDecayRate) * Time.deltaTime;
            suspicion = Mathf.Clamp(suspicion, 0f, suspicionThreshold);

            HomeownerState next = suspicion <= 0f ? HomeownerState.Idle
                : suspicion >= suspicionThreshold ? HomeownerState.Alerted
                : HomeownerState.Suspicious;

            SetState(next, seenPlayer);
        }

        private void StopMoving()
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        private void FaceTarget(Vector3 position)
        {
            Vector3 flatDirection = position - transform.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(flatDirection);
        }

        private void ResumePatrol()
        {
            if (patrolPoints.Count > 0) agent.SetDestination(patrolPoints[patrolIndex].position);
        }

        private void UpdatePatrol()
        {
            if (patrolPoints.Count == 0) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
        }

        // EnterAlerted already pointed the agent at spawnPosition -- the
        // NavMeshAgent keeps walking there on its own without needing a
        // fresh SetDestination every frame, and simply stops once it
        // arrives (nothing else needed for that part). This just watches
        // for "the coast is clear" (not currently seen AND no PoliceAI
        // still actively responding/searching/chasing anywhere) and
        // calms back down once that's been continuously true for
        // alertedSearchDuration.
        private void UpdateAlerted()
        {
            if (FindVisiblePlayer() != null || !AreaClear())
            {
                alertedSearchTimer = alertedSearchDuration;
                return;
            }

            alertedSearchTimer -= Time.deltaTime;
            if (alertedSearchTimer <= 0f)
            {
                Calm();
            }
        }

        // "Police clear the space" -- every PoliceAI in the scene is back
        // to plain Patrol, none still actively Responding/Searching/
        // Chasing. Only one hand-placed officer exists before Milestone
        // D's dispatch pooling, so today this really just means "that
        // one officer's calmed down," but it generalizes correctly once
        // more officers exist.
        private bool AreaClear()
        {
            foreach (PoliceAI police in FindObjectsByType<PoliceAI>(FindObjectsSortMode.None))
            {
                if (police.State != PoliceState.Patrol) return false;
            }
            return true;
        }

        // Nothing seen for the whole alertedSearchDuration -- give up and
        // resume normal life. suspicion is reset to 0 directly (not left
        // to decay normally) so this doesn't just immediately re-trigger
        // Suspicious/Alerted again next frame.
        private void Calm()
        {
            suspicion = 0f;
            SetState(HomeownerState.Idle, null);
            ResumePatrol();
        }

        // Sends this Homeowner fleeing home the instant Alerted starts
        // (SetState/ForceAlert both call this) -- a single SetDestination
        // is enough; the agent keeps walking there and stops on its own
        // once it arrives, no per-frame upkeep needed.
        private void EnterAlerted()
        {
            agent.SetDestination(spawnPosition);
            alertedSearchTimer = alertedSearchDuration;
        }

        // Checks every connected player (PlayerInventory.AllPlayers) rather
        // than one hardcoded target, since any of them could be standing in
        // this house's vision cone. Returns the first one seen -- with
        // several players in the same house at once, that's an arbitrary
        // but stable choice (Update() re-evaluates every frame anyway).
        private Transform FindVisiblePlayer()
        {
            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                if (player == null) continue;
                if (CanSee(player.transform)) return player.transform;
            }
            return null;
        }

        private bool CanSee(Transform target)
        {
            if (target == null || eye == null) return false;

            Vector3 toPlayer = target.position - eye.position;
            float distance = toPlayer.magnitude;
            if (distance > viewDistance) return false;

            // Angle is measured on the horizontal plane only -- see the
            // matching comment in PoliceAI.CanSeePlayer for why.
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
        private void SetState(HomeownerState next, Transform seenPlayer)
        {
            if (next == state) return;

            HomeownerState previous = state;
            state = next; // SyncVar assignment -- OnStateChanged fires on every client, including this one (server/host)

            if (next == HomeownerState.Alerted)
            {
                EnterAlerted();

                if (seenPlayer != null)
                {
                    Debug.Log($"{name} called the police!");
                    OnPoliceCalled?.Invoke();
                    OnAlertRaised?.Invoke(seenPlayer.position, null); // organic sighting -- nobody specifically blamed
                }
            }
            else if (next == HomeownerState.Suspicious)
            {
                // Defensive initialization -- in practice seenPlayer is
                // almost always non-null the instant this transition
                // happens (suspicion only rises while seen), which would
                // set this the same frame anyway, but this guarantees it
                // regardless so a null-seenPlayer edge case can't cause
                // an instant give-up on Suspicious's very first frame.
                suspiciousSearchTimer = suspiciousSearchDuration;
            }
            else if (next == HomeownerState.Idle && previous == HomeownerState.Suspicious)
            {
                // Suspicious stopped movement entirely (see Update) --
                // resume patrolling now that suspicion's fully decayed,
                // rather than staying frozen forever (agent.remainingDistance
                // reads as infinite with no path set, so UpdatePatrol's own
                // "arrived, pick a new point" check would otherwise never
                // fire again on its own).
                ResumePatrol();
            }
        }

        // Bypasses the private, vision-cone-gated SetState -- lets an
        // outside system (the Alarm Clock's detonation) force this
        // Homeowner straight to Alerted and blame a specific player,
        // rather than only reacting to its own sighting.
        [Server]
        public void ForceAlert(Vector3 position, PlayerInventory blamed)
        {
            state = HomeownerState.Alerted; // SyncVar assignment -- OnStateChanged fires on every client, same as SetState
            EnterAlerted();
            Debug.Log($"{name} called the police! (framed: {(blamed != null ? blamed.name : "nobody")})");
            OnPoliceCalled?.Invoke();
            OnAlertRaised?.Invoke(position, blamed);
        }

        // Runs on every client (server included) whenever the SyncVar
        // changes -- this is now the *only* place bodyRenderer gets
        // touched, so a bystander client sees the exact same color change
        // the server decided, rather than each client trying to (and
        // possibly failing to) re-derive it independently. Also drives
        // the Suspicious animator bool, same reasoning -- every client
        // needs to see the same pointing pose, not just the server.
        private void OnStateChanged(HomeownerState _, HomeownerState next)
        {
            if (animator != null) animator.SetBool(animatorSuspiciousParam, next == HomeownerState.Suspicious);

            if (bodyRenderers == null) return;

            Color color = next switch
            {
                HomeownerState.Suspicious => suspiciousColor,
                HomeownerState.Alerted => alertedColor,
                _ => idleColor,
            };

            foreach (Renderer renderer in bodyRenderers)
            {
                if (renderer == null) continue;
                // .materials (plural), not .material -- a rig can have
                // more than one material slot (body/eyes/clothes), and
                // this instantiates a unique copy per slot the first
                // time it's touched, same as .material already did for
                // the single-renderer case.
                foreach (Material material in renderer.materials)
                {
                    material.color = color;
                }
            }
        }

        // Draws the vision cone in the Scene view when this Homeowner is
        // selected, so viewAngle/viewDistance can be tuned by eye instead of
        // by guesswork. Horizontal-only approximation -- the runtime check
        // above uses the full 3D angle.
        private void OnDrawGizmosSelected()
        {
            if (eye == null) return;

            Gizmos.color = Color.cyan;
            Vector3 leftEdge = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.up) * eye.forward;
            Vector3 rightEdge = Quaternion.AngleAxis(viewAngle * 0.5f, Vector3.up) * eye.forward;

            Gizmos.DrawRay(eye.position, eye.forward * viewDistance);
            Gizmos.DrawRay(eye.position, leftEdge * viewDistance);
            Gizmos.DrawRay(eye.position, rightEdge * viewDistance);
        }
    }
}

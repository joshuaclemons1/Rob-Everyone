using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.Round;
using UnityEngine;
using UnityEngine.AI;

namespace RobEveryone.AI
{
    public enum PoliceState { Patrol, Respond, Searching, Chase }

    // Patrol -> Respond -> Chase -> Catch. Responds to any HomeownerAI going
    // Alerted via the static HomeownerAI.OnAlertRaised event -- no manual
    // wiring needed between homeowners and police. Once responding, uses its
    // own vision check to actually spot a player and start a real chase.
    // Catching freezes just that one player (see FirstPersonController's
    // IsFrozen SyncVar) rather than ending the round for everyone.
    //
    // Networking (Stage 4): Police is a single hand-placed scene object
    // (not spawned per-player), so it just needs a NetworkIdentity added
    // in the Inspector -- Mirror auto-spawns scene-placed identities when
    // the server starts, no NetworkServer.Spawn call needed here. All the
    // actual AI logic (isServer-gated) now checks every connected player
    // (PlayerInventory.AllPlayers) instead of one hardcoded target, and
    // State is a SyncVar so every client's Animator/Speed feed and Scene
    // gizmo stay correct without re-deriving the state machine themselves.
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

        [SerializeField] private float searchDuration = 3f;
        [SerializeField] private float searchSweepAngle = 45f;
        [SerializeField] private float searchSweepFrequency = 2f;

        private NavMeshAgent agent;
        private int patrolIndex;
        private float timeSinceSeenPlayer;
        private float searchTimer;
        private float searchBaseYaw;
        // Whoever's currently being chased/responded to -- picked fresh
        // each time a chase starts (EnterChase/HandleAlertRaised), since
        // with multiple players it's no longer a fixed single target.
        private Transform chaseTarget;

        // [field: SyncVar], not [SyncVar] directly -- SyncVar only
        // applies to an actual field, and on an auto-property the
        // `field:` target attaches it to the compiler-generated backing
        // field instead of the property declaration itself.
        [field: SyncVar]
        public PoliceState State { get; private set; } = PoliceState.Patrol;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            HomeownerAI.OnAlertRaised += HandleAlertRaised;
        }

        private void OnDisable()
        {
            HomeownerAI.OnAlertRaised -= HandleAlertRaised;
        }

        public override void OnStartServer()
        {
            agent.speed = patrolSpeed;
            if (patrolPoints.Count > 0)
            {
                agent.SetDestination(patrolPoints[0].position);
            }
        }

        private void Update()
        {
            // Feed the real-time NavMeshAgent speed into the Animator every
            // frame, on every client -- a NavMeshAgent's own movement is
            // synced to clients via the same NetworkTransform pattern as
            // everything else that moves, so agent.velocity reads
            // correctly everywhere, not just on the server.
            if (animator != null)
            {
                animator.SetFloat(animatorSpeedParam, agent.velocity.magnitude);
            }

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
            }
        }

        private void HandleAlertRaised(Vector3 lastKnownPosition, PlayerInventory blamed)
        {
            if (!isServer) return;
            if (State == PoliceState.Chase) return;

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
            agent.speed = chaseSpeed;
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

            if (patrolPoints.Count == 0) return;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
                agent.SetDestination(patrolPoints[patrolIndex].position);
            }
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
                agent.speed = patrolSpeed;
                State = PoliceState.Patrol;
                if (patrolPoints.Count > 0)
                {
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                }
            }
        }

        private void UpdateChase()
        {
            if (chaseTarget == null)
            {
                State = PoliceState.Patrol;
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
            // which way Police is facing at that instant.
            if (Vector3.Distance(transform.position, chaseTarget.position) <= catchDistance)
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
            if (timeSinceSeenPlayer >= loseInterestTime)
            {
                State = PoliceState.Respond;
            }
        }

        private void EnterChase(Transform target)
        {
            chaseTarget = target;
            State = PoliceState.Chase;
            agent.speed = chaseSpeed;
            timeSinceSeenPlayer = 0f;
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

        private bool CanSee(Transform target)
        {
            if (target == null || eye == null) return false;

            Vector3 toPlayer = target.position - eye.position;
            float distance = toPlayer.magnitude;
            if (distance > viewDistance) return false;

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
            FirstPersonController controller = target.GetComponentInParent<FirstPersonController>();
            if (controller != null) controller.IsFrozen = true; // SyncVar -- freezes input on that player's own client

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

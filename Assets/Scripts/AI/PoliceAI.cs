using System.Collections.Generic;
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
    // own vision check to actually spot the player and start a real chase.
    // Catching the player freezes them and ends the round immediately, since
    // caught players earn nothing for the round.
    [RequireComponent(typeof(NavMeshAgent))]
    public class PoliceAI : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
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

        // [field: SerializeField] so State shows up (read-only, updates live)
        // in the Inspector during Play mode -- select Police while testing
        // to watch it transition between states in real time.
        [field: SerializeField] public PoliceState State { get; private set; } = PoliceState.Patrol;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();

            // See the matching comment in HomeownerAI.Awake -- same
            // fallback, in case Police ever gets instantiated rather than
            // hand-placed in the scene.
            if (playerTarget == null)
            {
                PlayerInventory player = FindFirstObjectByType<PlayerInventory>();
                if (player != null) playerTarget = player.transform;
            }
        }

        private void OnEnable()
        {
            HomeownerAI.OnAlertRaised += HandleAlertRaised;
        }

        private void OnDisable()
        {
            HomeownerAI.OnAlertRaised -= HandleAlertRaised;
        }

        private void Start()
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
            // frame -- this alone drives Idle/Walk/Run through a Blend Tree,
            // so no per-state animation code is needed for patrol vs. chase.
            if (animator != null)
            {
                animator.SetFloat(animatorSpeedParam, agent.velocity.magnitude);
            }

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

        private void HandleAlertRaised(Vector3 lastKnownPosition)
        {
            if (State == PoliceState.Chase) return;

            State = PoliceState.Respond;
            agent.speed = chaseSpeed;
            agent.SetDestination(lastKnownPosition);
        }

        private void UpdatePatrol()
        {
            if (CanSeePlayer())
            {
                EnterChase();
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
            if (CanSeePlayer())
            {
                EnterChase();
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
            if (CanSeePlayer())
            {
                EnterChase();
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
            if (playerTarget == null) return;

            // Path toward the player's live position every frame, regardless
            // of whether CanSeePlayer() currently succeeds -- gating the
            // destination on visibility created a feedback loop where losing
            // the cone for even one frame (e.g. while NavMeshAgent's
            // rotation is still catching up to a fresh path) left Police
            // facing the wrong way with nothing to correct it.
            agent.SetDestination(playerTarget.position);

            // Catching is pure proximity, not gated on the vision cone --
            // standing on top of someone is a catch regardless of exactly
            // which way Police is facing at that instant.
            if (Vector3.Distance(transform.position, playerTarget.position) <= catchDistance)
            {
                CatchPlayer();
                return;
            }

            if (CanSeePlayer())
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

        private void EnterChase()
        {
            State = PoliceState.Chase;
            agent.speed = chaseSpeed;
            timeSinceSeenPlayer = 0f;
        }

        private bool CanSeePlayer()
        {
            if (playerTarget == null || eye == null) return false;

            Vector3 toPlayer = playerTarget.position - eye.position;
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

        private void CatchPlayer()
        {
            FirstPersonController controller = playerTarget.GetComponentInParent<FirstPersonController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            if (roundManager != null)
            {
                roundManager.NotifyPlayerCaught();
            }

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

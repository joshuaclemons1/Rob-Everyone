using System;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.AI
{
    public enum HomeownerState { Idle, Suspicious, Alerted }

    // Idle -> Suspicious -> Alerted state machine driven by a facing-direction
    // vision cone. Suspicion builds while the player is seen and decays while
    // they're not, so a quick peek through a doorway doesn't instantly bust
    // you. Alerted fires OnPoliceCalled once -- Stage 3c's Police AI will
    // listen for that instead of this script chasing anyone itself.
    public class HomeownerAI : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform eye;
        [SerializeField] private float viewDistance = 10f;
        [SerializeField] private float viewAngle = 60f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [SerializeField] private float suspicionBuildRate = 1f;
        [SerializeField] private float suspicionDecayRate = 0.5f;
        [SerializeField] private float suspicionThreshold = 2f;

        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color suspiciousColor = Color.yellow;
        [SerializeField] private Color alertedColor = Color.red;

        private float suspicion;

        [field: SerializeField] public HomeownerState State { get; private set; } = HomeownerState.Idle;
        public event Action OnPoliceCalled;

        // Static so any number of PoliceAI instances can respond without
        // being hand-wired to every homeowner in the Inspector. Carries the
        // player's last-known position at the moment of the call.
        public static event Action<Vector3> OnAlertRaised;

        private void Awake()
        {
            // Houses spawned at runtime by HousePoolSpawner (Stage 3g) can't
            // have this hand-dragged in the Inspector like the Stage 3a/3e
            // scene-placed houses could -- fall back to finding the player
            // automatically. Only one player exists pre-multiplayer (Stage 4).
            if (playerTarget == null)
            {
                PlayerInventory player = FindFirstObjectByType<PlayerInventory>();
                if (player != null) playerTarget = player.transform;
            }
        }

        private void Update()
        {
            if (State == HomeownerState.Alerted) return;

            bool seesPlayer = CanSeePlayer();
            suspicion += (seesPlayer ? suspicionBuildRate : -suspicionDecayRate) * Time.deltaTime;
            suspicion = Mathf.Clamp(suspicion, 0f, suspicionThreshold);

            HomeownerState next = suspicion <= 0f ? HomeownerState.Idle
                : suspicion >= suspicionThreshold ? HomeownerState.Alerted
                : HomeownerState.Suspicious;

            SetState(next);
        }

        private bool CanSeePlayer()
        {
            if (playerTarget == null || eye == null) return false;

            Vector3 toPlayer = playerTarget.position - eye.position;
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

        private void SetState(HomeownerState next)
        {
            if (next == State) return;

            State = next;

            if (bodyRenderer != null)
            {
                bodyRenderer.material.color = next switch
                {
                    HomeownerState.Suspicious => suspiciousColor,
                    HomeownerState.Alerted => alertedColor,
                    _ => idleColor,
                };
            }

            if (next == HomeownerState.Alerted)
            {
                Debug.Log($"{name} called the police!");
                OnPoliceCalled?.Invoke();
                OnAlertRaised?.Invoke(playerTarget.position);
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

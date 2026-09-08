using System;
using Mirror;
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
    public class HomeownerAI : NetworkBehaviour
    {
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

        [SyncVar(hook = nameof(OnStateChanged))]
        private HomeownerState state = HomeownerState.Idle;
        public HomeownerState State => state;

        // Server-only event -- PoliceAI subscribes to this on the server
        // copy only (see its own isServer guard), so this never needs to
        // fire on a client that isn't the server/host.
        public event Action OnPoliceCalled;

        // Static so any number of PoliceAI instances can respond without
        // being hand-wired to every homeowner in the Inspector. Carries the
        // player's last-known position at the moment of the call.
        public static event Action<Vector3> OnAlertRaised;

        private void Update()
        {
            if (!isServer) return;
            if (state == HomeownerState.Alerted) return;

            Transform seenPlayer = FindVisiblePlayer();
            suspicion += (seenPlayer != null ? suspicionBuildRate : -suspicionDecayRate) * Time.deltaTime;
            suspicion = Mathf.Clamp(suspicion, 0f, suspicionThreshold);

            HomeownerState next = suspicion <= 0f ? HomeownerState.Idle
                : suspicion >= suspicionThreshold ? HomeownerState.Alerted
                : HomeownerState.Suspicious;

            SetState(next, seenPlayer);
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

            state = next; // SyncVar assignment -- OnStateChanged fires on every client, including this one (server/host)

            if (next == HomeownerState.Alerted && seenPlayer != null)
            {
                Debug.Log($"{name} called the police!");
                OnPoliceCalled?.Invoke();
                OnAlertRaised?.Invoke(seenPlayer.position);
            }
        }

        // Runs on every client (server included) whenever the SyncVar
        // changes -- this is now the *only* place bodyRenderer gets
        // touched, so a bystander client sees the exact same color change
        // the server decided, rather than each client trying to (and
        // possibly failing to) re-derive it independently.
        private void OnStateChanged(HomeownerState _, HomeownerState next)
        {
            if (bodyRenderer == null) return;

            bodyRenderer.material.color = next switch
            {
                HomeownerState.Suspicious => suspiciousColor,
                HomeownerState.Alerted => alertedColor,
                _ => idleColor,
            };
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

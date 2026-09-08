using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.World;
using UnityEngine;

namespace RobEveryone.AI
{
    // Drives a fixed lap around a waypoint loop, then returns to its origin
    // point and despawns -- no vision, no reaction to the player, it just
    // follows its route (per design: "completely oblivious," a hazard to
    // dodge rather than an AI that hunts). CarSpawnManager owns deciding
    // when/whether to spawn one of these at all; this script only knows how
    // to drive once it exists.
    //
    // Networking (Stage 4): only the server actually runs the movement/
    // impact logic below (isServer guards) -- a NetworkTransform component
    // on the prefab (Inspector-only, no code) broadcasts the resulting
    // position/rotation to every client the same way it already does for
    // players, so this same script's Update() doesn't need a client-side
    // branch of its own. Without the guards, every client would also run
    // its own independent copy of the waypoint-following logic (redundant
    // at best, actively fighting the synced NetworkTransform position at
    // worst) and every client's own OnTriggerEnter would independently
    // (and wrongly) decide it was responsible for applying the impact.
    [RequireComponent(typeof(AudioSource))]
    public class CarDriver : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float turnSpeed = 120f;
        [SerializeField] private float waypointArrivalDistance = 1.5f;

        [SerializeField] private AudioClip hornClip;
        [SerializeField] private AudioClip yellClip;

        [SerializeField] private float impactForce = 60f;
        // Small on purpose -- this used to dominate the shove direction
        // (2) and made every hit look like it just popped the player
        // straight up in place. It's only meant to help the knockdown
        // topple, not compete with the actual horizontal shove.
        [SerializeField] private float impactUpwardBias = 0.7f;

        private List<Transform> waypoints;
        private Transform originPoint;
        private CarSpawnManager spawnManager;

        private int waypointIndex;
        private bool returningToOrigin;
        private AudioSource audioSource;

        // Called by CarSpawnManager right after Instantiate -- this is
        // per-spawn data (which lap, which origin, who to report back to),
        // not something to hand-configure per prefab in the Inspector.
        public void Init(List<Transform> lapWaypoints, Transform origin, CarSpawnManager manager)
        {
            waypoints = lapWaypoints;
            originPoint = origin;
            spawnManager = manager;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (!isServer) return;

            Transform target = CurrentTarget();
            if (target == null) return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= waypointArrivalDistance * waypointArrivalDistance)
            {
                AdvanceTarget();
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * (moveSpeed * Time.deltaTime);
        }

        private Transform CurrentTarget()
        {
            if (returningToOrigin) return originPoint;
            if (waypoints == null || waypointIndex >= waypoints.Count) return null;
            return waypoints[waypointIndex];
        }

        private void AdvanceTarget()
        {
            if (returningToOrigin)
            {
                spawnManager.NotifyCarDespawned(this);
                // NetworkServer.Destroy, not a plain Destroy -- this
                // GameObject was spawned over the network
                // (CarSpawnManager.NetworkServer.Spawn), so clients need
                // the matching despawn message or it'd linger as a ghost
                // on every connected client forever.
                NetworkServer.Destroy(gameObject);
                return;
            }

            waypointIndex++;
            if (waypoints == null || waypointIndex >= waypoints.Count)
            {
                returningToOrigin = true;
            }
        }

        // Oblivious means it doesn't brake or swerve -- but it still needs
        // to know it *hit* something, purely to trigger the honk/yell and
        // the player's knockback. Trigger, not solid collision, so it never
        // needs real physics interaction with the road/world.
        private void OnTriggerEnter(Collider other)
        {
            if (!isServer) return;

            PlayerInventory player = other.GetComponentInParent<PlayerInventory>();
            if (player == null) return;

            RpcPlayImpactSfx();

            PlayerImpactRelay receiver = player.GetComponentInParent<PlayerImpactRelay>();
            if (receiver != null)
            {
                // Away from the car, not the car's own forward -- a
                // side-swipe should shove the player sideways, not forward
                // just because that's the way the car happens to be
                // pointed. Flattened to horizontal so the upward bias below
                // is the only thing controlling how much "pop" there is.
                Vector3 toPlayer = receiver.transform.position - transform.position;
                toPlayer.y = 0f;
                Vector3 shoveDirection = (toPlayer.normalized + Vector3.up * impactUpwardBias).normalized;
                receiver.ServerApplyImpact(shoveDirection, impactForce);
            }
        }

        [ClientRpc]
        private void RpcPlayImpactSfx()
        {
            if (hornClip != null) audioSource.PlayOneShot(hornClip);
            if (yellClip != null) audioSource.PlayOneShot(yellClip);
        }
    }
}

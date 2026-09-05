using System.Collections.Generic;
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
    [RequireComponent(typeof(AudioSource))]
    public class CarDriver : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float turnSpeed = 120f;
        [SerializeField] private float waypointArrivalDistance = 1.5f;

        [SerializeField] private AudioClip hornClip;
        [SerializeField] private AudioClip yellClip;

        [SerializeField] private float impactForce = 12f;
        // Small on purpose -- this used to dominate the shove direction
        // (2) and made every hit look like it just popped the player
        // straight up in place. It's only meant to help the knockdown
        // topple, not compete with the actual horizontal shove.
        [SerializeField] private float impactUpwardBias = 0.4f;

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
                Destroy(gameObject);
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
            PlayerInventory player = other.GetComponentInParent<PlayerInventory>();
            if (player == null) return;

            if (hornClip != null) audioSource.PlayOneShot(hornClip);
            if (yellClip != null) audioSource.PlayOneShot(yellClip);

            PlayerRagdoll receiver = player.GetComponentInParent<PlayerRagdoll>();
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
                receiver.ApplyImpact(shoveDirection, impactForce);
            }
        }
    }
}

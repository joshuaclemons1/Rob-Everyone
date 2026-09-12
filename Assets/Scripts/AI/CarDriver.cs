using System.Collections.Generic;
using Mirror;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.World;
using UnityEngine;

namespace RobEveryone.AI
{
    // Drives a fixed route out to each hand-placed waypoint and back to its
    // origin point, then despawns -- no vision, no reaction to the player,
    // it just follows its route (per design: "completely oblivious," a
    // hazard to dodge rather than an AI that hunts). The actual point list
    // it walks (see Init) is a dense, pre-smoothed curve through those
    // waypoints (CarSpawnManager.BuildSmoothedPath), not the sparse
    // waypoints themselves -- this script has no idea it's driving a
    // spline, it just walks whatever points it's handed the same simple
    // way either way. CarSpawnManager owns deciding when/whether to spawn
    // one of these at all; this script only knows how to drive once it
    // exists.
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
        // Confirmed bug: at the defaults above, the car's own minimum
        // turning radius (moveSpeed / turnSpeed-in-radians, ~3.8 units
        // here) is *larger* than this used to be (1.5) -- meaning it
        // could physically orbit a target forever without ever
        // "arriving," a stable pursuit-curve limit cycle, confirmed via
        // logging (pathIndex stuck at 1, distance oscillating between
        // ~6 and ~12, never dropping below the old 1.5). Sparse
        // waypoints never exposed this (plenty of room to curve in
        // before getting anywhere near arrival distance); the dense
        // spline points now can be closer together than that turning
        // radius. Raised well above the turning radius as a safety
        // margin -- see the passedTarget check below for the real fix
        // that doesn't depend on this staying tuned correctly forever.
        [SerializeField] private float waypointArrivalDistance = 5f;

        [SerializeField] private AudioClip hornClip;
        [SerializeField] private AudioClip yellClip;

        [SerializeField] private float impactForce = 60f;
        // Small on purpose -- this used to dominate the shove direction
        // (2) and made every hit look like it just popped the player
        // straight up in place. It's only meant to help the knockdown
        // topple, not compete with the actual horizontal shove.
        [SerializeField] private float impactUpwardBias = 0.7f;
        // A single drive-through fires OnTriggerEnter once for the
        // player's own collider *and* once more for each of their
        // ragdoll's limb colliders (arms/legs), since
        // GetComponentInParent finds the same PlayerInventory from any of
        // them -- without this cooldown, one real hit applied the impact
        // (and played the SFX) up to 9 times over, launching the player
        // far harder than intended.
        [SerializeField] private float impactCooldown = 0.5f;

        private readonly Dictionary<PlayerInventory, float> lastImpactTime = new();

        // A dense, pre-smoothed point list (CarSpawnManager.
        // BuildSmoothedPath -- a Catmull-Rom spline through the original
        // sparse waypoints), not the raw hand-placed waypoints
        // themselves. Already includes the return-to-origin leg as its
        // own trailing points, so driving through this list start-to-end
        // *is* the whole route -- no separate "now returning to origin"
        // state needed anymore.
        private List<Vector3> path;
        private CarSpawnManager spawnManager;

        private int pathIndex;
        private AudioSource audioSource;

        // Called by CarSpawnManager right after Instantiate -- this is
        // per-spawn data (which route, who to report back to), not
        // something to hand-configure per prefab in the Inspector.
        public void Init(List<Vector3> smoothedPath, CarSpawnManager manager)
        {
            path = smoothedPath;
            spawnManager = manager;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (!isServer) return;

            if (path == null || pathIndex >= path.Count)
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

            Vector3 toTarget = path[pathIndex] - transform.position;
            toTarget.y = 0f;

            // Confirmed bug: distance-only arrival let the car get stuck
            // in a stable pursuit-curve orbit around a target it could
            // never physically turn tightly enough to reach (its own
            // minimum turning radius was larger than the old arrival
            // distance) -- it would circle the same point forever,
            // pathIndex never advancing.
            bool arrived = toTarget.sqrMagnitude <= waypointArrivalDistance * waypointArrivalDistance;

            // Confirmed second bug: a bare "is the target behind us"
            // check (no distance gate) fired even for a point the car
            // had never actually attempted to approach yet -- if its
            // current heading (e.g. straight off the spawn rotation)
            // happened to point away from several points in a row, it
            // kept incrementing pathIndex every frame without ever
            // reaching the rotate/move code below (which is the only
            // place transform.forward changes), blowing through the
            // entire path in about a second without moving at all.
            // Gating this on actually being close first -- comparable to
            // the orbit radius this was built to catch -- means it can
            // only ever fire once the car has genuinely closed in on a
            // point, never as a substitute for trying at all.
            float passedCheckRadius = waypointArrivalDistance * 3f;
            bool nearEnoughToHaveOrbited = toTarget.sqrMagnitude <= passedCheckRadius * passedCheckRadius;
            bool passedTarget = nearEnoughToHaveOrbited && Vector3.Dot(transform.forward, toTarget) < 0f;

            if (arrived || passedTarget)
            {
                pathIndex++;
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * (moveSpeed * Time.deltaTime);
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

            if (lastImpactTime.TryGetValue(player, out float last) && Time.time - last < impactCooldown) return;
            lastImpactTime[player] = Time.time;

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

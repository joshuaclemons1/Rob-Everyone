using System.Collections;
using Mirror;
using RobEveryone.Items;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Sabotage
{
    // A thrown single-target sabotage item (Hammer) that hits on contact
    // and, once it lands, stays in the world as a real pickup carrying
    // whatever durability it had left -- unlike SabotageProjectile's
    // fuse-timer AOE (Dynamite), which is consumed and destroyed on
    // detonation. Only ever resolves once (a single hasResolved flag, not
    // CarDriver's per-victim Dictionary debounce -- that pattern exists
    // there because a car keeps driving and can hit the *same* player
    // again later in its lap; this projectile stops simulating the
    // moment it resolves, so a dictionary would be solving a problem that
    // can't actually happen here).
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class RetrievableProjectile : NetworkBehaviour, ILaunchable
    {
        [SerializeField] private float throwSpeed = 14f;
        // Lands even if it never hits anyone -- a miss sliding forever
        // (or flying over a chasm, never touching anything) would
        // otherwise never resolve.
        [SerializeField] private float maxFlightSeconds = 3f;
        // The PickupItem-bearing prefab spawned at landing, carrying
        // whatever uses are left -- see item-creation.md's asset
        // conventions; this is a separate prefab from the flying one,
        // same relationship DynamiteProjectile.prefab has to
        // Assets/Prefabs/Items/Dynamite.prefab.
        [SerializeField] private GameObject pickupPrefab;

        private Rigidbody body;
        private ItemDefinition sourceItem;
        private NetworkIdentity thrower;
        private int remainingUses;
        private bool hasResolved;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public override void OnStartClient()
        {
            // Only the server simulates this Rigidbody -- matches
            // SabotageProjectile's own reasoning.
            if (!isServer) body.isKinematic = true;
        }

        [Server]
        public void ServerLaunch(Vector3 direction, ItemDefinition item, NetworkIdentity throwerIdentity, int startingUses)
        {
            sourceItem = item;
            thrower = throwerIdentity;
            remainingUses = startingUses;
            body.linearVelocity = direction * throwSpeed;
            StartCoroutine(LandAfterMaxFlight());
        }

        private IEnumerator LandAfterMaxFlight()
        {
            yield return new WaitForSeconds(maxFlightSeconds);
            if (!hasResolved) Land();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isServer || hasResolved) return;

            // Ignore the thrower's own collider entirely (not just skip
            // applying impact) -- this spawns essentially at the
            // thrower's own camera position, almost certainly still
            // overlapping their own CharacterController capsule for the
            // very first physics step, and letting that trigger Land()
            // would drop the Hammer at their feet instead of it ever
            // actually flying anywhere.
            NetworkIdentity hitIdentity = collision.collider.GetComponentInParent<NetworkIdentity>();
            if (hitIdentity != null && hitIdentity == thrower) return;

            PlayerImpactRelay relay = collision.collider.GetComponentInParent<PlayerImpactRelay>();
            if (relay != null)
            {
                Vector3 direction = collision.collider.transform.position - transform.position;
                direction.y = 0f;
                direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
                relay.ServerApplyPvpImpact(direction + Vector3.up * 0.5f, sourceItem.ImpactForce, sourceItem.StunDuration);
                if (remainingUses > 0) remainingUses--;
            }

            // Lands on any solid hit (a player, a wall, the ground) -- a
            // thrown Hammer clatters to a stop wherever it first hits
            // something, not just on a successful hit on a player.
            Land();
        }

        [Server]
        private void Land()
        {
            if (hasResolved) return;
            hasResolved = true;

            if (remainingUses > 0 && pickupPrefab != null)
            {
                GameObject pickup = Instantiate(pickupPrefab, transform.position, Quaternion.identity);
                // WorldModelScale is never baked into a pickup prefab's
                // own saved scale -- every other spawn path (LootSpawnPoint,
                // PlayerInventory's drop) applies it explicitly after
                // instantiating. Confirmed bug: a landed Hammer came out
                // at its raw, unscaled import size (way too large)
                // because this path was the one spawner that skipped it.
                pickup.transform.localScale = sourceItem.WorldModelScale;
                pickup.GetComponent<PickupItem>()?.Initialize(sourceItem, remainingUses);
                NetworkServer.Spawn(pickup);
            }
            // remainingUses <= 0 (broken) or no pickupPrefab configured --
            // nothing spawns, it's just gone.

            NetworkServer.Destroy(gameObject);
        }
    }
}

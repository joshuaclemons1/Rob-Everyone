using Mirror;
using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Items
{
    // Marks a spot inside a house prefab where a random item should
    // appear each round, instead of a specific item's model being baked
    // into the prefab by hand. At Start, rolls lootTable and instantiates
    // the chosen item's own WorldModelPrefab right here, turning it into
    // a real, interactable PickupItem.
    //
    // How big the spawned model renders comes entirely from
    // ItemDefinition.WorldModelScale, the same for every spawn point that
    // ever rolls that item.
    //
    // The spawned item is deliberately NOT parented under this spawn
    // point, even though it visually sits exactly here -- Mirror's spawn
    // message replicates an object's *local* transform and reconstructs
    // it with no parent at all on every client (ad-hoc Transform
    // parenting isn't something NetworkServer.Spawn tracks or restores).
    // Since Instantiate(prefab, transform.position, ..., transform) sets
    // world position equal to this spawn point's own, the resulting
    // *local* position relative to that parent is always (0,0,0) --
    // which is exactly what got sent and re-applied as if it were world
    // position, landing the item at the world origin on every other
    // client. Confirmed bug: loot appeared to simply not exist for a
    // client, when it had actually spawned correctly, just at (0,0,0)
    // instead of in the house.
    //
    // Networking (Stage 4): server-only (OnStartServer instead of Start)
    // -- every client needs to see the *same* rolled item, not each
    // independently gamble their own. Every ItemDefinition's
    // WorldModelPrefab needs both a NetworkIdentity *and* a PickupItem
    // component baked into the prefab asset itself (ItemPrefabBatchTool
    // does this automatically) and to be registered as a Spawnable
    // Prefab on the NetworkManager, the same way house/car prefabs are
    // (see stage4-multiplayer-mirror.md Part 5). Neither is safe to add
    // here at runtime: NetworkIdentity isn't supported at all after
    // Instantiate, and PickupItem technically *can* be added here, but
    // NetworkIdentity.Awake() (which runs synchronously during
    // Instantiate, just above) already scans and caches this object's
    // NetworkBehaviours by the time this method would add it -- leaving
    // its netIdentity back-reference permanently null and throwing the
    // moment anything on it checks isServer/isClient/etc. Collider is
    // the only one of the three that's genuinely fine as a runtime
    // fallback, since it isn't a NetworkBehaviour.
    public class LootSpawnPoint : NetworkBehaviour
    {
        [SerializeField] private LootTable lootTable;

        // Issue #74: this point has a chance of coming up empty instead
        // of always spawning something -- a house search should sometimes
        // be a bust, which is what makes actually finding loot feel worth
        // it. baseSpawnChance is calibrated for a single player; each
        // additional connected player nudges the chance up, so a fuller
        // lobby (more competition for the same houses) sees meaningfully
        // more loot on the map overall without needing a separate
        // map-wide "total budget" coordinator across every spawn point --
        // each point still rolls independently, just with a
        // player-count-aware chance.
        [SerializeField, Range(0f, 1f)] private float baseSpawnChance = 0.6f;
        [SerializeField, Range(0f, 0.2f)] private float spawnChancePerExtraPlayer = 0.08f;

        // What ResolveSpawnOverlap/SnapToSurfaceBelow treat as solid world
        // geometry -- walls/floors/furniture all sit on the ordinary
        // Default layer in this project (confirmed no dedicated "level
        // geometry" layer exists), so this is Everything *except* Player
        // and Ragdoll: a loot item's resting place shouldn't depend on
        // whether some player's hitbox happened to be standing on this
        // exact spot the instant the round started. Computed once in
        // Awake (name lookup, not a hardcoded bit index, in case layers
        // ever get renumbered) rather than baked into the field
        // initializer -- narrow further in the Inspector if needed.
        [SerializeField] private LayerMask overlapResolveMask = ~0;
        // Hard cap on push-out iterations -- Physics.ComputePenetration
        // usually resolves a single-wall overlap in one pass, but a spawn
        // point wedged between two solids (e.g. a shelf against a wall)
        // can need a couple more. Bailing after this many just leaves the
        // item wherever it landed rather than fighting geometry forever.
        [SerializeField] private int maxOverlapResolveIterations = 4;
        // How far below the item SnapToSurfaceBelow will look for a
        // surface to rest on, and how big a gap it'll bother closing --
        // pushing an item out of a wall/table it was embedded in can
        // easily leave it hovering with visible daylight underneath,
        // which reads exactly as "odd placement" as clipping through
        // geometry does. Small min gap so this doesn't twitch-correct
        // genuinely-fine placements over float noise.
        [SerializeField] private float groundSnapMaxDistance = 1f;
        [SerializeField] private float groundSnapMinGap = 0.02f;

        private void Awake()
        {
            int excluded = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Ragdoll"));
            overlapResolveMask &= ~excluded;
        }

        public override void OnStartServer()
        {
            Spawn();
        }

        private void Spawn()
        {
            if (lootTable == null) return;

            if (Random.value > EffectiveSpawnChance())
            {
                return; // came up empty this round, on purpose -- see this field's own comment
            }

            int currentBatch = GameFlowManager.Instance != null ? GameFlowManager.Instance.BatchNumber : 1;
            ItemDefinition item = lootTable.GetRandomItem(currentBatch);
            if (item == null || item.WorldModelPrefab == null)
            {
                Debug.LogWarning($"[LootSpawnPoint] '{gameObject.name}' rolled a null item or one with no WorldModelPrefab -- nothing spawned.");
                return;
            }

            GameObject instance = Instantiate(item.WorldModelPrefab, transform.position,
                transform.rotation * item.WorldModelRotation);

            // No parent (see class comment), so localScale *is* world
            // scale directly -- no need to compensate for an inherited
            // parent distortion the way a parented child would.
            instance.transform.localScale = item.WorldModelScale;

            // Most item models already carry their own collider (matching
            // how they're built as ordinary decorative prefabs elsewhere)
            // -- this is just a fallback for one that doesn't.
            if (instance.GetComponent<Collider>() == null)
            {
                instance.AddComponent<BoxCollider>();
            }

            // Spawn points are hand-placed once and then reused by every
            // item the shared LootTable might roll there -- a marker set
            // for a small trinket can just as easily roll a bulkier item,
            // or an item's fitted collider isn't exactly centered on its
            // visual model, so the raw spawn point position alone isn't
            // reliably clear of nearby walls/furniture. Confirmed
            // complaint: items sometimes spawned partially inside a wall
            // or shelf. Nudges the instance out of whatever it's
            // overlapping right after scale is applied (the check has to
            // happen post-scale -- the collider's real size isn't known
            // until then).
            Collider[] ownColliders = ResolveSpawnOverlap(instance);

            // Pushing an item out of whatever it was embedded in can just
            // as easily leave it hovering above the real surface with a
            // visible gap underneath -- ComputePenetration only guarantees
            // "no longer overlapping," not "resting on something." Runs
            // after overlap resolution settles, using the item's now-final
            // position.
            SnapToSurfaceBelow(instance, ownColliders);

            if (instance.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogError($"{item.ItemName}'s World Model Prefab has no NetworkIdentity -- add one to the prefab asset itself, adding it at runtime here isn't supported by Mirror.", instance);
                return;
            }

            PickupItem pickup = instance.GetComponent<PickupItem>();
            if (pickup == null)
            {
                Debug.LogError($"{item.ItemName}'s World Model Prefab has no PickupItem -- add one to the prefab asset itself (ItemPrefabBatchTool does this automatically). Adding it here at runtime would leave its NetworkIdentity link permanently broken -- see this script's class comment for why.", instance);
                return;
            }
            pickup.Initialize(item);

            NetworkServer.Spawn(instance);
        }

        // Issue #74. Clamped to [0,1] since spawnChancePerExtraPlayer *
        // (playerCount - 1) has no natural ceiling on its own -- a very
        // full lobby should cap out at "always spawns," not overshoot
        // past a real probability.
        private float EffectiveSpawnChance()
        {
            int playerCount = Mathf.Max(1, PlayerInventory.AllPlayers.Count);
            float chance = baseSpawnChance + spawnChancePerExtraPlayer * (playerCount - 1);
            return Mathf.Clamp01(chance);
        }

        // Iteratively pushes `instance` out of whatever solid geometry its
        // own collider(s) currently overlap, using Physics.ComputePenetration
        // (the same depenetration primitive Unity's own physics engine
        // uses internally) rather than a raycast-based placement -- a
        // raycast only ever answers "where's the floor," not "does this
        // box's actual footprint intersect that wall over there," which
        // is the shape of bug actually being fixed here.
        private Collider[] ResolveSpawnOverlap(GameObject instance)
        {
            Collider[] ownColliders = instance.GetComponentsInChildren<Collider>();
            if (ownColliders.Length == 0) return ownColliders;

            for (int iteration = 0; iteration < maxOverlapResolveIterations; iteration++)
            {
                bool foundOverlap = false;

                foreach (Collider ownCollider in ownColliders)
                {
                    // bounds is already the world-space AXIS-ALIGNED box
                    // (Collider.bounds), so the query itself stays
                    // unrotated (Quaternion.identity) -- this is only a
                    // broad-phase "what's nearby" gather, the real
                    // shape-accurate check is ComputePenetration below.
                    Bounds bounds = ownCollider.bounds;
                    Collider[] nearby = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                        overlapResolveMask, QueryTriggerInteraction.Ignore);

                    foreach (Collider other in nearby)
                    {
                        if (other.transform.IsChildOf(instance.transform)) continue; // never push against itself

                        bool penetrating = Physics.ComputePenetration(
                            ownCollider, ownCollider.transform.position, ownCollider.transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 pushDirection, out float pushDistance);

                        if (!penetrating || pushDistance <= 0f) continue;

                        instance.transform.position += pushDirection * pushDistance;
                        foundOverlap = true;
                    }
                }

                if (!foundOverlap) break; // clear -- nothing left to resolve
            }

            return ownColliders;
        }

        // Lowers `instance` onto the nearest surface directly below it, if
        // one exists within groundSnapMaxDistance and there's actually a
        // gap worth closing -- ResolveSpawnOverlap only guarantees "not
        // overlapping," which is equally satisfied by "floating just
        // above the shelf it was pushed out of." Uses RaycastAll + a
        // self-filter (same pattern PlayerRagdoll.IsHipsNearGround
        // already established) rather than a layer-mask exclusion, since
        // the item's own collider sits on the same layer as the
        // environment it's being tested against.
        private void SnapToSurfaceBelow(GameObject instance, Collider[] ownColliders)
        {
            if (ownColliders.Length == 0) return;

            Bounds combined = ownColliders[0].bounds;
            for (int i = 1; i < ownColliders.Length; i++) combined.Encapsulate(ownColliders[i].bounds);

            // Starts a hair above the item's own true bottom (not exactly
            // on it) -- a ray whose origin sits precisely on a surface can
            // miss that surface on some hardware/precision edge cases.
            // Self-hits inside that margin are filtered below anyway.
            const float originEpsilon = 0.05f;
            Vector3 origin = new Vector3(combined.center.x, combined.min.y + originEpsilon, combined.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundSnapMaxDistance + originEpsilon,
                overlapResolveMask, QueryTriggerInteraction.Ignore);

            float? nearestSurfaceY = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(instance.transform)) continue;
                if (nearestSurfaceY == null || hit.point.y > nearestSurfaceY.Value) nearestSurfaceY = hit.point.y;
            }

            if (nearestSurfaceY == null) return; // nothing found within range -- leave it where overlap resolution put it

            float gap = combined.min.y - nearestSurfaceY.Value;
            if (gap > groundSnapMinGap)
            {
                instance.transform.position -= new Vector3(0f, gap, 0f);
            }
        }
    }
}

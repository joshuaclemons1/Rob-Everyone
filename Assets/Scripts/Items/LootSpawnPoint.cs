using Mirror;
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

        public override void OnStartServer()
        {
            Spawn();
        }

        private void Spawn()
        {
            if (lootTable == null) return;

            ItemDefinition item = lootTable.GetRandomItem();
            if (item == null || item.WorldModelPrefab == null)
            {
                Debug.LogWarning($"[LootSpawnPoint] '{gameObject.name}' rolled a null item or one with no WorldModelPrefab -- nothing spawned.");
                return;
            }

            GameObject instance = Instantiate(item.WorldModelPrefab, transform.position, transform.rotation);

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
    }
}

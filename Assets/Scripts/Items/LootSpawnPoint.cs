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
    // ever rolls that item -- Spawn() actively cancels out this object's
    // own lossy (inherited) scale so that holds true even under a house
    // whose room/floor geometry was stretched non-uniformly to fit its
    // footprint, which would otherwise distort anything parented under it.
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
            if (item == null || item.WorldModelPrefab == null) return;

            GameObject instance = Instantiate(item.WorldModelPrefab, transform.position, transform.rotation, transform);

            // Divide out this object's own inherited scale so the result
            // is always exactly item.WorldModelScale in world terms, no
            // matter how distorted the parent hierarchy happens to be.
            Vector3 parentScale = transform.lossyScale;
            instance.transform.localScale = new Vector3(
                item.WorldModelScale.x / parentScale.x,
                item.WorldModelScale.y / parentScale.y,
                item.WorldModelScale.z / parentScale.z);

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

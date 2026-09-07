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
    public class LootSpawnPoint : MonoBehaviour
    {
        [SerializeField] private LootTable lootTable;

        private void Start()
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

            PickupItem pickup = instance.GetComponent<PickupItem>();
            if (pickup == null) pickup = instance.AddComponent<PickupItem>();
            pickup.Initialize(item);
        }
    }
}

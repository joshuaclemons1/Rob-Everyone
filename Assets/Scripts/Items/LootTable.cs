using UnityEngine;

namespace RobEveryone.Items
{
    // A pool of possible items a LootSpawnPoint can roll from. Different
    // houses can reference different tables (or share one) once
    // gameplay-design.md's per-house Loot Tables need that -- for now,
    // every roll is uniform-random across whatever's in the list.
    [CreateAssetMenu(menuName = "Rob Everyone/Loot Table", fileName = "NewLootTable")]
    public class LootTable : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] possibleItems;

        public ItemDefinition GetRandomItem()
        {
            if (possibleItems == null || possibleItems.Length == 0) return null;
            return possibleItems[Random.Range(0, possibleItems.Length)];
        }
    }
}

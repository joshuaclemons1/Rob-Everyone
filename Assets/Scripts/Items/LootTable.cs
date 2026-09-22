using System.Collections.Generic;
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

        // Issue #74: currentBatch filters out anything whose own
        // ItemDefinition.LootUnlockBatch is still higher than the batch
        // actually being played -- a safe/high-value item shouldn't be
        // able to roll (and single-handedly fill quota) on round 1. Every
        // existing item defaults to LootUnlockBatch 1, so passing
        // currentBatch 1 behaves identically to the old unfiltered roll.
        // Falls back to an unfiltered roll if nothing in the table
        // actually qualifies yet, rather than returning null and leaving
        // a spawn point looking broken this early in a fresh loot table's
        // life -- worth revisiting once every table has a real spread of
        // batch tiers configured.
        public ItemDefinition GetRandomItem(int currentBatch)
        {
            if (possibleItems == null || possibleItems.Length == 0) return null;

            List<ItemDefinition> eligible = new();
            foreach (ItemDefinition item in possibleItems)
            {
                if (item != null && item.LootUnlockBatch <= currentBatch) eligible.Add(item);
            }

            if (eligible.Count == 0) return possibleItems[Random.Range(0, possibleItems.Length)];
            return eligible[Random.Range(0, eligible.Count)];
        }
    }
}

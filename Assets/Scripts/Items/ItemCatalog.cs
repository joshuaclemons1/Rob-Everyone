using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Items
{
    // Every ItemDefinition that can ever appear in a loot table, in one
    // asset. Needed for networking (Stage 4): Mirror can sync primitives/
    // strings over a SyncList, but not a direct ScriptableObject asset
    // reference -- PlayerInventory syncs each carried item by name instead
    // (empty string = empty slot) and looks it back up through this
    // catalog on the receiving end. One asset, referenced by
    // PlayerInventory and anything else (a future shop screen) that needs
    // to resolve a name back to the real ItemDefinition.
    [CreateAssetMenu(menuName = "Rob Everyone/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> items = new();

        public ItemDefinition GetByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            foreach (ItemDefinition item in items)
            {
                if (item != null && item.ItemName == itemName) return item;
            }

            return null;
        }
    }
}

using RobEveryone.Interaction;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Items
{
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition item;

        public int Value => item != null ? item.Value : 0;
        public string InteractionPrompt => item != null ? $"Take {item.ItemName} (${item.Value})" : "Take item";

        // Called by LootSpawnPoint right after it instantiates this
        // item's model at runtime, since a randomly-rolled pickup can't
        // have `item` wired in the Inspector ahead of time the way a
        // hand-placed one can.
        public void Initialize(ItemDefinition definition)
        {
            item = definition;
        }

        public void Interact(GameObject interactor)
        {
            if (item == null) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            // Only removed from the world if a slot actually had room --
            // a full 5-slot inventory just leaves it where it is.
            if (inventory.AddItem(item))
            {
                gameObject.SetActive(false);
            }
        }
    }
}

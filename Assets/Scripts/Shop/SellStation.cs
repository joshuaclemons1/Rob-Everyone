using RobEveryone.Interaction;
using RobEveryone.Inventory;
using RobEveryone.Items;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Shop
{
    // Lobby-scene pawnshop-owner prop. Same shape as PickupItem -- E to
    // interact, no automatic selling just from being in the Lobby.
    // Sells only the currently-selected/held item, not the whole
    // carried haul at once -- matches the held-item display's framing
    // of the selected slot as what's "in your hand" right now.
    [RequireComponent(typeof(Collider))]
    public class SellStation : MonoBehaviour, IInteractable
    {
        // InteractionPrompt/CanInteract are queried only by the local
        // player's own Interactor (see CrosshairUI) -- PlayerInventory.
        // LocalPlayer is "whoever's actually asking" in every real
        // usage, no need to thread an interactor reference through
        // IInteractable itself for this.
        public string InteractionPrompt
        {
            get
            {
                ItemDefinition item = ResolveSelectedItem(PlayerInventory.LocalPlayer);
                return item != null ? $"Sell {item.ItemName} for ${item.Value}" : "Nothing selected to sell";
            }
        }

        public bool CanInteract => ResolveSelectedItem(PlayerInventory.LocalPlayer) != null;

        public void Interact(GameObject interactor)
        {
            // Can't hand loot over with your hands full carrying someone.
            CarryController carry = interactor.GetComponent<CarryController>();
            if (carry != null && carry.IsCarrying) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            inventory.SellSelectedSlot();
        }

        private static ItemDefinition ResolveSelectedItem(PlayerInventory inventory)
        {
            if (inventory == null) return null;
            int index = inventory.SelectedSlot;
            if (index < 0 || index >= inventory.Slots.Count) return null;
            return inventory.Slots[index]?.Item;
        }
    }
}

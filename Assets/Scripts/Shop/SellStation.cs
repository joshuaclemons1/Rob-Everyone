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
                return item != null ? $"Sell {item.ItemName} for ${item.Value}" : "Nothing to sell...";
            }
        }

        // Deliberately doesn't gate on "is anything selected" --
        // Interactor.FindTarget only ever shows a prompt at all when
        // CanInteract is true, so gating this the old way silently
        // swallowed the "Nothing to sell..." text along with it
        // (confirmed bug, same shape as ShopShelfItem's own "Locked"
        // text never showing). The station is always a valid target;
        // Interact() below is what actually enforces "is there
        // something to sell," by simply no-oping.
        public bool CanInteract => true;

        public void Interact(GameObject interactor)
        {
            // Can't hand loot over with your hands full carrying someone.
            CarryController carry = interactor.GetComponent<CarryController>();
            if (carry != null && carry.IsCarrying) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;
            if (ResolveSelectedItem(inventory) == null) return;

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

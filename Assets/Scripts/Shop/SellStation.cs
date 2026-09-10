using RobEveryone.Interaction;
using RobEveryone.Inventory;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Shop
{
    // Lobby-scene pawnshop-owner prop. Same shape as PickupItem -- E to
    // interact, no automatic selling just from being in the Lobby.
    [RequireComponent(typeof(Collider))]
    public class SellStation : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "Sell loot to the pawnshop owner";
        public bool CanInteract => true;

        public void Interact(GameObject interactor)
        {
            // Can't hand loot over with your hands full carrying someone.
            CarryController carry = interactor.GetComponent<CarryController>();
            if (carry != null && carry.IsCarrying) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            inventory.SellCarried();
        }
    }
}

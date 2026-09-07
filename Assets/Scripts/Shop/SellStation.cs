using RobEveryone.Interaction;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Shop
{
    // Lobby-scene pawnshop-owner prop. Same shape as PickupItem -- E to
    // interact, no automatic selling just from being in the Lobby.
    [RequireComponent(typeof(Collider))]
    public class SellStation : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "Sell loot to the pawnshop owner";

        public void Interact(GameObject interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            inventory.SellCarried();
        }
    }
}

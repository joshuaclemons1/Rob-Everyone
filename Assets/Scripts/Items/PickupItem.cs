using RobEveryone.Interaction;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Items
{
    // Placeholder loot. Stage 3 will replace the plain (name, value) pair
    // here with a shared ItemDefinition ScriptableObject and per-house loot
    // tables, but a single dumb pickup is all Stage 2 needs.
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private string itemName = "Watch";
        [SerializeField] private int value = 25;

        public int Value => value;
        public string InteractionPrompt => $"Take {itemName} (${value})";

        public void Interact(GameObject interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            inventory.AddItem(itemName, value);
            gameObject.SetActive(false);
        }
    }
}

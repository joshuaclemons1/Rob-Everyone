using Mirror;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.Items
{
    // Networking (Stage 4): NetworkBehaviour so LootSpawnPoint can
    // NetworkServer.Spawn it (see that script). Interact() now only ever
    // runs on the server (called from Interactor's Command) -- Taken is a
    // SyncVar so every client hides the same item at the same moment
    // instead of only the interacting player's own view.
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(NetworkIdentity))]
    public class PickupItem : NetworkBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition item;

        [SyncVar(hook = nameof(OnTakenChanged))]
        private bool taken;

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

        // Server-only -- see Interactor's CmdInteract, the only caller.
        public void Interact(GameObject interactor)
        {
            if (!isServer || item == null || taken) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            // Only marked taken if a slot actually had room -- a full
            // 5-slot inventory just leaves it where it is.
            if (inventory.AddItem(item))
            {
                taken = true;
            }
        }

        private void OnTakenChanged(bool _, bool newValue)
        {
            gameObject.SetActive(!newValue);
        }
    }
}

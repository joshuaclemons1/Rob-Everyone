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
        // Inspector-assigned for a hand-placed pickup; set via Initialize
        // instead for one LootSpawnPoint rolls at runtime. Either way,
        // only the server's own copy is guaranteed correct here -- item
        // itself can't be synced directly (see PlayerInventory's own
        // comment: Mirror can't replicate a direct ScriptableObject
        // reference), so syncedItemName mirrors it by name for every
        // other client to resolve back through ItemCatalog. Confirmed
        // bug without this: a remote client's own copy always read item
        // as null, showing a generic "Take item" prompt instead of the
        // real name/value (pickup itself still worked, since Interact()
        // only ever runs on the server's own correctly-set copy).
        [SerializeField] private ItemDefinition item;

        [SyncVar(hook = nameof(OnItemNameChanged))]
        private string syncedItemName;

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

        // By now `item` is correctly set either way (Initialize already
        // ran for a LootSpawnPoint-rolled item, since it's called before
        // NetworkServer.Spawn triggers this; a hand-placed one already
        // had it assigned in the Inspector before Play even started).
        public override void OnStartServer()
        {
            syncedItemName = item != null ? item.ItemName : "";
        }

        private void OnItemNameChanged(string _, string newValue)
        {
            if (isServer) return; // server's own `item` is already the real reference
            item = ItemCatalog.Instance != null ? ItemCatalog.Instance.GetByName(newValue) : null;
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

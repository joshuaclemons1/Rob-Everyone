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

        // -1 = not overridden, AddItem seeds a fresh item.MaxUses as usual
        // (every hand-placed/LootSpawnPoint-rolled pickup). Set >= 0 only
        // by RetrievableProjectile's landing spawn, so a thrown-and-landed
        // Hammer keeps its already-reduced durability instead of
        // resetting to full when picked back up.
        private int overrideUses = -1;

        [SyncVar(hook = nameof(OnItemNameChanged))]
        private string syncedItemName;

        [SyncVar(hook = nameof(OnTakenChanged))]
        private bool taken;

        // True only for an item a player dropped (PlayerInventory.DropSlot),
        // false for LootSpawnPoint house loot. Drives the cosmetic
        // spin/bob below so a dropped item reads as "grabbable, just set
        // down" rather than blending into the furniture -- same feel as
        // the hotbar preview. Synced so every client (not just the
        // dropper) sees it. These prefabs carry no NetworkTransform
        // (house loot never moves), so spinning the root here is safe --
        // nothing is fighting it over the wire.
        [SyncVar]
        private bool dropped;

        [SerializeField] private float dropSpinSpeed = 60f;   // degrees/sec
        [SerializeField] private float dropBobHeight = 0.12f; // metres
        [SerializeField] private float dropBobSpeed = 2f;

        private Vector3 droppedBasePos;
        private bool capturedDroppedBase;

        [Server]
        public void MarkDropped() => dropped = true;

        public int Value => item != null ? item.Value : 0;
        public string InteractionPrompt => item != null ? $"Take {item.ItemName} (${item.Value})" : "Take item";
        public bool CanInteract => true;

        // Called by LootSpawnPoint right after it instantiates this
        // item's model at runtime, since a randomly-rolled pickup can't
        // have `item` wired in the Inspector ahead of time the way a
        // hand-placed one can.
        public void Initialize(ItemDefinition definition)
        {
            item = definition;
        }

        // See RetrievableProjectile -- the only caller that needs a
        // specific starting uses count rather than a fresh item.MaxUses.
        public void Initialize(ItemDefinition definition, int startingUses)
        {
            item = definition;
            overrideUses = startingUses;
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

            // Hands full while carrying a downed player.
            var carry = interactor.GetComponent<RobEveryone.Player.CarryController>();
            if (carry != null && carry.IsCarrying) return;

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null) return;

            // Only marked taken if a slot actually had room -- a full
            // 5-slot inventory just leaves it where it is.
            bool added = overrideUses >= 0 ? inventory.AddItem(item, overrideUses) : inventory.AddItem(item);
            if (added)
            {
                taken = true;
            }
        }

        private void OnTakenChanged(bool _, bool newValue)
        {
            gameObject.SetActive(!newValue);
        }

        private void Update()
        {
            if (!dropped) return;

            if (!capturedDroppedBase)
            {
                droppedBasePos = transform.position;
                capturedDroppedBase = true;

                // A dropped item is a floating pickup, not an obstacle --
                // make every collider a trigger so the player walks
                // through it instead of getting hung up on it. The
                // Interactor raycast still hits it (queriesHitTriggers is
                // on project-wide). Runs on every client + server so
                // local physics agrees.
                foreach (Collider c in GetComponentsInChildren<Collider>(true))
                {
                    c.isTrigger = true;
                }
            }

            transform.Rotate(0f, dropSpinSpeed * Time.deltaTime, 0f, Space.World);
            float bob = Mathf.Sin(Time.time * dropBobSpeed) * dropBobHeight;
            transform.position = droppedBasePos + new Vector3(0f, bob, 0f);
        }
    }
}

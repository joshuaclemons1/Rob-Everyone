using Mirror;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using RobEveryone.Items;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Sabotage
{
    // Makes a stunned player themself a valid E-key interaction target --
    // reuses Interactor.cs's existing raycast+prompt pipeline (already
    // used by SellStation/PickupItem) instead of a parallel input system.
    // CanInteract gates on PlayerImpactRelay.IsStealable, a client-visible
    // SyncVar, so the prompt only shows during an actual PvP steal window
    // (not a car-caused stun, which never sets IsStealable).
    [RequireComponent(typeof(PlayerImpactRelay))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerTheftTarget : NetworkBehaviour, IInteractable
    {
        private PlayerImpactRelay relay;
        private PlayerInventory victim;

        public string InteractionPrompt => "Steal item";
        public bool CanInteract => relay.IsStealable;

        private void Awake()
        {
            relay = GetComponent<PlayerImpactRelay>();
            victim = GetComponent<PlayerInventory>();
        }

        // Server-only -- see Interactor's CmdInteract, the only caller.
        public void Interact(GameObject interactorObject)
        {
            if (!isServer) return;
            if (!relay.IsStealable) return; // re-validate server-side, don't trust the client-visible flag alone

            PlayerInventory thief = interactorObject.GetComponent<PlayerInventory>();
            if (thief == null || thief == victim) return;

            int victimSlot = FindFirstOccupiedSlot(victim);
            if (victimSlot < 0) return;

            ItemDefinition stolen = victim.Slots[victimSlot]?.Item;
            if (stolen == null) return;

            // AddItem only ever tries the thief's currently-selected slot
            // (existing PlayerInventory behavior) -- a full hotbar, or
            // just an occupied selected slot with other slots free, both
            // fail this the same way. Nothing is taken from the victim
            // unless the thief's AddItem actually succeeds.
            if (!thief.AddItem(stolen)) return;
            victim.RemoveSlot(victimSlot);

            // One theft per stun -- clear immediately rather than only via
            // the window timer, so a second attacker can't also loot the
            // same stun.
            relay.ClearStealableNow();
        }

        private static int FindFirstOccupiedSlot(PlayerInventory inventory)
        {
            var spans = inventory.SlotSpanLengths;
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                if (i < spans.Count && spans[i] > 0 && inventory.Slots[i] != null) return i;
            }
            return -1;
        }
    }
}

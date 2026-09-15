using Mirror;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using RobEveryone.Player;
using RobEveryone.UI;
using UnityEngine;

namespace RobEveryone.Sabotage
{
    // On every Player. Two roles, same component:
    //
    //  - VICTIM: while this player is stunned by a PvP hit
    //    (PlayerImpactRelay.IsStealable), they're a valid E target.
    //    Pressing E grants the *steal window* to that one thief and opens
    //    a steal screen on the thief's client -- it does NOT transfer
    //    anything on its own. Being seated at the exit car (ExitCarState)
    //    deliberately does NOT plug into this on its own -- a seated
    //    player is just no longer *immune* to being stunned there (see
    //    FirstPersonController.ExitCarFrozen's own comment), the same as
    //    anywhere else; a rival still has to actually knock them out
    //    first, same as any other player.
    //
    //  - THIEF: the steal screen shows the victim's hotbar above the
    //    thief's own; dragging one item down fires CmdStealItem here,
    //    which does the server-authoritative transfer (one item per
    //    window, gameplay-design.md).
    //
    // Reuses the existing Interactor/IInteractable raycast+prompt
    // pipeline for the "press E on a stunned rival" half; the drag half
    // is InventoryScreenUI.
    [RequireComponent(typeof(PlayerImpactRelay))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerTheftTarget : NetworkBehaviour, IInteractable
    {
        private PlayerImpactRelay relay;
        private PlayerInventory inventory;
        private Carryable carryable;

        private bool IsStealable => relay != null && relay.IsStealable;

        // Server-only. The thief currently granted this player's steal
        // window (they pressed E, their screen is open). Null = the
        // window is open to whoever presses E first. Cleared on a
        // successful steal, on the thief closing the screen, or when the
        // window lapses.
        private NetworkIdentity activeThief;

        // Tap vs. hold only matters while this rival is still actively
        // ragdolling (Interactor's own hold-to-grab disambiguation) --
        // once the ragdoll animation ends but the steal window is still
        // open, E only ever robs them, so the hint drops away on its own.
        public string InteractionPrompt =>
            carryable != null && carryable.CanBeGrabbed
                ? $"Rob {(inventory != null ? inventory.DisplayName : "rival")} (hold to carry)"
                : $"Rob {(inventory != null ? inventory.DisplayName : "rival")}";

        // Client-visible gate for the E prompt. The server-side exclusivity
        // check (only one thief at a time) lives in Interact() below,
        // since activeThief isn't synced. A carried player is
        // theft-protected -- carrying is a grief/relocate toy, not a way
        // to strip-mine someone or pass a body around stealing from it.
        public bool CanInteract => IsStealable && (carryable == null || !carryable.IsCarried);

        private void Awake()
        {
            relay = GetComponent<PlayerImpactRelay>();
            inventory = GetComponent<PlayerInventory>();
            carryable = GetComponent<Carryable>();
        }

        private void Update()
        {
            // Manual isServer guard (not the [Server] attribute, which
            // warns every frame on a client).
            if (!isServer) return;
            // Window lapsed with nobody having stolen -- release it.
            if (activeThief != null && !IsStealable) activeThief = null;
        }

        // VICTIM side. Server-only -- see Interactor.CmdInteract, the only
        // caller. Grants this stun's steal window to the thief and opens
        // their steal screen.
        public void Interact(GameObject interactorObject)
        {
            if (!isServer || !IsStealable) return;
            if (carryable != null && carryable.IsCarried) return; // theft-protected while carried
            if (activeThief != null) return; // someone is already robbing this stun

            NetworkIdentity thief = interactorObject.GetComponent<NetworkIdentity>();
            if (thief == null || thief == netIdentity || thief.connectionToClient == null) return;

            activeThief = thief;

            PlayerTheftTarget thiefTheft = thief.GetComponent<PlayerTheftTarget>();
            if (thiefTheft != null) thiefTheft.TargetOpenStealScreen(thief.connectionToClient, netIdentity);
        }

        // THIEF side (the RPC lands on the thief's own component).
        [TargetRpc]
        private void TargetOpenStealScreen(NetworkConnectionToClient target, NetworkIdentity victim)
        {
            if (victim == null) return;
            InventoryScreenUI screen = FindAnyObjectByType<InventoryScreenUI>(FindObjectsInactive.Include);
            if (screen != null) screen.OpenSteal(victim.GetComponent<PlayerInventory>());
        }

        [TargetRpc]
        private void TargetCloseStealScreen(NetworkConnectionToClient target)
        {
            InventoryScreenUI screen = FindAnyObjectByType<InventoryScreenUI>(FindObjectsInactive.Include);
            if (screen != null) screen.CloseSteal();
        }

        // THIEF side. Called by InventoryScreenUI when the thief drags
        // victim slot `victimHead` onto their own slot `myHead`.
        public void RequestSteal(NetworkIdentity victim, int victimHead, int myHead) =>
            CmdStealItem(victim, victimHead, myHead);

        // THIEF side. Called by InventoryScreenUI when the thief closes
        // the screen without taking anything.
        public void ReleaseStealWindow(NetworkIdentity victim) => CmdCancelSteal(victim);

        [Command]
        private void CmdStealItem(NetworkIdentity victimIdentity, int victimHead, int myHead)
        {
            if (victimIdentity == null) return;
            PlayerTheftTarget victim = victimIdentity.GetComponent<PlayerTheftTarget>();
            if (victim == null || victim == this) return;
            if (victim.activeThief != netIdentity) return; // not your window
            if (!victim.IsStealable) return;                // window lapsed
            if (victim.carryable != null && victim.carryable.IsCarried) return; // theft-protected while carried

            if (victimHead < 0 || victimHead >= PlayerInventory.SlotCount) return;
            if (victimHead >= victim.inventory.SlotSpanLengths.Count ||
                victim.inventory.SlotSpanLengths[victimHead] <= 0) return; // not a head slot

            InventorySlot? slot = victim.inventory.Slots[victimHead];
            if (slot == null || slot.Value.Item == null) return;

            // No room on the thief's chosen slot -> nothing is taken from
            // the victim. The window stays open for another try.
            if (!inventory.TryPlaceAt(slot.Value.Item, slot.Value.RemainingUses, myHead)) return;

            victim.inventory.RemoveSlot(victimHead);
            victim.relay.ClearStealableNow(); // one item per window
            victim.activeThief = null;
            TargetCloseStealScreen(connectionToClient);
        }

        [Command]
        private void CmdCancelSteal(NetworkIdentity victimIdentity)
        {
            if (victimIdentity == null) return;
            PlayerTheftTarget victim = victimIdentity.GetComponent<PlayerTheftTarget>();
            if (victim != null && victim.activeThief == netIdentity) victim.activeThief = null;
        }
    }
}

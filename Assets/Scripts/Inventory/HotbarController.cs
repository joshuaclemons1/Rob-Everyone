using RobEveryone.Input;
using RobEveryone.Player;
using UnityEngine;

namespace RobEveryone.Inventory
{
    // Reads scroll wheel + number keys 1-5 (via InputManager.Gameplay)
    // and selects the matching hotbar slot. Pure input, no visuals --
    // HotbarUI reacts to whatever PlayerInventory.SelectedSlot ends up
    // being.
    //
    // Networking (Stage 4): selection is server-authoritative (Selected
    // Slot is a SyncVar on PlayerInventory), so this sends Commands
    // instead of calling SelectSlot directly -- and only the owner
    // should ever be reading their own keyboard/scroll for this, not a
    // remote player's copy.
    [RequireComponent(typeof(PlayerInventory))]
    public class HotbarController : MonoBehaviour
    {
        private PlayerInventory inventory;
        private CarryController carry;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            carry = GetComponent<CarryController>();
        }

        private void Update()
        {
            if (!inventory.isOwned || RobEveryone.UI.InventoryScreenUI.MenuOpen) return;
            // Hands full while carrying a body -- no slot is selected and
            // the number keys / scroll do nothing (PlayerInventory forces
            // SelectedSlot to -1 for the duration).
            if (carry != null && carry.IsCarrying) return;

            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                if (InputManager.Gameplay.Hotbar(i).WasPressedThisFrame())
                {
                    inventory.CmdSelectSlot(i);
                }
            }

            float scroll = InputManager.Gameplay.Scroll.ReadValue<Vector2>().y;
            if (scroll > 0f) inventory.CmdSelectRelative(-1);
            else if (scroll < 0f) inventory.CmdSelectRelative(1);
        }
    }
}

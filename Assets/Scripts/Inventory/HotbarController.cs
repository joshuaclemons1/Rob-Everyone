using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Inventory
{
    // Reads scroll wheel + number keys 1-5 and selects the matching
    // hotbar slot. Pure input, no visuals -- HotbarUI reacts to whatever
    // PlayerInventory.SelectedSlot ends up being. Mirrors
    // FirstPersonController's existing pattern of reading
    // Keyboard.current/Mouse.current directly (no Input Actions asset in
    // this project yet).
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

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            if (!inventory.isOwned || RobEveryone.UI.InventoryScreenUI.MenuOpen) return;

            if (Keyboard.current != null)
            {
                for (int i = 0; i < PlayerInventory.SlotCount; i++)
                {
                    if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                    {
                        inventory.CmdSelectSlot(i);
                    }
                }
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0f) inventory.CmdSelectRelative(-1);
                else if (scroll < 0f) inventory.CmdSelectRelative(1);
            }
        }
    }
}

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
            if (Keyboard.current != null)
            {
                for (int i = 0; i < PlayerInventory.SlotCount; i++)
                {
                    if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                    {
                        inventory.SelectSlot(i);
                    }
                }
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0f) inventory.SelectRelative(-1);
                else if (scroll < 0f) inventory.SelectRelative(1);
            }
        }
    }
}

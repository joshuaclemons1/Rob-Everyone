using RobEveryone.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Inventory
{
    // Press Q: drop whatever's in your currently selected hotbar slot
    // into the world just in front of you, floating at pickup height
    // (PlayerInventory.SelectedSlot is server-authoritative and already
    // resolved to a head, so a multi-slot item drops whole). Mirrors
    // HotbarController's "owner polls Keyboard.current, mutation goes
    // through a Command on PlayerInventory" shape -- a plain MonoBehaviour
    // can't declare a [Command] itself.
    //
    // The dropped item has no Rigidbody (like all loot), so it just
    // hangs where it's placed -- PickupItem's `dropped` flag makes it
    // spin/bob there. Tune dropForward / dropHeight until it lands where
    // it looks right in front of the character.
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerDropController : MonoBehaviour
    {
        [SerializeField] private float dropForward = 1.0f;  // metres ahead of the player
        [SerializeField] private float dropHeight = -0.4f;  // metres from the player transform origin (negative = toward the feet)
        [SerializeField] private Key dropKey = Key.Q;

        private PlayerInventory inventory;

        private void Awake() => inventory = GetComponent<PlayerInventory>();

        private void Update()
        {
            if (!inventory.isOwned || InventoryScreenUI.MenuOpen) return;
            if (Keyboard.current == null || !Keyboard.current[dropKey].wasPressedThisFrame) return;

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 pos = transform.position + flatForward * dropForward + Vector3.up * dropHeight;
            Quaternion rot = Quaternion.LookRotation(flatForward, Vector3.up);
            inventory.CmdDropSelected(pos, rot);
        }
    }
}

using System.Collections;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.UI
{
    // Ties the 5 hotbar slot boxes to *this client's own*
    // PlayerInventory.LocalPlayer (Stage 4) -- never another connected
    // player's copy. The local player spawns asynchronously after
    // connecting, so Start polls for it rather than assuming it already
    // exists the way a single-player Awake lookup safely could.
    public class HotbarUI : MonoBehaviour
    {
        [SerializeField] private HotbarSlotUI[] slots; // exactly PlayerInventory.SlotCount, left to right

        private PlayerInventory inventory;

        private void Start()
        {
            StartCoroutine(WaitForLocalPlayer());
        }

        private IEnumerator WaitForLocalPlayer()
        {
            while (inventory == null)
            {
                inventory = PlayerInventory.LocalPlayer;
                if (inventory != null) break;
                yield return null;
            }

            inventory.OnSlotsChanged += Refresh;
            inventory.OnSelectedSlotChanged += RefreshSelection;
            Refresh();
            RefreshSelection(inventory.SelectedSlot);
        }

        private void OnDisable()
        {
            if (inventory == null) return;

            inventory.OnSlotsChanged -= Refresh;
            inventory.OnSelectedSlotChanged -= RefreshSelection;
        }

        private void Refresh()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].SetItem(inventory.Slots[i]);
            }
        }

        private void RefreshSelection(int selected)
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].SetSelected(i == selected);
            }
        }
    }
}

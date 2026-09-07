using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.UI
{
    // Ties the 5 hotbar slot boxes to PlayerInventory's actual slot data
    // and selection state. Leave `Inventory` unassigned for a UI living
    // in a scene the Player doesn't exist in at edit time (e.g. the
    // Lobby) -- same auto-find fallback InventoryUI/EconomyBarsUI use.
    public class HotbarUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private HotbarSlotUI[] slots; // exactly PlayerInventory.SlotCount, left to right

        private void Awake()
        {
            if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (inventory == null) return;

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

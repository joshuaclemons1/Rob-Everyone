using System;
using System.Collections.Generic;
using RobEveryone.Items;
using UnityEngine;

namespace RobEveryone.Inventory
{
    // A single carried item -- just the catalog entry it came from, so
    // name/value/icon all stay in one place (ItemDefinition) instead of
    // being copied into the slot. Nullable slot entries (InventorySlot?)
    // represent an empty slot without needing a separate "is this slot
    // used" flag.
    public struct InventorySlot
    {
        public ItemDefinition Item;
    }

    // Tracks what a single player has stolen this round, in a real
    // 5-slot capacity-limited inventory (gameplay-design.md's "5 shared
    // carry slots") rather than an unlimited running list -- this is what
    // HotbarUI displays and HotbarController selects between. One of
    // these lives on the Player object.
    public class PlayerInventory : MonoBehaviour
    {
        public const int SlotCount = 5;

        private readonly InventorySlot?[] slots = new InventorySlot?[SlotCount];

        public IReadOnlyList<InventorySlot?> Slots => slots;
        public int SelectedSlot { get; private set; }

        // Separate from TotalValue (this round's carried loot, at risk
        // until sold) -- Cash is the safe, banked balance that persists
        // across rounds, per gameplay-design.md's Cash/carried split.
        public int Cash { get; private set; }

        // Computed from the slots each time, not cached -- always
        // correct, no risk of drifting from the slot array through some
        // missed update path.
        public int TotalValue
        {
            get
            {
                int total = 0;
                foreach (InventorySlot? slot in slots)
                {
                    if (slot.HasValue) total += slot.Value.Item.Value;
                }
                return total;
            }
        }

        // Kept alongside OnSlotsChanged so InventoryUI/EconomyBarsUI (both
        // written against the old running-total model) don't need to
        // change their subscriptions at all.
        public event Action<int> OnTotalValueChanged;
        public event Action OnSlotsChanged;
        public event Action<int> OnSelectedSlotChanged;
        public event Action<int> OnCashChanged;

        // Returns false (and leaves the item untouched) if every slot is
        // full -- PickupItem only deactivates the world item on success.
        public bool AddItem(ItemDefinition item)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new InventorySlot { Item = item };
                    NotifySlotsChanged();
                    return true;
                }
            }

            return false;
        }

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;

            SelectedSlot = index;
            OnSelectedSlotChanged?.Invoke(SelectedSlot);
        }

        // direction is +1/-1 -- wraps around both ends, for scroll wheel.
        public void SelectRelative(int direction)
        {
            SelectSlot(((SelectedSlot + direction) % SlotCount + SlotCount) % SlotCount);
        }

        public void ResetInventory()
        {
            for (int i = 0; i < SlotCount; i++) slots[i] = null;
            NotifySlotsChanged();
        }

        // Banks the current carried value into Cash, then clears carried
        // loot the same way ResetInventory does -- called from the
        // Lobby's SellStation, never automatically.
        public void SellCarried()
        {
            int total = TotalValue;
            if (total <= 0) return;

            Cash += total;
            OnCashChanged?.Invoke(Cash);
            ResetInventory();
        }

        // Anti-hoarding: called by GameFlowManager on a batch's final
        // round -- Cash above the batch quota is deleted rather than
        // banked indefinitely, per gameplay-design.md's Quota Batches
        // section.
        public void WipeCashSurplus(int quota)
        {
            if (Cash <= quota) return;

            Cash = quota;
            OnCashChanged?.Invoke(Cash);
        }

        private void NotifySlotsChanged()
        {
            OnSlotsChanged?.Invoke();
            OnTotalValueChanged?.Invoke(TotalValue);
        }
    }
}

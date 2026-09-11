using System.Collections;
using System.Collections.Generic;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.UI
{
    // Ties a row of hotbar slot boxes to a PlayerInventory.
    //
    // Default (bindToLocalPlayer = true): binds to *this client's own*
    // PlayerInventory.LocalPlayer -- never another connected player's
    // copy. The local player spawns asynchronously after connecting, so
    // Start polls for it.
    //
    // bindToLocalPlayer = false: stays idle until Bind() is called with
    // an explicit inventory. Used by InventoryScreenUI's steal screen to
    // show a *victim's* hotbar above the thief's own.
    public class HotbarUI : MonoBehaviour
    {
        [SerializeField] private HotbarSlotUI[] slots; // exactly PlayerInventory.SlotCount, left to right
        [SerializeField] private bool bindToLocalPlayer = true;

        private PlayerInventory inventory;
        public PlayerInventory BoundInventory => inventory;

        private void Start()
        {
            if (bindToLocalPlayer) StartCoroutine(WaitForLocalPlayer());
        }

        private IEnumerator WaitForLocalPlayer()
        {
            PlayerInventory found = null;
            while (found == null)
            {
                found = PlayerInventory.LocalPlayer;
                if (found != null) break;
                yield return null;
            }
            Bind(found);
        }

        // Rebinds (or, with null, clears) this row. Safe to call
        // repeatedly.
        public void Bind(PlayerInventory inv)
        {
            if (inventory != null)
            {
                inventory.OnSlotsChanged -= Refresh;
                inventory.OnSelectedSlotChanged -= RefreshSelection;
            }

            inventory = inv;

            if (inventory != null)
            {
                // Cooldown countdown text (HotbarSlotUI) only ever makes
                // sense for the row actually showing the local player's
                // own items -- the steal screen rebinds this same row
                // to a victim's PlayerInventory, where the viewer's own
                // SabotageUseController cooldowns are irrelevant.
                bool isOwnHotbar = inventory == PlayerInventory.LocalPlayer;
                if (slots != null)
                {
                    foreach (HotbarSlotUI slot in slots)
                    {
                        if (slot != null) slot.SetCooldownDisplayEnabled(isOwnHotbar);
                    }
                }

                inventory.OnSlotsChanged += Refresh;
                inventory.OnSelectedSlotChanged += RefreshSelection;
                Refresh();
                RefreshSelection(inventory.SelectedSlot);
            }
            else
            {
                BlankAll();
            }
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.OnSlotsChanged -= Refresh;
            inventory.OnSelectedSlotChanged -= RefreshSelection;
        }

        private void BlankAll()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].gameObject.SetActive(true);
                slots[i].SetItem(null);
                slots[i].SetSpan(slots[i].BaseAnchoredX, slots[i].BaseWidth);
                slots[i].SetSelected(false);
            }
        }

        // A bulky item's box merges into one wide rectangle instead of
        // repeating its icon across N separate same-size boxes: a span
        // of 0 means this index is covered by an earlier head and gets
        // hidden entirely, a span of 1+ means this index is a head (a
        // real item, or just an empty slot) whose box should cover that
        // many slot-widths. See PlayerInventory.SlotSpanLengths.
        //
        // The hotbar prefab positions each slot box by hand (fixed
        // anchoredPosition/sizeDelta, no Horizontal Layout Group), so the
        // merged box's center/width is computed here from the head's own
        // and its covered siblings' original authored geometry
        // (HotbarSlotUI.BaseAnchoredX/BaseWidth) rather than handed off
        // to a layout system.
        private void Refresh()
        {
            if (slots == null || inventory == null) return;

            IReadOnlyList<int> spans = inventory.SlotSpanLengths;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;

                int span = i < spans.Count ? spans[i] : 1;
                bool isHead = span > 0;

                slots[i].gameObject.SetActive(isHead);
                if (!isHead) continue;

                slots[i].SetItem(inventory.Slots[i]);

                int lastCovered = Mathf.Clamp(i + span - 1, i, slots.Length - 1);
                float centerX = (slots[i].BaseAnchoredX + slots[lastCovered].BaseAnchoredX) / 2f;
                float width = (slots[lastCovered].BaseAnchoredX - slots[i].BaseAnchoredX) + slots[i].BaseWidth;
                slots[i].SetSpan(centerX, width);
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

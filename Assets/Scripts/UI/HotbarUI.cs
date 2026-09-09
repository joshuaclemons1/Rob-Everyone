using System.Collections;
using System.Collections.Generic;
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
            if (slots == null) return;

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

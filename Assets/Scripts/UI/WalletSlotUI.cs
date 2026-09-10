using System.Collections;
using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.UI
{
    // The always-visible Prison Wallet box on the HUD, next to the
    // hotbar. Shows what's vaulted (gameplay-design.md's "carry slots
    // readout") so you don't need Tab open to know what you'd keep if
    // caught.
    //
    // The box IS a hotbar slot box -- it must be built by duplicating a
    // Slot so it comes with a fully-wired HotbarSlotUI (model preview
    // stage + text refs). This just feeds that HotbarSlotUI the wallet
    // item; put an InventoryDragSlot (Kind = MyWallet) on the same
    // GameObject for the drag half.
    //
    // Lock indicator: while an item's in the wallet it's either LOCKED
    // (mid-round -- can't be swapped until the shop phase) or UNLOCKED
    // (in the Lobby -- drag it out to a hotbar slot to sell it). Both
    // are small child Image objects; neither shows while the wallet is
    // empty. Wire whichever you have -- either alone is fine.
    [RequireComponent(typeof(HotbarSlotUI))]
    public class WalletSlotUI : MonoBehaviour
    {
        [SerializeField] private GameObject lockedIcon;
        [SerializeField] private GameObject unlockedIcon;

        private HotbarSlotUI display;
        private PlayerInventory inventory;

        private void Awake()
        {
            display = GetComponent<HotbarSlotUI>(); // always this box's own
            if (lockedIcon != null) lockedIcon.SetActive(false);
            if (unlockedIcon != null) unlockedIcon.SetActive(false);
        }

        private void Start()
        {
            Refresh(); // show "empty" right away, before the local player resolves
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
            inventory.OnWalletChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnWalletChanged -= Refresh;
        }

        private void Update()
        {
            if (inventory == null) return;

            bool filled = inventory.WalletFilled;
            bool inRound = GameFlowManager.Instance != null && GameFlowManager.Instance.InGameplayScene;

            if (lockedIcon != null) lockedIcon.SetActive(filled && inRound);
            if (unlockedIcon != null) unlockedIcon.SetActive(filled && !inRound);
        }

        private void Refresh()
        {
            if (display == null) return;

            bool filled = inventory != null && inventory.WalletFilled;
            display.SetItem(filled
                ? new InventorySlot { Item = inventory.WalletItem, RemainingUses = inventory.WalletUses }
                : (InventorySlot?)null);
        }
    }
}

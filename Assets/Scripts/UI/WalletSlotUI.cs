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
    // Locked indicator: once something's in the wallet during a gameplay
    // round it can't be swapped out until the shop phase -- the lock
    // object makes that legible rather than the drag just silently
    // failing. It's any small child object you make (an Image, or a TMP
    // set to a padlock glyph) -- nothing to download.
    [RequireComponent(typeof(HotbarSlotUI))]
    public class WalletSlotUI : MonoBehaviour
    {
        [SerializeField] private GameObject lockedIcon;

        private HotbarSlotUI display;
        private PlayerInventory inventory;

        private void Awake()
        {
            display = GetComponent<HotbarSlotUI>(); // always this box's own
            if (lockedIcon != null) lockedIcon.SetActive(false);
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
            if (lockedIcon == null || inventory == null) return;

            bool inRound = GameFlowManager.Instance != null && GameFlowManager.Instance.InGameplayScene;
            lockedIcon.SetActive(inventory.WalletFilled && inRound);
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

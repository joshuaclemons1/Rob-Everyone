using System.Collections;
using RobEveryone.Core;
using RobEveryone.Inventory;
using RobEveryone.Items;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // The always-visible Prison Wallet box on the HUD, next to the
    // hotbar. Shows what's vaulted (gameplay-design.md's "carry slots
    // readout" HUD need) so you don't need Tab open to know what you'd
    // keep if caught. Put an InventoryDragSlot (Kind = MyWallet) on the
    // same GameObject for the drag half.
    //
    // Locked indicator: once something is in the wallet during a gameplay
    // round it can't be swapped out until the shop phase -- the lock icon
    // makes that legible rather than the drag just silently failing.
    public class WalletSlotUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text usesText;
        [SerializeField] private GameObject emptyHint;   // e.g. a faint "wallet" watermark, shown when empty
        [SerializeField] private GameObject lockedIcon;  // shown when filled during a gameplay round

        private PlayerInventory inventory;

        private void Start() => StartCoroutine(WaitForLocalPlayer());

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
            // The lock state depends on the round phase, which isn't a
            // wallet event -- cheap enough to reconcile each frame.
            if (inventory != null) RefreshLock();
        }

        private void Refresh()
        {
            if (inventory == null) return;

            ItemDefinition item = inventory.WalletItem;
            bool filled = item != null;

            if (nameText != null) nameText.text = filled ? item.ItemName : "";
            if (usesText != null)
            {
                bool showUses = filled && item.MaxUses > 0;
                usesText.text = showUses ? $"x{inventory.WalletUses}" : "";
                usesText.enabled = showUses;
            }
            if (emptyHint != null) emptyHint.SetActive(!filled);

            RefreshLock();
        }

        private void RefreshLock()
        {
            if (lockedIcon == null) return;
            bool filled = inventory.WalletFilled;
            bool inRound = GameFlowManager.Instance != null && GameFlowManager.Instance.InGameplayScene;
            lockedIcon.SetActive(filled && inRound);
        }
    }
}

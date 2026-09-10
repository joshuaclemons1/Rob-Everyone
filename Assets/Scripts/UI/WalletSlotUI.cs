using System.Collections;
using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.UI
{
    // The always-visible Prison Wallet box on the HUD, next to the
    // hotbar. Shows what's vaulted (gameplay-design.md's "carry slots
    // readout") so you don't need Tab open to know what you'd keep if
    // caught. The box itself is a HotbarSlotUI (same 3D-model preview as
    // a hotbar slot) which this just feeds; put an InventoryDragSlot
    // (Kind = MyWallet) on the same GameObject for the drag half.
    //
    // Locked indicator: once something's in the wallet during a gameplay
    // round it can't be swapped out until the shop phase -- the lock icon
    // makes that legible rather than the drag just silently failing.
    [RequireComponent(typeof(HotbarSlotUI))]
    public class WalletSlotUI : MonoBehaviour
    {
        [SerializeField] private HotbarSlotUI display;   // usually this same GameObject's HotbarSlotUI
        [SerializeField] private GameObject lockedIcon;  // shown when filled during a gameplay round

        private PlayerInventory inventory;

        private void Awake()
        {
            if (display == null) display = GetComponent<HotbarSlotUI>();
        }

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
            // Lock state depends on the round phase, not a wallet event.
            if (inventory != null && lockedIcon != null)
            {
                bool inRound = GameFlowManager.Instance != null && GameFlowManager.Instance.InGameplayScene;
                lockedIcon.SetActive(inventory.WalletFilled && inRound);
            }
        }

        private void Refresh()
        {
            if (inventory == null || display == null) return;

            display.SetItem(inventory.WalletFilled
                ? new InventorySlot { Item = inventory.WalletItem, RemainingUses = inventory.WalletUses }
                : (InventorySlot?)null);
        }
    }
}

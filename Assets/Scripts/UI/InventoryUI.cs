using System.Collections;
using RobEveryone.Inventory;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Displays this client's own running total -- never another
    // connected player's, which is why this resolves PlayerInventory.
    // LocalPlayer (Stage 4) instead of a plain FindFirstObjectByType that
    // could just as easily find someone else's copy. Drag a
    // TextMeshProUGUI into `moneyText`/`cashText`.
    //
    // The local player object spawns asynchronously after connecting, so
    // it may not exist yet the instant this UI's scene loads -- Start
    // waits (polling once a frame) rather than assuming it's already
    // there the way a single-player Awake lookup safely could.
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI cashText;

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

            inventory.OnTotalValueChanged += UpdateText;
            inventory.OnCashChanged += UpdateCashText;
            UpdateText(inventory.TotalValue);
            UpdateCashText(inventory.Cash);
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.OnTotalValueChanged -= UpdateText;
            inventory.OnCashChanged -= UpdateCashText;
        }

        private void UpdateText(int total)
        {
            if (moneyText != null)
            {
                moneyText.text = $"${total}";
            }
        }

        private void UpdateCashText(int cash)
        {
            if (cashText != null)
            {
                cashText.text = $"${cash}";
            }
        }
    }
}

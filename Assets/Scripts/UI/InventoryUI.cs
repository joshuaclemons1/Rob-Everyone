using RobEveryone.Inventory;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Displays a single player's running total. Drag the Player's
    // PlayerInventory into `inventory` and a TextMeshProUGUI into `moneyText`.
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TextMeshProUGUI moneyText;

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.OnTotalValueChanged += UpdateText;
            UpdateText(inventory.TotalValue);
        }

        private void OnDisable()
        {
            if (inventory == null) return;
            inventory.OnTotalValueChanged -= UpdateText;
        }

        private void UpdateText(int total)
        {
            if (moneyText != null)
            {
                moneyText.text = $"${total}";
            }
        }
    }
}

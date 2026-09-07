using RobEveryone.Inventory;
using TMPro;
using UnityEngine;

namespace RobEveryone.UI
{
    // Displays a single player's running total. Drag the Player's
    // PlayerInventory into `inventory` and a TextMeshProUGUI into `moneyText`.
    // Leave `inventory` unassigned for a UI living in a scene the Player
    // doesn't exist in at edit time (e.g. the Lobby, where the Player is
    // only ever present at runtime via GameFlowManager's
    // DontDestroyOnLoad carry-over) -- Awake falls back to finding it,
    // same pattern PoliceAI/HomeownerAI already use for their player
    // reference.
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI cashText;

        private void Awake()
        {
            if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (inventory == null) return;
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

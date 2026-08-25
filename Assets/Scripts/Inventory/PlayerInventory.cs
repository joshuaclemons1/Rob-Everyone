using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Inventory
{
    // Tracks what a single player has stolen this round. One of these lives
    // on the Player object. The Round Manager (Stage 3) will read TotalValue
    // against the round's quota and reset it between rounds.
    public class PlayerInventory : MonoBehaviour
    {
        private readonly List<string> carriedItems = new();

        public int TotalValue { get; private set; }
        public IReadOnlyList<string> CarriedItems => carriedItems;

        public event Action<int> OnTotalValueChanged;

        public void AddItem(string itemName, int value)
        {
            carriedItems.Add(itemName);
            TotalValue += value;
            OnTotalValueChanged?.Invoke(TotalValue);
        }

        public void ResetInventory()
        {
            carriedItems.Clear();
            TotalValue = 0;
            OnTotalValueChanged?.Invoke(TotalValue);
        }
    }
}

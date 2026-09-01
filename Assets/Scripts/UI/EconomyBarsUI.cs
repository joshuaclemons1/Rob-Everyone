using RobEveryone.Inventory;
using RobEveryone.Items;
using RobEveryone.Round;
using UnityEngine;

namespace RobEveryone.UI
{
    // Drives the two stacked pixel-art level bars: Quota (starts full,
    // drains as the player earns toward it) and Cash (starts empty,
    // fills toward the combined value of every PickupItem present when
    // the round starts).
    //
    // Wired against today's single-total economy (PlayerInventory.TotalValue,
    // RoundManager.Quota) -- gameplay-design.md's Cash/Wallet/batch-quota
    // rework isn't implemented yet, so "Cash" here just means the current
    // running total, not the future banked-vs-carried split. Revisit this
    // script's math once that rework lands.
    public class EconomyBarsUI : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private LevelBarUI quotaBar;
        [SerializeField] private LevelBarUI cashBar;

        private int maxPossibleValue;

        private void Start()
        {
            // Snapshot at round start, per design -- doesn't update if
            // loot despawns/respawns mid-round. NOTE: once Stage 3g's
            // HousePoolSpawner instantiates houses at runtime, this needs
            // to run *after* that spawn happens (Script Execution Order,
            // or have the spawner call a public RecalculateMax() here)
            // or it'll sum zero/partial items depending on Start() order.
            maxPossibleValue = 0;
            foreach (PickupItem item in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            {
                maxPossibleValue += item.Value;
            }
        }

        private void Update()
        {
            if (playerInventory == null) return;

            if (quotaBar != null && roundManager != null && roundManager.Quota > 0)
            {
                float remaining = 1f - (float)playerInventory.TotalValue / roundManager.Quota;
                quotaBar.SetRatio(remaining);
            }

            if (cashBar != null && maxPossibleValue > 0)
            {
                float earned = (float)playerInventory.TotalValue / maxPossibleValue;
                cashBar.SetRatio(earned);
            }
        }
    }
}

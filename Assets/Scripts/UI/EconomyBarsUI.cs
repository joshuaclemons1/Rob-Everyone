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
    // RoundManager.Quota) -- a real, persistent PlayerInventory.Cash now
    // exists (see the Lobby's SellStation), but there's still no natural
    // denominator for it without gameplay-design.md's batch/tier system,
    // so this bar stays a TotalValue-based proxy for now, not real Cash.
    // Revisit this script's math once batches land.
    public class EconomyBarsUI : MonoBehaviour
    {
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private LevelBarUI quotaBar;
        [SerializeField] private LevelBarUI cashBar;

        private PlayerInventory playerInventory;
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
            // Resolved lazily (not cached in Awake) since this client's
            // own player object (Stage 4) may not have spawned yet the
            // instant this UI's scene loads -- and must be *this*
            // client's own, never another connected player's copy, which
            // is what a plain FindFirstObjectByType risked returning.
            if (playerInventory == null) playerInventory = PlayerInventory.LocalPlayer;
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

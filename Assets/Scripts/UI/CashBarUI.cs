using RobEveryone.Core;
using RobEveryone.Inventory;
using UnityEngine;

namespace RobEveryone.UI
{
    // Fills toward overall progress on the current quota -- Cash already
    // banked plus whatever's currently carried, unlike EconomyBarsUI's
    // cashBar (a carried-loot-only proxy that always reads $0 outside of
    // an active round, and ignores Cash entirely). So starting a round
    // with Cash already saved up shows the bar partly filled before
    // you've picked anything up, and picking up loot fills it further on
    // top of that. Fills toward GameFlowManager.LastQuota, since
    // RoundManager itself isn't persistent and gameplay-design.md's real
    // batch/tier target doesn't exist yet -- the most recent round's
    // quota is a reasonable placeholder denominator until it does.
    public class CashBarUI : MonoBehaviour
    {
        [SerializeField] private LevelBarUI bar;

        private PlayerInventory playerInventory;

        private void Awake()
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void Update()
        {
            if (bar == null || playerInventory == null || GameFlowManager.Instance == null) return;

            int quota = GameFlowManager.Instance.LastQuota;
            if (quota <= 0) return;

            int progress = playerInventory.Cash + playerInventory.TotalValue;
            bar.SetRatio((float)progress / quota);
        }
    }
}

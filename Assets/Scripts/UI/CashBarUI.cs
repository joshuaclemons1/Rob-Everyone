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
    // top of that. Fills toward GameFlowManager.CurrentQuota, the real
    // persistent batch quota (Stage 7b) -- always correct across the
    // Lobby round-trip, unlike RoundManager's own Quota field, which is
    // just a fresh copy of this taken at the start of whichever round
    // happens to currently exist.
    public class CashBarUI : MonoBehaviour
    {
        [SerializeField] private LevelBarUI bar;

        private PlayerInventory playerInventory;

        private void Update()
        {
            // Resolved lazily each frame (not cached in Awake/Start) since
            // this client's own player object (Stage 4) may not have
            // spawned yet the instant this UI's scene loads.
            if (playerInventory == null) playerInventory = PlayerInventory.LocalPlayer;
            if (bar == null || playerInventory == null || GameFlowManager.Instance == null) return;

            int quota = GameFlowManager.Instance.CurrentQuota;
            if (quota <= 0) return;

            int progress = playerInventory.Cash + playerInventory.TotalValue;
            bar.SetRatio((float)progress / quota);
        }
    }
}

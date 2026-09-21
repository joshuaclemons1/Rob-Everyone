using RobEveryone.Core;
using RobEveryone.Interaction;
using RobEveryone.Inventory;
using RobEveryone.Items;
using UnityEngine;

namespace RobEveryone.Shop
{
    // A pawn-shop shelf holding one sabotage item -- walk up, press E,
    // it's deducted from your Cash and added straight to your selected
    // hotbar slot, same as picking it up. Deliberately not a PickupItem:
    // supply never depletes (every player can always buy the same item
    // off the same shelf, unlike contested house loot), so there's no
    // "taken" flag or NetworkServer.Destroy -- the shelf just sits there
    // and stays interactable.
    [RequireComponent(typeof(Collider))]
    public class ShopShelfItem : MonoBehaviour, IInteractable, IInteractableWarning
    {
        [SerializeField] private ItemDefinition item;
        // Which batch this item unlocks at -- gameplay-design.md calls
        // for items gated behind quota tier, not all available from the
        // start. Proposed starter progression (tune later): 1 = Taser
        // (cheap, recharges), 2 = Bat/Alarm Clock, 3 = Hammer/Tranq Gun,
        // 4 = Dynamite (highest tier, the only item that can hit
        // multiple rivals at once).
        [SerializeField] private int unlockBatch = 1;

        // Read by BatchUnlockPopupUI (issue #61) to find what's newly
        // unlocked -- exposed rather than making that code reach past the
        // SerializeFields directly. Item is the full ItemDefinition (not
        // just its name) since the popup needs WorldModelPrefab for its
        // spinning preview too.
        public int UnlockBatch => unlockBatch;
        public ItemDefinition Item => item;

        private bool Unlocked =>
            GameFlowManager.Instance != null && GameFlowManager.Instance.BatchNumber >= unlockBatch;

        // Reuses the same curve quota itself grows on (GameFlowManager's
        // quotaGrowthMultiplier) rather than a second, separately-tuned
        // price curve -- batch 1 = base price, batch 2 = x1.5, etc.
        public int CurrentPrice
        {
            get
            {
                if (item == null) return 0;
                if (GameFlowManager.Instance == null) return item.Value;

                float multiplier = Mathf.Pow(GameFlowManager.Instance.QuotaGrowthMultiplier,
                    GameFlowManager.Instance.BatchNumber - 1);
                return Mathf.RoundToInt(item.Value * multiplier);
            }
        }

        public string InteractionPrompt =>
            item == null ? "" : Unlocked ? $"Buy {item.ItemName} for ${CurrentPrice}" : $"Locked until Batch {unlockBatch}";

        // Deliberately doesn't gate on Unlocked -- Interactor.FindTarget
        // only ever shows a prompt at all when CanInteract is true, so
        // gating this on Unlocked too would silently swallow the
        // "Locked until Batch N" text above along with it (confirmed
        // bug: a locked shelf never showed any prompt, not even the
        // locked message). A locked shelf should still be a valid,
        // visible target -- Interact() below is what actually enforces
        // the lock, by simply no-oping.
        public bool CanInteract => item != null;

        // Shown as a red sub-line under the main prompt (CrosshairUI) --
        // only relevant once the item's actually unlocked, since "Locked
        // until Batch N" already says everything the locked case needs.
        public string WarningText
        {
            get
            {
                if (item == null || !Unlocked) return "";
                PlayerInventory local = PlayerInventory.LocalPlayer;
                return local != null && local.Cash < CurrentPrice ? "Not enough cash!" : "";
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!Unlocked) return;
            PlayerInventory buyer = interactor.GetComponent<PlayerInventory>();
            buyer?.TryPurchase(item, CurrentPrice);
        }
    }
}

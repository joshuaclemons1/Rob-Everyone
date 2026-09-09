using System;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Items;
using UnityEngine;

namespace RobEveryone.Inventory
{
    // A single carried item -- just the catalog entry it came from, so
    // name/value/icon all stay in one place (ItemDefinition) instead of
    // being copied into the slot. Nullable slot entries (InventorySlot?)
    // represent an empty slot without needing a separate "is this slot
    // used" flag.
    public struct InventorySlot
    {
        public ItemDefinition Item;
    }

    // Tracks what a single player has stolen this round, in a real
    // 5-slot capacity-limited inventory (gameplay-design.md's "5 shared
    // carry slots") rather than an unlimited running list -- this is what
    // HotbarUI displays and HotbarController selects between. One of
    // these lives on the Player object.
    //
    // Networking (Stage 4): server-authoritative. Mirror can't sync a
    // direct ScriptableObject reference, so the real slot state is a
    // SyncList<string> of item names (empty string = empty slot) backed
    // by an ItemCatalog to resolve a name back to its ItemDefinition on
    // every client -- the local InventorySlot?[] view below is rebuilt
    // from that SyncList's hook rather than being the source of truth
    // itself. AddItem/SellCarried/etc. only ever run on the server now
    // (called from server-context code -- Interactor's Command, or
    // SellStation's Command), never called directly by a client.
    public class PlayerInventory : NetworkBehaviour
    {
        public const int SlotCount = 5;

        // Written into slotItemNames for every slot a bulky item occupies
        // *after* its first (head) slot -- ItemDefinition.InventorySize
        // (see item-creation.md's price table) is how many slots an item
        // costs, but the underlying sync representation is still just one
        // name per slot (no schema change needed for Mirror). A control
        // character prefix guarantees this can never collide with a real
        // ItemDefinition.ItemName. AddItem always writes a head followed
        // immediately by exactly (InventorySize - 1) of these, with no
        // gap -- RebuildSlotsFromSync and TotalValue both rely on that
        // invariant.
        private const string ContinuationMarker = "continued";

        [SerializeField] private ItemCatalog catalog;

        // Every connected player's PlayerInventory, server-side only --
        // AI scripts (HomeownerAI/PoliceAI) read this instead of
        // FindFirstObjectByType, which only ever found the one player
        // that existed before multiplayer.
        public static readonly List<PlayerInventory> AllPlayers = new();

        // The convenience every UI script needs: "my own" inventory, not
        // just any player's -- every client has a full local copy of
        // every connected player, so a plain FindFirstObjectByType could
        // just as easily return someone else's. Null until this client's
        // own player object has actually spawned (see each UI script's
        // WaitForLocalPlayer coroutine).
        public static PlayerInventory LocalPlayer =>
            NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<PlayerInventory>() : null;

        private readonly SyncList<string> slotItemNames = new();
        private readonly InventorySlot?[] slots = new InventorySlot?[SlotCount];

        public IReadOnlyList<InventorySlot?> Slots => slots;

        [SyncVar(hook = nameof(OnSelectedSlotChangedHook))]
        private int selectedSlot;
        public int SelectedSlot => selectedSlot;

        // Separate from TotalValue (this round's carried loot, at risk
        // until sold) -- Cash is the safe, banked balance that persists
        // across rounds, per gameplay-design.md's Cash/carried split.
        [SyncVar(hook = nameof(OnCashChangedHook))]
        private int cash;
        public int Cash => cash;

        // Computed from slotItemNames each time, not cached -- always
        // correct, no risk of drifting from the slot array through some
        // missed update path. Reads slotItemNames directly (not the
        // slots[] UI view, which repeats a bulky item's icon across
        // every slot it occupies) and only counts a head entry -- a
        // ContinuationMarker never adds value, or a fridge occupying 4
        // slots would count its own value 4 times over.
        public int TotalValue
        {
            get
            {
                int total = 0;
                for (int i = 0; i < slotItemNames.Count; i++)
                {
                    string itemName = slotItemNames[i];
                    if (string.IsNullOrEmpty(itemName) || itemName == ContinuationMarker) continue;

                    ItemDefinition item = catalog != null ? catalog.GetByName(itemName) : null;
                    if (item != null) total += item.Value;
                }
                return total;
            }
        }

        public event Action<int> OnTotalValueChanged;
        public event Action OnSlotsChanged;
        public event Action<int> OnSelectedSlotChanged;
        public event Action<int> OnCashChanged;

        private void Awake()
        {
            for (int i = 0; i < SlotCount; i++) slotItemNames.Add(string.Empty);
            // SyncList<T>.OnChange is (Operation, index, item) -- 3
            // params, not the 4-param (old, new) shape a SyncVar hook
            // uses. Ignored here regardless; any change just triggers a
            // full rebuild rather than patching one slot.
            slotItemNames.OnChange += (op, index, item) => RebuildSlotsFromSync();
        }

        public override void OnStartServer()
        {
            AllPlayers.Add(this);
        }

        public override void OnStopServer()
        {
            AllPlayers.Remove(this);
        }

        public override void OnStartClient()
        {
            RebuildSlotsFromSync();
        }

        // For UI purposes, every slot a bulky item occupies shows that
        // same item (repeated icon across its span reads clearly as "this
        // one thing takes up this much room" -- the same visual language
        // inventory-Tetris games use). A ContinuationMarker resolves to
        // whichever real item name comes immediately before it -- safe
        // because AddItem never leaves a gap between a head and its own
        // continuation slots.
        private void RebuildSlotsFromSync()
        {
            string currentOwner = null;

            for (int i = 0; i < SlotCount; i++)
            {
                string raw = i < slotItemNames.Count ? slotItemNames[i] : string.Empty;

                if (raw == ContinuationMarker)
                {
                    // currentOwner still holds whatever head this
                    // continues, from the previous iteration.
                }
                else
                {
                    currentOwner = string.IsNullOrEmpty(raw) ? null : raw;
                }

                ItemDefinition item = currentOwner != null && catalog != null ? catalog.GetByName(currentOwner) : null;
                slots[i] = item != null ? new InventorySlot { Item = item } : (InventorySlot?)null;
            }

            OnSlotsChanged?.Invoke();
            OnTotalValueChanged?.Invoke(TotalValue);
        }

        // Returns false (and leaves the item untouched) if there aren't
        // ItemDefinition.InventorySize *consecutive* free slots -- a
        // bulky item can't split across a gap. PickupItem only
        // deactivates the world item on success. Server-only: called
        // from Interactor's Command by way of PickupItem.Interact, never
        // directly by a client.
        [Server]
        public bool AddItem(ItemDefinition item)
        {
            int size = item.InventorySize;

            for (int start = 0; start <= SlotCount - size; start++)
            {
                bool allFree = true;
                for (int offset = 0; offset < size; offset++)
                {
                    if (!string.IsNullOrEmpty(slotItemNames[start + offset]))
                    {
                        allFree = false;
                        break;
                    }
                }

                if (!allFree) continue;

                slotItemNames[start] = item.ItemName; // SyncList write -- propagates to every client automatically
                for (int offset = 1; offset < size; offset++)
                {
                    slotItemNames[start + offset] = ContinuationMarker;
                }

                return true;
            }

            return false;
        }

        [Command]
        public void CmdSelectSlot(int index) => SelectSlot(index);

        [Server]
        public void SelectSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            selectedSlot = index;
        }

        // direction is +1/-1 -- wraps around both ends, for scroll wheel.
        [Command]
        public void CmdSelectRelative(int direction)
        {
            SelectSlot(((selectedSlot + direction) % SlotCount + SlotCount) % SlotCount);
        }

        [Server]
        public void ResetInventory()
        {
            for (int i = 0; i < SlotCount; i++) slotItemNames[i] = string.Empty;
        }

        // Banks the current carried value into Cash, then clears carried
        // loot the same way ResetInventory does -- called from the
        // Lobby's SellStation via a Command, never automatically.
        [Server]
        public void SellCarried()
        {
            int total = TotalValue;
            if (total <= 0) return;

            cash += total;
            ResetInventory();
        }

        // Anti-hoarding: called by GameFlowManager on a batch's final
        // round -- Cash above the batch quota is deleted rather than
        // banked indefinitely, per gameplay-design.md's Quota Batches
        // section.
        [Server]
        public void WipeCashSurplus(int quota)
        {
            if (cash <= quota) return;
            cash = quota;
        }

        private void OnCashChangedHook(int _, int newValue) => OnCashChanged?.Invoke(newValue);
        private void OnSelectedSlotChangedHook(int _, int newValue) => OnSelectedSlotChanged?.Invoke(newValue);
    }
}

using System;
using System.Collections.Generic;
using Mirror;
using RobEveryone.Core;
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
        // Mirrors PlayerInventory's slotUses -- 0 for anything with
        // ItemDefinition.MaxUses == 0 (untracked/unlimited), since it's
        // never read unless MaxUses > 0.
        public int RemainingUses;
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
        // Parallel to slotItemNames, same index semantics -- a separate
        // list rather than encoding the count into the name string, since
        // every ContinuationMarker-sensitive comparison (ResolveHead,
        // CmdSelectRelative, RebuildSlotsFromSync) does plain string
        // equality against slotItemNames and would need reworking to
        // tolerate an embedded delimiter. A continuation slot's entry is
        // always 0 (unused, only the head index is ever read).
        private readonly SyncList<int> slotUses = new();
        private readonly InventorySlot?[] slots = new InventorySlot?[SlotCount];
        // Parallel to slots[] -- 0 means "this index is covered by an
        // earlier head, don't draw a box here at all," N (>=1) means
        // "this index is a head (real item or just an empty slot) whose
        // box should visually span N slot-widths." HotbarUI reads this to
        // merge a bulky item's boxes into one wide rectangle instead of
        // repeating its icon in N separate same-size boxes.
        private readonly int[] slotSpanLength = new int[SlotCount];

        public IReadOnlyList<InventorySlot?> Slots => slots;
        public IReadOnlyList<int> SlotSpanLengths => slotSpanLength;

        [SyncVar(hook = nameof(OnSelectedSlotChangedHook))]
        private int selectedSlot;
        public int SelectedSlot => selectedSlot;

        // A human-readable label for this player -- shown as "Rob X" /
        // "Steal from: X" in the theft flow. Set by
        // RobEveryoneNetworkManager.OnServerAddPlayer (join order for
        // now; a real Steam persona name is a follow-up once per-connection
        // SteamIDs are wired -- Stage 5).
        [SyncVar] private string displayName;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? "rival" : displayName;

        [Server]
        public void SetDisplayName(string name) => displayName = name;

        // Separate from TotalValue (this round's carried loot, at risk
        // until sold) -- Cash is the safe, banked balance that persists
        // across rounds, per gameplay-design.md's Cash/carried split.
        [SyncVar(hook = nameof(OnCashChangedHook))]
        private int cash;
        public int Cash => cash;

        // The Prison Wallet (gameplay-design.md's 6th slot) -- one item of
        // any InventorySize, kept as its own two SyncVars rather than a
        // slotItemNames entry so "survives being caught" is structural:
        // ResetInventory only ever loops 0..SlotCount, so the wallet is
        // untouched for free. Placeable only during a gameplay round,
        // retrievable only in the shop/Lobby, and locked once filled for
        // the round -- those rules live in MoveToWallet/MoveFromWallet.
        [SyncVar(hook = nameof(OnWalletChangedHook))]
        private string walletItemName;
        [SyncVar]
        private int walletUses;

        public ItemDefinition WalletItem =>
            string.IsNullOrEmpty(walletItemName) || catalog == null ? null : catalog.GetByName(walletItemName);
        public int WalletUses => walletUses;
        public bool WalletFilled => !string.IsNullOrEmpty(walletItemName);

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
        public event Action OnWalletChanged;

        private void Awake()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                slotItemNames.Add(string.Empty);
                slotUses.Add(0);
            }
            // SyncList<T>.OnChange is (Operation, index, item) -- 3
            // params, not the 4-param (old, new) shape a SyncVar hook
            // uses. Ignored here regardless; any change just triggers a
            // full rebuild rather than patching one slot.
            slotItemNames.OnChange += (op, index, item) => RebuildSlotsFromSync();
            slotUses.OnChange += (op, index, item) => RebuildSlotsFromSync();
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
            int currentHeadIndex = -1;

            for (int i = 0; i < SlotCount; i++)
            {
                string raw = i < slotItemNames.Count ? slotItemNames[i] : string.Empty;

                if (raw == ContinuationMarker)
                {
                    // currentOwner still holds whatever head this
                    // continues, from the previous iteration -- and that
                    // head's span grows by one to cover this index too.
                    slotSpanLength[i] = 0;
                    if (currentHeadIndex >= 0) slotSpanLength[currentHeadIndex]++;
                }
                else
                {
                    currentOwner = string.IsNullOrEmpty(raw) ? null : raw;
                    currentHeadIndex = i;
                    slotSpanLength[i] = 1; // grows below if continuations follow; an empty slot is its own span-1 unit
                }

                ItemDefinition item = currentOwner != null && catalog != null ? catalog.GetByName(currentOwner) : null;
                int uses = i < slotUses.Count ? slotUses[i] : 0;
                slots[i] = item != null ? new InventorySlot { Item = item, RemainingUses = uses } : (InventorySlot?)null;
            }

            OnSlotsChanged?.Invoke();
            OnTotalValueChanged?.Invoke(TotalValue);
        }

        // Only ever tries the *currently selected* slot -- deliberately
        // does not fall back to scanning for the next free run elsewhere.
        // Picking something up while your selected slot can't fit it
        // (occupied, or not enough room left for a bulky item starting
        // there) just fails, item stays in the world. This is the
        // intended lead-in to a future feature: the selected slot is
        // meant to represent what's currently in your hands, so a
        // pickup always goes there specifically, not wherever happens to
        // be free. PickupItem only deactivates the world item on
        // success. Server-only: called from Interactor's Command by way
        // of PickupItem.Interact, never directly by a client.
        [Server]
        public bool AddItem(ItemDefinition item) => AddItem(item, item.MaxUses);

        // overrideUses lets a picked-back-up item (a landed, already-used
        // Hammer) keep its current remaining-uses count instead of
        // resetting to full durability -- the plain AddItem(item) above
        // covers every other pickup (a fresh world spawn always starts at
        // MaxUses).
        [Server]
        public bool AddItem(ItemDefinition item, int overrideUses)
        {
            int size = item.InventorySize;
            int start = selectedSlot;

            if (start < 0 || start + size > SlotCount) return false;

            for (int offset = 0; offset < size; offset++)
            {
                if (!string.IsNullOrEmpty(slotItemNames[start + offset])) return false;
            }

            slotItemNames[start] = item.ItemName; // SyncList write -- propagates to every client automatically
            slotUses[start] = overrideUses;
            for (int offset = 1; offset < size; offset++)
            {
                slotItemNames[start + offset] = ContinuationMarker;
                slotUses[start + offset] = 0;
            }

            return true;
        }

        // Consumes a used item (e.g. a thrown Dynamite) -- clears the head
        // slot and, like AddItem, its whole continuation span so a bulky
        // item's used-up slots don't stay stuck occupied. Server-only:
        // called from SabotageUseController's Command, never directly by
        // a client.
        [Server]
        public bool RemoveSlot(int headIndex)
        {
            if (headIndex < 0 || headIndex >= SlotCount) return false;
            if (string.IsNullOrEmpty(slotItemNames[headIndex]) || slotItemNames[headIndex] == ContinuationMarker) return false;

            slotItemNames[headIndex] = string.Empty;
            slotUses[headIndex] = 0;
            for (int i = headIndex + 1; i < SlotCount && slotItemNames[i] == ContinuationMarker; i++)
            {
                slotItemNames[i] = string.Empty;
                slotUses[i] = 0;
            }
            return true;
        }

        // Ticks down a slot with tracked durability/ammo (Bat, Hammer,
        // Tranquilizer Gun) by one use, clearing the slot once it hits 0 --
        // a no-op for an untracked item (MaxUses == 0, e.g. the Taser,
        // whose limiter is CooldownSeconds instead). Server-only: called
        // from SabotageUseController after a successful hit, never
        // directly by a client.
        [Server]
        public void DecrementUses(int headIndex)
        {
            if (headIndex < 0 || headIndex >= SlotCount) return;
            if (slotUses[headIndex] <= 0) return;

            slotUses[headIndex]--;
            if (slotUses[headIndex] <= 0) RemoveSlot(headIndex);
        }

        // Generic "put this item at exactly this head slot if it fits and
        // the whole span is clear" -- the shared primitive behind
        // retrieving from the wallet and receiving a stolen item. Returns
        // false (changes nothing) if headIndex is out of range, the item
        // would run off the end, or any covered slot is occupied.
        [Server]
        public bool TryPlaceAt(ItemDefinition item, int uses, int headIndex)
        {
            if (item == null) return false;
            int size = item.InventorySize;
            if (headIndex < 0 || headIndex + size > SlotCount) return false;

            for (int offset = 0; offset < size; offset++)
            {
                if (!string.IsNullOrEmpty(slotItemNames[headIndex + offset])) return false;
            }

            slotItemNames[headIndex] = item.ItemName;
            slotUses[headIndex] = uses;
            for (int offset = 1; offset < size; offset++)
            {
                slotItemNames[headIndex + offset] = ContinuationMarker;
                slotUses[headIndex + offset] = 0;
            }
            return true;
        }

        // Drag-and-drop within the Tab screen. Both indices are heads (a
        // continuation box isn't draggable). Moves the whole span; blocks
        // (rather than swaps) if the destination overlaps anything that
        // isn't part of the item being moved, so nudging a 2-slot item
        // one slot over still works.
        [Server]
        public bool MoveItem(int fromHead, int toHead)
        {
            if (fromHead < 0 || fromHead >= SlotCount || toHead < 0 || toHead >= SlotCount) return false;
            if (fromHead == toHead) return false;

            string name = slotItemNames[fromHead];
            if (string.IsNullOrEmpty(name) || name == ContinuationMarker) return false;

            ItemDefinition item = catalog != null ? catalog.GetByName(name) : null;
            if (item == null) return false;
            int size = item.InventorySize;
            if (toHead + size > SlotCount) return false;

            int uses = slotUses[fromHead];

            for (int offset = 0; offset < size; offset++)
            {
                int dest = toHead + offset;
                bool isOwnSlot = dest >= fromHead && dest < fromHead + size;
                if (isOwnSlot) continue;
                if (!string.IsNullOrEmpty(slotItemNames[dest])) return false;
            }

            for (int offset = 0; offset < size; offset++)
            {
                slotItemNames[fromHead + offset] = string.Empty;
                slotUses[fromHead + offset] = 0;
            }
            slotItemNames[toHead] = name;
            slotUses[toHead] = uses;
            for (int offset = 1; offset < size; offset++)
            {
                slotItemNames[toHead + offset] = ContinuationMarker;
                slotUses[toHead + offset] = 0;
            }
            return true;
        }

        [Command]
        public void CmdMoveItem(int fromHead, int toHead) => MoveItem(fromHead, toHead);

        // Hotbar head slot -> Prison Wallet. Only during a gameplay round
        // (you stash on the way in, not in the shop), and only if the
        // wallet is empty -- once filled it's locked for the round
        // (gameplay-design.md).
        [Server]
        public bool MoveToWallet(int fromHead)
        {
            if (WalletFilled) return false;
            if (!InGameplayPhase()) return false;
            if (fromHead < 0 || fromHead >= SlotCount) return false;

            string name = slotItemNames[fromHead];
            if (string.IsNullOrEmpty(name) || name == ContinuationMarker) return false;

            walletItemName = name;
            walletUses = slotUses[fromHead];
            RemoveSlot(fromHead);
            return true;
        }

        // Prison Wallet -> hotbar slot toHead. Only in the shop/Lobby
        // phase (you can't pull it back out mid-round).
        [Server]
        public bool MoveFromWallet(int toHead)
        {
            if (!WalletFilled) return false;
            if (InGameplayPhase()) return false;

            ItemDefinition item = WalletItem;
            if (item == null) return false;
            if (!TryPlaceAt(item, walletUses, toHead)) return false;

            walletItemName = string.Empty;
            walletUses = 0;
            return true;
        }

        [Command] public void CmdMoveToWallet(int fromHead) => MoveToWallet(fromHead);
        [Command] public void CmdMoveFromWallet(int toHead) => MoveFromWallet(toHead);

        // "Are we mid-round" -- the wallet's place/retrieve gate. The
        // server's active scene is reliably exactly the gameplay or Lobby
        // scene (single-mode ServerChangeScene), and GameFlowManager owns
        // both names.
        [Server]
        private bool InGameplayPhase() =>
            GameFlowManager.Instance != null && GameFlowManager.Instance.InGameplayScene;

        // Drops the item at headIndex into the world in front of the
        // player: spawns its own WorldModelPrefab (Collider +
        // NetworkIdentity + PickupItem baked in by ItemPrefabBatchTool --
        // see LootSpawnPoint.cs's class comment for why runtime AddComponent
        // isn't an option), carrying its remaining uses, and marks it
        // `dropped` so it spins/bobs like the hotbar preview instead of
        // sitting inert like house loot. Server-only: PlayerDropController's
        // Command.
        [Server]
        public void DropSlot(int headIndex, Vector3 position, Quaternion rotation)
        {
            if (headIndex < 0 || headIndex >= SlotCount) return;

            string name = slotItemNames[headIndex];
            if (string.IsNullOrEmpty(name) || name == ContinuationMarker) return;

            ItemDefinition item = catalog != null ? catalog.GetByName(name) : null;
            if (item == null || item.WorldModelPrefab == null) return;

            int uses = slotUses[headIndex];
            if (!RemoveSlot(headIndex)) return;

            GameObject instance = Instantiate(item.WorldModelPrefab, position, rotation);
            instance.transform.localScale = item.WorldModelScale;

            PickupItem pickup = instance.GetComponent<PickupItem>();
            if (pickup == null)
            {
                Debug.LogError($"{item.ItemName}'s World Model Prefab has no PickupItem -- can't drop it (same fix as LootSpawnPoint's error).", instance);
                Destroy(instance);
                return;
            }
            pickup.Initialize(item, uses);
            pickup.MarkDropped();
            NetworkServer.Spawn(instance);
        }

        [Command]
        public void CmdSelectSlot(int index) => SelectSlot(index);

        // Selecting any index within a bulky item's span (e.g. pressing
        // either "3" or "4" for an item occupying both) always resolves
        // to the same logical selection -- its head slot -- rather than
        // treating each physical slot as independently selectable.
        [Server]
        public void SelectSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            selectedSlot = ResolveHead(index);
        }

        // Walks backward from a continuation slot to the head it
        // belongs to. A no-op for an already-head (or empty) index.
        // Safe because AddItem never leaves a gap between a head and its
        // own continuation slots, and slot 0 can never itself be a
        // continuation (nothing precedes it to continue from).
        private int ResolveHead(int index)
        {
            while (index > 0 && slotItemNames[index] == ContinuationMarker)
            {
                index--;
            }
            return index;
        }

        // direction is +1/-1, for scroll wheel -- wraps around both ends,
        // and steps by *logical* slot (skipping over a bulky item's own
        // continuation entries) so a multi-slot item only ever costs one
        // scroll step, the same as any single-slot item.
        [Command]
        public void CmdSelectRelative(int direction)
        {
            int next = selectedSlot;
            for (int guard = 0; guard < SlotCount; guard++)
            {
                next = ((next + direction) % SlotCount + SlotCount) % SlotCount;
                if (slotItemNames[next] != ContinuationMarker) break;
            }

            SelectSlot(next);
        }

        // Clears the 5 normal slots -- deliberately does NOT touch the
        // Prison Wallet (walletItemName/walletUses aren't in this loop),
        // which is the whole point of the wallet: RoundManager calls this
        // on a caught player and the wallet item survives.
        [Server]
        public void ResetInventory()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                slotItemNames[i] = string.Empty;
                slotUses[i] = 0;
            }
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
        private void OnWalletChangedHook(string _, string __) => OnWalletChanged?.Invoke();
    }
}

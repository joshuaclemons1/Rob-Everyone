# Inventory / UX — Tab screen, drop-with-Q, Prison Wallet

Everything here is still **unbuilt** — this doc is the plan for writing
it, code first then Editor wiring, the same shape as
`stage4-multiplayer-mirror.md`. Do the Parts in order; each has a
🔴 Rest Point to two-Editor test before moving on.

## Why this now

Stage 6 added a PvP steal-window (`PlayerTheftTarget`: stun a rival →
press `E` → take one item) but the surrounding inventory UX is thin:

- there's no way to **see** more than the 5-slot hotbar, or to
  **rearrange** what's in it;
- there's no way to **drop** something you don't want (a bulky low-value
  item clogging three slots, a sabotage item you'd rather not carry into
  the exit);
- the **Prison Wallet** (gameplay-design.md's 6th slot — one item that
  survives being caught) was deferred and never built;
- the steal itself just grabs `FindFirstOccupiedSlot`, which can hand a
  thief your equipped Bat instead of your loot.

All four touch `PlayerInventory` and the hotbar UI, so they're one pass.

## Design decisions (settle before coding)

- **The hotbar stays the hotbar.** `SlotCount` remains `5`. The Tab
  screen shows those same 5 slots plus the wallet as one extra box —
  it's a *view + editor* over the existing `SyncList`s, not a new
  container.
- **Server-authoritative, like everything else.** Every mutation (move,
  drop, wallet in/out) goes through a `[Command]` → `[Server]` method on
  `PlayerInventory`. The Tab screen only ever *requests* changes and
  redraws from `OnSlotsChanged`.
- **Drop = the same world pickup, flagged.** A dropped item spawns the
  item's own `WorldModelPrefab` (which already carries
  `Collider` + `NetworkIdentity` + `PickupItem`, baked by
  `ItemPrefabBatchTool` — see `LootSpawnPoint.cs`'s class comment for why
  that matters). The only new thing is a synced `dropped` flag that makes
  `PickupItem` spin/bob its own transform locally, matching the hotbar
  preview's feel. House loot leaves `dropped` false and stays static.
- **Wallet is one item of any size.** A 3-slot fridge fits in the wallet
  the same as a coin does — the wallet is a single `SyncVar` pair
  (`walletItemName` / `walletUses`), not a `SyncList`. Its UI box is one
  square regardless of the item's `InventorySize`.
- **You can't sell straight out of the wallet.** `SellStation` keeps
  selling the 5 hotbar slots only. To cash out a wallet item you drag it
  into a hotbar slot first. Keeps `SellStation` unchanged and makes the
  wallet feel like a vault, not a fast-sell lane. (If playtesting says
  otherwise, revisit — it's a one-line change in `SellCarried`.)
- **Opening Tab is a local input-suppress, not a network freeze.**
  `FirstPersonController.IsFrozen` is the *jailed* state and is synced —
  don't reuse it. Add a separate local `LookSuppressed` bool.

---

## Part 1 — `PlayerInventory`: move, wallet, drop hooks

All server-side. Add to `Assets/Scripts/Inventory/PlayerInventory.cs`.

### 1a. Move an item between slots

```csharp
// Drag-and-drop from the Tab screen. Both indices are head slots
// (the UI only ever hands us a head -- a continuation box isn't
// draggable). Moves the whole span; only succeeds if the destination
// span is clear of anything that isn't part of the item being moved.
// Server-only: called from CmdMoveItem below.
[Server]
public bool MoveItem(int fromHead, int toHead)
{
    if (fromHead < 0 || fromHead >= SlotCount) return false;
    if (toHead < 0 || toHead >= SlotCount) return false;
    if (fromHead == toHead) return false;

    string name = slotItemNames[fromHead];
    if (string.IsNullOrEmpty(name) || name == ContinuationMarker) return false;

    ItemDefinition item = catalog != null ? catalog.GetByName(name) : null;
    if (item == null) return false;
    int size = item.InventorySize;
    if (toHead + size > SlotCount) return false;

    int uses = slotUses[fromHead];

    // Destination must be empty, ignoring the slots the source itself
    // currently occupies (so nudging a 2-slot item one slot over works).
    for (int offset = 0; offset < size; offset++)
    {
        int dest = toHead + offset;
        bool isOwnSlot = dest >= fromHead && dest < fromHead + size;
        if (isOwnSlot) continue;
        if (!string.IsNullOrEmpty(slotItemNames[dest])) return false;
    }

    // Clear source span, then write destination span.
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
```

> **Swap vs. block.** The above *blocks* a move onto an occupied slot.
> True swap (A↔B) is fiddly with mismatched spans — a 1-slot item can't
> always trade places with a 3-slot one. Ship block-only; add swap later
> only for the equal-size case if it's annoying in practice.

### 1b. Prison Wallet

```csharp
// The 6th slot -- one item of any InventorySize, immune to being
// caught. Separate SyncVars rather than a SyncList entry so the
// "immune to ResetInventory" behavior is structural, not a special
// case inside every loop over slotItemNames.
[SyncVar(hook = nameof(OnWalletChangedHook))] private string walletItemName;
[SyncVar] private int walletUses;

public ItemDefinition WalletItem =>
    string.IsNullOrEmpty(walletItemName) || catalog == null ? null : catalog.GetByName(walletItemName);
public int WalletUses => walletUses;

public event Action OnWalletChanged;
private void OnWalletChangedHook(string _, string __) => OnWalletChanged?.Invoke();

// Hotbar head slot -> wallet. Fails if the wallet's occupied.
[Server]
public bool MoveToWallet(int fromHead)
{
    if (!string.IsNullOrEmpty(walletItemName)) return false;
    if (fromHead < 0 || fromHead >= SlotCount) return false;

    string name = slotItemNames[fromHead];
    if (string.IsNullOrEmpty(name) || name == ContinuationMarker) return false;

    walletItemName = name;
    walletUses = slotUses[fromHead];
    RemoveSlot(fromHead); // clears the whole span
    return true;
}

// Wallet -> hotbar slot `toHead`. Reuses AddItem's own selected-slot
// spirit but targets an explicit slot, so the UI drop point decides.
[Server]
public bool MoveFromWallet(int toHead)
{
    ItemDefinition item = WalletItem;
    if (item == null) return false;
    if (toHead < 0 || toHead + item.InventorySize > SlotCount) return false;

    for (int offset = 0; offset < item.InventorySize; offset++)
    {
        if (!string.IsNullOrEmpty(slotItemNames[toHead + offset])) return false;
    }

    slotItemNames[toHead] = item.ItemName;
    slotUses[toHead] = walletUses;
    for (int offset = 1; offset < item.InventorySize; offset++)
    {
        slotItemNames[toHead + offset] = ContinuationMarker;
        slotUses[toHead + offset] = 0;
    }
    walletItemName = string.Empty;
    walletUses = 0;
    return true;
}

[Command] public void CmdMoveToWallet(int fromHead) => MoveToWallet(fromHead);
[Command] public void CmdMoveFromWallet(int toHead) => MoveFromWallet(toHead);
```

**`ResetInventory` must not touch the wallet** — it already only loops
`0..SlotCount`, so as long as the wallet stays out of `slotItemNames`,
being caught (`RoundManager.NotifyPlayerCaught` → `ResetInventory`)
leaves it alone for free. Add a one-line comment there saying so, so
nobody "helpfully" clears it later.

**`TotalValue` / `SellCarried`** deliberately ignore the wallet (see the
design note above). Leave them.

### 1c. Drop the selected item

```csharp
// Server-only: called from PlayerDropController.CmdDrop. Spawns the
// item's own world model (Collider + NetworkIdentity + PickupItem
// already baked in -- ItemPrefabBatchTool) a little in front of the
// player, carrying its remaining uses, and marks it `dropped` so it
// spins/floats like the hotbar preview instead of sitting inert like
// house loot.
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
        Debug.LogError($"{item.ItemName}'s World Model Prefab has no PickupItem -- can't drop it. Same fix as LootSpawnPoint's error.", instance);
        Destroy(instance);
        return;
    }
    pickup.Initialize(item, uses);
    pickup.MarkDropped();
    NetworkServer.Spawn(instance);
}
```

### 1d. Trim `catalog`'s double life (optional cleanup)

`RebuildSlotsFromSync` uses the serialized `catalog` field; `PickupItem`
uses `ItemCatalog.Instance`. Both are the same asset. If you touch this
file anyway, consider switching the reads above to `ItemCatalog.Instance`
and dropping the field — one less thing to wire on the Player prefab.
Not required.

### 🔴 Rest Point 1

No UI yet — test via a temporary debug key or the Inspector:

- `MoveItem(0, 3)` on a populated slot 0 → item appears in slot 3 on
  **both** Editors, slot 0 empties.
- Moving a 2-slot item onto a slot where it wouldn't fit (occupied, or
  `toHead + size > 5`) → returns false, nothing changes.
- `MoveToWallet(0)` then get caught by police → wallet item survives,
  hotbar clears.
- `DropSlot(...)` → the world model appears in front of you for both
  players and is pick-up-able again with its uses intact.

---

## Part 2 — Drop item (hold + `Q`)

### 2a. `PickupItem`: the `dropped` spin/float

Add to `Assets/Scripts/Items/PickupItem.cs`:

```csharp
// True only for an item a player dropped (PlayerInventory.DropSlot);
// false for house loot from LootSpawnPoint. Drives the cosmetic
// spin/bob below so a dropped item reads as "grabbable, just set down"
// rather than blending into the furniture. Synced so late joiners and
// non-droppers see it too.
[SyncVar] private bool dropped;

[SerializeField] private float dropSpinSpeed = 60f;   // deg/sec
[SerializeField] private float dropBobHeight = 0.15f; // metres
[SerializeField] private float dropBobSpeed = 2f;

private Vector3 droppedBaseLocalPos;
private bool capturedBasePos;

[Server]
public void MarkDropped() => dropped = true;

private void Update()
{
    if (!dropped) return;

    if (!capturedBasePos)
    {
        droppedBaseLocalPos = transform.localPosition;
        capturedBasePos = true;
    }

    transform.Rotate(0f, dropSpinSpeed * Time.deltaTime, 0f, Space.World);
    float bob = Mathf.Sin(Time.time * dropBobSpeed) * dropBobHeight;
    transform.localPosition = droppedBaseLocalPos + new Vector3(0f, bob, 0f);
}
```

> The item prefabs have **no `NetworkTransform`** (house loot never
> moves), so spinning the root here is safe — nothing is fighting it
> over the wire, and every client runs the same cosmetic locally. If you
> ever add a `NetworkTransform` to these prefabs, move the spin to a
> child.

### 2b. `PlayerDropController`

New file `Assets/Scripts/Inventory/PlayerDropController.cs`:

```csharp
using Mirror;
using RobEveryone.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobEveryone.Inventory
{
    // Hold nothing, just press Q: drops the currently selected hotbar
    // item into the world in front of you. Mirrors HotbarController's
    // "owner reads Keyboard.current directly, mutation goes through a
    // Command" shape.
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerDropController : MonoBehaviour
    {
        [SerializeField] private Transform dropOrigin; // usually the camera/viewPoint
        [SerializeField] private float dropForward = 1.2f;
        [SerializeField] private float dropUp = 0.2f;

        private PlayerInventory inventory;

        private void Awake() => inventory = GetComponent<PlayerInventory>();

        private void Update()
        {
            if (!inventory.isOwned) return;
            if (Keyboard.current == null || !Keyboard.current.qKey.wasPressedThisFrame) return;

            Transform o = dropOrigin != null ? dropOrigin : transform;
            Vector3 pos = o.position + o.forward * dropForward + Vector3.up * dropUp;
            Quaternion rot = Quaternion.Euler(0f, o.eulerAngles.y, 0f);
            CmdDrop(inventory.SelectedSlot, pos, rot);
        }

        [Command]
        private void CmdDrop(int headIndex, Vector3 position, Quaternion rotation)
        {
            inventory.DropSlot(headIndex, position, rotation);
        }
    }
}
```

`inventory.SelectedSlot` is already resolved to a head by
`PlayerInventory.SelectSlot`/`ResolveHead`, so pressing `3` while a
2-slot item sits in 3–4 and then `Q` drops the whole thing.

### 🔴 Rest Point 2

Two-Editor: pick up a few items, select each with the number keys, press
`Q`. The item should appear in front of you spinning + bobbing, visible
and pick-up-able for the **other** player too. A multi-slot item drops
as one object and frees its whole span. House loot still sits still.

---

## Part 3 — Tab inventory screen

### 3a. `FirstPersonController`: local look-suppress

```csharp
// Local-only (NOT synced -- unlike IsFrozen, which is the jailed
// state). Set by InventoryScreenUI while the Tab screen is open so
// mouse movement drives the cursor, not the camera.
public bool LookSuppressed { get; set; }
```

In `Update()`:

```csharp
if (!isOwned) return;
if (IsFrozen) return;

if (!LookSuppressed) HandleLook();
HandleCrouch();
if (!LookSuppressed) HandleMove();
```

(Keep `HandleCrouch` running or not — your call; leaving movement frozen
but crouch live is harmless. Simpler to gate all three.)

### 3b. `InventoryScreenUI`

New file `Assets/Scripts/UI/InventoryScreenUI.cs`. Structure, not a full
listing — the drag mechanic is the only non-obvious part:

```csharp
// Toggled by Tab. While open: unlocks the cursor, sets
// FirstPersonController.LookSuppressed, and shows 5 hotbar slot boxes +
// 1 wallet box as drag sources/targets. A drag is: pointer-down on a
// box that has an item -> a "ghost" icon follows the mouse -> pointer-up
// over another box -> fire the matching Cmd on PlayerInventory. Redraws
// entirely from PlayerInventory.OnSlotsChanged / OnWalletChanged, never
// from its own optimistic state.
public class InventoryScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;               // the whole screen, toggled
    [SerializeField] private InventoryDragSlot[] hotbarSlots; // exactly SlotCount, left to right
    [SerializeField] private InventoryDragSlot walletSlot;
    [SerializeField] private RectTransform dragGhost;        // an Image that follows the cursor mid-drag

    private PlayerInventory inventory;
    private FirstPersonController fpc;
    private bool open;

    // Start() polls for PlayerInventory.LocalPlayer the same way HotbarUI
    // does (see its WaitForLocalPlayer coroutine) -- copy that pattern.

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) Toggle();
        if (open && dragGhost.gameObject.activeSelf) dragGhost.position = Mouse.current.position.ReadValue();
    }

    private void Toggle()
    {
        open = !open;
        panel.SetActive(open);
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (fpc != null) fpc.LookSuppressed = open;
        if (open) Redraw();
    }

    // Called by an InventoryDragSlot when a drag it started is dropped
    // onto `target` (also an InventoryDragSlot). `source`/`target` each
    // know whether they're a hotbar index or the wallet.
    public void ResolveDrag(InventoryDragSlot source, InventoryDragSlot target)
    {
        if (source == target || inventory == null) return;

        if (source.IsWallet && !target.IsWallet)      inventory.CmdMoveFromWallet(target.HeadIndex);
        else if (!source.IsWallet && target.IsWallet) inventory.CmdMoveToWallet(source.HeadIndex);
        else if (!source.IsWallet && !target.IsWallet) inventory.CmdMoveItem(source.HeadIndex, target.HeadIndex);
        // wallet -> wallet: nothing
    }

    private void Redraw()
    {
        IReadOnlyList<int> spans = inventory.SlotSpanLengths;
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            int span = i < spans.Count ? spans[i] : 1;
            hotbarSlots[i].Bind(this, i, isWallet: false, inventory.Slots[i], span);
        }
        walletSlot.Bind(this, -1, isWallet: true,
            inventory.WalletItem != null ? new InventorySlot { Item = inventory.WalletItem, RemainingUses = inventory.WalletUses } : (InventorySlot?)null,
            span: 1);
    }
}
```

`InventoryDragSlot` is a small `MonoBehaviour` implementing
`IPointerDownHandler` / `IDragHandler` / `IPointerUpHandler` (or
`IBeginDragHandler`/`IEndDragHandler` + `IDropHandler`). On begin-drag it
tells the parent screen to show `dragGhost` with this slot's icon; on
drop (`IDropHandler.OnDrop`) it calls `screen.ResolveDrag(eventData
source slot, this)`. Reuse `HotbarSlotUI`'s model-preview render for the
box art, or just show `ItemDefinition.Icon` if set / the name text
otherwise — a static thumbnail is fine here, the live 3D spin is a
hotbar-only flourish.

**Multi-slot boxes in the Tab screen:** simplest is to render the 5
boxes at fixed positions (not merged) and just *widen the icon* of a
head to visually bleed over its continuation boxes, or draw a bracket/
outline around the span. Continuation boxes are not drag sources. Don't
reuse `HotbarSlotUI.SetSpan`'s anchored-position math here — a fixed grid
is easier to drag onto.

### 3c. Which canvas / render mode

The Tab screen is a full-screen overlay with no world anchoring —
**Screen Space – Overlay** is fine (unlike the HUD canvases, which are
Screen Space – Camera and get their `worldCamera` wired per-client by
`GameFlowManager`). One less thing to wire.

### 🔴 Rest Point 3

Two-Editor:

- Tab toggles the screen; cursor appears and the camera stops following
  the mouse; Tab again closes it and re-locks.
- Drag an item from slot 0 to slot 4 → moves on both Editors.
- Drag onto an occupied slot → snaps back, no change.
- Drag a loot item into the wallet box, close, get caught → wallet item
  is still there next round; drag it back out and sell it.
- Opening the screen mid-round doesn't freeze the *other* player.

---

## Part 4 — Steal-window rework

Fold the code-review nits from `todo.md` into `PlayerTheftTarget` /
`PlayerImpactRelay` while the inventory code is fresh.

### 4a. `PlayerTheftTarget`: steal loot, not gear

```csharp
// Was: FindFirstOccupiedSlot -> any slot, including an equipped Bat.
// Now: highest-value slot whose item isn't a sabotage tool. A thief
// wants your loot; your weapons staying with you is correct.
private static int FindBestStealSlot(PlayerInventory inv)
{
    var spans = inv.SlotSpanLengths;
    int best = -1;
    int bestValue = -1;
    for (int i = 0; i < PlayerInventory.SlotCount; i++)
    {
        if (i >= spans.Count || spans[i] <= 0) continue;   // not a head
        ItemDefinition item = inv.Slots[i]?.Item;
        if (item == null) continue;
        if (item.SabotageType != SabotageType.None) continue; // leave gear
        if (item.Value > bestValue) { bestValue = item.Value; best = i; }
    }
    return best;
}
```

In `Interact`, also carry the uses across so a stolen part-used item
keeps its count:

```csharp
int victimSlot = FindBestStealSlot(victim);
if (victimSlot < 0) return;
InventorySlot? slot = victim.Slots[victimSlot];
if (slot == null) return;

if (!thief.AddItem(slot.Value.Item, slot.Value.RemainingUses)) return;
victim.RemoveSlot(victimSlot);
relay.ClearStealableNow();
```

### 4b. `PlayerImpactRelay`: expiry instead of racing coroutines

Replace the `ClearStealableAfter` coroutine with a timestamp the server
checks, so two overlapping PvP hits don't have the first one's coroutine
close the window early for the second attacker:

```csharp
[SyncVar] public bool IsStealable { get; private set; }
private double stealableUntil; // server clock

[Server]
public void ServerApplyPvpImpact(Vector3 dir, float force, float dur, float stealWindow)
{
    ServerApplyImpact(dir, force, dur);
    IsStealable = true;
    stealableUntil = NetworkTime.time + stealWindow; // extends, never shortens
}

[Server]
private void Update()
{
    if (IsStealable && NetworkTime.time >= stealableUntil) IsStealable = false;
}

[Server]
public void ClearStealableNow() { IsStealable = false; stealableUntil = 0; }
```

Also: decide on purpose whether `ServerApplyPvpImpact` should open the
steal window when the target was **already stunned** (a car hit them
first) and the inner `ServerApplyImpact` no-ops. Current behavior: it
does. That's probably fine ("they're down, rob them"), but put a comment
saying it's deliberate.

### 4c. `PlayerImpactRelay` class comment

Stale — it says "Only the server ever calls `RpcApplyImpact` (from
CarDriver's own isServer-gated impact detection)". Now it's `private`,
reached via `ServerApplyImpact` / `ServerApplyPvpImpact`, and the
sabotage path calls it too. Just fix the comment.

### 🔴 Rest Point 4

Two-Editor: carry a Bat **and** a Laptop, get stunned by the other
player, let them press `E` → they get the Laptop, you keep the Bat. Stun
someone twice in quick succession from two angles → the window lasts the
full second time, not cut short by the first.

---

## Editor wiring (after all code compiles)

### Player prefab (`Assets/Prefabs/Player.prefab`)

1. Add **Player Drop Controller**. Drag the camera/`viewPoint` transform
   into **Drop Origin**.
2. `PlayerTheftTarget` / `PlayerImpactRelay` already there from Stage 6 —
   no new fields.

### Tab screen

3. In `SampleScene` **and** `Lobby` (keep both copies in sync, like the
   hotbar): under a **Screen Space – Overlay** Canvas, build an
   `InventoryPanel` (start inactive):
   - a dimmed full-screen background `Image`;
   - a row of 5 `HotbarSlot` boxes + 1 `WalletSlot` box, each with an
     `InventoryDragSlot` + an `Image` for the icon + a `TextMeshPro` for
     name/uses;
   - a single `DragGhost` `Image` (start inactive, `raycastTarget` off).
4. Add **Inventory Screen UI** to the Canvas (or a child). Wire `panel`,
   the 5 `hotbarSlots` in order, `walletSlot`, `dragGhost`.
5. `EventSystem` must exist in the scene (it does, from the HUD) for
   pointer drag events to fire.

### PickupItem prefabs

6. The new `dropped` spin fields have sensible defaults — only touch
   them on a per-item prefab if something looks wrong (a huge item bobs
   too far, etc.).

### 🔴 Rest Point 5 — full pass

Two-Editor, one full round:

- Loot a house, open Tab, rearrange, drop the item you don't want (`Q`),
  watch the other player pick it up.
- Put your best item in the wallet, get caught, confirm it survives into
  the Lobby, drag it to a hotbar slot, sell it.
- Stun the other player, steal their loot (not their gear), confirm the
  numbers.
- Confirm none of Tab/drag/drop dessyncs position, camera, or the
  hotbar between the two Editors.

Then tell me and we mark inventory/UX done in `completed.md`.

---

## Follow-ups (not blocking)

- **Swap-on-drag** for the equal-`InventorySize` case.
- **Right-click a Tab slot** = quick-drop (skip the drag).
- **Wallet on the HUD** — a small always-visible 6th box next to the
  hotbar, so you don't need Tab to see what's vaulted.
- **`InputActionAsset`** — this project still polls `Keyboard.current`
  directly everywhere; if rebindable controls ever matter, that's the
  bigger refactor this feature sits on top of.

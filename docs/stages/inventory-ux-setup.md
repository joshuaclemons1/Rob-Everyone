# Inventory / UX — Editor setup (Tab screen, drop-with-Q, Prison Wallet)

All the code is written. This is what to wire up by hand in the Unity
Editor to bring it online. Everything below is Editor work.

**Always press Play from `MainMenu`** — `NetworkManager` only exists
there.

---

## What the code does (reference)

| Script | Where it goes | Role |
|---|---|---|
| `PlayerInventory` (changed) | Player prefab (already there) | `MoveItem`/`TryPlaceAt` for drag-rearrange, `MoveToWallet`/`MoveFromWallet` (phase-gated), `DropSlot`, wallet SyncVars, `DisplayName` |
| `PlayerDropController` (new) | Player prefab | `Q` → drop the selected slot into the world |
| `InventoryCameraRig` (new) | Player prefab | swaps your camera to a static front-facing third-person shot while the screen is open |
| `PlayerTheftTarget` (rewritten) | Player prefab (already there) | E on a stunned rival → opens the steal screen on your client; drag-driven transfer, one item per stun |
| `PlayerImpactRelay` (changed) | Player prefab (already there) | steal window is now an expiry timestamp, not a coroutine (no code wiring) |
| `PickupItem` (changed) | item prefabs (already there) | a dropped item spins/bobs; house loot doesn't |
| `InventoryScreenUI` (new) | HUD Canvas, each scene | the Tab / steal screen orchestrator |
| `InventoryDragSlot` (new) | each slot box | drag source / drop target conduit |
| `WalletSlotUI` (new) | HUD, each scene | the always-visible wallet box |
| `HotbarUI` (changed) | Hotbar prefab | can now bind to an explicit inventory (for the victim row) |

Design calls already baked in (from `gameplay-design.md` + your notes):
you're **fully vulnerable** while the screen is open; the wallet takes
**one item of any size**, placeable only mid-round, retrievable only in
the Lobby, **locked once filled** for the round; a walleted item
**survives being caught** and persists across rounds until you drag it
out in the Lobby and sell it (it does *not* auto-bank — say the word if
you want that); stealing is **drag one item from the rival's hotbar to
yours**, one per stun.

---

## Part 1 — Player prefab

Open `Assets/Prefabs/Player.prefab` in prefab edit mode.

1. **Add Component → Player Drop Controller.**
   - `Drop Forward` `1.0`, `Drop Height` `-0.4`, `Drop Key` `Q`.
   - These are the "where does the dropped item appear" knobs — tune
     them in Rest Point 2 until it lands just in front of the character
     at about knee/waist height.
2. **Add Component → Inventory Camera Rig.**
   - `Front Offset` `(0, 1.6, 2.2)`, `Look At Height` `1.3`.
   - No references to wire — it finds the camera and `PlayerSkinSpawner`
     itself. Tune the offset in Rest Point 3 so you see your character
     from the front, head-to-knee in frame.
3. Confirm **Player Theft Target** and **Player Impact Relay** are
   already on the prefab (from `stage6-sabotage-items-phase2-setup.md`
   Part 3). No new fields on either.
4. Save the prefab.

> If you still have the temporary **Debug Third Person Camera** (`T`
> key) on the Player, don't use it and the Tab screen at the same time —
> they both move the camera. Remove it whenever.

---

## Part 2 — The Prison Wallet box (both scenes)

Do this in `SampleScene` first, then repeat in `Lobby` (same as the
hotbar — both scenes keep their own HUD copy).

1. Under the HUD Canvas, next to the `Hotbar`, create a UI **Image**
   named `WalletBox`. Style it to read as a slightly-separated 6th slot
   (a gap, a different tint, a small "vault" label — your call). Its
   Image **Raycast Target** must be **ON**.
2. As children of `WalletBox`:
   - a **TextMeshPro - Text** `NameText` (item name),
   - a **TextMeshPro - Text** `UsesText` (small, a corner — the `x2`
     durability readout, same as the hotbar's),
   - an object `EmptyHint` (a faint watermark / "empty" state — shown
     when nothing's vaulted),
   - an object `LockedIcon` (a small padlock — shown when the wallet is
     filled *and* you're mid-round, i.e. it can't be swapped right now).
3. On `WalletBox`, **Add Component → Wallet Slot UI**. Wire `Name Text`,
   `Uses Text`, `Empty Hint`, `Locked Icon`.
4. On `WalletBox`, **Add Component → Inventory Drag Slot**.
   `Kind` = **My Wallet**, `Index` = `-1`.

### 🔴 Rest Point 1
Play from MainMenu, host, loot a house. The `WalletBox` shows the empty
hint. (It won't do anything else until Part 3 — the drag screen — is up.)

---

## Part 3 — The Tab / steal screen (both scenes)

Again: `SampleScene` first, then `Lobby`. The **victim row** (Part 4) is
`SampleScene`-only — skip it in the Lobby.

### 3a. Canvas prerequisites

1. Select the Canvas that hosts the `Hotbar`. Make sure it has a
   **Graphic Raycaster** (Add Component if not).
2. Make sure the scene has an **EventSystem** (`GameObject → UI → Event
   System` if not — the HUD may not have needed one until now).

### 3b. Screen objects

Under that same Canvas:

3. **`DimBackground`** — a full-screen **Image**, dark, ~60% alpha,
   **Raycast Target ON**. **Start disabled.** In the hierarchy, place it
   *directly above the `Hotbar`* in the sibling list, so the dim covers
   the rest of the HUD (cash, timer, crosshair) but the hotbar, wallet,
   victim row, and drag ghost — all below it — render on top.
4. **`DragGhost`** — a small **Image** (semi-transparent), with a child
   **TextMeshPro - Text** `GhostLabel`. **Raycast Target OFF** on both.
   **Start disabled.** Put it *last* in the Canvas sibling list (renders
   on top of everything).

### 3c. Slot drag components

5. On each of the Hotbar's 5 slot boxes (`Slot0`…`Slot4`):
   **Add Component → Inventory Drag Slot**, `Kind` = **My Hotbar**,
   `Index` = `0`…`4` to match. Each slot box's background Image needs
   **Raycast Target ON**.

### 3d. The InventoryScreenUI component

6. On the **Canvas root** GameObject, **Add Component → Inventory Screen
   UI**. Wire:
   - **Dim Background** → `DimBackground`
   - **Hotbar Container** → the `Hotbar`'s own **RectTransform**
   - **My Hotbar Slots** → the 5 `Slot0`…`Slot4` objects (their
     `InventoryDragSlot`), **in left-to-right order**
   - **My Wallet Slot** → `WalletBox`'s `InventoryDragSlot`
   - **Drag Ghost** → `DragGhost` (RectTransform), **Drag Ghost Label** →
     `GhostLabel`
   - **Victim Row / Victim Hotbar UI / Victim Hotbar Slots / Victim
     Label** → leave empty here; filled in Part 4 (SampleScene only)
   - **Steal Break Distance** → `6`
7. **Hotbar transform (compact vs expanded):**
   - **Compact Anchored Pos** → copy the `Hotbar` RectTransform's
     *current* `Anchored Position` exactly (so it doesn't jump on load).
   - **Compact Scale** → `1`.
   - **Expanded Anchored Pos** → the same X, a higher Y (e.g. +250 —
     "moves up a bit"). Eyeball it in Rest Point 4.
   - **Expanded Scale** → `1.6`.
   - **Transform Lerp Speed** → `12`.

### 🔴 Rest Point 2 — drop
Host + join. Loot items into different slots, select each with the
number keys, press **Q**. The item should appear just in front of your
character, spinning and bobbing, and be visible + pick-up-able for the
**other** player. A multi-slot item drops as one object and frees its
whole span. House loot still sits still. Tune `Drop Forward` /
`Drop Height` on the Player prefab if it lands somewhere awkward.

### 🔴 Rest Point 3 — Tab screen basics
Press **Tab**: camera swaps to a front view of your character, the
hotbar rises + grows, the cursor appears, movement/look stop. **Tab** or
**Esc** closes it and re-locks. Drag an item from one slot to another →
it moves on **both** Editors. Drop onto an occupied slot → snaps back.
While your screen is open the other player is unaffected. Tune the
`Inventory Camera Rig` `Front Offset` and the Expanded transform values
until it looks right.

### 🔴 Rest Point 4 — the wallet
In `SampleScene` (mid-round): Tab, drag a loot item onto the `WalletBox`
→ it moves in, the padlock shows. Try to drag it back out → nothing
happens (locked). Get caught by police → in the Lobby, the wallet item
is still there. In the `Lobby` screen: drag it out of the wallet onto a
hotbar slot, then sell it at the Sell Station. Confirm you **can't**
stash into the wallet while in the Lobby, and **can't** retrieve while
mid-round.

---

## Part 4 — The victim row (SampleScene only)

The steal screen is the same screen, plus the rival's hotbar shown above
yours.

1. Under the HUD Canvas, create an empty `VictimRow`. **Start disabled.**
2. Drag a **second instance of `Assets/Prefabs/UI/Hotbar.prefab`** in as
   a child of `VictimRow`. Position it *above* where the expanded hotbar
   sits.
3. On that second Hotbar instance's **Hotbar UI** component, **uncheck
   Bind To Local Player**.
4. On each of its 5 slot boxes: **Add Component → Inventory Drag Slot**,
   `Kind` = **Victim Hotbar**, `Index` = `0`…`4`.
5. Add a **TextMeshPro - Text** `VictimLabel` to `VictimRow` (e.g. above
   the row) — the text is set at runtime to `Steal from: <name>`.
6. Back on the **Inventory Screen UI** component, wire the fields left
   empty in Part 3:
   - **Victim Row** → `VictimRow`
   - **Victim Hotbar UI** → the second Hotbar instance's `Hotbar UI`
   - **Victim Hotbar Slots** → its 5 `InventoryDragSlot`, in order
   - **Victim Label** → `VictimLabel`

### Interact mask
The Player needs to be a valid raycast target for `Interactor`. Confirm
the Player prefab's body/ragdoll colliders sit on a layer included in
the **Interactor** component's **Interactable Mask** (default is
Everything — only an issue if you've narrowed it).

### 🔴 Rest Point 5 — steal
Two Editors. Player A tases Player B. While B is down, A looks at B and
presses **E** — A's steal screen opens: B's hotbar on top with
"Steal from: Player 2", A's own hotbar below. A drags one item from B's
row onto an empty slot of their own → it transfers, the screen closes,
and A can't take a second item from that stun. Confirm:
- dragging onto a full/too-small slot takes nothing (window stays open);
- B's sabotage gear can be taken too (it's their hotbar — if you decide
  gear should be off-limits, that's a one-line change in
  `PlayerTheftTarget.CmdStealItem`, tell me);
- walking >6 m from B, or the ~6 s window lapsing, closes A's screen;
- a **third** player pressing E on the same stun while A's screen is
  open gets nothing (one thief per stun).

---

## Part 5 — full pass

Two Editors, one full round, start to finish:

1. Loot a house, Tab, rearrange, drop the item you don't want (`Q`), let
   the other player pick it up.
2. Stash your best item in the wallet, get caught, confirm it survives
   into the Lobby, drag it out, sell it.
3. Stun the other player, steal one item, confirm the transfer and the
   one-per-stun limit.
4. Confirm none of Tab / drag / drop / steal desyncs position, camera,
   Cash, or the hotbar between the two Editors, and that after every
   close the cursor re-locks and movement/look come back.

Then tell me and it goes in `completed.md`.

---

## Follow-ups (tracked, not blocking)

- **Swap-on-drag** for the equal-`InventorySize` case (currently
  drag-onto-occupied just fails).
- **Right-click a slot in the Tab screen** = quick-drop.
- **Real Steam persona names** in the "Steal from:" label (currently
  "Player 1 / Player 2" by join order).
- **Auto-bank an unretrieved wallet item** at the shop phase, if that
  feels better than it persisting.
- **Wallet item off-limits to theft**, if playtesting says stealing gear
  is annoying.

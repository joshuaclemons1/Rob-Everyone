# Inventory / UX — Editor setup (Tab screen, drop-with-Q, Prison Wallet)

All the code is written. This is what to wire up by hand. Almost all of
it is one pass on **`Assets/Prefabs/UI/Hotbar.prefab`** — it's a live
prefab instance in both `SampleScene` and `Lobby`, so building the whole
screen inside it means zero per-scene wiring beyond a Canvas check.

**Always press Play from `MainMenu`** — `NetworkManager` only exists
there.

---

## What the code does (reference)

| Script | Where it goes | Role |
|---|---|---|
| `PlayerInventory` (changed) | Player prefab (already there) | `MoveItem`/`TryPlaceAt` (drag-rearrange), `MoveToWallet`/`MoveFromWallet` (phase-gated), `CmdDropSelected`/`DropSlot`, wallet SyncVars, `DisplayName` |
| `PlayerDropController` (new) | Player prefab | `Q` → `inventory.CmdDropSelected(...)` |
| `PlayerCameraRig` (new) | Player prefab | shared camera cut/blend/re-dock — used by both the inventory screen and the ragdoll cutaway; both transitions are smooth now |
| `InventoryCameraRig` (new) | Player prefab | the wide front-facing framing for the screen; hands it to `PlayerCameraRig` |
| `PlayerRagdoll` (changed) | Player prefab (already there) | camera cutaway moved onto `PlayerCameraRig` — `Camera Transform` field removed, in/out no longer snap |
| `PlayerTheftTarget` (rewritten) | Player prefab (already there) | E on a stunned rival opens the steal screen; drag-driven transfer, one item per stun |
| `PlayerImpactRelay` (changed) | Player prefab (already there) | steal window is an expiry timestamp now — no wiring |
| `PickupItem` (changed) | item prefabs (already there) | a dropped item spins/bobs *and* its colliders become triggers (walk through it); house loot unaffected |
| `FirstPersonController` (changed) | Player prefab (already there) | `LookSuppressed` parks **only mouse-look** while the screen is open — walk/sprint/jump/crouch still work |
| `InventoryScreenUI` (new) | `Hotbar.prefab` root | the Tab / steal screen orchestrator |
| `InventoryDragSlot` (new) | each slot box | drag source / drop target conduit |
| `WalletSlotUI` (new) | `Hotbar.prefab` (wallet box) | the always-visible wallet box + lock indicator |
| `HotbarUI` (changed) | `Hotbar.prefab` (main + victim rows) | can bind to an explicit inventory (`Bind To Local Player` toggle) |

Design calls already baked in: **fully vulnerable** while the screen is
open; wallet takes **one item of any size**, placeable only mid-round,
retrievable only in the Lobby, **locked once filled**, **survives being
caught**, persists across rounds until you drag it out in the Lobby and
sell it (no auto-bank); stealing is **drag one item from the rival's
hotbar to yours**, one per stun.

---

## Part 1 — Player prefab

Open `Assets/Prefabs/Player.prefab` in prefab edit mode.

1. **Add Component → Player Camera Rig.** No fields to wire (it finds
   the camera and `PlayerSkinSpawner` itself). `Default Blend` `0.35` is
   the shared transition time — this is the one component that now owns
   *all* camera cutaways.
2. **Add Component → Player Drop Controller.** `Drop Forward` `1.0`,
   `Drop Height` `-0.4`, `Drop Key` `Q`. (Tuning knobs — adjust in
   Rest Point 1 so the item lands just in front of the character at
   about knee height.)
3. **Add Component → Inventory Camera Rig.** `Front Offset` `(0, 1.7,
   6.0)`, `Look At Height` `1.0`, `Field Of View` `62`, `Blend Duration`
   `0.35`. For a wider shot: pull `Front Offset` Z out further, or (if
   the camera would clip into geometry behind the player) raise
   `Field Of View` instead and keep Z shorter.
4. **Player Ragdoll** — its old `Camera Transform` field is gone; the
   ragdoll cutaway now goes through `Player Camera Rig` too, so its
   in/out are smooth instead of snapping. `Third Person Offset` /
   `Look At Height Offset` / `Camera Follow Speed` are unchanged.
5. Confirm **Player Theft Target** and **Player Impact Relay** are
   already on the prefab (Stage 6 Phase 2 Part 3). No new fields.
6. **Delete any second camera on the Player.** The prefab must have
   **exactly one** camera (`PlayerCamera`, at eye height). If there's a
   leftover `ThirdPersonCamera` child from the old **Debug Third Person
   Camera** tool, delete that GameObject *and* the `Debug Third Person
   Camera` component — with the script gone, nothing disables that
   camera and it renders over the real one from chest height.
7. Save.

---

## Part 2 — Rebuild `Hotbar.prefab` into the inventory screen

Open `Assets/Prefabs/UI/Hotbar.prefab` in prefab edit mode. Current
structure is `Hotbar` (root, has `Hotbar UI`) → `Slot0`…`Slot4`.

### 2a. Pull the slots into a container that can move

The screen needs one child that rises + scales while the rest (dim
background, ghost) stays put.

1. Create an empty child of `Hotbar` named **`SlotRow`**. On its
   RectTransform: same anchors/pivot as `Hotbar`, `Anchored Position`
   `(0, 0)`, `Scale` `1`, size matching `Hotbar` (or stretch). The goal
   is that `SlotRow` sits exactly where the slots already are.
2. Drag `Slot0`…`Slot4` in the Hierarchy so they become children of
   `SlotRow`. Their on-screen positions should not move — if they jump,
   `SlotRow`'s RectTransform isn't lined up with the root; fix it and
   redo.
3. Move the **`Hotbar UI`** component from `Hotbar` (root) onto
   `SlotRow`: Add Component → Hotbar UI on `SlotRow`, re-populate its
   **Slots** array with `Slot0`…`Slot4` in order, leave **Bind To Local
   Player** checked, then remove the old `Hotbar UI` from the root.

### 2b. The wallet box

4. **Duplicate `Slot0`** (Ctrl/Cmd+D) — *don't* build this from a plain
   Image; it has to carry a fully-wired `Hotbar Slot UI` (its
   `Model Image`, `Item Text`, `Uses Text` fields pointing at its own
   children — automatic on a duplicate) or the model preview won't
   render. Drag the copy to be the last child of `SlotRow`, rename it
   **`WalletBox`**, position it just past `Slot4` with a small gap. Its
   background Image needs **Raycast Target ON**.
5. Change the `Inventory Drag Slot` it copied over: `Kind` = **My
   Wallet**, `Index` `-1`.
6. Two small child **Image** objects on `WalletBox` (both start
   disabled), positioned in a corner:
   - **`LockedIcon`** — Image, `Source Image` = `Assets/Art/UI/Icon_Lock`
     (sprite, already imported). Shows while the wallet is filled *and*
     you're mid-round.
   - **`UnlockedIcon`** — Image, `Source Image` =
     `Assets/Art/UI/Icon_Unlock`. Shows while the wallet is filled *and*
     you're in the Lobby (drag it out to sell). Either icon alone is
     fine if you only want one.
7. On `WalletBox`: **Add Component → Wallet Slot UI** → wire **Locked
   Icon** = `LockedIcon`, **Unlocked Icon** = `UnlockedIcon`. (It finds
   its own `Hotbar Slot UI` — no Display field to wire.)

### 2c. Drag components on the 5 slots

8. On each of `Slot0`…`Slot4`: **Add Component → Inventory Drag Slot**,
   `Kind` = **My Hotbar**, `Index` = `0`…`4` to match. Each slot's
   background Image needs **Raycast Target ON**.

### 2d. Screen chrome

9. Create a child of **`Hotbar`** (root, *not* `SlotRow`) named
   **`DimBackground`** — a full-screen **Image**, dark, ~60% alpha,
   **Raycast Target ON**. Make it the **first** child of `Hotbar` so it
   renders behind everything else. **Disable it** (uncheck the
   GameObject).
10. Create a child of **`Hotbar`** (root — **not** `SlotRow`, or it'll
    inherit `SlotRow`'s scale) named **`DragGhost`**, **last** child of
    `Hotbar`. **Disable it.** Size/anchors don't matter — the code
    forces them. Under it:
    - **`GhostImage`** — a **Raw Image** (shows the dragged item's live
      spinning model). **Raycast Target OFF.** Size doesn't matter, the
      code stretches it to fill.
    - **`GhostLabel`** — a **TextMeshPro - Text**, centered,
      **Raycast Target OFF**. Only shows for an item with no model.
    - Tune the on-screen size with **Ghost Size** on the `Inventory
      Screen UI` component (default `160`).

### 2e. The victim row (for steal mode)

11. Create a child of **`Hotbar`** (root) named **`VictimRow`**.
    **Disable it.** Position it above where `SlotRow` sits when
    expanded (see 2g).
12. Under `VictimRow`, create **`VictimSlots`** and give it 5 child slot
    boxes — select `Slot0`…`Slot4` under `SlotRow`, Ctrl/Cmd+D, drag the
    copies under `VictimSlots`, rename them `VSlot0`…`VSlot4`.
13. On `VictimSlots`: **Add Component → Hotbar UI**, populate **Slots**
    with `VSlot0`…`VSlot4`, and **uncheck Bind To Local Player**.
14. On each `VSlot0`…`VSlot4`: change its **Inventory Drag Slot** (copied
    from the original) → `Kind` = **Victim Hotbar**, `Index` = `0`…`4`.
15. Add a **TextMeshPro - Text** `VictimLabel` under `VictimRow` (above
    the row) — text is set at runtime to `Steal from: <name>`.

### 2f. The InventoryScreenUI component

16. On the **`Hotbar`** root: **Add Component → Inventory Screen UI**.
    Wire (all references are inside this prefab):
    - **Dim Background** → `DimBackground`
    - **Hotbar Container** → `SlotRow` (its RectTransform)
    - **Victim Row** → `VictimRow`
    - **Victim Hotbar UI** → `VictimSlots`'s `Hotbar UI`
    - **Victim Label** → `VictimLabel`
    - **My Hotbar Slots** → `Slot0`…`Slot4` (their `Inventory Drag
      Slot`), in order
    - **My Wallet Slot** → `WalletBox`'s `Inventory Drag Slot`
    - **Victim Hotbar Slots** → `VSlot0`…`VSlot4` (their `Inventory Drag
      Slot`), in order
    - **Drag Ghost** → `DragGhost` (RectTransform), **Drag Ghost Image**
      → `GhostImage`, **Drag Ghost Label** → `GhostLabel`
    - **Steal Break Distance** → `6`

### 2g. Compact vs. expanded transform

17. Still on **Inventory Screen UI**:
    - **Compact Anchored Pos** → copy `SlotRow`'s current **Anchored
      Position** exactly (so it doesn't jump on load — should be
      `(0, 0)` after 2a).
    - **Compact Scale** → `1`.
    - **Expanded Anchored Pos** → same X, a higher Y (start with
      `(0, 250)` — "moves up a bit"; eyeball in Rest Point 2).
    - **Expanded Scale** → `1.6`.
    - **Transform Lerp Speed** → `12`.
18. Position `VictimRow` so it sits above `SlotRow` *at the expanded
    position/scale* — easiest to temporarily set `SlotRow`'s anchored
    pos to the expanded value, place `VictimRow` above it, then set
    `SlotRow` back.

Save the prefab.

---

## Part 3 — Per scene (SampleScene + Lobby)

The Hotbar prefab instance is already in both scenes, so it picks all
of the above up automatically. Only check:

1. The **Canvas** hosting the Hotbar has a **Graphic Raycaster**
   component (Add Component if missing).
2. The scene has an **EventSystem** (`GameObject → UI → Event System`
   if missing — the HUD may not have needed one before).
3. Play from MainMenu once and confirm the Hotbar still looks right
   (nothing shifted from the `SlotRow` reparent) in both scenes.

That's it — no per-scene component wiring.

---

## Rest Points

### 🔴 Rest Point 1 — drop
Host + join. Loot items into different slots, select each with the
number keys, press **Q**. The item appears just in front of your
character, spinning and bobbing, visible + pick-up-able for the **other**
player — and you can **walk straight through it** (its colliders are
triggers now). A multi-slot item drops whole and frees its span. House
loot still sits still and stays solid. Tune `Drop Forward` /
`Drop Height` on the Player prefab.

### 🔴 Rest Point 2 — Tab screen + camera
Press **Tab**: camera **blends** (doesn't snap — ~0.35 s) to a wide
front view of your character, `SlotRow` rises + grows, cursor appears.
Mouse-look stops (the mouse is the cursor now) but you can still
**walk / sprint / jump / crouch** — the camera stays framed on you as
you move. **Tab** / **Esc** closes — the camera blends back and look
only returns once it's home. Drag an item between two slots
→ moves on **both** Editors; the drag ghost is a live mini-render of the
item, not a white box. Drop onto an occupied slot → snaps back. The
other player is unaffected while your screen is open. Also confirm the
**ragdoll** stun cutaway now eases in/out instead of snapping. Tune
`Inventory Camera Rig`'s `Front Offset` and the Expanded transform
values.

### 🔴 Rest Point 3 — wallet
Mid-round (`SampleScene`): Tab, drag a loot item onto `WalletBox` → it
moves in, the padlock shows, dragging it back out does nothing. Get
caught → in the Lobby the wallet item is still there. In the Lobby
screen: drag it out onto a hotbar slot, sell it at the Sell Station.
Confirm you **can't** stash while in the Lobby and **can't** retrieve
mid-round.

### 🔴 Rest Point 4 — steal
Two Editors. A tases B. While B is down, A looks at B, presses **E** →
A's screen opens with B's hotbar on top ("Steal from: Player 2") and
A's own below. A drags one item from B's row onto an empty slot → it
transfers, the screen closes, A can't take a second from that stun.
Confirm: dragging onto a full/too-small slot takes nothing (window
stays open); walking >6 m from B or the ~6 s window lapsing closes A's
screen; a third player pressing E during A's screen gets nothing.

### 🔴 Rest Point 5 — full pass
One full round, two Editors: loot → Tab → rearrange → drop the junk
(`Q`) → other player grabs it; stash your best item, get caught, confirm
it survives, retrieve + sell it in the Lobby; stun the other player,
steal one item. Confirm nothing desyncs position / camera / Cash /
hotbar, and every close re-locks the cursor and restores movement.

Then tell me and it goes in `completed.md`.

---

## Follow-ups (tracked, not blocking)

- Swap-on-drag for the equal-`InventorySize` case.
- Right-click a Tab slot = quick-drop.
- Real Steam persona names in the "Steal from:" label (currently
  "Player N" by join order).
- Auto-bank an unretrieved wallet item at the shop phase, if it feels
  better than persisting.
- Wallet item off-limits to theft, if stealing gear plays badly (a
  one-liner in `PlayerTheftTarget.CmdStealItem`).

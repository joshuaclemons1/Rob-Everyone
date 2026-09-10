# Ragdoll carry — Editor setup

Code's written. This is the by-hand wiring on the **Player prefab**,
then a two-Editor playtest. **Always press Play from `MainMenu`.**

## What the code does (reference)

| Script | Where | Role |
|---|---|---|
| `Carryable` (new) | Player prefab | `[SyncVar] carriedBy`. While carried: the carried player's own client drives their networked root to the carrier's `CarryAnchor`; every client pins the local ragdoll hips there so the body hangs floppy. |
| `CarryController` (new) | Player prefab | `E` grabs a ragdolled rival you're looking at (only when `Interactor` has no target of its own — steal/interact wins first); `G` sets them down; **hold LMB to charge a throw**, release to launch. |
| `PlayerRagdoll` (changed) | Player prefab | won't stand up while carried; holds the ragdoll `carryReleaseStun` past a drop/throw; `ApplyThrowImpulse` for the launch. |
| `PlayerImpactRelay` (changed) | Player prefab | relays the throw impulse to every client. |
| `PlayerTheftTarget` (changed) | Player prefab | **carried players can't be stolen from** — carrying is a grief/relocate toy, not strip-mining or body-passing. |
| `FirstPersonController` (changed) | Player prefab | `CarryingSomething` kills air-control + autohop (no bhop while carrying); walk/sprint stay normal. |
| `SabotageUseController` (changed) | Player prefab | suppressed while carrying (LMB is the throw); range-checks `SelectedSlot` now it can be `-1`. |
| `PlayerInventory` / `HotbarController` (changed) | Player prefab | carrying forces `SelectedSlot` to `-1` (no slot selected); `1`–`5` and scroll are dead until the body is dropped, then the old slot returns. |
| `Interactor` / `PickupItem` / `PlayerDropController` / `SellStation` (changed) | Player prefab / Lobby | no loot pickup, item-drop, or selling while carrying — hands full. |
| `InventoryScreenUI` (changed) | Hotbar prefab | Tab won't open your own inventory or a steal screen while carrying. |
| `RobEveryoneNetworkManager` (changed) | MainMenu | drops the body if the carrier disconnects mid-carry. |

Design (from the Q&A): `E` on *any* ragdolled rival (car or sabotage);
carrying is a light inconvenience — no bhop, no hotbar item (both hands
are on the body), and you drop it instantly if you're ragdolled; the
body stays floppy in your hands; a
carried player can't be stolen from and can't get up until a bit after
being dropped/thrown; the point is relocating rivals off the exit and
dumping them into hazards.

---

## Part 1 — Player prefab

Open `Assets/Prefabs/Player.prefab` in prefab edit mode.

### 1a. The carry anchor

1. Add an empty child of the Player root named **`CarryAnchor`**. This
   is where a carried body's hips sit. Start with:
   - **Position** `(0.35, 1.35, 0.15)` — roughly over the right shoulder.
   - **Rotation** `(0, 0, -80)` — tips the body so it drapes rather than
     standing straight up.
   You'll tune both in Rest Point 2 — the body is pinned at the hips and
   the limbs flop, so it won't look like a clean fireman's carry until
   there's a carry animation (tracked separately in todo.md). For now
   aim for "slung, not levitating."

### 1b. Components

2. **Add Component → Carryable.** `Carry Release Stun` `1.5` (seconds
   the ragdoll is held after a drop/throw before they can stand).
3. **Add Component → Carry Controller.** Wire:
   - **View Point** → the same camera/eye transform `Interactor` and
     `Sabotage Use Controller` use.
   - **Carry Anchor** → the `CarryAnchor` child from 1a.
   - **Grab Range** `2.5`
   - **Min / Max Throw Force** `8` / `45` (tune the ceiling in Rest
     Point 3 — 45 is a solid "across the road" toss)
   - **Throw Charge Time** `1.2` (seconds LMB-held for a full-power
     throw)
   - **Player Mask** → include the layer players/ragdolls sit on
     (`Default` — leave it `Everything` unless you've narrowed it)
   - **Grab Key** `E`, **Set Down Key** `G`
4. Nothing to wire on `Player Ragdoll` / `Player Impact Relay` /
   `Player Theft Target` — the carry hooks are code-only.

Save the prefab.

### 🔴 Rest Point 1 — grab + follow
Two Editors. A tases or car-hits B. While B is ragdolled, A walks up
(within ~2.5 m), looks at B, presses **E** → B's body snaps to A's
`CarryAnchor` and follows A around, limbs flopping, on **both** screens.
B can't stand up. A presses **G** → B drops, stays down ~1.5 s, then
stands.

### 🔴 Rest Point 2 — tune the anchor
Adjust `CarryAnchor`'s Position/Rotation until a carried body reads as
"being hauled" from both first person (A looking down/around) and third
person (B and any observer). Expect some clipping into A's model —
that's the missing carry animation, not a bug.

### 🔴 Rest Point 3 — throw
While carrying B, A **holds LMB** — (add a HUD charge meter later;
`CarryController.ThrowCharge01` is exposed for it) — and **releases**.
B launches along A's view, re-ragdolls, flies, lands, stays down a beat,
then stands. Tap-release = a weak toss; full charge = a real throw.
Confirm it works into the road (a traffic car then hits B) and toward
the map boundary.

### 🔴 Rest Point 4 — the guards
- A gets tased **while carrying** B → A drops B instantly and ragdolls.
- While A carries B, **B's "Steal" prompt doesn't appear** and E won't
  open a steal screen on B — for A *or* a third player C. Drop B,
  re-stun with a fresh sabotage hit → stealing works again.
- A **can't bhop** while carrying (hold Space does nothing; a single
  jump is fine but builds no air speed). Walk/sprint feel normal.
- While A carries B, **A's hotbar has no slot selected** — the
  highlight is gone, `1`–`5` and the scroll wheel do nothing, and
  `PlayerInventory.SelectedSlot` reads `-1`. Drop B and the slot A had
  before the grab comes back.
- A **can't pick up loot or drop a hotbar item** while carrying (hands
  full — no "Take" prompt on items, `Q` does nothing). `E` on a **Ready
  Spot** still works, but the **Sell Station does nothing** (no selected
  item to hand over), and **Tab won't open** A's own inventory or a
  steal screen. Sabotage LMB/RMB does not fire either (that's the throw).
- A disconnects mid-carry → B drops.

### 🔴 Rest Point 5 — full pass
One round, two Editors: stun a rival, take your one item via the steal
screen, **then** carry them off the exit path and throw them into
traffic. Confirm nothing desyncs (body position, camera, the carrier's
movement) between the two Editors, and that B is helpless the whole
time (can't act, can't stand, can't be robbed) until well after they
land.

Then tell me and it goes in `completed.md`.

---

## Follow-ups (tracked, not blocking)

- **Carry animation** — a one-armed/over-the-shoulder carry pose for the
  carrier, and a "carried" limp pose blend for the victim. Animator +
  clips. In todo.md's "held item + carry/run animations" item.
- **Throw-charge HUD meter** — a small fill bar while LMB is held
  (`CarryController.ThrowCharge01`).
- **Carried indicator** — something over a carried player so bystanders
  read the situation.
- **No-carry near the exit?** Design says relocating-off-the-exit is a
  feature, so probably leave it — but if grabbing someone the instant
  they touch the exit trigger feels cheap, gate `Carryable.CanBeGrabbed`
  on distance from `ExitPoint`.
- **Carrier keeps their one steal while carrying** — currently blocked
  outright; if you want knock-down → carry → then take your one item,
  change `PlayerTheftTarget`'s carried check to only block when the
  interacting thief *isn't* the carrier.

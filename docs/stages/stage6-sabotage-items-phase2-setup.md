# Stage 6 — Sabotage items, Phase 2 (Editor setup)

All of Phase 2's code is written: durability/ammo tracking, Bat,
Tranquilizer Gun, the PvP steal-window, Hammer's dual melee/thrown mode,
and the Alarm Clock's Homeowner-framing. This doc is what to wire up by
hand in the Editor to bring it all online. Everything below is Editor
work, not code to write yourself.

**Always press Play from `MainMenu`** — `NetworkManager` only exists
there.

---

## Part 1 — Hotbar uses/ammo display

1. Open `Assets/Prefabs/UI/Hotbar.prefab` (or find `Hotbar` in
   `SampleScene`'s Canvas if it isn't a prefab yet at this point).
2. For each of the 5 slot boxes (`Slot0`...`Slot4`): add a new
   **TextMeshPro - Text** child, positioned in a corner of the box (e.g.
   bottom-right) so it doesn't overlap the item name/model preview.
   Small font size.
3. On each slot's **Hotbar Slot UI** component, drag this new text object
   into the new **Uses Text** field.
4. Save the prefab (and re-apply to `Lobby`'s copy if it's already been
   placed there, same as `stage7b-batch-economy-hotbar-setup.md`'s own
   note about keeping both scenes' copies in sync).

### 🔴 Rest Point 1
Once Part 2 tunes the Bat, picking one up should show `x3` (or whatever
its Max Uses is set to) in the corner of its hotbar box, ticking down on
each hit. No visible change for Taser/Dynamite/plain loot.

---

## Part 2 — Bat and Tranquilizer Gun

Both already have their sabotage numbers tuned in code
(`Assets/Data/Items/BaseballBat.asset`, `TranquilizerGun.asset`) —
nothing to change here unless you want to retune them. Just confirm:

1. Select `BaseballBat.asset` — **Sabotage Type** should read `Melee`,
   **Max Uses** = `3`.
2. Select `TranquilizerGun.asset` — **Sabotage Type** should read
   `Ranged`, **Max Uses** = `2`, **Range** = `15`.

### 🔴 Rest Point 2
Two-Editor test: pick up the Bat, hit a target 3 times, confirm the
slot empties on the 3rd. Pick up the Tranquilizer Gun, stun a target
from range, confirm it doesn't fire through a wall placed between you
and them, and empties after 2 shots.

---

## Part 3 — PvP steal-window

1. Open `Assets/Prefabs/Player.prefab` (prefab edit mode).
2. Select the root, **Add Component** → search `Player Theft Target`,
   add it. No fields to wire — it finds `PlayerImpactRelay`/
   `PlayerInventory` on the same object itself.
3. Save the prefab.

### 🔴 Rest Point 3
Taser or Bat a target, aim at them within the ~6 second window, confirm
an E-key prompt appears (same style as a `PickupItem`'s prompt) and
pressing E moves one of their items into your hotbar. Confirm the prompt
disappears immediately after a successful steal, and that driving a
traffic car into someone never shows the prompt (only PvP sabotage hits
open the window).

---

## Part 4 — Hammer

This is the most involved part — a Hammer thrown and landed needs to
become a real pickup again, carrying whatever durability it had left.

1. In `Assets/Prefabs/Sabotage/`, build `HammerProjectile.prefab` the
   same way `DynamiteProjectile.prefab` was built in Phase 1:
   - Reuse the Hammer art model (from `Assets/Prefabs/Items/Hammer.prefab`).
   - **Sphere Collider** (or Box, whichever wraps the model better) —
     leave as a normal (non-trigger) collider.
   - **Rigidbody** — **Use Gravity** on.
   - **Network Identity**
   - **Network Transform (Reliable)** — **Sync Direction: Server To
     Client** (the mistake caught in Phase 1 — don't leave this at its
     Client To Server default).
   - **Retrievable Projectile** (`RobEveryone.Sabotage`), not
     `Sabotage Projectile`. Drag `Assets/Prefabs/Items/Hammer.prefab`
     (the existing pickup) into its **Pickup Prefab** field — a landed
     Hammer respawns as that exact prefab, just with its uses count
     carried over via `Initialize(item, uses)` instead of resetting to
     full.
2. Select `Assets/Data/Items/Hammer.asset`, drag
   `HammerProjectile.prefab` into its **Thrown Projectile Prefab**
   field. (**Sabotage Type** already reads `Melee, Thrown` and **Max
   Uses** = `3` from code — no change needed there.)
3. Register `HammerProjectile.prefab` in `NetworkManager`'s **Spawnable
   Prefabs** list (`MainMenu.unity`), same as Phase 1's Dynamite
   registration.

### 🔴 Rest Point 4
Melee-swing the Hammer (left-click), confirm it shares the same uses
counter Bat uses. Right-click to throw it at a target, confirm it stuns
them the moment it hits (not after a fuse delay) and decrements uses.
Walk to where it landed, confirm it's a real pickupable `Hammer.prefab`
showing the *reduced* uses count in its hotbar box (not reset to `x3`).
Let a thrown Hammer miss entirely (no one in its path) — confirm it
still lands and stays pickupable with its uses unchanged. Use up all 3
charges — confirm the last swing/throw breaks it (no pickup spawns).

---

## Part 5 — Alarm Clock

1. In `Assets/Prefabs/Sabotage/`, build `AlarmClockProjectile.prefab`,
   same component shape as `DynamiteProjectile.prefab` (**Network
   Identity**, **Network Transform (Reliable)** — Server To Client,
   **Rigidbody**, a **Collider**) but with **Alarm Clock Projectile**
   (`RobEveryone.Sabotage`) instead of `Sabotage Projectile`.
2. Select `Assets/Data/Items/AlarmClock.asset`, drag
   `AlarmClockProjectile.prefab` into its **Thrown Projectile Prefab**
   field. (**Sabotage Type** already reads `Thrown` and **Blast
   Radius** = `8` from code — this doubles as both the "how close a
   rival needs to be to get framed" and "how close a Homeowner needs to
   be to hear it" radius.)
3. Register `AlarmClockProjectile.prefab` in `NetworkManager`'s
   **Spawnable Prefabs** list.

### 🔴 Rest Point 5
Throw the Alarm Clock near a Homeowner with another player standing
nearby (not the thrower) — confirm the Homeowner goes straight to
**Alerted** (red) and Police head directly for the *framed* player,
even from across the map where they'd normally have no vision-cone
reason to react at all. Throw it with nobody else nearby — confirm the
Homeowner still goes Alerted, just with nobody specifically framed
(Police fall back to investigating the position, same as an organic
sighting). Confirm a real, non-Alarm-Clock Homeowner sighting still
behaves exactly as before.

---

## Editor note: test pickups

Same as Phase 1 — there's no shop-buy flow yet and these items must
never go in a `LootTable` (`item-creation-setup.md` §4b). Hand-place a Bat,
Tranquilizer Gun, Hammer, and Alarm Clock in `SampleScene` for testing,
and remove them once every Rest Point above passes.

If something silently does nothing, check the Console first — the most
likely culprits are a missing **Thrown Projectile Prefab** wiring (Parts
4/5), the new prefab missing from **Spawnable Prefabs**, or
`NetworkTransformReliable` left on its Client To Server default instead
of Server To Client.

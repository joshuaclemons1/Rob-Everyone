# Stage 6 — Sabotage items, Phase 1 (Editor setup)

All 6 sabotage items (Taser, Bat, Hammer, Tranquilizer Gun, Dynamite,
Alarm Clock) already have a prefab and an `ItemDefinition` — spawnable,
pick-up-able, networked, but with zero use-behavior. **Phase 1's code is
already written** (`ItemDefinition`'s new `SabotageType`/stun/force/
cooldown/range/blastRadius/projectile fields, `PlayerInventory.RemoveSlot`,
`PlayerRagdoll`/`PlayerImpactRelay`'s configurable stun duration, and the
new `Assets/Scripts/Sabotage/SabotageUseController.cs` +
`SabotageProjectile.cs`). This doc is what to wire up by hand in the
Editor to bring exactly 2 items — **Taser** (melee) and **Dynamite**
(thrown AOE) — online end to end. Everything below is Editor work, not
code to write yourself.

Phase 2 (Bat, Hammer, Tranquilizer Gun, the PvP steal-window, Alarm
Clock's Homeowner-framing) is deliberately not covered here — see
`stage6-sabotage-items-phase2-setup.md`, or
[issue #35](https://github.com/joshuaclemons1/Rob-Everyone/issues/35)
for why those are scoped separately (both phases are done now).

**Always press Play from `MainMenu`**, same as Stage 4/5 — `NetworkManager`
only exists there, and the real test flow is Play → Host → `Lobby` →
`SampleScene`.

---

## Part 1 — Add the use-controller to the Player prefab

1. In the Project window, open `Assets/Prefabs/Player.prefab` (double-
   click to enter prefab edit mode, not just select it).
2. Select the prefab's root GameObject. In the Inspector, click **Add
   Component**, search `Sabotage Use Controller`, add it.
3. While still on the root GameObject, look at the existing **Interactor**
   component and note whatever Transform is dragged into its **View
   Point** field (this is the camera). Drag that exact same Transform
   into the new **Sabotage Use Controller → View Point** field.
4. Leave **Player Mask** as `Everything` and **Melee Range Slack** at its
   default (`0.5`) — no change needed.
5. Exit prefab edit mode and save (Ctrl+S).

### 🔴 Rest Point 1
Console should still be clean. Re-select `Player.prefab` and confirm
**Sabotage Use Controller**'s **View Point** field is not empty.

---

## Part 2 — Tune the Taser's `ItemDefinition`

1. In the Project window, select `Assets/Data/Items/Taser.asset`.
2. Under the new **Sabotage** header in the Inspector, set:
   - **Sabotage Type** = `Melee`
   - **Stun Duration** = `1.5`
   - **Impact Force** = `15`
   - **Cooldown Seconds** = `8`
   - **Range** = `2.5`
3. These are placeholder numbers (matching the plan's own tuning
   caveat) — fine to adjust after playtesting once it's in.

---

## Part 3 — Build the Dynamite projectile prefab

This is a **separate** prefab from `Assets/Prefabs/Items/Dynamite.prefab`
(that one stays exactly as-is — it's the world pickup). This new one is
what actually flies through the air after it's thrown.

1. In the Project window, create a new folder: `Assets/Prefabs/Sabotage/`.
2. In `SampleScene`'s Hierarchy, drag the Dynamite art model (the same
   model used inside `Assets/Prefabs/Items/Dynamite.prefab` — open that
   prefab to find its model reference if you're not sure which asset it
   is) into the scene to create a new GameObject. Rename it
   `DynamiteProjectile`.
3. With `DynamiteProjectile` selected, **Add Component** for each of the
   following:
   - **Sphere Collider** — leave as a normal (non-trigger) collider,
     sized to roughly wrap the model.
   - **Rigidbody** — check **Use Gravity** on.
   - **Network Identity**
   - **Network Transform (Reliable)** — set its **Sync Direction** to
     **Server To Client** (this is the opposite of the Player prefab's
     setting — this object is server-simulated physics, not
     client-predicted, same as the traffic cars).
   - **Sabotage Projectile** (`RobEveryone.Sabotage`) — leave **Throw
     Speed** (`12`) and **Fuse Seconds** (`2.5`) at their defaults for
     now.
4. Drag `DynamiteProjectile` from the Hierarchy into
   `Assets/Prefabs/Sabotage/` to turn it into a prefab, then delete the
   instance from the Hierarchy (`SampleScene` shouldn't keep a permanent
   copy sitting in it).

### 🔴 Rest Point 3
Console clean, and `Assets/Prefabs/Sabotage/DynamiteProjectile.prefab`
exists with all 5 components listed above.

---

## Part 4 — Tune the Dynamite's `ItemDefinition`

1. Select `Assets/Data/Items/Dynamite.asset`.
2. Under **Sabotage**, set:
   - **Sabotage Type** = `Thrown`
   - **Stun Duration** = `30`
   - **Impact Force** = `50`
   - **Blast Radius** = `6`
   - **Thrown Projectile Prefab** = drag in
     `Assets/Prefabs/Sabotage/DynamiteProjectile.prefab` from Part 3.

---

## Part 5 — Register the projectile for networking

1. Open `Assets/Scenes/MainMenu.unity`.
2. Select the `NetworkManager` GameObject.
3. On the `NetworkManager` component, find the **Spawnable Prefabs**
   list (the same list already holding the ~34 loot prefabs and 6
   sabotage pickups). Increase its **Size** by 1.
4. Drag `Assets/Prefabs/Sabotage/DynamiteProjectile.prefab` into the new
   empty slot.
5. Save the scene.

### 🔴 Rest Point 5
`NetworkManager`'s Spawnable Prefabs list now includes
`DynamiteProjectile`. If this step is skipped, throwing Dynamite will
throw a `NetworkServer.Spawn` error in the Console the moment it's used
(Mirror refuses to spawn a prefab it doesn't recognize).

---

## Part 6 — Place test pickups

There's no shop-purchase flow yet (that's Stage 7) and sabotage items
must never be added to a `LootTable` (`item-creation-setup.md` §4b), so
testing needs a temporary hand-placed copy of each.

1. Open `SampleScene`.
2. Drag one `Assets/Prefabs/Items/Taser.prefab` and one
   `Assets/Prefabs/Items/Dynamite.prefab` into the scene, somewhere near
   a player spawn point so they're easy to reach.
3. Remember to remove both once testing in Part 7 is done — they're not
   meant to stay in the scene permanently.

---

## Part 7 — Test (two-Editor ParrelSync, same convention as Stage 4/5)

1. Press Play from `MainMenu` in one Editor instance, **Host**. In the
   ParrelSync clone, Play from `MainMenu`, **Join**. Both land in
   `Lobby`, then walk to `ReadySpot` to carry into `SampleScene`.
2. **Compile/regression check first**: confirm the Console is clean, and
   drive a traffic car through a player — confirm the original ~2 second
   stun still looks and behaves exactly like before (`CarDriver.cs`
   needed zero changes, so this should be unaffected).
3. **Taser test**: Player A walks to the Taser, presses `E` to pick it
   up, selects its hotbar slot, gets within melee range of Player B, and
   left-clicks.
   - Confirm on **both** clients that B ragdolls for about 1.5 seconds
     then regains control.
   - Immediately left-click again — confirm nothing happens until the
     ~8 second cooldown passes.
   - While B is still stunned, taser them again — confirm it does
     nothing (no stacking a second stun on top of an active one).
4. **Dynamite test**: Player A picks up the Dynamite, selects it, and
   throws it (left-click) so both players end up within roughly 6 meters
   of where it lands.
   - Confirm the model visibly arcs under gravity on **both** clients.
   - Confirm it disappears from A's hotbar the instant it's thrown (it's
     consumed on use, not after it lands).
   - After the ~2.5 second fuse, confirm **both** players ragdoll for
     ~30 seconds simultaneously (true AOE) — then move one player
     outside the 6m blast radius before it detonates and confirm they're
     unaffected while a closer player still gets hit.
5. **`IsFrozen` regression check**: get a player mid-ragdoll-stun (Taser
   or Dynamite), then have Police catch them while they're still
   stunned. Confirm that once the stun timer would have ended, the
   player stays frozen (police-caught) instead of controls silently
   coming back — this is the coordination bug Phase 1's `PlayerRagdoll`
   change specifically fixed.
6. Once everything above passes, delete the two test pickups placed in
   Part 6 from `SampleScene`.

If something doesn't fire at all, check the Console first — the most
likely culprits are a missing **View Point** wiring on **Sabotage Use
Controller** (Part 1), the Dynamite prefab missing from **Spawnable
Prefabs** (Part 5), or an `ItemDefinition`'s **Sabotage Type** left at
`None` (Parts 2/4).

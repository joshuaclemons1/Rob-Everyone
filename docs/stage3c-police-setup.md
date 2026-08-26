# Stage 3c — Editor setup (police AI + jail)

Script is in `Assets/Scripts/AI/PoliceAI.cs`. This project already has the
**AI Navigation** package installed (`com.unity.ai.navigation` in
`Packages/manifest.json`), which is what Unity 6 uses for NavMesh baking —
you won't need to install anything.

Do this in order: bake a NavMesh first, then build the Police character —
a `NavMeshAgent` refuses to move if it isn't standing on baked NavMesh.

## 1. Bake the NavMesh

1. Right-click in the **Hierarchy** → **Create Empty**. Rename it
   `Navigation`.
2. With it selected, **Add Component** → search `Nav Mesh Surface` → add it.
3. Leave all its settings at default (**Collect Objects: All**,
   **Use Geometry: Render Meshes**) — this scans every renderer in the scene
   automatically, you don't need to point it at `Ground` or the houses
   manually.
4. Scroll to the bottom of the Nav Mesh Surface component and click
   **Bake**.
5. You should see a light blue overlay appear over `Ground` and each
   house's floor in the Scene view — that's the walkable area. If it's
   missing from inside a house, double-check that house's floor has a
   Collider/Renderer (it will, if it's built the same way as the others).

Walls shouldn't get included automatically — NavMesh baking only includes
roughly-flat surfaces (steeper than 45° by default), so a vertical wall
won't accidentally become "walkable." You don't need to mark anything as
an obstacle by hand.

## 2. The police officer's body

1. Right-click Hierarchy → **3D Object → Capsule**. Rename it `Police`.
2. Position it somewhere on the open `Ground`, away from any house — e.g.
   near wherever your row of houses starts, opposite the `Exit`.
3. **Add Component** → search `Nav Mesh Agent` → add it. This is the
   component that actually walks the character around; it's separate from
   the `Nav Mesh Surface` you added in step 1, which just holds the baked
   data.
4. On the Nav Mesh Agent component, set **Stopping Distance** to `0.5`.
   (Default is `0`, which makes "have I arrived?" checks unreliable due to
   how the agent's path smoothing works.)

## 3. The "eye" point

Same idea as the homeowner — a dedicated look-from point instead of the
capsule's center.

1. Right-click `Police` → **Create Empty**. Rename it `Eye`.
2. Set its **Position** to `X 0, Y 0.7, Z 0.3` (local, relative to `Police`).
3. Leave **Rotation** at `(0, 0, 0)`.

## 4. Patrol points

1. Right-click Hierarchy → **Create Empty**, rename `PatrolPoint_1`. Repeat
   3 more times for `PatrolPoint_2`, `PatrolPoint_3`, `PatrolPoint_4` — these
   are just position markers, no components needed.
2. Position each one somewhere on the open `Ground` between/around your
   houses, forming a rough loop. This is what Police walks between when
   nothing's happening.

## 5. The Police AI component

1. Select `Police` in the Hierarchy.
2. **Add Component** → search `Police AI` → add it.
3. Drag `PlayerCamera` (the child under `Player`) into **Player Target** —
   same reasoning as the homeowner, a fair head-height target.
4. Drag `Eye` into the **Eye** field.
5. Drag `RoundManager` into the **Round Manager** field — this is what lets
   a catch actually end the round.
6. Expand the **Patrol Points** list (click the arrow, or the number field
   controls its size). Drag `PatrolPoint_1` through `PatrolPoint_4` in, one
   at a time, in order.

   Shortcut: you can select all 4 patrol point objects at once in the
   Hierarchy (click the first, then Shift-click the last) and drag them
   together onto the **Patrol Points** field to fill the whole list in one
   drag — worth trying, but double-check the order landed correctly
   afterward.
7. Leave the rest at default for the first test: **View Distance** `12`,
   **View Angle** `90`, **Catch Distance** `1.5`, **Lose Interest Time**
   `4`.

## 6. Test

1. Press Play. `Police` should immediately start walking a loop between
   your 4 patrol points.
2. Walk into a homeowner's vision cone until it turns red (Alerted), like
   in Stage 3b. Confirm `Police` breaks off its patrol loop and heads
   toward roughly where you were standing.
3. Let `Police` get within its own cone (drawn in **red** when it's
   selected, to tell it apart from a homeowner's cyan one) — it should
   switch into an active chase, pathing around walls rather than cutting
   through them.
4. Let it close the distance and catch you. You should see:
   - Your movement freezes completely (WASD/mouse stop responding).
   - The round ends immediately with **"Caught by the police!"** on the
     result banner, regardless of how much you'd picked up.
5. Also test the "lose interest" path: get spotted, then break line of
   sight (duck behind a wall) for a few seconds. `Police` should head to
   where it last saw you, search briefly, then give up and resume patrol if
   it doesn't reacquire you.

If `Police` never moves at all: check the Console for a message like "agent
not on NavMesh" — it means either the Bake in step 1 didn't run, or
`Police` is positioned somewhere that wasn't included in the baked area
(floating slightly off the ground, for instance).

If it doesn't react to an alert: this shouldn't need any manual wiring
between `Homeowner` and `Police` — they're connected automatically through
code, not an Inspector reference. Recheck that both scripts compiled
without errors (Console) and that `Police Target`/`Eye` are actually
assigned on `Police AI`.

Once this works, we can duplicate `Police` and `Homeowner` into the
remaining houses, then start thinking about Stage 3's remaining pieces —
tell me how this test goes.

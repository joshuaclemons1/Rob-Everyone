# Stage 3e — Editor setup (real house prefab #1, prefab-pool foundation)

Goal for this doc: build **one** fully real, self-contained house prefab —
real building, furnished interior, yard padding to a consistent plot size,
its own `Homeowner`, a loot spot — that can drop into the scene exactly
where `House_01`'s blockout currently sits. Once this one works end to end,
we repeat it 2–3 more times (Stage 3f) and then build the random-placement
spawner (Stage 3g). Don't build those yet — get one house fully right first.

Plot size target: **40×40** (updated from the original 25×25 to give houses
more breathing room), so this can be swapped in without touching
`RoundManager`, `Exit`, or anything else already placed.

## 0. Quick check before starting: does furniture render correctly?

If you haven't confirmed this yet, drag one Furniture Kit piece (e.g.
`chair.fbx`) into an empty spot in the scene and look at it from a few
angles. If it looks wrong (missing color, see-through from some angle),
run it through the same fix as the buildings before continuing:
**Materials tab → Extract Materials... → select the extracted material →
Render Face: Both**. Don't furnish the whole interior until one test piece
looks right — much cheaper to fix once than to redo a furnished room.

## 1. Place and fix the base building

1. In the **Project** window, pick one building from
   `Assets/Art/Environment/Kenney-CityKitSuburban/FBX/` — try
   `building-type-a.fbx` first, you can always swap later.
2. Drag it into the Scene view, away from your existing houses.
3. Fix its material the same way as before: select the `.fbx` → **Materials**
   tab → **Extract Materials...** → pick a destination folder (e.g.
   `Assets/Art/Environment/Kenney-CityKitSuburban/Materials/`) → select the
   extracted material → **Render Face: Both**. Skip this if you already did
   it for this specific building file earlier.
4. Rename the instance in the Hierarchy to `House_Real_01`.
5. **Measure its actual footprint**: select it, look at its bounds — either
   eyeball against the 1-unit grid squares in the Scene view, or
   temporarily add a Box Collider (Add Component → Box Collider) and read
   its auto-fitted **Size** values in the Inspector, then remove that
   collider once you've noted the numbers. You'll need this in step 3.

## 2. Verify (and fix, if needed) the door

Kenney's complete building models may or may not have a real walk-through
gap where the door is drawn — and by default the FBX likely imports with
**no collider at all**. Fix collision first, then test the door:

1. Select the building `.fbx` → **Model** tab → check **Generate
   Colliders** → **Apply**. Confirm the scene instance now has a **Mesh
   Collider**.
2. Press Play, walk into the doorway specifically.
3. **If you walk straight through but are blocked by the walls** — the
   model has a real door gap, you're done, nothing more needed.
4. **If you're blocked at the door too** (or the whole building has no
   collider at all) — don't hand-build Box Colliders around the building.
   Use **`DoorTeleporter`** (`Assets/Scripts/World/DoorTeleporter.cs`)
   instead: leave the building's default/generated collider completely
   untouched (solid door and all — this also means yard clutter like
   bushes and fences never need custom collision either), and add a paired
   trigger-teleporter at the doorway. Setup:
   - Two empty GameObjects with **Box Colliders** (**Is Trigger**) —
     `Door_Outside` (right at the door, outside) and `Door_Inside` (a
     couple units into the room) — each with a **Door Teleporter**
     component.
   - Two more **plain empty GameObjects with no collider** — `Landing_Inside`
     and `Landing_Outside` — positioned clear of either trigger's bounds.
   - `Door_Outside`'s Destination → `Landing_Inside`. `Door_Inside`'s
     Destination → `Landing_Outside`. (Destinations must be the separate
     landing points, not the trigger objects themselves, or you'll get an
     immediate teleport-back loop.)
   - Test the round trip: walk in, confirm a clean landing; walk back near
     where you landed, confirm a clean exit.

## 3. Build the yard padding

Using pieces from `Assets/Art/Environment/Kenney-CityKitSuburban/FBX/`
(`driveway-long`/`driveway-short`, `fence-1x2` through `fence-3x3`):

1. Compare the building's footprint (measured in step 1.5) against the
   40×40 target. Whatever's left over is yard.
2. Add a flat ground piece under the whole 40×40 plot for the yard area —
   reuse the same technique as your original `Ground` object (a flattened
   Cube, or a Plane), sized to fill the plot, sitting at the same height as
   the building's own floor level so there's no seam.
3. Place fence pieces around the plot's outer edge (pick sizes that combine
   to roughly the plot's perimeter — mixing `fence-1x2`/`1x3`/etc. to fit
   is expected, this is a puzzle-piece kit, not a single stretchable piece).
4. Add a driveway piece leading from the plot edge to the building's door,
   for visual clarity about where the entrance is.
5. Group the building + all yard pieces under one empty parent GameObject,
   rename it `House_Real_01` at the top level (rename the old building
   object underneath to something like `Building` to avoid the naming
   clash).

## 4. Furnish the interior

1. From `Assets/Art/Environment/Kenney-FurnitureKit/FBX/`, drag in a few
   pieces appropriate to a single-room house — a `chair`, a `table`,
   a `loungeSofa`, a `bookcaseOpen`, whatever reads well in the space you've
   got.
2. Kenney packs are generally authored at a consistent real-world scale
   across their whole catalog, so Furniture Kit pieces should already be
   correctly sized against the building without manual rescaling — sanity
   check by comparing a chair's height against the `Player` capsule (roughly
   waist-to-seat height, not door-sized) and adjust only if something looks
   obviously off.
3. Position pieces so they don't block the doorway or the loot spot you'll
   add next. Larger pieces placed mid-room (a sofa, a bookcase) are useful
   both for visual interest and as partial cover from vision cones, per our
   earlier discussion on interior walls.

## 5. Add the loot spot and Homeowner

Both live **inside** this prefab now, unlike the blockout houses (where
`Homeowner` was a separate sibling object) — that's the actual change this
system needs, since a randomly-picked house prefab has to bring its own
homeowner and loot with it.

1. Right-click Hierarchy → **3D Object → Cube**, position it somewhere
   findable inside the room. Add component **Pickup Item**
   (`RobEveryone.Items`), set `Item Name`/`Value`. Make sure it's a **child**
   of `House_Real_01` (drag it onto that row if it isn't already).
2. In the **Project** window, drag `Assets/Prefabs/Houses/Homeowner.prefab`
   into the Hierarchy **as a child** of `House_Real_01`. This is a nested
   prefab — it stays linked to the base `Homeowner` prefab (tuning changes
   there still propagate), while its position is local to this house.
3. Position it somewhere sensible in the room, rotate it (watch the cyan
   cone) to face a natural spot — e.g. toward the door, or toward the loot.
4. **Re-check the `Player Target` field** on this `Homeowner` instance's
   `Homeowner AI` component — nested-prefab instances sometimes need the
   scene-object reference (`PlayerCamera`) re-assigned per instance, same
   as when we duplicated houses back in Stage 3a.

## 6. Make it a Prefab, and test

1. Drag `House_Real_01` from the Hierarchy into `Assets/Prefabs/Houses/`,
   same as `House_01` and `Homeowner` before it.
2. Position this new prefab instance exactly where the old `House_01`
   blockout currently sits (or just disable/hide `House_01` for now rather
   than deleting it — keep it as a fallback until this is fully confirmed
   working).
3. Press Play. Walk in through the door, confirm the loot pickup works,
   confirm the `Homeowner`'s vision cone/state escalation still works
   exactly like it did in the blockout version, confirm you can walk back
   out.

Once this one house fully works — door, yard, furniture, loot, homeowner,
all as one prefab — tell me, and we'll move to Stage 3f: repeating this for
2–3 more building variants to actually build the pool this whole system
needs.

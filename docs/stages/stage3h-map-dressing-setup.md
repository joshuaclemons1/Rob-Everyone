# Stage 3h — Editor setup (roads, police compound, exit)

**Status (2026-09-04):** Road loop + driveways placed. Compound fence
replaced with a real modular chain-link/barbed-wire/gate kit (TampaJoey's
Chain Link Fence Pack, CC-BY — see `art-info.md`) — perimeter is placed
(`Fence_Straight`/`Barbwire_*`/two `Gate6` instances) around the
compound. Police station placed with an essential interior: 3 jail cells
(`Jail_1277`, Poly by Google, CC-BY) + 2 `PoliceDesk`s — intentionally not
over-furnished since the Jail & Bail system itself isn't built until
Stage 7. `Exit` already repositioned away from the compound (z: 110,
opposite side from the compound's z: -78 to -110).
**Done** (see [issue #30](https://github.com/joshuaclemons1/Rob-Everyone/issues/30)): the 2 Good House slots
were repositioned to read as inside/adjacent to the compound, and the
full-loop playtest in section 4 is confirmed working — Stage 3 is
feature-complete on real art.

With Stage 3g's slots spawning real houses randomly, this stage fills in
everything between them — roads connecting the slots, the fenced central
compound with the police station, and the exit — turning the layout into
an actual readable map instead of houses floating in empty space.

## 1. Roads

Using `Assets/Art/Environment/Kenney-CityKitRoads/FBX/`:

1. Same material fix as every other Kenney building import if you haven't
   done it for road pieces yet: select the `.fbx` → Materials tab →
   Extract Materials → select the material → Render Face: Both.
2. Lay a road/path from each house slot's driveway (Stage 3e/3f already
   added a driveway per house) toward a shared central path system linking
   all the slots — doesn't need to be a fully modeled street grid, just
   enough to make walking between houses read as "a neighborhood," not
   scattered buildings on grass.
3. Keep the same 40×40-plot-based spacing from Stage 3g in mind — road
   pieces should fit the gaps you already left between slots, not force
   you to re-space houses.

## 2. Fenced police compound

Using `Assets/Art/Environment/Kenney-CityKitCommercial/FBX/` (police
station piece) and fence pieces from `Kenney-CityKitSuburban/FBX/`
(`fence-1x2` through `fence-3x3`, same as house yards):

1. Pick a central position (per the sketch: middle of the house ring) and
   place a police station building there.
2. Fence the compound perimeter, same puzzle-piece approach as a house
   yard fence in Stage 3e — mix fence sizes to fit whatever perimeter
   shape you land on.
3. Decide the compound entrance count now (per plan.md's note, this was
   left open) — a single gated entrance is the simpler, more defensible
   default matching "campable single exit" thinking from
   [gameplay-design.md](gameplay-design.md), but it's your call once you
   see the actual compound size.
4. Place the **2 Good House slots** from Stage 3g adjacent to/inside this
   compound, per the sketch.
5. If the police station has a jail/interior relevant to the Jail & Bail
   design (gameplay-design.md), this is also the point to decide whether
   it needs a walkable interior now or can stay exterior-only until that
   system is actually built (Stage 7) — don't over-build interior detail
   for a system that isn't coded yet.

## 3. Exit

- Move the existing `Exit` GameObject (Stage 3a) to a sensible position
  relative to the finished layout — away from the compound, ideally a
  clear sightline/walk from the house ring per the sketch's "single exit"
  intent.
- No functional changes needed — `ExitPoint.cs` already works, this is
  purely a repositioning + making sure it reads as "the goal" visually
  (per the still-open art-info.md to-do item on this).

## 4. Test the whole loop on the real layout

1. Press Play. Walk the actual road network between a few houses.
2. Confirm the compound reads clearly as off-limits/dangerous (fence
   line, police station visible) without needing a label.
3. Rob a couple of houses, including a Good House, and walk to the exit
   through the real layout — confirm nothing about the road/fence
   placement blocks a path that should be walkable, or leaves a gap where
   a wall should be.

Once this is solid, the offline single-player loop (Stage 3) is
essentially feature-complete on real art — that's a good point to
playtest solo a few times before moving to Stage 4 (two players, same
machine).

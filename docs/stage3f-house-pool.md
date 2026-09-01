# Stage 3f — Editor setup (build the house prefab pool)

`Real_House_01` proved the pattern works end to end. This stage just
repeats it 2–3 more times with different buildings, so Stage 3g (the random
spawner) has an actual pool to pick from. Nothing new to learn here — same
process as [stage3e-house-prefabs.md](stage3e-house-prefabs.md), condensed.

Aim for **3–4 total house prefabs** before moving to Stage 3g — enough that
random placement doesn't feel repetitive, without over-investing before the
spawner system (which is what actually proves this was worth building) is
in place.

## Per-building checklist

For each new building (try `building-type-b`, `-c`, `-d`, or whichever
look visually distinct from `-a` and from each other):

1. Drag the `.fbx` in, extract its material if needed, `Render Face: Both`.
   Skip extraction if it shares a material already extracted from a
   previous building — check the Materials tab first.
2. Check the **Scale Factor** on this building's own import settings before
   manually scaling the instance — if it's the same source pack as
   `building-type-a`, the same ~15x correction likely applies, and fixing
   it at the import level saves you from eyeballing scale by hand every
   time.
3. **Generate Colliders**, test the door, add a `DoorTeleporter` pair if
   needed (per the updated Stage 3e).
4. Measure the footprint, pad to the 25×25 plot with yard pieces
   (`driveway-*`, `fence-*`) from `CityKitSuburban`.
5. Furnish the interior from `Kenney-FurnitureKit` — vary the furniture
   choices between houses too, not just the building shell, so the pool
   doesn't feel like reskins of the same room.
6. Add a loot spot (`PickupItem`) and a nested `Homeowner` instance, same as
   before — re-check `Player Target` on the nested `Homeowner AI` each time.
7. Group under one parent, name it `Real_House_0N`, drag into
   `Assets/Prefabs/Houses/` to make it a Prefab.

## When the pool is done

Don't place these in the scene yet beyond whatever you need to test each
one individually — Stage 3g's spawner is what actually places them
correctly across the map layout. Once you've got 3–4 real house prefabs
built and each one tested solo (walk in, loot works, homeowner reacts,
walk back out cleanly), tell me and we'll write Stage 3g: the slot layout
matching plan.md's map sketch, and the script that randomly assigns one
prefab per slot.

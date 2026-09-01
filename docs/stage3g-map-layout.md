# Stage 3g — Editor setup (slot layout + randomized house spawner)

Once Stage 3f's house pool has 3–4 real house prefabs, each individually
tested, this stage lays out where houses actually go on the map and writes
the script that randomly assigns one pool prefab per slot — this is what
turns "a pool of house prefabs" into an actual map.

Per [plan.md](plan.md)'s note, exact house/Good House counts are rough
guidance, not locked — this doc gives you a process to decide them, not a
fixed number to hit.

## 1. Decide slot count

- Start conservative: **as many slots as you have house prefabs, ×2**
  (e.g. 4 prefabs → 8 slots) so repeats aren't back-to-back-obvious, without
  overbuilding the map before you've even seen it in motion.
- You can always add more slots later — the spawner script (step 3) reads
  whatever slot list you give it, no code changes needed to resize the map.

## 2. Place slot markers

1. Create an empty parent GameObject named `HouseSlots` in the Hierarchy.
2. Under it, create one empty child Transform per slot, named `Slot_01`,
   `Slot_02`, etc. — these are just position/rotation markers, no
   components needed yet.
3. Arrange them roughly in the sketch's ring layout around where the
   fenced compound will sit (Stage 3h). Each real house prefab is a 25×25
   plot (from Stage 3e) — space slots at least 25–30 units apart center to
   center so houses don't overlap, with enough gap between them to walk
   around (the original Stage 3a blockout used ~40-unit spacing as a
   comfortable reference).
4. Set each slot's rotation to face however you want that house's door to
   orient — the spawned prefab will inherit this.
5. Mark **2 of these slots** (however you decide, e.g. `Slot_01` and
   `Slot_05`) as the **Good House** slots — closer to where the compound
   will go, per the sketch.

## 3. Write the spawner script

New script: `Assets/Scripts/World/HousePoolSpawner.cs` (or similar,
`RobEveryone.World` namespace to match `DoorTeleporter.cs`).

- Fields: a `List<GameObject> normalHousePrefabs`, a
  `List<GameObject> goodHousePrefabs` (can be the same list with a value
  multiplier instead of separate prefabs, if that's simpler once you're
  actually building this), a `List<Transform> normalSlots`, a
  `List<Transform> goodSlots`.
- On `Start()`, for each slot, `Instantiate()` a randomly-picked prefab
  from the matching pool at that slot's position/rotation.
- **Important code note:** every house prefab has a nested `Homeowner`
  whose `Homeowner AI` component needs a `Player Target` reference — until
  now that's been hand-dragged per instance in the Inspector (Stage 3a/3e
  notes). That doesn't work for prefabs instantiated at runtime. Add a
  fallback in `HomeownerAI.cs`'s `Awake()`/`Start()`: if `playerTarget` is
  null, find it automatically (e.g.
  `FindFirstObjectByType<PlayerInventory>()` or tag-based lookup) instead
  of requiring a manual drag every time. Same applies to `PoliceAI.cs` if
  Police ever gets instantiated rather than hand-placed.

## 4. Test

1. Press Play. Confirm each slot spawns a house (no empty slots, no
   overlapping houses).
2. Walk into 2–3 different spawned houses, confirm loot pickup and the
   Homeowner's vision cone/alert both work without any manual Inspector
   wiring (this is the actual proof the auto-find fallback from step 3
   works).
3. Confirm the 2 Good House slots specifically spawn from the Good House
   pool, not a regular one.

## 5. Iterate on layout, not code

Once the spawner works, tuning the map (slot count, spacing, which slots
are "Good") is now a matter of moving/adding/removing empty Transforms in
the Hierarchy — no script changes needed. Playtest a few configurations
before locking in a final layout.

Once this works, tell me and we'll move to Stage 3h: connecting the slots
with real roads and building the fenced police compound.

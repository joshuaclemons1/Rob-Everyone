# Stage 3a — Editor setup (quota, timer, multiple houses, exit)

Scripts are already in `Assets/Scripts/Round/` and `Assets/Scripts/UI/`. This
covers wiring them into `SampleScene`, alongside the Stage 2 room that
already works.

This is **3a only**: quota + timer + exit, no AI yet. Homeowner AI (3b) and
police AI + jail (3c) come after this is tested and working.

## 1. More houses

`House_01` is now a Prefab at `Assets/Prefabs/Houses/House_01.prefab`, one
size for now (larger/smaller variants come later). Reuse it for the other
1–2 houses:

1. In the **Project** window, open `Assets/Prefabs/Houses/`.
2. Drag `House_01` from the Project window into the **Scene view** twice —
   each drag creates a new linked instance in the Hierarchy, named
   `House_01` (and `House_01 (1)` etc. if it clashes).
3. In the **Hierarchy**, rename the two new instances to `House_02` and
   `House_03` (F2 or slow double-click). Renaming an instance is a
   per-instance override — it does **not** change the shared Prefab asset or
   affect `House_01`.
4. Select each new house's Transform in the Inspector and set its
   **Position** so the 25×25m rooms don't overlap — e.g. if `House_01` sits
   at (0, 0, 0), try `House_02` at (40, 0, 0) and `House_03` at (0, 0, 40).
   That leaves roughly a 15m gap between walls, plenty of room to walk
   around the outside.
5. For each new house, add one `PickupItem` cube **inside** the room (not
   part of the prefab): right-click Hierarchy → **3D Object → Cube**, add
   component **Pickup Item** (`RobEveryone.Items`), set `Item Name` and
   `Value` in the Inspector, and position it somewhere inside that house's
   floor. Give each item a different value so the quota takes a few pickups
   to hit — items aren't baked into the Prefab on purpose, since loot should
   vary per house.

Placeholder blockout geometry only, same as Stage 2 — don't spend real time
polishing here yet, see [artist-todo.md](artist-todo.md) for when final
house art is safe to build.

## 2. Round Manager

- Create an empty GameObject named `RoundManager`, add component
  **Round Manager** (`RobEveryone.Round`).
- Drag `Player` (for its `PlayerInventory` component) into the
  `Player Inventory` field.
- Set `Quota` (default 200) and `Round Duration` (default 180 = 3 minutes)
  to whatever feels right for testing — lower them for now so you don't have
  to wait 3 minutes per playtest.

## 3. Exit

- Create a GameObject named `Exit`, place it away from the houses.
- Add a `Box Collider`, check **Is Trigger**, size it to be walkable-into
  (roughly doorway-sized or larger).
- Add component **Exit Point** (`RobEveryone.Round`), drag `RoundManager`
  into its `Round Manager` field.
- Optional: add a plane or cube under it so it's visually findable while
  there's no real art yet.

## 4. Round UI

- In the existing Canvas (from Stage 2), add two more **TextMeshPro - Text**
  elements: one for the quota, one for the timer. Position them near the
  existing money text.
- Add a third TMP text element for the end-of-round result banner —
  something large and centered works well. Leave its text as a placeholder;
  it starts hidden automatically.
- On the existing `UIManager` GameObject (or a new one), add component
  **Round UI** (`RobEveryone.UI`).
- Drag `RoundManager` into `Round Manager`, and the three new TMP text
  objects into `Quota Text`, `Timer Text`, and `Result Text`.

## 5. Test

- Press Play. Quota and a counting-down timer should both be visible
  immediately.
- Walk between houses, pick up items with **E** as in Stage 2, watch the
  money total climb.
- Walk into the `Exit` trigger. The round should end immediately — the
  result banner appears saying whether you hit quota.
- Alternatively, let the timer run out without reaching the exit and confirm
  the same banner appears with the correct met/not-met result.

If the result banner never appears: check the Console for a null reference
(most likely a missing drag-and-drop reference above), and confirm the
`Exit` GameObject's collider has **Is Trigger** checked — a non-trigger
collider will block the player instead of firing the trigger event.

Once this works, tell me and we'll move to **Stage 3b**: a homeowner that
notices you and calls the police.

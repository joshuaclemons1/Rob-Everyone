# Stage 2 — Editor setup

Scripts are already in `Assets/Scripts/`. This is what to wire up by hand in
the Unity Editor. Do it in `SampleScene` for now — a dedicated scene can wait.

## 1. Blockout room

- Add a `Plane` for the floor and a few scaled `Cube`s as walls, forming one
  small room. Placeholder geometry only — don't spend time here.

## 2. Player

- Create an empty GameObject named `Player`, position at the room's entrance.
- Add component **Character Controller** (leave defaults).
- Add component **First Person Controller** (`RobEveryone.Player`).
- Add component **Interactor** (`RobEveryone.Interaction`).
- Add component **Player Inventory** (`RobEveryone.Inventory`).
- Create a child GameObject named `PlayerCamera`, position it at roughly
  head height (Y ≈ 1.6), add a **Camera** component to it, remove/disable
  the scene's default `Main Camera` so there's only one active camera.
- On `Player`'s **First Person Controller**, drag `PlayerCamera` into the
  `Camera Transform` field.
- On `Player`'s **Interactor**, drag `PlayerCamera` into the `View Point`
  field.

## 3. Item

- Create a `Cube`, scale it down to something item-sized, name it
  `Item_Watch`, place it somewhere in the room.
- It needs a `Collider` (a Cube already has a `Box Collider` by default —
  leave it as a non-trigger).
- Add component **Pickup Item** (`RobEveryone.Items`), set `Item Name` and
  `Value` in the Inspector.

## 4. Money UI

- Create a UI **Canvas** (right-click Hierarchy → UI → Canvas). Unity will
  add an EventSystem automatically if one doesn't exist.
- Inside it, add a **TextMeshPro - Text** element (accept the TMP Essentials
  import prompt if it appears), position it top-left, set starting text to
  `$0`.
- Add an empty GameObject (can live under the Canvas or anywhere) named
  `UIManager`, add component **Inventory UI** (`RobEveryone.UI`).
- On `Inventory UI`, drag `Player` (for its `PlayerInventory` component)
  into `Inventory`, and drag the TMP text object into `Money Text`.

## 5. Test

- Press Play. Mouse should be captured for looking around, WASD to move.
- Look at `Item_Watch` until the raycast (invisible, no crosshair yet) hits
  it, press **E**. The cube should disappear and the UI should update from
  `$0` to `$25` (or whatever value you set).

If E doesn't do anything: check the Console for a null reference (usually a
missing drag-and-drop reference above), and confirm `Item_Watch` is within
`Interact Range` (default 3m) of `PlayerCamera` and roughly centered in view.

Once this works, tell me and we'll move to Stage 3: multiple houses, a
quota, a homeowner that notices you, and a police officer that chases and
jails you.

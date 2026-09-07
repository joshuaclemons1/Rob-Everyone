# Stage 7 — Shop/Lobby Editor setup

Scripts are already written (`GameFlowManager`, `PlayerSpawnPoint`,
`PartyGate`, `SellStation`, `ReadySpot`, `ReadyCountdownUI`, plus changes
to `RoundManager`, `PlayerInventory`, `ExitPoint`, `RoundUI`,
`InventoryUI`). This is what to wire up by hand in the Unity Editor.

**What this closes**: right now, when a round ends (exit reached / timer
runs out / caught by police), the game just shows a result banner and
stops — there's no way to play again. After this setup, ending a round
loads a separate Lobby scene where you can sell loot for Cash and walk
onto a Ready Spot to start a fresh round back in the gameplay map.

The Lobby is a genuinely separate **scene**, not a room in the gameplay
map — it'll later double as the pre-game multiplayer lobby, and is meant
to be a fairly large open area with a couple of buildings once real art
goes in. Right now we're just blocking it out.

## 1. Create the Lobby scene

- **File → New Scene**. If Unity shows a template picker, choose the same
  template `SampleScene` uses (URP-based — look for **Basic (URP)** or
  similar; if unsure, pick whatever has a Directional Light and skybox
  already in it).
- **File → Save As**, save it as `Assets/Scenes/Lobby.unity`.

## 2. Add both scenes to Build Settings

`SceneManager.LoadScene("...")` (what `GameFlowManager` uses to switch
scenes) only works for scenes registered here — this step is easy to
forget and causes a silent failure/console error if skipped.

- **File → Build Settings** (or **File → Build Profiles** depending on
  your Unity version, then **Scene List**).
- Drag `Assets/Scenes/SampleScene.unity` into the list if it isn't
  already there.
- Open `Assets/Scenes/Lobby.unity`, then click **Add Open Scenes** (or
  drag `Lobby.unity` in from the Project window directly).
- Confirm both scenes are checked/enabled in the list.

## 3. Blockout the Lobby room

Still in `Lobby.unity`:

- Add a `Plane` for the floor (scale it up, e.g. `(5, 1, 5)`, so there's
  real room to walk around) and a few scaled `Cube`s as boundary walls —
  same placeholder-only approach as every other blockout in this project.
  Don't spend real time on art here; this just needs to exist so you can
  walk around and test.
- Add a **Directional Light** if the scene template didn't include one
  (Hierarchy → right-click → Light → Directional Light) so the scene
  isn't pitch black.

## 4. Player Spawn Points (one per scene)

`GameFlowManager` needs to know where to put the player each time a
scene loads.

**In `Lobby.unity`:**
- Create an empty GameObject named `PlayerSpawnPoint`, position it just
  inside the blockout room (e.g. near one edge, facing into the room).
- Add component **Player Spawn Point** (`RobEveryone.Core`). No fields to
  set — it's just a locatable marker.

**In `SampleScene.unity`:**
- Check whether an empty GameObject already marks the player's original
  round-start position. If not, create one named `PlayerSpawnPoint` at
  wherever `Player` currently starts when you press Play.
- Add component **Player Spawn Point** (`RobEveryone.Core`) to it too.

## 5. Ready Spot (Lobby only)

- Create an empty GameObject named `ReadySpot`, positioned somewhere
  open and walkable in the Lobby.
- Add a **Box Collider**, check **Is Trigger**, and size it to a
  reasonable pad footprint (e.g. `2 x 2` on the ground plane) — this is
  what the player needs to stand inside.
- Add component **Ready Spot** (`RobEveryone.Shop`). Leave **Ready
  Delay** at `5`.
- (You'll wire its `Shop Manager`-equivalent connection automatically —
  `ReadySpot` doesn't need a direct reference to anything else; it just
  fires its own events, which `GameFlowManager` finds and subscribes to
  automatically when the Lobby scene loads. Nothing to drag here.)

## 6. Sell Station (Lobby only)

- Create a `Cube` (or a placeholder humanoid model if you have one handy
  — this represents the pawnshop owner NPC), name it `SellStation`,
  place it somewhere reachable in the Lobby.
- It needs a `Collider` (a Cube already has a **Box Collider** by
  default) — leave **Is Trigger unchecked**, same as `Item_Watch`'s
  pickup collider, so it's also solid, walkable-up-to geometry.
- Add component **Sell Station** (`RobEveryone.Shop`). No fields to set.
- Confirm the Player's **Interactor** component's **Interactable Mask**
  includes whatever layer this object is on (same check you'd already
  have done for `Item_Watch`/other loot pickups — if `Interactable Mask`
  is set to "Everything," there's nothing to check).

## 7. Shared Cash HUD (bar + text, one prefab in both scenes)

Rather than building separate Cash UI for the Lobby, this turns your
existing SampleScene Cash bar/text into a single prefab placed in both
scenes. Two new components make this possible: **Cash Bar UI**
(`RobEveryone.UI`) fills toward overall quota progress — banked Cash
*plus* whatever's currently carried, added together — unlike
`EconomyBarsUI`'s existing `cashBar`, which only tracks carried loot and
always reads `$0` outside of an active round, ignoring Cash entirely.
So starting a round with Cash already saved shows the bar partly filled
before you've picked anything up, and picking up loot fills it further
from there. Both it and `Inventory UI` auto-find the Player at runtime,
so the same prefab instance works correctly in either scene without
per-scene rewiring.

**In `SampleScene`:**

1. Find the existing Cash bar GameObject — click on `Economy Bars UI`'s
   GameObject in the Hierarchy, look at its **Cash Bar** field in the
   Inspector, then click that field once (not the circle icon) to
   highlight/ping the referenced object in the Hierarchy panel so you
   know exactly which GameObject it is.
2. **Clear that field properly, don't just drag something else over
   it**: on `Economy Bars UI`, click the small circle icon (⊙) at the
   right edge of the **Cash Bar** field — this opens an Object Picker
   window (a separate popup, not inline editing). In that popup, near
   the top of the list, click **None**. Confirm the field now reads
   `None (Level Bar UI)` before moving on — if it still shows an object
   name, this step didn't take and the next step will conflict with it.
3. Select the Cash bar GameObject itself (found in step 1). Add
   component **Cash Bar UI** (`RobEveryone.UI`). Drag that *same*
   GameObject's own **Level Bar UI** component into the new **Bar**
   field (you can drag the GameObject itself from the Hierarchy onto the
   field — Unity will find the right component on it automatically).
4. Find the existing Cash text element — same ping trick: click
   `Inventory UI`'s **Cash Text** field to highlight it in the Hierarchy.
5. **In the Hierarchy panel, right-click directly on the `Canvas`
   GameObject** (not on empty space below it) and choose **Create Empty**
   from the context menu. This guarantees the new object is created
   *already parented inside* Canvas, which is the part that's easy to
   get wrong by creating it elsewhere and dragging it in after. Rename
   it `CashHUD`.
   - **Verify it worked**: `Canvas` should now show an expand arrow (▶)
     in the Hierarchy with `CashHUD` listed indented underneath it. If
     `CashHUD` instead appears as its own top-level entry alongside
     `Canvas` (not indented under it), it is **not** parented correctly
     and won't render — go back and drag it onto `Canvas` until you see
     it nest properly before continuing.
6. Drag the Cash bar GameObject (step 1) and the Cash text GameObject
   (step 4) from wherever they currently are onto `CashHUD` in the
   Hierarchy, so both become children of it. Leave `Money Text`'s own
   object where it is — that one's staying SampleScene-only, since
   carried loot doesn't mean anything in the Lobby.
   - **Reparenting a UI element can visually move it** (Unity
     recalculates its anchored position relative to the *new* parent's
     rect, which can look very different from the old one). After
     dragging each one, immediately check the Game view — if either
     element jumped somewhere unexpected or vanished, select it, open
     its **Rect Transform** component's **⋮**/gear menu, and click
     **Reset**, then reposition it with the **Rect Tool** (press **T**)
     while watching the Game view.
7. Select `CashHUD` itself. Add an **Inventory UI** (`RobEveryone.UI`)
   component to it. Drag the child Cash text GameObject into **Cash
   Text**. Leave **Inventory** and **Money Text** empty (auto-found /
   not needed here).
8. **Verify before making the prefab**: select `CashHUD`'s **Rect
   Transform** — its `Anchored Position` and `Scale` should be
   reasonable, human-looking numbers (roughly hundreds at most, scale at
   or near `1`), not anything in the thousands or a scale like `0.36`.
   If they look strange, click **Reset** on it now and reposition with
   the Rect Tool before continuing. Also confirm the Cash bar and number
   are actually visible in the Game view right now, before turning
   anything into a prefab — it's much easier to fix a visibility problem
   while everything's still a normal scene object than after it's a
   prefab in two scenes.
9. Drag `CashHUD` from the Hierarchy into your Project window (e.g. into
   `Assets/Prefabs/UI/`) to turn it into a prefab.

**In `Lobby.unity`:**
- Create a UI **Canvas** (right-click Hierarchy → UI → Canvas; Unity
  adds an EventSystem automatically if one doesn't exist).
- Drag the `CashHUD` prefab from the Project window into this Canvas.
  Leave every field on it exactly as the prefab already has them —
  nothing needs rewiring per scene.
- Add a **TextMeshPro - Text** element for the ready-up countdown,
  position it bottom-center (or wherever reads clearly), starting text
  empty/blank. Name it `CountdownText`.
- Add an empty GameObject named `LobbyUIManager`, add component **Ready
  Countdown UI** (`RobEveryone.UI`). Drag the `ReadySpot` GameObject into
  `Ready Spot`, and `CountdownText` into `Countdown Text`.

Note: `Cash Bar UI` fills toward `GameFlowManager.LastQuota` (the most
recently-seen round's quota) since there's no real batch/tier target yet
per `gameplay-design.md` — it won't move at all (not even from picking
up loot) until you've completed at least one full round, since that's
the first time `LastQuota` gets set to anything above `0`. Once it has,
the bar reflects Cash *and* whatever you're currently carrying added
together, so a round started with Cash already saved shows it partly
filled before you pick anything up. Cash+carried exceeding quota is
expected to just show a full bar (it clamps, doesn't overflow/break).

## 8. Game Flow Manager (SampleScene only)

This is the persistent object that actually drives the scene switch —
only place it in `SampleScene`, **not** in the Lobby too (it carries
itself forward automatically once created).

- In `SampleScene.unity`, create an empty GameObject in the scene root
  named `GameFlowManager`.
- Add component **Game Flow Manager** (`RobEveryone.Core`).
- Confirm **Gameplay Scene Name** reads `SampleScene` and **Lobby Scene
  Name** reads `Lobby` (these are the defaults — only change them if your
  scene files are actually named differently).

## 9. Party Gate + Exit Point wiring (SampleScene only)

- Find the existing `ExitPoint` GameObject in `SampleScene` (the trigger
  at the map's extraction point).
- Add component **Party Gate** (`RobEveryone.Round`) to that same
  GameObject (or a new nearby one — either works, it just needs to exist
  somewhere in the scene).
- On `ExitPoint`'s **Exit Point** component, drag the GameObject holding
  **Party Gate** into the new **Party Gate** field.

## 10. Test the full loop

- Press Play from `SampleScene`.
- Finish a round any of the three ways:
  - Walk to the exit — confirm the round ends and the Lobby scene loads
    (with just one player, this should happen the instant you reach it).
  - Let the round timer run out.
  - Get caught by a police officer.
- In the Lobby, confirm:
  - You appear at `PlayerSpawnPoint`, not falling through the floor or
    stuck outside the blockout.
  - If you were **Caught**, your carried-loot value should already read
    `$0` (lost immediately) — check the existing money/quota HUD text if
    you kept it visible, or just trust the reset happened.
  - Walk up to `SellStation`, look at it, press **E** — your Cash total
    (`CashText`) should increase by whatever you were carrying.
  - Walk onto `ReadySpot` — `CountdownText` should appear and count down
    from 5. Step off before it reaches 0 — it should disappear
    immediately (canceled). Step back on and let it finish.
- Confirm `SampleScene` reloads: fresh timer, fresh quota progress, player
  back at `PlayerSpawnPoint`, and any loot you didn't sell before
  readying up is gone (expected — that's the deliberate "sell before you
  leave" pressure, not a bug).

If something doesn't fire: check the Console for a null reference first
(almost always a missing drag-and-drop reference above), and confirm
`SellStation`'s layer is covered by the Player's `Interactable Mask`,
and that both scenes are actually checked in the Build Settings scene
list (a `SceneManager.LoadScene` for an unregistered scene throws
`Scene ... couldn't be loaded` in the Console).

Once this works end-to-end, tell me and we can talk about what's next —
either polishing this loop further, or moving on to Stage 4
(multiplayer).

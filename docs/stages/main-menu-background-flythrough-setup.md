# Main Menu background: drone-shot flythrough (issue #51)

Replaces the old static single-house diorama (`Exterior`/`Roads`/`Fence`/
`Grass` in `MainMenu.unity`) with a small, non-networked stand-in
neighborhood the background camera slowly orbits over. Written entirely
in code — a procedural ring of real house prefabs, a parametric camera
orbit, a few wandering police stand-ins — rather than hand-placed scene
content, for the same reason `MenuCharacterPreview` builds its own stage
procedurally: it's exact, reasoned-about math instead of blind-placed
Transforms nobody's actually seen render. This doc is the remaining
checklist for the pieces that genuinely need real Editor eyes.

## What changed, and why (read this before touching the Editor)

Two real approaches were on the table (see the issue itself for the full
writeup). Went with **option 2**: a lightweight, non-networked stand-in,
not the real `SampleScene` loaded additively. The real `PoliceAI` is a
`NetworkBehaviour` that leans on a live `RoundManager`/server context
MainMenu doesn't have before a player clicks Play, and MainMenu already
runs its own `NetworkManager` for lobby purposes — loading the whole
gameplay scene on top of that risked a collision for a shot that's
ultimately just cosmetic background.

Four new scripts, all in `Assets/Scripts/UI/`:

- **`MenuBackgroundBuilder`** — the orchestrator, on a new `MenuBackground`
  root object. At `Awake`, it disables the old diorama's root objects
  (`Roads` and the `Exterior`/`Fence`/`Grass` bundle — **disabled, not
  deleted**, so this stays reversible; safe to delete for real once
  you've confirmed the new background works), builds a flat ground
  plane, rings 8 `House_01` instances around it (140-unit radius — at 8
  houses that's ~110 units between centers, clear of a house's own
  ~40x40 footprint), and spawns 3 police stand-ins.
- **`MenuPoliceWander`** — added to each police stand-in at runtime; a
  simple non-networked "pick a random point nearby, walk to it, repeat"
  loop, not the real `PoliceAI` state machine.
- **`MenuBackgroundCamera`** — on the existing Main Camera (previously
  completely static). A parametric circular orbit, not a hand-authored
  waypoint spline (no Cinemachine package in this project) — loops with
  zero seam by construction, with a slight look-ahead offset and a slow
  vertical bob so it doesn't read as a camera locked to a rail.
- **`MenuBackgroundDimOverlay`** — on the Canvas. A single semi-
  transparent full-screen Image, forced to the back of the sibling order
  at runtime, so the button stack/title/character preview stay legible
  over a moving background per the issue's own note.

**Only `House_01` is used for the ring**, not `Real_House_01/02/03` —
deliberately. `House_01` has zero scripts on it (pure visual shell,
confirmed by reading the prefab directly), safe to `Instantiate` outside
a networked context with no risk. The `Real_House_*` variants each carry
a live `NetworkIdentity` plus other `NetworkBehaviour` scripts (loot
spawn points, etc.) — probably harmless to instantiate un-networked
(most Mirror component logic gates behind `isServer`/`isClient`, which
default false outside `NetworkServer.Spawn`), but that's an assumption,
not something confirmed running. Same reasoning for `Police.prefab`'s
own networked components (`PoliceAI`, `NetworkIdentity`,
`NetworkTransformReliable`, `NavMeshAgent`) — `MenuBackgroundBuilder`
strips all four off each spawned stand-in at runtime before adding
`MenuPoliceWander`.

## Editor steps still needed

1. **Press Play and just look at it.** This is the big one — ring
   radius, camera orbit radius/height/speed, police wander area, and the
   dim overlay's opacity (`MenuBackgroundDimOverlay.dimAlpha`, starts at
   0.45) are all starting values, not tuned ones. Every one of them is a
   plain `[SerializeField]` number on the relevant component
   (`MenuBackgroundBuilder` on `MenuBackground`, `MenuBackgroundCamera`
   on `Main Camera`, `MenuBackgroundDimOverlay` on `Canvas`) — safe to
   just drag values around in Play mode and see what reads best before
   committing to numbers.
2. **Check the Console for errors/warnings on Play**, specifically
   around the police stand-ins (`NavMeshAgent` may log a one-time
   "not close enough to the NavMesh" warning before
   `MenuBackgroundBuilder` strips it off the following frame — expected,
   harmless, not a bug) and confirm nothing else complains about running
   outside a network context.
3. **Confirm menu legibility** over the moving background — buttons,
   title, and the character preview panel should all still read clearly
   with the dim overlay in place per `main-menu-visual-design.md`'s
   composition regions.
4. **Optional variety**: once you've confirmed `Real_House_01/02/03`
   instantiate cleanly with no console errors outside a network context,
   they can be added to `MenuBackgroundBuilder`'s `House Prefabs` list
   alongside `House_01` for a more varied-looking ring — not done by
   default here since that's exactly the kind of thing that needs a real
   Play-mode check, not an assumption.
5. **Cleanup**: once the new background is confirmed working, the old
   diorama objects (`Roads`, and the `Exterior`/`Fence`/`Grass` bundle,
   both referenced in `MenuBackgroundBuilder`'s `Legacy Diorama Roots`
   list) are safe to delete from the scene for real.

## Where to look

- `Assets/Scripts/UI/MenuBackgroundBuilder.cs` — the orchestrator.
- `Assets/Scripts/UI/MenuPoliceWander.cs` — non-networked patrol stand-in.
- `Assets/Scripts/UI/MenuBackgroundCamera.cs` — the orbit flythrough.
- `Assets/Scripts/UI/MenuBackgroundDimOverlay.cs` — legibility overlay.
- `Assets/Scripts/AI/PoliceAI.cs` — the real networked state machine this
  deliberately doesn't run in the Main Menu.
- `Assets/Scripts/World/HousePoolSpawner.cs` — the real gameplay house
  spawner, for reference on house-plot sizing (this doesn't reuse its
  networked spawn path, just its `housePlotSize` reasoning).

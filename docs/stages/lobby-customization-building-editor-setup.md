# In-Lobby customization building — Editor setup (Phases 3 & 4, issue #52)

Phases 0–2 (skin unlocks, the live-swap capability, and the
`PaintCan`/`SkinOfferPedestal`/`SkinOfferManager`/`MirrorSkinCycleButton`
interaction scripts) are done and committed — see
[lobby-customization-building-plan.md](lobby-customization-building-plan.md)
for what each of those actually does. Nothing from this doc has any
effect until it's placed and wired in `Lobby.unity`, which is real
Editor-hands work — this is that walkthrough, split into stages with a
rest point after each one so a problem gets caught close to whatever
caused it instead of at the very end.

## Before you start

- **Reference points already in `Lobby.unity`**: the Pawn Shop
  (`PawnShop`) sits at world `(0, 0, -30)`; the player spawn points
  cluster around `(-5..5, 1, 5..10)`. Use these to judge where a new
  room reads as "part of the Lobby" rather than off in empty space —
  not a hard requirement, just orientation.
- **`Interactor`'s raycast mask** (`interactableMask` on the Player
  prefab) defaults to "everything" in code, and nothing in the current
  Player prefab overrides it — so a `Collider` is all any of these
  objects need to be detected; there's no dedicated layer to remember
  to assign unless the team already narrowed that mask at some point
  (worth a quick check of `Player.prefab`'s `Interactor` component
  before assuming).
- **Assets to reuse**: `Assets/PlayerColorPalette.asset` (8 colors) and
  whatever `PlayerSkinRoster` asset `PlayerSkinSpawner`/`CustomizationUI`
  already reference — both need wiring onto the new scripts below, not
  new assets.

## Stage 1 — block out the room

Build (or reuse Pawn Shop kit pieces for) a room somewhere in
`Lobby.unity`. Needs space for: 5–6 pedestals with walking room around
each, 8 paint cans, and a wall/area for the mirror with room to stand
in front of it. No code depends on the room's shape or size — purely a
level-design call.

## Stage 2 — the skin pedestals

For each of 5–6 pedestal spots:

1. Add a `GameObject` with a `Collider` (the pedestal's physical base
   — whatever size reads right) and a `SkinOfferPedestal` component.
2. Wire `Skin Roster` to the same `PlayerSkinRoster` asset
   `PlayerSkinSpawner` uses.
3. (Optional) Add an empty child Transform positioned where the
   preview model's feet should land, wire it into `Preview Anchor` —
   leave unset and it defaults to the pedestal's own Transform, which
   is probably fine for a first pass.
4. This `SkinOfferPedestal` needs a `NetworkIdentity` (Mirror should
   add one automatically the first time you add a `NetworkBehaviour`
   component in the Editor; if it doesn't, add one by hand) — and, per
   `stage4-multiplayer-mirror.md`'s own Part 5, needs to be registered
   correctly as a **scene object**, not a spawnable prefab (it's
   hand-placed in `Lobby.unity`, never runtime-instantiated) — this
   should just work automatically for a scene-placed object, nothing
   extra to configure.

Then, add one more `GameObject` somewhere in the room (doesn't need to
be visible/physical) with a `SkinOfferManager` component:

5. Wire its own `Skin Roster` (same asset again).
6. Drag all 5–6 `SkinOfferPedestal` objects into its `Pedestals` list,
   in whatever order you like — the order doesn't matter functionally,
   `SkinOfferManager` shuffles before assigning.
7. This needs a `NetworkIdentity` too, same as the pedestals.

### 🔴 Rest point 1 — pedestals alone

Press Play (two clients if you can). Confirm:

- Every pedestal shows a different skin standing on it (no duplicates
  within one visit), and both clients see the *same* offers.
- The prompt reads `Unlock <skin name>` when you look at one.
- Pressing E on a pedestal doesn't visibly do anything to your own
  character yet — that's correct, unlocking isn't equipping (see
  Stage 4). Check the Console isn't logging anything unexpected on
  interact.
- Leave the Lobby (finish a round) and come back — the pedestals
  should show a **different** set of skins than last time.
- **This is also the point to first check the four `OnSkinRebuilt`
  dependents didn't regress anything** — Phase 1's rework touched
  `PlayerAnimationDriver`, `PlayerRagdoll`, `PlayerHeadTalkScale`,
  `HeldItemDisplay` — even though nothing has triggered a live swap
  yet at this rest point, confirm normal Lobby behavior (animations,
  held hotbar items, ragdoll if you trigger one) still looks exactly
  as it did before this issue.

## Stage 3 — the paint cans

For each of the palette's 8 colors:

1. Add a `GameObject` with a `Collider` (a can/small prop model, or a
   placeholder primitive for now) and a `PaintCan` component.
2. Set `Color Index` to 0–7, matching that entry's position in
   `PlayerColorPalette.asset`.
3. Worth tinting the can's own material to the matching color (open
   `PlayerColorPalette.asset` in the Inspector to read the exact RGB
   values) so a player can tell them apart at a glance without reading
   every prompt — cosmetic, not required for the interaction to work.

No `NetworkIdentity`/networking needed here — `PaintCan` is a plain
`MonoBehaviour`, not a `NetworkBehaviour` (it doesn't hold any state of
its own to sync; `colorIndex` is a fixed, identical value on every
client already, baked into the scene).

### 🔴 Rest point 2 — paint cans

Press Play. Confirm each can's prompt reads "Change color," pressing E
actually changes your body color immediately (unlike pedestals, this
one applies live), and every other connected client sees the color
change too.

## Stage 4 — the mirror area's cycle buttons

Pick a wall for the mirror (the actual reflective surface is Stage 5 —
this stage just needs the two interactables that pick *which* unlocked
skin to wear, which work independently of whether the mirror itself
renders anything yet).

1. Add two small `GameObject`s flanking where the mirror will go (e.g.
   physical arrow props, or simple placeholder shapes for now), each
   with a `Collider` and a `MirrorSkinCycleButton` component.
2. Leave `Forward` checked (`true`) on one, uncheck it on the other —
   these are your Next/Previous.

No networking needed here either (plain `MonoBehaviour`, no state of
its own).

### 🔴 Rest point 3 — cycling, the real test of Phases 0–2 together

This is the one that actually exercises the full loop end to end:

1. Unlock 2–3 skins from different pedestals (across a couple of Lobby
   visits, so you're not just testing with everything unlocked from a
   fresh save).
2. Stand at the cycle buttons and press E on Next — your character
   should visibly change to one of your unlocked skins, **live**, with
   your legs still working, hotbar item still visible in your hand,
   animations still playing correctly (this is Phase 1's live-swap
   path actually firing for the first time).
3. Keep pressing Next — it should cycle through every skin you've
   unlocked and wrap back to the first one, never landing on something
   you *haven't* unlocked.
4. Press Previous — same cycle, reverse direction.
5. **Leave the Lobby and come back (or fully quit and relaunch)** —
   confirm your selected skin is still the one you last picked (this
   is `PlayerCosmeticSelection` persistence, already existing
   infrastructure, but worth confirming the new swap path still writes
   to it correctly).
6. Two clients: confirm the *other* player sees your skin change too,
   live, not just on their next scene load.
7. Watch the Console for anything unexpected during a swap — this is
   the path most likely to surface a problem with one of the four
   `OnSkinRebuilt` dependents if something was missed.

If cycling works correctly here, Phases 0–2 are fully validated end to
end and Phase 5 (removing the old Main Menu Customization screen)
becomes safe to do whenever you're ready — no need to wait for the
mirror itself.

## Stage 5 — the mirror's actual reflection

`MirrorReflectionCamera.cs` (already written, Phase 4's actual driver
script) does the per-frame reflection math — this stage is entirely
about giving it something to render onto and through.

1. **Create the mirror surface**: a `Quad` (or a flat plane from
   whatever art kit) sized to how big you want the mirror to read,
   placed on the wall. This is `mirrorPlane`'s eventual home — its
   forward direction needs to point *out* into the room, the direction
   a player standing in front of it would be facing while looking at
   it.
2. **Create a `RenderTexture`** asset (`Assets > Create > Render
   Texture`) — 1024×1024 is a reasonable starting size, tune later if
   it looks too soft/too expensive.
3. **Create a Material** using that `RenderTexture` as its main
   texture (an Unlit shader is fine — the reflection camera is already
   rendering a fully lit scene into the texture, no need to light the
   quad displaying it again on top). Assign this material to the Quad
   from step 1.
4. **Add the reflection camera**: a new `Camera` GameObject anywhere
   in the scene (its starting Transform doesn't matter — the script
   overwrites its position/rotation every frame it's active). Set its
   `Target Texture` to the `RenderTexture` from step 2. Consider
   giving it its own culling mask if you don't want it rendering UI
   layers/other cameras' preview stages (`MenuPreview`, `ItemPreview`)
   into the mirror — "everything except those two" is a reasonable
   starting mask.
5. **Wire `MirrorReflectionCamera`**: add the component to any
   GameObject (the mirror Quad itself is a natural home), set
   `Reflection Camera` to the camera from step 4, and `Mirror Plane` to
   the Quad's own Transform from step 1.
6. Tune `Max Active Distance`/`Max Active Angle` once you can see it
   working — the defaults (6 units, 100°) are a starting guess, not
   measured against this specific room's actual scale.

### 🔴 Rest point 4 — the mirror itself

This is the piece with real, unverified-until-now correctness risk —
watch closely, not just "does it look roughly right":

1. Stand in front of the mirror. Confirm you see your own reflection,
   correctly mirrored (left/right, not doubled or facing the wrong
   way), tracking your movement smoothly as you walk around/turn.
2. Walk away past `Max Active Distance` (or well off to the side, past
   `Max Active Angle`) — the reflection camera should stop rendering
   (check its own `enabled` state in the Inspector, or just notice
   performance) rather than running constantly regardless of whether
   anyone's actually looking.
3. **The known likely artifact**: geometry *behind* the mirror surface
   showing up reflected into view, since this first pass deliberately
   skips the oblique near-clip-plane technique (see
   `MirrorReflectionCamera.ApplyReflection`'s own comment) that
   normally prevents that. If it's visually distracting, that's the
   next thing to add — not a sign anything here is broken, just the
   one corner deliberately cut to avoid guessing at camera-projection
   math with no way to check it.
4. Two clients, both near the mirror: confirm each sees *their own*
   reflection (not the other player's) — this should already be
   correct by construction (`MirrorReflectionCamera` reads
   `NetworkClient.localPlayer` specifically, and every client runs its
   own independent, non-networked copy of this script), but worth
   confirming since it's exactly the kind of thing that's easy to get
   subtly wrong and hard to reason about without seeing two clients
   side by side.
5. Test cycling skins (Stage 4's buttons) while actually looking in
   the mirror — this is the payoff shot the whole feature was framed
   around ("a real mirror... so you can preview the skins").

## After all four rest points pass

Phase 5 from the plan doc: delete `CustomizationUI.cs` and its Main
Menu scene wiring (the Customize panel, Next/Previous buttons, swatch
grid) — the building now covers everything that screen did.

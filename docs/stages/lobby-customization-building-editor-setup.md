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

**Still needs a `NetworkIdentity`**, even though `PaintCan` is a plain
`MonoBehaviour`, not a `NetworkBehaviour`, and holds no state of its own
to sync (`colorIndex` is fixed and identical on every client already).
Confirmed real bug from a live Editor session: `Interactor.FireInteract`
resolves `GetComponentInParent<NetworkIdentity>()` on whatever you're
looking at and silently returns (never sending the `Command` at all) if
that comes back null — it needs the identity purely as a network handle
to reference the target across the client→server call, regardless of
whether the target itself holds synced state. Add a `NetworkIdentity`
component to each can (no other component needed alongside it).

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

Same correction as Stage 3's paint cans: add a `NetworkIdentity` to
each of these two objects too, even though `MirrorSkinCycleButton` is a
plain `MonoBehaviour` with no state of its own — `Interactor` needs it
as a network handle regardless, and pressing E silently does nothing
without one (confirmed live, not theoretical).

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

## Stage 5 — the mirror's character preview

**Replaces the original live reflection-camera design.** A true planar
reflection (`MirrorReflectionCamera.cs`, now deleted) turned out to need
more real, open, correctly-shaped space behind the mirror's wall than
this room actually has, on top of several genuine bugs along the way
(a backward-facing normal, a sign error in the activation gate, the
camera capturing its own surface) — see git history on that file for the
full trail if useful context ever comes up again. Confirmed working, but
felt "janky" once live (motion lag, the fragility of needing real depth
behind every wall a mirror ever gets placed on).

Replaced with the same proven pattern `MenuCharacterPreview.cs` already
uses for the Main Menu: no live camera reflection at all, just a static
idle model of your own currently-selected skin, posed to look like it's
standing inside the frame. `MirrorCharacterPreview.cs` is the new driver
script — it reuses the *same* `RenderTexture`/Material/Quad chain Stage
5 originally set up (nothing there needs to change), just points a
repurposed camera at a small isolated stage instead of doing per-frame
reflection math.

1. **Add the `MirrorPreview` layer** (`Project Settings > Tags and
   Layers`) if it isn't there already — `MirrorCharacterPreview` sets
   the preview camera's culling mask to only this layer at runtime, so
   nothing else in the room ever renders into the mirror by accident.
2. **Create `modelSpawnPoint`**: an empty child GameObject under the
   mirror (or anywhere convenient), positioned at roughly floor height,
   a couple of units in front of the mirror's existing camera (reuse the
   same "MirrorCamera" object from the old setup — `MirrorCharacterPreview`
   already has it wired as `Preview Camera`). Rotate this object so its
   forward axis points back toward the camera — this is the one
   genuinely Editor-only step, same as ever with 3D placement: watch the
   Scene view gizmo, not a predicted number.
3. Wire `modelSpawnPoint` into `MirrorCharacterPreview`'s **Model Spawn
   Point** field. `Skin Roster`, `Palette`, and `Player Animator
   Controller` are already wired to the same assets every other
   customization script uses.

### 🔴 Rest point 4 — the mirror's preview

1. Look at the mirror. Confirm it shows an idle, standing pose of
   whatever skin/color you currently have selected — not a live
   reflection, just a static "mannequin," which is the whole point.
2. Cycle skins/colors (Stage 4's buttons) and confirm the mirror updates
   to match (`PlayerCosmeticSelection.OnChanged`-driven, same refresh
   path `MenuCharacterPreview` already uses on the Main Menu).
3. Two clients: confirm each sees *their own* selection in the mirror,
   not the other player's — this is automatic by construction (every
   client reads its own local `PlayerCosmeticSelection`), but worth a
   real look since it's the kind of thing that's easy to get subtly
   wrong.
4. Walk around/away from the mirror — the model should hold still (no
   camera-tracking motion, unlike the old reflection) since there's
   nothing live being computed anymore.

## After all four rest points pass

Phase 5 from the plan doc: delete `CustomizationUI.cs` and its Main
Menu scene wiring (the Customize panel, Next/Previous buttons, swatch
grid) — the building now covers everything that screen did.

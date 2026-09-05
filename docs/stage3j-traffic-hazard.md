# Stage 3j — Editor setup (traffic hazard: cars on the road)

Adds a second obstacle alongside the police: cars that drive one lap of
the road loop and can knock the player down. Confirmed design (your
answers):

- **Oblivious** — cars don't see or react to the player at all, they just
  drive their route. Avoiding them is entirely on the player.
- **Pure knockback + stun**, not a round-ending catch like police — a real
  per-bone ragdoll (built with Unity's Ragdoll Wizard, see the ragdoll
  section below) plays for a couple seconds, then the player stands back
  up wherever it landed.
- **1–2 cars at a time, sometimes zero** — a spawn manager rolls a chance
  on a timer rather than keeping cars patrolling constantly.
- Cars spawn and despawn at **one point you place** (near the player
  spawn/`Exit`, a bit further back), drive one lap, then return to that
  same point and disappear.
- **No pre-hit telegraph** (no brake lights/swerve) — but a horn + a
  driver "yelling" sound cue plays at the moment of impact.

New scripts (all written, nothing left to code): `CarDriver.cs`,
`CarSpawnManager.cs` (`Assets/Scripts/AI/` and `Assets/Scripts/World/`
respectively, matching where PoliceAI/HousePoolSpawner already live), and
`PlayerRagdoll.cs`, `PlayerSkinSpawner.cs`, `RagdollHips.cs`
(`Assets/Scripts/Player/`) — see Section 4 for what each does.

## 1. Turn a car FBX into a usable prefab

The `Kenney-CarKit/FBX/` files are raw models — same as every other Kenney
pack, they need the material fix, plus this time some actual components
added since a car needs to drive and detect the player.

Pick 2–3 for variety (e.g. `sedan.fbx`, `taxi.fbx`, `van.fbx`) and repeat
for each:

1. Same fix as always: select the `.fbx` → Materials tab → Extract
   Materials → Render Face: Both.
2. Drag it into an empty spot in the Scene.
3. Add a **Box Collider** (Add Component → Box Collider). Click **Edit
   Collider** (or just check **Is Trigger**) and resize it to roughly
   match the car's actual body — doesn't need to be pixel-perfect, just
   enough that the player has to actually be in the car's path to trigger
   it. Check **Is Trigger**.
4. Add an **Audio Source** component (required by `CarDriver`). Leave
   **Play On Awake** unchecked — the script triggers sounds itself.
5. Add the **Car Driver** component (Add Component → search "Car
   Driver"). Leave **Horn Clip**/**Yell Clip** empty for now — no SFX
   sourced yet for either (added to `art-info.md`'s SFX to-do list below).
6. Drag this configured instance from the Hierarchy into
   `Assets/Prefabs/` (make a `Cars/` subfolder) to make it a reusable
   prefab, then delete the instance from the Hierarchy.

## 2. Place the waypoint loop + origin point

1. Create an empty parent `CarWaypoints` in the Hierarchy.
2. Under it, place child empty Transforms tracing **one lap** of the
   actual road loop from Stage 3h, in the order a car should drive
   them — same idea as `HouseSlots`, these are just position markers, no
   components needed. Space them enough to smoothly follow corners
   (tighter spacing on curves, looser on straights).
3. Create one more empty GameObject, **not** part of `CarWaypoints`, near
   the player spawn/`Exit` area but set back a bit — name it
   `CarOriginPoint`. This is where cars spawn, and where they return to
   despawn.

## 3. Set up the spawn manager

1. Create an empty GameObject, name it `CarSpawnManager`.
2. Add the **Car Spawn Manager** component.
3. Drag your 2–3 car prefabs into **Car Prefabs**.
4. Drag every `CarWaypoints` child, in order, into **Lap Waypoints**.
5. Drag `CarOriginPoint` into **Origin Point**.
6. Leave the defaults for a first pass — **Max Concurrent Cars** (2),
   **Spawn Check Interval** (20s), **Spawn Chance** (0.5) — tune once
   you've seen it in action; these three together are what control "how
   often, and how much traffic at once."

## 4. Set up the player to receive impacts

**Revision history, short version:** v1 tumbled the whole invisible
capsule as one rigid object (camera attached to it, so it inherited a
sickening spin). v2 fixed the spin/direction but was still one rigid
lump, not an actual ragdoll. v3 built a real per-limb ragdoll on a
manually-dragged-in, always-hidden-except-during-the-hit model. **This is
v4**, built for multiplayer/character-selection compatibility per a
design requirement: the ragdoll must show *whichever skin the player
actually picked*, and (once Mirror exists, not built here) other players
need to be able to see it — which a hide-it-with-`SetActive` approach
can't do, since that hides it from *everyone*, not just its own camera.
No networking is added in this pass — Mirror still isn't installed, per
`plan.md`'s build order — this only makes sure the single-player version
doesn't need a rewrite when it is.

The exact visibility rule: **other players see your character all the
time, including while it's ragdolling — you only ever see your own model
during the ragdoll cutaway, never during normal first-person play.**
That's *not* a permanent Culling Mask exclusion, since the same camera is
reused for the third-person stun view — `PlayerRagdoll` flips the skin
layer back into that camera's Culling Mask only for the stun's duration,
then removes it again the instant control returns.

What changed structurally: the player model is no longer manually dragged
in — `PlayerSkinSpawner` instantiates whichever skin/color
`PlayerCosmeticSelection` (the existing main-menu system) has stored, at
gameplay start. It's also no longer hidden via `SetActive` — it stays
active and rendering permanently (so a future second camera/player could
see it), and *only your own camera* is told to ignore it, via a Layer +
Culling Mask that's toggled off/on around the stun as described above.
The ragdoll itself now toggles via kinematic on/off (standard practice
once a model can't just disappear), and finds its
"hips" bone through a small marker component (`RagdollHips`) instead of a
hardcoded Inspector reference, since the different selectable skins won't
all share identical bone names.

### 4a. Two Physics layers

1. **Edit → Project Settings → Tags and Layers.** Add `Player` (User
   Layer 8) and `Ragdoll` (User Layer 9) if you haven't already from an
   earlier pass at this.
2. Select your **Player** object, set its **Layer** to `Player` (choose
   **No** if asked about children — the camera doesn't need to move).
3. **Edit → Project Settings → Physics**, scroll to the **Layer Collision
   Matrix**, uncheck **Player × Ragdoll**. (The skin's own layer gets set
   automatically at runtime by `PlayerSkinSpawner` below — nothing to
   assign by hand per-instance.)
4. Select **PlayerCamera**, find its **Camera** component's **Culling
   Mask**, and uncheck `Ragdoll`. This is the actual "you can't see your
   own body, everyone else's camera can" mechanism.

### 4b. Build the ragdoll once, then batch-copy it to the rest

The 6 skins are currently raw `.fbx` assets, not prefabs — Unity's Ragdoll
Wizard can't add components directly to an FBX (it's a read-only "Model"
prefab; anything added would vanish on the next reimport). And since all
6 share the same rig, doing the Wizard's manual bone-assignment 6 times
over would just be repeating identical work — so there's a batch tool
(`Assets/Scripts/Editor/RagdollBatchTool.cs`) that builds the rest from
one done-by-hand template.

**Step 1 — wrap the template skin as a real prefab, then ragdoll it by
hand** (the *only* one you do manually):

1. Drag the FBX into an empty spot in a scene, then drag that instance
   from the Hierarchy back into a Project folder (e.g.
   `Assets/Prefabs/PlayerSkins/`) to save it as a proper `.prefab`. Delete
   the scene instance afterward.
2. Double-click that new prefab to open it in **Prefab Edit Mode**.
   Expand its bone hierarchy (Hips/Pelvis, Spine, Head, limbs).
3. Menu: **GameObject → 3D Object → Ragdoll...** Assign each slot (Pelvis,
   Left/Right Hips, Knee, Foot, Arm, Elbow, Middle Spine, Head) by
   dragging the matching bone Transform in. Leave **Total Mass**/
   **Strength** at defaults. Click **Create**.
4. Select every bone the wizard just added a **Rigidbody** to and check
   **Is Kinematic** on each — this has to be the prefab's baked default,
   otherwise the *menu preview* of this same prefab would immediately
   collapse under gravity too, since it's the same asset.
5. Select the **Pelvis/Hips** bone specifically and add **Ragdoll Hips**
   (`Assets/Scripts/Player/RagdollHips.cs`).
6. Exit Prefab Edit Mode and save. This one is now the **template**.

**Step 2 — batch-copy it onto the other 5:**

1. In the Project window, select the other 5 skin FBX files (they can
   stay as raw FBX — the tool wraps them into prefabs automatically),
   then **ctrl/cmd-click the template prefab last**, so it's the active
   (highlighted) selection.
2. Menu: **Assets → Rob Everyone → Copy Ragdoll To Selected Skins**.
3. Check the Console. For each of the 5, it logs either the new wrapped
   prefab path it created, or a warning naming exactly which bone it
   couldn't match — it never fails silently.
4. For any that got auto-wrapped into a new prefab, update
   `PlayerSkinRoster`'s list to point at that new prefab instead of the
   original FBX (the tool doesn't touch the roster itself).
5. **Actually test each one** — select it, check its bones got a
   Rigidbody/Collider/CharacterJoint and `Is Kinematic` is on, and try it
   in Play mode. This tool is new and untested against your actual
   models; don't assume all 5 came out perfect without checking.

### 4c. Wire up the two player components

1. Select your **Player** object and add **Player Skin Spawner**.
   - Drag your `PlayerSkinRoster` asset into **Skin Roster**.
   - Drag your `PlayerColorPalette` asset into **Palette**.
   - Set **Skin Layer** to `Ragdoll`.
2. Add **Player Ragdoll** (this replaces the old **Car Impact Receiver**
   — remove that component if it's still on the object from an earlier
   pass).
   - Drag the existing **PlayerCamera** into **Camera Transform**.
   - Leave **Stun Duration** (2s), **Third Person Offset** (`0, 2.5, -5`),
     and **Look At Height Offset** (`0.5`) at their defaults for a first
     test. There's no **Player Model Root**/**Hips Rigidbody** field to
     wire anymore — both are found automatically at runtime (`Start()`,
     after `Player Skin Spawner`'s `Awake()` has already instantiated the
     skin).
3. Press Play. You should see whichever skin is currently selected
   (`PlayerCosmeticSelection.SkinIndex`, defaults to `0` if you've never
   opened the customization screen) standing where the Player is, in its
   authored color, and invisible in your own view (Culling Mask working)
   but visible from the Scene view/any other camera.

If the Console logs a warning about a missing `RagdollHips` marker,
that's `PlayerRagdoll` telling you which skin still needs step 4b's pass
run on it — expected for the 5 you haven't done yet, not a bug.

## 5. SFX to source later

Added to `art-info.md`'s SFX to-do list: a car horn one-shot, and a short
driver "yelling at the player" line/stinger. Both fields exist on
`CarDriver` already (**Horn Clip**/**Yell Clip**) — dropping clips in
later needs zero code changes.

## Test

Press Play and wait near the road (up to `spawnCheckInterval` seconds for
the first roll — don't assume it's broken if nothing happens
immediately, that's the RNG working as designed). Confirm: a car spawns
at `CarOriginPoint`, drives the full lap in order with no getting stuck
on corners, returns to the same point, and despawns. Confirm you never
see your own body during normal first-person play first (Culling Mask
excluding it correctly). Then deliberately stand in the car's path —
confirm the view cuts to a stable third-person angle showing your
selected skin actually going limp (limbs reacting independently via the
ragdoll joints, not the whole body spinning as one rigid lump — and
critically, that you *can* now see it, unlike a second ago), holds there
for the stun, then cuts back to first-person with control restored
wherever the ragdoll actually landed (not necessarily the exact impact
spot) and your own body invisible again — and that none of this touches
your quota, loot, or round state. Separately, open the customization
screen, pick a *different* skin, then come back and get hit again to
confirm the ragdoll now shows that skin instead (proves
`PlayerSkinSpawner` is actually reading the live selection, not something
cached). Also try getting hit near a step/slope, not just flat ground, to
confirm the landing raycast finds sensible footing rather than leaving
you floating or sunk into the floor. Finally, let a full play session run
a few spawn-check cycles to confirm it really does sometimes stay at zero
cars, not just "always spawns eventually."

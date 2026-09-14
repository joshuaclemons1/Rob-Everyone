# Stage 3i — Editor setup (skybox + background skyline)

Playtest confirmed the offline loop works on the real layout (Stage 3h) —
this is a small polish pass before Stage 4, making the map feel like it
sits inside a real place instead of floating in Unity's default gray/blue
void. Four pieces — do 1 and 2 in either order, but 3 (forest ring) and 4
(hills) make more sense once the skyline (2) already exists, since both
are placed relative to where the skyline ring already sits.

## 1. Skybox

Assets already in the repo: `Assets/Art/Environment/Kenney-Skyboxes/`
(`skybox-day.png`, `skybox-night.png`, `skybox-morning.png`, plus
`skybox-alien.png`/`skybox-space.png` you won't need). These are
**panoramic (equirectangular)** images, not the 6-image cross layout —
that changes which shader to use below.

1. In the Project window, right-click `Assets/Art/Environment/Kenney-Skyboxes/`
   → **Create → Material**. Name it `SM_Skybox_Day`.
2. Select it. In the Inspector, click the **Shader** dropdown at the top
   (currently "Universal Render Pipeline/Lit") and change it to
   **Skybox/Panoramic**.
3. The Inspector now shows different fields — find **Spherical (HDR)** and
   drag `skybox-day.png` onto it. Leave **Mapping** on its default
   (Latitude-Longitude Layout) — that's the correct one for this image
   type. Leave Tint white and Exposure at `1` for now.
4. Open **Window → Rendering → Lighting**, go to the **Environment** tab,
   and set **Skybox Material** to `SM_Skybox_Day`.
5. Look at the Scene/Game view — you should see the Kenney sky instead of
   Unity's default procedural one. If it looks flipped or seams are
   visible at the horizon, that's a Rotation tweak on the material
   (Inspector → Rotation slider), not a reimport issue.

**Night-mode readiness:** while you're in there, also create
`SM_Skybox_Night` the same way (steps 1–3, using `skybox-night.png`) and
`SM_Skybox_Morning` if you want the dusk transition too — don't wire them
to anything yet. Having them exist as separate Material assets now means
the actual time-of-day system (whenever it's built) is just:
```csharp
RenderSettings.skybox = nightSkyboxMaterial;
DynamicGI.UpdateEnvironment();
```
one line to swap, no rebuilding the skybox setup later. Matches the
draft day/dusk/night tints already sketched in `art-info.md`'s Lighting
section.

## 2. Background skyline

Goal: a ring of distant, non-walkable buildings around the outside of the
actual map, so the horizon isn't empty grass in every direction. Uses the
`low-detail-building-*` pieces from `Kenney-CityKitCommercial/FBX/` —
these exist specifically for this (lower detail than the real houses,
meant to be seen from a distance, not walked up to).

**Before placing them:** run each `low-detail-building-*` FBX you plan to
use through the same fix as every other Kenney piece if you haven't
already for these specific files — Materials tab → Extract Materials →
Render Face: Both. There are 16 variants total (`low-detail-building-a`
through `-n`, plus `-wide-a`/`-wide-b`) — using one of each around the
ring gives you a full lap with zero repeats.

1. Create an empty parent GameObject at the world origin, name it
   `Skyline`.
2. Place one `low-detail-building-*` instance at each position below, as
   a child of `Skyline` (position values are world-space — since
   `Skyline` sits at (0,0,0), these can just be entered directly). Y
   rotation barely matters here — nothing will get close enough to see
   the back of these — so feel free to eyeball-rotate for variety instead
   of using the values below exactly.

   | Piece | Position (X, Y, Z) |
   |---|---|
   | low-detail-building-a | 0, 0, 230 |
   | low-detail-building-b | 88, 0, 213 |
   | low-detail-building-c | 163, 0, 163 |
   | low-detail-building-d | 213, 0, 88 |
   | low-detail-building-e | 230, 0, 0 |
   | low-detail-building-f | 213, 0, -88 |
   | low-detail-building-g | 163, 0, -163 |
   | low-detail-building-h | 88, 0, -213 |
   | low-detail-building-i | 0, 0, -230 |
   | low-detail-building-j | -88, 0, -213 |
   | low-detail-building-k | -163, 0, -163 |
   | low-detail-building-l | -213, 0, -88 |
   | low-detail-building-m | -230, 0, 0 |
   | low-detail-building-n | -213, 0, 88 |
   | low-detail-building-wide-a | -163, 0, 163 |
   | low-detail-building-wide-b | -88, 0, 213 |

   This radius (230) sits well clear of the actual map — houses/compound/
   fence currently extend to roughly ±85 on X and ±140 on Z, so there's a
   comfortable buffer in every direction.
3. Optional variety pass: once all 16 are placed, randomly scale a few
   up/down slightly (e.g. 0.9–1.3 on all three axes) and nudge a couple
   off the exact ring radius — a perfectly even circle of identical-height
   buildings reads as artificial up close, even at this distance.
4. **No colliders needed** — these are purely visual, well outside where
   the player can currently walk. If you don't already have an invisible
   boundary wall stopping the player from wandering out past the house
   ring, that's a separate, worth-doing task now that there's something
   (the skyline) a player could theoretically try to walk toward — flag
   it to me if you want that added.

## 3. Forest ring (enclosure + world boundary)

The gap between the house ring (~85 on X, ~140 on Z) and the skyline ring
(radius 230) is currently empty grass, which is what makes the map feel
unenclosed even with the skyline in place. Fill it with a dense multi-row
treeline using the two tree pieces already in
`Kenney-CityKitSuburban/FBX/` (`tree-large.fbx`, `tree-small.fbx`) — no new
assets needed.

This uses a new script, `ForestRingSpawner.cs`
(`Assets/Scripts/World/`) — same shape as `HousePoolSpawner.cs` you
already know. It places trees on several evenly-spaced concentric
**rounded-rectangle** rings (with a little per-tree jitter so it doesn't
look like a robotic grid), not circles — the house ring is a rectangle
(wider along Z than X, since the S-row/Good-House axis reaches further
out than the E/W columns do), so a circular treeline would either leave
the long ends uncovered or waste a lot of tree budget on the short ends.
Trees are spaced evenly by actual distance around that rounded-rect's
perimeter (not by angle), so spacing stays even on the straight sides and
the curved corners alike.

That coverage is even, but tree *collision* still isn't perfectly solid —
trunk/canopy colliders don't tile with zero gaps, so a determined player
could still find a spot to squeeze through. Rather than fight that, the
script also generates a small, plain, invisible **world boundary**: four
`BoxCollider`s (no mesh, never visible) forming a rectangle right at the
treeline's outer edge. The trees are why the map *looks* enclosed and why
a player can't see past the edge; the boxes are why they *actually* can't
walk past it — sharp invisible corners are fine since nobody ever sees
them, so it doesn't bother matching the trees' rounded corners.

1. Same fix as always if you haven't run it on these two files yet:
   select `tree-large.fbx` and `tree-small.fbx` → Materials tab → Extract
   Materials → Render Face: Both.
2. Also check the **Model** tab on both: **Generate Colliders** should be
   checked. Trees still block the player on their own most of the time —
   the boundary wall below is the backstop for the gaps, not a
   replacement for this.
3. Create an empty GameObject at the world origin (0,0,0), name it
   `ForestRing`.
4. Add the **Forest Ring Spawner** component to it (Add Component →
   search "Forest Ring Spawner").
5. Drag `tree-large` and `tree-small` into the **Tree Prefabs** list (2
   entries).
6. Leave the defaults for a first pass — **Inner Half Width/Height**
   (100/150), **Outer Half Width/Height** (140/190), **Corner Radius**
   (40), **Ring Count**/**Trees Per Ring** (4/90). The Scene view will
   immediately show 4 concentric green rounded-rectangle wireframes (this
   works in Edit mode, no need to press Play) so you can check the band
   sits between the house ring and the skyline ring before committing to
   it — the defaults were sized against the house ring's actual bounds
   (~85 on X, ~140 on Z) with the outer edge staying well inside the
   skyline's radius-230 ring.
7. Press Play to actually spawn the trees and see the real result. Tune
   **Inner/Outer Half Width/Height** independently if the band feels too
   close on one axis but fine on the other (that's the whole point of
   splitting width and height instead of one radius), **Corner Radius**
   if the corners look too sharp/too round for how tight the house ring's
   own corners are, and **Trees Per Ring**/**Ring Count** if there are
   visible gaps or it feels too dense (more trees = more instantiated
   GameObjects — a few hundred low-poly trees is trivial for this scene,
   don't worry about performance at this scale).

You'll also see a red wireframe box in the Scene view (Edit mode, no need
to press Play) — that's the boundary wall preview, generated automatically
at the outer ring's footprint using the **World Boundary** section's
settings (**Generate Boundary Wall**, on by default; **Boundary Wall
Height**/**Thickness**, 25/4). Leave those alone unless the wall doesn't
feel tall enough to stop a determined jump — you won't ever see this
wall, only feel it.

## 4. Hills (extra horizon coverage)

The forest ring blocks sightlines close to the map, but the skyline sits
much further out (radius 230) — there's still a gap of flat, empty
ground between "where the trees stop" and "where the skyline starts"
where the horizon can peek through. A ring of low rolling hills in that
gap closes it off, and reads as natural rather than another wall of trees.

There's no existing asset for this — but a hill doesn't need one. A
simple flattened sphere, colored to match the locked palette, reads fine
as a background mound at this distance and this art style ("low poly,
minimal, simple readable silhouettes" — `art-info.md`'s Style direction).

1. **Create the Hill prefab once:**
   - In the Hierarchy: **GameObject → 3D Object → Sphere**. Rename it
     `Hill`.
   - Set its **Scale** to `(80, 35, 80)` — wide and squashed, not a
     round ball.
   - Set its **Position** to `(0, 0, 0)` — same ground level as
     everything else. Half the sphere sits below ground (never seen,
     doesn't matter) and half pokes up as a smooth dome roughly 17
     units tall.
   - Give it a simple Material in a slightly darker/duller green than
     your locked grass color (`#6FCF97`) — something like `#4E8770`
     reads as "distant hill" rather than "more walkable grass," which is
     the point (real distant terrain looks slightly hazier/cooler than
     what's nearby — free depth cue).
   - Remove the **Sphere Collider** component Unity added automatically
     — hills sit well behind the boundary wall, they don't need their
     own collision.
   - Drag it from the Hierarchy into `Assets/Art/Environment/` to make it
     a reusable prefab, then delete the instance from the Hierarchy.
2. Create an empty GameObject at the world origin, name it `HillRing`.
3. Add the **Hill Ring Spawner** component (`Assets/Scripts/World/`,
   same pattern as the other two spawners).
4. Drag your new `Hill` prefab into the **Hill Prefabs** list (one entry
   is enough — per-instance scale variance does the rest of the work).
5. Leave the defaults for a first pass — they're sized to start just past
   the forest's outer edge and extend slightly beyond the skyline's
   radius-230 ring, so hills partially sit in front of the distant
   buildings too (a nice layered-depth effect, not a bug). Check the
   tan/brown gizmo wireframes in the Scene view against the forest's
   green ones and the skyline buildings before pressing Play.
6. Press Play. Hills vary a lot more in size than trees did by design
   (`Scale Range XZ`/`Scale Range Y`) — real rolling hills are uneven, so
   don't tune this one for even coverage the way you did the treeline.

## Test

Press Play, walk to the edge of the house ring, and look outward — confirm
the skyline reads as a distant backdrop (not obviously a ring, not
close enough to look like flat cardboard cutouts), the skybox shows
correctly behind/above it, the hills fill the gap between the treeline
and the skyline without an obvious flat horizon line poking through, and
the forest ring reads as an enclosing treeline with no visible gaps. Then
actually try to walk into and through the treeline from a few different
angles to confirm the invisible boundary wall actually stops you — that's
the real test of whether it works as a boundary, not just how it looks
from a distance.

# Stage 3d — Editor setup (real character art + house interior check)

Two independent tasks: swap the placeholder capsules for real Quaternius
characters, and check whether the Kenney suburban buildings have walkable
interiors. Do them in either order — they don't depend on each other.

## A. Homeowner character swap

Homeowner never moves (per its Idle → Suspicious → Alerted design — no
locomotion), so this is the simpler of the two swaps: just a visual
replacement plus a single Idle animation, no Animator parameters needed.

1. In the **Project** window, open `Assets/Prefabs/Houses/`, double-click
   `Homeowner` to enter **Prefab Edit Mode** (its own isolated view, separate
   from the main scene — anything you change here updates every instance).
2. Select the `Homeowner` root object in the Hierarchy. In the Inspector,
   find its **Mesh Renderer** component and **uncheck the checkbox** next to
   the component name (not the GameObject's own checkbox) — this hides the
   gray capsule visually while leaving the **Capsule Collider** active, so
   it still physically blocks the player.
3. In the Project window, navigate to
   `Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/FBX/` and
   pick a model — `Casual_Male.fbx` or `Casual_Female.fbx` reads well as
   "resident."
4. Drag that `.fbx` from the Project window onto `Homeowner` in the
   Hierarchy, dropping it **as a child** (drop it directly on top of the
   `Homeowner` row, not beside it).
5. Select the new child model. Set its **Position** to `X 0, Y -1, Z 0`.
   Why `-1`: `Homeowner`'s Capsule Collider is centered on the parent's own
   origin with a height of 2, so its bottom sits 1 unit below that origin —
   `-1` puts the character's feet exactly there, flush with the floor.
6. Check the result from the Scene view: the model should stand fully
   inside where the invisible capsule was, feet on the floor, not sunk in
   or floating.
7. **Verify the facing direction** before moving on — this is the
   [artist-todo.md](artist-todo.md) note from earlier. With `Homeowner`'s
   own rotation at whatever value currently aims its vision cone correctly,
   does the character's face point the same way the cyan cone does? If not,
   the model's own forward axis doesn't match Unity's convention — select
   just the child model (not the parent) and adjust *its* local rotation
   only until the face matches the cone, leaving `Homeowner`'s own rotation
   (and therefore the AI's facing logic) untouched.
8. Adjust `Eye`'s position if the new model's head sits noticeably higher or
   lower than the old capsule's — it's currently `(0, 0.7, 0.3)`, tuned for
   a 2-unit-tall capsule. Eyeball it against the new model's actual head
   height.
9. Set up the Idle animation:
   - Right-click in `Assets/Art/Characters/` (or make a new
     `Assets/Art/Characters/Animators/` folder) → **Create → Animator
     Controller**, name it `HomeownerAnimator`.
   - Double-click it to open the **Animator** window.
   - In the Project window, expand the character `.fbx` you dragged in
     (click the arrow next to it) — you'll see its embedded clips, matching
     your screenshot (`Idle`, `Walk`, `Death`, etc.).
   - Drag the **Idle** clip into the open Animator graph. Since it's the
     only state, Unity automatically makes it the default (shown in
     orange) — nothing else to wire up.
   - Select the child model object again, find its **Animator** component
     (added automatically when you dragged in a rigged FBX), and drag
     `HomeownerAnimator` into its **Controller** field.
10. Exit Prefab Edit Mode (the `<` back arrow at the top of the Hierarchy,
    next to the prefab's name breadcrumb).
11. Press Play, confirm the new model stands in place, idling, and that the
    vision cone / catch behavior from Stage 3b is unaffected.

## B. Police character swap

Police *does* move, so this one needs a real Animator setup driven by
speed — code for this is already in place: `PoliceAI.cs` now has an
`Animator` field and feeds `agent.velocity.magnitude` into a `Speed`
parameter every frame automatically.

1. Steps 1–8 above, but on the `Police` prefab instead of `Homeowner`:
   hide its Mesh Renderer (keep the Capsule Collider), drag in a character
   (e.g. `BlueSoldier_Male.fbx`, for visual contrast with the homeowner),
   position the child at local `(0, -1, 0)`, verify facing against the
   **red** cone this time, adjust `Eye` if needed.

   Note: `Police`'s Capsule Collider was already set to **Is Trigger**
   during Stage 3c's catch-distance debugging — leave that as-is, it's
   intentional (see that stage's notes on why).
2. Create a second Animator Controller, `PoliceAnimator`, same way as
   above.
3. This time, build a **Blend Tree** instead of a flat list of states:
   - In the Animator window, right-click the empty graph → **Create State
     → From New Blend Tree**. Rename the new state `Locomotion` (select it,
     rename in the Inspector) and right-click → **Set as Layer Default
     State**.
   - Double-click the `Locomotion` state to open the Blend Tree editor.
   - In the Inspector, under **Parameter**, click the dropdown and create a
     new **Float** parameter named exactly `Speed` (must match — this is
     what the code writes to every frame).
   - Click the **+** under Motion → **Add Motion Field**, three times, and
     assign the character's `Idle`, `Walk`, and `Run` clips to the three
     slots.
   - Set each motion's **Threshold** to roughly match real speeds: `Idle =
     0`, `Walk = 3.5` (matches `PoliceAI`'s `Patrol Speed`), `Run = 6`
     (matches `Chase Speed`). This makes the blend switch smoothly between
     them as the NavMeshAgent's actual speed changes.
4. Back out to the main Animator graph (breadcrumb at the top).
5. Select `Police`, drag `PoliceAnimator` into its **Animator** component's
   **Controller** field, exit Prefab Edit Mode.
6. Select `Police` again in the main scene (not prefab edit mode) — the
   `Police AI` component now has an **Animator** field near the top. Drag
   the child model's `Animator` component onto it (drag the child
   GameObject itself; Unity will pick up the right component).
7. Press Play: `Police` should idle while patrolling stationary at a
   waypoint, walk between patrol points, and switch to a running animation
   the moment it enters `Chase`. If it looks stiff or doesn't blend, double
   check the `Speed` parameter name matches exactly (`Speed`, case-sensitive)
   between the Blend Tree and the `Animator Speed Param` field on
   `Police AI` (defaults to `"Speed"` already, only change one if you
   change the other).

## C. House interior check

Before deciding how houses get rebuilt with real art:

1. In the Project window, go to `Assets/Art/Environment/Kenney-CityKitSuburban/FBX/`.
2. Drag any `building-type-*.fbx` (try a few letters) into an empty area of
   the Scene view, away from your existing houses.
3. In the Scene view, use the **Rotate/Orbit** (right-click drag) or fly the
   camera through where a door or window would be, and look inside.
4. Two outcomes:
   - **Fully modeled interior, walkable** → tell me, and we'll replace
     `House_01`'s blockout directly with one of these, keeping the same
     door-trigger and item-placement pattern from Stage 3a.
   - **Solid/hollow shell, exterior only** → also tell me, and we'll use it
     as the *outside* look while keeping a separate interior floor plan
     underneath — a normal technique, just a bit more setup than a direct
     swap.

Once both character swaps are tested and the house interior question is
answered, we'll move on to actually rebuilding the houses and laying out
real streets with the `CityKitRoads` pieces.

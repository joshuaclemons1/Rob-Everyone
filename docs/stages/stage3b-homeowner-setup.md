# Stage 3b — Editor setup (homeowner vision-cone AI)

Script is already in `Assets/Scripts/AI/HomeownerAI.cs`. This covers building
one homeowner in `House_01` and wiring it up. Do this before adding more
homeowners to the other houses — get one working and tuned first.

## 1. The homeowner's body

1. Right-click in the **Hierarchy** → **3D Object → Capsule**. Rename it
   `Homeowner` (F2 or slow double-click).
2. Drag it so it sits **inside** `House_01`'s floor in the Scene view —
   position it somewhere the player has to approach, not right at the
   doorway.
3. Rotate it (the **Rotation → Y** field in the Inspector) so it's facing a
   sensible direction — e.g. facing across the room, or facing the doorway.
   This rotation is its "forward," and the vision cone will point wherever
   it's facing.
4. Leave its default **Capsule Collider** as-is (added automatically) — this
   makes the homeowner a solid obstacle the player can't walk through,
   which is correct.

## 2. The "eye" point

The AI needs a specific point and direction to look from — using the
capsule's center would put the vision cone origin buried inside its own
body.

1. Right-click `Homeowner` in the Hierarchy → **Create Empty**. This creates
   it as a child automatically. Rename it `Eye`.
2. Set its **Position** (this is *local* position, relative to `Homeowner`,
   since it's a child) to `X 0, Y 0.7, Z 0.3` — roughly head height, slightly
   toward the front of the capsule.
3. Leave its **Rotation** at `(0, 0, 0)`. It doesn't need its own rotation —
   as a child with no local rotation, it automatically faces the same
   direction `Homeowner` is rotated to.

## 3. The Homeowner AI component

1. Select `Homeowner` in the Hierarchy (not `Eye`).
2. In the Inspector, **Add Component** → search `Homeowner AI` → add it.
3. Drag the **`PlayerCamera`** object (the child under `Player`, not `Player`
   itself) into the **Player Target** field. Using the camera gives a
   head-height target instead of the player's feet, which makes the vision
   cone feel fair.
4. Drag `Eye` (the child object you just made) into the **Eye** field.
5. Drag `Homeowner` itself into the **Body Renderer** field — Unity will
   automatically pick up its Mesh Renderer component from the GameObject you
   drop in.
6. Leave everything else at its default for now:
   - **View Distance**: `10`
   - **View Angle**: `60`
   - **Obstruction Mask**: `Everything` (default — this is what makes walls
     block the homeowner's sight; you shouldn't need to touch it)
   - **Suspicion Build/Decay Rate** and **Suspicion Threshold**: defaults
     give roughly 2 seconds of continuous, unobstructed visibility before
     the homeowner goes fully Alerted.
   - **Idle/Suspicious/Alerted Color**: white / yellow / red — this is your
     main testing feedback, see below.

## 4. Test

1. Select `Homeowner` in the Hierarchy (not in Play mode yet) — you should
   see three cyan lines fan out from `Eye` in the Scene view. That's the
   vision cone gizmo; it shows you exactly where the homeowner can see,
   which is useful for repositioning it before you even press Play.
2. Press Play. Walk toward `Homeowner` from outside its cone first — it
   should stay **white** (Idle).
3. Walk into the cone, within `View Distance`, and stay there. It should
   turn **yellow** (Suspicious) almost immediately, then **red** (Alerted)
   after roughly 2 seconds of being continuously visible.
4. Check the **Console** window (Window → General → Console if it's not
   open) — you should see a log line like `Homeowner called the police!` the
   moment it turns red.
5. Try ducking behind a wall or out of the cone while it's still yellow —
   suspicion should decay and it should fade back to white if you stay
   hidden long enough. Once it's fully **red**, it stays red — that's
   intentional, it doesn't forget it saw you.

If it never leaves white: check the Console for a null reference (most
likely `Player Target` or `Eye` wasn't dragged in), and confirm you're
actually inside the cyan cone gizmo lines, not just nearby.

Once this feels right, tell me and we'll either tune the numbers (view
distance/angle, suspicion timing) or move on to duplicating a homeowner into
the other houses and then Stage 3c: police AI that responds to
`OnPoliceCalled` and chases the player down.

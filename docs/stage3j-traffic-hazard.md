# Stage 3j — Editor setup (traffic hazard: cars on the road)

Adds a second obstacle alongside the police: cars that drive one lap of
the road loop and can knock the player down. Confirmed design (your
answers):

- **Oblivious** — cars don't see or react to the player at all, they just
  drive their route. Avoiding them is entirely on the player.
- **Pure knockback + stun**, not a round-ending catch like police, and not
  a full per-bone ragdoll (see the scripts' comments for why) — the whole
  player capsule tumbles as one physics object for a couple seconds, then
  stands back up.
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
`CarImpactReceiver.cs` (`Assets/Scripts/Player/`).

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

1. Select your player prefab/object (the one with `FirstPersonController`
   on it).
2. Add the **Car Impact Receiver** component. This auto-adds a
   **Rigidbody** and a **Capsule Collider** if they're not already
   there — both start disabled/inert and only activate during the brief
   tumble, so they won't interfere with normal `CharacterController`
   movement the rest of the time.
3. Size that new **Capsule Collider** to roughly match your
   `CharacterController`'s **Height**/**Radius**/**Center** values (check
   the CharacterController component for the numbers) — it's what
   actually collides with the ground during the tumble, so it should be
   close to the same shape as the player normally is.
4. Leave **Stun Duration** at its default (1.5s) for a first test.

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
on corners, returns to the same point, and despawns. Then deliberately
stand in its path — confirm you get knocked down (capsule tumbles, brief
stun, then you're standing again and back in control), and that this
doesn't touch your quota, loot, or round state at all. Finally, let a full
play session run a few spawn-check cycles to confirm it really does
sometimes stay at zero cars, not just "always spawns eventually."

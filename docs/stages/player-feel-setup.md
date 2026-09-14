# Player feel pass — instant jump, bhop/autohop, first-person body

Mostly code. The Editor side is a handful of Inspector values on the
**Player prefab** to tune to taste.

## What changed

- **Instant jump.** `FirstPersonController` applies the launch velocity
  the frame Space goes down — no more waiting on the jump animation's
  anticipation window (`ScheduleJumpLaunch` / `DelayedLaunch` are gone).
  `PlayerAnimationDriver` still fires the Jump trigger and rescales the
  clip (`JumpSpeed`) to roughly match air time, but never gates input.
  The anticipation squat now plays while you're already rising; set
  **Jump Anticipation Fraction** to `0` on `Player Animation Driver` if
  you'd rather skip it.
- **Autohop.** `First Person Controller` → **Hold To Auto Hop** (default
  on): holding Space re-jumps the instant you land. Turn it off for
  jump-only-on-press.
- **CS-style air control, with a speed ceiling.** `Air Acceleration`
  `100`, `Air Wish Speed` `1.0` (was `12` / `3`). The tight wish-speed
  cap is the point — holding W in the air doesn't accelerate you; you
  gain speed by air-strafing (turn the view while holding a strafe key).
  Ground friction is skipped on the jump frame so a clean bhop keeps its
  speed through the hop. **`Max Air Speed`** (`16`, ~2x sprint) is a
  hard ceiling — air-strafing keeps redirecting momentum but can't push
  past it.
- **First-person visible body, trimmed.** Your own skin renders now
  (it's not a floating camera, and it's the anchor for the future
  held-hotbar-item-in-hands). On *your* copy only, `FirstPersonBodyTrim`
  (added at runtime by `PlayerSkinSpawner`) scales bones to zero:
  **grounded** just the head (look down, see your torso/arms/legs);
  **airborne** the whole upper body too (the jump spring pushes it into
  the camera otherwise). It restores everything while the camera is cut
  away (Tab / steal screen, ragdoll stun show a full third-person view).
  Every other client sees the complete model.

## Editor

### Player prefab

1. **First Person Controller** — tune to taste:
   - **Hold To Auto Hop** — on.
   - **Air Acceleration** `100`, **Air Wish Speed** `1.0` — the bhop
     feel. Lower Air Wish Speed = harder to gain speed; higher = easier
     (too high and holding W just accelerates you, which kills the
     skill element).
   - **Max Air Speed** `16` — the hard bhop ceiling. Raise/lower to
     taste; set huge to uncap.
   - **Jump Height** — unchanged; the animation follows it now.
2. **Player Animation Driver** — **Jump Anticipation Fraction**: `0` for
   no squat, or leave at `0.2` and accept the squat plays mid-rise.
3. **Player Skin Spawner** — two arrays, **First Person Hidden Bones
   Grounded** (`Head`, `Head_end`) and **... Airborne** (`Head`,
   `Head_end`, `Torso`). If a skin's bones are named differently, add
   them — `FirstPersonBodyTrim` logs a warning naming the skin if
   nothing matched. If the airborne collapse looks bad, try just
   `Torso`, or swap it for `Neck` + `Shoulder.L`/`Shoulder.R`.
4. No layer changes. The owner's skin is on **Default** now like
   everyone else's (`skinLayer` is kept only for `PlayerRagdoll`/
   `PlayerCameraRig`'s now-no-op culling toggle).

### 🔴 Rest Point
Two Editors, or one for the movement:

- Jump responds the **instant** you press Space — no lag.
- Hold Space while running → you bunny-hop continuously without
  re-pressing.
- Strafe-jump (hold A + turn left, or D + turn right, repeatedly) and
  speed builds past sprint. Holding just W in the air does not.
- Land without holding Space → friction brings you back to walk speed.
- Look down → you see your torso/legs, no head, no clipping. A rival
  looking at you sees your full model with head.
- Get hit by a car → the ragdoll shows your full head (trim pauses
  during the stun), then trims again once you're up.

## Still open

Tracked as [issue #40](https://github.com/joshuaclemons1/Rob-Everyone/issues/40):

- Arm/shoulder clipping in first person if it's bad — nudge the camera
  forward a touch, or add `Shoulder.L`/`Shoulder.R`/`UpperArm.*` to the
  hidden-bones list.
- A real separate first-person viewmodel (own mesh, own FOV) is the
  "proper" version — deferred.

The ragdoll get-up-too-fast bug was a separate deeper dive — fixed, see
[issue #31](https://github.com/joshuaclemons1/Rob-Everyone/issues/31).

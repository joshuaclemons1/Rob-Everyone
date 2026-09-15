# Tier 1 / Tier 2 bug fixes — 2026-09-14

Code-only pass through the Tier 1 (bug + urgent) and Tier 2 (bug +
non-urgent) items from [issue-tracking.md](../issue-tracking.md)'s
priority list — everything here was written and reasoned through
without Unity Editor access, so most of it went unverified by a
compiler until the Editor testing pass noted below.

Also picked up **#45**, **#46**, and **#47** partway through this pass
— bugs reported directly, not originally on the Tier 1/2 list — since
all three were diagnosable with the same level of confidence as the
rest of this doc. See their own sections near the end.

All nine fixes are committed on `jclem's-branch` (`4244623` for
#4/#8/#11, `5e066b9` for #1/#15, `47ebf19` for #45, `b16d27c` for #46,
`33cf530` for #47 — see each issue's own GitHub comment for the exact
commit hash) and have progress comments on their issues.

**Status as of 2026-09-14 (Editor testing pass):** #1, #4, #15, #46, and
#47 have all been tested in the Editor and confirmed fixed — **closed**.
The remaining three (#8, #11, #45, and everything in the "Investigated,
not fixed" section) still need testing and remain open; #8/#11 need
real Editor play but not multiplayer, #45 needs 2+ players (ParrelSync
or same-machine is enough), and the rest need a real Steam multiplayer
session with actual network latency.

**Note on #47:** confirming the Canvas Scaler fix required repositioning/
rescaling a handful of MainMenu elements by hand to look right at the
new reference resolution (the pre-existing unrelated working-tree
changes noted in an earlier version of this doc were part of that same
pass) — that's your own Editor/art work sitting locally
(`Assets/Scenes/MainMenu.unity`, `Assets/Prefabs/UI/SettingsPanel.prefab`),
not touched or committed by any fix in this doc. Commit it on your own
schedule.

## Confirmed fixed — closed

### #1 — Sabotage cooldowns don't reset between rounds ✅ closed

**What was wrong:** both cooldown clocks in `SabotageUseController`
(`nextReadyTime`, the server-authoritative one, and `localNextReadyTime`,
its client-side cosmetic mirror for the HUD) are tracked purely against
`Time.time` — a session-wide clock that's never reset on its own.
Nothing previously cleared either dictionary between rounds, so a
cooldown (e.g. Taser) started late in one round could still be counting
down into the next.

**What changed:** added `SabotageUseController.ServerResetCooldowns()`
(clears the server dictionary, and `TargetRpc`s the owning client to
clear its local mirror too) and call it for every player at the top of
`GameFlowManager.HandleRoundStarted` — the same "runs at the start of
every fresh round" hook the #12/#16 fixes already use.

### #4 — Caught by Police while no officer is visible ✅ closed

**What was wrong:** `PoliceAI`'s point-blank catch check
(`UpdateChase`) was pure straight-line `Vector3.Distance`, with nothing
checking whether a wall was actually in the way. On a thin wall between
two rooms that distance can read as "in catch range" while the officer
is actually on the other side of solid geometry, navigating the long
way around — nothing visible to the player at all.

**What changed:** added `PoliceAI.IsBlockedByGeometry`, a raycast reusing
the same `obstructionMask` the vision cone (`CanSee`) already checks
against. The point-blank catch now also requires a clear line to the
target, not just proximity.

### #15 — End-of-round screen text too long, renders off screen ✅ closed

**What was wrong:** `LoadingScreenUI`'s message text mostly shows short
static strings ("Starting next round...") but also
`GameFlowManager`'s dynamically-built batch-progress summary
(`"Batch progress: $X cash + $Y inventory / $Z"`), whose length depends
entirely on how much cash/inventory a player is carrying. A font size
tuned for the short messages can run past the edge of the panel once
those numbers get large.

**What changed:** `LoadingScreenUI.Show` now turns on TMP's built-in
shrink-to-fit (`enableAutoSizing`) the first time it's called, capping
`fontSizeMax` at whatever size was already authored (so short messages
still render exactly as before) and `fontSizeMin` at 60% of that. Only
does this once — if auto-sizing is already configured in the Editor, it
leaves it alone entirely.

### #46 — Intro video shows empty scene around it on non-16:9 displays ✅ closed

**What was wrong:** confirmed by reading the scene file — the target
camera's `m_ClearFlags` was set to Skybox, not a solid color, so
anywhere `VideoPlayer` (rendering in `CameraNearPlane` mode) doesn't
cover the screen, the camera's own regular render shows through instead
of a blank background.

**What changed:** `IntroSequence.Awake` now forces both relevant
settings in code rather than trusting the Editor-authored scene values:
`VideoPlayer.aspectRatio = VideoAspectRatio.FitHorizontally` (always
pins the video to the full screen width — height scales to match, so
any gap is strictly top/bottom on a wider-than-16:9 display, never left
or right), and the target camera's clear flags forced to `SolidColor`
black, so that gap always renders pure black instead of the scene
behind it.

### #47 — MainMenu UI scales/positions wrong on any resolution besides 4K ✅ closed

**What was wrong:** confirmed by reading the scene file — MainMenu's
main Canvas (the one carrying the Play/Settings/Quit buttons) had its
`CanvasScaler` set to **Constant Pixel Size**, with a stale, irrelevant
800x600 reference resolution left over from it (that mode ignores
reference resolution entirely — 1 UI unit is always exactly 1 screen
pixel, no matter the actual resolution). Every other Canvas in the
project — MainMenu's own `LoadingScreenCanvas`, SampleScene's HUD
Canvas — uses **Scale With Screen Size** at a 3840x2160 reference, and
`MenuNavigator.slideDistance`'s own comment says it assumes this exact
Canvas uses that reference width. It never actually did.

**What changed:** MainMenu's Canvas `CanvasScaler` now matches every
other Canvas in the project — Scale With Screen Size, 3840x2160
reference, 0.5 match. Scene-data change only, no script involved.
Confirming this fix also needed a hand pass repositioning/rescaling a
handful of MainMenu elements to look right at the corrected reference
resolution — your own follow-up Editor work, not part of this fix.

## Fixed — code done, needs a playtest to confirm

### #8 — Jailed players can walk straight out of their cell

**What was wrong:** `JailState.EnterJail`'s own long-standing comment
says containment was always meant to rely entirely on the cell's level
geometry, with movement deliberately left unfrozen. Any gap, seam, or
jump-over lets a jailed player just walk out with nothing left to stop
them.

**What changed:** added a server-authoritative horizontal leash in
`JailState` on top of the existing geometry (not replacing it) —
`confinementRadius` (`[SerializeField]`, default 4m) around the exact
`JailPoint` slot `GameFlowManager` assigns that player.
`GameFlowManager.TeleportToJail` now returns that slot's `Transform` so
`JailState` can hold onto it as the anchor; a jailed player who strays
past the radius gets snapped straight back to it. Still free to walk/
look anywhere inside the radius, same as before.

**Editor steps needed:** `confinementRadius` defaults to 4m, which is a
guess — once in the Editor, check it against the actual jail cell
dimensions (`JailPoint` layout in the gameplay scene) and tune it up/
down in the Inspector if 4m is noticeably smaller or larger than the
real cell footprint. Doesn't need a code change to retune, just the
serialized field.

**Test:** get jailed, try to walk straight out through/around whatever
gap used to let this happen. Confirm you get pulled back once you clear
`confinementRadius`, and that you can still move around normally inside
it (this shouldn't feel like a hard wall at the exact cell boundary,
just an invisible snap-back past it).

### #11 — Changing display resolution in a compiled build breaks the UI / can softlock

**What was wrong:** `DisplaySettingsApplier` forwarded a custom
resolution index into `Screen.SetResolution` even in
`FullScreenMode.FullScreenWindow` (the project's default) — that mode
silently ignores the requested width/height and always renders at the
OS desktop's native resolution regardless. That desyncs
`Screen.width`/`height` (and every screen-space UI raycast built on
them, including the Settings menu's own Back button) from what's
actually on screen, which is the "can softlock" part — nothing left
clickable to undo it.

**What changed:**
- `DisplaySettingsApplier.Apply` now treats `FullScreenWindow` as always
  "Current" (same as the no-override case), since a custom size was
  never actually applying there anyway.
- Added `DisplaySettingsResetHotkey` — **F9** (unbound anywhere in
  `RobEveryoneControls.inputactions`) resets display settings to native
  resolution + borderless windowed. Polls the raw keyboard directly
  (same pattern `IntroSequence` already uses pre-gameplay), not through
  any action map or UI, so it works even if the screen is currently in
  the broken state this issue describes.
- `DisplaySettings.ResetToSafeDefaults()` is the new method both of the
  above call into.

**Editor steps needed:** none for the fix itself. `DisplaySettingsResetHotkey`
self-bootstraps (`RuntimeInitializeOnLoadMethod`) — no scene placement
needed, but worth confirming in the Editor that F9 doesn't collide with
anything else (Unity Editor's own Play Mode shortcuts, any future
rebind). It didn't overlap any existing binding as of this pass.

**Test:** in a compiled build (not the Editor — this bug is specifically
about builds), open Settings > Graphics, switch to Windowed or
Exclusive Fullscreen, pick a non-native resolution, confirm it actually
applies correctly. Then switch back to Fullscreen Window and pick a
different resolution from the dropdown — confirm the UI stays usable
and doesn't desync. Separately, as a deliberate worst-case check: try
to get the UI into a bad state on purpose (rapid resolution/mode
switching) and confirm **F9** recovers it.

### #45 — Exit car only seated the first player; others got bounced out

**Reported behavior:** in a real multiplayer test, only the first
player to interact with the exit car actually got seated. Every player
who tried after that was immediately teleported to roughly the "Get
Out" stand position — but their round still ended correctly about 5
seconds later anyway, even though they never visibly sat down.

**What was wrong:** `ExitPoint` had exactly one shared `seatPoint`
Transform, with nothing tracking who was already using it. A second
player entering while the first was still seated got teleported into
the *exact same* coordinates the first player already occupied.
`ExitCarState.EnterCar` sets `FirstPersonController.ExitCarFrozen`,
which only gates the controller's own input-driven movement — it does
**not** disable the `CharacterController` component itself. The moment
the second player's `CharacterController` found itself overlapping the
first player's at the identical position, Unity's own automatic
depenetration silently shoved it out to whatever nearby space was
free, which happens to look like the "Get Out" stand point since
that's the natural open space right next to the car. Nothing in code
ever actually called `CmdExitCar` for the second player or cleared
their `isWaiting` — which is exactly why the round still finished
correctly for them regardless; the server-side state was fine the
whole time, only the visual position was wrong. Same class of problem
`GameFlowManager.ClaimJailPoint` already solves for jail cells
(multiple slots, one claimed per occupant) — `ExitPoint` never got the
equivalent, most likely because it was only ever tested solo before
this multiplayer pass.

**What changed:**
- `ExitPoint` now tracks occupants (`ClaimSeatPosition`/`ReleaseSeat`)
  and hands out a computed, non-overlapping offset position to every
  occupant after the first — alternating left/right off the one real
  `seatPoint`, `seatSpacing` (default 0.6m) further out each pair.
- `GameFlowManager.TeleportPlayerTo` gained a `Vector3`/`Quaternion`
  overload (the original `Transform`-taking one now just delegates to
  it) since a computed offset position has no backing scene Transform.
- `ExitCarState` releases its claimed seat in every exit path (climb
  out, finalize, force-release).
- `RobEveryoneNetworkManager.OnServerDisconnect` now also releases a
  disconnecting player's claimed seat, alongside the existing
  carried-body drop, so a mid-wait disconnect can't leave a phantom
  occupant permanently holding a slot.

**This is a functional fix, not a visual one** — extra riders won't
visually look "inside" the car the way the first one does; they'll
stand at a computed offset point beside/behind the real seat. Real
multi-seat placement (actual seatPoint Transforms positioned inside the
car model) is still worth doing in the Editor once there's a car model
that visually supports more than one rider — `seatSpacing` is the one
tuning knob available without that.

**Editor steps needed:** none for the fix to function. Optional: place
real second/third seatPoint Transforms in the car model and wire up a
`seatPoints` array instead of the computed offset, if/when the car
model actually gets built out to show multiple riders. Also worth
eyeballing `seatSpacing` (0.6m) against the actual car's footprint —
too small and riders could still clip each other; too large and they
drift away from looking like they're using the same car at all.

**Test:** with 3+ players, have all of them interact with the same
exit car in quick succession while the first is still waiting. Confirm
every player gets a distinct, non-overlapping position (not bounced
out to the "Get Out" spot) and that all of their rounds correctly
resolve after their own `carWaitDuration`. Also test a disconnect mid-
wait (close the game on one client while seated) and confirm it doesn't
permanently block that seat slot for later rounds.

## Investigated, not fixed — needs to be watched happen live

These didn't get a code change. Each one was dug into with the same
scrutiny as the fixed items above, but the evidence ran out short of an
actual fix — forcing a guess here risks spending your playtest time
confirming something that was never the real cause. Comments are
already posted on each issue with the specific theory and what to watch
for.

### #7 — Ragdoll carry: thrown player sometimes stands up in front of carrier instead of being thrown

The whole throw path (`CarryController.ServerDrop` → `Carryable.
ServerDetach` → `RpcOnDetached` → `PlayerRagdoll.ApplyThrowImpulse`) is
already hardened against the two known failure modes its own comments
describe (RPC/SyncVar ordering, hips-only impulse getting absorbed by
joints) — neither explains this symptom. Best remaining theory:
collider depenetration at the instant of release, if the carried body's
ragdoll colliders happen to be overlapping something other than the
carrier (a wall, another player) when the impulse fires — collision
with the *carrier* specifically is already ignored for a window after
release, but nothing else is.

**What to watch for when it happens:** does the throw visibly launch
and then snap back, or does it never launch at all? That distinguishes
"impulse applied then cancelled" (points at depenetration) from
"impulse never applied" (points at something upstream not firing) —
whichever one it is narrows this a lot.

### #10 — Settings menu background renders mismatched in compiled build (fine in Editor)

No script in `Assets/Scripts` touches a background Image/Material/
Camera for the Settings menu at all — `SettingsPanelController` only
handles tab switching. This is a scene/prefab configuration issue, not
something fixable with a C# change.

**What to check first, in this order:**
1. **Sprite Atlas packing** — if the background sprite is in a Sprite
   Atlas, a Player build can pull a different packed variant than the
   Editor's live-packed Game View does. Window > 2D > Sprite Atlas.
2. **Texture compression/platform overrides** — the Editor Game View
   often previews a texture uncompressed regardless of import settings;
   a build always applies real platform compression.
3. **Aspect-ratio mismatch** — same root theme as #11: the Editor Game
   View is usually pinned to a fixed aspect, while a build launches at
   the OS desktop's native resolution. Worth checking the background
   Image's scale mode/anchoring handles arbitrary aspect ratios.

Given #11 turned out to be exactly an aspect/native-resolution class of
bug, start with #3.

### #13 — General networking jankiness over Steam (animation desync, movement jitter)

Already cross-referenced against #2/#3/#5/#9 from the prior investigation
pass — most likely the same root cause across all of them:
`NetworkTransformReliable` interpolation/extrapolation behavior under
real internet latency, which never shows up in near-zero-latency
same-machine testing. This needs to be tuned against real Steam-hosted
latency, which means Editor + a real multiplayer session with an actual
non-host player, not a code guess.

## Also still open, unchanged this pass

**#2, #3, #5, #9** — same NetworkTransform-under-real-latency theory as
#13 above; investigation comments already posted on each from the prior
session, nothing new found this pass. All four need the same real
multiplayer test as #13 once more than one PC is on the network at
once.

## Suggested order once back at the PC

1. ~~Let the Editor recompile everything in this doc — check the Console
   for any errors before doing anything else.~~ Done — #1/#4/#15/#46/#47
   confirmed working.
2. Work the remaining "Fixed" items (#8, #11) — each is independently
   testable in a single-player or same-machine session, no real network
   latency needed. Tune `JailState.confinementRadius` (#8) against the
   real cell geometry while you're in there anyway.
3. Then move to #10 (Settings background) — Editor-only investigation,
   no multiplayer needed.
4. #45 (exit car) needs at least 2 players, but not real network
   latency — same-machine/ParrelSync testing should reproduce it fine,
   so it doesn't need to wait for a real Steam session like the group
   below does.
5. Save #2/#3/#5/#9/#7/#13 for a real multiplayer session with a genuine
   non-host player over Steam — these all need actual network latency
   to reproduce and can't be meaningfully tested solo.

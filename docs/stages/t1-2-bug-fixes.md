# Tier 1 / Tier 2 bug fixes — 2026-09-14

Code-only pass through the Tier 1 (bug + urgent) and Tier 2 (bug +
non-urgent) items from [issue-tracking.md](../issue-tracking.md)'s
priority list — everything here was written and reasoned through
without Unity Editor access, so **none of it has been compiled or
run yet**. This doc is the checklist for going through it
systematically once back at a PC: open the Editor, let it recompile,
then work top to bottom.

All six fixes are committed on `jclem's-branch` (`4244623` for #4/#8/#11,
a later commit for #1/#15 — see each issue's own GitHub comment for the
exact commit hash) and have progress comments on their issues. **None
of the issues are closed** — every one of them needs a real playtest
confirming the fix actually works before it gets closed, per
`issue-tracking.md`'s own rule (closed = done *and confirmed*, not just
"code's been pushed").

## Fixed — code done, needs a playtest to confirm

### #4 — Caught by Police while no officer is visible

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

**Editor steps needed:** none — `obstructionMask` and `eye` were already
serialized fields with values already set on the Police prefab; nothing
new to wire up.

**Test:** get chased by Police, and specifically try to break line of
sight around a thin wall/corner while staying within ~2m of the officer
(catchDistance). Confirm you're no longer caught through the wall, but
still caught normally in the open. Also sanity-check normal catches
still work at all (regression check on the geometry raycast itself).

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

## Fixed — code done, needs a playtest to confirm (Tier 2)

### #1 — Sabotage cooldowns don't reset between rounds

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

**Editor steps needed:** none.

**Test:** use a cooldown item (Taser) right near the end of a round,
then confirm it's immediately usable again at the start of the next
round rather than still showing a countdown.

### #15 — End-of-round screen text too long, renders off screen

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

**Editor steps needed:** none required, but worth eyeballing in the
Editor once: open the panel with a deliberately long fake message (a
huge cash/inventory number) and confirm the shrink still looks readable
at the 60%-of-original floor. If it looks too small before it looks
unreadable, that floor (currently hardcoded at `* 0.6f` in
`LoadingScreenUI.ShrinkToFitIfNeeded`) is the one knob to adjust.

**Test:** finish a round carrying a large cash/inventory total (big
enough that the old fixed-size text would've overflowed) and confirm
the end-of-round summary text stays fully on screen and readable.

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

1. Let the Editor recompile everything in this doc — check the Console
   for any errors before doing anything else (none of this was compiler-
   verified, only manually brace-balance-checked).
2. Work the "Fixed" sections above in any order — each is independently
   testable in a single-player or same-machine session, no real network
   latency needed.
3. Tune `JailState.confinementRadius` (#8) against the real cell
   geometry while you're in there anyway.
4. Then move to #10 (Settings background) — Editor-only investigation,
   no multiplayer needed.
5. Save #2/#3/#5/#9/#7/#13 for a real multiplayer session with a genuine
   non-host player over Steam — these all need actual network latency
   to reproduce and can't be meaningfully tested solo.

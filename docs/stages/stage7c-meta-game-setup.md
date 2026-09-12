# Stage 7c — Full meta-game (buy-side shop, Jail & Bail, Homeowner patrol, police dispatch pooling, night mode, rival HUD ping)

Working tracker for the rest of Stage 7 (see `stage7-shop-lobby-setup.md`
and `stage7b-batch-economy-hotbar-setup.md` for the earlier shop/Lobby/
batch-economy work that already shipped ahead of a formal "Stage 7"
label). Built in 7 sequential milestones, simplest/most self-contained
first — same incremental spirit as `stage6-sabotage-items-phase2-setup.md`.
Update the status marker and add to "Bugs found & fixed" as each
milestone actually gets built, so this stays a real record instead of
drifting stale like `gameplay-design.md`'s own "Gap vs. current code"
section did.

**Design reference**: `gameplay-design.md`. **Full code-level design**:
worked out in a planning session before Milestone A started — every
milestone below is a condensed version of that plan; ask for the fuller
reasoning behind any specific decision if a comment here isn't enough.

**Always press Play from `MainMenu`** — `NetworkManager` only exists
there.

---

## Status overview

- [x] **A** — Buy-side shop (pawn-shop shelves) + Lobby practice-mode gate + sabotage tier/pricing — **done, playtested**
- [x] **B** — Real Jail & Bail — **done, playtested**
- [x] **C** — Homeowner patrol — **done, playtested**
- [ ] **D** — Police dispatch pooling
- [ ] **E** — Night mode
- [ ] **F** — Rival-status HUD ping
- [ ] **G** (optional, lowest priority) — Per-pickup randomized item value range

---

## Milestone A — Buy-side shop + Lobby practice mode + tier/pricing — ✅ Done

**What shipped**: walk up to a shelf in the pawn shop, press E, the item
is deducted from your Cash and added straight to your hotbar — no menu,
matching the physical pawn-shop layout (`ShopShelfItem.cs`, mirrors
`SellStation.cs`'s shape). Supply never depletes — every player can
always buy the same item off the same shelf. Price scales on the same
×1.5-per-batch curve quota itself grows on (`GameFlowManager.
QuotaGrowthMultiplier`, `ShopShelfItem.CurrentPrice`). Items unlock by
batch number (`unlockBatch`, tune per shelf in the Inspector) — proposed
starter tiering, adjust by playtesting:

| Batch | Items |
|---|---|
| 1 | Baseball Bat |
| 2 | Taser, Alarm Clock |
| 3 | Hammer, Tranquilizer Gun |
| 4 | Dynamite |

Spending Cash on a purchase (`PlayerInventory.TryPurchase`) is the
*entire* implementation of `gameplay-design.md`'s "sabotage-spending
quota add-on" — it reduces the same Cash balance the batch-end quota
check reads, so hitting quota gets harder automatically. No separate
tracking exists or is needed.

**Lobby practice mode**: sabotage items don't consume durability/ammo
(`PlayerInventory.DecrementUses`) or get removed from your hotbar when
thrown (`SabotageUseController.CmdUseThrown`) while `GameFlowManager.
InLobbyScene` is true — so players can mess around with friends before
a round starts without burning through gear. Both gates, and every
other Stage 7 scene-check in this doc, reuse that same existing
property; no new scene-detection mechanism was introduced.

### Editor setup

1. Build the pawn-shop interior in the Lobby scene (done) — counter
   with `SellStation` on one side, shelves on the other.
2. For each of the 6 sabotage items' `WorldModelPrefab` placed on a
   shelf, add a **Shop Shelf Item** component:
   - **Item** — drag the matching `ItemDefinition` asset
     (`Assets/Data/Items/{AlarmClock,Hammer,BaseballBat,TaserX26,
     TranquilizerGun,Dynamite}.asset`).
   - **Unlock Batch** — per the tiering table above.
   - Confirm a **Collider** (non-trigger) roughly matches the model's
     visible bounds, and a **Network Identity** exists on the object or
     a parent (required for the purchase Command to resolve its
     target — see Bug 1 below).
3. Add the same **Network Identity** requirement check to `SellStation`
   objects if any are missing one.

### Bugs found & fixed

- **Missing `NetworkIdentity` on a shelf silently broke purchasing.**
  `Interactor.CmdInteract` needs a `NetworkIdentity` to resolve the
  interact target on the server — a shelf built directly from an
  item's `WorldModelPrefab` (which bakes `PickupItem` + `NetworkIdentity`
  in via `ItemPrefabBatchTool`) had both stripped as prefab overrides
  when `PickupItem` was correctly removed. Removing `PickupItem` is
  right (it's not needed here); removing `NetworkIdentity` alongside it
  broke the purchase entirely. Fix: keep/re-add `NetworkIdentity` on
  every shelf.
- **`CanInteract` gating swallowed "informational" prompt text.**
  `Interactor.FindTarget` only ever shows *any* prompt when
  `CanInteract` is true — so `ShopShelfItem`'s original `CanInteract =>
  item != null && Unlocked` meant a locked shelf's "Locked until Batch
  N" text could never actually display (no prompt at all for a locked
  shelf), and separately, `SellStation`'s original `CanInteract`
  (gated on "something's selected") meant its own "nothing to sell"
  text had the same problem. Fix, same shape both places: `CanInteract`
  no longer depends on "is this actually usable right now" — it's true
  whenever the target is structurally valid (an item is assigned; the
  station exists). The *actual* gating (locked, empty hand) moved into
  `Interact()` itself, which now just no-ops rather than never being
  reachable. `SellStation`'s prompt text was also reworded to "Nothing
  to sell...".
- **Thrown Hammer duplicated itself in the Lobby.** The Lobby
  practice-mode gate kept a thrown item in the thrower's hotbar (as
  intended), but `RetrievableProjectile.Land()` still unconditionally
  spawned a second, landed-pickup copy at the impact point regardless
  of scene — so a thrown Hammer ended up both still in your hand *and*
  lying on the ground. Fixed by gating that landed-pickup spawn on
  `!GameFlowManager.Instance.InLobbyScene` too.
- **Lobby had no crosshair/interact-prompt UI at all.** `CrosshairUI`
  (the script that drives the reticle's interact-hint icon and prompt
  text) lived only on `SampleScene`'s `UIManager` object — the Lobby
  scene's Canvas never had an equivalent, so *no* interact prompt ever
  showed there for anything (including `SellStation`/`ReadySpot`,
  apparently never noticed before since those are usable "blind").
  Fixed by turning the visual `Crosshair` hierarchy (dot + interact
  hint + prompt text) into a shared prefab
  (`Assets/Prefabs/UI/Crosshair.prefab`) with `CrosshairUI` living
  directly on the prefab's own root, wired to its own children — one
  self-contained, reusable UI element instead of requiring a separate
  external manager script wired to it in every scene. `SampleScene`'s
  own `Crosshair` object was converted in-place into an instance of
  this same prefab; its old standalone `CrosshairUI` on `UIManager` was
  then redundant and removed.

### Follow-up: "Not enough cash" / "Nothing to sell" polish

Added alongside the bug fixes above, not originally scoped: a red
warning sub-line under the main prompt.

- New `IInteractableWarning` interface (`Assets/Scripts/Interaction/`)
  — an optional second line any interactable can supply. `CrosshairUI`
  reads it off the current target (if implemented) into a new
  **Warning Text** field, shown/hidden alongside the main prompt.
- `ShopShelfItem` implements it — shows **"Not enough cash!"** in red
  when an unlocked item costs more than you currently have.
- **Editor**: add a second `TextMeshProUGUI` child to the `Crosshair`
  prefab (below the existing prompt text), color it red, drag it into
  `CrosshairUI`'s new **Warning Text** field. One edit, applies to both
  scenes via the shared prefab.

### 🔴 Rest Point A
Batch 1: only the Taser shelf is buyable; every other shows "Locked
until Batch N". Buying deducts Cash and the item lands in your selected
hotbar slot. Empty-handed at the Sell Station shows "Nothing to sell...".
Standing at an affordable-but-locked shelf shows no warning; standing at
an unlocked shelf you can't afford shows red "Not enough cash!" under
the buy prompt. In the Lobby only: swinging a Bat repeatedly never
ticks down its uses counter; throwing a Dynamite/Hammer still
detonates/stuns but stays in your hotbar, with no duplicate landed
pickup. Walking the same items into `SampleScene` mid-round, normal
consumption resumes. Crosshair dot + prompt text work correctly in
*both* the Lobby and `SampleScene`.

---

## Milestone B — Real Jail & Bail — ✅ Done

Replaces `PoliceAI.CatchPlayer`'s instant "strip loot and finalize the
round" with a reversible **Jailed** state: lose your 5 slots
immediately (unchanged), get teleported to a real jail cell, and only
get finalized as `RoundResult.Caught` if the round ends before someone
bails you out. Also builds the separate end-of-batch quota-failure
jailing, with self-bail after one full round.

**New scripts**: `Assets/Scripts/Round/JailState.cs` (on the Player
prefab — synced jailed/end-of-batch flags + the rescue `IInteractable`,
in one component since nothing else needs them split), `Assets/Scripts/
Core/JailPoint.cs` / `JailExitPoint.cs` (pure locatable markers, same
spirit as `PlayerSpawnPoint.cs`, gameplay scene only).

**Key mechanics**:
- Mid-round catch: `RoundManager.NotifyPlayerCaught` still strips loot,
  but now calls `JailState.EnterJail(endOfBatch: false)` instead of
  resolving immediately. The round only actually ends once every player
  is *either* resolved *or* jailed (`RoundManager` gets a parallel
  `jailedPlayers` set alongside the existing `resolvedPlayers`) — a
  still-jailed player at that point is finalized as Caught then, or
  earlier if rescued first.
- Rescue: walk up, press E on the jailed player (`JailState.Interact`,
  same `IInteractable` pattern as everything else). **The rescuer gets
  paid, not the jailed player** — a flat Cash reward with no cost to
  anyone, `CurrentQuota / 6` for a mid-round bond, `CurrentQuota / 3`
  for an end-of-batch bounty (matching `gameplay-design.md`'s own
  formula). Both land at a `JailExitPoint` marker afterward.
- End-of-batch: anyone under quota when a batch ends gets queued and
  actually jailed (teleported, frozen) at the *start* of the new
  batch's round 1 — `GameFlowManager.HandleRoundStarted`, newly
  subscribed to `RoundManager`'s existing (currently unused)
  `OnRoundStarted` event.
- Self-bail: a `RoundOrdinal` counter on `GameFlowManager` (increments
  every round, batch or not) is the clock — an end-of-batch jailing
  auto-releases once a full round has passed unrescued.

Also added during playtesting, beyond the original plan: since being
jailed no longer needs to be a hard freeze (see the movement note
below), a jailed player can pop into a simple third-person spectate cam
(`Assets/Scripts/Round/SpectatorController.cs`) — **T** toggles it,
left-click cycles through non-jailed players to watch. `CrosshairUI`
shows "Press T to spectate" (or the stop/switch hint once active)
instead of the normal interact prompt while jailed.

**Editor**: your actual layout is 3 jail cells, 2 player slots each — so
6 `JailPoint` markers total (one per physical *slot*, not one per
cell), not just one. `GameFlowManager.ClaimJailPoint` hands out a free
slot per jailed player and tracks who's in which, so simultaneous
jailings spread across the layout instead of stacking. One
`JailExitPoint` marker (shared by everyone released) outside the
station's back door. Add `JailState` to the Player prefab (self-finds
its dependencies, nothing to wire). Add `SpectatorController` too —
needs a second Camera + Audio Listener child (disabled by default) for
the chase-cam, wired alongside the existing FPS camera/listener; see
the component's own tooltips/fields.

### Bugs found & fixed
- **`Interactor`'s raycast couldn't reach a jailed player at all** — the
  cell's own wall/bars collider sat between the raycast and whoever was
  inside, so no prompt ever showed. Fixed the same way `Interactor`
  already handles grabbing a ragdolled rival through its messy limb
  colliders: a collider-free fallback (`FindJailedPlayerNearby`) that
  just checks real distance/angle to every currently-jailed player.
- **E did nothing even once the prompt showed up.** `Interactor.
  CmdInteract` resolved the target with `GetComponent<IInteractable>()`
  — but a Player's root GameObject can carry *two* `IInteractable`s now
  (`PlayerTheftTarget` for robbing a ragdolled rival, `JailState` for a
  bail), and plain `GetComponent<T>` arbitrarily returns whichever is
  earliest in the component list regardless of which is actually valid.
  It was silently running `PlayerTheftTarget.Interact` (a no-op, since
  a merely-jailed player isn't also ragdoll-stunned) instead of
  `JailState.Interact`. Fixed by checking every `IInteractable` on the
  target and using whichever one's `CanInteract` is actually true right
  now.
- **Non-jailed players couldn't see into the cell, but the jailed
  player could see out.** Not a script bug — backface culling on the
  cell wall/bars material (normals facing outward render solid from
  outside, cull to invisible from inside). Fixed by setting the
  material's **Render Face** to `Both` instead of `Front`.
- **`PlayerCamera` had no `AudioListener`.** `FirstPersonController.
  OnStartClient` already expected one (disables it on every non-owned
  copy) — it just silently no-op'd since none existed. Added one
  directly to `PlayerCamera`. Also hardened that same disable logic to
  use `GetComponentsInChildren` (plural) instead of singular, now that
  `SpectatorController` adds a second Camera/AudioListener pair to the
  same prefab — singular would have arbitrarily picked only one of the
  two to disable on a remote copy, depending on hierarchy order.
- **Jailed players were fully frozen, including movement** — the
  original plan reused `FirstPersonController.IsFrozen` (a full input
  freeze) for jail. Changed so jail no longer freezes anything —
  physical cell geometry is what actually confines a jailed player now,
  and they can walk/look around inside it. A new local-only
  `SpectatingFrozen` flag (same shape as the existing `LookSuppressed`)
  pauses movement/look specifically while the third-person spectate cam
  above is active, since your real body shouldn't be wandering
  off-screen while you're watching someone else.
- **The end-of-round loading screen flashed for a fraction of a second
  with its spinner frozen on frame 0, and never showed at all for the
  Lobby -> gameplay transition.** `LoadingScreenUI` lived as a normal
  object inside `SampleScene` itself, which gets destroyed and recreated
  every round-trip -- it only ever survived the handful of frames
  between a `Show()` call and that scene's own teardown. Fixed by moving
  it to live as a child of the persistent `NetworkManager` object in
  `MainMenu.unity` (`Don't Destroy On Load`) instead. Also added a
  "Joining game..."/host's own "Loading..." cover for the initial
  connect (`RobEveryoneNetworkManager.OnClientConnect`/`OnStartHost`) --
  the host's variant needed its own dedicated hide trigger
  (`GameFlowManager.OnStartServer`) since a hosting client never gets a
  real `OnClientSceneChanged` callback for its own local connection the
  way a genuine remote client does.
- **The Lobby's `ReadySpot` silently stopped working.** Its
  `NetworkIdentity` had gone missing (likely lost while re-parenting it
  during level dressing) -- without one, Mirror never recognizes it as a
  real networked object at all, so `OnStartServer` (and therefore
  `GameFlowManager.RegisterReadySpot`) never ran. Re-added the
  `NetworkIdentity`.

### 🔴 Rest Point B
Get caught mid-round — slots empty, teleported to a free cell slot,
round keeps running for everyone else, and you can still walk around
inside the cell. Another player presses E on you — their Cash goes up
by `CurrentQuota / 6`, you're freed, both land at the jail exit, you can
keep playing that round. Press T while jailed — chase-cam follows
another player, click cycles targets, movement pauses until you press T
again. Get caught and *not* rescued before the timer — finalized
Caught, round ends normally. Force a batch to end under quota — jailed
at the new batch's round 1, Cash untouched, a rescue there pays
`CurrentQuota / 3`. Same but nobody rescues — self-bail releases you at
the start of the round after that.

---

## Milestone C — Homeowner patrol — ✅ Done

**What shipped**: `HomeownerAI` now has its own `NavMeshAgent` patrol
loop between hand-placed points, reusing `PoliceAI`'s exact
patrol-point-cycling pattern. Both non-Idle states go well beyond the
original plan's scope, worked out during playtesting once a real
character model made the old "just stop and do nothing" states read as
obviously broken:

- **Suspicious (yellow)**: stops in place and stares directly at
  whoever triggered it while still visible; if it loses sight, spins in
  place looking around for `suspiciousSearchDuration` (3s, tunable)
  before giving up and resuming patrol. A frozen first-frame pose
  (`Shoot_OneHanded`, driven by a new `Suspicious` Animator bool) makes
  it visibly look like it's pointing at whoever it's watching.
- **Alerted (red)**: flees to its own spawn point (captured
  automatically at startup, no marker needed) instead of standing and
  fighting. Waits there until the area's genuinely clear --
  `AreaClear()` checks every `PoliceAI` in the scene is back to plain
  `Patrol`, combined with no longer seeing the player, so it can't calm
  down mid-chase just because Police hasn't reacted yet on the very
  first frame. Only one hand-placed officer exists before Milestone D's
  dispatch pooling, so today this really just means "that one officer
  calmed down," but the check already generalizes correctly once more
  officers exist.

Both states also drive a full walk/point animation set now (see Bugs
below), not just color.

**Editor**: patrol points placed per house prefab. Added a
`NetworkTransformReliable` to each Homeowner (same fix `PoliceAI` needed
once for the identical reason -- frozen on every non-host client
without one).

### Bugs found & fixed
- **Alert status coloring stopped working after swapping the capsule
  placeholder for a real `BaseCharacter` model.** `HomeownerAI`'s
  `bodyRenderer` field still pointed at the old capsule's now-disabled
  `MeshRenderer` -- color changes landed on a renderer nobody could ever
  see. Fixed by auto-discovering every renderer under the Homeowner in
  `Awake` (`GetComponentsInChildren<Renderer>`) and tinting every
  material slot on each, instead of one hand-wired reference -- also
  makes this survive any future model swap for free.
- **The model sat half-buried in the ground.** The nested
  `BaseCharacter` model sits at local Y `-1` relative to the Homeowner
  root (its rig's own origin is at hip height, not its feet), but
  `NavMeshAgent.baseOffset` was still `0`. Set to `1` to compensate.
- **Going Alerted didn't actually stop movement.** Clearing the
  NavMeshAgent's path (`ResetPath()`) doesn't zero out whatever velocity
  it already had -- it kept coasting for a beat after a mid-patrol
  alert. Fixed by explicitly zeroing `agent.velocity` too.
- **Police's Animator only worked on the host, never on another
  client.** `PoliceAI` fed `agent.velocity.magnitude` into the Animator
  under the assumption it "reads correctly everywhere" -- it doesn't. A
  remote client's own `NavMeshAgent` never receives a destination (all
  the actual pathing is `[Server]`-gated), so it never simulates
  anything and its velocity sits at zero forever, even though the
  object visibly moves via `NetworkTransform`. The host doesn't hit this
  since host and server are the same process there. Fixed by measuring
  actual position change frame-to-frame instead
  (`Vector3.Distance(transform.position, lastPosition) / Time.
  deltaTime`), which is correct regardless of what's driving the
  Transform -- then `Mathf.SmoothDamp`'d before reaching the Animator,
  since the raw instantaneous value is noisy on an interpolated remote
  client and read as jerky blending otherwise. Applied to both
  `PoliceAI` and `HomeownerAI`'s own new Speed feed.
- **`Interactor.CmdInteract` resolved the wrong `IInteractable`.**
  Same root cause as one of Milestone B's own bugs (`GetComponent<T>`
  arbitrarily picking whichever matching component is earliest, not
  whichever is actually valid) -- fixed there already applies here too,
  no separate change needed.

### 🔴 Rest Point C
Two-Editor test — a Homeowner visibly patrols between its points on
*both* clients, with a smooth walk animation on each, not just the
host's. Getting seen freezes it staring/pointing at you (yellow); break
line of sight and it spins looking around for a few seconds before
resuming patrol. Fully alerting it (red) sends it fleeing to its own
spawn point, where it waits until Police calms back down before
resuming patrol.

---

## Milestone D — Police dispatch pooling — ⬜ Not started

Replaces the single hand-placed `PoliceAI` reacting to every alert with
a spawnable/poolable prefab, dispatched per `HomeownerAI.OnAlertRaised`
up to a cap that scales with player count (proposed default: `Max(2,
Ceil(playerCount / 2))`, tune later). New `Assets/Scripts/AI/
PoliceDispatcher.cs`, reusing `HousePoolSpawner.cs`'s exact `Instantiate`
+ `NetworkServer.Spawn` runtime-spawn pattern. The cap only gates *new*
dispatch spawns — already-active officers (including a kept hand-placed
baseline one) keep reacting to every alert independently, as they do
today.

**Editor**: turn the current hand-placed `PoliceAI` into a real prefab
(`Assets/Prefabs/AI/PoliceOfficer.prefab`) with a `NetworkIdentity`,
register it in `NetworkManager`'s Spawnable Prefabs list. Create a
`PoliceDispatcher` object in `SampleScene`, wire the prefab + station
spawn points.

### 🔴 Rest Point D
Two-Editor test with 2+ players — trigger 3+ simultaneous alerts across
different houses, confirm multiple distinct officers spawn and each
heads to its own alert up to the computed cap; a 4th simultaneous alert
past the cap gets no *new* officer.

---

## Milestone E — Night mode — ⬜ Not started

The last round of every 3-round batch (`roundInBatch == 3`,
`GameFlowManager.IsNightRound`) is a deterministic night round:

1. **Homeowners go inert** — no suspicion/vision-cone logic and no
   patrol movement at all for the whole round; warped to a `bedSpot`
   marker instead of starting patrol.
2. **Police move faster** — `nightSpeedMultiplier` (proposed `1.4×`)
   applied everywhere `patrolSpeed`/`chaseSpeed` gets assigned.
3. **Police see farther and chase longer** —
   `nightViewDistanceMultiplier` (proposed `1.5×`) on the vision-cone
   distance check, `nightLoseInterestMultiplier` (proposed `2×`) on how
   long they keep chasing after losing line-of-sight.
4. **More police can be dispatched** — `PoliceDispatcher`'s cap gets a
   night-only bonus (proposed `+2`) on top of Milestone D's player-count
   scaling.
5. **Visual swap** — new `Assets/Scripts/World/NightModeVisuals.cs`
   (plain, unnetworked — every client independently reads the
   already-synced `IsNightRound` flag) swaps the skybox/sun intensity
   at scene start. Secondary/atmospheric, don't let it block testing
   1-4.

**Editor**: source a night skybox (Kenney Skyboxes pack, alongside the
existing day one), wire `NightModeVisuals` to the scene's Directional
Light. Author `bedSpot` markers per house.

### 🔴 Rest Point E
Play a full 3-round batch. Rounds 1-2 normal. Round 3: every Homeowner
inert and posed at its bed; Police visibly faster, spot from farther,
chase longer after losing line-of-sight; more simultaneous officers
dispatchable than a day round; skybox/lighting visibly different,
reverts for the next batch's round 1.

---

## Milestone F — Rival-status HUD ping — ⬜ Not started

A ping when a rival gets spotted/Alerted by a Homeowner **or** chased
by Police — both halves. `HomeownerAI` gets a new ping-only static
event (`OnRivalSpotted`, always carries a name, unlike the existing
`OnAlertRaised` whose `blamed` stays deliberately null for an organic
sighting — don't touch that, `PoliceAI`'s dispatch logic depends on it).
`PoliceAI` gets `OnRivalChased`, fired from `EnterChase`, broadcast via
`ClientRpc` so every client sees it. New `Assets/Scripts/UI/
RivalPingUI.cs` listens to both, shows a short-lived text ping, skips
pinging about yourself.

**Editor**: one TMP text element added to `SampleScene`'s gameplay
Canvas, wired to `RivalPingUI`.

### 🔴 Rest Point F
Two-Editor test — Player 1 seen by a Homeowner (organic sighting),
confirm Player 2's HUD pings Player 1's name; Player 1 chased by
Police, confirm the same; confirm Player 1's own HUD never pings about
themselves.

---

## Milestone G (optional, lowest priority) — Randomized item value range — ⬜ Not started

`gameplay-design.md`'s "value randomized per pickup within a range"
(e.g. Jewelry $60–140) instead of `ItemDefinition`'s current single
fixed `value`. More invasive than it sounds — the rolled value has to
be captured once at spawn/drop and carried per-slot, the same way
`slotUses` already parallels `slotItemNames`. Touches `ItemDefinition`
(`minValue`/`maxValue`, both `-1` = "use the fixed value", fully
backward-compatible), `PlayerInventory` (a new parallel `SyncList<int>
slotRolledValue`), `LootSpawnPoint` (roll once at spawn), `PickupItem`
(carry the rolled value through `Initialize`).

### 🔴 Rest Point G
Pick up the same item type from two different houses with a configured
range — sale price differs between the two but stays within range;
every item still at `-1/-1` behaves exactly as before.

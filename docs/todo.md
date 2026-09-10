# To-dos

Everything genuinely open right now, pulled together from scattered
"still open"/"not done"/"remaining" notes across the individual stage
docs (several of which had drifted stale — see
[completed.md](completed.md)'s note on that). Roughly ordered by "should
happen soon" to "later stage."

## Priority order (the plan)

1. ~~**Inventory / UX**~~ — done and playtested
   ([completed.md](completed.md)).
2. ~~**Player feel pass**~~ — done: instant jump, CS bhop + autohop +
   speed cap, first-person visible body
   ([player-feel-setup.md](stages/player-feel-setup.md)).
3. **Held item + carry/run animations** — selected hotbar item shows in
   the character's hand; Animator gains sprint / carry-bulky /
   carry-body states.
4. **Carry / throw ragdolled players** — un-ragdoll-too-fast bug fixed;
   carry mechanic **code done**, Editor wiring + playtest remain
   ([ragdoll-carry-setup.md](stages/ragdoll-carry-setup.md)).
5. **Finish Stage 6 Phase 2** — Alarm Clock build + full two-Editor
   Phase 2 playtest.
6. **Stage 5 real Steam overlay test** — parked until a second Steam
   account is available; not blocking anything else.
7. **Stage 7 — full meta-game** (medium) — sabotage purchases, real
   Jail & Bail, sabotage-spending quota.
8. **VoIP / proximity voice chat** (medium-low) — Steam's own voice API.
   Build guide: [voip-setup.md](stages/voip-setup.md).
9. **Stage 8 — friend-group playtest** — depends on 3–7.
10. **Art & audio** — more house variants (unblocks loot variety), all
    SFX, ambient music, "Good House" tell, skin unlock-gating, HUD
    result banner, environmental detail. Mostly Zach / asset work.
11. **Housekeeping + code-review nits** — as they come up.

Detail for each below.

## Done — Inventory / UX + Player feel pass

- **Inventory / UX** — Tab / steal screen, drop-with-Q, Prison Wallet,
  steal-window rework. **Playtested.**
  [inventory-ux-setup.md](stages/inventory-ux-setup.md), detail in
  [completed.md](completed.md). Deferred follow-ups (see the setup
  doc's list): swap-on-drag, right-click quick-drop, real Steam names in
  the "Steal from:" label, auto-banking an unretrieved wallet item,
  making the wallet item off-limits to theft.
- **Player feel pass** — instant jump (animation no longer gates it),
  CS-style bhop with a `Max Air Speed` ceiling + hold-Space autohop,
  first-person visible body with grounded/airborne bone trimming and a
  restore while the camera's cut away.
  [player-feel-setup.md](stages/player-feel-setup.md). Deferred
  follow-ups: arm/shoulder clipping if it's bad (nudge camera / trim
  more bones), a real separate first-person viewmodel.

## Do first — held item + carry/run animations

- **Current hotbar item visible in your hands** — whatever slot is
  selected shows its `WorldModelPrefab` held in the character's hand
  (a hand/wrist bone socket), for you in first person (the body trim
  keeps the arms) and for everyone else in third person. Ties into the
  `FirstPersonBodyTrim` work and the eventual drop/throw-from-hand.
- **Running + carry animation states** — the Animator only blends
  Idle/Walk/Run/Jump right now. Needs at least: a proper sprint state
  (distinct from Run), a "carrying something bulky" pose/gait
  (multi-slot item held two-handed?), and — once the ragdoll carry
  mechanic exists — a "carrying a body" state. Animator-controller work
  plus clips.

## Do first — finish Stage 6 Phase 2

## Do first — finish Stage 6 Phase 2

- **Alarm Clock build + full Phase 2 playtest** — Parts 1–4 of
  [stage6-sabotage-items-phase2-setup.md](stages/stage6-sabotage-items-phase2-setup.md)
  are done (hotbar uses-count UI, `PlayerTheftTarget` on the Player
  prefab, `HammerProjectile.prefab` built and registered). Only **Part 5**
  remains: build `AlarmClockProjectile.prefab`, wire it into
  `AlarmClock.asset`'s Thrown Projectile Prefab + `NetworkManager`'s
  Spawnable Prefabs. Then a real two-Editor playtest of all of Phase 2
  (Bat, Hammer swing/throw, Tranq Gun, Alarm Clock, the steal-window)
  before it's done.

## Known bugs / deeper dives

- ~~**Player un-ragdolls too fast after a car hit**~~ — fixed:
  `defaultStunDuration` bumped to 3.5 s, and `ImpactSequence` now waits
  past that until the hips have been near-still for `settleHoldTime`
  (hard-capped at `+settleTimeoutExtra`), so you can't stand up
  mid-air/mid-tumble. `IsStunned` stays a fixed server timer, but the
  owner's controller is disabled for the whole (possibly longer)
  ragdoll regardless, so it doesn't matter if the two drift on a big
  launch. Tuning knobs on `Player Ragdoll`. Retest with a fast car hit.
- **Carry / throw ragdolled players** — **code done**, Editor wiring +
  playtest remain: [ragdoll-carry-setup.md](stages/ragdoll-carry-setup.md).
  `E` on any ragdolled rival to hoist them (floppy in your hands),
  walk them around (no bhop while carrying), `G` to set down, hold LMB
  to charge a throw. `Carryable` / `CarryController` + hooks in
  `PlayerRagdoll` / `PlayerImpactRelay` / `PlayerTheftTarget` (theft
  protection) / `FirstPersonController` / `SabotageUseController`.

## Test when able

- **Stage 5 real Steam overlay test** — every Rest Point through
  `stage5-steam-multiplayer.md` Part 3 is done (Steamworks.NET +
  FizzySteamworks installed, `SteamLobby`/`SteamManager` wired, Host
  button correctly calls `SteamLobby.HostLobby()`, `DISABLESTEAMWORKS`
  removed from Scripting Define Symbols). **Rest Point 4 — the actual
  "two separate Steam accounts, real overlay invite" test — has not been
  run**: no second account/friend was available to test with. Assumed
  working based on Steam initializing cleanly and every earlier Rest
  Point passing, but needs a real two-account (or two-machine) pass to
  actually confirm the overlay invite → join → full batch flow, per that
  doc's own Rest Point 4 checklist, before this is genuinely "done."

## Art & audio (see art-info.md for full detail)

- **Loot variety feels repetitive in play** — not an RNG bug (confirmed
  by reading the actual house prefabs/tables): 33 `ItemDefinition`s
  exist now, but only 2 house prefab variants
  (`Real_House_01`/`Real_House_02`) fill all 15+2 house slots, **both**
  wired to the same `LootTable_Medium` (13 items), and each house has
  only 1 `LootSpawnPoint` — one roll per house. `LootTable_Small` (13
  items) and `LootTable_Large` (7 items) aren't referenced by any house
  at all right now. Fixing this for real needs more house variants
  (blocked on Zach's house pool work below) — once those exist, revisit
  which table each house tier uses and whether 1 spawn point per house
  is enough, rather than just patching the 2 current houses in
  isolation.
- **"Good House" visual tell** — should read as visually distinct at a
  glance (gold accent trim, lighting, signage) since it's deliberately
  higher-risk/higher-reward. Not confirmed done.
- **SFX** — none implemented yet. Confirmed trigger list in
  `art-info.md`: Homeowner state-transition stingers, police siren
  (spatial), per-movement-state footsteps, item pickup/caught/jailed,
  car horn + driver "yelling" line on traffic-hazard impact (fields
  already exist on `CarDriver.cs`, just need clips).
- **Ambient music** — calm exploration + tense "spotted" loop,
  crossfading off the existing Homeowner/Police alert state transitions.
  Not built.
- **Skin unlock-gating** — every configured skin is currently pickable;
  the design calls for unlocks eventually (`ui-design.md`).
- **Sabotage item SFX** — no use/impact SFX exist for any sabotage item
  yet (Taser zap, Dynamite blast, Bat/Hammer thwack, Tranq dart, Alarm
  Clock ring). Icons aren't needed — the hotbar uses the 3D model as its
  preview, same as regular loot.
- **HUD result banner** — `RoundUI.cs`'s `resultText` is still plain
  text. Lower priority now that round-end flows into a loading screen +
  Lobby scene rather than lingering on this screen — worth re-scoping
  before building it rather than building the originally-designed
  version blind.
- **Environmental detail pass** (clutter, lighting bake, foliage) — later
  polish, once the map layout is fully settled.

## Bigger stages (per plan.md's build order)

- **Stage 4 — multiplayer, same machine** — Mirror over `localhost`.
  **Done, all 9 Rest Points confirmed working** (two-Editor ParrelSync
  testing) — see [completed.md](completed.md) for the full bug list
  found/fixed along the way.
- **Stage 5 — Steam multiplayer** — Steamworks.NET + FizzySteamworks,
  test AppID 480. Editor setup done through Part 3; the real overlay
  invite test (Rest Point 4) still needs a second Steam account — see
  the "Test when able" section near the top of this doc.
- **Stage 6 Phase 1 — sabotage foundation, Taser + Dynamite — done and
  tested.** All 6 items have a prefab + `ItemDefinition`, in
  `ItemCatalog`/Spawnable Prefabs. `ItemDefinition` gained a
  `SabotageType` (Melee/Thrown) plus stun/force/cooldown/range/
  blastRadius/projectile fields; `PlayerRagdoll`/`PlayerImpactRelay` take
  a configurable stun duration, with `PlayerImpactRelay.IsStunned` as a
  real server-synced flag (also fixed the bug where a police-frozen
  player mid-stun could get control handed back early); new
  `Assets/Scripts/Sabotage/` folder (`SabotageUseController`,
  `SabotageProjectile`) wires left-click to melee/throw, confirmed
  working two-Editor (Taser melee hit/cooldown/no-restack, Dynamite
  throw/AOE/consumed-on-use, all per
  [stage6-sabotage-items-setup.md](stages/stage6-sabotage-items-setup.md)'s
  Part 7). Also fixed along the way: `Player.prefab`'s
  `CharacterController` (`Height`/`Center`) was undersized for its own
  camera height, so head-height hits (Taser, and implicitly Police
  vision/anything else raycasting the player) silently missed — now
  `Height: 3, Center: (0, 0.5, 0)`, chosen so the capsule top clears eye
  height with margin while the origin (and camera, a fixed child offset
  from it) lands at exactly the original height; `DynamiteProjectile.prefab`
  had its `NetworkTransformReliable` left at the default Client-To-Server
  sync instead of Server-To-Client, breaking it for non-host clients; and
  `GameFlowManager` gained a periodic server-side fall-through safety net
  (any player below Y `-20` gets teleported to a spawn point, skipping
  anyone still mid-ragdoll so `PlayerRagdoll.EndRagdoll` doesn't fight
  the rescue) for the edge case where ragdoll physics carries a player
  off the map.
- **Stage 6 Phase 2 — Bat, Hammer, Tranquilizer Gun, PvP steal-window,
  Alarm Clock framing — code done, Editor setup + playtesting not done
  yet.** `ItemDefinition` gained `MaxUses` (per-slot durability/ammo) and
  `SabotageType` became `[Flags]` (`Ranged` added, Hammer is
  `Melee | Thrown`) — every exact-value `SabotageType` comparison across
  `SabotageUseController` was rewritten to `HasFlag`. `PlayerInventory`
  gained a parallel `slotUses` SyncList, an `AddItem(item, overrideUses)`
  overload, and `DecrementUses`. `PlayerImpactRelay` gained `IsStealable`
  + `ServerApplyPvpImpact` (a car impact still only calls the plain
  `ServerApplyImpact` — no steal window from that). The E-key
  `IInteractable` pipeline gained a `CanInteract` check, and a new
  `PlayerTheftTarget` component makes a stunned player themself a valid
  steal target through that same existing pipeline. New `ILaunchable`
  interface abstracts "thing a thrown item spawns" — `SabotageProjectile`
  (Dynamite, unchanged) and the new `RetrievableProjectile` (Hammer:
  collision-triggered, lands and respawns as a real pickup carrying its
  remaining uses via a new `PickupItem.Initialize(item, uses)` overload)
  both implement it. `HomeownerAI` gained `ForceAlert(position, blamed)`
  and `OnAlertRaised` now carries a `PlayerInventory` to blame (`null` for
  every existing organic sighting); `PoliceAI.HandleAlertRaised` chases a
  blamed player directly via `EnterChase` instead of just moving toward a
  position. New `Assets/Scripts/Sabotage/AlarmClockProjectile.cs`
  resolves who's nearby to frame and which Homeowner to alert. **Editor
  setup Parts 1–4 done** (hotbar uses-count UI, `PlayerTheftTarget` on
  the Player prefab, `HammerProjectile.prefab` built + registered). Only
  Part 5 (`AlarmClockProjectile.prefab` build + wiring) and a full
  two-Editor playtest remain — see the "finish Stage 6 Phase 2" item
  near the top of this doc. Reference:
  [stage6-sabotage-items-phase2-setup.md](stages/stage6-sabotage-items-phase2-setup.md),
  [item-creation.md](stages/item-creation.md)'s Section 4b, and
  `gameplay-design.md`'s Sabotage items section for intended numbers.
- **Stage 7 — full meta-game (medium priority)** — the v1 shop/lobby
  loop and batch economy above are a deliberately scoped-down slice.
  Still missing: sabotage purchases, real Jail & Bail
  (rescue/bond/self-bail), the personal sabotage-spending quota add-on.
  See `gameplay-design.md` for the full design.
- **Stage 8 — playtest with the friend group** — not started, depends on
  Stage 4-6 existing.
- **VoIP / in-game proximity voice chat (medium-low priority)** — not
  started. Full build plan now written up:
  [voip-setup.md](stages/voip-setup.md) — Steam's own voice API
  (`SteamUser.*Voice*`, already available via the Stage 5 Steamworks.NET
  install) captured locally, relayed as ordinary Mirror Rpcs over the
  FizzySteamworks connection, played back through a 3D `AudioSource` on
  each speaker's player object so distance attenuation is automatic. The
  doc settles the design calls (proximity not team-wide, push-to-talk,
  unreliable channel, never hear yourself); the "team channel" toggle
  stays a Stage 8 question.

## Housekeeping

- **Zach's house pool work** — still hasn't started building the
  additional `Real_House_0X` variants; `HouseDesigner` scene exists to
  make that faster whenever he picks it up.
- **New Kenney packs** — remember the FBX import Scale Factor fix
  (`15`, matching `Kenney-CityKitSuburban`) before assuming a freshly
  imported pack (e.g. Industrial, Car Kit) "looks tiny" for some other
  reason.

## Code-review nits (Stage 6 sabotage read-through)

Minor, none blocking — surfaced reviewing the Phase 1/2 commits.

**Fixed** in the inventory/UX pass:
- ~~`PlayerImpactRelay` class comment stale~~ — rewritten.
- ~~`ServerApplyPvpImpact` opening the steal window on a no-op stun~~ —
  now explicitly commented as deliberate.
- ~~Multi-attacker steal-window race~~ — the window is an expiry
  timestamp now, not racing coroutines.
- ~~Theft not discriminating what it takes~~ — moot: the thief now
  drag-picks the item off the victim's hotbar (still *can* take gear;
  making it loot-only is a one-liner if playtesting wants it — tracked
  in the setup doc's follow-ups).

**Still open:**
- **Sabotage cooldowns never reset between rounds** —
  `SabotageUseController.nextReadyTime` keys off `Time.time`, continuous
  across the Lobby round-trip, so a Taser fired near the end of a round
  can still be on cooldown at the start of the next. Low impact; a
  `ClearCooldowns()` from round start would be tidy.

# To-dos

Everything genuinely open right now, pulled together from scattered
"still open"/"not done"/"remaining" notes across the individual stage
docs (several of which had drifted stale — see
[completed.md](completed.md)'s note on that). Roughly ordered by "should
happen soon" to "later stage."

## Do first — inventory / UX (high priority)

This gates how looting a downed player actually feels. Stage 6's PvP
steal-window (`PlayerTheftTarget`, stun a rival → press E → take one
item) is in but half-formed, and once you've taken something there's no
way to rearrange or drop what you end up carrying.

- **Inventory screen (Tab)** — not built. Shows the mouse in-game,
  click-and-drag to move an item between slots (including a multi-slot
  item — dragging it should move its whole span, not just one cell of
  it). Real UI work: needs its own drag-and-drop system, separate from
  the hotbar's existing number-key/scroll selection. This is also where
  a stolen item should land in a way the player can see and manage.
- **Drop item (hold + press Q)** — not built. A dropped item should look
  the same as it does in the hotbar preview (spinning, slightly
  floating) when it lands in the world, not disappear or revert to a
  plain static object. Ties into the same "selected slot represents
  what's in your hands" direction that shaped `AddItem`'s block-not-
  fallback behavior (see `PlayerInventory.cs`'s own comment) — worth
  designing both together rather than dropping first and reconciling
  later.
- **Prison Wallet slot** — gameplay-design.md's 6th, separate inventory
  slot: holds exactly 1 item of any size/value, immune to whatever
  happens to the other 5 when caught. Deliberately not built alongside
  `InventorySize` (see [item-creation.md](stages/item-creation.md)) since
  it needs its own design pass through `PoliceAI`/`RoundManager`'s catch
  handling (what does "immune" actually do to a caught player's
  inventory) and `SellStation` (can you sell straight out of the wallet,
  or does it need moving to a normal slot first) -- not just a UI slot.
- **Revisit the steal-window design** — the code-review nits at the
  bottom of this doc (theft not discriminating loot from equipped
  sabotage gear, resetting a used item's durability, the multi-attacker
  race) are all in `PlayerTheftTarget`/`PlayerImpactRelay` and worth
  settling in the same pass as the inventory screen.

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
  started, not designed yet. Steamworks.NET exposes Steam's own voice API (`SteamUser`
  `StartVoiceRecording`/`GetVoiceData`/`DecompressVoice`), which would
  fit naturally alongside the existing FizzySteamworks transport
  (`stage5-steam-multiplayer.md`) without pulling in a separate
  networking library — worth checking that route first before reaching
  for a third-party voice SDK. Proximity (falls off/mutes with distance,
  scoped per player like `HomeownerAI`/`PoliceAI`'s vision checks) vs.
  always-on team-wide chat is an open design question, not just an
  implementation one — probably worth a real design pass alongside
  Stage 8 rather than bolting it on ad hoc.

## Housekeeping

- **Zach's house pool work** — still hasn't started building the
  additional `Real_House_0X` variants; `HouseDesigner` scene exists to
  make that faster whenever he picks it up.
- **New Kenney packs** — remember the FBX import Scale Factor fix
  (`15`, matching `Kenney-CityKitSuburban`) before assuming a freshly
  imported pack (e.g. Industrial, Car Kit) "looks tiny" for some other
  reason.

## Code-review nits (Stage 6 sabotage read-through)

Minor, none blocking — surfaced reviewing the Phase 1/2 commits, worth
folding into whatever pass revisits sabotage next.

- **`PlayerImpactRelay` class comment is stale** — still says "Only the
  server ever calls `RpcApplyImpact` (from CarDriver's own isServer-
  gated impact detection)". It's now `private`, reached via
  `ServerApplyImpact` / `ServerApplyPvpImpact`, and called from the
  sabotage path too, not just `CarDriver`.
- **Sabotage cooldowns never reset between rounds** —
  `SabotageUseController.nextReadyTime` keys off `Time.time`, which is
  continuous across the Lobby round-trip, so a Taser fired near the end
  of a round can still be on cooldown at the start of the next. Short
  windows, low impact, but a `ClearCooldowns()` called from round start
  would be tidy.
- **`ServerApplyPvpImpact` opens the steal window even when the stun
  no-ops** — if the target is already stunned (e.g. by a car), the
  inner `ServerApplyImpact` early-returns but `IsStealable` still gets
  set. Probably fine/desirable (they're already down), but it's an
  implicit decision worth making on purpose.
- **Multi-attacker steal window race** — two PvP hits on the same
  target start two independent `ClearStealableAfter` coroutines; the
  first to fire closes the window early for the second attacker.
- **Theft doesn't discriminate what it takes** —
  `PlayerTheftTarget.Interact` uses plain `AddItem(stolen)` (a
  partially-used item resets to full durability) and
  `FindFirstOccupiedSlot` will happily steal an equipped sabotage item,
  not just loot. Consider preferring highest-value loot and/or skipping
  sabotage gear — a design call, not just a code one.

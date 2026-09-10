# To-dos

Everything genuinely open right now, pulled together from scattered
"still open"/"not done"/"remaining" notes across the individual stage
docs (several of which had drifted stale — see
[completed.md](completed.md)'s note on that). Roughly ordered by "should
happen soon" to "later stage."

## In progress — do this first

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

- **Prison Wallet slot** — gameplay-design.md's 6th, separate inventory
  slot: holds exactly 1 item of any size/value, immune to whatever
  happens to the other 5 when caught. Deliberately not built alongside
  `InventorySize` (see [item-creation.md](stages/item-creation.md)) since
  it needs its own design pass through `PoliceAI`/`RoundManager`'s catch
  handling (what does "immune" actually do to a caught player's
  inventory) and `SellStation` (can you sell straight out of the wallet,
  or does it need moving to a normal slot first) -- not just a UI slot.

- **Drop item (hold + press Q)** — not built. A dropped item should look
  the same as it does in the hotbar preview (spinning, slightly
  floating) when it lands in the world, not disappear or revert to a
  plain static object. Ties into the same "selected slot represents
  what's in your hands" direction that shaped `AddItem`'s block-not-
  fallback behavior (see `PlayerInventory.cs`'s own comment) — worth
  designing both together rather than dropping first and reconciling
  later.
- **Inventory screen (Tab)** — not built. Shows the mouse in-game,
  click-and-drag to move an item between slots (including a multi-slot
  item — dragging it should move its whole span, not just one cell of
  it). Real UI work: needs its own drag-and-drop system, separate from
  the hotbar's existing number-key/scroll selection.

## Verify / playtest (built but not confirmed)

- **Stage 7b (batch economy + hotbar)** — code and Editor wiring both
  landed in the same commit, but unlike Stage 7's v1 shop/lobby loop,
  there's no explicit "playtested and confirmed working" note anywhere
  for the ×1.5 batch growth, surplus wipe, or the 5-slot hotbar. Play a
  full 3-round batch and confirm all of it end to end.
- **`Real_House_02`** — exists in `Assets/Prefabs/Houses/`, but there's
  no record of it going through the same solo test every other house
  prefab got (door, loot, homeowner, walk back out) the way
  `stage3e-house-prefabs.md`/`stage3f-house-pool.md` describe for
  `Real_House_01`.
- **Ragdoll batch results** — `RagdollBatchTool.cs` processed all 52
  player skins, but per `stage3j-traffic-hazard.md`'s own step 4b:8,
  only a handful were spot-checked, not all 51 non-template ones. Pick a
  few more (not just the first alphabetically) and confirm bones got a
  Rigidbody/Collider/CharacterJoint, `Is Kinematic` is on, and it looks
  right in Play mode.

## Small cleanup

- Delete the redundant `BaseCharacter_Ragdoll.prefab` (harmless but
  unused — `PlayerSkinRoster`'s `BaseCharacter` entry points at the
  original hand-built prefab instead).
- Wire the character-selection UI to something other than the
  placeholder first-skin default (`PlayerCosmeticSelection.SkinIndex`
  defaults to `0`) — the main-menu customization screen already lets you
  pick one, so check whether this is actually still open or already
  covered by that.
- A few docs have stale inline status callouts now superseded by
  [completed.md](completed.md) — not urgent to fix since completed.md
  and this doc are the current source of truth, but worth trimming next
  time one of those specific docs is being read anyway (e.g.
  `stage3h-map-dressing.md`'s "still open" line, `stage3j`'s "5 skins
  not done yet" line, `art-info.md`'s "skybox not wired up" line).

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
- **Sabotage item icons + models + SFX** — taser, hammer, alarm clock,
  bat. Blocked on Stage 6 actually starting (see below).
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
  the top of this doc's "In progress" section.
- **Stage 6 — sabotage items** — taser, hammer, alarm clock, bat,
  tranquilizer gun, dynamite (AOE stun/ragdoll). **Assets done**: all 6
  have a prefab (`NetworkIdentity`/`PickupItem`/fitted collider) +
  `ItemDefinition` (scale tuned, in `ItemCatalog`, registered as
  Spawnable Prefabs) — mechanically real, spawnable, pick-up-able,
  networked. Hammer's role is decided: dual-mode like the Bat but also
  throwable, same durability pool either way (see `gameplay-design.md`).
  **Phase 1 code landed**: `ItemDefinition` gained a `SabotageType`
  (Melee/Thrown) plus stun/force/cooldown/range/blastRadius/projectile
  fields; `PlayerRagdoll`/`PlayerImpactRelay` now take a configurable
  stun duration and `PlayerImpactRelay.IsStunned` is a real server-synced
  flag (also fixes a bug where a police-frozen player mid-stun could get
  control handed back early); new `Assets/Scripts/Sabotage/` folder
  (`SabotageUseController`, `SabotageProjectile`) wires left-click to
  melee/throw. Taser and Dynamite are the 2 items wired through this
  path end to end. **Still needed before this is playable**: the Editor
  half, written up step by step in
  [stage6-sabotage-items-setup.md](stages/stage6-sabotage-items-setup.md)
  (add `SabotageUseController` to the Player prefab, tune Taser/
  Dynamite's new sabotage fields, build the `DynamiteProjectile.prefab`
  and register it in `NetworkManager`'s Spawnable Prefabs, hand-place
  test pickups) plus actual two-Editor playtesting — none of that has
  been done yet. Bat/Hammer/Tranquilizer
  Gun/the PvP steal-window/Alarm Clock framing are Phase 2, not started
  (Hammer's thrown-and-retrievable mode and Bat/Hammer/Tranq Gun's
  durability/ammo all need a new "remaining uses per slot" concept that
  doesn't exist yet). See [item-creation.md](stages/item-creation.md)'s
  Section 4b for the asset pipeline and `gameplay-design.md`'s Sabotage
  items section for each item's intended numbers.
- **Stage 7 — full meta-game** — the v1 shop/lobby loop and batch economy
  above are a deliberately scoped-down slice. Still missing: sabotage
  purchases, real Jail & Bail (rescue/bond/self-bail), the personal
  sabotage-spending quota add-on. See `gameplay-design.md` for the full
  design.
- **Stage 8 — playtest with the friend group** — not started, depends on
  Stage 4-6 existing.

## Housekeeping

- **Zach's house pool work** — still hasn't started building the
  additional `Real_House_0X` variants; `HouseDesigner` scene exists to
  make that faster whenever he picks it up.
- **New Kenney packs** — remember the FBX import Scale Factor fix
  (`15`, matching `Kenney-CityKitSuburban`) before assuming a freshly
  imported pack (e.g. Industrial, Car Kit) "looks tiny" for some other
  reason.

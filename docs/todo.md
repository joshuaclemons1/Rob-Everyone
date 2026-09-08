# To-dos

Everything genuinely open right now, pulled together from scattered
"still open"/"not done"/"remaining" notes across the individual stage
docs (several of which had drifted stale — see
[completed.md](completed.md)'s note on that). Roughly ordered by "should
happen soon" to "later stage."

## In progress — do this first

- **Stage 4/5 multiplayer (Mirror + Steam)** — code for full system sync
  is written (`stage4-multiplayer-mirror.md`, `stage5-steam-multiplayer.md`),
  but **none of it has been opened in the Editor, compiled, or tested**.
  This is a huge, invasive change (12+ scripts converted to
  `NetworkBehaviour`, `PartyGate.cs` deleted and folded into
  `RoundManager`, several UI scripts changed from Inspector-wired to
  runtime-resolved) — go through both docs' Rest Points in order and
  report back per-Part rather than trying to power through both docs in
  one sitting.

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

- **Loot item variety** — only one `ItemDefinition` exists (`Laptop`).
  The loot table system works, but needs more items (watch, cash,
  jewelry, etc. — simple modeled props + a Photoshop icon is enough, per
  `art-info.md`) to actually feel varied in play.
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
  Not started at all.
- **Stage 5 — Steam multiplayer** — Steamworks.NET + FizzySteamworks,
  test AppID 480. Not started.
- **Stage 6 — sabotage items** — taser, hammer, alarm clock, bat,
  networked. Not started (also blocked on Stage 4/5 for the "networked
  correctly" part).
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

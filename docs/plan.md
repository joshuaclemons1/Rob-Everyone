# Rob Everyone — Dev Plan

Full designed version with the map sketch and visual layout: https://claude.ai/code/artifact/e48a1ffb-47e8-47e6-ba90-dfb7b4a1d4f3

## Where to pick up next

**Stage 3h is done** — full-loop playtest confirmed working on the real
layout (roads, fenced compound, jail interior, exit), and the 2 Good
House slots were repositioned to actually read as inside/adjacent to the
finished compound. **Stage 3i** (skybox, background skyline, forest ring
+ world boundary, background hills) is done, scripts written and working.
Currently on **Stage 3j** (traffic hazard — cars that drive the road loop
and knock the player down) before moving to Stage 4 — see
[stage3j-traffic-hazard.md](stage3j-traffic-hazard.md); scripts are
written, Editor setup (prefabs, waypoints, spawn manager) not done yet.

Done so far:

- **Stage 3d** — Homeowner and Police reskinned with real Quaternius
  characters (capsule collider kept, Mesh Renderer hidden on the swap
  target). `HomeownerAnimator` (Idle-only) and `PoliceAnimator`
  (Idle/Walk/Run Blend Tree, driven by `PoliceAI.cs` feeding
  `agent.velocity.magnitude` into a `Speed` param each frame) exist under
  `Assets/Art/Characters/Animators/`.
- **Stage 3e** — `Real_House_01` built and proven: real Kenney building +
  yard padding to the 40×40 plot size + Kenney Furniture Kit interior +
  nested `Homeowner` + loot spot, all as one self-contained prefab at
  `Assets/Prefabs/Houses/Real_House_01.prefab`. Also added
  `DoorTeleporter` (`Assets/Scripts/World/DoorTeleporter.cs`) — a
  paired-trigger doorway workaround for buildings without a real modeled
  door gap, so building colliders never need hand-fitting.
- **Stage 3f** — house pool still just `Real_House_01` (Zach owns adding
  more variants, not started yet) — everything downstream was built to
  not require more than one prefab, so this isn't blocking anything.
- **Stage 3g** — `HousePoolSpawner.cs` (`Assets/Scripts/World/`) randomly
  assigns a prefab per slot on `Start()`; `HomeownerAI`/`PoliceAI` fall
  back to auto-finding the player if `Player Target` isn't hand-wired, so
  runtime-spawned houses work with zero manual Inspector wiring. Full
  rectangular-ring layout (15 outer house slots + 2 Good House slots + a
  3-building compound stack) matched to the team's actual map sketch, not
  the earlier circular-arc placeholder. `OnDrawGizmos` on the spawner
  draws a 40×40 wireframe box per slot at all times (not just Play mode),
  since houses only exist once spawned at runtime.
- **Stage 3h** — road loop placed around the compound and driveways
  connect it to the house ring. Compound fence replaced with a real
  chain-link/barbed-wire/gate kit (perimeter placed); police station
  placed with an essential interior (3 jail cells + 2 desks); `Exit`
  repositioned away from the compound; 2 Good House slots repositioned to
  read as inside/adjacent to the finished compound. Full-loop playtest
  confirmed working — Stage 3 is feature-complete on real art. Note: both
  `Kenney-CityKitRoads` and `Kenney-CityKitCommercial` needed the same FBX
  import Scale Factor fix (`15`, matching `Kenney-CityKitSuburban`)
  already applied to every other Kenney pack — worth checking any *new*
  Kenney pack import (Industrial, Car Kit) for the same issue before
  assuming it "looks tiny" for some other reason.
- **Stage 3i** — skybox, background skyline, forest ring (doubles as the
  map's world boundary — see `ForestRingSpawner.cs`), and background
  hills. Done, see [stage3i-skybox-skyline.md](stage3i-skybox-skyline.md).
- **Stage 3j (in progress)** — traffic hazard: cars drive one lap of the
  road loop and knock the player down (pure knockback + stun, not a
  round-ending catch) via a real per-limb ragdoll (Unity's Ragdoll
  Wizard). Built for future multiplayer/character-selection compatibility
  without adding any actual networking yet: the ragdoll shows whichever
  skin `PlayerCosmeticSelection` (the existing main-menu system) has
  picked (`PlayerSkinSpawner`, reading a new shared `PlayerSkinRoster`
  asset that `CustomizationUI`'s menu preview also reads from now), and
  is hidden from the owner's own camera via Culling Mask rather than
  `SetActive` — so a future networked player can still be seen by others.
  Scripts written (`CarDriver.cs`, `CarSpawnManager.cs`, `PlayerRagdoll.cs`,
  `PlayerSkinSpawner.cs`, `RagdollHips.cs`); Editor setup (car prefabs,
  waypoint loop, spawn manager, player components) in progress — see
  [stage3j-traffic-hazard.md](stage3j-traffic-hazard.md). `PlayerSkinRoster`
  now covers **all 52 files** in `Quaternius-UltimateAnimatedCharacterPack`
  (a few looked like accessory props/non-humanoid animals at a glance,
  but share the same rig and ragdoll fine, so nothing was excluded). All
  52 are raw FBX (not prefabs) sharing an identical rig, so rather than
  running the Ragdoll Wizard by hand 52 times, `RagdollBatchTool.cs`
  (`Assets/Scripts/Editor/`) builds it on one template skin (manual, via
  the Wizard) and copies that setup — including remapping each joint's
  bone-to-bone connections correctly, which plain copy-paste wouldn't —
  onto the rest, auto-wrapping any FBX targets into real prefabs along
  the way. Template built manually on the `BaseCharacter` skin — 11
  bones (pelvis, spine, head, 2 thighs, 2 shins, 2 upper arms, 2
  forearms), which is the Ragdoll Wizard's actual complete output, not a
  gap (feet/elbows are sizing references for the shin/forearm, not
  separate bodies — this was briefly misdiagnosed as broken, it isn't).
  The tool grew a **Force** option (redo an already-processed target) and
  a selection-order fix (explicit drag-in Template field, was
  `Selection.activeGameObject`) along the way regardless. **Done:** all 52
  skins batch-copied and organized into
  `Assets/Prefabs/PlayerSkins/_Ragdoll/`; `PlayerSkinRoster` updated to
  point at all 52 (the `BaseCharacter` entry points at the original
  hand-built template, not the redundant `BaseCharacter_Ragdoll.prefab`
  the batch tool also produced — that duplicate is harmless, delete
  whenever). Remaining: actually playtest a handful of the 52 (not just
  one) before trusting the batch results, and eventually wire the
  character-selection UI to something other than the placeholder
  first-skin default.

Full walkthroughs: [stage3d-character-art.md](stage3d-character-art.md),
[stage3e-house-prefabs.md](stage3e-house-prefabs.md),
[stage3f-house-pool.md](stage3f-house-pool.md),
[stage3g-map-layout.md](stage3g-map-layout.md),
[stage3h-map-dressing.md](stage3h-map-dressing.md) (roads, fenced police
compound, exit placement),
[stage3i-skybox-skyline.md](stage3i-skybox-skyline.md) (skybox, skyline,
forest/hills),
[stage3j-traffic-hazard.md](stage3j-traffic-hazard.md) (traffic hazard
cars). Style/asset reference (palette, sourced packs, remaining art
to-do) is in [art-info.md](art-info.md); UI/menu element spec is in
[ui-design.md](ui-design.md).

**Next steps:** find better compound fence assets, source jail-cell
props for the police station interior (needed later for Stage 7, fine to
grab now while looking at compound assets generally), finish the
compound fence + gates, relocate `Exit`, then playtest the whole loop on
the real layout — that completes Stage 3 on real art. Work happens on
`jclem's-branch`; merge to `main` once tested and confirmed working (the
Stage 3g/3h work through this point has been playtested and confirmed —
see commit history on `main` after this point).

## Strategy

Build the entire game single-player first. Steam P2P is the right multiplayer
choice, but it's the hardest part of this project — bolt it on once the core
loop (steal, quota, exit) is proven fun by yourself. Networking arrives at
**Stage 4**, not Stage 1.

## Stack

| Layer | Pick | Why |
|---|---|---|
| Engine | Unity LTS | Fewer breaking changes, most tutorials target LTS |
| Language | C# | Only option in Unity |
| Networking | Mirror + FizzySteamworks | Largest beginner tutorial base for "co-op over Steam" |
| Steam layer | Steamworks.NET | Lobbies, invites, P2P transport |
| Steam App ID | Test AppID `480` (Spacewar) | Free full Steamworks access until near release |
| Version control | Git + GitHub, Git LFS | Free for private 2-person repo |

## Core systems

Full detailed design for economy/capacity/jail-bail/sabotage/movement is
in [gameplay-design.md](gameplay-design.md) (written 2026-09-01, not yet
implemented) — summary below, that doc is the source of truth for specifics.

- **Player Controller** — first-person movement + interaction raycast;
  designed (not yet built) to add sprint, crouch, jump, and bhop-style
  advanced movement
- **Inventory** — 5 shared carry slots (loot + brought-in items) + a
  separate "Prison Wallet" safe slot; a Cash balance (separate from
  carried items) accumulated by selling loot in the shop
- **Loot Tables** — per-house spawn tables, randomized value per pickup;
  "Good Houses" roll higher value *and* carry more risk (faster suspicion,
  closer to the police station)
- **Homeowner AI** — Idle → Suspicious → Alerted → calls police
- **Police AI** — Patrol → Respond → Chase → Catch
- **Jail & Bail** — two tiers (mid-round catch vs. end-of-batch quota
  failure), rescuable by other players for a Cash bond/bounty, self-bail
  after enough time if unrescued — see gameplay-design.md for the full
  breakdown
- **Sabotage Tools** — mostly direct PvP with some environmental items,
  tiered with a benefit+drawback per item (exact list TBD)
- **Round Manager** — rounds happen in escalating batches of 3 (quota +
  shop prices step up each batch, gating new shop unlocks); Ready-up shop
  phase where players physically walk to a ready spot, not a countdown
- **Economy/Quota** — quota is cumulative across a 3-round batch, plus a
  personal add-on based on sabotage spending; Cash surplus above quota is
  wiped at the batch boundary (anti-hoarding)
- **Win Condition** — none — deliberately endless score-attack/freeplay,
  no fixed round count or results screen

## Build order

1. **Learn the basics, together** — each of you finishes a short standalone Unity/C# tutorial before touching this project.
2. **Single house, single player** — walk in, pick up an item, see it in an inventory readout. No AI, no networking.
3. **Full offline loop** — a few houses, quota + UI, a homeowner that notices you, a police officer that chases and jails you, an exit that ends the round.
4. **Two players, same machine** — Mirror over `localhost` via two instances (ParrelSync or two builds).
5. **Actually over the internet** — Steamworks.NET + FizzySteamworks, lobby via Steam overlay invite, test AppID 480.
6. **Turn friends into rivals** — taser, hammer, alarm clock, bat, all networked correctly.
7. **The meta-game** — Ready-up shop phase, Cash/quota-batch economy,
   jail & bail — see [gameplay-design.md](gameplay-design.md) for the
   full design.
8. **Playtest with the friend group** — real match with 3–4 people (design target is 4–8), collect notes, loop back as needed.

## Map (from the sketch)

Outer ring of ~10 houses (reuse 2–3 house prefabs, don't build 10 uniques),
a fenced central compound with a police station and two "Good Houses" (higher
loot, deliberately placed next to the police), and a single exit as the
extraction choke point.

These exact numbers (~10 houses, 2 Good Houses, one compound entrance) are
**rough guidance, not a locked spec** — decide the real counts at Stage
3g's slot layout, informed by how Stage 3f's house pool and playtesting
actually feel. The single exit is deliberately kept as one contested
chokepoint even at higher player counts (see
[gameplay-design.md](gameplay-design.md)) — not something to "fix" into
multiple exits without a specific reason to.

Map size is fixed regardless of player count — the design target is 4–8
players (see [gameplay-design.md](gameplay-design.md)), all sharing this
same house pool rather than the map scaling up. More players competing
over the same fixed loot pool is the intended chaos, not a bigger map.

## Team workflow

- Own systems, not disciplines — split by system (e.g. player+inventory+houses
  vs. AI+police+round manager), not by "art vs. code."
- Merge often, in small pieces — Unity scenes don't merge well in Git.
- One person owns the Steam/networking integration end-to-end at Stage 5.
- Playtest together every stage, even solo-buildable ones.

## Future ideas (not yet scoped)

- **Time-of-day rounds** — most rounds play in bright daytime, but
  occasionally a round is dusk or night instead, raising difficulty (lower
  visibility). See [art-info.md](art-info.md)'s "Lighting & time of day"
  section for the draft visual treatment. Not part of the current build
  order — stays daytime-only through at least Stage 3 — needs its own
  scoping pass (how rounds get selected, how much harder night actually
  is, whether it affects AI behavior) before it's added to a stage.
- **Real per-blade 3D grass** — yards currently use a procedural
  striped-lawn Shader Graph material (flat plane + normal map, see
  [lawn-shader-setup.md](lawn-shader-setup.md)), which was judged good
  enough for now. Actual blade geometry (GPU-instanced or geometry-shader
  grass, with wind sway) would look more like real grass, but is a real
  technical undertaking — new rendering technique, LOD/performance tuning
  across every yard on the map — considered and deliberately deferred
  rather than taken on for a 2-person beginner team right now. Revisit
  only as a late polish pass, not before the core loop is fun.

## Risks

- **Scope creep** — police AI, homeowner AI, shop economy, and 4 sabotage
  items are each their own mini-project. Get the bare loop fun first, with
  placeholder cubes if needed.
- **Networking debugging** — bugs that only show up "for the other player"
  are the hardest to chase as a beginner. Test with two clients from Stage 4
  onward, not just before a playtest.
- **Good news** — round-based, lobby-based games sidestep the hardest MP
  problems (no world streaming, no persistent server state, no mid-session
  reconnect needed for v1).

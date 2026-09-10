# Rob Everyone — Dev Plan

Full designed version with the map sketch and visual layout: https://claude.ai/code/artifact/e48a1ffb-47e8-47e6-ba90-dfb7b4a1d4f3

## Where to pick up next

Status tracking has moved out of this file — **[completed.md](completed.md)**
is the flowing log of everything actually built, **[todo.md](todo.md)** is
everything genuinely still open, both kept current instead of buried in
this file's history. This section just orients you.

**Current state**: Stage 3 (full offline loop, real art, road/compound/
skybox/traffic hazard) is feature-complete and playtested. Also jumped
ahead of the build order into a scoped-down Stage 7 (v1 Shop/Lobby loop,
batch economy, hotbar inventory) since the core loop had no restart path
— see completed.md for why that's fine and what's still deliberately
missing from the full Stage 7 design.

**Stage 4 (multiplayer, localhost) is done -- all 9 Rest Points confirmed
working.** Chose full system sync (not the minimal "just player
presence + loot" slice) -- every system (player movement/animation/skin,
loot/inventory, Homeowner/Police AI, traffic hazard cars, the round/
batch economy) was converted to Mirror. This was the single biggest,
riskiest change made to the codebase so far, and it showed: a long tail
of real bugs (not just Editor wiring) surfaced and got fixed going
through the doc's Rest Points one at a time -- see
[stage4-multiplayer-mirror.md](stages/stage4-multiplayer-mirror.md) and
[completed.md](completed.md) for the full list.

**Stage 5 (Steam) is done through Editor setup Part 3** --
Steamworks.NET/FizzySteamworks installed, `SteamLobby`/`SteamManager`
wired, Steam initializes cleanly. The one thing still open: Rest Point
4, the real two-Steam-account overlay invite test, since that needs a
second account/friend to actually verify against -- see
[stage5-steam-multiplayer.md](stages/stage5-steam-multiplayer.md) and
[todo.md](todo.md).

Full stage-by-stage how-to walkthroughs (procedural reference, not status
— check completed.md/todo.md for what's actually done):
[stage2-editor-setup.md](stages/stage2-editor-setup.md),
[stage3-editor-setup.md](stages/stage3-editor-setup.md),
[stage3b-homeowner-setup.md](stages/stage3b-homeowner-setup.md),
[stage3c-police-setup.md](stages/stage3c-police-setup.md),
[stage3d-character-art.md](stages/stage3d-character-art.md),
[stage3e-house-prefabs.md](stages/stage3e-house-prefabs.md),
[stage3f-house-pool.md](stages/stage3f-house-pool.md),
[stage3g-map-layout.md](stages/stage3g-map-layout.md),
[stage3h-map-dressing.md](stages/stage3h-map-dressing.md),
[stage3i-skybox-skyline.md](stages/stage3i-skybox-skyline.md),
[stage3j-traffic-hazard.md](stages/stage3j-traffic-hazard.md),
[stage7-shop-lobby-setup.md](stages/stage7-shop-lobby-setup.md),
[stage7b-batch-economy-hotbar-setup.md](stages/stage7b-batch-economy-hotbar-setup.md),
[stage4-multiplayer-mirror.md](stages/stage4-multiplayer-mirror.md),
[stage5-steam-multiplayer.md](stages/stage5-steam-multiplayer.md),
[item-creation.md](stages/item-creation.md) (turning
`Assets/Art/Items/`'s 33 raw models into spawnable loot with
size-tiered exclusion, done between Stage 4 Parts 3 and 4),
[inventory-ux-setup.md](stages/inventory-ux-setup.md) (Tab inventory
screen, drop-with-Q, Prison Wallet, steal-window rework — done),
[player-feel-setup.md](stages/player-feel-setup.md) (instant jump,
bhop/autohop, first-person visible body — code done, tuning open) and
[voip-setup.md](stages/voip-setup.md) (Steam proximity voice chat —
planned, not built).
Style/asset reference (palette, sourced packs, remaining art to-do) is in
[art-info.md](art-info.md); UI/menu element spec is in
[ui-design.md](stages/ui-design.md); main menu build docs are
[main-menu-visual-design.md](stages/main-menu-visual-design.md) and
[main-menu-customization-setup.md](stages/main-menu-customization-setup.md).

**Next steps**: Stage 5's Rest Point 4 (real Steam overlay test) is
parked until a second account/friend is available -- move on to Stage 6
(sabotage items, networked) in the meantime, per the build order. The
older [todo.md](todo.md) "Verify / playtest" items (Stage 7b,
`Real_House_02`, the ragdoll batch results) are all still open too and
worth doing whenever there's a natural pause, but don't block Stage 6.
Work happens on `jclem's-branch`; merge to `main` once tested and
confirmed working.

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
in [gameplay-design.md](stages/gameplay-design.md) (written 2026-09-01, not yet
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
   jail & bail — see [gameplay-design.md](stages/gameplay-design.md) for the
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
[gameplay-design.md](stages/gameplay-design.md)) — not something to "fix" into
multiple exits without a specific reason to.

Map size is fixed regardless of player count — the design target is 4–8
players (see [gameplay-design.md](stages/gameplay-design.md)), all sharing this
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
  [lawn-shader-setup.md](stages/lawn-shader-setup.md)), which was judged good
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

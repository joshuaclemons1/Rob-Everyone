# Rob Everyone — Dev Plan

Full designed version with the map sketch and visual layout: https://claude.ai/code/artifact/e48a1ffb-47e8-47e6-ba90-dfb7b4a1d4f3

## Where to pick up next

Status tracking lives in **[GitHub Issues](https://github.com/joshuaclemons1/Rob-Everyone/issues)**
now, not a doc in this repo — open issues (`enhancement`/`bug` labels)
are everything genuinely still open, closed ones are the flowing record
of what's actually built and confirmed. This section just orients you
at a glance; check Issues for the current, authoritative state. See
[issue-tracking.md](issue-tracking.md) for how this project uses them.

**Current state**: the core offline loop, full multiplayer (Mirror +
Steam), sabotage items, and the full meta-game (shop, Jail & Bail,
Homeowner/Police AI, night mode) are all built and playtested — see the
closed issues for the detailed record (start from the `[Done]`-titled
ones). Six alpha builds have shipped (`v1.0.0-alpha` → `v1.0.5-alpha`) and
the team is now in the friend-group-playtest stage, filing real bugs as
they turn up.

Full stage-by-stage how-to walkthroughs (procedural reference, not
status — check the Issues tab for what's actually done):
[stage2-editor-setup.md](stages/stage2-editor-setup.md),
[stage3-editor-setup.md](stages/stage3-editor-setup.md),
[stage3b-homeowner-setup.md](stages/stage3b-homeowner-setup.md),
[stage3c-police-setup.md](stages/stage3c-police-setup.md),
[stage3d-character-art-setup.md](stages/stage3d-character-art-setup.md),
[stage3e-house-prefabs-setup.md](stages/stage3e-house-prefabs-setup.md),
[stage3f-house-pool-setup.md](stages/stage3f-house-pool-setup.md),
[stage3g-map-layout-setup.md](stages/stage3g-map-layout-setup.md),
[stage3h-map-dressing-setup.md](stages/stage3h-map-dressing-setup.md),
[stage3i-skybox-skyline-setup.md](stages/stage3i-skybox-skyline-setup.md),
[stage3j-traffic-hazard-setup.md](stages/stage3j-traffic-hazard-setup.md),
[stage7-shop-lobby-setup.md](stages/stage7-shop-lobby-setup.md),
[stage7b-batch-economy-hotbar-setup.md](stages/stage7b-batch-economy-hotbar-setup.md),
[stage4-multiplayer-mirror.md](stages/stage4-multiplayer-mirror.md),
[stage5-steam-multiplayer.md](stages/stage5-steam-multiplayer.md),
[item-creation-setup.md](stages/item-creation-setup.md) (turning
`Assets/Art/Items/`'s 33 raw models into spawnable loot with
size-tiered exclusion),
[inventory-ux-setup.md](stages/inventory-ux-setup.md) (Tab inventory
screen, drop-with-Q, Prison Wallet, steal-window rework),
[player-feel-setup.md](stages/player-feel-setup.md) (instant jump,
bhop/autohop, first-person visible body),
[ragdoll-carry-setup.md](stages/ragdoll-carry-setup.md) (pick up / carry
/ charge-throw downed players),
[player-animations-setup.md](stages/player-animations-setup.md) (carry
gait + pick-up / shoot / swing one-shots),
[stage6-sabotage-items-setup.md](stages/stage6-sabotage-items-setup.md)
and
[stage6-sabotage-items-phase2-setup.md](stages/stage6-sabotage-items-phase2-setup.md)
(Taser/Dynamite, then Bat/Hammer/Tranq Gun/Alarm Clock/steal-window),
[stage7c-meta-game-setup.md](stages/stage7c-meta-game-setup.md) (buy-side
shop, real Jail & Bail, Homeowner patrol, police dispatch pooling, night
mode),
[settings-menu-setup.md](stages/settings-menu-setup.md) (Input System
migration, rebinding, audio mixer, graphics, accessibility, pause menu),
[voip-setup.md](stages/voip-setup.md) (Steam proximity voice chat),
[ui-implementation-setup.md](stages/ui-implementation-setup.md) (crosshair,
Cash/Quota/Timer HUD), and
[launcher-setup.md](stages/launcher-setup.md) (pre-launch auto-update
launcher, replacing the old in-game update popup),
[t1-2-bug-fixes.md](stages/t1-2-bug-fixes.md) (code-only fixes for the
Tier 1/2 priority-list bugs, written without Editor access — the
checklist for testing them once back at a PC), and
[main-menu-character-preview-setup.md](stages/main-menu-character-preview-setup.md)
(issue #39: persistent character preview + Settings fall animation,
the Editor checklist for the pieces that needed real eyes), and
[main-menu-background-flythrough-setup.md](stages/main-menu-background-flythrough-setup.md)
(issue #51: replacing the static diorama with a moving drone-shot
flythrough over a procedural stand-in neighborhood), [reconnect-setup.md](stages/reconnect-setup.md) (issue #53: rejoining
an in-progress lobby after a disconnect/crash without losing your run),
and
[lobby-customization-building-plan.md](stages/lobby-customization-building-plan.md)
(issue #52: implementation plan for the in-Lobby skin/color
customization building; Phases 0-2 are built, see the plan for what
each does), and
[lobby-customization-building-editor-setup.md](stages/lobby-customization-building-editor-setup.md)
(Phases 3 & 4 of the same issue: placing/wiring the pedestals, paint
cans, mirror cycle buttons, and the mirror's own reflection rendering
in `Lobby.unity`, with rest points), [sfx-plan.md](stages/sfx-plan.md) (issues #22/#23: what audio already
exists vs. the real gaps, sabotage item use sounds first), and
[retention-research.md](stages/retention-research.md) (why playtest
sessions stall after the first quota batch, researched directly
against Super Battle Golf and Gamble With Your Friends), and
[polish-deep-dive.md](stages/polish-deep-dive.md) (a second, deeper
research pass covering both gameplay depth and graphics/visual
fidelity, with a prioritized actionable list for each), and
[postprocessing-setup.md](stages/postprocessing-setup.md) (issue #64:
Editor walkthrough for the post-processing pass, with three named
value-table presets to compare).
Style/asset reference (palette, sourced packs, remaining art to-do) is in
[art-info.md](art-info.md); UI/menu element spec is in
[ui-design.md](stages/ui-design.md); main menu build docs are
[main-menu-visual-design.md](stages/main-menu-visual-design.md) and
[main-menu-customization-setup.md](stages/main-menu-customization-setup.md);
[lawn-shader-setup.md](stages/lawn-shader-setup.md) covers the procedural
lawn material.

**Next steps**: check the [Issues tab](https://github.com/joshuaclemons1/Rob-Everyone/issues)
for what's actually open — real playtest bugs from the friend group take
priority over the remaining `enhancement`-labeled polish items. Work
happens on `jclem's-branch`; merge to `main` once tested and confirmed
working.

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

# Rob Everyone — Dev Plan

Full designed version with the map sketch and visual layout: https://claude.ai/code/artifact/e48a1ffb-47e8-47e6-ba90-dfb7b4a1d4f3

## Where to pick up next

Currently mid-**Stage 3** (full offline loop). Code side (quota/timer/exit,
homeowner AI, police AI) is already merged on `jclem's-branch`.

Art side: real assets have been sourced and imported, but **not yet placed
in the scene**:

- Kenney City Kit (Suburban, Roads, Commercial) + Modular Buildings under
  `Assets/Art/Environment/`
- Quaternius Ultimate Animated Character Pack under
  `Assets/Art/Characters/`

Full details (what each pack is for, the locked color palette, remaining
art to-do by stage) are in [art-info.md](art-info.md).

**Next steps:** open Unity, let it reimport `Assets/Art/`, then start
swapping the Stage 3 placeholder/gray-box geometry for the real Kenney
house pieces first (police station + character placement can follow).
Remember: work happens on `jclem's-branch`; only merge to `main` once
tested and confirmed working.

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

- **Player Controller** — first-person movement + interaction raycast
- **Inventory** — carried items, weight/value, capacity
- **Loot Tables** — per-house spawn tables; "Good Houses" roll higher value
- **Homeowner AI** — Idle → Suspicious → Alerted → calls police
- **Police AI** — Patrol → Respond → Chase → Catch
- **Jail State** — caught players sit out the round, earn nothing, start next round behind
- **Sabotage Tools** — taser, hammer, alarm clock, bat — used on players or homeowners
- **Round Manager** — Pre-Round Shop → Timer → Extraction → Scoring → next round, 5 rounds
- **Economy/Quota** — per-player money, per-round quota, shop
- **Win Condition** — highest total money after round 5

## Build order

1. **Learn the basics, together** — each of you finishes a short standalone Unity/C# tutorial before touching this project.
2. **Single house, single player** — walk in, pick up an item, see it in an inventory readout. No AI, no networking.
3. **Full offline loop** — a few houses, quota + UI, a homeowner that notices you, a police officer that chases and jails you, an exit that ends the round.
4. **Two players, same machine** — Mirror over `localhost` via two instances (ParrelSync or two builds).
5. **Actually over the internet** — Steamworks.NET + FizzySteamworks, lobby via Steam overlay invite, test AppID 480.
6. **Turn friends into rivals** — taser, hammer, alarm clock, bat, all networked correctly.
7. **The meta-game** — pre-round shop, money carries across 5 rounds, results screen.
8. **Playtest with the friend group** — real match with 3–4 people, collect notes, loop back as needed.

## Map (from the sketch)

Outer ring of ~10 houses (reuse 2–3 house prefabs, don't build 10 uniques),
a fenced central compound with a police station and two "Good Houses" (higher
loot, deliberately placed next to the police), and a single exit as the
extraction choke point.

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

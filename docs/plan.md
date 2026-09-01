# Rob Everyone — Dev Plan

Full designed version with the map sketch and visual layout: https://claude.ai/code/artifact/e48a1ffb-47e8-47e6-ba90-dfb7b4a1d4f3

## Where to pick up next

Currently mid-**Stage 3**, art-integration sub-stages (3d done, 3e done,
mid-3f). Code side (quota/timer/exit, homeowner AI, police AI) has been
done since Stage 3c.

Done so far:

- **Stage 3d** — Homeowner and Police reskinned with real Quaternius
  characters (capsule collider kept, Mesh Renderer hidden on the swap
  target). `HomeownerAnimator` (Idle-only) and `PoliceAnimator`
  (Idle/Walk/Run Blend Tree, driven by `PoliceAI.cs` feeding
  `agent.velocity.magnitude` into a `Speed` param each frame) exist under
  `Assets/Art/Characters/Animators/`.
- **Stage 3e** — `Real_House_01` built and proven: real Kenney building +
  yard padding to the 25×25 plot size + Kenney Furniture Kit interior +
  nested `Homeowner` + loot spot, all as one self-contained prefab at
  `Assets/Prefabs/Houses/Real_House_01.prefab`. Also added
  `DoorTeleporter` (`Assets/Scripts/World/DoorTeleporter.cs`) — a
  paired-trigger doorway workaround for buildings without a real modeled
  door gap, so building colliders never need hand-fitting.
- **Stage 3f (in progress)** — repeat the Stage 3e pattern for 2–3 more
  building variants to build an actual house pool (target: 3–4 total real
  house prefabs) before Stage 3g's random spawner has something to pick
  from.

Full walkthroughs: [stage3d-character-art.md](stage3d-character-art.md),
[stage3e-house-prefabs.md](stage3e-house-prefabs.md),
[stage3f-house-pool.md](stage3f-house-pool.md). Style/asset reference
(palette, sourced packs, remaining art to-do) is in
[art-info.md](art-info.md).

**Next steps:** finish Stage 3f (2–3 more house prefabs, each tested solo
— door, loot, homeowner, walk back out), then Stage 3g: lay out slots
matching the map sketch and write the script that randomly assigns one
house prefab per slot. Work happens on `jclem's-branch`; only merge to
`main` once tested and confirmed working.

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

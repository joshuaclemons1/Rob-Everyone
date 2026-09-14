# Rob Everyone

A first-person heist game where you're not on the same team as your
friends — everyone has their own quota, everyone's stealing from the
same houses, and everyone's a legitimate target. Steam multiplayer,
4–8 players, no fixed round count — an endless score-attack you keep
coming back to, not a match with a winner.

## The gameplay loop

Each round you spawn into a neighborhood of houses surrounding a fenced
police compound with a single, deliberately contested exit. Break into
houses and loot what you find into a 5-slot hotbar, while:

- **Homeowners** patrol, get suspicious if they see you, and call the
  **police** if you linger.
- **Police** patrol, respond, chase, and catch — getting caught strips
  your loot and jails you, but you're not out of the round: another
  player can rescue you for a Cash bond, or you self-bail if nobody
  does.
- **Rivals** are fair game — sabotage items (Taser, Baseball Bat,
  Hammer, Tranquilizer Gun, Dynamite, Alarm Clock) let you stun and
  steal from another player directly, or even pick up their downed body
  and carry it somewhere inconvenient.
- **Traffic** on the road loop will flatten you if you're not careful,
  ragdoll and all.

Reach the exit (or survive to the timer) before you're caught, then sell
your loot and buy gear back at the Lobby's pawn shop between rounds.
Quota and prices grow every 3-round batch, Cash above quota gets wiped
at the batch boundary, and the last round of each batch is a harder
night round. Full design detail:
[docs/stages/gameplay-design.md](docs/stages/gameplay-design.md).

## Team

- **Chayton** (`jclem's-branch`) — lead, most of the scripting
- **Zach** (`zach's-branch`) — house/level design, art
- **Brian** (`brian's-branch`) — house/level design, local playtesting
- **Goodson** (`goodson's-branch`) — house/level design, local playtesting

New here? Start with **[Setup.md](Setup.md)**.

## Tech stack

| Layer | Pick |
|---|---|
| Engine | Unity 6 (`6000.5.9f1`), URP |
| Language | C# |
| Networking | [Mirror](https://mirror-networking.com/) + FizzySteamworks transport |
| Steam layer | Steamworks.NET (test AppID `480`/Spacewar until closer to release) |
| Input | Unity's Input System package, fully rebindable |
| Version control | Git + GitHub, Git LFS for art/audio/video |

## Status

Alpha — three builds shipped so far (`alpha-v1` → `alpha-v1.0.3`), the
core loop plus the full meta-game (shop, Jail & Bail, AI, night mode,
VoIP, a real Settings menu) are built and playtested. See
**[docs/completed.md](docs/completed.md)** for what's actually done and
**[docs/todo.md](docs/todo.md)** for what's genuinely still open — both
are kept current; an individual doc's own header sometimes isn't.

## Docs

- **[Setup.md](Setup.md)** — get the project running and start
  contributing.
- **[docs/plan.md](docs/plan.md)** — the dev plan, build order, and an
  index of every stage's build-it-yourself walkthrough doc.
- **[docs/completed.md](docs/completed.md)** / **[docs/todo.md](docs/todo.md)**
  — current status.
- **[docs/stages/gameplay-design.md](docs/stages/gameplay-design.md)** —
  full economy/capacity/jail-bail/sabotage/movement design.
- **[docs/stages/ui-design.md](docs/stages/ui-design.md)** — UI element
  spec.
- **[docs/art-info.md](docs/art-info.md)** — art style/palette/sourced-asset
  reference.
- **[Issues](https://github.com/joshuaclemons1/Rob-Everyone/issues)** —
  bug reports (use the **Bug report** template).

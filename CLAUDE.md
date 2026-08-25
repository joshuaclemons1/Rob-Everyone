# Rob Everyone

A heist game where players work *against* each other, each with their own quota — steal from houses, sabotage rivals, avoid the police, hit the exit before your friend does. Built in Unity by a two-person beginner team, no deadline.

Full design/roadmap: see `docs/plan.md`.

## Stack

- Unity LTS, C#
- Networking: Mirror + FizzySteamworks transport (added at Stage 4/5 — not yet)
- Steamworks.NET, using test AppID 480 until closer to release
- Git + Git LFS for art/audio (`.gitattributes` already configured)

## Build order

Follow `docs/plan.md` stage by stage. Don't jump ahead to networking, the shop,
or sabotage items before the single-player loop (loot → quota → exit) works —
see the "Build it single-player first" section of the plan for why.

## Conventions

- Scripts live under `Assets/Scripts/<System>/`, one system per folder
  (e.g. `Assets/Scripts/Inventory/`, `Assets/Scripts/AI/`, `Assets/Scripts/Round/`).
- Prefer `[SerializeField] private` fields over public fields for anything
  wired up in the Inspector.
- No networking code until Mirror is actually installed (Stage 4). Keep
  early systems plain MonoBehaviours so they're simple to read while learning.

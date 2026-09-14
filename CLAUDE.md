# Rob Everyone

A heist game where players work *against* each other, each with their own quota — steal from houses, sabotage rivals, avoid the police, hit the exit before your friend does. Built in Unity by a four-person team (Chayton, Zach, Brian, Goodson), no deadline. Alpha builds are shipping.

Full design/roadmap: see `docs/plan.md`. Project overview/team/tech stack: see `README.md`.

## Stack

- Unity 6, C#
- Networking: Mirror + FizzySteamworks transport (live — Stage 4/5 are done)
- Steamworks.NET, using test AppID 480 until closer to release
- Git + Git LFS for art/audio (`.gitattributes` already configured)

## Build order

Follow `docs/plan.md` stage by stage for anything still open. Most of the
core build order (through the full Stage 7 meta-game) is done — check
[GitHub Issues](https://github.com/joshuaclemons1/Rob-Everyone/issues)
before assuming something isn't built yet.

## Status tracking

This project tracks status in **GitHub Issues**, not a doc in this repo
— `docs/todo.md`/`docs/completed.md` were retired for exactly this.
Closed issues = done and confirmed; open issues (`enhancement`/`bug`
labels) = genuinely still open. Read `docs/issue-tracking.md` before
reporting a bug, filing a feature, or closing something out — it covers
labels, granularity, titles, and cross-referencing conventions. Don't
recreate a status-tracking markdown file.

## Conventions

- Scripts live under `Assets/Scripts/<System>/`, one system per folder
  (e.g. `Assets/Scripts/Inventory/`, `Assets/Scripts/AI/`, `Assets/Scripts/Round/`).
- Prefer `[SerializeField] private` fields over public fields for anything
  wired up in the Inspector.

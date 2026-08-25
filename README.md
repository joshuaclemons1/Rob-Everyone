# Rob Everyone

Setup and dev plan for the game. Full plan: `docs/plan.md`.

## First-time setup

1. **Install Unity Hub and Unity LTS** (2022 LTS or the current Unity 6 LTS)
   from unity.com if you haven't already.
2. **Install Git LFS** (one-time, per machine): `git lfs install`
3. **Create the Unity project in this exact folder:**
   - Open Unity Hub → New Project
   - Template: 3D (Core) — URP is fine too if you want nicer visuals later
   - Project name: `Rob-Everyone`
   - Location: `/Users/Shared`
   - Unity Hub will populate `Assets/`, `Packages/`, and `ProjectSettings/`
     directly into this existing folder alongside the files already here
     (`.gitignore`, `CLAUDE.md`, `docs/`). That's expected.
4. Once Unity finishes generating the project, come back here and commit:
   ```
   git add .
   git commit -m "Initial Unity project"
   ```
5. **Your friend clones the repo** (once it's pushed to GitHub) rather than
   creating their own Unity project — everyone should share one project,
   not merge two separately-generated ones.

## Day to day

- Follow `docs/plan.md` stage by stage — don't skip ahead to networking or
  the shop before Stage 3's offline loop works.
- Scripts live under `Assets/Scripts/<System>/`.

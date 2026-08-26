# Rob Everyone

Setup and dev plan for the game. Full plan: `docs/plan.md`.

## Getting set up (Josh and Zach, separately)

The Unity project already exists in this repo — you're cloning it, not
creating a new one.

1. **Install Unity Hub**, then install **Unity 6000.5.9f1** specifically
   (matches `ProjectSettings/ProjectVersion.txt`) — pick that exact version
   in Unity Hub's install screen, not just "the latest 6000.x."
2. **Install Git LFS** (one-time, per machine): `git lfs install`
3. **Clone the repo:**
   ```
   git clone https://github.com/joshuaclemons1/Rob-Everyone.git
   ```
4. **Check out your own branch:**
   - Josh: `git checkout jclem's-branch`
   - Zach: `git checkout zach's-branch`
5. **Open the project in Unity Hub — use Add, not Import:**
   - Projects tab → **Add** ▾ → **Add project from disk**
   - Select the cloned `Rob-Everyone` folder itself (the one containing
     `Assets/` and `ProjectSettings/`), not a subfolder — single-click to
     highlight it, then **Add Project**
   - "Import Project" won't recognize this folder as a Unity project; **Add**
     is the one that works for a folder that already has a project in it
6. First open takes several minutes — `Library/` isn't tracked in Git, so
   Unity has to reimport every asset and resolve packages from scratch on a
   fresh clone. A long "Importing" bar is normal, not a hang.

## Day to day

- Follow `docs/plan.md` stage by stage — don't skip ahead to networking or
  the shop before Stage 3's offline loop works.
- Scripts live under `Assets/Scripts/<System>/`.
- Commit and push to your own branch as you work. Merge into `main` in
  small, frequent pieces rather than letting two branches drift apart —
  Unity scene files don't merge well in Git, so the longer two people
  diverge on the same scene, the worse a conflict gets. See `docs/plan.md`'s
  "Team workflow" section for how work is split between systems.

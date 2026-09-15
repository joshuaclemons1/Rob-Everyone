# Setup

How to get the project running on your machine and start contributing.
For what the game *is*, see [README.md](README.md) — this doc is just
the mechanics of getting set up and the day-to-day workflow.

## 1. Install prerequisites

- **Unity Hub**, then install **Unity 6000.5.9f1** specifically (matches
  `ProjectSettings/ProjectVersion.txt`) — pick that exact version in Unity
  Hub's install screen, not just "the latest 6000.x." A mismatched
  version can silently reimport/rewrite scene and asset files in ways
  that show up as noisy diffs later.
- **Git LFS** (one-time, per machine): `git lfs install`. Art, audio,
  and video are LFS-tracked (`.gitattributes`) — without this step
  they'll clone as tiny pointer files instead of the real assets.
- **Steam client**, signed into your own Steam account. Multiplayer runs
  over Steamworks (`FizzySteamworks`, test AppID `480`/Spacewar) — you
  don't need to own anything, just have Steam running while you Play.

## 2. Clone and check out your branch

```
git clone https://github.com/joshuaclemons1/Rob-Everyone.git
```

Everyone works on their own branch, merged into `main` only once
something is tested and confirmed working:

| Person | Branch |
|---|---|
| Chayton | `jclem's-branch` |
| Zach | `zach's-branch` |
| Brian | `brian's-branch` |
| Goodson | `goodson's-branch` |

```
git checkout <your-branch>
```

If your branch doesn't exist yet, ask Chayton — don't invent your own
naming.

## 3. Open the project in Unity Hub

- Projects tab → **Add** ▾ → **Add project from disk**
- Select the cloned `Rob-Everyone` folder itself (the one containing
  `Assets/` and `ProjectSettings/`), not a subfolder — single-click to
  highlight it, then **Add Project**
- "Import Project" won't recognize this folder as a Unity project;
  **Add** is the one that works for a folder that already has a project
  in it.
- First open takes several minutes — `Library/` isn't tracked in Git,
  so Unity has to reimport every asset and resolve packages from scratch
  on a fresh clone. A long "Importing" bar is normal, not a hang.

## 4. Always press Play from `MainMenu`

`NetworkManager` (and everything hanging off it — Steam, the loading
screen, the update checker) only exists in `MainMenu.unity`. Pressing
Play from any other scene (`SampleScene`, `Lobby`, `Intro`) directly
will misbehave — always open `MainMenu` first.

## 5. Testing multiplayer on your own

- **Two windows, one machine**: install **ParrelSync** (`Window →
  Package Manager → Install package from git URL`:
  `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`), then
  `ParrelSync → Clones Manager → Add new clone` for a second Editor
  window pointed at the same project. Press Play in both.
- **Host** starts a Steam lobby; **Join** opens the Steam Friends
  overlay to invite/accept, rather than typing an IP. That means a real
  two-account test needs two separate Steam accounts (two machines, or
  one machine + a friend) — see [README.md](README.md)'s doc links for
  `stage5-steam-multiplayer.md` if you're specifically testing the Steam
  layer itself.
- Alpha builds are tagged on GitHub Releases (`v1.0.0-alpha`,
  `v1.0.1-alpha`, …) if you just want to play rather than run from
  source — grab the standalone launcher (see
  [docs/stages/launcher-setup.md](docs/stages/launcher-setup.md)) and it
  checks for a newer build on every run.

## 6. Day-to-day workflow

- Follow [docs/plan.md](docs/plan.md)'s build order — don't jump ahead
  to a later stage's system before the one before it actually works.
  Check the [Issues tab](https://github.com/joshuaclemons1/Rob-Everyone/issues)
  (closed = built and confirmed, open `enhancement`/`bug` = genuinely
  still open) before assuming something isn't built yet — it's kept
  current; an individual stage doc's own header sometimes isn't. See
  [docs/issue-tracking.md](docs/issue-tracking.md) for how issues are
  used on this project.
- Scripts live under `Assets/Scripts/<System>/`, one system per folder.
- Commit and push to your own branch as you work. Merge into `main` in
  small, frequent pieces rather than letting branches drift apart —
  Unity scene/prefab files don't merge well in Git, so the longer two
  people diverge on the same scene, the worse a conflict gets.
- Own systems, not disciplines — split by system (player/inventory,
  AI/police, houses/art, UI), not "art vs. code."
- **Never commit someone else's in-progress Editor work.** If a scene or
  prefab shows as locally modified and you didn't intentionally change
  it, leave it alone and ask rather than committing or discarding it.
- Found something broken? File it as a
  [GitHub Issue](https://github.com/joshuaclemons1/Rob-Everyone/issues/new/choose)
  using the **Bug report** template — especially useful during local
  playtests when the person hitting the bug isn't the one who'll fix it.
  If you're telling Chayton about it directly instead, he can file it
  for you from the conversation.

## Where things live

- [README.md](README.md) — what the game is, the gameplay loop, tech
  stack, and links to every design/build doc.
- [docs/plan.md](docs/plan.md) — the dev plan and build order.
- [Issues](https://github.com/joshuaclemons1/Rob-Everyone/issues) —
  current status (closed = done, open = still open), bug reports (use
  the **Bug report** template), and feature/polish tracking (`enhancement`
  label). See [docs/issue-tracking.md](docs/issue-tracking.md) for the
  full process.

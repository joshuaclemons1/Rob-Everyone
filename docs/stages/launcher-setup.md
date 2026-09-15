# Pre-launch auto-update launcher

Replaces the old in-game "a new version is available" popup with a real
pre-launch updater. Tracked in
[issue #41](https://github.com/joshuaclemons1/Rob-Everyone/issues/41).

## What changed

- **`UpdateChecker.cs` / `UpdateAvailablePopupUI.cs` are deleted.** The
  Unity game itself no longer checks for updates or shows any popup
  about them.
- **New `Launcher/RobEveryoneLauncher/`** — a separate Avalonia (.NET,
  cross-platform desktop UI) application, entirely outside the Unity
  project. It:
  1. Checks GitHub Releases for the newest tag.
  2. Downloads and installs it if the player's local copy is behind (or
     missing).
  3. Launches the actual game.
- **A real bug found building this**: GitHub's `/releases/latest` API
  endpoint — what the old in-game checker used — excludes prerelease
  and draft releases entirely. Every release this project has published
  so far (all the `alpha-vX.X.X` tags) is marked prerelease, so that
  endpoint 404s every single time. Confirmed live against the real repo
  while building the launcher. This means **the old in-game update
  checker never once found an update the whole time it existed** — it
  fails open on any non-success response, so nobody would have noticed.
  The launcher uses `/releases?per_page=1` (the list endpoint, which
  does include prereleases, sorted newest first) instead.

## Why Avalonia

Cross-platform (Windows/macOS/Linux — matters directly for issue #43),
.NET/C#, so it stays in the same language the team already knows from
Unity, without pulling in something heavier like Electron/Node for a
small launcher. MIT-licensed, mature, straightforward to get a
borderless splash-image window + a slim progress bar out of.

## How it works

`Launcher/RobEveryoneLauncher/UpdateService.cs` is the whole flow,
independent of the UI:

1. `GetLatestReleaseAsync` — GET the releases list, take index 0.
2. `SelectAssetForCurrentPlatform` — picks the release asset matching
   this OS (`RobEveryone-<tag>-win.zip` / `-mac.zip` / `-linux.zip`, the
   naming convention issue #43 is expected to introduce). Falls back to
   "there's exactly one .zip and it has no platform marker at all" so
   this keeps working against today's actual releases
   (`RobEveryone-<tag>.zip`, Windows-only, unsuffixed) — but won't
   blindly grab an asset that's clearly marked for a *different*
   platform than the one it's running on.
3. `DownloadAndInstallAsync` — streams the zip to a temp file (reporting
   progress), extracts to a `Rob Everyone.staging/` folder, then swaps it
   in for `Rob Everyone/` only once extraction fully succeeds (the
   previous install is kept as `Rob Everyone.old/` until the swap
   completes, then removed) — a crash or killed process mid-update can't
   leave `Rob Everyone/` half-written and unplayable.
4. `FindGameExecutable` — walks `Rob Everyone/` for something launchable:
   `*.exe` on Windows (skipping Unity's `UnityCrashHandler*.exe`
   companions), a `*.app` bundle on macOS, an extensionless file with
   the execute bit set on Linux.
5. `LaunchGame` — starts it (`open` on macOS, since a `.app` isn't
   directly executable; the file directly on Windows/Linux).

Fails open the same way the old in-game checker did: a broken network
check never blocks someone who already has a working local install from
playing (offline, or GitHub down, just launches whatever's already
installed). Only shows an error if there's genuinely nothing playable
yet (first run, no network, no build for this platform).

State (which version is installed) is a small `launcher-state.json`
written next to the launcher's own executable — deliberately not a
per-user app-data folder, so the whole thing (launcher + `Rob Everyone/`
+ state) stays one self-contained, movable folder, same spirit as the
game's own zip releases today.

## UI

`MainWindow.axaml` — borderless (`WindowDecorations="None"`), fixed
960×540, centered on screen. A large background fills the whole window
(currently a placeholder gradient + wordmark — see the comment at the
top of the XAML for how to swap in real splash art once it exists), a
status line and a slim full-width progress bar anchored to the bottom
edge. Escape closes the window (the only way out if it's stuck on an
error — there's no other window chrome).

## Building it yourself

```
cd Launcher/RobEveryoneLauncher
dotnet build          # or: dotnet run
```

Needs the .NET SDK (10.0+) installed. A plain
`dotnet publish -c Release -r <RID> --self-contained` technically works,
but leaves dozens of loose runtime DLLs sitting next to the .exe —
confusing to hand someone as "here's the launcher." The actual command
(same one `.github/workflows/launcher-build.yml` runs) bundles all of
that into the single executable instead:

```
dotnet publish -c Release -r <RID> --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
rm -f bin/Release/net10.0/<RID>/publish/*.pdb
```

The `rm` isn't optional on Windows: SkiaSharp/HarfBuzzSharp's own NuGet
packages ship their *native* library `.pdb` files as content items that
copy into the publish output regardless of `DebugType` (that only
controls this project's own compiled output, not a referenced package's
content items) — confirmed via CI, ~105MB combined. Nobody's debugging
native Skia/HarfBuzz crashes here, so just deleting them after the fact
is the reliable fix.

`<RID>` is `win-x64`, `osx-arm64`/`osx-x64`, or `linux-x64`. Output is
one executable per platform — that's what should get zipped up and
attached to each GitHub release alongside the actual game build, once
issue #43 sets up per-platform game builds too. First run creates a
`Rob Everyone/` folder next to it with the actual game files
(`Rob Everyone.staging/`/`Rob Everyone.old/` show up briefly during an
update, never left behind once one finishes) — the end state next to
the launcher's own .exe is exactly `RobEveryoneLauncher.exe` +
`Rob Everyone/` + a small `launcher-state.json`, nothing else.

## Editor: remove the old popup from `MainMenu.unity`

The scripts are deleted, which leaves two GameObjects in `MainMenu.unity`
with a "Missing Script" warning — delete both from the scene hierarchy:

- **`UpdateChecker`** (root-level GameObject, had the `UpdateChecker`
  component)
- **`UpdatePopupPanel`** (root-level GameObject, the popup's UI panel)

### 🔴 Rest point

Open `MainMenu.unity`, confirm neither GameObject exists anymore and the
Console shows no missing-script warnings for them. Press Play — Main
Menu loads exactly as before, just with no update-check happening (that
now only ever runs from the standalone launcher, before the game
process even starts).

## Follow-ups (tracked separately)

- [Issue #42](https://github.com/joshuaclemons1/Rob-Everyone/issues/42)
  — Windows SmartScreen still shows "Windows protected your PC" on the
  launcher's own `.exe` the same way it did on the game's, until that's
  resolved (code signing or otherwise).
- [Issue #43](https://github.com/joshuaclemons1/Rob-Everyone/issues/43)
  — macOS/Linux game builds, and the per-platform release-asset naming
  convention this launcher's `SelectAssetForCurrentPlatform` is already
  written to expect.
- Real splash art (currently a placeholder gradient).
- A distributable installer/first-run experience — right now getting
  the launcher itself onto a new machine is still a manual "download
  and unzip the launcher once" step, same as the game is today.

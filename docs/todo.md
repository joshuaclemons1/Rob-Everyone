# To-dos

Everything genuinely open right now, pulled together from scattered
"still open"/"not done"/"remaining" notes across the individual stage
docs (several of which had drifted stale — see
[completed.md](completed.md)'s note on that). Roughly ordered by "should
happen soon" to "later stage."

## Priority order (the plan)

1. **Stage 5 real Steam overlay test** — parked until a second Steam
   account is available; not blocking anything else.
2. **Stage 7c Milestone F — rival-status HUD ping** — the one piece of
   the meta-game still not started; A–E (shop, Jail & Bail, Homeowner
   patrol, police dispatch pooling, night mode) are all done and
   playtested. See
   [stage7c-meta-game-setup.md](stages/stage7c-meta-game-setup.md).
3. **Stage 8 — friend-group playtest** — three alpha builds
   (`alpha-v1` → `alpha-v1.0.3`) have already gone out; Brian and
   Goodson just joined the team for house/level design and local
   playtesting. Treat this as underway, not "not started."
4. **Art & audio** — more house variants (Real_House_03 is in; loot
   variety is partially addressed but the "Good House" visual tell,
   ambient music, sabotage-item-specific SFX, skin unlock-gating, and
   the HUD result banner are still open), environmental detail. Mostly
   Zach/Brian/Goodson asset work.
5. **Stage 7c Milestone G (optional, lowest priority)** — randomized
   item value range per pickup.
6. **Housekeeping + code-review nits** — as they come up.

~~VoIP / proximity voice chat~~, ~~Settings menu~~, ~~held item in
hand + carry/run animations~~, and ~~carry/throw ragdolled players~~
are all done and playtested — see [completed.md](completed.md).

Detail for each below.

## Test when able

- **Stage 5 real Steam overlay test** — every Rest Point through
  `stage5-steam-multiplayer.md` Part 3 is done (Steamworks.NET +
  FizzySteamworks installed, `SteamLobby`/`SteamManager` wired, Host
  button correctly calls `SteamLobby.HostLobby()`, `DISABLESTEAMWORKS`
  removed from Scripting Define Symbols). **Rest Point 4 — the actual
  "two separate Steam accounts, real overlay invite" test — has not been
  run**: no second account/friend was available to test with. Assumed
  working based on Steam initializing cleanly and every earlier Rest
  Point passing, but needs a real two-account (or two-machine) pass to
  actually confirm the overlay invite → join → full batch flow, per that
  doc's own Rest Point 4 checklist, before this is genuinely "done."

## Art & audio (see art-info.md for full detail)

- **Loot variety** — partially addressed: `Real_House_03` (Zach's
  design) brought the pool to 3 houses and rolls `LootTable_Small`
  instead of piling onto `01`/`02`'s `LootTable_Medium`. Still worth a
  revisit once more house variants exist (now Brian/Goodson territory
  too): `LootTable_Large` isn't referenced by any house yet, and every
  house still has only 1 `LootSpawnPoint` (one roll per house).
- **"Good House" visual tell** — should read as visually distinct at a
  glance (gold accent trim, lighting, signage) since it's deliberately
  higher-risk/higher-reward. Not confirmed done.
- ~~Footstep / pickup / shop / AI / jail / impact SFX~~ — **done**, see
  [completed.md](completed.md)'s Audio section. Police siren (spatial)
  still not built.
- **Sabotage item use/impact SFX** — the generic PvP/car-impact hit
  sound now plays on every impact, but no *item-specific* sound exists
  yet (Taser zap, Bat/Hammer thwack, Tranq dart). Dynamite's
  `explosionClips` field is wired but empty — no CC0 explosion pack
  sourced yet.
- **Ambient music** — calm exploration + tense "spotted" loop,
  crossfading off the existing Homeowner/Police alert state transitions.
  Not built.
- **Skin unlock-gating** — every configured skin is currently pickable;
  the design calls for unlocks eventually (`ui-design.md`).
- **HUD result banner** — `RoundUI.cs`'s `resultText` is still plain
  text. Lower priority now that round-end flows into a loading screen +
  Lobby scene rather than lingering on this screen — worth re-scoping
  before building it rather than building the originally-designed
  version blind.
- **Environmental detail pass** (clutter, lighting bake, foliage) — later
  polish, once the map layout is fully settled.

## Bigger stages (per plan.md's build order)

- **Stage 4 — multiplayer, same machine** — Mirror over `localhost`.
  **Done, all 9 Rest Points confirmed working** (two-Editor ParrelSync
  testing) — see [completed.md](completed.md) for the full bug list
  found/fixed along the way.
- **Stage 5 — Steam multiplayer** — Steamworks.NET + FizzySteamworks,
  test AppID 480. Editor setup done through Part 3; the real overlay
  invite test (Rest Point 4) still needs a second Steam account — see
  the "Test when able" section near the top of this doc.
- **Stage 6 — sabotage items (Taser, Dynamite, Bat, Hammer, Tranquilizer
  Gun, Alarm Clock, PvP steal-window)** — **done and playtested**, both
  phases — see [completed.md](completed.md) for the full bug list and
  design detail found/fixed along the way.
- **Stage 7 — full meta-game (medium priority)** — the v1 shop/lobby
  loop and batch economy shipped first; Stage 7c's Milestones A–E then
  built the rest — buy-side shop (which *is* the sabotage-spending
  quota add-on, see below), real Jail & Bail with rescue/bond/self-bail,
  Homeowner patrol, police dispatch pooling, and night mode. **All done
  and playtested.** Only Milestone F (rival HUD ping) and optional G
  (randomized item values) remain — see
  [stage7c-meta-game-setup.md](stages/stage7c-meta-game-setup.md).
- **Stage 8 — playtest with the friend group** — underway: three alpha
  builds shipped (`alpha-v1` → `alpha-v1.0.3`), and Brian/Goodson joined
  for house/level design and local playtesting.
- ~~VoIP / in-game proximity voice chat~~ — **done, playtested working
  on first try.** See [completed.md](completed.md)'s Voice & settings
  section, [voip-setup.md](stages/voip-setup.md). The "team channel"
  toggle stays a Stage 8 question.
- ~~Settings menu~~ — **done, playtested working end to end**
  (Milestones A–G, including the in-game pause overlay in both `Lobby`
  and `SampleScene`). See [completed.md](completed.md)'s Voice & settings
  section, [settings-menu-setup.md](stages/settings-menu-setup.md).

## Housekeeping

- **Team**: Brian and Goodson joined for house/level design and local
  playtesting (`brian's-branch`/`goodson's-branch` exist on the remote).
  Point them at [Setup.md](../Setup.md) to get set up.
- **House pool** — `Real_House_03` (Zach's design) is in; more
  `Real_House_0X` variants still welcome, `HouseDesigner` scene exists to
  make that faster.
- **New Kenney packs** — remember the FBX import Scale Factor fix
  (`15`, matching `Kenney-CityKitSuburban`) before assuming a freshly
  imported pack (e.g. Industrial, Car Kit) "looks tiny" for some other
  reason.
- **Bug reports live in GitHub Issues now** — `.github/ISSUE_TEMPLATE/
  bug_report.md` (on `main`, so it shows up for everyone's "New Issue")
  covers anyone, including Brian/Goodson during local playtests, filing
  a real bug report. Check the
  [Issues tab](https://github.com/joshuaclemons1/Rob-Everyone/issues)
  periodically — it's not otherwise wired into this workflow. Replaces
  the short-lived `Known Bugs.md` file.

## Code-review nits (Stage 6 sabotage read-through)

Minor, none blocking — surfaced reviewing the Phase 1/2 commits.

**Fixed** in the inventory/UX pass:
- ~~`PlayerImpactRelay` class comment stale~~ — rewritten.
- ~~`ServerApplyPvpImpact` opening the steal window on a no-op stun~~ —
  now explicitly commented as deliberate.
- ~~Multi-attacker steal-window race~~ — the window is an expiry
  timestamp now, not racing coroutines.
- ~~Theft not discriminating what it takes~~ — moot: the thief now
  drag-picks the item off the victim's hotbar (still *can* take gear;
  making it loot-only is a one-liner if playtesting wants it — tracked
  in the setup doc's follow-ups).

**Still open:**
- **Sabotage cooldowns never reset between rounds** — tracked as
  [Issue #1](https://github.com/joshuaclemons1/Rob-Everyone/issues/1)
  now rather than described here. Low impact; a `ClearCooldowns()` from
  round start would be tidy.

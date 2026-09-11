# To-dos

Everything genuinely open right now, pulled together from scattered
"still open"/"not done"/"remaining" notes across the individual stage
docs (several of which had drifted stale — see
[completed.md](completed.md)'s note on that). Roughly ordered by "should
happen soon" to "later stage."

## Priority order (the plan)

1. **Stage 5 real Steam overlay test** — parked until a second Steam
   account is available; not blocking anything else.
2. **Stage 7 — full meta-game** (medium) — sabotage purchases, real
   Jail & Bail, sabotage-spending quota, night time mode.
3. **VoIP / proximity voice chat** (medium-low) — Steam's own voice API.
   Build guide: [voip-setup.md](stages/voip-setup.md).
4. **Stage 8 — friend-group playtest** — depends on 1–2.
5. **Art & audio** — more house variants (unblocks loot variety), all
   SFX, ambient music, "Good House" tell, skin unlock-gating, HUD
   result banner, environmental detail. Mostly Zach / asset work.
6. **Housekeeping + code-review nits** — as they come up.

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

- **Loot variety feels repetitive in play** — not an RNG bug (confirmed
  by reading the actual house prefabs/tables): 33 `ItemDefinition`s
  exist now, but only 2 house prefab variants
  (`Real_House_01`/`Real_House_02`) fill all 15+2 house slots, **both**
  wired to the same `LootTable_Medium` (13 items), and each house has
  only 1 `LootSpawnPoint` — one roll per house. `LootTable_Small` (13
  items) and `LootTable_Large` (7 items) aren't referenced by any house
  at all right now. Fixing this for real needs more house variants
  (blocked on Zach's house pool work below) — once those exist, revisit
  which table each house tier uses and whether 1 spawn point per house
  is enough, rather than just patching the 2 current houses in
  isolation.
- **"Good House" visual tell** — should read as visually distinct at a
  glance (gold accent trim, lighting, signage) since it's deliberately
  higher-risk/higher-reward. Not confirmed done.
- **SFX** — none implemented yet. Confirmed trigger list in
  `art-info.md`: Homeowner state-transition stingers, police siren
  (spatial), per-movement-state footsteps, item pickup/caught/jailed,
  car horn + driver "yelling" line on traffic-hazard impact (fields
  already exist on `CarDriver.cs`, just need clips).
- **Ambient music** — calm exploration + tense "spotted" loop,
  crossfading off the existing Homeowner/Police alert state transitions.
  Not built.
- **Skin unlock-gating** — every configured skin is currently pickable;
  the design calls for unlocks eventually (`ui-design.md`).
- **Sabotage item SFX** — no use/impact SFX exist for any sabotage item
  yet (Taser zap, Dynamite blast, Bat/Hammer thwack, Tranq dart, Alarm
  Clock ring). Icons aren't needed — the hotbar uses the 3D model as its
  preview, same as regular loot.
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
  loop and batch economy above are a deliberately scoped-down slice.
  Still missing: sabotage purchases, real Jail & Bail
  (rescue/bond/self-bail), the personal sabotage-spending quota add-on.
  See `gameplay-design.md` for the full design.
- **Stage 8 — playtest with the friend group** — not started, depends on
  Stage 4-6 existing.
- **VoIP / in-game proximity voice chat (medium-low priority)** — not
  started. Full build plan now written up:
  [voip-setup.md](stages/voip-setup.md) — Steam's own voice API
  (`SteamUser.*Voice*`, already available via the Stage 5 Steamworks.NET
  install) captured locally, relayed as ordinary Mirror Rpcs over the
  FizzySteamworks connection, played back through a 3D `AudioSource` on
  each speaker's player object so distance attenuation is automatic. The
  doc settles the design calls (proximity not team-wide, push-to-talk,
  unreliable channel, never hear yourself); the "team channel" toggle
  stays a Stage 8 question.

## Housekeeping

- **Zach's house pool work** — still hasn't started building the
  additional `Real_House_0X` variants; `HouseDesigner` scene exists to
  make that faster whenever he picks it up.
- **New Kenney packs** — remember the FBX import Scale Factor fix
  (`15`, matching `Kenney-CityKitSuburban`) before assuming a freshly
  imported pack (e.g. Industrial, Car Kit) "looks tiny" for some other
  reason.

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
- **Sabotage cooldowns never reset between rounds** —
  `SabotageUseController.nextReadyTime` keys off `Time.time`, continuous
  across the Lobby round-trip, so a Taser fired near the end of a round
  can still be on cooldown at the start of the next. Low impact; a
  `ClearCooldowns()` from round start would be tidy.

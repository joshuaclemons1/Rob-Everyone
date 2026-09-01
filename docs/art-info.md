# Art Info

Single working doc for visual style, palette, sourced assets, and the
art/audio to-do list. Replaces the old `art-style.md`, `artist-todo.md`,
and `asset-sources.md` (consolidated here so there's one place to work
out of).

Cross-reference: build stages are defined in [plan.md](plan.md). Lawn/yard
ground material: [lawn-shader-setup.md](lawn-shader-setup.md).

---

## Style direction

**Inspiration games:** PEAK, Gamble with your friends, Lethal Company.

- **Low poly** — minimal 3D designs for items, players, and environment
  pieces. Favor simple, readable silhouettes over surface detail.
- **Clean aesthetic** — simple color palettes, not busy or cluttered.
- **Cartoonish, not overly stylized** — leans cartoony (like the inspo
  games above) but stops short of exaggerated proportions or a "toy-like"
  look. Grounded enough to still read as a heist, not a kids' game.

## Color palette — "Bright Cartoon Suburbia"

Locked base (daytime) palette. Closest to PEAK — vivid, saturated, sunny.
Most "cartoonish" of the directions considered, while staying low poly and
clean rather than exaggerated.

| Use | Hex | Swatch |
|---|---|---|
| Wall / stucco | `#F5EFE0` | off-white |
| Roof | `#E76F51` | coral red |
| Grass / ground | `#6FCF97` | bright green |
| Sky | `#6EC6E8` | vivid sky blue |
| Good House accent | `#FFD166` | bright gold |
| Police / danger accent | `#EF476F` | hot red |
| Police / danger accent (secondary) | `#118AB2` | bright blue |
| UI accent | `#FFD166` | bright gold (matches Good House) |

## Lighting & time of day

Baseline lighting is **bright daytime** — clear sky, soft shadows, high
readability. Most rounds play in daytime.

Planned future feature (see `plan.md`'s "Future ideas" section): a
**time-of-day system** that occasionally swaps a round to dusk or night,
raising difficulty (lower visibility). Not being built yet — stays
daytime-only through at least Stage 3 — but the palette should keep these
variants in mind so the swap doesn't require redesigning colors later.

Draft variant tints (unlocked, needs revisiting once the feature is
actually built and playtested for readability):

| Use | Daytime | Dusk (draft) | Night (draft) |
|---|---|---|---|
| Sky | `#6EC6E8` | `#E8955C` (warm orange) | `#1B2430` (deep navy) |
| Ambient light | full brightness | warm low-angle, longer shadows | streetlamp/window glow as main light source |
| Good House accent | `#FFD166` | `#FFD166` (glows brighter against dim bg) | `#FFD166` (same — should read as a beacon at night) |
| Police / danger accent | `#EF476F` / `#118AB2` | unchanged | unchanged, but higher contrast against dark background |

Reasoning: keep house/UI accent colors constant across all three states so
players don't have to relearn what "Good House" or "danger" looks like —
only the ambient/sky lighting shifts. Difficulty should come from
*visibility* (darker ambient, smaller lit radius), not from changing what
colors mean.

---

## Sourced assets (imported)

All CC0 (public domain — no attribution required, fully commercial-safe).
Binaries route through Git LFS (`.gitattributes`, set up already) — if a
pull brings down LFS pointer text instead of real files, run
`git lfs install` once, then `git lfs pull`.

| Pack | Use | In repo | Source |
|---|---|---|---|
| Kenney City Kit (Suburban) | House exteriors — the "reuse 2–3 shells" kit for the map's house ring | `Assets/Art/Environment/Kenney-CityKitSuburban/` | [kenney.nl/assets/city-kit-suburban](https://kenney.nl/assets/city-kit-suburban) |
| Kenney Modular Buildings | Kitbash pieces for custom variants (e.g. a visually distinct "Good House") | `Assets/Art/Environment/Kenney-ModularBuildings/` | [kenney.nl/assets/modular-buildings](https://kenney.nl/assets/modular-buildings) |
| Kenney City Kit (Roads) | Streets/sidewalks connecting the houses | `Assets/Art/Environment/Kenney-CityKitRoads/` | [kenney.nl/assets/city-kit-roads](https://kenney.nl/assets/city-kit-roads) |
| Kenney City Kit (Commercial) | Police station / fenced compound | `Assets/Art/Environment/Kenney-CityKitCommercial/` | [kenney.nl/assets/city-kit-commercial](https://kenney.nl/assets/city-kit-commercial) |
| Quaternius Ultimate Animated Character Pack | Homeowners, police, (later) player skins — 50+ animated low-poly characters, pick/reskin per role | `Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/` | [quaternius.com/packs/ultimatedanimatedcharacter.html](https://quaternius.com/packs/ultimatedanimatedcharacter.html) |

Only FBX + Textures were kept from each pack (each also ships redundant
OBJ/glTF/Blend copies); each folder still has its `License.txt`.

None of these are placed in the scene yet — imported to the repo only.
**That's the starting point for next session:** open Unity, let it
reimport `Assets/Art/`, then start swapping placeholder geometry for real
pieces (houses first — Stage 3's prefabs currently reference gray-box
placeholders).

## Folder convention

- Art: `Assets/Art/<Category>/<PackName>/` — currently `Environment/` and
  `Characters/`, following the source packs above rather than a 1:1 mirror
  of `Assets/Scripts/<System>/`.
- Audio: `Assets/Audio/<System>/` (e.g. `Assets/Audio/SFX/`) — doesn't
  exist yet, create it when the first real audio asset lands.

## Format & export cheatsheet

For anything custom-made (not sourced from a pack above):

| Asset type | Format | Notes |
|---|---|---|
| UI/HUD/icons | PNG, with alpha | Export @2x actual display size for crispness; Unity imports as Sprite (2D and UI) |
| Textures (materials) | PNG or TGA | Power-of-2 dimensions (512, 1024, 2048); keep a source .PSD alongside |
| 3D models | FBX | 1 unit = 1 meter in your 3D tool so scale matches Unity on import |
| Music | WAV (source) → OGG (in-project) | Loop points matter for ambient tracks |
| SFX | WAV | Short one-shots, 44.1kHz is plenty |

---

## To-do, by when it's safe to start

"Safe to start" means the thing it depends on (map layout, UI flow, item
list) is locked enough that the work won't get thrown away.

### Start anytime (style-independent)

- ~~Mood board / visual style~~ — done, see Style direction above.
- ~~Color palette~~ — done, see Color palette above.
- **HUD style pass** — money counter, quota bar, inventory readout. Mock up
  against a screenshot of the current scene. Plugs directly into
  [InventoryUI.cs](../Assets/Scripts/UI/InventoryUI.cs) once it looks right.
- **Musical identity** — sketch the game's sonic palette (instrumentation,
  tempo, genre lean). Doesn't need to be a finished track yet.
- **Logo / title treatment** — for the eventual title screen, no rush.

### Needed for Stage 3 (full offline loop) — current stage

- ~~House exterior kit~~ — sourced (Kenney City Kit Suburban + Modular
  Buildings, imported). Still needs placing into prefabs/scene.
- ~~Police station + fenced compound~~ — sourced (Kenney City Kit
  Commercial, imported). Still needs placing into scene.
- ~~Homeowner character~~ / ~~Police character~~ — sourced (Quaternius
  pack, imported). Still needs: picking specific character(s) per role,
  wiring animations, and the alert-state reads (Idle → Suspicious →
  Alerted for homeowner; patrol/chase for police).
  **Model/export the face pointing local +Z** (Unity's forward
  convention) at rotation (0,0,0) — the vision-cone AI in
  [HomeownerAI.cs](../Assets/Scripts/AI/HomeownerAI.cs) always looks along
  `transform.forward`. If a picked character's face doesn't match that
  axis, wrap it as a child of an empty parent and rotate just the child to
  compensate — keep the AI components on the parent.
- **Loot item models/icons** — one per loot type (watch, cash, jewelry,
  etc.). Simple modeled props or distinct-colored/shaped primitives with a
  Photoshop-made icon are enough to unblock gameplay testing — doesn't
  need to be final art yet. Not sourced from a pack; likely custom/simple.
- **"Good House" visual tell** — since these are deliberately placed next
  to the police station and carry better loot, they should read as
  visually distinct at a glance (trim color — use the gold accent from the
  palette above — lighting, signage).
- **Exit / extraction point** — needs to be readable as "the goal" from a
  distance.
- **SFX** — footsteps, item pickup, homeowner alert stinger, police siren,
  player caught/jailed.
- **Ambient music states** — calm exploration loop and a tenser "you've
  been spotted" loop, since the homeowner/police alert states are binary
  triggers you can hook music changes to later.

### Needed for Stage 5–6 (Steam multiplayer, sabotage)

- **Player skins/colors** — players need to be visually distinguishable
  from each other once there's more than one on screen. Likely reskins of
  Quaternius characters already imported.
- **Sabotage item icons + models** — taser, hammer, alarm clock, bat (per
  plan.md's Core Systems list).
- **Sabotage SFX** — taser zap, hammer hit, alarm ring, bat swing, plus a
  "you've been sabotaged" stinger.

### Needed for Stage 7 (meta-game)

- **Pre-round shop UI** — full screen, needs the final loot/sabotage item
  icon set locked first.
- **Results/scoreboard screen** — end-of-round and end-of-game (5 rounds)
  summary.
- **Shop SFX** — purchase confirm, insufficient funds.

### Later / polish (no fixed stage)

- Full homeowner/police animation sets (idle, walk, run, react) beyond the
  placeholder poses used to unblock Stage 3.
- Environmental detail pass (clutter, lighting bake, foliage) once the map
  layout is finalized end-to-end.
- Title screen, credits, settings menu art.
- Steam capsule/store art (only matters near release).

## Open / TODO

- Place the sourced Kenney/Quaternius assets into the actual scene
  (see "starting point for next session" above).
- Playtest the dusk/night draft tints once the time-of-day system is
  actually built — confirm accent colors still read clearly against a
  dark background.
- Any concept art or reference screenshots from the inspo games worth
  pinning here.

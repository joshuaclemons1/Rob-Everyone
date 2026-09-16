# Art Info

Single working doc for visual style, palette, sourced assets, and the
art/audio to-do list. Replaces the old `art-style.md`, `artist-todo.md`,
and `asset-sources.md` (consolidated here so there's one place to work
out of).

Cross-reference: build stages are defined in [plan.md](plan.md). Lawn/yard
ground material: [lawn-shader-setup.md](stages/lawn-shader-setup.md). Full UI/menu
element spec: [ui-design.md](stages/ui-design.md).

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

Mostly CC0 (public domain — no attribution required, fully commercial-safe);
one exception is called out below. Binaries route through Git LFS
(`.gitattributes`, set up already) — if a pull brings down LFS pointer text
instead of real files, run `git lfs install` once, then `git lfs pull`.

| Pack | Use | In repo | Source | License |
|---|---|---|---|---|
| Kenney City Kit (Suburban) | House exteriors — the "reuse 2–3 shells" kit for the map's house ring | `Assets/Art/Environment/Kenney-CityKitSuburban/` | [kenney.nl/assets/city-kit-suburban](https://kenney.nl/assets/city-kit-suburban) | CC0 |
| Kenney Modular Buildings | Kitbash pieces for custom variants (e.g. a visually distinct "Good House") | `Assets/Art/Environment/Kenney-ModularBuildings/` | [kenney.nl/assets/modular-buildings](https://kenney.nl/assets/modular-buildings) | CC0 |
| Kenney City Kit (Roads) | Streets/sidewalks connecting the houses | `Assets/Art/Environment/Kenney-CityKitRoads/` | [kenney.nl/assets/city-kit-roads](https://kenney.nl/assets/city-kit-roads) | CC0 |
| Kenney City Kit (Commercial) | Police station / fenced compound | `Assets/Art/Environment/Kenney-CityKitCommercial/` | [kenney.nl/assets/city-kit-commercial](https://kenney.nl/assets/city-kit-commercial) | CC0 |
| Kenney City Kit (Industrial) | Police compound dressing (buildings/tanks/containers) — sourced looking for a better compound-fence read; **doesn't actually include fence/chain-link pieces**, still need those separately | `Assets/Art/Environment/Kenney-CityKitIndustrial/` | [kenney.nl/assets/city-kit-industrial](https://kenney.nl/assets/city-kit-industrial) | CC0 |
| Kenney Car Kit | Parked cars for driveways now; road traffic later. Includes a `police.fbx` and `tractor-police.fbx` — worth a look for the compound too | `Assets/Art/Environment/Kenney-CarKit/` | [kenney.nl/assets/car-kit](https://kenney.nl/assets/car-kit) | CC0 |
| Kenney Skyboxes | 5 panoramic (equirectangular, 4096×2048) sky textures — day/morning/night/alien/space. The **day** one is wired as the scene skybox (Stage 3i, see [issue #30](https://github.com/joshuaclemons1/Rob-Everyone/issues/30)); the Morning/Day/Night time-of-day system (`NightModeVisuals.cs`, see [issue #33](https://github.com/joshuaclemons1/Rob-Everyone/issues/33)) is built now too — check whether it's actually drawing from the morning/night panoramas here or separate art before assuming either way; alien/space are still unused | `Assets/Art/Environment/Kenney-Skyboxes/` | [kenney.nl/assets/skyboxes](https://kenney.nl/assets/skyboxes) | CC0 |
| Quaternius Ultimate Animated Character Pack | Homeowners, police, (later) player skins — 50+ animated low-poly characters, pick/reskin per role | `Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/` | [quaternius.com/packs/ultimatedanimatedcharacter.html](https://quaternius.com/packs/ultimatedanimatedcharacter.html) | CC0 |
| "Jail" by Poly by Google | Police station jail cell — single low-poly cell model (flat-colored materials, no textures) | `Assets/Art/Environment/PolyByGoogle-Jail/` | [poly.pizza/m/bF8mr05ofaY](https://poly.pizza/m/bF8mr05ofaY) | **CC-BY 3.0 — needs credit in the eventual credits screen**, see the folder's `License.txt` |
| Concrete030 (ambientCG) | Compound/parking-lot ground material — Color/Normal/Roughness/AO PBR set, 2K | `Assets/Art/Environment/Textures/Concrete030/` | [ambientcg.com/view?id=Concrete030](https://ambientcg.com/view?id=Concrete030) | CC0 |
| Chain Link Fence Pack (TampaJoey) | Compound perimeter fence — replaces the CityKitSuburban picket fence that didn't read as a security boundary. Real modeled geometry (short wall, curb base, residential variant) with an optional barbed-wire attachment and signs. Superseded the Quaternius "Metal Fence" model (removed — its FBX was missing the alpha-cutout texture the mesh depends on for its chain-link holes, so it rendered as a solid gray plane instead) | `Assets/Art/Environment/TampaJoey-ChainLinkFence/` | [sketchfab.com — Chain Link Fence Pack](https://sketchfab.com/3d-models/chain-link-fence-pack-low-poly-game-ready-777e50cd6e5d4db99d70bf7b20370f7a) | **CC-BY 4.0 — needs credit in the eventual credits screen**, see the folder's `License.txt` |

Only FBX + Textures were kept from each Kenney pack (each also ships
redundant OBJ/glTF/Blend copies); each folder still has its `License.txt`.
The Poly Pizza jail model only ships as OBJ (+ .mtl, no separate texture
files — its materials are flat colors), kept under an `OBJ/` subfolder
instead.

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
- **HUD style pass** — full element-by-element spec (crosshair, money/
  quota/timer treatment, carry slots, item icons, shop screen, menus, and
  more) is in [ui-design.md](stages/ui-design.md), organized by what's safe to
  build now vs. later. Mock up against a screenshot of the current scene.
  Plugs directly into [InventoryUI.cs](../Assets/Scripts/UI/InventoryUI.cs)
  and [RoundUI.cs](../Assets/Scripts/UI/RoundUI.cs) once it looks right.
- **Musical identity** — sketch the game's sonic palette (instrumentation,
  tempo, genre lean). Doesn't need to be a finished track yet. Full track
  list, format, and looping/transition spec now in
  [music-brief.md](music-brief.md).
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
- **SFX** — confirmed trigger list (see
  [gameplay-design.md](stages/gameplay-design.md) for the design context):
  - A short stinger on each Homeowner state transition (Idle→Suspicious,
    Suspicious→Alerted) — immediate audio feedback without needing to be
    looking at the Homeowner.
  - Police siren, spatial/directional, when Police enters Respond/Chase —
    doubles as gameplay info ("police coming, roughly from where"), not
    just flavor.
  - Distinct footstep sounds per movement state (walk/sprint/crouch) —
    since detection is vision-cone-only (no real noise-propagation
    system, see gameplay-design.md's "Detection & AI behavior"), this is
    player-feedback flavor, not an AI-detectable signal.
  - Item pickup, player caught/jailed — as before.
  - Car horn + a short driver "yelling at the player" line, on impact
    (`CarDriver.cs`, Stage 3j traffic hazard) — fields already exist on
    the component, just need clips dropped in.
- **Ambient music states** — calm exploration loop and a tenser "you've
  been spotted" loop, **crossfading based on nearby Homeowner/Police alert
  state** (confirmed trigger, not just a concept) — the binary state
  transitions already in `HomeownerAI.cs`/`PoliceAI.cs` are what should
  drive the crossfade. See [music-brief.md](music-brief.md) for the exact
  layered-stems approach this needs and the full track list (Day/Night
  variants, Menu/Lobby themes, stingers).

### Needed for Stage 5–6 (Steam multiplayer, sabotage)

- ~~Player skins/colors~~ — built ahead of schedule: a Body-color palette
  picker + live preview, see
  [main-menu-customization-setup.md](stages/main-menu-customization-setup.md).
  Not yet wired onto actual networked players (Stage 4–5 doesn't exist
  yet), just the selection/persistence/preview system.
- **Sabotage item icons + models** — taser, hammer, alarm clock, bat (per
  plan.md's Core Systems list).
- **Sabotage SFX** — taser zap, hammer hit, alarm ring, bat swing, plus a
  "you've been sabotaged" stinger.

### Needed for Stage 7 (meta-game)

- **Pre-round shop UI** — full screen, needs the final loot/sabotage item
  icon set locked first. Per gameplay-design.md, this is a **ready-up
  space players physically walk into/out of**, not a menu with a Ready
  button — needs a floor marker/zone that reads clearly as "stand here,"
  plus the carry-slots readout (5 slots + Prison Wallet) and active
  sabotage-item cooldowns from the HUD needs list.
- **Batch-end summary** — there's no fixed end-of-game screen (endless
  freeplay, no formal winner — see gameplay-design.md's "Win condition"),
  but each 3-round batch boundary (quota step-up, new shop unlocks, any
  surplus Cash wiped) is a real beat worth a UI moment.
- **Shop SFX** — purchase confirm, insufficient funds, item sold.

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

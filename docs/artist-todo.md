# Artist To-Do

A running list of every art/audio/UI asset the game will eventually need,
organized by *when it's safe to start* rather than by discipline. "Safe to
start" means the thing it depends on (map layout, UI flow, item list) is
locked enough that the work won't get thrown away.

Cross-reference: build stages are defined in [plan.md](plan.md).

## Format & export cheatsheet

| Asset type | Format | Notes |
|---|---|---|
| UI/HUD/icons | PNG, with alpha | Export @2x actual display size for crispness; Unity imports as Sprite (2D and UI) |
| Textures (materials) | PNG or TGA | Power-of-2 dimensions (512, 1024, 2048); keep a source .PSD alongside |
| 3D models | FBX | 1 unit = 1 meter in your 3D tool so scale matches Unity on import |
| Music | WAV (source) → OGG (in-project) | Loop points matter for ambient tracks — see Music section |
| SFX | WAV | Short one-shots, 44.1kHz is plenty |

Folder convention to mirror the code layout in [CLAUDE.md](../CLAUDE.md)
(`Assets/Scripts/<System>/`): put art under `Assets/Art/<System>/` and audio
under `Assets/Audio/<System>/` (e.g. `Assets/Art/UI/`, `Assets/Audio/SFX/`)
once you start importing. Doesn't exist yet — create it when the first real
asset lands.

**Before importing any binary art/audio files**, we need to add a
`.gitattributes` for Git LFS — it's referenced in CLAUDE.md but was never
actually committed. Flagging again here so it doesn't get missed once real
files start landing.

---

## Start anytime (style-independent)

Nothing below depends on code or layout — pure exploration, do these whenever inspiration strikes.

- **Mood board / visual style** — tone reference (cartoony heist vs. grounded/gritty), lighting mood, palette direction.
- **Color palette** — a locked set of hex values for houses, UI, "Good Houses" accent color, police/danger accent color.
- **HUD style pass** — money counter, quota bar, inventory readout. Mock up in Photoshop against a screenshot of the current gray-box scene. Plugs directly into [InventoryUI.cs](../Assets/Scripts/UI/InventoryUI.cs) once it looks right.
- **Musical identity** — sketch the game's sonic palette (instrumentation, tempo, genre lean). Doesn't need to be a finished track yet, just proof of direction.
- **Logo / title treatment** — for the eventual title screen, no rush.

## Needed for Stage 3 (full offline loop)

Stage 3 adds multiple houses, a quota, homeowner AI, police AI, and an exit — this is where "does it look like a heist" starts to matter.

- **Loot item models/icons** — one per loot type (watch, cash, jewelry, etc.). Can be simple modeled props (Blender) or even just distinct-colored/shaped primitives with a Photoshop-made icon for the inventory UI — doesn't need to be final art to unblock Stage 3 gameplay testing.
- **House exterior kit** — 2–3 reusable house shells per [plan.md](plan.md)'s "reuse 2–3 house prefabs" note. Modular kit (walls/roof/door pieces) in Blender beats 10 unique builds.
- **"Good House" visual tell** — since these are deliberately placed next to the police station and carry better loot, they should read as visually distinct at a glance (trim color, lighting, signage).
- **Homeowner character** — model + the 3 alert-state reads (Idle → Suspicious → Alerted). Even a simple rig with 3 poses/animations communicates state without needing full animation work yet. **Model/export the face pointing local +Z** (Unity's forward convention) at rotation (0,0,0) — the vision-cone AI in [HomeownerAI.cs](../Assets/Scripts/AI/HomeownerAI.cs) always looks along `transform.forward`, so if the face doesn't match that axis on import (a common Blender export gotcha, since Blender's own forward is -Y), the cone and the visible face will point different directions. Fix by re-exporting with correct orientation, or wrap the model as a child of an empty parent and rotate just the child to compensate — keep the AI components on the parent.
- **Police character** — model + patrol/chase animation.
- **Police station + fenced compound** — matches the central-compound layout from the map sketch in plan.md.
- **Exit / extraction point** — needs to be readable as "the goal" from a distance.
- **SFX** — footsteps, item pickup, homeowner alert stinger, police siren, player caught/jailed.
- **Ambient music states** — calm exploration loop and a tenser "you've been spotted" loop, since the homeowner/police alert states are binary triggers you can hook music changes to later.

## Needed for Stage 5–6 (Steam multiplayer, sabotage)

- **Player skins/colors** — players need to be visually distinguishable from each other once there's more than one on screen.
- **Sabotage item icons + models** — taser, hammer, alarm clock, bat (per plan.md's Core Systems list).
- **Sabotage SFX** — taser zap, hammer hit, alarm ring, bat swing, plus a "you've been sabotaged" stinger.

## Needed for Stage 7 (meta-game)

- **Pre-round shop UI** — full screen, needs the final loot/sabotage item icon set to be locked first.
- **Results/scoreboard screen** — end-of-round and end-of-game (5 rounds) summary.
- **Shop SFX** — purchase confirm, insufficient funds.

## Later / polish (no fixed stage)

- Full homeowner/police animation sets (idle, walk, run, react) beyond the placeholder poses used to unblock Stage 3.
- Environmental detail pass (clutter, lighting bake, foliage) once the map layout is finalized end-to-end.
- Title screen, credits, settings menu art.
- Steam capsule/store art (only matters near release).

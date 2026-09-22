# Post-processing pass (#64) — Editor walkthrough

Pure Editor work, no code — good laptop task. Rest points (🔴) after
each stage; test in Play Mode before moving on.

## What's actually true right now (checked directly, not assumed)

Worth reading before touching anything — the real state turned out to
be more specific than "no post-processing exists":

- **`SampleScene.unity` already has a real, working baseline.** It has
  a `Global Volume` GameObject (`m_IsGlobal: 1`, priority 0, weight 1)
  pointing at `Assets/Settings/SampleSceneProfile.asset`, which already
  has genuine (if modest) values: Bloom **on** (intensity 0.25,
  threshold 1, scatter 0.5), Tonemapping **on** (Neutral mode), Vignette
  **on** (intensity 0.2), Motion Blur present but **off**. This guide
  is about pushing this further and filling real gaps in it (it has no
  Color Adjustments at all), not starting from nothing.
- **`MainMenu.unity` and `Lobby.unity` have no Volume component at
  all.** Confirmed by scene grep — zero. They fall back entirely to
  the project-wide default, which is currently neutralized (next
  point) — meaning **zero post-processing on the new #51 drone
  flythrough background**, since that's the `Lobby` scene loaded
  additively behind the Main Menu. This is the single easiest place
  for this pass to be visible.
- **The project-wide fallback profile is a "blank template," not
  actually missing.** `Assets/Settings/DefaultVolumeProfile.asset` is
  wired in as URP's global default via
  `UniversalRenderPipelineGlobalSettings.asset`, and it does have all
  19 major override categories switched on (Bloom, Color Adjustments,
  Tonemapping, Vignette, etc.) — but checked every value directly:
  every single one is set to its neutral/no-op default (Bloom
  intensity `0`, Tonemapping mode `None`, Color Adjustments all `0`).
  So it's technically "on" everywhere and visually equivalent to
  nothing. This is the file to give real values to first, since
  MainMenu/Lobby inherit straight from it.
- **This same file also has some harmless clutter** — leftover blocks
  named things like `CopyPasteTestComponent1/2/3`, `TestVolume`,
  `OasisFogVolumeComponent` (Unity's own internal URP test-suite
  debris, likely from an Editor copy/paste gone sideways at some
  point). Confirmed these are **not** in the profile's actual
  `components` list, so they do nothing and are safe to ignore. Worth
  a mention only so it doesn't look alarming if you open the file in a
  text editor — nothing to fix.
- **SSAO (ambient occlusion) is already on and reasonably tuned** —
  confirmed on `PC_Renderer.asset`: `m_Active: 1`, Intensity `0.4`,
  Radius `0.3`. Not a gap. No action needed here; mentioned so you
  don't spend time re-adding it.
- **Skip these on purpose**: Depth of Field (blurs the background,
  which hurts fairness in a competitive multiplayer game — players
  need to actually see rivals/police at range) and Chromatic
  Aberration (fights legibility more than it adds mood, in a game this
  fast-paced). Motion Blur should stay off for the same reason it
  already is (reduces clarity in fast first-person movement, and can
  cause motion sickness for some players). Don't add these unless you
  specifically want to try one and see.

## How Unity's Volume system works (read this before editing)

Two independent toggles per effect, easy to trip over if you don't
know they're separate:

1. **The component's own `active` checkbox** (next to the effect's
   name, e.g. "Bloom") — whether this override group participates at
   all.
2. **Each individual field's own checkbox** (to the left of each
   slider, e.g. next to "Intensity") — whether *that specific value*
   overrides whatever a lower-priority Volume set, or falls through.
   An unchecked field is ignored even if the component itself is
   active.

Both need to be checked for a value to actually apply — this is
exactly why `DefaultVolumeProfile.asset` looks "on" everywhere but
does nothing: every field's override checkbox is ticked, but every
value is left at neutral. Ticking the checkbox and leaving the
slider at `0`/default is a no-op; you have to also move the slider.

A `Volume` component on a GameObject (`Global Volume` in the existing
scenes) points at one of these profile assets via its `Profile` field,
and needs **`Is Global`** checked (it already is, on the existing one)
to affect the whole scene rather than a local trigger zone.

## Starting points to compare

Not final numbers — starting points to punch in, look at in Play Mode,
and nudge from. All four keep Motion Blur off, Depth of Field off,
Chromatic Aberration off, per the reasoning above. **Preset D is the
current pick** — confirmed a good look at the end of Stage 1's
comparison, built directly off real Super Battle Golf gameplay
screenshots (not its marketing key art, which turned out to be
misleading — see D's own notes).

### A — Subtle / Cinematic

Closest to what `SampleSceneProfile.asset` already quietly has, pushed
a little further. Safe, unlikely to fight the existing textures.

| Effect | Field | Value |
|---|---|---|
| Bloom | Threshold | `1.0` |
| Bloom | Intensity | `0.3` |
| Bloom | Scatter | `0.5` |
| Tonemapping | Mode | `Neutral` |
| Color Adjustments | Post Exposure | `0` |
| Color Adjustments | Contrast | `5` |
| Color Adjustments | Saturation | `5` |
| Vignette | Intensity | `0.2` |
| Vignette | Smoothness | `0.4` |

### B — Vibrant / Stylized

Leans into the bright, punchy, low-poly-cartoony energy the four
reference games (and this project's own existing Kenney-sourced art
direction) already share. Probably the better fit for this project's
established look — worth starting comparisons here.

| Effect | Field | Value |
|---|---|---|
| Bloom | Threshold | `0.9` |
| Bloom | Intensity | `0.6` |
| Bloom | Scatter | `0.6` |
| Tonemapping | Mode | `Neutral` |
| Color Adjustments | Post Exposure | `0.1` |
| Color Adjustments | Contrast | `15` |
| Color Adjustments | Saturation | `20` |
| Vignette | Intensity | `0.3` |
| Vignette | Smoothness | `0.3` |

### C — Warm Heist Daytime

Same intensity range as B, but a genuinely different mood via color
temperature rather than just "more of the same slider" — a warm
golden-hour tint, since the game's whole setting is a bright daytime
suburb, not a grim/cool horror palette like Lethal Company's.

| Effect | Field | Value |
|---|---|---|
| Bloom | Threshold | `0.9` |
| Bloom | Intensity | `0.5` |
| Bloom | Scatter | `0.6` |
| Bloom | Tint | warm off-white, e.g. `#FFF1D6` |
| Tonemapping | Mode | `Neutral` |
| White Balance | Temperature | `8` (slightly warm) |
| Color Adjustments | Contrast | `10` |
| Color Adjustments | Saturation | `15` |
| Color Adjustments | Color Filter | very light warm tint, e.g. `#FFF6E8` |
| Vignette | Intensity | `0.25` |
| Vignette | Color | dark warm brown rather than pure black, e.g. `#1A1208` |

### D — Super Battle Golf Stylized

Added after actually comparing Super Battle Golf's *marketing key art*
against its *real gameplay screenshots* — they don't match, and the
real screenshots point somewhere more specific than "turn everything
up":

- **No black outlines/cel-shading anywhere in real gameplay.** The key
  art (store page hero image) is hand-painted with heavy ink outlines;
  actual gameplay screenshots have none — it's smooth-shaded low-poly,
  not toon-shaded. Worth knowing before chasing an outline-shader
  project (a much bigger undertaking — a new Renderer Feature plus
  reworking every material) — that's not actually what's giving the
  game its identity.
- **Bloom is restrained, not heavy** — none of the real screenshots
  show a hazy glow wash. The "vibrant" read comes from saturated,
  clearly-separated flat material colors and simple, soft, single-key-
  light shading, not from post-processing glow. Preset B's `0.6`
  bloom intensity is likely *too much* for this specific look — pull
  it back down.
- **Vignette is close to absent.** Consistent with the "clarity of
  feedback" design value already noted in
  [retention-research.md](retention-research.md) — nothing crowds the
  edges of the frame.
- **Saturation and contrast are pushed hard**, and warm — sandy
  beaches, warm greens, a consistently warm sky gradient even in the
  "cooler" desert course.
- **The mowed-stripe grass pattern in every course screenshot is the
  exact same idea this project's own `LawnStripes.shadergraph` already
  does** — worth knowing this isn't a gap to close, it's already the
  right call; just don't let post-processing wash the stripes out
  (keep bloom/vignette modest, per above).

| Effect | Field | Value |
|---|---|---|
| Bloom | Threshold | `1.0` |
| Bloom | Intensity | `0.35` (pulled back from B/C — see reasoning above) |
| Bloom | Scatter | `0.5` |
| Tonemapping | Mode | `Neutral` |
| White Balance | Temperature | `10` (warmer than C) |
| Color Adjustments | Post Exposure | `0.1` |
| Color Adjustments | Contrast | `20` |
| Color Adjustments | Saturation | `30` |
| Color Adjustments | Color Filter | very light warm tint, e.g. `#FFF6E8` |
| Vignette | Intensity | `0.15` |
| Vignette | Smoothness | `0.4` |

**One non-Volume tweak worth trying alongside this**, since it's part
of what's actually giving Super Battle Golf its clean look and is
still pure Editor work: find the scene's main Directional Light and
soften its shadows a touch (lower **Shadow Strength** slightly, e.g.
`0.8` instead of `1`, and/or increase **Normal Bias** a little) — a
single soft-shadowed warm key light reads a lot closer to Super Battle
Golf's screenshots than Unity's default hard-edged shadow. Not part of
the Volume profile, but worth doing in the same sitting since it's the
same kind of "no code, just Inspector values" work.

## Stage 1 — give the project-wide default real values

This fixes `MainMenu.unity` and `Lobby.unity` in one place, since
neither has its own Volume yet and both currently fall straight
through to this file.

1. Open `Assets/Settings/DefaultVolumeProfile.asset` (select it in the
   Project window — it opens in the Inspector like any other asset,
   no scene needed).
2. Use preset **D** (Super Battle Golf Stylized) — confirmed the
   direction to go with after comparing live in the Editor, and this
   is the profile MainMenu/Lobby actually see.
3. For each effect in the preset table, find it in the Inspector,
   check its **own `active`** box if not already on (most already are
   — see the grounding notes above), then for each listed field, tick
   its own override checkbox and type in the value.
4. Leave every field **not** listed in the preset table alone — those
   stay at their current neutral values.

🔴 **Rest point**: open `MainMenu.unity` and enter Play Mode. You
should see the effect immediately on the menu's drone flythrough
background (the additively-loaded `Lobby` scene) — that scene has no
Volume of its own, so it's reading this file directly. If nothing
changed, double check both the component's `active` box *and* each
field's own override checkbox are ticked (the two-checkbox trap from
the primer above).

## Stage 2 — make SampleScene fall through to the same default

Simplified per a direct request: one shared profile everywhere instead
of Stage 2/3's original per-scene split (duplicate profiles for
MainMenu/Lobby, rounding out SampleScene's own separate file). That
original plan is still a reasonable thing to revisit later if a scene
ever needs its own distinct look (e.g. Lobby's interior mirror room
from #52 wanting different values than an outdoor scene) — nothing
below forecloses it, it just isn't needed right now.

MainMenu and Lobby already fall through to `DefaultVolumeProfile.asset`
automatically (Stage 1) since neither has a Volume of its own.
`SampleScene.unity` is the one holdout — it has its own `Global
Volume` GameObject pointing at `SampleSceneProfile.asset`, which
overrides the project default and stops it from seeing Stage 1's
values at all.

1. Open `SampleScene.unity` and find the `Global Volume` GameObject in
   the Hierarchy (search "Global Volume" in the scene search bar if
   it's not obvious).
2. On its **Volume** component, clear the **Profile** field (or just
   delete the `Global Volume` GameObject entirely — either works, since
   an empty/missing profile and no Volume component both fall through
   to the project default the same way; deleting it is the cleaner,
   more consistent choice since it then matches MainMenu/Lobby exactly).
3. `Assets/Settings/SampleSceneProfile.asset` becomes unused after
   this — leaving it in place rather than deleting it, in case a scene
   wants its own distinct profile again later (same non-destructive
   instinct as #59's approach to layout experiments).

🔴 **Rest point**: Play Mode `SampleScene.unity`, walk through a full
house-robbery loop — it should now look identical to MainMenu/Lobby's
tuning from Stage 1, all three scenes reading Preset D from the same
file. Check it still reads clearly at night if a night-round is active
(`NightModeVisuals`) — a look tuned for daytime can hurt visibility
once it's dark; if so, that's a real reason to give `SampleScene` its
own profile back later (see the note above), not something to fix by
weakening the daytime look.

## Stage 3 — optional extra touches, once the above feels right

Only worth doing after Stages 1-2 are confirmed and you have a sense
of whether the look needs more:

- **Film Grain** — a very subtle amount (`Intensity` around `0.1-0.15`,
  thin/fine type) can help sell the "stylized, not sterile" look
  without being distracting. Easy to overdo — keep it low.
- **Lens Distortion** — normally skip (see reasoning above), but a
  tiny negative amount (barely pincushion) can add a bit of
  "handheld camera" character if the first-person view ever feels too
  flat. Very easy to overdo; if in doubt, leave at `0`.
- Don't touch AO/SSAO settings on `PC_Renderer.asset` unless something
  specific looks wrong after the above — it's already active and
  reasonably tuned (Intensity `0.4`).

## Where to look

- `Assets/Settings/DefaultVolumeProfile.asset` — the project-wide
  fallback, Stage 1, now the single shared profile for all three
  scenes as of Stage 2.
- `Assets/Settings/SampleSceneProfile.asset` — SampleScene's old
  per-scene profile, now unused as of Stage 2 (left in place, not
  deleted, in case a scene wants its own look again later).
- `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/Lobby.unity`,
  `Assets/Scenes/SampleScene.unity` — MainMenu/Lobby have no Volume of
  their own (by design); SampleScene's own `Global Volume` GameObject
  is what Stage 2 clears/removes.
- `Assets/Settings/PC_Renderer.asset` — SSAO's existing settings,
  already fine, reference only.
- `Assets/Scripts/UI/MenuBackgroundBuilder.cs` — the #51 code that
  additively loads `Lobby.unity` behind the Main Menu; both scenes
  read the same shared default now, so no divergence to worry about
  there.
- [polish-deep-dive.md](polish-deep-dive.md) — the research this issue
  came from.
- Issue [#64](https://github.com/joshuaclemons1/Rob-Everyone/issues/64).

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

## Three starting points to compare

Not final numbers — starting points to punch in, look at in Play Mode,
and nudge from. All three keep Motion Blur off, Depth of Field off,
Chromatic Aberration off, per the reasoning above.

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

## Stage 1 — give the project-wide default real values

This fixes `MainMenu.unity` and `Lobby.unity` in one place, since
neither has its own Volume yet and both currently fall straight
through to this file.

1. Open `Assets/Settings/DefaultVolumeProfile.asset` (select it in the
   Project window — it opens in the Inspector like any other asset,
   no scene needed).
2. Pick one preset above (recommend starting with **B** or **C**,
   since this is the profile MainMenu/Lobby actually see).
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

## Stage 2 — give MainMenu and Lobby their own profiles

Stage 1 already makes both scenes look better via the shared default,
but a shared file means MainMenu and Lobby can never be tuned
independently later (e.g. Lobby's interior mirror room from #52 may
want different values than the outdoor menu flythrough). Give each its
own profile now, seeded from Stage 1's values, so they're free to
diverge later.

1. Duplicate `Assets/Settings/SampleSceneProfile.asset` (Ctrl+D in the
   Project window) twice; rename the copies `MainMenuProfile.asset`
   and `LobbyProfile.asset`.
2. Open each and set its values to match whichever preset you chose in
   Stage 1 (or intentionally diverge — that's the point of giving them
   their own file).
3. In `MainMenu.unity`: add an empty GameObject, name it `Global
   Volume` (matching the existing naming convention in `SampleScene`),
   add a **Volume** component to it, check **Is Global**, and drag
   `MainMenuProfile.asset` into its **Profile** field.
4. Repeat in `Lobby.unity` with `LobbyProfile.asset`.

🔴 **Rest point**: Play Mode both scenes again. MainMenu's own flythrough
background is the `Lobby` scene loaded additively by
`MenuBackgroundBuilder` — worth double-checking whether that additive
load should pick up `LobbyProfile.asset` (the scene's own Volume) or
whether you'd rather it use `MainMenuProfile.asset` for a fully
separate look; either is fine, just be aware which one you're actually
seeing. Also do a real multiplayer smoke test of `Lobby.unity` itself
(not just the menu background), since it's a real gameplay scene with
its own player-facing moments (#52's customization room).

## Stage 3 — round out SampleScene's existing profile

`SampleSceneProfile.asset` already has Bloom/Tonemapping/Vignette —
the real gap in it is **no Color Adjustments at all**, and its Bloom/
Vignette values are on the conservative side.

1. Open `Assets/Settings/SampleSceneProfile.asset`.
2. Right-click in empty space in the Inspector → **Add Override** →
   find **Color Adjustments** under the Post-processing category, add
   it.
3. Fill in the Color Adjustments row from whichever preset you're
   standardizing on.
4. Bump Bloom's `Intensity` and Vignette's `Intensity` from their
   current mild values (`0.25`/`0.2`) toward the preset's numbers.

🔴 **Rest point**: Play Mode `SampleScene.unity`, walk through a full
house-robbery loop. Check it still reads clearly at night if a
night-round is active (`NightModeVisuals`) — a vignette/bloom tuned
for daytime can look wrong or hurt visibility once it's dark; if so,
it's fine for the numbers to differ, this file only needs to cover the
default daytime case for now.

## Stage 4 — optional extra touches, once the above feels right

Only worth doing after Stages 1-3 are confirmed and you have a sense
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
  fallback, Stage 1.
- `Assets/Settings/SampleSceneProfile.asset` — SampleScene's existing
  real profile, Stage 3.
- `MainMenuProfile.asset` / `LobbyProfile.asset` (new, Stage 2).
- `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/Lobby.unity`,
  `Assets/Scenes/SampleScene.unity` — where each scene's `Global
  Volume` GameObject lives (or needs adding).
- `Assets/Settings/PC_Renderer.asset` — SSAO's existing settings,
  already fine, reference only.
- `Assets/Scripts/UI/MenuBackgroundBuilder.cs` — the #51 code that
  additively loads `Lobby.unity` behind the Main Menu; worth knowing
  about for the Stage 2 rest point's "which profile is the flythrough
  actually seeing" question.
- [polish-deep-dive.md](polish-deep-dive.md) — the research this issue
  came from.
- Issue [#64](https://github.com/joshuaclemons1/Rob-Everyone/issues/64).

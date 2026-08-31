# Art Style

Living doc for the visual direction. Feeds
the "Mood board / visual style" and "Color palette" tasks in
[artist-todo.md](artist-todo.md).

## Inspiration games

- **PEAK**
- **Gamble with your friends**
- **Lethal Company**

## Direction

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

Planned future feature: a **time-of-day system** that occasionally swaps a
round to dusk or night, raising difficulty (lower visibility, presumably
tighter margin for spotting homeowner/police alert states). Not being built
yet — Stage 3 (current) stays daytime-only — but the palette should keep
these variants in mind so the swap doesn't require redesigning colors later.

Draft variant tints (unlocked, needs revisiting once the feature is actually
built and playtested for readability):

| Use | Daytime | Dusk (draft) | Night (draft) |
|---|---|---|---|
| Sky | `#6EC6E8` | `#E8955C` (warm orange) | `#1B2430` (deep navy) |
| Ambient light | full brightness | warm low-angle, longer shadows | streetlamp/window glow as main light source |
| Good House accent | `#FFD166` | `#FFD166` (glows brighter against dim bg) | `#FFD166` (same — should read as a beacon at night) |
| Police / danger accent | `#EF476F` / `#118AB2` | unchanged | unchanged, but higher contrast against dark background |

Reasoning: keep house/UI accent colors constant across all three states so
players don't have to relearn what "Good House" or "danger" looks like —
only the ambient/sky lighting shifts. Difficulty should come from *visibility*
(darker ambient, smaller lit radius), not from changing what colors mean.

## Open / TODO

- Playtest the dusk/night draft tints once the time-of-day system is
  actually built — confirm accent colors still read clearly against a dark
  background.
- Any concept art or reference screenshots from the inspo games worth
  pinning here.

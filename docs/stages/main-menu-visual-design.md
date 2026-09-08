# Main menu — visual design

Content spec for the Photoshop pass on the main menu. Covers layout with
exact pixel regions, the sliding-panel navigation flow, animation specs,
and a precise per-asset checklist — what file, what size, what it's for,
whether to adapt the pixel UI kit or build new. This replaces the plain-
placeholder-button approach in
[main-menu-customization-setup.md](main-menu-customization-setup.md)'s
Parts 5–8 once this art exists.

Style: matches [art-info.md](../art-info.md)'s locked "Bright Cartoon
Suburbia" palette. **Design everything on a 3840×2160 canvas** — matches
the Canvas Scaler setup used elsewhere, so pixel positions here translate
directly to Unity Rect Transform values later.

---

## Navigation flow

```
┌─────────────┐   Play    ┌─────────────┐  Customization  ┌─────────────┐
│  Main Menu  │ ────────> │    Lobby    │ ──────────────> │Customization│
│             │ <──────── │             │ <────────────── │             │
└──────┬──────┘   Back    └─────────────┘       Back       └─────────────┘
       │ Settings
       v
┌─────────────┐
│  Settings   │  (full screen, separate branch off Main — see below)
└─────────────┘
```

- **Main → Lobby → Customization**: forward click slides the current
  panel left (stays partly visible, dims ~50% black, non-interactive)
  while the next panel slides in from the right to center. Back reverses
  it. The character preview (see below) **persists across all three** —
  it doesn't reset or re-spawn between them.
- **Settings** is a separate branch straight off Main Menu, not part of
  that chain. It goes **full screen** (needs more room than the others),
  and the character preview **falls out of frame** rather than staying
  visible — see Character preview behavior below.

## Composition regions (3840×2160 canvas)

Applies to Main Menu, Lobby, and Customization — they share this layout,
just with different button labels/content on the left:

```
0                                                                    3840
┌────────────────────────────────────────────────────────────────────┐ 0
│                         TITLE (Main only)                          │
│                      x: 620–3220, y: 150–550                       │
│                                                                      │
│  BUTTON STACK              │        CHARACTER PREVIEW               │
│  x: 200–1700               │        x: 2200–3700, y: 500–2000       │
│  vertically centered       │        (frame + live 3D render sit     │
│  around y: 1080            │         here, character persists       │
│                             │         across Main/Lobby/Custom.)     │
│                                                                      │
└────────────────────────────────────────────────────────────────────┘ 2160
```

- Button stack: left ~44% of the screen, vertically centered on the
  screen's midline (y: 1080).
- Character preview safe zone: right ~40% of the screen, roughly
  centered vertically but biased slightly down (leaves headroom for the
  title on the Main screen specifically).
- Settings screen ignores this split entirely — it's full-canvas content,
  no reserved character region.

## Character preview behavior

- **Idles in place** on Main Menu, Lobby, and Customization — same spot,
  same frame, doesn't move between these three screens.
- **Entering Settings**: character **falls through the frame** — drops
  straight down and out of view (through the floor of its own preview
  frame) as the Settings panel takes over the full screen. Pure Transform
  animation (position Y drops fast, character exits below the frame's
  bottom edge), not a special art state.
- **Leaving Settings** (Back): character **falls from the sky** back into
  its normal position — drops in from above the frame, lands, resumes
  idling. Consider a small bounce/settle on landing for a bit of cartoon
  personality, matching the game's style.
- **Art implication**: the preview frame asset needs a **visually solid
  floor/base element near its bottom edge** — the character needs
  something to visually disappear *behind* as it falls, or the "falls
  through" effect won't read. This is why `preview_frame.png` below is
  flagged as likely-custom rather than a straight kit-adapt — the pixel
  kit's plain outline frames don't have a floor/base built in.

---

## Animation specs (Unity-side, not art — noted so the art is built
## knowing what will move)

| Element | Effect | Rough values |
|---|---|---|
| Title | Continuous idle pulse | Scale 100% → 103% → 100%, ~2–3s per cycle, ease in/out |
| Panel transition (Main/Lobby/Customization) | Slide + dim | ~0.4–0.6s, ease out. Outgoing panel dims ~50% black while sliding partway left; incoming slides from off-screen-right to center |
| Character preview → Settings | Fall through frame | Fast drop (~0.3–0.4s), character's Y position moves down past the frame's floor line, ease in (accelerating, like a real drop) |
| Character preview ← Settings (Back) | Fall from sky | Drop in from above frame (~0.4–0.5s), ease out with a small bounce on landing |

None of these need special art frames (no "mid-pulse," "mid-slide," or
"mid-fall" art) — they're Transform/color animations applied to the
static assets below.

---

## Asset checklist (exact specs)

**First, check the kit**: open `Assets/Art/UI/Pixel UI pack 3/All.png`
(or the sliced individual sheets) and look for pieces close to each size/
shape below before building from scratch — recolor/adapt what's close
enough rather than starting over. Rows marked **Adapt** below are good
candidates based on the contact sheet (pill-shaped bars, colored square
frames); rows marked **New** are things the kit likely doesn't cover.

| # | Asset (filename) | Source | Canvas size | Format | Notes |
|---|---|---|---|---|---|
| 1 | `bg_neighborhood.png` | New | 3840×2160 | PNG, no alpha needed (full bleed, opaque) | Drone/aerial shot of the neighborhood, locked palette colors, **should visually tie into the actual in-game map layout** (house ring + fenced compound from the sketch, see [plan.md](../plan.md)'s Map section and Stage 3g/3h) rather than being a generic unrelated neighborhood. Full-bleed, no transparent areas. Shared behind all screens including Settings. |
| 2 | `title_wordmark.png` | New | 2400×700 (author large for crispness — final on-screen size is smaller, ~2600×400 placed at Title region above) | PNG, alpha | "ROB EVERYONE" logo art. Transparent background — sits over `bg_neighborhood`. Static art only, the pulse is applied in Unity. |
| 3 | `button_base.png` | **Adapt** — check the pill-shaped bars, top-left of the kit sheet | ~900×180 | PNG, alpha, 9-slice friendly (flat tileable center, fixed-width rounded ends/corners) | Shared background for Play, Settings, Quit, Invite Players, Game Settings, Customization, Back — one asset, Unity's Button component recolors via Color Tint on hover/press, no separate states needed. |
| 4 | `button_startgame.png` | New (or heavily recolor an adapted piece) | ~1000×220 (slightly larger than `button_base`) | PNG, alpha | Gold accent (`#FFD166`) baked into the art, not runtime-tinted — this is the one visually emphasized button, worth a dedicated asset rather than a tint trick. |
| 5 | `preview_frame.png` | **New** (kit's plain outline frames don't have a floor built in — see "Character preview behavior" above) | ~1500×1700 | PNG, alpha (transparent center where the live 3D character shows through) | Must include a visually solid floor/base element near the bottom so the character can "fall through" it convincingly. Everywhere else transparent. |
| 6 | `panel_backdrop.png` (optional) | **Adapt** if the kit has a suitable panel/parchment piece | ~1900×1900 | PNG, alpha, 9-slice friendly | Subtle semi-transparent strip behind the button stack, purely for legibility against the busy background photo. Skip if the buttons already read fine directly over the background. |

**Settings screen**: no new background needed — reuse `bg_neighborhood.png`
with a plain semi-transparent dark full-screen `Image` overlay (built in
Unity, not Photoshop) for legibility, since it needs full-canvas space
for actual settings content (sliders, dropdowns — not designed yet, out
of scope for this pass).

**Don't build:**

- Hover/pressed button states — Unity's Button **Transition: Color Tint**
  does a brightness shift with zero extra art.
- The dim overlay on inactive panels, or the Settings backdrop overlay —
  both are plain semi-transparent black `Image`s built in Unity.
- Any "mid-animation" art for the pulse/slide/fall effects — all pure
  Transform/color animation on the static assets above.

---

## Sequencing note

Since `bg_neighborhood.png` should tie into the actual map layout, it's
worth roughing it out only loosely until Stage 3g/3h's house/compound
layout is actually finalized — a full repaint once the real layout
exists will look better than guessing now and redoing it later. Fine to
block out placement/composition ideas in the meantime.

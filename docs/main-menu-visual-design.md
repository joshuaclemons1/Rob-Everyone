# Main menu — visual design

Content spec for the Photoshop pass on the main menu, based on a design
session (2026-09-01). Covers layout, the sliding-panel navigation flow,
animation specs, and the concrete asset checklist. This replaces the
plain-placeholder-button approach in
[main-menu-customization-setup.md](main-menu-customization-setup.md)'s
Parts 5–8 once this art exists — that doc's structure (panels, buttons,
`MenuActions`/`CustomizationUI` scripts) still applies, just re-skinned
and with real slide/dim animation added instead of simple show/hide.

Style: matches [art-info.md](art-info.md)'s locked "Bright Cartoon
Suburbia" palette. Canvas reference size: **3840×2160** (matches the
Canvas Scaler setup already used elsewhere), keep key content readable
if cropped to other aspect ratios.

---

## Navigation flow

Three screens, same sliding pattern repeating at each step:

```
┌─────────────┐   Play    ┌─────────────┐  Customization  ┌─────────────┐
│  Main Menu  │ ────────> │    Lobby    │ ──────────────> │Customization│
│             │ <──────── │             │ <────────────── │             │
└─────────────┘   Back    └─────────────┘       Back       └─────────────┘
```

- Clicking forward: the current panel **slides left** (doesn't fully
  leave — stops partway, staying partly visible) and **dims** (~50%
  black overlay, non-interactive while dimmed). The next panel **slides
  in from the right** to center.
- Clicking Back: reverses it — the dimmed panel undims and slides back to
  center, the panel in front slides out to the right and is removed/
  hidden.
- Only ever one panel is fully active/interactive (centered, undimmed) at
  a time.

### Main Menu

- **Title**, top-center: "ROB EVERYONE" wordmark. Continuous subtle idle
  pulse once on screen (Unity-side animation, not baked into the art —
  see Animation specs below).
- **Buttons**, vertically stacked, positioned in the **left half** of the
  screen (leaving the right half clear for the character preview): Play,
  Settings, Quit.
- **Character preview**, right side: a live 3D render (not art — Unity
  renders your currently-equipped skin/color here, idling). Photoshop's
  job is a **backdrop/frame** for this area, not the character itself —
  see Asset checklist.
- **Background**: the shared drone/aerial neighborhood shot (see below) —
  visible in full here since nothing is dimming it yet.

### Lobby (after Play)

- Buttons: **Invite Players**, **Game Settings**, **Customization** —
  same visual weight as each other — then **Start Game**, spaced apart
  with extra gap, and visually emphasized (bigger, gold accent color
  `#FFD166` from the locked palette) as the clear primary action.
- Main Menu panel sits dimmed, partly visible on the left.
- Open question, your call: does the character preview stay visible here
  too (persistent "this is you" reminder), or was that Main-Menu-only?
  Doesn't block starting art — the preview's backdrop frame (if any)
  works the same either way, just decide before finalizing Lobby's exact
  button-column width.

### Customization (after Customization button)

- This is the screen `CustomizationUI.cs` already drives: skin
  Previous/Next, color swatches, live preview. Re-skin its existing
  layout (see main-menu-customization-setup.md Part 6) with real art
  instead of placeholder buttons — the functional layout doesn't need to
  change, just needs real button/panel art dropped in.
- Lobby panel sits dimmed, partly visible on the left (Main Menu is now
  fully off-screen, two levels back).

---

## Animation specs (Unity-side, not art — noted here so the art is built
## knowing what will move)

| Element | Effect | Rough values |
|---|---|---|
| Title | Continuous idle pulse | Scale 100% → 103% → 100%, ~2–3s per cycle, ease in/out |
| Panel transition | Slide + dim | ~0.4–0.6s, ease out. Outgoing panel dims to ~50% black overlay while sliding partway left; incoming panel slides from off-screen-right to center |
| Character preview | Idle animation | Whichever skin is shown just idles in place (Stage 3d's Animator pattern) — no Photoshop art needed, this is a 3D animation |

None of these need special art states (no "title mid-zoom" frame, no
"panel mid-slide" frame) — they're plain Transform/color animations
applied to whatever static art you make, built once the art exists.

---

## Asset checklist

Reuse pieces from `Assets/Art/UI/Pixel UI pack 3/` wherever they fit —
recolor/adapt rather than starting from scratch. From the kit's contact
sheet (`All.png`), likely candidates already in the pack:

- The rounded pill-shaped bars (top-left of the sheet, black/brown/blue
  variants) — good **button background** candidates for Play/Settings/
  Quit/Invite Players/Game Settings/Customization/Back.
- The colored square frames (green/orange/cyan/purple outlined squares) —
  candidates for the **character preview frame/backdrop**.

Check what's actually usable once you're looking at the sliced sprites in
Unity (Part 6's Sprite Editor slicing from
main-menu-customization-setup.md) — the sheet has more pieces than just
these two candidates, browse before deciding what's missing.

**New art likely needed** (create in Photoshop, kit probably doesn't
cover these):

| Asset | Notes | Suggested size |
|---|---|---|
| Shared background | Drone/aerial shot of the neighborhood, matching locked palette. Visible behind all 3 screens. | 3840×2160 (design at full canvas size) |
| Title wordmark | "ROB EVERYONE", static PNG w/ alpha — the pulse animation is applied to this in Unity, don't bake motion into the art | Design large, e.g. 2000×600 canvas, export @ needed display size |
| Start Game button (emphasized variant) | Same button family as the others but larger + gold accent (`#FFD166`) — the clear "primary action" | Match other button assets' base size, scaled up |
| Character preview backdrop/frame (optional) | A framing element behind/around where the live 3D model renders — not the character itself | Sized to whatever preview area you land on |

**Probably don't need new art for:**

- Button hover/pressed states — Unity's Button component can do a
  brightness shift automatically (**Transition: Color Tint** on the
  Button component) with zero extra art. Only make separate
  hover/pressed sprites if you specifically want a different look than a
  simple brightness change.
- The dim overlay on inactive panels — a plain semi-transparent black
  `Image` in Unity, no art asset needed.
- The slide transition itself — pure animation, not an asset.

---

## Open items to decide before finalizing (non-blocking)

- Does the character preview persist across Lobby/Customization screens,
  or Main-Menu-only?
- Exact background composition — is the "drone shot" a static painted
  scene, or intended to later tie visually to the actual in-game map
  layout (Stage 3g/3h)? Not required to match now, just worth having an
  opinion before spending a long time on it.

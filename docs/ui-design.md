# UI / Menu Design

Full content spec for every UI screen/element the game needs, organized by
what's safe to build now vs. later. Written for a Photoshop session away
from Unity — deliverables are individual PNG sprites/icons (Unity assembles
them into Canvas UI), not full-screen flat comps, except where noted as a
"layout mockup" (reference only, not imported directly).

Style: follow [art-info.md](art-info.md)'s locked palette and "clean,
low-poly cartoon, not overly stylized" direction — UI should feel like it
belongs to the same game as the 3D art, not a generic default HUD.

## General export rules

Per art-info.md's Format & export cheatsheet: **PNG with alpha, @2x actual
display size.** A few UI-specific additions:

- Build small, reusable pieces — icons, panel backgrounds, single buttons —
  not full baked screens. Unity's Canvas system assembles them; a flat
  exported "shop screen.png" can't have working buttons/dynamic text.
- **9-slice friendly panels**: if designing a background panel (shop
  window, HUD panel backing), keep corners/edges a fixed width with a
  flat, tileable center — lets Unity's Image component scale it to any
  size without stretching the corners. Ask if this is unfamiliar, it's a
  quick concept to explain once you're back at a screen where I can show
  reference images.
- **Icon canvas size**: design item/status icons on a 256×256 canvas
  (exports crisp at typical in-game display sizes of 48–128px). Keep the
  actual artwork centered with ~10% padding so icons don't touch their own
  edges.
- **Reference resolution**: mock up full-screen layouts (see "layout
  mockup" callouts below) at 1920×1080 — doesn't need to be the final
  render resolution, just a consistent canvas so proportions translate.
- Name files descriptively and consistently, e.g. `icon_taser.png`,
  `panel_shop_background.png`, `hud_walletslot_locked.png` — this becomes
  the actual filenames imported into `Assets/Art/UI/` (per art-info.md's
  folder convention), so future-you and Zach can find things.

---

## Tier 1 — build now (Stage 3, in-round HUD)

These support systems that already exist in code (`InventoryUI.cs`,
`RoundUI.cs`, `Interactor.cs`) or are simple additions to them — highest
value to build first since they're usable almost immediately.

- **Crosshair / interact prompt** — nothing exists for this yet at all
  (`Interactor.cs` does the raycast but has no visual feedback). Needs:
  - A small neutral crosshair/reticle, always visible (or hidden until
    aiming at something interactable — either works, simpler is fine for
    now).
  - An "interact available" state — either the crosshair itself changes
    (highlight/color shift) or a small icon + "E" prompt appears near it
    when `Interactor.CurrentTarget` is non-null. Design both the neutral
    and "interactable" states as separate icon assets.
- **Money/Cash readout icon treatment** — `InventoryUI.cs`'s `$0` text
  exists but is plain TMP text. Design a small dollar-sign/coin icon to
  sit next to it, and optionally a background panel chip so it reads as a
  distinct HUD element rather than floating text.
- **Quota readout treatment** — same idea for `RoundUI.cs`'s quota text.
  Consider a small progress bar (current Cash vs. quota) instead of/in
  addition to the raw number — more readable at a glance during play.
  Needs a bar background + fill sprite (9-slice friendly).
- **Timer treatment** — icon/panel chip for `RoundUI.cs`'s countdown text,
  matching the money/quota style so the three feel like one HUD group.
- **Result banner** — `RoundUI.cs`'s `resultText` ("Quota met!" / "Caught
  by the police!" / "Quota not met.") is plain text today. Design a banner
  background + simple icon per outcome (checkmark, handcuffs/siren,
  X-mark) so the moment reads instantly, not just as text.

## Tier 2 — design now, needed for the economy rebuild (Stage 7)

Not blocked on anything — the design is locked in
[gameplay-design.md](gameplay-design.md) — but won't be wired into code
until the inventory/round-manager rework happens. Safe to build ahead.

- **Carry slots readout** — 6 icon slots total: 5 normal + 1 Prison
  Wallet, visually distinct from each other. Needs:
  - Empty slot frame (×2 styles: normal, Wallet — Wallet should read as
    "special," e.g. a lock icon or different border color/material,
    matching the gold "Good House" accent from the palette).
  - Filled slot state (item icon sits inside the frame — this is really
    "frame + whatever item icon," so just design the frame states, item
    icons are their own asset category below).
  - Wallet's "locked" state once an item's been placed in it this round
    (distinct from empty — e.g. a small padlock overlay).
- **Loot item icons** — one icon per loot type (watch, cash, jewelry,
  etc. — exact list still TBD, start with placeholders: 4–5 generic types
  is enough to unblock). 256×256 canvas per the general rules above.
- **Sabotage item icons** — taser, bat, alarm clock, hammer (exact final
  list TBD per gameplay-design.md, but these four are safe to start).
  Same 256×256 treatment.
- **Active sabotage cooldown overlay** — a radial or linear "recharging"
  fill overlay that sits on top of a sabotage item's icon (generic,
  reusable across all sabotage icons rather than baked per-item) — plus a
  small "uses left" number badge style for durability-based items (Bat)
  and a "used" grayed-out state for single-use items (Alarm Clock).
- **Rival status ping icon** — a world-space or screen-edge indicator
  (exact trigger still TBD per gameplay-design.md, but the icon itself —
  e.g. an alert/exclamation mark, or a small silhouette — can be designed
  now regardless of the final trigger logic).
- **Shop item card** — the repeating unit for the shop screen: icon slot +
  name + price + (later) a "locked, unlocks at next tier" grayed variant
  for items not yet reached. Design as one flexible card background,
  reused for every item.
- **Shop ready-spot marker** — this is actually a **world-space floor
  decal/prop texture**, not a UI sprite — the design (Stage 7) is players
  physically walking onto a marked spot to ready up, not a menu button.
  Worth designing now anyway: a clear, glanceable floor marker (arrow
  ring, glowing tile, footprint icon) in the gold accent color so it
  reads as "stand here." Export as a texture for a simple plane/decal,
  not a UI PNG.
- **Cash/quota shop-phase readout** — reuses the Tier 1 money/quota icon
  treatment, just needs a shop-context layout variant if the numbers need
  to sit differently on a full shop screen vs. the in-round HUD corner.
- **Batch-end summary elements** — quota met/failed icon per player (reuse
  the Tier 1 result-banner icons if they fit), a "new quota" callout
  treatment, and a "surplus wiped" notice icon (e.g. coins draining away).
- **Jail/bail prompt** — a bond/bounty price callout (reuses the
  money-icon style) shown when near a jailed player, plus a distinct
  "jailed" status icon (handcuffs/bars) for whatever indicates a jailed
  player's location to teammates.

## Tier 3 — menu shell (stage-independent, but lower urgency)

Not blocked by anything, but not needed until Stage 4+ (multiplayer) or
Stage 7 (meta-progression) — fine to pick up between the above if you want
variety, but Tier 1/2 unblocks more actual gameplay testing sooner.

- **Title screen** — logo treatment already on art-info.md's to-do list;
  pairs with a simple background composition (a stylized shot of the
  suburb, matching the locked palette).
- **Main menu** — Play / Settings / Quit, simple vertical button list is
  fine for a 2-person indie project, no need to over-design.
- **Pause menu (in-round)** — Resume / Settings / Quit-to-menu.
- **Settings menu** — sensitivity, volume (master/music/SFX at minimum,
  matching the confirmed SFX/music categories in art-info.md), keybind
  display (not necessarily rebinding UI yet).
- **Lobby screen (Stage 4–5)** — player list (up to 8, per
  gameplay-design.md's player-count target), ready status per player,
  Steam invite button. Also needs a small "voice active" icon per player
  row once proximity voice chat is built.
- **Cosmetic/skin select screen (Stage 5–6, meta-progression)** — grid of
  unlocked vs. locked player skins.
- **Lifetime stats screen (meta-progression)** — simple stat list (total
  Cash earned, times caught, best batch reached, etc.) — a single
  readable panel, not a complex dashboard.

---

## Suggested order for this session

Given you're doing this standalone in Photoshop without Unity to check
against live, the safest order (least likely to need rework once you see
it in-engine) is roughly Tier 1 top-to-bottom, then Tier 2's icon sets
(loot items, sabotage items, carry slots) since icons are the most
"finish once, reuse everywhere" payoff — the full-screen layout pieces
(shop card composition, lobby screen) are easiest to get right once
there's an actual scene to screenshot and mock up against, which the
existing "HUD style pass" to-do item in art-info.md already calls out as
the intended workflow.

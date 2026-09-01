# UI implementation setup — crosshair + pixel UI kit

Editor steps to get the first two UI assets actually working: the
crosshair (`Assets/Art/UI/`) and the downloaded pixel UI kit
(`Assets/Art/Design/`). Do this in `SampleScene`, same Canvas/UIManager
already used by `InventoryUI`/`RoundUI`.

## 1. Crosshair

`Crosshair - Dot.png` is 20×20px. `Crosshair - Interact.png` is 118×74px
— it bakes the dot **and** a hand icon into one wider image. Don't swap
between these two on a single Image component: a fixed-size Image forced
to display two very differently-sized/shaped sprites is exactly what
squishes the art and shifts everything around. Instead, use **two
separate Images**: the dot (always on, never changes) and a hand-only
icon (toggled on/off next to it).

### 1a. Split the hand out as its own asset

In Photoshop, using `Assets/Art/Design/PSD/Rob-Everyone UI.psd`: hide the
dot layer, export **just the hand** (Image → Trim, or export the
selection only) as a new file, e.g. `Assets/Art/UI/Crosshair - Hand.png`.
Trimmed to the hand's own bounding box, not the full 118×74 canvas —
otherwise you'll hit the exact same size-mismatch problem one level down.

### 1b. Fix the scale (the "way too big" issue)

This is a **Canvas Scaler** problem, not an asset problem. By default a
Canvas uses **Constant Pixel Size** — 20px is always 20 *actual screen
pixels*, so art sized for a 4K canvas (3840×2160, where 20px is tiny
relative to the screen) reads as oversized on any lower-resolution
display, since it's still 20 real pixels but now a bigger fraction of a
smaller screen.

1. Select the **Canvas** in the Hierarchy → find its **Canvas Scaler**
   component.
2. Set **UI Scale Mode** → **Scale With Screen Size**.
3. Set **Reference Resolution** → `3840 x 2160` (matches the 4K canvas
   this was designed against).
4. **Screen Match Mode** → Match Width Or Height, **Match** slider → `0.5`
   as a starting point (favors neither width nor height scaling
   specifically) — adjust toward `1` (Height) if the crosshair still
   feels off on your actual monitor's aspect ratio.

This affects every UI element on this Canvas (money/quota/timer too), not
just the crosshair — worth confirming those still look right afterward.

### 1c. Set up the two crosshair elements

1. Select `Crosshair - Dot.png` and your new `Crosshair - Hand.png` in the
   Project window. In the Inspector: **Texture Type** → Sprite (2D and
   UI), **Filter Mode** → Point (no filter) for crisp small art, click
   **Apply**.
2. In the Hierarchy, inside the **Canvas**, right-click → **UI → Image**.
   Rename it `Crosshair`.
   - **Rect Transform**: Anchor Preset → center (Alt+Shift+click the
     center preset), **Pos X/Y** → `0, 0`.
   - **Source Image** → `Crosshair - Dot.png`.
   - Click **Set Native Size** (button in the Image component) instead of
     typing Width/Height by hand — guarantees it matches the sprite's
     real 20×20 pixels exactly.
   - This object's sprite/size never changes at runtime — it's done.
3. Right-click the **Canvas** again → **UI → Image**. Rename it
   `InteractHint`.
   - **Source Image** → `Crosshair - Hand.png`, then **Set Native Size**.
   - Position it **next to** the dot, not on top of it — e.g. Anchor
     Preset center, then set **Pos X** to roughly half the dot's width
     plus half the hand's width plus a small gap (something like
     `10 + (hand width / 2) + 4`— eyeball it in the Scene view against the
     centered dot, exact offset is a feel call).
   - Leave it **active in the Hierarchy** for now (you'll see both at
     once while setting position) — the script disables it at runtime.
4. Add component **Crosshair UI** (`RobEveryone.UI`) — to either object,
   or to `UIManager`.
5. Wire its two fields:
   - `Interactor` → drag the `Player` object (for its `Interactor`
     component)
   - `Interact Hint` → drag the `InteractHint` object itself
6. Press Play. The dot should stay fixed at the correct 4K-relative size;
   `InteractHint` should only appear next to it while looking at
   `Item_Watch`/any pickup within `Interact Range`, without the dot ever
   moving or resizing.

## 2. Pixel UI kit

The kit landed as **numbered sprite sheets**
(`Assets/Art/Design/PSD/Assets/Pixel UI pack 3/00.png` through `07.png`,
plus `All.png` as a full contact sheet) — each one packs multiple UI
elements (buttons, panels, icons) into a single image, not individual
files. These need **slicing** in Unity before you can drag out one
button/panel at a time.

1. Select one of the numbered PNGs (start with `00.png`) in the Project
   window. In the Inspector:
   - **Texture Type**: Sprite (2D and UI)
   - **Sprite Mode**: **Multiple** (this is the key setting — Single
     treats the whole sheet as one image)
   - **Filter Mode**: Point (no filter) — pixel art kits almost always
     want this, otherwise edges blur.
   - **Compression**: None, if it looks soft/artifacted at 100% zoom with
     compression on.
   - Click **Apply**.
2. Click **Sprite Editor** (button in the Inspector, opens a separate
   window — installs the **2D Sprite Editor** package automatically the
   first time if it isn't already present).
3. In the Sprite Editor toolbar, **Slice → Automatic**, then click
   **Slice**. This detects each padded-apart element by its alpha
   boundaries — works well for most UI kits where elements have gaps
   between them. Check the result: if elements got merged together that
   should be separate (no visible gap in-source between them), you'll
   need to slice by hand instead (drag out each rectangle manually in the
   editor) or check if the kit's own product page lists an exact grid
   cell size to use **Slice → Grid By Cell Size** instead.
4. Click **Apply** (top-right of the Sprite Editor window), close it.
5. Repeat for `01.png` through `07.png` — same three Inspector settings,
   same Slice → Automatic → Apply.
6. Once sliced, expand any of these PNGs in the Project window (small
   arrow) — each individual sliced piece now shows as its own sub-sprite
   you can drag directly into an Image's **Source Image** field, same as
   any other sprite.

## Notes

- `Assets/Art/Design/PSD/Rob-Everyone UI.psd` is your working source file
  — keep working from that for new mockups, the numbered PNGs are just
  the kit's pre-made element sheets.
- Once you've identified which specific sliced elements you're actually
  using (a button, a panel, etc.), it's worth renaming those sub-sprites
  in the Sprite Editor (double-click a slice's name field) to something
  readable (`button_primary`, `panel_shop`) instead of the kit's default
  numbered names — much easier for Zach (or future you) to find later.

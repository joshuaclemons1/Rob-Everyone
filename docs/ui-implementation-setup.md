# UI implementation setup — crosshair + pixel UI kit

Editor steps to get the first two UI assets actually working: the
crosshair (`Assets/Art/UI/`) and the downloaded pixel UI kit
(`Assets/Art/Design/`). Do this in `SampleScene`, same Canvas/UIManager
already used by `InventoryUI`/`RoundUI`.

## 1. Crosshair

Script already added: `Assets/Scripts/UI/CrosshairUI.cs` — swaps between
two sprites based on whether `Interactor.CurrentTarget` is non-null (i.e.
looking at something with `E`-to-interact available).

1. Select **both** `Crosshair - Dot.png` and `Crosshair - Interact.png` in
   the Project window. In the Inspector, confirm/set:
   - **Texture Type**: Sprite (2D and UI)
   - **Filter Mode**: Point (no filter) if these are meant to stay crisp
     pixel-perfect at their native size; Bilinear if they're smooth
     vector-style art rather than pixel art. (Point is usually right for
     small precise crosshair dots either way — try it, switch if it looks
     wrong.)
   - Click **Apply** if you changed anything.
2. In the Hierarchy, inside the existing **Canvas**, right-click → **UI →
   Image**. Rename it `Crosshair`.
3. On its **Rect Transform**: set the **Anchor Preset** to center
   (Alt+Shift+click the center preset), **Pos X/Y** to `0, 0` — this pins
   it to the exact center of the screen regardless of resolution.
4. Set its **Width/Height** to match the crosshair PNGs' actual pixel
   size (check the Inspector when the PNG is selected) so it doesn't get
   stretched.
5. Drag `Crosshair - Dot.png` into this Image component's **Source
   Image** field as the default state.
6. Add component **Crosshair UI** (`RobEveryone.UI`) to the `Crosshair`
   object (or to `UIManager`, either works — just needs a reference to
   this Image).
7. Wire the `Crosshair UI` component's fields:
   - `Interactor` → drag the `Player` object (for its `Interactor`
     component)
   - `Crosshair Image` → drag the `Crosshair` Image object itself
   - `Default Sprite` → `Crosshair - Dot.png`
   - `Interact Sprite` → `Crosshair - Interact.png`
8. Press Play. Crosshair should show the dot normally, and swap to the
   interact sprite when looking at `Item_Watch`/any pickup within
   `Interact Range`.

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

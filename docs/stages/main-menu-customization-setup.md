# Main menu + player customization — Editor setup

**Visual/animation design (sliding panels, title pulse, layout, asset
specs) is in [main-menu-visual-design.md](main-menu-visual-design.md)** —
this doc is the Unity wiring.

Detailed, click-by-click version. This is out of the normal build order
(menus/cosmetics were "Tier 3, lower urgency" in [ui-design.md](ui-design.md))
but self-contained, so it doesn't block or get blocked by Stage 3's
remaining work.

**Button style: brackets, not filled buttons.** You're using the pixel
kit's `Button_L`/`Button_R` pieces as small end-caps that sit on either
side of the label text — `[ Play ]` where `[` and `]` are the cap
graphics — rather than one stretched background image behind the text.
Because you need this exact 3-piece construction 7 times (every button in
the menu), Part 5 builds it **once as a reusable Prefab**, and every
button after that is just "drag a copy, rename it, change its label."

Two real assets already exist and get used below:
`Assets/Art/UI/bg_neighborhood.png` (3840×2160) and
`Assets/Art/UI/Title_Wordmark.png` (2400×700). Everything else
(`button_startgame.png`, `preview_frame.png`, `panel_backdrop.png`) is
still pending per main-menu-visual-design.md's checklist — those slot in
later without changing anything structural here.

## The end state (read this first)

```
MainMenu (scene)
├── Canvas
│   ├── Background            (bg_neighborhood.png, behind everything)
│   ├── MainMenuPanel        (active by default)
│   │   ├── TitleWordmark     (Title_Wordmark.png)
│   │   └── ButtonList
│   │       ├── PlayButton         (MenuButton prefab instance)
│   │       ├── CustomizeButton    (MenuButton prefab instance)
│   │       ├── SettingsButton     (MenuButton prefab instance)
│   │       └── QuitButton         (MenuButton prefab instance)
│   ├── CustomizePanel       (starts inactive)
│   │   ├── BackButton             (MenuButton prefab instance)
│   │   ├── PreviousButton         (MenuButton prefab instance)
│   │   ├── NextButton             (MenuButton prefab instance)
│   │   └── SwatchContainer
│   │       └── SwatchButtonTemplate   (starts inactive)
│   └── SettingsPanel        (starts inactive)
│       └── BackButton             (MenuButton prefab instance)
├── EventSystem               (auto-created with Canvas)
├── MenuManager                (empty GameObject, holds scripts)
├── PreviewSpawnPoint          (3D world position, not UI)
└── Main Camera + a Directional Light
```

`Assets/Prefabs/UI/MenuButton.prefab` **(new this session)** — the
reusable bracket-button. Everywhere the old version of this doc said
"add a Button - TextMeshPro," it now means "drag a copy of this prefab."

Scripts involved (all already exist from last session, nothing new to
write):

- `Assets/Scripts/UI/MenuActions.cs` — Play/Quit/panel-switching logic.
- `Assets/Scripts/UI/CustomizationUI.cs` — skin cycling + swatch picker +
  live preview.
- `Assets/Scripts/Customization/PlayerColorPalette.cs` /
  `PlayerColorizer.cs` / `PlayerCosmeticSelection.cs` — palette data,
  recoloring, save/load.

---

## Part 1 — Create the scene

1. **File → New Scene**. Pick the **Basic** template → **Create**.
2. **File → Save As...** → `Assets/Scenes/` → filename `MainMenu` →
   **Save**.

## Part 2 — Add the scene to Build Settings

1. **File → Build Settings...**
2. With `MainMenu` open/active, click **Add Open Scenes**.
3. Open `SampleScene`, click **Add Open Scenes** again.
4. Drag `MainMenu` to the **top** of the list (index 0) — Unity launches
   whichever scene is listed first.
5. Re-open `MainMenu` before continuing (Build Settings switches your
   active scene around). Close the window.

## Part 3 — Add the Canvas

1. Right-click empty Hierarchy space → **UI → Canvas**. This
   auto-creates an `EventSystem` too — required for buttons to be
   clickable, don't delete it.
2. Select `Canvas` → **Canvas Scaler** component:
   - **UI Scale Mode** → **Scale With Screen Size**
   - **Reference Resolution** → `X: 3840`, `Y: 2160`
   - **Screen Match Mode** → **Match Width Or Height**, **Match** → `0.5`

## Part 4 — Add the Menu Actions script

1. Right-click empty Hierarchy space → **Create Empty**. Rename it
   `MenuManager`.
2. **Add Component** → search `Menu Actions` → add it. Leave its fields
   empty for now (wired in Part 11).

---

## Part 5 — Build the MenuButton prefab (do this once)

### 5a. Import the cap sprites correctly

`Button_L`/`Button_R` are already sliced inside
`Assets/Art/UI/Pixel UI pack 3/07.png` at **8×26px** — tiny, meant to be
scaled up as pixel art (keep **Filter Mode: Point** on that sheet, per
the earlier pixel-kit import steps). You'll size them up in the Rect
Transform in step 5c, not by changing the source import.

### 5b. Build the hierarchy

1. In the Hierarchy, right-click `Canvas` → **Create Empty**. Rename it
   `MenuButton`. (Building it under Canvas temporarily so it previews at
   the right scale — it becomes a prefab and gets removed from the scene
   in step 5f.)
2. On `MenuButton`'s **Rect Transform**, nothing special yet — its size
   will come from step 5d's Content Size Fitter.
3. **Add Component → Image**. This is the invisible click-catcher for
   the whole bracket, not a visible background:
   - **Color** → set **Alpha (A)** to `0` (fully transparent). Leave
     **Raycast Target** checked — this is what makes clicks register
     across the whole button, including the gap between the caps and the
     text, not just directly on top of the cap graphics.
4. **Add Component → Button**. Its **Target Graphic** should
   auto-fill with the Image from step 3 — leave it for now, you'll
   repoint it in step 5e for a better hover effect.

### 5c. Add the two caps and the label

1. Right-click `MenuButton` → **UI → Image**. Rename it `CapLeft`.
   - **Source Image** → `Button_L` (from the sliced sheet).
   - **Image Type** → Simple.
   - Rect Transform **Width/Height** → set manually (don't use Set Native
     Size — 8×26 is too small). Start around `46 x 150`, keeping the
     source's ~1:3.25 width:height ratio. Adjust once you see it next to
     real text.
   - Uncheck **Raycast Target** on this Image (the parent's Image already
     handles clicks — this avoids two overlapping raycast targets).
2. Right-click `MenuButton` → **UI → Text - TextMeshPro**. Rename it
   `Label`. Set placeholder text (`Play`, etc. — you'll change this per
   button copy later). Uncheck its **Raycast Target** too, same reason.
3. Right-click `MenuButton` → **UI → Image**. Rename it `CapRight`.
   - **Source Image** → `Button_R`.
   - Same size as `CapLeft`.
   - **Rect Transform → Scale → X** → `-1` (mirrors it horizontally,
     since `Button_R` in the sheet may already be a distinct mirrored
     piece — check by eye once placed; only flip if it looks
     backwards/wrong without this).
   - Uncheck **Raycast Target**.
4. Order in the Hierarchy should be `CapLeft`, `Label`, `CapRight` (top
   to bottom) — this is the left-to-right visual order once the layout
   group (next step) arranges them.

### 5d. Auto-arrange with a Layout Group

1. Select `MenuButton` itself (the parent). **Add Component → Horizontal
   Layout Group**:
   - **Child Alignment** → Middle Center
   - **Spacing** → `16` (gap between each cap and the text — adjust to
     taste)
   - **Child Force Expand** → both unchecked
2. **Add Component → Content Size Fitter**:
   - **Horizontal Fit** → Preferred Size
   - **Vertical Fit** → Preferred Size
   (This makes the whole button — the invisible click-catcher included —
   automatically resize to fit however wide the label text is, so
   `Customization` and `Back` both get correctly fitted brackets without
   manual sizing per button.)

### 5e. Hover feedback (optional but easy)

On the **Button** component (added in step 5b):

- **Transition** → Color Tint (default)
- **Target Graphic** → drag `Label` (the TMP text) instead of the
  default Image. Since the click-catcher Image is invisible (alpha 0),
  tinting it does nothing visible — tinting the text instead gives a
  simple "brightens on hover" effect with zero extra art.

### 5f. Turn it into a Prefab

1. In the Project window, create the folder `Assets/Prefabs/UI/` if it
   doesn't exist.
2. Drag `MenuButton` from the Hierarchy into that folder — this creates
   the Prefab asset and turns the Hierarchy instance blue/linked.
3. Delete the `MenuButton` instance from the Hierarchy now (right-click →
   Delete) — it was only there to build/preview it. You'll drag fresh
   copies from the Project window wherever you need a button, starting
   in Part 7.

---

## Part 6 — Add the background and title art

1. Right-click `Canvas` → **UI → Image**. Rename it `Background`.
   - **Source Image** → `bg_neighborhood.png`.
   - **Rect Transform** → Anchor Preset: stretch-stretch (the "full
     rectangle" option, holding Alt+Shift when clicking it), then set all
     four (Left/Top/Right/Bottom) to `0` — fills the entire Canvas.
   - Drag `Background` to be the **first** child under `Canvas` in the
     Hierarchy (topmost in the list) — UI draws later siblings on top of
     earlier ones, so this needs to render behind every panel.
2. You'll add `TitleWordmark` as a child of `MainMenuPanel` in Part 7
   (needs `MainMenuPanel` to exist first).

---

## Part 7 — Build MainMenuPanel

1. Right-click `Canvas` → **UI → Panel**. Rename it `MainMenuPanel`. On
   its **Image** component, set **Color alpha to 0** (or delete the Image
   component entirely) — you don't want the default gray Panel fill
   covering your background art; this object is just a container now.
2. Right-click `MainMenuPanel` → **UI → Image**. Rename it
   `TitleWordmark`.
   - **Source Image** → `Title_Wordmark.png`, click **Set Native Size**
     (this one's fine to use at its authored resolution, then scale down
     if needed).
   - Position per main-menu-visual-design.md's composition spec: Anchor
     Preset top-center, roughly centered in the `x: 620–3220, y: 150–550`
     region.
3. Right-click `MainMenuPanel` → **Create Empty**. Rename it
   `ButtonList`.
   - **Add Component → Vertical Layout Group**: Child Alignment → Middle
     Center, Spacing → `20`, Child Force Expand unchecked.
   - **Add Component → Content Size Fitter**: Vertical Fit → Preferred
     Size.
   - Rect Transform: Anchor Preset middle-center. Per the composition
     spec, the button stack sits in the **left half** of the screen (not
     dead-center) — set **Pos X** to roughly `-900` to shift it left,
     leaving the right side clear for the character preview.
4. Drag a copy of `MenuButton` (from `Assets/Prefabs/UI/`) into
   `ButtonList`. Rename the instance `PlayButton`. Expand it, select its
   `Label` child, change the text to `Play`.
5. Repeat for `CustomizeButton` ("Customize"), `SettingsButton`
   ("Settings"), `QuitButton` ("Quit") — drag 3 more copies of the
   prefab into `ButtonList`, rename each, edit each one's `Label` text.
   The Vertical Layout Group stacks them in Hierarchy order automatically
   — drag to reorder if needed.

Press Play — you should see the neighborhood background, the title
wordmark, and 4 bracket-style buttons stacked on the left, each auto-
sized to its own label text.

## Part 8 — Build CustomizePanel

1. Right-click `Canvas` → **UI → Panel**. Rename it `CustomizePanel`.
   Same as `MainMenuPanel`, zero out its Image's alpha (or remove it) so
   the shared background shows through.
2. Drag a `MenuButton` copy in, rename it `BackButton`, label `Back`,
   position top-left.
3. Drag two more copies: `PreviousButton` (label `<`), `NextButton`
   (label `>`), positioned flanking where the character preview sits.
4. Right-click `CustomizePanel` → **Create Empty**. Rename it
   `SwatchContainer`.
   - **Add Component → Horizontal Layout Group**: Spacing `10`, Child
     Alignment Middle Center.
   - Position near the bottom of the panel (Anchor Preset bottom-center).
5. Inside `SwatchContainer`, right-click → **UI → Button** (plain Button,
   not the MenuButton prefab — swatches are just solid color squares, no
   bracket/label needed). Rename it `SwatchButtonTemplate`.
   - **Delete its child `Text (Legacy)` object** — Unity's default Button
     always creates one saying "Button," and it's not wanted here (a
     swatch is just a colored square, no label). Skipping this step makes
     every swatch show as a wall of "Button" text instead of a square.
   - Its **Image** → **Color** is overwritten per-swatch at runtime by
     `CustomizationUI`, so its color here doesn't matter — but its
     **Image Type** does: set it to **Simple**, not the default
     **Sliced**. At a small size like 60×60, Sliced 9-slicing on Unity's
     built-in `UISprite` collapses the stretchable middle down to nothing,
     leaving only 4 disconnected corner fragments instead of a solid
     square.
   - Width/Height → `60 x 60`.
   - **Disable it** (uncheck the checkbox next to its name at the top of
     the Inspector) — `CustomizationUI` instantiates copies of this;
     the template itself must stay hidden.
6. On `SwatchContainer`'s **Horizontal Layout Group**, uncheck **Control
   Child Size** (both Width and Height). This defaults to *checked* when
   the component is added, which overrides each swatch's manually-set
   60×60 size with a computed one instead — leave it unchecked so your
   size actually sticks.
7. `CustomizePanel` should start **hidden**: uncheck its own active
   checkbox.

## Part 9 — Add the 3D preview spot

Lives in actual 3D space, not inside the Canvas.

1. Right-click empty Hierarchy space → **Create Empty**. Rename it
   `PreviewSpawnPoint`.
2. Get it in front of the camera **precisely**, without guessing
   coordinates:
   - Select **Main Camera** → right-click it (or **GameObject** menu) →
     **Align View to Selected** — snaps your Scene view to match the
     Main Camera's exact position/facing.
   - Without touching Scene view navigation, select `PreviewSpawnPoint`
     instead → **GameObject** menu → **Move To View**
     (`Ctrl+Alt+F` / `Cmd+Option+F`) — moves it to wherever the Scene
     view is now centered, which is directly in the Main Camera's line of
     sight.
   - Adjust distance afterward (nudge along whichever axis the camera
     actually faces) if the preview ends up too close/far once you see it
     in Play mode.
3. Optional: add a flattened Cube under it as a simple pedestal so the
   character doesn't look like it's floating.
4. Confirm there's a **Directional Light** in the scene so the preview
   isn't pitch black.

## Part 10 — Build SettingsPanel (stub)

Settings content isn't designed yet — just a placeholder shell.

1. Right-click `Canvas` → **UI → Panel**. Rename it `SettingsPanel`.
   Zero out its Image alpha like the others.
2. Drag a `MenuButton` copy in, rename `BackButton`, label `Back`,
   position top-left.
3. Right-click `SettingsPanel` → **UI → Text - TextMeshPro**. Text:
   `Settings coming soon`.
4. Start it hidden (uncheck its active checkbox).

---

## Part 11 — Wire everything together

### 11a. MenuManager (MenuActions component)

Select `MenuManager` → **Menu Actions** component:

- `Main Menu Panel` → `MainMenuPanel`
- `Customize Panel` → `CustomizePanel`
- `Settings Panel` → `SettingsPanel`

### 11b. Button OnClick() events

For each row: select the button → **Button** component → **On Click ()**
→ **+** → drag `MenuManager` into the **Object** slot → function
dropdown → pick the method.

| Button | Function |
|---|---|
| `PlayButton` | `MenuActions → PlayGame ()` |
| `QuitButton` | `MenuActions → QuitGame ()` |
| `CustomizeButton` | `MenuActions → OpenCustomize ()` |
| `SettingsButton` | `MenuActions → OpenSettings ()` |
| `BackButton` (under `CustomizePanel`) | `MenuActions → CloseCustomize ()` |
| `BackButton` (under `SettingsPanel`) | `MenuActions → CloseSettings ()` |

### 11c. Customization UI component

Add component **Customization UI** to **`CustomizePanel`** — not
`MenuManager`. This matters: `CustomizationUI`'s setup logic (spawning
the preview, building swatches) runs in `OnEnable()`, which needs to fire
each time the Customize screen actually opens. `MenuManager` is active
for the entire scene lifetime, so putting it there means that logic only
ever runs once, at scene load, while `CustomizePanel` (and everything
nested inside it, including `SwatchContainer`) is still hidden — and a
UI element instantiated while its ancestor is inactive never properly
initializes. Putting the component directly on `CustomizePanel` means
`OnEnable()` fires exactly when the panel becomes visible, which is what
you actually want.

Wire:

- `Skin Roster` → a `PlayerSkinRoster` asset (Project window → right-click
  → **Create → Rob Everyone → Player Skin Roster** if you haven't made one
  yet) holding its own `Skin Prefabs` list — currently all 52 files in
  `Assets/Art/Characters/Quaternius-UltimateAnimatedCharacterPack/FBX/`,
  `BaseCharacter` included (a few looked like accessory props or
  non-humanoid animals at a glance, but they share the same rig and
  ragdoll correctly, so there was no reason to exclude them — see
  [stage3j-traffic-hazard.md](stage3j-traffic-hazard.md)).
  Pulled out into its own shared asset (was a
  direct array on this component) so gameplay's `PlayerSkinSpawner` reads
  the exact same list instead of keeping a second copy in sync by hand —
  see [stage3j-traffic-hazard.md](stage3j-traffic-hazard.md)'s ragdoll
  section for why.
- `Palette` → your `PlayerColorPalette` asset (Project window → right-
  click → **Create → Rob Everyone → Player Color Palette** if you
  haven't made one yet)
- `Preview Spawn Point` → `PreviewSpawnPoint`
- `Swatch Container` → `SwatchContainer`
- `Swatch Button Template` → `SwatchButtonTemplate`

**Before testing, check every color in the palette's `Colors` array has
Alpha set to 255 (or `1`)** — a freshly-created array entry defaults to
fully transparent `(0,0,0,0)`, and pasting a 6-digit hex code only sets
R/G/B, never touches Alpha. A palette full of 0-alpha colors will build
correctly-sized, correctly-positioned, correctly-"colored" swatches that
are just 100% invisible — everything about them will look right in the
Inspector except that one easy-to-miss number.

### 11d. Previous/Next buttons

Since `Customization UI` now lives on `CustomizePanel` (not
`MenuManager`), point these there too:

- `PreviousButton` → On Click () → `CustomizePanel` →
  **Customization UI → PreviousSkin ()**
- `NextButton` → same, **NextSkin ()**

## Part 12 — Test

1. Background + title + 4 bracket buttons on `MainMenuPanel`.
2. **Customize** → panel swap, a character model appears at
   `PreviewSpawnPoint`.
3. Click a swatch → model's Body color changes (not Head).
4. **`<`**/**`>`** → cycles skins.
5. **Back** → returns to Main Menu.
6. **Play** → loads `SampleScene`.
7. Stop and re-enter Play, go to Customize — your last pick should
   already be selected (`PlayerPrefs` working).

---

## Troubleshooting

- **Nothing happens when I click a button**: exactly one `EventSystem`
  should exist in the scene.
- **A button's click area feels smaller/offset from what's visible**:
  the click-catcher is the invisible root Image, sized by the Content
  Size Fitter around `CapLeft`+`Label`+`CapRight` — if `CapLeft`/
  `CapRight`'s **Raycast Target** got left checked instead of unchecked
  (step 5c), overlapping raycast targets can cause inconsistent click
  behavior. Double-check both caps have Raycast Target **off**, only the
  root Image has it **on**.
- **Caps look stretched/blurry**: confirm the source sheet
  (`07.png`)'s **Filter Mode** is **Point**, and that you resized
  `CapLeft`/`CapRight` manually rather than via Set Native Size (native
  8×26 is far too small to be usable directly).
- **Button text and caps don't resize together when I change the label**:
  confirm `MenuButton`'s own Content Size Fitter is set to Preferred Size
  on **both** Horizontal and Vertical, and that the Horizontal Layout
  Group's Child Force Expand is **unchecked** (checked would fight the
  Content Size Fitter).
- **Buttons don't stack/center correctly in `ButtonList`**: same
  Vertical Layout Group + Content Size Fitter check as before, middle-
  center anchor.
- **Swatches don't appear at all**: in rough order of likelihood —
  1. `PlayerColorPalette`'s `Colors` array entries have **Alpha at 0**
     (see the callout in 11c) — everything about the swatch will look
     correct in the Inspector (right size, right position, "colored")
     except this one number. This is the most likely cause if you can
     select a swatch clone in Play mode, see reasonable Rect
     Transform/Image values, and it's still invisible.
  2. `SwatchButtonTemplate` still has its default child `Text (Legacy)`
     object (delete it, step 5 of Part 8) — shows as a wall of "Button"
     text instead of colored squares.
  3. `SwatchButtonTemplate`'s **Image Type** is `Sliced` instead of
     `Simple` — at 60×60 this collapses into 4 disconnected corner
     fragments instead of a solid square.
  4. `SwatchContainer`'s Horizontal Layout Group has **Control Child
     Size** still checked, overriding your manual 60×60 size.
  5. `Customization UI` is still on `MenuManager` instead of
     `CustomizePanel` (see 11c) — its setup only ran once at scene load
     while the panel was still hidden.
  6. `PlayerColorPalette`'s `Colors` array is just empty (no entries at
     all), or `SwatchButtonTemplate` was deleted instead of disabled.
- **Model spawns T-posing**: expected — none of the preview skins have an
  Animator wired yet (Stage 3d's pattern). Fine for a static preview.
- **"Play" does nothing / errors**: confirm `SampleScene` is spelled
  exactly right (case-sensitive) in Build Settings and is checked.
- **Background doesn't fully cover the screen, or covers other UI**:
  check `Background`'s anchors are all stretched to `0,0,0,0`, and that
  it's the **first** sibling under `Canvas` (topmost in the Hierarchy
  list — later siblings draw on top).

## Part 12 — Play submenu + slide navigation (unblocks Stage 4 Host/Join)

Builds the "Lobby" screen from main-menu-visual-design.md's nav diagram
(Main → **Play** → Lobby → Customization) — renamed **Play submenu** in
code/Hierarchy to avoid confusion with the separate gameplay `Lobby`
*scene* (Stage 7's post-round shop/ready-up area, an unrelated thing that
happens to share a name). This is the minimal slice: the slide+dim
transition works, Host/Join/Customize/Back buttons live on it. **Not**
included in this pass (deliberately, see `todo.md`): the title's
continuous pulse animation, and Settings' fall-through-frame/fall-from-
sky character animation — Settings stays exactly the flat show/hide it
already is. The character preview also doesn't yet appear on the Play
submenu or Main Menu themselves, only on Customize, same as today — full
persistence across all three panels is part of that later pass, not this
one.

1. In the Hierarchy, duplicate `CustomizePanel` (or build fresh — either
   way you want a full-canvas-stretched RectTransform, same as
   `MainMenuPanel`/`CustomizePanel` already are) and rename it
   `PlayPanel`. Strip out anything Customize-specific you copied; you
   want it empty except for what step 3 adds.
2. On the `Canvas` object (or wherever `MenuActions` already lives), add
   component **`Menu Navigator`** (`Assets/Scripts/UI/MenuNavigator.cs`).
3. Inside `PlayPanel`, add 4 buttons using the existing `MenuButton`
   prefab (same bracket style as everywhere else): **Host**, **Join**,
   **Customization**, **Back**. Add a `TMP_InputField` near the Join
   button for typing an address (optional for now — leave it unassigned
   and `JoinGame()` defaults to `"localhost"`).
4. Wire buttons' `OnClick()`:
   - **Main Menu's "Play" button** (was previously wired to
     `MenuActions.PlayGame` — that method no longer exists, remove the
     stale reference if Unity shows a missing-method warning): call
     **`MenuNavigator.NavigateTo`**, drag `PlayPanel`'s RectTransform
     into the dynamic RectTransform argument slot.
   - **PlayPanel's Host button**: `MenuActions.HostGame`.
   - **PlayPanel's Join button**: `MenuActions.JoinGame`.
   - **PlayPanel's Customization button**: `MenuNavigator.NavigateTo`,
     argument = `CustomizePanel`'s RectTransform.
   - **PlayPanel's Back button**: `MenuNavigator.NavigateBack` (no
     argument — it always returns to whatever's on top of the history
     stack).
   - **CustomizePanel's existing Back button**: change it from whatever
     closed it before (`MenuActions.CloseCustomize`, now removed) to
     **`MenuNavigator.NavigateBack`**.
5. On `MenuNavigator`, drag `MainMenuPanel`'s RectTransform into a quick
   test call to `SetInitial` from somewhere that runs once at startup —
   easiest is adding one line to `MenuActions.Awake()`
   (`GetComponent<MenuNavigator>().SetInitial(mainMenuPanelRectTransform)`)
   if you're comfortable editing that, or ask me to add it as a proper
   field + `Awake()` if not — this makes sure Main Menu starts already
   correctly registered as "current" rather than the navigator finding
   out for the first time on your first click.
6. `MainMenuPanel`'s old direct-to-Customize button (if you had one
   before this Part existed) should be removed or repointed — per the
   design, Customize is only reached via the Play submenu now, not
   directly from Main.

### Test
Click Play — Main should slide left and dim while PlayPanel slides in
from the right. Click Back — reverses. From PlayPanel, click
Customization — same slide, PlayPanel dims behind it. Back from there
returns to PlayPanel (not all the way to Main — confirms the history
stack, not just a single toggle). Confirm Host/Join buttons still call
the right `MenuActions` methods (check the Console for Mirror's own
connection logs when testing against Stage 4's Rest Points).

## Notes / open follow-ups

- No skin **unlocking** yet — every prefab in `Skin Prefabs` is pickable
  immediately (future work, per gameplay-design.md's meta-progression).
- This preview system isn't wired to the actual multiplayer `Player`
  prefab yet — Stage 4/5 code exists but hasn't been playtested end to
  end. Future work: spawn each networked player's chosen skin/color at
  spawn time using `PlayerCosmeticSelection` + `PlayerColorizer` (this is
  actually already how `PlayerSkinSpawner` works as of Stage 4's code —
  just needs confirming in an actual playtest).
- Still pending from main-menu-visual-design.md's asset checklist:
  `button_startgame.png`, `preview_frame.png` (needs the fall-through
  floor detail), and the optional `panel_backdrop.png`. None of these
  change anything structural here — they slot into the existing
  `MenuButton` prefab (for Start Game, likely as a resized/recolored
  variant or a second prefab) and the preview area whenever they're
  ready.
- Still not built: the title's continuous pulse animation, and Settings'
  fall-through-frame/fall-from-sky character animation — both explicitly
  deferred out of Part 12 above, see that Part's intro for why.
